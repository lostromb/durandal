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
    public class DynamicTableTests
    {
        [TestMethod]
        public void TestHpackDynamicTable_ShouldStartWith0Items()
        {
            var t = new DynamicTable(4096);
            Assert.AreEqual(0, t.Length);
            Assert.AreEqual(0, t.UsedSize);
        }

        [TestMethod]
        public void TestHpackDynamicTable_ShouldStartWithTheSetMaximumSize()
        {
            var t = new DynamicTable(4096);
            Assert.AreEqual(4096, t.MaxTableSize);
            t.MaxTableSize = 0;
            Assert.AreEqual(0, t.MaxTableSize);
        }

        [TestMethod]
        public void TestHpackDynamicTable_ShouldIncreaseUsedSizeAndLengthIfItemIsInserted()
        {
            var t = new DynamicTable(4096);

            t.Insert("a", 1, "b", 1);
            var expectedSize = 1 + 1 + 32;
            Assert.AreEqual(expectedSize, t.UsedSize);
            Assert.AreEqual(1, t.Length);

            t.Insert("xyz", 3, "abcd", 4);
            expectedSize += 3 + 4 + 32;
            Assert.AreEqual(expectedSize, t.UsedSize);
            Assert.AreEqual(2, t.Length);

            // Fill the table up
            t.Insert("", 0, "a", 4096 - expectedSize - 32);
            expectedSize = 4096;
            Assert.AreEqual(expectedSize, t.UsedSize);
            Assert.AreEqual(3, t.Length);
        }

        [TestMethod]
        public void TestHpackDynamicTable_ShouldReturnTrueIfAnItemCouldBeInserted()
        {
            var t = new DynamicTable(4096);

            var res = t.Insert("a", 1, "b", 1);
            Assert.IsTrue(res);

            res = t.Insert("a", 1, "b", 1);
            Assert.IsTrue(res);

            res = t.Insert("a", 1, "b", 4096 - 2 * (1 + 1 + 32) - 32 - 1);
            Assert.IsTrue(res);
        }

        [TestMethod]
        public void TestHpackDynamicTable_ShouldReturnFalseIfAnItemCanNotBeInserted()
        {
            var t = new DynamicTable(4096);

            var res = t.Insert("a", 5000, "b", 0);
            Assert.IsFalse(res);
            Assert.AreEqual(0, t.Length);
            Assert.AreEqual(t.UsedSize, 0);

            res = t.Insert("a", 0, "b", 5000);
            Assert.IsFalse(res);
            Assert.AreEqual(0, t.Length);
            Assert.AreEqual(t.UsedSize, 0);

            res = t.Insert("a", 3000, "b", 3000);
            Assert.IsFalse(res);
            Assert.AreEqual(t.UsedSize, 0);
            Assert.AreEqual(0, t.Length);
        }

        [TestMethod]
        public void TestHpackDynamicTable_ShouldDrainTheTableIfAnElementCanNotBeInserted()
        {
            var t = new DynamicTable(4096);

            var res = t.Insert("a", 2000, "b", 2000);
            Assert.IsTrue(res);
            Assert.AreEqual(1, t.Length);
            Assert.AreEqual(4032, t.UsedSize);

            res = t.Insert("a", 0, "b", 32);
            Assert.IsTrue(res);
            Assert.AreEqual(4096, t.UsedSize);
            Assert.AreEqual(2, t.Length);

            res = t.Insert("a", 3000, "b", 3000);
            Assert.IsFalse(res);
            Assert.AreEqual(t.UsedSize, 0);
            Assert.AreEqual(0, t.Length);
        }

        private class TableEntryComparer : IEqualityComparer<TableEntry>
        {
            public bool Equals(TableEntry x, TableEntry y)
            {
                return x.Name == y.Name && x.Value == y.Value
                    && x.NameLen == y.NameLen && x.ValueLen == y.ValueLen;
            }

            public int GetHashCode(TableEntry obj)
            {
                return obj.GetHashCode();
            }
        }

        TableEntryComparer ec = new TableEntryComparer();

        [TestMethod]
        public void TestHpackDynamicTable_ShouldReturnTheCorrectElementWhenIndexed()
        {
            var t = new DynamicTable(4096);
            t.Insert("a", 1, "b", 1);
            t.Insert("c", 1, "d", 1);
            t.Insert("e", 1, "f", 1);
            t.Insert("ab", 2, "cd", 2);
            AssertAreEqual(new TableEntry { Name = "ab", NameLen = 2, Value = "cd", ValueLen = 2 }, t.GetAt(0), ec);
            AssertAreEqual(new TableEntry { Name = "e", NameLen = 1, Value = "f", ValueLen = 1 }, t.GetAt(1), ec);
            AssertAreEqual(new TableEntry { Name = "c", NameLen = 1, Value = "d", ValueLen = 1 }, t.GetAt(2), ec);
            AssertAreEqual(new TableEntry { Name = "a", NameLen = 1, Value = "b", ValueLen = 1 }, t.GetAt(3), ec);

            t.Insert("xyz", 3, "fgh", 99);
            AssertAreEqual(new TableEntry { Name = "xyz", NameLen = 3, Value = "fgh", ValueLen = 99 }, t.GetAt(0), ec);
        }

        [TestMethod]
        public void TestHpackDynamicTable_ShouldThrowAnErrorWhenInvalidlyIndexed()
        {
            var t = new DynamicTable(4096);
            AssertThrows<IndexOutOfRangeException>(() => t.GetAt(-1));
            AssertThrows<IndexOutOfRangeException>(() => t.GetAt(0));
            AssertThrows<IndexOutOfRangeException>(() => t.GetAt(1));

            t.Insert("a", 0, "b", 0);
            AssertThrows<IndexOutOfRangeException>(() => t.GetAt(-1));
            t.GetAt(0); // should not throw
            AssertThrows<IndexOutOfRangeException>(() => t.GetAt(1));
        }

        private static void AssertAreEqual<T>(T a, T b, IEqualityComparer<T> comparer)
        {
            Assert.IsTrue(comparer.Equals(a, b));
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

        private static bool InsertItemOfSize(DynamicTable table, string keyName, int size)
        {
            size -= 32;
            var aSize = size / 2;
            var bSize = size - aSize;
            return table.Insert(keyName, aSize, "", bSize);
        }

        [TestMethod]
        public void TestHpackDynamicTable_ShouldEvictItemsWhenMaxSizeIsLowered()
        {
            var t = new DynamicTable(4096);
            InsertItemOfSize(t, "a", 1999);
            InsertItemOfSize(t, "b", 2001);
            InsertItemOfSize(t, "c", 64);
            Assert.AreEqual(3, t.Length);
            Assert.AreEqual(4064, t.UsedSize);

            t.MaxTableSize = 3000;
            Assert.AreEqual(3000, t.MaxTableSize);
            Assert.AreEqual(2, t.Length);
            Assert.AreEqual(2065, t.UsedSize);
            Assert.AreEqual("c", t.GetAt(0).Name);
            Assert.AreEqual("b", t.GetAt(1).Name);

            t.MaxTableSize = 1000;
            Assert.AreEqual(1000, t.MaxTableSize);
            Assert.AreEqual(1, t.Length);
            Assert.AreEqual(64, t.UsedSize);
            Assert.AreEqual("c", t.GetAt(0).Name);

            t.MaxTableSize = 64;
            Assert.AreEqual(64, t.MaxTableSize);
            Assert.AreEqual(1, t.Length);
            Assert.AreEqual(64, t.UsedSize);

            t.MaxTableSize = 63;
            Assert.AreEqual(63, t.MaxTableSize);
            Assert.AreEqual(0, t.Length);
            Assert.AreEqual(0, t.UsedSize);

            t.MaxTableSize = 4096;
            Assert.AreEqual(4096, t.MaxTableSize);
            InsertItemOfSize(t, "aa", 1000);
            InsertItemOfSize(t, "bb", 1000);
            Assert.AreEqual(2, t.Length);
            Assert.AreEqual(2000, t.UsedSize);

            t.MaxTableSize = 999;
            Assert.AreEqual(999, t.MaxTableSize);
            Assert.AreEqual(0, t.Length);
            Assert.AreEqual(0, t.UsedSize);
        }

        [TestMethod]
        public void TestHpackDynamicTable_ShouldEvictTheOldestItemsIfNewItemsGetInserted()
        {
            var t = new DynamicTable(4000);
            InsertItemOfSize(t, "a", 2000);
            InsertItemOfSize(t, "b", 2000);
            InsertItemOfSize(t, "c", 2000);
            Assert.AreEqual(2, t.Length);
            Assert.AreEqual(4000, t.UsedSize);
            Assert.AreEqual("c", t.GetAt(0).Name);
            Assert.AreEqual("b", t.GetAt(1).Name);

            InsertItemOfSize(t, "d", 3000);
            Assert.AreEqual(1, t.Length);
            Assert.AreEqual(3000, t.UsedSize);
            Assert.AreEqual("d", t.GetAt(0).Name);

            InsertItemOfSize(t, "e", 100);
            InsertItemOfSize(t, "f", 100);
            InsertItemOfSize(t, "g", 100);
            InsertItemOfSize(t, "h", 701);
            Assert.AreEqual(4, t.Length);
            Assert.AreEqual(1001, t.UsedSize);
            Assert.AreEqual("h", t.GetAt(0).Name);
            Assert.AreEqual("e", t.GetAt(3).Name);
        }
    }
}
