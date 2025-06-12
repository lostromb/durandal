using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    public interface ISocketFactory : IDisposable
    {
        /// <summary>
        /// Creates a new TCP socket connection to a host. This method signature is deprecated
        /// in favor of the one which accepts a <see cref="TcpConnectionConfiguration"/>.
        /// </summary>
        /// <param name="hostname">The hostname (DNS or IP) to connect to</param>
        /// <param name="port">The port to use for the connection</param>
        /// <param name="secure">If true, establish an SSL-enabled socket</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="traceLogger">A logger for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>A socket which has an opened connection to the specified endpoint</returns>
        Task<ISocket> Connect(
            string hostname,
            int port,
            bool secure,
            ILogger traceLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null);

        /// <summary>
        /// Creates a new TCP socket connection to a host with specific parameters
        /// </summary>
        /// <param name="connectionConfig">The connection parameters</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="traceLogger">A logger for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>A socket which has an opened connection to the specified endpoint</returns>
        Task<ISocket> Connect(
            TcpConnectionConfiguration connectionConfig,
            ILogger traceLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null);
    }
}
