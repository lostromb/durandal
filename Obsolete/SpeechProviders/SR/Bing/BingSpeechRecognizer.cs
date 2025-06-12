

namespace Durandal.Common.Speech.SR.Bing
{
    using Durandal.Common.Audio;
    using Durandal.Common.AudioV2;
    using Durandal.Common.Logger;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Durandal.Common.IO;
    using Durandal.API;
    using Durandal.Common.Net.Http;

    public class BingSpeechRecognizer : ISpeechRecognizer
    {
        private static BingSRAdmAuthentication StaticTokenProvider;
        private const string ENDPOINT = @"https://speech.platform.bing.com/recognize";

        private ILogger _logger;

        // Per-request parameters
        HttpWebRequest _currentWebRequest = null;
        Stream _currentRequestStream = null;
        private int _disposed = 0;

        static BingSpeechRecognizer()
        {
            StaticTokenProvider = new BingSRAdmAuthentication("Durandal", "kjSIKTm0CI2rmYPvJG6CcdGsZPPrjBKpck4zI30m0Zo=");
        }

        public BingSpeechRecognizer(ILogger logger, bool enableIntermediateReco)
        {
            _logger = logger;
        }

        ~BingSpeechRecognizer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Begins recognizing speech in the given language (locale)
        /// </summary>
        /// <param name="locale">The locale to interpret the speech in, i.e. "en-us"</param>
        public void StartUnderstandSpeech(string locale)
        {
            try
            {
                string requestUri = ENDPOINT.Trim(new char[] { '/', '?' });

                /* URI Params. Refer to the README file for more information. */
                requestUri += @"?scenarios=smd";                                  // websearch is the other main option.
                requestUri += @"&appid=D4D52672-91D7-4C74-8AD8-42B1D98141A5";     // You must use this ID.
                requestUri += @"&locale=en-US";                                   // TODO: Limit this to actually supported languages?
                requestUri += @"&device.os=wp7";
                requestUri += @"&version=3.0";
                requestUri += @"&format=json";
                requestUri += @"&instanceid=565D69FF-E928-4B7E-87DA-9A755B96D9E3";
                requestUri += @"&requestid=" + Guid.NewGuid().ToString();
                //requestUri += @"&maxnbest=5"; // this doesn't work?
                //requestUri += @"&result.profanity=0";

                string anyError = StaticTokenProvider.GetError();
                if (!string.IsNullOrEmpty(anyError))
                {
                    _logger.Log("ADM token error reported (Bing SR component)", LogLevel.Err);
                    _logger.Log(anyError, LogLevel.Err);
                    return;
                }

                AdmAccessToken admToken = StaticTokenProvider.GetAccessToken();

                if (admToken == null)
                {
                    _logger.Log("ADM token manager has no token; can't run speech reco");
                    return;
                }

                _currentWebRequest = (HttpWebRequest)HttpWebRequest.Create(requestUri);
                _currentWebRequest.SendChunked = true;
                _currentWebRequest.Accept = @"application/json;text/xml";
                _currentWebRequest.Method = "POST";
                _currentWebRequest.ProtocolVersion = HttpVersion.Version11;
                _currentWebRequest.Host = @"speech.platform.bing.com";
                _currentWebRequest.ContentType = @"audio/wav; codec=""audio/pcm""; samplerate=" + AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE;
                // currentWebRequest.KeepAlive = false;
                // currentWebRequest.Pipelined = true;
                // currentWebRequest.PreAuthenticate = true;
                // currentWebRequest.AllowReadStreamBuffering = false;
                // currentWebRequest.AllowWriteStreamBuffering = false;
                _currentWebRequest.Headers["Authorization"] = "Bearer " + admToken.access_token;
                
                // Start by sending an empty riff header down the stream
                _currentRequestStream = _currentWebRequest.GetRequestStream();
                byte[] header = SimpleWaveReader.BuildRiffHeader(0, AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE);
                _currentRequestStream.Write(header, 0, header.Length);
            }
            catch (Exception e)
            {
                _logger.Log("Caught exception in SR while starting speech reco: " + e.Message, LogLevel.Err);
            }
        }

