using Dorado.Common.Utils.Scheduling;
using Photon.Common.Monitors;
using Photon.Common.Schemas;
using Durandal.Common.Logger;
using Durandal.Common.Utils.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Photon.Common.TestResultStore;

namespace Photon.Common.Scheduler
{
    /// <summary>
    /// A driver which queues up test executions each to their own, new system thread. This allows the warden / watchdog / whatever
    /// to make sure tests cannot run for too long or run away
    /// </summary>
    public class NonCooperativeMonitorDriver : IMonitorDriver
    {
        /// <summary>
        /// Cancel work items that run for longer than this
        /// </summary>
        private readonly TimeSpan MAX_THREAD_LIFETIME = TimeSpan.FromSeconds(90);

        private readonly TimeSpan MIN_TEST_EXECUTION_INTERVAL = TimeSpan.FromMilliseconds(5000);

        /// <summary>
        /// A thread pool that runs the tasks which observe the runner threads. I know, lots of threads
        /// </summary>
        private CustomThreadPool _wardenThreadPool;
        private ILogger _logger;

        public NonCooperativeMonitorDriver(ILogger logger, int threadPoolSize)
        {
            _logger = logger;
            _wardenThreadPool = new CustomThreadPool(logger, "WardenThreadPool", threadPoolSize);
        }

        public void QueueTest(IServiceMonitor testCase, ITestResultStore testResultStore, DeltaClock<IServiceMonitor> testScheduler, int testScaleDenominator, GlobalTestContext globalContext)
        {
            _wardenThreadPool.EnqueueUserAsyncWorkItem(() => RunNonCooperative(testCase, testResultStore, testScheduler, testScaleDenominator, globalContext));
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
            _wardenThreadPool.Dispose();
        }

        private async Task RunNonCooperative(IServiceMonitor testCase, ITestResultStore testResultStore, DeltaClock<IServiceMonitor> testScheduler, int testScaleDenominator, GlobalTestContext globalContext)
        {
            SingleTestResultInternal testResult = null;
            TestContext localContext = globalContext.CreateLocalTestContext(DateTimeOffset.UtcNow, Guid.NewGuid());

            // Latency measured within the task
            Stopwatch latencyTimer = new Stopwatch();

            // Latency of entire execution including overhead caused by starting the thread
            Stopwatch executionTimer = new Stopwatch();

            try
            {
                // Start an isolated thread to do the processing in
                // We can't use thread pools because we need a way to forcibly terminate a process to enforce SLA, and I can't find
                // a design that uses pooled tasks AND non-cooperative task cancellation.
                Thread remoteExecutionThread = new Thread(() =>
                {
                    latencyTimer.Start();
                    try
                    {
                        _logger.Log("Executing " + testCase.TestName, LogLevel.Vrb);
                        SingleTestResult reportedResult = testCase.Run(localContext).Await();
                        latencyTimer.Stop();
                        testResult = new SingleTestResultInternal()
                        {
                            TestName = testCase.TestName,
                            TestSuiteName = testCase.TestSuiteName,
                            ErrorMessage = reportedResult.ErrorMessage,
                            LatencyMs = latencyTimer.Elapsed.TotalMilliseconds,
                            Success = reportedResult.Success,
                            Timestamp = localContext.TestBeginTime,
                            TraceId = localContext.TraceId,
                            DatacenterName = localContext.Datacenter
                        };
                    }
                    catch (ThreadAbortException)
                    {
                        // Ignore this because it just means the plugin violated its timeout SLA
                    }
                    catch (Exception e)
                    {
                        latencyTimer.Stop();
                        testResult = new SingleTestResultInternal()
                        {
                            TestName = testCase.TestName,
                            TestSuiteName = testCase.TestSuiteName,
                            ErrorMessage = BuildExceptionMessage(e),
                            LatencyMs = latencyTimer.Elapsed.TotalMilliseconds,
                            Success = false,
                            Timestamp = localContext.TestBeginTime,
                            TraceId = localContext.TraceId,
                            DatacenterName = localContext.Datacenter
                        };
                    }
                    finally
                    {
                        latencyTimer.Stop();
                    }
                });

                remoteExecutionThread.Name = "Sandbox " + testCase.TestName;
                remoteExecutionThread.IsBackground = true;
                remoteExecutionThread.Start();

                executionTimer.Start();
                bool terminateThread = false;
                while (remoteExecutionThread.IsAlive && !terminateThread)
                {
                    Thread.Sleep(10); // Wait for thread to finish
                    terminateThread = executionTimer.Elapsed > MAX_THREAD_LIFETIME;
                }
                executionTimer.Stop();

                if (terminateThread && remoteExecutionThread.IsAlive)
                {
                    remoteExecutionThread.Abort();
                    testResult = new SingleTestResultInternal()
                    {
                        TestName = testCase.TestName,
                        TestSuiteName = testCase.TestSuiteName,
                        ErrorMessage = "The test ran for more than " + MAX_THREAD_LIFETIME.TotalSeconds + " seconds and was forcibly terminated",
                        LatencyMs = MAX_THREAD_LIFETIME.TotalMilliseconds,
                        Success = false,
                        Timestamp = localContext.TestBeginTime,
                        TraceId = localContext.TraceId,
                        DatacenterName = localContext.Datacenter
                    };
                }
                else
                {
                    _logger.Log("Finished " + testCase.TestName + " after " + latencyTimer.ElapsedMilliseconds + "ms", LogLevel.Vrb);
                }
            }
            finally
            {
                executionTimer.Stop();

                if (testResult != null && !string.IsNullOrEmpty(testResult.ErrorMessage) && (_logger.ValidLevels & LogLevel.Vrb) != LogLevel.None)
                {
                    _logger.Log(testResult.ErrorMessage, LogLevel.Err);
                }

                // Augment the testing interval based on the number of instances in this service, so that
                // the total net throughput will remain constant if the service expands
                int baseTestInterval = (int)(testCase.QueryInterval.TotalMilliseconds * testScaleDenominator);

                // don't allow tests to run crazy fast like that one time when watchdogs killed ReactiveHost
                int msUntilNextTest = Math.Max((int)MIN_TEST_EXECUTION_INTERVAL.TotalMilliseconds, baseTestInterval - (int)executionTimer.ElapsedMilliseconds);

                // Reschedule the test and write the test results to the database
                testScheduler.ScheduleEvent(testCase, msUntilNextTest);

                if (testResultStore != null)
                {
                    try
                    {
                        await testResultStore.Store(testResult);
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

            StringBuilder exceptionMessage = new StringBuilder();
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
