using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.MathExt;
using Durandal.Common.Time.Scheduling;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Monitoring
{
    public class MonitorRunner : IMetricSource
    {
        private readonly IList<IServiceMonitor> _testCases;
        private readonly ITestResultStore _testResultStore;
        private readonly ILogger _logger;
        private readonly int _testScaleDenominator;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly IRandom _random;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="testCases">The list of monitors to run</param>
        /// <param name="testResultStore">The place where test results should be written</param>
        /// <param name="logger">A logger for service errors or warnings (not test errors)</param>
        /// <param name="testScaleDenominator">A multiplier used to slow down the test rate for this local machine.
        /// Set this to the number of machines in a cluster to achieve the desired total test rate at the cluster level</param>
        /// <param name="metrics">A metric collector</param>
        /// <param name="metricDimensions">Dimensions to use for metric reporting</param>
        /// <param name="random">A random provider, used mainly for deterministic unit tests</param>
        public MonitorRunner(
            IList<IServiceMonitor> testCases,
            ITestResultStore testResultStore,
            ILogger logger,
            int testScaleDenominator = 1,
            IMetricCollector metrics = null,
            DimensionSet metricDimensions = null,
            IRandom random = null)
        {
            if (testCases == null)
            {
                throw new ArgumentNullException(nameof(testCases));
            }

            if (testResultStore == null)
            {
                throw new ArgumentNullException(nameof(testResultStore));
            }

            _random = random ?? new FastRandom();
            _dimensions = metricDimensions ?? DimensionSet.Empty;
            _logger = logger ?? NullLogger.Singleton;
            _testCases = testCases;
            _testResultStore = testResultStore;
            _testScaleDenominator = testScaleDenominator;
            _metrics = new WeakPointer<IMetricCollector>(metrics ?? NullMetricCollector.Singleton);
        }

        /// <summary>
        /// Runs all the given monitors in an infinite loop.
        /// </summary>
        /// <param name="driver">The driver for each monitor (the thing that actually runs the work item)</param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public async Task RunMonitorLoop(
            IMonitorDriver driver,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            // Are there any tests to run?
            if (_testCases.Count == 0)
            {
                _logger.Log("No tests to run. I guess I'm done!");
                return;
            }

            using (DeltaClock<IServiceMonitor> testScheduler = new DeltaClock<IServiceMonitor>(realTime))
            {
                // Put all of the tests we have onto the delta clock
                CreateInitialSchedule(_testCases, testScheduler, _logger, _random);

                while (!cancelToken.IsCancellationRequested)
                {
                    IServiceMonitor nextTestToRun = await testScheduler.WaitForNextEventAsync(cancelToken).ConfigureAwait(false);
                    if (nextTestToRun == null)
                    {
                        // This can happen if there are very few tests and all of them are currently running.... The driver is still running, but there's nothing to run at the moment
                        //_logger.Log("No tests to run at the moment...", LogLevel.Vrb);
                        await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _metrics.Value.ReportInstant("Tests Queued / sec", _dimensions);
                        _logger.Log("Queueing " + nextTestToRun.TestName, LogLevel.Vrb);
                        driver.QueueTest(nextTestToRun, _testResultStore, testScheduler, _testScaleDenominator, cancelToken, realTime);
                    }
                }
            }
        }

        private static void CreateInitialSchedule(
            IEnumerable<IServiceMonitor> testCases,
            DeltaClock<IServiceMonitor> targetClock,
            ILogger logger,
            IRandom rand)
        {
            // Process exclusivity groups and create a set of all clustered periodic events that will represent test executions
            Dictionary<string, List<PeriodicEvent<IServiceMonitor>>> exclusivityGroups = new Dictionary<string, List<PeriodicEvent<IServiceMonitor>>>();
            foreach (IServiceMonitor testCase in testCases)
            {
                string group = testCase.ExclusivityKey;
                if (string.IsNullOrEmpty(group))
                {
                    group = "null";
                }

                if (!exclusivityGroups.ContainsKey(group))
                {
                    exclusivityGroups.Add(group, new List<PeriodicEvent<IServiceMonitor>>());
                }

                exclusivityGroups[group].Add(new PeriodicEvent<IServiceMonitor>()
                {
                    Object = testCase,
                    Offset = TimeSpan.Zero,
                    Period = testCase.QueryInterval
                });
            }

            // Solve for near-optimal scheduling of all these tests with respect to their exclusivity groups
            // and schedule them on the delta clock
            TimeSpan groupOffset = TimeSpan.Zero;
            foreach (KeyValuePair<string, List<PeriodicEvent<IServiceMonitor>>> schedulingGroup in exclusivityGroups)
            {
                logger.Log("Calculating optimal test schedule for group \"" + schedulingGroup.Key + "\"");
                PeriodicExclusivitySolver.Solve(schedulingGroup.Value, rand);
                foreach (PeriodicEvent<IServiceMonitor> monitor in schedulingGroup.Value)
                {
                    targetClock.ScheduleEvent(monitor.Object, monitor.Offset + groupOffset);
                }
                
                // Offset each group by a few seconds as well in case every group has one event that runs at time = 0
                groupOffset = groupOffset + TimeSpan.FromSeconds(2);
            }

            logger.Log("All tests are scheduled on delta clock");
        }

        public void InitializeMetrics(IMetricCollector collector)
        {
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
        }
    }
}
