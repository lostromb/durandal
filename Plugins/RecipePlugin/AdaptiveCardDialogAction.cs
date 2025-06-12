using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe
{
    public class AdaptiveCardDialogAction
    {
        public AdaptiveCardDialogAction(string dialogActionId)
        {
            DialogActionId = dialogActionId;
        }

        public AdaptiveCardDialogAction() : this(string.Empty) { }

        [JsonProperty("dialogActionId")]
        public string DialogActionId { get; set; }
    }
}
