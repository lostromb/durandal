using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
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
    /// Class which behaves like a read-only MemoryStream for reading from a pooled buffer.
    /// The buffer is then disposed when the stream is disposed.
    /// </summary>
    public class PooledBufferMemoryStream : NonRealTimeStream
    {
        private readonly PooledBuffer<byte> _buffer;
        private readonly int _bufferLength;
        private readonly int _bufferOffset;
        private int _cursor;
        private int _disposed;

        public PooledBufferMemoryStream(PooledBuffer<byte> buffer) : this(buffer, 0, buffer.Length)
        {
        }

        public PooledBufferMemoryStream(PooledBuffer<byte> buffer, int bufferIdx, int bufferLength)
        {
            _buffer = buffer.AssertNonNull(nameof(buffer));

            if (bufferIdx < 0 || bufferIdx >= buffer.Buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferIdx));
            }

            if (_bufferLength < 0 || bufferIdx + bufferLength > buffer.Buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferLength));
            }

            _bufferOffset = bufferIdx;
            _cursor = 0;
            _bufferLength = bufferLength;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PooledBufferMemoryStream()
        {
            Dispose(false);
        }
#endif

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _bufferLength;

        public override long Position
        {
            get
            {
                return _cursor;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("Stream position cannot be negative");
                }
                if (value > _bufferLength)
                {
                    throw new ArgumentOutOfRangeException("Stream position exceeds stream length");
                }

                _cursor = (int)value;
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton);
        }

        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            int amountToRead = FastMath.Min(count, (int)(Length - Position));
            if (amountToRead > 0)
            {
                ArrayExtensions.MemCopy(_buffer.Buffer, _bufferOffset + _cursor, targetBuffer, offset, amountToRead);
                _cursor += amountToRead;
            }

            return amountToRead;
        }

        public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult(Read(targetBuffer, offset, count, cancelToken, realTime));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
        {
            return Task.FromResult(Read(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.End:
                    Position = _bufferLength + offset; // assuming offset is negative
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                default:
                    throw new NotImplementedException();
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                if (disposing)
                {
                    _buffer?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
