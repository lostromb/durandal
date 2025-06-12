using Durandal.Common.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Durandal.Tests.Common.Parser
{
    [TestClass]
    public class StarDateTest
    {
        static readonly Parser<DateTime> StarTrek2009_StarDate =
            from year in Parse.Digit.Many().Text()
            from delimiter in Parse.Char('.')
            from dayOfYear in Parse.Digit.Repeat(1, 3).Text().End()
            select new DateTime(int.Parse(year), 1, 1).AddDays(int.Parse(dayOfYear) - 1);

        [TestMethod]
        public void TestParser_ItIsPossibleToParseAStarDate()
        {
            Assert.AreEqual(new DateTime(2259, 2, 24), StarTrek2009_StarDate.Parse("2259.55"));
        }

        [TestMethod]
        public void TestParser_InvalidStarDatesAreNotParsed()
        {
            try
            {
                var date = StarTrek2009_StarDate.Parse("2259.4000");
                Assert.Fail("Expected a ParseException");
            }
            catch (ParseException) { }
        }
    }
}
