using Durandal.Common.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Durandal.Tests.Common.Parser
{
    [TestClass]
    public class DecimalTests : IDisposable
    {
        private static readonly Parser<string> DecimalParser = Parse.Decimal.End();
        private static readonly Parser<string> DecimalInvariantParser = Parse.DecimalInvariant.End();

        private CultureInfo _previousCulture;

        public DecimalTests()
        {
            _previousCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
        }

        [TestMethod]
        public void TestParser_LeadingDigits()
        {
            Assert.AreEqual("12.23", DecimalParser.Parse("12.23"));
        }

        [TestMethod]
        public void TestParser_NoLeadingDigits()
        {
            Assert.AreEqual(".23", DecimalParser.Parse(".23"));
        }

        [TestMethod]
        public void TestParser_TwoPeriods()
        {
            try
            {
                DecimalParser.Parse("1.2.23");
                Assert.Fail("Expected a ParseException");
            }
            catch (ParseException) { }
        }

        [TestMethod]
        public void TestParser_Letters()
        {
            try
            {
                DecimalParser.Parse("1A.5");
                Assert.Fail("Expected a ParseException");
            }
            catch (ParseException) { }
        }

        [TestMethod]
        public void TestParser_LeadingDigitsInvariant()
        {
            Assert.AreEqual("12.23", DecimalInvariantParser.Parse("12.23"));
        }

    }
}
