using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Class which lets you read from a string as a stream
    /// of bytes in a specific encoding. Why does the runtime not provide
    /// anything like this?
    /// </summary>
    public class StringStream : Stream
    {
        private const int BASE_BUFFER_SIZE = 4096;

        private readonly PooledBuffer<char> _charScratch;
        private readonly PooledBuffer<byte> _byteScratch;
        private readonly string _sourceString;
        private readonly int _sourceCharEnd;
        private readonly Encoding _encoding;
        private readonly Encoder _encoder;
        private int? _totalByteLength;
        private int _bytesRead = 0;
        private int _sourceCharIdx;
        private int _scratchReadIdx = 0;
        private int _scratchTotalBytes = 0;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new stream which reads bytes from a string in the given encoding.
        /// </summary>
        /// <param name="input">The string to read from</param>
        /// <param name="encoding">The encoding to use</param>
        public StringStream(string input, Encoding encoding)
            : this(input, 0, (input?.Length).GetValueOrDefault(0), encoding)
        {
        }

        /// <summary>
        /// Constructs a new stream which reads bytes from a string in the given encoding.
        /// </summary>
        /// <param name="input">The string to read from</param>
        /// <param name="charOffset">The initial char offset to use when reading the string</param>
        /// <param name="charCount">The total number of chars to read from the string</param>
        /// <param name="encoding">The encoding to use</param>
        public StringStream(string input, int charOffset, int charCount, Encoding encoding)
        {
            _sourceString = input.AssertNonNull(nameof(input));

            int sourceStringLength = _sourceString.Length;
            if (charOffset < 0 || charOffset > sourceStringLength)
            {
                throw new ArgumentOutOfRangeException(nameof(charOffset));
            }

            if (charCount < 0 || charOffset + charCount > sourceStringLength)
            {
                throw new ArgumentOutOfRangeException(nameof(charCount));
            }

            _sourceCharIdx = charOffset;
            _sourceCharEnd = _sourceCharIdx + charCount;
            _encoder = encoding.AssertNonNull(nameof(encoding)).GetEncoder();
            _encoding = encoding;
            _charScratch = BufferPool<char>.Rent(BASE_BUFFER_SIZE);
            _byteScratch = BufferPool<byte>.Rent(_encoding.GetMaxByteCount(BASE_BUFFER_SIZE));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~StringStream()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length
        {
            get
            {
                if (!_totalByteLength.HasValue)
                {
                    _totalByteLength = _encoding.GetByteCount(_sourceString);
                }

                return _totalByteLength.Value;
            }
        }

        /// <inheritdoc/>
        public override long Position
        {
            get
            {
                return _bytesRead;
            }
            set
            {
                throw new InvalidOperationException();
            }
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            throw new InvalidOperationException();
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        /// <inheritdoc/>
        public override void WriteByte(byte value)
        {
            throw new InvalidOperationException();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesReturned = 0;
            while (bytesReturned < count)
            {
                if (!TryFillInternalBuffer())
                {
                    return bytesReturned;
                }

                int bytesToCopyFromScratchToCaller = FastMath.Min(count - bytesReturned, _scratchTotalBytes - _scratchReadIdx);
                ArrayExtensions.MemCopy(_byteScratch.Buffer, _scratchReadIdx, buffer, offset + bytesReturned, bytesToCopyFromScratchToCaller);
                bytesReturned += bytesToCopyFromScratchToCaller;
                _bytesRead += bytesToCopyFromScratchToCaller;
                _scratchReadIdx += bytesToCopyFromScratchToCaller;
            }

            return bytesReturned;
        }

        /// <inheritdoc/>
        public override int ReadByte()
        {
            if (!TryFillInternalBuffer())
            {
                return -1;
            }

            int returnVal = _byteScratch.Buffer[_scratchReadIdx++];
            _bytesRead++;
            return returnVal;
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
                    _charScratch?.Dispose();
                    _byteScratch?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Ensures that are there is valid data in the byteScratch buffer for reading from.
        /// </summary>
        /// <returns>True if there are more bytes to read from the string.</returns>
        private bool TryFillInternalBuffer()
        {
            if (_scratchReadIdx == _scratchTotalBytes)
            {
                if (_sourceCharIdx == _sourceCharEnd)
                {
                    // Stream is done.
                    return false;
                }

                // Fill our scratch byte block if needed
                int charsUsed;
                bool completed;
                int charsCanCopyFromString = FastMath.Min(_charScratch.Length, _sourceCharEnd - _sourceCharIdx);
                bool isLastBlock = _sourceCharIdx + charsCanCopyFromString == _sourceCharEnd;
                _sourceString.CopyTo(_sourceCharIdx, _charScratch.Buffer, 0, charsCanCopyFromString);
                _encoder.Convert(
                    _charScratch.Buffer,
                    0,
                    charsCanCopyFromString,
                    _byteScratch.Buffer,
                    0,
                    _byteScratch.Buffer.Length,
                    isLastBlock,
                    out charsUsed,
                    out _scratchTotalBytes,
                    out completed);

                _sourceCharIdx += charsUsed;
                _scratchReadIdx = 0;
            }

            return true;
        }
    }
}
