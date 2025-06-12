
using Durandal.Common.Dialog;
using Durandal.Common.Logger;
using System.Collections.Generic;
using System.Reflection;

namespace Durandal.API
{
    /// <summary>
    /// Represents the result of a single-turn utterance posed to a plugin.
    /// </summary>
    public class PluginResult
    {
        private Result _responseCode = Result.Failure;
        private string _responseText = string.Empty;
        private string _responseSsml = string.Empty;
        private string _responseUrl = string.Empty;
        private string _responseHtml = string.Empty;
        private MultiTurnBehavior _multiTurnResult = MultiTurnBehavior.None;
        private AudioResponse _responseAudio = null;
        private string _errorMessage = string.Empty;
        private IDictionary<string, string> _responseData = new Dictionary<string, string>();
        private string _resultConversationNode = string.Empty;
        private string _augmentedQuery = null;
        private string _clientAction = null;
        private DialogAction _invokedDialogAction = null;
        private List<string> _suggestedQueries = new List<string>();
        private List<TriggerKeyword> _triggerKeywords = new List<TriggerKeyword>();
        private string _continuationFuncName = null;
        private DataPrivacyClassification _responsePrivacyClassification = DataPrivacyClassification.Unknown;

        /// <summary>
        /// Make the result code required; everything else is optional
        /// </summary>
        /// <param name="result"></param>
        public PluginResult(Result result)
        {
            ResponseCode = result;
        }

        /// <summary>
        /// The result of the plugin execution.
        /// </summary>
        public Result ResponseCode
        {
            get
            {
                return _responseCode;
            }
            set
            {
                _responseCode = value;
            }
        }

        /// <summary>
        /// The string for the synthesizer to speak. This can be formatted as plaintext or SSML.
        /// </summary>
        public string ResponseSsml
        {
            get
            {
                return _responseSsml;
            }
            set
            {
                _responseSsml = value;
            }
        }

        /// <summary>
        /// An external URL to return to the client
        /// </summary>
        public string ResponseUrl
        {
            get
            {
                return _responseUrl;
            }
            set
            {
                _responseUrl = value;
            }
        }

        /// <summary>
        /// An HTML-formatted view to display to the user
        /// </summary>
        public string ResponseHtml
        {
            get
            {
                return _responseHtml;
            }
            set
            {
                _responseHtml = value;
            }
        }

        /// <summary>
        /// Plain text to show to the user (fallback for if they do not have an html interface)
        /// </summary>
        public string ResponseText
        {
            get
            {
                return _responseText;
            }
            set
            {
                _responseText = value;
            }
        }

        /// <summary>
        /// The desired multiturn state that should follow this response
        /// </summary>
        public MultiTurnBehavior MultiTurnResult
        {
            get
            {
                return _multiTurnResult;
            }
            set
            {
                _multiTurnResult = value;
            }
        }

        /// <summary>
        /// Custom audio to play back to the user. If there is both SSML and audio in the same response, the ordering of which goes first
        /// should be explicitly specified using AudioOrdering enum
        /// </summary>
        public AudioResponse ResponseAudio
        {
            get
            {
                return _responseAudio;
            }
            set
            {
                _responseAudio = value;
            }
        }

        /// <summary>
        /// If an error occurred in the plugin, you can log it here. This will be used for debug tracing only.
        /// </summary>
        public string ErrorMessage
        {
            get
            {
                return _errorMessage;
            }
            set
            {
                _errorMessage = value;
            }
        }

        /// <summary>
        /// A bag of assorted response data objects.
        /// </summary>
        public IDictionary<string, string> ResponseData
        {
            get
            {
                return _responseData;
            }
            set
            {
                _responseData = value;
            }
        }

        /// <summary>
        /// The answer can choose to jump to a specific node in the conversation tree, identified by a string, as a result of processing
        /// </summary>
        public string ResultConversationNode
        {
            get
            {
                return _resultConversationNode;
            }
            set
            {
                _resultConversationNode = value;
            }
        }

