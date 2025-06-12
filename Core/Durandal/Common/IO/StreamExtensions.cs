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
    /// Extension methods for <see cref="Stream"/>.
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Variant of <see cref="Stream.CopyTo(Stream)"/> which uses a pooled buffer
        /// internally so it has better allocation performance.
        /// </summary>
        /// <param name="source">The source stream</param>
        /// <param name="destination">The stream being copied to</param>
        public static void CopyToPooled(this Stream source, Stream destination)
        {
            source.AssertNonNull(nameof(source));
            destination.AssertNonNull(nameof(destination));
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
            {
                int amountRead = 1;
                while (amountRead > 0)
                {
                    amountRead = source.Read(scratch.Buffer, 0, scratch.Buffer.Length);
                    if (amountRead > 0)
                    {
                        destination.Write(scratch.Buffer, 0, amountRead);
                    }
                }
            }
        }

        /// <summary>
        /// Variant of <see cref="Stream.CopyToAsync(Stream, int, CancellationToken)"/> which uses a pooled buffer
        /// internally so it has better allocation performance.
        /// </summary>
        /// <param name="source">The source stream</param>
        /// <param name="destination">The stream being copied to</param>
        /// <param name="cancelToken">A token for canceling the operation</param>
        public static async Task CopyToAsyncPooled(this Stream source, Stream destination, CancellationToken cancelToken)
        {
            source.AssertNonNull(nameof(source));
            destination.AssertNonNull(nameof(destination));
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
            {
                int amountRead = 1;
                while (amountRead > 0)
                {
                    amountRead = await source.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length, cancelToken).ConfigureAwait(false);
                    if (amountRead > 0)
                    {
                        await destination.WriteAsync(scratch.Buffer, 0, amountRead, cancelToken);
                    }
                }
            }
        }

        /// <summary>
        /// Variant of <see cref="Stream.CopyToAsync(Stream, int, CancellationToken)"/> which uses a pooled buffer
        /// internally so it has better allocation performance.
        /// </summary>
        /// <param name="source">The source stream</param>
        /// <param name="destination">The stream being copied to</param>
        /// <param name="cancelToken">A token for canceling the operation</param>
        /// <param name="realTime">A definition of real time.</param>
        public static async Task CopyToAsyncPooled(
            this NonRealTimeStream source,
            Stream destination,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            source.AssertNonNull(nameof(source));
            destination.AssertNonNull(nameof(destination));
            realTime.AssertNonNull(nameof(realTime));
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
            {
                int amountRead = 1;
                while (amountRead > 0)
                {
                    amountRead = await source.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length, cancelToken, realTime).ConfigureAwait(false);
                    if (amountRead > 0)
                    {
                        await destination.WriteAsync(scratch.Buffer, 0, amountRead, cancelToken);
                    }
                }
            }
        }

        /// <summary>
        /// Variant of <see cref="Stream.CopyToAsync(Stream, int, CancellationToken)"/> which uses a pooled buffer
        /// internally so it has better allocation performance.
        /// </summary>
        /// <param name="source">The source stream</param>
        /// <param name="destination">The stream being copied to</param>
        /// <param name="cancelToken">A token for canceling the operation</param>
        /// <param name="realTime">A definition of real time.</param>
        public static async Task CopyToAsyncPooled(
            this Stream source,
            NonRealTimeStream destination,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            source.AssertNonNull(nameof(source));
            destination.AssertNonNull(nameof(destination));
            realTime.AssertNonNull(nameof(realTime));
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
            {
                int amountRead = 1;
                while (amountRead > 0)
                {
                    amountRead = await source.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length, cancelToken).ConfigureAwait(false);
                    if (amountRead > 0)
                    {
                        await destination.WriteAsync(scratch.Buffer, 0, amountRead, cancelToken, realTime);
                    }
                }
            }
        }

        /// <summary>
        /// Variant of <see cref="Stream.CopyToAsync(Stream, int, CancellationToken)"/> which uses a pooled buffer
        /// internally so it has better allocation performance.
        /// </summary>
        /// <param name="source">The source stream</param>
        /// <param name="destination">The stream being copied to</param>
        /// <param name="cancelToken">A token for canceling the operation</param>
        /// <param name="realTime">A definition of real time.</param>
        public static async Task CopyToAsyncPooled(
            this NonRealTimeStream source,
            NonRealTimeStream destination,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            source.AssertNonNull(nameof(source));
            destination.AssertNonNull(nameof(destination));
            realTime.AssertNonNull(nameof(realTime));
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
            {
                int amountRead = 1;
                while (amountRead > 0)
                {
                    amountRead = await source.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length, cancelToken, realTime).ConfigureAwait(false);
                    if (amountRead > 0)
                    {
                        await destination.WriteAsync(scratch.Buffer, 0, amountRead, cancelToken, realTime);
                    }
                }
            }
        }
    }
}
