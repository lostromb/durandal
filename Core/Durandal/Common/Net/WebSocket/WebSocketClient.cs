using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.IO;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.MathExt;
using Durandal.Common.Collections;
using Durandal.Common.Logger;

namespace Durandal.Common.Net.WebSocket
{
    /// <summary>
    /// An implementation of <see cref="IWebSocket"/> operating over an abstract <see cref="ISocket"/>.
    /// </summary>
    public class WebSocketClient : IWebSocket
    {
        private static readonly TimeSpan PING_INTERVAL = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The size of internal buffers used to stage headers + payload data for read and write operations.
        /// </summary>
        private const int INTERNAL_BUFFER_SIZE = BufferPool<byte>.DEFAULT_BUFFER_SIZE;

        /// <summary>
        /// The maximum size of the variable-length websocket header, without extensions.
        /// </summary>
        private const int MAX_HEADER_SIZE = 14;

        /// <summary>
        /// The maximum number of bytes that can fit in the payload of a single frame
        /// whose combined payload + header fits into the internal buffer size.
        /// </summary>
        private const int FRAGMENTATION_THRESHOLD = INTERNAL_BUFFER_SIZE - MAX_HEADER_SIZE;

        private readonly IRandom _secureRandom;
        private readonly WeakPointer<ISocket> _socket;
        private readonly bool _ownsSocket;
        private readonly ILogger _logger;
        private readonly bool _isServer;
        private readonly AsyncLockSlim _writeLock = new AsyncLockSlim();
        private readonly CancellationTokenSource _pingThreadCancel;
        private bool _writeClosed = false;
        private bool _readClosed = false;
        private int _disposed = 0;

