using Durandal.Common.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Durandal.Tests.Common.Parser;

namespace Durandal.Tests.Common.Parser
{
    [TestClass]
    public class InputTests
    {
        [TestMethod]
        public void TestParser_InputsOnTheSameString_AtTheSamePosition_AreEqual()
        {
            var s = "Nada";
            var p = 2;
            var i1 = ConstructInput(s, p);
            var i2 = ConstructInput(s, p);
            Assert.AreEqual(i1, i2);
            Assert.IsTrue(i1 == i2);
        }

        [TestMethod]
        public void TestParser_InputsOnTheSameString_AtDifferentPositions_AreNotEqual()
        {
            var s = "Nada";
            var i1 = ConstructInput(s, 1);
            var i2 = ConstructInput(s, 2);
            Assert.AreNotEqual(i1, i2);
            Assert.IsTrue(i1 != i2);
        }

        [TestMethod]
        public void TestParser_InputsOnDifferentStrings_AtTheSamePosition_AreNotEqual()
        {
            var p = 2;
            var i1 = ConstructInput("Algo", p);
            var i2 = ConstructInput("Nada", p);
            Assert.AreNotEqual(i1, i2);
        }

        [TestMethod]
        public void TestParser_InputsAtEnd_CannotAdvance()
        {
            var i = ConstructInput("", 0);
            Assert.IsTrue(i.AtEnd);
            try
            {
                i.Advance();
                Assert.Fail("Should have thrown InvalidOperationException");
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public void TestParser_AdvancingInput_MovesForwardOneCharacter()
        {
            var i = ConstructInput("abc", 1);
            var j = i.Advance();
            Assert.AreEqual(2, j.Position);
        }

        [TestMethod]
        public void TestParser_CurrentCharacter_ReflectsPosition()
        {
            var i = ConstructInput("abc", 1);
            Assert.AreEqual('b', i.Current);
        }

        [TestMethod]
        public void TestParser_ANewInput_WillBeAtFirstCharacter()
        {
            var i = new Input("abc");
            Assert.AreEqual(0, i.Position);
        }

        [TestMethod]
        public void TestParser_AdvancingInput_IncreasesColumnNumber()
        {
            var i = ConstructInput("abc", 1);
            var j = i.Advance();
            Assert.AreEqual(2, j.Column);
        }
        [TestMethod]
        public void TestParser_AdvancingInputAtEOL_IncreasesLineNumber()
        {
            var i = new Input("\nabc");
            var j = i.Advance();
            Assert.AreEqual(2, j.Line);
        }

        [TestMethod]
        public void TestParser_AdvancingInputAtEOL_ResetsColumnNumber()
        {
            var i = new Input("\nabc");
            var j = i.Advance();
            Assert.AreEqual(2, j.Line);
            Assert.AreEqual(1, j.Column);
        }

        [TestMethod]
        public void TestParser_LineCountingSmokeTest()
        {
            IInput i = new Input("abc\ndef");
            Assert.AreEqual(0, i.Position);
            Assert.AreEqual(1, i.Line);
            Assert.AreEqual(1, i.Column);

            i = i.AdvanceAssert((a, b) =>
            {
                Assert.AreEqual(1, b.Position);
                Assert.AreEqual(1, b.Line);
                Assert.AreEqual(2, b.Column);
            });
            i = i.AdvanceAssert((a, b) =>
            {
                Assert.AreEqual(2, b.Position);
                Assert.AreEqual(1, b.Line);
                Assert.AreEqual(3, b.Column);
            });
            i = i.AdvanceAssert((a, b) =>
            {
                Assert.AreEqual(3, b.Position);
                Assert.AreEqual(1, b.Line);
                Assert.AreEqual(4, b.Column);
            });
            i = i.AdvanceAssert((a, b) =>
            {
                Assert.AreEqual(4, b.Position);
                Assert.AreEqual(2, b.Line);
                Assert.AreEqual(1, b.Column);
            });
            i = i.AdvanceAssert((a, b) =>
            {
                Assert.AreEqual(5, b.Position);
                Assert.AreEqual(2, b.Line);
                Assert.AreEqual(2, b.Column);
            });
            i = i.AdvanceAssert((a, b) =>
            {
                Assert.AreEqual(6, b.Position);
                Assert.AreEqual(2, b.Line);
                Assert.AreEqual(3, b.Column);
            });
        }

        private static Input ConstructInput(string s, int p)
        {
            return new Input(s, p, 1, 1);
        }
    }
}
