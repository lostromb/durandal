//using Durandal.Common.Audio;
//using Durandal.Common.Logger;
//using Durandal.Common.Net;
//using Durandal.Common.Net.Http;
//using Durandal.Common.Utils;
//using Durandal.Common.Tasks;
//using Durandal.Common.Time;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Durandal.Common.Speech.SR.Cortana
//{
//    public class CortanaSpeechRecognizerFactory : ISpeechRecognizerFactory
//    {
//        private readonly string _apiKey;

//        private readonly ISocketFactory _socketFactory;
//        private readonly ILogger _logger;
//        private static readonly ISet<string> SUPPORTED_LOCALES = new HashSet<string>(new string[]
//            {
//                "ar-eg", "ca-es", "da-dk", "de-de", "en-au", "en-ca", "en-gb", "en-in", "en-nz",
//                "en-US", "es-es", "es-mx", "fi-fi", "fr-ca", "fr-fr", "hi-in", "it-it", "ja-jp",
//                "ko-kr", "nb-no", "nl-nl", "pl-pl", "pt-br", "pt-pt", "ru-ru", "sv-se", "zh-cn",
//                "zh-hk", "zh-tw"
//            });
        
//        private readonly IHttpClient _tokenRefreshClient;
//        private readonly TokenRefresher<string> _tokenRefresher;
//        private int _disposed = 0;

//        public CortanaSpeechRecognizerFactory(ISocketFactory socketFactory, ILogger logger, string apiKey, IRealTimeProvider realTime)
//        {
//            _socketFactory = socketFactory;
//            _logger = logger;
//            _apiKey = apiKey;
//            _tokenRefreshClient = new PortableHttpClient("api.cognitive.microsoft.com", 443, _logger.Clone("SRTokenRefreshClient"), true);

//            // Start a long-running task that will continually refresh the SR token proactively.
//            _tokenRefresher = new TokenRefresher<string>(_logger.Clone("SRTokenRefresh"), RefreshToken, realTime);
//        }

//        ~CortanaSpeechRecognizerFactory()
//        {
//            Dispose(false);
//        }

//        public bool IsLocaleSupported(string locale)
//        {
//            if (string.IsNullOrEmpty(locale))
//            {
//                return false;
//            }

//            return SUPPORTED_LOCALES.Contains(locale.ToLowerInvariant());
//        }

//        public void Dispose()
//        {
//            Dispose(true);
//            GC.SuppressFinalize(this);
//        }

//        protected virtual void Dispose(bool disposing)
//        {
//            if (!AtomicOperations.ExecuteOnce(ref _disposed))
//            {
//                return;
//            }

//            if (disposing)
//            {
//                _tokenRefresher?.Dispose();
//                _tokenRefreshClient?.Dispose();
//            }
//        }

//        public async Task<ISpeechRecognizer> CreateRecognitionStream(string locale, ILogger queryLogger, IRealTimeProvider realTime)
//        {
//            if (queryLogger == null)
//            {
//                queryLogger = _logger;
//            }

//            if (!IsLocaleSupported(locale))
//            {
//                queryLogger.Log("The locale \"" + locale + "\" is not supported by Cortana speech reco", LogLevel.Err);
//                return null;
//            }

//            string authToken = await _tokenRefresher.GetToken(queryLogger, realTime, TimeSpan.FromSeconds(1)).ConfigureAwait(false);

//            if (authToken == null)
//            {
//                queryLogger.Log("Auth token is null - SR cannot continue", LogLevel.Err);
//                return null;
//            }
            
//            try
//            {
//                TcpConnectionConfiguration connectionConfig = new TcpConnectionConfiguration()
//                {
//                    DnsHostname = "websockets.platform.bing.com",
//                    Port = 443,
//                    UseSSL = true,
//                    NoDelay = true
//                };

//                ISocket socket = await _socketFactory.Connect(connectionConfig, queryLogger, realTime).ConfigureAwait(false);
//                if (socket == null)
//                {
//                    queryLogger.Log("Error while connecting to SR service: Connection timed out", LogLevel.Err);
//                    return null;
//                }
//                else
//                {
//                    return await CortanaSpeechRecognizer.OpenConnection(socket, locale, authToken, queryLogger.Clone("CortanaSpeechReco"), realTime).ConfigureAwait(false);
//                }
//            }
//            catch (Exception e)
//            {
//                queryLogger.Log("Error while connecting to SR service", LogLevel.Err);
//				queryLogger.Log(e, LogLevel.Err);
//            }

//            return null;
//        }

//        private async Task<TokenRefreshResult<string>> RefreshToken(CancellationToken cancelToken, IRealTimeProvider realTime)
//        {
//            try
//            {
//                HttpRequest req = new HttpRequest()
//                {
//                    RequestFile = "/sts/v1.0/issueToken",
//                    RequestMethod = "POST"
//                };

//                req.RequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
//                NetworkResponseInstrumented<HttpResponse> response = await _tokenRefreshClient.SendInstrumentedRequestAsync(req, cancelToken, realTime).ConfigureAwait(false);

//                if (!response.Success)
//                {
//                    return new TokenRefreshResult<string>(false, "Non-success response while refreshing SR token");
//                }
//                else if (response.Response.ResponseCode == 200)
//                {
//                    string newToken = response.Response.GetPayloadAsString();
//                    return new TokenRefreshResult<string>(newToken, TimeSpan.FromMinutes(10));
//                }
//                else if (response.Response.ResponseCode == 404 || response.Response.ResponseCode == 419)
//                {
//                    // Assume that there is no internet connection.
//                    return new TokenRefreshResult<string>(false, "Response " + response.Response.ResponseCode + " while refreshing SR token. Assuming there is no connectivity");
//                }
//                else
//                {
//                    return new TokenRefreshResult<string>(true, "Non-success response while refreshing SR token: " + response.Response.ResponseCode + " " + response.Response.ResponseMessage);
//                }
//            }
//            catch (Exception e)
//            {
//                return new TokenRefreshResult<string>(false, "Error while refreshing SR token: " + e.GetDetailedMessage());
//            }
//        }
//    }
//}
