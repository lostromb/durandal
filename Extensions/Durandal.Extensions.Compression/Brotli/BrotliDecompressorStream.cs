namespace Durandal.Extensions.Compression.Brotli
{
    using Durandal.Common.Collections;
    using Durandal.Common.IO;
    using Durandal.Common.MathExt;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Performance-tuned implementation of a Brotli decompression stream, using custom P/Invoke bindings.
    /// </summary>
    public sealed class BrotliDecompressorStream : Stream
    {
        private readonly Stream baseStream;
        private readonly SafeHandle decoder;
        private readonly PooledBuffer<byte> inputBuffer;
        private readonly PooledBuffer<byte> outputBuffer;
        private readonly IBrolib brotliImpl;

        private bool innerStreamFinished = false;
        private bool brotliDecodeFinished = false;
        private long outputBytesProduced = 0;
        private int bytesInInputBuffer = 0;
        private int bytesInOutputBuffer = 0;
        private int disposed = 0;

        /// <summary>
        /// Constructs a new instance of <see cref="BrotliDecompressorStream"/>.
        /// </summary>
        /// <param name="baseStream">The stream to read brotli compressed data from.</param>
        /// <param name="brotliLibrary">The brotli library implementation to use, or null for the platform default.</param>
        /// <exception cref="ArgumentNullException">Thrown if the inner stream is null.</exception>
        /// <exception cref="BrotliException">Thrown if the native library encounters an error (or a required DLL is missing)</exception>
        public BrotliDecompressorStream(Stream baseStream, IBrolib brotliLibrary = null)
        {
            if (baseStream == null)
            {
                throw new ArgumentNullException("baseStream");
            }

            if (!baseStream.CanRead)
            {
                throw new ArgumentException("Input stream must be readable");
            }

            this.baseStream = baseStream;
            brotliImpl = brotliLibrary ?? BrotliImplFactory.Singleton;
            decoder = brotliImpl.CreateDecoder();
            if (decoder.IsInvalid)
            {
                throw new BrotliException("Unable to create Brotli encoder instance; is brolib_x64.dll missing?");
            }

            inputBuffer = BufferPool<byte>.Rent();
            outputBuffer = BufferPool<byte>.Rent();
        }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length
        {
            get
            {
                return outputBytesProduced;
            }
        }

        /// <inheritdoc />
        public override long Position
        {
            get
            {
                return outputBytesProduced;
            }

            set
            {
                throw new InvalidOperationException();
            }
        }

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException();
        }

        /// <inheritdoc />
        public override void Flush()
        {
            throw new InvalidOperationException();
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (disposed != 0)
            {
                throw new ObjectDisposedException(nameof(BrotliDecompressorStream), "Brotli stream has been disposed");
            }

            if (brotliDecodeFinished && bytesInOutputBuffer == 0)
            {
                return 0;
            }

            // By some miracle, do we just have the required output available in the output buffer?
            if (bytesInOutputBuffer >= count)
            {
                ArrayExtensions.MemCopy(outputBuffer.Buffer, 0, buffer, offset, count);
                if (bytesInOutputBuffer > count)
                {
                    ArrayExtensions.MemMove(outputBuffer.Buffer, count, 0, bytesInOutputBuffer - count);
                    bytesInOutputBuffer -= count;
                }
                else
                {
                    bytesInOutputBuffer = 0;
                }

                outputBytesProduced += count;
                return count;
            }

            int bytesRead = 0;

            while (bytesRead < count)
            {
                // this bit of logic is here so that we don't try and completely fill a buffer
                // of 256KB or whatever and decode it all if the caller just wants 16 bytes of data.
                // 3:1 is used here as a very lenient estimate for brotli compression ratios
                int estimatedBytesOfInputNeeded = FastMath.Max(1024, (count - bytesRead - bytesInOutputBuffer) / 3);

                // Try to fill input buffer
                if (!innerStreamFinished &&
                    bytesInInputBuffer < estimatedBytesOfInputNeeded &&
                    bytesInInputBuffer < inputBuffer.Length)
                {
                    int bytesShouldReadIntoInput = FastMath.Min(inputBuffer.Length, estimatedBytesOfInputNeeded) - bytesInInputBuffer;
                    int bytesReadFromBase = await baseStream.ReadAsync(inputBuffer.Buffer, bytesInInputBuffer, bytesShouldReadIntoInput).ConfigureAwait(false);
                    if (bytesReadFromBase == 0)
                    {
                        innerStreamFinished = true;
                    }
                    else
                    {
                        bytesInInputBuffer += bytesReadFromBase;
                    }
                }

                if (!brotliDecodeFinished)
                {
                    DecompressSingleBufferInternal();
                }

                // Copy decompressed data to caller
                if (bytesInOutputBuffer > 0)
                {
                    int amountCanCopyToCaller = FastMath.Min(bytesInOutputBuffer, count - bytesRead);
                    ArrayExtensions.MemCopy(outputBuffer.Buffer, 0, buffer, offset + bytesRead, amountCanCopyToCaller);
                    bytesRead += amountCanCopyToCaller;
                    outputBytesProduced += amountCanCopyToCaller;
                    int outputBytesRemaining = bytesInOutputBuffer - amountCanCopyToCaller;
                    if (outputBytesRemaining > 0)
                    {
                        // Shift output buffer left if not all output was consumed
                        ArrayExtensions.MemMove(outputBuffer.Buffer, amountCanCopyToCaller, 0, outputBytesRemaining);
                        bytesInOutputBuffer = outputBytesRemaining;
                    }
                    else
                    {
                        bytesInOutputBuffer = 0;
                    }
                }
                else if (brotliDecodeFinished) // && bytesInOutputBuffer == 0
                {
                    // We just returned the last bit of data we can.
                    return bytesRead;
                }
            }

            return bytesRead;
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (disposed != 0)
            {
                throw new ObjectDisposedException(nameof(BrotliDecompressorStream), "Brotli stream has been disposed");
            }

            if (brotliDecodeFinished && bytesInOutputBuffer == 0)
            {
                return 0;
            }

            // By some miracle, do we just have the required output available in the output buffer?
            if (bytesInOutputBuffer >= count)
            {
                ArrayExtensions.MemCopy(outputBuffer.Buffer, 0, buffer, offset, count);
                if (bytesInOutputBuffer > count)
                {
                    ArrayExtensions.MemMove(outputBuffer.Buffer, count, 0, bytesInOutputBuffer - count);
                    bytesInOutputBuffer -= count;
                }
                else
                {
                    bytesInOutputBuffer = 0;
                }

                outputBytesProduced += count;
                return count;
            }

            int bytesRead = 0;
            while (bytesRead < count)
            {
                // this bit of logic is here so that we don't try and completely fill a buffer
                // of 256KB or whatever and decode it all if the caller just wants 16 bytes of data.
                // 3:1 is used here as a very lenient estimate for brotli compression ratios
                int estimatedBytesOfInputNeeded = FastMath.Max(1024, (count - bytesRead - bytesInOutputBuffer) / 3);

                // Try to fill input buffer
                if (!innerStreamFinished &&
                    bytesInInputBuffer < estimatedBytesOfInputNeeded &&
                    bytesInInputBuffer < inputBuffer.Length)
                {
                    int bytesShouldReadIntoInput = FastMath.Min(inputBuffer.Length, estimatedBytesOfInputNeeded) - bytesInInputBuffer;
                    int bytesReadFromBase = baseStream.Read(inputBuffer.Buffer, bytesInInputBuffer, bytesShouldReadIntoInput);
                    if (bytesReadFromBase == 0)
                    {
                        innerStreamFinished = true;
                    }
                    else
                    {
                        bytesInInputBuffer += bytesReadFromBase;
                    }
                }

                if (!brotliDecodeFinished)
                {
                    DecompressSingleBufferInternal();
                }

                // Copy decompressed data to caller
                if (bytesInOutputBuffer > 0)
                {
                    int amountCanCopyToCaller = FastMath.Min(bytesInOutputBuffer, count - bytesRead);
                    ArrayExtensions.MemCopy(outputBuffer.Buffer, 0, buffer, offset + bytesRead, amountCanCopyToCaller);
                    bytesRead += amountCanCopyToCaller;
                    outputBytesProduced += amountCanCopyToCaller;
                    int outputBytesRemaining = bytesInOutputBuffer - amountCanCopyToCaller;
                    if (outputBytesRemaining > 0)
                    {
                        // Shift output buffer left if not all output was consumed
                        ArrayExtensions.MemMove(outputBuffer.Buffer, amountCanCopyToCaller, 0, outputBytesRemaining);
                        bytesInOutputBuffer = outputBytesRemaining;
                    }
                    else
                    {
                        bytesInOutputBuffer = 0;
                    }
                }
                else if (brotliDecodeFinished) // && bytesInOutputBuffer == 0
                {
                    // We just returned the last bit of data we can.
                    return bytesRead;
                }
            }

            return bytesRead;
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
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Can't write on this stream");
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Can't write on this stream");
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) != 0)
            {
                return;
            }

            try
            {
                if (disposing)
                {
                    baseStream.Dispose();
                    inputBuffer.Dispose();
                    outputBuffer.Dispose();
                    decoder.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private unsafe void DecompressSingleBufferInternal()
        {
            ulong totalBytesOut;
            ulong inputByteCount = checked((ulong)bytesInInputBuffer);
            ulong outputByteCount = checked((ulong)(outputBuffer.Length - bytesInOutputBuffer));
            fixed (byte* ptrInput = inputBuffer.Buffer, ptrOutput = outputBuffer.Buffer)
            {
                IntPtr ptrNextInput = new IntPtr(ptrInput);
                IntPtr ptrNextOutput = new IntPtr(ptrOutput + bytesInOutputBuffer);

                BrotliDecoderResult decodeResult = brotliImpl.DecoderDecompressStream(
                    decoder,
                    ref inputByteCount,
                    ref ptrNextInput,
                    ref outputByteCount,
                    ref ptrNextOutput,
                    out totalBytesOut);

                switch (decodeResult)
                {
                    case BrotliDecoderResult.Error:
                        string errorMessage = brotliImpl.DecoderGetErrorString(decoder);
                        throw new BrotliException($"Error while decoding Brotli stream: {errorMessage}");
                    case BrotliDecoderResult.Success:
                        brotliDecodeFinished = true;
                        break;
                    case BrotliDecoderResult.NeedsMoreInput:
                        if (innerStreamFinished)
                        {
                            throw new BrotliException("Brotli decoder did not report finished status before ending stream. Was the input stream truncated?");
                        }

                        break;
                }
            }

            int inputBytesRemaining = checked((int)inputByteCount);
            if (inputBytesRemaining > 0)
            {
                // Shift input buffer left if not all input was consumed
                ArrayExtensions.MemMove(inputBuffer.Buffer, bytesInInputBuffer - inputBytesRemaining, 0, inputBytesRemaining);
                bytesInInputBuffer = inputBytesRemaining;
            }
            else
            {
                bytesInInputBuffer = 0;
            }

            bytesInOutputBuffer += outputBuffer.Length - bytesInOutputBuffer - checked((int)outputByteCount);
        }
    }
}