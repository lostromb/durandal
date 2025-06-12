using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Drop-in replacement for <see cref="StreamWriter"/> for UTF8/ASCII encoding only.
    /// </summary>
    public sealed class Utf8StreamWriter : TextWriter
    {
        private const string HEX_DIGITS_LOWERCASE = "0123456789abcdef";
        private const float MAX_FLOAT = 16777216;

        /// <summary>
        /// The underlying stream to write encoded UTF8 to.
        /// </summary>
        private readonly Stream _stream;

        /// <summary>
        /// Whether to leave the underlying stream open
        /// </summary>
        private readonly bool _leaveOpen;

        /// <summary>
        /// Pooled buffer for output
        /// </summary>
        private readonly PooledBuffer<byte> _outputBuffer;

        /// <summary>
        /// Pooled buffer for formatting numbers
        /// </summary>
        private readonly PooledBuffer<char> _charBuffer;

        /// <summary>
        /// This is the size of the output buffer minus a safe zone of either 6 bytes or a whole vector of ASCII chars, whichever is greater
        /// </summary>
        private readonly int _outputBufferSafeMaxLength;

        /// <summary>
        /// Encoder used for handling complex Unicode surrogates and such
        /// </summary>
        private readonly Encoder _fallbackEncoder;

#if !NET6_0_OR_GREATER
        /// <summary>
        /// Tiny scratch buffer for single char encoding
        /// </summary>
        private readonly char[] charIn = new char[1];
#endif

        private int _bytesInOutputBuffer;
        private int _charBufferPos;

        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="Utf8StreamWriter"/>.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="leaveOpen">If true, leave the inner stream open after disposal.</param>
        public Utf8StreamWriter(Stream stream, bool leaveOpen = false)
        {
            _stream = stream.AssertNonNull(nameof(stream));
            _leaveOpen = leaveOpen;
            _outputBuffer = BufferPool<byte>.Rent(BufferPool<byte>.DEFAULT_BUFFER_SIZE);
            _charBuffer = BufferPool<char>.Rent(41);
            _fallbackEncoder = StringUtils.UTF8_WITHOUT_BOM.GetEncoder();
            _outputBufferSafeMaxLength = _outputBuffer.Length - 6;
            if (Vector.IsHardwareAccelerated)
            {
                _outputBufferSafeMaxLength = Math.Min(_outputBufferSafeMaxLength, _outputBuffer.Length - (StringUtils.UTF8_WITHOUT_BOM.GetMaxByteCount(Vector<byte>.Count)));
            }
        }

        /// <inheritdoc />
        public override Encoding Encoding
        {
            get
            {
                return Encoding.UTF8;
            }
        }

        /// <inheritdoc />
        public override void Flush()
        {
            FlushBuffer();
        }

        /// <inheritdoc />
        public override async Task FlushAsync()
        {
            await FlushBufferAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override void Write(int val)
        {
            WriteDec(val);
        }

        /// <inheritdoc />
        public override void Write(uint val)
        {
            WriteDec(val);
        }

        /// <inheritdoc />
        public override void Write(long val)
        {
            WriteDec(val);
        }

        /// <inheritdoc />
        public override void Write(ulong val)
        {
            WriteDec(val);
        }

        /// <inheritdoc />
        public override void Write(char[] buffer)
        {
            buffer.AssertNonNull(nameof(buffer));
            WriteInternal(buffer.AsSpan());
        }

        /// <inheritdoc />
        public override void Write(char value)
        {
            EncodeSingleCharSlow(value);
        }

        /// <inheritdoc />
        public override async Task WriteAsync(char value)
        {
            await EncodeSingleCharSlowAsync(value).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override void Write(char[] buffer, int offset, int count)
        {
            buffer.AssertNonNull(nameof(buffer));
            offset.AssertNonNegative(nameof(offset));
            count.AssertNonNegative(nameof(count));
            WriteInternal(buffer.AsSpan(offset, count));
        }

        /// <inheritdoc />
        public override async Task WriteAsync(char[] buffer, int index, int count)
        {
            buffer.AssertNonNull(nameof(buffer));
            index.AssertNonNegative(nameof(index));
            count.AssertNonNegative(nameof(count));
            await WriteInternalAsync(buffer.AsMemory(index, count)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override void Write(string val)
        {
            val.AssertNonNull(nameof(val));
            WriteInternal(val.AsSpan());
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string val)
        {
            val.AssertNonNull(nameof(val));
            await WriteInternalAsync(val.AsMemory()).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override void Write(float value)
        {
            if ((value >= 0) && (value <= MAX_FLOAT))
            {
                double intPart = Math.Truncate(value);

                double remain = value - intPart;

                if (remain < double.Epsilon)
                {
                    Write((long)intPart);
                    return;
                }
            }

            Write(value.ToString());
        }

        /// <inheritdoc />
        public override void WriteLine(string val)
        {
            val.AssertNonNull(nameof(val));
            WriteInternal(val.AsSpan());
            base.WriteLine();
        }

        /// <inheritdoc />
        public override async Task WriteLineAsync(string val)
        {
            val.AssertNonNull(nameof(val));
            await WriteInternalAsync(val.AsMemory()).ConfigureAwait(false);
            base.WriteLine();
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
                    Flush();

                    if (!_leaveOpen && _stream != null)
                    {
                        _stream.Dispose();
                    }

                    _outputBuffer.Dispose();
                    _charBuffer.Dispose();
                }
            }
            //catch (Exception)
            //{
            //}
            finally
            {
                base.Dispose(disposing);
            }
        }

        #region Implementation

        private void BufferChar(char ch)
        {
            _charBuffer.Buffer[_charBufferPos++] = ch;
        }

        private void ReverseWrite()
        {
            for (int i = _charBufferPos - 1; i >= 0; i--)
            {
                EncodeSingleCharSlow(_charBuffer.Buffer[i]);
            }

            _charBufferPos = 0;
        }

        /// <summary>
        /// Write signed integer
        /// </summary>
        /// <param name="val"></param>
        private void WriteDec(long val)
        {
            if (val < 0)
            {
                EncodeSingleCharSlow('-');

                if (val == long.MinValue)
                {
                    WriteDec(((ulong)1) << 63);
                    return;
                }

                val = -val;
            }

            do
            {
                BufferChar(HEX_DIGITS_LOWERCASE[(int)(val % 10)]);
                val /= 10;
            }
            while (val != 0);

            ReverseWrite();
        }

        /// <summary>
        /// Write unsigned integer
        /// </summary>
        /// <param name="val"></param>
        private void WriteDec(ulong val)
        {
            do
            {
                BufferChar(HEX_DIGITS_LOWERCASE[(int)(val % 10)]);

                val /= 10;
            }
            while (val != 0);

            ReverseWrite();
        }

        private void WriteInternal(ReadOnlySpan<char> input)
        {
            int charsEncoded = 0;
            int vectorEnd = 0;
            if (Vector.IsHardwareAccelerated)
            {
                vectorEnd = input.Length - (input.Length % Vector<byte>.Count); // Intentionally using Vector<byte> here because we operate on two int16 vectors at once
            }

            while (charsEncoded < input.Length)
            {
#if NET6_0_OR_GREATER
#if DEBUG
                if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
                if (Vector.IsHardwareAccelerated)
#endif
                {
                    Vector<ushort> compareVec = new Vector<ushort>(0x7F);
                    while (charsEncoded < vectorEnd && _bytesInOutputBuffer < _outputBufferSafeMaxLength)
                    {
                        Vector<ushort> low = new Vector<ushort>(MemoryMarshal.Cast<char, ushort>(input.Slice(charsEncoded)));
                        Vector<ushort> high = new Vector<ushort>(MemoryMarshal.Cast<char, ushort>(input.Slice(charsEncoded + Vector<ushort>.Count)));
                        if (Vector.LessThanAll(low, compareVec) &&
                            Vector.LessThanAll(high, compareVec))
                        {
                            // All chars are ASCII. Fast path!
                            Vector.Narrow(low, high).CopyTo(_outputBuffer.Buffer, _bytesInOutputBuffer);
                            charsEncoded += Vector<byte>.Count;
                            _bytesInOutputBuffer += Vector<byte>.Count;
                        }
                        else
                        {
                            // There's something mixed in here. Do the slow path.
                            _bytesInOutputBuffer += _fallbackEncoder.GetBytes(input.Slice(charsEncoded, Vector<byte>.Count), _outputBuffer.Buffer.AsSpan(_bytesInOutputBuffer), false);
                            charsEncoded += Vector<byte>.Count;
                        }
                    }
                }

                while (charsEncoded < input.Length && _bytesInOutputBuffer < _outputBufferSafeMaxLength)
                {
                    // because of vector padding this should always be at least 1
                    int charsToEncodeThisTime = Math.Min(input.Length - charsEncoded, (_outputBuffer.Length - _bytesInOutputBuffer) / 4);
                    _bytesInOutputBuffer += _fallbackEncoder.GetBytes(input.Slice(charsEncoded, charsToEncodeThisTime), _outputBuffer.Buffer.AsSpan(_bytesInOutputBuffer), false);
                    charsEncoded += charsToEncodeThisTime;
                }
#else
                while (charsEncoded < input.Length && _bytesInOutputBuffer < _outputBufferSafeMaxLength)
                {
                    Utf8EncodeWithoutBoundsCheck(input[charsEncoded++]);
                }
#endif // NET6_0_OR_GREATER

                // Flush if necessary
                if (_bytesInOutputBuffer >= _outputBufferSafeMaxLength)
                {
                    _stream.Write(_outputBuffer.Buffer, 0, _bytesInOutputBuffer);
                    _bytesInOutputBuffer = 0;

                    if (Vector.IsHardwareAccelerated)
                    {
                        // We may have been interrupted by the output buffer getting full during the residual loop and ended up
                        // having our start index misaligned to vector blocks. Recalculate the vector block end index to adjust.
                        vectorEnd = input.Length - ((input.Length - charsEncoded) % Vector<byte>.Count);
                    }
                }
            }
        }

        private async ValueTask WriteInternalAsync(ReadOnlyMemory<char> input)
        {
            int charsEncoded = 0;
            int vectorEnd = 0;
            if (Vector.IsHardwareAccelerated)
            {
                vectorEnd = input.Length - (input.Length % Vector<byte>.Count); // Intentionally using Vector<byte> here because we operate on two int16 vectors at once
            }

            while (charsEncoded < input.Length)
            {
#if NET6_0_OR_GREATER
#if DEBUG
                if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
                if (Vector.IsHardwareAccelerated)
#endif
                {
                    Vector<ushort> compareVec = new Vector<ushort>(0x7F);
                    while (charsEncoded < vectorEnd && _bytesInOutputBuffer < _outputBufferSafeMaxLength)
                    {
                        Vector<ushort> low = new Vector<ushort>(MemoryMarshal.Cast<char, ushort>(input.Span.Slice(charsEncoded)));
                        Vector<ushort> high = new Vector<ushort>(MemoryMarshal.Cast<char, ushort>(input.Span.Slice(charsEncoded + Vector<ushort>.Count)));
                        if (Vector.LessThanAll(low, compareVec) &&
                            Vector.LessThanAll(high, compareVec))
                        {
                            // All chars are ASCII. Fast path!
                            Vector.Narrow(low, high).CopyTo(_outputBuffer.Buffer, _bytesInOutputBuffer);
                            charsEncoded += Vector<byte>.Count;
                            _bytesInOutputBuffer += Vector<byte>.Count;
                        }
                        else
                        {
                            // There's something mixed in here. Do the slow path.
                            _bytesInOutputBuffer += _fallbackEncoder.GetBytes(input.Span.Slice(charsEncoded, Vector<byte>.Count), _outputBuffer.Buffer.AsSpan(_bytesInOutputBuffer), false);
                            charsEncoded += Vector<byte>.Count;
                        }
                    }
                }

                while (charsEncoded < input.Length && _bytesInOutputBuffer < _outputBufferSafeMaxLength)
                {
                    // because of vector padding this should always be at least 1
                    int charsToEncodeThisTime = Math.Min(input.Length - charsEncoded, (_outputBuffer.Length - _bytesInOutputBuffer) / 4);
                    _bytesInOutputBuffer += _fallbackEncoder.GetBytes(input.Span.Slice(charsEncoded, charsToEncodeThisTime), _outputBuffer.Buffer.AsSpan(_bytesInOutputBuffer), false);
                    charsEncoded += charsToEncodeThisTime;
                }
#else
                while (charsEncoded < input.Length && _bytesInOutputBuffer < _outputBufferSafeMaxLength)
                {
                    Utf8EncodeWithoutBoundsCheck(input.Span[charsEncoded++]);
                }
#endif // NET6_0_OR_GREATER

                // Flush if necessary
                if (_bytesInOutputBuffer >= _outputBufferSafeMaxLength)
                {
                    await _stream.WriteAsync(_outputBuffer.Buffer, 0, _bytesInOutputBuffer).ConfigureAwait(false);
                    _bytesInOutputBuffer = 0;

                    if (Vector.IsHardwareAccelerated)
                    {
                        // We may have been interrupted by the output buffer getting full during the residual loop and ended up
                        // having our start index misaligned to vector blocks. Recalculate the vector block end index to adjust.
                        vectorEnd = input.Length - ((input.Length - charsEncoded) % Vector<byte>.Count);
                    }
                }
            }
        }

        private void EncodeSingleCharSlow(char ch)
        {
            if (_bytesInOutputBuffer >= _outputBufferSafeMaxLength)
            {
                FlushBuffer();
            }

            Utf8EncodeWithoutBoundsCheck(ch);
        }

        private async ValueTask EncodeSingleCharSlowAsync(char ch)
        {
            if (_bytesInOutputBuffer >= _outputBufferSafeMaxLength)
            {
                await FlushBufferAsync().ConfigureAwait(false);
            }

            Utf8EncodeWithoutBoundsCheck(ch);
        }

        // This relies on there being at least 6 bytes available in the output buffer
        private void Utf8EncodeWithoutBoundsCheck(char c)
        {
            if (c < 0x7F)
            {
                // single byte
                _outputBuffer.Buffer[_bytesInOutputBuffer++] = (byte)c;
            }
            else if ((c & 0xf800) == 0)
            {
                // 2 bytes
                _outputBuffer.Buffer[_bytesInOutputBuffer++] = (byte)(0xc0 | (c >> 6));
                _outputBuffer.Buffer[_bytesInOutputBuffer++] = (byte)(0x80 | (c & 0x3f));
            }
            else
            {
                // It's something complicated. Fall back to the system decoder.
#if NET6_0_OR_GREATER
                Span<char> charScratch = stackalloc char[1];
                charScratch[0] = c;
                _bytesInOutputBuffer += _fallbackEncoder.GetBytes(charScratch, _outputBuffer.Buffer.AsSpan(_bytesInOutputBuffer), false);
#else
                charIn[0] = c;
                _bytesInOutputBuffer += _fallbackEncoder.GetBytes(charIn, 0, 1, _outputBuffer.Buffer, _bytesInOutputBuffer, false);
#endif
            }
        }

        /// <summary>
        /// Flush content in buffer to output stream
        /// </summary>
        private void FlushBuffer()
        {
            if (_bytesInOutputBuffer > 0)
            {
                _stream.Write(_outputBuffer.Buffer, 0, _bytesInOutputBuffer);
                _bytesInOutputBuffer = 0;
            }
        }

        /// <summary>
        /// Flush content in buffer to output stream
        /// </summary>
        private async Task FlushBufferAsync()
        {
            if (_bytesInOutputBuffer > 0)
            {
                await _stream.WriteAsync(_outputBuffer.Buffer, 0, _bytesInOutputBuffer).ConfigureAwait(false);
                _bytesInOutputBuffer = 0;
            }
        }

#endregion
    }
}
