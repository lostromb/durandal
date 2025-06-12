using Durandal.Common.Net.Http2.HPack;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Durandal.Tests.Common.Net.Http2;
using Durandal.Common.Test;
using Durandal.Common.Collections;

namespace Durandal.Tests.Common.Net.Http2
{
    [TestClass]
    public class HPackEncoderTests
    {
        const int MaxFrameSize = 65535;

        struct EncodeResult
        {
            public byte[] Bytes;
            public int FieldCount;
        }

        private EncodeResult EncodeToTempBuf(
            HPackEncoder encoder, IEnumerable<HeaderField> headers, int maxSize)
        {
            var buf = new byte[maxSize];
            var res = encoder.EncodeInto(new ArraySegment<byte>(buf), headers);
            // Clamp the bytes
            var newBuf = new byte[res.UsedBytes];
            Array.Copy(buf, 0, newBuf, 0, res.UsedBytes);
            return new EncodeResult
            {
                Bytes = newBuf,
                FieldCount = res.FieldCount,
            };
        }

        [TestMethod]
        public void TestHpackEncoder_ShouldHaveADefaultDynamicTableSizeOf4096()
        {
            var encoder = new HPackEncoder();
            Assert.AreEqual(4096, encoder.DynamicTableSize);
        }

        [TestMethod]
        public void TestHpackEncoder_ShouldAllowToAdjustTheDynamicTableSizeThroughConstructor()
        {
            var encoder = new HPackEncoder(new HPackEncoder.Options
            {
                DynamicTableSize = 0,
            });
            Assert.AreEqual(0, encoder.DynamicTableSize);
        }

        [TestMethod]
        public void TestHpackEncoder_ShouldAllowToAdjustTheDynamicTableSizeThroughPropertySetter()
        {
            var encoder = new HPackEncoder();
            encoder.DynamicTableSize = 200;
            Assert.AreEqual(200, encoder.DynamicTableSize);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(30)]
        [DataRow(16535)]
        public void TestHpackEncoder_SizeUpdatesShouldBeEncodedWithinNextHeaderBlock(
            int newSize)
        {
            var encoder = new HPackEncoder(new HPackEncoder.Options
            {
                HuffmanStrategy = HuffmanStrategy.Never,
            });
            encoder.DynamicTableSize = newSize;

            var expectedTableUpdateBytes = IntEncoder.Encode(newSize, 0x20, 5);

            var fields = new HeaderField[] {
                new HeaderField{ Name = "ab", Value = "cd", Sensitive = true }
            };

            var result = new HpackTestBuffer();
            result.WriteBytes(expectedTableUpdateBytes);
            result.AddHexString("1002");
            result.WriteString("ab");
            result.AddHexString("02");
            result.WriteString("cd");

            var res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(1, res.FieldCount);
            Assert.AreEqual(0, encoder.DynamicTableUsedSize);
            Assert.AreEqual(0, encoder.DynamicTableLength);
            Assert.AreEqual(newSize, encoder.DynamicTableSize);
        }

