
namespace Durandal.ConsoleClient
{
    using Durandal;
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Audio.Codecs.Opus;
    using Durandal.Common.Audio.Components;
    using Durandal.Common.Audio.Test;
    using Durandal.Common.Client;
    using Durandal.Common.Compression;
    using Durandal.Common.Config;
    using Durandal.Common.Config.Annotation;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.Events;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Net.Http2;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Security.Client;
    using Durandal.Common.Security.Login;
    using Durandal.Common.Security.Login.Providers;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Speech;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Tasks;
    using Durandal.Common.Test;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.Extensions.BondProtocol;
    using Durandal.Extensions.NativeAudio.Codecs;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    public class MetronomeClient : IDisposable
    {
        private readonly string _clientName;
        private readonly string _userName;
        private readonly ClientCore _clientCore;
        private readonly ILogger _logger;
        private readonly ClientConfiguration _clientConfig;
        private readonly IFileSystem _fileSystem;
        private readonly IDialogTransportProtocol _dialogProtocol;
        private readonly bool _useAudio;
        private readonly IMetricCollector _metrics;
        private readonly DimensionSet _dimensions;
        private readonly ISocketFactory _socketFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private int _disposed = 0;

        public MetronomeClient(string[] args, bool useAudio)
        {
            string componentName = "ConsoleClient";
            string machineHostName = Dns.GetHostName();
            LogLevel level;

#if DEBUG
            level = LogLevel.All;
#else
            level = LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Ins;
#endif
            LogoUtil.PrintLogo("Metronome Client", Console.Out);
            ServicePointManager.Expect100Continue = false;

            ILogger bootstrapLogger = new ConsoleLogger(componentName, level, null);
            bootstrapLogger.Log(string.Format("Durandal client {0} built on {1}",
                SVNVersionInfo.VersionString,
                SVNVersionInfo.BuildDate));

            _metrics = new MetricCollector(bootstrapLogger, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            _dimensions = new DimensionSet(new MetricDimension[]
                {
                    new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceName, "Durandal.ConsoleClient"),
                    new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceVersion, SVNVersionInfo.AssemblyVersion),
                    new MetricDimension(CommonInstrumentation.Key_Dimension_HostName, machineHostName)
                });

            _useAudio = useAudio;

            // Unpack bundle data if present
            string rootRuntimeDirectory = ServiceCommon.GetDurandalEnvironmentDirectory(args, bootstrapLogger);
            ServiceCommon.UnpackBundleFile(bootstrapLogger, "bundle_client.zip", rootRuntimeDirectory);

            _fileSystem = new RealFileSystem(bootstrapLogger, rootRuntimeDirectory);
            VirtualPath configFileName = new VirtualPath("Durandal.ConsoleClient_config.ini");
            IConfiguration configFile = IniFileConfiguration.Create(bootstrapLogger, configFileName, _fileSystem, DefaultRealTimeProvider.Singleton, true, true).Await();
            _clientConfig = new ClientConfiguration(configFile);

            string remoteLoggingEndpoint = _clientConfig.GetBase().GetString("remoteLoggingEndpoint", "durandal-ai.net");
            int remoteLoggingPort = _clientConfig.GetBase().GetInt32("remoteLoggingPort", 62295);
            string remoteLoggingStream = _clientConfig.GetBase().GetString("remoteLoggingStream", "Dev");

            _socketFactory = new PooledTcpClientSocketFactory(bootstrapLogger.Clone("SocketFactory"), _metrics, _dimensions);
            _httpClientFactory = new SocketHttpClientFactory(
                new WeakPointer<ISocketFactory>(_socketFactory),
                new WeakPointer<IMetricCollector>(_metrics),
                _dimensions,
                Http2SessionManager.Default);

            //_httpClientFactory = new PortableHttpClientFactory(new WeakPointer<IMetricCollector>(_metrics), _dimensions);

            _logger = new AggregateLogger(componentName,
                null,
                bootstrapLogger,
                new FileLogger(
                    new RealFileSystem(bootstrapLogger.Clone("FileLogger"), rootRuntimeDirectory),
                    componentName,
                    logFilePrefix: Process.GetCurrentProcess().ProcessName,
                    backgroundLogThreadPool: LoggerBase.DEFAULT_BACKGROUND_LOGGING_THREAD_POOL,
                    bootstrapLogger: bootstrapLogger,
                    validLogLevels: LogLevel.All,
                    maxLogLevels: LogLevel.All,
                    maxFileSizeBytes: 100 * 1024 * 1024,
                    logDirectory: new VirtualPath("logs")),
                new RemoteInstrumentationLogger(
                    _httpClientFactory.CreateHttpClient(remoteLoggingEndpoint, remoteLoggingPort, false, bootstrapLogger),
                    new InstrumentationBlobSerializer(),
                    DefaultRealTimeProvider.Singleton,
                    remoteLoggingStream,
                    tracesOnly: true,
                    bootstrapLogger: bootstrapLogger,
                    metrics: _metrics,
                    dimensions: _dimensions,
                    componentName: componentName,
                    validLogLevels: level,
                    maxLogLevels: LogLevel.All,
                    //maxPrivacyClasses: DataPrivacyClassification.All,
                    //defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
                    backgroundLogThreadPool: LoggerBase.DEFAULT_BACKGROUND_LOGGING_THREAD_POOL));

            if (string.IsNullOrEmpty(_clientConfig.ClientId))
            {
                _logger.Log("No client ID found, generating a new value...");
                RawConfigValue newClientId = new RawConfigValue("clientId", StringUtils.HashToGuid(Environment.MachineName).ToString("N"), ConfigValueType.String);
                newClientId.Annotations.Add(new DescriptionAnnotation("The unique ID to identify this client. If this value is removed, a new one will be generated."));
                _clientConfig.GetBase().Set(newClientId);
            }

            if (string.IsNullOrEmpty(_clientConfig.UserId))
            {
                _logger.Log("No user ID found, generating a new value...");
                RawConfigValue newUserId = new RawConfigValue("userId", StringUtils.HashToGuid(Environment.UserName).ToString("N"), ConfigValueType.String);
                newUserId.Annotations.Add(new DescriptionAnnotation("The unique ID to identify this user. If this value is removed, a new one will be generated."));
                _clientConfig.GetBase().Set(newUserId);
            }

            if (string.IsNullOrEmpty(_clientConfig.ClientName))
            {
                _clientName = Dns.GetHostName();
                _logger.Log("No client name found, using machine name \"" + _clientName + "\" instead...");
                RawConfigValue newClientName = new RawConfigValue("clientName", _clientName, ConfigValueType.String);
                newClientName.Annotations.Add(new DescriptionAnnotation("A human-readable name for this client. By default, this is the local machine name."));
                newClientName.Annotations.Add(new GUIAnnotation());
                _clientConfig.GetBase().Set(newClientName);
            }
            else
            {
                _clientName = _clientConfig.ClientName;
            }

            if (string.IsNullOrEmpty(_clientConfig.UserName))
            {
                _userName = Environment.UserName;
                _logger.Log("No user name found, using logged-in name \"" + _userName + "\" instead...");
                RawConfigValue newUserName = new RawConfigValue("userName", _userName, ConfigValueType.String);
                newUserName.Annotations.Add(new DescriptionAnnotation("A human-readable name for this user. By default, this is the local logged-in user name."));
                newUserName.Annotations.Add(new GUIAnnotation());
                _clientConfig.GetBase().Set(newUserName);
            }
            else
            {
                _userName = _clientConfig.UserName;
            }

            _dialogProtocol = new DialogBondTransportProtocol();

            _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.EndUserPseudonymousIdentifiers, "Client ID = {0} User ID = {1}", _clientConfig.ClientId, _clientConfig.UserId);
            _clientCore = new ClientCore();

