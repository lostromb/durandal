using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Tests.Common.Utils;

namespace Durandal.Tests.Common.Utils
{
    [TestClass]
    public class CommitterTests
    {
        /// <summary>
        /// Tests a single synchronous commitment
        /// </summary>
        [TestMethod]
        public void TestCommitterSingle()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider fakeRealTimeProvider = new LockStepRealTimeProvider(logger);

            using (CommitmentTestClass commitObject = new CommitmentTestClass())
            using (Committer committer = new Committer(commitObject.Commit, fakeRealTimeProvider))
            {
                // Commit once
                commitObject.CommitStarted.Reset();
                committer.Commit();
                Assert.IsTrue(commitObject.CommitStarted.Wait(1000));
                Assert.AreEqual(0, commitObject.CommitCount);

                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(100), 10);
                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(5));
                committer.WaitUntilCommitFinished(CancellationToken.None, fakeRealTimeProvider, 100);
                Assert.AreEqual(1, commitObject.CommitCount);
            }
        }

        /// <summary>
        /// Tests double-committing
        /// </summary>
        [TestMethod]
        public void TestCommitterDouble()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider fakeRealTimeProvider = new LockStepRealTimeProvider(logger);

            using (CommitmentTestClass commitObject = new CommitmentTestClass())
            using (Committer committer = new Committer(commitObject.Commit, fakeRealTimeProvider))
            {
                // Commit twice
                commitObject.CommitStarted.Reset();
                committer.Commit();
                Assert.IsTrue(commitObject.CommitStarted.Wait(1000));
                Assert.AreEqual(0, commitObject.CommitCount);
                committer.Commit();
                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(5));
                Assert.AreEqual(0, commitObject.CommitCount);

                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(300), 10);
                committer.WaitUntilCommitFinished(CancellationToken.None, fakeRealTimeProvider, 100);
                Assert.AreEqual(2, commitObject.CommitCount);
            }
        }

        /// <summary>
        /// Tests committing a hundred times in a row
        /// </summary>
        [TestMethod]
        public void TestCommitterTriple()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider fakeRealTimeProvider = new LockStepRealTimeProvider(logger);

            using (CommitmentTestClass commitObject = new CommitmentTestClass())
            using (Committer committer = new Committer(commitObject.Commit, fakeRealTimeProvider))
            {
                commitObject.CommitStarted.Reset();
                committer.Commit();
                Assert.IsTrue(commitObject.CommitStarted.Wait(1000));

                for (int c = 0; c < 100; c++)
                {
                    committer.Commit();
                }

                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(500), 10);
                committer.WaitUntilCommitFinished(CancellationToken.None, fakeRealTimeProvider, 100);
                Assert.AreEqual(2, commitObject.CommitCount);
            }
        }

        /// <summary>
        /// Tests commitment with a time delay
        /// </summary>
        [TestMethod]
        public void TestCommitterTimeDelay()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider fakeRealTimeProvider = new LockStepRealTimeProvider(logger);

            using (CommitmentTestClass commitObject = new CommitmentTestClass())
            using (Committer committer = new Committer(commitObject.Commit, fakeRealTimeProvider, TimeSpan.FromMilliseconds(1000)))
            {
                // Commit once and ensure that it happens after the delay
                commitObject.CommitStarted.Reset();
                committer.Commit();
                Assert.AreEqual(0, commitObject.CommitCount);
                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(950), 100);

                // Commitment has not started
                Assert.AreEqual(0, commitObject.CommitCount);
                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(100), 10);

                // Commitment has started but not finished
                Assert.IsTrue(commitObject.CommitStarted.Wait(1000));
                Assert.AreEqual(0, commitObject.CommitCount);
                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(100), 10);

                // Commitment has finished
                committer.WaitUntilCommitFinished(CancellationToken.None, fakeRealTimeProvider, 100);
                Assert.AreEqual(1, commitObject.CommitCount);

                // And assert it doesn't happen again
                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(2000), 100);
                Assert.AreEqual(1, commitObject.CommitCount);
            }
        }

        /// <summary>
        /// Tests commitment with a time delay where a bunch of commits happen within the delay
        /// </summary>
        [TestMethod]
        public void TestCommitterTimeDelayCoalescing()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider fakeRealTimeProvider = new LockStepRealTimeProvider(logger);

            using (CommitmentTestClass commitObject = new CommitmentTestClass())
            using (Committer committer = new Committer(commitObject.Commit, fakeRealTimeProvider, TimeSpan.FromMilliseconds(1000)))
            {
                // Commit a bunch of times in a row, every 100 ms for 10 seconds
                commitObject.CommitStarted.Reset();
                for (int c = 0; c < 100; c++)
                {
                    committer.Commit();
                    fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(100));
                    Assert.AreEqual(0, commitObject.CommitCount);
                }

                // And then assert the commit finally happened after the full timeout
                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(1000), 100);

                Assert.IsTrue(commitObject.CommitStarted.Wait(1000));

                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(400), 100);
                committer.WaitUntilCommitFinished(CancellationToken.None, fakeRealTimeProvider, 100);
                Assert.AreEqual(1, commitObject.CommitCount);

                // And assert it doesn't happen again
                fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(2000), 100);
                Assert.AreEqual(1, commitObject.CommitCount);
            }
        }

        /// <summary>
        /// Tests commitment with a max delay will honor that delay if events are continuously firing
        /// </summary>
        [TestMethod]
        public void TestCommitterMaxDelay()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider fakeRealTimeProvider = new LockStepRealTimeProvider(logger);

            using (CommitmentTestClass commitObject = new CommitmentTestClass())
            using (Committer committer = new Committer(commitObject.Commit, fakeRealTimeProvider, TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(10000)))
            {
                // Commit a bunch of times in a row, every 500 ms for 100 seconds
                for (int loop = 0; loop < 4; loop++)
                {
                    for (int c = 0; c < 20; c++)
                    {
                        committer.Commit();
                        fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(500));
                        Assert.AreEqual(loop, commitObject.CommitCount);
                    }

                    // Let the commit finish now
                    fakeRealTimeProvider.Step(TimeSpan.FromMilliseconds(100));
                }
            }
        }

        private class CommitmentTestClass : IDisposable
        {
            private int _commitCount;

            public readonly ManualResetEventSlim CommitStarted = new ManualResetEventSlim();

            public int CommitCount
            {
                get
                {
                    return _commitCount;
                }
            }

            public CommitmentTestClass()
            {
            }

            public async Task Commit(IRealTimeProvider realTime)
            {
                CommitStarted.Set();
                realTime.Wait(TimeSpan.FromMilliseconds(100), CancellationToken.None);
                Interlocked.Increment(ref _commitCount);
                await DurandalTaskExtensions.NoOpTask;
            }

            public void Dispose()
            {
                CommitStarted.Dispose();
            }
        }
    }
}
