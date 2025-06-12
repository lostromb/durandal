using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Statistics.Ranking
{
    public class RankedUtterance
    {
        public string ActualDomainIntent;
        public ISet<string> Features;

        public RankedUtterance(string domainIntent)
        {
            ActualDomainIntent = domainIntent;
            Features = new HashSet<string>();
        }
    }
}
