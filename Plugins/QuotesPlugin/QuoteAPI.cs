using Durandal.Common.Utils;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Internal.CoreOntology.SchemaDotOrg;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Durandal.Common.Time;
using System.Threading;

namespace Durandal.Plugins.Quotes
{
    public static class QuoteAPI
    {
        private static readonly Regex QUOTE_RIPPER = new Regex("<div class=\\\"author-quote\\\"><a href=\\\"/quote/[\\d]+\\\">(.+?)</a>");

        internal static async Task<QuotesResult> GetQuotes(SchemaDotOrg.Person author, IHttpClient httpClient, ILogger logger)
        {
            QuotesResult returnVal = new QuotesResult()
            {
                Author = author,
                Quotes = new List<string>()
            };

            string query = author.Name.Value;
            IList<QuotesSearchSuggestion> qf = await RunQF(query, httpClient, logger).ConfigureAwait(false);
            if (qf == null || qf.Count == 0)
            {
                return returnVal;
            }

            QuotesSearchSuggestion topAuthor = qf.FirstOrDefault((b) => b.category.Equals("Authors"));
            string link = topAuthor.link.AbsolutePath.Replace("//", "/");
            using (HttpRequest request = HttpRequest.CreateOutgoing(link))
            using (NetworkResponseInstrumented<HttpResponse> netResp = await httpClient.SendInstrumentedRequestAsync(
                request,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton,
                logger).ConfigureAwait(false))
            {
                try
                {
                    if (!netResp.Success)
                    {
                        return returnVal;
                    }

                    string html = await netResp.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                    foreach (Match m in QUOTE_RIPPER.Matches(html))
                    {
                        returnVal.Quotes.Add(m.Groups[1].Value);
                    }

                    return returnVal;
                }
                finally
                {
                    if (netResp != null)
                    {
                        await netResp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }
        }

        private class QuotesSearchSuggestion
        {
#pragma warning disable 649
            public long id;
            public string term;
            public string value;
            public Uri link;
            public string category;
#pragma warning restore 649
        }

        private static async Task<IList<QuotesSearchSuggestion>> RunQF(string query, IHttpClient httpClient, ILogger logger)
        {
            HttpRequest request = HttpRequest.CreateOutgoing("/gw.php", "POST");
            request.SetContent(new Dictionary<string, string>()
            {
                { "action", "get_ac" },
                { "term", query},
                { "type", "1" }
            });

            using (NetworkResponseInstrumented<HttpResponse> netResp = await httpClient.SendInstrumentedRequestAsync(
                request,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton,
                logger).ConfigureAwait(false))
            {
                try
                {
                    if (!netResp.Success)
                    {
                        return new List<QuotesSearchSuggestion>();
                    }

                    string json = await netResp.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<List<QuotesSearchSuggestion>>(json);
                }
                finally
                {
                    if (netResp != null)
                    {
                        await netResp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
