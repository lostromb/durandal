using Durandal.Common.Config;
using Durandal.Common.Config.Accessors;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog
{
    /// <summary>
    /// Provides a view over a Configuration object that contains options specific to DialogProcessingEngine
    /// </summary>
    public class DialogConfiguration
    {
        private readonly WeakPointer<IConfiguration> _internal;

        public DialogConfiguration(WeakPointer<IConfiguration> container)
        {
            _internal = container;
        }

        public WeakPointer<IConfiguration> GetBase()
        {
            return _internal;
        }

        /// <summary>
        /// Minimum confidence needed to trigger an answer
        /// </summary>
        public float MinPluginConfidence
        {
            get
            {
                return _internal.Value.GetFloat32("minAnswerConfidence", 0.4f);
            }
            set
            {
                _internal.Value.Set("minAnswerConfidence", value);
            }
        }

        /// <summary>
        /// Prevent side speech confidence from going above this amount. This is to prevent cases where both side speech AND a valid answer are tagged with a high confidence.
        /// This does not affect the ranking of the side_speech_highconf intent.
        /// </summary>
        public float MaxSideSpeechConfidence
        {
            get
            {
                return _internal.Value.GetFloat32("maxSideSpeechConfidence", 0.75f);
            }
            set
            {
                _internal.Value.Set("maxSideSpeechConfidence", value);
            }
        }

        /// <summary>
        /// Determines whether to allow first-turn side-speech queries. If this is false, conversation-starting queries that are confidently tagged as side speech will go to the side_speech domain for routing
        /// </summary>
        public bool IgnoreSideSpeech
        {
            get
            {
                return _internal.Value.GetBool("ignoreSideSpeech", false);
            }
            set
            {
                _internal.Value.Set("ignoreSideSpeech", value);
            }
        }

        /// <summary>
        /// If non-null, only the domains specified in this list will be able to edit the global user profile
        /// </summary>
        public IList<string> AllowedGlobalProfileEditors
        {
            get
            {
                if (_internal.Value.ContainsKey("allowedGlobalProfileEditors"))
                {
                    return _internal.Value.GetStringList("allowedGlobalProfileEditors");
                }

                return null;
            }
            set
            {
                _internal.Value.Set("allowedGlobalProfileEditors", value);
            }
        }

        /// <summary>
        /// The callback URI to be passed to third parties for their redirect_uri parameter
        /// </summary>
        public string OAuthCallbackUri
        {
            get
            {
                return _internal.Value.GetString("oauthCallbackUrl", null);
            }
            set
            {
                _internal.Value.Set("oauthCallbackUrl", value);
            }
        }
        
        public bool AssumePluginResponsesArePII
        {
            get
            {
                return _internal.Value.GetBool("assumePluginResponsesArePII", false);
            }
            set
            {
                _internal.Value.Set("assumePluginResponsesArePII", value);
            }
        }

        /// <summary>
        /// Maximum allowed size in bytes that a single plugin can use for its session data or user profile stores.
        /// </summary>
        public int MaxStoreSizeBytes
        {
            get
            {
                return _internal.Value.GetInt32("maxStoreSizeBytes", 64 * 1024);
            }
            set
            {
                _internal.Value.Set("maxStoreSizeBytes", value);
            }
        }

        /// <summary>
        /// Maximum number of turns that conversation history gets stored in the state
        /// </summary>
        public int MaxConversationHistoryLength
        {
            get
            {
                return _internal.Value.GetInt32("maxConversationHistoryLength", 10);
            }
            set
            {
                _internal.Value.Set("maxConversationHistoryLength", value);
            }
        }

        public IConfigValue<int> MaxConversationHistoryLengthAccessor(ILogger logger)
        {
             return _internal.Value.CreateInt32Accessor(logger, "maxConversationHistoryLength", 10);
        }
    }
}