            _clientCore.ShowTextOutput.Subscribe(ShowText);
            _clientCore.ShowErrorOutput.Subscribe(ShowError);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~MetronomeClient()
        {
            Dispose(false);
        }
#endif

        public async Task Run()
        {
            QueryFlags queryFlags;
#if DEBUG
            queryFlags = QueryFlags.Debug;
#else
            queryFlags = QueryFlags.None;
#endif

            ILogger httpClientLogger = _logger.Clone("DialogHttpClient");
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DefaultRealTimeProvider.HighPrecisionWaitProvider = new Win32HighPrecisionWaitProvider();
            }

            IHttpClient dialogHttp = //new PortableHttpClient(_clientConfig.RemoteDialogServerAddress, httpClientLogger, new WeakPointer<IMetricCollector>(_metrics), _dimensions);
                _httpClientFactory.CreateHttpClient(_clientConfig.RemoteDialogServerAddress, httpClientLogger);
            dialogHttp.SetReadTimeout(TimeSpan.FromMilliseconds(30000));
            //dialogHttp.InitialProtocolVersion = Durandal.Common.Net.Http.HttpVersion.HTTP_1_1;

            IHttpClient dialogFloodHttp = _httpClientFactory.CreateHttpClient(_clientConfig.RemoteDialogServerAddress, httpClientLogger);
            dialogFloodHttp.SetReadTimeout(TimeSpan.FromMilliseconds(500));
            
            List<ILoginProvider> loginProviders = new List<ILoginProvider>();
            if (_clientConfig.AuthenticationEndpoint != null)
            {
                loginProviders.Add(AdhocLoginProvider.BuildForClient(_httpClientFactory, _logger.Clone("AdhocAuthenticator"), _clientConfig.AuthenticationEndpoint));
                MSAPortableLoginProvider msaProvider = MSAPortableLoginProvider.BuildForClient(_httpClientFactory, _logger.Clone("MSALogin"), _clientConfig.AuthenticationEndpoint);
                loginProviders.Add(msaProvider);
            }

