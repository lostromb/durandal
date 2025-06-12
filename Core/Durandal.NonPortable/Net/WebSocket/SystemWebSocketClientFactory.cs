using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DurandalWS = Durandal.Common.Net.WebSocket;
using SystemWS = System.Net.WebSockets;

namespace Durandal.Common.Net.WebSocket
{
    public class SystemWebSocketClientFactory : DurandalWS.IWebSocketClientFactory
    {
        /// <inheritdoc />
        public async Task<IWebSocket> OpenWebSocketConnection(
            ILogger logger,
            TcpConnectionConfiguration remoteConfig,
            string uriPath,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            WebSocketConnectionParams additionalParams = null)
        {
            remoteConfig.AssertNonNull(nameof(remoteConfig));
            logger.AssertNonNull(nameof(logger));
            int port = remoteConfig.Port.GetValueOrDefault(0);
            if (port == 0)
            {
                if (remoteConfig.UseTLS)
                {
                    port = 443;
                }
                else
                {
                    port = 80;
                }
            }

            if (string.IsNullOrEmpty(uriPath))
            {
                uriPath = "/";
            }

            Uri remoteHost = new Uri(
                string.Format("{0}://{1}:{2}{3}",
                    remoteConfig.UseTLS ? "wss" : "ws",
                    remoteConfig.DnsHostname,
                    port,
                    uriPath));

            SystemWS.ClientWebSocket socket = new SystemWS.ClientWebSocket();

            if (additionalParams != null)
            {
                if (additionalParams.AvailableProtocols != null)
                {
                    foreach (string subProtocol in additionalParams.AvailableProtocols)
                    {
                        socket.Options.AddSubProtocol(subProtocol);
                    }
                }

                if (additionalParams.AdditionalHeaders != null)
                {
                    foreach (var headerPair in additionalParams.AdditionalHeaders)
                    {
                        foreach (string headerValue in headerPair.Value)
                        {
                            socket.Options.SetRequestHeader(headerPair.Key, headerValue);
                        }
                    }
                }
            }

            logger.LogFormat(
                LogLevel.Std,
                DataPrivacyClassification.EndUserPseudonymousIdentifiers,
                "Opening websocket connection to {0}",
                remoteHost);

            await socket.ConnectAsync(remoteHost, cancelToken).ConfigureAwait(false);
            return new SystemWebSocketWrapper(socket);
        }
    }
}