        [TestMethod]
        [DataRow("0,30", "203e")]
        [DataRow("30,0", "20")]
        [DataRow("5000,0,30", "203e")]
        [DataRow("5000,0,30,0", "20")]
        [DataRow("5000,15,30,0", "20")]
        [DataRow("5000,15,30,7,10", "272a")]
        public void TestHpackEncoder_IfSizeIsChangedMultipleTimesAllNecessaryUpdatesShouldBeEncoded(string sizeChangesString, string expectedTableUpdateHexBytes)
        {
            int[] sizeChanges = sizeChangesString.Split(',').Select((str) => int.Parse(str, CultureInfo.InvariantCulture)).ToArray();
            var encoder = new HPackEncoder(new HPackEncoder.Options
            {
                HuffmanStrategy = HuffmanStrategy.Never,
            });
            foreach (var newSize in sizeChanges)
            {
                encoder.DynamicTableSize = newSize;
            }

            var fields = new HeaderField[] {
                new HeaderField{ Name = "ab", Value = "cd", Sensitive = true }
            };

            var result = new HpackTestBuffer();
            result.AddHexString(expectedTableUpdateHexBytes);
            result.AddHexString("1002");
            result.WriteString("ab");
            result.AddHexString("02");
            result.WriteString("cd");

            var res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(1, res.FieldCount);
            Assert.AreEqual(0, encoder.DynamicTableUsedSize);
            Assert.AreEqual(0, encoder.DynamicTableLength);
            Assert.AreEqual(sizeChanges[sizeChanges.Length - 1], encoder.DynamicTableSize);

            // Encode a further header block
            // This may not contain any tableupdate data
            result = new HpackTestBuffer();
            result.AddHexString("1002");
            result.WriteString("ab");
            result.AddHexString("02");
            result.WriteString("cd");

            res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(1, res.FieldCount);
            Assert.AreEqual(0, encoder.DynamicTableUsedSize);
            Assert.AreEqual(0, encoder.DynamicTableLength);
            Assert.AreEqual(sizeChanges[sizeChanges.Length - 1], encoder.DynamicTableSize);
        }

        [TestMethod]
        public void TestHpackEncoder_ShouldHandleExampleC2_1OfTheSpecificationCorrectly()
        {
            var encoder = new HPackEncoder(new HPackEncoder.Options
            {
                HuffmanStrategy = HuffmanStrategy.Never,
            });
            var fields = new HeaderField[] {
                new HeaderField{ Name = "custom-key", Value = "custom-header", Sensitive = false }
            };

            var result = new HpackTestBuffer();
            result.AddHexString(
                "400a637573746f6d2d6b65790d637573" +
                "746f6d2d686561646572");

            var res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(1, res.FieldCount);
            Assert.AreEqual(55, encoder.DynamicTableUsedSize);
            Assert.AreEqual(1, encoder.DynamicTableLength);
        }

        [TestMethod]
        public void TestHpackEncoder_ShouldHandleExampleC2_2OfTheSpecificationCorrectly()
        {
            var encoder = new HPackEncoder(new HPackEncoder.Options
            {
                HuffmanStrategy = HuffmanStrategy.Never,
            });
            // Decrease table size to avoid using indexing
            // This will enforce a table size update which we need to compensate for
            encoder.DynamicTableSize = 0;
            var fields = new HeaderField[] {
                new HeaderField{ Name = ":path", Value = "/sample/path", Sensitive = false }
            };

            // first item with name :path
            var result = new HpackTestBuffer();
            result.AddHexString("20040c2f73616d706c652f70617468");

            var res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(1, res.FieldCount);
            Assert.AreEqual(0, encoder.DynamicTableUsedSize);
            Assert.AreEqual(0, encoder.DynamicTableLength);
        }

        [TestMethod]
        public void TestHpackEncoder_ShouldHandleExampleC2_3OfTheSpecificationCorrectly()
        {
            var encoder = new HPackEncoder(new HPackEncoder.Options
            {
                HuffmanStrategy = HuffmanStrategy.Never,
            });
            var fields = new HeaderField[] {
                new HeaderField{ Name = "password", Value = "secret", Sensitive = true }
            };

            var result = new HpackTestBuffer();
            result.AddHexString("100870617373776f726406736563726574");

            var res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(1, res.FieldCount);
            Assert.AreEqual(0, encoder.DynamicTableUsedSize);
            Assert.AreEqual(0, encoder.DynamicTableLength);
        }

        [TestMethod]
        public void TestHpackEncoder_ShouldHandleExampleC2_4OfTheSpecificationCorrectly()
        {
            var encoder = new HPackEncoder(new HPackEncoder.Options
            {
                HuffmanStrategy = HuffmanStrategy.Never,
            });
            var fields = new HeaderField[] {
                new HeaderField{ Name = ":method", Value = "GET", Sensitive = false }
            };

            var result = new HpackTestBuffer();
            result.AddHexString("82");

            var res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(1, res.FieldCount);
            Assert.AreEqual(0, encoder.DynamicTableUsedSize);
            Assert.AreEqual(0, encoder.DynamicTableLength);
        }

