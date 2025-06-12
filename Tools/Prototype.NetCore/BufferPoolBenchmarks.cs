using BenchmarkDotNet.Attributes;
using Durandal.Common.IO.Hashing;
using Durandal.Common.MathExt;
using System;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using Durandal.Common.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Durandal.Common.Collections.Interning;
using Durandal.Common.IO;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Time;

// var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(Benchmarks));
namespace Prototype.NetCore
{
    [MemoryDiagnoser]
    public class BufferPoolBenchmarks
    {
        [GlobalSetup]
        public void GlobalSetup()
        {
            
        }

        //[IterationSetup]
        //public void IterationSetup()
        //{
        //}

        [Benchmark(Baseline = true)]
        public void Something()
        {
        }
    }
}
