using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.ICM
{
    public class SuiteAlertStatus
    {
        public Dictionary<string, TestAlertStatus> TestStatus { get; }
        public string SuiteName { get; set; }

        public SuiteAlertStatus()
        {
            TestStatus = new Dictionary<string, TestAlertStatus>();
        }
    }
}