            IAudioGraph inputAudioGraph = new AudioGraph(AudioGraphCapabilities.Concurrent);
            IAudioGraph outputAudioGraph = new AudioGraph(AudioGraphCapabilities.Concurrent);
            AudioSampleFormat inputFormat = AudioSampleFormat.Mono(16000);
            AudioSampleFormat outputFormat = AudioSampleFormat.Mono(48000);
            IUtteranceRecorder recorder = new StaticUtteranceRecorder(new WeakPointer<IAudioGraph>(inputAudioGraph), inputFormat, "FakeUtteranceRecorder", TimeSpan.FromSeconds(4), _logger.Clone("UtteranceRecorder"));
            IAudioSampleSource microphone = new FakeMultistreamMicrophone(_logger.Clone("FakeMicrophone"), new WeakPointer<IAudioGraph>(inputAudioGraph), inputFormat);
            NullAudioSampleTarget speakers = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(outputAudioGraph), outputFormat, "FakeSpeakers");
            IRealTimeProvider speakerReadTime = realTime.Fork("ClientSpeakerReadThread");
            Task speakerReadTask = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(
                async () => await RunActiveReadThread(_logger.Clone("SpeakersReadThread"), speakers, outputAudioGraph, speakerReadTime).ConfigureAwait(false));

            AggregateCodecFactory codecFactory = new AggregateCodecFactory(
                new OpusRawCodecFactory(_logger.Clone("OpusCodec"), realTimeDecodingBudget: TimeSpan.FromMilliseconds(10)),
                new NativeFlacCodecFactory(_logger.Clone("FlacCodec")),
                new RawPcmCodecFactory(),
                new OggOpusCodecFactory(),
                new SquareDeltaCodecFactory(),
                new ALawCodecFactory(),
                new ULawCodecFactory());

            ClientCoreParameters coreParams = new ClientCoreParameters(_clientConfig, BuildClientContext)
            {
                Logger = _logger,
                EnableRSA = true,
                DialogConnection = new DialogHttpClient(dialogHttp, httpClientLogger, _dialogProtocol),
                LoginProviders = loginProviders,
                PrivateKeyStore = new FileBasedClientKeyStore(_fileSystem, _logger),
                Speakers = new WeakPointer<IAudioSampleTarget>(speakers),
                CodecFactory = codecFactory,
                SpeechReco = new WeakPointer<ISpeechRecognizerFactory>(NullSpeechRecoFactory.Singleton),
                SpeechSynth = new WeakPointer<ISpeechSynth>(new FakeSpeechSynth(LanguageCode.EN_US)),
                Microphone = new WeakPointer<IAudioSampleSource>(microphone),
                HttpClientFactory = _httpClientFactory,
                InputAudioGraph = new WeakPointer<IAudioGraph>(inputAudioGraph),
                OutputAudioGraph = new WeakPointer<IAudioGraph>(outputAudioGraph),
                UtteranceRecorder = new WeakPointer<IUtteranceRecorder>(recorder),
            };

            await _clientCore.Initialize(coreParams);

            Console.WriteLine("Metronome client started. Requests will continue at a constant rate indefinitely");

            string[] queries = new string[]
            {
                "what time is it",
                "what day is it",
                "what does the fox say",
                "what is your name",
                "tell me a joke",
                "why did the chicken cross the road"
            };

            RateLimiter rateLimiter = new RateLimiter(1, 100);

            AudioData fakeAudio = DialogTestHelpers.GenerateAudioData(AudioSampleFormat.Mono(16000), 10);

            bool running = true;
            FastRandom randomDelay = new FastRandom();
            while (running)
            {
                foreach (string query in queries)
                {
                    rateLimiter.Limit(realTime, CancellationToken.None);
                    try
                    {
                        if (_useAudio)
                        {
                            SpeechRecognitionResult recoResult = new SpeechRecognitionResult()
                            {
                                RecognitionStatus = SpeechRecognitionStatus.Success,
                                ConfusionNetworkData = null,
                                RecognizedPhrases = new List<SpeechRecognizedPhrase>()
                                {
                                    new SpeechRecognizedPhrase()
                                    {
                                        DisplayText = query,
                                        InverseTextNormalizationResults = new List<string>() { query },
                                        IPASyllables = string.Empty,
                                        Locale = "en-US",
                                        SREngineConfidence = 1.0f,
                                        AudioTimeLength = TimeSpan.FromSeconds(3),
                                        AudioTimeOffset = TimeSpan.Zero,
                                        MaskedInverseTextNormalizationResults = new List<string>() { query },
                                        PhraseElements = new List<SpeechPhraseElement>(),
                                        ProfanityTags = new List<Tag>()
                                    }
                                }
                            };

                            await _clientCore.TryMakeAudioRequestWithSpeechResult(
                                recoResult,
                                new AudioSample(new float[16000], AudioSampleFormat.Mono(16000)),
                                BuildClientContext(),
                                flags: queryFlags,
                                realTime: realTime);
                        }
                        else
                        {
                            await _clientCore.TryMakeTextRequest(
                                query,
                                BuildClientContext(),
                                flags: queryFlags,
                                realTime: realTime);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                }
            }
        }

        private async Task RunActiveReadThread(ILogger logger, IAudioSampleTarget speakers, IAudioGraph audioGraph, IRealTimeProvider realTime)
        {
            try
            {
                logger.Log("Active audio read thread started", LogLevel.Vrb);
                const int REALTIME_RATIO = 10; // Represents how much faster than realtime we want to simulate the read stream
                const int MAX_READ_LENGTH_MS = 1000;
                int bufferSizePerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(speakers.InputFormat.SampleRateHz, TimeSpan.FromMilliseconds(MAX_READ_LENGTH_MS * REALTIME_RATIO));
                float[] buffer = new float[bufferSizePerChannel * speakers.InputFormat.NumChannels];
                bool playbackFinished = false;
                Stopwatch loopTimer = Stopwatch.StartNew();
                while (!playbackFinished)
                {
                    await audioGraph.LockGraphAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    if (speakers.Input == null)
                    {
                        audioGraph.UnlockGraph();
                        await realTime.WaitAsync(TimeSpan.FromMilliseconds(1), CancellationToken.None).ConfigureAwait(false); 
                    }
                    else
                    {
                        try
                        {
                            loopTimer.Stop();
                            int samplesToRead = Math.Min(bufferSizePerChannel, REALTIME_RATIO * (int)AudioMath.ConvertTicksToSamplesPerChannel(speakers.InputFormat.SampleRateHz, loopTimer.ElapsedTicks));
                            if (samplesToRead > 0)
                            {
                                int thisBatchSize = await speakers.Input.ReadAsync(buffer, 0, samplesToRead, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                                if (thisBatchSize < 0)
                                {
                                    playbackFinished = true;
                                }
                            }

                            loopTimer.Restart();
                        }
                        finally
                        {
                            audioGraph.UnlockGraph();
                        }
                    }

                    // this would normally wait for about 16ms unless we use a winmm high precision wait provider, which hopefully we do
                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(1), CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Err);
            }
            finally
            {
                logger.Log("Active audio read thread finished", LogLevel.Vrb);
                realTime.Merge();
            }
        }

        private ClientContext BuildClientContext()
        {
            ClientContext context = new ClientContext();
            if (_useAudio)
            {
                context.SetCapabilities(
                    ClientCapabilities.HasInternetConnection |
                    ClientCapabilities.DisplayUnlimitedText |
                    ClientCapabilities.HasSpeakers |
                    ClientCapabilities.HasMicrophone |
                    ClientCapabilities.SupportsCompressedAudio |
                    ClientCapabilities.SupportsStreamingAudio |
                    ClientCapabilities.RsaEnabled);
            }
            else
            {
                context.SetCapabilities(
                    ClientCapabilities.HasInternetConnection |
                    ClientCapabilities.DisplayUnlimitedText |
                    ClientCapabilities.RsaEnabled);
            }

            context.ClientId = _clientConfig.ClientId;
            context.UserId = _clientConfig.UserId;

            // The locale code for the communication.
            context.Locale = LanguageCode.EN_US;

            // Local client time
            context.ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            context.UTCOffset = -420;

            // The common name of the client (to be used in dialog-side configuration, prettyprinting, etc.)
            context.ClientName = _clientName;

            // Client coordinates
            // context.Latitude = clientLatitude;
            // context.Longitude = clientLongitude;

            // Form factor code
            context.ExtraClientContext[ClientContextField.FormFactor] = FormFactor.Integrated.ToString();
            context.ExtraClientContext[ClientContextField.ClientType] = "METRONOME";
            context.ExtraClientContext[ClientContextField.ClientVersion] = SVNVersionInfo.VersionString;
            return context;
        }

        private async Task ShowText(object source, TextEventArgs args, IRealTimeProvider realTime)
        {
            this._logger.Log("Got response text: " + args.Text);
            await DurandalTaskExtensions.NoOpTask;
        }

        private async Task ShowError(object source, TextEventArgs args, IRealTimeProvider realTime)
        {
            this._logger.Log("Got error message: " + args.Text, LogLevel.Err);
            await DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc/>
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
                _clientCore?.Dispose();
                _metrics?.Dispose();
                _socketFactory?.Dispose();
            }
        }
    }
}
