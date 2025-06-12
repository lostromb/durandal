using Durandal.Common.Utils;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Durandal.Common.Time;
using System.Threading;

namespace Durandal.ExternalServices
{
    /// <summary>
    /// Basic and kind of hackish interface to the TinyURL.com url shorten API.
    /// </summary>
    public class TinyUrl
    {
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;

        public TinyUrl(IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateHttpClient(new Uri("https://tinyurl.com"), _logger);
        }

        public async Task<Uri> ShortenUrl(Uri sourceUrl, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime, string alias = null)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing("/api-create.php", "GET"))
            {
                request.GetParameters["url"] = sourceUrl.AbsoluteUri;
                if (!string.IsNullOrEmpty(alias))
                {
                    request.GetParameters["alias"] = alias;
                }

                using (HttpResponse response = await _httpClient.SendRequestAsync(request, cancelToken, realTime, queryLogger).ConfigureAwait(false))
                {
                    try
                    {
                        if (response == null || response.ResponseCode != 200)
                        {
                            return null;
                        }

                        // The entire response body is just the shortened URL
                        string responsePage = await response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                        Uri returnVal;
                        if (string.IsNullOrEmpty(responsePage))
                        {
                            return null;
                        }
                        else if (!Uri.TryCreate(responsePage, UriKind.Absolute, out returnVal))
                        {
                            return null;
                        }

                        return returnVal;
                    }
                    finally
                    {
                        if (response != null)
                        {
                            await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
        }
    }
}
