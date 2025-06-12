using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Durandal.Common.Monitoring
{
    public interface ITestResultStore
    {
        Task Store(SingleTestResultInternal testResult);
        Task<TestSuiteStatus> GetSuiteTestStatus(string suiteName, TimeSpan window, IRealTimeProvider realTime);
        Task<TestMonitorStatus> GetTestStatus(string testName, TimeSpan window, IRealTimeProvider realTime);

        Task<Dictionary<string, TestSuiteStatus>> GetAllSuitesStatus(TimeSpan window, IRealTimeProvider realTime);
    }
}