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
using Durandal.Common.Collections.Interning.Impl;
using BenchmarkDotNet.Jobs;
using System.IO;
using BenchmarkDotNet.Diagnosers;
using System.Collections.Frozen;
using System.Diagnostics;

namespace Prototype.NetCore
{
    //[MemoryDiagnoser]
    //[LongRunJob]
    //[MediumRunJob]
    //[SimpleJob(runtimeMoniker: RuntimeMoniker.Net70)]
    //[SimpleJob(runtimeMoniker: RuntimeMoniker.Net80)]
    //[DisassemblyDiagnoser(maxDepth: 2)]
    //[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions)]
    public class InterningBenchmarks
    {
        //[Params(10, 100, 1000)]
        [Params(600)]
        public int NumValues { get; set; }

        //[Params(10, 30, 100)]
        [Params(40)]
        public int ValueMaxLength { get; set; }

        //[Params(10, 30, 100)]
        [Params(10)]
        public int OptPasses { get; set; }

        private SealedInternalizer_CharIgnoreCase_PerfectHash char_perfecthash;
        private SealedInternalizer_CharIgnoreCase_Linear char_linear;
        private SealedInternalizer_StringIgnoreCase_Linear string_linear;
        private SealedInternalizer_StringIgnoreCase_Linear string_linear2;
        private SealedInternalizer_CharIgnoreCase_CacheOptimized char_cacheoptimized_dfs;
        private SealedInternalizer_CharIgnoreCase_CacheOptimized char_cacheoptimized_bfs;
        private SealedInternalizer_CharIgnoreCase_PerfectHash_Unchecked char_perfecthash_unchecked;
        private Dictionary<ReadOnlyMemory<char>, int> char_dictionary;
        private FrozenDictionary<ReadOnlyMemory<char>, int> char_frozendictionary;
        private FastConcurrentDictionary<string, int> char_concurrentdictionary;

        private List<KeyValuePair<int, string>> inputs;
        private List<KeyValuePair<int, string>> miss_inputs;
        private List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> inputs2;
        private List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>> inputs3;

        public class ReadOnlyMemoryCharComparer : IEqualityComparer<ReadOnlyMemory<char>>
        {
            public static ReadOnlyMemoryCharComparer Instance { get; } = new();

            public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y) =>
                x.Span.Equals(y.Span, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode(ReadOnlyMemory<char> obj) =>
                string.GetHashCode(obj.Span, StringComparison.OrdinalIgnoreCase);
        }

        [GlobalSetup]
        public void GlobalSetup()
        {
            IRandom random = new FastRandom(7777);
            StringBuilder builder = new StringBuilder();
            HashSet<string> existingStrings = new HashSet<string>();
            inputs = new List<KeyValuePair<int, string>>();
            miss_inputs = new List<KeyValuePair<int, string>>();
            inputs2 = new List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>>();
            inputs3 = new List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>>();

            //while (existingStrings.Count < NumValues)
            //{
            //    int valueLength = random.NextInt(1, ValueMaxLength);
            //    while (builder.Length < valueLength)
            //    {
            //        builder.Append((char)('a' + random.NextInt(0, 26)));
            //    }

            //    string s = builder.ToString();
            //    if (!existingStrings.Contains(s))
            //    {
            //        inputs.Add(new KeyValuePair<int, string>(existingStrings.Count, s));
            //        inputs2.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
            //            new InternedKey<ReadOnlyMemory<char>>(existingStrings.Count),
            //            s.AsMemory()));
            //        inputs3.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>(
            //            new InternedKey<ReadOnlyMemory<char>>(existingStrings.Count),
            //            s));
            //        existingStrings.Add(s);
            //    }

            //    builder.Clear();
            //}

            string[] rawInputs = File.ReadAllLines(@"C:\Code\Durandal\config strings.txt");
            foreach (string s in rawInputs)
            {
                if (!existingStrings.Contains(s))
                {
                    inputs.Add(new KeyValuePair<int, string>(existingStrings.Count, s));
                    inputs2.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                        new InternedKey<ReadOnlyMemory<char>>(existingStrings.Count),
                        s.AsMemory()));
                    inputs3.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>(
                        new InternedKey<ReadOnlyMemory<char>>(existingStrings.Count),
                        s));
                    existingStrings.Add(s);
                }

