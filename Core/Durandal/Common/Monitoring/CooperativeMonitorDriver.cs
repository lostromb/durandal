using Durandal.Common.Logger;
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
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Monitoring
{
    /// <summary>
    /// A simple driver which queues up test executions in a cooperative way (i.e. the test is expected to behave properly).
    /// All tests are run on a single thread pool provided to this class during construction.
    /// </summary>
    public class CooperativeMonitorDriver : IMonitorDriver
    {
        /// <summary>
        /// The minimum amount of time to schedule successive runs of the same test case.
        /// Used as a safeguard to prevent runaway tests.
        /// </summary>
        private static readonly TimeSpan MINIMUM_TEST_INTERVAL = TimeSpan.FromSeconds(5);

        private readonly IThreadPool _threadPool;
        private readonly ILogger _logger;
        private int _disposed = 0;

        public CooperativeMonitorDriver(ILogger logger, IThreadPool runnerThreadPool)
        {
            _threadPool = runnerThreadPool;
            _logger = logger;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~CooperativeMonitorDriver()
        {
            Dispose(false);
        }
#endif

        public void QueueTest(IServiceMonitor testCase, ITestResultStore testResultStore, DeltaClock<IServiceMonitor> testScheduler, int testScaleDenominator, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            IRealTimeProvider threadLocalTime = realTime.Fork("CooperativeMonitorTest");
            _threadPool.EnqueueUserAsyncWorkItem(
                async () =>
                {
                    try
                    {
                        await RunSingleTestCase(testCase, testResultStore, testScheduler, testScaleDenominator, threadLocalTime, cancelToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
        }

        public int QueuedTestCount
        {
            get
            {
                return _threadPool.TotalWorkItems;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
            }
        }

        private async Task RunSingleTestCase(
            IServiceMonitor testCase,
            ITestResultStore testResultStore,
            DeltaClock<IServiceMonitor> testScheduler,
            int testScaleDenominator,
            IRealTimeProvider threadLocalTime,
            CancellationToken cancelToken)
        {
            DateTimeOffset testStart = threadLocalTime.Time;
            Guid traceId = Guid.NewGuid();
            Stopwatch latencyTimer = Stopwatch.StartNew();
            SingleTestResultInternal testResult = null;
            try
            {
                _logger.Log("Executing " + testCase.TestName, LogLevel.Vrb);
                SingleTestResult reportedResult = await testCase.Run(traceId, cancelToken, threadLocalTime).ConfigureAwait(false);
                latencyTimer.Stop();
                double finalLatency = threadLocalTime.IsForDebug ? (threadLocalTime.Time - testStart).TotalMilliseconds : latencyTimer.ElapsedMillisecondsPrecise();
                if (reportedResult.OverrideTestExecutionTime.HasValue)
                {
                    finalLatency = reportedResult.OverrideTestExecutionTime.Value.TotalMilliseconds;
                }

                testResult = new SingleTestResultInternal()
                {
                    TestName = testCase.TestName,
                    TestSuiteName = testCase.TestSuiteName,
                    ErrorMessage = reportedResult.ErrorMessage,
                    Latency = TimeSpanExtensions.TimeSpanFromMillisecondsPrecise(finalLatency),
                    Success = reportedResult.Success,
                    BeginTimestamp = testStart,
                    EndTimestamp = threadLocalTime.Time,
                    TraceId = traceId,
                };
            }
            catch (Exception e)
            {
                latencyTimer.Stop();
                double finalLatency = threadLocalTime.IsForDebug ? (threadLocalTime.Time - testStart).TotalMilliseconds : latencyTimer.ElapsedMillisecondsPrecise();

                testResult = new SingleTestResultInternal()
                {
                    TestName = testCase.TestName,
                    TestSuiteName = testCase.TestSuiteName,
                    ErrorMessage = BuildExceptionMessage(e),
                    Latency = TimeSpanExtensions.TimeSpanFromMillisecondsPrecise(finalLatency),
                    Success = false,
                    BeginTimestamp = testStart,
                    EndTimestamp = threadLocalTime.Time,
                    TraceId = traceId,
                };
            }
            finally
            {
                _logger.Log("Finished " + testCase.TestName + " with success = " + testResult.Success + " after " + testResult.Latency.TotalMilliseconds + " reported ms", LogLevel.Vrb);
                if (testResult != null && !string.IsNullOrEmpty(testResult.ErrorMessage) && (_logger.ValidLogLevels & LogLevel.Vrb) != LogLevel.None)
                {
                    _logger.Log(testResult.ErrorMessage, LogLevel.Err);
                }

                // Augment the testing interval based on the number of instances in this service, so that
                // the total net throughput will remain constant if the service expands
                TimeSpan baseTestInterval = TimeSpan.FromTicks(testCase.QueryInterval.Ticks * testScaleDenominator);

                // don't allow tests to run crazy fast
                TimeSpan timeUntilNextTest = baseTestInterval - (threadLocalTime.Time - testStart);
                if (timeUntilNextTest < MINIMUM_TEST_INTERVAL)
                {
                    timeUntilNextTest = MINIMUM_TEST_INTERVAL;
                }

                // Reschedule the test and write the test results to the database
                testScheduler.ScheduleEvent(testCase, timeUntilNextTest);

                if (testResultStore != null)
                {
                    try
                    {
                        await testResultStore.Store(testResult).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                }
            }
        }

        private static string BuildExceptionMessage(Exception e)
        {
            if (e == null)
            {
                return "Unknown exception: null";
            }


            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder exceptionMessage = pooledSb.Builder;
                exceptionMessage.AppendLine(e.GetType().Name + ": " + e.Message);
                Exception inner = e.InnerException;
                int nestCount = 0;
                while (inner != null && nestCount++ < 3)
                {
                    exceptionMessage.AppendLine("Inner exception: " + inner.GetType().Name + ": " + inner.Message);
                    inner = inner.InnerException;
                }

                if (e.StackTrace != null)
                {
                    exceptionMessage.AppendLine(e.StackTrace);
                }

                return exceptionMessage.ToString();
            }
        }
    }
}
