using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
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
    /// Implements a stream which wraps a single read stream and allows you to define multiple "cursors", each
    /// one implementing a read stream at a different point in the base stream. This effectively allows you to
    /// read the same input multiple times, or to allow limited rewinding in streams that are otherwise non-seekable.
    /// </summary>
    public class MultiReadStream
    {
        private readonly AsyncLockSlim _lock;
        private readonly int _bufferBlockSize;
        private readonly NonRealTimeStream _innerStream;
        private readonly Deque<PooledBuffer<byte>> _buffer;
        private readonly int _minimumBufferedBytes;
        private long _bufferStartPosInStream;
        private long _bufferAvailable;
        private bool _innerStreamFinished;
        private HashSet<MultiReadStreamCursor> _activeCursors;

        public MultiReadStream(
            Stream innerStream,
            int minimumBufferedBytes = BufferPool<byte>.DEFAULT_BUFFER_SIZE,
            int bufferBlockSize = BufferPool<byte>.DEFAULT_BUFFER_SIZE) 
            : this (new NonRealTimeStreamWrapper(innerStream, ownsStream: true), minimumBufferedBytes, bufferBlockSize)
        {
        }

        public MultiReadStream(
            NonRealTimeStream innerStream,
            int minimumBufferedBytes = BufferPool<byte>.DEFAULT_BUFFER_SIZE,
            int bufferBlockSize = BufferPool<byte>.DEFAULT_BUFFER_SIZE)
        {
            if (innerStream == null)
            {
                throw new ArgumentNullException(nameof(innerStream));
            }

            if (!innerStream.CanRead)
            {
                throw new ArgumentException("The inner stream of a " + nameof(MultiReadStream) + " must be readable");
            }

            if (minimumBufferedBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumBufferedBytes));
            }

            if (bufferBlockSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferBlockSize));
            }

            _lock = new AsyncLockSlim();
            _bufferBlockSize = bufferBlockSize;
            _innerStream = innerStream;
            _minimumBufferedBytes = minimumBufferedBytes;
            _bufferStartPosInStream = _innerStream.Position;
            _buffer = new Deque<PooledBuffer<byte>>();
            _activeCursors = new HashSet<MultiReadStreamCursor>();
            _bufferAvailable = 0;
            _innerStreamFinished = false;
        }

        public MultiReadStreamCursor CreateCursor(long cursorBeginIndexInStream)
        {
            _lock.GetLock();
            try
            {
                if (cursorBeginIndexInStream < _bufferStartPosInStream)
                {
                    throw new ArgumentOutOfRangeException("Requested offset is too far back in the stream; you must either create the cursor sooner or increase your buffer size");
                }

                // TODO support cursors in the future
                MultiReadStreamCursor returnVal = new MultiReadStreamCursor(this, cursorBeginIndexInStream);
                _activeCursors.Add(returnVal);
                return returnVal;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Returns the amount of data cached locally in this buffered stream.
        /// This should not be used for anything except as a diagnostic for amount of memory used by this class.
        /// </summary>
        public long BufferedDataLength => _bufferAvailable;

        /// <summary>
        /// Marks a cursor as disposed.
        /// This implements a sort of reference counting destructor - when the final cursor is disposed, we dispose
        /// of shared resources and the inner stream at that same time.
        /// This has a small bug where, if no cursors are ever created, nobody does this disposal.
        /// But we assume that design would be nonsensical for anyone actually using this class.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void UnregisterCursor(MultiReadStreamCursor cursor)
        {
            bool isLastCursor = false;
            _lock.GetLock();
            try
            {
                _activeCursors.Remove(cursor);
                isLastCursor = _activeCursors.Count == 0;
            }
            finally
            {
                _lock.Release();
            }

            if (isLastCursor)
            {
                while (_buffer.Count > 0)
                {
                    // have to return all pooled buffers
                    _buffer.RemoveFromFront()?.Dispose();
                }

                _innerStream.Dispose();
                _lock.Dispose();
            }
        }

        /// <summary>
        /// Called by cursor implementations. Reads from the inner buffer and advances the wrapped stream if necessary.
        /// </summary>
        /// <param name="cursorStartPos"></param>
        /// <param name="outputBuffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private int Read(long cursorStartPos, byte[] outputBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _lock.GetLock();
            try
            {
                // Try and fill the buffer enough to satisfy the bytes to read
                int maxCanReadFromBuffer = (int)(_innerStream.Position - cursorStartPos);
                while (maxCanReadFromBuffer <= 0 && !_innerStreamFinished)
                {
                    // Is the last buffer block filled?
                    int bytesAvailableInLastBuffer = (int)(_bufferAvailable % _bufferBlockSize);
                    if (bytesAvailableInLastBuffer == 0)
                    {
                        // Make a new buffer block
                        PooledBuffer<byte> newBuf = BufferPool<byte>.Rent(_bufferBlockSize);
                        int readFromBaseStream = _innerStream.Read(newBuf.Buffer, 0, _bufferBlockSize, cancelToken, realTime);
                        if (readFromBaseStream == 0)
                        {
                            _innerStreamFinished = true;
                        }
                        else
                        {
                            _buffer.AddToBack(newBuf);
                            _bufferAvailable += readFromBaseStream;
                            maxCanReadFromBuffer += readFromBaseStream;
                        }
                    }
                    else
                    {
                        // Appending to an existing buffer
                        PooledBuffer<byte> existingBuf = _buffer.PeekBack();
                        int readFromBaseStream = _innerStream.Read(
                            existingBuf.Buffer,
                            bytesAvailableInLastBuffer,
                            _bufferBlockSize - bytesAvailableInLastBuffer,
                            cancelToken,
                            realTime);
                        if (readFromBaseStream == 0)
                        {
                            _innerStreamFinished = true;
                        }
                        else
                        {
                            _bufferAvailable += readFromBaseStream;
                            maxCanReadFromBuffer += readFromBaseStream;
                        }
                    }
                }

                // And now, actually do the read
                int bytesRead = 0;
                while (_bufferStartPosInStream + _bufferAvailable > cursorStartPos + bytesRead &&
                    bytesRead < maxCanReadFromBuffer && 
                    bytesRead < count)
                {
                    int virtualStartIdx = ((int)(cursorStartPos - _bufferStartPosInStream) + bytesRead);
                    int blockId = virtualStartIdx / _bufferBlockSize;
                    int blockStartIdx = virtualStartIdx % _bufferBlockSize;
                    int amountToRead = FastMath.Min(   // Take minimum of:
                        (int)(maxCanReadFromBuffer - bytesRead), // total bytes we can read from the entire buffer
                        FastMath.Min(count - bytesRead, // bytes remaining to satisfy the caller
                        _bufferBlockSize - blockStartIdx)); // bytes we can read from a single buffer block
#if DEBUG
                    if (amountToRead <= 0) throw new Exception("Amount to read from multireadstream was zero");
#endif
                    PooledBuffer<byte> block = _buffer[blockId];
                    ArrayExtensions.MemCopy(block.Buffer, blockStartIdx, outputBuffer, offset + bytesRead, amountToRead);
                    bytesRead += amountToRead;
                }

                PruneBufferIfPossible();

                return bytesRead;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Called by cursor implementations. Reads from the inner buffer and advances the wrapped stream if necessary.
        /// </summary>
        /// <param name="cursorStartPos"></param>
        /// <param name="outputBuffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private async ValueTask<int> ReadAsync(long cursorStartPos, byte[] outputBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await _lock.GetLockAsync().ConfigureAwait(false);
            try
            {
                // Try and fill the buffer enough to satisfy any non-zero read
                int maxCanReadFromBuffer = (int)(_innerStream.Position - cursorStartPos);
                while (maxCanReadFromBuffer <= 0 && !_innerStreamFinished)
                {
                    // Is the last buffer block filled?
                    int bytesAvailableInLastBuffer = (int)(_bufferAvailable % _bufferBlockSize);
                    if (bytesAvailableInLastBuffer == 0)
                    {
                        // Make a new buffer block
                        PooledBuffer<byte> newBuf = BufferPool<byte>.Rent(_bufferBlockSize);
                        int readFromBaseStream = await _innerStream.ReadAsync(
                            newBuf.Buffer,
                            0,
                            _bufferBlockSize,
                            cancelToken,
                            realTime).ConfigureAwait(false);
                        if (readFromBaseStream == 0)
                        {
                            _innerStreamFinished = true;
                        }
                        else
                        {
                            _buffer.AddToBack(newBuf);
                            _bufferAvailable += readFromBaseStream;
                            maxCanReadFromBuffer += readFromBaseStream;
                        }
                    }
                    else
                    {
                        // Appending to the existing tail buffer
                        PooledBuffer<byte> existingBuf = _buffer.PeekBack();
                        int readFromBaseStream = await _innerStream.ReadAsync(
                            existingBuf.Buffer,
                            bytesAvailableInLastBuffer,
                            _bufferBlockSize - bytesAvailableInLastBuffer,
                            cancelToken,
                            realTime).ConfigureAwait(false);
                        if (readFromBaseStream == 0)
                        {
                            _innerStreamFinished = true;
                        }
                        else
                        {
                            _bufferAvailable += readFromBaseStream;
                            maxCanReadFromBuffer += readFromBaseStream;
                        }
                    }
                }

                // And now, actually do the read
                int bytesRead = 0;
                while (_bufferStartPosInStream + _bufferAvailable > cursorStartPos + bytesRead &&
                    bytesRead < maxCanReadFromBuffer &&
                    bytesRead < count)
                {
                    int virtualStartIdx = ((int)(cursorStartPos - _bufferStartPosInStream) + bytesRead);
                    int blockId = virtualStartIdx / _bufferBlockSize;
                    int blockStartIdx = virtualStartIdx % _bufferBlockSize;
                    int amountToRead = FastMath.Min(   // Take minimum of:
                        (int)(maxCanReadFromBuffer - bytesRead), // total bytes we can read from the entire buffer
                        FastMath.Min(count - bytesRead, // bytes remaining to satisfy the caller
                        _bufferBlockSize - blockStartIdx)); // bytes we can read from a single buffer block
#if DEBUG
                    if (amountToRead <= 0) throw new Exception("Amount to read from multireadstream was zero");
#endif
                    PooledBuffer<byte> block = _buffer[blockId];
                    ArrayExtensions.MemCopy(block.Buffer, blockStartIdx, outputBuffer, offset + bytesRead, amountToRead);
                    bytesRead += amountToRead;
                }

                PruneBufferIfPossible();

                return bytesRead;
            }
            finally
            {
                _lock.Release();
            }
        }

        private void PruneBufferIfPossible()
        {
            long earliestCursor = long.MaxValue;
            foreach (MultiReadStreamCursor cursor in _activeCursors)
            {
                earliestCursor = Math.Min(earliestCursor, cursor.Position);
            }

            while (_bufferAvailable > (_minimumBufferedBytes + _bufferBlockSize) &&
                    _bufferStartPosInStream + _bufferBlockSize <= earliestCursor)
            {
                _buffer.RemoveFromFront().Dispose();
                _bufferStartPosInStream += _bufferBlockSize;
                _bufferAvailable -= _bufferBlockSize;
            }
        }

        private long StreamLength => _innerStream.Length;
        
        private int GetCursorAvailable(long cursorPos)
        {
            return (int)(_innerStream.Position - cursorPos);
        }

        public class MultiReadStreamCursor : NonRealTimeStream
        {
            private readonly MultiReadStream _wrapper;
            private long _cursorIndexInStream;
            private int _disposed = 0;

            internal MultiReadStreamCursor (MultiReadStream wrapper, long cursorIndexInStream)
            {
                _wrapper = wrapper;
                _cursorIndexInStream = cursorIndexInStream;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _wrapper.StreamLength;

            /// <summary>
            /// Returns the number of bytes that we can guarantee to read without blocking
            /// </summary>
            public int GuaranteedAvailable => _wrapper.GetCursorAvailable(_cursorIndexInStream);

            public override long Position
            {
                get
                {
                    return _cursorIndexInStream;
                }
                set
                {
                    throw new NotSupportedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }

            public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                int amountRead = _wrapper.Read(_cursorIndexInStream, targetBuffer, offset, count, cancelToken, realTime);
                _cursorIndexInStream += amountRead;
                return amountRead;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
            {
                return ReadAsync(buffer, offset, count, cancelToken, DefaultRealTimeProvider.Singleton);
            }

            public override async Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                int amountRead = await _wrapper.ReadAsync(_cursorIndexInStream, targetBuffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
                _cursorIndexInStream += amountRead;
                return amountRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void Flush()
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
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
                        _wrapper.UnregisterCursor(this);
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }

            public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new NotSupportedException();
            }

            public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new NotSupportedException();
            }
        }
    }
}
