using Durandal.Common.Collections.Interning;
using Durandal.Common.Collections.Interning.Impl;
using Durandal.Common.Compression.BZip2;
using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Collections.Interning.Impl
{
    [TestClass]
    public class Test_BasicInternalizer_Char
    {
        [TestMethod]
        public void Test_BasicInternalizer_Char_Basic()
        {
            BasicInternalizer_Char internalizer = new BasicInternalizer_Char(new InternedKeySource<ReadOnlyMemory<char>>());
            Assert.AreEqual(InternalizerFeature.None, internalizer.Features);

            List<char[]> inputs = new List<char[]>();
            inputs.Add("One".ToArray());
            inputs.Add("Two".ToArray());
            inputs.Add("Three".ToArray());
            inputs.Add("Four".ToArray());
            inputs.Add("5".ToArray());
            inputs.Add("Six".ToArray());
            inputs.Add("Seven".ToArray());
            inputs.Add("Eight".ToArray());
            inputs.Add("Niiiiiiiiiiiiiiiiiiiiiiiiiiiiine".ToArray());
            inputs.Add(new char[0]);

            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
            ReadOnlySpan<char> internalizedValue;
            foreach (char[] input in inputs)
            {
                var key = internalizer.InternalizeValue(input.AsSpan(), out internalizedValue);
                Assert.IsTrue(internalizedValue.SequenceEqual(input.AsSpan()));
                dict[key] = input.AsMemory();
            }

            // Double-internalize, make sure the key is unchanged
            foreach (char[] input in inputs)
            {
                ReadOnlyMemory<char> existingValue;
                var key = internalizer.InternalizeValue(input.AsSpan(), out internalizedValue);
                Assert.IsTrue(dict.TryGetValue(key, out existingValue));
                Assert.IsTrue(existingValue.Span.SequenceEqual(input.AsSpan()));
            }

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> internedValue;
            foreach (var kvp in dict)
            {
                Assert.IsTrue(internalizer.TryGetInternalizedKey(kvp.Value.Span, out ordinal));
                Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                Assert.IsTrue(internalizer.TryGetInternalizedValue(kvp.Value.Span, out internedValue, out ordinal));
                Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                Assert.IsTrue(internedValue.SequenceEqual(kvp.Value.Span));
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
                Assert.IsTrue(mem.Span.SequenceEqual(enumeratedValue.Value.Span));
            }
        }

        [TestMethod]
        public void Test_BasicInternalizer_Char_Random()
        {
            IRandom random = new FastRandom(12311);
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

                BasicInternalizer_Char internalizer = new BasicInternalizer_Char(new InternedKeySource<ReadOnlyMemory<char>>());
                Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
                foreach (string s in existingStrings)
                {
                    ReadOnlySpan<char> internalizedValue;
                    dict[internalizer.InternalizeValue(s.AsSpan(), out internalizedValue)] = s.AsSpan().ToArray();
                }

                InternedKey<ReadOnlyMemory<char>> ordinal;
                ReadOnlySpan<char> internedValue;
                foreach (var kvp in dict)
                {
                    Assert.IsTrue(internalizer.TryGetInternalizedKey(kvp.Value.Span, out ordinal));
                    Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                    Assert.IsTrue(internalizer.TryGetInternalizedValue(kvp.Value.Span, out internedValue, out ordinal));
                    Assert.AreEqual(kvp.Key.Key, ordinal.Key);
                    Assert.IsTrue(internedValue.SequenceEqual(kvp.Value.Span));
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
                    Assert.IsTrue(mem.Span.SequenceEqual(enumeratedValue.Value.Span));
                }

                Assert.IsNotNull(((System.Collections.IEnumerable)internalizer).GetEnumerator());
            }
        }

        [TestMethod]
        public void Test_BasicInternalizer_Char_NullValueSource()
        {
            try
            {
                BasicInternalizer_Char internalizer = new BasicInternalizer_Char(null);
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void Test_BasicInternalizer_Char_ContainsEmpty()
        {
            BasicInternalizer_Char internalizer = new BasicInternalizer_Char(new InternedKeySource<ReadOnlyMemory<char>>());

            List<char[]> inputs = new List<char[]>();
            inputs.Add(new char[0]);
            inputs.Add("One".ToArray());

            Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> dict = new Dictionary<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>();
            ReadOnlySpan<char> internalizedValue;
            foreach (char[] input in inputs)
            {
                var key = internalizer.InternalizeValue(input.AsSpan(), out internalizedValue);
                Assert.IsTrue(internalizedValue.SequenceEqual(input.AsSpan()));
                dict[key] = input.AsMemory();
            }

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> internedValue;
            Assert.IsTrue(internalizer.TryGetInternalizedKey(new char[0].AsSpan(), out ordinal));
            Assert.AreEqual(0, ordinal.Key);
            Assert.IsFalse(internalizer.TryGetInternalizedKey(new char[1].AsSpan(), out ordinal));
            Assert.IsTrue(internalizer.TryGetInternalizedKey("One".AsSpan(), out ordinal));
            Assert.AreEqual(1, ordinal.Key);

            Assert.IsTrue(internalizer.TryGetInternalizedValue(new char[0].AsSpan(), out internedValue, out ordinal));
            Assert.AreEqual(0, ordinal.Key);
            Assert.AreEqual(0, internedValue.Length);
            Assert.IsFalse(internalizer.TryGetInternalizedValue(new char[1].AsSpan(), out internedValue, out ordinal));
            Assert.IsTrue(internalizer.TryGetInternalizedValue("One".AsSpan(), out internedValue, out ordinal));
            Assert.AreEqual(1, ordinal.Key);
            Assert.IsTrue(internedValue.SequenceEqual("One".AsSpan()));
        }

        [TestMethod]
        public void Test_BasicInternalizer_Char_ZeroZeroIsAllowed()
        {
            BasicInternalizer_Char internalizer = new BasicInternalizer_Char(new InternedKeySource<ReadOnlyMemory<char>>());
            InternedKey<ReadOnlyMemory<char>> key;
            ReadOnlySpan<char> value;
            Assert.IsFalse(internalizer.TryGetInternalizedKey(ReadOnlySpan<char>.Empty, out key));
            Assert.IsFalse(internalizer.TryGetInternalizedValue(ReadOnlySpan<char>.Empty, out value, out key));

            key = internalizer.InternalizeValue(ReadOnlySpan<char>.Empty, out value);
            Assert.AreEqual(0, key.Key);
            Assert.AreEqual(0, value.Length);
            Assert.IsTrue(internalizer.TryGetInternalizedKey(ReadOnlySpan<char>.Empty, out key));
            Assert.AreEqual(0, key.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedValue(ReadOnlySpan<char>.Empty, out value, out key));
            Assert.AreEqual(0, key.Key);
            Assert.AreEqual(0, value.Length);
        }

        private class UnitTestInternedKeySource<T> : IInternedKeySource<T>
        {
            public int NextOrdinal { get; set; }

            public UnitTestInternedKeySource(int initialOrdinal)
            {
                NextOrdinal = initialOrdinal;
            }

            public InternedKey<T> GenerateNewUniqueValue()
            {
                return new InternedKey<T>(NextOrdinal);
            }
        }

        [TestMethod]
        public void Test_BasicInternalizer_Char_RedundantOrdinals()
        {
            BasicInternalizer_Char internalizer = new BasicInternalizer_Char(new UnitTestInternedKeySource<ReadOnlyMemory<char>>(5));

            List<char[]> inputs = new List<char[]>();
            inputs.Add("One".ToArray());
            inputs.Add("Two".ToArray());
            inputs.Add("Three".ToArray());
            inputs.Add("Four".ToArray());
            inputs.Add("5".ToArray());
            inputs.Add("Six".ToArray());
            inputs.Add("Seven".ToArray());
            inputs.Add("Eight".ToArray());
            inputs.Add("Niiiiiiiiiiiiiiiiiiiiiiiiiiiiine".ToArray());
            inputs.Add(new char[0]);

            ReadOnlySpan<char> internalizedValue;
            foreach (char[] input in inputs)
            {
                var key = internalizer.InternalizeValue(input.AsSpan(), out internalizedValue);
                Assert.IsTrue(internalizedValue.SequenceEqual(input.AsSpan()));
            }

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> internedValue;
            foreach (var input in inputs)
            {
                Assert.IsTrue(internalizer.TryGetInternalizedKey(input.AsSpan(), out ordinal));
                Assert.AreEqual(5, ordinal.Key);
                Assert.IsTrue(internalizer.TryGetInternalizedValue(input.AsSpan(), out internedValue, out ordinal));
                Assert.AreEqual(5, ordinal.Key);
                Assert.IsTrue(internedValue.SequenceEqual(input.AsSpan()));
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
            Assert.AreEqual(inputs.Count, internalizer.Count());
        }

        [TestMethod]
        public void Test_BasicInternalizer_Char_CrcHashCollisions()
        {
            BasicInternalizer_Char internalizer = new BasicInternalizer_Char(new InternedKeySource<ReadOnlyMemory<char>>());

            List<char[]> inputsA = new List<char[]>();
            List<char[]> inputsB = new List<char[]>();

            // collide to 2763617405
            inputsA.Add("rwrwwkpq".ToArray());
            inputsB.Add("uzivbqsk".ToArray());

            // collide to 1254032560
            inputsA.Add("ybslkdfz".ToArray());
            inputsB.Add("bcrkzjtz".ToArray());

            // collide to 393633157
            inputsA.Add("aayevrvc".ToArray());
            inputsB.Add("iqdnjzjt".ToArray());

            InternedKey<ReadOnlyMemory<char>> key;
            ReadOnlySpan<char> internedValue;

            // Internalize the input A set
            HashSet<InternedKey<ReadOnlyMemory<char>>> keysA = new HashSet<InternedKey<ReadOnlyMemory<char>>>();
            HashSet<InternedKey<ReadOnlyMemory<char>>> keysB = new HashSet<InternedKey<ReadOnlyMemory<char>>>();
            foreach (char[] input in inputsA)
            {
                key = internalizer.InternalizeValue(input.AsSpan(), out internedValue);
                Assert.IsTrue(internedValue.SequenceEqual(input.AsSpan()));
                Assert.IsTrue(keysA.Add(key));
            }

            // Inputs A should all be found
            foreach (var input in inputsA)
            {
                Assert.IsTrue(internalizer.TryGetInternalizedKey(input.AsSpan(), out key));
                Assert.IsTrue(internalizer.TryGetInternalizedValue(input.AsSpan(), out internedValue, out key));
                Assert.IsTrue(internedValue.SequenceEqual(input.AsSpan()));
            }

            // Inputs B should not be found even though they all hash collide
            foreach (var input in inputsB)
            {
                Assert.IsFalse(internalizer.TryGetInternalizedKey(input.AsSpan(), out key));
                Assert.IsFalse(internalizer.TryGetInternalizedValue(input.AsSpan(), out internedValue, out key));
            }

            // Internalize the input B set
            foreach (char[] input in inputsB)
            {
                key = internalizer.InternalizeValue(input.AsSpan(), out internedValue);
                Assert.IsTrue(internedValue.SequenceEqual(input.AsSpan()));
                Assert.IsTrue(keysB.Add(key));
            }

            // Keys for the two sets should be unique even though they hash collide
            Assert.IsFalse(keysA.Intersect(keysB).Any());

            // Inputs A and B should all be found
            foreach (var input in inputsA)
            {
                Assert.IsTrue(internalizer.TryGetInternalizedKey(input.AsSpan(), out key));
                Assert.IsTrue(internalizer.TryGetInternalizedValue(input.AsSpan(), out internedValue, out key));
                Assert.IsTrue(internedValue.SequenceEqual(input.AsSpan()));
            }

            foreach (var input in inputsB)
            {
                Assert.IsTrue(internalizer.TryGetInternalizedKey(input.AsSpan(), out key));
                Assert.IsTrue(internalizer.TryGetInternalizedValue(input.AsSpan(), out internedValue, out key));
                Assert.IsTrue(internedValue.SequenceEqual(input.AsSpan()));
            }

            // Enumeration should enumerate 6 values
            Assert.AreEqual(6, internalizer.Count());
        }

        [TestMethod]
        [Conditional("DEBUG")]
        public void Test_BasicInternalizer_Char_NegativeOrdinals()
        {
            UnitTestInternedKeySource<ReadOnlyMemory<char>> keySource = new UnitTestInternedKeySource<ReadOnlyMemory<char>>(-1);
            BasicInternalizer_Char internalizer = new BasicInternalizer_Char(keySource);

            InternedKey<ReadOnlyMemory<char>> key;
            ReadOnlySpan<char> internedValue;

            try
            {
                key = internalizer.InternalizeValue("some input".AsSpan(), out internedValue);
                Assert.Fail("Expected an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }

            keySource.NextOrdinal = 0;
            key = internalizer.InternalizeValue("rwrwwkpq".AsSpan(), out internedValue);
            Assert.AreEqual(0, key.Key);

            keySource.NextOrdinal = -1;
            try
            {
                // this should collide on CRC32 with the previous value to trigger the range check on a separate code path
                key = internalizer.InternalizeValue("uzivbqsk".AsSpan(), out internedValue);
                Assert.Fail("Expected an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void Test_BasicInternalizer_Char_CaseSensitivity()
        {
            BasicInternalizer_Char internalizer = new BasicInternalizer_Char(new InternedKeySource<ReadOnlyMemory<char>>());

            InternedKey<ReadOnlyMemory<char>> ordinal;
            ReadOnlySpan<char> internedValue;
            internalizer.InternalizeValue("lowercase".AsSpan(), out internedValue);
            internalizer.InternalizeValue("UPPERCASE".AsSpan(), out internedValue);
            internalizer.InternalizeValue("MixedCase".AsSpan(), out internedValue);

            Assert.IsTrue(internalizer.TryGetInternalizedKey("lowercase".AsSpan(), out ordinal));
            Assert.AreEqual(0, ordinal.Key);
            Assert.IsFalse(internalizer.TryGetInternalizedKey("LowerCase".AsSpan(), out ordinal));
            //Assert.AreEqual(0, ordinal.Key);
            Assert.IsFalse(internalizer.TryGetInternalizedKey("LOWERCASE".AsSpan(), out ordinal));
            //Assert.AreEqual(0, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedValue("lowercase".AsSpan(), out internedValue, out ordinal));
            Assert.IsTrue(internedValue.Equals("lowercase".AsSpan(), StringComparison.Ordinal));

            Assert.IsFalse(internalizer.TryGetInternalizedKey("uppercase".AsSpan(), out ordinal));
            //Assert.AreEqual(1, ordinal.Key);
            Assert.IsFalse(internalizer.TryGetInternalizedKey("UpperCase".AsSpan(), out ordinal));
            //Assert.AreEqual(1, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedKey("UPPERCASE".AsSpan(), out ordinal));
            Assert.AreEqual(1, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedValue("UPPERCASE".AsSpan(), out internedValue, out ordinal));
            Assert.IsTrue(internedValue.Equals("UPPERCASE".AsSpan(), StringComparison.Ordinal));

            Assert.IsFalse(internalizer.TryGetInternalizedKey("mixedcase".AsSpan(), out ordinal));
            //Assert.AreEqual(2, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedKey("MixedCase".AsSpan(), out ordinal));
            Assert.AreEqual(2, ordinal.Key);
            Assert.IsFalse(internalizer.TryGetInternalizedKey("MIXEDCASE".AsSpan(), out ordinal));
            //Assert.AreEqual(2, ordinal.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedValue("MixedCase".AsSpan(), out internedValue, out ordinal));
            Assert.IsTrue(internedValue.Equals("MixedCase".AsSpan(), StringComparison.Ordinal));
        }
    }
}
