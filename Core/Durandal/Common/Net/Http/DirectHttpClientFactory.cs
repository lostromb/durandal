using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    public class DirectHttpClientFactory : IHttpClientFactory
    {
        private readonly IHttpServerDelegate _defaultTarget;
        private readonly IDictionary<string, IHttpServerDelegate> _virtualHosts;

        /// <summary>
        /// Creates a http client factory which will hand requests in-memory directly to one or more server delegates
        /// </summary>
        /// <param name="defaultTarget">The default host to route http requests to</param>
        /// <param name="virtualHosts">A dictionary of virtual hosts to try and match against.
        /// Any HTTP client request to one of the matching hosts will generate a client which talks to that delegate.
        /// The key is a string-sensitive DNS authority name, such as "durandal-ai.net"</param>
        public DirectHttpClientFactory(IHttpServerDelegate defaultTarget, IDictionary<string, IHttpServerDelegate> virtualHosts = null)
        {
            _defaultTarget = defaultTarget;
            _virtualHosts = virtualHosts ?? new Dictionary<string, IHttpServerDelegate>();
        }

        public IHttpClient CreateHttpClient(Uri targetUrl, ILogger logger = null)
        {
            IHttpServerDelegate target;
            if (!_virtualHosts.TryGetValue(targetUrl.Authority, out target))
            {
                target = _defaultTarget;
            }
            
            if (target == null)
            {
                return new NullHttpClient();
            }

            return new DirectHttpClient(target);
        }

        public IHttpClient CreateHttpClient(string targetHostName, int targetPort = 80, bool secure = false, ILogger logger = null)
        {
            IHttpServerDelegate target;
            if (!_virtualHosts.TryGetValue(targetHostName, out target))
            {
                target = _defaultTarget;
            }

            if (target == null)
            {
                return new NullHttpClient();
            }

            return new DirectHttpClient(target);
        }
    }
}
