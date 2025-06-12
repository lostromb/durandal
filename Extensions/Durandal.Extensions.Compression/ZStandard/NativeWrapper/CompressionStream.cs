﻿using Durandal.Common.IO;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static Durandal.Extensions.Compression.ZStandard.NativeWrapper.ExternMethods;

namespace Durandal.Extensions.Compression.ZStandard.NativeWrapper
{
    internal class CompressionStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly PooledBuffer<byte> _outputBuffer;
        private readonly int _bufferSize;
        private readonly bool _isolateInnerStream;
#if !(NET45 || NETSTANDARD2_0)
		private readonly ReadOnlyMemory<byte> outputMemory;
#endif

        private IntPtr cStream;
        private UIntPtr pos;

        public readonly CompressionOptions Options;

        public CompressionStream(Stream stream)
            : this(stream, CompressionOptions.Default)
        { }

        public CompressionStream(Stream stream, int bufferSize)
            : this(stream, CompressionOptions.Default, bufferSize)
        { }

        public CompressionStream(Stream stream, CompressionOptions options, int bufferSize = 0, bool isolateInnerStream = false)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite)
                throw new ArgumentException("Stream is not writable", nameof(stream));
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            _isolateInnerStream = isolateInnerStream;
            _innerStream = stream;

            cStream = ZSTD_createCStream().EnsureZstdSuccess();
            ZSTD_CCtx_reset(cStream, ZSTD_ResetDirective.ZSTD_reset_session_only).EnsureZstdSuccess();

            Options = options;
            if (options != null)
            {
                options.ApplyCompressionParams(cStream);

                if (options.Cdict != IntPtr.Zero)
                    ZSTD_CCtx_refCDict(cStream, options.Cdict).EnsureZstdSuccess();
            }

            this._bufferSize = bufferSize > 0 ? bufferSize : (int)ZSTD_CStreamOutSize().EnsureZstdSuccess();
            _outputBuffer = BufferPool<byte>.Rent(this._bufferSize);
        }
        ~CompressionStream() => Dispose(false);

#if !(NET45 || NETSTANDARD2_0)
		public override void Write(ReadOnlySpan<byte> buffer)
		{
			EnsureNotDisposed();

			WriteInternal(buffer);
		}

		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
		{
			EnsureNotDisposed();

			return WriteInternalAsync(buffer, cancellationToken);
		}
#endif

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureParamsValid(buffer, offset, count);
            EnsureNotDisposed();

            WriteInternal(new Span<byte>(buffer, offset, count));
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            EnsureParamsValid(buffer, offset, count);
            EnsureNotDisposed();
            await WriteInternalAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken);
        }

        private void WriteInternal(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length == 0)
                return;

            var input = new ZSTD_Buffer(UIntPtr.Zero, (UIntPtr)buffer.Length);
            var output = new ZSTD_Buffer(pos, (UIntPtr)_bufferSize);

            var outputSpan = _outputBuffer.Buffer.AsSpan(0, _bufferSize);

            do
            {
                if (output.IsFullyConsumed)
                {
                    FlushOutputBuffer(outputSpan.Slice(0, (int)output.pos));
                    output.pos = UIntPtr.Zero;
                }

                Compress(buffer, ref output, ref input, ZSTD_EndDirective.ZSTD_e_continue);
            } while (!input.IsFullyConsumed);

            pos = output.pos;
        }

        private async ValueTask WriteInternalAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (buffer.Length == 0)
                return;

            var input = new ZSTD_Buffer(UIntPtr.Zero, (UIntPtr)buffer.Length);
            var output = new ZSTD_Buffer(pos, (UIntPtr)_bufferSize);

            do
            {
                if (output.IsFullyConsumed)
                {
                    await FlushOutputBufferAsync(ref output, cancellationToken).ConfigureAwait(false);
                    output.pos = UIntPtr.Zero;
                }

                Compress(buffer.Span, ref output, ref input, ZSTD_EndDirective.ZSTD_e_continue);
            } while (!input.IsFullyConsumed);

            pos = output.pos;
        }

        private unsafe UIntPtr Compress(ReadOnlySpan<byte> buffer, ref ZSTD_Buffer output, ref ZSTD_Buffer input, ZSTD_EndDirective directive)
        {
            fixed (void* inputHandle = &MemoryMarshal.GetReference(buffer))
            fixed (void* outputHandle = &_outputBuffer.Buffer[0])
            {
                input.buffer = new IntPtr(inputHandle);
                output.buffer = new IntPtr(outputHandle);

                return ZSTD_compressStream2(cStream, ref output, ref input, directive).EnsureZstdSuccess();
            }
        }

        private void FlushOutputBuffer(ReadOnlySpan<byte> outputSpan)
            => _innerStream.Write(_outputBuffer.Buffer, 0, outputSpan.Length);
        private Task FlushOutputBufferAsync(ref ZSTD_Buffer output, CancellationToken cancellationToken)
            => _innerStream.WriteAsync(_outputBuffer.Buffer, 0, (int)output.pos, cancellationToken);

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            EnsureNotDisposed();

            FlushCompressStream(ZSTD_EndDirective.ZSTD_e_flush);
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            EnsureNotDisposed();
            await FlushCompressStreamAsync(ZSTD_EndDirective.ZSTD_e_flush, cancellationToken);
        }

        private void FlushCompressStream(ZSTD_EndDirective directive)
        {
            var buffer = ReadOnlySpan<byte>.Empty;

            var input = new ZSTD_Buffer(UIntPtr.Zero, UIntPtr.Zero);
            var output = new ZSTD_Buffer(pos, (UIntPtr)_bufferSize);

            var outputSpan = _outputBuffer.Buffer.AsSpan(0, _bufferSize);

            do
            {
                if (output.IsFullyConsumed)
                {
                    FlushOutputBuffer(outputSpan.Slice(0, (int)output.pos));
                    output.pos = UIntPtr.Zero;
                }
            } while (Compress(buffer, ref output, ref input, directive) != UIntPtr.Zero);

            if (output.pos != UIntPtr.Zero)
                FlushOutputBuffer(outputSpan.Slice(0, (int)output.pos));

            pos = UIntPtr.Zero;
        }

        private async ValueTask FlushCompressStreamAsync(ZSTD_EndDirective directive, CancellationToken cancellationToken)
        {
            var input = new ZSTD_Buffer(UIntPtr.Zero, UIntPtr.Zero);
            var output = new ZSTD_Buffer(pos, (UIntPtr)_bufferSize);

            do
            {
                if (!output.IsFullyConsumed)
                    continue;

                await FlushOutputBufferAsync(ref output, cancellationToken).ConfigureAwait(false);
                output.pos = UIntPtr.Zero;
            } while (Compress(ReadOnlySpan<byte>.Empty, ref output, ref input, directive) != UIntPtr.Zero);

            if (output.pos != UIntPtr.Zero)
                await FlushOutputBufferAsync(ref output, cancellationToken).ConfigureAwait(false);

            pos = UIntPtr.Zero;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (cStream == IntPtr.Zero)
                return;

            try
            {
                if (disposing)
                {
                    FlushCompressStream(ZSTD_EndDirective.ZSTD_e_end);

                    if (!_isolateInnerStream)
                    {
                        _innerStream?.Dispose();
                    }
                }
            }
            finally
            {
                ZSTD_freeCStream(cStream);
                _outputBuffer?.Dispose();
                cStream = IntPtr.Zero;
                base.Dispose(disposing);
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
            if (cStream == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(CompressionStream));
        }
    }
}