using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Swashbuckle.Swagger.Annotations;
using System.Threading.Tasks;
using Photon.Common.MySQL;
using Microsoft.WindowsAzure.ServiceRuntime;
using Durandal.Common.Logger;
using Photon;
using System.Text;
using Photon.StatusReporter.Razor;
using Photon.Common.AppInsights;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.Threading;
using Photon.StatusReporter.Repositories;
using Photon.Common.Monitors;
using Photon.Common.TestResultStore;
using Photon.Common.Schemas;
using Photon.Common.Config;

namespace Photon.StatusReporter.Controllers
{
    public class DashboardController : ApiController
    {
        #region Constants
        
        private static readonly TimeSpan MOVING_WINDOW_SIZE = TimeSpan.FromMinutes(20);

        #endregion

        #region Static controller state

        /// <summary>
        /// A repository to fetch test status from
        /// </summary>
        private static readonly TestStatusRepository _statusRepo;

        private static readonly ITestResultStore _testResultStore;

        /// <summary>
        /// The public-facing URL of this service, i.e. http://dorado-monitoring-prod.cloudapp.net
        /// </summary>
        private static readonly string _thisServiceUrl;

        private static ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        private static bool _initialized = false;

        #endregion

        /// <summary>
        /// Initializes monitor states and database connections
        /// </summary>
        static DashboardController()
        {
            GhettoGlobalState.Initialize();

            // only allow one initializer to ever run
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_initialized)
                {
                    return;
                }

                _lock.EnterWriteLock();
                try
                {
                    if (!_initialized)
                    {
                        _initialized = true;
                        ILogger serviceLogger = GhettoGlobalState.ServiceLogger;
                        // Build the SQL test result store
                        _testResultStore = new MySqlTestResultStore(GhettoGlobalState.SqlConnectionPool, serviceLogger.Clone("SqlTestResultStore"));

                        // And also build a copy of each monitor so we can do reflection on it (get its configured threshold, description, etc.)
                        Dictionary<string, IServiceMonitor> allMonitors = new Dictionary<string, IServiceMonitor>();
                        
                        IList<IServiceMonitor> monitors = MonitorCollection.BuildAllMonitors(serviceLogger, new BasicEnvironmentConfiguration());
                        foreach (IServiceMonitor monitor in monitors)
                        {
                            allMonitors[monitor.TestName] = monitor;
                        }

                        _statusRepo = new TestStatusRepository(_testResultStore, allMonitors);
                        _thisServiceUrl = RoleEnvironment.GetConfigurationSettingValue("LocalUrl");
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        [HttpGet]
        [Route("dashboard")]
        public async Task<IHttpActionResult> GetAllTestResult()
        {
            Dictionary<string, TestSuiteStatus> suiteResults = await _statusRepo.GetAllSuitesStatus(MOVING_WINDOW_SIZE);

            if (suiteResults == null)
            {
                return this.NotFound();
            }

            // Build a simple page compiling the test results
            AllSuitesStatusPage view = new AllSuitesStatusPage();
            view.SuiteResults = suiteResults.Values
                .Where((t) => !"InternalAlerting".Equals(t.SuiteName))
                .OrderBy((t) => t.SuiteName);
            view.WindowEnd = DateTimeOffset.UtcNow;
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

            HttpResponseMessage finalResponse = new HttpResponseMessage(HttpStatusCode.OK);
            finalResponse.Content = new StringContent(view.Render(), Encoding.UTF8, "text/html");
            return this.ResponseMessage(finalResponse);
        }

        [HttpGet]
        [Route("dashboard/suite/{suiteName}")]
        public async Task<IHttpActionResult> GetSuiteResult([FromUri]string suiteName)
        {
            TestSuiteStatus suiteResult = await _statusRepo.GetSuiteTestStatus(suiteName, MOVING_WINDOW_SIZE);

            if (suiteResult == null || suiteResult.TestResults == null || suiteResult.TestResults.Count == 0)
            {
                return this.NotFound();
            }

            SingleSuiteStatusPage view = new SingleSuiteStatusPage();
            view.SuiteResult = suiteResult;
            view.WindowEnd = DateTimeOffset.UtcNow;
            view.WindowStart = view.WindowEnd - MOVING_WINDOW_SIZE;

            StringBuilder testResultPage = new StringBuilder();

            bool suiteFailed = suiteResult.TestResults.Values.Any((t) => !t.IsPassing);

            HttpResponseMessage finalResponse = new HttpResponseMessage(HttpStatusCode.OK);
            finalResponse.Content = new StringContent(view.Render(), Encoding.UTF8, "text/html");
            return this.ResponseMessage(finalResponse);
        }

        [HttpGet]
        [Route("dashboard/test/{testName}")]
        public async Task<IHttpActionResult> GetSingleTestResult([FromUri]string testName)
        {
            TestMonitorStatus testResult = await _statusRepo.GetTestStatus(testName, MOVING_WINDOW_SIZE);

            if (testResult == null)
            {
                return this.NotFound();
            }

            // Build a simple page compiling the test results
            SingleTestStatusPage testResultPage = new SingleTestStatusPage();
            testResultPage.TestResult = testResult;
            testResultPage.WindowEnd = DateTimeOffset.UtcNow;
            testResultPage.WindowStart = testResultPage.WindowEnd - MOVING_WINDOW_SIZE;

            HttpResponseMessage finalResponse = new HttpResponseMessage(HttpStatusCode.OK);
            finalResponse.Content = new StringContent(testResultPage.Render(), Encoding.UTF8, "text/html");
            return this.ResponseMessage(finalResponse);
        }
    }
}
