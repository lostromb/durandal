using Durandal.Common.Utils.Time;
using Photon.Common.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.ICM
{
    public class AlertEventProcessor
    {
        /// <summary>
        /// Ensure that the same DRI team will not get calls more frequently than this span.
        /// In other words, mute RaiseAlertEvents for this amount of time after the first one occurs
        /// </summary>
        public static readonly TimeSpan ALERT_DEDUPLICATION_WINDOW = TimeSpan.FromHours(1);
        
        /// <summary>
        /// If a failing test is set to "notify" and fails continually for longer than this amount, promote it to a full alert
        /// </summary>
        public static readonly TimeSpan NOTIFICATION_UPGRADE_TIME = TimeSpan.FromHours(12);

        /// <summary>
        /// If a test alert status was updated within this time window, consider is being part of an ongoing incident.
        /// If not, consider the failure as being the start of a new incident
        /// </summary>
        public static readonly TimeSpan ALERT_CORRELATION_WINDOW = TimeSpan.FromMinutes(15);

        private readonly IList<DRITeam> _driTeams;
        private readonly IAlertRepository _alertDb;
        private readonly string _localServiceUrl;
        private readonly IRealTimeProvider _timeProvider;

        /// <summary>
        /// Creates a new alert event processor that performs alerting logic
        /// </summary>
        /// <param name="alertDatabase">The database of alerting history</param>
        /// <param name="driTeams">A collection of DRI teams with escalation paths</param>
        /// <param name="localServiceUrl">The URL of this local server, used when the output of the alert contains links to local test results</param>
        /// <param name="timeProvider">An object which defines how time works; used only for testing. If null, time works as expected on Earth</param>
        public AlertEventProcessor(
            IAlertRepository alertDatabase,
            IList<DRITeam> driTeams,
            string localServiceUrl,
            IRealTimeProvider timeProvider = null)
        {
            _driTeams = driTeams;
            _alertDb = alertDatabase;
            _localServiceUrl = localServiceUrl;
            _timeProvider = timeProvider ?? new DefaultRealTimeProvider();
        }

        public async Task<RaiseAlertEvent> ProcessAlert(string targetTeamName, TestSuiteStatus currentSuiteStatus)
        {
            DRITeam responsibleTeam = ResolveResponsibleTeam(targetTeamName);
            if (responsibleTeam == null)
            {
                return null;
            }

            // See if there are existing alerts for the same suite
            SuiteAlertStatus suiteAlertStatus = await _alertDb.GetSuiteAlertStatus(currentSuiteStatus.SuiteName);
            
            // Create or update alert entry using severity data
            AlertLevel highestAlertLevel = AlertLevel.NoAlert;

            DateTimeOffset currentTime = _timeProvider.Time;
            Dictionary<string, AlertLevel> updatedTestAlertLevels = new Dictionary<string, AlertLevel>();

            // Determine the effective alert level based on failure history and test configuration
            List<string> failingTestNames = new List<string>();
            foreach (TestMonitorStatus testStatus in currentSuiteStatus.TestResults.Values)
            {
                if (!testStatus.IsPassing)
                {
                    failingTestNames.Add(testStatus.TestName);

                    // Fetch alert config for this test
                    TestAlertStatus testAlertStatus;
                    if (suiteAlertStatus.TestStatus.TryGetValue(testStatus.TestName, out testAlertStatus))
                    {
                        // Apply muting if needed
                        if (testAlertStatus.DefaultFailureLevel == AlertLevel.Mute)
                        {
                            continue;
                        }

                        AlertLevel thisTestAlertLevel = testAlertStatus.DefaultFailureLevel;

                        // If this test has been notifying for more than an hour, promote to alert
                        if (testAlertStatus.MostRecentFailureLevel == AlertLevel.Notify &&
                            testAlertStatus.MostRecentFailureEnd.HasValue &&
                            testAlertStatus.MostRecentFailureBegin.HasValue &&
                            testAlertStatus.MostRecentFailureEnd.Value > currentTime - ALERT_CORRELATION_WINDOW &&
                            testAlertStatus.MostRecentFailureBegin.Value < currentTime - NOTIFICATION_UPGRADE_TIME)
                        {
                            thisTestAlertLevel = AlertLevel.Alert;
                        }

                        highestAlertLevel = Max(highestAlertLevel, thisTestAlertLevel);

                        string testKey = currentSuiteStatus.SuiteName + "\t" + testStatus.TestName;
                        updatedTestAlertLevels[testKey] = thisTestAlertLevel;
                    }
                }
            }

            // Has team been alerted in last hour? Then mute the generated alert event
            // This step needs to be done before the StoreAlertEvent calls below because we don't want the current alert event to count
            DateTimeOffset? lastTeamAlertTime = await _alertDb.GetMostRecentAlertTime(targetTeamName, highestAlertLevel);
            bool suppressAsDuplicateAlert = lastTeamAlertTime.HasValue && lastTeamAlertTime.Value > currentTime - ALERT_DEDUPLICATION_WINDOW;

            // Write back alerting status for all tests
            foreach (KeyValuePair<string, AlertLevel> updatedAlertLevel in updatedTestAlertLevels)
            {
                int split = updatedAlertLevel.Key.IndexOf('\t');
                string suiteName = updatedAlertLevel.Key.Substring(0, split);
                string testName = updatedAlertLevel.Key.Substring(split + 1);
                // TODO this can be done in parallel if I really care about perf
                await _alertDb.StoreAlertEvent(testName, suiteName, updatedAlertLevel.Value, responsibleTeam.TeamFriendlyName);
            }

            if (suppressAsDuplicateAlert)
            {
                return new RaiseAlertEvent()
                {
                    Level = AlertLevel.NoAlert,
                    Message = "***SUPPRESSED***\r\n" + WriteSuiteStatusAsString(currentSuiteStatus),
                    TargetTeam = responsibleTeam,
                    FailingSuite = currentSuiteStatus.SuiteName,
                    FailingTests = failingTestNames
                };
            }

            return new RaiseAlertEvent()
            {
                Level = highestAlertLevel,
                Message = WriteSuiteStatusAsString(currentSuiteStatus),
                TargetTeam = responsibleTeam,
                FailingSuite = currentSuiteStatus.SuiteName,
                FailingTests = failingTestNames
            };
        }

        private static AlertLevel Max(AlertLevel a, AlertLevel b)
        {
            return ((int)a) > ((int)b) ? a : b;
        }

        private static AlertLevel Min(AlertLevel a, AlertLevel b)
        {
            return ((int)a) < ((int)b) ? a : b;
        }

        private DRITeam ResolveResponsibleTeam(string teamName)
        {
            // No DRI teams to alert to; return null
            if (_driTeams == null || _driTeams.Count == 0)
            {
                return null;
            }

            // Find out which team is responsible
            DRITeam responsibleTeam = null;
            foreach (var team in _driTeams)
            {
                if (string.Equals(team.TeamFriendlyName, teamName, StringComparison.OrdinalIgnoreCase))
                {
                    responsibleTeam = team;
                    break;
                }
            }

            // Default to the first team in the list
            if (responsibleTeam == null)
            {
                responsibleTeam = _driTeams[0];
            }

            return responsibleTeam;
        }

        private string WriteSuiteStatusAsString(TestSuiteStatus testSuiteStatus)
        {
            StringBuilder failureMessageBuilder = new StringBuilder();

            failureMessageBuilder.AppendFormat("Dorado monitoring detected one or more failures in the test suite {0}. ", testSuiteStatus.SuiteName);
            failureMessageBuilder.AppendFormat(@"Suite report link: {0}/dashboard/suite/{1} ", _localServiceUrl, testSuiteStatus.SuiteName);
            failureMessageBuilder.AppendFormat("Specific failures below: ");
            foreach (TestMonitorStatus testResult in testSuiteStatus.TestResults.Values)
            {
                if (!testResult.IsPassing)
                {
                    failureMessageBuilder.AppendFormat("TEST {0}: \r\n", testResult.TestName);
                    if (testResult.LatencyThreshold.HasValue && testResult.MedianLatency > testResult.LatencyThreshold.Value.TotalMilliseconds)
                    {
                        failureMessageBuilder.AppendFormat("Latency of {0} ms exceeds threshold of {1} \r\n", testResult.MedianLatency, testResult.LatencyThreshold.Value.TotalMilliseconds);
                    }

                    if (testResult.PassRateThreshold.HasValue && testResult.PassRate < testResult.PassRateThreshold.Value)
                    {
                        failureMessageBuilder.AppendFormat("Pass rate of {0} % is below threshold of {1} % \r\n", testResult.PassRate, testResult.PassRateThreshold.Value);
                    }

                    if (testResult.LastErrors != null && testResult.LastErrors.Count > 0)
                    {
                        failureMessageBuilder.AppendFormat("Last error ({0}) message: \"{1}\" \r\n", testResult.LastErrors[0].TraceId, Cap(testResult.LastErrors[0].Message, 500));
                    }

                    failureMessageBuilder.AppendFormat("Link to test results: {0}/dashboard/test/{1} \r\n", _localServiceUrl, testResult.TestName);
                }
            }

            return failureMessageBuilder.ToString();
        }

        private static string Cap(string val, int max)
        {
            if (val.Length > max)
            {
                return val.Substring(0, max);
            }
            else
            {
                return val;
            }
        }
    }
}
