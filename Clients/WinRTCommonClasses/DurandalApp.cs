using Durandal.API;
using Durandal.Common.Client;
using Durandal.Common.Client.Actions;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Security;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.SR.Cortana;
using Durandal.Common.Speech.SR.Remote;
using Durandal.Common.Speech.Triggers;
using Durandal.Common.Speech.Triggers.Sphinx;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Durandal.Common.File;
using Durandal.Common.Security.Login;
using Durandal.Common.Security.Login.Providers;
using System.Collections.Generic;
using Durandal.Common.Security.Client;
using Durandal.Common.Speech.SR.Azure;
using Windows.UI.Core;
using Durandal;
using Durandal.Common.Time;
using Durandal.Common.Events;

#if WINDOWS_PHONE_APP
using Durandal.Common.Audio.BassAudio;
#endif

#if WINDOWS_UWP
using DurandalClientWin10.Audio;
#endif

namespace DurandalWinRT
{
    public class DurandalApp : IDisposable
    {
        private ClientCore _core = null;
        private IFileSystem _localAssetsFilesystem;
        private IFileSystem _userDataFilesystem;
        private ILogger _coreLogger;
        private IHttpClientFactory _httpClientFactory;
        private IHttpClient _dialogHttpConnection;
        private IDialogClient _dialogClient;
        private IClientSideKeyStore _privateKeyStore;
        private IRealTimeProvider _realTimeProvider;

        private AudioChunk _promptSound;
        private AudioChunk _failSound;
        private AudioChunk _successSound;
        
        private IAudioOutDevice _audioDevice;
        private BasicAudioMixer _audioMixer;
        private IMicrophone _microphone;
        private IAudioCodec _audioCodec;
        private ISpeechRecognizerFactory _speechReco;
        private SphinxAudioTrigger _sphinxTrigger;
        private ISocketFactory _speechRecoSocketFactory;

        private PresentationWebServer _webServer;
        
        private Geolocator _geolocator;
        private Geoposition _userCurrentLocation = null;
        private ClientConfiguration _clientConfig;
        private ReaderWriterLockAsync _coreLock = new ReaderWriterLockAsync();
        private ManualResetEventSlim _appInitFinishedSignal = new ManualResetEventSlim(false);

        // Client action handlers
        private JsonClientActionDispatcher _actionDispatcher;
        private ExecuteDelayedActionHandler _delayedActionHandler;

        private int _disposed = 0;

        ~DurandalApp()
        {
            Dispose(false);
        }

        public async Task<ClientCore> GetClient()
        {
            int hLock = await _coreLock.EnterReadLockAsync();
            try
            {
                return _core;
            }
            finally
            {
                _coreLock.ExitReadLock(hLock);
            }
        }

        public ILogger Logger
        {
            get
            {
                return _coreLogger;
            }
        }

        public ClientConfiguration ClientConfig
        {
            get
            {
                return _clientConfig;
            }
        }

        public IClientPresentationLayer HttpProxy
        {
            get
            {
                return _webServer;
            }
        }

        /// <summary>
        /// Initializes the app backend
        /// </summary>
        /// <param name="uiThreadDispatcher">A dispatcher used to delegate logic to the windows UI thread, because some winRT API calls require it (ex: launching an external browser URL)</param>
        /// <returns></returns>
        public async Task Initialize(CoreDispatcher uiThreadDispatcher)
        {
            int hLock = await _coreLock.EnterWriteLockAsync();
            try
            {
                await StartBackend(uiThreadDispatcher);
            }
            finally
            {
                _coreLogger.Log("Initial core config done; releasing lock...");
                _coreLock.ExitWriteLock(hLock);
            }
        }

        // Code to execute when the application is activated (brought to foreground)
        // This code will not execute when the application is first launched
        public async void AppResumed()
        {
            int hLock = await _coreLock.EnterWriteLockAsync();
            try
            {
                await _core.Resume();
            }
            finally
            {
                _coreLock.ExitWriteLock(hLock);
            }
        }

