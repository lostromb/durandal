using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Monitoring
{
    public class NullTestResultStore : ITestResultStore
    {
        public static readonly NullTestResultStore Singleton = new NullTestResultStore();

        private NullTestResultStore() { }

        public Task<Dictionary<string, TestSuiteStatus>> GetAllSuitesStatus(TimeSpan window, IRealTimeProvider realTime)
        {
            return Task.FromResult(new Dictionary<string, TestSuiteStatus>());
        }

        public Task<TestSuiteStatus> GetSuiteTestStatus(string suiteName, TimeSpan window, IRealTimeProvider realTime)
        {
            return Task.FromResult<TestSuiteStatus>(null);
        }

        public Task<TestMonitorStatus> GetTestStatus(string testName, TimeSpan window, IRealTimeProvider realTime)
        {
            return Task.FromResult<TestMonitorStatus>(null);
        }

        public Task Store(SingleTestResultInternal testResult)
        {
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
