using System.Collections.Generic;
using Newtonsoft.Json;
using Durandal.API;
using Durandal.Common.Logger;

namespace Durandal.Common.Dialog
{
    /// <summary>
    /// Represents the overall response from the dialog processor.
    /// This contains the dialog engine result code and response data, passed internally
    /// from the plugin to its invocation container (the master dialog logic in DialogProcessingEngine)
    /// </summary>
    public class DialogEngineResponse
    {
        public Result ResponseCode = Result.Failure;
        public MultiTurnBehavior NextTurnBehavior = MultiTurnBehavior.None;
        public string SpokenSsml = string.Empty;
        public string DisplayedText = string.Empty;
        public string ActionURL = string.Empty;
        public string PresentationHtml = string.Empty;
        public string ErrorMessage = string.Empty;
        public string AugmentedQuery = string.Empty;
        public AudioResponse ResponseAudio = null;
        public RecoResult SelectedRecoResult = null;
        public IDictionary<string, string> ResponseData = new Dictionary<string, string>();
        public UrlScope UrlScope = UrlScope.Local;
        public bool IsRetrying = false;
        public string ClientAction = string.Empty;
        public List<string> SuggestedQueries = new List<string>();
        public List<TriggerKeyword> TriggerKeywords = new List<TriggerKeyword>();
        public PluginStrongName ExecutedPlugin = null;
        public DataPrivacyClassification PluginResponsePrivacyClass = DataPrivacyClassification.Unknown;

        [JsonIgnore]
        public string TriggeredDomain
        {
            get
            {
                return (SelectedRecoResult != null) ? SelectedRecoResult.Domain : string.Empty;
            }
        }

        [JsonIgnore]
        public string TriggeredIntent
        {
            get
            {
                return (SelectedRecoResult != null) ? SelectedRecoResult.Intent : string.Empty;
            }
        }
    }
}
