using Durandal.Common.Config;
using Durandal.Common.NLP.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Client
{
    /// <summary>
    /// Provides a view over a Configuration object that contains client-specific options
    /// </summary>
    public class ClientConfiguration
    {
        private IConfiguration _internal;
        
        public ClientConfiguration(IConfiguration container)
        {
            _internal = container;
        }

        public IConfiguration GetBase()
        {
            return _internal;
        }

        /// <summary>
        /// Fully qualified URL of the dialog server to connect to, e.g. "https://www.durandal.com:62292"
        /// </summary>
        public Uri RemoteDialogServerAddress
        {
            get
            {
                string returnVal = _internal.GetString("remoteDialogServerAddress");
                Uri returnUri;
                if (!string.IsNullOrEmpty(returnVal) &&
                    Uri.TryCreate(returnVal, UriKind.Absolute, out returnUri) &&
                    returnUri.HostNameType != UriHostNameType.Unknown)
                {
                    return returnUri;
                }

                return null;
            }
            set
            {
                if (value == null || !value.IsAbsoluteUri || value.HostNameType == UriHostNameType.Unknown)
                    _internal.Set("remoteDialogServerAddress", string.Empty);
                else
                    _internal.Set("remoteDialogServerAddress", value.AbsoluteUri);
            }
        }

        /// <summary>
        /// Fully qualified URL of the service which manages authentication, e.g. "https://www.durandal-auth.com:443"
        /// </summary>
        public Uri AuthenticationEndpoint
        {
            get
            {
                string returnVal = _internal.GetString("authenticationEndpoint");
                Uri returnUri;
                if (!string.IsNullOrEmpty(returnVal) &&
                    Uri.TryCreate(returnVal, UriKind.Absolute, out returnUri) &&
                    returnUri.HostNameType != UriHostNameType.Unknown)
                {
                    return returnUri;
                }

                return null;
            }
            set
            {
                if (value == null || !value.IsAbsoluteUri || value.HostNameType == UriHostNameType.Unknown)
                    _internal.Set("authenticationEndpoint", string.Empty);
                else
                    _internal.Set("authenticationEndpoint", value.AbsoluteUri);
            }
        }

        /// <summary>
        /// The port number of the local web server (for serving local html)
        /// </summary>
        public int LocalPresentationServerPort
        {
            get
            {
                return _internal.GetInt32("localPresentationServerPort", 62293);
            }
            set
            {
                _internal.Set("localPresentationServerPort", value);
            }
        }

        /// <summary>
        /// The actual hardware capture sample rate to use for the microphone. This is for hardware compatibility, as the client will always resample to 16Khz internally
        /// </summary>
        public int MicrophoneSampleRate
        {
            get
            {
                return _internal.GetInt32("microphoneSampleRate", 44100);
            }
            set
            {
                _internal.Set("microphoneSampleRate", value);
            }
        }

        /// <summary>
        /// The amplification to apply to the microphone input
        /// </summary>
        public float MicrophonePreamp
        {
            get
            {
                return _internal.GetFloat32("microphonePreamp", 1.0f);
            }
            set
            {
                _internal.Set("microphonePreamp", value);
            }
        }

        /// <summary>
        /// For BASS backends, this specifies the microphone device ID to use (0 to use the default device)
        /// </summary>
        public int MicrophoneDeviceId
        {
            get
            {
                return _internal.GetInt32("micId", 0);
            }
            set
            {
                _internal.Set("micId", value);
            }
        }

        /// <summary>
        /// The actual hardware sample rate to use for the speakers. This is for hardware compatibility, as the client will always resample to 16Khz internally
        /// </summary>
        public int SpeakerSampleRate
        {
            get
            {
                return _internal.GetInt32("speakerSampleRate", 44100);
            }
            set
            {
                _internal.Set("speakerSampleRate", value);
            }
        }

        /// <summary>
        /// For BASS backends, this specifies the speaker device ID to use (0 to use the default device)
        /// </summary>
        public int SpeakerDeviceId
        {
            get
            {
                return _internal.GetInt32("speakerId", 0);
            }
            set
            {
                _internal.Set("speakerId", value);
            }
        }

        /// <summary>
        /// The length of data to buffer before starting to play streamed audio. Higher = more latency, lower = less chance of stuttering
        /// </summary>
        public TimeSpan StreamingAudioPrebufferTime
        {
            get
            {
                return _internal.GetTimeSpan("streamingAudioPrebufferTime", TimeSpan.FromMilliseconds(500));
            }
            set
            {
                _internal.Set("streamingAudioPrebufferTime", value);
            }
        }

        /// <summary>
        /// The default audio codec to use for speech requests
        /// </summary>
        public string AudioCodec
        {
            get
            {
                return _internal.GetString("audioCodec", "none");
            }
            set
            {
                _internal.Set("audioCodec", value);
            }
        }

        /// <summary>
        /// Define which SR (Speech Recognition) provider to use. Possible values are 'oxford', 'remote', 'bing', 'google', and 'sapi'
        /// </summary>
        public string SRProvider
        {
            get
            {
                return _internal.GetString("srProvider", "oxford");
            }
            set
            {
                _internal.Set("srProvider", value);
            }
        }

        /// <summary>
        /// The API key to be passed to the selected speech reco provider
        /// </summary>
        public string SRApiKey
        {
            get
            {
                return _internal.GetString("srApiKey", null);
            }
            set
            {
                _internal.Set("srApiKey", value);
            }
        }

        /// <summary>
        /// Define which TTS (Text-to-speech) provider to use. Possible values are 'bing', 'google', and 'sapi'
        /// </summary>
        public string TTSProvider
        {
            get
            {
                return _internal.GetString("ttsProvider", "sapi");
            }
            set
            {
                _internal.Set("ttsProvider", value);
            }
        }

        /// <summary>
        /// The API key to be passed to the selected text-to-speech provider
        /// </summary>
        public string TTSApiKey
        {
            get
            {
                return _internal.GetString("ttsApiKey", null);
            }
            set
            {
                _internal.Set("ttsApiKey", value);
            }
        }

        /// <summary>
        /// If srProvider is set to "remote", this specifies the remote SR server to use.
        /// The URI scheme used is "sr://" by convention but is not really relevant
        /// </summary>
        public Uri RemoteSpeechRecoAddress
        {
            get
            {
                string returnVal = _internal.GetString("remoteSpeechRecoAddress");
                Uri returnUri;
                if (!string.IsNullOrEmpty(returnVal) &&
                    Uri.TryCreate(returnVal, UriKind.Absolute, out returnUri) &&
                    returnUri.HostNameType != UriHostNameType.Unknown)
                {
                    return returnUri;
                }

                return null;
            }
            set
            {
                if (value == null || !value.IsAbsoluteUri || value.HostNameType == UriHostNameType.Unknown)
                    _internal.Set("remoteSpeechRecoAddress", string.Empty);
                else
                    _internal.Set("remoteSpeechRecoAddress", value.AbsoluteUri);
            }
        }

        /// <summary>
        /// Enables or disables the PocketSphinx continuous listening trigger
        /// </summary>
        public bool TriggerEnabled
        {
            get
            {
                return _internal.GetBool("triggerEnabled", false);
            }
            set
            {
                _internal.Set("triggerEnabled", value);
            }
        }


        /// <summary>
        /// This is the key word or phrase that will trigger the client whenever it is spoken
        /// </summary>
        public string TriggerPhrase
        {
            get
            {
                return _internal.GetString("triggerPhrase", "durandal");
            }
            set
            {
                _internal.Set("triggerPhrase", value);
            }
        }

        /// <summary>
        /// The sensitivity of the primary voice trigger, from 0 to 10
        /// </summary>
        public double PrimaryAudioTriggerSensitivity
        {
            get
            {
                return _internal.GetFloat64("primaryTriggerSensitivity", 5.0);
            }
            set
            {
                _internal.Set("primaryTriggerSensitivity", value);
            }
        }

        /// <summary>
        /// The sensitivity to use for "secondary" trigger activation, meaning context-sensitive intents that are separate from the primary trigger phrase. Between 0 and 10
        /// </summary>
        public double SecondaryAudioTriggerSensitivity
        {
            get
            {
                return _internal.GetFloat64("secondaryTriggerSensitivity", 5.0);
            }
            set
            {
                _internal.Set("secondaryTriggerSensitivity", value);
            }
        }

        /// <summary>
        /// The path of the acoustic model to use for triggering
        /// </summary>
        public string PocketSphinxAmDirectory
        {
            get
            {
                return _internal.GetString("pocketSphinxAmDir", "en-US-semi");
            }
            set
            {
                _internal.Set("pocketSphinxAmDir", value);
            }
        }

        /// <summary>
        /// The path of the dictionary file to use for triggering
        /// </summary>
        public string PocketSphinxDictionaryFile
        {
            get
            {
                return _internal.GetString("pocketSphinxDictFile", "sphinx_dict.txt");
            }
            set
            {
                _internal.Set("pocketSphinxDictFile", value);
            }
        }

        /// <summary>
        /// The client's locale to report to the dialog service
        /// </summary>
        public LanguageCode Locale
        {
            get
            {
                string stringVal = _internal.GetString("locale");
                return LanguageCode.TryParse(stringVal) ?? LanguageCode.UNDETERMINED;
            }
            set
            {
                _internal.Set("locale", value.ToBcp47Alpha2String());
            }
        }

        /// <summary>
        /// A unique GUID that represents this client throughout the system
        /// </summary>
        public string ClientId
        {
            get
            {
                return _internal.GetString("clientId");
            }
            set
            {
                _internal.Set("clientId", value);
            }
        }

        /// <summary>
        /// A unique GUID that represents this user throughout the system
        /// </summary>
        public string UserId
        {
            get
            {
                return _internal.GetString("userId");
            }
            set
            {
                _internal.Set("userId", value);
            }
        }

        /// <summary>
        /// A human-readable name for this user. By default, this is the logged-in user name
        /// </summary>
        public string UserName
        {
            get
            {
                return _internal.GetString("userName");
            }
            set
            {
                _internal.Set("userName", value);
            }
        }

        /// <summary>
        /// A human-readable name for this client. By default, this is the local machine name.
        /// </summary>
        public string ClientName
        {
            get
            {
                return _internal.GetString("clientName");
            }
            set
            {
                _internal.Set("clientName", value);
            }
        }

        /// <summary>
        /// The default latitude value to use in the client's context
        /// </summary>
        public double ClientLatitude
        {
            get
            {
                return _internal.GetFloat64("clientLatitude");
            }
            set
            {
                _internal.Set("clientLatitude", value);
            }
        }

        /// <summary>
        /// The default longitude value to use in the client's context
        /// </summary>
        public double ClientLongitude
        {
            get
            {
                return _internal.GetFloat64("clientLongitude");
            }
            set
            {
                _internal.Set("clientLongitude", value);
            }
        }

        /// <summary>
        /// Fully qualified URL of the server which can arbitrate audio triggers between multiple machines
        /// </summary>
        public Uri TriggerArbitratorUrl
        {
            get
            {
                string returnVal = _internal.GetString("triggerArbitratorUrl");
                Uri returnUri;
                if (!string.IsNullOrEmpty(returnVal) &&
                    Uri.TryCreate(returnVal, UriKind.Absolute, out returnUri) &&
                    returnUri.HostNameType != UriHostNameType.Unknown)
                {
                    return returnUri;
                }

                return null;
            }
            set
            {
                if (value == null || !value.IsAbsoluteUri || value.HostNameType == UriHostNameType.Unknown)
                    _internal.Set("triggerArbitratorUrl", string.Empty);
                else
                    _internal.Set("triggerArbitratorUrl", value.AbsoluteUri);
            }
        }

        /// <summary>
        /// A key shared between clients that should have their triggers all be arbitrated together
        /// </summary>
        public string TriggerArbitratorGroupName
        {
            get
            {
                return _internal.GetString("triggerArbitratorGroupName", "default");
            }
            set
            {
                _internal.Set("triggerArbitratorGroupName", value);
            }
        }
    }
}
