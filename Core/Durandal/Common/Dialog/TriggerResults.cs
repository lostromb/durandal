using Durandal.API;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Utils;
using Durandal.Common.NLP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog
{
    public class TriggerResults
    {
        public IDictionary<string, InMemoryDataStore> SessionStores = new Dictionary<string, InMemoryDataStore>();
        public IDictionary<string, TriggerResult> Results = new Dictionary<string, TriggerResult>();
        public List<RankedHypothesis> AugmentedHypotheses;
        public bool RequiresDisambiguation;
    }
}