        // Code to execute when the application is deactivated (sent to background)
        // This code will not execute when the application is closing
        public async void SuspendApp()
        {
            int hLock = await _coreLock.EnterWriteLockAsync();
            try
            {
                await _core.Suspend();
                await _coreLogger.Flush(CancellationToken.None, _realTimeProvider, true); // Attempt to send out the last network instrumentation before we die
            }
            finally
            {
                _coreLock.ExitWriteLock(hLock);
            }
        }

        // Code to execute when the application is closing (eg, user hit Back)
        // This code will not execute when the application is deactivated
        public async void ResumeApp()
        {
            int hLock = await _coreLock.EnterWriteLockAsync();
            try
            {
                await _core.Resume();
            }
            finally
            {
                _coreLock.ExitWriteLock(hLock);
            }
        }

        public void SetActiveUserId(UserIdentity userId)
        {
            if (_core != null && userId != null)
            {
                try
                {
                    _core.SetActiveUserIdentity(userId);
                }
                catch (Exception e)
                {
                    _coreLogger.Log(e, LogLevel.Err);
                }
            }
        }

        /// <summary>
        /// Generates a user identity that is tied uniquely to the current device
        /// </summary>
        /// <returns></returns>
        public static async Task<UserIdentity> GetBuiltInUserIdentity(ILogger logger)
        {
            UserIdentity returnVal = new UserIdentity();

            // Use device ID as user ID by default - so client ID and user ID will be the same in those cases
            try
            {
#if WINDOWS_PHONE_APP
                var token = Windows.System.Profile.HardwareIdentification.GetPackageSpecificToken(null);
                byte[] bytes = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(token.Id);
                byte[] guid = new byte[16];
                Array.Copy(bytes, guid, 16);
                returnVal.Id = new Guid(guid).ToString("N");
#elif WINDOWS_UWP
                var deviceInformation = new Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation();
                returnVal.Id = deviceInformation.Id.ToString("N");
#else
                returnVal.Id = Guid.NewGuid().ToString("N");
#endif
            }
            catch (Exception e)
            {
                logger.Log("Error while getting device GUID: " + e.Message, LogLevel.Wrn);
                returnVal.Id = Guid.NewGuid().ToString("N");
            }

#if WINDOWS_PHONE_APP
            if (Windows.System.UserProfile.UserInformation.NameAccessAllowed)
            {
                returnVal.FullName = await Windows.System.UserProfile.UserInformation.GetDisplayNameAsync();
            }
            else
            {
                returnVal.FullName = "Unknown user";
            }
#elif WINDOWS_UWP
            returnVal.FullName = "Unknown user";
            await DurandalTaskExtensions.NoOpTask;
#else
            returnVal.FullName = "Unknown user";
            await DurandalTaskExtensions.NoOpTask;
#endif

            return returnVal;
        }

        public static async Task<ClientIdentity> GetBuiltInClientIdentity(ILogger logger)
        {
            await DurandalTaskExtensions.NoOpTask;
            ClientIdentity returnVal = new ClientIdentity();
            
            // Try and generate a guid for this device to use as clientId
            try
            {
#if WINDOWS_PHONE_APP
                var token = Windows.System.Profile.HardwareIdentification.GetPackageSpecificToken(null);
                byte[] bytes = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(token.Id);
                byte[] guid = new byte[16];
                Array.Copy(bytes, guid, 16);
                returnVal.Id = new Guid(guid).ToString("N");
#elif WINDOWS_UWP
                var deviceInformation = new Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation();
                returnVal.Id = deviceInformation.Id.ToString("N");
#else
                returnVal.Id = Guid.NewGuid().ToString("N");
#endif
            }
            catch (Exception e)
            {
                logger.Log("Error while getting device GUID: " + e.Message, LogLevel.Wrn);
                returnVal.Id = Guid.NewGuid().ToString("N");
            }
            
            try
            {
                var hardwareInfo = new Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation().FriendlyName;
                returnVal.Name = hardwareInfo;
            }
            catch (Exception e)
            {
                logger.Log("Error while getting device friendly name: " + e.Message, LogLevel.Wrn);
#if WINDOWS_PHONE_APP
                returnVal.Name = "Windows Phone 8.1";
#elif WINDOWS_UWP
                returnVal.Name = "Windows 10";
#else
                returnVal.Name = "Windows";
#endif
            }

            return returnVal;
        }

