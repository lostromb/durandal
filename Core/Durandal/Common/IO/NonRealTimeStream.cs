using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Defines an abstract <see cref="Stream"/> that allows reads and writes using non-realtime semantics.
    /// </summary>
    public abstract class NonRealTimeStream : Stream
    {
        /// <summary>
        /// Reads data from this stream in a way that is aware of augmented real time
        /// </summary>
        /// <param name="targetBuffer">The buffer to read data into</param>
        /// <param name="offset">The offset when reading into the target buffer</param>
        /// <param name="count">The maximum number of bytes to read</param>
        /// <param name="cancelToken">A cancel token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The number of bytes read, or zero for end of stream</returns>
        public abstract int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Reads data from this stream asynchronously in a way that is aware of augmented real time
        /// </summary>
        /// <param name="targetBuffer">The buffer to read data into</param>
        /// <param name="offset">The offset when reading into the target buffer</param>
        /// <param name="count">The maximum number of bytes to read</param>
        /// <param name="cancelToken">A cancel token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The number of bytes read, or zero for end of stream</returns>
        public abstract Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Writes data to this stream in a way that is aware of augmented real time.
        /// </summary>
        /// <param name="sourceBuffer">The data to be written</param>
        /// <param name="offset">The offset when reading from source buffer</param>
        /// <param name="count">The exact number of bytes to read</param>
        /// <param name="cancelToken">A cancel token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        public abstract void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Writes data to this stream asynchronously in a way that is aware of augmented real time.
        /// </summary>
        /// <param name="sourceBuffer">The data to be written</param>
        /// <param name="offset">The offset when reading from source buffer</param>
        /// <param name="count">The exact number of bytes to read</param>
        /// <param name="cancelToken">A cancel token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>A task representing execution</returns>
        public abstract Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Non-real-time aware implementation of FlushAsync().
        /// </summary>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>An async task.</returns>
        public virtual Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return FlushAsync(cancelToken);
        }
    }
}
