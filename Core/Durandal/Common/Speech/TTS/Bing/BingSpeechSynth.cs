namespace Durandal.Common.Speech.TTS.Bing
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Audio.Components;
    using Durandal.Common.Logger;
    using Durandal.Common.Utils;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Durandal.Common.Time;
    using Durandal.Common.IO;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// Text-to-speech synthesizer backed by Azure speech services (so I guess not really Bing anymore, but whatever)
    /// Todo: replace this implementation with the new HTTP and audio stack so it has proper streaming, etc.
    /// </summary>
    public class BingSpeechSynth : ISpeechSynth
    {
        private const int NET_TIMEOUT_MS = 10000;

        private readonly string _apiKey;
        private readonly string _serviceRegion;
        private readonly IHttpClient _tokenClient;
        private readonly IHttpClient _serviceClient;
        private readonly ILogger _logger;
        private readonly TokenRefresher<string> _tokenRefresher;
        private readonly VoiceGender _defaultGender;
        private readonly INLPToolsCollection _nlTools;
        private int _disposed = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="BingSpeechSynth"/> class.
        /// </summary>
        /// <param name="logger">The default logger</param>
        /// <param name="apiKey">The API key IN THE FORMAT "region=westus2;key=xxxxx". If only an API key is given then the westus2 region will be assumed</param>
        /// <param name="httpClientFactory">HTTP client factory to use</param>
        /// <param name="tokenRefreshRealTime">The real time to use for the token refresher</param>
        /// <param name="nlTools">NLP tools used for wordbreaking</param>
        /// <param name="defaultGender">The voice gender to use</param>
        public BingSpeechSynth(
            ILogger logger, 
            string apiKey,
            IHttpClientFactory httpClientFactory,
            IRealTimeProvider tokenRefreshRealTime,
            INLPToolsCollection nlTools,
            VoiceGender defaultGender = VoiceGender.Female)
        {
            _defaultGender = defaultGender;
            _logger = logger;
            
            ParseApiKeyAndRegion(apiKey, out _apiKey, out _serviceRegion);
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new ArgumentNullException("API key is null");
            }
            if (string.IsNullOrEmpty(_serviceRegion))
            {
                _serviceRegion = "westus2";
            }

            _nlTools = nlTools.AssertNonNull(nameof(nlTools));
            _tokenClient = httpClientFactory.CreateHttpClient(BingTtsServiceConfig.GetTokenUri(_serviceRegion), logger.Clone("BingTTSTokenRefresh"));
            _tokenRefresher = new TokenRefresher<string>(logger, RefreshAuthToken, tokenRefreshRealTime);
            _serviceClient = httpClientFactory.CreateHttpClient(BingTtsServiceConfig.GetServiceUri(_serviceRegion), logger.Clone("BingTTS"));
            _serviceClient.SetReadTimeout(TimeSpan.FromMilliseconds(NET_TIMEOUT_MS));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~BingSpeechSynth()
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
                _tokenRefresher.Dispose();
                _serviceClient.Dispose();
                _tokenClient.Dispose();
            }
        }

        public bool IsLocaleSupported(LanguageCode locale)
        {
            return BingTtsServiceConfig.IsLocaleSupported(locale);
        }

        /// <inheritdoc />
        public async Task<SynthesizedSpeech> SynthesizeSpeechAsync(
            SpeechSynthesisRequest request,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            ILogger traceLogger = null)
        {
            traceLogger = traceLogger ?? _logger;
            ValidateRequest(request, traceLogger);
            Voice selectedVoice = BingTtsServiceConfig.SelectVoice(request.Locale, request.VoiceGender == VoiceGender.Unspecified ? _defaultGender : request.VoiceGender);

            if (selectedVoice == null)
            {
                traceLogger.Log("No suitable TTS voice was found to satisfy the request!", LogLevel.Err);
                return null;
            }

            //traceLogger.Log("Parsing SSML", LogLevel.Vrb);
            string ssml = GenerateFinalSsml(request, selectedVoice);

            using (IAudioGraph disposableGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (IAudioSampleSource sampleSource = await CreateSynthesizedAudioSource(ssml, selectedVoice, new WeakPointer<IAudioGraph>(disposableGraph), traceLogger, cancelToken, realTime).ConfigureAwait(false))
            {
                if (sampleSource == null)
                {
                    return null;
                }

                WeakPointer<IAudioGraph> localGraph = new WeakPointer<IAudioGraph>(disposableGraph);
                using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(localGraph, sampleSource.OutputFormat, "BingTTSBucket"))
                using (AudioSplitter splitter = new AudioSplitter(localGraph, sampleSource.OutputFormat, "BingTTSSplitter"))
                using (RawPcmEncoder pcmEncoder = new RawPcmEncoder(localGraph, sampleSource.OutputFormat, "BingTTSPcmPassthrough"))
                using (RecyclableMemoryStream pcmOutput = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                {
                    bool calculateSpeechTimings = false;
                    NLPTools localeTools;
                    if (_nlTools.TryGetNLPTools(request.Locale, out localeTools) &&
                        localeTools.SpeechTimingEstimator != null &&
                        localeTools.WordBreaker != null)
                    {
                        calculateSpeechTimings = true;
                    }

                    if (calculateSpeechTimings)
                    {
                        sampleSource.ConnectOutput(splitter);
                        splitter.AddOutput(pcmEncoder);
                        splitter.AddOutput(bucket);
                    }
                    else
                    {
                        sampleSource.ConnectOutput(pcmEncoder);
                    }

                    await pcmEncoder.Initialize(pcmOutput, false, cancelToken, realTime).ConfigureAwait(false);
                    await pcmEncoder.ReadFully(cancelToken, realTime, TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);

                    SynthesizedSpeech returnVal = new SynthesizedSpeech()
                    {
                        Audio = new AudioData()
                        {
                            Data = new ArraySegment<byte>(pcmOutput.ToArray()),
                            Codec = pcmEncoder.Codec,
                            CodecParams = pcmEncoder.CodecParams,
                        },
                        Locale = request.Locale.ToBcp47Alpha2String(),
                        Ssml = request.Ssml,
                        PlainText = request.Plaintext,
                    };

                    if (calculateSpeechTimings)
                    {
                        // opt: this implementation copies the entire audio data payload twice, once in the pcmOutput.ToArray(),
                        // and once for the GetAllAudio() here. Would be nice to not have that...
                        returnVal.Words = SpeechUtils.EstimateSynthesizedWordTimings(
                            bucket.GetAllAudio(),
                            ssml,
                            localeTools.WordBreaker,
                            localeTools.SpeechTimingEstimator,
                            _logger);
                    }

                    return returnVal;
                }
            }
        }

        /// <inheritdoc />
        public async Task<IAudioSampleSource> SynthesizeSpeechToStreamAsync(
            SpeechSynthesisRequest request,
            WeakPointer<IAudioGraph> parentGraph,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            ILogger logger = null)
        {
            logger = logger ?? _logger;
            ValidateRequest(request, logger);
            Voice selectedVoice = BingTtsServiceConfig.SelectVoice(request.Locale, request.VoiceGender == VoiceGender.Unspecified ? _defaultGender : request.VoiceGender);

            if (selectedVoice == null)
            {
                throw new NotSupportedException("No suitable TTS voice was found to satisfy the request!");
            }

            logger.Log("Parsing SSML");
            string ssml = GenerateFinalSsml(request, selectedVoice);

            // Kick off a background task that will do the synthesis
            return await CreateSynthesizedAudioSource(ssml, selectedVoice, parentGraph, logger, cancelToken, realTime).ConfigureAwait(false);
        }

        private static void ValidateRequest(SpeechSynthesisRequest request, ILogger logger)
        {
            request = request.AssertNonNull(nameof(request));

            if (string.IsNullOrEmpty(request.Ssml) && string.IsNullOrEmpty(request.Plaintext))
            {
                throw new ArgumentException("Speak SSML and plaintext is null or empty!");
            }

            if (request.Locale == null)
            {
                throw new ArgumentNullException("Speak locale is null!");
            }

            if (string.IsNullOrEmpty(request.Ssml))
            {
                request.Ssml = SpeechUtils.NormalizeSsml(request.Plaintext, logger);
            }
            else if (string.IsNullOrEmpty(request.Plaintext))
            {
                request.Plaintext = SpeechUtils.StripSsml(request.Ssml);
            }

            if (!SpeechUtils.IsSsml(request.Ssml))
            {
                logger.Log("The given synth text \"" + request.Ssml + "\" is not SSML-formatted; wrapping with standard SSML tags...", LogLevel.Vrb);
                request.Ssml = SpeechUtils.NormalizeSsml(request.Plaintext, logger);
            }
        }

        /// <summary>
        /// Parses a key string such as "region=westus;key=xxxxxxx" into a tuple of API key and region name
        /// </summary>
        /// <param name="keyString">Input secret key</param>
        /// <param name="apiKey">Parsed api key</param>
        /// <param name="region">Parsed region name</param>
        /// <returns></returns>
        private static void ParseApiKeyAndRegion(string keyString, out string apiKey, out string region)
        {
            apiKey = string.Empty;
            region = string.Empty;
            if (string.IsNullOrEmpty(keyString))
            {
                return;
            }

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

        private async Task<TokenRefreshResult<string>> RefreshAuthToken(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            try
            {
                using (HttpRequest request = HttpRequest.CreateOutgoing(BingTtsServiceConfig.TokenPath, "POST"))
                {
                    request.RequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                    using (HttpResponse tokenResponse = await _tokenClient.SendRequestAsync(request, cancelToken, realTime).ConfigureAwait(false))
                    {
                        try
                        {
                            if (tokenResponse != null)
                            {
                                if (tokenResponse.ResponseCode == 200)
                                {
                                    string token = await tokenResponse.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                                    return new TokenRefreshResult<string>(token, TimeSpan.FromMinutes(10));
                                }
                                else if (tokenResponse.ResponseCode == 404 || tokenResponse.ResponseCode == 419)
                                {
                                    return new TokenRefreshResult<string>(false, "Response code " + tokenResponse.ResponseCode + " from token service. " + (await tokenResponse.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false)));
                                }
                                else
                                {
                                    return new TokenRefreshResult<string>(true, "Response code " + tokenResponse.ResponseCode + " from token service. " + (await tokenResponse.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false)));
                                }
                            }
                            else
                            {
                                return new TokenRefreshResult<string>(false, "Null token response from token service");
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
                return new TokenRefreshResult<string>(false, "Exception while refreshing Bing TTS token: " + e.GetDetailedMessage());
            }
        }

        private async Task<HttpRequest> GenerateHttpRequest(string ssml, Voice voice, ILogger traceLogger, IRealTimeProvider realTime)
        {
            string authToken = await _tokenRefresher.GetToken(traceLogger, realTime, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            if (string.IsNullOrEmpty(authToken))
            {
                throw new NullReferenceException("No API token is available for Bing speech synth");
            }

            traceLogger.Log("Fetched Bing TTS auth token");
            var request = HttpRequest.CreateOutgoing(BingTtsServiceConfig.GetServiceUri(_serviceRegion).PathAndQuery, "POST");
            request.SetContent(ssml, "application/ssml+xml");
            request.RequestHeaders.Add("Authorization", string.Format("Bearer {0}", authToken));
            request.RequestHeaders.Add("User-Agent", string.Format("Durandal {0}", SVNVersionInfo.AssemblyVersion));
            request.RequestHeaders.Add("X-Microsoft-OutputFormat", "raw-16khz-16bit-mono-pcm");
            return request;
        }

        private async Task<IAudioSampleSource> CreateSynthesizedAudioSource(
            string bingSsml,
            Voice voice,
            WeakPointer<IAudioGraph> parentGraph,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            traceLogger.Log("Generating request to Bing TTS");
            HttpResponse response = null;
            AudioDecoder decoder = null;
            using (HttpRequest request = await GenerateHttpRequest(bingSsml, voice, traceLogger, realTime).ConfigureAwait(false))
            {
                try
                {
                    traceLogger.Log("Sending request to Bing TTS");
                    response = await _serviceClient.SendRequestAsync(request).ConfigureAwait(false);

                    if (response == null)
                    {
                        traceLogger.Log("Bing TTS service returned null response", LogLevel.Err);
                    }
                    else if (response.ResponseCode == 200)
                    {
                        traceLogger.Log("Finished TTS request. Downloading stream...");
                        NonRealTimeStream audioStream = response.ReadContentAsStream();
                        traceLogger.Log("Creating PCM decoder");
                        decoder = new RawPcmDecoder(parentGraph, AudioSampleFormat.Mono(16000), "BingSpeechSynthPcmDecoder");
                        AudioInitializationResult initializeResult = await decoder.Initialize(
                            audioStream,
                            ownsStream: true,
                            cancelToken: cancelToken,
                            realTime: realTime).ConfigureAwait(false);

                        if (initializeResult < AudioInitializationResult.Success)
                        {
                            throw new Exception("Couldn't initialize PCM decoder for reading bing TTS output. This should never happen");
                        }

                        decoder.TakeOwnershipOfDisposable(response);
                        response = null;
                        AudioDecoder returnVal = decoder;
                        decoder = null;
                        return returnVal;
                    }
                    else
                    {
                        traceLogger.Log("Bing TTS service returned HTTP " + response.ResponseCode, LogLevel.Err);
                    }
                }
                catch (TaskCanceledException)
                {
                    traceLogger.Log("The remote TTS service call timed out.", LogLevel.Err);
                }
                catch (Exception e)
                {
                    traceLogger.Log("Unhandled exception occurred while running Bing TTS", LogLevel.Err);
                    traceLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    if (response != null)
                    {
                        await response.FinishAsync(cancelToken, realTime);
                        response.Dispose();
                    }

                    request?.Dispose();
                    decoder?.Dispose();
                    traceLogger.Log("Finished creating streaming TTS graph component");
                }
            }

            // FIXME need a better response here
            return null;
        }
        
        private const string SpeakMarkup = "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xmlns:mstts=\"http://www.w3.org/2001/mstts\" xml:lang=\"{0}\"><voice lang=\"{0}\" gender=\"{1}\" name=\"{2}\">{3}</voice></speak>";
        private static readonly Regex SpeakTagRegex = new Regex("</?speak.*?>");

        private static string GenerateFinalSsml(SpeechSynthesisRequest request, Voice selectedVoice)
        {
            // Remove the <speak> tag from the input SSML
            // FIXME we should really just decorate the properties of the existing tags instead of stripping out and replacing them
            string formattedSsml = StringUtils.RegexReplace(SpeakTagRegex, request.Ssml, string.Empty);

            // Then add new surrogate speak and voice tags that have the attributes we want
            formattedSsml = string.Format(SpeakMarkup, request.Locale, selectedVoice.Gender == VoiceGender.Male ? "male" : "female", selectedVoice.Name, formattedSsml);

            return formattedSsml;
        }
    }
}
