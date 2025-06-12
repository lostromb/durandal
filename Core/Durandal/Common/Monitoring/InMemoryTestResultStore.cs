using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Monitoring
{
    public class InMemoryTestResultStore : ITestResultStore
    {
        private readonly TimeSpan _maxRetentionTime;
        private readonly List<SingleTestResultInternal> _results;
        private readonly Dictionary<string, IServiceMonitor> _allTests;
        private int _pruningCounter = 0;

        public InMemoryTestResultStore(IList<IServiceMonitor> allTests, TimeSpan maxRetentionTime)
        {
            if (allTests == null)
            {
                throw new ArgumentNullException(nameof(allTests));
            }

            _maxRetentionTime = maxRetentionTime;
            _results = new List<SingleTestResultInternal>();
            _allTests = new Dictionary<string, IServiceMonitor>();
            foreach (IServiceMonitor test in allTests)
            {
                _allTests[test.TestName] = test;
            }
        }

        public Task<Dictionary<string, TestSuiteStatus>> GetAllSuitesStatus(TimeSpan window, IRealTimeProvider realTime)
        {
            Dictionary<string, TestMonitorStatus> resultsPerTest = GetResultsPerTest(window, realTime);
            Dictionary<string, TestSuiteStatus> returnVal = new Dictionary<string, TestSuiteStatus>();

            foreach (TestMonitorStatus monitorStatus in resultsPerTest.Values)
            {
                TestSuiteStatus suiteStatus;
                if (!returnVal.TryGetValue(monitorStatus.TestSuiteName, out suiteStatus))
                {
                    suiteStatus = new TestSuiteStatus();
                    suiteStatus.SuiteName = monitorStatus.TestSuiteName;
                    returnVal[monitorStatus.TestSuiteName] = suiteStatus;
                }

                suiteStatus.TestResults[monitorStatus.TestName] = monitorStatus;
            }

            return Task.FromResult(returnVal);
        }

        public async Task<TestSuiteStatus> GetSuiteTestStatus(string suiteName, TimeSpan window, IRealTimeProvider realTime)
        {
            Dictionary<string, TestSuiteStatus> resultsPerSuite = await GetAllSuitesStatus(window, realTime).ConfigureAwait(false);
            TestSuiteStatus returnVal;

            if (resultsPerSuite.TryGetValue(suiteName, out returnVal))
            {
                return returnVal;
            }
            
            return null;
        }

        public async Task<TestMonitorStatus> GetTestStatus(string testName, TimeSpan window, IRealTimeProvider realTime)
        {
            Dictionary<string, TestMonitorStatus> resultsPerTest = GetResultsPerTest(window, realTime);
            TestMonitorStatus returnVal;

            if (resultsPerTest.TryGetValue(testName, out returnVal))
            {
                return returnVal;
            }

            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            return null;
        }

        public Task Store(SingleTestResultInternal testResult)
        {
            lock (_results)
            {
                _results.Add(testResult);

                // Prune old results here to save memory
                if (_pruningCounter++ > 1000)
                {
                    _pruningCounter = 0;

                    DateTimeOffset newestResult = new DateTimeOffset(1500, 1, 1, 0, 0, 0, TimeSpan.Zero);

                    List<SingleTestResultInternal> prunedResults = new List<SingleTestResultInternal>();
                    foreach (SingleTestResultInternal result in _results)
                    {
                        if (result.BeginTimestamp > newestResult)
                        {
                            newestResult = result.BeginTimestamp;
                        }

                        prunedResults.Add(result);
                    }

                    DateTimeOffset cutoffTime = newestResult - _maxRetentionTime;
                    _results.Clear();

                    foreach (SingleTestResultInternal result in prunedResults)
                    {
                        if (result.BeginTimestamp > cutoffTime)
                        {
                            _results.Add(result);
                        }
                    }
                }
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        private Dictionary<string, TestMonitorStatus> GetResultsPerTest(TimeSpan window, IRealTimeProvider realTime)
        {
            DateTimeOffset cutoffTime = realTime.Time - window;

            Dictionary<string, List<SingleTestResultInternal>> resultsPerTest = new Dictionary<string, List<SingleTestResultInternal>>();

            lock (_results)
            {
                foreach (SingleTestResultInternal result in _results)
                {
                    if (result.BeginTimestamp > cutoffTime)
                    {
                        List<SingleTestResultInternal> list;
                        if (!resultsPerTest.TryGetValue(result.TestName, out list))
                        {
                            list = new List<SingleTestResultInternal>();
                            resultsPerTest[result.TestName] = list;
                        }

                        list.Add(result);
                    }
                }
            }

            Dictionary<string, TestMonitorStatus> aggregatedStatuses = new Dictionary<string, TestMonitorStatus>();
            StaticAverage latencyAvg = new StaticAverage();
            StaticAverage passRateAvg = new StaticAverage();
            MovingPercentile medianLatency = new MovingPercentile(100, 0.5);

            foreach (List<SingleTestResultInternal> resultList in resultsPerTest.Values)
            {
                SingleTestResultInternal sampleTestResult = resultList[0];
                IServiceMonitor actualTest;
                if (!_allTests.TryGetValue(sampleTestResult.TestName, out actualTest))
                {
                    actualTest = null;
                }
                
                TestMonitorStatus monitorStatus = new TestMonitorStatus();
                monitorStatus.MonitoringWindow = window;
                monitorStatus.TestName = sampleTestResult.TestName;
                monitorStatus.TestsRan = resultList.Count;
                monitorStatus.TestSuiteName = sampleTestResult.TestSuiteName;
                monitorStatus.LastErrors = new List<ErrorResult>();

                if (actualTest == null)
                {
                    monitorStatus.PassRateThreshold = null;
                    monitorStatus.TestDescription = "";
                    monitorStatus.LatencyThreshold = null;
                }
                else
                {
                    monitorStatus.PassRateThreshold = actualTest.PassRateThreshold;
                    monitorStatus.TestDescription = actualTest.TestDescription;
                    monitorStatus.LatencyThreshold = actualTest.LatencyThreshold;
                }

                // Aggregate error messages and statistics
                latencyAvg.Reset();
                passRateAvg.Reset();
                medianLatency.Clear();
                foreach (SingleTestResultInternal result in resultList)
                {
                    latencyAvg.Add(result.Latency.TotalMilliseconds);
                    medianLatency.Add(result.Latency.TotalMilliseconds);

                    if (result.Success)
                    {
                        passRateAvg.Add(1);
                    }
                    else
                    {
                        passRateAvg.Add(0);
                        monitorStatus.LastErrors.Add(new ErrorResult()
                        {
                            BeginTimestamp = result.BeginTimestamp,
                            EndTimestamp = result.EndTimestamp,
                            Message = result.ErrorMessage,
                            TraceId = result.TraceId
                        });
                    }
                }

                monitorStatus.MeanLatency = TimeSpanExtensions.TimeSpanFromMillisecondsPrecise(latencyAvg.Average);
                monitorStatus.MedianLatency = TimeSpanExtensions.TimeSpanFromMillisecondsPrecise(medianLatency.GetPercentile(0.5));
                monitorStatus.PassRate = (float)passRateAvg.Average;

                aggregatedStatuses[sampleTestResult.TestName] = monitorStatus;
            }

            return aggregatedStatuses;
        }
    }
}
