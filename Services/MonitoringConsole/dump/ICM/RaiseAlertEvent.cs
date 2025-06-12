using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.ICM
{
    public class RaiseAlertEvent
    {
        public string Message { get; set; }
        public DRITeam TargetTeam { get; set; }
        public AlertLevel Level { get; set; }
        public string FailingSuite { get; set; }
        public List<string> FailingTests { get; set; }
        public string DashboardLink { get; set; }
    }
}
