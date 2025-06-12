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
using Microsoft.AzureAd.Icm.Types;
using Photon.Common.Schemas;
using Photon.Common.Config;

namespace Photon.StatusReporter.Controllers
{
    public class AlertController : ApiController
    {
        #region Constants

        private static readonly TimeSpan MOVING_WINDOW_SIZE = TimeSpan.FromMinutes(20);

        /// <summary>
        /// A constant mapping of all ICM team names mapped to their corresponding alert webhook URLs
        /// </summary>
        private static List<DRITeam> DRI_TEAM_NAME_MAPPING;

        #endregion

        #region Static controller state

        /// <summary>
        /// A repository to fetch test status from
        /// </summary>
        private static readonly TestStatusRepository _statusRepo;

        /// <summary>
        /// The public-facing URL of this service, i.e. http://dorado-monitoring-prod.cloudapp.net
        /// </summary>
        private static readonly string _thisServiceUrl;

        /// <summary>
        /// Logic to handle when alerting events come
        /// </summary>
        private static AlertEventProcessor _alertProcessor;

        /// <summary>
        /// Repository for alerting config + state
        /// </summary>
        private static IAlertRepository _alertConfigRepository;
        
        /// <summary>
        /// A mapping from test name -> service monitor object so we can lookup all tests by name
        /// </summary>
        private static Dictionary<string, IServiceMonitor> _allMonitors;

        private static ILogger _serviceLogger;

        private static ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        private static bool _initialized = false;

        #endregion

        /// <summary>
        /// Initializes database connections
        /// </summary>
        static AlertController()
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

                        _serviceLogger = GhettoGlobalState.ServiceLogger;
                        // Build the SQL test result store
                        MySqlTestResultStore sqlStore = new MySqlTestResultStore(GhettoGlobalState.SqlConnectionPool, _serviceLogger.Clone("SqlTestResultStore"));

                        // And also build a copy of each monitor so we can do reflection on it (get its configured threshold, description, etc.)
                        _allMonitors = new Dictionary<string, IServiceMonitor>();
                        
                        IList<IServiceMonitor> monitors = MonitorCollection.BuildAllMonitors(_serviceLogger, new BasicEnvironmentConfiguration());
                        foreach (IServiceMonitor monitor in monitors)
                        {
                            _allMonitors[monitor.TestName] = monitor;
                        }

                        _statusRepo = new TestStatusRepository(sqlStore, _allMonitors);
                        _thisServiceUrl = RoleEnvironment.GetConfigurationSettingValue("LocalUrl");
                        bool isDev = string.Equals("DEV", RoleEnvironment.GetConfigurationSettingValue("Datacenter"));

                        if (isDev)
                        {
                            _serviceLogger.Log("Setting DEV icm configuration");
                            DRI_TEAM_NAME_MAPPING = new List<DRITeam>()
                        {
                            new DRITeam()
                            {
                                TeamFriendlyName ="DoradoCore",
                                CertThumbprint = string.Empty,
                                ConnectorId = Guid.Empty,
                                IcmId = "DoradoCoreIcmId"
                            },
                            new DRITeam()
                            {
                                TeamFriendlyName ="Portal",
                                CertThumbprint = string.Empty,
                                ConnectorId = Guid.Empty,
                                IcmId = "DoradoPortalIcmId"
                            },
                            new DRITeam()
                            {
                                TeamFriendlyName ="DoradoPortal",
                                CertThumbprint = string.Empty,
                                ConnectorId = Guid.Empty,
                                IcmId = "DoradoPortalIcmId"
                            }
                        };
                        }
                        else
                        {
                            // PROD TEAM CONFIGURATION
                            // TODO I really need to just put this in the database as well
                            _serviceLogger.Log("Setting PROD icm configuration");
                            DRI_TEAM_NAME_MAPPING = new List<DRITeam>()
                        {
                            new DRITeam() // First entry in the list is the default if no other team name is specified
                            {
                                TeamFriendlyName ="DoradoCore",
                                IcmId = "BINGPLATCORTANALANGUAGEUNDERSTANDING\\ConversationsEngineering",
                                CertThumbprint = "616B0ADA32F5DDB1A9CBC5C4E4CA0E4E6133EA44",
                                ConnectorId = Guid.Parse("551f11dd-aee4-4d87-9930-301eeaa3d16a"),
                            },
                            new DRITeam()
                            {
                                TeamFriendlyName ="Portal",
                                IcmId = "BINGPLATCORTANALANGUAGEUNDERSTANDING\\DoradoPortal",
                                CertThumbprint = "903B4D572AD62947FA86D62C19504FD30E874D09",
                                ConnectorId = Guid.Parse("61b118e0-2254-48ad-896a-111e6f4bdb2c"),
                            },
                            new DRITeam()
                            {
                                TeamFriendlyName ="DoradoPortal",
                                IcmId = "BINGPLATCORTANALANGUAGEUNDERSTANDING\\DoradoPortal",
                                CertThumbprint = "903B4D572AD62947FA86D62C19504FD30E874D09",
                                ConnectorId = Guid.Parse("61b118e0-2254-48ad-896a-111e6f4bdb2c"),
                            }
                        };
                        }

