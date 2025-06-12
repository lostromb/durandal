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
    /// Defines a stream that needs to be finalized in some way (by calling Finish()) with semantics somewhat in between Flush() and Dispose().
    /// This is typically used for stream implementations that need to append some kind of trailer at the end of data,
    /// for example, a block transformation scheme like AES encryption that needs to pad + finish the final block.
    /// </summary>
    public abstract class FinalizableStream : NonRealTimeStream
    {
        /// <summary>
        /// Signals that this stream has finished writing data, but is not quite disposed yet.
        /// After this method is called, the stream is still accessible, but no more data can be written.
        /// </summary>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        public abstract void Finish(CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Signals that this stream has finished writing data, but is not quite disposed yet.
        /// After this method is called, the stream is still accessible, but no more data can be written.
        /// </summary>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>An async task.</returns>
        public abstract Task FinishAsync(CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}
