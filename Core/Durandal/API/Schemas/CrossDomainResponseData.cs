using Newtonsoft.Json;
using System.Collections.Generic;

namespace Durandal.API
{
    public class CrossDomainResponseData
    {
        /// <summary>
        /// The list of slot values that are actually being passed from the cross-domain caller domain to the callee.
        /// This should satisfy whatever slots were asked for in the earlier CrossDomainRequest.
        /// </summary>
        public List<SlotValue> FilledSlots = new List<SlotValue>();

        /// <summary>
        /// Specifies the multiturn behavior that will be followed when/if control returns from the callee domain (the subdomain) back to the caller.
        /// The caller may want to continue immediately or just end the conversation.
        /// </summary>
        public MultiTurnBehavior CallbackMultiturnBehavior = MultiTurnBehavior.None;

        [JsonConstructor]
        public CrossDomainResponseData()
        {
        }

        /// <summary>
        /// Constructs a CrossDomainResponse that specifies to the callee that would like a callback after it is finished, to a specific domain and intent.
        /// The domain is almost always "this.Domain" (for the current answer), and the intent is whatever was specified in the caller's conversation tree
        /// for the node which transitions away from the external domain node.
        /// </summary>
        /// <param name="callbackDomain"></param>
        /// <param name="callbackIntent"></param>
        public CrossDomainResponseData(string callbackDomain, string callbackIntent)
        {
            FilledSlots.Add(new SlotValue(DialogConstants.CALLBACK_DOMAIN_SLOT_NAME, callbackDomain, SlotValueFormat.CrossDomainTag));
            FilledSlots.Add(new SlotValue(DialogConstants.CALLBACK_INTENT_SLOT_NAME, callbackIntent, SlotValueFormat.CrossDomainTag));
        }
    }
}