                        _alertConfigRepository = new MySqlAlertRepository(GhettoGlobalState.SqlConnectionPool, _serviceLogger.Clone("MySqlAlertRepository"));
                        _alertProcessor = new AlertEventProcessor(_alertConfigRepository, DRI_TEAM_NAME_MAPPING, _thisServiceUrl);
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

        /// <summary>
        /// The HTTP view for the main alert spreadsheet
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("alerts")]
        public async Task<IHttpActionResult> ShowAlertConfigPage()
        {
            // First, get alert status for all suites
            IDictionary<string, SuiteAlertStatus> allAlertStatus = await GetAllSuiteAlertStatus();

            // Now build the HTML page
            AlertPage view = new AlertPage();
            view.AllAlertStatus = allAlertStatus.OrderBy((a) => a.Key).Select((a) => a.Value);

            HttpResponseMessage finalResponse = new HttpResponseMessage(HttpStatusCode.OK);
            finalResponse.Content = new StringContent(view.Render(), Encoding.UTF8, "text/html");
            return this.ResponseMessage(finalResponse);
        }

        private async Task<IDictionary<string, SuiteAlertStatus>> GetAllSuiteAlertStatus()
        {
            HashSet<string> allSuiteNames = new HashSet<string>();
            HashSet<string> allTestNames = new HashSet<string>();
            foreach (IServiceMonitor monitor in _allMonitors.Values)
            {
                if (!allSuiteNames.Contains(monitor.TestSuiteName))
                {
                    allSuiteNames.Add(monitor.TestSuiteName);
                }
                if (!allTestNames.Contains(monitor.TestName))
                {
                    allTestNames.Add(monitor.TestName);
                }
            }

            IDictionary<string, SuiteAlertStatus> allAlertStatus = await _alertConfigRepository.GetAllSuitesAlertStatus();

            foreach (string suiteName in allSuiteNames)
            {
                SuiteAlertStatus thisSuiteAlertConfig;
                if (!allAlertStatus.TryGetValue(suiteName, out thisSuiteAlertConfig))
                {
                    thisSuiteAlertConfig = new SuiteAlertStatus()
                    {
                        SuiteName = suiteName
                    };
                    allAlertStatus[suiteName] = thisSuiteAlertConfig;
                }

                // Fill in all missing enties in the alert data, if we have no tests with no alert config
                foreach (IServiceMonitor monitor in _allMonitors.Values)
                {
                    if (string.Equals(monitor.TestSuiteName, suiteName) && 
                        !thisSuiteAlertConfig.TestStatus.ContainsKey(monitor.TestName))
                    {
                        thisSuiteAlertConfig.TestStatus[monitor.TestName] = new TestAlertStatus()
                        {
                            TestName = monitor.TestName,
                            SuiteName = suiteName,
                            DefaultFailureLevel = AlertLevel.NoAlert
                        };
                    }
                }

                // Remove entries for tests that no longer exist
                List<string> nonExistentTests = new List<string>();
                foreach (string testName in thisSuiteAlertConfig.TestStatus.Keys)
                {
                    if (!allTestNames.Contains(testName))
                    {
                        nonExistentTests.Add(testName);
                    }
                }
                foreach (string nonExistentTest in nonExistentTests)
                {
                    thisSuiteAlertConfig.TestStatus.Remove(nonExistentTest);
                }

                allAlertStatus[suiteName] = thisSuiteAlertConfig;
            }

            return allAlertStatus;
        }
        
