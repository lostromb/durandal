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
    public class Test_BasicInternalizer_Byte
    {
        [TestMethod]
        public void Test_BasicInternalizer_Byte_Basic()
        {
            BasicInternalizer_Byte internalizer = new BasicInternalizer_Byte(new InternedKeySource<ReadOnlyMemory<byte>>());
            Assert.AreEqual(InternalizerFeature.None, internalizer.Features);

            List<byte[]> inputs = new List<byte[]>();
            inputs.Add(Encoding.UTF8.GetBytes("One"));
            inputs.Add(Encoding.UTF8.GetBytes("Two"));
            inputs.Add(Encoding.UTF8.GetBytes("Three"));
            inputs.Add(Encoding.UTF8.GetBytes("Four"));
            inputs.Add(Encoding.UTF8.GetBytes("5"));
            inputs.Add(Encoding.UTF8.GetBytes("Six"));
            inputs.Add(Encoding.UTF8.GetBytes("Seven"));
            inputs.Add(Encoding.UTF8.GetBytes("Eight"));
            inputs.Add(Encoding.UTF8.GetBytes("Niiiiiiiiiiiiiiiiiiiiiiiiiiiiine"));
            inputs.Add(new byte[0]);

            Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>> dict = new Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>();
            ReadOnlySpan<byte> internalizedValue;
            foreach (byte[] input in inputs)
            {
                var key = internalizer.InternalizeValue(input.AsSpan(), out internalizedValue);
                Assert.IsTrue(internalizedValue.SequenceEqual(input.AsSpan()));
                dict[key] = input.AsMemory();
            }

            // Double-internalize, make sure the key is unchanged
            foreach (byte[] input in inputs)
            {
                ReadOnlyMemory<byte> existingValue;
                var key = internalizer.InternalizeValue(input.AsSpan(), out internalizedValue);
                Assert.IsTrue(dict.TryGetValue(key, out existingValue));
                Assert.IsTrue(existingValue.Span.SequenceEqual(input.AsSpan()));
            }

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

            // Test missing values as well
            IRandom rand = new FastRandom(451123);
            byte[] randField = new byte[100];
            for (int c = 0; c < 1000; c++)
            {
                int length = rand.NextInt(2, randField.Length);
                rand.NextBytes(randField);
                Assert.IsFalse(internalizer.TryGetInternalizedKey(randField.AsSpan(0, length), out ordinal));
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
        public void Test_BasicInternalizer_Byte_Random()
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

                BasicInternalizer_Byte internalizer = new BasicInternalizer_Byte(new InternedKeySource<ReadOnlyMemory<byte>>());
                Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>> dict = new Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>();
                foreach (string s in existingStrings)
                {
                    ReadOnlySpan<byte> internalizedValue;
                    dict[internalizer.InternalizeValue(Encoding.UTF8.GetBytes(s).AsSpan(), out internalizedValue)] = Encoding.UTF8.GetBytes(s);
                }

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
                        Assert.IsFalse(internalizer.TryGetInternalizedKey(Encoding.UTF8.GetBytes(s), out ordinal));
                    }

                    builder.Clear();
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
        public void Test_BasicInternalizer_Byte_NullValueSource()
        {
            try
            {
                BasicInternalizer_Byte internalizer = new BasicInternalizer_Byte(null);
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void Test_BasicInternalizer_Byte_ContainsEmpty()
        {
            BasicInternalizer_Byte internalizer = new BasicInternalizer_Byte(new InternedKeySource<ReadOnlyMemory<byte>>());

            List<byte[]> inputs = new List<byte[]>();
            inputs.Add(new byte[0]);
            inputs.Add(Encoding.UTF8.GetBytes("One"));

            Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>> dict = new Dictionary<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>();
            ReadOnlySpan<byte> internalizedValue;
            foreach (byte[] input in inputs)
            {
                var key = internalizer.InternalizeValue(input.AsSpan(), out internalizedValue);
                Assert.IsTrue(internalizedValue.SequenceEqual(input.AsSpan()));
                dict[key] = input.AsMemory();
            }

            InternedKey<ReadOnlyMemory<byte>> ordinal;
            ReadOnlySpan<byte> internedValue;
            Assert.IsTrue(internalizer.TryGetInternalizedKey(new byte[0].AsSpan(), out ordinal));
            Assert.AreEqual(0, ordinal.Key);
            Assert.IsFalse(internalizer.TryGetInternalizedKey(new byte[1].AsSpan(), out ordinal));
            Assert.IsTrue(internalizer.TryGetInternalizedKey(Encoding.UTF8.GetBytes("One").AsSpan(), out ordinal));
            Assert.AreEqual(1, ordinal.Key);

            Assert.IsTrue(internalizer.TryGetInternalizedValue(new byte[0].AsSpan(), out internedValue, out ordinal));
            Assert.AreEqual(0, ordinal.Key);
            Assert.AreEqual(0, internedValue.Length);
            Assert.IsFalse(internalizer.TryGetInternalizedValue(new byte[1].AsSpan(), out internedValue, out ordinal));
            Assert.IsTrue(internalizer.TryGetInternalizedValue(Encoding.UTF8.GetBytes("One").AsSpan(), out internedValue, out ordinal));
            Assert.AreEqual(1, ordinal.Key);
            Assert.IsTrue(internedValue.SequenceEqual(Encoding.UTF8.GetBytes("One").AsSpan()));
        }

        [TestMethod]
        public void Test_BasicInternalizer_Byte_ZeroZeroIsAllowed()
        {
            BasicInternalizer_Byte internalizer = new BasicInternalizer_Byte(new InternedKeySource<ReadOnlyMemory<byte>>());
            InternedKey<ReadOnlyMemory<byte>> key;
            ReadOnlySpan<byte> value;
            Assert.IsFalse(internalizer.TryGetInternalizedKey(ReadOnlySpan<byte>.Empty, out key));
            Assert.IsFalse(internalizer.TryGetInternalizedValue(ReadOnlySpan<byte>.Empty, out value, out key));

            key = internalizer.InternalizeValue(ReadOnlySpan<byte>.Empty, out value);
            Assert.AreEqual(0, key.Key);
            Assert.AreEqual(0, value.Length);
            Assert.IsTrue(internalizer.TryGetInternalizedKey(ReadOnlySpan<byte>.Empty, out key));
            Assert.AreEqual(0, key.Key);
            Assert.IsTrue(internalizer.TryGetInternalizedValue(ReadOnlySpan<byte>.Empty, out value, out key));
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
        public void Test_BasicInternalizer_Byte_RedundantOrdinals()
        {
            BasicInternalizer_Byte internalizer = new BasicInternalizer_Byte(new UnitTestInternedKeySource<ReadOnlyMemory<byte>>(5));

            List<byte[]> inputs = new List<byte[]>();
            inputs.Add(Encoding.UTF8.GetBytes("One"));
            inputs.Add(Encoding.UTF8.GetBytes("Two"));
            inputs.Add(Encoding.UTF8.GetBytes("Three"));
            inputs.Add(Encoding.UTF8.GetBytes("Four"));
            inputs.Add(Encoding.UTF8.GetBytes("5"));
            inputs.Add(Encoding.UTF8.GetBytes("Six"));
            inputs.Add(Encoding.UTF8.GetBytes("Seven"));
            inputs.Add(Encoding.UTF8.GetBytes("Eight"));
            inputs.Add(Encoding.UTF8.GetBytes("Niiiiiiiiiiiiiiiiiiiiiiiiiiiiine"));
            inputs.Add(new byte[0]);

            ReadOnlySpan<byte> internalizedValue;
            foreach (byte[] input in inputs)
            {
                var key = internalizer.InternalizeValue(input.AsSpan(), out internalizedValue);
                Assert.IsTrue(internalizedValue.SequenceEqual(input.AsSpan()));
            }

            InternedKey<ReadOnlyMemory<byte>> ordinal;
            ReadOnlySpan<byte> internedValue;
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
            byte[] randField = new byte[100];
            for (int c = 0; c < 1000; c++)
            {
                int length = rand.NextInt(2, randField.Length);
                rand.NextBytes(randField);
                Assert.IsFalse(internalizer.TryGetInternalizedKey(randField.AsSpan(0, length), out ordinal));
            }

            // And test enumeration too
            Assert.AreEqual(inputs.Count, internalizer.Count());
        }

        [TestMethod]
        public void Test_BasicInternalizer_Byte_CrcHashCollisions()
        {
            BasicInternalizer_Byte internalizer = new BasicInternalizer_Byte(new InternedKeySource<ReadOnlyMemory<byte>>());

            List<byte[]> inputsA = new List<byte[]>();
            List<byte[]> inputsB = new List<byte[]>();

            // collide to 2763617405
            inputsA.Add(MemoryMarshal.Cast<char, byte>("rwrwwkpq".AsSpan()).ToArray());
            inputsB.Add(MemoryMarshal.Cast<char, byte>("uzivbqsk".AsSpan()).ToArray());

            // collide to 1254032560
            inputsA.Add(MemoryMarshal.Cast<char, byte>("ybslkdfz".AsSpan()).ToArray());
            inputsB.Add(MemoryMarshal.Cast<char, byte>("bcrkzjtz".AsSpan()).ToArray());

            // collide to 393633157
            inputsA.Add(MemoryMarshal.Cast<char, byte>("aayevrvc".AsSpan()).ToArray());
            inputsB.Add(MemoryMarshal.Cast<char, byte>("iqdnjzjt".AsSpan()).ToArray());

            InternedKey<ReadOnlyMemory<byte>> key;
            ReadOnlySpan<byte> internedValue;

            // Internalize the input A set
            HashSet<InternedKey<ReadOnlyMemory<byte>>> keysA = new HashSet<InternedKey<ReadOnlyMemory<byte>>>();
            HashSet<InternedKey<ReadOnlyMemory<byte>>> keysB = new HashSet<InternedKey<ReadOnlyMemory<byte>>>();
            foreach (byte[] input in inputsA)
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
            foreach (byte[] input in inputsB)
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
        public void Test_BasicInternalizer_Byte_NegativeOrdinals()
        {
            UnitTestInternedKeySource<ReadOnlyMemory<byte>> keySource = new UnitTestInternedKeySource<ReadOnlyMemory<byte>>(-1);
            BasicInternalizer_Byte internalizer = new BasicInternalizer_Byte(keySource);

            InternedKey<ReadOnlyMemory<byte>> key;
            ReadOnlySpan<byte> internedValue;

            try
            {
                key = internalizer.InternalizeValue(MemoryMarshal.Cast<char, byte>("some input".AsSpan()), out internedValue);
                Assert.Fail("Expected an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }

            keySource.NextOrdinal = 0;
            key = internalizer.InternalizeValue(MemoryMarshal.Cast<char, byte>("rwrwwkpq".AsSpan()), out internedValue);
            Assert.AreEqual(0, key.Key);

            keySource.NextOrdinal = -1;
            try
            {
                // this should collide on CRC32 with the previous value to trigger the range check on a separate code path
                key = internalizer.InternalizeValue(MemoryMarshal.Cast<char, byte>("uzivbqsk".AsSpan()), out internedValue);
                Assert.Fail("Expected an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }
    }
}
