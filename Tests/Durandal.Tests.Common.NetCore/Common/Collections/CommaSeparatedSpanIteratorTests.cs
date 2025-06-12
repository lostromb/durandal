using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Collections
{
    [TestClass]
    public class CommaSeparatedSpanIteratorTests
    {
        [TestMethod]
        public void TestCSVSpanEnumeratorNullInput()
        {
            CommaSeparatedStringSpanEnumerator iter = new CommaSeparatedStringSpanEnumerator((string)null);
            Assert.AreEqual(0, iter.Current.Length);
            Assert.IsFalse(iter.MoveNext());
            Assert.IsFalse(iter.MoveNext());
            Assert.IsTrue(iter.Reset());
            Assert.IsFalse(iter.MoveNext());
            Assert.IsFalse(iter.MoveNext());
        }

        [TestMethod]
        public void TestCSVSpanEnumeratorEmptyInput()
        {
            CommaSeparatedStringSpanEnumerator iter = new CommaSeparatedStringSpanEnumerator(string.Empty.AsMemory());
            Assert.AreEqual(0, iter.Current.Length);
            Assert.IsFalse(iter.MoveNext());
            Assert.IsFalse(iter.MoveNext());
            Assert.IsTrue(iter.Reset());
            Assert.IsFalse(iter.MoveNext());
            Assert.IsFalse(iter.MoveNext());
        }

        [TestMethod]
        public void TestCSVSpanEnumeratorSingleString()
        {
            CommaSeparatedStringSpanEnumerator iter = new CommaSeparatedStringSpanEnumerator("single test string".AsMemory());
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue("single test string".AsSpan().SequenceEqual(iter.Current));
            Assert.IsFalse(iter.MoveNext());
            Assert.IsTrue(iter.Reset());
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue("single test string".AsSpan().SequenceEqual(iter.Current));
            Assert.IsFalse(iter.MoveNext());
        }

        [TestMethod]
        public void TestCSVSpanEnumeratorTwoStrings()
        {
            CommaSeparatedStringSpanEnumerator iter = new CommaSeparatedStringSpanEnumerator("one,two".AsMemory());
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue("one".AsSpan().SequenceEqual(iter.Current));
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue("two".AsSpan().SequenceEqual(iter.Current));
            Assert.IsFalse(iter.MoveNext());
        }

        [TestMethod]
        public void TestCSVSpanEnumeratorAdjacentCommas()
        {
            CommaSeparatedStringSpanEnumerator iter = new CommaSeparatedStringSpanEnumerator("one,,two".AsMemory());
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue("one".AsSpan().SequenceEqual(iter.Current));
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue(string.Empty.AsSpan().SequenceEqual(iter.Current));
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue("two".AsSpan().SequenceEqual(iter.Current));
            Assert.IsFalse(iter.MoveNext());
        }

        [TestMethod]
        public void TestCSVSpanEnumeratorNothingButCommas()
        {
            CommaSeparatedStringSpanEnumerator iter = new CommaSeparatedStringSpanEnumerator(",,,,".AsMemory());
            for (int c = 0; c < 5; c++)
            {
                Assert.IsTrue(iter.MoveNext());
                Assert.IsTrue(string.Empty.AsSpan().SequenceEqual(iter.Current));
            }

            Assert.IsFalse(iter.MoveNext());
            Assert.IsFalse(iter.MoveNext());
        }

        [TestMethod]
        public void TestCSVSpanEnumeratorBeginWithComma()
        {
            CommaSeparatedStringSpanEnumerator iter = new CommaSeparatedStringSpanEnumerator(",one,two".AsMemory());
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue(string.Empty.AsSpan().SequenceEqual(iter.Current));
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue("one".AsSpan().SequenceEqual(iter.Current));
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue("two".AsSpan().SequenceEqual(iter.Current));
            Assert.IsFalse(iter.MoveNext());
        }

        [TestMethod]
        public void TestCSVSpanEnumeratorEndWithComma()
        {
            CommaSeparatedStringSpanEnumerator iter = new CommaSeparatedStringSpanEnumerator("one,two,".AsMemory());
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue("one".AsSpan().SequenceEqual(iter.Current));
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue("two".AsSpan().SequenceEqual(iter.Current));
            Assert.IsTrue(iter.MoveNext());
            Assert.IsTrue(string.Empty.AsSpan().SequenceEqual(iter.Current));
            Assert.IsFalse(iter.MoveNext());
            Assert.IsFalse(iter.MoveNext());
        }
    }
}
