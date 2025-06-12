using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Instrumentation.Profiling
{
    /// <summary>
    /// Microprofiler client which writes profiling data to an abstract stream. Write operations are
    /// thread-safe and performant, though buffering can still cause file writes to be delayed.
    /// </summary>
    public abstract class StreamMicroProfilerClient : IMicroProfilerClient
    {
        /// <summary>
        /// The size of each page of memory
        /// </summary>
        protected const int WRITE_BUFFER_SIZE = 64 * 1024; // 64Kb buffers

        /// <summary>
        /// A concurrent dictionary containing a loose collection of "active" pages (that is, memory buffers not yet committed to stream)
        /// </summary>
        private readonly FastConcurrentDictionary<int, PooledBuffer<byte>> _writePages;

        /// <summary>
        /// Controls access to the actual stream. This is the lock that ensures bytes are written sequentially even if many threads are writing messages.
        /// </summary>
        private readonly object _asyncWriteLock = new object();

        /// <summary>
        /// The stream to write to.
        /// </summary>
        protected readonly Stream _stream;

        /// <summary>
        /// A deferred task which writes pages to the stream in the background.
        /// </summary>
        private Task _asyncWriteTask = null;

        /// <summary>
        /// The total number of bytes we have written to the stream. This is accessed
        /// atomically to reserve portions of output buffer to individual caller threads.
        /// </summary>
        private long _writeBufferIndex = 0;

        private int _disposed = 0;

        protected StreamMicroProfilerClient(Stream targetStream)
        {
            _stream = targetStream;
            _writePages = new FastConcurrentDictionary<int, PooledBuffer<byte>>();
            _writePages[0] = BufferPool<byte>.Rent(WRITE_BUFFER_SIZE);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~StreamMicroProfilerClient()
        {
            Dispose(false);
        }
#endif

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Flush()
        {
            Monitor.Enter(_asyncWriteLock);
            try
            {
                if (_asyncWriteTask != null)
                {
                    _asyncWriteTask.Await();
                }
            }
            finally
            {
                Monitor.Exit(_asyncWriteLock);
            }
        }

        public virtual void SendProfilingData(byte[] data, int offset, int count)
        {
            if (count >= WRITE_BUFFER_SIZE)
            {
                throw new ArgumentException("Profiling data is too large");
            }

            // Calculate buffer boundaries for this write
            long absoluteIndexEnd = Interlocked.Add(ref _writeBufferIndex, count);
            int pageEnd = (int)(absoluteIndexEnd / WRITE_BUFFER_SIZE);
            long absoluteIndexStart = absoluteIndexEnd - count;
            int pageStart = (int)(absoluteIndexStart / WRITE_BUFFER_SIZE);
            int relativeIndexStart = (int)(absoluteIndexStart % WRITE_BUFFER_SIZE);

            // Write to first page of buffer (usually all of the data will get written here)
            int firstWriteSize = Math.Min(count, WRITE_BUFFER_SIZE - relativeIndexStart);
            PooledBuffer<byte> firstWriteBuffer;
#pragma warning disable CA2000 // Dispose objects before losing scope
            _writePages.TryGetValueOrSet(pageStart, out firstWriteBuffer, () => BufferPool<byte>.Rent(WRITE_BUFFER_SIZE));
#pragma warning restore CA2000 // Dispose objects before losing scope
            ArrayExtensions.MemCopy(data, offset, firstWriteBuffer.Buffer, relativeIndexStart, firstWriteSize);

            // Did we just fill up a page entirely?
            if (pageStart != pageEnd)
            {
                // Write whatever leftover bytes we need to across the page boundary
                int secondWriteSize = count - firstWriteSize;
                PooledBuffer<byte> secondWriteBuffer;
#pragma warning disable CA2000 // Dispose objects before losing scope
                _writePages.TryGetValueOrSet(pageEnd, out secondWriteBuffer, () => BufferPool<byte>.Rent(WRITE_BUFFER_SIZE));
#pragma warning restore CA2000 // Dispose objects before losing scope
                ArrayExtensions.MemCopy(data, offset + firstWriteSize, secondWriteBuffer.Buffer, 0, secondWriteSize);

                // And then commit the previous buffer in the background.
                // There is a subtle race condition in this code. If we try and commit the prevous page while another thread
                // (which allocated some buffer space earlier on pageStart) is still writing to that buffer,
                // the bytes we commit to the stream may be inconsistent. To avoid that, we commit the _previous_ page before that one
                // (that is, 1 page before the one that just filled up). We assume by this time that the data on that page is consistent.
                PooledBuffer<byte> committedBuffer;
                if (_writePages.TryGetValueAndRemove(pageStart - 1, out committedBuffer))
                {
                    Monitor.Enter(_asyncWriteLock);
                    try
                    {
                        if (_asyncWriteTask != null)
                        {
                            _asyncWriteTask.Await();
                        }

                        _asyncWriteTask = _stream.WriteAsync(committedBuffer.Buffer, 0, WRITE_BUFFER_SIZE).ContinueWith((t) => committedBuffer.Dispose());
                    }
                    finally
                    {
                        Monitor.Exit(_asyncWriteLock);
                    }
                }
            }
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
                Monitor.Enter(_asyncWriteLock);
                try
                {
                    if (_asyncWriteTask != null)
                    {
                        _asyncWriteTask.Await();
                    }

                    // Write everything that's currently in the buffer too
                    long absoluteIndexEnd = _writeBufferIndex;
                    int pageEnd = (int)(absoluteIndexEnd / WRITE_BUFFER_SIZE);
                    PooledBuffer<byte> committedBuffer;

                    for (int page = pageEnd - 3; page < pageEnd; page++)
                    {
                        if (_writePages.TryGetValueAndRemove(page, out committedBuffer))
                        {
                            _stream.WriteAsync(committedBuffer.Buffer, 0, WRITE_BUFFER_SIZE).Await();
                            committedBuffer.Dispose();
                        }
                    }

                    int bytesInFinalPage = (int)(absoluteIndexEnd % WRITE_BUFFER_SIZE);
                    if (_writePages.TryGetValueAndRemove(pageEnd, out committedBuffer))
                    {
                        _stream.WriteAsync(committedBuffer.Buffer, 0, bytesInFinalPage).Await();
                        committedBuffer.Dispose();
                    }

                    _stream.Flush();
                    _stream.Dispose();
                }
                catch (IOException e)
                {
                    Debug.WriteLine("Error while closing microprofiler output: " + e);
                }
                finally
                {
                    Monitor.Exit(_asyncWriteLock);
                }
            }
        }
    }
}
