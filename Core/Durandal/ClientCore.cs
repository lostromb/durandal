namespace Durandal
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Audio.Components;
    using Durandal.Common.Client;
    using Durandal.Common.Client.Actions;
    using Durandal.Common.Collections;
    using Durandal.Common.Dialog;
    using Durandal.Common.Events;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Ontology;
    using Durandal.Common.Security;
    using Durandal.Common.Security.Client;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Speech;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.Triggers;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The primary client class for interfacing with a dialog service. This class abstracts the tasks of handling asynchronous
    /// requests, recording audio, scheduling follow-up prompts, listening for audio triggers, and processing most of
    /// the dialog responses. Users of this class will initialize it with a specific configuration and then generally
    /// call TryMakeTextRequest() or TryMakeAudioRequest(). The client will then fire events that correspond to most
    /// things associated with that request: when it succeeds, when it shows an error, when it needs to play audio, etc.
    /// This class should have exclusive control over an IMicrophone, IAudioPlayer, and if desired, an IPresentationWebServer for
    /// locally hosted HTML.
    /// </summary>
    public sealed class ClientCore : IDisposable
    {
        /// <summary>
        /// The number of ms a request will wait to be honored before giving up
        /// "Request" in this sense usually means that the user presses the microphone button to start a conversation
        /// </summary>
        private const int TRY_REQUEST_TIMEOUT = 500;
   
        /// <summary>
        /// The ms to wait between the audio response being played and the prompt for the next audio query to come up.
        /// This can be overridden by the client response SuggestedRetryDelay
        /// </summary>
        private const int DEFAULT_REPROMPT_DELAY = 1500;

        /// <summary>
        /// The maximum amount of delay (in ms) which the client can wait before starting another audio prompt
        /// (when we are not in tenative multiturn)
        /// </summary>
        private const int MAX_REPROMPT_DELAY = 5000;

        /// <summary>
        /// Number of milliseconds that the user can still trigger a barge-in keyword after the audio has finished playing
        /// </summary>
        private const int BARGE_IN_POSTAUDIO_LENIENCY = 500;

        /// <summary>
        /// When we report the barge-in time on the client request, it's technically supposed to represent when the barge-in phrase _began_, whereas KWS is only triggered after it _ends_.
        /// So, for the time being, we use this number to assume the length of time (in ms) that the user spent saying the keyphrase before the spotter actually registered the trigger.
        /// </summary>
        private const int BARGE_IN_ASSUMED_LENGTH_OF_KEYPHRASE = 500;

        private enum SRChannelStatus
        {
            SR_STATUS_RECORDING,
            SR_STATUS_SUCCESS,
            SR_STATUS_ERROR,
        }

        /// <summary>
        /// The primary configuration object for the client
        /// </summary>
        private ClientConfiguration _clientConfig;

        /// <summary>
        /// The interface to a dialog web service
        /// </summary>
        private IDialogClient _dialogConnection;

        /// <summary>
        /// A local HTTP server for locally-hosted HTML (which is needed for javascript->client connectivity in certain scenarios)
        /// </summary>
        private IClientPresentationLayer _clientPresentationLayer;

        /// <summary>
        /// Used when the client needs to fetch arbitrary HTTP data (for now, this only applies to audio streams)
        /// </summary>
        private IHttpClientFactory _clientHttpFactory;

        /// <summary>
        /// If true, store RSA private keys and try to send a token with each request
        /// </summary>
        private bool _rsaEnabled;
        
        /// <summary>
        /// An authenticator object which stores keys and authenticates outgoing requests
        /// </summary>
        private ClientAuthenticator _authenticator;

        /// <summary>
        /// The primary client logger
        /// </summary>
        private ILogger _logger;

        /// <summary>
        /// An externally-provided method which generates the default client context for outgoing requests
        /// </summary>
        private ClientContextFactory _contextGenerator;
        
        /// <summary>
        /// A state machine representing the current state of the client and its valid transitions
        /// </summary>
        private readonly StateMachine<ClientInteractionState> _clientStateMachine;
        private readonly AsyncLockSlim _clientStateMachineLock;
        private ClientInteractionState _clientInteractionState;

        /// <summary>
        /// The primary background task which processes outgoing requests and handles their responses
        /// </summary>
        private Task _requestWorkerThread;

        private Task _clientActionThread;

        /// <summary>
        /// A signal which indicates the requestWorkerThread has finished
        /// </summary>
        private AutoResetEventAsync _requestWorkerFinished;

        /// <summary>
        /// Interprets and dispatches client action strings which come from the service
        /// </summary>
        private IClientActionDispatcher _clientActionDispatcher;

        private IClientSideKeyStore _clientSideKeyStorage;

        private CancellationTokenSource _clientActionCancelizer = new CancellationTokenSource();

        private IRandom _random = new FastRandom();

        private WeakPointer<IMetricCollector> _metrics;
        private DimensionSet _metricDimensions;

        private int _disposed = 0;

        #region  Asynchronous audio parameters.
        private bool _audioOutputEnabled;
        private bool _audioInputEnabled;
        private bool _audioTriggeringEnabled;
        private WeakPointer<IAudioGraph> _inputAudioGraph;
        private WeakPointer<IAudioGraph> _outputAudioGraph;
        private WeakPointer<IAudioSampleSource> _microphone;
        private WeakPointer<IAudioSampleTarget> _speakers;
        private WeakPointer<ISpeechRecognizerFactory> _speechRecoEngine;
        private WeakPointer<ISpeechSynth> _ttsEngine;
        private WeakPointer<IAudioTrigger> _audioTrigger;
        //private WeakPointer<IVoiceActivityDetector> _vad;
        private WeakPointer<IUtteranceRecorder> _utteranceRecorder;
        private ITriggerArbitrator _triggerArbitrator;
        private IAudioCodecFactory _audioCodecCollection;
        private AudioSplitter _microphoneInputSplitter;
        private BucketAudioSampleTarget _audioRecordingBucket;
        private ISpeechRecognizer _transientSpeechRecognizer;
        private AudioExceptionCircuitBreaker _transientSpeechCircuitBreaker;
        private AudioRequestBundle _transientAudioRequestParams;
        private Stopwatch _audioRecordingStopwatch;
        private string _preferredAudioCodec;
        private LinearMixerAutoConforming _speakerOutputMixer;
        private long _lastAudioPlaybackStartTime;
        private long _lastAudioPlaybackFinishTime;
        private int _currentPrimaryAudioToken;
        private long _stopListeningUntil;
        private bool _sendAudioNextTurn;
        private List<TriggerKeyword> _currentSecondaryKeywords;
        private AudioProcessingQuality _audioQuality;
        #endregion

        /// <summary>
        /// This is the traceid of the current active request, or if none is active, the id to be used for the _next_ request.
        /// It's done this way so that all client logs are attached to the request that follows, which helps a lot to debug
        /// exactly what the client did before the user made a request.
        /// This is reset to a new value after each request finishes.
        /// </summary>
        private Guid? _currentTraceId;

        #region Initialization and disposal

        public ClientCore()
        {
            Success = new AsyncEvent<EventArgs>();
            Skip = new AsyncEvent<EventArgs>();
            Fail = new AsyncEvent<EventArgs>();
            NavigateUrl = new AsyncEvent<UriEventArgs>();
            ShowErrorOutput = new AsyncEvent<TextEventArgs>();
            ShowTextOutput = new AsyncEvent<TextEventArgs>();
            RetryEvent = new AsyncEvent<EventArgs>();
            ResponseReceived = new AsyncEvent<EventArgs>();
            GreetResponseReceived = new AsyncEvent<EventArgs>();
            SpeechPrompt = new AsyncEvent<EventArgs>();
            SpeechCaptureFinished = new AsyncEvent<SpeechCaptureEventArgs>();
            SpeechCaptureIntermediate = new AsyncEvent<TextEventArgs>();
            UpdateQuery = new AsyncEvent<TextEventArgs>();
            InitializationUpdate = new AsyncEvent<TextEventArgs>();
            Initialized = new AsyncEvent<EventArgs>();
            AudioTriggered = new AsyncEvent<EventArgs>();
            SpeechRecoError = new AsyncEvent<EventArgs>();
            AudioPlaybackStarted = new AsyncEvent<EventArgs>();
            AudioPlaybackFinished = new AsyncEvent<EventArgs>();
            Linger = new AsyncEvent<TimeSpanEventArgs>();
            MakingRequest = new AsyncEvent<EventArgs>();
            UserIdentityChanged = new AsyncEvent<UserIdentityChangedEventArgs>();

            IReadOnlyDictionary<ClientInteractionState, ClientInteractionState[]> stateMap = new Dictionary<ClientInteractionState, ClientInteractionState[]>
            {
                { ClientInteractionState.NotStarted, new ClientInteractionState[] { ClientInteractionState.Initializing } },
                { ClientInteractionState.Initializing, new ClientInteractionState[] { ClientInteractionState.WaitingForUserInput } },
                { ClientInteractionState.WaitingForUserInput, new ClientInteractionState[] { ClientInteractionState.RecordingUtterance, ClientInteractionState.MakingRequest } },
                { ClientInteractionState.RecordingUtterance, new ClientInteractionState[] { ClientInteractionState.WaitingForUserInput, ClientInteractionState.MakingRequest } },
                { ClientInteractionState.MakingRequest, new ClientInteractionState[] { ClientInteractionState.PlayingAudio, ClientInteractionState.WaitingForUserInput } },
                { ClientInteractionState.PlayingAudio, new ClientInteractionState[] { ClientInteractionState.DelayBeforePrompt, ClientInteractionState.WaitingForUserInput, ClientInteractionState.RecordingUtterance } },
                { ClientInteractionState.DelayBeforePrompt, new ClientInteractionState[] {ClientInteractionState.RecordingUtterance } }
            };

            _clientInteractionState = ClientInteractionState.NotStarted;
            _clientStateMachine = new StateMachine<ClientInteractionState>(stateMap);
            _clientStateMachineLock = new AsyncLockSlim();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ClientCore()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Initializes this client and establishes a connection with a dialog service
        /// </summary>
        /// <param name="parameters">An object containing all the initialization parameters for the client</param>
        public async Task Initialize(ClientCoreParameters parameters)
        {
            Stopwatch timer = Stopwatch.StartNew();
            IRealTimeProvider realTime = parameters.RealTimeProvider ?? DefaultRealTimeProvider.Singleton;
            _logger = parameters.Logger;
            OnInitializationUpdate("Initializing client", realTime, _logger);
            _clientStateMachine.Transition(ref _clientInteractionState, ClientInteractionState.Initializing);
            _clientConfig = parameters.ClientConfig;
            _microphone = parameters.Microphone;
            _inputAudioGraph = parameters.InputAudioGraph;
            _speakers = parameters.Speakers;
            _outputAudioGraph = parameters.OutputAudioGraph;
            _requestWorkerFinished = new AutoResetEventAsync(true);
            _contextGenerator = parameters.ContextGenerator;
            _dialogConnection = parameters.DialogConnection;
            _rsaEnabled = parameters.EnableRSA;
            _clientPresentationLayer = parameters.LocalPresentationLayer;
            _clientActionDispatcher = parameters.ClientActionDispatcher;
            _clientSideKeyStorage = parameters.PrivateKeyStore;
            _triggerArbitrator = parameters.AudioTriggerArbitrator;
            _clientHttpFactory = parameters.HttpClientFactory;
            _metrics = parameters.Metrics.DefaultIfNull(NullMetricCollector.Singleton);
            _metricDimensions = parameters.MetricDimensions ?? DimensionSet.Empty;
            _preferredAudioCodec = parameters.ClientConfig.AudioCodec;
            if (string.IsNullOrEmpty(_preferredAudioCodec))
            {
                _preferredAudioCodec = RawPcmCodecFactory.CODEC_NAME_PCM_S16LE;
            }

            ResetTraceId();

            _audioOutputEnabled = _outputAudioGraph.Value != null;
            if (_speakers.Value != null ^ _outputAudioGraph.Value != null)
            {
                throw new ArgumentException("Audio output requires both speakers and output audio graph to be non-null");
            }

            _audioInputEnabled = _inputAudioGraph.Value != null;
            if (_microphone.Value != null ^ _inputAudioGraph.Value != null)
            {
                throw new ArgumentException("Audio input requires both microphone and input audio graph to be non-null");
            }

            if (_dialogConnection == null)
            {
                throw new ArgumentException("No DialogClient was passed to client core");
            }

            if (_audioInputEnabled || _audioOutputEnabled)
            {
                _audioQuality = AudioHelpers.GetAudioQualityBasedOnMachinePerformance();
                if (parameters.CodecFactory == null)
                {
                    _logger.Log("No audio codec supplied; using PCM", LogLevel.Wrn);
                    _audioCodecCollection = new RawPcmCodecFactory();
                }
                else if (!parameters.CodecFactory.CanEncode(RawPcmCodecFactory.CODEC_NAME_PCM_S16LE))
                {
                    // Add PCM support if not already included in the codec package 
                    _audioCodecCollection = new AggregateCodecFactory(parameters.CodecFactory, new RawPcmCodecFactory());
                }
                else
                {
                    _audioCodecCollection = parameters.CodecFactory;
                }

                _audioRecordingBucket = new BucketAudioSampleTarget(_inputAudioGraph, _microphone.Value.OutputFormat, "ClientAudioRecordingBucket");
            }

            if (_audioInputEnabled)
            {
                OnInitializationUpdate("Configuring audio input", realTime, _logger);
                _audioRecordingStopwatch = new Stopwatch();

                if (parameters.SpeechReco.Value == null)
                {
                    _logger.Log("Using null speech reco", LogLevel.Wrn);
                    parameters.SpeechReco = new WeakPointer<ISpeechRecognizerFactory>(NullSpeechRecoFactory.Singleton);
                }

                _microphoneInputSplitter = new AudioSplitter(_inputAudioGraph, _microphone.Value.OutputFormat, "MicrophoneInputSplitter");
                _microphoneInputSplitter.ConnectInput(_microphone.Value);
                _speechRecoEngine = parameters.SpeechReco;
                _ttsEngine = parameters.SpeechSynth;
                _audioTrigger = parameters.AudioTrigger;
                _utteranceRecorder = parameters.UtteranceRecorder;

                if (_utteranceRecorder.Value == null)
                {
                    _logger.Log("Utterance recorder is null. Will use the default implementation", LogLevel.Wrn);
                    _utteranceRecorder = new WeakPointer<IUtteranceRecorder>(
                        new DynamicUtteranceRecorder(
                            _inputAudioGraph,
                            _microphone.Value.OutputFormat,
                            "ClientUtteranceRecorder",
                            _logger.Clone("UtteranceRecorder")));
                }

                _utteranceRecorder.Value.UtteranceFinishedEvent.Subscribe(HandleRecordingFinishedEvent);

                if (_audioTrigger.Value != null)
                {
                    _audioTriggeringEnabled = true;
                    _audioTrigger.Value.TriggeredEvent.Subscribe(AudioTriggerFired);
                }
            }

            if (_audioOutputEnabled)
            {
                OnInitializationUpdate("Configuring audio output", realTime, _logger);
                _speakerOutputMixer = new LinearMixerAutoConforming(
                    _outputAudioGraph,
                    _speakers.Value.InputFormat,
                    "SpeakerOutputMixer",
                    true,
                    _logger.Clone("ClientOutputMixer"),
                    _metrics,
                    _metricDimensions);
                _speakerOutputMixer.ChannelFinishedEvent.Subscribe(SpeakerChannelFinished);
                _speakerOutputMixer.ConnectOutput(_speakers.Value);
            }

            if (_rsaEnabled && _clientSideKeyStorage != null)
            {
                _authenticator = await ClientAuthenticator.Create(
                    _logger.Clone("ClientAuthenticator"),
                    new StandardRSADelegates(),
                    _clientSideKeyStorage,
                    parameters.LoginProviders).ConfigureAwait(false);
            }

            if (_clientPresentationLayer != null)
            {
                OnInitializationUpdate("Starting local presentation layer", realTime, _logger);
                _clientPresentationLayer.Initialize(
                    _dialogConnection,
                    _contextGenerator,
                    _authenticator,
                    parameters.LocalHtmlRenderer);
                await _clientPresentationLayer.Start(CancellationToken.None, realTime).ConfigureAwait(false);
            }

            await _requestWorkerFinished.WaitAsync().ConfigureAwait(false);
            try
            {
                await DoInitialHandshake(_logger, realTime).ConfigureAwait(false);
                OnInitializationUpdate("Initialization Finished", realTime, _logger);
                OnInitialized(realTime, _logger);
            }
            finally
            {
                _requestWorkerThread = null;
                _requestWorkerFinished.Set();
            }

            _clientStateMachine.Transition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);

            timer.Stop();
            _logger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_Initialize, timer), LogLevel.Ins);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                if (_clientPresentationLayer != null)
                {
                    _clientPresentationLayer.Stop(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                }

                _clientActionCancelizer?.Dispose();
                _audioRecordingBucket?.Dispose();
                _microphoneInputSplitter?.Dispose();
                _speakerOutputMixer?.Dispose();
                _transientSpeechCircuitBreaker?.Dispose();
                _transientSpeechRecognizer?.Dispose();
                _clientStateMachineLock?.Dispose();
            }
        }

        private void ResetTraceId()
        {
            Guid nextTraceid = Guid.NewGuid();
            _logger.Log("Client switching to new trace ID " + CommonInstrumentation.FormatTraceId(nextTraceid), LogLevel.Vrb);
            _currentTraceId = nextTraceid;
            _logger = _logger.CreateTraceLogger(_currentTraceId);

            if (_clientPresentationLayer != null)
            {
                _clientPresentationLayer.UpdateNextTurnTraceId(_currentTraceId);
            }
        }

        private void AddClientJavascriptDataToRequest(DialogRequest request)
        {
            if (_clientPresentationLayer == null)
            {
                return;
            }

            IDictionary<string, string> clientData = _clientPresentationLayer.GetClientJavascriptData();

            if (clientData == null)
            {
                return;
            }

            foreach (var kvp in clientData)
            {
                if (request.RequestData.ContainsKey(kvp.Key))
                {
                    _logger.Log("Client javascript set a value for " + kvp.Key + " but that field was already set in the request data!", LogLevel.Wrn);
                }

                request.RequestData[kvp.Key] = kvp.Value;
            }
        }

        private void CancelAnyCurrentClientActions()
        {
            if (_clientActionCancelizer != null)
            {
                if (!_clientActionCancelizer.IsCancellationRequested)
                {
                    _clientActionCancelizer.Cancel();
                }

                _clientActionCancelizer?.Dispose();
            }

            _clientActionCancelizer = new CancellationTokenSource();
        }

