using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Implements a stream which reads arbitrary data and encodes it into a stream of base64 ASCII bytes.
    /// IMPORTANT: To properly get all of the data, you must call FinishAsync() at the end!
    /// </summary>
    public class Base64AsciiEncodingStream : FinalizableStream
    {
        private const int BASE_BLOCK_SIZE = 8196;
        private static readonly byte[] BASE64_ASCII_ENCODING_TABLE =
            {
                // A - Z
                65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90,
                // a - z
                97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122,
                // 0 - 9
                48, 49, 50, 51, 52, 53, 54, 55, 56, 57,
                // +
                43,
                // /
                47,
                // =
                61
            };

        // Indicates whether we are reading data from the inner stream and encoding on read, or if someone is writing data to us and we encode before passing to inner stream
        private readonly StreamDirection _streamDirection;

        private readonly NonRealTimeStream _innerStream;

        private readonly PooledBuffer<byte> _inputBuffer;
        private readonly PooledBuffer<byte> _outputByteBuffer;
        private readonly PooledBuffer<char> _outputCharBuffer;

        // Keep these values cached because we access them frequently
        private readonly byte[] _inputBufferRaw;
        private readonly byte[] _outputByteBufferRaw;
        private readonly int _inputBufferLength;
        private readonly int _outputBufferLength;

        private readonly bool _ownsInnerStream;

        private bool _finished = false;
        private bool _innerStreamFinished = false;
        private long _position = 0;
        private int _bytesInOutputBuffer = 0;
        private int _bytesInInputBuffer = 0;
        private int _disposed = 0;

        public Base64AsciiEncodingStream(Stream wrapperStream, StreamDirection direction, bool ownsInnerStream) :
            this (new NonRealTimeStreamWrapper(wrapperStream, ownsInnerStream), direction, ownsInnerStream)
        {
        }

        public Base64AsciiEncodingStream(NonRealTimeStream wrapperStream, StreamDirection direction, bool ownsInnerStream)
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
            _inputBuffer = BufferPool<byte>.Rent(3 * BASE_BLOCK_SIZE);
            _outputByteBuffer = BufferPool<byte>.Rent(4 * BASE_BLOCK_SIZE);
            _outputCharBuffer = BufferPool<char>.Rent(4 * BASE_BLOCK_SIZE);
            _inputBufferRaw = _inputBuffer.Buffer;
            _outputByteBufferRaw = _outputByteBuffer.Buffer;
            _inputBufferLength = _inputBuffer.Length;
            _outputBufferLength = _outputByteBuffer.Length;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~Base64AsciiEncodingStream()
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
                throw new ObjectDisposedException(nameof(Base64AsciiEncodingStream));
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
                throw new ObjectDisposedException(nameof(Base64AsciiEncodingStream));
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
                throw new ObjectDisposedException(nameof(Base64AsciiEncodingStream));
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
                        _bytesInOutputBuffer = ConvertToBase64Array(_inputBufferRaw, 0, _bytesInInputBuffer, _outputByteBufferRaw, 0, true);
                        _bytesInInputBuffer = 0;
                    }

                    if (_bytesInOutputBuffer > 0)
                    {
                        // We are just returning the final padded block before returning 0 for stream closed.
                        int bytesWeCanCopyFromFinishedBlock = FastMath.Min(count - bytesProduced, _bytesInOutputBuffer);
                        ArrayExtensions.MemCopy(_outputByteBufferRaw, 0, targetBuffer, offset + bytesProduced, bytesWeCanCopyFromFinishedBlock);
                        int bytesRemainingInOutputBuffer = _bytesInOutputBuffer - bytesWeCanCopyFromFinishedBlock;
                        if (bytesRemainingInOutputBuffer > 0)
                        {
                            // shift output buffer left
                            ArrayExtensions.MemMove(_outputByteBufferRaw, bytesWeCanCopyFromFinishedBlock, 0, bytesRemainingInOutputBuffer);
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
                        int nextReadSize = _inputBufferLength - _bytesInInputBuffer;
                        int actualReadSize = _innerStream.Read(_inputBufferRaw, _bytesInInputBuffer, nextReadSize, cancelToken, realTime);
                        if (actualReadSize > 0)
                        {
                            _bytesInInputBuffer += actualReadSize;
                            if (_bytesInInputBuffer == _inputBufferLength)
                            {
                                // Is input buffer full? Then convert a block
                                _bytesInOutputBuffer = ConvertToBase64Array(_inputBufferRaw, 0, _inputBufferLength, _outputByteBufferRaw, 0, false);
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
                        ArrayExtensions.MemCopy(_outputByteBufferRaw, 0, targetBuffer, offset + bytesProduced, bytesWeCanCopyFromFinishedBlock);
                        int bytesRemainingInOutputBuffer = _bytesInOutputBuffer - bytesWeCanCopyFromFinishedBlock;
                        if (bytesRemainingInOutputBuffer > 0)
                        {
                            // shift output buffer left
                            ArrayExtensions.MemMove(_outputByteBufferRaw, bytesWeCanCopyFromFinishedBlock, 0, bytesRemainingInOutputBuffer);
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
                throw new ObjectDisposedException(nameof(Base64AsciiEncodingStream));
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
                        _bytesInOutputBuffer = ConvertToBase64Array(_inputBufferRaw, 0, _bytesInInputBuffer, _outputByteBufferRaw, 0, true);
                        _bytesInInputBuffer = 0;
                    }

                    if (_bytesInOutputBuffer > 0)
                    {
                        // We are just returning the final padded block before returning 0 for stream closed.
                        int bytesWeCanCopyFromFinishedBlock = FastMath.Min(count - bytesProduced, _bytesInOutputBuffer);
                        ArrayExtensions.MemCopy(_outputByteBufferRaw, 0, targetBuffer, offset + bytesProduced, bytesWeCanCopyFromFinishedBlock);
                        int bytesRemainingInOutputBuffer = _bytesInOutputBuffer - bytesWeCanCopyFromFinishedBlock;
                        if (bytesRemainingInOutputBuffer > 0)
                        {
                            // shift output buffer left
                            ArrayExtensions.MemMove(_outputByteBufferRaw, bytesWeCanCopyFromFinishedBlock, 0, bytesRemainingInOutputBuffer);
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
                        int nextReadSize = _inputBufferLength - _bytesInInputBuffer;
                        int actualReadSize = await _innerStream.ReadAsync(_inputBufferRaw, _bytesInInputBuffer, nextReadSize, cancelToken, realTime).ConfigureAwait(false);
                        if (actualReadSize > 0)
                        {
                            _bytesInInputBuffer += actualReadSize;
                            if (_bytesInInputBuffer == _inputBufferLength)
                            {
                                // Is input buffer full? Then convert a block
                                _bytesInOutputBuffer = ConvertToBase64Array(_inputBufferRaw, 0, _inputBufferLength, _outputByteBufferRaw, 0, false);
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
                        ArrayExtensions.MemCopy(_outputByteBufferRaw, 0, targetBuffer, offset + bytesProduced, bytesWeCanCopyFromFinishedBlock);
                        int bytesRemainingInOutputBuffer = _bytesInOutputBuffer - bytesWeCanCopyFromFinishedBlock;
                        if (bytesRemainingInOutputBuffer > 0)
                        {
                            // shift output buffer left
                            ArrayExtensions.MemMove(_outputByteBufferRaw, bytesWeCanCopyFromFinishedBlock, 0, bytesRemainingInOutputBuffer);
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
                throw new ObjectDisposedException(nameof(Base64AsciiEncodingStream));
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
                int blockCopyLength = FastMath.Min(count - bytesReadFromCaller, _inputBufferLength - _bytesInInputBuffer);

                // Check if the caller is giving us enough to satisfy an entire block at once. If so, no need to copy to intermediate buffer
                if (blockCopyLength == _inputBufferLength)
                {
                    int outputBytes = ConvertToBase64Array(sourceBuffer, offset + bytesReadFromCaller, _inputBufferLength, _outputByteBufferRaw, 0, false);
                    _innerStream.Write(_outputByteBufferRaw, 0, outputBytes, cancelToken, realTime);
                    _position += _outputBufferLength;
                    // _bytesInInputBuffer should already be zero so no need to touch it
                }
                else
                {
                    ArrayExtensions.MemCopy(
                        sourceBuffer,
                        offset + bytesReadFromCaller,
                        _inputBufferRaw,
                        _bytesInInputBuffer,
                        blockCopyLength);

                    _bytesInInputBuffer += blockCopyLength;

                    // Can we convert an entire block?
                    if (_bytesInInputBuffer == _inputBufferLength)
                    {
                        int outputBytes = ConvertToBase64Array(_inputBufferRaw, 0, _inputBufferLength, _outputByteBufferRaw, 0, false);
                        _innerStream.Write(_outputByteBufferRaw, 0, outputBytes, cancelToken, realTime);
                        _bytesInInputBuffer = 0;
                        _position += _outputBufferLength;
                    }
                }

                bytesReadFromCaller += blockCopyLength;
            }
        }

        public override async Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Base64AsciiEncodingStream));
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
                int blockCopyLength = FastMath.Min(count - bytesReadFromCaller, _inputBufferLength - _bytesInInputBuffer);

                // Check if the caller is giving us enough to satisfy an entire block at once. If so, no need to copy to intermediate buffer
                if (blockCopyLength == _inputBufferLength)
                {
                    int outputBytes = ConvertToBase64Array(sourceBuffer, offset + bytesReadFromCaller, _inputBufferLength, _outputByteBufferRaw, 0, false);
                    await _innerStream.WriteAsync(_outputByteBufferRaw, 0, outputBytes, cancelToken, realTime).ConfigureAwait(false);
                    _position += _outputBufferLength;
                    // _bytesInInputBuffer should already be zero so no need to touch it
                }
                else
                {
                    ArrayExtensions.MemCopy(
                        sourceBuffer,
                        offset + bytesReadFromCaller,
                        _inputBufferRaw,
                        _bytesInInputBuffer,
                        blockCopyLength);

                    _bytesInInputBuffer += blockCopyLength;

                    // Can we convert an entire block?
                    if (_bytesInInputBuffer == _inputBufferLength)
                    {
                        int outputBytes = ConvertToBase64Array(_inputBufferRaw, 0, _inputBufferLength, _outputByteBufferRaw, 0, false);
                        await _innerStream.WriteAsync(_outputByteBufferRaw, 0, outputBytes, cancelToken, realTime).ConfigureAwait(false);
                        _bytesInInputBuffer = 0;
                        _position += _outputBufferLength;
                    }
                }

                bytesReadFromCaller += blockCopyLength;
            }
        }

        public override void Finish(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Base64AsciiEncodingStream));
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
                int outputBytes = ConvertToBase64Array(_inputBufferRaw, 0, _bytesInInputBuffer, _outputByteBufferRaw, 0, true);
                _innerStream.Write(_outputByteBufferRaw, 0, outputBytes, cancelToken, realTime);
                _position += outputBytes;
            }
        }

        public override async Task FinishAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Base64AsciiEncodingStream));
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
                int outputBytes = ConvertToBase64Array(_inputBufferRaw, 0, _bytesInInputBuffer, _outputByteBufferRaw, 0, true);
                await _innerStream.WriteAsync(_outputByteBufferRaw, 0, outputBytes, cancelToken, realTime).ConfigureAwait(false);
                _position += outputBytes;
            }
        }

        private int ConvertToBase64Array(byte[] inData, int inDataOffset, int length, byte[] outAscii, int outAsciiOffset, bool isFinalBlock)
        {
            int bytesConsumed, bytesWritten;
            var operationStatus = Base64.EncodeToUtf8(inData.AsSpan(inDataOffset, length), outAscii.AsSpan(outAsciiOffset), out bytesConsumed, out bytesWritten, isFinalBlock);
            return bytesWritten;

            //int charsConverted = Convert.ToBase64CharArray(inData, inDataOffset, length, _outputCharBuffer.Buffer, 0);
            //return StringUtils.ASCII_ENCODING.GetBytes(_outputCharBuffer.Buffer, 0, charsConverted, outAscii, outAsciiOffset);
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
                    _outputByteBuffer?.Dispose();
                    _outputCharBuffer?.Dispose();

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

        ///// <summary>
        ///// Adapted from C# reference source https://source.dot.net/#System.Private.CoreLib/Convert.cs,440a570fbff23b16.
        ///// Will produce padded bytes unless input length is exactly a multiple of 3.
        ///// This code is obsolete as the modern C# runtime outperforms even my most optimized SIMD code, let alone this
        ///// </summary>
        ///// <param name="inData"></param>
        ///// <param name="inDataOffset"></param>
        ///// <param name="length"></param>
        ///// <param name="outAscii"></param>
        ///// <param name="outAsciiOffset"></param>
        ///// <returns>The number of output bytes produced</returns>
        //private static int ConvertToBase64ArrayImpl(byte[] inData, int inDataOffset, int length, byte[] outAscii, int outAsciiOffset)
        //{
        //    int lengthmod3 = length % 3;
        //    int calcLength = inDataOffset + (length - lengthmod3);
        //    int j = outAsciiOffset;
        //    // Convert three bytes at a time to base64 notation.  This will consume 4 chars of output.
        //    int i;

        //    for (i = inDataOffset; i < calcLength; i += 3)
        //    {
        //        outAscii[j] = BASE64_ASCII_ENCODING_TABLE[(inData[i] & 0xfc) >> 2];
        //        outAscii[j + 1] = BASE64_ASCII_ENCODING_TABLE[((inData[i] & 0x03) << 4) | ((inData[i + 1] & 0xf0) >> 4)];
        //        outAscii[j + 2] = BASE64_ASCII_ENCODING_TABLE[((inData[i + 1] & 0x0f) << 2) | ((inData[i + 2] & 0xc0) >> 6)];
        //        outAscii[j + 3] = BASE64_ASCII_ENCODING_TABLE[inData[i + 2] & 0x3f];
        //        j += 4;
        //    }

        //    // Where we left off before
        //    i = calcLength;

        //    switch (lengthmod3)
        //    {
        //        case 2: // One character padding needed
        //            outAscii[j] = BASE64_ASCII_ENCODING_TABLE[(inData[i] & 0xfc) >> 2];
        //            outAscii[j + 1] = BASE64_ASCII_ENCODING_TABLE[((inData[i] & 0x03) << 4) | ((inData[i + 1] & 0xf0) >> 4)];
        //            outAscii[j + 2] = BASE64_ASCII_ENCODING_TABLE[(inData[i + 1] & 0x0f) << 2];
        //            outAscii[j + 3] = BASE64_ASCII_ENCODING_TABLE[64]; // Pad
        //            j += 4;
        //            break;
        //        case 1: // Two character padding needed
        //            outAscii[j] = BASE64_ASCII_ENCODING_TABLE[(inData[i] & 0xfc) >> 2];
        //            outAscii[j + 1] = BASE64_ASCII_ENCODING_TABLE[(inData[i] & 0x03) << 4];
        //            outAscii[j + 2] = BASE64_ASCII_ENCODING_TABLE[64]; // Pad
        //            outAscii[j + 3] = BASE64_ASCII_ENCODING_TABLE[64]; // Pad
        //            j += 4;
        //            break;
        //    }

        //    return j;
        //}
    }
}
