using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Durandal.Common.Client;
using Durandal.API;
using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Audio;
using Durandal.Common.Speech.SR.Remote;
using Durandal.Common.Speech.Triggers;
using Durandal.Common.Speech.Triggers.Sphinx;
using System.Collections.Generic;
using Durandal.Common.Logger;
using Durandal.Common.Utils.IO;
using Durandal.Common.Config;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Instrumentation;
using Durandal.Common.Net;
using Durandal.Common.Dialog;
using Durandal.API.Data;
using Durandal.Common.Utils.Tasks;
using Durandal.Common.Audio.BassAudio;
using Durandal.Common.Speech.SR.Cortana;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Durandal.Common.Speech.SR;
using Durandal.Common.Net.Http;
using System.Text;
using System.Diagnostics;
using ManagedBass;
using Durandal.BondProtocol;
using Durandal.Common.File;
using Durandal.Common.Security.Login;
using Durandal.Common.Security.Login.Providers;
using Durandal.Common.Security.Client;

#if RPI
using Raspberry.IO.GeneralPurpose;
#endif

namespace DurandalPiClient
{
    public class MainClass
    {
        private static AudioChunk _promptSound;
        private static AudioChunk _failSound;
        private static AudioChunk _successSound;

        private static ILogger _logger;
        private static ClientCore _client;
        private static IFileSystem _fileSystem;
        private static IMicrophone _mic;
        private static IAudioPlayer _audioOut;
        private static OpusAudioCodec _codec;
        private static ISpeechRecognizerFactory _speechReco;
		private static SphinxAudioTrigger _trigger;
        private static DateTime _lastButtonPress = default(DateTime);
        private static bool _buttonPressed = false;

        private static ClientConfiguration _clientConfig;

        public static void Main (string[] args)
        {
			//Bass.Load("."); // Override the linux libdl search path and just load from the current directory
			//SpeechRecoTest();

			string componentName = "LinuxClient";
            ConsoleLogger consoleLogger = new ConsoleLogger(componentName, LogLevel.All, false);
            RemoteInstrumentationLogger instrumentation = new RemoteInstrumentationLogger(
                new PortableHttpClient("durandal-ai.net", 62295, NullLogger.Singleton),
                new BondByteConverterInstrumentationEventList(),
                "Prod",
                false,
                componentName);
#if DEBUG
            instrumentation.StreamName = "Dev";
            instrumentation.ValidLevels = LogLevel.All;
            _logger = new AggregateLogger(componentName, consoleLogger, instrumentation);
#else
            instrumentation.StreamName = "Prod";
            instrumentation.ValidLevels = LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Ins;
            _logger = new AggregateLogger(componentName, consoleLogger, instrumentation);
#endif

            _fileSystem = new WindowsFileSystem(_logger, ".");

            _logger.Log("Configuring Mono...");
            ServicePointManager.ServerCertificateValidationCallback = DefaultCertificateValidationCallback;

            _logger.Log("Loading config...");
            Configuration baseConfig = IniFileConfiguration.Create(_logger, new VirtualPath("client_config"), _fileSystem, true).Await();
            _clientConfig = new ClientConfiguration(baseConfig);

            int micChannel = _clientConfig.MicrophoneDeviceId;
            int speakerChannel = _clientConfig.SpeakerDeviceId;
            int speakerSampleRate = _clientConfig.SpeakerSampleRate;
            string srProxy = _clientConfig.RemoteSpeechRecoAddress;
            string triggerPhrase = _clientConfig.TriggerPhrase;
            double triggerConfidence = _clientConfig.PrimaryAudioTriggerSensitivity;
            float micAmp = _clientConfig.MicrophonePreamp;

            _logger.Log("Loading audio data...");
            _promptSound = new AudioChunk(File.ReadAllBytes("Prompt.raw"), 16000);
            _failSound = new AudioChunk(File.ReadAllBytes("Fail.raw"), 16000);
            _successSound = new AudioChunk(File.ReadAllBytes("Confirm.raw"), 16000);

            _logger.Log("Initializing BASS audio backend...");
            _mic = new BassMicrophone(_logger, micChannel, 16000, micAmp, _clientConfig.MicrophoneSampleRate);
            _audioOut = new BassAudioPlayer(_logger, speakerChannel, speakerSampleRate);

            _logger.Log("Creating audio interfaces...");
            _codec = new OpusAudioCodec(_logger, 0); 
            _codec.QualityKbps = 48;

            string srApiKey = _clientConfig.SRApiKey;
			ISocketFactory srSocketFactory = new TcpClientSocketFactory(_logger.Clone("SRSocketFactory"), System.Security.Authentication.SslProtocols.Tls, true);
			_speechReco = new CortanaSpeechRecognizerFactory(srSocketFactory, _logger, srApiKey); 
            //_speechReco = new RemoteSpeechRecognizer(_codec, new WindowsSocketProvider(srProxy, srProxyPort), _logger);

            _logger.Log("Initializing keyword spotter...");
            KeywordSpottingConfiguration defaultSpotConfig = new KeywordSpottingConfiguration()
            {
                PrimaryKeyword = _clientConfig.TriggerPhrase,
                PrimaryKeywordSensitivity = _clientConfig.PrimaryAudioTriggerSensitivity,
                SecondaryKeywords = new List<string>(),
                SecondaryKeywordSensitivity = _clientConfig.SecondaryAudioTriggerSensitivity
            };

            _trigger = new SphinxAudioTrigger(
                PocketSphinxAdapterPInvoke.GetPInvokeAdapterForPlatform(_logger),
                _logger.Clone("PocketSphinx"),
                _clientConfig.PocketSphinxAmDirectory,
                _clientConfig.PocketSphinxDictionaryFile,
                defaultSpotConfig,
#if DEBUG
                true);
#else
                false);
#endif