#endregion

        #region Authentication and identity management

        /// <summary>
        /// Lists all user identities currently registered with this service
        /// </summary>
        /// <returns></returns>
        public IList<UserIdentity> GetAvailableUserIdentities()
        {
            return _authenticator.GetAvailableUserIdentities();
        }

        /// <summary>
        /// Lists all user identities currently registered with this service
        /// </summary>
        /// <returns></returns>
        public IList<ClientIdentity> GetAvailableClientIdentities()
        {
            return _authenticator.GetAvailableClientIdentities();
        }

        /// <summary>
        /// Tells the client to switch to the specified user identity for subsequent requests.
        /// Note that the client retains the power to alter the active user identity at any time, generally
        /// in response to things like face or voice print recognition which identifies a specific user
        /// </summary>
        /// <param name="newIdent">The user identity to use</param>
        /// <param name="realTime">wallclock time</param>
        public void SetActiveUserIdentity(UserIdentity newIdent, IRealTimeProvider realTime)
        {
            if (string.Equals(newIdent.Id, _clientConfig.UserId))
            {
                return;
            }

            _logger.Log("Changing user identity to " + newIdent.Id + " (was " + _clientConfig.UserId + ")");
            _clientConfig.UserId = newIdent.Id;
            _clientConfig.UserName = newIdent.FullName;
            OnUserIdentityChanged(newIdent, realTime, _logger);
        }

        /// <summary>
        /// Logs out a specified user, removing their context and associated private keys from the device.
        /// Note that this method does not change the active user identity, so it would be a very good idea to SetActiveUserIdentity() to
        /// a new identity after calling this. If you don't, things won't break, you'll just continue using the previous identity 
        /// without authentication.
        /// </summary>
        /// <param name="userId"></param>
        public async Task LogOutUser(string userId)
        {
            _logger.Log("Logging out user " + userId);
            await _authenticator.LogOutUser(userId).ConfigureAwait(false);
        }

        /// <summary>
        /// Provides credentials to this client object that will be used to register a new user account.
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="externalToken"></param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time (mostly for testing)</param>
        /// <returns></returns>
        public Task<UserIdentity> RegisterNewAuthenticatedUser(string providerName, string externalToken, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _authenticator.RegisterNewAuthenticatedUser(providerName, externalToken, cancelToken, realTime);
        }

        /// <summary>
        /// Provides credentials to this client which will be retrieved from the given auth provider (though that provider will be "adhoc" for all currently conceivable cases)
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="externalToken"></param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time (mostly for testing)</param>
        /// <returns></returns>
        public Task<ClientIdentity> RegisterAuthenticatedClient(string providerName, string externalToken, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _authenticator.RegisterAuthenticatedClient(providerName, externalToken, cancelToken, realTime);
        }

        #endregion

        #region Text request handler

        /// <summary>
        /// Attempts to make a text request asynchronously. If initiating the request is successful,
        /// this method returns true and the request begins executing in the background.
        /// Otherwise (if there is already a request in progress), this return false.
        /// </summary>
        /// <param name="queryText">The text query to be sent</param>
        /// <param name="context">The client context to use. If null, the value of the default ContextGenerator() will be used</param>
        /// <param name="flags">Flags to set on the request</param>
        /// <param name="inputEntities">Ontological entity references to include</param>
        /// <param name="entityContext">Context which stores input entities</param>
        /// <param name="realTime">Definition of real time</param>
        /// <returns>True if we were able to initiate a text request</returns>
        public Task<bool> TryMakeTextRequest(
            string queryText,
            ClientContext context = null,
            QueryFlags flags = QueryFlags.None,
            IList<ContextualEntity> inputEntities = null,
            KnowledgeContext entityContext = null,
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            return TryMakeTextRequest(
                queryText,
                InputMethod.Typed,
                realTime,
                context: context,
                flags: flags,
                clientAudioPlaybackTimeMs: null,
                inputEntities: inputEntities,
                entityContext: entityContext);
        }

        /// <summary>
        /// Listens asynchronously for the _requestWorkerFinished signal. Returns true if the signal is already set or becomes
        /// set within TRY_REQUEST_TIMEOUT, according to the IRealTimeProvider associated with this object.
        /// </summary>
        /// <returns>True if the caller has been granted the right to update the background requestWorkerThread</returns>
        private async Task<bool> WaitForRequestLockToBecomeAvailable(IRealTimeProvider realTime)
        {
            // Convert real time to "simulated" real time as it pertains to waiting for the lock.
            // This loop will break when either the lock is obtained or TRY_REQUEST_TIMEOUT has passed
            // according to the simulated real time, not actual wallclock time
            // (this difference is mostly relevant in unit tests that have full control over the temporal continuum)
            //Task workerTask = _requestWorkerThread;
            //if (workerTask == null)
            //{
            //    return true;
            //}

            //Task delayTask = realTime.WaitAsync(TimeSpan.FromMilliseconds(TRY_REQUEST_TIMEOUT), CancellationToken.None);
            //Task winningTask = await Task.WhenAny(delayTask, workerTask);
            //return winningTask == workerTask;

            using (CancellationTokenSource cancelSource = new NonRealTimeCancellationTokenSource(realTime, TimeSpan.FromMilliseconds(TRY_REQUEST_TIMEOUT)))
            {
                try
                {
                    Task waitTask = _requestWorkerFinished.WaitAsync(cancelSource.Token);
                    await waitTask.ConfigureAwait(false);
                    return !waitTask.IsCanceled;
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Attempts to make a text request asynchronously. If initiating the request is successful,
        /// this method returns true and the request begins executing in the background.
        /// Otherwise (if there is already a request in progress), this return false.
        /// 
        /// This method is also used for barge-on keywords which is why it has audio parameters.
        /// </summary>
        /// <param name="queryText">The text query to be sent</param>
        /// <param name="inputMethod">The input method to be used</param>
        /// <param name="realTime"></param>
        /// <param name="context">The client context to use. If null, the value of the default ContextGenerator() will be used</param>
        /// <param name="flags"></param>
        /// <param name="clientAudioPlaybackTimeMs">The amount of audio that has played on the client</param>
        /// <param name="inputEntities"></param>
        /// <param name="entityContext"></param>
        /// <returns>True if we were able to initiate a text request</returns>
        private async Task<bool> TryMakeTextRequest(
            string queryText,
            InputMethod inputMethod,
            IRealTimeProvider realTime,
            ClientContext context = null,
            QueryFlags flags = QueryFlags.None,
            int? clientAudioPlaybackTimeMs = null,
            IList<ContextualEntity> inputEntities = null,
            KnowledgeContext entityContext = null)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(ClientCore), "Client has been disposed");
            }

            _logger.Log("TryMakeTextRequest() called");
            
            if (await WaitForRequestLockToBecomeAvailable(realTime).ConfigureAwait(false))
            {
                await _clientStateMachineLock.GetLockAsync().ConfigureAwait(false);
                try
                {
                    if (_clientInteractionState != ClientInteractionState.WaitingForUserInput && _clientInteractionState != ClientInteractionState.PlayingAudio)
                    {
                        _logger.Log("Client core is in a state \"" + _clientInteractionState.ToString() + "\" which does not allow text requests");
                        return false;
                    }

                    CancelAnyCurrentClientActions();

                    if (context == null)
                    {
                        context = _contextGenerator();
                    }

                    if (_clientActionDispatcher != null)
                    {
                        context.SupportedClientActions = _clientActionDispatcher.GetSupportedClientActions();
                    }

                    // Make the client capabilities consistent with what is actually running right now
                    if (_clientPresentationLayer == null)
                    {
                        context.RemoveCapabilities(ClientCapabilities.ServeHtml);
                    }

                    if (!_audioOutputEnabled)
                    {
                        context.RemoveCapabilities(ClientCapabilities.HasSpeakers |
                            ClientCapabilities.SupportsCompressedAudio |
                            ClientCapabilities.SupportsStreamingAudio |
                            ClientCapabilities.CanSynthesizeSpeech);
                    }

                    if (!_audioInputEnabled)
                    {
                        context.RemoveCapabilities(ClientCapabilities.HasMicrophone);
                    }

                    // Queue the request on a new thread
                    _clientStateMachine.Transition(ref _clientInteractionState, ClientInteractionState.MakingRequest);
                    IRealTimeProvider requestTimeProvider = realTime.Fork("ClientTextRequestThread");
                    _requestWorkerThread = Task.Run(async () =>
                    {
                        try
                        {
                            // We need to clone the logger so we have a non-volatile copy local to this thread
                            ILogger queryLogger = _logger.Clone();
                            queryLogger.Log("Text request trace ID is " + CommonInstrumentation.FormatTraceId(queryLogger.TraceId));
                            RequestResponsePair result = await TextRequestInternal(queryText, inputMethod, context, queryLogger, flags, clientAudioPlaybackTimeMs, inputEntities, entityContext, requestTimeProvider).ConfigureAwait(false);
                            await HandleResult(result, queryLogger, requestTimeProvider, null, CancellationToken.None).ConfigureAwait(false);
                        }
                        finally
                        {
                            ResetTraceId();

                            // Self-destruct the thread
                            _clientStateMachine.Transition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);
                            _requestWorkerThread = null;
                            _requestWorkerFinished.Set();
                            requestTimeProvider.Merge();
                        }
                    });

                    return true;
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                    _clientStateMachine.ForceTransition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);
                    _requestWorkerThread = null;
                    _requestWorkerFinished.Set();
                }
                finally
                {
                    _clientStateMachineLock.Release();
                }
            }

            return false;
        }

        private async Task<RequestResponsePair> TextRequestInternal(
            string queryText,
            InputMethod inputMethod,
            ClientContext context,
            ILogger queryLogger,
            QueryFlags flags,
            int? clientAudioPlaybackTimeMs,
            IList<ContextualEntity> inputEntities,
            KnowledgeContext entityContext,
            IRealTimeProvider realTime)
        {
            OnMakingRequest(realTime, queryLogger);

            RequestResponsePair returnVal = new RequestResponsePair()
            {
                Request = null,
                Response = null
            };

            try
            {
                returnVal.Request = new DialogRequest()
                    {
                        InteractionType = inputMethod,
                        TextInput = queryText,
                        ClientAudioPlaybackTimeMs = clientAudioPlaybackTimeMs,
                        ClientContext = context,
                        TraceId = (queryLogger == null || !queryLogger.TraceId.HasValue) ? string.Empty : CommonInstrumentation.FormatTraceId(queryLogger.TraceId.Value),
                        RequestFlags = flags,
                        PreferredAudioCodec = _preferredAudioCodec ?? string.Empty,
                        PreferredAudioFormat = !_audioOutputEnabled ? string.Empty : CommonCodecParamHelper.CreateCodecParams(_speakers.Value.InputFormat),
                    };

                // Get client JS parameters as well
                AddClientJavascriptDataToRequest(returnVal.Request);

                if (inputEntities != null && inputEntities.Count > 0 && entityContext != null && !entityContext.IsEmpty)
                {
                    // Add entities and serialized context to input
                    using (PooledBuffer<byte> serializedEntityContext = KnowledgeContextSerializer.SerializeKnowledgeContext(entityContext))
                    {
                        byte[] copiedEntityContext = new byte[serializedEntityContext.Length];
                        ArrayExtensions.MemCopy(serializedEntityContext.Buffer, 0, copiedEntityContext, 0, copiedEntityContext.Length);
                        returnVal.Request.EntityContext = new ArraySegment<byte>(copiedEntityContext);
                        returnVal.Request.EntityInput = new List<EntityReference>();
                        foreach (ContextualEntity inputEntity in inputEntities)
                        {
                            returnVal.Request.EntityInput.Add(new EntityReference()
                            {
                                EntityId = inputEntity.Entity.EntityId,
                                Relevance = inputEntity.Relevance
                            });
                        }
                    }
                }

                // Add authentication info
                if (_authenticator != null)
                {
                    Stopwatch authTimer = Stopwatch.StartNew();
                    queryLogger.Log("Adding authentication tokens to text request", LogLevel.Vrb);
                    await _authenticator.AuthenticateClientRequest(returnVal.Request, queryLogger, realTime).ConfigureAwait(false);
                    authTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_GenerateRequestToken, authTimer), LogLevel.Ins);
                }

