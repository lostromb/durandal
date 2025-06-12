using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http2;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http
{
    public class SocketHttpClientFactory : IHttpClientFactory
    {
        private static readonly Http2SessionPreferences DEFAULT_HTTP2_CLIENT_PREFERENCES = new Http2SessionPreferences()
        {
            // Magic number representing a "generous" bandwidth allocation of 12MB.
            // This is close to what Firefox uses in its h2 client.
            DesiredGlobalConnectionFlowWindow = 12_582_912,
            MaxIdleTime = TimeSpan.FromSeconds(300),
            MaxPromisedStreamsToStore = 0,
            MaxStreamId = null,
            OutgoingPingInterval = TimeSpan.FromSeconds(60),
            PromisedStreamTimeout = TimeSpan.FromSeconds(5),
            SettingsTimeout = TimeSpan.FromSeconds(5),
        };

        private readonly WeakPointer<ISocketFactory> _socketFactory;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly WeakPointer<IHttp2SessionManager> _h2SessionManager;
        private readonly Http2SessionPreferences _h2SessionPreferences;

        public SocketHttpClientFactory(
            WeakPointer<ISocketFactory> socketFactory,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            WeakPointer<IHttp2SessionManager> h2SessionManager,
            Http2SessionPreferences h2SessionPreferences = null)
        {
            _socketFactory = socketFactory.AssertNonNull(nameof(socketFactory));
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            _h2SessionManager = h2SessionManager.AssertNonNull(nameof(h2SessionManager));
            _h2SessionPreferences = h2SessionPreferences ?? DEFAULT_HTTP2_CLIENT_PREFERENCES;
        }

        public IHttpClient CreateHttpClient(Uri url, ILogger logger = null)
        {
            return new SocketHttpClient(_socketFactory, url, logger ?? NullLogger.Singleton, _metrics, _metricDimensions, _h2SessionManager, _h2SessionPreferences);
        }

        public IHttpClient CreateHttpClient(string targetHostName, int targetPort = 80, bool secure = false, ILogger logger = null)
        {
            if (targetPort <= 0)
            {
                throw new ArgumentOutOfRangeException("TCP port is invalid");
            }

            TcpConnectionConfiguration config = new TcpConnectionConfiguration()
            {
                DnsHostname = targetHostName,
                Port = targetPort,
                UseTLS = secure,
                ReportHttp2Capability = true
            };

            return new SocketHttpClient(_socketFactory, config, logger ?? NullLogger.Singleton, _metrics, _metricDimensions, _h2SessionManager, _h2SessionPreferences);
        }
    }
}
