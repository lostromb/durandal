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

namespace Durandal.Common.IO
{
    /// <summary>
    /// A shim class to adapt regular C# streams to the <see cref="HttpContentStream"/> interface.
    /// The inner stream can either be a plain <see cref="Stream"/> or a <see cref="NonRealTimeStream"/>.
    /// </summary>
    public class HttpContentStreamWrapper : HttpContentStream
    {
        // This wrapper handles either NRT streams or regular streams as a disjunction.
        // _plainStream will always be non-null, but if the inner stream
        // implements NonRealTimeStream, _nrtStream will be non-null and we can use that.
        // Both of these references MUST point to the same stream object.
        private readonly NonRealTimeStream _nrtStream;
        private readonly Stream _plainStream;
        private readonly bool _ownsStream;
        private int _disposed = 0;
        private long _contentBytesTransferred = 0;

        public HttpContentStreamWrapper(Stream innerStream, bool ownsStream)
        {
            _plainStream = innerStream.AssertNonNull(nameof(innerStream));
            _nrtStream = null;
            _ownsStream = ownsStream;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public HttpContentStreamWrapper(NonRealTimeStream innerStream, bool ownsStream)
        {
            _nrtStream = innerStream.AssertNonNull(nameof(innerStream));
            _plainStream = innerStream;
            _ownsStream = ownsStream;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public override HttpHeaders Trailers => null;

        public override long ContentBytesTransferred => _contentBytesTransferred;

        public override bool CanRead => _plainStream.CanRead;

        public override bool CanSeek => _plainStream.CanSeek;

        public override bool CanWrite => _plainStream.CanWrite;

        public override long Length => _plainStream.Length;

        public override long Position
        {
            get
            {
                return _plainStream.Position;
            }
            set
            {
                _plainStream.Position = value;
            }
        }

        public override void Flush()
        {
            _plainStream.Flush();
        }

        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int returnVal;
            if (_nrtStream != null)
            {
                returnVal = _nrtStream.Read(targetBuffer, offset, count, cancelToken, realTime);
            }
            else
            {
                returnVal = _plainStream.Read(targetBuffer, offset, count);
            }

            if (returnVal > 0)
            {
                _contentBytesTransferred += returnVal;
            }

            return returnVal;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int returnVal = _plainStream.Read(buffer, offset, count);
            if (returnVal > 0)
            {
                _contentBytesTransferred += returnVal;
            }

            return returnVal;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int returnVal = await _plainStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            if (returnVal > 0)
            {
                _contentBytesTransferred += returnVal;
            }

            return returnVal;
        }

        public override async Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int returnVal;
            if (_nrtStream != null)
            {
                returnVal = await _nrtStream.ReadAsync(targetBuffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
            }
            else
            {
                returnVal = await _plainStream.ReadAsync(targetBuffer, offset, count, cancelToken).ConfigureAwait(false);
            }

            if (returnVal > 0)
            {
                _contentBytesTransferred += returnVal;
            }

            return returnVal;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _plainStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _plainStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _plainStream.Write(buffer, offset, count);
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_nrtStream != null)
            {
                _nrtStream.Write(sourceBuffer, offset, count, cancelToken, realTime);
            }
            else
            {
                _plainStream.Write(sourceBuffer, offset, count);
            }
        }

        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_nrtStream != null)
            {
                return _nrtStream.WriteAsync(sourceBuffer, offset, count, cancelToken, realTime);
            }
            else
            {
                return _plainStream.WriteAsync(sourceBuffer, offset, count, cancelToken);
            }
        }

        public override Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_nrtStream != null)
            {
                return _nrtStream.FlushAsync(cancelToken, realTime);
            }
            else
            {
                return _plainStream.FlushAsync();
            }
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
                    if (_ownsStream)
                    {
                        _plainStream?.Dispose();
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