#pragma warning disable CA2000 // Dispose objects before losing scope
                queryLogger.Log("Starting dialog text query request", LogLevel.Vrb);
                NetworkResponseInstrumented<DialogResponse> clientResponse = await _dialogConnection.MakeQueryRequest(returnVal.Request, queryLogger, CancellationToken.None, realTime).ConfigureAwait(false);
                queryLogger.Log("Finished dialog text query request", LogLevel.Vrb);

                if (clientResponse == null)
                {
                    queryLogger.Log("The network response from dialog engine was null", LogLevel.Err);
                    return returnVal;
                }

                queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_Request, clientResponse.RequestSize), LogLevel.Ins);
                queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_Response, clientResponse.ResponseSize), LogLevel.Ins);
                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_E2E, clientResponse.EndToEndLatency), LogLevel.Ins);

                if (clientResponse.Response == null)
                {
                    queryLogger.Log("No dialog response was received", LogLevel.Err);
                    return returnVal;
                }

                returnVal.Response = clientResponse.Response;
                return returnVal;
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            catch (Exception e)
            {
                queryLogger.Log("Unhandled exception in client during text query", LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
                returnVal.Request = null;
                returnVal.Response = new DialogResponse();
                return returnVal;
            }
        }

#endregion

        #region Dialog Action request handler
        
        /// <summary>
        /// Attempts to initiate a dialog action request asynchronously. If the client is currently available (i.e. not already making
        /// another request), this method will return TRUE and the request will be sent. Otherwise, this method will return FALSE and no
        /// side effects will occur.
        /// </summary>
        /// <param name="actionId">The plain string ID of the dialog action to invoke</param>
        /// <param name="inputMethod">???</param>
        /// <param name="context">Custom client context to use. If null, the default client context generator will be invoked.</param>
        /// <param name="flags">Flags to set on the dialog query</param>
        /// <param name="inputEntities">An optional set of relational entities to pass to the dialog engine.</param>
        /// <param name="entityContext">An optional context for holding relational entities</param>
        /// <param name="realTime">A definition of real time, mostly for unit testing.</param>
        /// <param name="requestData">An optional dictionary of key-value pairs to send to the dialog engine.
        /// This can be used for advanced scenarios such as client-side resolution and delayed programmatic client actions.</param>
        /// <returns>An async task which returns a boolean indicating whether the request was honored.</returns>
        public async Task<bool> TryMakeDialogActionRequest(
            string actionId,
            InputMethod inputMethod,
            ClientContext context = null,
            QueryFlags flags = QueryFlags.None,
            IList<ContextualEntity> inputEntities = null,
            KnowledgeContext entityContext = null,
            IRealTimeProvider realTime = null,
            Dictionary<string, string> requestData = null)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(ClientCore), "Client has been disposed");
            }

            _logger.Log("TryMakeDialogActionRequest() called");

            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            if (await WaitForRequestLockToBecomeAvailable(realTime).ConfigureAwait(false))
            {
                await _clientStateMachineLock.GetLockAsync().ConfigureAwait(false);
                try
                {
                    if (_clientInteractionState != ClientInteractionState.WaitingForUserInput && _clientInteractionState != ClientInteractionState.PlayingAudio)
                    {
                        _logger.Log("Client core is in a state \"" + _clientInteractionState.ToString() + "\" which does not allow action requests");
                        return false;
                    }

                    CancelAnyCurrentClientActions();

                    if (context == null)
                    {
                        context = _contextGenerator();
                    }

                    if (_clientActionDispatcher != null)
                    {
                        context.SupportedClientActions = _clientActionDispatcher.GetSupportedClientActions();
                    }

                    // Make the client capabilities consistent with what is actually running right now
                    if (_clientPresentationLayer == null)
                    {
                        context.RemoveCapabilities(ClientCapabilities.ServeHtml);
                    }
                    if (!_audioOutputEnabled || (inputMethod != InputMethod.Spoken && inputMethod != InputMethod.TactileWithAudio))
                    {
                        // Disable audio playback unless the dialog action specifically says it should be spoken
                        context.RemoveCapabilities(ClientCapabilities.HasSpeakers |
                            ClientCapabilities.SupportsCompressedAudio |
                            ClientCapabilities.SupportsStreamingAudio |
                            ClientCapabilities.CanSynthesizeSpeech);
                    }

                    if (!_audioInputEnabled)
                    {
                        context.RemoveCapabilities(ClientCapabilities.HasMicrophone);
                    }

                    _clientStateMachine.Transition(ref _clientInteractionState, ClientInteractionState.MakingRequest);

                    // Queue the request on a new thread
                    IRealTimeProvider requestTimeProvider = realTime.Fork("ClientDialogActionRequestThread");
                    _requestWorkerThread = Task.Run(async () =>
                    {
                        try
                        {
                            // We need to clone the logger so we have a non-volatile copy local to this thread
                            ILogger queryLogger = _logger.Clone();
                            queryLogger.Log("Dialog action trace ID is " + CommonInstrumentation.FormatTraceId(queryLogger.TraceId));
                            RequestResponsePair result = await DialogActionRequestInternal(
                                actionId,
                                inputMethod,
                                context,
                                queryLogger,
                                flags,
                                inputEntities,
                                entityContext,
                                requestTimeProvider,
                                requestData).ConfigureAwait(false);
                            await HandleResult(result, queryLogger, requestTimeProvider, null, CancellationToken.None).ConfigureAwait(false);
                        }
                        finally
                        {
                            ResetTraceId();

                            // Self-destruct the thread
                            _requestWorkerThread = null;
                            _requestWorkerFinished.Set();
                            requestTimeProvider.Merge();
                            _clientStateMachine.Transition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);
                        }
                    });

                    return true;
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                    _clientStateMachine.ForceTransition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);
                    _requestWorkerThread = null;
                    _requestWorkerFinished.Set();
                }
                finally
                {
                    _clientStateMachineLock.Release();
                }
            }

            return false;
        }

        private async Task<RequestResponsePair> DialogActionRequestInternal(
            string actionId,
            InputMethod inputMethod,
            ClientContext context,
            ILogger queryLogger,
            QueryFlags flags,
            IList<ContextualEntity> inputEntities,
            KnowledgeContext entityContext,
            IRealTimeProvider realTime,
            Dictionary<string, string> requestData)
        {
            RequestResponsePair returnVal = new RequestResponsePair()
            {
                Request = null,
                Response = null
            };

            try
            {
                returnVal.Request = new DialogRequest()
                {
                    InteractionType = inputMethod,
                    TraceId = (queryLogger == null || !queryLogger.TraceId.HasValue) ? string.Empty : CommonInstrumentation.FormatTraceId(queryLogger.TraceId.Value),
                    RequestFlags = flags,
                    PreferredAudioCodec = _preferredAudioCodec ?? string.Empty,
                    RequestData = requestData,
                    PreferredAudioFormat = !_audioOutputEnabled ? string.Empty : CommonCodecParamHelper.CreateCodecParams(_speakers.Value.InputFormat),
                };

                returnVal.Request.ClientContext = context;

                if (inputEntities != null && inputEntities.Count > 0 && entityContext != null && !entityContext.IsEmpty)
                {
                    // Add entities and serialized context to input
                    using (PooledBuffer<byte> serializedEntityContext = KnowledgeContextSerializer.SerializeKnowledgeContext(entityContext))
                    {
                        byte[] copiedEntityContext = new byte[serializedEntityContext.Length];
                        ArrayExtensions.MemCopy(serializedEntityContext.Buffer, 0, copiedEntityContext, 0, copiedEntityContext.Length);
                        returnVal.Request.EntityContext = new ArraySegment<byte>(copiedEntityContext);
                        returnVal.Request.EntityInput = new List<EntityReference>();
                        foreach (ContextualEntity inputEntity in inputEntities)
                        {
                            returnVal.Request.EntityInput.Add(new EntityReference()
                            {
                                EntityId = inputEntity.Entity.EntityId,
                                Relevance = inputEntity.Relevance
                            });
                        }
                    }
                }

                if (_authenticator != null)
                {
                    Stopwatch authTimer = Stopwatch.StartNew();
                    await _authenticator.AuthenticateClientRequest(returnVal.Request, queryLogger, realTime).ConfigureAwait(false);
                    authTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_GenerateRequestToken, authTimer), LogLevel.Ins);
                }