        private async Task SetDefaultConfig()
        {
            UserIdentity defaultUser = await GetBuiltInUserIdentity(_coreLogger);
            ClientIdentity defaultClient = await GetBuiltInClientIdentity(_coreLogger);

            if (!_clientConfig.GetBase().ContainsKey("clientId") || string.IsNullOrEmpty(_clientConfig.ClientId))
                _clientConfig.ClientId = defaultClient.Id;
            if (!_clientConfig.GetBase().ContainsKey("clientName") || string.IsNullOrEmpty(_clientConfig.ClientName))
                _clientConfig.ClientName = defaultClient.Name;
            if (!_clientConfig.GetBase().ContainsKey("userId") || string.IsNullOrEmpty(_clientConfig.UserId))
                _clientConfig.UserId = defaultUser.Id;
            if (!_clientConfig.GetBase().ContainsKey("userName") || string.IsNullOrEmpty(_clientConfig.UserName))
                _clientConfig.UserName = defaultUser.FullName;
            
            if (!_clientConfig.GetBase().ContainsKey("remoteDialogServerAddress") || _clientConfig.RemoteDialogServerAddress == null)
                _clientConfig.RemoteDialogServerAddress = new Uri("https://durandal-ai.net:62292");

            if (!_clientConfig.GetBase().ContainsKey("authenticationEndpoint") || _clientConfig.AuthenticationEndpoint == null)
                _clientConfig.AuthenticationEndpoint = new Uri("https://durandal-ai.net:443");

            if (!_clientConfig.GetBase().ContainsKey("locale") || string.IsNullOrEmpty(_clientConfig.Locale))
            {
                string userDisplayLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
                if (userDisplayLanguage.Equals("en"))
                {
                    _clientConfig.Locale = "en-us";
                }
                else if (userDisplayLanguage.Equals("es"))
                {
                    _clientConfig.Locale = "es-es";
                }
                else if (userDisplayLanguage.Equals("zh"))
                {
                    _clientConfig.Locale = "zh-cn";
                }
                else if (userDisplayLanguage.Equals("fr"))
                {
                    _clientConfig.Locale = "fr-fr";
                }
                else if (userDisplayLanguage.Equals("de"))
                {
                    _clientConfig.Locale = "de-de";
                }
                else if (userDisplayLanguage.Equals("pt"))
                {
                    _clientConfig.Locale = "pt-br";
                }
                else
                {
                    _clientConfig.Locale = "xx-xx";
                }
            }

            if (!_clientConfig.GetBase().ContainsKey("triggerPhrase") || string.IsNullOrEmpty(_clientConfig.TriggerPhrase))
                _clientConfig.TriggerPhrase = "durandal";
            if (!_clientConfig.GetBase().ContainsKey("primaryTriggerSensitivity"))
                _clientConfig.PrimaryAudioTriggerSensitivity = 5;
            if (!_clientConfig.GetBase().ContainsKey("secondaryTriggerSensitivity"))
                _clientConfig.SecondaryAudioTriggerSensitivity = 5;
            if (!_clientConfig.GetBase().ContainsKey("triggerEnabled"))
                _clientConfig.TriggerEnabled = false;
            if (!_clientConfig.GetBase().ContainsKey("remoteSpeechRecoAddress") || _clientConfig.RemoteSpeechRecoAddress == null)
                _clientConfig.RemoteSpeechRecoAddress = new Uri("sr://durandal-ai.net:62298");
            if (!_clientConfig.GetBase().ContainsKey("srProvider") || string.IsNullOrEmpty(_clientConfig.SRProvider))
                _clientConfig.SRProvider = "azure";

            /// ### Hardcoded values that can't be configured yet ### ///
            _clientConfig.StreamingAudioPrebufferTime = TimeSpan.FromMilliseconds(500);
            _clientConfig.AudioCodec = "opus";
            _clientConfig.MicrophonePreamp = 1.0f;
            _clientConfig.SRApiKey = "region=westus2;key=d75f2bddd7e84c7a8c94cf1a1e79fad0"; // let's just hope that this never needs to be changed
        }
        
