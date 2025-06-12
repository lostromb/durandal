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
    public class HuffmanDecodeTests
    {
        [TestMethod]
        public void TestHpackHuffman_ShouldDecodeValidHuffmanCodes()
        {
            var buffer = new HpackTestBuffer();
            buffer.WriteByte(0xff);
            buffer.WriteByte(0xc7);
            var decoded = Huffman.Decode(buffer.View);
            Assert.AreEqual(decoded.Length, 1);
            Assert.AreEqual(decoded[0], 0);

            buffer = new HpackTestBuffer();
            buffer.WriteByte(0xf8); // '&'
            decoded = Huffman.Decode(buffer.View);
            Assert.AreEqual(decoded.Length, 1);
            Assert.AreEqual(decoded[0], '&');

            buffer = new HpackTestBuffer();
            buffer.WriteByte(0x59); // 0101 1001
            buffer.WriteByte(0x7f); // 0111 1111
            buffer.WriteByte(0xff); // 1111 1111
            buffer.WriteByte(0xe1); // 1110 0001
            decoded = Huffman.Decode(buffer.View);
            Assert.AreEqual(decoded.Length, 3);
            Assert.AreEqual(decoded[0], '-');
            Assert.AreEqual(decoded[1], '.');
            Assert.AreEqual(decoded[2], '\\');

            buffer = new HpackTestBuffer();
            buffer.WriteByte(0x86); // AB = 100001 1011101 = 1000 0110 1110 1
            buffer.WriteByte(0xEF);
            decoded = Huffman.Decode(buffer.View);
            Assert.AreEqual(decoded.Length, 2);
            Assert.AreEqual(decoded[0], 'A');
            Assert.AreEqual(decoded[1], 'B');
        }

        [TestMethod]
        public void TestHpackHuffman_ShouldThrowErrorIfEOSSymbolIsEncountered()
        {
            var buffer = new HpackTestBuffer();
            buffer.WriteByte(0xf8); // '&'
            buffer.WriteByte(0xff);
            buffer.WriteByte(0xff);
            buffer.WriteByte(0xff);
            buffer.WriteByte(0xfc); //EOS unpadded
            var ex = AssertThrows<Exception>(() => {
                Huffman.Decode(buffer.View);
            });
            Assert.AreEqual(ex.Message, "Encountered EOS in huffman code");
        }

        [TestMethod]
        public void TestHpackHuffman_ShouldThrowErrorIfEOSSymbolIsEncountered_FillLastByte()
        {
            var buffer = new HpackTestBuffer();
            buffer.WriteByte(0xf8); // '&'
            buffer.WriteByte(0xff);
            buffer.WriteByte(0xff);
            buffer.WriteByte(0xff);
            buffer.WriteByte(0xff); // EOS but last bits are filled
            var ex = AssertThrows<Exception>(() => {
                Huffman.Decode(buffer.View);
            });
            Assert.AreEqual(ex.Message, "Encountered EOS in huffman code");
        }

        [TestMethod]
        public void TestHpackHuffman_ShouldThrowErrorIfPaddingIsZero()
        {
            var buffer = new HpackTestBuffer();
            buffer.WriteByte(0x86); // AB = 100001 1011101 = 1000 0110 1110 1
            buffer.WriteByte(0xE8);
            var ex = AssertThrows<Exception>(() => {
                Huffman.Decode(buffer.View);
            });
            Assert.AreEqual("Invalid padding", ex.Message);
        }

        [TestMethod]
        public void TestHpackHuffman_ShouldThrowErrorIfPaddingIsLongerThanNecessary()
        {
            var buffer = new HpackTestBuffer();
            buffer.WriteByte(0x86); // AB = 100001 1011101 = 1000 0110 1110 1
            buffer.WriteByte(0xEF);
            buffer.WriteByte(0xFF); // Extra padding
            var ex = AssertThrows<Exception>(() => {
                Huffman.Decode(buffer.View);
            });
            Assert.AreEqual("Padding exceeds 7 bits", ex.Message);

            buffer = new HpackTestBuffer();
            buffer.WriteByte(0xFA); // ',' = 0xFA
            buffer.WriteByte(0xFF); // Padding
            ex = AssertThrows<Exception>(() => {
                Huffman.Decode(buffer.View);
            });
            Assert.AreEqual("Padding exceeds 7 bits", ex.Message);
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
