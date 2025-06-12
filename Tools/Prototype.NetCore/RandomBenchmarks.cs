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

// var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(Benchmarks));
namespace Prototype.NetCore
{
    //[ShortRunJob]
    public class RandomBenchmarks
    {
        private FastRandom _fastRand;
        private DefaultRandom _systemRand;
        private byte[] _field;

        [Params(32, 128, 100000)]
        public int ByteCount { get; set; }

        //[Params(0, 1, 2, 3, 4, 5, 6, 7, 8)]
        [Params(0, 3)]
        public int Offset { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            _fastRand = new FastRandom();
            _systemRand = new DefaultRandom();
            _field = new byte[ByteCount + Offset];
        }

        //[IterationSetup]
        //public void IterationSetup()
        //{
        //}

        [Benchmark]
        public void FastRandomBytes()
        {
            _fastRand.NextBytes(_field.AsSpan(Offset));
        }

        [Benchmark]
        public void DefaultRandomBytes()
        {
            _systemRand.NextBytes(_field.AsSpan(Offset));
        }

        //[Benchmark]
        //public void GenFloats_Default()
        //{
        //    for (int c = 0; c < _randomFloats.Length; c++)
        //    {
        //        _randomFloats[c] = _defaultRand.NextFloat();
        //    }
        //}
    }
}
