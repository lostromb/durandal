using Durandal.Common.IO;
using Durandal.Common.MathExt;
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
using Durandal.Common.Test;
using Durandal.Common.Collections;

namespace Durandal.Tests.Common.IO
{
    [TestClass]
    public class StringStreamTests
    {
        [TestMethod]
        public void TestStringStreamEmptyString()
        {
            string input = string.Empty;
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            using (MemoryStream output = new MemoryStream())
            using (StringStream stringStream = new StringStream(input, encoding))
            {
                stringStream.CopyTo(output);
                Assert.AreEqual(0, output.Length);
            }
        }

        [TestMethod]
        public void TestStringStreamInvalidNullString()
        {
            try
            {
                using (StringStream stringStream = new StringStream(null, Encoding.ASCII))
                {
                    Assert.Fail("Expected an ArgumentNullException");
                }
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestStringStreamInvalidNullEncoding()
        {
            try
            {
                using (StringStream stringStream = new StringStream(string.Empty, null))
                {
                    Assert.Fail("Expected an ArgumentNullException");
                }
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestStringStreamInvalidBounds()
        {
            try
            {
                using (StringStream stringStream = new StringStream("0123456789", 11, 5, Encoding.ASCII))
                {
                    Assert.Fail("Expected an ArgumentOutOfRangeException");
                }
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                using (StringStream stringStream = new StringStream("0123456789", 2, 9, Encoding.ASCII))
                {
                    Assert.Fail("Expected an ArgumentOutOfRangeException");
                }
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                using (StringStream stringStream = new StringStream("0123456789", -1, 5, Encoding.ASCII))
                {
                    Assert.Fail("Expected an ArgumentOutOfRangeException");
                }
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void TestStringStreamBasicUTF8()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            using (MemoryStream output = new MemoryStream())
            using (StringStream stringStream = new StringStream(input, encoding))
            {
                stringStream.CopyTo(output);
                byte[] actualEncodedData = output.ToArray();
                byte[] expectedEncodedData = encoding.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
            }
        }

        [TestMethod]
        public void TestStringStreamBasicUTF32()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = Encoding.UTF32;
            using (MemoryStream output = new MemoryStream())
            using (StringStream stringStream = new StringStream(input, encoding))
            {
                stringStream.CopyTo(output);
                byte[] actualEncodedData = output.ToArray();
                byte[] expectedEncodedData = encoding.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
            }
        }

        [TestMethod]
        public void TestStringStreamBasicUnicode()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = Encoding.Unicode;
            using (MemoryStream output = new MemoryStream())
            using (StringStream stringStream = new StringStream(input, encoding))
            {
                stringStream.CopyTo(output);
                byte[] actualEncodedData = output.ToArray();
                byte[] expectedEncodedData = encoding.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
            }
        }

        [TestMethod]
        public void TestStringStreamBasicASCII()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = Encoding.ASCII;
            using (MemoryStream output = new MemoryStream())
            using (StringStream stringStream = new StringStream(input, encoding))
            {
                stringStream.CopyTo(output);
                byte[] actualEncodedData = output.ToArray();
                byte[] expectedEncodedData = encoding.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
            }
        }

        [TestMethod]
        public void TestStringStreamBasicUTF8WithOffset()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            IRandom rand = new FastRandom(74512);
            for (int test = 0; test < 100; test++)
            {
                int start = rand.NextInt(1, 100);
                int count = rand.NextInt(1, input.Length - start);
                using (MemoryStream output = new MemoryStream())
                using (StringStream stringStream = new StringStream(input, start, count, encoding))
                {
                    stringStream.CopyTo(output);
                    byte[] actualEncodedData = output.ToArray();
                    byte[] expectedEncodedData = encoding.GetBytes(input.ToCharArray(), start, count);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                }
            }
        }

        [TestMethod]
        public void TestStringStreamBasicASCIIWithOffset()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = Encoding.ASCII;
            IRandom rand = new FastRandom(12673);
            for (int test = 0; test < 100; test++)
            {
                int start = rand.NextInt(1, 100);
                int count = rand.NextInt(1, input.Length - start);
                using (MemoryStream output = new MemoryStream())
                using (StringStream stringStream = new StringStream(input, start, count, encoding))
                {
                    stringStream.CopyTo(output);
                    byte[] actualEncodedData = output.ToArray();
                    byte[] expectedEncodedData = encoding.GetBytes(input.ToCharArray(), start, count);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                }
            }
        }

        [TestMethod]
        public void TestStringStreamComplexUTF8()
        {
            string input = BuildLongString("🧝‍♀️ 💇‍♂️ 🕴 🧖‍♀️ 🧜🤶🤴👨‍🔧🤦");
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            using (MemoryStream output = new MemoryStream())
            using (StringStream stringStream = new StringStream(input, encoding))
            {
                stringStream.CopyTo(output);
                byte[] actualEncodedData = output.ToArray();
                byte[] expectedEncodedData = encoding.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
            }
        }

        // Specifically chosen because the turtle is a single multi-code-point glyph
        [TestMethod]
        public void TestStringStreamTurtles()
        {
            string input = BuildLongString("🐢");
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            using (MemoryStream output = new MemoryStream())
            using (StringStream stringStream = new StringStream(input, encoding))
            {
                stringStream.CopyTo(output);
                byte[] actualEncodedData = output.ToArray();
                byte[] expectedEncodedData = encoding.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
            }
        }

        [TestMethod]
        public void TestStringStreamGetLength()
        {
            string input = "🐢";
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            using (MemoryStream output = new MemoryStream())
            using (StringStream stringStream = new StringStream(input, encoding))
            {
                stringStream.CopyTo(output);
                byte[] actualEncodedData = output.ToArray();
                byte[] expectedEncodedData = encoding.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                Assert.AreEqual(output.Length, stringStream.Length);
                Assert.AreEqual(output.Position, stringStream.Position);
            }
        }

        [TestMethod]
        public void TestStringStreamReadByte()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            byte[] expectedEncodedData = encoding.GetBytes(input);
            byte[] actualEncodedData = new byte[expectedEncodedData.Length];

            using (StringStream stringStream = new StringStream(input, encoding))
            {
                int b = 0;
                int outIdx = 0;
                while (true)
                {
                    b = stringStream.ReadByte();
                    if (b < 0)
                    {
                        Assert.AreEqual(outIdx, expectedEncodedData.Length);
                        break;
                    }

                    actualEncodedData[outIdx++] = (byte)b;
                }

                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
            }
        }

        private static string BuildLongString(string seed)
        {
            StringBuilder builder = new StringBuilder();

            while (builder.Length < 100000)
            {
                builder.Append(seed);
            }

            return builder.ToString();
        }
    }
}
