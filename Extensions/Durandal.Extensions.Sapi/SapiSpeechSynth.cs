namespace Durandal.Extensions.Sapi
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP;
    using Durandal.Common.Utils;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Speech.AudioFormat;
    using System.Speech.Synthesis;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Durandal.Common.Audio.Components;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.IO;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Speech;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// Speech synthesis engine backed by the local machine's SAPI, and whatever voices are currently installed
    /// </summary>
    public class SapiSpeechSynth : ISpeechSynth
    {
        private const string SpeakMarkup = "<speak version=\"1.0\" xml:lang=\"{0}\">{1}</speak>";
        private static readonly Regex SpeakTagRegex = new Regex("</?speak.*?>");

        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly WeakPointer<IThreadPool> _synthThreadPool;
        private readonly AudioSampleFormat _audioOutputFormat;
        private readonly ResourcePool<SpeechSynthesizer> _pooledSynths;
        private readonly ILogger _logger;
        private readonly IDictionary<LanguageCode, CultureInfo> _supportedCultures;
        private int _disposed = 0;

        public SapiSpeechSynth(
            ILogger logger,
            WeakPointer<IThreadPool> threadPool,
            AudioSampleFormat outputFormat,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            int speechPoolSize = 1)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _synthThreadPool = threadPool.AssertNonNull(nameof(threadPool));
            _audioOutputFormat = outputFormat.AssertNonNull(nameof(outputFormat));
            _metrics = metrics.DefaultIfNull(NullMetricCollector.Singleton);
            _metricDimensions = metricDimensions ?? DimensionSet.Empty;
            _supportedCultures = BuildListOfSupportedLocales();

            if (speechPoolSize < 1)
            {
                throw new ArgumentOutOfRangeException("Speech pool size must be 1 or higher");
            }
            if (speechPoolSize > 100)
            {
                speechPoolSize = 100;
            }

            List<SpeechSynthesizer> poolResources = new List<SpeechSynthesizer>();
            for (int c = 0; c < speechPoolSize; c++)
            {
                SpeechSynthesizer synth = new SpeechSynthesizer();
                // FIXME implement voice selection by gender
                synth.SelectVoiceByHints(System.Speech.Synthesis.VoiceGender.Female, VoiceAge.Adult);
                poolResources.Add(synth);
            }

            _pooledSynths = new ResourcePool<SpeechSynthesizer>(poolResources, _logger.Clone("SAPIEnginePool"), metricDimensions, "SAPIEngines");
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~SapiSpeechSynth()
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
                _pooledSynths.Dispose();
            }
        }

        private IDictionary<LanguageCode, CultureInfo> BuildListOfSupportedLocales()
        {
            Dictionary<LanguageCode, CultureInfo> allLocales = new Dictionary<LanguageCode, CultureInfo>();

            SpeechSynthesizer testSynth = new SpeechSynthesizer();
            foreach (InstalledVoice voice in testSynth.GetInstalledVoices())
            {
                LanguageCode parsedLocale = LanguageCode.Parse(voice.VoiceInfo.Culture.Name);
                if (voice.Enabled && !allLocales.ContainsKey(parsedLocale))
                {
                    allLocales.Add(parsedLocale, voice.VoiceInfo.Culture);
                }
            }

            return allLocales;
        }

        /// <inheritdoc />
        public bool IsLocaleSupported(LanguageCode locale)
        {
            return _supportedCultures.ContainsKey(locale);
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
            if (!IsLocaleSupported(request.Locale))
            {
                traceLogger.Log("Unsupported TTS locale " + request.Locale, LogLevel.Err);
                return new SynthesizedSpeech()
                {
                    Audio = new AudioData(),
                    Ssml = request.Ssml,
                    Locale = request.Locale.ToBcp47Alpha2String(),
                    PlainText = string.Empty,
                    Words = new List<SynthesizedWord>()
                };
            }

            if (string.IsNullOrEmpty(request.Ssml) && string.IsNullOrEmpty(request.Plaintext))
            {
                await DurandalTaskExtensions.NoOpTask;
                return new SynthesizedSpeech()
                {
                    Audio = new AudioData(),
                    Ssml = request.Ssml,
                    Locale = request.Locale.ToBcp47Alpha2String(),
                    PlainText = string.Empty,
                    Words = new List<SynthesizedWord>()
                };
            }

            // Now replace the initial <speak> tag with one that has the locale
            //traceLogger.Log("Parsing SSML", LogLevel.Vrb);
            string formattedSsml = GenerateFinalSsml(request);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            {
                Tuple<IAudioSampleSource, IList<SynthesizedWord>> responseTuple = await SynthesizeSpeechToStreamAsyncInternal(
                    formattedSsml,
                    request.Locale,
                    request.VoiceGender,
                    new WeakPointer<IAudioGraph>(graph),
                    _audioOutputFormat,
                    cancelToken,
                    realTime,
                    traceLogger);

                // OPT: This is a bit wasteful because we convert raw PCM data back and forth 
                traceLogger.Log("Converting SAPI speech stream into static sample (this is a bit wasteful)");
                using (IAudioSampleSource synthSource = responseTuple.Item1)
                using (RecyclableMemoryStream encodedAudioStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                //using (BucketAudioSampleTarget sampleBucket = new BucketAudioSampleTarget(graph, synthSource.OutputFormat))
                //using (AudioSplitter splitter = new AudioSplitter(graph, synthSource.OutputFormat))
                using (AudioEncoder pcmEncoder = new RawPcmEncoder(new WeakPointer<IAudioGraph>(graph), synthSource.OutputFormat, "SAPIPcmEncoder"))
                {
                    await pcmEncoder.Initialize(new NonRealTimeStreamWrapper(encodedAudioStream, false), true, cancelToken, realTime).ConfigureAwait(false);
                    synthSource.ConnectOutput(pcmEncoder);
                    //splitter.AddOutput(pcmEncoder);
                    //splitter.AddOutput(sampleBucket);
                    await pcmEncoder.ReadFully(cancelToken, realTime, TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);

                    AudioData encodedResponseData = new AudioData()
                    {
                        Codec = pcmEncoder.Codec,
                        CodecParams = pcmEncoder.CodecParams,
                        Data = new ArraySegment<byte>(encodedAudioStream.ToArray())
                    };

                    return new SynthesizedSpeech()
                    {
                        Audio = encodedResponseData,
                        Locale = request.Locale.ToBcp47Alpha2String(),
                        Ssml = request.Ssml,
                        PlainText = request.Plaintext,
                        Words = responseTuple.Item2
                    };
                }
            }
        }

        /// <inheritdoc />
        public async Task<IAudioSampleSource> SynthesizeSpeechToStreamAsync(
            SpeechSynthesisRequest request,
            WeakPointer<IAudioGraph> parentGraph,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            ILogger traceLogger = null)
        {
            traceLogger = traceLogger ?? _logger;
            ValidateRequest(request, traceLogger);
            if (!IsLocaleSupported(request.Locale))
            {
                traceLogger.Log("Unsupported TTS locale " + request.Locale, LogLevel.Err);
                return new FixedAudioSampleSource(parentGraph, new AudioSample(BinaryHelpers.EMPTY_FLOAT_ARRAY, AudioSampleFormat.Mono(16000)), "FakeSpeechSource");
            }

            if (string.IsNullOrEmpty(request.Ssml) && string.IsNullOrEmpty(request.Plaintext))
            {
                return new FixedAudioSampleSource(parentGraph, new AudioSample(BinaryHelpers.EMPTY_FLOAT_ARRAY, AudioSampleFormat.Mono(16000)), "FakeSpeechSource");
            }

            // Now replace the initial <speak> tag with one that has the locale
            //traceLogger.Log("Parsing SSML", LogLevel.Vrb);
            string formattedSsml = GenerateFinalSsml(request);

            Tuple<IAudioSampleSource, IList<SynthesizedWord>> responseTuple = await SynthesizeSpeechToStreamAsyncInternal(
                formattedSsml,
                request.Locale,
                request.VoiceGender,
                parentGraph,
                _audioOutputFormat,
                cancelToken,
                realTime,
                traceLogger);

            return responseTuple.Item1;
        }

        private static void ValidateRequest(SpeechSynthesisRequest request, ILogger logger)
        {
            request = request.AssertNonNull(nameof(request));

            if (request.Locale == null)
            {
                throw new ArgumentException("Speak locale is null!");
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

        private static string GenerateFinalSsml(SpeechSynthesisRequest request)
        {
            // Remove the <speak> tag from the input SSML
            string formattedSsml = StringUtils.RegexReplace(SpeakTagRegex, request.Ssml, string.Empty);

            // Then add a new surrogate speak tag that has the attributes we want
            formattedSsml = string.Format(SpeakMarkup, request.Locale, formattedSsml);

            return formattedSsml;
        }

        private async Task<Tuple<IAudioSampleSource, IList<SynthesizedWord>>> SynthesizeSpeechToStreamAsyncInternal(
            string ssml,
            LanguageCode locale,
            Durandal.API.VoiceGender voiceGender,
            WeakPointer<IAudioGraph> parentGraph,
            AudioSampleFormat outputFormat,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            ILogger traceLogger)
        {

            traceLogger.Log("Creating internal SAPI TTS stream");
            SapiSpeechStreamInternal synthStream = new SapiSpeechStreamInternal(
                traceLogger,
                ssml,
                GetVoiceCulture(locale),
                voiceGender,
                outputFormat,
                new WeakPointer<ResourcePool<SpeechSynthesizer>>(_pooledSynths));
            AudioDecoder decoder = new RawPcmDecoder(parentGraph, outputFormat, "SAPIDecoder");
            traceLogger.Log("Queued async SAPI rendering thread", LogLevel.Vrb);
            _synthThreadPool.Value.EnqueueUserAsyncWorkItem(synthStream.RunSynchronously);
            await decoder.Initialize(synthStream.AudioDataReadStream, false, cancelToken, realTime);
            decoder.TakeOwnershipOfDisposable(synthStream);
            traceLogger.Log("Finished initializing SAPI TTS stream");
            return new Tuple<IAudioSampleSource, IList<SynthesizedWord>>(decoder, synthStream.SynthesizedWords);
        }

        private CultureInfo GetVoiceCulture(LanguageCode locale)
        {
            CultureInfo returnVal;
            if (_supportedCultures.TryGetValue(locale, out returnVal))
            {
                return returnVal;
            }

            return CultureInfo.InvariantCulture;
        }

        private class SapiSpeechStreamInternal : IAudioDataSource
        {
            private readonly ILogger _logger;
            private readonly WeakPointer<ResourcePool<SpeechSynthesizer>> _synthPool;
            private readonly PipeStream _pipeStream; // this will end up buffering the speech output; maybe there's a better design?
            private readonly string _ssml;
            private readonly CultureInfo _locale;
            private readonly AudioSampleFormat _outputFormat;
            private readonly NonRealTimeStream _readStream;
            private readonly IList<SynthesizedWord> _synthesizedWords;
            private readonly Durandal.API.VoiceGender _desiredVoiceGender;
            private int _disposed = 0;

            public SapiSpeechStreamInternal(
                ILogger logger,
                string ssml, 
                CultureInfo locale,
                Durandal.API.VoiceGender desiredVoiceGender,
                AudioSampleFormat outputFormat,
                WeakPointer<ResourcePool<SpeechSynthesizer>> synthPool)
            {
                _logger = logger;
                _ssml = ssml;
                _locale = locale;
                _synthPool = synthPool;
                _outputFormat = outputFormat;
                _desiredVoiceGender = desiredVoiceGender;
                _synthesizedWords = new List<SynthesizedWord>();
                _pipeStream = new PipeStream();
                _readStream = _pipeStream.GetReadStream();
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~SapiSpeechStreamInternal()
            {
                Dispose(false);
            }
#endif

            public string Codec => RawPcmCodecFactory.CODEC_NAME_PCM_S16LE;

            public string CodecParams => CommonCodecParamHelper.CreateCodecParams(_outputFormat);

            public NonRealTimeStream AudioDataReadStream => _readStream;

            public IList<SynthesizedWord> SynthesizedWords => _synthesizedWords;

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
                    _pipeStream.Dispose();
                }
            }

            private void HandleSpeakProgressEvent(object sender, SpeakProgressEventArgs args)
            {
                // Collect word timings as the synth progresses through the prompt
                _synthesizedWords.Add(new SynthesizedWord()
                {
                    Word = args.Text,
                    Offset = args.AudioPosition,
                });
            }

            public async Task RunSynchronously()
            {
                _logger.Log("Started SAPI rendering thread", LogLevel.Vrb);
                ValueStopwatch speakStopwatch = ValueStopwatch.StartNew();
                RetrieveResult<PooledResource<SpeechSynthesizer>> rr = await _synthPool.Value.TryGetResourceAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
                if (rr.Success)
                {
                    SpeechSynthesizer synth = rr.Result.Value;

                    // This logic will default to female voice if unspecified
                    System.Speech.Synthesis.VoiceGender desiredSapiGender = System.Speech.Synthesis.VoiceGender.Female;
                    if (_desiredVoiceGender == Durandal.API.VoiceGender.Male)
                    {
                        desiredSapiGender = System.Speech.Synthesis.VoiceGender.Male;
                    }

                    // Select voice and language only if something has changed from the previous invocation
                    // This could potentially cause some slowdown if many requests to the pool are selecting many different voices
                    if (_locale != synth.Voice.Culture || desiredSapiGender != synth.Voice.Gender)
                    {
                        synth.SelectVoiceByHints(desiredSapiGender, VoiceAge.Adult, 0, _locale);
                    }

                    using (NonRealTimeStream writeStream = _pipeStream.GetWriteStream())
                    {
                        synth.SetOutputToAudioStream(
                            writeStream,
                            new SpeechAudioFormatInfo(
                                EncodingFormat.Pcm,
                                _outputFormat.SampleRateHz,
                                16,
                                _outputFormat.NumChannels,
                                _outputFormat.SampleRateHz * sizeof(short) * _outputFormat.NumChannels,
                                _outputFormat.NumChannels * sizeof(short),
                                BinaryHelpers.EMPTY_BYTE_ARRAY));

                        synth.SpeakProgress += HandleSpeakProgressEvent;

                        try
                        {
                            _logger.Log("Beginning SAPI SpeakSsml", LogLevel.Vrb);
                            synth.SpeakSsml(_ssml);
                        }
                        catch (Exception e)
                        {
                            _logger.Log(e, LogLevel.Err);
                        }
                        finally
                        {
                            _logger.Log("Finished SAPI SpeakSsml", LogLevel.Vrb);
                            speakStopwatch.Stop();
                            _logger.Log(CommonInstrumentation.GenerateLatencyEntry("SAPI_RunFullSpeechSynthesis", ref speakStopwatch), LogLevel.Ins);
                            synth.SetOutputToNull();
                            synth.SpeakProgress -= HandleSpeakProgressEvent;
                            _synthPool.Value.ReleaseResource(rr.Result);
                        }
                    }

                    //_logger.Log("Disposed of SAPI write stream");

                    // Fill in word timings that weren't given to us by the synth
                    for (int wordIdx = 0; wordIdx < _synthesizedWords.Count; wordIdx++)
                    {
                        if (wordIdx == _synthesizedWords.Count - 1)
                        {
                            // Last word in the sentence - we don't actually know the length so estimate it
                            _synthesizedWords[wordIdx].ApproximateLength = TimeSpan.FromMilliseconds(400);
                        }
                        else
                        {
                            // Assume all words are joined together by about 50ms of silence
                            _synthesizedWords[wordIdx].ApproximateLength = _synthesizedWords[wordIdx + 1].Offset - _synthesizedWords[wordIdx].Offset - TimeSpan.FromMilliseconds(50);
                            if (_synthesizedWords[wordIdx].ApproximateLength < TimeSpan.Zero)
                            {
                                _synthesizedWords[wordIdx].ApproximateLength = TimeSpan.Zero;
                            }
                        }
                    }
                }
            }
        }
    }
}