        /// <summary>
        /// A plugin may alter the query or choose a different hypothesis than what the client is displaying.
        /// To rectify this, the service can use this to pass back an object representing the actual query
        /// that was acted upon
        /// </summary>
        public string AugmentedQuery
        {
            get
            {
                return _augmentedQuery;
            }
            set
            {
                _augmentedQuery = value;
            }
        }

        /// <summary>
        /// Alternative setter for AugmentedQuery that lets you modify one or more slots in TaggedData and have it recreated back as a plain string.
        /// </summary>
        public TaggedData AugmentedQueryTaggedData
        {
            set
            {
                _augmentedQuery = DialogHelpers.ConvertTaggedDataToAugmentedQuery(value);
            }
        }

        /// <summary>
        /// This represents an implementation-dependent "action" that you would like the client to execute on your behalf.
        /// Examples may be opening a browser, turning off the microphone, etc.
        /// </summary>
        public string ClientAction
        {
            get
            {
                return _clientAction;
            }
            set
            {
                _clientAction = value;
            }
        }

        /// <summary>
        /// This field is used to hand off the results of this plugin and begin executing another plugin instead.
        /// It is used almost exclusively for callbacks at the end of cross-domain interactions
        /// </summary>
        public DialogAction InvokedDialogAction
        {
            get
            {
                return _invokedDialogAction;
            }
            set
            {
                _invokedDialogAction = value;
            }
        }

        /// <summary>
        /// The plugin can return one or more suggested follow-up queries that can be used as hints on the client
        /// to guide the user and aid discoverability of multiturn scenarios
        /// </summary>
        public List<string> SuggestedQueries
        {
            get
            {
                return _suggestedQueries;
            }
            set
            {
                _suggestedQueries = value;
            }
        }

        /// <summary>
        /// A plugin can choose to return an explicit callback that will be invoked on the next turn, if the LU result
        /// is within the same domain or common domain. The callback function must be globally available (i.e. not an anonymous method)
        /// </summary>
        public PluginContinuation Continuation
        {
            set
            {
                // Validate the continuation and then transform it into a string value that is serializable (which is how it will be stored in conversation state)
                MethodInfo methodInfo = value.GetMethodInfo();
                if (methodInfo.IsAssembly)
                {
                    throw new DialogException("A dialog continuation must not be an anonymous method: " + methodInfo.Module.Name + " " + methodInfo.Name);
                }
                if (!methodInfo.IsPublic)
                {
                    throw new DialogException("A dialog continuation must have public visibility: " + methodInfo.Name);
                }

                _continuationFuncName = methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
            }
        }

        /// <summary>
        /// The name of the continuation function. Don't try and set this manually; use the Continuation setter instead
        /// </summary>
        public string ContinuationFuncName
        {
            get
            {
                return _continuationFuncName;
            }
            set
            {
                _continuationFuncName = value;
            }
        }

        /// <summary>
        /// This field makes use of the PocketSphinx keyword spotter functionality. When a plugin puts words or phrases in
        /// this dictionary (and returns a tenative multiturn result), the client's keyword spotter will begin listening
        /// for all of these keywords, in addition to the regular trigger phrase, until the conversation expires.
        /// When a custom keyword is triggered (spoken) by the user, it will be treated as an instant speech reco result
        /// and sent as a regular query (bypassing the regular record-utterance path), so make sure your LU can properly classify the actual keywords themselves
        /// </summary>
        public List<TriggerKeyword> TriggerKeywords
        {
            get
            {
                return _triggerKeywords;
            }
            set
            {
                _triggerKeywords = value;
            }
        }
        
        /// <summary>
        /// Indicates the level of data sensitivity of the dialog response. This is applicable to the "non-metadata" fields of the response, including response text, SSML, HTML, audio, etc.
        /// </summary>
        public DataPrivacyClassification ResponsePrivacyClassification
        {
            get
            {
                return _responsePrivacyClassification;
            }
            set
            {
                _responsePrivacyClassification = value;
            }
        }
    }
}
