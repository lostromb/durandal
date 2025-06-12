using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.Net.Http;
using Durandal.Common.Logger;

namespace Durandal.Common.Net.WebSocket
{
    /// <summary>
    /// A factory interface for something that can create websocket connections as a client to
    /// a remote server.
    /// </summary>
    public interface IWebSocketClientFactory
    {
        /// <summary>
        /// Opens a client websocket connection to the given host.
        /// </summary>
        /// <param name="logger">A logger that will be associated with the connection.</param>
        /// <param name="remoteConfig">The connection information for the remote server.</param>
        /// <param name="uriPath">The URI path component to request, e.g. "/ws/chat?vnext=true"</param>
        /// <param name="cancelToken">A cancel token for the operation.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <param name="additionalParams">Additional optional connection parameters.</param>
        /// <returns>An newly established web socket connection.</returns>
        Task<IWebSocket> OpenWebSocketConnection(
            ILogger logger,
            TcpConnectionConfiguration remoteConfig,
            string uriPath,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            WebSocketConnectionParams additionalParams = null);
    }
}
