using Durandal.API.Data;

namespace SystemTrayClient
{
    using Durandal.API;
    using Durandal.API.Utils;
    using Durandal.BondProtocol;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Interfaces;
    using Durandal.Common.Audio.NAudio;
    using Durandal.Common.Client;
    using Durandal.Common.Config;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Utils.IO;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public class ClientInterface
    {
        private ClientCore _core;

        private IResourceManager _resourceManager;
        private ILogger _coreLogger;
        private ClientConfiguration _clientConfig;
        private float clientLatitude;
        private float clientLongitude;
        private string _clientId;
        private string _clientName;

        private IAudioPlayer _audioOut;
        private IMicrophone _audioIn;
        private AudioChunk _successSound;
        private AudioChunk _failSound;
        private AudioChunk _skipSound;
        private AudioChunk _promptSound;

        public ClientInterface()
        {
            
        }

        public async Task Initialize()
        {
            ThreadPool.SetMaxThreads(16, 32);
            _coreLogger = new ConsoleAndFileLogger("DurandalClient");
            _resourceManager = new FileResourceManager(_coreLogger);
            _coreLogger.Log("Durandal Tray Client, revision r" + SVNVersionInfo.Revision + " built on " + SVNVersionInfo.BuildDate);
            DurandalUtils.SetLogger(_coreLogger.Clone("DurandalUtils"));
            Configuration configFile = new IniFileConfiguration(_coreLogger.Clone("PrimaryConfig"), new ResourceName("DurandalClient_config"), _resourceManager, true);
            _clientConfig = new ClientConfiguration(configFile);

            // Check for existing client id. If not found, create a new one and save it back to the config
            if (!string.IsNullOrEmpty(_clientConfig.ClientId))
            {
                _clientId = Guid.NewGuid().ToString("N");
                _coreLogger.Log("No client ID found, generating a new value " + _clientId);
                ConfigValue newClientId = new ConfigValue("clientId", _clientId, ConfigValueType.String);
                newClientId.Annotations.Add(new DescriptionAnnotation("The unique ID to identify this client. If this value is removed, a new one will be generated."));
                _clientConfig.GetBase().Set(newClientId);
            }
            else
            {
                _clientId = _clientConfig.ClientId;
            }

            if (!string.IsNullOrEmpty(_clientConfig.ClientName))
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

            Task geocodeTask = GetGeoIPCoords();

            _audioOut = new DirectSoundPlayer();
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

            int inputSampleRate = _clientConfig.MicrophoneSampleRate;

            _audioIn = new NAudioMicrophone(inputSampleRate, _clientConfig.MicrophonePreamp);

            _core = new ClientCore();

            _core.Success += Success;
            _core.Fail += Fail;
            _core.Skip += Skip;
            _core.ShowErrorOutput += ShowErrorOutput;
            _core.ResponseReceived += ResponseReceived;
            _core.SpeechPrompt += SpeechPrompt;
            _core.SpeechCaptureFinished += SpeechFinished;
            _core.Initialized += ReadyToGo;

            ILogger httpClientLogger = _coreLogger.Clone("DialogHttpClient");
            ClientCoreParameters coreParams = new ClientCoreParameters(_clientConfig,
                GenerateClientContext)
            {
                Logger = _coreLogger.Clone("ClientCore"),
                ResourceManager = _resourceManager,
                Microphone = _audioIn,
                DialogConnection = new DialogHttpClient(new PortableHttpClient(_clientConfig.RemoteDialogServerAddress, _clientConfig.RemoteDialogServerPort, httpClientLogger), httpClientLogger, new DialogBondTransportProtocol())
            };

            await _core.Initialize(coreParams);
        }

        public void Trigger()
        {
            if (!_core.TryMakeAudioRequest(this.GenerateClientContext()))
            {
                _coreLogger.Log("Audio request denied, as the client core is already engaged in another conversation", LogLevel.Wrn);
            }
        }

        public void ForceTriggerFinish()
        {
            _core.ForceRecordingFinish();
        }

        public void Shutdown()
        {
            _core.Dispose();
            _audioOut.Dispose();
        }

        private void ShowErrorOutput(object sender, TextEventArgs args)
        {
            Console.Error.WriteLine(args.Text);
        }

        private void ReadyToGo(object sender, EventArgs args)
        {
            _audioOut.PlaySound(_successSound);
        }

        private void Success(object sender, EventArgs args)
        {
            // TODO On success, we will get the Success event, and then the server may have returned audio.
            // If we PlaySound with async = true, it will queue up the two sounds so they don't both play at once.
            // However, that's technically a race condition, so it may need a better design.
            _audioOut.PlaySound(_successSound);
        }

        private void Fail(object sender, EventArgs args)
        {
            _audioOut.PlaySound(_failSound);
        }

        private void Skip(object sender, EventArgs args)
        {
            _audioOut.PlaySound(_skipSound);
        }

        /// <summary>
        /// This is fired when a response of any type is recieved from the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ResponseReceived(object sender, EventArgs args)
        {
        }

        private void SpeechPrompt(object sender, EventArgs args)
        {
            _audioOut.PlaySound(_promptSound);
        }

        public void SpeechFinished(object sender, SpeechCaptureEventArgs args)
        {
            if (!args.Success)
            {
                _audioOut.PlaySound(_skipSound);
            }
        }

        private ClientContext GenerateClientContext()
        {
            ClientContext context = new ClientContext();
            context.SetCapabilities(
                ClientCapabilities.HasInternetConnection |
                ClientCapabilities.HasSpeakers |
                ClientCapabilities.HasMicrophone |
                ClientCapabilities.RsaEnabled);
            context.ClientId = _clientId;
            context.UserId = _clientId;

            // The locale code for the communication.
            context.Locale = "en-us";

            // Local client time
            context.ReferenceDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            // The common name of the client (to be used in dialog-side configuration, prettyprinting, etc.)
            context.ClientName = "Desktop client";

            // Client coordinates
            context.Latitude = clientLatitude;
            context.Longitude = clientLongitude;

            // Form factor code
            context.Data[ClientContextField.FormFactor] = FormFactor.Desktop.ToString();
            context.Data[ClientContextField.ClientType] = "WINDOWS_SYSTRAY";
            context.Data[ClientContextField.ClientVersion] = SVNVersionInfo.VersionString;

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
                /// http://geoip.prototypeapp.com/api/locate
                IHttpClient httpClient = new PortableHttpClient("www.telize.com", 80, _coreLogger.Clone("GeoIP"), false);
                NetworkResponseInstrumented<HttpResponse> response = await httpClient.SendRequestAsync(HttpRequest.BuildFromUrlString("/geoip"));
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
                }
            }
        }
    }
}
