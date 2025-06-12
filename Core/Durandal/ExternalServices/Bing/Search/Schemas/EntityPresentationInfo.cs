using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class EntityPresentationInfo
    {
        public string entityScenario { get; set; }
        public List<string> entityTypeHints { get; set; }
        public string entityTypeDisplayHint { get; set; }
        public List<FormattedFact> formattedFacts { get; set; }

        public string TryGetFormattedFact(string factName)
        {
            if (formattedFacts == null)
            {
                return null;
            }

            foreach (FormattedFact f in formattedFacts)
            {
                if (string.Equals(f.label, factName))
                {
                    foreach (FormattedFactItem i in f.items)
                    {
                        return i.text;
                    }
                }
            }

            return null;
        }
    }
}
