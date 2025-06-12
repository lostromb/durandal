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
using Photon.Common.ICM;
using Photon.Common.TestResultStore;
using Photon.Common.Schemas;
using Photon.Common.Config;

namespace Photon.StatusReporter.Controllers
{
    public class StatusController : ApiController
    {
        #region Constants

        private static readonly TimeSpan MOVING_WINDOW_SIZE = TimeSpan.FromMinutes(20);
        private const int MINIMUM_TEST_RUNS_REQUIRED = 4;

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
        static StatusController()
        {
            GhettoGlobalState.Initialize();
            
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
        [Route("api/status")]
        public async Task<IHttpActionResult> GetAllTestResult()
        {
            Dictionary<string, TestSuiteStatus> suiteResults = await _statusRepo.GetAllSuitesStatus(MOVING_WINDOW_SIZE);

            if (suiteResults == null)
            {
                return this.NotFound();
            }

            bool allSuitesPassed = true;
            foreach (TestSuiteStatus suiteResult in suiteResults.Values)
            {
                bool suitePassed = !suiteResult.TestResults.Values.Any((t) =>!t.IsPassing && t.TestsRan >= MINIMUM_TEST_RUNS_REQUIRED);
                allSuitesPassed = suitePassed && allSuitesPassed;
            }

            HttpResponseMessage finalResponse = new HttpResponseMessage(allSuitesPassed ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
            finalResponse.Content = BuildDefaultHttpResponse(allSuitesPassed ? "All suites are passing. " : "One or more suites are failing. ");
            return this.ResponseMessage(finalResponse);
        }

        [HttpGet]
        [Route("api/status/suite/{suiteName}")]
        public async Task<IHttpActionResult> GetSuiteResult([FromUri]string suiteName)
        {
            TestSuiteStatus suiteResult = await _statusRepo.GetSuiteTestStatus(suiteName, MOVING_WINDOW_SIZE);

            if (suiteResult == null || suiteResult.TestResults == null || suiteResult.TestResults.Count == 0)
            {
                return this.NotFound();
            }

            bool suitePassing = !suiteResult.TestResults.Values.Any((t) => !t.IsPassing && t.TestsRan >= MINIMUM_TEST_RUNS_REQUIRED);

            HttpResponseMessage finalResponse = new HttpResponseMessage(suitePassing ? HttpStatusCode.OK: HttpStatusCode.ServiceUnavailable);
            finalResponse.Content = BuildDefaultHttpResponse(suitePassing ? "All tests are passing. " : "One or more test are failing. ");
            return this.ResponseMessage(finalResponse);
        }

        [HttpGet]
        [Route("api/status/test/{testName}")]
        public async Task<IHttpActionResult> GetSingleTestResult([FromUri]string testName)
        {
            TestMonitorStatus testResult = await _statusRepo.GetTestStatus(testName, MOVING_WINDOW_SIZE);

            if (testResult == null)
            {
                return this.NotFound();
            }

            bool testPassing = testResult.IsPassing || testResult.TestsRan < MINIMUM_TEST_RUNS_REQUIRED;

            HttpResponseMessage finalResponse = new HttpResponseMessage(testPassing ?  HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
            finalResponse.Content = BuildDefaultHttpResponse(testPassing ? "The test is passing. " : "The test is failing. ");
            return this.ResponseMessage(finalResponse);
        }

        private static HttpContent BuildDefaultHttpResponse(string message = "")
        {
            string page = string.Format("<html><body>" + message + "Please refer to the <a href=\"{0}/dashboard\">monitoring dashboard</a> for up-to-date status.</body></html>", _thisServiceUrl);
            return new StringContent(page, Encoding.UTF8, "text/html");
        }
    }
}