#pragma warning disable CA2000 // Dispose objects before losing scope
                queryLogger.Log("Starting dialog action request", LogLevel.Vrb);
                NetworkResponseInstrumented<DialogResponse> clientResponse = await _dialogConnection.MakeDialogActionRequest(returnVal.Request, actionId, queryLogger, CancellationToken.None, realTime).ConfigureAwait(false);
                queryLogger.Log("Finished dialog action request", LogLevel.Vrb);

                if (clientResponse == null)
                {
                    queryLogger.Log("The network response from dialog engine was null", LogLevel.Err);
                    return returnVal;
                }

                queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_Request, clientResponse.RequestSize), LogLevel.Ins);
                queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_Response, clientResponse.ResponseSize), LogLevel.Ins);
                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_E2E, clientResponse.EndToEndLatency), LogLevel.Ins);

                if (clientResponse.Response == null)
                {
                    queryLogger.Log("No dialog response was received", LogLevel.Err);
                    return returnVal;
                }

                returnVal.Response = clientResponse.Response;
                return returnVal;
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            catch (Exception e)
            {
                queryLogger.Log("Unhandled exception in client during dialog action query", LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
                returnVal.Request = null;
                returnVal.Response = new DialogResponse();
                return returnVal;
            }
        }

#endregion

        #region Response handler

        private async Task HandleResult(RequestResponsePair results, ILogger queryLogger, IRealTimeProvider realTime, Stopwatch audioUplStopwatch, CancellationToken cancelToken)
        {
            DialogResponse durandalResult = results.Response;
            DialogRequest originalRequest = results.Request;

            if (durandalResult == null)
            {
                // Could not connect
                OnFail(realTime, queryLogger);
                return;
            }

            if (originalRequest.LanguageUnderstanding == null ||
                originalRequest.LanguageUnderstanding.Count == 0 ||
                originalRequest.LanguageUnderstanding[0].Recognition == null ||
                originalRequest.LanguageUnderstanding[0].Recognition.Count == 0 ||
                !originalRequest.LanguageUnderstanding[0].Recognition[0].Domain.Equals(DialogConstants.REFLECTION_DOMAIN) ||
                !originalRequest.LanguageUnderstanding[0].Recognition[0].Intent.Equals("greet"))
            {
                OnResponseReceived(realTime, queryLogger);
            }
            else
            {
                OnGreetResponseReceived(realTime, queryLogger);
            }

            if (durandalResult.ExecutionResult == Result.Success && durandalResult.IsRetrying)
            {
                _lastAudioPlaybackStartTime = realTime.TimestampMilliseconds;
                _lastAudioPlaybackFinishTime = _lastAudioPlaybackStartTime;

                // Special handler for the "retry" scenario.
                // Since we don't want to clear the canvas (we just want clarification from the user regarding what
                // is currently on the screen), all we handle on this path is TTS and text output
                //IAudioSampleSource audioOut = await GenerateFinalResponseAudio(durandalResult, queryLogger, audioUplStopwatch).ConfigureAwait(false);
                
                // Queue up the reprompt
                if (originalRequest.InteractionType == InputMethod.Spoken && durandalResult.ContinueImmediately)
                {
                    EnqueueAudioPromptIn(queryLogger, realTime, durandalResult.SuggestedRetryDelay.GetValueOrDefault(-1));
                }

                // Process streaming audio
                IAudioSampleSource streamingAudio = null;
                if (_audioOutputEnabled && !string.IsNullOrEmpty(durandalResult.StreamingAudioUrl))
                {
#pragma warning disable CA2000 // Dispose objects before losing scope
                    streamingAudio = await StartStreamingAudio(durandalResult.StreamingAudioUrl, queryLogger, audioUplStopwatch, cancelToken, realTime).ConfigureAwait(false);
#pragma warning restore CA2000 // Dispose objects before losing scope
                    if (streamingAudio != null && !streamingAudio.PlaybackFinished)
                    {
                        // Just use a really large value here because the actual finish time will be corrected as soon as the audio stream finishes playing
                        _lastAudioPlaybackFinishTime += 3000000;
                        OnAudioPlaybackStarted(realTime, queryLogger);
                        // Set a token to identify the audio
                        _currentPrimaryAudioToken = _random.NextInt();
                        _speakerOutputMixer.AddInput(streamingAudio, _currentPrimaryAudioToken, true);
                    }
                }

                OnRetryEvent(realTime, queryLogger);
            }
            else if (durandalResult.ExecutionResult == Result.Success)
            {
                OnSuccess(realTime, queryLogger);
                if (durandalResult.ConversationLifetimeSeconds.HasValue &&
                    durandalResult.ConversationLifetimeSeconds.Value > 0)
                {
                    OnLinger(TimeSpan.FromSeconds(durandalResult.ConversationLifetimeSeconds.Value), realTime, queryLogger);
                }

                // Set default trigger times in the case that there is no response audio
                _lastAudioPlaybackStartTime = realTime.TimestampMilliseconds;
                _lastAudioPlaybackFinishTime = _lastAudioPlaybackStartTime;

                // Display the returned text
                if (!string.IsNullOrWhiteSpace(durandalResult.ResponseText))
                {
                    OnShowTextOutput(durandalResult.ResponseText, realTime, queryLogger);
                }

                // Process all audio output
                if (_audioOutputEnabled)
                {
                    // Process streaming audio (this will augment the prompt timings on its own so it's by design that this comes after EnqueueAudioPrompt)
                    if (!string.IsNullOrEmpty(durandalResult.StreamingAudioUrl))
                    {
#pragma warning disable CA2000 // Dispose objects before losing scope
                        IAudioSampleSource stream = await StartStreamingAudio(durandalResult.StreamingAudioUrl, queryLogger, audioUplStopwatch, cancelToken, realTime).ConfigureAwait(false);
#pragma warning restore CA2000 // Dispose objects before losing scope

                        if (stream != null && !stream.PlaybackFinished)
                        {
                            // Just use a really large value here because the actual finish time will be corrected as soon as the audio stream finished playing
                            _lastAudioPlaybackFinishTime += 3000000;
                            OnAudioPlaybackStarted(realTime, queryLogger);
                            // Set a token to identify the audio
                            _currentPrimaryAudioToken = _random.NextInt();
                            _speakerOutputMixer.AddInput(stream, _currentPrimaryAudioToken, takeOwnership: true);
                        }
                    }
                    else if (!string.IsNullOrEmpty(durandalResult.ResponseSsml) ||
                        (durandalResult.ResponseAudio != null && durandalResult.ResponseAudio.Data.Array != null && durandalResult.ResponseAudio.Data.Count > 0))
                    {
                        IAudioSampleSource outputAudio = await GenerateFinalResponseAudio(
                            durandalResult.ResponseAudio,
                            durandalResult.ResponseSsml,
                            durandalResult.CustomAudioOrdering,
                            queryLogger,
                            //audioUplStopwatch,
                            _audioCodecCollection,
                            _speakers.Value.InputFormat,
                            _outputAudioGraph,
                            _ttsEngine.Value,
                            _clientConfig.Locale,
                            _audioQuality,
                            cancelToken,
                            realTime).ConfigureAwait(false);

                        OnAudioPlaybackStarted(realTime, queryLogger);

                        // Just use a really large value here because the actual finish time will be corrected as soon as the audio stream finishes playing
                        _lastAudioPlaybackFinishTime += 3000000;

                        // Set a token to identify the audio (so we can track when playback ends)
                        _currentPrimaryAudioToken = _random.NextInt();
                        _speakerOutputMixer.AddInput(outputAudio, _currentPrimaryAudioToken, takeOwnership: true);
                    }
                }

                if (_clientPresentationLayer != null)
                {
                    Uri url = await _clientPresentationLayer.GeneratePresentationUrlFromResponse(durandalResult, realTime).ConfigureAwait(false);
                    if (url != null)
                    {
                        OnNavigateUrl(url, realTime, queryLogger);
                    }
                }
                else if (!string.IsNullOrEmpty(durandalResult.ResponseUrl))
                {
                    OnNavigateUrl(new Uri(_dialogConnection.GetConnectionString() + durandalResult.ResponseUrl), realTime, queryLogger);
                }

                if (!string.IsNullOrEmpty(durandalResult.AugmentedFinalQuery))
                {
                    OnUpdateQuery(durandalResult.AugmentedFinalQuery, realTime, queryLogger);
                }

                // Queue up the next turn's audio prompt, if necessary
                if (originalRequest.InteractionType == InputMethod.Spoken && durandalResult.ContinueImmediately)
                {
                    EnqueueAudioPromptIn(queryLogger, realTime, durandalResult.SuggestedRetryDelay.GetValueOrDefault(-1));
                }

                if (_audioTriggeringEnabled && durandalResult.TriggerKeywords != null && durandalResult.TriggerKeywords.Count > 0)
                {
                    queryLogger.Log("Reconfiguring audio trigger to temporarily accept " + durandalResult.TriggerKeywords.Count + " instant keywords");

                    List<string> secondaryKeywords = new List<string>();
                    foreach (TriggerKeyword keyword in durandalResult.TriggerKeywords)
                    {
                        secondaryKeywords.Add(keyword.TriggerPhrase);
                    }

                    KeywordSpottingConfiguration spottingConfig = new KeywordSpottingConfiguration()
                    {
                        PrimaryKeyword = _clientConfig.TriggerPhrase,
                        PrimaryKeywordSensitivity = _clientConfig.PrimaryAudioTriggerSensitivity,
                        SecondaryKeywordSensitivity = _clientConfig.SecondaryAudioTriggerSensitivity,
                        SecondaryKeywords = secondaryKeywords
                    };

                    _audioTrigger.Value.Configure(spottingConfig);
                    _currentSecondaryKeywords = durandalResult.TriggerKeywords;
                }

                if (_clientActionDispatcher != null && !string.IsNullOrEmpty(durandalResult.ResponseAction))
                {
                    // We need to process client actions on their own background thread, otherwise they will hog up the request lock while we wait for them to finish.
                    IRealTimeProvider clientActionRealTime = realTime.Fork("ClientSideActionHandlerThread");
                    _clientActionThread = Task.Run(async () =>
                    {
                        try
                        {
                            await _clientActionDispatcher.InterpretClientAction(
                                durandalResult.ResponseAction, this, queryLogger, _clientActionCancelizer.Token, clientActionRealTime).ConfigureAwait(false);
                        }
                        finally
                        {
                            clientActionRealTime.Merge();
                        }
                    });
                }
            }
            else if (durandalResult.ExecutionResult == Result.Skip)
            {
                // This usually happens when the utterance is tagged as side speech.
                // Just trigger "skip" and back out. Don't treat it
                // as a major error.
                OnSkip(realTime, queryLogger);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(durandalResult.ErrorMessage))
                {
                    OnShowErrorOutput(durandalResult.ErrorMessage, realTime, queryLogger);
                }

                OnFail(realTime, queryLogger);
            }

            await queryLogger.Flush(CancellationToken.None, realTime, false).ConfigureAwait(false);
        }

        /// <summary>
        /// Given a dialog response, create an audio graph which will eventually produce
        /// the correct audio stream for this response. This includes synthesizing TTS
        /// as well as handling custom plugin audio.
        /// </summary>
        /// <param name="responseAudio">The encoded response audio from dialog</param>
        /// <param name="responseSsml">The spoken SSML from dialog, if any</param>
        /// <param name="customAudioOrdering">A flag specifying which order custom audio from dialog should be played</param>
        /// <param name="queryLogger">A query logger</param>
        /// <param name="audioCodecCollection">A collection of audio codecs supported by the client</param>
        /// <param name="audioOutputFormat">The client's audio graph output format</param>
        /// <param name="audioGraph">The audio graph used by the client for output</param>
        /// <param name="ttsEngine">The client's text-to-speech synthesizer</param>
        /// <param name="locale">The locale of the conversation</param>
        /// <param name="audioQuality">The quality hint to use for audiio processing</param>
        /// <param name="cancelToken">A cancellation token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>A single audio graph component (which itself may contain more subcomponents) which will produce the entire output for this conversation turn.</returns>
        public static async Task<IAudioSampleSource> GenerateFinalResponseAudio(
            AudioData responseAudio,
            string responseSsml,
            AudioOrdering customAudioOrdering,
            ILogger queryLogger,
            IAudioCodecFactory audioCodecCollection,
            AudioSampleFormat audioOutputFormat,
            WeakPointer<IAudioGraph> audioGraph,
            ISpeechSynth ttsEngine,
            LanguageCode locale,
            AudioProcessingQuality audioQuality,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            AudioConcatenator concatenator = new AudioConcatenator(audioGraph, audioOutputFormat, "DialogResponseConcatenator", false, queryLogger);
            IAudioSampleSource customPluginSound = null;
            IAudioSampleSource ttsStream = null;

            ////// Process custom audio if present //////
            if (responseAudio != null && responseAudio.Data.Count > 0)
            {
                if (audioCodecCollection.CanDecode(responseAudio.Codec))
                {
                    queryLogger.Log("Response audio uses \"" + responseAudio.Codec + "\" encoding");

                    // Ownership of these streams is transferred to the AudioDecoder which gets put in the return value, so 
#pragma warning disable CA2000
                    NonRealTimeStream stream = new NonRealTimeStreamWrapper(
                        new MemoryStream(
                            responseAudio.Data.Array,
                            responseAudio.Data.Offset,
                            responseAudio.Data.Count,
                            false), true);
#pragma warning restore CA2000
                    AudioDecoder pluginResultDecoder = audioCodecCollection.CreateDecoder(
                        responseAudio.Codec,
                        responseAudio.CodecParams,
                        audioGraph,
                        queryLogger.Clone("ResponseAudioDecoder"),
                        "PluginResultDecoder");
                    
                    AudioInitializationResult codecInitialize = await pluginResultDecoder.Initialize(stream, true, cancelToken, realTime).ConfigureAwait(false);
                    if (codecInitialize != AudioInitializationResult.Success)
                    {
                        queryLogger.Log("Failed to initialize decoder for \"" + responseAudio.Codec + "\" audio", LogLevel.Err);
                    }
                    else
                    {
                        AudioConformer pluginResultConformer = new AudioConformer(
                            audioGraph,
                            pluginResultDecoder.OutputFormat,
                            concatenator.OutputFormat,
                            "PluginResultConformer",
                            queryLogger.Clone("PluginResultConformer"),
                            resamplerQuality: audioQuality);

                        try
                        {
                            pluginResultConformer.TakeOwnershipOfDisposable(pluginResultDecoder);
                            pluginResultDecoder.ConnectOutput(pluginResultConformer);
                            customPluginSound = pluginResultConformer;
                            pluginResultConformer = null;
                        }
                        finally
                        {
                            pluginResultConformer?.Dispose(); // error case
                        }
                    }
                }
                else
                {
                    queryLogger.Log("Response audio is encoded with an unknown codec \"" + responseAudio.Codec + "\"", LogLevel.Err);
                }
            }

            ////// Synthesize TTS if present //////
            if (ttsEngine != null && !string.IsNullOrEmpty(responseSsml))
            {
                Stopwatch ttsTimer = Stopwatch.StartNew();
                SpeechSynthesisRequest speechSynthRequest = new SpeechSynthesisRequest()
                {
                    Ssml = responseSsml,
                    Locale = locale,
                    VoiceGender = VoiceGender.Female,
                };

                IAudioSampleSource ttsSynth = await ttsEngine.SynthesizeSpeechToStreamAsync(
                    speechSynthRequest,
                    audioGraph,
                    cancelToken,
                    realTime,
                    queryLogger.Clone("TTSSynth")).ConfigureAwait(false);

                AudioConformer ttsConformer = null;
                try
                {
                    ttsConformer = new AudioConformer(
                        audioGraph,
                        ttsSynth.OutputFormat,
                        concatenator.OutputFormat,
                        "TTSConformer",
                        queryLogger.Clone("TTSConformer"),
                        resamplerQuality: audioQuality);

                    ttsConformer.TakeOwnershipOfDisposable(ttsSynth);
                    ttsSynth.ConnectOutput(ttsConformer);
                    ttsStream = ttsConformer;
                    ttsTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_RunTTS, ttsTimer), LogLevel.Ins);
                    ttsConformer = null;
                }
                finally
                {
                    ttsConformer?.Dispose();
                }
            }

            // And arbitrate custom audio + TTS with respect to the answer's desired ordering
            if (customPluginSound != null &&
                (ttsStream == null || customAudioOrdering == AudioOrdering.BeforeSpeech))
            {
                concatenator.EnqueueInput(customPluginSound, null, true);
            }
            if (ttsStream != null)
            {
                concatenator.EnqueueInput(ttsStream, null, true);
            }
            if (customPluginSound != null && ttsStream != null && customAudioOrdering != AudioOrdering.BeforeSpeech)
            {
                concatenator.EnqueueInput(customPluginSound, null, true);
            }

            // If there is audio on this path and not streaming audio, then mark the audio UPL metric here on the assumption that a stream will not play asynchronously later
            //if (finalOutputAudio != null && finalOutputAudio.DataLength > 0 && string.IsNullOrEmpty(durandalResult.StreamingAudioUrl))
            //{
            //    audioUplStopwatch.Stop();
            //    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_AudioUPL, audioUplStopwatch), LogLevel.Ins);
            //}

            return concatenator;
        }

