using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Logger;

namespace Durandal.Common.Net.Http
{
    public class NullHttpClientFactory : IHttpClientFactory
    {
        public IHttpClient CreateHttpClient(Uri targetUrl, ILogger logger = null)
        {
            return new NullHttpClient();
        }

        public IHttpClient CreateHttpClient(string targetHostName, int targetPort = 80, bool secure = false, ILogger logger = null)
        {
            return new NullHttpClient();
        }
    }
}
