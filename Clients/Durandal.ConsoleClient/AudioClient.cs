
namespace Durandal.ConsoleClient
{
    using Durandal;
    using Durandal.API;
    using Durandal.Extensions.BondProtocol;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Client;
    using Durandal.Common.Client.Actions;
    using Durandal.Common.Compression;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.Language.English;
    using Durandal.Common.Security;
    using Durandal.Common.Security.Client;
    using Durandal.Common.Security.Login;
    using Durandal.Common.Security.Login.Providers;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.SR.Azure;
    using Durandal.Common.Speech.Triggers;
    using Durandal.Common.Speech.Triggers.Sphinx;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Utils;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Config.Annotation;
    using Durandal.Common.Audio.Components;
    using Durandal.Common.IO;
    using Durandal.Common.Speech;
    using Durandal.Common.Test;
    using Durandal.Common.Events;
    using Durandal.Extensions.Sapi;
    using Durandal.Extensions.NAudio.Devices;
    using Durandal.Extensions.NativeAudio.Codecs;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Net.Http2;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Dialog;
    using Durandal.Common.Audio.Codecs.Opus;
    using Durandal.Extensions.NativeAudio;
    using Durandal.Common.Audio.Hardware;
    using Durandal.Extensions.NAudio;

    public class AudioClient : IDisposable
    {
        private readonly ClientCore _core;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _coreLogger;
        private readonly ClientConfiguration _clientConfig;
        private readonly string _clientId;
        private readonly string _clientName;
        private readonly IMetricCollector _metrics;
        private readonly DimensionSet _dimensions;
        private readonly IAudioDriver _audioDeviceDriver;
        private readonly IAudioCaptureDevice _audioCaptureDevice;
        private readonly AudioExceptionCircuitBreaker _inputCircuitBreaker;
        private readonly JsonClientActionDispatcher _actionDispatcher;
        private readonly ExecuteDelayedActionHandler _delayedActionHandler;
        private readonly IDialogTransportProtocol _dialogProtocol;
        private readonly LinearMixerAutoConforming _audioMixer;
        private readonly PassthroughAudioPipe _clientMixerInputPipe;
        private readonly VolumeFilter _microphonePreamp;
        private readonly IAudioRenderDevice _audioRenderDevice;
        private readonly AudioExceptionCircuitBreaker _outputCircuitBreaker;
        private readonly IUtteranceRecorder _utteranceRecorder;
        private readonly AudioSample _successSound;
        private readonly AudioSample _failSound;
        private readonly AudioSample _skipSound;
        private readonly AudioSample _promptSound;
        private readonly IAudioGraph _audioInputGraphStrong;
        private readonly IAudioGraph _audioOutputGraphStrong;
        private readonly WeakPointer<IAudioGraph> _audioInputGraph;
        private readonly WeakPointer<IAudioGraph> _audioOutputGraph;
        private readonly AudioSampleFormat _inputFormat;
        private readonly AudioSampleFormat _outputFormat;

        private readonly AudioEncoder _speakerEncoder;

        private string _userId;
        private string _userName;
        private double _clientLatitude;
        private double _clientLongitude;
        private int _disposed;

