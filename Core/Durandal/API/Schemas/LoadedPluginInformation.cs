using Durandal.Common.Dialog;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class LoadedPluginInformation
    {
        private SerializedConversationTree _serializedConversationTree = null;
        private IConversationTree _realConversationTree = null;

        public PluginStrongName PluginStrongName { get; set; }

        public string LUDomain { get; set; }

        public string PluginId { get; set; }

        public PluginInformation PluginInfo { get; set; }

        public SerializedConversationTree SerializedConversationTree
        {
            get
            {
                return _serializedConversationTree;
            }
            set
            {
                _realConversationTree = value == null ? null : value.Deserialize();
                _serializedConversationTree = value;
            }
        }
        
        [JsonIgnore]
        public IConversationTree ConversationTree
        {
            get
            {
                return _realConversationTree;
            }
            set
            {
                _realConversationTree = value;
                _serializedConversationTree = value == null ? null : value.Serialize();
            }
        }
    }
}
