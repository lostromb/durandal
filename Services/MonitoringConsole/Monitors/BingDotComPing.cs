using Durandal.Common.Monitoring.Monitors;
using Durandal.Common.Monitoring.Monitors.Http;
using Durandal.Common.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MonitorConsole.Monitors
{
    public class BingDotComPing : AbstractHttpMonitor
    {
        public BingDotComPing()
            : base(testName: "BingDotComPing",
                  testSuiteName: "Basic",
                  queryInterval: TimeSpan.FromSeconds(10),
                  targetUrl: "https://www.bing.com",
                  sslHostname: "www.bing.com",
                  timeout: TimeSpan.FromSeconds(5),
                  passRateThreshold: 0.5f,
                  latencyThreshold: TimeSpan.FromMilliseconds(1000))
        {
        }

        public override string TestDescription => "Pings Bing.com";
    }
}