        public AudioClient(string[] args)
        {
            string componentName = "ConsoleClient";
            string machineHostName = Dns.GetHostName();
            LogLevel level;
#if DEBUG
            level = LogLevel.All;
#else
            level = LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Ins;
#endif
            LogoUtil.PrintLogo("Audio Client", Console.Out);
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

            string rootRuntimeDirectory = ServiceCommon.GetDurandalEnvironmentDirectory(args, bootstrapLogger);
            ServiceCommon.UnpackBundleFile(bootstrapLogger, "bundle_client.zip", rootRuntimeDirectory);

            _fileSystem = new RealFileSystem(bootstrapLogger, rootRuntimeDirectory);

            IConfiguration configFile = IniFileConfiguration.Create(
                bootstrapLogger.Clone("PrimaryConfig"),
                new VirtualPath("Durandal.ConsoleClient_config.ini"),
                _fileSystem,
                DefaultRealTimeProvider.Singleton,
                true,
                true).Await();

            _clientConfig = new ClientConfiguration(configFile);

            string remoteLoggingEndpoint = _clientConfig.GetBase().GetString("remoteLoggingEndpoint", "durandal-ai.net");
            int remoteLoggingPort = _clientConfig.GetBase().GetInt32("remoteLoggingPort", 62295);
            string remoteLoggingStream = _clientConfig.GetBase().GetString("remoteLoggingStream", "Dev");

            ILogger console = new ConsoleLogger(componentName, level, null);
            _coreLogger = new AggregateLogger(componentName,
                null,
                console,
                new FileLogger(
                    new RealFileSystem(bootstrapLogger.Clone("FileLogger"), rootRuntimeDirectory),
                    componentName,
                    logFilePrefix: Process.GetCurrentProcess().ProcessName,
                    backgroundLogThreadPool: LoggerBase.DEFAULT_BACKGROUND_LOGGING_THREAD_POOL,
                    bootstrapLogger: bootstrapLogger,
                    validLogLevels: LogLevel.All,
                    maxLogLevels: LogLevel.All,
                    maxFileSizeBytes: 10 * 1024 * 1024,
                    logDirectory: new VirtualPath("logs")),
                new RemoteInstrumentationLogger(
                    new PortableHttpClient(
                        remoteLoggingEndpoint,
                        remoteLoggingPort,
                        false,
                        console,
                        new WeakPointer<IMetricCollector>(_metrics),
                        _dimensions),
                    new InstrumentationBlobSerializer(),
                    DefaultRealTimeProvider.Singleton,
                    streamName: remoteLoggingStream,
                    tracesOnly: true,
                    bootstrapLogger: bootstrapLogger,
                    metrics: _metrics,
                    dimensions: _dimensions,
                    componentName: componentName,
                    //validLogLevels: LogLevel.All,
                    //maxLogLevels: LogLevel.All,
                    //maxPrivacyClasses: DataPrivacyClassification.All,
                    //defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
                    backgroundLogThreadPool: LoggerBase.DEFAULT_BACKGROUND_LOGGING_THREAD_POOL));

            AssemblyReflector.ApplyAccelerators(typeof(NativeOpusAccelerator).Assembly, _coreLogger);

            // Check for existing client id. If not found, create a new one and save it back to the config
            if (string.IsNullOrEmpty(_clientConfig.ClientId))
            {
                _coreLogger.Log("No client ID found, generating a new value...");
                RawConfigValue newClientId = new RawConfigValue("clientId", StringUtils.HashToGuid(Environment.MachineName).ToString("N"), ConfigValueType.String);
                newClientId.Annotations.Add(new DescriptionAnnotation("The unique ID to identify this client. If this value is removed, a new one will be generated."));
                _clientConfig.GetBase().Set(newClientId);
            }

            if (string.IsNullOrEmpty(_clientConfig.UserId))
            {
                _coreLogger.Log("No user ID found, generating a new value...");
                RawConfigValue newUserId = new RawConfigValue("userId", StringUtils.HashToGuid(Environment.UserName).ToString("N"), ConfigValueType.String);
                newUserId.Annotations.Add(new DescriptionAnnotation("The unique ID to identify this user. If this value is removed, a new one will be generated."));
                _clientConfig.GetBase().Set(newUserId);
            }

            if (string.IsNullOrEmpty(_clientConfig.ClientName))
            {
                _clientName = Dns.GetHostName();
                _coreLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.EndUserIdentifiableInformation, "No client name found, using machine name \"{0}\" instead...", _clientName);
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
                _coreLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.EndUserIdentifiableInformation, "No user name found, using logged-in name \"{0}\" instead...", _userName);
                RawConfigValue newUserName = new RawConfigValue("userName", _userName, ConfigValueType.String);
                newUserName.Annotations.Add(new DescriptionAnnotation("A human-readable name for this user. By default, this is the local logged-in user name."));
                newUserName.Annotations.Add(new GUIAnnotation());
                _clientConfig.GetBase().Set(newUserName);
            }
            else
            {
                _userName = _clientConfig.UserName;
            }

            _dialogProtocol = new DialogJsonTransportProtocol();
            _clientId = _clientConfig.ClientId;
            _userId = _clientConfig.UserId;

            GetGeoIPCoords().Await();

            // Configure audio output graph
            _audioOutputGraphStrong = new AudioGraph(AudioGraphCapabilities.Concurrent);
            _audioOutputGraph = new WeakPointer<IAudioGraph>(_audioOutputGraphStrong);
            _outputFormat = AudioSampleFormat.Stereo(48000);
            _audioDeviceDriver = new WasapiDeviceDriver(_coreLogger.Clone("WasapiDriver"));

            _audioRenderDevice = _audioDeviceDriver.OpenRenderDevice(
                null,
                _audioOutputGraph,
                _outputFormat,
                "ClientSpeakers",
                desiredLatency: TimeSpan.FromMilliseconds(50));

            AudioSplitter speakerSplitter = new AudioSplitter(_audioOutputGraph, _outputFormat, "SpeakerSplitter");
            _speakerEncoder = new RiffWaveEncoder(_audioOutputGraph, _outputFormat, "SpeakerWriter", _coreLogger.Clone("SpeakerWriter"));
            speakerSplitter.AddOutput(_audioRenderDevice);
            speakerSplitter.AddOutput(_speakerEncoder);

            _outputCircuitBreaker = new AudioExceptionCircuitBreaker(_audioOutputGraph, _outputFormat, "OutputAudioCircuitBreaker", _coreLogger.Clone("OutputAudioCircuitBreaker"));
            _audioMixer = new LinearMixerAutoConforming(_audioOutputGraph, _outputFormat, "MainAudioMixer", true, _coreLogger.Clone("AudioMixer"), new WeakPointer<IMetricCollector>(_metrics), _dimensions);
            _clientMixerInputPipe = new PassthroughAudioPipe(_audioOutputGraph, _outputFormat, "ClientMixerInputPipe");
            _audioMixer.AddInput(_clientMixerInputPipe);
            _outputCircuitBreaker.ConnectOutput(speakerSplitter);
            _audioMixer.ConnectOutput(_outputCircuitBreaker);