        /// <summary>
        /// For asynchronous requests, this method can be used to append a chunk of wave data to the current
        /// stream being interpreted. The speech engine should give its best effort to return an intermediate
        /// recognition result in the form of a string. However, if no intermediate result is available, it
        /// is possible for this method to return null.
        /// </summary>
        /// <param name="continualData">The audio chunk to send to the stream</param>
        /// <returns></returns>
        public async Task<string> ContinueUnderstandSpeech(AudioChunk continualData)
        {
            if (_currentRequestStream == null)
            {
                _logger.Log("No request stream!", LogLevel.Wrn);
                return string.Empty;
            }

            try
            {
                if (continualData != null)
                {
                    byte[] buffer = null;
                    int bytesRead = 0;
                    byte[] inputData = continualData.GetDataAsBytes();
                    using (RecyclableMemoryStream readStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default, "", inputData.Length))
                    {
                        readStream.Write(inputData, 0, inputData.Length);
                        readStream.Seek(0, SeekOrigin.Begin);
                        buffer = new byte[checked((uint)Math.Min(1024, (int)readStream.Length))];
                        while ((bytesRead = await readStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                        {
                            await _currentRequestStream.WriteAsync(buffer, 0, bytesRead);
                        }

                        // Flush (is this necessary?)
                        //currentRequestStream.Flush();
                    }
                }
                
                return string.Empty;
            }
            catch (Exception e)
            {
                _logger.Log("Caught exception in SR while running intermediate speech reco: " + e.Message, LogLevel.Err);
                return string.Empty;
            }
        }

        /// <summary>
        /// Finalizes an asynchronous speech reco request and returns the final hypotheses.
        /// This method will block until recognition fully completes.
        /// </summary>
        /// <param name="continualData">The final audio data to write, if any</param>
        /// <returns>The set of all final speech hypotheses</returns>
        public async Task<SpeechRecognitionResult> FinishUnderstandSpeech(AudioChunk continualData = null)
        {
            if (_currentWebRequest == null || _currentRequestStream == null)
            {
                _logger.Log("Bing SR failed to make a request; failing out", LogLevel.Err);
                return new SpeechRecognitionResult()
                {
                    RecognitionStatus = SpeechRecognitionStatus.Error
                };
            }

            try
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();

                await ContinueUnderstandSpeech(continualData);
                SpeechRecognitionResult fallbackResults = new SpeechRecognitionResult();

                // currentRequestStream.Flush();
                // currentRequestStream.Close();
                
                _currentWebRequest.Timeout = 1000;

                string responseString;
                using (WebResponse response = await _currentWebRequest.GetResponseAsync())
                {
                    _logger.Log(((HttpWebResponse)response).StatusCode);

                    using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                    {
                        responseString = sr.ReadToEnd();
                        sr.Close();
                    }

                    _logger.Log(responseString, LogLevel.Vrb);
                }

                timer.Stop();
                _logger.Log("Speech reco call took " + timer.ElapsedMilliseconds + "ms", LogLevel.Std);

                // Clean up
                _currentWebRequest = null;
                _currentRequestStream = null;

                SpeechRecognitionResult returnVal = ParseResults(responseString);

                // If the service returned nothing or error, fall back on the SAPI results as a last resort
                if (returnVal == null || returnVal.RecognitionStatus != SpeechRecognitionStatus.Success || returnVal.RecognizedPhrases == null || returnVal.RecognizedPhrases.Count == 0)
                {
                    _logger.Log("Bing SR service provided no results! Falling back to on-device reco", LogLevel.Wrn);
                    returnVal = fallbackResults;
                }

                return returnVal;
            }
            catch (WebException e)
            {
                _logger.Log("Caught web exception in SR while finishing speech reco: " + e.Message, LogLevel.Err);
                if (e.InnerException != null)
                {
                    _logger.Log(e.InnerException.Message, LogLevel.Err);
                }

                return new SpeechRecognitionResult()
                {
                    RecognitionStatus = SpeechRecognitionStatus.Error
                };
            }
            catch (Exception e)
            {
                _logger.Log("Caught exception in SR while finishing speech reco: " + e.Message, LogLevel.Err);

                return new SpeechRecognitionResult()
                {
                    RecognitionStatus = SpeechRecognitionStatus.Error
                };
            }
        }

