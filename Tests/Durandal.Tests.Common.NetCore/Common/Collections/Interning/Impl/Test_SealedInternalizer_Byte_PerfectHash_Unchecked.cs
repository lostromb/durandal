using Durandal.Common.Collections.Interning;
using Durandal.Common.Collections.Interning.Impl;
using Durandal.Common.Compression.BZip2;
using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Collections.Interning.Impl
{
    [TestClass]
    public class Test_SealedInternalizer_Byte_PerfectHash_Unchecked
    {
        [TestMethod]
        public void Test_SealedInternalizer_Byte_PerfectHash_Unchecked_Basic()
        {
            Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>> dict = new Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>();
            dict[new InternedKey<ReadOnlyMemory<byte>>(1)] = Encoding.UTF8.GetBytes("One");
            dict[new InternedKey<ReadOnlyMemory<byte>>(2)] = Encoding.UTF8.GetBytes("Two");
            dict[new InternedKey<ReadOnlyMemory<byte>>(3)] = Encoding.UTF8.GetBytes("Three");
            dict[new InternedKey<ReadOnlyMemory<byte>>(4)] = Encoding.UTF8.GetBytes("Four");
            dict[new InternedKey<ReadOnlyMemory<byte>>(5)] = Encoding.UTF8.GetBytes("5");
            dict[new InternedKey<ReadOnlyMemory<byte>>(6)] = Encoding.UTF8.GetBytes("Six");
            dict[new InternedKey<ReadOnlyMemory<byte>>(7)] = Encoding.UTF8.GetBytes("Seven");
            dict[new InternedKey<ReadOnlyMemory<byte>>(8)] = Encoding.UTF8.GetBytes("Eight");
            dict[new InternedKey<ReadOnlyMemory<byte>>(9)] = Encoding.UTF8.GetBytes("Niiiiiiiiiiiiiiiiiiiiiiiiiiiiine");
            dict[new InternedKey<ReadOnlyMemory<byte>>(10)] = new byte[0];
            SealedInternalizer_Byte_PerfectHash_Unchecked internalizer = new SealedInternalizer_Byte_PerfectHash_Unchecked(dict);
            Assert.AreEqual(InternalizerFeature.OnlyMatchesWithinSet, internalizer.Features);

            InternedKey<ReadOnlyMemory<byte>> ordinal;
            ReadOnlySpan<byte> internedValue;
            foreach (var kvp in dict)
            {
                Assert.IsTrue(internalizer.TryGetInternalizedKey(kvp.Value.Span, out ordinal));
                Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                Assert.IsTrue(internalizer.TryGetInternalizedValue(kvp.Value.Span, out internedValue, out ordinal));
                Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                Assert.IsTrue(internedValue.SequenceEqual(kvp.Value.Span));
            }

            // And test enumeration too
            Assert.AreEqual(dict.Count, internalizer.Count());
            foreach (var enumeratedValue in internalizer)
            {
                ReadOnlyMemory<byte> mem;
                Assert.IsTrue(dict.TryGetValue(enumeratedValue.Key, out mem));
                Assert.IsTrue(mem.Span.SequenceEqual(enumeratedValue.Value.Span));
            }
        }

        [TestMethod]
        public void Test_SealedInternalizer_Byte_PerfectHash_Unchecked_Random()
        {
            IRandom random = new FastRandom(76909);
            StringBuilder builder = new StringBuilder();
            HashSet<string> existingStrings = new HashSet<string>();
            for (int iter = 0; iter < 10; iter++)
            {
                int numInputs = random.NextInt(1, 1000);
                existingStrings.Clear();
                while (existingStrings.Count < numInputs)
                {
                    // use polynomial factors so the length distribution is non-uniform, this should ensure that there
                    // are holes and irregularities in the generated table
                    int length = random.NextInt(1, 100) * random.NextInt(1, 100);
                    while (builder.Length < length)
                    {
                        builder.Append((char)('a' + random.NextInt(0, 26)));
                    }

                    string s = builder.ToString();
                    while (existingStrings.Contains(s))
                    {
                        builder[random.NextInt(0, builder.Length)] = (char)('a' + random.NextInt(0, 26));
                        s = builder.ToString();
                    }

                    existingStrings.Add(s);

                    builder.Clear();
                }

                Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>> dict = new Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>();
                int ordinalNum = 1;
                foreach (string s in existingStrings)
                {
                    dict[new InternedKey<ReadOnlyMemory<byte>>(ordinalNum++)] = Encoding.UTF8.GetBytes(s);
                }

                SealedInternalizer_Byte_PerfectHash_Unchecked internalizer = new SealedInternalizer_Byte_PerfectHash_Unchecked(dict);

                Assert.AreNotEqual(0, internalizer.EstimatedMemoryUse);

                InternedKey<ReadOnlyMemory<byte>> ordinal;
                ReadOnlySpan<byte> internedValue;
                foreach (var kvp in dict)
                {
                    Assert.IsTrue(internalizer.TryGetInternalizedKey(kvp.Value.Span, out ordinal));
                    Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                    Assert.IsTrue(internalizer.TryGetInternalizedValue(kvp.Value.Span, out internedValue, out ordinal));
                    Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                    Assert.IsTrue(internedValue.SequenceEqual(kvp.Value.Span));
                }

                // And test enumeration too
                Assert.AreEqual(dict.Count, internalizer.Count());
                foreach (var enumeratedValue in internalizer)
                {
                    ReadOnlyMemory<byte> mem;
                    Assert.IsTrue(dict.TryGetValue(enumeratedValue.Key, out mem));
                    Assert.IsTrue(mem.Span.SequenceEqual(enumeratedValue.Value.Span));
                }

                Assert.IsNotNull(((System.Collections.IEnumerable)internalizer).GetEnumerator());
            }
        }

        [TestMethod]
        public void Test_SealedInternalizer_Byte_PerfectHash_Unchecked_NullInput()
        {
            try
            {
                SealedInternalizer_Byte_PerfectHash_Unchecked internalizer = new SealedInternalizer_Byte_PerfectHash_Unchecked(null);
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void Test_SealedInternalizer_Byte_PerfectHash_Unchecked_ContainsEmpty()
        {
            Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>> dict = new Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>();
            dict[new InternedKey<ReadOnlyMemory<byte>>(1)] = new byte[0];
            dict[new InternedKey<ReadOnlyMemory<byte>>(2)] = Encoding.UTF8.GetBytes("One");
            SealedInternalizer_Byte_PerfectHash_Unchecked internalizer = new SealedInternalizer_Byte_PerfectHash_Unchecked(dict);

            InternedKey<ReadOnlyMemory<byte>> ordinal;
            ReadOnlySpan<byte> internedValue;
            Assert.IsTrue(internalizer.TryGetInternalizedKey(new byte[0].AsSpan(), out ordinal));
            Assert.AreEqual(1, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedKey(Encoding.UTF8.GetBytes("One").AsSpan(), out ordinal));
            Assert.AreEqual(2, ordinal.Key);

            Assert.IsTrue(internalizer.TryGetInternalizedValue(new byte[0].AsSpan(), out internedValue, out ordinal));
            Assert.AreEqual(1, ordinal.Key);
            Assert.AreEqual(0, internedValue.Length);
            Assert.IsTrue(internalizer.TryGetInternalizedValue(Encoding.UTF8.GetBytes("One").AsSpan(), out internedValue, out ordinal));
            Assert.AreEqual(2, ordinal.Key);
            Assert.IsTrue(internedValue.SequenceEqual(Encoding.UTF8.GetBytes("One").AsSpan()));
        }

        [TestMethod]
        public void Test_SealedInternalizer_Byte_PerfectHash_Unchecked_ZeroZeroIsAllowed()
        {
            Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>> dict = new Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>();
            dict[new InternedKey<ReadOnlyMemory<byte>>(0)] = new byte[0];

            SealedInternalizer_Byte_PerfectHash_Unchecked internalizer = new SealedInternalizer_Byte_PerfectHash_Unchecked(dict);

            InternedKey<ReadOnlyMemory<byte>> ordinal;
            ReadOnlySpan<byte> internedValue;
            Assert.IsTrue(internalizer.TryGetInternalizedKey(new byte[0].AsSpan(), out ordinal));
            Assert.AreEqual(0, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedValue(new byte[0].AsSpan(), out internedValue, out ordinal));
            Assert.AreEqual(0, ordinal.Key);
            Assert.AreEqual(0, internedValue.Length);
        }

        [TestMethod]
        public void Test_SealedInternalizer_Byte_PerfectHash_Unchecked_ContainsMultipleEmptyValues()
        {
            Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>> dict = new Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>();
            dict[new InternedKey<ReadOnlyMemory<byte>>(1)] = new byte[0];
            dict[new InternedKey<ReadOnlyMemory<byte>>(2)] = new byte[0];
            try
            {
                SealedInternalizer_Byte_PerfectHash_Unchecked internalizer = new SealedInternalizer_Byte_PerfectHash_Unchecked(dict);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void Test_SealedInternalizer_Byte_PerfectHash_Unchecked_ThrowsExceptionOnDuplicateValues()
        {
            Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>> dict = new Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>();
            dict[new InternedKey<ReadOnlyMemory<byte>>(1)] = Encoding.UTF8.GetBytes("One");
            dict[new InternedKey<ReadOnlyMemory<byte>>(2)] = Encoding.UTF8.GetBytes("One");
            try
            {
                SealedInternalizer_Byte_PerfectHash_Unchecked internalizer = new SealedInternalizer_Byte_PerfectHash_Unchecked(dict);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void Test_SealedInternalizer_Byte_PerfectHash_Unchecked_ReliablyConstructsTree()
        {
            IRandom random = new FastRandom(9121);
            StringBuilder builder = new StringBuilder();
            HashSet<string> existingStrings = new HashSet<string>();
            Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>> dict = new Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>();
            for (int iter = 0; iter < 100; iter++)
            {
                int numInputs = random.NextInt(1, 1000);
                existingStrings.Clear();
                while (existingStrings.Count < numInputs)
                {
                    int length = random.NextInt(1, 100) * random.NextInt(1, 100);
                    while (builder.Length < length)
                    {
                        builder.Append((char)('a' + random.NextInt(0, 26)));
                    }

                    string s = builder.ToString();
                    if (!existingStrings.Contains(s))
                    {
                        existingStrings.Add(s);
                    }

                    builder.Clear();
                }

                dict.Clear();
                int ordinalNum = 1;
                foreach (string s in existingStrings)
                {
                    dict[new InternedKey<ReadOnlyMemory<byte>>(ordinalNum++)] = Encoding.UTF8.GetBytes(s);
                }

                SealedInternalizer_Byte_PerfectHash_Unchecked internalizer = new SealedInternalizer_Byte_PerfectHash_Unchecked(dict);
                foreach (var kvp in dict)
                {
                    InternedKey<ReadOnlyMemory<byte>> ordinal;
                    Assert.IsTrue(internalizer.TryGetInternalizedKey(kvp.Value.Span, out ordinal));
                    Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                }
            }
        }

        [TestMethod]
        public void Test_SealedInternalizer_Byte_PerfectHash_Unchecked_RedundantOrdinals()
        {
            List<KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>> dict = new List<KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>>();
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>(new InternedKey<ReadOnlyMemory<byte>>(1), Encoding.UTF8.GetBytes("One")));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>(new InternedKey<ReadOnlyMemory<byte>>(1), Encoding.UTF8.GetBytes("Two")));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>(new InternedKey<ReadOnlyMemory<byte>>(1), Encoding.UTF8.GetBytes("Three")));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>(new InternedKey<ReadOnlyMemory<byte>>(1), Encoding.UTF8.GetBytes("Four")));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>(new InternedKey<ReadOnlyMemory<byte>>(1), Encoding.UTF8.GetBytes("5")));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>(new InternedKey<ReadOnlyMemory<byte>>(1), Encoding.UTF8.GetBytes("Six")));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>(new InternedKey<ReadOnlyMemory<byte>>(1), Encoding.UTF8.GetBytes("Seven")));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>(new InternedKey<ReadOnlyMemory<byte>>(1), Encoding.UTF8.GetBytes("Eight")));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>(new InternedKey<ReadOnlyMemory<byte>>(1), Encoding.UTF8.GetBytes("Niiiiiiiiiiiiiiiiiiiiiiiiiiiiine")));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>(new InternedKey<ReadOnlyMemory<byte>>(1), new byte[0]));
            SealedInternalizer_Byte_PerfectHash_Unchecked internalizer = new SealedInternalizer_Byte_PerfectHash_Unchecked(dict);

            InternedKey<ReadOnlyMemory<byte>> ordinal;
            ReadOnlySpan<byte> internedValue;
            foreach (var kvp in dict)
            {
                Assert.IsTrue(internalizer.TryGetInternalizedKey(kvp.Value.Span, out ordinal));
                Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                Assert.IsTrue(internalizer.TryGetInternalizedValue(kvp.Value.Span, out internedValue, out ordinal));
                Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                Assert.IsTrue(internedValue.SequenceEqual(kvp.Value.Span));
            }

            Assert.AreEqual(dict.Count, internalizer.Count());
        }
    }
}
