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

namespace Durandal.Common.IO
{
    public class PooledSegmentedBuffer<T> : IDisposable
    {
        private const int SEGMENT_SIZE = 1024;
        private readonly Deque<PooledBuffer<T>> _segments = new Deque<PooledBuffer<T>>();
        private int _firstSegmentIndex = 0;
        private int _count;
        private int _disposed = 0;

        public PooledSegmentedBuffer()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PooledSegmentedBuffer()
        {
            Dispose(false);
        }
#endif

        public int Count => _count;

        public int Load(Stream input, int desiredReadSize)
        {
            if (typeof(T) != typeof(byte))
            {
                throw new InvalidCastException("Cannot copy stream data to non-byte container type");
            }

            return 0;
        }

        public int Load(TextReader input, int desiredReadSize)
        {
            if (typeof(T) != typeof(char))
            {
                throw new InvalidCastException("Cannot copy char data to non-char container type");
            }

            int lastSegmentIndex = (_firstSegmentIndex + _count) % SEGMENT_SIZE;
            int lastSegmentPage = (_firstSegmentIndex + _count) / SEGMENT_SIZE;
            int charsRead = 0;
            bool keepReading = true;
            while (keepReading)
            {
                // Ensure space for this next read
                if (_segments.Count < lastSegmentPage + 1)
                {
                    _segments.AddToBack(BufferPool<T>.Rent(SEGMENT_SIZE));
                }

                PooledBuffer<char> charSegment = _segments.PeekBack() as PooledBuffer<char>;
                int amountCanRead = SEGMENT_SIZE - lastSegmentIndex;

                // Do the read
                int amountActuallyRead = input.ReadBlock(charSegment.Buffer, lastSegmentIndex, amountCanRead);
                if (amountActuallyRead > 0)
                {
                    charsRead += amountActuallyRead;
                    _count += amountActuallyRead;
                    lastSegmentIndex = (lastSegmentIndex + amountActuallyRead);
                    if (lastSegmentIndex >= SEGMENT_SIZE)
                    {
                        lastSegmentIndex = 0;
                        lastSegmentPage++;
                    }

                    // Keep reading as long as the stream didn't return partial data
                    keepReading = amountActuallyRead == amountCanRead &&
                       charsRead < desiredReadSize;
                }
                else
                {
                    keepReading = false;
                }
            }

            return charsRead;
        }

        public void DiscardFromFront(int numElements)
        {
            if (numElements > _count)
            {
                throw new ArgumentOutOfRangeException("Elements to discard exceeds the number currently in the buffer");
            }

            _firstSegmentIndex += numElements;
            _count -= numElements;

            while (_firstSegmentIndex >= SEGMENT_SIZE)
            {
                _firstSegmentIndex -= SEGMENT_SIZE;
                _segments.RemoveFromFront().Dispose();
            }
        }

        //public void WriteToFront(T[] target, int readOffset, int length)
        //{
        //    if (length > _count)
        //    {
        //        throw new ArgumentOutOfRangeException("Elements to read exceeds the number currently in the buffer");
        //    }

        //    int numRead = 0;
        //    int idx = _firstSegmentIndex;
        //    while (numRead < length)
        //    {
        //        int page = idx / SEGMENT_SIZE;
        //        int startIdxInPage = idx % SEGMENT_SIZE;
        //        int readSize = FastMath.Min(length - numRead, SEGMENT_SIZE - startIdxInPage);
        //        ArrayExtensions.MemCopy(
        //            target,
        //            readOffset + numRead,
        //            _segments[page].Buffer,
        //            startIdxInPage,
        //            readSize);
        //        numRead += readSize;
        //        idx += readSize;
        //    }
        //}

        public void ReadFromFront(T[] target, int writeOffset, int length)
        {
            Read(0, target, writeOffset, length);
        }

        public void Read(int readOffset, T[] target, int writeOffset, int length)
        {
            if (readOffset + length > _count)
            {
                throw new ArgumentOutOfRangeException("Elements to read exceeds the number currently in the buffer");
            }

            int numRead = 0;
            int idx = _firstSegmentIndex + readOffset;
            while (numRead < length)
            {
                int page = idx / SEGMENT_SIZE;
                int startIdxInPage = idx % SEGMENT_SIZE;
                int readSize = FastMath.Min(length - numRead, SEGMENT_SIZE - startIdxInPage);
                ArrayExtensions.MemCopy(
                    _segments[page].Buffer,
                    startIdxInPage,
                    target,
                    writeOffset + numRead,
                    readSize);
                numRead += readSize;
                idx += readSize;
            }
        }

        public void Clear()
        {
            while (_segments.Count > 0)
            {
                _segments.RemoveFromFront().Dispose();
            }

            _count = 0;
            _firstSegmentIndex = 0;
        }

        public T FirstElement
        {
            get
            {
                if (_count == 0)
                {
                    throw new IndexOutOfRangeException("No elements in the collection");
                }

                return _segments[0].Buffer[_firstSegmentIndex];
            }
        }

        public T this[int index]
        {
            get
            {
                int actualIndex = index + _firstSegmentIndex;
                int page = actualIndex / SEGMENT_SIZE;
                int character = actualIndex % SEGMENT_SIZE;
                return _segments[page].Buffer[character];
            }
        }

        //public T LastElement
        //{
        //    get
        //    {
        //        return default(T);
        //    }
        //}

        /// <inheritdoc/>
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
                Clear();
            }
        }
    }
}
