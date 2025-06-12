using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class FormattedFact
    {
        public string label { get; set; }
        public List<FormattedFactItem> items { get; set; }
    }

    public class FormattedFactItem
    {
        public string text { get; set; }
    }
}
