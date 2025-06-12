using Durandal.Common.Net.Http2.HPack;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Net.Http2
{
    [TestClass]
    public class StringEncoderTests
    {
        [TestMethod]
        public void TestHpackStringEncoder_ShouldEncodeEmptyStringsWithoutHuffmanEncoding()
        {
            var testStr = "";
            var bytes = StringEncoder.Encode(testStr, HuffmanStrategy.Never);
            Assert.AreEqual(1, bytes.Length);

            // Compare the bytes
            Assert.AreEqual(0x00, bytes[0]);
        }

        [TestMethod]
        public void TestHpackStringEncoder_ShouldEncodeEmptyStringsWithHuffmanEncoding()
        {
            var testStr = "";
            var bytes = StringEncoder.Encode(testStr, HuffmanStrategy.Always);
            Assert.AreEqual(1, bytes.Length);

            // Compare the bytes
            Assert.AreEqual(0x80, bytes[0]);
        }

        [TestMethod]
        public void TestHpackStringEncoder_ShouldEncodeStringsWithoutHuffmanEncoding()
        {
            var testStr = "Hello World";
            var bytes = StringEncoder.Encode(testStr, HuffmanStrategy.Never);
            Assert.AreEqual(12, bytes.Length);

            // Compare the bytes
            Assert.AreEqual(11, bytes[0]);
            for (var i = 0; i < testStr.Length; i++)
            {
                var c = testStr[i];
                Assert.AreEqual((byte)c, bytes[1 + i]);
            }

            // Test with a longer string
            testStr = "";
            for (var i = 0; i < 64; i++)
            {
                testStr += "a";
            }
            for (var i = 0; i < 64; i++)
            {
                testStr += "b";
            }
            bytes = StringEncoder.Encode(testStr, HuffmanStrategy.Never);
            Assert.AreEqual(130, bytes.Length);

            // Compare the bytes
            Assert.AreEqual(127, bytes[0]);
            Assert.AreEqual(1, bytes[1]);
            for (var i = 0; i < testStr.Length; i++)
            {
                var c = testStr[i];
                Assert.AreEqual((byte)c, bytes[2 + i]);
            }
        }

        [TestMethod]
        public void TestHpackStringEncoder_ShouldEncodeStringsWithHuffmanEncoding()
        {
            var testStr = "Hello";
            // 1100011 00101 101000 101000 00111
            // 11000110 01011010 00101000 00111
            // var expectedResult = 0xC65A283F;
            var bytes = StringEncoder.Encode(testStr, HuffmanStrategy.Always);
            Assert.AreEqual(5, bytes.Length);

            // Compare the bytes
            Assert.AreEqual(0x84, bytes[0]);
            Assert.AreEqual(0xC6, bytes[1]);
            Assert.AreEqual(0x5A, bytes[2]);
            Assert.AreEqual(0x28, bytes[3]);
            Assert.AreEqual(0x3F, bytes[4]);

            // Test with a longer string
            testStr = "";
            for (var i = 0; i < 64; i++)
            {
                testStr += (char)9; // ffffea  [24]
                testStr += "Z"; // fd  [ 8]
            }

            bytes = StringEncoder.Encode(testStr, HuffmanStrategy.Always);
            Assert.AreEqual(3 + 4 * 64, bytes.Length);

            // Compare the bytes
            Assert.AreEqual(255, bytes[0]); // 127
            Assert.AreEqual(0x81, bytes[1]); // 127 + 1 = 128
            Assert.AreEqual(1, bytes[2]); // 128 + 128 = 256
            for (var i = 3; i < testStr.Length; i += 4)
            {
                Assert.AreEqual(0xFF, bytes[i + 0]);
                Assert.AreEqual(0xFF, bytes[i + 1]);
                Assert.AreEqual(0xEA, bytes[i + 2]);
                Assert.AreEqual(0xFD, bytes[i + 3]);
            }
        }

        [TestMethod]
        public void TestHpackStringEncoder_ShouldApplyHuffmanEncodingIfStringGetsSmaller()
        {
            var testStr = "test"; // 01001 00101 01000 01001 => 01001001 01010000 1001

            var bytes = StringEncoder.Encode(testStr, HuffmanStrategy.IfSmaller);
            Assert.AreEqual(4, bytes.Length);

            // Compare the bytes
            Assert.AreEqual(0x83, bytes[0]);
            Assert.AreEqual(0x49, bytes[1]);
            Assert.AreEqual(0x50, bytes[2]);
            Assert.AreEqual(0x9F, bytes[3]);
        }

        [TestMethod]
        public void TestHpackStringEncoder_ShouldNotApplyHuffmanEncodingIfStringDoesNotGetSmaller()
        {
            var testStr = "XZ"; // 11111100 11111101

            var bytes = StringEncoder.Encode(testStr, HuffmanStrategy.IfSmaller);
            Assert.AreEqual(3, bytes.Length);

            // Compare the bytes
            Assert.AreEqual(0x02, bytes[0]);
            Assert.AreEqual((byte)'X', bytes[1]);
            Assert.AreEqual((byte)'Z', bytes[2]);
        }
    }
}