        [TestMethod]
        public void TestHpackEncoder_ShouldHandleExampleC3OfTheSpecificationCorrectly()
        {
            var encoder = new HPackEncoder(new HPackEncoder.Options
            {
                HuffmanStrategy = HuffmanStrategy.Never,
            });
            var fields = new HeaderField[] {
                new HeaderField{ Name = ":method", Value ="GET", Sensitive = false },
                new HeaderField{ Name = ":scheme", Value ="http", Sensitive = false },
                new HeaderField{ Name = ":path", Value ="/", Sensitive = false },
                new HeaderField{ Name = ":authority", Value ="www.example.com", Sensitive = false },
            };

            // C.3.1
            var result = new HpackTestBuffer();
            result.AddHexString("828684410f7777772e6578616d706c652e636f6d");

            var res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(4, res.FieldCount);
            Assert.AreEqual(57, encoder.DynamicTableUsedSize);
            Assert.AreEqual(1, encoder.DynamicTableLength);

            // C.3.2
            fields = new HeaderField[] {
                new HeaderField{ Name = ":method", Value ="GET", Sensitive = false },
                new HeaderField{ Name = ":scheme", Value ="http", Sensitive = false },
                new HeaderField{ Name = ":path", Value ="/", Sensitive = false },
                new HeaderField{ Name = ":authority", Value ="www.example.com", Sensitive = false },
                new HeaderField{ Name = "cache-control", Value ="no-cache", Sensitive = false },
            };
            result = new HpackTestBuffer();
            result.AddHexString("828684be58086e6f2d6361636865");

            res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(5, res.FieldCount);
            Assert.AreEqual(110, encoder.DynamicTableUsedSize);
            Assert.AreEqual(2, encoder.DynamicTableLength);

            // C.3.3
            fields = new HeaderField[] {
                new HeaderField{ Name = ":method", Value ="GET", Sensitive = false },
                new HeaderField{ Name = ":scheme", Value ="https", Sensitive = false },
                new HeaderField{ Name = ":path", Value ="/index.html", Sensitive = false },
                new HeaderField{ Name = ":authority", Value ="www.example.com", Sensitive = false },
                new HeaderField{ Name = "custom-key", Value ="custom-value", Sensitive = false },
            };
            result = new HpackTestBuffer();
            result.AddHexString("828785bf400a637573746f6d2d6b65790c637573746f6d2d76616c7565");

            res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(5, res.FieldCount);
            Assert.AreEqual(164, encoder.DynamicTableUsedSize);
            Assert.AreEqual(3, encoder.DynamicTableLength);
        }

        [TestMethod]
        public void TestHpackEncoder_ShouldHandleExampleC4OfTheSpecificationCorrectly()
        {
            var encoder = new HPackEncoder(new HPackEncoder.Options
            {
                HuffmanStrategy = HuffmanStrategy.Always,
            });
            var fields = new HeaderField[] {
                new HeaderField{ Name = ":method", Value ="GET", Sensitive = false },
                new HeaderField{ Name = ":scheme", Value ="http", Sensitive = false },
                new HeaderField{ Name = ":path", Value ="/", Sensitive = false },
                new HeaderField{ Name = ":authority", Value ="www.example.com", Sensitive = false },
            };

            // C.4.1
            var result = new HpackTestBuffer();
            result.AddHexString("828684418cf1e3c2e5f23a6ba0ab90f4ff");

            var res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(4, res.FieldCount);
            Assert.AreEqual(57, encoder.DynamicTableUsedSize);
            Assert.AreEqual(1, encoder.DynamicTableLength);

            // C.4.2
            fields = new HeaderField[] {
                new HeaderField{ Name = ":method", Value ="GET", Sensitive = false },
                new HeaderField{ Name = ":scheme", Value ="http", Sensitive = false },
                new HeaderField{ Name = ":path", Value ="/", Sensitive = false },
                new HeaderField{ Name = ":authority", Value ="www.example.com", Sensitive = false },
                new HeaderField{ Name = "cache-control", Value ="no-cache", Sensitive = false },
            };
            result = new HpackTestBuffer();
            result.AddHexString("828684be5886a8eb10649cbf");

            res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(5, res.FieldCount);
            Assert.AreEqual(110, encoder.DynamicTableUsedSize);
            Assert.AreEqual(2, encoder.DynamicTableLength);

            // C.4.3
            fields = new HeaderField[] {
                new HeaderField{ Name = ":method", Value ="GET", Sensitive = false },
                new HeaderField{ Name = ":scheme", Value ="https", Sensitive = false },
                new HeaderField{ Name = ":path", Value ="/index.html", Sensitive = false },
                new HeaderField{ Name = ":authority", Value ="www.example.com", Sensitive = false },
                new HeaderField{ Name = "custom-key", Value ="custom-value", Sensitive = false },
            };
            result = new HpackTestBuffer();
            result.AddHexString("828785bf408825a849e95ba97d7f8925a849e95bb8e8b4bf");

            res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(5, res.FieldCount);
            Assert.AreEqual(164, encoder.DynamicTableUsedSize);
            Assert.AreEqual(3, encoder.DynamicTableLength);
        }