            // Audio input graph
            _audioInputGraphStrong = new AudioGraph(AudioGraphCapabilities.Concurrent);
            _audioInputGraph = new WeakPointer<IAudioGraph>(_audioInputGraphStrong);
            _audioCaptureDevice = _audioDeviceDriver.OpenCaptureDevice(
                null,
                _audioInputGraph,
                _outputFormat,
                "ClientMicrophone",
                desiredLatency: TimeSpan.FromMilliseconds(50));
            _inputFormat = _audioCaptureDevice.OutputFormat;
            _microphonePreamp = new VolumeFilter(_audioInputGraph, _inputFormat, "MicPreamp");
            _microphonePreamp.VolumeLinear = _clientConfig.MicrophonePreamp;
            _inputCircuitBreaker = new AudioExceptionCircuitBreaker(_audioInputGraph, _inputFormat, "InputAudioCircuitBreaker", _coreLogger.Clone("InputAudioCircuitBreaker"));
            _audioCaptureDevice.ConnectOutput(_inputCircuitBreaker);
            _inputCircuitBreaker.ConnectOutput(_microphonePreamp);
            _utteranceRecorder = new DynamicUtteranceRecorder(_audioInputGraph, _inputFormat, "ClientUtteranceRecorder", _coreLogger.Clone("UtteranceRecorder"));

            _failSound = ReadWaveFile(rootRuntimeDirectory + "\\data\\Fail.wav");
            _successSound = ReadWaveFile(rootRuntimeDirectory + "\\data\\Confirm.wav");
            _skipSound = ReadWaveFile(rootRuntimeDirectory + "\\data\\Fail.wav");
            _promptSound = ReadWaveFile(rootRuntimeDirectory + "\\data\\Prompt.wav");

            _actionDispatcher = new JsonClientActionDispatcher();
            _actionDispatcher.AddHandler(new StopListeningActionHandler());
            _actionDispatcher.AddHandler(new SendNextTurnAudioActionHandler());

            _delayedActionHandler = new ExecuteDelayedActionHandler();
            _delayedActionHandler.ExecuteActionEvent.Subscribe(ExecuteDelayedDialogAction);
            _actionDispatcher.AddHandler(_delayedActionHandler);

            _core = new ClientCore();

            _core.Success.Subscribe(Success);
            _core.Fail.Subscribe(Fail);
            _core.Skip.Subscribe(Skip);
            _core.ShowErrorOutput.Subscribe(ShowErrorOutput);
            _core.ResponseReceived.Subscribe(ResponseReceived);
            _core.SpeechPrompt.Subscribe(SpeechPrompt);
            _core.SpeechCaptureFinished.Subscribe(SpeechFinished);
            _core.SpeechCaptureIntermediate.Subscribe(SpeechIntermediate);
            _core.InitializationUpdate.Subscribe(CoreInitializationMessage);
            _core.MakingRequest.Subscribe(MakingRequest);
            _core.UserIdentityChanged.Subscribe(UserIdentityChanged);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AudioClient()
        {
            Dispose(false);
        }
#endif

