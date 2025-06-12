using Durandal.Common.Audio;
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
using Durandal.Common.Collections;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Net.WebSocket;

namespace Durandal.Common.Speech.SR.Azure
{
    /// <summary>
    /// Speech recognition client based on reverse engineering of the Websocket-backed Microsoft Cognitive Speech SDK.
    /// </summary>
    public class AzureSpeechRecognizerFactory : ISpeechRecognizerFactory
    {
        // https://docs.microsoft.com/en-US/azure/cognitive-services/speech-service/language-support#speech-to-text
        private static readonly Durandal.Common.Collections.IReadOnlySet<string> SUPPORTED_LOCALES = new ReadOnlySetWrapper<string>(new HashSet<string>(new string[]
            {
                "ar-eg", "ca-es", "da-dk", "de-de", "en-au", "en-ca", "en-gb", "en-in", "en-nz",
                "en-US", "es-es", "es-mx", "fi-fi", "fr-ca", "fr-fr", "hi-in", "it-it", "ja-jp",
                "ko-kr", "nb-no", "nl-nl", "pl-pl", "pt-br", "pt-pt", "ru-ru", "sv-se", "zh-cn",
                "zh-hk", "zh-tw"
            }, StringComparer.OrdinalIgnoreCase));

        private readonly string _apiKey;
        private readonly IWebSocketClientFactory _webSocketFactory;
        private readonly ILogger _logger;
        private readonly string _region;
        private readonly string _speechRecoHostName;

        private readonly IHttpClient _tokenRefreshClient;
        private readonly TokenRefresher<string> _tokenRefresher;
        private int _disposed = 0;

        /// <summary>
        /// Creates a speech recognizer factory backed by Azure Speech Service
        /// </summary>
        /// <param name="tokenRefreshClientFactory">An HTTP client factory used for the token refresher</param>
        /// <param name="webSocketFactory">A socket factory used for SR websocket connections</param>
        /// <param name="logger">A logger</param>
        /// <param name="apiKey">The API key IN THE FORMAT "region=westus2;key=xxxxx". If only an API key is given then the westus2 region will be assumed.</param>
        /// <param name="realTime"></param>
        public AzureSpeechRecognizerFactory(
            IHttpClientFactory tokenRefreshClientFactory,
            IWebSocketClientFactory webSocketFactory,
            ILogger logger,
            string apiKey,
            IRealTimeProvider realTime)
        {
            _webSocketFactory = webSocketFactory.AssertNonNull(nameof(webSocketFactory));
            _logger = logger.AssertNonNull(nameof(logger));
            ParseApiKeyAndRegion(apiKey, out _apiKey, out _region);
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new ArgumentNullException("API key is null");
            }
            if (string.IsNullOrEmpty(_region))
            {
                _region = "westus2";
            }

            _speechRecoHostName = _region + ".stt.speech.microsoft.com";
            _tokenRefreshClient = tokenRefreshClientFactory.CreateHttpClient(_region + ".api.cognitive.microsoft.com", 443, true, _logger.Clone("SRTokenRefreshClient"));

            // Start a long-running task that will continually refresh the SR token proactively.
            _tokenRefresher = new TokenRefresher<string>(_logger.Clone("SRTokenRefresh"), RefreshToken, realTime);

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AzureSpeechRecognizerFactory()
        {
            Dispose(false);
        }
#endif

        public bool IsLocaleSupported(LanguageCode locale)
        {
            if (locale == null)
            {
                return false;
            }

            return SUPPORTED_LOCALES.Contains(locale.ToBcp47Alpha2String());
        }

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
                _tokenRefreshClient?.Dispose();
            }
        }

        /// <inheritdoc />
        public async Task<ISpeechRecognizer> CreateRecognitionStream(
            WeakPointer<IAudioGraph> audioGraph,
            string graphNodeName,
            LanguageCode locale,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
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

            string authToken = await _tokenRefresher.GetToken(queryLogger, realTime, TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            if (authToken == null)
            {
                queryLogger.Log("Auth token is null - SR cannot continue", LogLevel.Err);
                return null;
            }

            try
            {
                TcpConnectionConfiguration tcpConfig = new TcpConnectionConfiguration()
                {
                    DnsHostname = _speechRecoHostName,
                    Port = 443,
                    UseTLS = true,
                    NoDelay = true
                };

                return await AzureSpeechRecognizer.OpenConnection(
                    audioGraph,
                    _webSocketFactory,
                    tcpConfig,
                    locale,
                    authToken,
                    queryLogger.Clone("AzureSpeechReco"),
                    _speechRecoHostName,
                    cancelToken,
                    realTime).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                queryLogger.Log("Error while connecting to SR service", LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
            }

            return null;
        }

        /// <summary>
        /// Parses a key string such as "region=westus;key=xxxxxxx" into a tuple of API key and region name
        /// </summary>
        /// <param name="keyString">The secret key string</param>
        /// <param name="apiKey">Parsed API key</param>
        /// <param name="region">Parsed region name</param>
        /// <returns></returns>
        public static void ParseApiKeyAndRegion(string keyString, out string apiKey, out string region)
        {
            apiKey = string.Empty;
            region = string.Empty;

            if (!keyString.Contains(";") && !keyString.Contains(","))
            {
                // Interpret the entire key as the API key only, no region
                apiKey = keyString;
            }
            else
            {
                string[] kvps = keyString.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string kvp in kvps)
                {
                    int eqIdx = kvp.IndexOf('=');
                    if (eqIdx > 0)
                    {
                        string key = kvp.Substring(0, eqIdx).Trim();
                        string value = kvp.Substring(eqIdx + 1).Trim();
                        if (string.Equals(key, "region", StringComparison.OrdinalIgnoreCase))
                        {
                            region = value;
                        }
                        else if (string.Equals(key, "key", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(key, "apikey", StringComparison.OrdinalIgnoreCase))
                        {
                            apiKey = value;
                        }
                    }
                }
            }
        }

        private async Task<TokenRefreshResult<string>> RefreshToken(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            try
            {
                using (HttpRequest req = HttpRequest.CreateOutgoing("/sts/v1.0/issuetoken", "POST"))
                {
                    req.RequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                    using (NetworkResponseInstrumented<HttpResponse> response = await _tokenRefreshClient.SendInstrumentedRequestAsync(req, cancelToken, realTime).ConfigureAwait(false))
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
                                return new TokenRefreshResult<string>(true, "Non-success response while refreshing SR token: " + response.Response.ResponseCode + " " + (await response.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false)));
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
