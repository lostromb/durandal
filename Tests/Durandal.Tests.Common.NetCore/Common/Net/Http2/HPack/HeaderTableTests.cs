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
    public class HeaderTableTests
    {
        class TableEntryComparer : IEqualityComparer<TableEntry>
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
        public void TestHpackHeaderTable_ShouldBeAbleToSetDynamicTableSize()
        {
            var t = new HeaderTable(2001);
            Assert.AreEqual(2001, t.MaxDynamicTableSize);
            t.MaxDynamicTableSize = 300;
            Assert.AreEqual(300, t.MaxDynamicTableSize);
        }

        [TestMethod]
        public void TestHpackHeaderTable_ShouldReturnItemsFromStaticTable()
        {
            var t = new HeaderTable(400);
            var item = t.GetAt(1);
            AssertAreEqual(
                new TableEntry { Name = ":authority", NameLen = 10, Value = "", ValueLen = 0 },
                item, ec);
            item = t.GetAt(2);
            AssertAreEqual(
                new TableEntry { Name = ":method", NameLen = 7, Value = "GET", ValueLen = 3 },
                item, ec);
            item = t.GetAt(61);
            AssertAreEqual(
                new TableEntry { Name = "www-authenticate", NameLen = 16, Value = "", ValueLen = 0 },
                item, ec);
        }

        [TestMethod]
        public void TestHpackHeaderTable_ShouldReturnItemsFromDynamicTable()
        {
            var t = new HeaderTable(400);
            t.Insert("a", 1, "b", 2);
            t.Insert("c", 3, "d", 4);
            var item = t.GetAt(62);
            AssertAreEqual(
                new TableEntry { Name = "c", NameLen = 3, Value = "d", ValueLen = 4 },
                item, ec);
            item = t.GetAt(63);
            AssertAreEqual(
                new TableEntry { Name = "a", NameLen = 1, Value = "b", ValueLen = 2 },
                item, ec);
        }

        [TestMethod]
        public void TestHpackHeaderTable_ShouldThrowWhenIncorrectlyIndexed()
        {
            var t = new HeaderTable(400);
            AssertThrows<IndexOutOfRangeException>(() => t.GetAt(-1));
            AssertThrows<IndexOutOfRangeException>(() => t.GetAt(0));
            t.GetAt(1); // Valid
            t.GetAt(61); // Last valid
            AssertThrows<IndexOutOfRangeException>(() => t.GetAt(62));

            // Put something into the dynamic table and test again
            t.Insert("a", 1, "b", 1);
            t.Insert("a", 1, "b", 1);
            t.GetAt(62);
            t.GetAt(63);
            AssertThrows<IndexOutOfRangeException>(() => t.GetAt(64));
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
    }
}
