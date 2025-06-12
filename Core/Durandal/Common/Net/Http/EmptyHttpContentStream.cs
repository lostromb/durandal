using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.Time;

namespace Durandal.Common.Net.Http
{
    public class EmptyHttpContentStream : HttpContentStream
    {
        private static readonly Task<int> ZERO_TASK = Task.FromResult<int>(0);
        private static readonly EmptyHttpContentStream _singleton = new EmptyHttpContentStream();
        public static EmptyHttpContentStream Singleton => _singleton;

        private EmptyHttpContentStream()
        {
        }

        public override long ContentBytesTransferred => 0;

        public override HttpHeaders Trailers => null;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

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
            throw new NotSupportedException();
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            // The empty singleton stream can be disposed multiple times so this is explicitly a no-op
        }
    }
}