        private async Task StartBackend(CoreDispatcher uiThreadDispatcher)
        {
#if WINDOWS_PHONE_APP
            string mainComponentName = "WP8Client";
#elif WINDOWS_UWP
            string mainComponentName = "W10Client";
#else
            string mainComponentName = "WindowsClient";
#endif
            _realTimeProvider = DefaultRealTimeProvider.Singleton;

#if DEBUG
            string instrumentationStreamName = "Dev";
            LogLevel validLogLevels = LogLevel.All;
#else
            string instrumentationStreamName = "Prod";
            LogLevel validLogLevels = LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Ins;
#endif
            RemoteInstrumentationLogger instrumentation = new RemoteInstrumentationLogger(
                new PortableHttpClient("durandal-ai.net", 62295, NullLogger.Singleton, false),
                new InstrumentationBlobSerializer(),
                _realTimeProvider,
                instrumentationStreamName,
                false,
                componentName: mainComponentName,
                validLogLevels: validLogLevels);
            IThreadPool backgroundLoggerPool = new TaskThreadPool();

#if DEBUG
            _coreLogger = new AggregateLogger(mainComponentName, backgroundLoggerPool, null, new DebugLogger(mainComponentName, LogLevel.All, false), instrumentation, new EventOnlyLogger(mainComponentName));
#else
            _coreLogger = new AggregateLogger(mainComponentName, backgroundLoggerPool, null, instrumentation, new EventOnlyLogger(mainComponentName));
#endif
            _coreLogger.Log("My version number is " + SVNVersionInfo.AssemblyVersion);
            _localAssetsFilesystem = new WinRTFileStorage(Windows.ApplicationModel.Package.Current.InstalledLocation, _coreLogger.Clone("LocalAssetFiles"));
            _userDataFilesystem = new WinRTFileStorage(ApplicationData.Current.LocalFolder, _coreLogger.Clone("UserDataFiles"));
            _clientConfig = new ClientConfiguration(new WindowsLocalConfiguration(_coreLogger.Clone("Configuration")));

            await SetDefaultConfig();

            // Initialize a random trace ID for the initialization steps.
            // Formerly this would set it to be the client's client ID, but that caused lots of problems / ineffeciencies in the instrumentation
            // pipeline. Having separate events for each client init separates the concerns better
            _coreLogger = _coreLogger.CreateTraceLogger(Guid.NewGuid());

            // Start getting GPS coordinates in the background
            _geolocator = new Geolocator();
            _geolocator.DesiredAccuracyInMeters = 25;
            _geolocator.MovementThreshold = 25;
            _geolocator.PositionChanged += GpsPositionUpdated;
            
            Stream wavFileStream = await _localAssetsFilesystem.OpenStreamAsync(new VirtualPath("\\Assets\\Prompt.raw"), FileOpenMode.Open, Durandal.Common.File.FileAccessMode.Read);
            _promptSound = AudioChunkFactory.CreateFromRawStream(wavFileStream, 16000);
            wavFileStream = await _localAssetsFilesystem.OpenStreamAsync(new VirtualPath("\\Assets\\Fail.raw"), FileOpenMode.Open, Durandal.Common.File.FileAccessMode.Read);
            _failSound = AudioChunkFactory.CreateFromRawStream(wavFileStream, 16000);
            wavFileStream = await _localAssetsFilesystem.OpenStreamAsync(new VirtualPath("\\Assets\\Confirm.raw"), FileOpenMode.Open, Durandal.Common.File.FileAccessMode.Read);
            _successSound = AudioChunkFactory.CreateFromRawStream(wavFileStream, 16000);

            _audioCodec = new OpusAudioCodec(_coreLogger.Clone("OpusAudio"));
            _httpClientFactory = new PortableHttpClientFactory();

            _actionDispatcher = new JsonClientActionDispatcher();
            _actionDispatcher.AddHandler(new StopListeningActionHandler());
            _actionDispatcher.AddHandler(new SendNextTurnAudioActionHandler());
            _actionDispatcher.AddHandler(new OAuthLoginActionHandlerWinRT(uiThreadDispatcher));

            _delayedActionHandler = new ExecuteDelayedActionHandler();
            _delayedActionHandler.ExecuteAction += ExecuteDelayedDialogAction;
            _actionDispatcher.AddHandler(_delayedActionHandler);

            ISocketServer socketServerBase = new WinRTSocketServer(62293, _coreLogger.Clone("SocketServerBase"), new TaskThreadPool());
            IHttpServer httpServerBase = new HttpSocketServer(socketServerBase, _coreLogger.Clone("HttpServerBase"));

            _webServer = new PresentationWebServer(
                httpServerBase,
                _coreLogger.Clone("PresentationWebServer"));
            await _webServer.Start();

            _privateKeyStore = new FileBasedClientKeyStore(_userDataFilesystem, _coreLogger.Clone("ClientKeyStore"));

            _coreLogger.Log("I am initializing the core for the first time");
            await InitializeCoreInternal();

            _appInitFinishedSignal.Set();
        }
        
