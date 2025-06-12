using System.Collections.Generic;

namespace Durandal.API
{
    public class CrossDomainRequestData
    {
        /// <summary>
        /// Contains the set of slots that are requested of the caller by the callee. The names of the slots are local
        /// to the callee's domain, and the schema is specified by the schema field of each CrossDomainSlot object.
        /// This set of slots encapsulates the primary cross-domain API for one plugin.
        /// </summary>
        public ISet<CrossDomainSlot> RequestedSlots = new HashSet<CrossDomainSlot>();
    }
}
