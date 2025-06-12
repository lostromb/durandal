using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Implements a stream which reads ASCII-formatted base64 characters (i.e. IaE5BC39+f==) and decodes them
    /// into plain bytes. IMPORTANT: If you are writing to this stream, you must call FinishAsync() at the end to decode all the data!
    /// </summary>
    public class Base64AsciiDecodingStream : FinalizableStream
    {
        private const int BASE_BLOCK_SIZE = 8196;

        // Indicates whether we are reading data from the inner stream and decoding on read, or if someone is writing data to us and we decode before passing to inner stream
        private readonly StreamDirection _streamDirection;

        private readonly NonRealTimeStream _innerStream;

        private readonly PooledBuffer<byte> _inputBuffer;
        private readonly PooledBuffer<byte> _outputBuffer;

        private readonly bool _ownsInnerStream;

        private bool _finished = false;
        private bool _innerStreamFinished = false;
        private long _position = 0;
        private int _bytesInOutputBuffer = 0;
        private int _bytesInInputBuffer = 0;

        // Decoder state
        private uint _decode_block; // The current 24-bit block of data
        private int _base64CharsAbsolutePosition = 0; // Total number of bytes processed by decoder
        private int _base64CharsDecodedPosition = 0; // Total number of non-whitespace bytes processed by decoder
        private int _base64CharModulo = 0; // Current decoded position % 4, used to figure out the bitmask for the next decoded byte
        private int _paddingCharsFound = 0; // Number of padding chars encoded so far in the stream

        private int _disposed = 0;

        public Base64AsciiDecodingStream(Stream wrapperStream, StreamDirection direction, bool ownsInnerStream) :
            this(new NonRealTimeStreamWrapper(wrapperStream, ownsInnerStream), direction, ownsInnerStream)
        {
        }

        public Base64AsciiDecodingStream(NonRealTimeStream wrapperStream, StreamDirection direction, bool ownsInnerStream)
        {
            _innerStream = wrapperStream.AssertNonNull(nameof(wrapperStream));
            _streamDirection = direction;
            _ownsInnerStream = ownsInnerStream;
            if (_streamDirection == StreamDirection.Unknown)
            {
                throw new ArgumentException("Cannot pass an unknown stream direction");
            }

            if (_streamDirection == StreamDirection.Read && !_innerStream.CanRead)
            {
                throw new ArgumentException("StreamDirection is read, but the inner stream is not readable");
            }

            if (_streamDirection == StreamDirection.Write && !_innerStream.CanWrite)
            {
                throw new ArgumentException("StreamDirection is write, but the inner stream is not writeable");
            }

            // The sizes and alignments of these buffers are very specific, to make sure we only encode entire unpadded blocks at once
            _inputBuffer = BufferPool<byte>.Rent(4 * BASE_BLOCK_SIZE);
            _outputBuffer = BufferPool<byte>.Rent(3 * BASE_BLOCK_SIZE);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~Base64AsciiDecodingStream()
        {
            Dispose(false);
        }
#endif

        public override bool CanRead => _streamDirection == StreamDirection.Read;

        public override bool CanSeek => false;

        public override bool CanWrite => _streamDirection == StreamDirection.Write;

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override bool CanTimeout => _innerStream.CanTimeout;

        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                throw new InvalidOperationException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton);
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
            Write(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton);
        }

        public override void Flush()
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Base64AsciiDecodingStream));
            }

            if (_streamDirection != StreamDirection.Write)
            {
                throw new InvalidOperationException("Cannot flush a read stream");
            }

            _innerStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancelToken)
        {
            return FlushAsync(cancelToken, DefaultRealTimeProvider.Singleton);
        }

        public override Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Base64AsciiDecodingStream));
            }

            if (_streamDirection != StreamDirection.Write)
            {
                throw new InvalidOperationException("Cannot flush a read stream");
            }

            return _innerStream.FlushAsync(cancelToken, realTime);
        }

        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Base64AsciiDecodingStream));
            }

            if (_streamDirection != StreamDirection.Read)
            {
                throw new InvalidOperationException("Can't read from a non-readable stream");
            }

            int bytesProduced = 0;

            while (bytesProduced < count)
            {
                if (_innerStreamFinished)
                {
                    if (_bytesInInputBuffer > 0)
                    {
                        // Transform final block with padding if needed
                        _bytesInOutputBuffer = ConvertFromBase64Array(_inputBuffer.Buffer, 0, _bytesInInputBuffer, _outputBuffer.Buffer, 0, true);
                        _bytesInInputBuffer = 0;
                        AssertValidBase64FinalState();
                    }

                    if (_bytesInOutputBuffer > 0)
                    {
                        // We are just returning the final padded block before returning 0 for stream closed.
                        int bytesWeCanCopyFromFinishedBlock = FastMath.Min(count - bytesProduced, _bytesInOutputBuffer);
                        ArrayExtensions.MemCopy(_outputBuffer.Buffer, 0, targetBuffer, offset + bytesProduced, bytesWeCanCopyFromFinishedBlock);
                        int bytesRemainingInOutputBuffer = _bytesInOutputBuffer - bytesWeCanCopyFromFinishedBlock;
                        if (bytesRemainingInOutputBuffer > 0)
                        {
                            // shift output buffer left
                            ArrayExtensions.MemMove(_outputBuffer.Buffer, bytesWeCanCopyFromFinishedBlock, 0, bytesRemainingInOutputBuffer);
                        }

                        _position += bytesWeCanCopyFromFinishedBlock;
                        bytesProduced += bytesWeCanCopyFromFinishedBlock;
                        _bytesInOutputBuffer = bytesRemainingInOutputBuffer;
                    }

                    return bytesProduced;
                }
                else
                {
                    if (_bytesInOutputBuffer == 0)
                    {
                        // Read as much as we can into input buffer
                        int nextReadSize = _inputBuffer.Length - _bytesInInputBuffer;
                        int actualReadSize = _innerStream.Read(_inputBuffer.Buffer, _bytesInInputBuffer, nextReadSize, cancelToken, realTime);
                        if (actualReadSize > 0)
                        {
                            _bytesInInputBuffer += actualReadSize;
                            if (_bytesInInputBuffer == _inputBuffer.Length)
                            {
                                // Is input buffer full? Then convert a block
                                _bytesInOutputBuffer = ConvertFromBase64Array(_inputBuffer.Buffer, 0, _inputBuffer.Length, _outputBuffer.Buffer, 0, false);
                                _bytesInInputBuffer = 0;
                            }
                        }
                        else
                        {
                            // Inner stream has ended. This will cause us to skip this read path on the next loop.
                            _innerStreamFinished = true;
                        }
                    }

                    // Return what we can from the currently finished block to the caller
                    if (_bytesInOutputBuffer > 0)
                    {
                        int bytesWeCanCopyFromFinishedBlock = FastMath.Min(count - bytesProduced, _bytesInOutputBuffer);
                        ArrayExtensions.MemCopy(_outputBuffer.Buffer, 0, targetBuffer, offset + bytesProduced, bytesWeCanCopyFromFinishedBlock);
                        int bytesRemainingInOutputBuffer = _bytesInOutputBuffer - bytesWeCanCopyFromFinishedBlock;
                        if (bytesRemainingInOutputBuffer > 0)
                        {
                            // shift output buffer left
                            ArrayExtensions.MemMove(_outputBuffer.Buffer, bytesWeCanCopyFromFinishedBlock, 0, bytesRemainingInOutputBuffer);
                        }

                        _position += bytesWeCanCopyFromFinishedBlock;
                        bytesProduced += bytesWeCanCopyFromFinishedBlock;
                        _bytesInOutputBuffer = bytesRemainingInOutputBuffer;
                    }
                }
            }

            return bytesProduced;
        }

        public override async Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Base64AsciiDecodingStream));
            }

            if (_streamDirection != StreamDirection.Read)
            {
                throw new InvalidOperationException("Can't read from a non-readable stream");
            }

            int bytesProduced = 0;

            while (bytesProduced < count)
            {
                if (_innerStreamFinished)
                {
                    if (_bytesInInputBuffer > 0)
                    {
                        // Transform final block with padding if needed
                        _bytesInOutputBuffer = ConvertFromBase64Array(_inputBuffer.Buffer, 0, _bytesInInputBuffer, _outputBuffer.Buffer, 0, true);
                        _bytesInInputBuffer = 0;
                        AssertValidBase64FinalState();
                    }

                    if (_bytesInOutputBuffer > 0)
                    {
                        // We are just returning the final padded block before returning 0 for stream closed.
                        int bytesWeCanCopyFromFinishedBlock = FastMath.Min(count - bytesProduced, _bytesInOutputBuffer);
                        ArrayExtensions.MemCopy(_outputBuffer.Buffer, 0, targetBuffer, offset + bytesProduced, bytesWeCanCopyFromFinishedBlock);
                        int bytesRemainingInOutputBuffer = _bytesInOutputBuffer - bytesWeCanCopyFromFinishedBlock;
                        if (bytesRemainingInOutputBuffer > 0)
                        {
                            // shift output buffer left
                            ArrayExtensions.MemMove(_outputBuffer.Buffer, bytesWeCanCopyFromFinishedBlock, 0, bytesRemainingInOutputBuffer);
                        }

                        _position += bytesWeCanCopyFromFinishedBlock;
                        bytesProduced += bytesWeCanCopyFromFinishedBlock;
                        _bytesInOutputBuffer = bytesRemainingInOutputBuffer;
                    }

                    return bytesProduced;
                }
                else
                {
                    if (_bytesInOutputBuffer == 0)
                    {
                        // Read as much as we can into input buffer
                        int nextReadSize = _inputBuffer.Length - _bytesInInputBuffer;
                        int actualReadSize = await _innerStream.ReadAsync(_inputBuffer.Buffer, _bytesInInputBuffer, nextReadSize, cancelToken, realTime).ConfigureAwait(false);
                        if (actualReadSize > 0)
                        {
                            _bytesInInputBuffer += actualReadSize;
                            if (_bytesInInputBuffer == _inputBuffer.Length)
                            {
                                // Is input buffer full? Then convert a block
                                _bytesInOutputBuffer = ConvertFromBase64Array(_inputBuffer.Buffer, 0, _inputBuffer.Length, _outputBuffer.Buffer, 0, false);
                                _bytesInInputBuffer = 0;
                            }
                        }
                        else
                        {
                            // Inner stream has ended. This will cause us to skip this read path on the next loop.
                            _innerStreamFinished = true;
                        }
                    }

                    // Return what we can from the currently finished block to the caller
                    if (_bytesInOutputBuffer > 0)
                    {
                        int bytesWeCanCopyFromFinishedBlock = FastMath.Min(count - bytesProduced, _bytesInOutputBuffer);
                        ArrayExtensions.MemCopy(_outputBuffer.Buffer, 0, targetBuffer, offset + bytesProduced, bytesWeCanCopyFromFinishedBlock);
                        int bytesRemainingInOutputBuffer = _bytesInOutputBuffer - bytesWeCanCopyFromFinishedBlock;
                        if (bytesRemainingInOutputBuffer > 0)
                        {
                            // shift output buffer left
                            ArrayExtensions.MemMove(_outputBuffer.Buffer, bytesWeCanCopyFromFinishedBlock, 0, bytesRemainingInOutputBuffer);
                        }

                        _position += bytesWeCanCopyFromFinishedBlock;
                        bytesProduced += bytesWeCanCopyFromFinishedBlock;
                        _bytesInOutputBuffer = bytesRemainingInOutputBuffer;
                    }
                }
            }

            return bytesProduced;
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Base64AsciiDecodingStream));
            }

            if (_finished)
            {
                throw new InvalidOperationException("Can't write additional data after stream is finished");
            }

            if (_streamDirection != StreamDirection.Write)
            {
                throw new InvalidOperationException("Can't write to a non-writable stream");
            }

            int bytesReadFromCaller = 0;
            while (bytesReadFromCaller < count)
            {
                int blockCopyLength = FastMath.Min(count - bytesReadFromCaller, _inputBuffer.Length - _bytesInInputBuffer);
                ArrayExtensions.MemCopy(
                    sourceBuffer,
                    offset + bytesReadFromCaller,
                    _inputBuffer.Buffer,
                    _bytesInInputBuffer,
                    blockCopyLength);

                _bytesInInputBuffer += blockCopyLength;
                bytesReadFromCaller += blockCopyLength;

                // Can we convert an entire block?
                if (_bytesInInputBuffer == _inputBuffer.Length)
                {
                    int outputBytes = ConvertFromBase64Array(_inputBuffer.Buffer, 0, _inputBuffer.Length, _outputBuffer.Buffer, 0, false);
                    _innerStream.Write(_outputBuffer.Buffer, 0, outputBytes, cancelToken, realTime);
                    _bytesInInputBuffer = 0;
                    _position += outputBytes;
                }
            }
        }

        public override async Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Base64AsciiDecodingStream));
            }

            if (_finished)
            {
                throw new InvalidOperationException("Can't write additional data after stream is finished");
            }

            if (_streamDirection != StreamDirection.Write)
            {
                throw new InvalidOperationException("Can't write to a non-writable stream");
            }

            int bytesReadFromCaller = 0;
            while (bytesReadFromCaller < count)
            {
                int blockCopyLength = FastMath.Min(count - bytesReadFromCaller, _inputBuffer.Length - _bytesInInputBuffer);
                ArrayExtensions.MemCopy(
                    sourceBuffer,
                    offset + bytesReadFromCaller,
                    _inputBuffer.Buffer,
                    _bytesInInputBuffer,
                    blockCopyLength);

                _bytesInInputBuffer += blockCopyLength;
                bytesReadFromCaller += blockCopyLength;

                // Can we convert an entire block?
                if (_bytesInInputBuffer == _inputBuffer.Length)
                {
                    int outputBytes = ConvertFromBase64Array(_inputBuffer.Buffer, 0, _inputBuffer.Length, _outputBuffer.Buffer, 0, false);
                    await _innerStream.WriteAsync(_outputBuffer.Buffer, 0, outputBytes, cancelToken, realTime).ConfigureAwait(false);
                    _bytesInInputBuffer = 0;
                    _position += outputBytes;
                }
            }
        }

        public override void Finish(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Base64AsciiDecodingStream));
            }

            if (_finished)
            {
                throw new InvalidOperationException("Can't finish a stream more than once");
            }

            if (_streamDirection != StreamDirection.Write)
            {
                throw new InvalidOperationException("Can't finish a non-writable stream");
            }

            _finished = true;

            // Actually finish the last block and add padding
            if (_bytesInInputBuffer > 0)
            {
                int outputBytes = ConvertFromBase64Array(_inputBuffer.Buffer, 0, _bytesInInputBuffer, _outputBuffer.Buffer, 0, true);
                _innerStream.Write(_outputBuffer.Buffer, 0, outputBytes, cancelToken, realTime);
                _position += outputBytes;
                AssertValidBase64FinalState();
            }
        }

        public override async Task FinishAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Base64AsciiDecodingStream));
            }

            if (_finished)
            {
                throw new InvalidOperationException("Can't finish a stream more than once");
            }

            if (_streamDirection != StreamDirection.Write)
            {
                throw new InvalidOperationException("Can't finish a non-writable stream");
            }

            _finished = true;

            // Actually finish the last block and add padding
            if (_bytesInInputBuffer > 0)
            {
                int outputBytes = ConvertFromBase64Array(_inputBuffer.Buffer, 0, _bytesInInputBuffer, _outputBuffer.Buffer, 0, true);
                await _innerStream.WriteAsync(_outputBuffer.Buffer, 0, outputBytes, cancelToken, realTime).ConfigureAwait(false);
                _position += outputBytes;
                AssertValidBase64FinalState();
            }
        }

        /// <summary>
        /// Throws an exception if the decoder state is invalid at the end of a base64 data stream.
        /// This should only be called after all input bytes are processed.
        /// </summary>
        private void AssertValidBase64FinalState()
        {
            if (_base64CharModulo != 0)
            {
                throw new FormatException(string.Format("Base64 data is invalid: Input length not a multiple of 4. Absolute pos: {0} Decoded pos: {1}", _base64CharsAbsolutePosition, _base64CharsDecodedPosition));
            }

            // This exception gets thrown at decode time so we don't actually need to check it here
            //if (_paddingCharsFound > 2)
            //{
            //    throw new FormatException("Base64 data is invalid: More than 2 padding chars encountered");
            //}
        }

        private static void DetermineAsciiCharClass(byte curByte, out bool byteIsValid, out bool byteIsWhitespace)
        {
            // Bisect to find what char class it is in
            if (curByte <= 57)
            {
                if (curByte <= 47)
                {
                    if (curByte == 43 || // +
                        curByte == 47) // /
                    {
                        byteIsValid = true;
                        byteIsWhitespace = false;
                    }
                    else if (curByte == 9 || // TAB
                        curByte == 10 || // LF
                        curByte == 13 || // CR
                        curByte == 32) // SPACE
                    {
                        byteIsValid = true;
                        byteIsWhitespace = true;
                    }
                    else
                    {
                        byteIsValid = false;
                        byteIsWhitespace = false;
                    }
                }
                else
                {
                    byteIsValid = true; // It is in the 0-9 character class
                    byteIsWhitespace = false;
                }
            }
            else
            {
                if (curByte <= 90)
                {
                    byteIsValid = curByte >= 65 || curByte == 61; // It is in the A-Z character class, or it is =
                    byteIsWhitespace = false;
                }
                else
                {
                    byteIsValid = curByte >= 97 && curByte <= 122; // It is in the a-z character class
                    byteIsWhitespace = false;
                }
            }
        }

        /// <summary>
        /// Decodes base64 string data (interpreted as ASCII bytes) to raw bytes.
        /// </summary>
        /// <param name="inAscii">The buffer containing input ASCII data. Any bytes outside of ASCII range will throw an exception.</param>
        /// <param name="inAsciiOffset">Offset when reading from input</param>
        /// <param name="length">Number of ASCII chars to process</param>
        /// <param name="outData">Buffer for output data. Must be at least 3/4 as long as input length</param>
        /// <param name="outDataOffset">Offset when writing to output buffer</param>
        /// <param name="isFinalBlock">Whether this is the final block of the input stream.</param>
        /// <returns>The total number of output bytes generated</returns>
        private int ConvertFromBase64Array(byte[] inAscii, int inAsciiOffset, int length, byte[] outData, int outDataOffset, bool isFinalBlock)
        {
            // This function doesn't have parity with regards to whitespace handling, so I don't think we can use it yet...
            //int bytesConsumed, bytesWritten;
            //var operationStatus = Base64.DecodeFromUtf8(inAscii.AsSpan(inAsciiOffset, length), outData.AsSpan(outDataOffset), out bytesConsumed, out bytesWritten, isFinalBlock);
            //return bytesWritten;

            int bytesProduced = 0;
            int outPtr = outDataOffset;

            for (int inPtr = inAsciiOffset; inPtr < inAsciiOffset + length; inPtr++)
            {
                // Inspect the current byte
                byte curByte = inAscii[inPtr];
                bool byteIsValid, byteIsWhitespace;
                DetermineAsciiCharClass(curByte, out byteIsValid, out byteIsWhitespace);

                if (!byteIsValid)
                {
                    throw new FormatException(string.Format("Invalid character found in base64 decoding stream: {0:X2}, position {1}", curByte, _base64CharsAbsolutePosition));
                }

                _base64CharsAbsolutePosition++;
                if (byteIsWhitespace)
                {
                    continue;
                }

                uint decodedValue = BASE64_ASCII_DECODING_TABLE[curByte];
#if DEBUG
                if (decodedValue > PADDING_CHAR)
                {
                    throw new Exception("Base64 decoding table was invalid: mapped byte " + curByte + " to " + decodedValue);
                }
#endif

                if (decodedValue == PADDING_CHAR)
                {
                    _paddingCharsFound++;
                    if (_paddingCharsFound > 2)
                    {
                        throw new FormatException(string.Format("Too many padding chars found in Base64 string. Pos: {0}", _base64CharsAbsolutePosition));
                    }

                    if (_base64CharModulo < 2)
                    {
                        throw new FormatException(string.Format("Padding char at invalid position found in Base64 string. Pos: {0}", _base64CharsAbsolutePosition));
                    }

                    // If we just finished a block of 4 input bytes with padding, we can produce 1-2 output bytes
                    _base64CharsDecodedPosition++;
                    _base64CharModulo = _base64CharsDecodedPosition % 4;

                    if (_base64CharModulo == 0)
                    {
                        outData[outPtr++] = (byte)((_decode_block >> 16) & 0xFF);
                        bytesProduced++;

                        if (_paddingCharsFound == 1)
                        {
                            outData[outPtr++] = (byte)((_decode_block >> 8) & 0xFF);
                            bytesProduced++;
                        }
                    }
                }
                else
                {
                    if (_paddingCharsFound > 0)
                    {
                        throw new FormatException(string.Format("Extra base64 char data found after padding. Pos: {0}", _base64CharsAbsolutePosition));
                    }

                    // Apply the decoded byte to our current bit map
                    switch (_base64CharModulo)
                    {
                        case 0:
                            _decode_block = decodedValue << 18;
                            break;
                        case 1:
                            _decode_block |= (decodedValue & 0x3F) << 12;
                            break;
                        case 2:
                            _decode_block |= (decodedValue & 0x3F) << 6;
                            break;
                        case 3:
                            _decode_block |= (decodedValue & 0x3F);
                            break;
                    }

                    _base64CharsDecodedPosition++;
                    _base64CharModulo = _base64CharsDecodedPosition % 4;

                    // If we just finished a block of 4 input bytes, we can produce 3 output bytes
                    if (_base64CharModulo == 0)
                    {
                        outData[outPtr++] = (byte)((_decode_block >> 16) & 0xFF);
                        outData[outPtr++] = (byte)((_decode_block >> 8) & 0xFF);
                        outData[outPtr++] = (byte)(_decode_block & 0xFF);
                        bytesProduced += 3;
                    }
                }
            }

            return bytesProduced;
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
                    _inputBuffer?.Dispose();
                    _outputBuffer?.Dispose();

                    if (_ownsInnerStream)
                    {
                        _innerStream?.Dispose();
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private const int PADDING_CHAR = 64;
        #region Big long table
        private static ReadOnlySpan<byte> BASE64_ASCII_DECODING_TABLE => new byte[]
            {
                255, // NULL
                255, // SOH
                255, // STX
                255, // ETX
                255, // EOT
                255, // ENQ
                255, // ACK
                255, // BEL
                255, // BS
                255, // TAB
                255, // LF
                255, // VT
                255, // FF
                255, // CR
                255, // SO
                255, // SI
                255, // DLE
                255, // DC1
                255, // DC2
                255, // DC3
                255, // DC4
                255, // NAK
                255, // SYN
                255, // ETB
                255, // CAN
                255, // EM
                255, // SUB
                255, // ESC
                255, // FS
                255, // GS
                255, // RS
                255, // US
                255, // SPACE
                255, // !
                255, // "
                255, // #
                255, // $
                255, // %
                255, // &
                255, // '
                255, // (
                255, // )
                255, // *
                62, // +
                255, // ,
                255, // -
                255, // .
                63, // /
                52, // 0
                53, // 1
                54, // 2
                55, // 3
                56, // 4
                57, // 5
                58, // 6
                59, // 7
                60, // 8
                61, // 9
                255, // :
                255, // ;
                255, // <
                PADDING_CHAR, // =
                255, // >
                255, // ?
                255, // @
                0, // A
                1, // B
                2, // C
                3, // D
                4, // E
                5, // F
                6, // G
                7, // H
                8, // I
                9, // J
                10, // K
                11, // L
                12, // M
                13, // N
                14, // O
                15, // P
                16, // Q
                17, // R
                18, // S
                19, // T
                20, // U
                21, // V
                22, // W
                23, // X
                24, // Y
                25, // Z
                255, // [
                255, // \
                255, // ]
                255, // ^
                255, // _
                255, // `
                26, // a
                27, // b
                28, // c
                29, // d
                30, // e
                31, // f
                32, // g
                33, // h
                34, // i
                35, // j
                36, // k
                37, // l
                38, // m
                39, // n
                40, // o
                41, // p
                42, // q
                43, // r
                44, // s
                45, // t
                46, // u
                47, // v
                48, // w
                49, // x
                50, // y
                51, // z
            };
        #endregion
    }
}
