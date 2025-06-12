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
        /// <summary>
        /// Verifies that the exclusivity solver will work for homogeneous periodic events.
        /// </summary>
        [TestMethod]
        public void TestPeriodicExclusivitySolverHomogeneous()
        {
            IRandom rand = new FastRandom(199);
            List<PeriodicEvent<int>> events = new List<PeriodicEvent<int>>();
            for (int c = 0; c < 5; c++)
            {
                events.Add(new PeriodicEvent<int>()
                {
                    Object = c,
                    Period = TimeSpan.FromSeconds(10)
                });
            }

            PeriodicExclusivitySolver.Solve(events, rand);

            Assert.AreEqual(6.182, events[0].Offset.TotalSeconds, 0.2);
            Assert.AreEqual(9.921, events[1].Offset.TotalSeconds, 0.2);
            Assert.AreEqual(2.420, events[2].Offset.TotalSeconds, 0.2);
            Assert.AreEqual(8.133, events[3].Offset.TotalSeconds, 0.2);
            Assert.AreEqual(4.221, events[4].Offset.TotalSeconds, 0.2);
        }

        /// <summary>
        /// Verifies that the exclusivity solver will work for heterogeneous periodic events.
        /// </summary>
        [TestMethod]
        public void TestPeriodicExclusivitySolverHeterogeneous()
        {
            IRandom rand = new FastRandom(199);
            List<PeriodicEvent<int>> events = new List<PeriodicEvent<int>>();
            for (int c = 0; c < 5; c++)
            {
                events.Add(new PeriodicEvent<int>()
                {
                    Object = c,
                    Period = TimeSpan.FromSeconds(c + 1)
                });
            }

            PeriodicExclusivitySolver.Solve(events, rand);

            Assert.AreEqual(0, events[0].Offset.TotalSeconds, 0.2);
            Assert.AreEqual(1.583, events[1].Offset.TotalSeconds, 0.2);
            Assert.AreEqual(0.448, events[2].Offset.TotalSeconds, 0.2);
            Assert.AreEqual(0.804, events[3].Offset.TotalSeconds, 0.2);
            Assert.AreEqual(0.296, events[4].Offset.TotalSeconds, 0.2);
        }

        [TestMethod]
        public void TestPeriodicExclusivitySolverSmallOrEmptyList()
        {
            IRandom rand = new FastRandom(199);
            List<PeriodicEvent<int>> events = new List<PeriodicEvent<int>>();

            PeriodicExclusivitySolver.Solve(events, rand);

            events.Add(new PeriodicEvent<int>()
            {
                Object = 0,
                Period = TimeSpan.FromSeconds(10)
            });

            PeriodicExclusivitySolver.Solve(events, rand);
            Assert.AreEqual(0, events[0].Offset.TotalSeconds, 0.2);
        }

        [TestMethod]
        public async Task TestMonitorRunnerBasicPassing()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            IRandom rand = new FastRandom(199);
            CancellationTokenSource testStopper = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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

            using (IMonitorDriver driver = new CooperativeMonitorDriver(logger.Clone("MonitorDriver"), new TaskThreadPool()))
            {
                IRealTimeProvider forkedTime = realTime.Fork("CooperativeDriverTest");
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

                // should step into the middle of test 1's execution
                realTime.Step(TimeSpan.FromMilliseconds(500), 100);
                Assert.AreEqual(1, fakeMonitor.TestsStarted);
                Assert.AreEqual(0, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromSeconds(1), 100);
                Assert.AreEqual(1, fakeMonitor.TestsStarted);
                Assert.AreEqual(1, fakeMonitor.TestsFinished);

                // Now jump ahead 10 seconds. This should take us to the middle of the next execution
                realTime.Step(TimeSpan.FromSeconds(9), 1000);
                Assert.AreEqual(2, fakeMonitor.TestsStarted);
                Assert.AreEqual(1, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromSeconds(1), 100);
                Assert.AreEqual(2, fakeMonitor.TestsStarted);
                Assert.AreEqual(2, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromSeconds(9), 1000);
                Assert.AreEqual(3, fakeMonitor.TestsStarted);
                Assert.AreEqual(2, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromSeconds(1), 100);
                Assert.AreEqual(3, fakeMonitor.TestsStarted);
                Assert.AreEqual(3, fakeMonitor.TestsFinished);

                realTime.Step(TimeSpan.FromSeconds(5), 1000);
                TestMonitorStatus testStatus = await resultStore.GetTestStatus(fakeMonitor.TestName, TimeSpan.FromMinutes(10), realTime);
                Assert.AreEqual(3, testStatus.TestsRan);
                Assert.IsTrue(testStatus.IsPassing);

                testStopper.Cancel();
                await backgroundTask;
            }
        }

        [TestMethod]
        public async Task TestMonitorRunnerManyTests()
        {
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(199);
            CancellationTokenSource testStopper = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            IList<IServiceMonitor> monitors = new List<IServiceMonitor>();
            for (int c = 0; c < 10; c++)
            {
                monitors.Add(new FakeServiceMonitor());
            }

            InMemoryTestResultStore resultStore = new InMemoryTestResultStore(monitors, TimeSpan.FromMinutes(30));
            MonitorRunner runner = new MonitorRunner(
                monitors,
                resultStore,
                logger.Clone("TestRunner"),
                random: rand);
            using (IMonitorDriver driver = new CooperativeMonitorDriver(logger.Clone("MonitorDriver"), new TaskThreadPool()))
            {
                IRealTimeProvider forkedTime = realTime.Fork("CooperativeDriverTest");
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

                realTime.Step(TimeSpan.FromSeconds(60), 1000);

                foreach (IServiceMonitor monitor in monitors)
                {
                    FakeServiceMonitor fsm = monitor as FakeServiceMonitor;
                    Assert.IsTrue(fsm.TestsStarted == 6 || fsm.TestsStarted == 7);
                }

                testStopper.Cancel();
                await backgroundTask;
            }
        }

        [TestMethod]
        public async Task TestMonitorRunnerBasicFailingTest()
        {
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(199);
            CancellationTokenSource testStopper = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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

            using (IMonitorDriver driver = new CooperativeMonitorDriver(logger.Clone("MonitorDriver"), new TaskThreadPool()))
            {
                IRealTimeProvider forkedTime = realTime.Fork("CooperativeDriverTest");
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

                // Have the tests pass for a while
                realTime.Step(TimeSpan.FromSeconds(65), 1000);

                TestMonitorStatus testStatus = await resultStore.GetTestStatus(fakeMonitor.TestName, TimeSpan.FromMinutes(5), realTime);
                Assert.IsTrue(testStatus.TestsRan >= 6);
                Assert.AreEqual(1, testStatus.PassRate, 0.0001);
                Assert.IsTrue(testStatus.IsPassing);

                // Now make them fail for a while
                fakeMonitor.ShouldFail = true;
                realTime.Step(TimeSpan.FromSeconds(65), 1000);

                testStatus = await resultStore.GetTestStatus(fakeMonitor.TestName, TimeSpan.FromMinutes(5), realTime);
                Assert.IsTrue(testStatus.TestsRan >= 12);
                Assert.IsFalse(testStatus.IsPassing);
                Assert.IsTrue(testStatus.PassRate > 0.2f && testStatus.PassRate < 0.8f);
                Assert.IsTrue(testStatus.LastErrors.Count >= 6);
                Assert.AreEqual("Test failed", testStatus.LastErrors[0].Message);

                // And then have them recover again for the entire window that we check reporting for. The status should recover to 100%
                fakeMonitor.ShouldFail = false;
                realTime.Step(TimeSpan.FromSeconds(120), 5000);
                testStatus = await resultStore.GetTestStatus(fakeMonitor.TestName, TimeSpan.FromMinutes(2), realTime);
                Assert.IsTrue(testStatus.IsPassing);
                Assert.AreEqual(1, testStatus.PassRate, 0.0001);

                testStopper.Cancel();
                await backgroundTask;
            }
        }

        [TestMethod]
        public async Task TestMonitorRunnerBasicTimingOutTest()
        {
            ILogger logger = new ConsoleLogger();
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
            using (IMonitorDriver driver = new CooperativeMonitorDriver(logger.Clone("MonitorDriver"), new TaskThreadPool()))
            {
                IRealTimeProvider forkedTime = realTime.Fork("CooperativeDriverTest");
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
                realTime.Step(TimeSpan.FromSeconds(85), 1000);

                TestMonitorStatus testStatus = await resultStore.GetTestStatus(fakeMonitor.TestName, TimeSpan.FromMinutes(5), realTime);
                Assert.IsTrue(testStatus.TestsRan >= 3);
                Assert.IsFalse(testStatus.IsPassing);
                Assert.AreEqual(1, testStatus.PassRate, 0.0001);
                // With cooperative scheduling the maximum latency should be the actual test timeout since we can't interrupt it
                Assert.AreEqual(20, testStatus.MedianLatency.TotalSeconds, 1);

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
