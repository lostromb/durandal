using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Audio;
using Durandal.Common.Collections;
using Durandal.Common.Speech.SR;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.CognitiveServices.Speech
{
    /// <summary>
    /// Speech recognizer backed by the Microsoft.CognitiveServices.Speech package
    /// </summary>
    public class AzureNativeSpeechRecognizerFactory : ISpeechRecognizerFactory
    {
        private readonly string _apiKey;
        private readonly string _region;
        private readonly ILogger _logger;
        private readonly IHttpClient _tokenRefreshClient;
        private readonly TokenRefresher<string> _tokenRefresher;

        private static readonly IReadOnlySet<LanguageCode> SUPPORTED_LOCALES = new ReadOnlySetWrapper<LanguageCode>(
            new HashSet<LanguageCode>(new LanguageCode[]
            {
                LanguageCode.Parse("ar-eg"),
                LanguageCode.Parse("ca-es"),
                LanguageCode.Parse("da-dk"),
                LanguageCode.Parse("de-de"),
                LanguageCode.Parse("en-au"),
                LanguageCode.Parse("en-ca"),
                LanguageCode.Parse("en-gb"),
                LanguageCode.Parse("en-in"),
                LanguageCode.Parse("en-nz"),
                LanguageCode.Parse("en-us"),
                LanguageCode.Parse("es-es"),
                LanguageCode.Parse("es-mx"),
                LanguageCode.Parse("fi-fi"),
                LanguageCode.Parse("fr-ca"),
                LanguageCode.Parse("fr-fr"),
                LanguageCode.Parse("hi-in"),
                LanguageCode.Parse("it-it"),
                LanguageCode.Parse("ja-jp"),
                LanguageCode.Parse("ko-kr"),
                LanguageCode.Parse("nb-no"),
                LanguageCode.Parse("nl-nl"),
                LanguageCode.Parse("pl-pl"),
                LanguageCode.Parse("pt-br"),
                LanguageCode.Parse("pt-pt"),
                LanguageCode.Parse("ru-ru"),
                LanguageCode.Parse("sv-se"),
                LanguageCode.Parse("zh-cn"),
                LanguageCode.Parse("zh-hk"),
                LanguageCode.Parse("zh-tw")
            }));

        /// <summary>
        /// Creates a new speech recognizer factory
        /// </summary>
        /// <param name="tokenRefreshClientFactory">An HTTP client to use during the token refresh</param>
        /// <param name="logger">A default logger</param>
        /// <param name="apiKey">The API key IN THE FORMAT "region=westus2;key=xxxxx". If only an API key is given then the westus2 region will be assumed.</param>
        /// <param name="realTime">Real time definition</param>
        public AzureNativeSpeechRecognizerFactory(IHttpClientFactory tokenRefreshClientFactory, ILogger logger, string apiKey, IRealTimeProvider realTime)
        {
            _logger = logger;
            Durandal.Common.Speech.SR.Azure.AzureSpeechRecognizerFactory.ParseApiKeyAndRegion(apiKey, out _apiKey, out _region);
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException("API key is null");
            }
            if (string.IsNullOrEmpty(_region))
            {
                _region = "westus2";
            }

            _tokenRefreshClient = tokenRefreshClientFactory.CreateHttpClient(_region + ".api.cognitive.microsoft.com", 443, true, _logger.Clone("SRTokenRefreshClient"));

            // Start a long-running task that will continually refresh the SR token proactively.
            _tokenRefresher = new TokenRefresher<string>(_logger.Clone("SRTokenRefresh"), RefreshToken, realTime);
        }

        /// <inheritdoc />
        public async Task<ISpeechRecognizer> CreateRecognitionStream(WeakPointer<IAudioGraph> audioGraph, string graphNodeName, LanguageCode locale, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (queryLogger == null)
            {
                queryLogger = _logger;
            }

            if (!IsLocaleSupported(locale))
            {
                queryLogger.Log("The locale \"" + locale + "\" is not supported by Azure speech reco", LogLevel.Err);
                return null;
            }

            string authToken = await _tokenRefresher.GetToken(queryLogger, realTime, TimeSpan.FromSeconds(3));

            if (authToken == null)
            {
                queryLogger.Log("Auth token is null - SR cannot continue", LogLevel.Err);
                return null;
            }
            
            return await AzureNativeSpeechRecognizer.Create(audioGraph, graphNodeName, queryLogger, authToken, _region, locale);
        }

        public void Dispose() { }

        public bool IsLocaleSupported(LanguageCode locale)
        {
            if (locale == null)
            {
                return false;
            }

            return SUPPORTED_LOCALES.Contains(locale);
        }

        private async Task<TokenRefreshResult<string>> RefreshToken(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            try
            {
                using (HttpRequest req = HttpRequest.CreateOutgoing("/sts/v1.0/issuetoken", "POST"))
                {
                    req.RequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                    using (NetworkResponseInstrumented<HttpResponse> response = await _tokenRefreshClient.SendInstrumentedRequestAsync(req, cancelToken, realTime))
                    {
                        try
                        {
                            if (!response.Success)
                            {
                                return new TokenRefreshResult<string>(false, "Non-success response while refreshing SR token");
                            }
                            else if (response.Response.ResponseCode == 200)
                            {
                                string newToken = await response.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                                return new TokenRefreshResult<string>(newToken, TimeSpan.FromMinutes(10));
                            }
                            else if (response.Response.ResponseCode == 404 || response.Response.ResponseCode == 419)
                            {
                                // Assume that there is no internet connection.
                                return new TokenRefreshResult<string>(false, "Response " + response.Response.ResponseCode + " while refreshing SR token. Assuming there is no connectivity");
                            }
                            else
                            {
                                return new TokenRefreshResult<string>(true, "Non-success response while refreshing SR token: " + response.Response.ResponseCode + " " + response.Response.ResponseMessage);
                            }
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
            catch (Exception e)
            {
                return new TokenRefreshResult<string>(false, "Error while refreshing SR token: " + e.GetDetailedMessage());
            }
        }
    }
}
