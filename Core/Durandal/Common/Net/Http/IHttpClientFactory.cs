using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http
{
    public interface IHttpClientFactory
    {
        /// <summary>
        /// Creates a platform-specific HTTP client which can access specific Web resources. The host URL, port, etc. need to be specified in advance and are fixed for the lifetime of the client.
        /// </summary>
        /// <param name="targetUrl">The target host URL to connect to (path does not matter)</param>
        /// <param name="logger">A logger that will serve as the default logger for HTTP requests made by the client, if no logger is passed at request time</param>
        /// <returns></returns>
        IHttpClient CreateHttpClient(Uri targetUrl, ILogger logger = null);

        /// <summary>
        /// Creates a platform-specific HTTP client which can access specific Web resources. The host URL, port, etc. need to be specified in advance and are fixed for the lifetime of the client.
        /// </summary>
        /// <param name="targetHostName">The target hostname</param>
        /// <param name="targetPort">The target port to connect to</param>
        /// <param name="secure">If true, use HTTPS</param>
        /// <param name="logger">A logger that will serve as the default logger for HTTP requests made by the client, if no logger is passed at request time</param>
        /// <returns></returns>
        IHttpClient CreateHttpClient(string targetHostName, int targetPort = 80, bool secure = false, ILogger logger = null);
    }
}