        /// <summary>
        /// Creates a new websocket connection over an already existing socket.
        /// </summary>
        /// <param name="innerSocket">The socket to communicate over.</param>
        /// <param name="ownsSocket">Whether this object should take ownership of disposal of the socket.</param>
        /// <param name="logger">A logger to use for the lifetime of the connection.</param>
        /// <param name="realTime">A definition of real time, used for ping thread timing.</param>
        /// <param name="isServer">Whether this is the "server" endpoint of the socket.</param>
        /// <param name="secureRandom">A cryptographic random number generator.</param>
        /// <param name="subProtocol">The subprotocol that was established during the initial handshake, or null if not present.</param>
        public WebSocketClient(
            WeakPointer<ISocket> innerSocket,
            bool ownsSocket,
            ILogger logger,
            IRealTimeProvider realTime,
            bool isServer,
            IRandom secureRandom,
            string subProtocol = null)
        {
            _socket = innerSocket.AssertNonNull(nameof(innerSocket));
            _secureRandom = secureRandom.AssertNonNull(nameof(secureRandom));
            _logger = logger.AssertNonNull(nameof(logger));
            _ownsSocket = ownsSocket;
            realTime.AssertNonNull(nameof(realTime));
            _isServer = isServer;
            SubProtocol = subProtocol;
            State = WebSocketState.Open;
            _pingThreadCancel = new CancellationTokenSource();
            IRealTimeProvider pingThreadTime = realTime.Fork("WebSocketPing");
            RunPingThread(pingThreadTime, _pingThreadCancel.Token).Forget(_logger.Clone("WebSocketPing"));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~WebSocketClient()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public string SubProtocol { get; private set; }

        /// <inheritdoc />
        public WebSocketState State { get; private set; }

        /// <inheritdoc />
        public WebSocketCloseReason? RemoteCloseReason { get; private set; }

        /// <inheritdoc />
        public string RemoteCloseMessage { get; private set; }

        /// <inheritdoc />
        public async Task CloseWrite(
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            WebSocketCloseReason? status = null,
            string debugCloseMessage = null)
        {
            // This is just an optimistic check. If the write is definitely already closed,
            // don't do anything else.
            // It's possible that two separate CloseWrite() operations can happen simultaneously.
            // In that case, they will hit the mutex just as they're about to send the actual frame,
            // and that will ensure only one operation actually takes effect.
            if (_writeClosed)
            {
                return;
            }

            _pingThreadCancel.Cancel();

            // Allocate a scratch buffer big enough to generously encode the entire debug string, if present.
            int outputBufSize = MAX_HEADER_SIZE;
            if (status.HasValue)
            {
                outputBufSize += 2;
            }

            if (debugCloseMessage != null)
            {
                outputBufSize += Encoding.UTF8.GetMaxByteCount(debugCloseMessage.Length);
            }

            using (PooledBuffer<byte> outputBuf = BufferPool<byte>.Rent(outputBufSize))
            {
                // Prepare the output frame
                int payloadLength = 0;
                if (status.HasValue)
                {
                    payloadLength += 2;
                    BinaryHelpers.UInt16ToByteArrayBigEndian((ushort)status.Value, outputBuf.Buffer, 0);
                }

                if (debugCloseMessage != null)
                {
                    payloadLength += StringUtils.UTF8_WITHOUT_BOM.GetBytes(
                        debugCloseMessage,
                        0,
                        debugCloseMessage.Length,
                        outputBuf.Buffer,
                        payloadLength);
                }

                // Send the close frame. Don't touch read closed until the remote endpoint actually sends its own close frame.
                // This is because there might still be valid incoming messages on the wire.
                await WriteOutgoingFrameInternal(
                    new ArraySegment<byte>(outputBuf.Buffer, 0, payloadLength),
                    outputBuf,
                    WebSocketOpcode.CloseConnection,
                    true,
                    cancelToken,
                    realTime).ConfigureAwait(false);

                // And close the write end of the socket (making sure to read open)
                await _socket.Value.Disconnect(cancelToken, realTime, NetworkDuplex.Write, allowLinger: false).ConfigureAwait(false);
            }
        }

        public async Task WaitForGracefulClose(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            while (!_readClosed)
            {
                (await ReceiveAsBufferAsync(cancelToken, realTime).ConfigureAwait(false)).Dispose();
            }
        }

        /// <inheritdoc />
        public async ValueTask<bool> SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Preemptive check if the socket has already closed.
            // Don't throw an exception because it's possible that some other error closed the socket while the
            // client was in the middle of a regular send loop.
            if (_writeClosed)
            {
                return false;
            }

            // Rent a scratch buffer big enough to hold the largest possible fragment
            using (PooledBuffer<byte> outputBuf = BufferPool<byte>.Rent(FastMath.Min(INTERNAL_BUFFER_SIZE, buffer.Count + MAX_HEADER_SIZE)))
            {
                bool first = true;
                int inputBytesCopied = 0;
                while (!_writeClosed && inputBytesCopied < buffer.Count)
                {
                    // Break the message into multiple frames if needed.
                    int thisFramePayloadLength = Math.Min(FRAGMENTATION_THRESHOLD, buffer.Count - inputBytesCopied);
                    bool endOfMessage = inputBytesCopied + thisFramePayloadLength == buffer.Count;
                    WebSocketOpcode opcode = first ? ConvertMessageTypeToOpcode(messageType) : WebSocketOpcode.Continuation;
                    await WriteOutgoingFrameInternal(
                        new ArraySegment<byte>(buffer.Array, buffer.Offset + inputBytesCopied, thisFramePayloadLength),
                        outputBuf,
                        opcode,
                        endOfMessage,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    inputBytesCopied += thisFramePayloadLength;
                    first = false;
                }
            }

            return !_writeClosed;
        }

        /// <inheritdoc />
        public async ValueTask<bool> SendAsync(NonRealTimeStream stream, WebSocketMessageType messageType, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Preemptive check if the socket has already closed.
            // Don't throw an exception because it's possible that some other error closed the socket while the
            // client was in the middle of a regular send loop.
            if (_writeClosed)
            {
                return false;
            }

            // Rent a scratch buffer big enough to hold the largest possible fragment
            using (PooledBuffer<byte> outputBuf = BufferPool<byte>.Rent(INTERNAL_BUFFER_SIZE))
            {
                bool first = true;
                int bytesReadFromStream = 1;
                while (!_writeClosed && bytesReadFromStream > 0)
                {
                    // Read from stream
                    bytesReadFromStream = await stream.ReadAsync(outputBuf.Buffer, 0, FRAGMENTATION_THRESHOLD, cancelToken, realTime).ConfigureAwait(false);
                    ArraySegment<byte> payloadData;
                    bool endOfMessage;

                    if (bytesReadFromStream > 0)
                    {
                        // Got some data. Send it as a fragment with FIN flag always set to false.
                        payloadData = new ArraySegment<byte>(outputBuf.Buffer, 0, bytesReadFromStream);
                        endOfMessage = false;
                    }
                    else
                    {
                        // End of stream.
                        // With stream semantics how they are, we have to terminate the message with a separate empty frame with FIN flag set.
                        payloadData = new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY, 0, 0);
                        endOfMessage = true;
                    }

                    WebSocketOpcode opcode = first ? ConvertMessageTypeToOpcode(messageType) : WebSocketOpcode.Continuation;
                    await WriteOutgoingFrameInternal(
                        payloadData,
                        outputBuf,
                        opcode,
                        endOfMessage,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    first = false;
                }
            }

            return !_writeClosed;
        }

        /// <inheritdoc />
        public async ValueTask<WebSocketBufferResult> ReceiveAsBufferAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            PooledBuffer<byte> receiveBuf = BufferPool<byte>.Rent(INTERNAL_BUFFER_SIZE);
            RecyclableMemoryStream receiveStream = null;
            try
            {
                WebSocketMessageType messageType = WebSocketMessageType.Unknown;
                bool firstFrame = true;
                bool expectMoreFrames = true;
                while (expectMoreFrames)
                {
                    if (_readClosed)
                    {
                        return new WebSocketBufferResult(RemoteCloseReason.GetValueOrDefault(WebSocketCloseReason.Empty), RemoteCloseMessage);
                    }

                    // First, read the frame header. This method will advance the socket to just the beginning of the payload.
                    PartialFrameInfo frameInfo = await ReadIncomingFrameHeader(_socket, _logger, receiveBuf, cancelToken, realTime).ConfigureAwait(false);

                    if (frameInfo.Error.HasValue)
                    {
                        // Usually this means the socket failed to read any data from the header, or the header
                        // was invalid. Abort connection.
                        CloseRead(frameInfo.Error.Value, null);
                        return new WebSocketBufferResult(frameInfo.Error.Value, null);
                    }
                    else if (frameInfo.Opcode != WebSocketOpcode.TextFrame &&
                        frameInfo.Opcode != WebSocketOpcode.BinaryFrame &&
                        frameInfo.Opcode != WebSocketOpcode.Continuation)
                    {
                        // Is it a control frame? Handle it separately.
                        await HandleIncomingControlFrame(frameInfo, cancelToken, realTime).ConfigureAwait(false);
                        expectMoreFrames = true;
                        continue;
                    }
                    else if (firstFrame)
                    {
                        // Message type is only sent on the first frame, otherwise it must be continuation
                        if (frameInfo.Opcode == WebSocketOpcode.Continuation)
                        {
                            await CloseWrite(cancelToken, realTime, WebSocketCloseReason.ProtocolError, "Received continuation opcode on first fragment").ConfigureAwait(false);
                            return new WebSocketBufferResult(WebSocketCloseReason.ProtocolError, "Remote peer sent continuation opcode on first fragment");
                        }

                        messageType = ConvertOpcodeTypeToMessageType(frameInfo.Opcode);
                    }
                    else if (!firstFrame)
                    {
                        // The opcode MUST be continuation, otherwise it's a protocol error
                        if (frameInfo.Opcode != WebSocketOpcode.Continuation)
                        {
                            await CloseWrite(cancelToken, realTime, WebSocketCloseReason.ProtocolError, "Received non-continuation frame where one was expected").ConfigureAwait(false);
                            return new WebSocketBufferResult(WebSocketCloseReason.ProtocolError, "Remote peer sent non-continuation frame where one was expected");
                        }
                    }

                    if (frameInfo.FlagMask && !_isServer)
                    {
                        // Servers MUST NOT send masked frames.
                        await CloseWrite(cancelToken, realTime, WebSocketCloseReason.ProtocolError, "Server sent a masked frame").ConfigureAwait(false);
                        return new WebSocketBufferResult(WebSocketCloseReason.ProtocolError, "Server sent a masked frame");
                    }
                    else if (!frameInfo.FlagMask && _isServer)
                    {
                        // Client MUST send masked frames.
                        await CloseWrite(cancelToken, realTime, WebSocketCloseReason.ProtocolError, "Client sent an unmasked frame").ConfigureAwait(false);
                        return new WebSocketBufferResult(WebSocketCloseReason.ProtocolError, "Client sent an unmasked frame");
                    }

                    if (firstFrame)
                    {
                        // Does the whole message fit into one buffer?
                        if (frameInfo.PayloadLength <= FRAGMENTATION_THRESHOLD && frameInfo.FlagFin)
                        {
                            // Awesome. We can just return our scratch buffer with some modification.
                            int totalPayloadSize = (int)frameInfo.PayloadLength;
                            if (totalPayloadSize > 0)
                            {
                                int bytesRead = await _socket.Value.ReadAsync(receiveBuf.Buffer, frameInfo.HeaderLength, totalPayloadSize, cancelToken, realTime).ConfigureAwait(false);
                                if (bytesRead < frameInfo.PayloadLength)
                                {
                                    // The socket closed unexpectedly.
                                    CloseRead(frameInfo.Error.Value, "Socket read operation was interrupted");
                                    return new WebSocketBufferResult(WebSocketCloseReason.ConnectionInterrupted, null);
                                }

                                if (frameInfo.FlagMask)
                                {
                                    MaskOrUnmaskData(
                                        new ArraySegment<byte>(receiveBuf.Buffer, frameInfo.HeaderLength - 4, 4),
                                        new ArraySegment<byte>(receiveBuf.Buffer, frameInfo.HeaderLength, totalPayloadSize));
                                }

                                ArrayExtensions.MemMove(receiveBuf.Buffer, frameInfo.HeaderLength, 0, totalPayloadSize);
                            }

                            // Shrink scratch buffer and nullify references to it so it won't be disposed at the end of this method as normal.
                            receiveBuf.Shrink(totalPayloadSize);
                            WebSocketBufferResult returnVal = new WebSocketBufferResult(messageType, receiveBuf);
                            receiveBuf = null;
                            return returnVal;
                        }
                        else
                        {
                            // The message will be too big for a single buffer. Make a stream to hold it.
                            receiveStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default);
                        }
                    }

                    // Now loop and read the entire payload, unmasking each frame if needed, and copying to our temporary buffer
                    long bytesReadFromThisFrame = 0;
                    while (bytesReadFromThisFrame < frameInfo.PayloadLength)
                    {
                        int maxCanReadFromThisFrame = (int)Math.Min((long)receiveBuf.Length - frameInfo.HeaderLength, frameInfo.PayloadLength - bytesReadFromThisFrame);
                        int bytesRead = await _socket.Value.ReadAnyAsync(receiveBuf.Buffer, frameInfo.HeaderLength, maxCanReadFromThisFrame, cancelToken, realTime).ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            // The socket closed unexpectedly. Make sure to report any partial data we wrote to the output.
                            CloseRead(WebSocketCloseReason.ConnectionInterrupted, "Socket read operation was interrupted");
                            return new WebSocketBufferResult(WebSocketCloseReason.ConnectionInterrupted, null);
                        }

                        if (frameInfo.FlagMask)
                        {
                            MaskOrUnmaskData(
                                new ArraySegment<byte>(receiveBuf.Buffer, frameInfo.HeaderLength - 4, 4),
                                new ArraySegment<byte>(receiveBuf.Buffer, frameInfo.HeaderLength, bytesRead),
                                initialMaskOffset: (int)(bytesReadFromThisFrame % 4));
                        }

                        receiveStream.Write(receiveBuf.Buffer, frameInfo.HeaderLength, bytesRead);
                        bytesReadFromThisFrame += bytesRead;
                    }

                    firstFrame = false;
                    expectMoreFrames = !frameInfo.FlagFin;
                }

