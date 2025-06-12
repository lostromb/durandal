using Durandal.Common.Monitoring;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using Durandal.MonitorConsole.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MonitorConsole.Controllers
{
    public class DashboardController
    {
        private static readonly TimeSpan MOVING_WINDOW_SIZE = TimeSpan.FromMinutes(10);
        private readonly ITestResultStore _statusRepo;

        public DashboardController(ITestResultStore testResultStore)
        {
            _statusRepo = testResultStore;
        }
        
        public async Task<HttpResponse> GetAllTestResult(IRealTimeProvider realTime)
        {
            Dictionary<string, TestSuiteStatus> suiteResults = await _statusRepo.GetAllSuitesStatus(MOVING_WINDOW_SIZE, realTime);

            if (suiteResults == null)
            {
                return HttpResponse.NotFoundResponse();
            }

            // Build a simple page compiling the test results
            AllSuitesStatusPage view = new AllSuitesStatusPage();
            view.SuiteResults = suiteResults.Values
                .Where((t) => !"InternalAlerting".Equals(t.SuiteName))
                .OrderBy((t) => t.SuiteName);
            view.WindowEnd = realTime.Time;
            view.WindowStart = view.WindowEnd - MOVING_WINDOW_SIZE;

            StringBuilder testResultPage = new StringBuilder();

            bool allSuitesPassed = true;
            foreach (TestSuiteStatus suiteResult in suiteResults.Values)
            {
                bool suitePassed = !suiteResult.TestResults.Values.Any((t) => !t.IsPassing);
                allSuitesPassed = suitePassed && allSuitesPassed;

                // Strip all error messages out of this view just to prevent clutter
                foreach (TestMonitorStatus monitorResult in suiteResult.TestResults.Values)
                {
                    if (monitorResult.LastErrors != null)
                    {
                        monitorResult.LastErrors.Clear();
                    }
                }
            }

            HttpResponse finalResponse = HttpResponse.OKResponse();
            finalResponse.SetContent(view.Render(), "text/html");
            return finalResponse;
        }
        
        public async Task<HttpResponse> GetSuiteResult(IRealTimeProvider realTime, string suiteName)
        {
            TestSuiteStatus suiteResult = await _statusRepo.GetSuiteTestStatus(suiteName, MOVING_WINDOW_SIZE, realTime);

            if (suiteResult == null || suiteResult.TestResults == null || suiteResult.TestResults.Count == 0)
            {
                return HttpResponse.NotFoundResponse();
            }

            SingleSuiteStatusPage view = new SingleSuiteStatusPage();
            view.SuiteResult = suiteResult;
            view.WindowEnd = realTime.Time;
            view.WindowStart = view.WindowEnd - MOVING_WINDOW_SIZE;

            StringBuilder testResultPage = new StringBuilder();

            bool suiteFailed = suiteResult.TestResults.Values.Any((t) => !t.IsPassing);

            HttpResponse finalResponse = HttpResponse.OKResponse();
            finalResponse.SetContent(view.Render(), "text/html");
            return finalResponse;
        }

        public async Task<HttpResponse> GetSingleTestResult(IRealTimeProvider realTime, string testName)
        {
            TestMonitorStatus testResult = await _statusRepo.GetTestStatus(testName, MOVING_WINDOW_SIZE, realTime);

            if (testResult == null)
            {
                return HttpResponse.NotFoundResponse();
            }

            // Build a simple page compiling the test results
            SingleTestStatusPage view = new SingleTestStatusPage();
            view.TestResult = testResult;
            view.WindowEnd = realTime.Time;
            view.WindowStart = view.WindowEnd - MOVING_WINDOW_SIZE;

            HttpResponse finalResponse = HttpResponse.OKResponse();
            finalResponse.SetContent(view.Render(), "text/html");
            return finalResponse;
        }
    }
}
