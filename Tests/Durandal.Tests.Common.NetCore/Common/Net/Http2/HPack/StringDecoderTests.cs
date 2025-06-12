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
    public class StringDecoderTests
    {
        [TestMethod]
        public void TestHpackStringDecoder_ShouldDecodeAnASCIIStringFromACompleteBuffer()
        {
            StringDecoder Decoder = new StringDecoder(1024);

            // 0 Characters
            var buf = new HpackTestBuffer();
            buf.WriteByte(0x00);
            var consumed = Decoder.Decode(buf.View);
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual("", Decoder.Result);
            Assert.AreEqual(0, Decoder.StringLength);
            Assert.AreEqual(1, consumed);

            buf = new HpackTestBuffer();
            buf.WriteByte(0x04); // 4 Characters, non huffman
            buf.WriteByte('a');
            buf.WriteByte('s');
            buf.WriteByte('d');
            buf.WriteByte('f');

            consumed = Decoder.Decode(buf.View);
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual("asdf", Decoder.Result);
            Assert.AreEqual(4, Decoder.StringLength);
            Assert.AreEqual(5, consumed);

            // Multi-byte prefix
            buf = new HpackTestBuffer();
            buf.WriteByte(0x7F); // Prefix filled, non huffman, I = 127
            buf.WriteByte(0xFF); // I = 0x7F + 0x7F * 2^0 = 0xFE
            buf.WriteByte(0x03); // I = 0xFE + 0x03 * 2^7 = 0xFE + 0x180 = 0x27E = 638
            var expectedLength = 638;
            var expectedString = "";
            for (var i = 0; i < expectedLength; i++)
            {
                buf.WriteByte(' ');
                expectedString += ' ';
            }
            consumed = Decoder.Decode(buf.View);
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(expectedString, Decoder.Result);
            Assert.AreEqual(expectedLength, Decoder.StringLength);
            Assert.AreEqual(3 + expectedLength, consumed);
        }

        [TestMethod]
        public void TestHpackStringDecoder_ShouldDecodeAnASCIIStringIfPayloadIsInMultipleBuffers()
        {
            StringDecoder Decoder = new StringDecoder(1024);

            // Only put the prefix in the first byte
            var buf = new HpackTestBuffer();
            buf.WriteByte(0x04); // 4 Characters, non huffman
            var consumed = Decoder.Decode(buf.View);
            Assert.IsFalse(Decoder.Done);
            Assert.AreEqual(1, consumed);

            // Next chunk with part of data
            buf = new HpackTestBuffer();
            buf.WriteByte('a');
            buf.WriteByte('s');
            consumed = Decoder.DecodeCont(buf.View);
            Assert.IsFalse(Decoder.Done);
            Assert.AreEqual(2, consumed);

            // Give the thing a depleted buffer
            consumed = Decoder.DecodeCont(new ArraySegment<byte>(buf.Bytes, 2, 0));
            Assert.IsFalse(Decoder.Done);
            Assert.AreEqual(0, consumed);

            // Final chunk
            buf = new HpackTestBuffer();
            buf.WriteByte('d');
            buf.WriteByte('f');
            consumed = Decoder.DecodeCont(buf.View);
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual("asdf", Decoder.Result);
            Assert.AreEqual(4, Decoder.StringLength);
            Assert.AreEqual(2, consumed);
        }

        [TestMethod]
        public void TestHpackStringDecoder_ShouldDecodeAHuffmanEncodedStringIfLengthAndPayloadAreInMultipleBuffers()
        {
            StringDecoder Decoder = new StringDecoder(1024);

            // Only put the prefix in the first byte
            var buf = new HpackTestBuffer();
            buf.WriteByte(0xFF); // Prefix filled, non huffman, I = 127
            var consumed = Decoder.Decode(buf.View);
            Assert.IsFalse(Decoder.Done);
            Assert.AreEqual(1, consumed);

            // Remaining part of the length plus first content byte
            buf = new HpackTestBuffer();
            buf.WriteByte(0x02); // I = 0x7F + 0x02 * 2^0 = 129 byte payload
            buf.WriteByte(0xf9); // first byte of the payload
            var expectedResult = "*";
            consumed = Decoder.DecodeCont(buf.View);
            Assert.IsFalse(Decoder.Done);
            Assert.AreEqual(2, consumed);

            // Half of other content bytes
            buf = new HpackTestBuffer();
            for (var i = 0; i < 64; i = i + 2)
            {
                expectedResult += ")-";
                buf.WriteByte(0xfe);
                buf.WriteByte(0xd6);
            }
            consumed = Decoder.DecodeCont(buf.View);
            Assert.IsFalse(Decoder.Done);
            Assert.AreEqual(64, consumed);

            // Last part of content bytes
            buf = new HpackTestBuffer();
            for (var i = 0; i < 64; i = i + 2)
            {
                expectedResult += "0+";
                buf.WriteByte(0x07);
                buf.WriteByte(0xfb);
            }
            consumed = Decoder.DecodeCont(buf.View);
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual(expectedResult, Decoder.Result);
            Assert.AreEqual(129, Decoder.StringLength);
            Assert.AreEqual(64, consumed);
        }

        [TestMethod]
        public void TestHpackStringDecoder_ShouldCheckTheMaximumStringLength()
        {
            StringDecoder Decoder = new StringDecoder(2);

            // 2 Characters are ok
            var buf = new HpackTestBuffer();
            buf.WriteByte(0x02);
            buf.WriteByte('a');
            buf.WriteByte('b');
            var consumed = Decoder.Decode(buf.View);
            Assert.IsTrue(Decoder.Done);
            Assert.AreEqual("ab", Decoder.Result);
            Assert.AreEqual(2, Decoder.StringLength);
            Assert.AreEqual(3, consumed);

            // 3 should fail
            buf = new HpackTestBuffer();
            buf.WriteByte(0x03);
            buf.WriteByte('a');
            buf.WriteByte('b');
            buf.WriteByte('c');
            var ex = AssertThrows<Exception>(() => Decoder.Decode(buf.View));
            Assert.AreEqual("Maximum string length exceeded", ex.Message);

            // Things were the length is stored in a continuation byte should also fail
            buf = new HpackTestBuffer();
            buf.WriteByte(0x7F); // More than 127 bytes
            consumed = Decoder.Decode(buf.View);
            Assert.IsFalse(Decoder.Done);
            Assert.AreEqual(1, consumed);
            buf.WriteByte(1);
            var view = new ArraySegment<byte>(buf.Bytes, 1, 1);
            ex = AssertThrows<Exception>(() => Decoder.DecodeCont(view));
            Assert.AreEqual("Maximum string length exceeded", ex.Message);
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
