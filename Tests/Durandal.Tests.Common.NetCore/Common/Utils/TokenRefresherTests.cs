namespace Durandal.Tests.Common.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Logger;
    using Durandal.Common.Utils;
    using Durandal.Common.Collections;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for TokenRefresher class.
    /// </summary>
    [TestClass]
    public class TokenRefresherTests
    {
        [TestMethod]
        public async Task TestTokenRefresherBasicSuccess()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);
            using (TokenRefresher<string> refresher = new TokenRefresher<string>(
                logger.Clone("Refresher"),
                (c, rt) =>
                {
                    return Task.FromResult(new TokenRefreshResult<string>("token", TimeSpan.FromMinutes(5)));
                },
                realTime))
            {
                string token = await refresher.GetToken(logger, realTime, TimeSpan.FromSeconds(1));
                Assert.AreEqual("token", token);
            }
        }

        [TestMethod]
        public async Task TestTokenRefresherBasicFailure()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);
            using (TokenRefresher<string> refresher = new TokenRefresher<string>(
                logger.Clone("Refresher"),
                (c, rt) =>
                {
                    return Task.FromResult(new TokenRefreshResult<string>(false, "The token refresh failed"));
                },
                realTime))
            {
                string token = await refresher.GetToken(logger, realTime, TimeSpan.FromSeconds(1));
                Assert.IsNull(token);
            }
        }

        [TestMethod]
        public async Task TestTokenRefresherAutomaticallyRefreshes()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);
            using (TokenRefresher<long> refresher = new TokenRefresher<long>(
                logger.Clone("Refresher"),
                (c, rt) =>
                {
                    // The token value is equal to the time it expires
                    return Task.FromResult(new TokenRefreshResult<long>(rt.TimestampMilliseconds + 30000L, TimeSpan.FromSeconds(30)));
                },
                realTime))
            {
                for (int c = 0; c < 20; c++)
                {
                    realTime.Step(TimeSpan.FromSeconds(5));
                    long token = await refresher.GetToken(logger, realTime, TimeSpan.FromSeconds(1));
                    Assert.AreNotEqual(token, default(long));
                    Assert.IsTrue(token > realTime.TimestampMilliseconds);
                }
            }
        }

        [TestMethod]
        public async Task TestTokenRefresherHonorsSuggestedRefreshTime()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);

            int firstRun = 1;
            int triggeredFailure = 0;
            long validTime = realTime.TimestampMilliseconds + 100000;

            using (TokenRefresher<string> refresher = new TokenRefresher<string>(
                logger.Clone("Refresher"),
                (c, rt) =>
                {
                    // On first run, return a throttling response
                    // On second run, if the time is earlier than the valid time, fail the test (because the refresher should have backed off)
                    // Otherwise, return the valid token
                    if (Interlocked.CompareExchange(ref firstRun, 0, 1) == 1)
                    {
                        return Task.FromResult(new TokenRefreshResult<string>(false, "Try again later", TimeSpan.FromSeconds(100)));
                    }
                    else if (rt.TimestampMilliseconds < validTime)
                    {
                        Interlocked.CompareExchange(ref triggeredFailure, 1, 0);
                        return Task.FromResult(new TokenRefreshResult<string>(false, "FOOL! You refreshed too soon!"));
                    }
                    else
                    {
                        return Task.FromResult(new TokenRefreshResult<string>("valid_token", TimeSpan.FromHours(1)));
                    }
                },
                realTime))
            {
                string token = await refresher.GetToken(logger, realTime, TimeSpan.FromSeconds(1));
                Assert.IsNull(token);
                realTime.Step(TimeSpan.FromSeconds(100));
                token = await refresher.GetToken(logger, realTime, TimeSpan.FromSeconds(1));
                Assert.AreEqual("valid_token", token);
                Assert.AreNotEqual(1, triggeredFailure);
            }
        }

        [TestMethod]
        public void TestTokenRefresherHasExpectedBackoffPattern()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);
            ConcurrentQueue<DateTimeOffset> queue = new ConcurrentQueue<DateTimeOffset>();
            DateTimeOffset startTime = realTime.Time;

            using (TokenRefresher<string> refresher = new TokenRefresher<string>(
                logger.Clone("Refresher"),
                async (c, rt) =>
                {
                    queue.Enqueue(rt.Time);
                    await DurandalTaskExtensions.NoOpTask;
                    return new TokenRefreshResult<string>(false, "An error happened, try again later");
                },
                realTime))
            {
                realTime.Step(TimeSpan.FromMinutes(10));

                List<TimeSpan> expectedCurve = new List<TimeSpan>()
                {
                    TimeSpan.FromSeconds(0),
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(45),
                    TimeSpan.FromSeconds(60),
                    TimeSpan.FromSeconds(75),
                    TimeSpan.FromSeconds(90),
                    TimeSpan.FromSeconds(105),
                    TimeSpan.FromSeconds(120),
                    TimeSpan.FromSeconds(135),
                };

                DateTimeOffset res;
                foreach (TimeSpan expectedRefreshTime in expectedCurve)
                {
                    Assert.IsTrue(queue.TryDequeue(out res));
                    TimeSpan actualSpan = (res - startTime);
                    Assert.AreEqual(expectedRefreshTime.Seconds, actualSpan.Seconds, 1);
                }
            }
        }

        [TestMethod]
        public void TestTokenRefresherHasExpectedCustomBackoffPattern()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);
            ConcurrentQueue<DateTimeOffset> queue = new ConcurrentQueue<DateTimeOffset>();
            DateTimeOffset startTime = realTime.Time;

            using (TokenRefresher<string> refresher = new TokenRefresher<string>(
                logger.Clone("Refresher"),
                async (c, rt) =>
                {
                    queue.Enqueue(rt.Time);
                    await DurandalTaskExtensions.NoOpTask;
                    return new TokenRefreshResult<string>(false, "An error happened, try again later");
                },
                realTime,
                TimeSpan.FromSeconds(1)))
            {
                realTime.Step(TimeSpan.FromMinutes(10));

                List<TimeSpan> expectedCurve = new List<TimeSpan>()
                {
                    TimeSpan.FromSeconds(0),
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromSeconds(4),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(6),
                    TimeSpan.FromSeconds(7),
                    TimeSpan.FromSeconds(8),
                    TimeSpan.FromSeconds(9),
                    TimeSpan.FromSeconds(10),
                };

                DateTimeOffset res;
                foreach (TimeSpan expectedRefreshTime in expectedCurve)
                {
                    Assert.IsTrue(queue.TryDequeue(out res));
                    TimeSpan actualSpan = (res - startTime);
                    Assert.AreEqual(expectedRefreshTime.Seconds, actualSpan.Seconds, 0.5);
                }
            }
        }

        [TestMethod]
        public void TestTokenRefresherHasExpectedBackoffPatternOnExplicitFailure()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);
            ConcurrentQueue<DateTimeOffset> queue = new ConcurrentQueue<DateTimeOffset>();
            DateTimeOffset startTime = realTime.Time;

            using (TokenRefresher<string> refresher = new TokenRefresher<string>(
                logger.Clone("Refresher"),
                async (c, rt) =>
                {
                    queue.Enqueue(rt.Time);
                    await DurandalTaskExtensions.NoOpTask;
                    return new TokenRefreshResult<string>(true, "An error happened; your credentials are bad");
                },
                realTime))
            {
                realTime.Step(TimeSpan.FromHours(10));

                List<TimeSpan> expectedCurve = new List<TimeSpan>()
                {
                    TimeSpan.FromMinutes(0),
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromMinutes(3),
                    TimeSpan.FromMinutes(7),
                    TimeSpan.FromMinutes(15),
                    TimeSpan.FromMinutes(31),
                    TimeSpan.FromMinutes(63),
                    TimeSpan.FromMinutes(123),
                    TimeSpan.FromMinutes(183),
                    TimeSpan.FromMinutes(243),
                };

                DateTimeOffset res;
                foreach (TimeSpan expectedRefreshTime in expectedCurve)
                {
                    Assert.IsTrue(queue.TryDequeue(out res));
                    Assert.AreEqual(expectedRefreshTime.TotalMinutes, (res - startTime).TotalMinutes, 0.5);
                }
            }
        }
    }
}