#endregion

        #region Turn-0 stuff

        /// <summary>
        /// Just pings the dialog server to make sure the connection is valid
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private async Task DoInitialHandshake(ILogger logger, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(ClientCore), "Client has been disposed");
            }

            OnInitializationUpdate("Contacting dialog service", realTime, logger);
            // Get basic dialog server capabilities
            IDictionary<string, string> deStatus = await _dialogConnection.GetStatus(logger, CancellationToken.None, realTime).ConfigureAwait(false);
            if (deStatus == null)
            {
                logger.Log("Dialog service did not respond to ping", LogLevel.Wrn);
            }
            else
            {
                logger.Log("Dialog reports the following status:");
                foreach (KeyValuePair<string, string> kvp in deStatus)
                {
                    logger.Log(kvp.Key + " = " + kvp.Value);
                    if (kvp.Key.Equals("ProtocolVersion"))
                    {
                        string expectedProtocolVersion = new DialogRequest().ProtocolVersion.ToString();
                        if (!kvp.Value.Equals(expectedProtocolVersion))
                        {
                            logger.Log("The dialog service reports a different protocol version (" + kvp.Value + ") than expected (" + expectedProtocolVersion + "). Queries will probably not work", LogLevel.Err);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Resets all conversation state and sends a special "greet" request to the dialog service. This request
        /// will return a conversational response which usually contains some kind of start page and a greeting from
        /// the service, which will be handled like a regular response
        /// </summary>
        /// <param name="context">The client context to use</param>
        /// <param name="requestFlags"></param>
        /// <param name="realTime"></param>
        /// <returns>The asynchronous task for the greet request</returns>
        public async Task Greet(
            ClientContext context,
            QueryFlags requestFlags = QueryFlags.None,
            IRealTimeProvider realTime = null)
        {
            _logger.Log("Greet() called");
            // FIXME: this doesn't use requestWorkerMutex so it's possible two requests can be happening at once
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            ResetConversationStateResult resetResult = await _dialogConnection.ResetConversationState(_clientConfig.UserId, _clientConfig.ClientId, _logger, CancellationToken.None, realTime).ConfigureAwait(false);
            if (resetResult.Success)
            {
                // We need to clone the logger so we have a non-volatile copy local to this thread
                ILogger queryLogger = _logger.Clone();

                DialogRequest greetRequest = new DialogRequest()
                {
                    InteractionType = InputMethod.Programmatic,
                    RequestFlags = requestFlags
                };

                if (_clientPresentationLayer == null)
                {
                    context.RemoveCapabilities(ClientCapabilities.ServeHtml);
                }

                greetRequest.ClientContext = context;
                greetRequest.LanguageUnderstanding = new List<RecognizedPhrase>();
                greetRequest.LanguageUnderstanding.Add(new RecognizedPhrase());
                greetRequest.LanguageUnderstanding[0].Recognition = new List<RecoResult>();
                greetRequest.LanguageUnderstanding[0].Recognition.Add(new RecoResult() { Domain = DialogConstants.REFLECTION_DOMAIN, Intent = "greet", Confidence = 1.0f });
                
                if (_authenticator != null)
                {
                    Stopwatch authTimer = Stopwatch.StartNew();
                    queryLogger.Log("Adding authentication tokens to greet request", LogLevel.Vrb);
                    await _authenticator.AuthenticateClientRequest(greetRequest, queryLogger, realTime).ConfigureAwait(false);
                    authTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_GenerateRequestToken, authTimer), LogLevel.Ins);
                }

                queryLogger.Log("Starting dialog greet request", LogLevel.Vrb);
                using (NetworkResponseInstrumented<DialogResponse> result = await _dialogConnection.MakeQueryRequest(greetRequest, queryLogger, CancellationToken.None, realTime).ConfigureAwait(false))
                {
                    queryLogger.Log("Finished dialog greet request", LogLevel.Vrb);
                    if (result == null)
                    {
                        OnShowErrorOutput("Null response was recieved from the dialog service.", realTime, queryLogger);
                    }
                    else
                    {
                        queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_Request, result.RequestSize), LogLevel.Ins);
                        queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_Response, result.ResponseSize), LogLevel.Ins);
                        queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_E2E, result.EndToEndLatency), LogLevel.Ins);

                        if (result.Response == null)
                        {
                            OnShowErrorOutput("No response was recieved from the dialog service.", realTime, queryLogger);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(result.Response.ResponseHtml) && _clientPresentationLayer != null)
                            {
                                Uri url = await _clientPresentationLayer.GeneratePresentationUrlFromResponse(result.Response, realTime).ConfigureAwait(false);
                                if (url != null)
                                {
                                    OnNavigateUrl(url, realTime, queryLogger);
                                }
                            }
                            else if (!string.IsNullOrEmpty(result.Response.ResponseUrl))
                            {
                                OnNavigateUrl(new Uri(_dialogConnection.GetConnectionString() + result.Response.ResponseUrl), realTime, queryLogger);
                            }

                            // TODO: Handle text or audio greeting here, not just the splash page HTML
                        }
                    }
                }

                ResetTraceId();
            }
            else
            {
                OnShowErrorOutput("Could not retrieve a context from the dialog server. It may be misconfigured or not responding. Error: " + resetResult.ErrorMessage,
                    realTime, _logger);
            }
        }

