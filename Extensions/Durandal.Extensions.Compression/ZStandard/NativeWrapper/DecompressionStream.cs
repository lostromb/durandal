using Durandal.Common.IO;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static Durandal.Extensions.Compression.ZStandard.NativeWrapper.ExternMethods;

namespace Durandal.Extensions.Compression.ZStandard.NativeWrapper
{
    internal class DecompressionStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly PooledBuffer<byte> _inputBuffer;
        private readonly int _bufferSize;
        private readonly bool _isolateInnerStream;
#if !(NET45 || NETSTANDARD2_0)
		private readonly Memory<byte> inputMemory;
#endif

        private IntPtr dStream;
        private UIntPtr pos;
        private UIntPtr size;

        public readonly DecompressionOptions Options;

        public DecompressionStream(Stream stream)
            : this(stream, null)
        { }

        public DecompressionStream(Stream stream, int bufferSize)
            : this(stream, null, bufferSize)
        { }

        public DecompressionStream(Stream stream, DecompressionOptions options, int bufferSize = 0, bool isolateInnerStream = false)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not readable", nameof(stream));
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            _innerStream = stream;
            _isolateInnerStream = isolateInnerStream;

            dStream = ZSTD_createDStream().EnsureZstdSuccess();
            ZSTD_DCtx_reset(dStream, ZSTD_ResetDirective.ZSTD_reset_session_only).EnsureZstdSuccess();

            Options = options;
            if (options != null)
            {
                options.ApplyDecompressionParams(dStream);

                if (options.Ddict != IntPtr.Zero)
                    ZSTD_DCtx_refDDict(dStream, options.Ddict).EnsureZstdSuccess();
            }

            this._bufferSize = bufferSize > 0 ? bufferSize : (int)ZSTD_DStreamInSize().EnsureZstdSuccess();
            _inputBuffer = BufferPool<byte>.Rent(this._bufferSize);
            pos = size = (UIntPtr)this._bufferSize;
        }

        ~DecompressionStream() => Dispose(false);

#if !(NET45 || NETSTANDARD2_0)
		public override int Read(Span<byte> buffer)
		{
			EnsureNotDisposed();

			return ReadInternal(buffer);
		}

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			EnsureNotDisposed();

			return ReadInternalAsync(buffer, cancellationToken);
		}
#endif

        public override int Read(byte[] buffer, int offset, int count)
        {
            EnsureParamsValid(buffer, offset, count);
            EnsureNotDisposed();

            return ReadInternal(new Span<byte>(buffer, offset, count));
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            EnsureParamsValid(buffer, offset, count);
            EnsureNotDisposed();
            return await ReadInternalAsync(new Memory<byte>(buffer, offset, count), cancellationToken);
        }

        private int ReadInternal(Span<byte> buffer)
        {
            var input = new ZSTD_Buffer(pos, size);
            var output = new ZSTD_Buffer(UIntPtr.Zero, (UIntPtr)buffer.Length);

            var inputSpan = _inputBuffer.Buffer.AsSpan(0, _bufferSize);

            while (!output.IsFullyConsumed && (!input.IsFullyConsumed || FillInputBuffer(inputSpan, ref input) > 0))
                Decompress(buffer, ref output, ref input);

            pos = input.pos;
            size = input.size;

            return (int)output.pos;
        }

        private async ValueTask<int> ReadInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var input = new ZSTD_Buffer(pos, size);
            var output = new ZSTD_Buffer(UIntPtr.Zero, (UIntPtr)buffer.Length);

            while (!output.IsFullyConsumed)
            {
                if (input.IsFullyConsumed)
                {
                    int bytesRead;
                    if ((bytesRead = await _innerStream.ReadAsync(_inputBuffer.Buffer, 0, _bufferSize, cancellationToken).ConfigureAwait(false)) == 0)
                        break;

                    input.size = (UIntPtr)bytesRead;
                    input.pos = UIntPtr.Zero;
                }

                Decompress(buffer.Span, ref output, ref input);
            }

            pos = input.pos;
            size = input.size;

            return (int)output.pos;
        }

        private unsafe void Decompress(Span<byte> buffer, ref ZSTD_Buffer output, ref ZSTD_Buffer input)
        {
            fixed (void* inputBufferHandle = &_inputBuffer.Buffer[0])
            fixed (void* outputBufferHandle = &MemoryMarshal.GetReference(buffer))
            {
                input.buffer = new IntPtr(inputBufferHandle);
                output.buffer = new IntPtr(outputBufferHandle);

                ZSTD_decompressStream(dStream, ref output, ref input).EnsureZstdSuccess();
            }
        }

        private int FillInputBuffer(Span<byte> inputSpan, ref ZSTD_Buffer input)
        {
            int bytesRead = _innerStream.Read(_inputBuffer.Buffer, 0, inputSpan.Length);
            input.size = (UIntPtr)bytesRead;
            input.pos = UIntPtr.Zero;

            return bytesRead;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (dStream == IntPtr.Zero)
                return;

            ZSTD_freeDStream(dStream);
            dStream = IntPtr.Zero;

            if (disposing)
            {
                _inputBuffer?.Dispose();

                if (!_isolateInnerStream)
                {
                    _innerStream?.Dispose();
                }
            }
        }

        private void EnsureParamsValid(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count > buffer.Length - offset)
                throw new ArgumentException("The sum of offset and count is greater than the buffer length");
        }

        private void EnsureNotDisposed()
        {
            if (dStream == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(DecompressionStream));
        }
    }
}