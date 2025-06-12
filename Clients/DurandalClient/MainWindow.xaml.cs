using System;
using Durandal.API.Data;


namespace Durandal.Client
{
    using Common.Audio.Codecs;
    using Common.Speech;
    using Common.Speech.SR;
    using Common.Speech.SR.Cortana;
    using Common.Speech.SR.Remote;
    using Common.Speech.Triggers;
    using Common.Speech.TTS;
    using Common.Speech.TTS.Bing;
    using Common.Speech.Triggers.Sphinx;
    using Common.Utils;
    using Common.Utils.Tasks;
    using Durandal.API;
    using Durandal.API.Utils;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Interfaces;
    using Durandal.Common.Audio.NAudio;
    using Durandal.Common.Client;
    using Durandal.Common.Config;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Utils.IO;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using Common.Net.Http;
    using BondProtocol;
    using Common.Dialog.Web;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ClientCore _core;

        private IResourceManager _resourceManager;
        private ILogger _coreLogger;
        private ClientConfiguration _clientConfig;
        private IDialogTransportProtocol _dialogProtocol;
        private PresentationWebServer _clientWebServer;
        private float clientLatitude;
        private float clientLongitude;
        private string _clientId;
        private string _userId;
        private string _clientName;
        private long _lastTimeMicButtonPressed;
        
        private IAudioPlayer _audioOut;
        private IMicrophone _audioIn;
        private AudioChunk _successSound;
        private AudioChunk _failSound;
        private AudioChunk _skipSound;
        private AudioChunk _promptSound;
        private CodecCollection _codecCollection;

        private const string PLACEHOLDER_TEXT = "ask me anything";
        private const int MIC_BUTTON_HOLD_DOWN_THRESHOLD = 700;

        private delegate void GUITextDelegate(string input);

        private BrowserScriptInterface _javascriptInterface;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string debugString = string.Empty;
#if DEBUG
            debugString = " (DEBUG)";
#endif

            this.Title = string.Format("Durandal Client {0}{1}", SVNVersionInfo.VersionString, debugString);
            this.inputTextBox.Text = PLACEHOLDER_TEXT;

