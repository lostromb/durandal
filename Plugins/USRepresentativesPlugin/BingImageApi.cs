using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Plugins.Plugins.USRepresentatives
{
    public class BingImageApi
    {
        private static readonly string API_KEY = "ddfb6d784ad140599088583d41160518";
        private static readonly string SERVICE_URL = "/bing/v5.0/images/search";
        private readonly IHttpClient _webClient;

        public BingImageApi(IPluginServices services)
        {
            _webClient = services.HttpClientFactory.CreateHttpClient(new Uri("https://api.cognitive.microsoft.com"));
        }

        public async Task<ImageSearchImage> GetRepresentativeImage(string query, ILogger queryLogger)
        {
            ImageSearchResult result = await BingImageSearch(query, queryLogger).ConfigureAwait(false);
            if (result == null || result.value == null || result.value.Count == 0)
            {
                return null;
            }

            return result.value[0];
        }

        /// <summary>
        /// Performs a Bing Image search and return the results as a SearchResult.
        /// </summary>
        private async Task<ImageSearchResult> BingImageSearch(string searchQuery, ILogger queryLogger)
        {
            using (HttpRequest webRequest = HttpRequest.CreateOutgoing(SERVICE_URL, "GET"))
            {
                webRequest.GetParameters["q"] = searchQuery;
                webRequest.GetParameters["count"] = "1";
                webRequest.GetParameters["safeSearch"] = "strict";
                webRequest.RequestHeaders["Ocp-Apim-Subscription-Key"] = API_KEY;
                using (NetworkResponseInstrumented<HttpResponse> apiResponse = await _webClient.SendInstrumentedRequestAsync(webRequest, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger).ConfigureAwait(false))
                {
                    try
                    {
                        if (apiResponse == null || !apiResponse.Success || apiResponse.Response.ResponseCode != 200)
                        {
                            return null;
                        }

                        // Extract Bing HTTP headers
                        //foreach (String header in response.Headers)
                        //{
                        //    if (header.StartsWith("BingAPIs-") || header.StartsWith("X-MSEdge-"))
                        //        searchResult.relevantHeaders[header] = response.Headers[header];
                        //}

                        ImageSearchResult returnVal = await apiResponse.Response.ReadContentAsJsonObjectAsync<ImageSearchResult>(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                        return returnVal;
                    }
                    finally
                    {
                        if (apiResponse != null)
                        {
                            await apiResponse.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        public class ImageSearchResult
        {
            public List<ImageSearchImage> value { get; set; }
        }

        public class ImageSearchImage
        {
            public string webSearchUrl { get; set; }
            public string name { get; set; }
            public string thumbnailUrl { get; set; }
            public string contentUrl { get; set; }
            public string hostPageUrl { get; set; }
        }
    }
}