        [TestMethod]
        public void TestHpackEncoder_ShouldHandleExampleC5OfTheSpecificationCorrectly()
        {
            var encoder = new HPackEncoder(new HPackEncoder.Options
            {
                HuffmanStrategy = HuffmanStrategy.Never,
                DynamicTableSize = 256,
            });
            var fields = new HeaderField[] {
                new HeaderField{ Name = ":status", Value ="302", Sensitive = false },
                new HeaderField{ Name = "cache-control", Value ="private", Sensitive = false },
                new HeaderField{ Name = "date", Value ="Mon, 21 Oct 2013 20:13:21 GMT", Sensitive = false },
                new HeaderField{ Name = "location", Value ="https://www.example.com", Sensitive = false },
            };

            // C.5.1
            var result = new HpackTestBuffer();
            result.AddHexString(
                "4803333032580770726976617465611d" +
                "4d6f6e2c203231204f63742032303133" +
                "2032303a31333a323120474d546e1768" +
                "747470733a2f2f7777772e6578616d70" +
                "6c652e636f6d");

            var res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(4, res.FieldCount);
            Assert.AreEqual(222, encoder.DynamicTableUsedSize);
            Assert.AreEqual(4, encoder.DynamicTableLength);

            // C.5.2
            fields = new HeaderField[] {
                new HeaderField{ Name = ":status", Value ="307", Sensitive = false },
                new HeaderField{ Name = "cache-control", Value ="private", Sensitive = false },
                new HeaderField{ Name = "date", Value ="Mon, 21 Oct 2013 20:13:21 GMT", Sensitive = false },
                new HeaderField{ Name = "location", Value ="https://www.example.com", Sensitive = false },
            };
            result = new HpackTestBuffer();
            result.AddHexString("4803333037c1c0bf");

            res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(4, res.FieldCount);
            Assert.AreEqual(222, encoder.DynamicTableUsedSize);
            Assert.AreEqual(4, encoder.DynamicTableLength);

            // C.5.3
            fields = new HeaderField[] {
                new HeaderField{ Name = ":status", Value ="200", Sensitive = false },
                new HeaderField{ Name = "cache-control", Value ="private", Sensitive = false },
                new HeaderField{ Name = "date", Value ="Mon, 21 Oct 2013 20:13:22 GMT", Sensitive = false },
                new HeaderField{ Name = "location", Value ="https://www.example.com", Sensitive = false },
                new HeaderField{ Name = "content-encoding", Value ="gzip", Sensitive = false },
                new HeaderField{ Name = "set-cookie", Value ="foo=ASDJKHQKBZXOQWEOPIUAXQWEOIU; max-age=3600; version=1", Sensitive = false },
            };
            result = new HpackTestBuffer();
            result.AddHexString(
                "88c1611d4d6f6e2c203231204f637420" +
                "323031332032303a31333a323220474d" +
                "54c05a04677a69707738666f6f3d4153" +
                "444a4b48514b425a584f5157454f5049" +
                "5541585157454f49553b206d61782d61" +
                "67653d333630303b2076657273696f6e" +
                "3d31");

            res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(6, res.FieldCount);
            Assert.AreEqual(215, encoder.DynamicTableUsedSize);
            Assert.AreEqual(3, encoder.DynamicTableLength);
        }