        private async Task InitializeSphinx(bool sphinxEnabled, string triggerPhrase, double sensitivity)
        {
            if (sphinxEnabled)
            {
                KeywordSpottingConfiguration keywordConfig = new KeywordSpottingConfiguration()
                {
                    PrimaryKeyword = triggerPhrase,
                    PrimaryKeywordSensitivity = sensitivity
                };

                if (_sphinxTrigger == null)
                {
                    _coreLogger.Log("Initializing sphinx trigger...");
                    ILogger sphinxLogger = _coreLogger.Clone("Pocketsphinx");
#if WINDOWS_PHONE_APP && ARM
                    await DurandalTaskExtensions.NoOpTask;
                    // Native sphinx for WP8
                    _sphinxTrigger = new SphinxAudioTrigger(
                        new PocketSphinxAdapterWinRT(),
                        sphinxLogger,
                        "\\Assets\\Sphinx\\en-us-semi",
                        "\\Assets\\Sphinx\\sphinx.dict",
                        keywordConfig,
#if DEBUG
                        true);
#else
                        false);
#endif

#else // !WINDOWS_PHONE_APP
                    // Portable sphinx for UWP and others
                    Stream sphinxPackStream = await _localAssetsFilesystem.OpenStreamAsync(new VirtualPath("\\Assets\\sphinx.pack"), FileOpenMode.Open, Durandal.Common.File.FileAccessMode.Read);
                    InMemoryFileSystem sphinxDataPack = InMemoryFileSystem.Deserialize(sphinxPackStream, false);
                    _sphinxTrigger = new SphinxAudioTrigger(
                        new PortablePocketSphinx(sphinxDataPack, sphinxLogger),
                        sphinxLogger,
                        "en-us-semi",
                        "cmudict_SPHINX_40.txt",
                        keywordConfig,
#if DEBUG
                        true);
#else
                        false);
#endif
#endif // !WINDOWS_PHONE_APP

#if DEBUG
                    _sphinxTrigger.Initialize();
#else
                    // We initialize the actual resources in the background here since it can take a while to load
#pragma warning disable CS4014
                    DurandalTaskExtensions.LongRunningTaskFactory.StartNew(() =>
                    {
                        try
                        {
                            _sphinxTrigger.Initialize();
                        }
                        catch (Exception e)
                        {
                            _coreLogger.Log(e, LogLevel.Err);
                        }
                    });
#pragma warning restore CS4014
#endif
                }
                else
                {
                    _coreLogger.Log("Reconfiguring sphinx...");
                    _sphinxTrigger.Configure(keywordConfig);
                }
            }
            else
            {
                // This path is executed if the trigger was previously initialized but then disabled in settings
                if (_sphinxTrigger != null)
                {
                    _coreLogger.Log("Sphinx has been disabled; freeing resources...");
                    _sphinxTrigger.Dispose();
                    _sphinxTrigger = null;
                }
            }
        }

        public async Task Reinitialize()
        {
            int hLock = await _coreLock.EnterWriteLockAsync();
            try
            {
                await InitializeCoreInternal();
            }
            finally
            {
                _coreLock.ExitWriteLock(hLock);
            }
        }

        private async Task InitializeCoreInternal()
        {
            if (_core != null)
            {
                // Disconnect actions from core
                _core.MakingRequest -= _delayedActionHandler.ResetFromClientActivity;
                _webServer.UserInteraction -= HtmlPageInteractionHandler;
                _core.Dispose();
            }

            // Init record and playback.
#if WINDOWS_UWP
            _microphone = new UWPMicrophone(_coreLogger.Clone("UWPMic"), AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE, _clientConfig.MicrophonePreamp);
            _audioDevice = await UWPAudioPlayer.Create(_coreLogger.Clone("UWPSpeaker"), AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE);
#else
            // We assume that there's only 1 interface for each on a phone,
            // so just use the auto device id
            _microphone = new BassMicrophone(_coreLogger.Clone("BassMic"), -1, AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE, _clientConfig.MicrophonePreamp);
            _audioPlayer = new BassAudioPlayer(_coreLogger.Clone("BassAudio"), -1, AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE);
#endif

            _audioMixer = new BasicAudioMixer(_coreLogger.Clone("Mixer"));
            _audioDevice.SampleProvider = _audioMixer;

            bool useSynchronousSocketReadHack = false;
#if WINDOWS_PHONE_APP
            useSynchronousSocketReadHack = true;
#endif
            _speechRecoSocketFactory = new WinRTSocketFactory(_coreLogger.Clone("SRSocket"), SocketProtectionLevel.Tls12, SocketQualityOfService.LowLatency, useSynchronousSocketReadHack);

            if (string.Equals("remote", _clientConfig.SRProvider))
            {
                string speechRecoHost = _clientConfig.RemoteSpeechRecoAddress.Host;
                int speechRecoPort = _clientConfig.RemoteSpeechRecoAddress.Port > 0 ? _clientConfig.RemoteSpeechRecoAddress.Port : 62290;
                _speechReco = new RemoteSpeechRecognizerFactory(
                    _audioCodec,
                    _speechRecoSocketFactory,
                    new TaskThreadPool(),
                    speechRecoHost,
                    speechRecoPort,
                    _coreLogger.Clone("RemoteSpeechReco"));
            }
            else if (string.Equals("cortana", _clientConfig.SRProvider))
            {
                _speechReco = new CortanaSpeechRecognizerFactory(
                    _speechRecoSocketFactory,
                    _coreLogger.Clone("CortanaSpeechReco"),
                    _clientConfig.SRApiKey,
                    _realTimeProvider);
            }
            else if (string.Equals("azure", _clientConfig.SRProvider))
            {
                _speechReco = new AzureSpeechRecognizerFactory(
                    _httpClientFactory,
                    _speechRecoSocketFactory,
                    _coreLogger.Clone("AzureSpeechReco"),
                    _clientConfig.SRApiKey,
                    _realTimeProvider);
            }
            else
            {
                _speechReco = new NullSpeechRecoFactory();
            }

            await InitializeSphinx(_clientConfig.TriggerEnabled,
                _clientConfig.TriggerPhrase,
                _clientConfig.PrimaryAudioTriggerSensitivity);

            if (_clientConfig.RemoteDialogServerAddress == null)
            {
                _clientConfig.RemoteDialogServerAddress = new Uri("https://durandal-ai.net:62292");
            }

            _dialogHttpConnection = _httpClientFactory.CreateHttpClient(
                _clientConfig.RemoteDialogServerAddress,
                _coreLogger.Clone("DialogHttpBase"));

            IDialogTransportProtocol protocol;
#if WINDOWS_UWP
            protocol = new DialogLZ4JsonTransportProtocol(); // FIXME: REIMPLEMENT BOND PROTOCOL
#elif WINDOWS_PHONE_APP
            protocol = new DialogLZ4JsonTransportProtocol();
#else
            protocol = new DialogJsonTransportProtocol();
#endif

            _dialogClient = new DialogHttpClient(_dialogHttpConnection, _coreLogger.Clone("DialogHttp"), protocol);

            HttpTriggerArbitrator triggerArbitrator = null;

            if (_clientConfig.TriggerArbitratorUrl != null &&
                !string.IsNullOrEmpty(_clientConfig.TriggerArbitratorGroupName))
            {
                triggerArbitrator = new HttpTriggerArbitrator(
                    new PortableHttpClient(_clientConfig.TriggerArbitratorUrl, _coreLogger.Clone("TriggerArbitrator")),
                    TimeSpan.FromMilliseconds(500),
                    _clientConfig.TriggerArbitratorGroupName);
            }

#if WINDOWS_PHONE_APP
            // There is a really weird bug where a PortableHttpClient used in an authentication context will apparently return the results of previous calls (cached results?)
            // even though that makes no sense. So we work around it by using a sockethttp client
            ISocketFactory loginSocketFactory = new WinRTSocketFactory(_coreLogger.Clone("LoginSocketFactory"), SocketProtectionLevel.Tls12, SocketQualityOfService.Normal, true);
            IHttpClientFactory loginHttpClientFactory = new HttpSocketClientFactory(loginSocketFactory, _realTimeProvider);
#else
            IHttpClientFactory loginHttpClientFactory = _httpClientFactory;
#endif
            if (ClientConfig.AuthenticationEndpoint == null)
            {
                ClientConfig.AuthenticationEndpoint = new Uri("https://durandal-ai.net:443");
            }

            List <ILoginProvider> loginProviders = new List<ILoginProvider>();
            loginProviders.Add(AdhocLoginProvider.BuildForClient(loginHttpClientFactory, _coreLogger.Clone("AdhocLoginProvider"), ClientConfig.AuthenticationEndpoint));
            loginProviders.Add(MSAPortableLoginProvider.BuildForClient(loginHttpClientFactory, _coreLogger.Clone("MSAPortableLoginProvider"), ClientConfig.AuthenticationEndpoint));

            _coreLogger.Log("Starting Durandal core");
            
            _core = new ClientCore();
            ClientCoreParameters coreParams = new ClientCoreParameters(_clientConfig, BuildClientContext)
            {
                Logger = _coreLogger.Clone("ClientCore"),
                Microphone = _microphone,
                Speakers = _audioMixer,
                AudioTrigger = _sphinxTrigger,
                SpeechReco = _speechReco,
                Codec = _audioCodec,
                EnableRSA = true,
                DialogConnection = _dialogClient,
                ClientActionDispatcher = _actionDispatcher,
                LocalPresentationLayer = _webServer,
                LocalHtmlRenderer = new ClientSideHtmlRenderer(),
                AudioTriggerArbitrator = triggerArbitrator,
                LoginProviders = loginProviders,
                PrivateKeyStore = _privateKeyStore
            };

            await _core.Initialize(coreParams);
                
            // Connect actions to core
            _core.MakingRequest += _delayedActionHandler.ResetFromClientActivity;
            _webServer.UserInteraction += HtmlPageInteractionHandler;
        }
        
        private void HtmlPageInteractionHandler(object sender, TimeSpanEventArgs lingerEventArgs)
        {
            _core.OnLinger(lingerEventArgs.Time, DefaultRealTimeProvider.Singleton, _coreLogger);
        }

        public async Task WaitForAppInit()
        {
            while (!_appInitFinishedSignal.Wait(1))
            {
                await Task.Delay(10);
            }
        }

        private static object DispatcherLock = new object();
        
        private void GpsPositionUpdated(Geolocator source, PositionChangedEventArgs args)
        {
            if (args.Position != null && source.LocationStatus == PositionStatus.Ready)
            {
                _userCurrentLocation = args.Position;
                //_coreLogger.Log("Refreshed user location from GPS");
            }
        }

        public void PlaySuccessSound()
        {
            _audioMixer.PlaySound(_successSound);
        }

        public void PlayFailSound()
        {
            _audioMixer.PlaySound(_failSound);
        }

        public void PlayPromptSound()
        {
            _audioMixer.PlaySound(_promptSound);
        }

        /// <summary>
        /// Stops all currently playing audio sources, but does not
        /// permanently stop the audio engine.
        /// </summary>
        public void SilenceAudio()
        {
            _audioMixer.StopPlaying();
        }

        /// <summary>
        /// Executed when the client action processor detects that we need to execute a delayed dialog action
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public async void ExecuteDelayedDialogAction(object sender, DialogActionEventArgs args)
        {
            _coreLogger.Log("I am executing a delayed action and the ID is " + args.ActionId);
            await _core.TryMakeDialogActionRequest(args.ActionId, args.InteractionMethod);
        }

        public ClientContext BuildClientContext()
        {
            ClientContext returnVal = new ClientContext();
            returnVal.ClientId = _clientConfig.ClientId;
            returnVal.UserId = _clientConfig.UserId;
            returnVal.Locale = "en-us";
            returnVal.ClientName = _clientConfig.ClientName;

            if (_userCurrentLocation != null && _userCurrentLocation.Coordinate != null)
            {
                returnVal.Latitude = _userCurrentLocation.Coordinate.Point.Position.Latitude;
                returnVal.Longitude = _userCurrentLocation.Coordinate.Point.Position.Longitude;
                returnVal.LocationAccuracy = _userCurrentLocation.Coordinate.Accuracy;
            }

            returnVal.ReferenceDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            returnVal.UTCOffset = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes;
            returnVal.Capabilities = (
                ClientCapabilities.DisplayHtml5 |
                ClientCapabilities.DisplayUnlimitedText |
                ClientCapabilities.HasMicrophone |
                ClientCapabilities.HasSpeakers |
                ClientCapabilities.HasInternetConnection |
                ClientCapabilities.KeywordSpotter |
                ClientCapabilities.DynamicHtml |
                ClientCapabilities.JavascriptExtensions |
                ClientCapabilities.RsaEnabled |
                ClientCapabilities.ClientActions |
                ClientCapabilities.ServeHtml |
                ClientCapabilities.SupportsStreamingAudio |
                ClientCapabilities.SupportsCompressedAudio);

            returnVal.ExtraClientContext[ClientContextField.FormFactor] = FormFactor.Portable.ToString();

#if WINDOWS_PHONE_APP
            returnVal.ExtraClientContext[ClientContextField.ClientType] = "WP_8.1";
#elif WINDOWS_UWP
            returnVal.ExtraClientContext[ClientContextField.ClientType] = "WIN_10";
#else
            returnVal.ExtraClientContext[ClientContextField.ClientType] = "WINDOWS";
#endif
            returnVal.ExtraClientContext[ClientContextField.ClientVersion] = SVNVersionInfo.VersionString;
            
            return returnVal;
        }

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

            Durandal.Common.Utils.DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _core.Dispose();
                _sphinxTrigger.Dispose();
                _speechRecoSocketFactory.Dispose();
            }
        }
    }
}
