using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Stream which reads from a chunked-transfer encoded HTTP data stream. This class will automatically handle
    /// parsing of all chunk headers + trailers and only return the payload data.
    /// </summary>
    public class HttpChunkedContentStream : HttpContentStream
    {
        // Byte buffer to store intermediate data read from socket
        private readonly PooledBuffer<byte> _scratchBuf;

        // Socket to read the full payload from. Owned by this stream, so must be disposed when this stream is disposed
        private readonly WeakPointer<ISocket> _httpSocket;

        private readonly ILogger _logger;

        private readonly bool _expectTrailers;

        private HttpHeaders _parsedTrailers = null;

        // Total amount of CONTENT bytes (not including line breaks, chunk headers, etc.) read from HTTP
        private long _contentBytesRead;

        // Some endpoints may include the Content-Length header even if using chunked encoding.
        // If they do this, we track the "reported" content length here, just to emit a warning if it doesn't match.
        private long? _expectedContentLength;

        // Matcher for finding line breaks in chunked headers
        private readonly CapturePatternMatcher _lineBreakMatcher;

        // The declared length of the current chunk, or null if length is not known yet
        private int? _currentChunkLength;

        private int _bytesReadFromCurrentChunk;

        private int _disposed = 0;

        public HttpChunkedContentStream(
            WeakPointer<ISocket> httpSocket,
            ILogger logger,
            long? expectedContentLength,
            bool expectTrailers)
        {
            _httpSocket = httpSocket;
            _currentChunkLength = null;
            _bytesReadFromCurrentChunk = 0;
            _logger = logger;
            _expectTrailers = expectTrailers;
            _expectedContentLength = expectedContentLength;

            // We only need enough scratch buffer to parse the hex chunk length field
            _scratchBuf = BufferPool<byte>.Rent(16);

            // Use a shared line break matcher if possible
            _lineBreakMatcher = HttpHelpers.NEWLINE_MATCHERS.TryDequeue();
            if (_lineBreakMatcher == null)
            {
                _lineBreakMatcher = new CapturePatternMatcher(HttpConstants.CAPTURE_PATTERN_RN);
            }
            else
            {
                _lineBreakMatcher.Reset();
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~HttpChunkedContentStream()
        {
            Dispose(false);
        }
#endif

        public override long ContentBytesTransferred => _contentBytesRead;

        public override HttpHeaders Trailers => _parsedTrailers;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override long Position
        {
            get
            {
                return _contentBytesRead;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override int Read(byte[] target, int targetOffset, int count)
        {
            throw new NotImplementedException("Async reads are mandatory on HTTP streams");
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
        {
            return ReadAsync(buffer, offset, count, cancelToken, DefaultRealTimeProvider.Singleton);
        }

        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException("Async reads are mandatory on HTTP streams");
        }

        public override async Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_currentChunkLength.HasValue && _currentChunkLength.Value == 0)
            {
                return 0;
            }

            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(HttpChunkedContentStream), "HTTP content stream has been disposed");
            }

            // Do we have a chunk length header?
            if (!_currentChunkLength.HasValue || _bytesReadFromCurrentChunk == _currentChunkLength.Value)
            {
                _lineBreakMatcher.Reset();

                // Parse one
                bool foundChunkLength = false;
                int scratchBufferStartIdx = 0;
                while (!foundChunkLength && scratchBufferStartIdx < 16)
                {
                    int bytesReadIntoScratchBuf = await _httpSocket.Value.ReadAnyAsync(_scratchBuf.Buffer, scratchBufferStartIdx, 16 - scratchBufferStartIdx, cancelToken, realTime).ConfigureAwait(false);
                    if (bytesReadIntoScratchBuf == 0)
                    {
                        // Stream aborted.
                        return 0;
                    }

                    int lineBreakIdx = _lineBreakMatcher.Find(_scratchBuf.Buffer, scratchBufferStartIdx, bytesReadIntoScratchBuf);
                    scratchBufferStartIdx += bytesReadIntoScratchBuf;
                    if (lineBreakIdx > 0)
                    {
                        //string hexString = StringUtils.UTF8_WITHOUT_BOM.GetString(_dataBuffer.Buffer, 0, lineBreakIdx);
                        _currentChunkLength = HttpHelpers.ParseChunkLength(_scratchBuf.Buffer, 0, lineBreakIdx);
                        foundChunkLength = true;
                        _bytesReadFromCurrentChunk = 0;
                        int bytesOfPayloadRead = scratchBufferStartIdx - (lineBreakIdx + 2);
                        if (bytesOfPayloadRead > 0)
                        {
                            // FIXME This doesn't support the chunk-extension= *( ";" chunk-ext-name [ "=" chunk-ext-val ] ) syntax as defined in https://datatracker.ietf.org/doc/html/rfc2616#section-3.6.1
                            _httpSocket.Value.Unread(_scratchBuf.Buffer, lineBreakIdx + 2, bytesOfPayloadRead);
                        }
                    }
                }

                if (!foundChunkLength)
                {
                    throw new FormatException("Could not parse HTTP chunk header. Assuming corrupted stream. Data: 0x" + BinaryHelpers.ToHexString(_scratchBuf.Buffer, 0, scratchBufferStartIdx));
                }
            }

            if (_currentChunkLength.Value == 0)
            {
                // Reached the end of stream. Do some sanity checks on content length
                if (_expectedContentLength.HasValue &&
                    _contentBytesRead != _expectedContentLength.Value)
                {
                    _logger.Log(
                        string.Format("HTTP stream reported Content-Length header of {0} but only {1} bytes were transferred", _expectedContentLength.Value, _contentBytesRead),
                        LogLevel.Wrn);
                }

                // And read any trailers if we expect them.
                if (_expectTrailers && _parsedTrailers == null)
                {
                    Tuple<PooledBuffer<byte>, int> trailerBlock = await HttpHelpers.ReadHttpHeaderBlock(_httpSocket.Value, _logger, cancelToken, realTime).ConfigureAwait(false);
                    int endOfTrailers;
                    _parsedTrailers = HttpHelpers.ParseHttpHeaders(trailerBlock.Item1.Buffer, trailerBlock.Item2, 0, out endOfTrailers, stringCache: null);
                    return 0;
                }
            }

            // There's content, read it.
            // _currentChunkLength is guaranteed to be non-null at this point
            int actualReadSize = 0;
            if (_currentChunkLength.Value > 0)
            {
                int maxReadSize = Math.Min(_currentChunkLength.Value - _bytesReadFromCurrentChunk, count);
                actualReadSize = await _httpSocket.Value.ReadAnyAsync(targetBuffer, offset, maxReadSize, cancelToken, realTime).ConfigureAwait(false);
                if (actualReadSize > 0)
                {
                    _bytesReadFromCurrentChunk += actualReadSize;
                    _contentBytesRead += actualReadSize;
                }
            }

            // If we just finished reading a chunk, we also have to read the \r\n that comes after it
            // We have to make sure this happens for 0-length chunks as well, so the socket is in a consistent state afterwards.
            if (_bytesReadFromCurrentChunk == _currentChunkLength.Value)
            {
                // FIXME: If currentChunkLength == 0, it's possible that there are trailer fields right here before the final CRLF.
                // We don't currently handle those...
                await _httpSocket.Value.ReadAsync(_scratchBuf.Buffer, 0, 2, cancelToken, realTime).ConfigureAwait(false);
                if (_scratchBuf.Buffer[0] != (byte)'\r' ||
                    _scratchBuf.Buffer[1] != (byte)'\n')
                {
                    throw new FormatException("HTTP newlines not in expected place. Assuming corrupted stream (or there are trailers)");
                }
            }

            return actualReadSize;
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException();
        }

        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                if (disposing)
                {
                    _scratchBuf?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