            await StartBackend();
        }

        private async Task StartBackend()
        {
            _coreLogger = new ConsoleAndFileLogger("DurandalClient");
            _resourceManager = new FileResourceManager(_coreLogger);
            _coreLogger.Log(string.Format("Durandal Client {0} built on {1}", SVNVersionInfo.VersionString, SVNVersionInfo.BuildDate));
            DurandalUtils.SetLogger(_coreLogger.Clone("DurandalUtils"));
            Configuration configFile = new IniFileConfiguration(_coreLogger.Clone("PrimaryConfig"), new ResourceName("DurandalClient_config"), _resourceManager, true);
            _clientConfig = new ClientConfiguration(configFile);
            _dialogProtocol = new DialogBondTransportProtocol();

            // Register events for the mic button. For some reason Xaml does not fire the regular MouseUp and MouseDown so we have to override them
            MouseCommandBehavior.SetMouseDownCommand(microphoneButton, new MouseEventHandler(StartAudioInteraction));
            MouseCommandBehavior.SetMouseUpCommand(microphoneButton, new MouseEventHandler(EarlyTerminateAudioInteraction));

            // Set the scripting interface for the browser control (so the javascript API can operate)
            _javascriptInterface = new BrowserScriptInterface(_coreLogger);
            canvas.ObjectForScripting = _javascriptInterface;
            canvas.LoadCompleted += BrowserLoadCompleted;

            // Check for existing client id. If not found, create a new one and save it back to the config
            if (string.IsNullOrEmpty(_clientConfig.ClientId))
            {
                _coreLogger.Log("No client ID found, generating a new value " + _clientId);
                ConfigValue newClientId = new ConfigValue("clientId", StringUtils.HashToGuid(Environment.MachineName).ToString("N"), ConfigValueType.String);
                newClientId.Annotations.Add(new DescriptionAnnotation("The unique ID to identify this client. If this value is removed, a new one will be generated."));
                _clientConfig.GetBase().Set(newClientId);
            }

            _clientId = _clientConfig.ClientId;

            if (string.IsNullOrEmpty(_clientConfig.UserId))
            {
                _userId = _clientId;
                _coreLogger.Log("No user ID found, using clientId " + _clientId);
                ConfigValue newUserId = new ConfigValue("userId", _clientId, ConfigValueType.String);
                newUserId.Annotations.Add(new DescriptionAnnotation("The unique ID to identify the user of this device. If this value is removed, it will be set to clientId"));
                _clientConfig.GetBase().Set(newUserId);
            }
            else
            {
                _userId = _clientConfig.UserId;
            }

            if (string.IsNullOrEmpty(_clientConfig.ClientName))
            {
                _clientName = Dns.GetHostName();
                _coreLogger.Log("No client name found, using machine name \"" + _clientName + "\" instead...");

                ConfigValue newClientName = new ConfigValue("clientName", _clientName, ConfigValueType.String);
                newClientName.Annotations.Add(new DescriptionAnnotation("A human-readable name for this client. By default, this is the local machine name."));
                newClientName.Annotations.Add(new GUIAnnotation());
                _clientConfig.GetBase().Set(newClientName);
            }
            else
            {
                _clientName = _clientConfig.ClientName;
            }

            Stopwatch t = Stopwatch.StartNew();
            Task geocodeTask = GetGeoIPCoords();
            t.Stop();
            _coreLogger.Log("GeoIP " + t.ElapsedMilliseconds);

            _audioOut = new DirectSoundPlayer(_clientConfig.SpeakerSampleRate);
            using (FileStream readStream = new FileStream(".\\data\\Fail.wav", FileMode.Open, FileAccess.Read))
            {
                _failSound = AudioChunkFactory.CreateFromWavStream(readStream);
            }
            using (FileStream readStream = new FileStream(".\\data\\Confirm.wav", FileMode.Open, FileAccess.Read))
            {
                _successSound = AudioChunkFactory.CreateFromWavStream(readStream);
            }
            using (FileStream readStream = new FileStream(".\\data\\Fail.wav", FileMode.Open, FileAccess.Read))
            {
                _skipSound = AudioChunkFactory.CreateFromWavStream(readStream);
            }
            using (FileStream readStream = new FileStream(".\\data\\Prompt.wav", FileMode.Open, FileAccess.Read))
            {
                _promptSound = AudioChunkFactory.CreateFromWavStream(readStream);
            }

            _codecCollection = new CodecCollection(_coreLogger.Clone("CodecCollection"));
            _codecCollection.RegisterCodec(new SquareDeltaCodec());
            _codecCollection.RegisterCodec(new OpusAudioCodec(_coreLogger.Clone("OpusCodec")));
            _codecCollection.RegisterCodec(new ILBCAudioCodec(_coreLogger.Clone("iLBCCodec")));
            _codecCollection.RegisterCodec(new ALawAudioCodec());
            _codecCollection.RegisterCodec(new uLawAudioCodec());
            _codecCollection.RegisterCodec(new G722AudioCodec());

            int inputSampleRate = _clientConfig.MicrophoneSampleRate;

            _audioIn = new NAudioMicrophone(inputSampleRate, _clientConfig.MicrophonePreamp);

            List<string> localPresentationEndpoints = new List<string>();
            localPresentationEndpoints.Add("http://localhost:" + _clientConfig.LocalPresentationServerPort);

            ISocketServer socketServerBase = new Win32SocketServer(
                localPresentationEndpoints,
                _coreLogger.Clone("SocketServerBase"),
                new SystemThreadPool(_coreLogger.Clone("HttpSocketThreadPool")));

            IHttpServer httpServerBase = new HttpSocketServer(socketServerBase, _coreLogger.Clone("HttpServerBase"));
            
            _clientWebServer = new PresentationWebServer(
                httpServerBase,
                _coreLogger.Clone("PresentationWebServer"));

            _core = new ClientCore();
            
            IAudioTrigger clientTrigger = new NullAudioTrigger();
            if (_clientConfig.TriggerEnabled)
            {
                _coreLogger.Log("Initializing audio trigger...");

                KeywordSpottingConfiguration spottingConfig = new KeywordSpottingConfiguration()
                {
                    PrimaryKeyword = _clientConfig.TriggerPhrase,
                    PrimaryKeywordSensitivity = _clientConfig.PrimaryAudioTriggerSensitivity,
                    SecondaryKeywordSensitivity = _clientConfig.SecondaryAudioTriggerSensitivity,
                    SecondaryKeywords = new List<string>()
                };

                clientTrigger = new SphinxAudioTrigger(
                    PocketSphinxAdapterPInvoke.GetPInvokeAdapterForPlatform(_coreLogger),
                    _coreLogger.Clone("Pocketsphinx"),
                    _clientConfig.PocketSphinxAmDirectory,
                    _clientConfig.PocketSphinxDictionaryFile,
                    spottingConfig,
                    true);
            }
            else
            {
                _coreLogger.Log("No audio trigger is configured");
            }

            ILogger httpClientLogger = _coreLogger.Clone("DialogHttpClient");
            ClientCoreParameters clientParameters = new ClientCoreParameters(_clientConfig,
                GenerateClientContext)
            {
                Logger = _coreLogger.Clone("ClientCore"),
                ResourceManager = _resourceManager,
                Microphone = _audioIn,
                LocalPresentationLayer = _clientWebServer,
                AudioTrigger = clientTrigger,
                DialogConnection = new DialogHttpClient(
                    new PortableHttpClient(_clientConfig.RemoteDialogServerAddress, _clientConfig.RemoteDialogServerPort, httpClientLogger),
                    httpClientLogger,
                    _dialogProtocol),
                LocalHtmlRenderer = new ClientSideHtmlRenderer()
            };

            string preferredAudioCodec = _clientConfig.AudioCodec;
            IAudioCodec candidateCodec = _codecCollection.TryGetAudioCodec(preferredAudioCodec);
            if (candidateCodec != null)
            {
                clientParameters.Codec = candidateCodec;
                _coreLogger.Log("Audio codec \"" + candidateCodec.GetFormatCode() + "\" successfully initialized");
            }

            Stopwatch srInitTimer = new Stopwatch();
            srInitTimer.Start();
            string srProvider = _clientConfig.SRProvider.ToLowerInvariant();
            if (srProvider.Equals("remote", StringComparison.OrdinalIgnoreCase))
            {
                //clientParameters.SpeechReco = new RemoteSpeechRecognizer(clientParameters.Codec,
                //    new WindowsSocketProvider(_clientConfig.RemoteSpeechRecoAddress, _clientConfig.RemoteSpeechRecoPort),
                //    _coreLogger.Clone("SpeechRecoProxy"));
            }
            else
            {
                clientParameters.SpeechReco = TryGetSpeechRecognizer(srProvider, _coreLogger, _clientConfig.SRApiKey, true, 1);
            }

            srInitTimer.Stop();
            _coreLogger.Log("SR provider initialized in " + srInitTimer.ElapsedMilliseconds + "ms", LogLevel.Std);

            string ttsProvider = _clientConfig.TTSProvider.ToLowerInvariant();
            clientParameters.SpeechSynth = TryGetSpeechSynth(ttsProvider, _coreLogger, _clientConfig.TTSApiKey, 1);
            _coreLogger.Log("TTS provider initialized");

            _core.NavigateUrl += OpenUrl;
            _core.Success += Success;
            _core.Fail += Fail;
            _core.Skip += Skip;
            _core.ShowErrorOutput += ShowErrorOutput;
            _core.ResponseReceived += ResponseReceived;
            _core.SpeechPrompt += SpeechPrompt;
            _core.SpeechCaptureFinished += SpeechFinished;
            _core.SpeechCaptureIntermediate += SpeechIntermediate;
            _core.UpdateQuery += UpdateQuery;
            _core.RetryEvent += Retry;
            _core.AudioTriggered += AudioTriggered;
            
            t.Restart();
            await _core.Initialize(clientParameters);
            t.Stop();
            _coreLogger.Log("Init " + t.ElapsedMilliseconds);
            t.Restart();
            await _core.Greet(GenerateClientContext());
            t.Stop();
            _coreLogger.Log("Greet " + t.ElapsedMilliseconds);
        }

        public void ShowErrorOutput(object sender, TextEventArgs args)
        {
            _coreLogger.Log("ERROR reported by dialog service: " + args.Text, LogLevel.Err);
        }

        public void OpenUrl(object sender, UriEventArgs args)
        {
            Dispatcher.Invoke(new GUITextDelegate(NavigateToUrl), args.Url.AbsoluteUri);
        }

        public void Success(object sender, EventArgs args)
        {
            // TODO On success, we will get the Success event, and then the server may have returned audio.
            // If we PlaySound with async = true, it will queue up the two sounds so they don't both play at once.
            // However, that's technically a race condition, so it may need a better design.
            _audioOut.PlaySound(_successSound);
        }

        public void Fail(object sender, EventArgs args)
        {
            _audioOut.PlaySound(_failSound);
        }

        public void Skip(object sender, EventArgs args)
        {
            _audioOut.PlaySound(_skipSound);
        }

        public void Retry(object sender, RetryEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.Text))
            {
                Dispatcher.Invoke(new GUITextDelegate(ChangeTextBox), args.Text);
            }

            _audioOut.PlaySound(_failSound);
            if (args.Audio != null)
            {
                _audioOut.PlaySound(args.Audio);
            }
            else if (args.StreamingAudio != null)
            {
                _audioOut.PlayStream(args.StreamingAudio);
            }
        }

        public void ResponseReceived(object sender, EventArgs args)
        {
            _coreLogger.Log("Got a response from dialog server");
            Dispatcher.Invoke(new GUITextDelegate(ChangeTextBox), string.Empty);
        }

        // Silence audio when the user triggers (this is typically needed in barge-in scenarios)
        public void AudioTriggered(object sender, EventArgs args)
        {
            _audioOut.StopPlaying();
        }

        public void SpeechPrompt(object sender, EventArgs args)
        {
            _coreLogger.Log("Speech Prompt Triggered...");
            _audioOut.StopPlaying();
            Dispatcher.Invoke(new GUITextDelegate(ChangeTextBox), "Listening...");
            _audioOut.PlaySound(_promptSound);
        }

        public void SpeechIntermediate(object sender, TextEventArgs args)
        {
            //_coreLogger.Log("Got intermediate speech reco result: " + args.Text);
            if (!string.IsNullOrEmpty(args.Text))
            {
                Dispatcher.Invoke(new GUITextDelegate(ChangeTextBox), args.Text);
            }
        }

        public void UpdateQuery(object sender, TextEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.Text))
            {
                _coreLogger.Log("Augmenting displayed query to new value: " + args.Text);
                Dispatcher.Invoke(new GUITextDelegate(ChangeTextBox), args.Text);
            }
        }

        public void SpeechFinished(object sender, SpeechCaptureEventArgs args)
        {
            if (args.Success)
            {
                _coreLogger.Log("Speech capture finished successfully");
                Dispatcher.Invoke(new GUITextDelegate(ChangeTextBox), args.Transcript);

                // Log the utterance to a file for debugging, etc.
                //args.Audio.WriteToFile(".\\data\\utterance_" + DateTime.Now.Ticks + ".wav");
            }
            else
            {
                _coreLogger.Log("Speech capture finished but did not record anything");
                Dispatcher.Invoke(new GUITextDelegate(ChangeTextBox), "Didn't hear anything");
                _audioOut.PlaySound(_skipSound);
            }
        }

        private void ChangeTextBox(string text)
        {
            inputTextBox.Text = text;
        }

        private void NavigateToUrl(string url)
        {
            _coreLogger.Log("Navigating to URL " + url);
            canvas.Source = new Uri(url);
        }

        private ClientContext GenerateClientContext()
        {
            ClientContext context = new ClientContext();
            context.SetCapabilities(ClientCapabilities.DisplayUnlimitedText |
                ClientCapabilities.DisplayBasicHtml |
                ClientCapabilities.HasInternetConnection |
                ClientCapabilities.HasSpeakers |
                ClientCapabilities.HasMicrophone |
                ClientCapabilities.SupportsCompressedAudio |
                ClientCapabilities.SupportsStreamingAudio |
                ClientCapabilities.RsaEnabled |
                ClientCapabilities.ServeHtml);
            context.ClientId = _clientId;
            context.UserId = _userId;

            // The locale code for the communication.
            context.Locale = "en-us";

            // Local client time
            context.ReferenceDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            context.UTCOffset = -8;

            // The common name of the client (to be used in dialog-side configuration, prettyprinting, etc.)
            context.ClientName = "Desktop client";

            // Client coordinates
            context.Latitude = clientLatitude;
            context.Longitude = clientLongitude;
            context.LocationAccuracy = 100;

            // Form factor code
            context.Data[ClientContextField.FormFactor] = FormFactor.Desktop.ToString();
            context.Data[ClientContextField.ClientType] = "WINDOWS_WIN32";
            context.Data[ClientContextField.ClientVersion] = SVNVersionInfo.VersionString;

            // Client screen information
            context.Data[ClientContextField.ScreenWidth] = canvas.ActualWidth.ToString();
            context.Data[ClientContextField.ScreenHeight] = canvas.ActualHeight.ToString();

            // Get any external data that may have been set from the javascript interface
            if (_javascriptInterface != null && _javascriptInterface.GetCustomRequestData() != null)
            {
                foreach (var kvp in _javascriptInterface.GetCustomRequestData())
                {
                    if (!context.Data.ContainsKey(kvp.Key))
                    {
                        context.Data.Add(kvp.Key, kvp.Value);
                    }
                }
            }

            return context;
        }

        private async Task GetGeoIPCoords()
        {
            // Default to Seattle if it's not set in the client config
            if (_clientConfig.GetBase().ContainsKey("clientLatitude") && _clientConfig.GetBase().ContainsKey("clientLongitude"))
            {
                clientLatitude = _clientConfig.ClientLatitude;
                clientLongitude = _clientConfig.ClientLongitude;
                _coreLogger.Log("Using lat/long from stored configuration: " + clientLatitude + " / " + clientLongitude);
            }
            else
            {
                _coreLogger.Log("No lat/long is stored in config! Querying geoip service to try and resolve location...");
                IHttpClient httpClient = new PortableHttpClient("geoip.prototypeapp.com", 80, _coreLogger.Clone("GeoIP"), false);
                NetworkResponseInstrumented<HttpResponse> response = await httpClient.SendRequestAsync(HttpRequest.BuildFromUrlString("/api/locate"));
                if (response == null || !response.Success || response.Response == null || response.Response.ResponseCode != 200)
                {
                    return;
                }

                string json = response.Response.GetPayloadAsString();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        JsonSerializer ser = JsonSerializer.Create(new JsonSerializerSettings());
                        JObject result = ser.Deserialize(new JsonTextReader(new StringReader(json))) as JObject;
                        if (result != null)
                        {
                            clientLatitude = result["location.coords.latitude"].Value<float>();
                            clientLongitude = result["location.coords.longitude"].Value<float>();

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
                }
            }
        }

        private void inputTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key.Equals(Key.Enter))
            {
                if (string.IsNullOrWhiteSpace(inputTextBox.Text))
                {
                    ResetPlaceholderText();
                }
                else
                {
                    string queryText = inputTextBox.Text;
                    ClientContext requestContext = this.GenerateClientContext();
                    requestContext.RemoveCapabilities(ClientCapabilities.HasSpeakers);
                    if (_core.TryMakeTextRequest(queryText, requestContext))
                    {
                        // Request was honored.
                        ChangeTextBox(string.Empty);
                    }
                    else
                    {
                        // Request was denied
                        _coreLogger.Log("Text request denied, as the client core is already engaged in another conversation",
                                        LogLevel.Wrn);
                    }
                }
            }
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            _core.Dispose();
            _audioOut.Dispose();

            // Sometimes things don't dispose cleanly (I need to fix that...) but just for safety, tear down everything if the program gets hung on shutdown
            Task.Run(() => { Thread.Sleep(1000); Environment.Exit(0); });
        }

        private void BrowserLoadCompleted(object sender, EventArgs e)
        {
            _javascriptInterface.ResetState();
        }

        private void inputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (PLACEHOLDER_TEXT.Equals(inputTextBox.Text))
            {
                inputTextBox.Text = string.Empty;
            }
        }

        private void inputTextBox_MouseEnter(object sender, MouseEventArgs e)
        {
            if (PLACEHOLDER_TEXT.Equals(inputTextBox.Text))
            {
                inputTextBox.Text = string.Empty;
            }
        }

        private void inputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ResetPlaceholderText();
        }

        private void inputTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ResetPlaceholderText();
        }

        private void ResetPlaceholderText()
        {
            if (string.IsNullOrWhiteSpace(inputTextBox.Text))
            {
                inputTextBox.Text = PLACEHOLDER_TEXT;
            }
        }

        private void StartAudioInteraction()
        {
            _lastTimeMicButtonPressed = DateTime.Now.Ticks;
            if (!_core.TryMakeAudioRequest(this.GenerateClientContext()))
            {
                _coreLogger.Log("Audio request denied, as the client core is already engaged in another conversation", LogLevel.Wrn);
            }
        }

        private void EarlyTerminateAudioInteraction()
        {
            // Has the user held the mic button for a while?
            long timeButtonHeld = (DateTime.Now.Ticks - _lastTimeMicButtonPressed) / 10000;
            if (timeButtonHeld > MIC_BUTTON_HOLD_DOWN_THRESHOLD)
            {
                // The user probably wants to hurry along the speech reco. Send the "force recognition" signal.
                _core.ForceRecordingFinish();
            }
        }

        /// <summary>
        /// Attempts to initialize a speech synthesizer using the given provider name
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static ISpeechSynth TryGetSpeechSynth(string providerName, ILogger logger, string apiKey, int maxPoolSize = 1)
        {
            logger.Log("Initializing TTS provider \"" + providerName + "\"...");
            if (providerName.Equals("bing", StringComparison.InvariantCultureIgnoreCase))
            {
                return new BingSpeechSynth(logger, apiKey, VoiceGender.Female);
            }
            else if (providerName.Equals("google", StringComparison.InvariantCultureIgnoreCase))
            {
                return new GoogleSpeechSynth(logger);
            }
            else
            {
                return new SapiSpeechSynth(logger, maxPoolSize);
            }
        }

        /// <summary>
        /// Attempts to initialize a speech recognizer using the given provider name
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static ISpeechRecognizerFactory TryGetSpeechRecognizer(string providerName, ILogger logger, string apiKey, bool enableIntermediateResults, int maxPoolSize = 1)
        {
            logger.Log("Initializing SR provider \"" + providerName + "\"...");
            if (providerName.Equals("cortana", StringComparison.InvariantCultureIgnoreCase))
            {
                return new CortanaSpeechRecognizerFactory(new TcpClientSocketFactory(logger.Clone("SRSocketFactory")), logger.Clone("CortanaSpeechReco"), apiKey);
            }
            else if (providerName.Equals("oxford", StringComparison.InvariantCultureIgnoreCase))
            {
                return new OxfordSpeechRecognizerFactory(logger.Clone("OxfordSpeechReco"), apiKey);
            }
            else if (providerName.Equals("bing", StringComparison.InvariantCultureIgnoreCase))
            {
                return new BingSpeechRecognizerFactory(logger.Clone("BingSpeechReco"), enableIntermediateResults);
            }
            else if (providerName.Equals("google", StringComparison.InvariantCultureIgnoreCase))
            {
                return new GoogleLegacySpeechRecognizerFactory(logger.Clone("GoogleSpeechReco"), enableIntermediateResults);
            }
            else
            {
                return new SapiSpeechRecognizerFactory(logger.Clone("SapiSpeechReco"));
            }
        }

        [ComVisible(true)]
        public class BrowserScriptInterface
        {
            private IDictionary<string, string> _nextRequestData = new Dictionary<string, string>();
            private ILogger _logger;
            private ILogger _javascriptLogger;

            public BrowserScriptInterface(ILogger logger)
            {
                _logger = logger.Clone("BrowserScriptInterface");
                _javascriptLogger = logger.Clone("ClientJavascript");
            }

            public IDictionary<string, string> GetCustomRequestData()
            {
                return _nextRequestData;
            }

            /// <summary>
            /// This should be called whenever the browser loads a new page
            /// </summary>
            public void ResetState()
            {
                _logger.Log("Reset the client script object state");
                _nextRequestData = new Dictionary<string, string>();
            }

            public bool IsDurandal() { return true; }
            
            public void UpdateRequestData(string key, string value)
            {
                if (_nextRequestData.ContainsKey(key))
                {
                    _nextRequestData.Remove(key);
                }

                _nextRequestData.Add(key, value);
            }

            public void LogMessage(string message)
            {
                _javascriptLogger.Log(message);
            }
        }
    }
}