#endregion
        
        #region Audio request handler

        /// <summary>
        /// Attempts to initiate an audio request with the dialog service. If successful, this method returns true
        /// and starts a task which begins recording audio from the microphone. Otherwise (if there is another request
        /// already in progress) this returns false.
        /// </summary>
        /// <param name="context">The client context to use for the request. If null, the default ClientContextFactory() is used</param>
        /// <param name="flags"></param>
        /// <param name="inputEntities"></param>
        /// <param name="entityContext"></param>
        /// <param name="realTime"></param>
        /// <returns>True if we were able to initiate a request</returns>
        public async Task<bool> TryMakeAudioRequest(
            ClientContext context = null,
            QueryFlags flags = QueryFlags.None,
            IList<ContextualEntity> inputEntities = null,
            KnowledgeContext entityContext = null,
            IRealTimeProvider realTime = null)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(ClientCore), "Client has been disposed");
            }

            _logger.Log("TryMakeAudioRequest() called");
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            if (!_audioInputEnabled)
            {
                _logger.Log("Can't start an audio request - audio input is not enabled in client core", LogLevel.Wrn);
                return false;
            }

            if (await WaitForRequestLockToBecomeAvailable(realTime).ConfigureAwait(false))
            {
                await _clientStateMachineLock.GetLockAsync().ConfigureAwait(false);
                try
                {
                    if (_clientInteractionState != ClientInteractionState.WaitingForUserInput && _clientInteractionState != ClientInteractionState.PlayingAudio)
                    {
                        _logger.Log("Client core is in a state \"" + _clientInteractionState.ToString() + "\" which does not allow audio requests");
                        return false;
                    }

                    CancelAnyCurrentClientActions();

                    if (context == null)
                    {
                        context = _contextGenerator();
                    }

                    if (_clientPresentationLayer == null)
                    {
                        context.RemoveCapabilities(ClientCapabilities.ServeHtml);
                    }

                    if (_clientActionDispatcher != null)
                    {
                        context.SupportedClientActions = _clientActionDispatcher.GetSupportedClientActions();
                    }

                    if (!context.SupportedClientActions.Contains(SendNextTurnAudioAction.ActionName))
                    {
                        context.SupportedClientActions.Add(SendNextTurnAudioAction.ActionName);
                    }
                    if (!context.SupportedClientActions.Contains(StopListeningAction.ActionName))
                    {
                        context.SupportedClientActions.Add(StopListeningAction.ActionName);
                    }

                    if (!_audioOutputEnabled && context.GetCapabilities().HasFlag(ClientCapabilities.HasSpeakers))
                    {
                        _logger.Log("Your context claims to support speakers, but no audio player reference was passed to client core", LogLevel.Wrn);
                        context.RemoveCapabilities(ClientCapabilities.HasSpeakers |
                            ClientCapabilities.SupportsCompressedAudio |
                            ClientCapabilities.SupportsStreamingAudio |
                            ClientCapabilities.CanSynthesizeSpeech);
                    }

                    // Tell the microphone listener to start diverting audio to speech reco
                    OnSpeechPrompt(realTime, _logger);
                    _audioRecordingStopwatch.Restart();
                    _logger.Log("Audio request triggered. Switching to active record mode", LogLevel.Std);

                    //_logger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_StartSpeechReco, _audioRecordingStopwatch), LogLevel.Ins);

                    // Queue the request on a new thread.
                    //IRealTimeProvider threadLocalTime = realTime.Fork("ClientAudioRequestThread");
                    //_requestWorkerThread = Task.Run((Func<object, Task>)RunAudioWorkerThread, new AudioWorkerClosureParams()
                    //    {
                    //        ClientContext = context,
                    //        QueryFlags = flags,
                    //        QueryLogger = _logger.Clone("ClientAudioWorkerThread"),                                         
                    //        RequestTimeProvider = threadLocalTime,
                    //        EntityContext = entityContext,
                    //        InputEntities = inputEntities
                    //    });

                    // To start recording an utterance, create an utterance recorder, route the microphone to it, and then set a callback event for when recording finishes

                    _utteranceRecorder.Value.Reset();
                    // this stops playback of all sounds from the client core (though the client implementation may continue with its own mixer)
                    _speakerOutputMixer.DisconnectAllInputs();
                    _transientSpeechRecognizer = await _speechRecoEngine.Value.CreateRecognitionStream(
                        _inputAudioGraph,
                        "ClientSpeechRecognizer",
                        context.Locale,
                        _logger.Clone("ClientSpeechReco"),
                        CancellationToken.None,
                        realTime).ConfigureAwait(false);

                    if (_transientSpeechRecognizer == null)
                    {
                        _logger.Log("Could not start recording because the speech recognizer failed to initialize", LogLevel.Err);
                        return false;
                    }

                    _transientSpeechCircuitBreaker = new AudioExceptionCircuitBreaker(_inputAudioGraph, _transientSpeechRecognizer.InputFormat, "SpeechRecoCircuitBreaker", _logger.Clone("SpeechRecoCircuitBreaker"));
                    _transientSpeechCircuitBreaker.ExceptionRaisedEvent.Subscribe(HandleAudioInputErrorEvent);
                    _audioRecordingBucket.ClearBucket();
                    if (_audioTriggeringEnabled)
                    {
                        _audioTrigger.Value.DisconnectInput();
                    }

                    _transientSpeechCircuitBreaker.ConnectOutput(_transientSpeechRecognizer);
                    _microphoneInputSplitter.AddOutput(_utteranceRecorder.Value);
                    _microphoneInputSplitter.AddOutput(_transientSpeechCircuitBreaker);
                    _microphoneInputSplitter.AddOutput(_audioRecordingBucket);
                    _transientAudioRequestParams = new AudioRequestBundle()
                    {
                        ClientContext = context,
                        QueryFlags = flags,
                        ContextualEntities = inputEntities,
                        KnowledgeContext = entityContext,
                        QueryLogger = _logger.Clone("AudioRequest")
                    };

                    _logger.Log("TryMakeAudioRequest: Transitioning to ClientInteractionState.RecordingUtterance");
                    _clientStateMachine.Transition(ref _clientInteractionState, ClientInteractionState.RecordingUtterance);

                    return true;
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                    _logger.Log("TryMakeAudioRequest exception: Transitioning to ClientInteractionState.WaitingForRequest");
                    _clientStateMachine.ForceTransition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);
                    _requestWorkerThread = null;
                    _requestWorkerFinished.Set();
                }
                finally
                {
                    _clientStateMachineLock.Release();
                }
            }

            _logger.Log("Can't start an audio request - another request is already in progress");

            return false;
        }

        private class AudioRequestBundle
        {
            public ILogger QueryLogger;
            public ClientContext ClientContext;
            public QueryFlags QueryFlags;
            public IList<ContextualEntity> ContextualEntities;
            public KnowledgeContext KnowledgeContext;
        }

        private async Task HandleAudioInputErrorEvent(object sender, EventArgs eventArgs, IRealTimeProvider realTime)
        {
            AudioRequestBundle bundle = _transientAudioRequestParams;
            ILogger queryLogger = bundle.QueryLogger;
            queryLogger.Log("Error detected in speech recognizer. Force quitting record mode", LogLevel.Err);

            await _clientStateMachineLock.GetLockAsync().ConfigureAwait(false);
            try
            {
                _utteranceRecorder.Value.DisconnectInput();
                _transientSpeechCircuitBreaker.DisconnectInput();
                _audioRecordingBucket.DisconnectInput();
                _transientSpeechCircuitBreaker.ExceptionRaisedEvent.Unsubscribe(HandleAudioInputErrorEvent);

                if (_audioTriggeringEnabled)
                {
                    _microphoneInputSplitter.AddOutput(_audioTrigger.Value);
                }
                
                _clientStateMachine.ForceTransition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);
                OnSpeechCaptureFinished(string.Empty, false, realTime, _logger);
                _transientSpeechRecognizer?.Dispose();
                _transientSpeechRecognizer = null;
                _transientSpeechCircuitBreaker?.Dispose();
                _transientSpeechCircuitBreaker = null;
            }
            finally
            {
                _clientStateMachineLock.Release();
                _requestWorkerFinished.Set();
            }
        }

        private async Task HandleRecordingFinishedEvent(object sender, RecorderStateEventArgs eventArgs, IRealTimeProvider realTime)
        {
            await _clientStateMachineLock.GetLockAsync().ConfigureAwait(false);
            try
            {
                // If we are not in recording state, ignore this event
                if (_clientInteractionState != ClientInteractionState.RecordingUtterance)
                {
                    return;
                }

                AudioRequestBundle bundle = _transientAudioRequestParams;
                ILogger queryLogger = bundle.QueryLogger;
                queryLogger.Log("End of utterance detected");

                // First, move the audio graph out of recording state
                _utteranceRecorder.Value.DisconnectInput();
                _transientSpeechCircuitBreaker.DisconnectInput();
                _audioRecordingBucket.DisconnectInput();
                _transientSpeechCircuitBreaker.ExceptionRaisedEvent.Unsubscribe(HandleAudioInputErrorEvent);

                if (_audioTriggeringEnabled)
                {
                    _microphoneInputSplitter.AddOutput(_audioTrigger.Value);
                }

                try
                {
                    if (eventArgs.State == RecorderState.Finished)
                    {
                        queryLogger.Log("Utterance captured. Processing final reco results", LogLevel.Std);
                        _audioRecordingStopwatch.Stop();
                        _logger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_RecordingUtterance, _audioRecordingStopwatch), LogLevel.Ins);
                        _audioRecordingStopwatch.Restart();

                        SpeechRecognitionResult recoResult = await _transientSpeechRecognizer.FinishUnderstandSpeech(CancellationToken.None, realTime).ConfigureAwait(false);
                        _audioRecordingStopwatch.Stop();

                        queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_FinishSpeechReco, _audioRecordingStopwatch), LogLevel.Ins);
                        queryLogger.Log(CommonInstrumentation.GenerateObjectEntry("Client.SRResults", recoResult), LogLevel.Ins, queryLogger.TraceId, DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                        _audioRecordingStopwatch.Start();

                        _logger.Log("HandleRecordingFinishedEvent: Transitioning to ClientInteractionState.MakingRequest");
                        _clientStateMachine.Transition(ref _clientInteractionState, ClientInteractionState.MakingRequest);
                        string bestHyp = string.Empty;
                        if (recoResult.RecognizedPhrases != null &&
                            recoResult.RecognizedPhrases.Count > 0)
                        {
                            bestHyp = recoResult.RecognizedPhrases[0].DisplayText;
                        }

                        OnSpeechCaptureFinished(bestHyp, true, realTime, queryLogger);

                        AudioSample rawUtterance = _audioRecordingBucket.GetAllAudio();
                        RequestResponsePair finalResponse = await AudioRequestInternal(
                            recoResult,
                            bundle.ClientContext,
                            rawUtterance,
                            queryLogger,
                            bundle.QueryFlags,
                            bundle.ContextualEntities,
                            bundle.KnowledgeContext,
                            realTime).ConfigureAwait(false);

                        await HandleResult(finalResponse, queryLogger, realTime, _audioRecordingStopwatch, CancellationToken.None).ConfigureAwait(false);

                        // FIXME: The "playing audio" and "waiting for reprompt" states are not implemented
                        _logger.Log("HandleRecordingFinishedEvent postrequest: Transitioning to ClientInteractionState.WaitingForRequest");
                        _clientStateMachine.Transition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);
                    }
                    else if (eventArgs.State == RecorderState.FinishedNothingRecorded)
                    {
                        // Tell user that the mic didn't pick up anything
                        // Send a signal down the channel to tell listeners to stop waiting on us
                        _logger.Log("Recorder said \"nothing recorded\" or error, canceling speech reco...", LogLevel.Wrn);
                        _logger.Log("HandleRecordingFinishedEvent postsilence: Transitioning to ClientInteractionState.WaitingForRequest");
                        _clientStateMachine.Transition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);
                        OnSpeechCaptureFinished(string.Empty, false, realTime, _logger);
                    }
                    else
                    {
                        // Error state
                        _logger.Log("Recorder returned error state. Don't know what to do", LogLevel.Err);
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                    _logger.Log("HandleRecordingFinishedEvent exception handler: Transitioning to ClientInteractionState.WaitingForRequest");
                    _clientStateMachine.ForceTransition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);
                    OnSpeechCaptureFinished(string.Empty, false, realTime, _logger);
                }
                finally
                {
                    ResetTraceId();
                    _transientSpeechRecognizer?.Dispose();
                    _transientSpeechRecognizer = null;
                    _transientSpeechCircuitBreaker?.Dispose();
                    _transientSpeechCircuitBreaker = null;
                    _requestWorkerFinished.Set();
                }
            }
            finally
            {
                _clientStateMachineLock.Release();
            }
        }
        
        /// <summary>
        /// Attempts to start an audio request using speech recognition results that are provided by the caller. This bypasses the normal async audio
        /// path which opens the mic, listens to user, runs speech reco, and then dispatches the request. 
        /// </summary>
        /// <param name="speechResults"></param>
        /// <param name="rawAudio"></param>
        /// <param name="context"></param>
        /// <param name="flags"></param>
        /// <param name="inputEntities"></param>
        /// <param name="entityContext"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public async Task<bool> TryMakeAudioRequestWithSpeechResult(
            SpeechRecognitionResult speechResults,
            AudioSample rawAudio,
            ClientContext context = null,
            QueryFlags flags = QueryFlags.None,
            IList<ContextualEntity> inputEntities = null,
            KnowledgeContext entityContext = null,
            IRealTimeProvider realTime = null)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(ClientCore), "Client has been disposed");
            }

            _logger.Log("TryMakeAudioRequestWithSpeechResult() called", LogLevel.Vrb);
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            if (await WaitForRequestLockToBecomeAvailable(realTime).ConfigureAwait(false))
            {
                await _clientStateMachineLock.GetLockAsync().ConfigureAwait(false);
                try
                {
                    if (_clientInteractionState != ClientInteractionState.WaitingForUserInput && _clientInteractionState != ClientInteractionState.PlayingAudio)
                    {
                        _logger.Log("Client core is in a state \"" + _clientInteractionState.ToString() + "\" which does not allow action requests");
                        return false;
                    }

                    if (context == null)
                    {
                        context = _contextGenerator();
                    }

                    if (_clientPresentationLayer == null)
                    {
                        context.RemoveCapabilities(ClientCapabilities.ServeHtml);
                    }

                    if (_clientActionDispatcher != null)
                    {
                        context.SupportedClientActions = _clientActionDispatcher.GetSupportedClientActions();
                    }

                    if (!context.SupportedClientActions.Contains(SendNextTurnAudioAction.ActionName))
                    {
                        context.SupportedClientActions.Add(SendNextTurnAudioAction.ActionName);
                    }
                    if (!context.SupportedClientActions.Contains(StopListeningAction.ActionName))
                    {
                        context.SupportedClientActions.Add(StopListeningAction.ActionName);
                    }

                    if (!_audioOutputEnabled && context.GetCapabilities().HasFlag(ClientCapabilities.HasSpeakers))
                    {
                        _logger.Log("Your context claims to support speakers, but no audio player object was passed to client core", LogLevel.Wrn);
                        context.RemoveCapabilities(ClientCapabilities.HasSpeakers |
                            ClientCapabilities.SupportsCompressedAudio |
                            ClientCapabilities.SupportsStreamingAudio |
                            ClientCapabilities.CanSynthesizeSpeech);
                    }

                    _clientStateMachine.Transition(ref _clientInteractionState, ClientInteractionState.MakingRequest);

                    // Queue the request on a new thread.
                    IRealTimeProvider requestTimeProvider = realTime.Fork("ClientAudioRequestThread");
                    _requestWorkerThread = Task.Run(async () =>
                    {
                        try
                        {
                            // We need to clone the logger so we have a non-volatile copy local to this thread
                            ILogger queryLogger = _logger.Clone();
                            queryLogger.Log("Audio request trace ID is " + CommonInstrumentation.FormatTraceId(queryLogger.TraceId));
                            Stopwatch audioUplStopwatch = Stopwatch.StartNew();
                            RequestResponsePair result = await AudioRequestInternal(speechResults, context, rawAudio, queryLogger, flags, inputEntities, entityContext, requestTimeProvider).ConfigureAwait(false);
                            await HandleResult(result, queryLogger, requestTimeProvider, audioUplStopwatch, CancellationToken.None).ConfigureAwait(false);
                        }
                        finally
                        {
                            ResetTraceId();

                            // Self-destruct the thread
                            _clientStateMachine.ForceTransition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);
                            _requestWorkerThread = null;
                            _requestWorkerFinished.Set();
                            requestTimeProvider.Merge();
                        }
                    });

                    return true;
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                    _clientStateMachine.ForceTransition(ref _clientInteractionState, ClientInteractionState.WaitingForUserInput);
                    _requestWorkerThread = null;
                    _requestWorkerFinished.Set();
                }
                finally
                {
                    _clientStateMachineLock.Release();
                }
            }

            _logger.Log("Can't start an audio request - another request is already in progress");

            return false;
        }

        private async Task<RequestResponsePair> AudioRequestInternal(
            SpeechRecognitionResult srResults,
            ClientContext context,
            AudioSample spokenAudio,
            ILogger queryLogger,
            QueryFlags flags,
            IList<ContextualEntity> inputEntities,
            KnowledgeContext entityContext,
            IRealTimeProvider realTime)
        {
            OnMakingRequest(realTime, queryLogger);

            RequestResponsePair returnVal = new RequestResponsePair();

            if (srResults == null)
            {
                returnVal.Request = null;
                queryLogger.Log("SR results are null!", LogLevel.Err);
                return null;
            }

            if (spokenAudio == null)
            {
                returnVal.Request = null;
                queryLogger.Log("Captured user speech is null!", LogLevel.Err);
                return null;
            }
            
            try
            {
                _logger.Log("Dispatching audio request to server", LogLevel.Vrb);
                Stopwatch timer = Stopwatch.StartNew();
                returnVal.Request = new DialogRequest()
                {
                    InteractionType = InputMethod.Spoken,
                    ClientContext = context,
                    TraceId = (queryLogger == null || !queryLogger.TraceId.HasValue) ? string.Empty : CommonInstrumentation.FormatTraceId(queryLogger.TraceId.Value),
                    RequestFlags = flags,
                    PreferredAudioCodec = _preferredAudioCodec,
                    SpeechInput = srResults,
                    PreferredAudioFormat = !_audioOutputEnabled ? string.Empty : CommonCodecParamHelper.CreateCodecParams(_speakers.Value.InputFormat),
                };

                if (inputEntities != null && inputEntities.Count > 0 && entityContext != null && !entityContext.IsEmpty)
                {
                    // Add entities and serialized context to input
                    using (PooledBuffer<byte> serializedEntityContext = KnowledgeContextSerializer.SerializeKnowledgeContext(entityContext))
                    {
                        byte[] copiedEntityContext = new byte[serializedEntityContext.Length];
                        ArrayExtensions.MemCopy(serializedEntityContext.Buffer, 0, copiedEntityContext, 0, copiedEntityContext.Length);
                        returnVal.Request.EntityContext = new ArraySegment<byte>(copiedEntityContext);
                        returnVal.Request.EntityInput = new List<EntityReference>();
                        foreach (ContextualEntity inputEntity in inputEntities)
                        {
                            returnVal.Request.EntityInput.Add(new EntityReference()
                            {
                                EntityId = inputEntity.Entity.EntityId,
                                Relevance = inputEntity.Relevance
                            });
                        }
                    }
                }

                // If no SR results, append an empty string. This will prevent the server from trying its own redundant SR
                // Disabling this because the server may have better luck performing its own SR, and we really can't make
                // it worse by _not_ sending what we have
                //if (returnVal.Request.Queries.Count == 0)
                //{
                //    returnVal.Request.Queries.Add(new SpeechHypothesis()
                //        {
                //            Utterance = string.Empty,
                //            Confidence = 0
                //        });
                //}

                // If we have no SR connection, or the service has requested audio for this turn, send it
                if (_speechRecoEngine.Value == null ||
                    _speechRecoEngine.Value is NullSpeechRecoFactory ||
                    returnVal.Request.SpeechInput == null ||
                    returnVal.Request.SpeechInput.RecognitionStatus != SpeechRecognitionStatus.Success ||
                    returnVal.Request.SpeechInput.RecognizedPhrases == null ||
                    returnVal.Request.SpeechInput.RecognizedPhrases.Count == 0 ||
                    _sendAudioNextTurn)
                {
                    // TODO: We should really just compress the audio as we're bucketizing it rather than doing it all at once here
                    // TODO: Also it would be nice to do actual compression here as well - what's the potential tradeoff of bandwidth vs. computation?
                    queryLogger.Log("Sending raw audio as part of client request");
                    returnVal.Request.AudioInput = await AudioHelpers.EncodeAudioSampleUsingCodec(spokenAudio, _audioCodecCollection, RawPcmCodecFactory.CODEC_NAME_PCM_S16LE, queryLogger).ConfigureAwait(false);
                }

                // Log the input audio volume to try and diagnose microphone or environment issues
                queryLogger.Log(() => CommonInstrumentation.GenerateObjectEntry("Client.Audio.Volume", spokenAudio.VolumeDb()), LogLevel.Ins, privacyClass: DataPrivacyClassification.SystemMetadata);
                queryLogger.Log(() => CommonInstrumentation.GenerateObjectEntry("Client.Audio.Length", spokenAudio.Duration.TotalMilliseconds), LogLevel.Ins, privacyClass: DataPrivacyClassification.SystemMetadata);
                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_BuildAudioContext, timer), LogLevel.Ins);
                timer.Restart();
                // Add authentication info
                if (_authenticator != null)
                {
                    queryLogger.Log("Adding authentication tokens to audio request", LogLevel.Vrb);
                    await _authenticator.AuthenticateClientRequest(returnVal.Request, queryLogger, realTime).ConfigureAwait(false);
                    timer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_GenerateRequestToken, timer), LogLevel.Ins);
                }

                queryLogger.Log("Starting dialog audio query request", LogLevel.Vrb);
                using (NetworkResponseInstrumented<DialogResponse> clientResponse = await _dialogConnection.MakeQueryRequest(returnVal.Request, queryLogger, CancellationToken.None, realTime).ConfigureAwait(false))
                {
                    queryLogger.Log("Finished dialog audio query request", LogLevel.Vrb);
                    if (clientResponse == null)
                    {
                        queryLogger.Log("The network response from dialog engine was null", LogLevel.Err);
                        return returnVal;
                    }

                    queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_Request, clientResponse.RequestSize), LogLevel.Ins);
                    queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_Response, clientResponse.ResponseSize), LogLevel.Ins);
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_E2E, clientResponse.EndToEndLatency), LogLevel.Ins);

                    if (clientResponse.Response == null)
                    {
                        queryLogger.Log("No dialog response was received", LogLevel.Err);
                        return returnVal;
                    }

                    returnVal.Response = clientResponse.Response;
                    return returnVal;
                }
            }
            catch (Exception e)
            {
                queryLogger.Log("Unhandled exception in client during audio query", LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
                returnVal.Request = null;
                returnVal.Response = new DialogResponse();
                return returnVal;
            }
        }

        public async Task AudioTriggerFired(object source, AudioTriggerEventArgs args, IRealTimeProvider realTime)
        {
            AudioTriggerResult trigger = args.AudioTriggerResult;
            long currentTime = realTime.TimestampMilliseconds;

            if (_stopListeningUntil > 0 && _stopListeningUntil > currentTime)
            {
                double secondsRemaining = (double)(_stopListeningUntil - currentTime) / 1000;
                _logger.Log("I got an audio trigger but I was instructed to ignore them for the next " + secondsRemaining + " seconds");
            }
            else if (trigger.WasPrimaryKeyword)
            {
                if (currentTime > _lastAudioPlaybackFinishTime)
                {
                    _logger.Log("Got a primary audio trigger by voice");

                    // Do we need to arbitrate?
                    if (_triggerArbitrator != null)
                    {
                        _logger.Log("Beginning trigger arbitration");
                        bool passedArbitration = await _triggerArbitrator.ArbitrateTrigger(_logger, realTime).ConfigureAwait(false);
                        if (!passedArbitration)
                        {
                            _logger.Log("Ignoring audio trigger based on arbitration result");
                            return;
                        }
                    }

                    OnAudioTriggered(realTime, _logger);
                    EnqueueAudioPromptIn(_logger, realTime, 0);
                    return;
                }
                else
                {
                    _logger.Log("Spotted a primary keyword but I am intentionally ignoring it because output audio is still playing");
                }
            }
            else
            {
                // Find the information about this keyword
                TriggerKeyword triggerInfo = null;
                foreach (TriggerKeyword keyword in _currentSecondaryKeywords)
                {
                    if (string.Equals(trigger.TriggeredKeyword, keyword.TriggerPhrase, StringComparison.OrdinalIgnoreCase))
                    {
                        triggerInfo = keyword;
                    }
                }

                if (triggerInfo == null)
                {
                    _logger.Log("Spotted a unknown keyword \"" + trigger.TriggeredKeyword + "\"; this should never happen");
                }
                else if (triggerInfo.AllowBargeIn && currentTime > _lastAudioPlaybackStartTime && currentTime < (_lastAudioPlaybackFinishTime + BARGE_IN_POSTAUDIO_LENIENCY - BARGE_IN_ASSUMED_LENGTH_OF_KEYPHRASE))
                {
                    int bargeInTime = (int)(currentTime - _lastAudioPlaybackStartTime - BARGE_IN_ASSUMED_LENGTH_OF_KEYPHRASE);
                    _logger.Log("Spotted a barge-in keyword \"" + trigger.TriggeredKeyword + "\"; will send it as an instant query (barge-in time is " + bargeInTime + "ms)");
                    OnAudioTriggered(realTime, _logger);
                    await TryMakeTextRequest(trigger.TriggeredKeyword.ToLowerInvariant(), InputMethod.Spoken, realTime, clientAudioPlaybackTimeMs: bargeInTime).ConfigureAwait(false);
                    return;
                }
                else if (currentTime > _lastAudioPlaybackFinishTime && currentTime < _lastAudioPlaybackFinishTime + (triggerInfo.ExpireTimeSeconds * 1000))
                {
                    _logger.Log("Spotted a secondary keyword \"" + trigger.TriggeredKeyword + "\"; will send it as an instant query");
                    OnAudioTriggered(realTime, _logger);
                    await TryMakeTextRequest(trigger.TriggeredKeyword.ToLowerInvariant(), InputMethod.Spoken, realTime).ConfigureAwait(false);
                    return;
                }
                else
                {
                    _logger.Log("Spotted a secondary keyword \"" + trigger.TriggeredKeyword + "\", but the expire time has already passed");
                }
            }
        }

