using Durandal.Common.Cache;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Cache
{
    [TestClass]
    public class DynamicLockFreeCacheTests
    {
        [TestMethod]
        public void TestDynamicLockFreeCache_Constructor()
        {
            TestAssert.ExceptionThrown<ArgumentOutOfRangeException>(() => new DynamicLockFreeCache<object>(-1));
            TestAssert.ExceptionThrown<ArgumentOutOfRangeException>(() => new DynamicLockFreeCache<object>(0));
            TestAssert.ExceptionThrown<ArgumentOutOfRangeException>(() => new DynamicLockFreeCache<object>(0x1 << 28));
            TestAssert.ExceptionThrown<ArgumentException>(() => new DynamicLockFreeCache<object>(3));
            TestAssert.ExceptionThrown<ArgumentException>(() => new DynamicLockFreeCache<object>(7));
            TestAssert.ExceptionThrown<ArgumentException>(() => new DynamicLockFreeCache<object>(9));
            TestAssert.ExceptionThrown<ArgumentOutOfRangeException>(() => new DynamicLockFreeCache<object>(8, TimeSpan.FromSeconds(-1)));
        }

        [TestMethod]
        [DataRow(0.0f)]
        [DataRow(0.5f)]
        [DataRow(1.0f)]
        public void TestDynamicLockFreeCache_Basic(float cacheFillRatio)
        {
            DynamicLockFreeCache<object> cache = new DynamicLockFreeCache<object>(64, TimeSpan.Zero);
            int itemsCreated = 0;
            int cacheFetches = 0;
            const int ITERATIONS = 100000;

            int fill = (int)(64 * cacheFillRatio);
            for (int c = 0; c < fill; c++)
            {
                cache.TryEnqueue(new object());
                Interlocked.Increment(ref itemsCreated);
            }

            for (int c = 0; c < ITERATIONS; c++)
            {
                object o = cache.TryDequeue();
                Interlocked.Increment(ref cacheFetches);
                if (o == null)
                {
                    Interlocked.Increment(ref itemsCreated);
                    o = new object();
                }

                cache.TryEnqueue(o);
            }

            Assert.IsTrue((uint)itemsCreated < 128);
        }

        [TestMethod]
        [DataRow(0.0f)]
        [DataRow(0.5f)]
        [DataRow(1.0f)]
        public void TestDynamicLockFreeCache_BasicDisposable_DoesntLeak(float cacheFillRatio)
        {
            DynamicLockFreeCache<DisposableObject> cache = new DynamicLockFreeCache<DisposableObject>(64, TimeSpan.Zero);
            int itemsCreated = 0;
            int itemsDisposed = 0;
            int cacheFetches = 0;
            DisposableObject o;
            const int ITERATIONS = 100000;

            int fill = (int)(64 * cacheFillRatio);
            for (int c = 0; c < fill; c++)
            {
                cache.TryEnqueue(new DisposableObject());
                Interlocked.Increment(ref itemsCreated);
            }

            for (int c = 0; c < ITERATIONS; c++)
            {
                o = cache.TryDequeue();
                Interlocked.Increment(ref cacheFetches);
                if (o == null)
                {
                    Interlocked.Increment(ref itemsCreated);
                    o = new DisposableObject();
                }

                if (!cache.TryEnqueue(ref o))
                {
                    o.Dispose();
                    Interlocked.Increment(ref itemsDisposed);
                }
            }

            o = cache.TryDequeueComprehensive();
            while (o != null)
            {
                o.Dispose();
                Interlocked.Increment(ref itemsDisposed);
                o = cache.TryDequeueComprehensive();
            }

            Assert.AreEqual(itemsCreated, itemsDisposed);
        }

        [TestMethod]
        [DoNotParallelize]
        [DataRow(0.0f)]
        [DataRow(0.5f)]
        [DataRow(1.0f)]
        public void TestDynamicLockFreeCache_Threaded(float cacheFillRatio)
        {
            DynamicLockFreeCache<object> cache = new DynamicLockFreeCache<object>(64, TimeSpan.Zero);
            int itemsCreated = 0;
            int cacheFetches = 0;
            const int THREAD_COUNT = 4;
            const int ITERATIONS = 100000;

            int fill = (int)(64 * cacheFillRatio);
            for (int c = 0; c < fill; c++)
            {
                cache.TryEnqueue(new DisposableObject());
                Interlocked.Increment(ref itemsCreated);
            }

            using (Barrier barrier = new Barrier(THREAD_COUNT))
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                CancellationToken cancelToken = cts.Token;
                List<Thread> threads = new List<Thread>();

                for (int thread = 0; thread < THREAD_COUNT; thread++)
                {
                    threads.Add(new Thread(() =>
                    {
                        for (int c = 0; c < ITERATIONS; c++)
                        {
                            barrier.SignalAndWait(cancelToken);
                            object o = cache.TryDequeue();
                            Interlocked.Increment(ref cacheFetches);
                            if (o == null)
                            {
                                Interlocked.Increment(ref itemsCreated);
                                o = new object();
                            }

                            cache.TryEnqueue(o);
                        }
                    }));
                }

                foreach (Thread thread in threads)
                {
                    thread.Start();
                }

                foreach (Thread thread in threads)
                {
                    thread.Join(TimeSpan.FromSeconds(20));
                }

                Assert.IsTrue((uint)itemsCreated < 128);
            }
        }

        [TestMethod]
        [DoNotParallelize]
        [DataRow(0.0f)]
        [DataRow(0.5f)]
        [DataRow(1.0f)]
        public void TestDynamicLockFreeCache_ThreadedDisposable_DoesntLeak(float cacheFillRatio)
        {
            DynamicLockFreeCache<DisposableObject> cache = new DynamicLockFreeCache<DisposableObject>(64, TimeSpan.Zero);
            int itemsCreated = 0;
            int itemsDisposed = 0;
            int cacheFetches = 0;
            const int THREAD_COUNT = 4;
            const int ITERATIONS = 100000;

            int fill = (int)(64 * cacheFillRatio);
            for (int c = 0; c < fill; c++)
            {
                cache.TryEnqueue(new DisposableObject());
                Interlocked.Increment(ref itemsCreated);
            }

            using (Barrier barrier = new Barrier(THREAD_COUNT))
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                CancellationToken cancelToken = cts.Token;
                List<Thread> threads = new List<Thread>();

                for (int thread = 0; thread < THREAD_COUNT; thread++)
                {
                    threads.Add(new Thread(() =>
                    {
                        DisposableObject o;
                        for (int c = 0; c < ITERATIONS; c++)
                        {
                            barrier.SignalAndWait(cancelToken);
                            o = cache.TryDequeue();
                            Interlocked.Increment(ref cacheFetches);
                            if (o == null)
                            {
                                Interlocked.Increment(ref itemsCreated);
                                o = new DisposableObject();
                            }

                            if (!cache.TryEnqueue(ref o))
                            {
                                o.Dispose();
                                Interlocked.Increment(ref itemsDisposed);
                            }
                        }

                        o = cache.TryDequeueComprehensive();
                        while (o != null)
                        {
                            o.Dispose();
                            Interlocked.Increment(ref itemsDisposed);
                            o = cache.TryDequeueComprehensive();
                        }
                    }));
                }

                foreach (Thread thread in threads)
                {
                    thread.Start();
                }

                foreach (Thread thread in threads)
                {
                    thread.Join(TimeSpan.FromSeconds(20));
                }

                Assert.AreEqual(itemsCreated, itemsDisposed);
            }
        }

        [TestMethod]
        [DataRow(0.3f)]
        [DataRow(0.5f)]
        [DataRow(0.7f)]
        public void TestDynamicLockFreeCache_Skew(float skew)
        {
            DynamicLockFreeCache<DisposableObject> cache = new DynamicLockFreeCache<DisposableObject>(64, TimeSpan.Zero);
            IRandom rand = new FastRandom(887126);
            int itemsCreated = 0;
            int itemsConsumed = 0;
            int itemsOverflowed = 0;
            const int ITERATIONS = 1000000;

            DisposableObject o;
            for (int c = 0; c < ITERATIONS; c++)
            {
                // Skew below 0.5 => we are taking more than we add
                if (rand.NextFloat() > skew)
                {
                    o = cache.TryDequeue();
                    if (o != null)
                    {
                        Interlocked.Increment(ref itemsConsumed);
                        o.Dispose();
                    }
                }
                else
                {
                    o = new DisposableObject();
                    Interlocked.Increment(ref itemsCreated);
                    if (!cache.TryEnqueue(ref o))
                    {
                        o.Dispose();
                        Interlocked.Increment(ref itemsOverflowed);
                    }
                }
            }

            o = cache.TryDequeueComprehensive();
            while (o != null)
            {
                o.Dispose();
                Interlocked.Increment(ref itemsConsumed);
                o = cache.TryDequeueComprehensive();
            }

            Console.WriteLine("Created " + itemsCreated);
            Console.WriteLine("Consumed " + itemsConsumed);
            Console.WriteLine("Overflowed " + itemsOverflowed);
            Assert.AreEqual(itemsCreated, itemsConsumed + itemsOverflowed);

            if (skew < -0.5f)
            {
                Assert.AreEqual(itemsCreated, itemsConsumed, ITERATIONS / 10);
                Assert.AreEqual(0, itemsOverflowed, ITERATIONS / 10);
                Assert.IsTrue(cache.PoolSize <= 64);
            }
            else if (skew == 0.5f)
            {
                Assert.AreEqual(itemsCreated, itemsConsumed, ITERATIONS / 10);
                Assert.AreEqual(0, itemsOverflowed, ITERATIONS / 10);
                Assert.IsTrue(cache.PoolSize <= 8096);
            }
            else if (skew > 0.5f)
            {
                Assert.IsTrue(cache.PoolSize > 64);
            }
        }

        [TestMethod]
        [DataRow(0.3f)]
        [DataRow(0.5f)]
        [DataRow(0.7f)]
        public void TestDynamicLockFreeCache_Skew_ComprehensiveDequeue(float skew)
        {
            DynamicLockFreeCache<DisposableObject> cache = new DynamicLockFreeCache<DisposableObject>(64, TimeSpan.Zero);
            IRandom rand = new FastRandom(887126);
            int itemsCreated = 0;
            int itemsConsumed = 0;
            int itemsOverflowed = 0;
            const int ITERATIONS = 1000000;

            DisposableObject o;
            for (int c = 0; c < ITERATIONS; c++)
            {
                // Skew below 0.5 => we are taking more than we add
                if (rand.NextFloat() > skew)
                {
                    o = cache.TryDequeueComprehensive();
                    if (o != null)
                    {
                        Interlocked.Increment(ref itemsConsumed);
                        o.Dispose();
                    }
                }
                else
                {
                    o = new DisposableObject();
                    Interlocked.Increment(ref itemsCreated);
                    if (!cache.TryEnqueue(ref o))
                    {
                        o.Dispose();
                        Interlocked.Increment(ref itemsOverflowed);
                    }
                }
            }

            o = cache.TryDequeueComprehensive();
            while (o != null)
            {
                o.Dispose();
                Interlocked.Increment(ref itemsConsumed);
                o = cache.TryDequeueComprehensive();
            }

            Console.WriteLine("Created " + itemsCreated);
            Console.WriteLine("Consumed " + itemsConsumed);
            Console.WriteLine("Overflowed " + itemsOverflowed);
            Assert.AreEqual(itemsCreated, itemsConsumed + itemsOverflowed);

            if (skew < -0.5f)
            {
                Assert.AreEqual(itemsCreated, itemsConsumed, ITERATIONS / 10);
                Assert.AreEqual(0, itemsOverflowed, ITERATIONS / 10);
                Assert.IsTrue(cache.PoolSize <= 64);
            }
            else if (skew == 0.5f)
            {
                Assert.AreEqual(itemsCreated, itemsConsumed, ITERATIONS / 10);
                Assert.AreEqual(0, itemsOverflowed, ITERATIONS / 10);
                Assert.IsTrue(cache.PoolSize <= 8096);
            }
            else if (skew > 0.5f)
            {
                Assert.IsTrue(cache.PoolSize > 64);
            }
        }

        [TestMethod]
        [DoNotParallelize]
        [DataRow(0.3f)]
        [DataRow(0.5f)]
        [DataRow(0.7f)]
        public void TestDynamicLockFreeCache_ThreadedSkew(float skew)
        {
            DynamicLockFreeCache<DisposableObject> cache = new DynamicLockFreeCache<DisposableObject>(64, TimeSpan.Zero);
            IRandom rand = new FastRandom(9102);
            int itemsCreated = 0;
            int itemsConsumed = 0;
            int itemsOverflowed = 0;
            const int THREAD_COUNT = 4;
            const int ITERATIONS = 100000;

            using (Barrier barrier = new Barrier(THREAD_COUNT))
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                CancellationToken cancelToken = cts.Token;
                List<Thread> threads = new List<Thread>();

                for (int thread = 0; thread < THREAD_COUNT; thread++)
                {
                    threads.Add(new Thread(() =>
                    {
                        DisposableObject o;
                        for (int c = 0; c < ITERATIONS; c++)
                        {
                            barrier.SignalAndWait(cancelToken);
                            // Skew below 0.5 => we are taking more than we add
                            if (rand.NextFloat() > skew)
                            {
                                o = cache.TryDequeue();
                                if (o != null)
                                {
                                    Interlocked.Increment(ref itemsConsumed);
                                    o.Dispose();
                                }
                            }
                            else
                            {
                                o = new DisposableObject();
                                Interlocked.Increment(ref itemsCreated);
                                if (!cache.TryEnqueue(ref o))
                                {
                                    o.Dispose();
                                    Interlocked.Increment(ref itemsOverflowed);
                                }
                            }
                        }

                        o = cache.TryDequeueComprehensive();
                        while (o != null)
                        {
                            o.Dispose();
                            Interlocked.Increment(ref itemsConsumed);
                            o = cache.TryDequeueComprehensive();
                        }
                    }));
                }

                foreach (Thread thread in threads)
                {
                    thread.Start();
                }

                foreach (Thread thread in threads)
                {
                    thread.Join(TimeSpan.FromSeconds(20));
                }

                Console.WriteLine("Created " + itemsCreated);
                Console.WriteLine("Consumed " + itemsConsumed);
                Console.WriteLine("Overflowed " + itemsOverflowed);
                Assert.AreEqual(itemsCreated, itemsConsumed + itemsOverflowed);

                if (skew < -0.5f)
                {
                    Assert.AreEqual(itemsCreated, itemsConsumed, THREAD_COUNT * ITERATIONS / 10);
                    Assert.AreEqual(0, itemsOverflowed, THREAD_COUNT * ITERATIONS / 10);
                    Assert.IsTrue(cache.PoolSize <= 64);
                }
                else if (skew == 0.5f)
                {
                    Assert.AreEqual(itemsCreated, itemsConsumed, THREAD_COUNT * ITERATIONS / 10);
                    Assert.AreEqual(0, itemsOverflowed, THREAD_COUNT * ITERATIONS / 10);
                    Assert.IsTrue(cache.PoolSize <= 8096);
                }
                else if (skew > 0.5f)
                {
                    Assert.IsTrue(cache.PoolSize > 64);
                }
            }
        }

        private class DisposableObject : IDisposable
        {
            public int _disposed = 0;

            public void Dispose()
            {
                AtomicOperations.ExecuteOnce(ref _disposed);
            }
        }
    }
}
