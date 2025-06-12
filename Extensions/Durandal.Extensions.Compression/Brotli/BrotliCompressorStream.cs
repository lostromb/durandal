namespace Durandal.Extensions.Compression.Brotli
{
    using Durandal.Common.Collections;
    using Durandal.Common.IO;
    using Durandal.Common.MathExt;
    using Durandal.Common.Utils;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Performance-tuned implementation of a Brotli compression stream, using native bindings provided by Brotli.Net.
    /// Compared to the BrotliStream normally provided by that library, this class uses pooled buffers, fewer intermediate copies,
    /// and safe handles to native structs.
    /// </summary>
    public sealed class BrotliCompressorStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly SafeHandle _encoder;
        private readonly PooledBuffer<byte> _inputBuffer;
        private readonly PooledBuffer<byte> _outputBuffer;
        private readonly bool _leaveOpen;
        private readonly IBrolib _brotliImpl;

        private int _bytesInInputBuffer = 0;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new instance of <see cref="BrotliCompressorStream"/>.
        /// </summary>
        /// <param name="baseStream">The stream to write brotli compressed data.</param>
        /// <param name="leaveOpen">If true, leave the inner stream open after disposal.</param>
        /// <param name="quality">The Brotli encoding quality, from 0 to 11.</param>
        /// <param name="window">The Brotli sliding window size, in bits, from 10 to 24.</param>
        /// <param name="brotliLibrary">The brotli library implementation to use, or null for the platform default.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if one of the encoding parameters is out of range.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the inner stream is null.</exception>
        /// <exception cref="BrotliException">Thrown if the native library encounters an error (or a required DLL is missing)</exception>
        public BrotliCompressorStream(Stream baseStream, bool leaveOpen = false, uint quality = 11, uint window = 22, IBrolib brotliLibrary = null)
        {
            if (baseStream == null)
            {
                throw new ArgumentNullException("baseStream");
            }

            if (!baseStream.CanWrite)
            {
                throw new ArgumentException("Output stream must be writable");
            }

            _baseStream = baseStream;
            _leaveOpen = leaveOpen;
            _brotliImpl = brotliLibrary ?? BrotliImplFactory.Singleton;
            _encoder = _brotliImpl.CreateEncoder();

            try
            {
                if (_encoder.IsInvalid)
                {
                    throw new BrotliException("Unable to create Brotli encoder instance; is brolib_x64.dll missing?");
                }

                SetQuality(quality);
                SetWindow(window);
                _inputBuffer = BufferPool<byte>.Rent();
                _outputBuffer = BufferPool<byte>.Rent();
            }
            catch (Exception)
            {
                // can usually happen if quality and window are out of range. If an exception gets thrown in the constructor,
                // this stream will be "half-constructed" and won't dispose normally, so we have to dispose of the encoder here.
                _encoder.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Set the compress quality from 0 to 11.
        /// </summary>
        /// <param name="quality">The compression quality.</param>
        public void SetQuality(uint quality)
        {
            if (quality > 11)
            {
                throw new ArgumentOutOfRangeException("quality", "The range of quality is 0 - 11");
            }

            _brotliImpl.EncoderSetParameter(_encoder, BrotliEncoderParameter.Quality, quality);
        }

        /// <summary>
        /// Set the compression window size, in bits, from 10 to 24.
        /// </summary>
        /// <param name="window">The window size</param>
        public void SetWindow(uint window)
        {
            if (window < 10 || window > 24)
            {
                throw new ArgumentOutOfRangeException("window", "The range of window is 10 - 24");
            }

            _brotliImpl.EncoderSetParameter(_encoder, BrotliEncoderParameter.LGWin, window);
        }

        /// <inheritdoc />
        public override bool CanRead => false;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => _baseStream.CanWrite;

        /// <inheritdoc />
        public override long Length => _baseStream.Length;

        /// <inheritdoc />
        public override long Position
        {
            get
            {
                return _baseStream.Length;
            }

            set
            {
                throw new InvalidOperationException();
            }
        }

        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(BrotliCompressorStream), "Brotli stream has been disposed");
            }

            await FlushBrotliStreamAsync(false).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override void Flush()
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(BrotliCompressorStream), "Brotli stream has been disposed");
            }

            FlushBrotliStream(false);
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Can't read on this stream");
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Can't read on this stream");
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        /// <inheritdoc />
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(BrotliCompressorStream), "Brotli stream has been disposed");
            }

            int inputBytesIngested = 0;
            int bytesInOutputBuffer = 0;
            while (inputBytesIngested < count)
            {
                CompressSingleBufferInternal(buffer, offset + inputBytesIngested, count - inputBytesIngested, ref bytesInOutputBuffer, ref inputBytesIngested);

                // Write any compressed data to output stream (have to do await outside of unsafe context, which is a little less efficient)
                if (bytesInOutputBuffer > 0)
                {
                    await _baseStream.WriteAsync(_outputBuffer.Buffer, 0, bytesInOutputBuffer).ConfigureAwait(false);
                    bytesInOutputBuffer = 0;
                }
            }
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(BrotliCompressorStream), "Brotli stream has been disposed");
            }

            int inputBytesIngested = 0;
            int bytesInOutputBuffer = 0;
            while (inputBytesIngested < count)
            {
                CompressSingleBufferInternal(buffer, offset + inputBytesIngested, count - inputBytesIngested, ref bytesInOutputBuffer, ref inputBytesIngested);

                // Write any compressed data to output stream
                if (bytesInOutputBuffer > 0)
                {
                    _baseStream.Write(_outputBuffer.Buffer, 0, bytesInOutputBuffer);
                    bytesInOutputBuffer = 0;
                }
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (disposing)
                {
                    FlushBrotliStream(true);

                    if (!_leaveOpen)
                    {
                        _baseStream.Dispose();
                    }

                    _inputBuffer.Dispose();
                    _outputBuffer.Dispose();
                    _encoder.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private unsafe void CompressSingleBufferInternal(byte[] writeBuffer, int offset, int count, ref int bytesInOutputBuffer, ref int inputBytesIngested)
        {
            // Fill input buffer
            int bytesToIngest = FastMath.Min(_inputBuffer.Length - _bytesInInputBuffer, count);
            if (bytesToIngest > 0)
            {
                ArrayExtensions.MemCopy(writeBuffer, offset, _inputBuffer.Buffer, _bytesInInputBuffer, bytesToIngest);
                _bytesInInputBuffer += bytesToIngest;
                inputBytesIngested += bytesToIngest;
            }

            // Compress
            ulong totalBytesOut;
            ulong inputByteCount = checked((ulong)_bytesInInputBuffer);
            ulong outputByteCount = checked((ulong)(_outputBuffer.Length - bytesInOutputBuffer));
            fixed (byte* ptrInput = _inputBuffer.Buffer, ptrOutput = _outputBuffer.Buffer)
            {
                IntPtr ptrNextInput = new IntPtr(ptrInput);
                IntPtr ptrNextOutput = new IntPtr(ptrOutput);

                if (!_brotliImpl.EncoderCompressStream(
                    _encoder,
                    BrotliEncoderOperation.Process,
                    ref inputByteCount,
                    ref ptrNextInput,
                    ref outputByteCount,
                    ref ptrNextOutput,
                    out totalBytesOut))
                {
                    throw new BrotliException($"Unable to compress Brotli stream");
                }
            }

            if (_brotliImpl.EncoderIsFinished(_encoder))
            {
                throw new BrotliException("Unexepected finish signal from Brotli encoder");
            }

            bytesInOutputBuffer = _outputBuffer.Length - checked((int)outputByteCount);
            int inputBytesRemaining = checked((int)inputByteCount);
            int inputBytesConsumed = _bytesInInputBuffer - inputBytesRemaining;
            if (inputBytesRemaining > 0)
            {
                // Shift input buffer left if not all input was consumed
                ArrayExtensions.MemMove(_inputBuffer.Buffer, _bytesInInputBuffer - inputBytesRemaining, 0, inputBytesRemaining);
                _bytesInInputBuffer = inputBytesRemaining;
            }
            else
            {
                _bytesInInputBuffer = 0;
            }
        }

        private async Task FlushBrotliStreamAsync(bool finished)
        {
            if (_brotliImpl.EncoderIsFinished(_encoder))
            {
                return;
            }

            int attempts = 0;
            BrotliEncoderOperation encoderOperation = finished ? BrotliEncoderOperation.Finish : BrotliEncoderOperation.Flush;
            bool encoderHasMoreOutput;
            do
            {
                if (attempts++ > 10)
                {
                    throw new BrotliException("Infinite loop detected during Brotli stream flush");
                }

                int bytesInOutputBuffer = 0;

                unsafe
                {
                    fixed (byte* ptrInput = _inputBuffer.Buffer, ptrOutput = _outputBuffer.Buffer)
                    {
                        ulong totalBytesOut;
                        ulong inputByteCount = checked((ulong)_bytesInInputBuffer);
                        ulong outputByteCount = checked((ulong)(_outputBuffer.Length - bytesInOutputBuffer));
                        IntPtr ptrNextInput = new IntPtr(ptrInput);
                        IntPtr ptrNextOutput = new IntPtr(ptrOutput);

                        if (!_brotliImpl.EncoderCompressStream(
                            _encoder,
                            encoderOperation,
                            ref inputByteCount,
                            ref ptrNextInput,
                            ref outputByteCount,
                            ref ptrNextOutput,
                            out totalBytesOut))
                        {
                            throw new BrotliException("Unable to flush Brotli stream");
                        }

                        encoderHasMoreOutput = inputByteCount > 0 || outputByteCount == 0;

                        bytesInOutputBuffer = _outputBuffer.Length - checked((int)outputByteCount);
                        int inputBytesRemaining = checked((int)inputByteCount);
                        int inputBytesConsumed = _bytesInInputBuffer - inputBytesRemaining;
                        if (inputBytesRemaining > 0)
                        {
                            // Shift input buffer left if not all input was consumed
                            ArrayExtensions.MemMove(_inputBuffer.Buffer, _bytesInInputBuffer - inputBytesRemaining, 0, inputBytesRemaining);
                            _bytesInInputBuffer = inputBytesRemaining;
                        }
                        else
                        {
                            _bytesInInputBuffer = 0;
                        }
                    }
                }

                // Write any compressed data to output stream
                if (bytesInOutputBuffer > 0)
                {
                    await _baseStream.WriteAsync(_outputBuffer.Buffer, 0, bytesInOutputBuffer).ConfigureAwait(false);
                    bytesInOutputBuffer = 0;
                }
            }
            while (encoderHasMoreOutput);

            if (_brotliImpl.EncoderIsFinished(_encoder) != finished)
            {
                throw new BrotliException("Unexepected finish signal from Brotli encoder");
            }
        }

        private unsafe void FlushBrotliStream(bool finished)
        {
            if (_brotliImpl.EncoderIsFinished(_encoder))
            {
                return;
            }

            int attempts = 0;
            BrotliEncoderOperation encoderOperation = finished ? BrotliEncoderOperation.Finish : BrotliEncoderOperation.Flush;
            bool encoderHasMoreOutput;

            fixed (byte* ptrInput = _inputBuffer.Buffer, ptrOutput = _outputBuffer.Buffer)
            {
                do
                {
                    if (attempts++ > 10)
                    {
                        throw new BrotliException("Infinite loop detected during Brotli stream flush");
                    }

                    int bytesInOutputBuffer = 0;
                    ulong totalBytesOut;
                    ulong inputByteCount = checked((ulong)_bytesInInputBuffer);
                    ulong outputByteCount = checked((ulong)(_outputBuffer.Length - bytesInOutputBuffer));
                    IntPtr ptrNextInput = new IntPtr(ptrInput);
                    IntPtr ptrNextOutput = new IntPtr(ptrOutput);

                    if (!_brotliImpl.EncoderCompressStream(
                        _encoder,
                        encoderOperation,
                        ref inputByteCount,
                        ref ptrNextInput,
                        ref outputByteCount,
                        ref ptrNextOutput,
                        out totalBytesOut))
                    {
                        throw new BrotliException("Unable to flush Brotli stream");
                    }

                    encoderHasMoreOutput = inputByteCount > 0 || outputByteCount == 0;

                    bytesInOutputBuffer = _outputBuffer.Length - checked((int)outputByteCount);
                    int inputBytesRemaining = checked((int)inputByteCount);
                    int inputBytesConsumed = _bytesInInputBuffer - inputBytesRemaining;
                    if (inputBytesRemaining > 0)
                    {
                        // Shift input buffer left if not all input was consumed
                        ArrayExtensions.MemMove(_inputBuffer.Buffer, _bytesInInputBuffer - inputBytesRemaining, 0, inputBytesRemaining);
                        _bytesInInputBuffer = inputBytesRemaining;
                    }
                    else
                    {
                        _bytesInInputBuffer = 0;
                    }

                    // Write any compressed data to output stream
                    if (bytesInOutputBuffer > 0)
                    {
                        _baseStream.Write(_outputBuffer.Buffer, 0, bytesInOutputBuffer);
                        bytesInOutputBuffer = 0;
                    }
                }
                while (encoderHasMoreOutput);

                if (_brotliImpl.EncoderIsFinished(_encoder) != finished)
                {
                    throw new BrotliException("Unexepected finish signal from Brotli encoder");
                }
            }
        }
    }
}