        /// <summary>
        /// AJAX handler from the UI to batch-update a set of alerting configs
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("api/alerts/updateAlertConfig")]
        public async Task<IHttpActionResult> UpdateAlertConfig()
        {
            // Parse url encoded payload
            var formData = await this.Request.Content.ReadAsFormDataAsync();

            List<Tuple<string, string, AlertLevel>> alertUpdates = new List<Tuple<string, string, AlertLevel>>();

            // Turn it into a batch of alert config updates
            foreach (var key in formData.Keys)
            {
                string testName = key as string;
                if (testName == null)
                {
                    return this.BadRequest("TestName is null");
                }
                if (!_allMonitors.ContainsKey(testName))
                {
                    return this.BadRequest("Attempted to update a nonexistent test");
                }

                string[] alertValueStrings = formData.GetValues(testName);
                if (alertValueStrings == null || alertValueStrings.Length != 1 || string.IsNullOrEmpty(alertValueStrings[0]))
                {
                    return this.BadRequest("Value for key " + testName + " is unexpected");
                }

                int alertOrdinal;
                if (!int.TryParse(alertValueStrings[0], out alertOrdinal))
                {
                    return this.BadRequest("Alert value for key " + testName + " is not an integer. (value is " + alertValueStrings[0] + ")");
                }

                AlertLevel alertLevel;
                switch (alertOrdinal)
                {
                    case 0:
                        alertLevel = AlertLevel.NoAlert;
                        break;
                    case 1:
                        alertLevel = AlertLevel.Mute;
                        break;
                    case 2:
                        alertLevel = AlertLevel.Notify;
                        break;
                    case 3:
                        alertLevel = AlertLevel.Alert;
                        break;
                    default:
                        return this.BadRequest("Alert value for key " + testName + " is out of range (value is " + alertValueStrings[0] + ")");
                }

                string suiteName = _allMonitors[testName].TestSuiteName;
                alertUpdates.Add(new Tuple<string, string, AlertLevel>(testName, suiteName, alertLevel));
            }

            foreach (var update in alertUpdates)
            {
                await _alertConfigRepository.UpdateTestAlertConfig(update.Item1, update.Item2, update.Item3);
            }

            return this.Ok();

        }

        /// <summary>
        /// When AppInsights detects that one of the probes is bad (by one of the /status endpoints above returning a 503) then it will raise an alert to the webhook
        /// that goes to this method. When we get it, we have to intercept the message, add some more descriptive data to it, and pipe it to ICM webhook for escalation
        /// </summary>
        /// <param name="teamName"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("api/icm/team/{teamName}")]
        public async Task<IHttpActionResult> ProxyICMAlert([FromUri]string teamName)
        {
            // Parse the body as AppInsightsAlertData
            AppInsightsAlertData alertData = JsonConvert.DeserializeObject<AppInsightsAlertData>(await Request.Content.ReadAsStringAsync());

            TestSuiteStatus failingSuite = null;

            if (alertData.Context != null && alertData.Context.Condition != null)
            {
                string originalFailureDetails = alertData.Context.Condition.FailureDetails;

                // Try and find out what test suite this was hitting
                Regex testSuiteUrlMatcher = new Regex("\\/api\\/status\\/suite\\/([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
                Match m = testSuiteUrlMatcher.Match(originalFailureDetails);
                if (m.Success)
                {
                    string suiteName = m.Groups[1].Value;
                    failingSuite = await _statusRepo.GetSuiteTestStatus(suiteName, MOVING_WINDOW_SIZE);
                }
            }

            if (failingSuite == null)
            {
                return this.Ok();
            }

            RaiseAlertEvent alertingEvent = await _alertProcessor.ProcessAlert(teamName, failingSuite);

            if (alertingEvent == null)
            {
                return this.Ok("No alert generated.");
            }

            alertingEvent.DashboardLink = _thisServiceUrl + "/dashboard/suite/" + alertingEvent.FailingSuite;

            if (alertingEvent.Level == AlertLevel.NoAlert ||
                alertingEvent.Level == AlertLevel.Mute)
            {
                return this.Ok("No alert generated. " + JsonConvert.SerializeObject(alertingEvent));
            }

            if (alertingEvent.TargetTeam == null)
            {
                return this.Ok("No target team. " + JsonConvert.SerializeObject(alertingEvent));
            }

            if (string.IsNullOrEmpty(alertingEvent.TargetTeam.CertThumbprint) ||
                string.IsNullOrEmpty(alertingEvent.TargetTeam.IcmId) ||
                alertingEvent.TargetTeam.ConnectorId == Guid.Empty)
            {
                return this.InternalServerError(new ArgumentException("Target team " + alertingEvent.TargetTeam.TeamFriendlyName + " has an invalid ICM connector configuration. " +  JsonConvert.SerializeObject(alertingEvent)));
            }

            _serviceLogger.Log("GENERATING AN ALERT: Suite " + alertingEvent.FailingSuite + " LEVEL: " + alertingEvent.Level + " TEAM: " + alertingEvent.TargetTeam.TeamFriendlyName);

            // Send event over ICM API
            ICMAdapter icmClient = new ICMAdapter();
            IncidentAddUpdateResult alertResult = icmClient.GenerateAlert(alertingEvent);
            if (alertResult == null)
            {
                return this.InternalServerError(new ArgumentNullException("Null ICM status returned"));
            }

            _serviceLogger.Log("ICM response was Status:" + alertResult.Status + " IncidentId: " + alertResult.IncidentId);

            if (alertResult.Status == IncidentAddUpdateStatus.AddedNew ||
                alertResult.Status == IncidentAddUpdateStatus.UpdatedExisting)
            {
                return this.Ok();
            }
            else
            {
                return this.InternalServerError(new Exception("Invalid ICM status returned: Status:" + alertResult.Status + " SubStatus: " + alertResult.SubStatus));
            }
        }

        /// <summary>
        /// This endpoint behaves much the same as the other one, except that it only has a contract with AbstractInternalAlertMonitor so it doesn't use appinsights at all
        /// </summary>
        /// <param name="teamName"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("api/internal_alert/team/{teamName}")]
        public async Task<IHttpActionResult> ProxyInternalAlert([FromUri]string teamName)
        {
            // Parse the body as AppInsightsAlertData
            InternalAlertFailureDetails alertData = JsonConvert.DeserializeObject<InternalAlertFailureDetails>(await Request.Content.ReadAsStringAsync());

            TestSuiteStatus failingSuite = await _statusRepo.GetSuiteTestStatus(alertData.TargetSuiteName, MOVING_WINDOW_SIZE);

            if (failingSuite == null)
            {
                _serviceLogger.Log("Attempted to evaluate suite status for suite " + alertData.TargetSuiteName + " but it was not found!", LogLevel.Wrn);
                return this.NotFound();
            }

            RaiseAlertEvent alertingEvent = await _alertProcessor.ProcessAlert(teamName, failingSuite);

            if (alertingEvent == null)
            {
                return this.Ok("No alert generated.");
            }

            if (alertingEvent.Level == AlertLevel.NoAlert ||
                alertingEvent.Level == AlertLevel.Mute)
            {
                return this.Ok("No alert generated. " + JsonConvert.SerializeObject(alertingEvent));
            }

            alertingEvent.DashboardLink = _thisServiceUrl + "/dashboard/suite/" + alertingEvent.FailingSuite;

            if (alertingEvent.TargetTeam == null)
            {
                return this.Ok("No target team. " + JsonConvert.SerializeObject(alertingEvent));
            }

            if (string.IsNullOrEmpty(alertingEvent.TargetTeam.CertThumbprint) ||
                string.IsNullOrEmpty(alertingEvent.TargetTeam.IcmId) ||
                alertingEvent.TargetTeam.ConnectorId == Guid.Empty)
            {
                return this.InternalServerError(new ArgumentException("Target team " + alertingEvent.TargetTeam.TeamFriendlyName + " has an invalid ICM connector configuration. " + JsonConvert.SerializeObject(alertingEvent)));
            }

            _serviceLogger.Log("GENERATING AN ALERT: Suite " + alertingEvent.FailingSuite + " LEVEL: " + alertingEvent.Level + " TEAM: " + alertingEvent.TargetTeam.TeamFriendlyName);

            // Send event over ICM API
            try
            {
                ICMAdapter icmClient = new ICMAdapter();
                IncidentAddUpdateResult alertResult = icmClient.GenerateAlert(alertingEvent);
                if (alertResult == null)
                {
                    _serviceLogger.Log("Null ICM status returned");
                    return this.InternalServerError(new ArgumentNullException("Null ICM status returned"));
                }

                _serviceLogger.Log("ICM response was Status:" + alertResult.Status + " IncidentId: " + alertResult.IncidentId);

                if (alertResult.Status == IncidentAddUpdateStatus.AddedNew ||
                    alertResult.Status == IncidentAddUpdateStatus.UpdatedExisting)
                {
                    return this.Ok();
                }
                else
                {
                    return this.InternalServerError(new Exception("Invalid ICM status returned: Status:" + alertResult.Status + " SubStatus: " + alertResult.SubStatus));
                }
            }
            catch (Exception e)
            {
                _serviceLogger.Log("Error while sending event to ICM API", LogLevel.Err);
                _serviceLogger.Log(e, LogLevel.Err);
                return this.InternalServerError(e);
            }
        }
    }
}