#endregion

        #region Audio helpers

        /// <summary>
        /// If an utterance is currently being recorded, calling this method will force speech reco to finish as quickly as possible
        /// using the data that has already been captured. This can be used to speed up user interaction.
        /// </summary>
        public Task ForceRecordingFinish(IRealTimeProvider realTime)
        {
            return HandleRecordingFinishedEvent(null, new RecorderStateEventArgs(RecorderState.Finished), realTime);
        }

        /// <summary>
        /// Fired whenever the playback of the main audio response has completed.
        /// Used to time prompts and 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <param name="realTime"></param>
        private Task SpeakerChannelFinished(object sender, PlaybackFinishedEventArgs e, IRealTimeProvider realTime)
        {
            if (e.ChannelToken != null && e.ChannelToken is int && _currentPrimaryAudioToken == (int)e.ChannelToken)
            {
                _logger.Log("The primary audio channel has finished playback", LogLevel.Vrb);
                _lastAudioPlaybackFinishTime = e.ThreadLocalTime.TimestampMilliseconds;
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <summary>
        /// Converts a streaming audio URL into an actual wave sample producer that can be hooked up to an IAudioPlayer.
        /// This method should also handle decompressing compressed formats on-the-fly.
        /// </summary>
        /// <param name="audioUrl"></param>
        /// <param name="queryLogger"></param>
        /// <param name="audioUplStopwatch"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private async Task<IAudioSampleSource> StartStreamingAudio(
            string audioUrl,
            ILogger queryLogger,
            Stopwatch audioUplStopwatch,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            IAudioSampleSource returnVal = null;
            IRealTimeProvider threadLocalTime = realTime.Fork("ClientStreamingAudioReadThread");
            queryLogger.Log("Opening streaming response audio URL " + audioUrl);

            // Open the URL and start a reader thread to pipe the audio to the speakers
            try
            {
                Stopwatch streamingTimer = Stopwatch.StartNew();
                IAudioDataSource audioStream = await _dialogConnection.GetStreamingAudioResponse(
                    audioUrl,
                    queryLogger,
                    cancelToken,
                    threadLocalTime).ConfigureAwait(false);
                if (audioStream == null || audioStream.AudioDataReadStream == null)
                {
                    queryLogger.Log("Failed to fetch response audio", LogLevel.Err);
                    return returnVal;
                }

                queryLogger.Log("Got audio stream; creating decompressor", LogLevel.Vrb);
                if (!_audioCodecCollection.CanDecode(audioStream.Codec))
                {
                    queryLogger.Log("The response audio stream is compressed in a format which this client is not configured to decode!", LogLevel.Err);
                    queryLogger.Log("The client codec is " + _preferredAudioCodec + ", the response audio is " + audioStream.Codec, LogLevel.Err);
                    return returnVal;
                }

                AudioDecoder decoder = _audioCodecCollection.CreateDecoder(
                    audioStream.Codec,
                    audioStream.CodecParams,
                    _outputAudioGraph,
                    queryLogger.Clone("ResponseStreamDecoder"),
                    "ResponseStreamDecoder");

                RateMonitoringNonRealTimeStream rateMonitorStream = new RateMonitoringNonRealTimeStream(audioStream.AudioDataReadStream, queryLogger, "ClientReadFromAudioCache");
                AudioInitializationResult initializeResult = await decoder.Initialize(rateMonitorStream, ownsStream: true, cancelToken: cancelToken, realTime: realTime).ConfigureAwait(false);
                //AudioInitializationResult initializeResult = await decoder.Initialize(audioStream.AudioDataReadStream, ownsStream: true, realTime: cancelToken, realTime: cancelToken).ConfigureAwait(false);
                if (initializeResult != AudioInitializationResult.Success)
                {
                    queryLogger.Log("Failed to initialize audio decoder: result was " + initializeResult.ToString(), LogLevel.Err);
                    return returnVal;
                }

                AudioSampleBuffer buffer = null;
                SilencePaddingFilter silencePad = null;
                try
                {
                    // Prebuffer some audio before we play it on speakers
                    buffer = new AudioSampleBuffer(_outputAudioGraph, decoder.OutputFormat, "StreamingResponsePreBuffer", _clientConfig.StreamingAudioPrebufferTime, true);

                    // Add a silence padding component which will return silence until the buffer is full - this is to keep the output mixer from hanging
                    // if it takes a while to download the stream
                    silencePad = new SilencePaddingFilter(_outputAudioGraph, decoder.OutputFormat, "StreamingResponseSilencePad");

                    AudioExceptionCircuitBreaker decoderCircuitBreaker = new AudioExceptionCircuitBreaker(_outputAudioGraph, decoder.OutputFormat, "StreamingAudioCircuitBreaker", queryLogger.Clone("StreamingAudioCircuitBreaker"));

                    buffer.PrebufferingStartedEvent.Subscribe((sender, args, eventRealTime) =>
                    {
                        queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry("Client_AudioPrebufferingStart", audioUplStopwatch), LogLevel.Ins);
                        queryLogger.Log("Audio UPL stopwatch at start of prebuffering is " + audioUplStopwatch.ElapsedMillisecondsPrecise() + " ms");
                        return DurandalTaskExtensions.NoOpTask;
                    });

                    buffer.PrebufferingFinishedEvent.Subscribe((sender, args, eventRealTime) =>
                        {
                            // Mark the audio playback as actually beginning once the streaming buffer is full
                            _lastAudioPlaybackStartTime = eventRealTime.TimestampMilliseconds;
                            queryLogger.Log("Marking start of streaming audio at " + _lastAudioPlaybackStartTime, LogLevel.Vrb);
                            audioUplStopwatch.Stop();
                            queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_AudioUPL, audioUplStopwatch), LogLevel.Ins);
                            return DurandalTaskExtensions.NoOpTask;
                        });

                    decoder.ConnectOutput(buffer);
                    buffer.ConnectOutput(silencePad);
                    silencePad.ConnectOutput(decoderCircuitBreaker);

                    decoderCircuitBreaker.TakeOwnershipOfDisposable(audioStream);
                    decoderCircuitBreaker.TakeOwnershipOfDisposable(buffer);
                    decoderCircuitBreaker.TakeOwnershipOfDisposable(decoder);
                    decoderCircuitBreaker.TakeOwnershipOfDisposable(silencePad);
                    returnVal = decoderCircuitBreaker;

                    //    }
                    //    catch (IOException e)
                    //    {
                    //        queryLogger.Log("The response audio URL stream was forcibly closed.", LogLevel.Err);
                    //        queryLogger.Log(e, LogLevel.Err);
                    //    }
                    //    finally
                    //    {
                    //        streamingTimer.Stop();
                    //        queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_StreamingAudioResponse, totalSize), LogLevel.Ins);
                    //        queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_StreamingAudioRead, streamingTimer), LogLevel.Ins);
                    //    }
                    //}
                    streamingTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_StreamingAudioBeginRead, streamingTimer), LogLevel.Ins);
                    queryLogger.Log("Audio UPL stopwatch at start of read is " + audioUplStopwatch.ElapsedMillisecondsPrecise() + " ms");
                    buffer = null;
                    silencePad = null;
                }
                finally
                {
                    buffer?.Dispose(); // Error case
                    silencePad?.Dispose();
                }
            }
            catch (Exception e)
            {
                queryLogger.Log("An exception occurred while reading streaming audio", LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
            }

            return returnVal;
        }

        /// <summary>
        /// Enqueues an audio prompt which opens the microphone after a certain delay. The delay
        /// is relative to the time that any current audio begins playing.
        /// </summary>
        /// <param name="delay">The recommended delay in ms. If less than zero, the default delay will be used</param>
        /// <param name="queryLogger"></param>
        /// <param name="realTime"></param>
        private void EnqueueAudioPromptIn(ILogger queryLogger, IRealTimeProvider realTime, int delay = -1)
        {
            if (delay >= 0)
            {
                delay = Math.Min(delay, MAX_REPROMPT_DELAY);
            }
            else
            {
                delay = DEFAULT_REPROMPT_DELAY;
            }

            queryLogger.Log("Queueing audio prompt to occur in " + delay + " milliseconds");
            
            // FIXME reimplement this
            //_nextAutoPromptDelay = delay;
            //_doPromptWhenAudioFinishes.Set();
        }

        /// <summary>
        /// Tells the audio client to send the full recorded audio on the next turn
        /// </summary>
        internal void SendAudioNextTurn()
        {
            _sendAudioNextTurn = true;
        }

        /// <summary>
        /// Tells this client to stop listening for keywords for a certain amount of time.
        /// Passing a delay of 0 will effectively make it start listening again.
        /// </summary>
        /// <param name="delay">The amount of time to wait</param>
        /// <param name="realTime">Real time definition</param>
        public void StopListening(TimeSpan delay, IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            _stopListeningUntil = realTime.TimestampMilliseconds + (long)delay.TotalMilliseconds;
        }

