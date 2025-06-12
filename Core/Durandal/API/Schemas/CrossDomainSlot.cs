using Newtonsoft.Json;
using System.Collections.Generic;

namespace Durandal.API
{
    /// <summary>
    /// This object represents a request for a single slot value between two conversation domains.
    /// When a cross-domain request happens, for example, the Time domain calls into the Weather
    /// answer, the callee (in this case Weather) sends a list of CrossDomainSlot items which represent
    /// the slot values that would be needed to satisfy the request. The caller (Time) then returns a list
    /// of SlotValues that match that criteria. The schema is loosely coupled by design; generally, the
    /// callee would have a fixed slot schema that the caller can rely on (i.e. "I will always ask for a
    /// slot called 'location'"). As a backup, the Schema field can be used to specify the data type
    /// of the information that is being requested, using either a Bond schema name, entity schema,
    /// FreeDB, Schema.org, etc, etc.
    /// </summary>
    public class CrossDomainSlot
    {
        public string SlotName;
        public ISet<string> AcceptedSchemas;
        public bool IsRequired;
        public string Documentation;
        
        public CrossDomainSlot(string slotName, bool isRequired, params string[] acceptedSchemas)
        {
            SlotName = slotName;
            IsRequired = isRequired;
            Documentation = string.Empty;
            AcceptedSchemas = new HashSet<string>();
            if (acceptedSchemas != null)
            {
                foreach (var s in acceptedSchemas)
                {
                    if (!AcceptedSchemas.Contains(s))
                        AcceptedSchemas.Add(s);
                }
            }
        }

        public CrossDomainSlot(string slotName, bool isRequired, IEnumerable<string> acceptedSchemas = null)
        {
            SlotName = slotName;
            IsRequired = isRequired;
            AcceptedSchemas = new HashSet<string>();
            if (acceptedSchemas != null)
            {
                foreach (var s in acceptedSchemas)
                {
                    if (!AcceptedSchemas.Contains(s))
                        AcceptedSchemas.Add(s);
                }
            }
        }

        [JsonConstructor]
        private CrossDomainSlot() { }
    }
}