			_trigger.Initialize();
            _logger.Log("Initializing Durandal client...");
            _client = InitializeClient(_logger, _clientConfig, _mic, _audioOut, _codec, _speechReco, _trigger, _fileSystem).Await();

            try
            {
#if RPI
                _logger.Log("Initializing GPIO...");
                IButton button = GPIOButton.Create(ConnectorPin.P1Pin40);
                if (button == null)
                {
                    _logger.Log("Could not initialize GPIO!", LogLevel.Err);
                    return;
                }
#else
                IButton button = new DummyGPIO();
#endif

                using (button)
                {
                    button.ButtonPressed += ButtonPressed;
                    button.ButtonReleased += ButtonReleased;

                    if (_clientConfig.GetBase().GetBool("daemon", false))
                    {
                        Console.WriteLine("Running in daemon mode. Disabling interactive console");
                        while (true)
                        {
                            Thread.Sleep(10000);
                        }
                    }
                    else
                    {
                        while (true)
                        {
                            Console.WriteLine("Waiting for GPIO button press... (Press Q to exit)");
                            if (Console.ReadKey().KeyChar == 'q')
                                break;
                        }
                    }
                }
            }
            finally
            {
                _logger.Log("Releasing audio resources...");
                _mic.Dispose();
                _audioOut.Dispose();
            }

            _logger.Log("Run finished");
        }

