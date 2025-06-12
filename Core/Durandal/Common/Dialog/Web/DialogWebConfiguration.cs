using Durandal.Common.Config;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog.Web
{
    /// <summary>
    /// Provides a view over a Configuration object that contains options specific to DialogWebService
    /// </summary>
    public class DialogWebConfiguration
    {
        private WeakPointer<IConfiguration> _internal;

        public DialogWebConfiguration(WeakPointer<IConfiguration> container)
        {
            _internal = container;
        }

        public WeakPointer<IConfiguration> GetBase()
        {
            return _internal;
        }

        /// <summary>
        /// The port for the dialog component interface (the server port for requests being made to this dialog engine)
        /// </summary>
        public IList<string> DialogServerEndpoints
        {
            get
            {
                return _internal.Value.GetStringList("dialogServerEndpoints");
            }
            set
            {
                _internal.Value.Set("dialogServerEndpoints", value);
            }
        }

        /// <summary>
        /// Define which HTTP implementation to use. Possible values are 'socket' and 'listener'
        /// </summary>
        public string HttpImplementation
        {
            get
            {
                return _internal.Value.GetString("httpServerImpl", "socket");
            }
            set
            {
                _internal.Value.Set("httpServerImpl", value);
            }
        }

        /// <summary>
        /// The set of answers to load
        /// </summary>
        public IList<string> PluginIdsToLoad
        {
            get
            {
                // emit a warning if the config is still using the old answerPlugins syntax, but don't break entirely
                IList<string> returnVal = _internal.Value.GetStringList("pluginIdsToLoad");

                if (!_internal.Value.ContainsKey("pluginIdsToLoad") &&
                    _internal.Value.ContainsKey("anwerPlugins"))
                {
                    return _internal.Value.GetStringList("anwerPlugins");
                }

                return returnVal;
            }
            set
            {
                _internal.Value.Set("pluginIdsToLoad", value);
            }
        }

        /// <summary>
        /// The set of audio codecs to load
        /// </summary>
        public IList<string> SupportedAudioCodecs
        {
            get
            {
                if (_internal.Value.ContainsKey("supportedAudioCodecs"))
                {
                    return _internal.Value.GetStringList("supportedAudioCodecs");
                }

                return new List<string>();
            }
            set
            {
                _internal.Value.Set("supportedAudioCodecs", value);
            }
        }

        /// <summary>
        /// Run each plugin in an isolated sandbox, if you don't trust them
        /// </summary>
        public bool SandboxPlugins
        {
            get
            {
                return _internal.Value.GetBool("sandboxPlugins", true);
            }
            set
            {
                _internal.Value.Set("sandboxPlugins", value);
            }
        }

        /// <summary>
        /// Debug mode. Skips most exception and error handling inside the dialog runtime
        /// </summary>
        public bool FailFastPlugins
        {
            get
            {
                return _internal.Value.GetBool("failFastPlugins", false);
            }
            set
            {
                _internal.Value.Set("failFastPlugins", value);
            }
        }

        /// <summary>
        /// The maximum time to allow each plugin to run, per turn
        /// </summary>
        public int MaxPluginExecutionTime
        {
            get
            {
                return _internal.Value.GetInt32("maxPluginExecutionTime", 30000);
            }
            set
            {
                _internal.Value.Set("maxPluginExecutionTime", value);
            }
        }

        /// <summary>
        /// Define which TTS (Text-To-Speech) provider to use. Possible values are 'bing', 'google', and 'sapi'
        /// </summary>
        public string TTSProvider
        {
            get
            {
                return _internal.Value.GetString("ttsProvider", "bing");
            }
            set
            {
                _internal.Value.Set("ttsProvider", value);
            }
        }

        /// <summary>
        /// The API key to be passed to the selected text-to-speech provider
        /// </summary>
        public string TTSApiKey
        {
            get
            {
                return _internal.Value.GetString("ttsApiKey", null);
            }
            set
            {
                _internal.Value.Set("ttsApiKey", value);
            }
        }

        /// <summary>
        /// Define which SR (Speech Recognition) provider to use. Possible values are 'google' and 'sapi'
        /// </summary>
        public string SRProvider
        {
            get
            {
                return _internal.Value.GetString("srProvider", "bing");
            }
            set
            {
                _internal.Value.Set("srProvider", value);
            }
        }

        /// <summary>
        /// The API key to be passed to the selected speech reco provider
        /// </summary>
        public string SRApiKey
        {
            get
            {
                return _internal.Value.GetString("srApiKey", null);
            }
            set
            {
                _internal.Value.Set("srApiKey", value);
            }
        }
        
        /// <summary>
        /// Controls the number of simultaneous speech recognizers / synthesizers to preallocate into the pool
        /// </summary>
        public int SpeechPoolSize
        {
            get
            {
                return _internal.Value.GetInt32("speechPoolSize", 1);
            }
            set
            {
                _internal.Value.Set("speechPoolSize", value);
            }
        }
    }
}
