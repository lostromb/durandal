using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Utils;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.ExternalServices.Bing
{
    public class BingTranslator : IDisposable
    {
        private static readonly Regex _stringContentParser = new Regex("<string.*?>(.+?)</string>");

        private readonly IHttpClient _tokenClient;
        private readonly IHttpClient _serviceClient;
        private readonly ILogger _logger;
        private readonly string _apiKey;
        private TokenRefresher<string> _tokenRefresher;
        private int _disposed = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="BingTranslator"/> class.
        /// </summary>
        public BingTranslator(string apiKey, ILogger logger, IHttpClientFactory httpClientFactory, IRealTimeProvider realTime)
        {
            _apiKey = apiKey;
            _logger = logger;
            _tokenClient = httpClientFactory.CreateHttpClient("api.cognitive.microsoft.com", 443, true, logger.Clone("BingTranslateTokenRefresh"));
            _serviceClient = httpClientFactory.CreateHttpClient("api.microsofttranslator.com", 443, true, logger.Clone("BingTranslate"));
            _tokenRefresher = new TokenRefresher<string>(logger, RefreshAuthToken, realTime);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~BingTranslator()
        {
            Dispose(false);
        }
#endif

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _tokenRefresher?.Dispose();
                _tokenClient?.Dispose();
                _serviceClient?.Dispose();
            }
        }

        /// <summary>
        /// Uses Bing Translate API to translate text between languages
        /// </summary>
        /// <param name="text">The text to be translated</param>
        /// <param name="logger">A logger</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="targetLang">Target language, as an ISO language identifier e.g. "en", "ru", "zh-CHS"</param>
        /// <param name="sourceLang">Source language, as an ISO language identifier e.g. "en", "ru", "zh-CHS", or leave null to auto-detect the source language</param>
        /// <returns></returns>
        public async Task<string> TranslateText(
            string text,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            LanguageCode targetLang,
            LanguageCode sourceLang = null)
        {
            string token = await _tokenRefresher.GetToken(logger, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token))
            {
                logger.Log("Could not retrieve access token for Translate request!", LogLevel.Err);
                return text;
            }

            using (HttpRequest request = HttpRequest.CreateOutgoing("/V2/Http.svc/Translate", "GET"))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + token);
                request.GetParameters.Add("text", text);
                request.GetParameters.Add("to", targetLang.Iso639_1);

                if (sourceLang != null)
                {
                    request.GetParameters.Add("from", sourceLang.Iso639_1);
                }

                using (NetworkResponseInstrumented<HttpResponse> response = await _serviceClient.SendInstrumentedRequestAsync(request, cancelToken, realTime, logger).ConfigureAwait(false))
                {
                    try
                    {
                        if (response.Success && response.Response != null)
                        {
                            if (response.Response.ResponseCode == 200)
                            {
                                string content = await response.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                                // expected payload: <string xmlns="http://schemas.microsoft.com/2003/10/Serialization/">Это тест системы аварийного вещания.</string>
                                string translation = StringUtils.RegexRip(_stringContentParser, content, 1, logger);
                                return translation;
                            }
                            else
                            {
                                string error = await response.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                                logger.Log("Translation call failed! " + error, LogLevel.Err);
                            }
                        }
                        else
                        {
                            logger.Log("Translation call failed!", LogLevel.Err);
                        }

                        return text;
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

        /// <summary>
        /// Uses Bing Translate API to infer the language of a piece of text
        /// </summary>
        /// <param name="text">The text to examine</param>
        /// <param name="logger">A logger</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The language code of the detected language</returns>
        public async Task<LanguageCode> DetectLanguage(string text, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            string token = await _tokenRefresher.GetToken(logger, realTime, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token))
            {
                logger.Log("Could not retrieve access token for Translate request!", LogLevel.Err);
                return null;
            }

            using (HttpRequest request = HttpRequest.CreateOutgoing("/V2/Http.svc/Detect", "GET"))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + token);
                request.GetParameters.Add("text", text);

                using (NetworkResponseInstrumented<HttpResponse> response = await _serviceClient.SendInstrumentedRequestAsync(request, cancelToken, realTime, logger).ConfigureAwait(false))
                {
                    try
                    {
                        if (response.Success && response.Response != null)
                        {
                            if (response.Response.ResponseCode == 200)
                            {
                                string content = await response.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                                // expected payload: <string xmlns="http://schemas.microsoft.com/2003/10/Serialization/">en</string>
                                string translation = StringUtils.RegexRip(_stringContentParser, content, 1, logger);
                                return LanguageCode.TryParse(translation);
                            }
                            else
                            {
                                string error = await response.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                                logger.Log("Detection call failed! " + error, LogLevel.Err);
                            }
                        }
                        else
                        {
                            logger.Log("Detection call failed!", LogLevel.Err);
                        }

                        return null;
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

        private async Task<TokenRefreshResult<string>> RefreshAuthToken(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            try
            {
                using (HttpRequest request = HttpRequest.CreateOutgoing("/sts/v1.0/issueToken", "POST"))
                {
                    request.RequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                    using (NetworkResponseInstrumented<HttpResponse> tokenResponse = await _tokenClient.SendInstrumentedRequestAsync(request, cancelToken, realTime).ConfigureAwait(false))
                    {
                        try
                        {
                            if (tokenResponse.Success && tokenResponse.Response != null)
                            {
                                if (tokenResponse.Response.ResponseCode == 200)
                                {
                                    string token = await tokenResponse.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                                    return new TokenRefreshResult<string>(token, TimeSpan.FromMinutes(10));
                                }
                                else if (tokenResponse.Response.ResponseCode == 404 || tokenResponse.Response.ResponseCode == 419)
                                {
                                    return new TokenRefreshResult<string>(false, "Response code " + tokenResponse.Response.ResponseCode + " from token service: " + (await tokenResponse.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false)));
                                }
                                else
                                {
                                    return new TokenRefreshResult<string>(true, "Response code " + tokenResponse.Response.ResponseCode + " from token service: " + (await tokenResponse.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false)));
                                }
                            }
                            else
                            {
                                return new TokenRefreshResult<string>(false, "Null response from token service");
                            }
                        }
                        finally
                        {
                            if (tokenResponse != null)
                            {
                                await tokenResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return new TokenRefreshResult<string>(false, "Exception while refreshing Bing Translate token: " + e.GetDetailedMessage());
            }
        }
    }
}
