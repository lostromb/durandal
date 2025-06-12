using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Quotes
{
    internal class QuotesResult
    {
        public SchemaDotOrg.Person Author { get; set; }
        public List<string> Quotes { get; set; }
    }
}
