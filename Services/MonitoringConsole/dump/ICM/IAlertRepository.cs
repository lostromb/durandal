using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.ICM
{
    public interface IAlertRepository
    {
        /// <summary>
        /// Returns the overall alert status of all suites
        /// </summary>
        /// <returns></returns>
        Task<IDictionary<string, SuiteAlertStatus>> GetAllSuitesAlertStatus();
        
        /// <summary>
        /// Returns the overall alert status of the given suite
        /// </summary>
        /// <param name="suiteName"></param>
        /// <returns></returns>
        Task<SuiteAlertStatus> GetSuiteAlertStatus(string suiteName);

        /// <summary>
        /// Returns the test alert status / config of an individual test
        /// </summary>
        /// <param name="testName"></param>
        /// <returns></returns>
        Task<TestAlertStatus> GetTestAlertStatus(string testName);

        /// <summary>
        /// Updates the default alert level of the given test
        /// </summary>
        /// <param name="testName"></param>
        /// <param name="suiteName"></param>
        /// <param name="newDefaultLevel"></param>
        /// <returns></returns>
        Task UpdateTestAlertConfig(string testName, string suiteName, AlertLevel newDefaultLevel);

        /// <summary>
        /// Tells this repository to store the failure event for the specified test, at the specified alert level
        /// </summary>
        /// <param name="testName"></param>
        /// <param name="suiteName"></param>
        /// <param name="failingLevel"></param>
        /// <returns></returns>
        Task StoreAlertEvent(string testName, string suiteName, AlertLevel failingLevel, string responsibleTeamName);

        /// <summary>
        /// Returns the time of the most recent alert of the specified level that went to the specified team
        /// </summary>
        /// <param name="teamName"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        Task<DateTimeOffset?> GetMostRecentAlertTime(string teamName, AlertLevel level);
    }
}
