using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.WebSocket
{
    /// <summary>
    /// Additional parameters associated with a new websocket client connection.
    /// </summary>
    public class WebSocketConnectionParams
    {
        /// <summary>
        /// A set of subprotocols acceptable to the client. May be null. If sent, the server
        /// will either select one of the available protocols given, or reject the request
        /// if none are supported.
        /// </summary>
        public IReadOnlyCollection<string> AvailableProtocols { get; set; }

        /// <summary>
        /// A set of additional headers to send in the initial websocket upgrade request. May be null.
        /// </summary>
        public IHttpHeaders AdditionalHeaders { get; set; }
    }
}
