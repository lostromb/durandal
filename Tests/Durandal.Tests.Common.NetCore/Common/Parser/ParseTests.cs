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
    public class ParseTests
    {
        [TestMethod]
        public void TestParser_Parser_OfChar_AcceptsThatChar()
        {
            AssertParser.SucceedsWithOne(Parse.Char('a').Once(), "a", 'a');
        }

        [TestMethod]
        public void TestParser_Parser_OfChar_AcceptsOnlyOneChar()
        {
            AssertParser.SucceedsWithOne(Parse.Char('a').Once(), "aaa", 'a');
        }

        [TestMethod]
        public void TestParser_Parser_OfChar_DoesNotAcceptNonMatchingChar()
        {
            AssertParser.FailsAt(Parse.Char('a').Once(), "b", 0);
        }

        [TestMethod]
        public void TestParser_Parser_OfChar_DoesNotAcceptEmptyInput()
        {
            AssertParser.Fails(Parse.Char('a').Once(), "");
        }

        [TestMethod]
        public void TestParser_Parser_OfChars_AcceptsAnyOfThoseChars()
        {
            var parser = Parse.Chars('a', 'b', 'c').Once();
            AssertParser.SucceedsWithOne(parser, "a", 'a');
            AssertParser.SucceedsWithOne(parser, "b", 'b');
            AssertParser.SucceedsWithOne(parser, "c", 'c');
        }

        [TestMethod]
        public void TestParser_Parser_OfChars_UsingString_AcceptsAnyOfThoseChars()
        {
            var parser = Parse.Chars("abc").Once();
            AssertParser.SucceedsWithOne(parser, "a", 'a');
            AssertParser.SucceedsWithOne(parser, "b", 'b');
            AssertParser.SucceedsWithOne(parser, "c", 'c');
        }

        [TestMethod]
        public void TestParser_Parser_OfManyChars_AcceptsEmptyInput()
        {
            AssertParser.SucceedsWithAll(Parse.Char('a').Many(), "");
        }

        [TestMethod]
        public void TestParser_Parser_OfManyChars_AcceptsManyChars()
        {
            AssertParser.SucceedsWithAll(Parse.Char('a').Many(), "aaa");
        }

        [TestMethod]
        public void TestParser_Parser_OfAtLeastOneChar_DoesNotAcceptEmptyInput()
        {
            AssertParser.Fails(Parse.Char('a').AtLeastOnce(), "");
        }

        [TestMethod]
        public void TestParser_Parser_OfAtLeastOneChar_AcceptsOneChar()
        {
            AssertParser.SucceedsWithAll(Parse.Char('a').AtLeastOnce(), "a");
        }

        [TestMethod]
        public void TestParser_Parser_OfAtLeastOneChar_AcceptsManyChars()
        {
            AssertParser.SucceedsWithAll(Parse.Char('a').AtLeastOnce(), "aaa");
        }

        [TestMethod]
        public void TestParser_ConcatenatingParsers_ConcatenatesResults()
        {
            var p = Parse.Char('a').Once().Then(a =>
                Parse.Char('b').Once().Select(b => a.Concat(b)));
            AssertParser.SucceedsWithAll(p, "ab");
        }

        [TestMethod]
        public void TestParser_ReturningValue_DoesNotAdvanceInput()
        {
            var p = Parse.Return(1);
            AssertParser.SucceedsWith(p, "abc", n => Assert.AreEqual(1, n));
        }

        [TestMethod]
        public void TestParser_ReturningValue_ReturnsValueAsResult()
        {
            var p = Parse.Return(1);
            var r = p.TryParse("abc");
            Assert.AreEqual(0, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_CanSpecifyParsersUsingQueryComprehensions()
        {
            var p = from a in Parse.Char('a').Once()
                    from bs in Parse.Char('b').Many()
                    from cs in Parse.Char('c').AtLeastOnce()
                    select a.Concat(bs).Concat(cs);

            AssertParser.SucceedsWithAll(p, "abbbc");
        }

        [TestMethod]
        public void TestParser_WhenFirstOptionSucceedsButConsumesNothing_SecondOptionTried()
        {
            var p = Parse.Char('a').Many().XOr(Parse.Char('b').Many());
            AssertParser.SucceedsWithAll(p, "bbb");
        }

        [TestMethod]
        public void TestParser_WithXOr_WhenFirstOptionFailsAndConsumesInput_SecondOptionNotTried()
        {
            var first = Parse.Char('a').Once().Concat(Parse.Char('b').Once());
            var second = Parse.Char('a').Once();
            var p = first.XOr(second);
            AssertParser.FailsAt(p, "a", 1);
        }

        [TestMethod]
        public void TestParser_WithOr_WhenFirstOptionFailsAndConsumesInput_SecondOptionTried()
        {
            var first = Parse.Char('a').Once().Concat(Parse.Char('b').Once());
            var second = Parse.Char('a').Once();
            var p = first.Or(second);
            AssertParser.SucceedsWithAll(p, "a");
        }

        [TestMethod]
        public void TestParser_ParsesString_AsSequenceOfChars()
        {
            var p = Parse.String("abc");
            AssertParser.SucceedsWithAll(p, "abc");
        }

        static readonly Parser<IEnumerable<char>> ASeq =
            (from first in Parse.Ref(() => ASeq)
             from comma in Parse.Char(',')
             from rest in Parse.Char('a').Once()
             select first.Concat(rest))
            .Or(Parse.Char('a').Once());

        [TestMethod]
        public void TestParser_DetectsLeftRecursion()
        {
            try
            {
                ASeq.TryParse("a,a,a");
                Assert.Fail("Expected a ParseException");
            }
            catch (ParseException) { }
        }

        static readonly Parser<IEnumerable<char>> ABSeq =
            (from first in Parse.Ref(() => BASeq)
             from rest in Parse.Char('a').Once()
             select first.Concat(rest))
            .Or(Parse.Char('a').Once());

        static readonly Parser<IEnumerable<char>> BASeq =
            (from first in Parse.Ref(() => ABSeq)
             from rest in Parse.Char('b').Once()
             select first.Concat(rest))
            .Or(Parse.Char('b').Once());

        [TestMethod]
        public void TestParser_DetectsMutualLeftRecursion()
        {
            try
            {
                ABSeq.End().TryParse("baba");
                Assert.Fail("Expected a ParseException");
            }
            catch (ParseException) { }
        }

        [TestMethod]
        public void TestParser_WithMany_WhenLastElementFails_FailureReportedAtLastElement()
        {
            var ab = from a in Parse.Char('a')
                     from b in Parse.Char('b')
                     select "ab";

            var p = ab.Many().End();

            AssertParser.FailsAt(p, "ababaf", 4);
        }

        [TestMethod]
        public void TestParser_WithXMany_WhenLastElementFails_FailureReportedAtLastElement()
        {
            var ab = from a in Parse.Char('a')
                     from b in Parse.Char('b')
                     select "ab";

            var p = ab.XMany().End();

            AssertParser.FailsAt(p, "ababaf", 5);
        }

        [TestMethod]
        public void TestParser_ExceptStopsConsumingInputWhenExclusionParsed()
        {
            var exceptAa = Parse.AnyChar.Except(Parse.String("aa")).Many().Text();
            AssertParser.SucceedsWith(exceptAa, "abcaab", r => Assert.AreEqual("abc", r));
        }

        [TestMethod]
        public void TestParser_UntilProceedsUntilTheStopConditionIsMetAndReturnsAllButEnd()
        {
            var untilAa = Parse.AnyChar.Until(Parse.String("aa")).Text();
            var r = untilAa.TryParse("abcaab");
            Assert.AreEqual("abc", r.Value);
            Assert.AreEqual(5, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_OptionalParserConsumesInputOnSuccessfulMatch()
        {
            var optAbc = Parse.String("abc").Text().Optional();
            var r = optAbc.TryParse("abcd");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(3, r.Remainder.Position);
            Assert.IsTrue(r.Value.IsDefined);
            Assert.AreEqual("abc", r.Value.Get());
        }

        [TestMethod]
        public void TestParser_OptionalParserDoesNotConsumeInputOnFailedMatch()
        {
            var optAbc = Parse.String("abc").Text().Optional();
            var r = optAbc.TryParse("d");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(0, r.Remainder.Position);
            Assert.IsTrue(r.Value.IsEmpty);
        }

#if SPRACHE2
        [TestMethod]
        public void TestParser_XOptionalParserConsumesInputOnSuccessfulMatch()
        {
            var optAbc = Parse.String("abc").Text().XOptional();
            var r = optAbc.TryParse("abcd");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(3, r.Remainder.Position);
            Assert.IsTrue(r.Value.IsDefined);
            Assert.AreEqual("abc", r.Value.Get());
        }

        [TestMethod]
        public void TestParser_XOptionalParserDoesNotConsumeInputOnFailedMatch()
        {
            var optAbc = Parse.String("abc").Text().XOptional();
            var r = optAbc.TryParse("d");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(0, r.Remainder.Position);
            Assert.IsTrue(r.Value.IsEmpty);
        }

        [TestMethod]
        public void TestParser_XOptionalParserFailsOnPartialMatch()
        {
            var optAbc = Parse.String("abc").Text().XOptional();
            AssertParser.FailsAt(optAbc, "abd", 2);
            AssertParser.FailsAt(optAbc, "aa", 1);
        }
#endif

        [TestMethod]
        public void TestParser_RegexParserConsumesInputOnSuccessfulMatch()
        {
            var digits = Parse.Regex(@"\d+");
            var r = digits.TryParse("123d");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual("123", r.Value);
            Assert.AreEqual(3, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_RegexParserDoesNotConsumeInputOnFailedMatch()
        {
            var digits = Parse.Regex(@"\d+");
            var r = digits.TryParse("d123");
            Assert.IsFalse(r.WasSuccessful);
            Assert.AreEqual(0, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_RegexMatchParserConsumesInputOnSuccessfulMatch()
        {
            var digits = Parse.RegexMatch(@"\d(\d*)");
            var r = digits.TryParse("123d");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual("123", r.Value.Value);
            Assert.AreEqual("23", r.Value.Groups[1].Value);
            Assert.AreEqual(3, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_RegexMatchParserDoesNotConsumeInputOnFailedMatch()
        {
            var digits = Parse.RegexMatch(@"\d+");
            var r = digits.TryParse("d123");
            Assert.IsFalse(r.WasSuccessful);
            Assert.AreEqual(0, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_PositionedParser()
        {
            var pos = (from s in Parse.String("winter").Text()
                       select new PosAwareStr { Value = s })
                       .Positioned();
            var r = pos.TryParse("winter");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(0, r.Value.Pos.Pos);
            Assert.AreEqual(6, r.Value.Length);
        }

        [TestMethod]
        public void TestParser_XAtLeastOnceParser_WhenLastElementFails_FailureReportedAtLastElement()
        {
            var ab = Parse.String("ab").Text();
            var p = ab.XAtLeastOnce().End();
            AssertParser.FailsAt(p, "ababaf", 5);
        }

        [TestMethod]
        public void TestParser_XAtLeastOnceParser_WhenFirstElementFails_FailureReportedAtFirstElement()
        {
            var ab = Parse.String("ab").Text();
            var p = ab.XAtLeastOnce().End();
            AssertParser.FailsAt(p, "d", 0);
        }

        [TestMethod]
        public void TestParser_NotParserConsumesNoInputOnFailure()
        {
            var notAb = Parse.String("ab").Text().Not();
            AssertParser.FailsAt(notAb, "abc", 0);
        }

        [TestMethod]
        public void TestParser_NotParserConsumesNoInputOnSuccess()
        {
            var notAb = Parse.String("ab").Text().Not();
            var r = notAb.TryParse("d");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(0, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_IgnoreCaseParser()
        {
            var ab = Parse.IgnoreCase("ab").Text();
            AssertParser.SucceedsWith(ab, "Ab", m => Assert.AreEqual("Ab", m));
        }

        [TestMethod]
        public void TestParser_RepeatParserConsumeInputOnSuccessfulMatch()
        {
            var repeated = Parse.Char('a').Repeat(3);
            var r = repeated.TryParse("aaabbb");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(3, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_RepeatParserDoesntConsumeInputOnFailedMatch()
        {
            var repeated = Parse.Char('a').Repeat(3);
            var r = repeated.TryParse("bbbaaa");
            Assert.IsTrue(!r.WasSuccessful);
            Assert.AreEqual(0, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_RepeatParserCanParseWithCountOfZero()
        {
            var repeated = Parse.Char('a').Repeat(0);
            var r = repeated.TryParse("bbb");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(0, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_RepeatParserCanParseAMinimumNumberOfValues()
        {
            var repeated = Parse.Char('a').Repeat(4, 5);

            // Test failure.
            var r = repeated.TryParse("aaa");
            Assert.IsFalse(r.WasSuccessful);
            Assert.AreEqual(0, r.Remainder.Position);

            // Test success.
            r = repeated.TryParse("aaaa");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(4, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_RepeatParserCanParseAMaximumNumberOfValues()
        {
            var repeated = Parse.Char('a').Repeat(4, 5);

            var r = repeated.TryParse("aaaa");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(4, r.Remainder.Position);

            r = repeated.TryParse("aaaaa");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(5, r.Remainder.Position);

            r = repeated.TryParse("aaaaaa");
            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual(5, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_RepeatParserErrorMessagesAreReadable()
        {
            var repeated = Parse.Char('a').Repeat(4, 5);

            var expectedMessage = "Parsing failure: Unexpected 'end of input'; expected 'a' between 4 and 5 times, but found 3";
            //var expectedColumnPosition = 1;

            try
            {
                var r = repeated.Parse("aaa");
            }
            catch (ParseException ex)
            {
                Assert.IsTrue(ex.Message.StartsWith(expectedMessage));
#if SPRACHE2
                Assert.AreEqual(expectedColumnPosition, ex.Position.Column);
#endif
            }
        }

        [TestMethod]
        [Ignore] // need to upgrade sprache dependencies
        public void TestParser_RepeatExactlyParserErrorMessagesAreReadable()
        {
            var repeated = Parse.Char('a').Repeat(4);

            var expectedMessage = "Parsing failure: Unexpected 'end of input'; expected 'a' 4 times, but found 3";
            //var expectedColumnPosition = 1;

            try
            {
                var r = repeated.Parse("aaa");
            }
            catch (ParseException ex)
            {
                Assert.IsTrue(ex.Message.StartsWith(expectedMessage));
#if SPRACHE2
                Assert.AreEqual(expectedColumnPosition, ex.Position.Column);
#endif
            }
        }

#if SPRACHE2
        [TestMethod]
        public void TestParser_RepeatParseWithOnlyMinimum()
        {
            var repeated = Parse.Char('a').Repeat(4, null);


            Assert.AreEqual(4, repeated.TryParse("aaaa").Remainder.Position);
            Assert.AreEqual(7, repeated.TryParse("aaaaaaa").Remainder.Position);
            Assert.AreEqual(10, repeated.TryParse("aaaaaaaaaa").Remainder.Position);

            try
            {
                repeated.Parse("aaa");
            }
            catch (ParseException ex)
            {
                Assert.IsTrue(ex.Message.StartsWith("Parsing failure: Unexpected 'end of input'; expected 'a' minimum 4 times, but found 3"));
            }
        }

        [TestMethod]
        public void TestParser_RepeatParseWithOnlyMaximum()
        {
            var repeated = Parse.Char('a').Repeat(null, 6);

            Assert.AreEqual(4, repeated.TryParse("aaaa").Remainder.Position);
            Assert.AreEqual(6, repeated.TryParse("aaaaaa").Remainder.Position);
            Assert.AreEqual(6, repeated.TryParse("aaaaaaaaaa").Remainder.Position);
            Assert.AreEqual(0, repeated.TryParse("").Remainder.Position);
        }
#endif

        [TestMethod]
        public void TestParser_CanParseSequence()
        {
            var sequence = Parse.Char('a').DelimitedBy(Parse.Char(','));
            var r = sequence.TryParse("a,a,a");
            Assert.IsTrue(r.WasSuccessful);
            Assert.IsTrue(r.Remainder.AtEnd);
        }

#if SPRACHE2
        [TestMethod]
        public void TestParser_DelimitedWithMinimumAndMaximum()
        {
            var sequence = Parse.Char('a').DelimitedBy(Parse.Char(','), 3, 4);
            Assert.AreEqual(3, sequence.TryParse("a,a,a").Value.Count());
            Assert.AreEqual(4, sequence.TryParse("a,a,a,a").Value.Count());
            Assert.AreEqual(4, sequence.TryParse("a,a,a,a,a").Value.Count());
            Assert.Throws<ParseException>(() => sequence.Parse("a,a"));
        }

        [TestMethod]
        public void TestParser_DelimitedWithMinimum()
        {
            var sequence = Parse.Char('a').DelimitedBy(Parse.Char(','), 3, null);
            Assert.AreEqual(3, sequence.TryParse("a,a,a").Value.Count());
            Assert.AreEqual(4, sequence.TryParse("a,a,a,a").Value.Count());
            Assert.AreEqual(5, sequence.TryParse("a,a,a,a,a").Value.Count());
            Assert.Throws<ParseException>(() => sequence.Parse("a,a"));
        }

        [TestMethod]
        public void TestParser_DelimitedWithMaximum()
        {
            var sequence = Parse.Char('a').DelimitedBy(Parse.Char(','), null, 4);
            Assert.AreEqual(1, sequence.TryParse("a").Value.Count());
            Assert.AreEqual(3, sequence.TryParse("a,a,a").Value.Count());
            Assert.AreEqual(4, sequence.TryParse("a,a,a,a").Value.Count());
            Assert.AreEqual(4, sequence.TryParse("a,a,a,a,a").Value.Count());
        }
#endif

        [TestMethod]
        public void TestParser_FailGracefullyOnSequence()
        {
            var sequence = Parse.Char('a').XDelimitedBy(Parse.Char(','));
            AssertParser.FailsWith(sequence, "a,a,b", result =>
            {
                Assert.IsTrue(result.Message.Contains("unexpected 'b'"));
                Assert.IsTrue(result.Expectations.Contains("a"));
            });
        }

        [TestMethod]
        public void TestParser_CanParseContained()
        {
            var parser = Parse.Char('a').Contained(Parse.Char('['), Parse.Char(']'));
            var r = parser.TryParse("[a]");
            Assert.IsTrue(r.WasSuccessful);
            Assert.IsTrue(r.Remainder.AtEnd);
        }

#if SPRACHE2
        [TestMethod]
        public void TestParser_TextSpanParserReturnsTheSpanOfTheParsedValue()
        {
            var parser =
                from leading in Parse.WhiteSpace.Many()
                from span in Parse.Identifier(Parse.Letter, Parse.LetterOrDigit).Span()
                from trailing in Parse.WhiteSpace.Many()
                select span;

            var r = parser.TryParse("  Hello!");
            Assert.IsTrue(r.WasSuccessful);
            Assert.IsFalse(r.Remainder.AtEnd);

            var id = r.Value;
            Assert.AreEqual("Hello", id.Value);
            Assert.AreEqual(5, id.Length);

            Assert.AreEqual(2, id.Start.Pos);
            Assert.AreEqual(1, id.Start.Line);
            Assert.AreEqual(3, id.Start.Column);

            Assert.AreEqual(7, id.End.Pos);
            Assert.AreEqual(1, id.End.Line);
            Assert.AreEqual(8, id.End.Column);
        }

        [TestMethod]
        public void TestParser_PreviewParserAlwaysSucceedsLikeOptionalParserButDoesntConsumeAnyInput()
        {
            var parser = Parse.Char('a').XAtLeastOnce().Text().Token().Preview();
            var r = parser.TryParse("   aaa   ");

            Assert.IsTrue(r.WasSuccessful);
            Assert.AreEqual("aaa", r.Value.GetOrDefault());
            Assert.AreEqual(0, r.Remainder.Position);

            r = parser.TryParse("   bbb   ");
            Assert.IsTrue(r.WasSuccessful);
            Assert.Null(r.Value.GetOrDefault());
            Assert.AreEqual(0, r.Remainder.Position);
        }

        [TestMethod]
        public void TestParser_PreviewParserIsSimilarToPositiveLookaheadInRegex()
        {
            var parser =
                from test in Parse.String("test").Token().Preview()
                from testMethod in Parse.String("testMethod").Token().Text()
                select testMethod;

            var result = parser.Parse("   testMethod  ");
            Assert.AreEqual("testMethod", result);
        }

        [TestMethod]
        public void TestParser_CommentedParserConsumesWhiteSpaceLikeTokenParserAndAddsLeadingAndTrailingComments()
        {
            var parser = Parse.Identifier(Parse.Letter, Parse.LetterOrDigit).Commented();

            // whitespace only
            var result = parser.Parse("    \t hello123   \t\r\n  ");
            Assert.AreEqual("hello123", result.Value);
            Assert.Empty(result.LeadingComments);
            Assert.Empty(result.TrailingComments);

            // leading comments
            result = parser.End().Parse(" /* My identifier */ world321   ");
            Assert.AreEqual("world321", result.Value);
            Assert.Single(result.LeadingComments);
            Assert.Empty(result.TrailingComments);
            Assert.AreEqual("My identifier", result.LeadingComments.Single().Trim());

            // trailing comments
            result = parser.End().Parse("    \t hello123   // what's that? ");
            Assert.AreEqual("hello123", result.Value);
            Assert.Empty(result.LeadingComments);
            Assert.Single(result.TrailingComments);
            Assert.AreEqual("what's that?", result.TrailingComments.Single().Trim());
        }

        [TestMethod]
        public void TestParser_CommentedParserConsumesAllLeadingCommentsAndOnlyOneTrailingCommentIfItIsOnTheSameLine()
        {
            var parser = Parse.Identifier(Parse.Letter, Parse.LetterOrDigit).Commented();

            // leading and trailing comments
            var result = parser.Parse(@" // leading comments!
            /* more leading comments! */

            helloWorld // trailing comments!

            // more trailing comments! (these comments don't belong to the parsed value)");

            Assert.AreEqual("helloWorld", result.Value);
            Assert.AreEqual(2, result.LeadingComments.Count());
            Assert.AreEqual("leading comments!", result.LeadingComments.First().Trim());
            Assert.AreEqual("more leading comments!", result.LeadingComments.Last().Trim());
            Assert.Single(result.TrailingComments);
            Assert.AreEqual("trailing comments!", result.TrailingComments.First().Trim());

            // multiple leading and trailing comments
            result = parser.Parse(@" // leading comments!

            /* multiline leading comments
            this is the second line */

            test321

            // trailing comments! these comments doesn't belong to the parsed value
            /* --==-- */");
            Assert.AreEqual("test321", result.Value);
            Assert.AreEqual(2, result.LeadingComments.Count());
            Assert.AreEqual("leading comments!", result.LeadingComments.First().Trim());
            Assert.StartsWith("multiline leading comments", result.LeadingComments.Last().Trim());
            Assert.EndsWith("this is the second line", result.LeadingComments.Last().Trim());
            Assert.Empty(result.TrailingComments);
        }

        [TestMethod]
        public void TestParser_CommentedParserAcceptsMultipleTrailingCommentsAsLongAsTheyStartOnTheSameLine()
        {
            var parser = Parse.Identifier(Parse.Letter, Parse.LetterOrDigit).Commented();

            // trailing comments
            var result = parser.Parse("    \t hello123   /* one */ /* two */ /* " + @"
                three */ // this is not a trailing comment
                // neither this");
            Assert.AreEqual("hello123", result.Value);
            Assert.IsFalse(result.LeadingComments.Any());
            Assert.IsTrue(result.TrailingComments.Any());

            var trailing = result.TrailingComments.ToArray();
            Assert.AreEqual(3, trailing.Length);
            Assert.AreEqual("one", trailing[0].Trim());
            Assert.AreEqual("two", trailing[1].Trim());
            Assert.AreEqual("three", trailing[2].Trim());

            // leading and trailing comments
            result = parser.Parse(@" // leading comments!
            /* more leading comments! */
            helloWorld /* one*/ // two!
            // more trailing comments! (that don't belong to the parsed value)");
            Assert.AreEqual("helloWorld", result.Value);
            Assert.AreEqual(2, result.LeadingComments.Count());
            Assert.AreEqual("leading comments!", result.LeadingComments.First().Trim());
            Assert.AreEqual("more leading comments!", result.LeadingComments.Last().Trim());

            trailing = result.TrailingComments.ToArray();
            Assert.AreEqual(2, trailing.Length);
            Assert.AreEqual("one", trailing[0].Trim());
            Assert.AreEqual("two!", trailing[1].Trim());
        }
#endif

        //        [TestMethod]
        //        public void TestParser_CommentedParserAcceptsCustomizedCommentParser()
        //        {
        //            var cp = new CommentParser("#", "{", "}", "\n");
        //            var parser = Parse.Identifier(Parse.Letter, Parse.LetterOrDigit).Commented(cp);

        //            // leading and trailing comments
        //            var result = parser.Parse(@" # leading comments!
        //            { more leading comments! }

        //            helloWorld # trailing comments!

        //# more trailing comments! (these comments don't belong to the parsed value)");

        //            Assert.AreEqual("helloWorld", result.Value);
        //            Assert.AreEqual(2, result.LeadingComments.Count());
        //            Assert.AreEqual("leading comments!", result.LeadingComments.First().Trim());
        //            Assert.AreEqual("more leading comments!", result.LeadingComments.Last().Trim());
        //            Assert.Single(result.TrailingComments);
        //            Assert.AreEqual("trailing comments!", result.TrailingComments.First().Trim());

        //            // multiple leading and trailing comments
        //            result = parser.Parse(@" # leading comments!

        //            { multiline leading comments
        //            this is the second line }

        //            test321

        //# trailing comments! these comments doesn't belong to the parsed value
        //            { --==-- }");
        //            Assert.AreEqual("test321", result.Value);
        //            Assert.AreEqual(2, result.LeadingComments.Count());
        //            Assert.AreEqual("leading comments!", result.LeadingComments.First().Trim());
        //            Assert.StartsWith("multiline leading comments", result.LeadingComments.Last().Trim());
        //            Assert.EndsWith("this is the second line", result.LeadingComments.Last().Trim());
        //            Assert.Empty(result.TrailingComments);
        //        }
    }
}
