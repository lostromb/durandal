using Durandal.Common.Utils.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.ICM
{
    public class InMemoryAlertRepository : IAlertRepository
    {
        private readonly List<TestAlertStatus> _alertConfigs;
        private readonly IRealTimeProvider _timeProvider;

        /// <summary>
        /// Creates a new alert repository that is stored entirely in-memory
        /// </summary>
        /// <param name="timeProvider">An object which defines how time works; used only for testing. If null, time works as expected on Earth</param>
        public InMemoryAlertRepository(IRealTimeProvider timeProvider = null)
        {
            _alertConfigs = new List<TestAlertStatus>();
            _timeProvider = timeProvider ?? new DefaultRealTimeProvider();
        }

        public Task<DateTimeOffset?> GetMostRecentAlertTime(string teamName, AlertLevel level)
        {
            DateTimeOffset? returnVal = null;
            foreach (TestAlertStatus status in _alertConfigs)
            {
                if (status.MostRecentFailureLevel == level &&
                    string.Equals(status.OwningTeamName, teamName, StringComparison.Ordinal) &&
                    status.MostRecentFailureBegin.HasValue)
                {
                    if (returnVal == null || returnVal.Value < status.MostRecentFailureBegin.Value)
                    {
                        returnVal = status.MostRecentFailureBegin;
                    }
                }
            }

            return Task.FromResult(returnVal);
        }

        public Task<IDictionary<string, SuiteAlertStatus>> GetAllSuitesAlertStatus()
        {
            IDictionary<string, SuiteAlertStatus> returnVal = new Dictionary<string, SuiteAlertStatus>();

            foreach (TestAlertStatus status in _alertConfigs)
            {
                if (!returnVal.ContainsKey(status.SuiteName))
                {
                    returnVal[status.SuiteName] = new SuiteAlertStatus()
                    {
                        SuiteName = status.SuiteName
                    };
                }
                
                returnVal[status.SuiteName].TestStatus.Add(status.TestName, status);
            }

            return Task.FromResult(returnVal);
        }

        public Task<SuiteAlertStatus> GetSuiteAlertStatus(string suiteName)
        {
            SuiteAlertStatus returnVal = new SuiteAlertStatus()
            {
                SuiteName = suiteName
            };

            foreach (TestAlertStatus status in _alertConfigs)
            {
                if (string.Equals(suiteName, status.SuiteName))
                {
                    returnVal.TestStatus.Add(status.TestName, status);
                }
            }

            return Task.FromResult(returnVal);
        }

        public Task<TestAlertStatus> GetTestAlertStatus(string testName)
        {
            foreach (TestAlertStatus status in _alertConfigs)
            {
                if (string.Equals(testName, status.TestName))
                {
                    return Task.FromResult(status);
                }
            }

            return Task.FromResult<TestAlertStatus>(null);
        }

        public Task UpdateTestAlertConfig(string testName, string suiteName, AlertLevel newDefaultLevel)
        {
            foreach (TestAlertStatus status in _alertConfigs)
            {
                if (string.Equals(testName, status.TestName) &&
                    string.Equals(suiteName, status.SuiteName))
                {
                    status.DefaultFailureLevel = newDefaultLevel;
                    return Task.Delay(0);
                }
            }

            // Task config not found. Make a new entry
            TestAlertStatus newConfig = new TestAlertStatus()
            {
                TestName = testName,
                SuiteName = suiteName,
                DefaultFailureLevel = newDefaultLevel,
                MostRecentFailureBegin = null,
                MostRecentFailureEnd = null,
                MostRecentFailureLevel = AlertLevel.NoAlert,
                OwningTeamName = null
            };

            _alertConfigs.Add(newConfig);
            return Task.Delay(0);
        }

        public Task StoreAlertEvent(string testName, string suiteName, AlertLevel failingLevel, string responsibleTeamName)
        {
            DateTimeOffset currentTime = _timeProvider.Time;

            foreach (TestAlertStatus status in _alertConfigs)
            {
                if (string.Equals(testName, status.TestName) &&
                    string.Equals(suiteName, status.SuiteName))
                {
                    // Does this event correlate to any previous failure? If not, mark this as the beginning of a new alert event
                    if (!status.MostRecentFailureBegin.HasValue ||
                        !status.MostRecentFailureEnd.HasValue ||
                        status.MostRecentFailureEnd.Value < currentTime - AlertEventProcessor.ALERT_CORRELATION_WINDOW)
                    {
                        status.MostRecentFailureBegin = currentTime;
                    }

                    status.MostRecentFailureLevel = failingLevel;
                    status.MostRecentFailureEnd = currentTime;
                    status.OwningTeamName = responsibleTeamName;
                    
                    return Task.Delay(0);
                }
            }

            // Task config not found. Make a new entry
            TestAlertStatus newConfig = new TestAlertStatus()
            {
                TestName = testName,
                SuiteName = suiteName,
                DefaultFailureLevel = AlertLevel.NoAlert,
                MostRecentFailureBegin = currentTime,
                MostRecentFailureEnd = currentTime,
                MostRecentFailureLevel = failingLevel,
                OwningTeamName = responsibleTeamName
            };

            _alertConfigs.Add(newConfig);
            return Task.Delay(0);
        }
    }
}
