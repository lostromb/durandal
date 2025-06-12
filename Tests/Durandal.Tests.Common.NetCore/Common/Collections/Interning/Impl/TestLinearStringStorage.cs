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
    public class TestLinearStringStorage
    {
        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_Basic()
        {
            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
            dict[new InternedKey<ReadOnlyMemory<char>>(1)] = "One".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(2)] = "Two".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(3)] = "Three".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(4)] = "Four".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(5)] = "5".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(6)] = "Six".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(7)] = "Seven".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(8)] = "Eight".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(9)] = "Niiiiiiiiiiiiiiiiiiiiiiiiiiiiine".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(10)] = new char[0];
            SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);
            Assert.AreEqual(InternalizerFeature.CaseInsensitive | InternalizerFeature.CanDoReverseLookup, internalizer.Features);

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> internedValue;
            foreach (var kvp in dict)
            {
                Assert.IsTrue(internalizer.TryGetInternalizedKey(kvp.Value.Span, out ordinal));
                Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                Assert.IsTrue(internalizer.TryGetInternalizedValue(kvp.Value.Span, out internedValue, out ordinal));
                Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                Assert.IsTrue(internedValue.Equals(kvp.Value.Span, StringComparison.Ordinal));
            }

            // Test missing values as well
            IRandom rand = new FastRandom(451123);
            char[] randField = new char[100];
            for (int c = 0; c < 1000; c++)
            {
                int length = rand.NextInt(2, randField.Length);
                for (int z = 0; z < length; z++)
                {
                    randField[z] = (char)('a' + rand.NextInt(0, 26));
                }

                Assert.IsFalse(internalizer.TryGetInternalizedKey(randField.AsSpan(0, length), out ordinal));
            }

            // And test enumeration too
            Assert.AreEqual(dict.Count, internalizer.Count());
            foreach (var enumeratedValue in internalizer)
            {
                ReadOnlyMemory<char> mem;
                Assert.IsTrue(dict.TryGetValue(enumeratedValue.Key, out mem));
                Assert.IsTrue(mem.Span.Equals(enumeratedValue.Value.Span, StringComparison.Ordinal));
            }
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_Random()
        {
            IRandom random = new FastRandom(11231);
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

                Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
                int ordinalNum = 1;
                foreach (string s in existingStrings)
                {
                    dict[new InternedKey<ReadOnlyMemory<char>>(ordinalNum++)] = s.AsMemory();
                }

                SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);

                Assert.AreNotEqual(0, internalizer.EstimatedMemoryUse);

                InternedKey<ReadOnlyMemory<char>> ordinal;
                ReadOnlySpan<char> internedValue;
                foreach (var kvp in dict)
                {
                    Assert.IsTrue(internalizer.TryGetInternalizedKey(kvp.Value.Span, out ordinal));
                    Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                    Assert.IsTrue(internalizer.TryGetInternalizedValue(kvp.Value.Span, out internedValue, out ordinal));
                    Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                    Assert.IsTrue(internedValue.Equals(kvp.Value.Span, StringComparison.Ordinal));
                }

                // Test missing values as well
                for (int c = 0; c < 100000; c++)
                {
                    int length = random.NextInt(1, 100);
                    while (builder.Length < length)
                    {
                        builder.Append((char)('a' + random.NextInt(0, 26)));
                    }

                    string s = builder.ToString();
                    if (!existingStrings.Contains(s))
                    {
                        Assert.IsFalse(internalizer.TryGetInternalizedKey(s.AsSpan(), out ordinal));
                    }

                    builder.Clear();
                }

                // And test enumeration too
                Assert.AreEqual(dict.Count, internalizer.Count());
                foreach (var enumeratedValue in internalizer)
                {
                    ReadOnlyMemory<char> mem;
                    Assert.IsTrue(dict.TryGetValue(enumeratedValue.Key, out mem));
                    Assert.IsTrue(mem.Span.Equals(enumeratedValue.Value.Span, StringComparison.Ordinal));
                }

                Assert.IsNotNull(((System.Collections.IEnumerable)internalizer).GetEnumerator());
            }
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_NullInput()
        {
            try
            {
                SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear((IEnumerable<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>>)null);
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }

            try
            {
                SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear((IEnumerable<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>>)null);
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_ContainsEmpty()
        {
            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
            dict[new InternedKey<ReadOnlyMemory<char>>(1)] = new char[0];
            dict[new InternedKey<ReadOnlyMemory<char>>(2)] = "One".AsMemory();
            SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> internedValue;
            Assert.IsTrue(internalizer.TryGetInternalizedKey(new char[0].AsSpan(), out ordinal));
            Assert.AreEqual(1, ordinal.Key);
            Assert.IsFalse(internalizer.TryGetInternalizedKey(new char[1].AsSpan(), out ordinal));
            Assert.IsTrue(internalizer.TryGetInternalizedKey("One".AsSpan(), out ordinal));
            Assert.AreEqual(2, ordinal.Key);

            Assert.IsTrue(internalizer.TryGetInternalizedValue(new char[0].AsSpan(), out internedValue, out ordinal));
            Assert.AreEqual(1, ordinal.Key);
            Assert.AreEqual(0, internedValue.Length);
            Assert.IsFalse(internalizer.TryGetInternalizedValue(new char[1].AsSpan(), out internedValue, out ordinal));
            Assert.IsTrue(internalizer.TryGetInternalizedValue("One".AsSpan(), out internedValue, out ordinal));
            Assert.AreEqual(2, ordinal.Key);
            Assert.IsTrue(internedValue.Equals("One".AsSpan(), StringComparison.Ordinal));
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_ZeroZeroIsAllowed()
        {
            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
            dict[new InternedKey<ReadOnlyMemory<char>>(0)] = new char[0];

            SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> internedValue;
            Assert.IsTrue(internalizer.TryGetInternalizedKey(new char[0].AsSpan(), out ordinal));
            Assert.AreEqual(0, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedValue(new char[0].AsSpan(), out internedValue, out ordinal));
            Assert.AreEqual(0, ordinal.Key);
            Assert.AreEqual(0, internedValue.Length);
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_ContainsMultipleEmptyValues()
        {
            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
            dict[new InternedKey<ReadOnlyMemory<char>>(1)] = new char[0];
            dict[new InternedKey<ReadOnlyMemory<char>>(2)] = new char[0];
            try
            {
                SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_DoesntContainEmpty()
        {
            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
            dict[new InternedKey<ReadOnlyMemory<char>>(1)] = "333".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(2)] = "22".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(3)] = "1".AsMemory();
            SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> internedValue;
            Assert.IsFalse(internalizer.TryGetInternalizedKey(new char[0].AsSpan(), out ordinal));
            Assert.IsFalse(internalizer.TryGetInternalizedKey(new char[1].AsSpan(), out ordinal));
            Assert.IsTrue(internalizer.TryGetInternalizedKey("333".AsSpan(), out ordinal));
            Assert.AreEqual(1, ordinal.Key);

            Assert.IsFalse(internalizer.TryGetInternalizedValue(new char[0].AsSpan(), out internedValue, out ordinal));
            Assert.IsFalse(internalizer.TryGetInternalizedValue(new char[1].AsSpan(), out internedValue, out ordinal));
            Assert.IsTrue(internalizer.TryGetInternalizedValue("333".AsSpan(), out internedValue, out ordinal));
            Assert.AreEqual(1, ordinal.Key);
            Assert.IsTrue(internedValue.Equals("333".AsSpan(), StringComparison.Ordinal));
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_DoesntContainEmpty2()
        {
            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
            // should generate a table with length mod 4 and nothing at entry 0 of the length table
            dict[new InternedKey<ReadOnlyMemory<char>>(1)] = "1".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(2)] = "333".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(3)] = "666666".AsMemory();
            SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> value;
            Assert.IsFalse(internalizer.TryGetInternalizedKey(new char[0].AsSpan(), out ordinal));
            Assert.IsFalse(internalizer.TryGetInternalizedValue(new char[0].AsSpan(), out value, out ordinal));
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_NoInputs()
        {
            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
            SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> internedValue;
            Assert.IsFalse(internalizer.TryGetInternalizedKey(new char[0].AsSpan(), out ordinal));
            Assert.IsFalse(internalizer.TryGetInternalizedKey(new char[1].AsSpan(), out ordinal));
            Assert.IsFalse(internalizer.TryGetInternalizedValue(new char[0].AsSpan(), out internedValue, out ordinal));
            Assert.IsFalse(internalizer.TryGetInternalizedValue(new char[1].AsSpan(), out internedValue, out ordinal));
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_ThrowsExceptionOnDuplicateValues()
        {
            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
            dict[new InternedKey<ReadOnlyMemory<char>>(1)] = "One".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(2)] = "One".AsMemory();
            try
            {
                SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_ReliablyConstructsTree()
        {
            IRandom random = new FastRandom(163);
            StringBuilder builder = new StringBuilder();
            HashSet<string> existingStrings = new HashSet<string>();
            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
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
                    dict[new InternedKey<ReadOnlyMemory<char>>(ordinalNum++)] = s.AsMemory();
                }

                SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);
                foreach (var kvp in dict)
                {
                    InternedKey<ReadOnlyMemory<char>> ordinal;
                    Assert.IsTrue(internalizer.TryGetInternalizedKey(kvp.Value.Span, out ordinal));
                    Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                }
            }
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_RedundantOrdinals()
        {
            List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> dict = new List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>>();
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(new InternedKey<ReadOnlyMemory<char>>(1), "One".AsMemory()));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(new InternedKey<ReadOnlyMemory<char>>(1), "Two".AsMemory()));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(new InternedKey<ReadOnlyMemory<char>>(1), "Three".AsMemory()));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(new InternedKey<ReadOnlyMemory<char>>(1), "Four".AsMemory()));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(new InternedKey<ReadOnlyMemory<char>>(1), "5".AsMemory()));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(new InternedKey<ReadOnlyMemory<char>>(1), "Six".AsMemory()));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(new InternedKey<ReadOnlyMemory<char>>(1), "Seven".AsMemory()));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(new InternedKey<ReadOnlyMemory<char>>(1), "Eight".AsMemory()));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(new InternedKey<ReadOnlyMemory<char>>(1), "Niiiiiiiiiiiiiiiiiiiiiiiiiiiiine".AsMemory()));
            dict.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(new InternedKey<ReadOnlyMemory<char>>(1), new char[0]));
            SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> internedValue;
            foreach (var kvp in dict)
            {
                Assert.IsTrue(internalizer.TryGetInternalizedKey(kvp.Value.Span, out ordinal));
                Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                Assert.IsTrue(internalizer.TryGetInternalizedValue(kvp.Value.Span, out internedValue, out ordinal));
                Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                Assert.IsTrue(internedValue.Equals(kvp.Value.Span, StringComparison.Ordinal));
            }

            // Test missing values as well
            IRandom rand = new FastRandom(451123);
            char[] randField = new char[100];
            for (int c = 0; c < 1000; c++)
            {
                int length = rand.NextInt(2, randField.Length);
                for (int z = 0; z < length; z++)
                {
                    randField[z] = (char)('a' + rand.NextInt(0, 26));
                }

                Assert.IsFalse(internalizer.TryGetInternalizedKey(randField.AsSpan(0, length), out ordinal));
            }

            Assert.AreEqual(dict.Count, internalizer.Count());
        }

        [TestMethod]
        public void Test_SealedInternalizer_StringIgnoreCase_Linear_CaseSensitivity()
        {
            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
            dict[new InternedKey<ReadOnlyMemory<char>>(1)] = "lowercase".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(2)] = "UPPERCASE".AsMemory();
            dict[new InternedKey<ReadOnlyMemory<char>>(3)] = "MixedCase".AsMemory();
            SealedInternalizer_StringIgnoreCase_Linear internalizer = new SealedInternalizer_StringIgnoreCase_Linear(dict);

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> internedValue;

            Assert.IsTrue(internalizer.TryGetInternalizedKey("lowercase".AsSpan(), out ordinal));
            Assert.AreEqual(1, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedKey("LowerCase".AsSpan(), out ordinal));
            Assert.AreEqual(1, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedKey("LOWERCASE".AsSpan(), out ordinal));
            Assert.AreEqual(1, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedValue("lowercase".AsSpan(), out internedValue, out ordinal));
            Assert.IsTrue(internedValue.Equals("lowercase".AsSpan(), StringComparison.Ordinal));

            Assert.IsTrue(internalizer.TryGetInternalizedKey("uppercase".AsSpan(), out ordinal));
            Assert.AreEqual(2, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedKey("UpperCase".AsSpan(), out ordinal));
            Assert.AreEqual(2, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedKey("UPPERCASE".AsSpan(), out ordinal));
            Assert.AreEqual(2, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedValue("UPPERCASE".AsSpan(), out internedValue, out ordinal));
            Assert.IsTrue(internedValue.Equals("UPPERCASE".AsSpan(), StringComparison.Ordinal));

            Assert.IsTrue(internalizer.TryGetInternalizedKey("mixedcase".AsSpan(), out ordinal));
            Assert.AreEqual(3, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedKey("MixedCase".AsSpan(), out ordinal));
            Assert.AreEqual(3, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedKey("MIXEDCASE".AsSpan(), out ordinal));
            Assert.AreEqual(3, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedValue("MixedCase".AsSpan(), out internedValue, out ordinal));
            Assert.IsTrue(internedValue.Equals("MixedCase".AsSpan(), StringComparison.Ordinal));
        }
    }
}
