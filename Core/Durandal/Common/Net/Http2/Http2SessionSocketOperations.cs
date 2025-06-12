using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.Time;
using System.IO;
using Durandal.Common.Net.Http2.Session;
using Durandal.Common.IO;
using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.API;
using Durandal.Common.Net.Http2.Frames;
using Durandal.Common.MathExt;
using Durandal.Common.Net.Http;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Net.Http2
{
    public partial class Http2Session
    {
        public async Task<Tuple<HttpResponse, ISocket>> OpenClientWebsocket(HttpRequest connectRequest, ILogger traceLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (!_isClient)
            {
                throw new InvalidOperationException("Only an HTTP/2 client can send HTTP requests");
            }

            if (_isActive != 1)
            {
                traceLogger.Log("HTTP request aborted because session is shut down");
                return null;
            }

            // Step 1. We need to make sure the remote server supports websockets.
            // We can't determine this until it has sent SETTINGS_ENABLE_CONNECT.
            // If we have just barely established this connection (as is quite likely),
            // we need to wait for this value to come in first.
            await _remoteSentSettingsSignal.WaitAsync(cancelToken).ConfigureAwait(false);

            connectRequest.RequestHeaders.Remove(HttpConstants.HEADER_KEY_CONNECTION);
            connectRequest.RequestHeaders.Remove(HttpConstants.HEADER_KEY_HOST);
            ValidateOutgoingRequest(connectRequest, traceLogger);

            using (CancellationTokenSource joinedCancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_sessionShutdown.Token, cancelToken))
            {
                CancellationToken httpRequestCancelToken = joinedCancelTokenSource.Token;
                try
                {
                    // Reserve the stream ID in the dictionary
                    int streamId = (Interlocked.Increment(ref _nextStreamIndex) * 2) - (_isClient ? 1 : 0);

                    // TODO enforce max_concurrent_streams settings
                    Http2Stream newStream = NewStream(streamId, StreamState.Open);

                    _activeStreams[streamId] = newStream;
                    HttpResponse webSocketResponse = await ProcessHttpRequestUsingNewStream(
                        connectRequest,
                        newStream,
                        httpRequestCancelToken,
                        realTime,
                        endOfData: false).ConfigureAwait(false);
                    if (webSocketResponse.ResponseCode == 101)
                    {
                        // Return a socket operating over the newly created stream
                        return new Tuple<HttpResponse, ISocket>(webSocketResponse, new Http2StreamSocket(new WeakPointer<Http2Session>(this), streamId));
                    }
                    else
                    {
                        traceLogger.Log("Websocket connect failed", LogLevel.Err);
                        return null;
                    }
                }
                catch (OperationCanceledException)
                {
                    traceLogger.Log("HTTP request aborted because session was being canceled", LogLevel.Wrn);
                    return null;
                }
                catch (Exception ex)
                {
                    traceLogger.Log("HTTP request aborted because of unhandled exception", LogLevel.Wrn);
                    traceLogger.Log(ex, LogLevel.Wrn);
                    return null;
                }
            }
        }

        internal void SocketStream_Close(NetworkDuplex duplex, int streamId, IRealTimeProvider realTime)
        {
            Http2Stream stream;
            if (_activeStreams.TryGetValue(streamId, out stream))
            {
                if (duplex.HasFlag(NetworkDuplex.Write))
                {
                    _outgoingCommands.EnqueueData(
                        new SendDataFrameCommand(
                            Http2DataFrame.CreateOutgoing(
                                BufferPool<byte>.Rent(0),
                                streamId,
                                endStream: true)),
                        realTime);
                }

                if (duplex.HasFlag(NetworkDuplex.Read))
                {
                    CloseStream(stream);
                }
            }
        }

        internal async Task<bool> SocketStream_SendData(int streamId, byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            Http2Stream stream;
            if (!_activeStreams.TryGetValue(streamId, out stream))
            {
                return false;
            }

            int bytesWrittenToStream = 0;
            int maxFrameSize = FastMath.Min(BufferPool<byte>.DEFAULT_BUFFER_SIZE, _remoteSettings.MaxFrameSize);

            // Wait on both the stream-local and global flow control windows until we have reserved
            // at least some credits to be able to send outgoing data
            int flowControlCreditAvailable = await WaitOnOutgoingFlowControlIfNeeded(
                stream,
                maxFrameSize,
                cancelToken,
                waitProvider).ConfigureAwait(false);

            try
            {
                if (stream.State == StreamState.HalfClosedLocal ||
                    stream.State == StreamState.Closed)
                {
                    // Stream was reset by remote peer.
                    CloseStream(stream);
                    return false;
                }

                int amountCanReadFromStream = FastMath.Min(count - bytesWrittenToStream, flowControlCreditAvailable);
                PooledBuffer<byte> dataPayload = BufferPool<byte>.Rent(amountCanReadFromStream);
                ArrayExtensions.MemCopy(data, offset + bytesWrittenToStream, dataPayload.Buffer, 0, amountCanReadFromStream);

                while (bytesWrittenToStream < count)
                {
                    flowControlCreditAvailable -= amountCanReadFromStream;
                    _outgoingCommands.EnqueueData(
                        new SendDataFrameCommand(
                            Http2DataFrame.CreateOutgoing(
                                dataPayload,
                                streamId,
                                endStream: false)),
                        waitProvider);

                    bytesWrittenToStream += amountCanReadFromStream;
                    dataPayload = null;

                    if (flowControlCreditAvailable == 0)
                    {
                        flowControlCreditAvailable = await WaitOnOutgoingFlowControlIfNeeded(
                            stream,
                            maxFrameSize,
                            cancelToken,
                            waitProvider).ConfigureAwait(false);
                    }

                    if (stream.State == StreamState.HalfClosedLocal ||
                        stream.State == StreamState.Closed)
                    {
                        CloseStream(stream);
                        return false;
                    }

                    amountCanReadFromStream = FastMath.Min(count - bytesWrittenToStream, flowControlCreditAvailable);
                    if (bytesWrittenToStream < count)
                    {
                        dataPayload = BufferPool<byte>.Rent(amountCanReadFromStream);
                        ArrayExtensions.MemCopy(data, offset + bytesWrittenToStream, dataPayload.Buffer, 0, amountCanReadFromStream);
                    }
                }
            }
            finally
            {
                if (flowControlCreditAvailable > 0)
                {
                    // return credits we didn't use
                    stream.OutgoingFlowControlWindow.AugmentCredits(flowControlCreditAvailable);
                    stream.OutgoingFlowControlAvailableSignal.Set();
                    _overallConnectionOutgoingFlowWindow.AugmentCredits(flowControlCreditAvailable);
                    _overallConnectionOutgoingFlowWindowAvailable.Set();
                }
            }

            return true;
        }

        internal async Task<int> SocketStream_ReadData(int streamId, byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            Http2Stream stream;
            if (_activeStreams.TryGetValue(streamId, out stream))
            {
                return await stream.ReadStream.ReadAsync(data, offset, count, cancelToken, waitProvider).ConfigureAwait(false);
            }

            return 0;
        }
    }
}
