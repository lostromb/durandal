using Durandal.Common.Net.Http2.HPack;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Durandal.Tests.Common.Net.Http2;

namespace Durandal.Tests.Common.Net.Http2
{
    [TestClass]
    public class HPackDecoderTests
    {
        static DynamicTable GetDynamicTableOfDecoder(HPackDecoder decoder)
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var fi = decoder.GetType().GetTypeInfo().GetField("_headerTable", flags);
            var headerTable = (HeaderTable)fi.GetValue(decoder);
            fi = headerTable.GetType().GetField("dynamic", flags);
            DynamicTable dtable = (DynamicTable)fi.GetValue(headerTable);
            return dtable;
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHaveADefaultDynamicTableSizeAndLimitOf4096()
        {
            HPackDecoder decoder = new HPackDecoder();

            Assert.AreEqual(4096, decoder.DynamicTableSize);
            Assert.AreEqual(4096, decoder.DynamicTableSizeLimit);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldAllowToAdjustTheDynamicTableSizeAndLimtThroughConstructor()
        {
            HPackDecoder decoder = new HPackDecoder(new HPackDecoder.Options
            {
                DynamicTableSize = 0,
                DynamicTableSizeLimit = 10
            });

            Assert.AreEqual(0, decoder.DynamicTableSize);
            Assert.AreEqual(10, decoder.DynamicTableSizeLimit);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldAllowReadingAFullyIndexedValueFromTheStaticTable()
        {
            HPackDecoder decoder = new HPackDecoder();

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x81);
            var consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(":authority", decoder.HeaderField.Name);
            Assert.AreEqual("", decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(42, decoder.HeaderSize);
            Assert.AreEqual(1, consumed);
            Assert.IsTrue(decoder.HasInitialState);

            buf.WriteByte(0x82);
            consumed = decoder.Decode(new ArraySegment<byte>(buf.Bytes, 1, 1));
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(":method", decoder.HeaderField.Name);
            Assert.AreEqual("GET", decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(42, decoder.HeaderSize);
            Assert.AreEqual(1, consumed);
            Assert.IsTrue(decoder.HasInitialState);

            buf.WriteByte(0xBD);
            consumed = decoder.Decode(new ArraySegment<byte>(buf.Bytes, 2, 1));
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual("www-authenticate", decoder.HeaderField.Name);
            Assert.AreEqual("", decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(48, decoder.HeaderSize);
            Assert.AreEqual(1, consumed);
            Assert.IsTrue(decoder.HasInitialState);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldAllowReadingAFullyIndexedValueFromTheStaticAndDynamicTable()
        {
            var decoder = new HPackDecoder(new HPackDecoder.Options
            {
                DynamicTableSize = 100000,
                DynamicTableSizeLimit = 100000,
            });

            var dtable = GetDynamicTableOfDecoder(decoder);

            for (var i = 100; i >= 0; i--)
            {
                // Add elements into the dynamic table
                // Insert in backward fashion because the last insertion
                // will get the lowest index
                dtable.Insert("key" + i, 1, "val" + i, 1);
            }

            var buf = new HpackTestBuffer();
            buf.WriteByte(0xFF); // Prefix 127
            var consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            Assert.IsFalse(decoder.HasInitialState);
            consumed = decoder.Decode(new ArraySegment<byte>(buf.Bytes, 1, 0));
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);
            Assert.IsFalse(decoder.HasInitialState);
            // Add next byte
            buf.WriteByte(0x0A); // 127 + 10
            consumed = decoder.Decode(new ArraySegment<byte>(buf.Bytes, 1, 1));
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(1, consumed);
            Assert.IsTrue(decoder.HasInitialState);

            var targetIndex = 137 - StaticTable.Length - 1;
            Assert.AreEqual("key" + targetIndex, decoder.HeaderField.Name);
            Assert.AreEqual("val" + targetIndex, decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(34, decoder.HeaderSize);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldThrowAnErrorWhenReadingFullyIndexedValueOnInvalidIndex()
        {
            var decoder = new HPackDecoder();
            var buf = new HpackTestBuffer();
            buf.WriteByte(0x80); // Index 0 // TODO: Might be used for something different
            try
            {
                decoder.Decode(buf.View);
                Assert.Fail();
            }
            catch (IndexOutOfRangeException) { }

            decoder = new HPackDecoder();
            buf = new HpackTestBuffer();
            buf.WriteByte(0xC0); // 1100 0000 => Index 64 is outside of static table
            try
            {
                decoder.Decode(buf.View);
                Assert.Fail();
            }
            catch (IndexOutOfRangeException) { }

            // Put enough elements into the dynamic table to reach 64 in total
            decoder = new HPackDecoder();
            var neededAdds = 64 - StaticTable.Length - 1; // -1 because 1 is not a valid index
            var dtable = GetDynamicTableOfDecoder(decoder);
            for (var i = neededAdds; i >= 0; i--)
            {
                // Add elements into the dynamic table
                // Insert in backward fashion because the last insertion
                // will get the lowest index
                dtable.Insert("key" + i, 1, "val" + i, 1);
            }
            // This should now no longer throw
            var consumed = decoder.Decode(buf.View);

            // Increase index by 1 should lead to throw again
            buf = new HpackTestBuffer();
            buf.WriteByte(0xC1); // 1100 0001 => Index 66 is outside of static and dynamic table
            try
            {
                decoder.Decode(buf.View);
                Assert.Fail();
            }
            catch (IndexOutOfRangeException) { }
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldAllowReceivingIncrementalIndexedFieldsWithIndexedName()
        {
            var decoder = new HPackDecoder();

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x42); // Incremental indexed, header index 2
            buf.WriteByte(0x03); // 3 bytes
            buf.WriteString("abc");
            var consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(":method", decoder.HeaderField.Name);
            Assert.AreEqual("abc", decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(42, decoder.HeaderSize);
            Assert.AreEqual(5, consumed);
            Assert.AreEqual(1, decoder.DynamicTableLength);
            Assert.AreEqual(32 + 7 + 3, decoder.DynamicTableUsedSize);
            Assert.IsTrue(decoder.HasInitialState);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldAllowReceivingIncrementalIndexedFieldsWithNotIndexedName()
        {
            var decoder = new HPackDecoder();

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x40); // Incremental indexed, no name index
            buf.WriteByte(0x02);
            buf.WriteString("de");
            buf.WriteByte(0x03);
            buf.WriteString("fgh");
            var consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual("de", decoder.HeaderField.Name);
            Assert.AreEqual("fgh", decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(37, decoder.HeaderSize);
            Assert.AreEqual(8, consumed);
            Assert.AreEqual(1, decoder.DynamicTableLength);
            Assert.AreEqual(32 + 2 + 3, decoder.DynamicTableUsedSize);
            Assert.IsTrue(decoder.HasInitialState);

            var emptyView = new ArraySegment<byte>(new byte[20], 20, 0);

            // Add a second entry to it, this time in chunks
            buf = new HpackTestBuffer();
            buf.WriteByte(0x40);
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            Assert.IsFalse(decoder.HasInitialState);
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte(0x02);
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte('z');
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte('1');
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte(0x03);
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte('2');
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte('3');
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte('4');
            consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual("z1", decoder.HeaderField.Name);
            Assert.AreEqual("234", decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(37, decoder.HeaderSize);
            Assert.AreEqual(1, consumed);
            Assert.IsTrue(decoder.HasInitialState);
            Assert.AreEqual(decoder.DynamicTableLength, 2);
            Assert.AreEqual(decoder.DynamicTableUsedSize, 32 + 2 + 3 + 32 + 2 + 3);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldAllowReceivingNotIndexedFieldsWithIndexedName()
        {
            var decoder = new HPackDecoder();

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x02); // Not indexed, header index 2
            buf.WriteByte(0x03); // 3 bytes
            buf.WriteString("abc");
            var consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(":method", decoder.HeaderField.Name);
            Assert.AreEqual("abc", decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(42, decoder.HeaderSize);
            Assert.AreEqual(5, consumed);
            Assert.IsTrue(decoder.HasInitialState);
            Assert.AreEqual(decoder.DynamicTableLength, 0);
            Assert.AreEqual(decoder.DynamicTableUsedSize, 0);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldAllowReceivingNotIndexedFieldsWithNotIndexedName()
        {
            var decoder = new HPackDecoder();

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x00); // Not indexed, no name index
            buf.WriteByte(0x02);
            buf.WriteString("de");
            buf.WriteByte(0x03);
            buf.WriteString("fgh");
            var consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual("de", decoder.HeaderField.Name);
            Assert.AreEqual("fgh", decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(37, decoder.HeaderSize);
            Assert.AreEqual(8, consumed);
            Assert.IsTrue(decoder.HasInitialState);
            Assert.AreEqual(decoder.DynamicTableLength, 0);
            Assert.AreEqual(decoder.DynamicTableUsedSize, 0);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldAllowReceivingNeverIndexedFieldsWithIndexedName()
        {
            var decoder = new HPackDecoder();

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x12); // Never indexed, header index 2
            buf.WriteByte(0x03); // 3 bytes
            buf.WriteString("abc");
            var consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(":method", decoder.HeaderField.Name);
            Assert.AreEqual("abc", decoder.HeaderField.Value);
            Assert.IsTrue(decoder.HeaderField.Sensitive);
            Assert.AreEqual(42, decoder.HeaderSize);
            Assert.AreEqual(5, consumed);
            Assert.IsTrue(decoder.HasInitialState);
            Assert.AreEqual(decoder.DynamicTableLength, 0);
            Assert.AreEqual(decoder.DynamicTableUsedSize, 0);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldAllowReceivingNeverIndexedFieldsWithNotIndexedName()
        {
            var decoder = new HPackDecoder();

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x10); // Never indexed, no name index
            buf.WriteByte(0x02);
            buf.WriteString("de");
            buf.WriteByte(0x03);
            buf.WriteString("fgh");
            var consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual("de", decoder.HeaderField.Name);
            Assert.AreEqual("fgh", decoder.HeaderField.Value);
            Assert.IsTrue(decoder.HeaderField.Sensitive);
            Assert.AreEqual(37, decoder.HeaderSize);
            Assert.AreEqual(8, consumed);
            Assert.IsTrue(decoder.HasInitialState);
            Assert.AreEqual(decoder.DynamicTableLength, 0);
            Assert.AreEqual(decoder.DynamicTableUsedSize, 0);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHandleTableSizeUpdateFrames()
        {
            var decoder = new HPackDecoder();

            // Table update in single step
            var buf = new HpackTestBuffer();
            buf.WriteByte(0x30);
            var consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(16, decoder.DynamicTableSize);
            Assert.AreEqual(1, consumed);
            Assert.IsTrue(decoder.HasInitialState);

            // Table update in multiple steps
            buf = new HpackTestBuffer();
            buf.WriteByte(0x3F); // I = 31
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(16, decoder.DynamicTableSize);
            Assert.AreEqual(1, consumed);
            Assert.IsFalse(decoder.HasInitialState);
            // 2nd data part
            buf = new HpackTestBuffer();
            buf.WriteByte(0x80); // I = 31
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(16, decoder.DynamicTableSize);
            Assert.AreEqual(1, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte(0x10); // I = 31 + 0x10 * 2^7 = 2079
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(2079, decoder.DynamicTableSize);
            Assert.AreEqual(1, consumed);
            Assert.IsTrue(decoder.HasInitialState);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHandleHeadersAfterATableSizeUpdateFrame()
        {
            var decoder = new HPackDecoder();

            // Table update in single step
            var buf = new HpackTestBuffer();
            buf.WriteByte(0x30); // Table update
            buf.WriteByte(0x81); // Header frame
            var consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(":authority", decoder.HeaderField.Name);
            Assert.AreEqual("", decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(42, decoder.HeaderSize);
            Assert.AreEqual(2, consumed);
            Assert.AreEqual(16, decoder.DynamicTableSize);
            Assert.IsTrue(decoder.HasInitialState);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHandleHeadersAfterMultipleTableSizeUpdates()
        {
            var decoder = new HPackDecoder();

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x20); // Table update
            buf.WriteByte(0x30); // Table update
            buf.WriteByte(0x81); // Header frame
            var consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(":authority", decoder.HeaderField.Name);
            Assert.AreEqual("", decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(42, decoder.HeaderSize);
            Assert.AreEqual(3, consumed);
            Assert.AreEqual(16, decoder.DynamicTableSize);
            Assert.IsTrue(decoder.HasInitialState);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldFailIfTableUpdateFollowsAfterHeader()
        {
            var decoder = new HPackDecoder();
            var buf = new HpackTestBuffer();
            buf.WriteByte(0x81); // Header frame
            buf.WriteByte(0x20); // Table update
            var consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(":authority", decoder.HeaderField.Name);
            Assert.AreEqual(1, consumed);
            buf.RemoveFront(consumed);
            try
            {
                decoder.Decode(buf.View);
                Assert.Fail();
            }
            catch (Exception) { }
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldThrowAnErrorIfTableSizeUpdateExceedsLimit()
        {
            var decoder = new HPackDecoder(new HPackDecoder.Options
            {
                DynamicTableSize = 100,
                DynamicTableSizeLimit = 100,
            });

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x3F); // I = 31
            buf.WriteByte(0x80); // I = 31
            buf.WriteByte(0x10); // I = 31 + 0x10 * 2^7 = 2079
            try
            {
                decoder.Decode(buf.View);
                Assert.Fail();
            }
            catch (Exception ex)
            {
                Assert.AreEqual("table size limit exceeded", ex.Message);
            }
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldNotCrashWhenDecodeIsStartedWith0Bytes()
        {
            var decoder = new HPackDecoder();

            var buf = new HpackTestBuffer();
            var consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);
            Assert.IsTrue(decoder.HasInitialState);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldNotCrashWhenNotNameIndexedDecodeIsContinuedWith0Bytes()
        {
            var decoder = new HPackDecoder();

            var emptyView = new ArraySegment<byte>(new byte[20], 20, 0);

            var buf = new HpackTestBuffer();
            buf.WriteByte(0x10);
            var consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            // 0 byte read
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte(0x01); // name length
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            // 0 byte read
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte('a'); // name value
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            // 0 byte read
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte(0x01); // value length
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            // 0 byte read
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);
            buf = new HpackTestBuffer();
            buf.WriteByte('b'); // value value
            consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(1, consumed);
            Assert.AreEqual("a", decoder.HeaderField.Name);
            Assert.AreEqual("b", decoder.HeaderField.Value);
            Assert.IsTrue(decoder.HeaderField.Sensitive);
            Assert.AreEqual(34, decoder.HeaderSize);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldNotCrashWhenNameIndexedDecodeIsContinuedWith0Bytes()
        {
            // Need more entries in the dynamic table
            var decoder = new HPackDecoder(new HPackDecoder.Options
            {
                DynamicTableSize = 100000,
                DynamicTableSizeLimit = 100000,
            });

            var emptyView = new ArraySegment<byte>(new byte[20], 20, 0);

            var dtable = GetDynamicTableOfDecoder(decoder);
            for (var i = 99; i >= 0; i--)
            {
                // Add elements into the dynamic table
                // Insert in backward fashion because the last insertion
                // will get the lowest index
                dtable.Insert("key" + i, 1, "val" + i, 1);
            }
            Assert.AreEqual(100, decoder.DynamicTableLength);
            Assert.AreEqual(34 * 100, decoder.DynamicTableUsedSize);

            // Need a more than 1byte index value for this test
            var buf = new HpackTestBuffer();
            buf.WriteByte(0x1F); // prefix filled with 15
            var consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            // 0 byte read
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);

            buf = new HpackTestBuffer();
            buf.WriteByte(0x80); // cont index
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            // 0 byte read
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);

            buf = new HpackTestBuffer();
            buf.WriteByte(0x01); // final index, value = 15 + 0 + 1 * 2^7 = 143
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            // 0 byte read
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);

            buf = new HpackTestBuffer();
            buf.WriteByte(0x02); // string length
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            // 0 byte read
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);

            buf = new HpackTestBuffer();
            buf.WriteByte('a'); // name value 1
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            // 0 byte read
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);

            buf = new HpackTestBuffer();
            buf.WriteByte('9'); // name value 2
            consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(1, consumed);
            var tableIdx = 143 - StaticTable.Length - 1;
            Assert.AreEqual("key" + tableIdx, decoder.HeaderField.Name);
            Assert.AreEqual("a9", decoder.HeaderField.Value);
            Assert.IsTrue(decoder.HeaderField.Sensitive);
            Assert.AreEqual(35, decoder.HeaderSize);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldNotCrashWhenFullyIndexedDecodeIsContinuedWith0Bytes()
        {
            // Need more entries in the dynamic table
            var decoder = new HPackDecoder(new HPackDecoder.Options
            {
                DynamicTableSize = 100000,
                DynamicTableSizeLimit = 100000,
            });

            var emptyView = new ArraySegment<byte>(new byte[20], 20, 0);
            var dtable = GetDynamicTableOfDecoder(decoder);
            for (var i = 199; i >= 0; i--)
            {
                // Add elements into the dynamic table
                // Insert in backward fashion because the last insertion
                // will get the lowest index
                dtable.Insert("key" + i, 1, "val" + i, 1);
            }
            Assert.AreEqual(200, decoder.DynamicTableLength);
            Assert.AreEqual(34 * 200, decoder.DynamicTableUsedSize);

            // Need a more than 1byte index value for this test
            var buf = new HpackTestBuffer();
            buf.WriteByte(0xFF); // prefix filled with 127
            var consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            // 0 byte read
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);

            buf = new HpackTestBuffer();
            buf.WriteByte(0x80); // cont index
            consumed = decoder.Decode(buf.View);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(1, consumed);
            // 0 byte read
            consumed = decoder.Decode(emptyView);
            Assert.IsFalse(decoder.Done);
            Assert.AreEqual(0, consumed);

            buf = new HpackTestBuffer();
            buf.WriteByte(0x01); // final index, value = 127 + 0 + 1 * 2^7 = 255
            consumed = decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(1, consumed);
            var tableIdx = 255 - StaticTable.Length - 1;
            Assert.AreEqual("key" + tableIdx, decoder.HeaderField.Name);
            Assert.AreEqual("val" + tableIdx, decoder.HeaderField.Value);
            Assert.IsFalse(decoder.HeaderField.Sensitive);
            Assert.AreEqual(34, decoder.HeaderSize);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHandleExampleC2_1OfTheSpecificationCorrectly()
        {
            var decoder = new HPackDecoder();

            var buf = new HpackTestBuffer();
            buf.AddHexString(
                "400a637573746f6d2d6b65790d637573" +
                "746f6d2d686561646572");

            decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual("custom-key", decoder.HeaderField.Name);
            Assert.AreEqual("custom-header", decoder.HeaderField.Value);
            Assert.AreEqual(55, decoder.HeaderSize);
            Assert.AreEqual(1, decoder.DynamicTableLength);
            Assert.AreEqual(55, decoder.DynamicTableUsedSize);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHandleExampleC2_2OfTheSpecificationCorrectly()
        {
            var decoder = new HPackDecoder();
            var buf = new HpackTestBuffer();
            buf.AddHexString("040c2f73616d706c652f70617468");

            decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(":path", decoder.HeaderField.Name);
            Assert.AreEqual("/sample/path", decoder.HeaderField.Value);
            Assert.AreEqual(49, decoder.HeaderSize);
            Assert.AreEqual(0, decoder.DynamicTableLength);
            Assert.AreEqual(0, decoder.DynamicTableUsedSize);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHandleExampleC2_3OfTheSpecificationCorrectly()
        {
            var decoder = new HPackDecoder();
            var buf = new HpackTestBuffer();
            buf.AddHexString("100870617373776f726406736563726574");

            decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual("password", decoder.HeaderField.Name);
            Assert.AreEqual("secret", decoder.HeaderField.Value);
            Assert.AreEqual(46, decoder.HeaderSize);
            Assert.AreEqual(0, decoder.DynamicTableLength);
            Assert.AreEqual(0, decoder.DynamicTableUsedSize);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHandleExampleC2_4OfTheSpecificationCorrectly()
        {
            var decoder = new HPackDecoder();
            var buf = new HpackTestBuffer();
            buf.AddHexString("82");

            decoder.Decode(buf.View);
            Assert.IsTrue(decoder.Done);
            Assert.AreEqual(":method", decoder.HeaderField.Name);
            Assert.AreEqual("GET", decoder.HeaderField.Value);
            Assert.AreEqual(42, decoder.HeaderSize);
            Assert.AreEqual(0, decoder.DynamicTableLength);
            Assert.AreEqual(0, decoder.DynamicTableUsedSize);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHandleExampleC3OfTheSpecificationCorrectly()
        {
            var decoder = new HPackDecoder();
            // C.3.1
            var buf = new HpackTestBuffer();
            buf.AddHexString("828684410f7777772e6578616d706c652e636f6d");

            var results = DecodeAll(decoder, buf);
            Assert.AreEqual(4, results.Count);
            Assert.AreEqual(":method", results[0].Name);
            Assert.AreEqual("GET", results[0].Value);
            Assert.AreEqual(":scheme", results[1].Name);
            Assert.AreEqual("http", results[1].Value);
            Assert.AreEqual(":path", results[2].Name);
            Assert.AreEqual("/", results[2].Value);
            Assert.AreEqual(":authority", results[3].Name);
            Assert.AreEqual("www.example.com", results[3].Value);
            Assert.AreEqual(57, decoder.DynamicTableUsedSize);
            Assert.AreEqual(1, decoder.DynamicTableLength);

            // C.3.2
            buf = new HpackTestBuffer();
            buf.AddHexString("828684be58086e6f2d6361636865");

            results = DecodeAll(decoder, buf);
            Assert.AreEqual(5, results.Count);
            Assert.AreEqual(":method", results[0].Name);
            Assert.AreEqual("GET", results[0].Value);
            Assert.AreEqual(":scheme", results[1].Name);
            Assert.AreEqual("http", results[1].Value);
            Assert.AreEqual(":path", results[2].Name);
            Assert.AreEqual("/", results[2].Value);
            Assert.AreEqual(":authority", results[3].Name);
            Assert.AreEqual("www.example.com", results[3].Value);
            Assert.AreEqual("cache-control", results[4].Name);
            Assert.AreEqual("no-cache", results[4].Value);
            Assert.AreEqual(110, decoder.DynamicTableUsedSize);
            Assert.AreEqual(2, decoder.DynamicTableLength);

            // C.3.3
            buf = new HpackTestBuffer();
            buf.AddHexString("828785bf400a637573746f6d2d6b65790c637573746f6d2d76616c7565");

            results = DecodeAll(decoder, buf);
            Assert.AreEqual(5, results.Count);
            Assert.AreEqual(":method", results[0].Name);
            Assert.AreEqual("GET", results[0].Value);
            Assert.AreEqual(":scheme", results[1].Name);
            Assert.AreEqual("https", results[1].Value);
            Assert.AreEqual(":path", results[2].Name);
            Assert.AreEqual("/index.html", results[2].Value);
            Assert.AreEqual(":authority", results[3].Name);
            Assert.AreEqual("www.example.com", results[3].Value);
            Assert.AreEqual("custom-key", results[4].Name);
            Assert.AreEqual("custom-value", results[4].Value);
            Assert.AreEqual(164, decoder.DynamicTableUsedSize);
            Assert.AreEqual(3, decoder.DynamicTableLength);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHandleExampleC4OfTheSpecificationCorrectly()
        {
            var decoder = new HPackDecoder();
            // C.4.1
            var buf = new HpackTestBuffer();
            buf.AddHexString("828684418cf1e3c2e5f23a6ba0ab90f4ff");

            var results = DecodeAll(decoder, buf);
            Assert.AreEqual(4, results.Count);
            Assert.AreEqual(":method", results[0].Name);
            Assert.AreEqual("GET", results[0].Value);
            Assert.AreEqual(":scheme", results[1].Name);
            Assert.AreEqual("http", results[1].Value);
            Assert.AreEqual(":path", results[2].Name);
            Assert.AreEqual("/", results[2].Value);
            Assert.AreEqual(":authority", results[3].Name);
            Assert.AreEqual("www.example.com", results[3].Value);
            Assert.AreEqual(57, decoder.DynamicTableUsedSize);
            Assert.AreEqual(1, decoder.DynamicTableLength);

            // C.4.2
            buf = new HpackTestBuffer();
            buf.AddHexString("828684be5886a8eb10649cbf");

            results = DecodeAll(decoder, buf);
            Assert.AreEqual(5, results.Count);
            Assert.AreEqual(":method", results[0].Name);
            Assert.AreEqual("GET", results[0].Value);
            Assert.AreEqual(":scheme", results[1].Name);
            Assert.AreEqual("http", results[1].Value);
            Assert.AreEqual(":path", results[2].Name);
            Assert.AreEqual("/", results[2].Value);
            Assert.AreEqual(":authority", results[3].Name);
            Assert.AreEqual("www.example.com", results[3].Value);
            Assert.AreEqual("cache-control", results[4].Name);
            Assert.AreEqual("no-cache", results[4].Value);
            Assert.AreEqual(110, decoder.DynamicTableUsedSize);
            Assert.AreEqual(2, decoder.DynamicTableLength);

            // C.4.3
            buf = new HpackTestBuffer();
            buf.AddHexString("828785bf408825a849e95ba97d7f8925a849e95bb8e8b4bf");

            results = DecodeAll(decoder, buf);
            Assert.AreEqual(5, results.Count);
            Assert.AreEqual(":method", results[0].Name);
            Assert.AreEqual("GET", results[0].Value);
            Assert.AreEqual(":scheme", results[1].Name);
            Assert.AreEqual("https", results[1].Value);
            Assert.AreEqual(":path", results[2].Name);
            Assert.AreEqual("/index.html", results[2].Value);
            Assert.AreEqual(":authority", results[3].Name);
            Assert.AreEqual("www.example.com", results[3].Value);
            Assert.AreEqual("custom-key", results[4].Name);
            Assert.AreEqual("custom-value", results[4].Value);
            Assert.AreEqual(164, decoder.DynamicTableUsedSize);
            Assert.AreEqual(3, decoder.DynamicTableLength);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHandleExampleC5OfTheSpecificationCorrectly()
        {
            var decoder = new HPackDecoder(new HPackDecoder.Options
            {
                DynamicTableSize = 256,
                DynamicTableSizeLimit = 256,
            });

            // C.5.1
            var buf = new HpackTestBuffer();
            buf.AddHexString(
                "4803333032580770726976617465611d" +
                "4d6f6e2c203231204f63742032303133" +
                "2032303a31333a323120474d546e1768" +
                "747470733a2f2f7777772e6578616d70" +
                "6c652e636f6d");

            var results = DecodeAll(decoder, buf);
            Assert.AreEqual(4, results.Count);
            Assert.AreEqual(":status", results[0].Name);
            Assert.AreEqual("302", results[0].Value);
            Assert.AreEqual("cache-control", results[1].Name);
            Assert.AreEqual("private", results[1].Value);
            Assert.AreEqual("date", results[2].Name);
            Assert.AreEqual("Mon, 21 Oct 2013 20:13:21 GMT", results[2].Value);
            Assert.AreEqual("location", results[3].Name);
            Assert.AreEqual("https://www.example.com", results[3].Value);
            Assert.AreEqual(222, decoder.DynamicTableUsedSize);
            Assert.AreEqual(4, decoder.DynamicTableLength);

            // C.5.2
            buf = new HpackTestBuffer();
            buf.AddHexString("4803333037c1c0bf");

            results = DecodeAll(decoder, buf);
            Assert.AreEqual(4, results.Count);
            Assert.AreEqual(":status", results[0].Name);
            Assert.AreEqual("307", results[0].Value);
            Assert.AreEqual("cache-control", results[1].Name);
            Assert.AreEqual("private", results[1].Value);
            Assert.AreEqual("date", results[2].Name);
            Assert.AreEqual("Mon, 21 Oct 2013 20:13:21 GMT", results[2].Value);
            Assert.AreEqual("location", results[3].Name);
            Assert.AreEqual("https://www.example.com", results[3].Value);
            Assert.AreEqual(222, decoder.DynamicTableUsedSize);
            Assert.AreEqual(4, decoder.DynamicTableLength);

            // C.5.3
            buf = new HpackTestBuffer();
            buf.AddHexString(
                "88c1611d4d6f6e2c203231204f637420" +
                "323031332032303a31333a323220474d" +
                "54c05a04677a69707738666f6f3d4153" +
                "444a4b48514b425a584f5157454f5049" +
                "5541585157454f49553b206d61782d61" +
                "67653d333630303b2076657273696f6e" +
                "3d31");

            results = DecodeAll(decoder, buf);
            Assert.AreEqual(6, results.Count);
            Assert.AreEqual(":status", results[0].Name);
            Assert.AreEqual("200", results[0].Value);
            Assert.AreEqual("cache-control", results[1].Name);
            Assert.AreEqual("private", results[1].Value);
            Assert.AreEqual("date", results[2].Name);
            Assert.AreEqual("Mon, 21 Oct 2013 20:13:22 GMT", results[2].Value);
            Assert.AreEqual("location", results[3].Name);
            Assert.AreEqual("https://www.example.com", results[3].Value);
            Assert.AreEqual("content-encoding", results[4].Name);
            Assert.AreEqual("gzip", results[4].Value);
            Assert.AreEqual("set-cookie", results[5].Name);
            Assert.AreEqual("foo=ASDJKHQKBZXOQWEOPIUAXQWEOIU; max-age=3600; version=1", results[5].Value);
            Assert.AreEqual(215, decoder.DynamicTableUsedSize);
            Assert.AreEqual(3, decoder.DynamicTableLength);
        }

        [TestMethod]
        public void TestHpackDecoder_ShouldHandleExampleC6OfTheSpecificationCorrectly()
        {
            var decoder = new HPackDecoder(new HPackDecoder.Options
            {
                DynamicTableSize = 256,
                DynamicTableSizeLimit = 256,
            });
            // C.6.1
            var buf = new HpackTestBuffer();
            buf.AddHexString(
                "488264025885aec3771a4b6196d07abe" +
                "941054d444a8200595040b8166e082a6" +
                "2d1bff6e919d29ad171863c78f0b97c8" +
                "e9ae82ae43d3");

            var results = DecodeAll(decoder, buf);
            Assert.AreEqual(4, results.Count);
            Assert.AreEqual(":status", results[0].Name);
            Assert.AreEqual("302", results[0].Value);
            Assert.AreEqual("cache-control", results[1].Name);
            Assert.AreEqual("private", results[1].Value);
            Assert.AreEqual("date", results[2].Name);
            Assert.AreEqual("Mon, 21 Oct 2013 20:13:21 GMT", results[2].Value);
            Assert.AreEqual("location", results[3].Name);
            Assert.AreEqual("https://www.example.com", results[3].Value);
            Assert.AreEqual(222, decoder.DynamicTableUsedSize);
            Assert.AreEqual(4, decoder.DynamicTableLength);

            // C.6.2
            buf = new HpackTestBuffer();
            buf.AddHexString("4883640effc1c0bf");

            results = DecodeAll(decoder, buf);
            Assert.AreEqual(4, results.Count);
            Assert.AreEqual(":status", results[0].Name);
            Assert.AreEqual("307", results[0].Value);
            Assert.AreEqual("cache-control", results[1].Name);
            Assert.AreEqual("private", results[1].Value);
            Assert.AreEqual("date", results[2].Name);
            Assert.AreEqual("Mon, 21 Oct 2013 20:13:21 GMT", results[2].Value);
            Assert.AreEqual("location", results[3].Name);
            Assert.AreEqual("https://www.example.com", results[3].Value);
            Assert.AreEqual(222, decoder.DynamicTableUsedSize);
            Assert.AreEqual(4, decoder.DynamicTableLength);

            // C.6.3
            buf = new HpackTestBuffer();
            buf.AddHexString(
                "88c16196d07abe941054d444a8200595" +
                "040b8166e084a62d1bffc05a839bd9ab" +
                "77ad94e7821dd7f2e6c7b335dfdfcd5b" +
                "3960d5af27087f3672c1ab270fb5291f" +
                "9587316065c003ed4ee5b1063d5007");

            results = DecodeAll(decoder, buf);
            Assert.AreEqual(6, results.Count);
            Assert.AreEqual(":status", results[0].Name);
            Assert.AreEqual("200", results[0].Value);
            Assert.AreEqual("cache-control", results[1].Name);
            Assert.AreEqual("private", results[1].Value);
            Assert.AreEqual("date", results[2].Name);
            Assert.AreEqual("Mon, 21 Oct 2013 20:13:22 GMT", results[2].Value);
            Assert.AreEqual("location", results[3].Name);
            Assert.AreEqual("https://www.example.com", results[3].Value);
            Assert.AreEqual("content-encoding", results[4].Name);
            Assert.AreEqual("gzip", results[4].Value);
            Assert.AreEqual("set-cookie", results[5].Name);
            Assert.AreEqual("foo=ASDJKHQKBZXOQWEOPIUAXQWEOIU; max-age=3600; version=1", results[5].Value);
            Assert.AreEqual(215, decoder.DynamicTableUsedSize);
            Assert.AreEqual(3, decoder.DynamicTableLength);
        }

        static List<HeaderField> DecodeAll(HPackDecoder decoder, HpackTestBuffer buf)
        {
            var results = new List<HeaderField>();
            var total = buf.View;
            var offset = total.Offset;
            var count = total.Count;
            while (true)
            {
                var segment = new ArraySegment<byte>(total.Array, offset, count);
                var consumed = decoder.Decode(segment);
                offset += consumed;
                count -= consumed;
                if (decoder.Done)
                {
                    results.Add(decoder.HeaderField);
                }
                else break;
            }
            return results;
        }
    }
}
