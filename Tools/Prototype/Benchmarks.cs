using BenchmarkDotNet.Attributes;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

// var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(Benchmarks));
namespace Prototype
{
    //[MemoryDiagnoser]
    //[MinIterationCount(5000)]
    //[MaxIterationCount(10000)]
    //[ShortRunJob]
    public class Benchmarks
    {
        [GlobalSetup]
        public void GlobalSetup()
        {
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
        }

        [Benchmark(Baseline = true)]
        public DateTimeOffset DefaultCode()
        {
            return DateTimeOffset.UtcNow;
        }

        [Benchmark]
        public DateTimeOffset MyCode()
        {
            return HighPrecisionTimer.GetCurrentUTCTime();
        }
    }
}
