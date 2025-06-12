using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Durandal.Tests.Common.Monitoring
{
    using Durandal.API;
    using Durandal.Common.Security;
    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using System.Threading.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.Common.Security.Client;
    using Durandal.Common.Security.Server;
    using Durandal.Common.Security.Login;
    using Durandal.Common.Security.Login.Providers;
    using Durandal.Common.MathExt;
    using Durandal.Common.Time.Scheduling;
    using Durandal.Common.Monitoring;
    using Durandal.Common.Tasks;
    using System.Threading;
    using Durandal.Common.Config;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Net;
    using Durandal.Tests.Common.Monitoring;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.ServiceMgmt;

    [TestClass]
    public class MonitoringTests
    {
        [TestMethod]
        public async Task TestMonitorRunnerBasicPassingNonCooperative()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            IRandom rand = new FastRandom(199);
            CancellationTokenSource testStopper = new CancellationTokenSource(TimeSpan.FromSeconds(300));
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            IList<IServiceMonitor> monitors = new List<IServiceMonitor>();

            FakeServiceMonitor fakeMonitor = new FakeServiceMonitor();
            monitors.Add(fakeMonitor);

            InMemoryTestResultStore resultStore = new InMemoryTestResultStore(monitors, TimeSpan.FromMinutes(30));
            MonitorRunner runner = new MonitorRunner(
                monitors,
                resultStore,
                logger.Clone("TestRunner"),
                random: rand);

            using (IMonitorDriver driver = new NonCooperativeMonitorDriver(logger.Clone("MonitorDriver"), TimeSpan.FromSeconds(5)))
            {
                IRealTimeProvider forkedTime = realTime.Fork("NonCooperativeDriverTest");
                Task backgroundTask = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await runner.RunMonitorLoop(driver, testStopper.Token, forkedTime);
                        }
                        catch (OperationCanceledException) { }
                        finally
                        {
                            forkedTime.Merge();
                        }
                    });

                // The test schedule should be:
                // Test 1: 0.010 -> 1.020
                // Test 2: 10.010 -> 11.020
                // Test 3: 20.010 -> 21.020

                realTime.Step(TimeSpan.FromMilliseconds(10)); // 0.010

                realTime.Step(TimeSpan.FromMilliseconds(500), 100); // 0.510, middle of test 1
                Assert.AreEqual(1, fakeMonitor.TestsStarted);
                Assert.AreEqual(0, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromMilliseconds(1000), 100); // 1.510, after test 1
                Assert.AreEqual(1, fakeMonitor.TestsStarted);
                Assert.AreEqual(1, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromMilliseconds(8000), 500); // 9.510, before test 2
                Assert.AreEqual(1, fakeMonitor.TestsStarted);
                Assert.AreEqual(1, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromMilliseconds(1000), 100); // 10.510, middle of test 2
                Assert.AreEqual(2, fakeMonitor.TestsStarted);
                Assert.AreEqual(1, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromMilliseconds(1000), 100); // 11.510, after test 2
                Assert.AreEqual(2, fakeMonitor.TestsStarted);
                Assert.AreEqual(2, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromMilliseconds(8000), 500); // 19.510, before test 3
                Assert.AreEqual(2, fakeMonitor.TestsStarted);
                Assert.AreEqual(2, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromMilliseconds(1000), 100); // 20.510, middle of test 3
                Assert.AreEqual(3, fakeMonitor.TestsStarted);
                Assert.AreEqual(2, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromMilliseconds(1000), 100); // 21.510, after test 3
                Assert.AreEqual(3, fakeMonitor.TestsStarted);
                Assert.AreEqual(3, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromSeconds(5), 100);
                TestMonitorStatus testStatus = await resultStore.GetTestStatus(fakeMonitor.TestName, TimeSpan.FromMinutes(10), realTime);
                Assert.AreEqual(3, testStatus.TestsRan);
                Assert.IsTrue(testStatus.IsPassing);

                testStopper.Cancel();
                await backgroundTask;
            }
        }

        [Ignore]
        [TestMethod]
        public async Task TestMonitorRunnerBasicTimingOutTestNonCooperative()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            IRandom rand = new FastRandom(199);
            CancellationTokenSource testStopper = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            IList<IServiceMonitor> monitors = new List<IServiceMonitor>();
            FakeServiceMonitor fakeMonitor = new FakeServiceMonitor();
            fakeMonitor.ShouldTimeout = true;
            monitors.Add(fakeMonitor);

            InMemoryTestResultStore resultStore = new InMemoryTestResultStore(monitors, TimeSpan.FromMinutes(30));
            MonitorRunner runner = new MonitorRunner(
                monitors,
                resultStore,
                logger.Clone("TestRunner"),
                random: rand);
            using (IMonitorDriver driver = new NonCooperativeMonitorDriver(logger.Clone("MonitorDriver"), TimeSpan.FromSeconds(1)))
            {
                IRealTimeProvider forkedTime = realTime.Fork("NonCooperativeDriverTest");
                Task backgroundTask = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await runner.RunMonitorLoop(driver, testStopper.Token, forkedTime);
                        }
                        catch (OperationCanceledException) { }
                        finally
                        {
                            forkedTime.Merge();
                        }
                    });

                // Have the tests run for a while
                realTime.Step(TimeSpan.FromSeconds(65), 1000);

                TestMonitorStatus testStatus = await resultStore.GetTestStatus(fakeMonitor.TestName, TimeSpan.FromMinutes(5), realTime);
                Assert.IsTrue(testStatus.TestsRan >= 6);
                Assert.IsFalse(testStatus.IsPassing);
                Assert.AreEqual(0, testStatus.PassRate, 0.0001);
                // With noncooperative scheduling the maximum latency should be the watchdog timeout, not test timeout
                Assert.AreEqual(1000, testStatus.MedianLatency.TotalMilliseconds, 100);

                testStopper.Cancel();
                await backgroundTask;
            }
        }

        private class FakeServiceMonitor : IServiceMonitor
        {
            private int _testsStarted = 0;
            private int _testsFinished = 0;

            public int TestsStarted => _testsStarted;

            public int TestsFinished => _testsFinished;

            public bool ShouldFail { get; set; }

            public bool ShouldTimeout { get; set; }

            public string TestName => "FakeTest";

            public string TestSuiteName => "Fake";

            public string TestDescription => "Waits 1 second and then increments a number";

            public float? PassRateThreshold => 0.8f;

            public TimeSpan? LatencyThreshold => TimeSpan.FromSeconds(2);

            public TimeSpan QueryInterval => TimeSpan.FromSeconds(10);

            public string ExclusivityKey => null;

            public void Dispose() { }

            public Task<bool> Initialize(
                IConfiguration environmentConfig,
                Guid machineLocalGuid,
                IFileSystem localFileSystem,
                IHttpClientFactory httpClientFactory,
                WeakPointer<ISocketFactory> socketFactory,
                WeakPointer<IMetricCollector> metrics,
                DimensionSet metricDimensions)
            {
                return Task.FromResult(true);
            }

            public async Task<SingleTestResult> Run(Guid traceId, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                Interlocked.Increment(ref _testsStarted);
                if (ShouldTimeout)
                {
                    await realTime.WaitAsync(TimeSpan.FromSeconds(20), cancelToken);
                }
                else
                {
                    await realTime.WaitAsync(TimeSpan.FromSeconds(1), cancelToken);
                }

                Interlocked.Increment(ref _testsFinished);

                if (ShouldFail)
                {
                    return new SingleTestResult()
                    {
                        ErrorMessage = "Test failed",
                        Success = false,
                    };
                }
                else
                {
                    return new SingleTestResult()
                    {
                        ErrorMessage = null,
                        Success = true,
                    };
                }
            }
        }
    }
}
