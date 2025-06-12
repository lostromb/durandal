using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Time.Scheduling;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Monitoring
{
    /// <summary>
    /// A driver which queues up test executions each to their own, new system thread. This allows the warden / watchdog / whatever
    /// to make sure tests cannot run for too long or run away
    /// </summary>
    public class NonCooperativeMonitorDriver : IMonitorDriver
    {
        /// <summary>
        /// The minimum amount of time to schedule successive runs of the same test case.
        /// Used as a safeguard to prevent runaway tests.
        /// </summary>
        private static readonly TimeSpan MINIMUM_TEST_INTERVAL = TimeSpan.FromSeconds(5);

        /// <summary>
        /// A thread pool that runs the tasks which observe the runner threads. I know, lots of threads
        /// </summary>
        private IThreadPool _wardenThreadPool;

        /// <summary>
        /// Cancel work items that run for longer than this
        /// </summary>
        private readonly TimeSpan _maxThreadLifetime;

        private ILogger _logger;
        private int _disposed = 0;

        public NonCooperativeMonitorDriver(ILogger logger, TimeSpan? maxTestLifetime = null)
        {
            _logger = logger;
            _maxThreadLifetime = maxTestLifetime.GetValueOrDefault(TimeSpan.FromSeconds(90));
            _wardenThreadPool = new SystemThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~NonCooperativeMonitorDriver()
        {
            Dispose(false);
        }
#endif

        public void QueueTest(IServiceMonitor testCase, ITestResultStore testResultStore, DeltaClock<IServiceMonitor> testScheduler, int testScaleDenominator, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            IRealTimeProvider threadLocalTime = realTime.Fork("NonCooperativeMonitorThread");
            _wardenThreadPool.EnqueueUserAsyncWorkItem(async () =>
            {
                try
                {
                    await RunNonCooperative(testCase, testResultStore, testScheduler, testScaleDenominator, threadLocalTime, cancelToken).ConfigureAwait(false);
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
                return _wardenThreadPool.TotalWorkItems;
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
                _wardenThreadPool?.Dispose();
            }
        }

        private async Task RunNonCooperative(IServiceMonitor testCase, ITestResultStore testResultStore, DeltaClock<IServiceMonitor> testScheduler, int testScaleDenominator, IRealTimeProvider wardenThreadTime, CancellationToken cancelToken)
        {
            Guid traceId = Guid.NewGuid();
            DateTimeOffset wardenStart = wardenThreadTime.Time;
            SingleTestResultInternal testResult = null;

            try
            {
                // Start an isolated thread to do the processing in
                // We can't use thread pools because we need a way to forcibly terminate a process to enforce SLA, and I can't find
                // a design that uses pooled tasks AND non-cooperative task cancellation.
                IRealTimeProvider testThreadTime = wardenThreadTime.Fork("NonCooperativeTestExecutor");
                int hasMergedThreadTime = 0;
                Thread remoteExecutionThread = new Thread(() =>
                {
                    Stopwatch latencyTimer = Stopwatch.StartNew();
                    DateTimeOffset testStart = testThreadTime.Time;
                    try
                    {
                        _logger.Log("Executing " + testCase.TestName, LogLevel.Vrb);
                        SingleTestResult reportedResult = testCase.Run(traceId, cancelToken, testThreadTime).Await();
                        latencyTimer.Stop();
                        double finalLatency = testThreadTime.IsForDebug ? (testThreadTime.Time - testStart).TotalMilliseconds : latencyTimer.ElapsedMillisecondsPrecise();
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
                            EndTimestamp = testThreadTime.Time,
                            TraceId = traceId,
                        };
                    }
                    catch (ThreadAbortException)
                    {
                        // Ignore this because it just means the plugin violated its timeout SLA
                    }
                    catch (Exception e)
                    {
                        latencyTimer.Stop();
                        double finalLatency = testThreadTime.IsForDebug ? (testThreadTime.Time - testStart).TotalMilliseconds : latencyTimer.ElapsedMillisecondsPrecise();

                        testResult = new SingleTestResultInternal()
                        {
                            TestName = testCase.TestName,
                            TestSuiteName = testCase.TestSuiteName,
                            ErrorMessage = BuildExceptionMessage(e),
                            Latency = TimeSpanExtensions.TimeSpanFromMillisecondsPrecise(finalLatency),
                            Success = false,
                            BeginTimestamp = testStart,
                            EndTimestamp = testThreadTime.Time,
                            TraceId = traceId,
                        };
                    }
                    finally
                    {
                        if (Interlocked.CompareExchange(ref hasMergedThreadTime, 1, 0) == 0)
                        {
                            testThreadTime.Merge();
                        }
                    }
                });

                remoteExecutionThread.Name = "Sandbox " + testCase.TestName;
                remoteExecutionThread.IsBackground = true;
                remoteExecutionThread.Start();

                bool terminateThread = false;
                while (remoteExecutionThread.IsAlive && !terminateThread)
                {
                    // Wait for thread to finish
                    await wardenThreadTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
                    terminateThread = (wardenThreadTime.Time - wardenStart) > _maxThreadLifetime;
                }

                if (terminateThread && remoteExecutionThread.IsAlive)
                {
                    remoteExecutionThread.Abort();

                    // Ensure that time provider gets merged if we destroy the thread
                    if (Interlocked.CompareExchange(ref hasMergedThreadTime, 1, 0) == 0)
                    {
                        testThreadTime.Merge();
                    }

                    testResult = new SingleTestResultInternal()
                    {
                        TestName = testCase.TestName,
                        TestSuiteName = testCase.TestSuiteName,
                        ErrorMessage = "The test ran for more than " + _maxThreadLifetime.TotalSeconds + " seconds and was forcibly terminated",
                        Latency = _maxThreadLifetime,
                        Success = false,
                        BeginTimestamp = wardenStart,
                        EndTimestamp = wardenThreadTime.Time,
                        TraceId = traceId,
                    };
                }
                else
                {
                    _logger.Log("Finished " + testCase.TestName + " after " + (wardenThreadTime.Time - wardenStart).TotalMilliseconds + " actual ms (" + testResult.Latency.TotalMilliseconds + " test ms)", LogLevel.Vrb);
                }
            }
            catch (Exception e)
            {
                // ??????
                _logger.Log(e, LogLevel.Err);
            }
            finally
            {
                if (testResult != null && !string.IsNullOrEmpty(testResult.ErrorMessage) && (_logger.ValidLogLevels & LogLevel.Vrb) != LogLevel.None)
                {
                    _logger.Log(testResult.ErrorMessage, LogLevel.Err);
                }

                // Augment the testing interval based on the number of instances in this service, so that
                // the total net throughput will remain constant if the service expands
                TimeSpan baseTestInterval = TimeSpan.FromTicks(testCase.QueryInterval.Ticks * testScaleDenominator);

                // don't allow tests to run crazy fast
                TimeSpan timeUntilNextTest = baseTestInterval - (wardenThreadTime.Time - wardenStart);
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
                    if (inner.InnerException == null)
                    {
                        exceptionMessage.AppendLine(inner.StackTrace);
                    }

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
