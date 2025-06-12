
namespace Durandal.Common.IO
{
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Writable stream which accepts byte input and decodes them as characters to a StringBuilder with a specific encoding.
    /// </summary>
    internal class StringBuilderWriteStream : NonRealTimeStream
    {
        private readonly Decoder _inputDecoder;
        private readonly PooledBuffer<char> _charBuffer;
        private readonly PooledBuffer<byte> _byteBuffer;
        private readonly StringBuilder _output;
        private int _bytePosition = 0;
        private int _leftoverBytes = 0;
        private int _disposed = 0;

        public StringBuilderWriteStream(StringBuilder output, Encoding encoding)
        {
            _inputDecoder = encoding.AssertNonNull(nameof(encoding)).GetDecoder();
            _output = output.AssertNonNull(nameof(output));
            _charBuffer = BufferPool<char>.Rent();
            _byteBuffer = BufferPool<byte>.Rent();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }


#if TRACK_IDISPOSABLE_LEAKS
        ~StringBuilderWriteStream()
        {
            Dispose(false);
        }
#endif

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _bytePosition;

        public override long Position
        {
            get => _bytePosition;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException();
        }

        public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton);
        }

        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            Write(sourceBuffer, offset, count, cancelToken, realTime);
            return DurandalTaskExtensions.NoOpTask;
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int totalBytesRead = 0;
            int bytesRead, charsWritten;
            bool completed;
            while (totalBytesRead < count)
            {
                if (_leftoverBytes > 0)
                {
                    // ensure that this cannot get stuck in an infinite loop if the decoder doesn't want to read all the data
                    // for example multi-codepoint Unicode glyphs
                    int bytesCanReadFromInput = Math.Min(count - totalBytesRead, _byteBuffer.Length - _leftoverBytes);
                    sourceBuffer.AsSpan(offset + totalBytesRead, bytesCanReadFromInput).CopyTo(_byteBuffer.Buffer.AsSpan(_leftoverBytes));
                    _leftoverBytes += bytesCanReadFromInput;
                    _inputDecoder.Convert(_byteBuffer.Buffer, 0, _leftoverBytes, _charBuffer.Buffer, 0, _charBuffer.Length, false, out bytesRead, out charsWritten, out completed);

                    if (charsWritten > 0)
                    {
                        _output.Append(_charBuffer.Buffer, 0, charsWritten);
                    }

                    totalBytesRead += bytesRead;
                    _leftoverBytes -= bytesRead;
                    if (_leftoverBytes > 0)
                    {
                        // Shift leftover buffer left
                        _byteBuffer.Buffer.AsSpan(bytesRead, _leftoverBytes).CopyTo(_byteBuffer.Buffer.AsSpan());
                    }
                }
                else
                {
                    _inputDecoder.Convert(sourceBuffer, offset, count, _charBuffer.Buffer, 0, _charBuffer.Length, false, out bytesRead, out charsWritten, out completed);
                    if (charsWritten > 0)
                    {
                        _output.Append(_charBuffer.Buffer, 0, charsWritten);
                    }

                    totalBytesRead += bytesRead;

                    // Decoder can't process this entire byte sequence (it's truncated or something)
                    // Just stash the data for later and process it on the next call
                    if (bytesRead == 0 && totalBytesRead < count)
                    {
                        // It's technically possible that the remaining bytes are larger than the scratch byte buffer.
                        // But that would also be wild because it implies that the string decoder looked at several thousand bytes and said "nope, nothing here!"
                        sourceBuffer.AsSpan(totalBytesRead, count - totalBytesRead).CopyTo(_byteBuffer.Buffer.AsSpan());
                        _bytePosition += totalBytesRead;
                        return;
                    }
                }
            }

            _bytePosition += count;
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return DurandalTaskExtensions.NoOpTask;
        }

        public override Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return DurandalTaskExtensions.NoOpTask;
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
                    _byteBuffer?.Dispose();
                    _charBuffer?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