        [TestMethod]
        public void TestHpackEncoder_ShouldHandleExampleC6OfTheSpecificationCorrectly()
        {
            var encoder = new HPackEncoder(new HPackEncoder.Options
            {
                HuffmanStrategy = HuffmanStrategy.Always,
                DynamicTableSize = 256,
            });

            var fields = new HeaderField[] {
                new HeaderField{ Name = ":status", Value ="302", Sensitive = false },
                new HeaderField{ Name = "cache-control", Value ="private", Sensitive = false },
                new HeaderField{ Name = "date", Value ="Mon, 21 Oct 2013 20:13:21 GMT", Sensitive = false },
                new HeaderField{ Name = "location", Value ="https://www.example.com", Sensitive = false },
            };

            // C.6.1
            var result = new HpackTestBuffer();
            result.AddHexString(
                "488264025885aec3771a4b6196d07abe" +
                "941054d444a8200595040b8166e082a6" +
                "2d1bff6e919d29ad171863c78f0b97c8" +
                "e9ae82ae43d3");

            var res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(4, res.FieldCount);
            Assert.AreEqual(222, encoder.DynamicTableUsedSize);
            Assert.AreEqual(4, encoder.DynamicTableLength);

            // C.6.2
            fields = new HeaderField[] {
                new HeaderField{ Name = ":status", Value ="307", Sensitive = false },
                new HeaderField{ Name = "cache-control", Value ="private", Sensitive = false },
                new HeaderField{ Name = "date", Value ="Mon, 21 Oct 2013 20:13:21 GMT", Sensitive = false },
                new HeaderField{ Name = "location", Value ="https://www.example.com", Sensitive = false },
            };
            result = new HpackTestBuffer();
            result.AddHexString("4883640effc1c0bf");

            res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(4, res.FieldCount);
            Assert.AreEqual(222, encoder.DynamicTableUsedSize);
            Assert.AreEqual(4, encoder.DynamicTableLength);

            // C.6.3
            fields = new HeaderField[] {
                new HeaderField{ Name = ":status", Value ="200", Sensitive = false },
                new HeaderField{ Name = "cache-control", Value ="private", Sensitive = false },
                new HeaderField{ Name = "date", Value ="Mon, 21 Oct 2013 20:13:22 GMT", Sensitive = false },
                new HeaderField{ Name = "location", Value ="https://www.example.com", Sensitive = false },
                new HeaderField{ Name = "content-encoding", Value ="gzip", Sensitive = false },
                new HeaderField{ Name = "set-cookie", Value ="foo=ASDJKHQKBZXOQWEOPIUAXQWEOIU; max-age=3600; version=1", Sensitive = false },
            };
            result = new HpackTestBuffer();
            result.AddHexString(
                "88c16196d07abe941054d444a8200595" +
                "040b8166e084a62d1bffc05a839bd9ab" +
                "77ad94e7821dd7f2e6c7b335dfdfcd5b" +
                "3960d5af27087f3672c1ab270fb5291f" +
                "9587316065c003ed4ee5b1063d5007");

            res = EncodeToTempBuf(encoder, fields, MaxFrameSize);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(result.Bytes, res.Bytes));
            Assert.AreEqual(6, res.FieldCount);
            Assert.AreEqual(215, encoder.DynamicTableUsedSize);
            Assert.AreEqual(3, encoder.DynamicTableLength);
        }

        // TODO: Add tests to verify that the encoder stops encoding when data
        // doesn't fit into a heaer block
        // Ideally check at various positions, since the out-of-memory-problem
        // can happen anywhere.
    }
}