                // We can only reach this point if the message was too big to fit in a single buffer.
                // So use the stream result.
                return new WebSocketBufferResult(messageType, receiveStream.ToPooledBuffer());
            }
            finally
            {
                receiveBuf?.Dispose();
                receiveStream?.Dispose();
            }
        }

        /// <inheritdoc />
        public async ValueTask<WebSocketStreamResult> ReceiveAsStreamAsync(NonRealTimeStream destinationStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            using (PooledBuffer<byte> receiveBuf = BufferPool<byte>.Rent(INTERNAL_BUFFER_SIZE))
            {
                WebSocketMessageType messageType = WebSocketMessageType.Unknown;
                bool firstFrame = true;
                long bytesWrittenToOutput = 0;
                bool expectMoreFrames = true;
                while (expectMoreFrames)
                {
                    if (_readClosed)
                    {
                        return new WebSocketStreamResult(RemoteCloseReason.GetValueOrDefault(WebSocketCloseReason.Empty), RemoteCloseMessage);
                    }

                    // First, read the frame header. This method will advance the socket to just the beginning of the payload.
                    PartialFrameInfo frameInfo = await ReadIncomingFrameHeader(_socket, _logger, receiveBuf, cancelToken, realTime).ConfigureAwait(false);

                    if (frameInfo.Error.HasValue)
                    {
                        // Usually this means the socket failed to read any data from the header, or the header
                        // was invalid. Abort connection.
                        CloseRead(frameInfo.Error.Value, null);
                        return new WebSocketStreamResult(frameInfo.Error.Value, null);
                    }
                    else if (frameInfo.Opcode != WebSocketOpcode.TextFrame &&
                        frameInfo.Opcode != WebSocketOpcode.BinaryFrame &&
                        frameInfo.Opcode != WebSocketOpcode.Continuation)
                    {
                        // Is it a control frame? Handle it separately.
                        await HandleIncomingControlFrame(frameInfo, cancelToken, realTime).ConfigureAwait(false);
                        expectMoreFrames = true;
                        continue;
                    }
                    else if (firstFrame)
                    {
                        // Message type is only sent on the first frame, otherwise it must be continuation
                        if (frameInfo.Opcode == WebSocketOpcode.Continuation)
                        {
                            await CloseWrite(cancelToken, realTime, WebSocketCloseReason.ProtocolError, "Received continuation opcode on first fragment").ConfigureAwait(false);
                            return new WebSocketStreamResult(WebSocketCloseReason.ProtocolError, "Remote peer sent continuation opcode on first fragment");
                        }

                        messageType = ConvertOpcodeTypeToMessageType(frameInfo.Opcode);
                    }
                    else if (!firstFrame)
                    {
                        // The opcode MUST be continuation, otherwise it's a protocol error
                        if (frameInfo.Opcode != WebSocketOpcode.Continuation)
                        {
                            await CloseWrite(cancelToken, realTime, WebSocketCloseReason.ProtocolError, "Received non-continuation frame where one was expected").ConfigureAwait(false);
                            return new WebSocketStreamResult(WebSocketCloseReason.ProtocolError, "Remote peer sent non-continuation frame where one was expected");
                        }
                    }

                    if (frameInfo.FlagMask && !_isServer)
                    {
                        // Servers MUST NOT send masked frames.
                        await CloseWrite(cancelToken, realTime, WebSocketCloseReason.ProtocolError, "Server sent a masked frame").ConfigureAwait(false);
                        return new WebSocketStreamResult(WebSocketCloseReason.ProtocolError, "Server sent a masked frame");
                    }
                    else if (!frameInfo.FlagMask && _isServer)
                    {
                        // Client MUST send masked frames.
                        await CloseWrite(cancelToken, realTime, WebSocketCloseReason.ProtocolError, "Client sent an unmasked frame").ConfigureAwait(false);
                        return new WebSocketStreamResult(WebSocketCloseReason.ProtocolError, "Client sent an unmasked frame");
                    }

                    // Now loop and read the entire payload, unmasking each frame if needed, and copying to output buffer
                    long bytesReadFromThisFrame = 0;
                    while (bytesReadFromThisFrame < frameInfo.PayloadLength)
                    {
                        int maxCanReadFromThisFrame = (int)Math.Min((long)receiveBuf.Length - frameInfo.HeaderLength, frameInfo.PayloadLength - bytesReadFromThisFrame);
                        int bytesRead = await _socket.Value.ReadAnyAsync(receiveBuf.Buffer, frameInfo.HeaderLength, maxCanReadFromThisFrame, cancelToken, realTime).ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            // The socket closed unexpectedly. Make sure to report any partial data we wrote to the output.
                            CloseRead(WebSocketCloseReason.ConnectionInterrupted, "Socket read operation was interrupted");
                            return new WebSocketStreamResult(WebSocketCloseReason.ConnectionInterrupted, null, bytesWrittenToOutput);
                        }

                        if (frameInfo.FlagMask)
                        {
                            MaskOrUnmaskData(
                                new ArraySegment<byte>(receiveBuf.Buffer, frameInfo.HeaderLength - 4, 4),
                                new ArraySegment<byte>(receiveBuf.Buffer, frameInfo.HeaderLength, bytesRead),
                                initialMaskOffset: (int)(bytesReadFromThisFrame % 4));
                        }

                        await destinationStream.WriteAsync(receiveBuf.Buffer, frameInfo.HeaderLength, bytesRead, cancelToken, realTime).ConfigureAwait(false);
                        bytesReadFromThisFrame += bytesRead;
                        bytesWrittenToOutput += bytesRead;
                    }

                    firstFrame = false;
                    expectMoreFrames = !frameInfo.FlagFin;
                }

                return new WebSocketStreamResult(messageType, bytesWrittenToOutput);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            _pingThreadCancel.Cancel();

            if (disposing)
            {
                _writeLock.Dispose();
                _pingThreadCancel.Dispose();
                try
                {
                    _socket.Value.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.ReadWrite, allowLinger: false).Await();
                }
                catch (Exception e)
                {
                    _logger.Log(e);
                }

                if (_ownsSocket)
                {
                    _socket.Value.Dispose();
                }
            }
        }

        private void CloseRead(WebSocketCloseReason closeStatus, string closeMessage)
        {
            if (State == WebSocketState.Open)
            {
                State = WebSocketState.HalfClosedRemote;
            }
            else
            {
                State = WebSocketState.Closed;
            }

            _readClosed = true;
            RemoteCloseReason = closeStatus;
            RemoteCloseMessage = closeMessage;
        }

        /// <summary>
        /// Represents the result of reading the headers on a single incoming websocket frame.
        /// </summary>
        private struct PartialFrameInfo
        {
            public bool FlagFin;
            public bool FlagMask;
            public WebSocketCloseReason? Error;
            public WebSocketOpcode Opcode;
            public int HeaderLength;
            public long PayloadLength;
        }

        /// <summary>
        /// Advances the socket far enough to read the entire header of an incoming frame,
        /// and returns metadata about the type of frame, its size, its opcode, etc., while
        /// while leaving it up to the caller to finish reading the rest of the payload.
        /// </summary>
        /// <param name="socket">The socket to read from.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="receiveBuf">A buffer at least 14 bytes in length</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>Information about the incoming websocket frame</returns>
        private static async ValueTask<PartialFrameInfo> ReadIncomingFrameHeader(
            WeakPointer<ISocket> socket,
            ILogger logger,
            PooledBuffer<byte> receiveBuf,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            int bytesRead = await socket.Value.ReadAsync(receiveBuf.Buffer, 0, 2, cancelToken, realTime).ConfigureAwait(false);

            if (bytesRead < 2)
            {
                return new PartialFrameInfo()
                {
                    Error = WebSocketCloseReason.ConnectionInterrupted
                };
            }

            WebSocketOpcode opcode = (WebSocketOpcode)(receiveBuf.Buffer[0] & 0x0F);
            int headerLength = 2;
            bool flagFin = (receiveBuf.Buffer[0] & 0x80) != 0;
            bool flagMask = (receiveBuf.Buffer[1] & 0x80) != 0;
            byte basePayloadLen = (byte)(receiveBuf.Buffer[1] & 0x7F);

            if (flagMask)
            {
                headerLength += 4;
            }

            if (basePayloadLen == 126)
            {
                headerLength += 2;
            }
            else if (basePayloadLen == 127)
            {
                headerLength += 8;
            }

            bytesRead = await socket.Value.ReadAsync(receiveBuf.Buffer, 2, headerLength - 2, cancelToken, realTime).ConfigureAwait(false);

            if (bytesRead < headerLength - 2)
            {
                return new PartialFrameInfo()
                {
                    Error = WebSocketCloseReason.ConnectionInterrupted
                };
            }

            // Now parse the full payload length
            long payloadLength;
            if (basePayloadLen < 126)
            {
                payloadLength = (long)basePayloadLen;
            }
            else if (basePayloadLen == 126)
            {
                payloadLength = BinaryHelpers.ByteArrayToUInt16BigEndian(receiveBuf.Buffer, 2);
            }
            else
            {
                payloadLength = BinaryHelpers.ByteArrayToInt64BigEndian(receiveBuf.Buffer, 2);
            }
            
            return new PartialFrameInfo()
            {
                FlagFin = flagFin,
                FlagMask = flagMask,
                HeaderLength = headerLength,
                PayloadLength = payloadLength,
                Opcode = opcode,
                Error = null
            };
        }

        private async ValueTask HandleIncomingControlFrame(
            PartialFrameInfo frameInfo,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            if (frameInfo.Opcode == WebSocketOpcode.CloseConnection)
            {
                // Parse close information
                WebSocketCloseReason closeReason = WebSocketCloseReason.Empty;
                string closeMessage = null;
                int closeFrameLength = (int)frameInfo.PayloadLength;
                if (closeFrameLength > 0)
                {
                    using (PooledBuffer<byte> outputBuf = BufferPool<byte>.Rent(closeFrameLength))
                    {
                        await _socket.Value.ReadAsync(outputBuf.Buffer, 0, closeFrameLength, cancelToken, realTime).ConfigureAwait(false);
                        closeReason = (WebSocketCloseReason)BinaryHelpers.ByteArrayToInt16BigEndian(outputBuf.Buffer, 0);

                        if (closeFrameLength > 2)
                        {
                            closeMessage = StringUtils.UTF8_WITHOUT_BOM.GetString(outputBuf.Buffer, 2, closeFrameLength - 2);
                        }
                    }
                }

                CloseRead(closeReason, closeMessage);
                // Cool. Guess we're closed now. This will do nothing if write has already been closed.
                await CloseWrite(cancelToken, realTime).ConfigureAwait(false);
            }
            else if (frameInfo.Opcode == WebSocketOpcode.Ping)
            {
                // Respond with a pong.
                int pongLength = (int)frameInfo.PayloadLength;
                using (PooledBuffer<byte> outputBuf = BufferPool<byte>.Rent(pongLength + MAX_HEADER_SIZE))
                {
                    await _socket.Value.ReadAsync(outputBuf.Buffer, 0, pongLength, cancelToken, realTime).ConfigureAwait(false);
                    await WriteOutgoingFrameInternal(
                        new ArraySegment<byte>(outputBuf.Buffer, 0, pongLength),
                        outputBuf,
                        WebSocketOpcode.Pong,
                        true,
                        cancelToken,
                        realTime).ConfigureAwait(false);
                }
            }
            else if (frameInfo.Opcode == WebSocketOpcode.Pong)
            {
                // Just read the payload, but we don't care about it.
                int pongLength = (int)frameInfo.PayloadLength;
                using (PooledBuffer<byte> outputBuf = BufferPool<byte>.Rent(pongLength))
                {
                    await _socket.Value.ReadAsync(outputBuf.Buffer, 0, pongLength, cancelToken, realTime).ConfigureAwait(false);
                }
            }
            else
            {
                await CloseWrite(cancelToken, realTime, WebSocketCloseReason.InvalidMessageType, "Unknown control opcode " + frameInfo.Opcode).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Prepares an outgoing package and writes it to the wire.
        /// If the outgoing frame is a Close frame, then this method will also atomically close
        /// the write side of this websocket session, and then ensure that no further packets
        /// can be written to the socket.
        /// </summary>
        /// <param name="payload">The read-only array containing the input data</param>
        /// <param name="responseBuffer">A scratch buffer whose length MUST be at least the payload size + MAX_HEADER_SIZE.
        /// The contents of this buffer will be mangled while preparing the frame. It is possible that this buffer already contains
        /// the actual payload data. If that is the case, the mangling will be done in such as way as to preserve
        /// the integrity of the data before it is sent.</param>
        /// <param name="opcode">The opcode to associate with this frame.</param>
        /// <param name="endOfMessage">Whether this is the final frame of a multipart message (the FIN flag)</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>An async task.</returns>
        private async ValueTask WriteOutgoingFrameInternal(
            ArraySegment<byte> payload,
            PooledBuffer<byte> responseBuffer,
            WebSocketOpcode opcode,
            bool endOfMessage,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            if (responseBuffer.Length < payload.Count + MAX_HEADER_SIZE)
            {
                throw new Exception("Internal websocket buffer length is too small");
            }

            int payloadSize = payload.Count;
            int headerSize = _isServer ? 2 : 6; // account for client requiring 4 bytes for mask
            if (payloadSize > 65535)
            {
                headerSize += 8;
            }
            else if (payloadSize > 125)
            {
                headerSize += 2;
            }

            // Set the mask and copy the payload. We do this first in case
            // the source buffer and the scratch buffer are the same, so we don't need two separate buffers
            // and this becomes a MemMove operation.
            if (payloadSize > 0)
            {
                if (!_isServer)
                {
                    // Copy the data to where it will eventually be in the buffer.
                    // This has a trade-off: we can't mask the data during the copy so we have to do 2 passes,
                    // but we don't have to allocate a temporary array for the mask itself since we can
                    // just put it into the proper place of the buffer to begin with.
                    ArrayExtensions.MemCopy(payload.Array, payload.Offset, responseBuffer.Buffer, headerSize, payloadSize);
                    
                    // Generate the mask in-place where it should go in the header
                    GenerateNonZeroBytes(_secureRandom, responseBuffer.Buffer, headerSize - 4, 4);

                    // Then apply that mask to the payload
                    MaskOrUnmaskData(
                        new ArraySegment<byte>(responseBuffer.Buffer, headerSize - 4, 4),
                        new ArraySegment<byte>(responseBuffer.Buffer, headerSize, payloadSize));
                }
                else
                {
                    ArrayExtensions.MemCopy(payload.Array, payload.Offset, responseBuffer.Buffer, headerSize, payloadSize);
                }
            }

            // Set the flags
            int maskBit = _isServer ? 0x00 : 0x80;
            int finBit = endOfMessage ? 0x80 : 0x00;
            responseBuffer.Buffer[0] = (byte)((int)opcode | finBit);

            // Set the length. Make sure we're using big-endian (network byte order) for these values.
            if (payloadSize > 65535)
            {
                responseBuffer.Buffer[1] = (byte)(127 | maskBit);
                BinaryHelpers.UInt64ToByteArrayBigEndian((ulong)payload.Count, responseBuffer.Buffer, 2);
            }
            else if (payloadSize > 125)
            {
                responseBuffer.Buffer[1] = (byte)(126 | maskBit);
                BinaryHelpers.UInt16ToByteArrayBigEndian((ushort)payload.Count, responseBuffer.Buffer, 2);
            }
            else
            {
                responseBuffer.Buffer[1] = (byte)(payload.Count | maskBit);
            }

            // Lock output and write to socket
            await _writeLock.GetLockAsync().ConfigureAwait(false);
            try
            {
                if (!_writeClosed)
                {
                    await _socket.Value.WriteAsync(responseBuffer.Buffer, 0, headerSize + payloadSize, cancelToken, realTime).ConfigureAwait(false);
                }

                // Deterministically set writeClosed here.
                // This is to ensure that, if we are in the middle of writing data, and we unexpectedly close the connection,
                // the close frame is the last frame to be written to output.
                if (opcode == WebSocketOpcode.CloseConnection)
                {
                    _logger.Log("Websocket output closed");
                    _writeClosed = true;
                    await _socket.Value.Disconnect(cancelToken, realTime, NetworkDuplex.Write, allowLinger: false).ConfigureAwait(false);

                    if (State == WebSocketState.Open)
                    {
                        State = WebSocketState.HalfClosedLocal;
                    }
                    else
                    {
                        State = WebSocketState.Closed;
                    }
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task RunPingThread(IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    await realTime.WaitAsync(PING_INTERVAL, cancelToken);

                    const int pingLength = 8;
                    using (PooledBuffer<byte> outputBuf = BufferPool<byte>.Rent(pingLength + MAX_HEADER_SIZE))
                    {
                        BinaryHelpers.Int64ToByteArrayLittleEndian(HighPrecisionTimer.GetCurrentTicks(), outputBuf.Buffer, 0);
                        await WriteOutgoingFrameInternal(
                            new ArraySegment<byte>(outputBuf.Buffer, 0, pingLength),
                            outputBuf,
                            WebSocketOpcode.Ping,
                            true,
                            cancelToken,
                            realTime).ConfigureAwait(false);
                    }

                    // We don't care about the pong, honestly.
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                realTime.Merge();
            }
        }

        /// <summary>
        /// Fills the destination buffer with N non-zero pseudorandom bytes
        /// </summary>
        /// <param name="rand"></param>
        /// <param name="dest"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        private static void GenerateNonZeroBytes(IRandom rand, byte[] dest, int offset, int count)
        {
            for (int c = offset; c < count + offset; c++)
            {
                dest[c] = 0;
                while (dest[c] == 0)
                {
                    dest[c] = (byte)(rand.NextInt() & 0xFF);
                }
            }
        }

        private static WebSocketOpcode ConvertMessageTypeToOpcode(WebSocketMessageType messageType)
        {
            switch (messageType)
            {
                case WebSocketMessageType.Text:
                    return WebSocketOpcode.TextFrame;
                case WebSocketMessageType.Binary:
                    return WebSocketOpcode.BinaryFrame;
                default:
                    throw new ArgumentException("Invalid websocket message type " + messageType);
            }
        }

        private static WebSocketMessageType ConvertOpcodeTypeToMessageType(WebSocketOpcode opcode)
        {
            switch (opcode)
            {
                case WebSocketOpcode.TextFrame:
                    return WebSocketMessageType.Text;
                case WebSocketOpcode.BinaryFrame:
                    return WebSocketMessageType.Binary;
                default:
                    throw new ArgumentException("Invalid websocket message type " + opcode);
            }
        }

        private static void MaskOrUnmaskData(ArraySegment<byte> mask, ArraySegment<byte> data, int initialMaskOffset = 0)
        {
            int maskIter = mask.Offset + initialMaskOffset;
            int outIter = data.Offset;
            int maxMask = mask.Offset + mask.Count;
            for (int c = 0; c < data.Count; c++)
            {
                data.Array[outIter] = (byte)(mask.Array[maskIter] ^ data.Array[outIter]);
                outIter++;
                maskIter++;
                if (maskIter >= maxMask)
                {
                    maskIter = mask.Offset;
                }
            }
        }
    }
}
