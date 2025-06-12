using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net
{
    /// <summary>
    /// Interface for creating server sockets.
    /// A "server socket" in this context is typically used for IPC; it represents a single listening endpoint on the local machine that another
    /// process can connect to and start exchanging data. For the more traditional multi-connection server like a web server, see <see cref="IServer"/>.
    /// </summary>
    public interface IServerSocketFactory
    {
        /// <summary>
        /// Opens a listening socket on the local machine for other processes to connect to. This can
        /// be an anonymous pipe, memory-mapped file, or loopback network connection.
        /// </summary>
        /// <param name="logger">A logger</param>
        /// <param name="metrics">Metric collector</param>
        /// <param name="metricDimensions">Metric dimensions</param>
        /// <param name="minimumBufferSizeBytes">The minimum buffer size to allocate for the pipe.</param>
        /// <returns>A newly bound socket.</returns>
        ISocket CreateServerSocket(
            ILogger logger,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            int minimumBufferSizeBytes = -1);
    }
}
