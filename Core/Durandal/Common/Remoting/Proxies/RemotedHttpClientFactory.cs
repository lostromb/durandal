using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Proxies
{
    public class RemotedHttpClientFactory : IHttpClientFactory
    {
        private readonly RemoteDialogMethodDispatcher _dispatcher;
        private readonly IRealTimeProvider _realTime;
        private readonly ILogger _fallbackLogger;

        public RemotedHttpClientFactory(RemoteDialogMethodDispatcher dispatcher, IRealTimeProvider realTime, ILogger fallbackLogger)
        {
            _dispatcher = dispatcher;
            _realTime = realTime;
            _fallbackLogger = fallbackLogger;
        }

        public IHttpClient CreateHttpClient(Uri targetUrl, ILogger logger = null)
        {
            return new RemotedHttpClient(targetUrl, logger ?? _fallbackLogger, _dispatcher, _realTime);
        }

        public IHttpClient CreateHttpClient(string targetHostName, int targetPort = 80, bool secure = false, ILogger logger = null)
        {
            return new RemotedHttpClient(new Uri(string.Format("{0}://{1}:{2}", secure ? "https" : "http", targetHostName, targetPort)),
                logger ?? _fallbackLogger,
                _dispatcher,
                _realTime);
        }
    }
}
