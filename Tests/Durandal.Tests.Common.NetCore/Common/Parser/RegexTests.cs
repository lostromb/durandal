using Durandal.Common.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Durandal.Tests.Common.Parser
{
    /// <summary>
    /// These tests exist in order to verify that the modification that is applied to
    /// the regex passed to every call to the <see cref="Parse.Regex(string,string)"/>
    /// or <see cref="Parse.Regex(Regex,string)"/> methods does not change the results
    /// in any way.
    /// </summary>
    [TestClass]
    public class RegexTests
    {
        private const string _startsWithCarrot = "^([a-z]{3})([0-9]{3})$";
        private const string _alternation = "(this)|(that)|(the other)";

        private static readonly MethodInfo _optimizeRegexMethod = typeof(Parse).GetMethod("OptimizeRegex", BindingFlags.NonPublic | BindingFlags.Static);

        [TestMethod]
        public void TestParser_OptimizedRegexIsNotSuccessfulWhenTheMatchIsNotAtTheBeginningOfTheInput()
        {
            var regexOriginal = new Regex("[a-z]+");
            var regexOptimized = OptimizeRegex(regexOriginal);

            const string input = "123abc";

            Assert.IsTrue(regexOriginal.Match(input).Success);
            Assert.IsFalse(regexOptimized.Match(input).Success);
        }

        [TestMethod]
        public void TestParser_OptimizedRegexIsSuccessfulWhenTheMatchIsAtTheBeginningOfTheInput()
        {
            var regexOriginal = new Regex("[a-z]+");
            var regexOptimized = OptimizeRegex(regexOriginal);

            const string input = "abc123";

            Assert.IsTrue(regexOriginal.Match(input).Success);
            Assert.IsTrue(regexOptimized.Match(input).Success);
        }

        [TestMethod]
        public void TestParser_RegexOptimizationDoesNotChangeRegexBehavior()
        {
            RegexOptimizationDoesNotChangeRegexBehavior(_startsWithCarrot, RegexOptions.None, "abc123");                 // TestName = "Starts with ^, no options, success"
            RegexOptimizationDoesNotChangeRegexBehavior(_startsWithCarrot, RegexOptions.ExplicitCapture, "abc123");      // TestName = "Starts with ^, explicit capture, success"
            RegexOptimizationDoesNotChangeRegexBehavior(_startsWithCarrot, RegexOptions.None, "123abc");                 // TestName = "Starts with ^, no options, failure"
            RegexOptimizationDoesNotChangeRegexBehavior(_startsWithCarrot, RegexOptions.ExplicitCapture, "123abc");      // TestName = "Starts with ^, explicit capture, failure"
            RegexOptimizationDoesNotChangeRegexBehavior(_alternation, RegexOptions.None, "abc123");                      // TestName = "Alternation, no options, success"
            RegexOptimizationDoesNotChangeRegexBehavior(_alternation, RegexOptions.ExplicitCapture, "abc123");           // TestName = "Alternation, explicit capture, success"
            RegexOptimizationDoesNotChangeRegexBehavior(_alternation, RegexOptions.None, "that");                        // TestName = "Alternation, no options, failure"
            RegexOptimizationDoesNotChangeRegexBehavior(_alternation, RegexOptions.ExplicitCapture, "that");             // TestName = "Alternation, explicit capture, failure"
        }

        private static void RegexOptimizationDoesNotChangeRegexBehavior(string pattern, RegexOptions options, string input)
        {
            var regexOriginal = new Regex(pattern, options);
            var regexOptimized = OptimizeRegex(regexOriginal);

            var matchOriginal = regexOriginal.Match(input);
            var matchModified = regexOptimized.Match(input);

            Assert.AreEqual(matchOriginal.Success, matchModified.Success);
            Assert.AreEqual(matchOriginal.Value, matchModified.Value);
            Assert.AreEqual(matchOriginal.Groups.Count, matchModified.Groups.Count);

            for (int i = 0; i < matchModified.Groups.Count; i++)
            {
                Assert.AreEqual(matchOriginal.Groups[i].Success, matchModified.Groups[i].Success);
                Assert.AreEqual(matchOriginal.Groups[i].Value, matchModified.Groups[i].Value);
            }
        }

        /// <summary>
        /// Calls the <see cref="Parse.OptimizeRegex"/> method via reflection.
        /// </summary>
        private static Regex OptimizeRegex(Regex regex)
        {
            // Reflection isn't the best way of verifying behavior,
            // but cluttering the public api sucks worse.

            if (_optimizeRegexMethod == null)
            {
                throw new Exception("Unable to locate a private static method named " +
                                    "\"OptimizeRegex\" in the Parse class. Has it been renamed?");
            }

            var optimizedRegex = (Regex)_optimizeRegexMethod.Invoke(null, new object[] { regex });
            return optimizedRegex;
        }
    }
}