                if (existingStrings.Count >= NumValues)
                {
                    break;
                }
            }

            char_dictionary = new Dictionary<ReadOnlyMemory<char>, int>(new ReadOnlyMemoryCharComparer());
            char_concurrentdictionary = new FastConcurrentDictionary<string, int>(10, StringComparer.OrdinalIgnoreCase);
            foreach (var input in inputs)
            {
                char_dictionary[input.Value.AsMemory()] = input.Key;
                char_concurrentdictionary[input.Value] = input.Key;
            }

            char_frozendictionary = char_dictionary.ToFrozenDictionary();
            inputs.Sort((a, b) => a.Value.GetHashCode() - b.Value.GetHashCode());

            //char_perfecthash = new SealedInternalizer_CharIgnoreCase_PerfectHash(inputs2);
            char_linear = new SealedInternalizer_CharIgnoreCase_Linear(inputs2);
            //string_linear = new SealedInternalizer_StringIgnoreCase_Linear(inputs2);
            //string_linear2 = new SealedInternalizer_StringIgnoreCase_Linear(inputs3);
            //char_cacheoptimized_dfs = new SealedInternalizer_CharIgnoreCase_CacheOptimized(inputs2, true);
            //char_cacheoptimized_bfs = new SealedInternalizer_CharIgnoreCase_CacheOptimized(inputs2, false);
            //char_perfecthash_unchecked = new SealedInternalizer_CharIgnoreCase_PerfectHash_Unchecked(inputs2);

            while (miss_inputs.Count < NumValues)
            {
                int valueLength = random.NextInt(1, ValueMaxLength);
                while (builder.Length < valueLength)
                {
                    builder.Append((char)('a' + random.NextInt(0, 26)));
                }

                string s = builder.ToString();
                miss_inputs.Add(new KeyValuePair<int, string>(miss_inputs.Count, s));
                builder.Clear();
            }

            // Construct missed inputs as a varying by one single char from known inputs,
            // to simulate the absolute worst case comparison
            //StringBuilder builder = new StringBuilder();
            //missInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            //foreach (string input in stringInputsIgnoreCase)
            //{
            //    builder.Clear();
            //    builder.Append(input);
            //    string s;
            //    do
            //    {
            //        builder[rand.NextInt(0, builder.Length)] = (char)('a' + rand.NextInt(0, 26));
            //        s = builder.ToString();
            //    } while (stringInputsIgnoreCase.Contains(s));

            //    missInputs.Add(s);
            //}
        }

        //[Benchmark]
        //public void Internalize_HashDictionary()
        //{
        //    int ordinal;
        //    foreach (var input in inputs)
        //    {
        //        char_dictionary.TryGetValue(input.Value, out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_HashDictionary_Miss()
        //{
        //    int ordinal;
        //    foreach (var input in miss_inputs)
        //    {
        //        char_dictionary.TryGetValue(input.Value, out ordinal);
        //    }
        //}

        [Benchmark]
        public void Internalize_FrozenDictionary()
        {
            int ordinal;
            foreach (var input in inputs)
            {
                char_frozendictionary.TryGetValue(input.Value.AsMemory(), out ordinal);
            }
        }

        [Benchmark]
        public void Internalize_FrozenDictionary_Miss()
        {
            int ordinal;
            foreach (var input in miss_inputs)
            {
                char_frozendictionary.TryGetValue(input.Value.AsMemory(), out ordinal);
            }
        }

        //[Benchmark]
        //public void Internalize_FastConcurrentDictionary()
        //{
        //    int ordinal;
        //    foreach (var input in inputs)
        //    {
        //        char_concurrentdictionary.TryGetValue(input.Value, out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_FastConcurrentDictionary_Miss()
        //{
        //    int ordinal;
        //    foreach (var input in miss_inputs)
        //    {
        //        char_concurrentdictionary.TryGetValue(input.Value, out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_PerfectHash()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in inputs)
        //    {
        //        char_perfecthash.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_PerfectHash_Miss()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in miss_inputs)
        //    {
        //        char_perfecthash.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_PerfectHash_Unchecked()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in inputs)
        //    {
        //        char_perfecthash_unchecked.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark(Baseline = true)]
        //public void Internalize_Linear()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in inputs)
        //    {
        //        char_linear.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_Linear_Miss()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in miss_inputs)
        //    {
        //        char_linear.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark(Baseline = true)]
        //public void Internalize_LinearString()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in inputs)
        //    {
        //        string_linear.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_LinearString_Miss()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in miss_inputs)
        //    {
        //        string_linear.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_LinearString_OptimalTree()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in inputs)
        //    {
        //        string_linear2.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_LinearString_OptimalTree_Miss()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in miss_inputs)
        //    {
        //        string_linear2.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_CacheOptimized()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in inputs)
        //    {
        //        char_cacheoptimized_dfs.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_CacheOptimized_Miss()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in miss_inputs)
        //    {
        //        char_cacheoptimized_dfs.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_CacheOptimizedBreathFirst()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in inputs)
        //    {
        //        char_cacheoptimized_bfs.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}

        //[Benchmark]
        //public void Internalize_CacheOptimizedBreathFirst_Miss()
        //{
        //    InternedKey<ReadOnlyMemory<char>> ordinal;
        //    foreach (var input in miss_inputs)
        //    {
        //        char_cacheoptimized_bfs.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
        //    }
        //}
    }
}
