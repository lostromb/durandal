using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Implements a single-read, single-write byte buffer stack (meaning that it grows to the left, towards negative indices,
    /// rather than towards the right) which uses pooled buffers internally to grow to a variable size.
    /// This is most commonly used for "unread" buffers on streams, where we would like to rewind the last little bit of data
    /// we read and push it back onto a stack for rereading later.
    /// </summary>
    public class StackBuffer : IDisposable
    {
        private readonly int BUFFER_SIZE = BufferPool<byte>.DEFAULT_BUFFER_SIZE;
        private readonly Stack<PooledBuffer<byte>> _buffers = new Stack<PooledBuffer<byte>>();
        private PooledBuffer<byte> _top;
        private int _bytesAvailableToRead;
        private int _firstValidByteInTopBuffer;
        private int _disposed;

        public StackBuffer()
        {
            _top = null;
            _firstValidByteInTopBuffer = BUFFER_SIZE;
            _bytesAvailableToRead = 0;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~StackBuffer()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Gets the number of bytes that can be currently read from this buffer.
        /// </summary>
        public int Available
        {
            get
            {
                return _bytesAvailableToRead;
            }
        }

        /// <summary>
        /// Attempts to read a specific number of bytes from the buffer.
        /// Can return any value between 0 and count. A return value of 0 does not
        /// mean end of stream (there's no such concept here), rather it just
        /// means that there is no data currently in the buffer.
        /// </summary>
        /// <param name="buffer">The destination to read the data into</param>
        /// <param name="offset">The offset in the destination buffer</param>
        /// <param name="count">The maximum number of bytes to read</param>
        /// <returns>The total number of bytes read</returns>
        public int Read(byte[] buffer, int offset, int count)
        {
            count.AssertNonNegative(nameof(count));
            offset.AssertNonNegative(nameof(offset));
            buffer.AssertNonNull(nameof(buffer));

            if (_bytesAvailableToRead == 0)
            {
                return 0;
            }

            int bytesRead = 0;
            while (_top != null && bytesRead < count && _bytesAvailableToRead > 0)
            {
                if (_disposed != 0)
                {
                    throw new ObjectDisposedException(nameof(StackBuffer));
                }
                
                // Read from top buffer
                int thisReadSize = FastMath.Min(count - bytesRead, BUFFER_SIZE - _firstValidByteInTopBuffer);
                ArrayExtensions.MemCopy(
                    _top.Buffer,
                    _firstValidByteInTopBuffer,
                    buffer,
                    offset + bytesRead,
                    thisReadSize);
                _firstValidByteInTopBuffer += thisReadSize;
                bytesRead += thisReadSize;

                // Finished reading from top buffer
                if (_firstValidByteInTopBuffer == BUFFER_SIZE)
                {
                    _buffers.Pop().Dispose();

                    // Queue up next buffer down in the stack, if available
                    if (_buffers.Count == 0)
                    {
                        _top = null;
                        _firstValidByteInTopBuffer = BUFFER_SIZE;
                    }
                    else
                    {
                        _top = _buffers.Peek();
                        _firstValidByteInTopBuffer = 0;
                    }
                }
            }

            _bytesAvailableToRead -= bytesRead;
            return bytesRead;
        }

        /// <summary>
        /// Writes data into the buffer, making it immediately available for reading.
        /// </summary>
        /// <param name="buffer">The source data</param>
        /// <param name="offset">The offset when copying from the source buffer</param>
        /// <param name="count">The number of bytes to copy</param>
        public void Write(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            buffer.AssertNonNull(nameof(buffer));

            int bytesWritten = 0;
            while (bytesWritten < count)
            {
                if (_disposed != 0)
                {
                    throw new ObjectDisposedException(nameof(StackBuffer));
                }

                // Do we need to grow the stack?
                if (_top == null || _firstValidByteInTopBuffer == 0)
                {
                    _top = BufferPool<byte>.Rent(BUFFER_SIZE);
                    _buffers.Push(_top);
                    _firstValidByteInTopBuffer = BUFFER_SIZE;
                }

                // Fill up tail buffer as much as possible
                int thisWriteSize = FastMath.Min(count - bytesWritten, _firstValidByteInTopBuffer);
                ArrayExtensions.MemCopy(
                    buffer,
                    offset + count - bytesWritten - thisWriteSize,
                    _top.Buffer,
                    _firstValidByteInTopBuffer - thisWriteSize,
                    thisWriteSize);
                _firstValidByteInTopBuffer -= thisWriteSize;
                bytesWritten += thisWriteSize;
            }

            _bytesAvailableToRead += count;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                try
                {
                    while (_buffers.Count > 0)
                    {
                        _buffers.Pop()?.Dispose();
                    }
                }
                catch (Exception) { }
            }
        }
    }
}
