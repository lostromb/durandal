using Durandal.Common.Collections;
using Durandal.Common.Logger;
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
    /// This implements a pair of streams (a read and write end) which can be operated independently to send data between processes.
    /// </summary>
    public class PipeStream : IDisposable
    {
        private PipeReadStream _readStream;
        private PipeWriteStream _writeStream;

        private readonly AutoResetEventAsync _dataChunksAvailable;
        private readonly object _lock = new object();
        private readonly Queue<PooledBuffer<byte>> _chunks = new Queue<PooledBuffer<byte>>();
        private PooledBuffer<byte> _currentChunk = null;
        private int _indexInCurrentChunk = 0;
        private long _totalBytesWritten = 0;
        private bool _writeClosed = false;
        private bool _readClosed = false;
        private int _disposed = 0;

        public PipeStream()
        {
            _dataChunksAvailable = new AutoResetEventAsync();
            _readStream = new PipeReadStream(this);
            _writeStream = new PipeWriteStream(this);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PipeStream()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Retrieves the read end of the pipe.
        /// </summary>
        public PipeReadStream GetReadStream()
        {
            PipeReadStream returnVal = Interlocked.Exchange(ref _readStream, null);
            if (returnVal == null)
            {
                throw new InvalidOperationException("ReadStream can only be fetched once, because ownership of the IDisposable is transferred");
            }

            return returnVal;
        }

        /// <summary>
        /// Retrieves the write end of the pipe.
        /// </summary>
        public PipeWriteStream GetWriteStream()
        {
            PipeWriteStream returnVal = Interlocked.Exchange(ref _writeStream, null);
            if (returnVal == null)
            {
                throw new InvalidOperationException("WriteStream can only be fetched once, because ownership of the IDisposable is transferred");
            }

            return returnVal;
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

            if (_readStream == null)
            {
                // If the read stream ownership has been transferred, that stream is now responsible for disposing of the whole pipe.
                // So nothing to do here.
            }
            else
            {
                if (disposing)
                {
                    _writeStream?.Dispose();
                    _readStream?.Dispose(); // This will call CloseRead which in turn will call ActuallyDisposeOfPipe and dispose of everything
                }
            }
        }

        private void ActuallyDisposeOfPipe()
        {
            Monitor.Enter(_lock);
            try
            {
                _currentChunk?.Dispose();
                while (_chunks.Count > 0)
                {
                    _chunks.Dequeue()?.Dispose();
                }
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }

        private int ReadInternal(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_readClosed)
            {
                return 0;
            }

            Monitor.Enter(_lock);
            bool holdingLock = true; // to prevent lock inconsistencies if we throw an exception in the middle of this function
            try
            {
                while (!_readClosed)
                {
                    // Make sure we have a chunk to read from
                    if (_currentChunk == null || _indexInCurrentChunk >= _currentChunk.Length)
                    {
                        _currentChunk?.Dispose();
                        _currentChunk = null;
                        _indexInCurrentChunk = 0;

                        // Chunks are immediately available, use one
                        if (_chunks.Count > 0)
                        {
                            _currentChunk = _chunks.Dequeue();
                        }
                        else if (!_writeClosed)
                        {
                            // Wait for more data if the write side is still open
                            Monitor.Exit(_lock);
                            holdingLock = false;
                            if (realTime.IsForDebug)
                            {
                                // Consume VT if we are in a nonrealtime scenario (less efficient but allows us to unit test)
                                while (!_dataChunksAvailable.TryGetAndClear())
                                {
                                    realTime.Wait(TimeSpan.FromMilliseconds(10), cancelToken);
                                }
                            }
                            else
                            {
                                _dataChunksAvailable.Wait();
                            }

                            Monitor.Enter(_lock);
                            holdingLock = true;

                            if (_chunks.Count > 0)
                            {
                                _currentChunk = _chunks.Dequeue();
                            }
                        }
                    }

                    if (_writeClosed && _currentChunk == null)
                    {
                        _readClosed = true;
                    }

                    // Do the read
                    if (_currentChunk != null)
                    {
                        int amountToRead = FastMath.Min(count, _currentChunk.Length - _indexInCurrentChunk);
                        ArrayExtensions.MemCopy(_currentChunk.Buffer, _indexInCurrentChunk, targetBuffer, offset, amountToRead);
                        _indexInCurrentChunk += amountToRead;
                        return amountToRead;
                    }
                }

                return 0;
            }
            finally
            {
                if (holdingLock)
                {
                    Monitor.Exit(_lock);
                }
            }
        }

        private async ValueTask<int> ReadInternalAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_readClosed)
            {
                return 0;
            }

            Monitor.Enter(_lock);
            bool holdingLock = true; // to prevent lock inconsistencies if we throw an exception in the middle of this function
            try
            {
                while (!_readClosed)
                {
                    // Make sure we have a chunk to read from
                    if (_currentChunk == null || _indexInCurrentChunk >= _currentChunk.Length)
                    {
                        _currentChunk?.Dispose();
                        _currentChunk = null;
                        _indexInCurrentChunk = 0;

                        // Chunks are immediately available, use one
                        if (_chunks.Count > 0)
                        {
                            _currentChunk = _chunks.Dequeue();
                        }
                        else if (!_writeClosed)
                        {
                            // Wait for more data if the write side is still open
                            Monitor.Exit(_lock);
                            holdingLock = false;
                            if (realTime.IsForDebug)
                            {
                                // Consume VT if we are in a nonrealtime scenario (less efficient but allows us to unit test)
                                while (!_dataChunksAvailable.TryGetAndClear())
                                {
                                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                await _dataChunksAvailable.WaitAsync().ConfigureAwait(false);
                            }

                            Monitor.Enter(_lock);
                            holdingLock = true;

                            if (_chunks.Count > 0)
                            {
                                _currentChunk = _chunks.Dequeue();
                            }
                        }
                    }

                    if (_writeClosed && _currentChunk == null)
                    {
                        _readClosed = true;
                    }

                    // Do the read
                    if (_currentChunk != null)
                    {
                        int amountToRead = FastMath.Min(count, _currentChunk.Length - _indexInCurrentChunk);
                        ArrayExtensions.MemCopy(_currentChunk.Buffer, _indexInCurrentChunk, targetBuffer, offset, amountToRead);
                        _indexInCurrentChunk += amountToRead;
                        return amountToRead;
                    }
                }

                return 0;
            }
            finally
            {
                if (holdingLock)
                {
                    Monitor.Exit(_lock);
                }
            }
        }

        private void WriteInternal(byte[] chunk, int offset, int count)
        {
            if (_writeClosed)
            {
                throw new InvalidOperationException("Cannot write to a closed stream");
            }

            int bytesWritten = 0;
            while (bytesWritten < count)
            {
                if (_readClosed)
                {
                    // Nobody will ever read the data, so stop working
                    _totalBytesWritten += bytesWritten;
                    return;
                }

                // Divide large reads so we don't end up renting huge buffers from the pool
                int thisBlockSize = FastMath.Min(BufferPool<byte>.DEFAULT_BUFFER_SIZE, count - bytesWritten);
                PooledBuffer<byte> copiedData = BufferPool<byte>.Rent(thisBlockSize);
                ArrayExtensions.MemCopy(chunk, offset + bytesWritten, copiedData.Buffer, 0, thisBlockSize);
                bytesWritten += thisBlockSize;

                Monitor.Enter(_lock);
                try
                {
                    _chunks.Enqueue(copiedData);
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            }

            _totalBytesWritten += count;
            _dataChunksAvailable.Set();
        }

        /// <summary>
        /// Closes the write end of this pipe. The data will still sit in the buffer and be readable,
        /// and EndOfStream will not be signalled until the buffer is entirely emptied.
        /// </summary>
        private void CloseWrite()
        {
            Monitor.Enter(_lock);
            try
            {
                _writeClosed = true;

                // We have to do a Task.Run here specifically to avoid a rare deadlock
                // that could happen because of some convoluted logic in async wait handles.
                Task.Run(_dataChunksAvailable.Set).Forget(NullLogger.Singleton);
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }

        /// <summary>
        /// Closes the read end of this pipe, which disposes of all resources.
        /// </summary>
        private void CloseRead()
        {
            _readClosed = true;
            ActuallyDisposeOfPipe();
        }

        /// <summary>
        /// Implements the read end of the pipe.
        /// The entire pipe will be disposed when this stream is disposed.
        /// </summary>
        public class PipeReadStream : NonRealTimeStream
        {
            private readonly PipeStream _pipe;
            private long _pos = 0;
            private int _disposed = 0;

            public PipeReadStream(PipeStream pipe)
            {
                _pipe = pipe;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => _pipe._totalBytesWritten - _pos;

            public override long Position
            {
                get
                {
                    return _pos;
                }
                set
                {
                    throw new InvalidOperationException();
                }
            }

            /// <summary>
            /// Gets a value indicating whether this PipeStream has closed the write end and there is no more data to read.
            /// </summary>
            public bool EndOfStream => _pipe._readClosed;

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int returnVal = _pipe.ReadInternal(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                _pos += returnVal;
                return returnVal;
            }

            public override int Read(byte[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                int returnVal = _pipe.ReadInternal(buffer, offset, count, cancelToken, realTime);
                _pos += returnVal;
                return returnVal;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
            {
                int returnVal = await _pipe.ReadInternalAsync(buffer, offset, count, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                _pos += returnVal;
                return returnVal;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                int returnVal = await _pipe.ReadInternalAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
                _pos += returnVal;
                return returnVal;
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
                        _pipe.CloseRead();
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }

        /// <summary>
        /// Implements the write end of the pipe.
        /// When this stream is closed, the data will still remain in the buffer to be accessed by the read stream, after which the read stream will end.
        /// </summary>
        public class PipeWriteStream : NonRealTimeStream
        {
            private readonly PipeStream _pipe;
            private long _pos = 0;
            private int _disposed = 0;

            public PipeWriteStream(PipeStream pipe)
            {
                _pipe = pipe;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => _pos;

            public override long Position
            {
                get
                {
                    return _pos;
                }
                set
                {
                    throw new InvalidOperationException();
                }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new InvalidOperationException();
            }

            public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new InvalidOperationException();
            }

            public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
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
                _pipe.WriteInternal(buffer, offset, count);
                _pos += count;
            }

            public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                _pipe.WriteInternal(sourceBuffer, offset, count);
                _pos += count;
            }

            public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                _pipe.WriteInternal(sourceBuffer, offset, count);
                _pos += count;
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
                        _pipe.CloseWrite();
                    }
                }
                finally
                {
                    base.Dispose();
                }
            }
        }
    }
}
