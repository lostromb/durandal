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
    /// A copy of <see cref="StringStream" /> which operates on <see cref="StringBuilder"/> objects.
    /// This lets you write the contents of a string builder directly to a stream without ever calling ToString() and making large allocations.
    /// </summary>
    public class StringBuilderReadStream : NonRealTimeStream
    {
        private const int BASE_BUFFER_SIZE = 4096;

        private readonly PooledBuffer<char> _charScratch;
        private readonly PooledBuffer<byte> _byteScratch;
        private readonly StringBuilder _sourceStringBuilder;
        private readonly int _sourceCharEnd;
        private readonly Encoding _encoding;
        private readonly Encoder _encoder;
        private readonly bool _maskNewline;
        private int? _totalByteLength;
        private int _bytesRead = 0;
        private int _sourceCharIdx;
        private int _bytesAvailableInScratch = 0;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new stream which reads bytes from a string in the given encoding.
        /// </summary>
        /// <param name="input">The string to read from</param>
        /// <param name="encoding">The encoding to use</param>
        /// <param name="maskNewline">If true, all newline \r \n characters in the input string buffer will be replaced with spaces on copy.</param>
        public StringBuilderReadStream(StringBuilder input, Encoding encoding, bool maskNewline = false)
            : this(input, 0, (input?.Length).GetValueOrDefault(0), encoding, maskNewline)
        {
        }

        /// <summary>
        /// Constructs a new stream which reads bytes from a string in the given encoding.
        /// </summary>
        /// <param name="input">The string to read from</param>
        /// <param name="charOffset">The initial char offset to use when reading the string</param>
        /// <param name="charCount">The total number of chars to read from the string</param>
        /// <param name="encoding">The encoding to use</param>
        /// <param name="maskNewline">If true, all newline \r \n characters in the input string buffer will be replaced with spaces on copy.</param>
        public StringBuilderReadStream(StringBuilder input, int charOffset, int charCount, Encoding encoding, bool maskNewline = false)
        {
            _sourceStringBuilder = input.AssertNonNull(nameof(input));

            int sourceStringLength = _sourceStringBuilder.Length;
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
            _byteScratch = BufferPool<byte>.Rent(encoding.GetMaxByteCount(BASE_BUFFER_SIZE));
            _maskNewline = maskNewline;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~StringBuilderReadStream()
        {
            Dispose(false);
        }
#endif

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                if (!_totalByteLength.HasValue)
                {
                    _totalByteLength = CalculateEncodedStringLength(_sourceStringBuilder, _encoding, _maskNewline);
                }

                return _totalByteLength.Value;
            }
        }

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

        public override void Flush()
        {
            throw new InvalidOperationException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new InvalidOperationException();
        }

        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new InvalidOperationException();
        }

        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Read(targetBuffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult<int>(Read(targetBuffer, offset, count));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesReturned = 0;
            while (bytesReturned < count)
            {
                if (_bytesAvailableInScratch == 0)
                {
                    if (_sourceCharIdx == _sourceCharEnd)
                    {
                        // Stream is done.
                        return bytesReturned;
                    }

                    // Fill our scratch byte block if needed
                    int charsUsed;
                    bool completed;
                    int charsCanCopyFromString = FastMath.Min(_charScratch.Length, _sourceCharEnd - _sourceCharIdx);
                    bool isLastBlock = _sourceCharIdx + charsCanCopyFromString == _sourceCharEnd;
                    _sourceStringBuilder.CopyTo(_sourceCharIdx, _charScratch.Buffer, 0, charsCanCopyFromString);

                    if (_maskNewline)
                    {
                        StringUtils.ReplaceNewlinesWithSpace(_charScratch.Buffer, 0, charsCanCopyFromString);
                    }

                    _encoder.Convert(
                        _charScratch.Buffer,
                        0,
                        charsCanCopyFromString,
                        _byteScratch.Buffer,
                        0,
                        _byteScratch.Buffer.Length,
                        isLastBlock,
                        out charsUsed,
                        out _bytesAvailableInScratch,
                        out completed);

                    _sourceCharIdx += charsUsed;
                }

                int bytesToCopyFromScratchToCaller = FastMath.Min(count - bytesReturned, _bytesAvailableInScratch);
                ArrayExtensions.MemCopy(_byteScratch.Buffer, 0, buffer, offset + bytesReturned, bytesToCopyFromScratchToCaller);
                bytesReturned += bytesToCopyFromScratchToCaller;
                _bytesRead += bytesToCopyFromScratchToCaller;

                if (bytesToCopyFromScratchToCaller < _bytesAvailableInScratch)
                {
                    // Shift scratch buffer left if this was a partial read
                    ArrayExtensions.MemMove(
                        _byteScratch.Buffer,
                        bytesToCopyFromScratchToCaller,
                        0,
                        _bytesAvailableInScratch - bytesToCopyFromScratchToCaller);
                }

                _bytesAvailableInScratch -= bytesToCopyFromScratchToCaller;
            }

            return bytesReturned;
        }

        private static int CalculateEncodedStringLength(StringBuilder builder, Encoding encoding, bool maskNewline)
        {
            int byteLength = 0;
            int totalChars = builder.Length;
            Encoder encoder = encoding.GetEncoder();

            using (PooledBuffer<char> charScratch = BufferPool<char>.Rent(BASE_BUFFER_SIZE))
            using (PooledBuffer<byte> byteSscratch = BufferPool<byte>.Rent(BASE_BUFFER_SIZE * 4))
            {
                int charsConsumed = 0;
                while (charsConsumed < totalChars)
                {
                    int charsUsed;
                    int bytesUsed;
                    bool completed;

                    int charsCanCopyFromString = FastMath.Min(charScratch.Length, totalChars - charsConsumed);
                    bool isLastBlock = charsConsumed + charsCanCopyFromString == totalChars;
                    builder.CopyTo(charsConsumed, charScratch.Buffer, 0, charsCanCopyFromString);

                    if (maskNewline)
                    {
                        // technically not necessary, but whatever
                        StringUtils.ReplaceNewlinesWithSpace(charScratch.Buffer, 0, charsCanCopyFromString);
                    }

                    encoder.Convert(
                        charScratch.Buffer,
                        0,
                        charsCanCopyFromString,
                        byteSscratch.Buffer,
                        0,
                        byteSscratch.Buffer.Length,
                        isLastBlock,
                        out charsUsed,
                        out bytesUsed,
                        out completed);

                    byteLength += bytesUsed;
                    charsConsumed += charsUsed;
                }
            }

            return byteLength;
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
    }
}
