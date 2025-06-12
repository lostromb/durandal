using BenchmarkDotNet.Attributes;
using Durandal.Common.Cache;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(Benchmarks));
namespace Prototype.NetCore
{
    //[ShortRunJob]
    //[MemoryDiagnoser]
    //[DisassemblyDiagnoser]
    //[LongRunJob]
    public class Benchmarks
    {
        //[Params(1, 2, 4, 8)]
        [Params(8)]
        public int ThreadCount { get; set; }

        const int ITERATIONS = 1000;

        private LockFreeCache<object> _fixedCache;
        private DynamicLockFreeCache<object> _dynamicCache;
        private Barrier _fixedCacheBarrier;
        private Barrier _dynamicCacheBarrier;
        private CancellationTokenSource _cts;
        private CancellationToken _cancelToken;
        private List<Thread> _threads;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _fixedCache = new LockFreeCache<object>(128);
            _dynamicCache = new DynamicLockFreeCache<object>(128, TimeSpan.Zero);

            for (int c = 0; c < 64; c++)
            {
                _fixedCache.TryEnqueue(new object());
                _dynamicCache.TryEnqueue(new object());
            }

            _cts = new CancellationTokenSource();
            _cancelToken = _cts.Token;

            _fixedCacheBarrier = new Barrier(ThreadCount + 1);
            _dynamicCacheBarrier = new Barrier(ThreadCount + 1);
            _threads = new List<Thread>();
            for (int c = 0; c < ThreadCount; c++)
            {
                _threads.Add(new Thread(() =>
                {
                    while (!_cancelToken.IsCancellationRequested)
                    {
                        _fixedCacheBarrier.SignalAndWait(_cancelToken);
                        for (int iter = 0; iter < ITERATIONS; iter++)
                        {
                            AccessFixedCache();
                        }
                        _fixedCacheBarrier.SignalAndWait(_cancelToken);
                    }
                }));

                _threads.Add(new Thread(() =>
                {
                    while (!_cancelToken.IsCancellationRequested)
                    {
                        _dynamicCacheBarrier.SignalAndWait(_cancelToken);
                        for (int iter = 0; iter < ITERATIONS; iter++)
                        {
                            AccessDynamicCache();
                        }
                        _dynamicCacheBarrier.SignalAndWait(_cancelToken);
                    }
                }));
            }

            foreach (Thread t in _threads)
            {
                t.Start();
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _cts.Cancel();
            _fixedCacheBarrier.Dispose();
            _dynamicCacheBarrier.Dispose();

            foreach (Thread t in _threads)
            {
                t.Join();
            }
        }

        private void AccessFixedCache()
        {
            object o = _fixedCache.TryDequeue();
            if (o == null)
            {
                o = new object();
            }

            _fixedCache.TryEnqueue(o);
        }

        private void AccessDynamicCache()
        {
            object o = _dynamicCache.TryDequeue();
            if (o == null)
            {
                o = new object();
            }

            _dynamicCache.TryEnqueue(o);
        }

        [Benchmark(Baseline = true)]
        public void FixedCache()
        {
            for (int iter = 0; iter < ITERATIONS; iter++)
            {
                AccessFixedCache();
            }
        }

        [Benchmark]
        public void DynamicCache()
        {
            for (int iter = 0; iter < ITERATIONS; iter++)
            {
                AccessDynamicCache();
            }
        }

        [Benchmark]
        public void FixedCacheThreaded()
        {
            _fixedCacheBarrier.SignalAndWait(_cancelToken);
            _fixedCacheBarrier.SignalAndWait(_cancelToken);
        }

        [Benchmark]
        public void DynamicCacheThreaded()
        {
            _dynamicCacheBarrier.SignalAndWait(_cancelToken);
            _dynamicCacheBarrier.SignalAndWait(_cancelToken);
        }
    }
}
