using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Represents a reusable pooled buffer of some type T that was rented from a <see cref="BufferPool{T}"/>.
    /// The actual length of the array may be longer than the actual valid contents; to codify the difference, the Length
    /// parameter is used (in cases where you pass a buffered pool to some function like a reader that parses data from it).
    /// This pool should be disposed when you are done using it, after which it is returned to the pool to be reclaimed.
    /// </summary>
    /// <typeparam name="T">The type of data that this buffer holds.</typeparam>
    public sealed class PooledBuffer<T> : IDisposable
    {
        private int _disposed = 0;
        internal sbyte _sourceBin;
        internal int _originalLength;
        //private static int _bufferIncrement = 0;

        internal PooledBuffer(T[] data, int length, sbyte sourceBin)
        {
            Buffer = data;
            Length = length;
            _originalLength = length;
            _sourceBin = sourceBin;
            //Id = Interlocked.Increment(ref _bufferIncrement);
            //Debug.WriteLine($"Created {typeof(T)} buffer {Id} length {Length}:{data.Length}");
        }

        // used when recyling this object
        internal void Reset(int length)
        {
            Length = length;
            _disposed = 0;
            //Debug.WriteLine($"Reset {typeof(T)} buffer {Id}");
        }

#if DEBUG
        private string _lastRentalStackTrace = "Unknown";

#pragma warning disable CA1063 // suppress "improper" finalizer style
        ~PooledBuffer()
        {
            // we don't actually need to mark it as disposed or return the buffer; just emit a warning that there is a leak
            if (_sourceBin >= 0)
            {
#if NETFRAMEWORK
                Console.WriteLine("Buffer pool leak detected: Buffer was rented by " + _lastRentalStackTrace);
#else
                Debug.WriteLine("Buffer pool leak detected: Buffer was rented by " + _lastRentalStackTrace);
#endif
            }
        }
#pragma warning restore CA1063

        internal void MarkRented(string callerFilePath, string callerMemberName, int callerLineNumber)
        {
            if (callerMemberName != null)
            {
                _lastRentalStackTrace = string.Format("{0}, in {1} line {2}", callerMemberName, callerFilePath, callerLineNumber);
            }

            //Debug.WriteLine($"Buffer {typeof(T)} {Id} marked rented by {_lastRentalStackTrace}");
        }
#endif // DEBUG

        /// <summary>
        /// The contiguous buffer to store data in.
        /// </summary>
        public T[] Buffer { get; private set; }

        /// <summary>
        /// The buffer's intended length. This may be smaller than what is actually available (since a larger pooled buffer may have been used
        /// to satisfy the request). This can be used as a signal to indicate the intended data size this buffer is used for.
        /// </summary>
        public int Length { get; private set; }

        // used for very in-depth debugging
        //public int Id { get; set; }

        /// <summary>
        /// Returns a pointer to this buffer's data as an array segment.
        /// The pointer will be invalid after the buffer is reclaimed. In other words, this is just a view over the data in-place.
        /// </summary>
        public ArraySegment<T> AsArraySegment => new ArraySegment<T>(Buffer, 0, Length);

        /// <summary>
        /// Returns a pointer to this buffer's data as a span.
        /// </summary>
        public Span<T> AsSpan => Buffer.AsSpan(0, Length);

        /// <summary>
        /// Reduces the size of this buffer's reported length. The intended use case for this is if you have
        /// read some data into a pooled buffer, but not enough the fill the whole space, and then you
        /// want to pass that pooled buffer to someone else while reporting accurately the number
        /// of bytes that are actually in use.
        /// </summary>
        /// <param name="newLength">The new length of the buffer. Must be equal to or smaller than the current allocated buffer length.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void Shrink(int newLength)
        {
            if (newLength > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(newLength));
            }

            Length = newLength;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "Disposal semantics are different here; we are not freeing memory but instead returning objects to a pool")]
        public void Dispose()
        {
            if (_sourceBin == BufferPool<T>.UNMAPPED_POOL)
            {
                // don't bother returning buffers that are not actually part of the pool.
                // This is especially important for the singleton empty buffer.
                return;
            }

            //Debug.WriteLine($"Disposing of {typeof(T)} buffer {Id}");

            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                // We can theoretically just ignore this, but it's set this way to make sure that users of pooled buffer are very very fastidious
                // about who owns what buffer and when they get disposed.
                //Debug.WriteLine($"ERROR: Double disposal detected on {typeof(T)} buffer {Id}");
                throw new ObjectDisposedException(nameof(PooledBuffer<T>), "Multiple disposal of PooledBuffer detected! You must dispose of buffers exactly once");
            }

            BufferPool<T>.ReturnBuffer(this);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }
    }
}
