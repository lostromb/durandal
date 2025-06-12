using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Default implementation of an HTTP client factory based on the runtime's built-in HttpClient and HttpClientHandler.
    /// </summary>
    public class PortableHttpClientFactory : IHttpClientFactory
    {
        /// <summary>
        /// The single static "core" object which underlies all HttpClient objects created by this factory
        /// </summary>
        private static readonly HttpClientHandler SHARED_HTTP_HANDLER = new HttpClientHandler();

        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;

        /// <summary>
        /// Constructs a new <see cref="PortableHttpClientFactory"/>. This default constructor
        /// does not setup metric collection for outgoing HTTP requests.
        /// </summary>
        public PortableHttpClientFactory()
            : this(NullMetricCollector.WeakSingleton, DimensionSet.Empty)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="PortableHttpClientFactory"/>.
        /// </summary>
        /// <param name="metrics">A collector for reporting metrics.</param>
        /// <param name="metricDimensions">Metric dimensions to use with this object.</param>
        public PortableHttpClientFactory(WeakPointer<IMetricCollector> metrics, DimensionSet metricDimensions)
        {
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
        }

        public IHttpClient CreateHttpClient(Uri url, ILogger logger = null)
        {
            return new PortableHttpClient(url, logger, _metrics, _metricDimensions, SHARED_HTTP_HANDLER);
        }

        public IHttpClient CreateHttpClient(string targetHostName, int targetPort = 80, bool secure = false, ILogger logger = null)
        {
            return new PortableHttpClient(targetHostName, targetPort, secure, logger, _metrics, _metricDimensions, SHARED_HTTP_HANDLER);
        }
    }
}