        public async Task Run()
        {
            ILogger httpClientLogger = _coreLogger.Clone("DialogHttpClient");
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;

            SphinxAudioTrigger trigger = null;
            if (_clientConfig.TriggerEnabled)
            {
                trigger = new SphinxAudioTrigger(
                    _audioInputGraph,
                    _inputFormat,
                    "ClientAudioTrigger",
                    NativePocketSphinxAdapter.GetPInvokeAdapterForPlatform(_fileSystem, _coreLogger.Clone("PocketSphinx")),
                    _coreLogger.Clone("SphinxTrigger"),
                    _clientConfig.PocketSphinxAmDirectory,
                    _clientConfig.PocketSphinxDictionaryFile,
                    new KeywordSpottingConfiguration()
                    {
                        PrimaryKeyword = _clientConfig.TriggerPhrase,
                        PrimaryKeywordSensitivity = _clientConfig.PrimaryAudioTriggerSensitivity,
                        SecondaryKeywords = new List<string>(),
                        SecondaryKeywordSensitivity = _clientConfig.SecondaryAudioTriggerSensitivity
                    },
                    false);
                trigger.Initialize();
            }

            ITriggerArbitrator triggerArbitrator = null;
            //IHttpClientFactory httpClientFactory = new PortableHttpClientFactory(new WeakPointer<IMetricCollector>(_metrics), _dimensions);

            ISocketFactory socketFactory = new PooledTcpClientSocketFactory(_coreLogger.Clone("SocketFactory"), _metrics, _dimensions);
            IHttpClientFactory httpClientFactory = new SocketHttpClientFactory(
                new WeakPointer<ISocketFactory>(socketFactory),
                new WeakPointer<IMetricCollector>(_metrics),
                _dimensions,
                Http2SessionManager.Default);

            if (_clientConfig.TriggerArbitratorUrl != null && !string.IsNullOrEmpty(_clientConfig.TriggerArbitratorGroupName))
            {
                triggerArbitrator = new HttpTriggerArbitrator(
                    httpClientFactory.CreateHttpClient(_clientConfig.TriggerArbitratorUrl, _coreLogger.Clone("TriggerArbitrator")),
                    TimeSpan.FromMilliseconds(500),
                    _clientConfig.TriggerArbitratorGroupName);
            }

            IHttpClient dialogHttp = httpClientFactory.CreateHttpClient(_clientConfig.RemoteDialogServerAddress, httpClientLogger);

            List<ILoginProvider> loginProviders = new List<ILoginProvider>();
            if (_clientConfig.AuthenticationEndpoint != null)
            {
                loginProviders.Add(AdhocLoginProvider.BuildForClient(httpClientFactory, _coreLogger.Clone("AdhocAuthenticator"), _clientConfig.AuthenticationEndpoint));
                MSAPortableLoginProvider msaProvider = MSAPortableLoginProvider.BuildForClient(httpClientFactory, _coreLogger.Clone("MSALogin"), _clientConfig.AuthenticationEndpoint);
                loginProviders.Add(msaProvider);
                _actionDispatcher.AddHandler(new MsaPortableLoginActionHandler(msaProvider, TimeSpan.FromMinutes(10)));
            }

            IDictionary<string, NLPTools> ttsNlTools = new Dictionary<string, NLPTools>();
            ttsNlTools["en-US"] = new NLPTools()
            {
                WordBreaker = new EnglishWholeWordBreaker(),
                SpeechTimingEstimator = new EnglishSpeechTimingEstimator()
            };

            FakeSpeechRecognizerFactory fakeSpeechReco = new FakeSpeechRecognizerFactory(_inputFormat);
            fakeSpeechReco.SetRecoResult("en-US", "test");

            ISpeechRecognizerFactory speechRecoFactory = 
                //new AzureSpeechRecognizerFactory(httpClientFactory, new TcpClientSocketFactory(_coreLogger.Clone("SRSocketFactory")), _coreLogger.Clone("SR"), _clientConfig.SRApiKey, realTime);
                //new AzureNativeSpeechRecognizerFactory(httpClientFactory, _coreLogger.Clone("SR"), _clientConfig.SRApiKey, realTime);
                fakeSpeechReco;
            ISpeechSynth synth = new SapiSpeechSynth(
                _coreLogger.Clone("SAPISynth"),
                new WeakPointer<IThreadPool>(new TaskThreadPool(new WeakPointer<IMetricCollector>(_metrics), _dimensions, "SAPIThreadPool")),
                _outputFormat,
                new WeakPointer<IMetricCollector>(_metrics),
                _dimensions,
                speechPoolSize: 1);

            AggregateCodecFactory codecFactory = new AggregateCodecFactory(
                new OpusRawCodecFactory(_coreLogger.Clone("OpusCodec"), realTimeDecodingBudget: TimeSpan.FromMilliseconds(10)),
                new NativeFlacCodecFactory(_coreLogger.Clone("FlacCodec")),
                new RawPcmCodecFactory(),
                new OggOpusCodecFactory(),
                new SquareDeltaCodecFactory(),
                new ALawCodecFactory(),
                new ULawCodecFactory());

            ClientCoreParameters coreParams = new ClientCoreParameters(_clientConfig, GenerateClientContext)
            {
                Logger = _coreLogger.Clone("ClientCore"),
                Microphone = new WeakPointer<IAudioSampleSource>(_microphonePreamp),
                InputAudioGraph = _audioInputGraph,
                Speakers = new WeakPointer<IAudioSampleTarget>(_clientMixerInputPipe),
                OutputAudioGraph = _audioOutputGraph,
                CodecFactory = codecFactory,
                SpeechReco = new WeakPointer<ISpeechRecognizerFactory>(speechRecoFactory),
                DialogConnection = new DialogHttpClient(dialogHttp, httpClientLogger, _dialogProtocol),
                AudioTrigger = new WeakPointer<IAudioTrigger>(trigger),
                UtteranceRecorder = new WeakPointer<IUtteranceRecorder>(_utteranceRecorder),
                EnableRSA = true,
                SpeechSynth = new WeakPointer<ISpeechSynth>(synth),
                ClientActionDispatcher = _actionDispatcher,
                LoginProviders = loginProviders,
                PrivateKeyStore = new FileBasedClientKeyStore(_fileSystem, _coreLogger),
                AudioTriggerArbitrator = triggerArbitrator
            };

            try
            {
                await _core.Initialize(coreParams);

                FileStream outStream = new FileStream("C:\\Code\\Durandal\\Data\\speakers.wav", FileMode.Create, FileAccess.ReadWrite);
                await _speakerEncoder.Initialize(outStream, ownsStream: true, CancellationToken.None, DefaultRealTimeProvider.Singleton);

                await _audioRenderDevice.StartPlayback(realTime);
                await _audioCaptureDevice.StartCapture(realTime);

                //ClientIdentifier authId = new ClientIdentifier(_userId, _userName, _clientId, _clientName);
                //if (await _core.DoCredentialsExist(authId, ClientAuthenticationScope.UserClient))
                //{
                //    await _core.LoadPrivateKeyFromLocalStore(authId, ClientAuthenticationScope.UserClient);
                //}
                //else
                //{
                //    await _core.LoadPrivateKeyFromAuthProvider(authId, ClientAuthenticationScope.UserClient);
                //}

                //await _core.AuthenticateAsUserClient(_userId, _userName, _clientId, _clientName, _coreLogger);

                Console.WriteLine("Client is initialized and ready to process queries.");
                Console.WriteLine("Either say the trigger phrase \"" + _clientConfig.TriggerPhrase + "\" or press [SPACE] to start recording.");
                Console.WriteLine("Any other input will end the program.");
                _audioMixer.AddInput(new FixedAudioSampleSource(_audioOutputGraph, _successSound, "InitializeOkSound"), null, true);
                bool running = true;
                while (running)
                {
                    ConsoleKeyInfo key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Spacebar)
                    {
                        if (!(await _core.TryMakeAudioRequest(this.GenerateClientContext(), realTime: realTime)))
                        {
                            _coreLogger.Log("Audio request denied, as the client core is already engaged in another conversation", LogLevel.Wrn);
                        }
                    }
                    else if (key.KeyChar == 'f')
                    {
                        Flood();
                    }
                    else
                    {
                        // Anything besides spacebar will end the program
                        running = false;
                    }
                }
            }
            finally
            {
                _core.Dispose();
                await _speakerEncoder.Finish(CancellationToken.None, realTime);
                _speakerEncoder.Dispose();
                _audioCaptureDevice.Dispose();
                _outputCircuitBreaker.Dispose();
            }
        }

        public async Task ExecuteDelayedDialogAction(object sender, DialogActionEventArgs args, IRealTimeProvider realTime)
        {
            Console.WriteLine("I am executing a delayed action and the ID is " + args.ActionId);
            await DurandalTaskExtensions.NoOpTask;
        }

        public async Task ShowErrorOutput(object sender, TextEventArgs args, IRealTimeProvider realTime)
        {
            Console.Error.WriteLine(args.Text);
            await DurandalTaskExtensions.NoOpTask;
        }

        public async Task Success(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            // TODO On success, we will get the Success event, and then the server may have returned audio.
            // If we PlaySound with async = true, it will queue up the two sounds so they don't both play at once.
            // However, that's technically a race condition, so it may need a better design.
            _audioMixer.AddInput(new FixedAudioSampleSource(_audioOutputGraph, _successSound, "TurnSuccessSound"), null, true);
            await DurandalTaskExtensions.NoOpTask;
        }

        public async Task Fail(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            _audioMixer.AddInput(new FixedAudioSampleSource(_audioOutputGraph, _failSound, "TurnFailSound"), null, true);
            await DurandalTaskExtensions.NoOpTask;
        }

        public async Task Skip(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            _audioMixer.AddInput(new FixedAudioSampleSource(_audioOutputGraph, _skipSound, "TurnSkipSound"), null, true);
            await DurandalTaskExtensions.NoOpTask;
        }

        /// <summary>
        /// This is fired when a response of any type is recieved from the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <param name="realTime"></param>
        public async Task ResponseReceived(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            Console.WriteLine("We got a response back");
            await DurandalTaskExtensions.NoOpTask;
        }

        public async Task SpeechPrompt(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            Console.WriteLine("Listening...");
            _delayedActionHandler.Reset();
            _audioMixer.AddInput(new FixedAudioSampleSource(_audioOutputGraph, _promptSound, "SpeechPromptSound"), null, true);
            await DurandalTaskExtensions.NoOpTask;
        }

        public async Task MakingRequest(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            await DurandalTaskExtensions.NoOpTask;
        }

        public async Task SpeechIntermediate(object sender, TextEventArgs args, IRealTimeProvider realTime)
        {
            if (!string.IsNullOrEmpty(args.Text))
            {
                //Console.WriteLine("Utterance: " + args.Text);
            }

            await DurandalTaskExtensions.NoOpTask;
        }

        public async Task SpeechFinished(object sender, SpeechCaptureEventArgs args, IRealTimeProvider realTime)
        {
            if (args.Success)
            {
                Console.WriteLine("Final utterance: " + args.Transcript);

                // Log the utterance to a file for debugging, etc.
                //args.Audio.WriteToFile(".\\data\\utterance_" + DateTimeOffset.Now.Ticks + ".wav");
            }
            else
            {
                Console.WriteLine("Didn't hear anything");
                _audioMixer.AddInput(new FixedAudioSampleSource(_audioOutputGraph, _skipSound, "SpeechSkipSound"), null, true);
            }

            await DurandalTaskExtensions.NoOpTask;
        }

        public async Task CoreInitializationMessage(object sender, TextEventArgs args, IRealTimeProvider realTime)
        {
            if (!string.IsNullOrEmpty(args.Text))
            {
                _coreLogger.Log(args.Text);
            }

            await DurandalTaskExtensions.NoOpTask;
        }

        private static AudioSample ReadWaveFile(string fileName)
        {
            using (IAudioGraph dummyGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream readStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(dummyGraph), null))
            using (NonRealTimeStream nrtStream = new NonRealTimeStreamWrapper(readStream, false))
            {
                decoder.Initialize(nrtStream, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();

                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(dummyGraph), decoder.OutputFormat, null))
                {
                    decoder.ConnectOutput(sampleTarget);
                    sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                    return sampleTarget.GetAllAudio();
                }
            }
        }
        
        private async Task UserIdentityChanged(object sender, UserIdentityChangedEventArgs e, IRealTimeProvider realTime)
        {
            _userId = e.NewIdentity.Id;
            _userName = e.NewIdentity.FullName;
            await DurandalTaskExtensions.NoOpTask;
        }

        private ClientContext GenerateClientContext()
        {
            ClientContext context = new ClientContext();
            context.SetCapabilities(
                ClientCapabilities.HasInternetConnection |
                ClientCapabilities.HasSpeakers |
                ClientCapabilities.HasMicrophone |
                ClientCapabilities.RsaEnabled |
                ClientCapabilities.SupportsCompressedAudio |
                ClientCapabilities.SupportsStreamingAudio |
                ClientCapabilities.ClientActions |
                ClientCapabilities.KeywordSpotter);
            context.ClientId = _clientId;
            context.UserId = _userId;

            // The locale code for the communication.
            context.Locale = LanguageCode.EN_US;

            // Local client time
            context.ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            context.UTCOffset = -420;

            // The common name of the client (to be used in dialog-side configuration, prettyprinting, etc.)
            context.ClientName = _clientName;

            // Client coordinates
            context.Latitude = _clientLatitude;
            context.Longitude = _clientLongitude;

            // Form factor code
            context.ExtraClientContext[ClientContextField.FormFactor] = FormFactor.Integrated.ToString();
            context.ExtraClientContext[ClientContextField.ClientType] = "AUDIO_CONSOLE";
            context.ExtraClientContext[ClientContextField.ClientVersion] = SVNVersionInfo.VersionString;
            return context;
        }

        private async Task GetGeoIPCoords()
        {
            if (_clientConfig.GetBase().ContainsKey("clientLatitude") && _clientConfig.GetBase().ContainsKey("clientLongitude"))
            {
                _clientLatitude = _clientConfig.ClientLatitude;
                _clientLongitude = _clientConfig.ClientLongitude;
                _coreLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.EndUserPseudonymousIdentifiers, "Using lat/long from stored configuration: {0} / {1}", _clientLatitude, _clientLongitude);
                await DurandalTaskExtensions.NoOpTask;
            }
            else
            {
                /*_coreLogger.Log("No lat/long is stored in config! Querying geoip service to try and resolve location...");
                string json = await DurandalUtils.HttpGetAsync("http://geoip.prototypeapp.com/api/locate");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        JsonSerializer ser = JsonSerializer.Create(new JsonSerializerSettings());
                        JObject result = ser.Deserialize(new JsonTextReader(new StringReader(json))) as JObject;
                        if (result != null)
                        {
                            clientLatitude = result["latitude"].Value<float>();
                            clientLongitude = result["longitude"].Value<float>();

                            ConfigValue latitudeConfig = new ConfigValue("clientLatitude", clientLatitude.ToString(), ConfigValueType.Float);
                            latitudeConfig.Annotations.Add(new DescriptionAnnotation("The default latitude value to use in the client's context"));
                            latitudeConfig.Annotations.Add(new GUIAnnotation());
                            _clientConfig.GetBase().Set(latitudeConfig);

                            ConfigValue longitudeConfig = new ConfigValue("clientLongitude", clientLongitude.ToString(), ConfigValueType.Float);
                            longitudeConfig.Annotations.Add(new DescriptionAnnotation("The default longitude value to use in the client's context"));
                            longitudeConfig.Annotations.Add(new GUIAnnotation());
                            _clientConfig.GetBase().Set(longitudeConfig);
                        }
                    }
                    catch (Exception e)
                    {
                        // Null result or json parsing failed
                        _coreLogger.Log("Error while retrieving lat/long: " + e.Message, LogLevel.Err);
                    }
                }
                else
                {
                    _coreLogger.Log("Error while retrieving lat/long: response was null", LogLevel.Err);
                }*/

                // FIXME Default to Seattle if it's not set in the client config. Should reenable geoIP service later...
                RawConfigValue latitudeConfig = new RawConfigValue("clientLatitude", "47.601757", ConfigValueType.Float);
                latitudeConfig.Annotations.Add(new DescriptionAnnotation("The default latitude value to use in the client's context"));
                latitudeConfig.Annotations.Add(new GUIAnnotation());
                _clientConfig.GetBase().Set(latitudeConfig);

                RawConfigValue longitudeConfig = new RawConfigValue("clientLongitude", "-122.336571", ConfigValueType.Float);
                longitudeConfig.Annotations.Add(new DescriptionAnnotation("The default longitude value to use in the client's context"));
                longitudeConfig.Annotations.Add(new GUIAnnotation());
                _clientConfig.GetBase().Set(longitudeConfig);
            }
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
                _actionDispatcher?.Dispose();
                _audioCaptureDevice?.Dispose();
                _audioInputGraphStrong?.Dispose();
                _audioMixer?.Dispose();
                _audioOutputGraphStrong?.Dispose();
                _audioRenderDevice?.Dispose();
                _clientMixerInputPipe?.Dispose();
                _core?.Dispose();
                _delayedActionHandler?.Dispose();
                _inputCircuitBreaker?.Dispose();
                _metrics?.Dispose();
                _microphonePreamp?.Dispose();
                _outputCircuitBreaker?.Dispose();
                _utteranceRecorder?.Dispose();
                _speakerEncoder?.Dispose();
            }
        }

        // For measuring throughput of DE and hardening against concurrency / race conditions

        private void Flood()
        {
            Stopwatch throughputTimer = new Stopwatch();
            throughputTimer.Start();
            TestRunResults results = RunFloodQueries(_coreLogger, _clientConfig.RemoteDialogServerAddress, _dialogProtocol, new WeakPointer<IMetricCollector>(_metrics), _dimensions);
            throughputTimer.Stop();
            long elapsedTime = throughputTimer.ElapsedMilliseconds;
            Console.WriteLine("Total time was " + elapsedTime);
            Console.WriteLine("Avg latency was " + results.AvgLatency);
            double throughput = (double)results.NumQueries * 1000 / elapsedTime;
            Console.WriteLine("Throughput was " + throughput);
            double failurePercent = (double)results.NumFailures / results.NumQueries;
            Console.WriteLine("Failure rate was " + failurePercent);
        }

        private static TestRunResults RunFloodQueries(
            ILogger logger,
            Uri dialogServerEndpoint,
            IDialogTransportProtocol protocol,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions)
        {
            using (IThreadPool basePool = new TaskThreadPool())
            using (IThreadPool fixedPool = new FixedCapacityThreadPool(
                basePool,
                logger.Clone("Flood"),
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                maxCapacity: 8,
                overschedulingBehavior: ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable))
            {
                IList<ThreadedExecutor> executions = new List<ThreadedExecutor>();

                AudioData query;

                using (IAudioGraph dummyGraph = new AudioGraph(AudioGraphCapabilities.None))
                using (FileStream readStream = new FileStream(@"C:\Code\Durandal\Data\HeyCortana7.wav", FileMode.Open, FileAccess.Read))
                using (MemoryStream writeStream = new MemoryStream())
                using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(dummyGraph), null))
                using (NonRealTimeStream nrtReadStream = new NonRealTimeStreamWrapper(readStream, false))
                using (NonRealTimeStream nrtWriteStream = new NonRealTimeStreamWrapper(writeStream, false))
                {
                    decoder.Initialize(nrtReadStream, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();

                    using (RawPcmEncoder encoder = new RawPcmEncoder(new WeakPointer<IAudioGraph>(dummyGraph), decoder.OutputFormat, null))
                    {
                        encoder.Initialize(nrtWriteStream, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                        decoder.ConnectOutput(encoder);
                        decoder.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                        query = new AudioData()
                        {
                            Codec = encoder.Codec,
                            CodecParams = encoder.CodecParams,
                            Data = new ArraySegment<byte>(writeStream.ToArray()),
                        };
                    }
                }

                IHttp2SessionManager h2SessionManager = new Http2SessionManager();
                ISocketFactory socketFactory = new PooledTcpClientSocketFactory(logger, metrics.Value, metricDimensions);
                IHttpClientFactory httpClientFactory = new SocketHttpClientFactory(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    metrics,
                    metricDimensions,
                    new WeakPointer<IHttp2SessionManager>(h2SessionManager));
                IHttpClient serviceHttpClient = httpClientFactory.CreateHttpClient(dialogServerEndpoint, logger);
                DialogHttpClient dialogClient = new DialogHttpClient(
                    serviceHttpClient,
                    logger,
                    protocol);
                dialogClient.SetReadTimeout(TimeSpan.FromMilliseconds(10000));

                int numQueries = 100000;
                for (int c = 0; c < numQueries; c++)
                {
                    ThreadedExecutor executor = new ThreadedExecutor(query, dialogClient, logger, protocol, metrics, metricDimensions);
                    executions.Add(executor);
                    fixedPool.EnqueueUserAsyncWorkItem(executor.Run);
                }

                TestRunResults results = new TestRunResults();
                results.NumQueries = numQueries;
                results.NumFailures = 0;

                foreach (var executor in executions)
                {
                    executor.Join();
                    results.AvgLatency += executor.Latency;
                    if (!executor.Success)
                    {
                        results.NumFailures++;
                    }
                }

                results.AvgLatency /= numQueries;

                return results;
            }
        }

        private struct TestRunResults
        {
            public double AvgLatency;
            public int NumQueries;
            public int NumFailures;
        }

        private class ThreadedExecutor : IDisposable
        {
            public ILogger Logger;
            public AudioData Query;
            public double Latency;
            public bool Success = true;

            private readonly WeakPointer<IMetricCollector> _metrics;
            private readonly DimensionSet _metricDimensions;
            private readonly EventWaitHandle _finished;
            private readonly IDialogTransportProtocol _protocol;
            private readonly IDialogClient _dialogClient;

            private int _disposed;

            public ThreadedExecutor(
                AudioData query,
                IDialogClient dialogClient,
                ILogger logger,
                IDialogTransportProtocol protocol,
                WeakPointer<IMetricCollector> metrics,
                DimensionSet metricDimensions)
            {
                Query = query;
                Logger = logger;
                _dialogClient = dialogClient;
                _finished = new EventWaitHandle(false, EventResetMode.ManualReset);
                _protocol = protocol;
                _metrics = metrics.AssertNonNull(nameof(metrics));
                _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~ThreadedExecutor()
            {
                Dispose(false);
            }
#endif

            public async Task Run()
            {
                try
                {
                    DialogRequest request = new DialogRequest();
                    request.InteractionType = InputMethod.Spoken;
                    request.ClientContext.ClientId = Guid.NewGuid().ToString("N");
                    request.ClientContext.UserId = Guid.NewGuid().ToString("N");
                    request.ClientContext.Locale = LanguageCode.EN_US;
                    request.ClientContext.ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                    request.ClientContext.ClientName = "floodclient";
                    request.ClientContext.ExtraClientContext[ClientContextField.FormFactor] = FormFactor.Integrated.ToString();
                    request.ClientContext.SetCapabilities(ClientCapabilities.HasMicrophone |
                        ClientCapabilities.HasSpeakers |
                        ClientCapabilities.SupportsCompressedAudio |
                        ClientCapabilities.SupportsStreamingAudio);
                    request.PreferredAudioCodec = "sqrt";
                    request.AudioInput = Query;
                    request.SpeechInput = new Durandal.API.SpeechRecognitionResult();
                    request.SpeechInput.RecognitionStatus = SpeechRecognitionStatus.Success;
                    request.SpeechInput.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
                    {
                        DisplayText = "do a barrel roll",
                        SREngineConfidence = 1.0f
                    });
                    request.SpeechInput.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
                    {
                        DisplayText = "do a barrel rol",
                        SREngineConfidence = 0.8f
                    });
                    request.SpeechInput.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
                    {
                        DisplayText = "do a barrel rule",
                        SREngineConfidence = 0.7f
                    });
                    request.SpeechInput.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
                    {
                        DisplayText = "do up barrel roll",
                        SREngineConfidence = 0.5f
                    });

                    NetworkResponseInstrumented<DialogResponse> netResponse = await _dialogClient.MakeQueryRequest(
                        request,
                        Logger,
                        CancellationToken.None,
                        DefaultRealTimeProvider.Singleton);

                    if (netResponse == null || netResponse.Response == null)
                    {
                        Logger.Log("Null Response");
                        Success = false;
                    }
                    else
                    {
                        DialogResponse response = netResponse.Response;
                        if (response.ExecutionResult != Result.Success)
                        {
                            Logger.Log("Failure result");
                            Success = false;
                        }
                        else if (string.IsNullOrEmpty(response.StreamingAudioUrl))
                        {
                            Logger.Log("No audio response stream");
                            Success = false;
                        }
                        else
                        {
                            // Download the response audio
                            using (IAudioDataSource responseAudioResponse = await _dialogClient.GetStreamingAudioResponse(response.StreamingAudioUrl, Logger).ConfigureAwait(false))
                            using (RecyclableMemoryStream destStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                            {
                                await responseAudioResponse.AudioDataReadStream.CopyToAsync(destStream).ConfigureAwait(false);
                                if (destStream.Length == 0)
                                {
                                    Success = false;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(response.ErrorMessage))
                        {
                            Logger.Log("Error message returned: " + response.ErrorMessage);
                        }
                    }

                    Latency = netResponse.EndToEndLatency;
                }
                catch (Exception e)
                {
                    Logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _finished.Set();
                }
            }

            public void Join()
            {
                _finished.WaitOne();
                _finished.Dispose();
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
                    _finished?.Dispose();
                }
            }
        }
    }
}
