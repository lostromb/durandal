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
    public class PooledStringBuilderStreamTests
    {
        [TestMethod]
        public void TestPooledStringBuilderStreamEmptyString()
        {
            PooledStringBuilder pooledSb = StringBuilderPool.Rent();
            try
            {
                Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
                using (MemoryStream output = new MemoryStream())
                using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, encoding))
                {
                    stringStream.CopyTo(output);
                    Assert.AreEqual(0, output.Length);
                }

                pooledSb = null;
            }
            finally
            {
                pooledSb?.Dispose();
            }
        }

        [TestMethod]
        public void TestPooledStringBuilderStreamInvalidNullString()
        {
            try
            {
                using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(null, Encoding.ASCII))
                {
                    Assert.Fail("Expected an ArgumentNullException");
                }
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestPooledStringBuilderStreamInvalidNullEncoding()
        {
            PooledStringBuilder pooledSb = StringBuilderPool.Rent();
            try
            {
                using (MemoryStream output = new MemoryStream())
                using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, null))
                {
                    Assert.Fail("Expected an ArgumentNullException");
                }

                pooledSb = null;
            }
            catch (ArgumentNullException) { }
            finally
            {
                pooledSb?.Dispose();
            }
        }

        [TestMethod]
        public void TestPooledStringBuilderStreamInvalidBounds()
        {
            PooledStringBuilder pooledSb = StringBuilderPool.Rent();
            try
            {
                try
                {
                    pooledSb.Builder.Append("0123456789");
                    using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, 11, 5, Encoding.ASCII))
                    {
                        Assert.Fail("Expected an ArgumentOutOfRangeException");
                    }
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, 11, 5, Encoding.ASCII))
                    {
                        Assert.Fail("Expected an ArgumentOutOfRangeException");
                    }
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, 11, 5, Encoding.ASCII))
                    {
                        Assert.Fail("Expected an ArgumentOutOfRangeException");
                    }
                }
                catch (ArgumentOutOfRangeException) { }
            }
            finally
            {
                pooledSb?.Dispose();
            }
        }

        [TestMethod]
        public void TestPooledStringBuilderStreamBasicUTF8()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            PooledStringBuilder pooledSb = StringBuilderPool.Rent();
            try
            {
                pooledSb.Builder.Append(input);
                using (MemoryStream output = new MemoryStream())
                using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, encoding))
                {
                    stringStream.CopyTo(output);
                    byte[] actualEncodedData = output.ToArray();
                    byte[] expectedEncodedData = encoding.GetBytes(input);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                }

                pooledSb = null;
            }
            catch (ArgumentNullException) { }
            finally
            {
                pooledSb?.Dispose();
            }
        }

        [TestMethod]
        public void TestPooledStringBuilderStreamBasicUTF32()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = Encoding.UTF32;
            PooledStringBuilder pooledSb = StringBuilderPool.Rent();
            try
            {
                pooledSb.Builder.Append(input);
                using (MemoryStream output = new MemoryStream())
                using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, encoding))
                {
                    stringStream.CopyTo(output);
                    byte[] actualEncodedData = output.ToArray();
                    byte[] expectedEncodedData = encoding.GetBytes(input);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                }

                pooledSb = null;
            }
            catch (ArgumentNullException) { }
            finally
            {
                pooledSb?.Dispose();
            }
        }

        [TestMethod]
        public void TestPooledStringBuilderStreamBasicUnicode()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = Encoding.Unicode;
            PooledStringBuilder pooledSb = StringBuilderPool.Rent();
            try
            {
                pooledSb.Builder.Append(input);
                using (MemoryStream output = new MemoryStream())
                using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, encoding))
                {
                    stringStream.CopyTo(output);
                    byte[] actualEncodedData = output.ToArray();
                    byte[] expectedEncodedData = encoding.GetBytes(input);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                }

                pooledSb = null;
            }
            catch (ArgumentNullException) { }
            finally
            {
                pooledSb?.Dispose();
            }
        }

        [TestMethod]
        public void TestPooledStringBuilderStreamBasicASCII()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = Encoding.ASCII;
            
            PooledStringBuilder pooledSb = StringBuilderPool.Rent();
            try
            {
                pooledSb.Builder.Append(input);
                using (MemoryStream output = new MemoryStream())
                using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, encoding))
                {
                    stringStream.CopyTo(output);
                    byte[] actualEncodedData = output.ToArray();
                    byte[] expectedEncodedData = encoding.GetBytes(input);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                }
                pooledSb = null;
            }
            catch (ArgumentNullException) { }
            finally
            {
                pooledSb?.Dispose();
            }
        }

        [TestMethod]
        public void TestPooledStringBuilderStreamBasicUTF8WithOffset()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            IRandom rand = new FastRandom(62234);
            for (int test = 0; test < 100; test++)
            {
                PooledStringBuilder pooledSb = StringBuilderPool.Rent();
                try
                {
                    int start = rand.NextInt(1, 100);
                    int count = rand.NextInt(1, input.Length - start);
                    pooledSb.Builder.Append(input);
                    using (MemoryStream output = new MemoryStream())
                    using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, start, count, encoding))
                    {
                        stringStream.CopyTo(output);
                        byte[] actualEncodedData = output.ToArray();
                        byte[] expectedEncodedData = encoding.GetBytes(input.ToCharArray(), start, count);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                    }

                    pooledSb = null;
                }
                catch (ArgumentNullException) { }
                finally
                {
                    pooledSb?.Dispose();
                }
            }
        }

        [TestMethod]
        public void TestPooledStringBuilderStreamBasicASCIIWithOffset()
        {
            string input = BuildLongString("This is a test of the basic string stream.");
            Encoding encoding = Encoding.ASCII;
            IRandom rand = new FastRandom(65201);
            for (int test = 0; test < 100; test++)
            {
                PooledStringBuilder pooledSb = StringBuilderPool.Rent();
                try
                {
                    int start = rand.NextInt(1, 100);
                    int count = rand.NextInt(1, input.Length - start);
                    pooledSb.Builder.Append(input);
                    using (MemoryStream output = new MemoryStream())
                    using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, start, count, encoding))
                    {
                        stringStream.CopyTo(output);
                        byte[] actualEncodedData = output.ToArray();
                        byte[] expectedEncodedData = encoding.GetBytes(input.ToCharArray(), start, count);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                    }

                    pooledSb = null;
                }
                catch (ArgumentNullException) { }
                finally
                {
                    pooledSb?.Dispose();
                }
            }
        }

        [TestMethod]
        public void TestPooledStringBuilderStreamComplexUTF8()
        {
            string input = BuildLongString("🧝‍♀️ 💇‍♂️ 🕴 🧖‍♀️ 🧜🤶🤴👨‍🔧🤦");
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            PooledStringBuilder pooledSb = StringBuilderPool.Rent();
            try
            {
                pooledSb.Builder.Append(input);
                using (MemoryStream output = new MemoryStream())
                using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, encoding))
                {
                    stringStream.CopyTo(output);
                    byte[] actualEncodedData = output.ToArray();
                    byte[] expectedEncodedData = encoding.GetBytes(input);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                }

                pooledSb = null;
            }
            catch (ArgumentNullException) { }
            finally
            {
                pooledSb?.Dispose();
            }
        }

        // Specifically chosen because the turtle is a single multi-code-point glyph
        [TestMethod]
        public void TestPooledStringBuilderStreamTurtles()
        {
            string input = BuildLongString("🐢");
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            PooledStringBuilder pooledSb = StringBuilderPool.Rent();
            try
            {
                pooledSb.Builder.Append(input);
                using (MemoryStream output = new MemoryStream())
                using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, encoding))
                {
                    stringStream.CopyTo(output);
                    byte[] actualEncodedData = output.ToArray();
                    byte[] expectedEncodedData = encoding.GetBytes(input);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                }

                pooledSb = null;
            }
            catch (ArgumentNullException) { }
            finally
            {
                pooledSb?.Dispose();
            }
        }

        [TestMethod]
        public void TestPooledStringBuilderStreamGetLength()
        {
            string input = "🐢";
            Encoding encoding = StringUtils.UTF8_WITHOUT_BOM;
            
            PooledStringBuilder pooledSb = StringBuilderPool.Rent();
            try
            {
                pooledSb.Builder.Append(input);
                using (MemoryStream output = new MemoryStream())
                using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, encoding))
                {
                    stringStream.CopyTo(output);
                    byte[] actualEncodedData = output.ToArray();
                    byte[] expectedEncodedData = encoding.GetBytes(input);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedEncodedData, actualEncodedData));
                    Assert.AreEqual(output.Length, stringStream.Length);
                    Assert.AreEqual(output.Position, stringStream.Position);
                }

                pooledSb = null;
            }
            catch (ArgumentNullException) { }
            finally
            {
                pooledSb?.Dispose();
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
