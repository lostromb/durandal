using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    /// <summary>
    /// An interface that defines a server which can be started/stopped and which listens on a certain (implementation-specific) endpoint
    /// </summary>
    public interface IServer : IDisposable
    {
        /// <summary>
        /// Starts a server
        /// </summary>
        /// <param name="serverName">The debugging name of this server</param>
        /// <param name="cancelToken">A token to cancel the startup operation (_not_ to shutdown the server later).</param>
        /// <param name="realTime">A definition of real time</param>
        Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Stops a server in a timely fashion.
        /// <param name="cancelToken">A token to cancel the stop operation. Intended as a last resort to prevent infinite hangs.</param>
        /// <param name="realTime">A definition of real time</param>
        /// </summary>
        Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// A list of IP endpoints that this server is bound to.
        /// </summary>
        IEnumerable<ServerBindingInfo> Endpoints { get; }

        /// <summary>
        /// Indicates if this server is running
        /// </summary>
        bool Running { get; }
    }
}