        private SpeechRecognitionResult ParseResults(string resultJson)
        {
            JsonSerializer ser = JsonSerializer.Create(new JsonSerializerSettings());
            var reader = new JsonTextReader(new StringReader(resultJson));
            JObject response = ser.Deserialize(reader) as JObject;

            SpeechRecognitionResult returnVal = new SpeechRecognitionResult();
            if (response["header"] == null ||
                response["header"]["status"] == null ||
                response["results"] == null)
            {
                // Bad results
                _logger.Log("Invalid results from speech reco service", LogLevel.Err);
                _logger.Log(resultJson, LogLevel.Vrb);
                returnVal.RecognitionStatus = SpeechRecognitionStatus.Error;
                return returnVal;
            }

            if (!response["header"]["status"].Value<string>().Equals("success", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Log("Speech reco service reported an error", LogLevel.Err);
                returnVal.RecognitionStatus = SpeechRecognitionStatus.Error;
                return returnVal;
            }

            returnVal.RecognitionStatus = SpeechRecognitionStatus.NoMatch;
            foreach (JToken result in response["results"].Children())
            {
                SpeechRecognizedPhrase recognizedPhrase = new SpeechRecognizedPhrase();
                recognizedPhrase.DisplayText = result["name"].Value<string>();
                recognizedPhrase.IPASyllables = result["lexical"].Value<string>();
                recognizedPhrase .SREngineConfidence = result["confidence"].Value<float>();
                returnVal.RecognizedPhrases.Add(recognizedPhrase);
                returnVal.RecognitionStatus = SpeechRecognitionStatus.Success;
            }

            return returnVal;
        }

        public void Close() { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            if (!disposing) Durandal.Common.Utils.DebugMemoryLeaktracer.TraceDisposableItemFinalized(this.GetType());

            if (disposing)
            {
                _currentRequestStream.Dispose();
            }
        }

        #region Low-level token authentication junk

        public class BingSRAdmAuthentication
        {
            public static readonly string DatamarketAccessUri = "https://datamarket.accesscontrol.windows.net/v2/OAuth2-13";
            private string clientId;
            private string clientSecret;
            private string request;
            private AdmAccessToken token;
            private Timer accessTokenRenewer;
            private string _lastErrorMessage = null;

            //Access token expires every 10 minutes. Renew it every 9 minutes only.
            private const int RefreshTokenDuration = 9;

            public BingSRAdmAuthentication(string clientId, string clientSecret)
            {
                this.clientId = clientId;
                this.clientSecret = clientSecret;

                /*
                 * If clientid or client secret has special characters, encode before sending request
                 */
                this.request = string.Format("grant_type=client_credentials&client_id={0}&client_secret={1}&scope={2}",
                                              WebUtility.UrlEncode(clientId),
                                              WebUtility.UrlEncode(clientSecret),
                                              WebUtility.UrlEncode("https://speech.platform.bing.com"));

                this.token = HttpPost(DatamarketAccessUri, this.request);

                // renew the token every specified minutes
                accessTokenRenewer = new Timer(new TimerCallback(OnTokenExpiredCallback),
                                               this,
                                               TimeSpan.FromMinutes(RefreshTokenDuration),
                                               TimeSpan.FromMilliseconds(-1));
            }

            public AdmAccessToken GetAccessToken()
            {
                lock (this)
                {
                    return this.token;
                }
            }

            public string GetError()
            {
                return _lastErrorMessage;
            }
            
            private void RenewAccessToken()
            {
                AdmAccessToken newAccessToken = HttpPost(DatamarketAccessUri, this.request);
                lock (this)
                {
                    //swap the new token with old one
                    this.token = newAccessToken;
                }
            }

            /// <summary>
            /// This method is called by a timer every few minutes when the token is about to expire
            /// </summary>
            private void OnTokenExpiredCallback(object stateInfo)
            {
                try
                {
                    RenewAccessToken();
                }
                catch (Exception ex)
                {
                    _lastErrorMessage = (string.Format("Failed renewing access token. Details: {0}", ex.Message));
                }
                finally
                {
                    try
                    {
                        accessTokenRenewer.Change(TimeSpan.FromMinutes(RefreshTokenDuration), TimeSpan.FromMilliseconds(-1));
                    }
                    catch (Exception ex)
                    {
                        _lastErrorMessage = (string.Format("Failed to reschedule the timer to renew access token. Details: {0}", ex.Message));
                    }
                }
            }

            private AdmAccessToken HttpPost(string DatamarketAccessUri, string requestDetails)
            {
                try
                {
                    //Prepare OAuth request 
                    WebRequest webRequest = WebRequest.Create(DatamarketAccessUri);
                    webRequest.ContentType = HttpConstants.MIME_TYPE_FORMDATA;
                    webRequest.Method = "POST";
                    byte[] bytes = Encoding.ASCII.GetBytes(requestDetails);
                    webRequest.ContentLength = bytes.Length;
                    using (Stream outputStream = webRequest.GetRequestStream())
                    {
                        outputStream.Write(bytes, 0, bytes.Length);
                    }
                    using (WebResponse webResponse = webRequest.GetResponse())
                    {
                        // Fixme: could this just be easily ported to newtonsoft?
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AdmAccessToken));
                        //Get deserialized object from JSON stream
                        AdmAccessToken token = (AdmAccessToken)serializer.ReadObject(webResponse.GetResponseStream());
                        return token;
                    }
                }
                catch (Exception e)
                {
                    _lastErrorMessage = e.Message;
                }

                return null;
            }
        }

#endregion
    }
}
