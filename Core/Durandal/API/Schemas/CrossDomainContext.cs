using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class CrossDomainContext
    {
        /// <summary>
        /// The domain that is being called out to (the external domain)
        /// </summary>
        public string RequestDomain { get; set; }

        /// <summary>
        /// The intent that is being called out to (the intent to execute on the remote plugin)
        /// </summary>
        public string RequestIntent { get; set; }

        /// <summary>
        /// The set of slots that the other plugin requires in order to satisfy this request
        /// </summary>
        public ISet<CrossDomainSlot> RequestedSlots { get; set; }

        /// <summary>
        /// The history of the conversation thus far, including the current turn
        /// </summary>
        public List<RecoResult> PastConversationTurns { get; set; }
    }
}
