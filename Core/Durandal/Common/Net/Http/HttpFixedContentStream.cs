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
    /// Represents a stream for reading HTTP data that has a fixed Content-Length. It's basically just a
    /// wrapper that reads a fixed # of bytes from a socket.
    /// </summary>
    public class HttpFixedContentStream : HttpContentStream
    {
        // Socket to read the full payload from. Not owned by this stream!
        private readonly WeakPointer<ISocket> _httpSocket;

        // The value of the Content-Length header which dictates how many bytes we expect
        private readonly long _fixedContentLength;

        // Total amount of CONTENT bytes (not including line breaks, chunk headers, etc.) read from HTTP
        private long _contentBytesRead;

        private int _disposed = 0;

        public HttpFixedContentStream(
            WeakPointer<ISocket> httpSocket,
            long fixedContentLength)
        {
            _httpSocket = httpSocket.AssertNonNull(nameof(httpSocket));
            _fixedContentLength = fixedContentLength;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~HttpFixedContentStream()
        {
            Dispose(false);
        }
#endif

        public override long ContentBytesTransferred => _contentBytesRead;

        public override HttpHeaders Trailers => null;

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
            long bytesRemainingInContent = _fixedContentLength - _contentBytesRead;
            if (bytesRemainingInContent == 0)
            {
                return 0;
            }

            int maxReadSize = (int)Math.Min(count, bytesRemainingInContent);
            int actualReadSize = await _httpSocket.Value.ReadAnyAsync(targetBuffer, offset, maxReadSize, cancelToken, realTime).ConfigureAwait(false);
            if (actualReadSize > 0)
            {
                _contentBytesRead += actualReadSize;
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
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
