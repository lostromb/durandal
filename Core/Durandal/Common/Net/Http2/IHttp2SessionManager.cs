using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using Durandal.Common.Net.Http;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Net.Http2
{
    public interface IHttp2SessionManager : IDisposable
    {
        /// <summary>
        /// Attempts to establish an HTTP/2 session with a given host. If one
        /// is already established, return the existing session. If the remote host
        /// does not support HTTP/2, return a plain socket connection with that host.
        /// </summary>
        /// <param name="socketFactory"></param>
        /// <param name="connectionConfig"></param>
        /// <param name="sessionPreferences"></param>
        /// <param name="logger"></param>
        /// <param name="metrics">A metric collector</param>
        /// <param name="metricDimensions">Metric dimensions to use for this session</param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        Task<Http2SessionInitiationResult> TryCreateH2Session(
            ISocketFactory socketFactory,
            TcpConnectionConfiguration connectionConfig,
            Http2SessionPreferences sessionPreferences,
            ILogger logger,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            CancellationToken cancelToken,
            IRealTimeProvider realTime);

        /// <summary>
        /// Returns either an active, preexisting H2 session with the given host,
        /// or null if no connection exists.
        /// </summary>
        /// <param name="connectionConfig"></param>
        /// <param name="logger"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        Http2Session CheckForExistingH2Session(
            TcpConnectionConfiguration connectionConfig,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime);

        /// <summary>
        /// Given that we are a socket client which has just received an HTTP 101 Switching Protocols
        /// response from an HTTP2-enabled server, this method will establish a new H2 session on the
        /// currently open socket, and return an HttpResponse object representing the server's
        /// response to the HTTP/1.1 request with optional upgrade which we originally made.
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="connectionConfig"></param>
        /// <param name="sessionPreferences"></param>
        /// <param name="logger"></param>
        /// <param name="clientSettings"></param>
        /// <param name="metrics"></param>
        /// <param name="metricDimensions"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        Task<HttpResponse> CreateH2ClientSessionFromUpgrade(
            ISocket socket,
            TcpConnectionConfiguration connectionConfig,
            Http2SessionPreferences sessionPreferences,
            ILogger logger,
            Http2Settings clientSettings,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            CancellationToken cancelToken,
            IRealTimeProvider realTime);
    }
}
