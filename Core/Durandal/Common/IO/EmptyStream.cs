using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Represents a read stream that always has no data in it, and if you write to it the data will be ignored.
    /// </summary>
    public class EmptyStream : NonRealTimeStream
    {
        private static readonly Task<int> ZERO_TASK = Task.FromResult<int>(0);
        private static readonly EmptyStream _singleton = new EmptyStream();
        public static EmptyStream Singleton => _singleton;

        private EmptyStream()
        {
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position
        {
            get
            {
                return 0;
            }
            set { }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return 0;
        }

        public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return ZERO_TASK;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return 0;
        }

        public override void SetLength(long value)
        {
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
        }

        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return DurandalTaskExtensions.NoOpTask;
        }

        protected override void Dispose(bool disposing)
        {
            // The empty singleton stream can be disposed multiple times so this is explicitly a no-op
        }
    }
}