#endregion
        
        #region Events

        /// <summary>
        /// Fired when the service returns a Success response
        /// </summary>
        public AsyncEvent<EventArgs> Success { get; private set; }

        /// <summary>
        /// Fired when the service returns a Skip response
        /// </summary>
        public AsyncEvent<EventArgs> Skip { get; private set; }

        /// <summary>
        /// Fired when the service returns a Fail response
        /// </summary>
        public AsyncEvent<EventArgs> Fail { get; private set; }

        /// <summary>
        /// Fired when the query response contains a URL to be navigated to
        /// </summary>
        public AsyncEvent<UriEventArgs> NavigateUrl { get; private set; }

        /// <summary>
        /// Fired when the query response contains an error message to display
        /// </summary>
        public AsyncEvent<TextEventArgs> ShowErrorOutput { get; private set; }

        /// <summary>
        /// Fired when the query response contains a text message to display
        /// </summary>
        public AsyncEvent<TextEventArgs> ShowTextOutput { get; private set; }

        /// <summary>
        /// Warning: The semantics of this event are not really clear even to me, so try to avoid using it.
        /// Fired when the response indicates a retry or a clarification of the last input.
        /// In this event, you should not clear the canvas; the client core will trigger a reprompt automatically.
        /// </summary>
        public AsyncEvent<EventArgs> RetryEvent { get; private set; }

        /// <summary>
        /// Fired when any type of query response is recieved except for the greet page.
        /// This event is fired before the Success event
        /// </summary>
        public AsyncEvent<EventArgs> ResponseReceived { get; private set; }

        /// <summary>
        /// Fired when the "greet" response comes back successfully
        /// </summary>
        public AsyncEvent<EventArgs> GreetResponseReceived { get; private set; }

        /// <summary>
        /// Fired when the client is just about to start recording speech and should give feedback to the user
        /// (e.g. turning on a recording light or microphone animation)
        /// </summary>
        public AsyncEvent<EventArgs> SpeechPrompt { get; private set; }

        /// <summary>
        /// Fired when the SR service returns its final speech hypothesis
        /// </summary>
        public AsyncEvent<SpeechCaptureEventArgs> SpeechCaptureFinished { get; private set; }

        /// <summary>
        /// Fired when the SR service returns an intermediate speech hypothesis
        /// </summary>
        public AsyncEvent<TextEventArgs> SpeechCaptureIntermediate { get; private set; }

        /// <summary>
        /// Fired when the service wants to update the query that is shown to the user
        /// (with augmented queries, this usually happens at the same time as ResponseReceived)
        /// </summary>
        public AsyncEvent<TextEventArgs> UpdateQuery { get; private set; }

        /// <summary>
        /// Fired while the core is initializing, to allow the UI to show some feedback on the loading screen
        /// </summary>
        public AsyncEvent<TextEventArgs> InitializationUpdate { get; private set; }

        /// <summary>
        /// Fired when the client is fully initialized
        /// </summary>
        public AsyncEvent<EventArgs> Initialized { get; private set; }

        /// <summary>
        /// Fired whenever the user triggers this client by voice (usually by saying the keyphrase).
        /// Also fired on "instant" intents, meaning context-specific keyphrases that depend on the previous turn.
        /// This is typically used to implement "barge-in" scenarios.
        /// </summary>
        public AsyncEvent<EventArgs> AudioTriggered { get; private set; }

        /// <summary>
        /// Signals that there was an error in speech recognition
        /// </summary>
        public AsyncEvent<EventArgs> SpeechRecoError { get; private set; }

        /// <summary>
        /// Fired when the primary response audio begins playback
        /// </summary>
        public AsyncEvent<EventArgs> AudioPlaybackStarted { get; private set; }

        /// <summary>
        /// Fired when the primary response audio finishes playback
        /// </summary>
        public AsyncEvent<EventArgs> AudioPlaybackFinished { get; private set; }

        /// <summary>
        /// Signals the client UI that we want to continue showing the current output (whatever it is) for
        /// at least the specified timespan
        /// </summary>
        public AsyncEvent<TimeSpanEventArgs> Linger { get; private set; }

        /// <summary>
        /// Signals that the client is about to make a request
        /// </summary>
        public AsyncEvent<EventArgs> MakingRequest { get; private set; }

        /// <summary>
        /// Signals that the active user identity in a multi-user has changed (eitther as a result of the user
        /// explicitly selecting a new identity, or from cognition such as face or voice print recognition by the client core)
        /// </summary>
        public AsyncEvent<UserIdentityChangedEventArgs> UserIdentityChanged { get; private set; }

        public void OnLinger(TimeSpan delay, IRealTimeProvider realTime, ILogger traceLogger)
        {
            Linger.FireInBackground(this, new TimeSpanEventArgs(delay), traceLogger, realTime);
        }

        public void OnUserIdentityChanged(UserIdentity newIdentity, IRealTimeProvider realTime, ILogger traceLogger)
        {
            UserIdentityChanged.FireInBackground(this, new UserIdentityChangedEventArgs(newIdentity), traceLogger, realTime);
        }

        private void OnMakingRequest(IRealTimeProvider realTime, ILogger traceLogger)
        {
            MakingRequest.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnSuccess(IRealTimeProvider realTime, ILogger traceLogger)
        {
            Success.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnFail(IRealTimeProvider realTime, ILogger traceLogger)
        {
            Fail.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnSkip(IRealTimeProvider realTime, ILogger traceLogger)
        {
            Skip.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnRetryEvent(IRealTimeProvider realTime, ILogger traceLogger)
        {
            RetryEvent.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnResponseReceived(IRealTimeProvider realTime, ILogger traceLogger)
        {
            ResponseReceived.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnGreetResponseReceived(IRealTimeProvider realTime, ILogger traceLogger)
        {
            GreetResponseReceived.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnNavigateUrl(Uri url, IRealTimeProvider realTime, ILogger traceLogger)
        {
            NavigateUrl.FireInBackground(this, new UriEventArgs(url), traceLogger, realTime);
        }

        private void OnShowErrorOutput(string error, IRealTimeProvider realTime, ILogger traceLogger)
        {
            ShowErrorOutput.FireInBackground(this, new TextEventArgs(error), traceLogger, realTime);
        }

        private void OnShowTextOutput(string message, IRealTimeProvider realTime, ILogger traceLogger)
        {
            ShowTextOutput.FireInBackground(this, new TextEventArgs(message), traceLogger, realTime);
        }

        private void OnSpeechPrompt(IRealTimeProvider realTime, ILogger traceLogger)
        {
            SpeechPrompt.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnSpeechCaptureIntermediate(string mostLikelyTranscript, IRealTimeProvider realTime, ILogger traceLogger)
        {
            SpeechCaptureIntermediate.FireInBackground(this, new TextEventArgs(mostLikelyTranscript), traceLogger, realTime);
        }


        private void OnSpeechCaptureFinished(string mostLikelyTranscript, bool succeeded, IRealTimeProvider realTime, ILogger traceLogger)
        {
            SpeechCaptureFinished.FireInBackground(this, new SpeechCaptureEventArgs(mostLikelyTranscript, succeeded), traceLogger, realTime);
        }

        private void OnUpdateQuery(string newQuery, IRealTimeProvider realTime, ILogger traceLogger)
        {
            UpdateQuery.FireInBackground(this, new TextEventArgs(newQuery), traceLogger, realTime);
        }

        private void OnInitializationUpdate(string message, IRealTimeProvider realTime, ILogger traceLogger)
        {
            InitializationUpdate.FireInBackground(this, new TextEventArgs(message), traceLogger, realTime);
        }

        private void OnInitialized(IRealTimeProvider realTime, ILogger traceLogger)
        {
            Initialized.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnAudioTriggered(IRealTimeProvider realTime, ILogger traceLogger)
        {
            AudioTriggered.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnSpeechRecoError(IRealTimeProvider realTime, ILogger traceLogger)
        {
            SpeechRecoError.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnAudioPlaybackStarted(IRealTimeProvider realTime, ILogger traceLogger)
        {
            AudioPlaybackStarted.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

        private void OnAudioPlaybackFinished(IRealTimeProvider realTime, ILogger traceLogger)
        {
            AudioPlaybackFinished.FireInBackground(this, new EventArgs(), traceLogger, realTime);
        }

#endregion
    }
}
