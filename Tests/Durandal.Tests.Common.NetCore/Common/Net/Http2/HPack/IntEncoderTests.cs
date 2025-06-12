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
    public class IntEncoderTests
    {
        [TestMethod]
        public void TestHPackIntEncoder_ShouldEncodeValuesWhichFitIntoThePrefix()
        {
            int val = 30; // Fits into 5bit prefix
            var buf = IntEncoder.Encode(val, 0x80, 5);
            Assert.AreEqual(buf.Length, 1);
            Assert.AreEqual(buf[0], 0x80 | 0x1E);

            val = 1; // Fits into 2 bit prefix
            buf = IntEncoder.Encode(val, 0xFC, 2);
            Assert.AreEqual(buf.Length, 1);
            Assert.AreEqual(buf[0], 0xFC | 0x01);

            val = 128; // Fits into 8 bit prefix
            buf = IntEncoder.Encode(val, 0x00, 8);
            Assert.AreEqual(buf.Length, 1);
            Assert.AreEqual(buf[0], 0x80);

            val = 254; // Fits into 8 bit prefix
            buf = IntEncoder.Encode(val, 0x00, 8);
            Assert.AreEqual(buf.Length, 1);
            Assert.AreEqual(buf[0], 254);
        }

        [TestMethod]
        public void TestHPackIntEncoder_ShouldEncodeValuesIntoPrefixPlusExtraBytes()
        {
            int val = 30; // Fits not into 4bit prefix
            var buf = IntEncoder.Encode(val, 0xA0, 4);
            Assert.AreEqual(buf.Length, 2);
            Assert.AreEqual(buf[0], 0xA0 | 0x0F);
            Assert.AreEqual(buf[1], 15); // 30 - 15 = 15

            val = 1; // Fits not into 1bit prefix
            buf = IntEncoder.Encode(val, 0xFE, 1);
            Assert.AreEqual(buf.Length, 2);
            Assert.AreEqual(buf[0], 0xFE | 0x01);
            Assert.AreEqual(buf[1], 0);

            val = 127; // Fits not into 1bit prefix
            buf = IntEncoder.Encode(val, 0x80, 7);
            Assert.AreEqual(buf.Length, 2);
            Assert.AreEqual(buf[0], 0x80 | 0xFF);
            Assert.AreEqual(buf[1], 0);

            val = 128; // Fits not into 1bit prefix
            buf = IntEncoder.Encode(val, 0x00, 7);
            Assert.AreEqual(buf.Length, 2);
            Assert.AreEqual(buf[0], 0x00 | 0x7F);
            Assert.AreEqual(buf[1], 1);

            val = 255; // Fits not into 8 bit prefix
            buf = IntEncoder.Encode(val, 0x00, 8);
            Assert.AreEqual(buf.Length, 2);
            Assert.AreEqual(buf[0], 0xFF);
            Assert.AreEqual(buf[1], 0);

            val = 256; // Fits not into 8 bit prefix
            buf = IntEncoder.Encode(val, 0x00, 8);
            Assert.AreEqual(buf.Length, 2);
            Assert.AreEqual(buf[0], 0xFF);
            Assert.AreEqual(buf[1], 1);

            val = 1337; // 3byte example from the spec
            buf = IntEncoder.Encode(val, 0xC0, 5);
            Assert.AreEqual(buf.Length, 3);
            Assert.AreEqual(buf[0], 0xC0 | 0x1F);
            Assert.AreEqual(buf[1], 0x9A);
            Assert.AreEqual(buf[2], 0x0A);

            // 4 byte example
            val = 27 * 128 * 128 + 31 * 128 + 1;
            buf = IntEncoder.Encode(val, 0, 1);
            Assert.AreEqual(buf.Length, 4);
            Assert.AreEqual(buf[0], 1);
            Assert.AreEqual(buf[1], 0x80);
            Assert.AreEqual(buf[2], 0x9F);
            Assert.AreEqual(buf[3], 27);
        }

        [TestMethod]
        public void TestHPackIntEncoder_ShouldEncodeTheMaximumAllowedValue()
        {
            int val = Int32.MaxValue; // Fits not into 4bit prefix
            var buf = IntEncoder.Encode(val, 0xA0, 2);
            Assert.AreEqual(buf.Length, 6);
            Assert.AreEqual(buf[0], 0xA3); // Remaining: 2147483644
            Assert.AreEqual(buf[1], 252); // Remaining: 16777215
            Assert.AreEqual(buf[2], 255); // Remaining: 131071
            Assert.AreEqual(buf[3], 255); // Remaining: 1023
            Assert.AreEqual(buf[4], 255); // Remaining: 7
            Assert.AreEqual(buf[5], 7);

            // TODO: Probably test this with other prefixes
        }
    }
}
