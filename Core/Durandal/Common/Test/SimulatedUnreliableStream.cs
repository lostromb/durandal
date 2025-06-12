using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;

namespace Durandal.Common.Test
{
    /// <summary>
    /// Implementation of a stream which will never quite return as many bytes as are requested on read.
    /// This is used as a test case utility to make sure all fixed-length reads are reliable.
    /// </summary>
    public class SimulatedUnreliableStream : NonRealTimeStream
    {
        private readonly IRandom _random;
        private readonly NonRealTimeStream _innerStream;
        private int _disposed = 0;

        public SimulatedUnreliableStream(NonRealTimeStream innerStream, int randomSeed = 425823)
        {
            _innerStream = innerStream;
            _random = new FastRandom(randomSeed);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public override bool CanRead => _innerStream.CanRead;

        public override bool CanSeek => _innerStream.CanSeek;

        public override bool CanWrite => _innerStream.CanWrite;

        public override long Length => _innerStream.Length;

        public override long Position
        {
            get
            {
                return _innerStream.Position;
            }
            set
            {
                _innerStream.Position = value;
            }
        }

        public override void Flush()
        {
            _innerStream.Flush();
        }

        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (count > 1)
            {
                return _innerStream.Read(targetBuffer, offset, _random.NextInt(1, count), cancelToken, realTime);
            }
            else
            {
                return _innerStream.Read(targetBuffer, offset, count, cancelToken, realTime);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count > 1)
            {
                return _innerStream.Read(buffer, offset, _random.NextInt(1, count));
            }
            else
            {
                return _innerStream.Read(buffer, offset, count);
            }
        }

        public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (count > 1)
            {
                return _innerStream.ReadAsync(targetBuffer, offset, _random.NextInt(1, count), cancelToken, realTime);
            }
            else
            {
                return _innerStream.ReadAsync(targetBuffer, offset, count, cancelToken, realTime);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _innerStream.Write(sourceBuffer, offset, count, cancelToken, realTime);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _innerStream.WriteAsync(sourceBuffer, offset, count, cancelToken, realTime);
        }

        public override Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _innerStream.FlushAsync(cancelToken, realTime);
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
                    _innerStream?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
