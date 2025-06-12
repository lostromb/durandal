using Durandal.Common.Utils.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Photon;
using Photon.Common.Schemas;
using Photon.Common.TestResultStore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.AppInsights
{
    public class AppInsightsTestResultStore : ITestResultStore
    {
        private TelemetryClient _telemetry;
        
        public AppInsightsTestResultStore(TelemetryConfiguration config)
        {
            _telemetry = new TelemetryClient(config);
        }

        public Task<Dictionary<string, TestSuiteStatus>> GetAllSuitesStatus(TimeSpan window)
        {
            return Task.FromResult<Dictionary<string, TestSuiteStatus>>(null);
        }

        public Task<TestSuiteStatus> GetSuiteTestStatus(string suiteName, TimeSpan window)
        {
            return Task.FromResult<TestSuiteStatus>(null);
        }

        public Task<TestMonitorStatus> GetTestStatus(string testName, TimeSpan window)
        {
            return Task.FromResult<TestMonitorStatus>(null);
        }

        public async Task Store(SingleTestResultInternal testResult)
        {
            EventTelemetry convertedEvent = new EventTelemetry()
            {
                Timestamp = testResult.Timestamp,
                Name = "TestRan"
            };

            convertedEvent.Metrics.Add("Latency", testResult.LatencyMs);
            convertedEvent.Properties.Add("TestName", testResult.TestName);
            convertedEvent.Properties.Add("SuiteName", testResult.TestSuiteName);
            convertedEvent.Properties.Add("Success", testResult.Success ? "1" : "0");
            convertedEvent.Properties.Add("TraceId", testResult.TraceId.ToString("N").ToLowerInvariant());
            if (!string.IsNullOrEmpty(testResult.DatacenterName))
            {
                convertedEvent.Properties.Add("DC", testResult.DatacenterName);
            }
            if (!string.IsNullOrEmpty(testResult.ErrorMessage))
            {
                convertedEvent.Properties.Add("ErrorMessage", testResult.ErrorMessage);
            }

            _telemetry.TrackEvent(convertedEvent);
            await DurandalTaskExtensions.NoOpTask;
        }
    }
}
