using Durandal.Common.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Durandal.Tests.Common.Parser
{
    [TestClass]
    public class ResultTests
    {
        [TestMethod]
        public void TestParser_FailureContainingBracketFormattedSuccessfully()
        {
            var p = Parse.String("xy").Text().XMany().End();
            var r =p.TryParse("x{");
            Assert.IsTrue(r.Message.Contains("unexpected '{'"));
        }

        [TestMethod]
        public void TestParser_FailureShowsNearbyParseResults()
        {
            var p = from a in Parse.Char('x')
                    from b in Parse.Char('y')
                    select string.Format("{0},{1}", a, b);

            var r = p.TryParse("x{");

            const string expectedMessage = @"Parsing failure: unexpected '{'; expected y (Line 1, Column 2); recently consumed: x";

            Assert.AreEqual(expectedMessage, r.ToString());
        }
    }
}
