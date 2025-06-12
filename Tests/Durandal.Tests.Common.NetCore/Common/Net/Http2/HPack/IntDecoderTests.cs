using Durandal.Common.Net.Http2.HPack;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Tests.Common.Net.Http2;

namespace Durandal.Tests.Common.Net.Http2
{
    [TestClass]
    public class IntDecoderTests
    {
        [TestMethod]
        public void TestHPackIntDecoder_ShouldDecodeAValueThatIsCompletelyInThePrefix()
        {
            IntDecoder Decoder = new IntDecoder();

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x2A);
            buf.WriteByte(0x80);
            buf.WriteByte(0xFE);

            var consumed = Decoder.Decode(5, new ArraySegment<byte>(buf.Bytes, 0, 3));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(10, Decoder.Result);
            Assert.AreEqual(1, consumed);

            consumed = Decoder.Decode(5, new ArraySegment<byte>(buf.Bytes, 1, 2));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(0, Decoder.Result);
            Assert.AreEqual(1, consumed);

            consumed = Decoder.Decode(5, new ArraySegment<byte>(buf.Bytes, 2, 1));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(30, Decoder.Result);
            Assert.AreEqual(1, consumed);

            // Test with 1bit prefix (least)
            buf = new HpackTestBuffer();
            buf.WriteByte(0xFE);
            buf.WriteByte(0x00);
            buf.WriteByte(0x54);

            consumed = Decoder.Decode(1, new ArraySegment<byte>(buf.Bytes, 0, 3));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(0, Decoder.Result);
            Assert.AreEqual(1, consumed);

            consumed = Decoder.Decode(1, new ArraySegment<byte>(buf.Bytes, 1, 2));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(0, Decoder.Result);
            Assert.AreEqual(1, consumed);

            consumed = Decoder.Decode(1, new ArraySegment<byte>(buf.Bytes, 2, 1));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(0, Decoder.Result);
            Assert.AreEqual(1, consumed);

            // Test with 8bit prefix (largest)
            buf = new HpackTestBuffer();
            buf.WriteByte(0xFE);
            buf.WriteByte(0xEF);
            buf.WriteByte(0x00);
            buf.WriteByte(0x01);
            buf.WriteByte(0x2A);

            consumed = Decoder.Decode(8, new ArraySegment<byte>(buf.Bytes, 0, 5));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(0xFE, Decoder.Result);
            Assert.AreEqual(1, consumed);

            consumed = Decoder.Decode(8, new ArraySegment<byte>(buf.Bytes, 1, 4));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(0xEF, Decoder.Result);
            Assert.AreEqual(1, consumed);

            consumed = Decoder.Decode(8, new ArraySegment<byte>(buf.Bytes, 2, 3));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(0, Decoder.Result);
            Assert.AreEqual(1, consumed);

            consumed = Decoder.Decode(8, new ArraySegment<byte>(buf.Bytes, 3, 2));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(1, Decoder.Result);
            Assert.AreEqual(1, consumed);

            consumed = Decoder.Decode(8, new ArraySegment<byte>(buf.Bytes, 4, 1));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(42, Decoder.Result);
            Assert.AreEqual(1, consumed);
        }

        [TestMethod]
        public void TestHPackIntDecoder_ShouldDecodeMultiByteValues()
        {
            IntDecoder Decoder = new IntDecoder();

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x1F);
            buf.WriteByte(0x9A);
            buf.WriteByte(10);
            var consumed = Decoder.Decode(5, new ArraySegment<byte>(buf.Bytes, 0, 3));
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(1337, Decoder.Result);
            Assert.AreEqual(3, consumed);
        }

        [TestMethod]
        public void TestHPackIntDecoder_ShouldDecodeInMultipleSteps()
        {
            IntDecoder Decoder = new IntDecoder();

            var buf1 = new HpackTestBuffer();
            buf1.WriteByte(0x1F);
            buf1.WriteByte(154);
            var buf2 = new HpackTestBuffer();
            buf2.WriteByte(10);

            var consumed = Decoder.Decode(5, buf1.View);
            Assert.IsFalse(Decoder.Done);
            Assert.AreEqual(2, consumed);

            consumed = Decoder.DecodeCont(buf2.View);
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(1337, Decoder.Result);
            Assert.AreEqual(1, consumed);

            // And test with only prefix in first byte
            buf1 = new HpackTestBuffer();
            buf1.WriteByte(0x1F);
            buf2 = new HpackTestBuffer();
            buf2.WriteByte(154);
            buf2.WriteByte(10);

            consumed = Decoder.Decode(5, buf1.View);
            Assert.IsFalse(Decoder.Done);
            Assert.AreEqual(1, consumed);

            consumed = Decoder.DecodeCont(buf2.View);
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(1337, Decoder.Result);
            Assert.AreEqual(2, consumed);

            // Test with a single bit prefix
            buf1 = new HpackTestBuffer();
            buf1.WriteByte(0xFF); // I = 1
            buf2 = new HpackTestBuffer();
            buf2.WriteByte(0x90); // I = 1 + 0x10 * 2^0 = 0x11
            buf2.WriteByte(0x10); // I = 0x81 + 0x10 * 2^7 = 0x11 + 0x800 = 0x811

            consumed = Decoder.Decode(1, buf1.View);
            Assert.IsFalse(Decoder.Done);
            Assert.AreEqual(1, consumed);

            consumed = Decoder.DecodeCont(buf2.View);
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(0x811, Decoder.Result);
            Assert.AreEqual(2, consumed);

            // Test with 8bit prefix
            buf1 = new HpackTestBuffer();
            buf1.WriteByte(0xFF); // I = 0xFF
            buf1.WriteByte(0x90); // I = 0xFF + 0x10 * 2^0 = 0x10F
            buf2 = new HpackTestBuffer();
            buf2.WriteByte(0x10); // I = 0x10F + 0x10 * 2^7 = 0x10F + 0x800 = 0x90F

            consumed = Decoder.Decode(8, buf1.View);
            Assert.IsFalse(Decoder.Done);
            Assert.AreEqual(2, consumed);

            consumed = Decoder.DecodeCont(buf2.View);
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(0x90F, Decoder.Result);
            Assert.AreEqual(1, consumed);
        }

        [TestMethod]
        public void TestHPackIntDecoder_ShouldThrowAnErrorIfDecodedValueGetsTooLarge()
        {
            IntDecoder Decoder = new IntDecoder();

            // Add 5*7 = 35bits after the prefix, which results in a larger value than 2^53-1
            var buf = new HpackTestBuffer();
            buf.WriteByte(0x1F);
            buf.WriteByte(0xFF);
            buf.WriteByte(0xFF);
            buf.WriteByte(0xFF);
            buf.WriteByte(0xFF);
            buf.WriteByte(0xEF);
            var ex = AssertThrows<Exception>(() => Decoder.Decode(5, buf.View));
            Assert.AreEqual(ex.Message, "invalid integer");
        }

        private static T AssertThrows<T>(Action lambda) where T : Exception
        {
            try
            {
                lambda();
                Assert.Fail("Should have thrown a " + typeof(T).Name);
                return default(T);
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(T));
                return ex as T;
            }
        }
    }
}
