using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.AppInsights
{
    public class AppInsightsQuery
    {
        public AppInsightsConnectorConfiguration TargetDatabase { get; set; }
        public string QueryString { get; set; }
        public string Label { get; set; }

        public Uri CreateDeeplink()
        {
            return AppInsightsQueryHelper.GenerateQueryDeeplink(TargetDatabase, QueryString);
        }
    }
}