        private static bool DefaultCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            bool isOk = true;
            // If there are errors in the certificate chain, look at each error to determine the cause.
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                for (int i = 0; i < chain.ChainStatus.Length; i++)
                {
                    if (chain.ChainStatus[i].Status != X509ChainStatusFlags.RevocationStatusUnknown)
                    {
                        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                        chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                        bool chainIsValid = chain.Build((X509Certificate2)certificate);
                        if (!chainIsValid)
                        {
                            isOk = false;
                        }
                    }
                }
            }

            return isOk;
        }

        private static async Task<ClientCore> InitializeClient
            (ILogger logger,
            ClientConfiguration config,
            IMicrophone mic,
            IAudioPlayer speakers,
            IAudioCodec codec,
            ISpeechRecognizerFactory speechReco,
            IAudioTrigger trigger,
            IFileSystem fileSystem)
        {
            IDialogClient dialogClient = new DialogHttpClient(
                new PortableHttpClient(new Uri(config.RemoteDialogServerAddress), logger.Clone("DialogHttpClient")),
                logger.Clone("DialogClient"),
                new DialogBondTransportProtocol());

            IHttpClientFactory loginHttpClientFactory = new PortableHttpClientFactory();
            IList<ILoginProvider> loginProviders = new List<ILoginProvider>();
            Uri authUri = new Uri("https://durandal-ai.net");
            loginProviders.Add(AdhocLoginProvider.BuildForClient(loginHttpClientFactory, logger.Clone("AdhocLogin"), authUri));
            loginProviders.Add(MSAPortableLoginProvider.BuildForClient(loginHttpClientFactory, logger.Clone("MsaLogin"), authUri));

            IClientSideKeyStore privateKeyStore = new FileBasedClientKeyStore(fileSystem, logger.Clone("ClientKeyStore"));

            ClientCoreParameters clientParams = new ClientCoreParameters(config, GenerateClientContext)
            {
                Logger = _logger.Clone("ClientCore"),
                ResourceManager = _fileSystem,
                Microphone = mic,
                Speakers = speakers,
                AudioTrigger = trigger,
                SpeechReco = speechReco,
                Codec = codec,
                DialogConnection = dialogClient,
                EnableRSA = true,
                LoginProviders = loginProviders,
                PrivateKeyStore = privateKeyStore
            };

            ClientCore client = new ClientCore();
            
            client.Success += ClientSuccess;
            client.Fail += ClientFail;
            client.Skip += ClientSkip;
            client.SpeechPrompt += SpeechPrompt;
            client.SpeechCaptureIntermediate += ClientIntermediateSpeech;
            client.Initialized += Initialized;
            
            await client.Initialize(clientParams);
            return client;
        }

        public static ClientContext GenerateClientContext()
        {
            ClientContext returnVal = new ClientContext()
            {
                ClientId = _clientConfig.ClientId,
                ClientName = _clientConfig.ClientName,
                UserId = _clientConfig.UserId,
                Locale = "en-us",
                ReferenceDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                Latitude = _clientConfig.ClientLatitude,
                Longitude = _clientConfig.ClientLongitude,
                LocationAccuracy = 10,
                UTCOffset = -420
            };

            returnVal.SetCapabilities(
                ClientCapabilities.HasInternetConnection |
                ClientCapabilities.HasMicrophone |
                ClientCapabilities.HasSpeakers |
                ClientCapabilities.RsaEnabled |
                ClientCapabilities.SupportsCompressedAudio |
                ClientCapabilities.SupportsStreamingAudio |
                ClientCapabilities.VerboseSpeechHint |
                ClientCapabilities.KeywordSpotter
            );

            returnVal.Data[ClientContextField.FormFactor] = FormFactor.Integrated.ToString();
            returnVal.Data[ClientContextField.ClientType] = "RASPBERRY_PI";
            returnVal.Data[ClientContextField.ClientVersion] = string.Format("{0}.{1}.{2}", SVNVersionInfo.MajorVersion, SVNVersionInfo.MinorVersion, SVNVersionInfo.Revision);
            return returnVal;
        }

        public static async void ButtonPressed(object source, EventArgs args)
        {
            if (_buttonPressed)
                return;
            _buttonPressed = true;
            _logger.Log("Hardware button pressed");
            if (await _client.TryMakeAudioRequest(GenerateClientContext()))
            {
                _lastButtonPress = DateTime.Now;
            }
            else if (_lastButtonPress == default(DateTime))
            {
                _lastButtonPress = DateTime.Now;
            }
        }

        public static void SpeechPrompt(object source, EventArgs args)
        {
            _audioOut.PlaySound(_promptSound, true);
        }

        public static void ButtonReleased(object source, EventArgs args)
        {
            if (!_buttonPressed)
                return;
            _buttonPressed = false;
            _logger.Log("Hardware button released");
            if (DateTime.Now.Subtract(_lastButtonPress).TotalMilliseconds > 700)
            {
                _client.ForceRecordingFinish();
            }
        }

        public static void ClientSuccess(object source, EventArgs args)
        {
            _logger.Log("Success");
        }

        public static void ClientFail(object source, EventArgs args)
        {
            _logger.Log("Fail");
            _audioOut.PlaySound(_failSound, true);
        }

        public static void ClientSkip(object source, EventArgs args)
        {
            _logger.Log("Skip");
            _audioOut.PlaySound(_failSound, true);
        }

        public static void Initialized(object source, EventArgs args)
        {
            _audioOut.PlaySound(_successSound, true);
        }

        public static void ClientIntermediateSpeech(object source, TextEventArgs args)
        {
            _logger.Log("Speech partial reco: " + args.Text);
        }

		private static void SpeechRecoTest()
		{
			int softwareSampleRate = 16000;
			int hardwareSampleRate = 48000;
			Console.OutputEncoding = Encoding.UTF8;

			ILogger logger = new ConsoleLogger("Main", LogLevel.All);
			IAudioCodec codec = new OpusAudioCodec(logger.Clone("OpusCodec"));
			/*NoiseFilterParams filterParams = new NoiseFilterParams()
                {
                    InputSampleRate = hardwareSampleRate,
                    Strength = 0.5f
                };*/
			IMicrophone audioIn = new BassMicrophone(logger.Clone("BassMic"), -1, softwareSampleRate, 2.0f, hardwareSampleRate);
			//ISpeechRecognizer recognizer = new RemoteSpeechRecognizer(codec, new WindowsSocketProvider("localhost", 62290), logger);


			ISocketFactory factory = new TcpClientSocketFactory(logger.Clone("SRSocketFactory"), System.Security.Authentication.SslProtocols.Default, false);
			ISpeechRecognizerFactory recognizerFactory = new CortanaSpeechRecognizerFactory(factory, logger.Clone("CortanaSpeech"), "010b1eff830c4670a9322f52d44fd369");
			//ISpeechRecognizerFactory recognizerFactory = new OxfordSpeechRecognizerFactory(logger.Clone("OxfordSpeech"), "f0bbf14694ef4264b39291c00bc100ee");

			audioIn.StartRecording();

			while (true)
			{
				Console.ReadKey();
				Console.WriteLine("Triggered");
				BucketAudioStream bucket = new BucketAudioStream();
				audioIn.ClearBuffers();
				ChunkedAudioStream stream = AudioUtils.RecordUtteranceOfFixedLength(audioIn, 3000);
				Stopwatch startCaptureTimer = new Stopwatch();
				startCaptureTimer.Start();
				ISpeechRecognizer recognizer = recognizerFactory.CreateRecognitionStream("en-us", logger).Await();
				if (recognizer == null)
				{
					Console.WriteLine("Could not create recognizer");
					recognizer = new NullSpeechReco();
				}
				startCaptureTimer.Stop();
				Console.WriteLine("Took " + startCaptureTimer.ElapsedMilliseconds + "ms to start reco");

				Console.WriteLine("Capture start");
				while (!stream.EndOfStream)
				{
					AudioChunk input = stream.Read();
					if (input == null)
					{
						continue;
					}

					bucket.Write(input.Data);
					string speechHyp = recognizer.ContinueUnderstandSpeech(input).Await();
					if (!string.IsNullOrEmpty(speechHyp))
					{
						Console.WriteLine(speechHyp);
					}
				}
				Console.WriteLine("Capture end");

				Stopwatch timer = new Stopwatch();
				timer.Start();
				IList<SpeechRecoResult> bingRecoResults = recognizer.FinishUnderstandSpeech().Await();
				timer.Stop();
				Console.WriteLine("Took " + timer.ElapsedMilliseconds + "ms to finish reco");
				AudioChunk utterance = new AudioChunk(bucket.GetAllData(), 16000);

				if (bingRecoResults == null)
				{
					Console.WriteLine("RecoResults are null");
				}
				else if (bingRecoResults.Count == 0)
				{
					Console.WriteLine("RecoResults are empty");
				}
				else
				{
					foreach (SpeechRecoResult result in bingRecoResults)
					{
						Console.WriteLine("{0} / {1} ({2})", result.NormalizedText, result.LexicalForm ?? result.NormalizedText, result.Confidence);
					}
				}

				recognizer.Close();
			}
		}
    }
}
