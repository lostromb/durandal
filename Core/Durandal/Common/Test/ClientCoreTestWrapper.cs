using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Test;
using Durandal.Common.Client;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Events;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Language;
using Durandal.Common.Security;
using Durandal.Common.Security.Client;
using Durandal.Common.Security.Login;
using Durandal.Common.Security.Login.Providers;
using Durandal.Common.Security.Server;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Speech;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.Triggers;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Test
{
    public class ClientCoreTestWrapper : IDisposable
    {
        private ClientCore _core;
        private ILogger _logger;
        private ClientConfiguration _clientConfig;
        private bool _audioEnabled;
        private IRealTimeProvider _realTime;
        private IAudioGraph _microphoneGraph;
        private IAudioGraph _speakersGraph;
        private FakeMultistreamMicrophone _microphone;
        private NullAudioSampleTarget _speakers;
        private FakeSpeechRecognizerFactory _speechReco;
        private IUtteranceRecorder _utteranceRecorder;
        private FakeAudioTrigger _trigger;
        private IFileSystem _fileSystem;
        private IClientActionDispatcher _actionHandler;
        private PresentationWebServer _clientWebServer;
        private AuthHttpServer _adhocAuthServer;
        private IPublicKeyStore _publicKeyStore;
        private InMemoryPrivateKeyStore _serverPrivateKeyStore;
        private IClientSideKeyStore _clientPrivateKeyStore;
        private int _disposed = 0;

        /// <summary>
        /// The virtual client object
        /// </summary>
        public ClientCore Core
        {
            get
            {
                return _core;
            }
        }

        /// <summary>
        /// The microphone mock
        /// </summary>
        public FakeMultistreamMicrophone Microphone
        {
            get
            {
                return _microphone;
            }
        }

        /// <summary>
        /// The speech recognition service mock
        /// </summary>
        public FakeSpeechRecognizerFactory SpeechReco
        {
            get
            {
                return _speechReco;
            }
        }

        /// <summary>
        /// The audio trigger mock
        /// </summary>
        public FakeAudioTrigger Trigger
        {
            get
            {
                return _trigger;
            }
        }

        /// <summary>
        /// The HTTP server that runs on the client - interacting with this can simulate user actions against the UI
        /// </summary>
        public PresentationWebServer PresentationWebServer
        {
            get
            {
                return _clientWebServer;
            }
        }

        public bool EnableStreamingAudio { get; set; }

        #region Event interceptors
        
        public void ResetEvents()
        {
            SuccessEvent.Reset();
            DisplayTextEvent.Reset();
            PlayAudioEvent.Reset();
            SpeechTriggerEvent.Reset();
            AudioPromptEvent.Reset();
            SpeechCaptureFinishedEvent.Reset();
        }

        public EventRecorder<EventArgs> SuccessEvent { get; private set; }
        public EventRecorder<TextEventArgs> DisplayTextEvent { get; private set; }
        public EventRecorder<EventArgs> PlayAudioEvent { get; private set; }
        public EventRecorder<EventArgs> SpeechTriggerEvent { get; private set; }
        public EventRecorder<EventArgs> AudioPromptEvent { get; private set; }
        public EventRecorder<SpeechCaptureEventArgs> SpeechCaptureFinishedEvent { get; private set; }

        #endregion

        /// <summary>
        /// The client's configuration
        /// </summary>
        public ClientConfiguration ClientConfig
        {
            get
            {
                return _clientConfig;
            }
            set
            {
                _clientConfig = value;
            }
        }

        /// <summary>
        /// The client's virtual filesystem
        /// </summary>
        public IFileSystem FileSystem
        {
            get
            {
                return _fileSystem;
            }
            set
            {
                _fileSystem = value;
            }
        }

        /// <summary>
        /// The object which dispatches client actions that come from dialog
        /// </summary>
        public IClientActionDispatcher ActionDispatcher
        {
            get
            {
                return _actionHandler;
            }
            set
            {
                _actionHandler = value;
            }
        }

        public ClientCoreTestWrapper(ILogger logger, bool audioEnabled, IRealTimeProvider realTime, IPublicKeyStore publicKeyStore = null, bool localHttpEnabled = true)
        {
            _audioEnabled = audioEnabled;
            _logger = logger;
            _realTime = realTime;
            _fileSystem = new InMemoryFileSystem();
            _publicKeyStore = publicKeyStore ?? new FileBasedPublicKeyStore(new VirtualPath("public.keys"), _fileSystem, logger.Clone("PublicKeyStore"));
            _serverPrivateKeyStore = new InMemoryPrivateKeyStore();
            _clientPrivateKeyStore = new FileBasedClientKeyStore(_fileSystem, _logger.Clone("PrivateKeyStore"));
            _adhocAuthServer = new AuthHttpServer(
                _logger.Clone("TestAuthServer"),
                null,
                null,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                new ILoginProvider[]
                {
                    AdhocLoginProvider.BuildForServer(_logger.Clone("AdHocLogincProvider"), _serverPrivateKeyStore, _publicKeyStore, null, 512)
                });

            _clientWebServer = localHttpEnabled ? new PresentationWebServer(new NullHttpServer(), _logger.Clone("PresentationWebServer")) : null;
            _microphoneGraph = new AudioGraph(AudioGraphCapabilities.Concurrent);
            _speakersGraph = new AudioGraph(AudioGraphCapabilities.Concurrent);

            ClientConfig = new ClientConfiguration(new InMemoryConfiguration(_logger.Clone("ClientConfig")));
            ClientConfig.ClientId = DialogTestHelpers.TEST_CLIENT_ID;
            ClientConfig.UserId = DialogTestHelpers.TEST_USER_ID;
            ClientConfig.ClientName = "unittestclientname";
            ClientConfig.UserName = "unittestusername";
            ClientConfig.ClientLatitude = 22f;
            ClientConfig.ClientLongitude = -101f;
            ClientConfig.Locale = LanguageCode.EN_US;
           
            if (_audioEnabled)
            {
                ClientConfig.AudioCodec = "opus";
                ClientConfig.MicrophoneSampleRate = 16000;
                ClientConfig.TriggerEnabled = true;
                ClientConfig.TriggerPhrase = "Durandal";
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ClientCoreTestWrapper()
        {
            Dispose(false);
        }
#endif

        public async Task Initialize(IDialogClient dialogConnection)
        {
            _core = new ClientCore();

            ClientCoreParameters clientParams = new ClientCoreParameters(ClientConfig, BuildClientContext)
            {
                DialogConnection = dialogConnection,
                Logger = _logger.Clone("ClientCore"),
                RealTimeProvider = _realTime,
                EnableRSA = true,
                ClientActionDispatcher = _actionHandler,
                PrivateKeyStore =_clientPrivateKeyStore,
                LoginProviders = new List<ILoginProvider>() { AdhocLoginProvider.BuildForClient(new DirectHttpClientFactory(_adhocAuthServer), _logger.Clone("AdhocAuthenticator"), new Uri("https://null")) },
                LocalPresentationLayer = _clientWebServer,
            };

            if (_audioEnabled)
            {
                _speechReco = new FakeSpeechRecognizerFactory(AudioSampleFormat.Mono(16000));
                _microphone = new FakeMultistreamMicrophone(_logger.Clone("FakeMicrophone"), new WeakPointer<IAudioGraph>(_microphoneGraph), AudioSampleFormat.Mono(16000));
                _speakers = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(_speakersGraph), AudioSampleFormat.Mono(16000), "ClientMockSpeaker");
                _utteranceRecorder = new StaticUtteranceRecorder(new WeakPointer<IAudioGraph>(_microphoneGraph), _microphone.OutputFormat, "ClientMockUtteranceRecorder", TimeSpan.FromSeconds(2), _logger.Clone("UtteranceRecorder"));

                if (_clientConfig.TriggerEnabled)
                {
                    _trigger = new FakeAudioTrigger(new WeakPointer<IAudioGraph>(_microphoneGraph), AudioSampleFormat.Mono(16000));
                    KeywordSpottingConfiguration triggerConfig = new KeywordSpottingConfiguration()
                    {
                        PrimaryKeyword = _clientConfig.TriggerPhrase,
                        PrimaryKeywordSensitivity = _clientConfig.PrimaryAudioTriggerSensitivity,
                        SecondaryKeywords = new List<string>(),
                        SecondaryKeywordSensitivity = _clientConfig.SecondaryAudioTriggerSensitivity
                    };
                    _trigger.Configure(triggerConfig);
                }

                clientParams.CodecFactory = new OpusRawCodecFactory(_logger.Clone("OpusCodec"));
                clientParams.AudioTrigger = new WeakPointer<IAudioTrigger>(_trigger);
                clientParams.Microphone = new WeakPointer<IAudioSampleSource>(_microphone);
                clientParams.Speakers = new WeakPointer<IAudioSampleTarget>(_speakers);
                clientParams.SpeechReco = new WeakPointer<ISpeechRecognizerFactory>(_speechReco);
                clientParams.SpeechSynth = default(WeakPointer<ISpeechSynth>);
                clientParams.InputAudioGraph = new WeakPointer<IAudioGraph>(_microphoneGraph);
                clientParams.OutputAudioGraph = new WeakPointer<IAudioGraph>(_speakersGraph);
                clientParams.UtteranceRecorder = new WeakPointer<IUtteranceRecorder>(_utteranceRecorder);
                
                await _microphone.StartCapture(_realTime);
                _speakers.BeginActivelyReading(_logger.Clone("FakeSpeakerReadThread"), _realTime, true);
            }

            SuccessEvent = new EventRecorder<EventArgs>();
            DisplayTextEvent = new EventRecorder<TextEventArgs>();
            PlayAudioEvent = new EventRecorder<EventArgs>();
            SpeechTriggerEvent = new EventRecorder<EventArgs>();
            AudioPromptEvent = new EventRecorder<EventArgs>();
            SpeechCaptureFinishedEvent = new EventRecorder<SpeechCaptureEventArgs>();

            _core.Success.Subscribe(SuccessEvent.HandleEventAsync);
            _core.ShowTextOutput.Subscribe(DisplayTextEvent.HandleEventAsync);
            _core.AudioPlaybackStarted.Subscribe(PlayAudioEvent.HandleEventAsync);
            _core.AudioTriggered.Subscribe(SpeechTriggerEvent.HandleEventAsync);
            _core.SpeechPrompt.Subscribe(AudioPromptEvent.HandleEventAsync);
            _core.SpeechCaptureFinished.Subscribe(SpeechCaptureFinishedEvent.HandleEventAsync);

            await _core.Initialize(clientParams);

            // Authenticate as a user by overriding the adhoc key provider and directing it towards a locally hosted auth server implementation
            UserClientSecretInfo adhocSecretInfo = new UserClientSecretInfo()
            {
                UserId = ClientConfig.UserId,
                UserFullName = ClientConfig.UserName,
                //ClientId = ClientConfig.ClientId,
                //ClientName = ClientConfig.ClientName,
                AuthProvider = "adhoc"
            };

            UserIdentity authenticatedIdentity = await _core.RegisterNewAuthenticatedUser("adhoc", JsonConvert.SerializeObject(adhocSecretInfo), CancellationToken.None, _realTime);
            _core.SetActiveUserIdentity(authenticatedIdentity, _realTime);
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
                _core?.Dispose();
                _speakers?.Dispose();
                _microphone?.Dispose();
                _speechReco?.Dispose();
            }
        }

        private ClientContext BuildClientContext()
        {
            ClientContext returnVal = new ClientContext()
            {
                ClientId = ClientConfig.ClientId,
                ClientName = ClientConfig.ClientName,
                UserId = ClientConfig.UserId,
                Locale = LanguageCode.EN_US,
                Latitude = ClientConfig.ClientLatitude,
                Longitude = ClientConfig.ClientLongitude,
                LocationAccuracy = 10,
                ReferenceDateTime = "2012-10-22T12:00:32",
                UTCOffset = -120
            };

            returnVal.SetCapabilities(ClientCapabilities.DisplayUnlimitedText |
                    ClientCapabilities.DisplayHtml5 |
                    ClientCapabilities.ClientActions |
                    ClientCapabilities.HasInternetConnection |
                    ClientCapabilities.ServeHtml);

            if (_audioEnabled)
            {
                returnVal.AddCapabilities(ClientCapabilities.HasSpeakers |
                    ClientCapabilities.SupportsCompressedAudio);

                if (EnableStreamingAudio)
                {
                    returnVal.AddCapabilities(ClientCapabilities.SupportsStreamingAudio);
                }
            }

            if (_clientWebServer != null)
            {
                returnVal.AddCapabilities(ClientCapabilities.ServeHtml);
            }

            return returnVal;
        }
    }
}
