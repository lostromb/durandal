using Durandal.Common.Logger;
using Durandal.Common.Cache;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.MathExt;
using System.Threading;

namespace Durandal.Tests.Common.Cache
{
    [TestClass]
    public class CacheTests
    {
        private static readonly ILogger _logger = new ConsoleLogger();

        [TestMethod]
        public async Task TestMemoryCacheRetrieveNonExistent()
        {
            InMemoryCache<int> cache = new InMemoryCache<int>();
            SeekableTimeProvider time = new SeekableTimeProvider();
            RetrieveResult<int> fetchResult = await cache.TryRetrieve("not_exist", _logger, time);
            Assert.IsFalse(fetchResult.Success);
        }

        [TestMethod]
        public async Task TestMemoryCacheRetrieveBasicNoExpire()
        {
            InMemoryCache<int> cache = new InMemoryCache<int>();
            SeekableTimeProvider time = new SeekableTimeProvider();
            await cache.Store("testkey", 5, null, null, true, _logger, time);
            RetrieveResult<int> fetchResult = await cache.TryRetrieve("testkey", _logger, time);
            Assert.IsTrue(fetchResult.Success);
            Assert.AreEqual(5, fetchResult.Result);

            time.SkipTime((long)TimeSpan.FromDays(3650).TotalMilliseconds);
            fetchResult = await cache.TryRetrieve("testkey", _logger, time);
            Assert.IsTrue(fetchResult.Success);
            Assert.AreEqual(5, fetchResult.Result);
        }

        [TestMethod]
        [DoNotParallelize]
        public async Task TestMemoryCacheConcurrency()
        {
            ILogger logger = new ConsoleLogger();
            InMemoryCache<int> cache = new InMemoryCache<int>();
            IRealTimeProvider time = DefaultRealTimeProvider.Singleton;
            Barrier barrier = new Barrier(5);

            Task writerTask = Task.Run(async () =>
            {
                try
                {
                    int count = 0;
                    for (int loop = 0; loop < 50; loop++)
                    {
                        barrier.SignalAndWait();
                        for (int c = 0; c < 1000; c++)
                        {
                            await cache.Store(new CachedItem<int>(count.ToString(), count, TimeSpan.FromMilliseconds(1000)), false, logger, time).ConfigureAwait(false);
                            count++;
                        }
                    }
                }
                catch (Exception)
                {
                    barrier.RemoveParticipant();
                    throw;
                }
            });

            List<Task> readerTasks = new List<Task>();
            for (int reader = 0; reader < 4; reader++)
            {
                readerTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        int count = 0;
                        for (int loop = 0; loop < 50; loop++)
                        {
                            barrier.SignalAndWait();
                            for (int c = 0; c < 1000; c++)
                            {
                                RetrieveResult<int> rr = await cache.TryRetrieve(count.ToString(), logger, time, TimeSpan.FromMilliseconds(1000)).ConfigureAwait(false);
                                Assert.IsTrue(rr.Success);
                                Assert.AreEqual(count, rr.Result);
                                count++;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        barrier.RemoveParticipant();
                        throw;
                    }
                }));
            }

            await writerTask;
            foreach (var readerTask in readerTasks)
            {
                await readerTask;
            }
        }

        [TestMethod]
        public void TestCacheMFUWorstCase()
        {
            IReadThroughCache<int, int> cache = new MFUCache<int, int>((a) => a * 2, 100);
            for (int loop = 0; loop < 10000; loop++)
            {
                Assert.AreEqual(loop * 2, cache.GetCache(loop));
                Assert.IsTrue(cache.ItemsCached < 110);
            }
        }

        [TestMethod]
        public void TestCacheMFUBestCase()
        {
            IReadThroughCache<int, int> cache = new MFUCache<int, int>((a) => a * 2, 100);
            for (int loop = 0; loop < 10000; loop++)
            {
                Assert.AreEqual(2, cache.GetCache(1));
                Assert.AreEqual(1, cache.ItemsCached);
            }
        }

        [TestMethod]
        public void TestCacheMFUAverageCase()
        {
            IReadThroughCache<int, int> cache = new MFUCache<int, int>((a) => a * 2, 100);
            IRandom rand = new FastRandom(5634);
            for (int loop = 0; loop < 100000; loop++)
            {
                int key;
                if (rand.NextInt(10) > 2)
                {
                    // Common value
                    key = rand.NextInt(100);
                }
                else
                {
                    // Rare value
                    key = rand.NextInt(10000);
                }

                Assert.AreEqual(key * 2, cache.GetCache(key));
                Assert.IsTrue(cache.ItemsCached < 120);
            }
        }

        [TestMethod]
        public void TestCacheMRUWorstCase()
        {
            IReadThroughCache<int, int> cache = new MRUCache<int, int>((a) => a * 2, 100);
            for (int loop = 0; loop < 10000; loop++)
            {
                Assert.AreEqual(loop * 2, cache.GetCache(loop));
                Assert.IsTrue(cache.ItemsCached < 110);
            }
        }

        [TestMethod]
        public void TestCacheMRUBestCase()
        {
            IReadThroughCache<int, int> cache = new MRUCache<int, int>((a) => a * 2, 100);
            for (int loop = 0; loop < 10000; loop++)
            {
                Assert.AreEqual(2, cache.GetCache(1));
                Assert.AreEqual(1, cache.ItemsCached);
            }
        }

        [TestMethod]
        public void TestCacheMRUAverageCase()
        {
            IReadThroughCache<int, int> cache = new MRUCache<int, int>((a) => a * 2, 100);
            IRandom rand = new FastRandom(65309);
            for (int loop = 0; loop < 10000; loop++)
            {
                int key;
                if (rand.NextInt(10) > 2)
                {
                    // Common value
                    key = rand.NextInt(100);
                }
                else
                {
                    // Rare value
                    key = rand.NextInt(1000);
                }

                Assert.AreEqual(key * 2, cache.GetCache(key));
                Assert.IsTrue(cache.ItemsCached < 110);
            }
        }
    }
}
