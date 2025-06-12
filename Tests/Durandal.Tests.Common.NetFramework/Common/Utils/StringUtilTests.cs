using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Utils
{
    /// <summary>
    /// Maintaining a copy of some string manipulation test cases in .Net Framework so we can test fallback
    /// on some internal code paths that otherwise depend on .Net Core
    /// </summary>
    [TestClass]
    public class StringUtilTests
    {
        [TestMethod]
        public async Task TestStringUtilsConvertStreamIntoStringArgumentValidation()
        {
            using (Stream memoryStream = new MemoryStream())
            using (NonRealTimeStreamWrapper nrtStream = new NonRealTimeStreamWrapper(memoryStream, ownsStream: false))
            {
                try
                {
                    await StringUtils.ConvertStreamIntoString(null, Encoding.UTF8);
                    Assert.Fail("Expected an ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    await StringUtils.ConvertStreamIntoString(memoryStream, null);
                    Assert.Fail("Expected an ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    await StringUtils.ConvertStreamIntoString(null, Encoding.UTF8, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Expected an ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    await StringUtils.ConvertStreamIntoString(nrtStream, null, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Expected an ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    await StringUtils.ConvertStreamIntoString(nrtStream, Encoding.UTF8, CancellationToken.None, null);
                    Assert.Fail("Expected an ArgumentNullException");
                }
                catch (ArgumentNullException) { }
            }
        }

        [TestMethod]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(10000)]
        [DataRow(100000)]
        public async Task TestStringUtilsConvertStreamIntoStringUTF8(int testStringLength)
        {
            Encoding encoding = Encoding.UTF8;
            StringBuilder builder = new StringBuilder(testStringLength + 100);
            while (builder.Length < testStringLength)
            {
                builder.Append("This is a test of the string utilities, to make sure we can convert strings to streams and back again. ");
            }

            string inputString = builder.ToString();
            byte[] encoded = encoding.GetBytes(inputString);
            using (Stream memoryStream = new MemoryStream(encoded, writable: false))
            using (PooledStringBuilder output = await StringUtils.ConvertStreamIntoString(memoryStream, encoding).ConfigureAwait(false))
            {
                Assert.AreEqual(inputString, output.Builder.ToString());
            }
        }

        [TestMethod]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(10000)]
        [DataRow(100000)]
        public async Task TestStringUtilsConvertStreamIntoStringUTF32(int testStringLength)
        {
            Encoding encoding = Encoding.UTF32;
            StringBuilder builder = new StringBuilder(testStringLength + 100);
            while (builder.Length < testStringLength)
            {
                builder.Append("This is a test of the string utilities, to make sure we can convert strings to streams and back again. ");
            }

            string inputString = builder.ToString();
            byte[] encoded = encoding.GetBytes(inputString);
            using (Stream memoryStream = new MemoryStream(encoded, writable: false))
            using (PooledStringBuilder output = await StringUtils.ConvertStreamIntoString(memoryStream, encoding).ConfigureAwait(false))
            {
                Assert.AreEqual(inputString, output.Builder.ToString());
            }
        }

        [TestMethod]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(10000)]
        [DataRow(100000)]
        public async Task TestStringUtilsConvertNRTStreamIntoStringUTF8(int testStringLength)
        {
            Encoding encoding = Encoding.UTF8;
            StringBuilder builder = new StringBuilder(testStringLength + 100);
            while (builder.Length < testStringLength)
            {
                builder.Append("This is a test of the string utilities, to make sure we can convert strings to streams and back again. ");
            }

            string inputString = builder.ToString();
            byte[] encoded = encoding.GetBytes(inputString);
            using (Stream memoryStream = new MemoryStream(encoded, writable: false))
            using (NonRealTimeStreamWrapper nrtStream = new NonRealTimeStreamWrapper(memoryStream, ownsStream: false))
            using (PooledStringBuilder output = await StringUtils.ConvertStreamIntoString(
                nrtStream,
                encoding,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton).ConfigureAwait(false))
            {
                Assert.AreEqual(inputString, output.Builder.ToString());
            }
        }

        [TestMethod]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(10000)]
        [DataRow(100000)]
        public async Task TestStringUtilsConvertNRTStreamIntoStringUTF32(int testStringLength)
        {
            Encoding encoding = Encoding.UTF32;
            StringBuilder builder = new StringBuilder(testStringLength + 100);
            while (builder.Length < testStringLength)
            {
                builder.Append("This is a test of the string utilities, to make sure we can convert strings to streams and back again. ");
            }

            string inputString = builder.ToString();
            byte[] encoded = encoding.GetBytes(inputString);
            using (Stream memoryStream = new MemoryStream(encoded, writable: false))
            using (NonRealTimeStreamWrapper nrtStream = new NonRealTimeStreamWrapper(memoryStream, ownsStream: false))
            using (PooledStringBuilder output = await StringUtils.ConvertStreamIntoString(
                nrtStream,
                encoding,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton).ConfigureAwait(false))
            {
                Assert.AreEqual(inputString, output.Builder.ToString());
            }
        }

        [TestMethod]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(10000)]
        [DataRow(100000)]
        public async Task TestStringStreamRoundTripUTF32(int testStringLength)
        {
            Encoding encoding = Encoding.UTF32;
            StringBuilder builder = new StringBuilder(testStringLength + 100);
            while (builder.Length < testStringLength)
            {
                builder.Append("This is a test of the string utilities, to make sure we can convert strings to streams and back again. ");
            }

            string inputString = builder.ToString();
            using (Stream memoryStream = new StringStream(inputString, encoding))
            using (PooledStringBuilder output = await StringUtils.ConvertStreamIntoString(memoryStream, encoding).ConfigureAwait(false))
            {
                Assert.AreEqual(inputString, output.Builder.ToString());
            }
        }

        [TestMethod]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(10000)]
        [DataRow(100000)]
        public async Task TestStringStreamRoundTripUTF8(int testStringLength)
        {
            Encoding encoding = Encoding.UTF8;
            StringBuilder builder = new StringBuilder(testStringLength + 100);
            while (builder.Length < testStringLength)
            {
                builder.Append("This is a test of the string utilities, to make sure we can convert strings to streams and back again. ");
            }

            string inputString = builder.ToString();
            using (Stream memoryStream = new StringStream(inputString, encoding))
            using (PooledStringBuilder output = await StringUtils.ConvertStreamIntoString(memoryStream, encoding).ConfigureAwait(false))
            {
                Assert.AreEqual(inputString, output.Builder.ToString());
            }
        }

        /// <summary>
        /// Tests that date time formatters can correctly format a typical DateTime with zero offset
        /// </summary>
        [TestMethod]
        public void TestStringUtilsDateTimeFormatterBasic_UTC()
        {
            DateTimeOffset dateTime = new DateTimeOffset(634759936331234560, TimeSpan.Zero);
            StringBuilder stringBuilder = new StringBuilder();
            StringUtils.FormatDateTime_ISO8601WithMicroseconds(dateTime, stringBuilder);
            Assert.AreEqual("2012-06-22T20:27:13.123456", stringBuilder.ToString());
            stringBuilder.Clear();
            StringUtils.FormatDateTime_ISO8601WithMilliseconds(dateTime, stringBuilder);
            Assert.AreEqual("2012-06-22T20:27:13.123", stringBuilder.ToString());
            stringBuilder.Clear();
            StringUtils.FormatDateTime_ISO8601(dateTime, stringBuilder);
            Assert.AreEqual("2012-06-22T20:27:13", stringBuilder.ToString());
        }

        /// <summary>
        /// Tests that date time formatters can correctly format a typical DateTime with kind Local.
        /// </summary>
        [TestMethod]
        public void TestStringUtilsDateTimeFormatterBasic_Local()
        {
            DateTimeOffset dateTime = new DateTimeOffset(634759936331234560, TimeSpan.FromHours(4));
            StringBuilder stringBuilder = new StringBuilder();
            StringUtils.FormatDateTime_ISO8601WithMicroseconds(dateTime, stringBuilder);
            Assert.AreEqual("2012-06-22T20:27:13.123456", stringBuilder.ToString());
            stringBuilder.Clear();
            StringUtils.FormatDateTime_ISO8601WithMilliseconds(dateTime, stringBuilder);
            Assert.AreEqual("2012-06-22T20:27:13.123", stringBuilder.ToString());
            stringBuilder.Clear();
            StringUtils.FormatDateTime_ISO8601(dateTime, stringBuilder);
            Assert.AreEqual("2012-06-22T20:27:13", stringBuilder.ToString());
        }

        /// <summary>
        /// Tests that date time formatters can correctly format various edge cases.
        /// </summary>
        [DataRow(2012, 06, 22, 20, 27, 13, 123456)] // average use case
        [DataRow(0001, 01, 01, 00, 00, 00, 000000)] // test various levels of padding
        [DataRow(0001, 01, 01, 01, 01, 01, 000001)]
        [DataRow(0012, 12, 12, 12, 12, 12, 000012)]
        [DataRow(0123, 12, 12, 12, 12, 12, 000123)]
        [DataRow(1234, 12, 12, 12, 12, 12, 001234)]
        [DataRow(1234, 12, 12, 12, 12, 12, 012345)]
        [DataRow(1234, 12, 12, 12, 12, 12, 123456)]
        [DataRow(0011, 01, 01, 01, 01, 01, 000111)]
        [DataRow(1100, 10, 10, 10, 10, 10, 111000)]
        [DataRow(0110, 10, 01, 10, 01, 10, 001100)]
        [DataRow(2024, 02, 29, 12, 15, 13, 345678)] // leap year
        [DataRow(2017, 01, 01, 00, 00, 00, 000000)] // leap second (doesn't actually affect anything in C# runtime currently)
        [TestMethod]
        public void TestStringUtilsDateTimeFormatter(int year, int month, int second, int hour, int minute, int day, long microsecond)
        {
            DateTimeOffset dateTime = new DateTimeOffset(year, month, second, hour, minute, day, TimeSpan.Zero);
            dateTime = new DateTime(dateTime.Ticks + (microsecond * 10), DateTimeKind.Unspecified);
            StringBuilder stringBuilder = new StringBuilder();
            StringUtils.FormatDateTime_ISO8601WithMicroseconds(dateTime, stringBuilder);
            Assert.AreEqual(dateTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff"), stringBuilder.ToString());
            stringBuilder.Clear();
            StringUtils.FormatDateTime_ISO8601WithMilliseconds(dateTime, stringBuilder);
            Assert.AreEqual(dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff"), stringBuilder.ToString());
            stringBuilder.Clear();
            StringUtils.FormatDateTime_ISO8601(dateTime, stringBuilder);
            Assert.AreEqual(dateTime.ToString("yyyy-MM-ddTHH:mm:ss"), stringBuilder.ToString());
            stringBuilder.Clear();
        }

        /// <summary>
        /// Tests 100,000 pseudorandom iterations of date time formatting to make totally sure we have parity
        /// </summary>
        [TestMethod]
        public void TestStringUtilsDateTimeFormatter_Fuzz()
        {
            Random rand = new Random(5532581);
            long maxTicks = DateTime.MaxValue.Ticks;
            long minTicks = DateTime.MinValue.Ticks;
            long tickRange = maxTicks - minTicks;
            StringBuilder stringBuilder = new StringBuilder();

            for (int iter = 0; iter < 100_000; iter++)
            {
                // Generate a random 64-bit tick value within the range of valid datetimes
                // Work around lack of Random.NextInt64() which isn't available yet
                long ticks = Math.Abs((rand.Next() << 32) | rand.Next());
                ticks = (ticks % tickRange) + minTicks;
                DateTimeOffset dateTime = new DateTimeOffset(ticks, TimeSpan.Zero);

                StringUtils.FormatDateTime_ISO8601WithMicroseconds(dateTime, stringBuilder);
                Assert.AreEqual(dateTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff"), stringBuilder.ToString());
                stringBuilder.Clear();
                StringUtils.FormatDateTime_ISO8601WithMilliseconds(dateTime, stringBuilder);
                Assert.AreEqual(dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff"), stringBuilder.ToString());
                stringBuilder.Clear();
                StringUtils.FormatDateTime_ISO8601(dateTime, stringBuilder);
                Assert.AreEqual(dateTime.ToString("yyyy-MM-ddTHH:mm:ss"), stringBuilder.ToString());
                stringBuilder.Clear();
            }
        }

        [TestMethod]
        public void TestStringUtilsSubstringEqualsInvalidArgs()
        {
            try
            {
                StringUtils.SubstringEquals(null, "chunked", 0, StringComparison.Ordinal);
                Assert.Fail("Should have thrown an ArgumentNullException");
            }
            catch (ArgumentNullException) { }

            try
            {
                StringUtils.SubstringEquals("Transfer-Encoding", null, 0, StringComparison.Ordinal);
                Assert.Fail("Should have thrown an ArgumentNullException");
            }
            catch (ArgumentNullException) { }

            try
            {
                StringUtils.SubstringEquals("term", "largesearchfield", -1, StringComparison.Ordinal);
                Assert.Fail("Should have thrown an IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            try
            {
                StringUtils.SubstringEquals("bigsearchterm", "small", 0, StringComparison.Ordinal);
                Assert.Fail("Should have thrown an IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            try
            {
                StringUtils.SubstringEquals("small", "abigfieldbutnottoobig", 18, StringComparison.Ordinal);
                Assert.Fail("Should have thrown an IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }
        }

        [TestMethod]
        public void TestStringUtilsSubstringEqualsBasic()
        {
            Assert.IsTrue(StringUtils.SubstringEquals("the", "This is the test", 8, StringComparison.Ordinal));
            Assert.IsFalse(StringUtils.SubstringEquals("the", "This is the test", 0, StringComparison.Ordinal));
            Assert.IsTrue(StringUtils.SubstringEquals("This", "This is the test", 0, StringComparison.Ordinal));
            Assert.IsTrue(StringUtils.SubstringEquals("chunked", "Transfer-Encoding: chunked", 19, StringComparison.Ordinal));
        }

        [TestMethod]
        public void TestStringUtilsSubstringEqualsOrdinalComparison()
        {
            Assert.IsTrue(StringUtils.SubstringEquals("the", "This is the test", 8, StringComparison.Ordinal));
            Assert.IsFalse(StringUtils.SubstringEquals("the", "This is the test", 0, StringComparison.Ordinal));
            Assert.IsTrue(StringUtils.SubstringEquals("This", "This is the test", 0, StringComparison.Ordinal));
            Assert.IsTrue(StringUtils.SubstringEquals("chunked", "Transfer-Encoding: chunked", 19, StringComparison.Ordinal));
            Assert.IsFalse(StringUtils.SubstringEquals("The", "This is the test", 8, StringComparison.Ordinal));
            Assert.IsFalse(StringUtils.SubstringEquals("The", "This is the test", 0, StringComparison.Ordinal));
            Assert.IsFalse(StringUtils.SubstringEquals("this", "This is the test", 0, StringComparison.Ordinal));
            Assert.IsFalse(StringUtils.SubstringEquals("Chunked", "Transfer-Encoding: chunked", 19, StringComparison.Ordinal));
        }

        [TestMethod]
        public void TestStringUtilsSubstringEqualsCaseInsensitiveComparison()
        {
            Assert.IsTrue(StringUtils.SubstringEquals("the", "This is the test", 8, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(StringUtils.SubstringEquals("the", "This is the test", 0, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(StringUtils.SubstringEquals("This", "This is the test", 0, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(StringUtils.SubstringEquals("chunked", "Transfer-Encoding: chunked", 19, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(StringUtils.SubstringEquals("The", "This is the test", 8, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(StringUtils.SubstringEquals("The", "This is the test", 0, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(StringUtils.SubstringEquals("this", "This is the test", 0, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(StringUtils.SubstringEquals("Chunked", "Transfer-Encoding: chunked", 19, StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void TestStringUtilsSubstringEqualsUnicode()
        {
            Assert.IsTrue(StringUtils.SubstringEquals("για", "Χάρηκα για τη γνωριμία", 7, StringComparison.Ordinal));
            Assert.IsTrue(StringUtils.SubstringEquals("για", "Χάρηκα για τη γνωριμία", 7, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(StringUtils.SubstringEquals("για", "Χάρηκα για τη γνωριμία", 8, StringComparison.Ordinal));
            Assert.IsTrue(StringUtils.SubstringEquals("你", "見到你好开心", 2, StringComparison.Ordinal));
            Assert.IsTrue(StringUtils.SubstringEquals("你", "見到你好开心", 2, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(StringUtils.SubstringEquals("你", "見到你好开心", 3, StringComparison.Ordinal));
            Assert.IsTrue(StringUtils.SubstringEquals("?🐢🐢?", "turtle ?🐢🐢?", 7, StringComparison.Ordinal));
            Assert.IsTrue(StringUtils.SubstringEquals("?🐢🐢?", "turtle ?🐢🐢?", 7, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(StringUtils.SubstringEquals("?🐢🐢?", "turtle ?🐢🐢?", 6, StringComparison.Ordinal));
        }

        [TestMethod]
        [DataRow(10)]
        [DataRow(99)]
        [DataRow(999)]
        [DataRow(9999)]
        [DataRow(99999)]
        public void TestStringUtilsReplaceNewlinesWithSpace(int bufferLength)
        {
            IRandom rand = new FastRandom(bufferLength);
            char[] buf = new char[bufferLength];

            for (int iter = 0; iter < 1000; iter++)
            {
                for (int c = 0; c < bufferLength; c++)
                {
                    buf[c] = (char)('0' + rand.NextInt(0, 9));
                }

                int numNewlines = rand.NextInt(0, bufferLength);
                for (int c = 0; c < numNewlines; c++)
                {
                    buf[rand.NextInt(0, bufferLength)] = rand.NextBool() ? '\r' : '\n';
                }

                int startIdx = rand.NextInt(0, bufferLength / 2);
                int endIdx = rand.NextInt(startIdx + 1, bufferLength);
                int length = endIdx - startIdx;
                char[] expected = new char[length];
                ArrayExtensions.MemCopy(buf, startIdx, expected, 0, length);
                for (int idx = 0; idx < length; idx++)
                {
                    if (expected[idx] == '\r' || expected[idx] == '\n')
                    {
                        expected[idx] = ' ';
                    }
                }

                StringUtils.ReplaceNewlinesWithSpace(buf, startIdx, endIdx - startIdx);
                char[] actual = new char[length];
                ArrayExtensions.MemCopy(buf, startIdx, actual, 0, length);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expected, 0, actual, 0, length), "Strings were not equal");
            }
        }

        [TestMethod]
        [DataRow(10)]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(10000)]
        [DataRow(50000)]
        public void TestStringUtilsIndexOfInStringBuilder(int stringLength)
        {
            IRandom rand = new FastRandom(stringLength);
            StringBuilder stringBuilder = new StringBuilder(stringLength);
            char[] buf = new char[stringLength];
            char[] charsToSearch = new char[] { '\t', '\r', '\n' };

            // Test base case
            buf.AsSpan().Fill('0');
            stringBuilder.Clear();
            stringBuilder.Append(buf, 0, stringLength);
            for (int iter = 0; iter < 10; iter++)
            {
                Assert.AreEqual(-1, StringUtils.IndexOfAnyInStringBuilder(stringBuilder, rand.NextInt(0, stringLength), charsToSearch.AsSpan()));
            }

            // Test the case where startIdx is one after the special char
            buf[stringLength - 2] = charsToSearch[0];
            stringBuilder.Clear();
            stringBuilder.Append(buf, 0, stringLength);
            Assert.AreEqual(-1, StringUtils.IndexOfAnyInStringBuilder(stringBuilder, stringLength - 1, charsToSearch.AsSpan()));

            // And where it's exactly equal
            Assert.AreEqual(stringLength - 2, StringUtils.IndexOfAnyInStringBuilder(stringBuilder, stringLength - 2, charsToSearch.AsSpan()));

            // Now exhaustively test that the exact correct index is returned for any char position
            for (int expectedIdx = 0; expectedIdx < stringLength; expectedIdx++)
            {
                buf.AsSpan().Fill('0');
                buf[expectedIdx] = charsToSearch[rand.NextInt(0, charsToSearch.Length)]; // Put one random token char at the specified index

                // Start at a random place before the special char
                int startIdx = rand.NextInt(0, expectedIdx);
                string parityString = new string(buf, 0, stringLength);
                int expectedValue = parityString.IndexOfAny(charsToSearch, startIdx); // The stringbuilder implementation should behave exactly as the string impl
                stringBuilder.Clear();
                stringBuilder.Append(parityString);
                int actualValue = StringUtils.IndexOfAnyInStringBuilder(stringBuilder, startIdx, charsToSearch.AsSpan());
                Assert.AreEqual(actualValue, expectedValue);
            }
        }

        [TestMethod]
        public void TestStringUtilsIndexOfInStringBuilder_HugeChunkIteration()
        {
            const int stringLength = 10_000_000;
            const int tokenIdx = stringLength - 10;
            StringBuilder stringBuilder = new StringBuilder();

            for (int cap = 0; cap < stringLength; cap += 10000)
            {
                // intentionally fragmenting the builder's internal memory so there's lots of chunks to iterate in the search
                stringBuilder.EnsureCapacity(cap);
            }

            stringBuilder.Append('0', tokenIdx);
            stringBuilder.Append('\t');
            stringBuilder.Append('0', stringLength - stringBuilder.Length);
            Assert.AreEqual(tokenIdx, StringUtils.IndexOfAnyInStringBuilder(stringBuilder, 0, new char[] { '\t', '\r', '\n' }.AsSpan()));
        }

        [TestMethod]
        public void TestStringUtilsIndexOfInStringBuilder_InvalidInputs()
        {
            char[] charsToSearch = new char[] { '\t', '\r', '\n' };
            try
            {
                StringUtils.IndexOfAnyInStringBuilder(null, 0, charsToSearch.AsSpan());
                Assert.Fail("Should have thrown an ArgumentNullException");
            }
            catch (ArgumentNullException) { }

            StringBuilder sb = new StringBuilder();
            sb.Append("test");

            try
            {
                StringUtils.IndexOfAnyInStringBuilder(sb, -1, charsToSearch.AsSpan());
                Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                StringUtils.IndexOfAnyInStringBuilder(sb, 5, charsToSearch.AsSpan());
                Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }

            Assert.AreEqual(-1, StringUtils.IndexOfAnyInStringBuilder(sb, 4, charsToSearch.AsSpan()));
            Assert.AreEqual(-1, StringUtils.IndexOfAnyInStringBuilder(sb, 0, new char[0].AsSpan()));

            sb.Clear();
            Assert.AreEqual(-1, StringUtils.IndexOfAnyInStringBuilder(sb, 0, charsToSearch.AsSpan()));
        }

        [TestMethod]
        public void TestStringBuildersEqual_NullInputs()
        {
            StringBuilder builder = new StringBuilder();
            Assert.IsTrue(StringUtils.StringBuildersEqual(null, null, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(StringUtils.StringBuildersEqual(builder, null, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(StringUtils.StringBuildersEqual(null, builder, StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void TestStringBuildersEqual_SameObjectRef()
        {
            StringBuilder builder = new StringBuilder();
            Stopwatch timer = new Stopwatch();
            const int ITERATIONS = 100;
            StatisticalSet stats = new StatisticalSet(ITERATIONS);
            Assert.IsTrue(StringUtils.StringBuildersEqual(builder, builder, StringComparison.OrdinalIgnoreCase));

            // Get baseline performance for comparing empty builders
            for (int iter = 0; iter < ITERATIONS; iter++)
            {
                timer.Restart();
                StringUtils.StringBuildersEqual(builder, builder, StringComparison.OrdinalIgnoreCase);
                timer.Stop();
                stats.Add(timer.ElapsedTicks);
            }

            double expectedTicks = stats.Mean;

            // Make the builder huge now
            builder.Append('a', 500000);
            Assert.IsTrue(StringUtils.StringBuildersEqual(builder, builder, StringComparison.OrdinalIgnoreCase));

            // Comparison should still be a trivial operation
            stats.Clear();
            for (int iter = 0; iter < ITERATIONS; iter++)
            {
                timer.Restart();
                StringUtils.StringBuildersEqual(builder, builder, StringComparison.OrdinalIgnoreCase);
                timer.Stop();
                stats.Add(timer.ElapsedTicks);
            }

            Assert.IsTrue(stats.Mean < expectedTicks * 4, $"Comparing a string builder with itself should be a trivial operation. Expected near {expectedTicks} ticks, got {stats.Mean} ticks");
        }

        [TestMethod]
        public void TestStringBuildersEqual_DifferentLengths()
        {
            StringBuilder builderA = new StringBuilder();
            StringBuilder builderB = new StringBuilder();
            Assert.IsTrue(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.OrdinalIgnoreCase));
            builderB.Append("A");
            Assert.IsFalse(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.OrdinalIgnoreCase));
            builderA.Append("A");
            builderB.Append("A");
            Assert.IsFalse(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.OrdinalIgnoreCase));
            builderA.Append("A");
            Assert.IsTrue(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void TestStringBuildersEqual_OrdinalCompare()
        {
            StringBuilder builderA = new StringBuilder();
            StringBuilder builderB = new StringBuilder();
            builderA.Append("Test string");
            builderB.Append("Test string");
            Assert.IsTrue(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.Ordinal));
            builderA.Append(" yes");
            builderB.Append(" yes");
            Assert.IsTrue(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.Ordinal));
            builderA.Append(" NO");
            builderB.Append(" no");
            Assert.IsFalse(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.Ordinal));
        }

        [TestMethod]
        public void TestStringBuildersEqual_OrdinalIgnoreCaseCompare()
        {
            StringBuilder builderA = new StringBuilder();
            StringBuilder builderB = new StringBuilder();
            builderA.Append("Test string");
            builderB.Append("Test string");
            Assert.IsTrue(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.OrdinalIgnoreCase));
            builderA.Append(" yes");
            builderB.Append(" yes");
            Assert.IsTrue(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.OrdinalIgnoreCase));
            builderA.Append(" NO");
            builderB.Append(" no");
            Assert.IsTrue(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.OrdinalIgnoreCase));
            builderA.Append(" blep");
            builderB.Append(" mooo");
            Assert.IsFalse(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        [DataRow(4)]
        [DataRow(32)]
        [DataRow(128)]
        [DataRow(1131)]
        [DataRow(65537)]
        [DataRow(512319)]
        public void TestStringBuildersEqual_OrdinalCompareVariousLengths(int length)
        {
            StringBuilder builderA = new StringBuilder();
            StringBuilder builderB = new StringBuilder();
            builderA.Append('x', length);
            builderB.Append('x', length);
            Assert.IsTrue(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.Ordinal));

            builderA.Append('a');
            builderB.Append('z');
            Assert.IsFalse(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.Ordinal));
        }

        [TestMethod]
        [DataRow(4)]
        [DataRow(32)]
        [DataRow(128)]
        [DataRow(1131)]
        [DataRow(65537)]
        [DataRow(512319)]
        public void TestStringBuildersEqual_OrdinalIgnoreCaseCompareVariousLengths(int length)
        {
            StringBuilder builderA = new StringBuilder();
            StringBuilder builderB = new StringBuilder();
            builderA.Append('x', length);
            builderB.Append('X', length);
            Assert.IsTrue(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.OrdinalIgnoreCase));

            builderA.Append('a');
            builderB.Append('z');
            Assert.IsFalse(StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.OrdinalIgnoreCase));
        }

        // This test only applies to legacy .Net code path
        [TestMethod]
        public void TestStringBuildersEqual_InvalidComparisons()
        {
            StringBuilder builderA = new StringBuilder();
            StringBuilder builderB = new StringBuilder();
            builderA.Append('x', 4);
            builderB.Append('x', 4);
            try
            {
                StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.CurrentCulture);
                Assert.Fail("Should have thrown a NotImplementedException");
            }
            catch (NotImplementedException) { }

            builderA.Append('x', 5000);
            builderB.Append('x', 5000);
            try
            {
                StringUtils.StringBuildersEqual(builderA, builderB, StringComparison.CurrentCulture);
                Assert.Fail("Should have thrown a NotImplementedException");
            }
            catch (NotImplementedException) { }
        }

        [TestMethod]
        [DataRow(StringComparison.Ordinal)]
        [DataRow(StringComparison.OrdinalIgnoreCase)]
        public void TestStringBuildersEqual_ComplexInput(StringComparison comparison)
        {
            StringBuilder builderA = new StringBuilder();
            StringBuilder builderB = new StringBuilder();
            IRandom rand = new FastRandom(625891);
            const int targetCapacity = 100000;
            StringBuilder inputBuffer = new StringBuilder(targetCapacity);
            while (inputBuffer.Length < targetCapacity)
            {
                inputBuffer.Append((char)(rand.NextInt(0, 26) + (rand.NextBool() ? 'a' : 'A')));
            }

            // Churn the builders' internal buffers so the chunks are less predictable
            for (int c = 0; c < 100; c++)
            {
                builderA.EnsureCapacity(rand.NextInt(1, targetCapacity));
                builderB.EnsureCapacity(rand.NextInt(1, targetCapacity));
                builderA.Append('0', rand.NextInt(1, 10000));
                builderB.Append('0', rand.NextInt(1, 10000));
                if (rand.NextBool())
                {
                    builderA.Clear();
                }
                if (rand.NextBool())
                {
                    builderB.Clear();
                }
            }

            builderA.Clear();
            builderB.Clear();
            builderA.Append(inputBuffer);
            if (comparison == StringComparison.OrdinalIgnoreCase)
            {
                builderB.Append(inputBuffer.ToString().ToLowerInvariant());
            }
            else
            {
                builderB.Append(inputBuffer);
            }

            Assert.IsTrue(StringUtils.StringBuildersEqual(builderA, builderB, comparison));
        }
    }
}
