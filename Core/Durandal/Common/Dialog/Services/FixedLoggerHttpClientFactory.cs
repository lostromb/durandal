using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Dialog.Services
{
    /// <summary>
    /// HTTP client factory which doesn't allow overriding of its ILogger instance.
    /// </summary>
    public class FixedLoggerHttpClientFactory : IHttpClientFactory
    {
        private IHttpClientFactory _baseFactory;
        private ILogger _logger;

        public FixedLoggerHttpClientFactory(IHttpClientFactory baseFactory, ILogger logger)
        {
            _baseFactory = baseFactory;
            _logger = logger;
        }

        public IHttpClient CreateHttpClient(Uri uri, ILogger logger = null)
        {
            return _baseFactory.CreateHttpClient(uri, _logger);
        }

        public IHttpClient CreateHttpClient(string targetHostName, int targetPort = 80, bool secure = false, ILogger logger = null)
        {
            return _baseFactory.CreateHttpClient(targetHostName, targetPort, secure, _logger);
        }
    }
}
