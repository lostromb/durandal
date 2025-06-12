using Durandal.Common.IO;
using Durandal.Common.Net.Http;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2
{
    internal class Http2ContentStream : HttpContentStream
    {
        private readonly WeakPointer<Http2Stream> _stream;
        private int _disposed = 0;
        private long _contentBytesTransferred = 0;

        public Http2ContentStream(WeakPointer<Http2Stream> innerStream)
        {
            _stream = innerStream.AssertNonNull(nameof(innerStream));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public override HttpHeaders Trailers => _stream.Value.Trailers;

        public override long ContentBytesTransferred => _contentBytesTransferred;

        public override bool CanRead => _stream.Value.ReadStream.CanRead;

        public override bool CanSeek => _stream.Value.ReadStream.CanSeek;

        public override bool CanWrite => _stream.Value.ReadStream.CanWrite;

        public override long Length => _stream.Value.ReadStream.Length;

        public override long Position
        {
            get
            {
                return _stream.Value.ReadStream.Position;
            }
            set
            {
                _stream.Value.ReadStream.Position = value;
            }
        }

        public override void Flush()
        {
            _stream.Value.ReadStream.Flush();
        }

        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int returnVal = _stream.Value.ReadStream.Read(targetBuffer, offset, count, cancelToken, realTime);
            if (returnVal > 0)
            {
                _contentBytesTransferred += returnVal;
            }

            return returnVal;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int returnVal = _stream.Value.ReadStream.Read(buffer, offset, count);
            if (returnVal > 0)
            {
                _contentBytesTransferred += returnVal;
            }

            return returnVal;
        }

        [Obsolete("Please pass a time provider")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
        {
            int returnVal = await _stream.Value.ReadStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            if (returnVal > 0)
            {
                _contentBytesTransferred += returnVal;
            }

            return returnVal;
        }

        public override async Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int returnVal = await _stream.Value.ReadStream.ReadAsync(targetBuffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
            if (returnVal > 0)
            {
                _contentBytesTransferred += returnVal;
            }

            return returnVal;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Value.ReadStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _stream.Value.ReadStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Value.ReadStream.Write(buffer, offset, count);
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _stream.Value.ReadStream.Write(sourceBuffer, offset, count, cancelToken, realTime);
        }

        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _stream.Value.ReadStream.WriteAsync(sourceBuffer, offset, count, cancelToken, realTime);
        }

        public override Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _stream.Value.ReadStream.FlushAsync(cancelToken, realTime);
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
                    _stream.Value.ReadStream?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
