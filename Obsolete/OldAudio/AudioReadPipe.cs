using Durandal.Common.IO;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    public class AudioReadPipe : NonRealTimeStream
    {
        private readonly NonRealTimeStream _baseStream;
        private readonly string _codec;
        private readonly string _codecParams;

        public AudioReadPipe(NonRealTimeStream baseStream, string codec, string codecParams)
        {
            _baseStream = baseStream;
            _codec = codec;
            _codecParams = codecParams;
        }

        ~AudioReadPipe()
        {
            Dispose(false);
        }

        public string GetCodec()
        {
            return _codec;
        }

        public string GetCodecParams()
        {
            return _codecParams;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _baseStream.Length;

        public override long Position
        {
            get
            {
                return _baseStream.Position;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _baseStream.Read(buffer, offset, count);
        }

        public Task Read(byte[] buffer, int offset, int count, CancellationToken cancelizer)
        {
            return _baseStream.ReadAsync(buffer, offset, count, cancelizer);
        }
        public override int Read(byte[] targetBuffer, int offset, int count, IRealTimeProvider realTime, CancellationToken cancelizer)
        {
            return _baseStream.Read(targetBuffer, offset, count, realTime, cancelizer);
        }

        public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, IRealTimeProvider realTime, CancellationToken cancelizer)
        {
            return _baseStream.ReadAsync(targetBuffer, offset, count, realTime, cancelizer);
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
    }
}
