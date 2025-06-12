using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Time
{
    /// <summary>
    /// This interface defines a class which allows threads to wait on it with millisecond-precision.
    /// This is necessary because the default implementations of Task.Delay / Thread.Sleep usually have a
    /// resolution above 15 ms, so asking them to wait for 1 ms can cause unexpected behavior.
    /// (On Windows at least, this frequency seems to depend on whether the waiting thread is the main interactive
    /// thread or a background worker. We want to make sure we support high-precision waits regardless of thread attributes)
    /// </summary>
    public interface IHighPrecisionWaitProvider : IDisposable
    {
        /// <summary>
        /// Waits for the specified number of milliseconds, using a high precision timer if available.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to wait.</param>
        /// <param name="cancelToken">A cancellation token.</param>
        void Wait(double milliseconds, CancellationToken cancelToken);


        /// <summary>
        /// Waits for the specified number of milliseconds, using a high precision timer if available.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to wait.</param>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <returns>An async task</returns>
        Task WaitAsync(double milliseconds, CancellationToken cancelToken);
    }
}
