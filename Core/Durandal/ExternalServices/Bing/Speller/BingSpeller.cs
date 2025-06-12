using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Language;
using Durandal.Common.Statistics;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.ExternalServices.Bing.Speller
{
    /// <summary>
    /// Provides an HTTP interface into the Bing Speller API for providing spell corrections on query strings
    /// </summary>
    public class BingSpeller
    {
        private const int MAX_INTERMEDIATE_SUGGESTIONS = 1000;
        private const int MAX_SUGGESTIONS = 10;

        private readonly string _apiKey;
        private readonly IHttpClient _webClient;
        private readonly ILogger _logger;

        public BingSpeller(string apiKey, IHttpClientFactory clientFactory, ILogger logger)
        {
            _logger = logger;
            _apiKey = apiKey;
            _webClient = clientFactory.CreateHttpClient("api.cognitive.microsoft.com", 443, true, logger);
            _webClient.SetReadTimeout(TimeSpan.FromMilliseconds(10000));
        }

        /// <summary>
        /// Accepts an input string and sends it to Bing speller. This method will then return
        /// a (potentially empty) list of spelling suggestions for that string. Suggestions are
        /// not ranked. The output count is capped at 10.
        /// </summary>
        /// <param name="input">The input string</param>
        /// <param name="locale">The text locale, i.e. "en-US"</param>
        /// <param name="logger">A logger for tracing</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="preContext">Optional extra text that comes before the span that is under consideration</param>
        /// <param name="postContext">Optional extra text that comes after the span that is under consideration</param>
        /// <returns></returns>
        public async Task<IList<Hypothesis<string>>> SpellCorrect(
            string input,
            LanguageCode locale,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            string preContext = "",
            string postContext = "")
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.Length > 130)
            {
                logger.Log("Input text \"" + input + "\" sent to speller is longer than the maximum allowable length (130); suggestions may not work", LogLevel.Wrn);
            }

            IDictionary<string, string> postParams = new Dictionary<string, string>();
            postParams["text"] = input;
            if (!string.IsNullOrWhiteSpace(preContext))
            {
                postParams["preContextText"] = preContext;
            }
            if (!string.IsNullOrWhiteSpace(postContext))
            {
                postParams["postContextText"] = postContext;
            }

            using (HttpRequest request = HttpRequest.CreateOutgoing("/bing/v7.0/spellcheck", "POST"))
            {
                request.GetParameters["mkt"] = locale.ToBcp47Alpha2String();
                request.GetParameters["mode"] = "spell";
                request.RequestHeaders.Add("User-Agent", "Durandal-" + SVNVersionInfo.MajorVersion + "." + SVNVersionInfo.MinorVersion);
                request.RequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                request.SetContent(postParams);

                using (NetworkResponseInstrumented<HttpResponse> response = await _webClient.SendInstrumentedRequestAsync(request, cancelToken, realTime).ConfigureAwait(false))
                {
                    try
                    {
                        if (response == null || response.Response == null)
                        {
                            _logger.Log("Null response from speller service", LogLevel.Err);
                            return new List<Hypothesis<string>>();
                        }

                        if (response.Response.ResponseCode != 200)
                        {
                            _logger.Log("Error response from speller service: " + response.Response.ResponseCode + " " + response.Response.ResponseMessage, LogLevel.Err);
                            return new List<Hypothesis<string>>();
                        }

                        // Sample response: {"_type": "SpellCheck", "flaggedTokens": [{"offset": 0, "token": "recieve", "type": "UnknownToken", "suggestions": [{"suggestion": "receive", "score": 1}]}], "correctionType": "High"}
                        string payload = await response.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                        logger.Log(payload);
                        SpellerResponse checkResponse = JsonConvert.DeserializeObject<SpellerResponse>(payload);
                        if (checkResponse == null || checkResponse.flaggedTokens == null || checkResponse.flaggedTokens.Count == 0)
                        {
                            return new List<Hypothesis<string>>();
                        }

                        return ApplySuggestions(input, checkResponse);
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
        
        private static IList<Hypothesis<string>> ApplySuggestions(string originalText, SpellerResponse checkResponse)
        {
            int curOffset = 0;
            List<IntermediateSuggestion> intermediates = new List<IntermediateSuggestion>();
            IList<Hypothesis<string>> returnVal = new List<Hypothesis<string>>();

            int permutationCount = 1;
            foreach (FlaggedToken t in checkResponse.flaggedTokens)
            {
                if (t.suggestions.Count > 1)
                {
                    permutationCount *= t.suggestions.Count - 1;
                }
            }

            // Cap the permutations that can be returned
            permutationCount = Math.Min(MAX_INTERMEDIATE_SUGGESTIONS, permutationCount);

            // Initialize a pooled stringbuilder for every permutation that will go in the result
            for (int c = 0; c < permutationCount; c++)
            {
                intermediates.Add(new IntermediateSuggestion());
            }
            
            try
            {
                foreach (FlaggedToken token in checkResponse.flaggedTokens)
                {
                    // Prepend data
                    if (token.offset > curOffset)
                    {
                        foreach (IntermediateSuggestion s in intermediates)
                        {
                            s.SentenceBuilder.Append(originalText.Substring(curOffset, token.offset - curOffset));
                        }
                    }
                    curOffset = token.offset;

                    // Apply substitution
                    for (int x = 0; x < permutationCount; x++)
                    {
                        var suggestion = token.suggestions[x % token.suggestions.Count];
                        intermediates[x].SentenceBuilder.Append(suggestion.suggestion);
                        intermediates[x].Confidence *= suggestion.score;
                    }

                    curOffset += token.token.Length;
                }

                // Append the suffix, if necessary
                if (curOffset < originalText.Length)
                {
                    foreach (IntermediateSuggestion s in intermediates)
                    {
                        s.SentenceBuilder.Append(originalText.Substring(curOffset));
                    }
                }

                // Sort all intermediates by confidence, descending
                intermediates.Sort();

                // Convert the output to regular strings
                int numReturnValues = Math.Min(MAX_SUGGESTIONS, intermediates.Count);
                foreach (IntermediateSuggestion s in intermediates)
                {
                    returnVal.Add(new Hypothesis<string>(s.SentenceBuilder.ToString(), s.Confidence));
                    if (returnVal.Count >= numReturnValues)
                    {
                        break;
                    }
                }
            }
            finally
            {
                foreach (IntermediateSuggestion s in intermediates)
                {
                    s.Dispose();
                }
            }

            return returnVal;
        }

        private class IntermediateSuggestion : IComparable, IDisposable
        {
            private PooledStringBuilder _pooledBuilder;
            public StringBuilder SentenceBuilder;
            public float Confidence = 1.0f;

            public IntermediateSuggestion()
            {
                _pooledBuilder = StringBuilderPool.Rent();
                SentenceBuilder = _pooledBuilder.Builder;
            }

            public int CompareTo(object obj)
            {
                if (!(obj is IntermediateSuggestion))
                {
                    throw new InvalidCastException();
                }

                IntermediateSuggestion other = obj as IntermediateSuggestion;
                return Math.Sign(other.Confidence - Confidence);
            }

            public void Dispose()
            {
                _pooledBuilder.Dispose();
            }
        }
    }
}
