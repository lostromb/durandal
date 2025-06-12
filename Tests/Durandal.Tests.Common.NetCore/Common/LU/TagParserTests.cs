using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Durandal.Common.NLP;

namespace Durandal.Tests.Common.LU
{
    using Durandal.API;
    using Durandal.Common.NLP.Train;
    using System.Text.RegularExpressions;
    using Durandal.Common.NLP.Tagging;
    using Durandal.Common.NLP.Language.English;
    using Durandal.Common.Dialog;

    [TestClass]
    public class TagParserTests
    {
        private static IWordBreaker Wordbreaker = new EnglishWordBreaker();

        [TestMethod]
        public void TestStripTagsNoTags()
        {
            Assert.AreEqual("no tags here", TaggedDataSplitter.StripTags("no tags here"));
        }

        [TestMethod]
        public void TestStripTagsFakeTag()
        {
            Assert.AreEqual("[song/stuff]fake tag", TaggedDataSplitter.StripTags("[song/stuff]fake tag"));
        }

        [TestMethod]
        public void TestStripTagsFakeTag2()
        {
            Assert.AreEqual("[not a real tag] keep looking] bro", TaggedDataSplitter.StripTags("[not a real tag] keep looking] bro"));
        }

        [TestMethod]
        public void TestStripTagsBasic()
        {
            Assert.AreEqual("here's a tag", TaggedDataSplitter.StripTags("[tag]here's a tag[/tag]"));
        }

        [TestMethod]
        public void TestStripTagsNestedTags()
        {
            Assert.AreEqual("nested tags", TaggedDataSplitter.StripTags("[another_tag][tag]nested tags[/tag][/another_tag]"));
        }

        [TestMethod]
        public void TestStripTagsShearedTags()
        {
            Assert.AreEqual("nested tags", TaggedDataSplitter.StripTags("[another_tag]nested [tag]tags[/another_tag][/tag]"));
        }

        [TestMethod]
        public void TestStripTagsPreserveIndices()
        {
            Assert.AreEqual(" preserve indices ", TaggedDataSplitter.StripTags(" [1]preserve[/1] [2]indices[/2] "));
        }

        [TestMethod]
        public void TestStripTagsTagWithPeriods()
        {
            Assert.AreEqual("keep looking bro", TaggedDataSplitter.StripTags("[test.slot]keep looking[/test.slot] bro"));
        }

        [TestMethod]
        public void TestExtractTagNamesNoTags()
        {
            ISet<string> tagNames = TaggedDataSplitter.ExtractTagNames("no tags here");
            Assert.AreEqual(0, tagNames.Count);
        }

        [TestMethod]
        public void TestExtractTagNamesOneTag()
        {
            ISet<string> tagNames = TaggedDataSplitter.ExtractTagNames("this contains exactly [tag]one[/tag] tag");
            Assert.AreEqual(1, tagNames.Count);
            Assert.IsTrue(tagNames.Contains("tag"));
        }

        [TestMethod]
        public void TestExtractTagNamesOneTagWithPeriods()
        {
            ISet<string> tagNames = TaggedDataSplitter.ExtractTagNames("this contains exactly [tag.name]one[/tag.name] tag");
            Assert.AreEqual(1, tagNames.Count);
            Assert.IsTrue(tagNames.Contains("tag.name"));
        }

        [TestMethod]
        public void TestExtractTagNamesInvalidTag()
        {
            ISet<string> tagNames = TaggedDataSplitter.ExtractTagNames("this is an [invalid tag]");
            Assert.AreEqual(0, tagNames.Count);
        }

        [TestMethod]
        public void TestExtractTagNamesNonMatchingTag()
        {
            ISet<string> tagNames = TaggedDataSplitter.ExtractTagNames("this is an [tag]non-matching[/tage] tag");
            Assert.AreEqual(0, tagNames.Count);
        }

        [TestMethod]
        public void TestExtractTagNamesNestedTags()
        {
            ISet<string> tagNames = TaggedDataSplitter.ExtractTagNames("[one]nested [two]tags[/two] test[/one]");
            Assert.AreEqual(2, tagNames.Count);
            Assert.IsTrue(tagNames.Contains("one"));
            Assert.IsTrue(tagNames.Contains("two"));
        }

        [TestMethod]
        public void TestExtractTagNamesShearedTags()
        {
            ISet<string> tagNames = TaggedDataSplitter.ExtractTagNames("offset [one]nested [two]tags[/one] test[/two]");
            Assert.AreEqual(2, tagNames.Count);
            Assert.IsTrue(tagNames.Contains("one"));
            Assert.IsTrue(tagNames.Contains("two"));
        }

        [TestMethod]
        public void TestParseSlotsNoTags()
        {
            TaggedData sentence = TaggedDataSplitter.ParseSlots("base case", Wordbreaker);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(0, sentence.Slots.Count);
        }

        [TestMethod]
        public void TestParseSlotsInvalidTag()
        {
            TaggedData sentence = TaggedDataSplitter.ParseSlots("an [invalid tag]", Wordbreaker);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(0, sentence.Slots.Count);
        }

        [TestMethod]
        public void TestParseSlotsOneTag()
        {
            TaggedData sentence = TaggedDataSplitter.ParseSlots("exactly [tag]one[/tag] tag", Wordbreaker);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(1, sentence.Slots.Count);
            Assert.AreEqual("tag", sentence.Slots[0].Name);
            Assert.IsTrue(sentence.Slots[0].Annotations.ContainsKey(SlotPropertyName.StartIndex));
            Assert.AreEqual("8", sentence.Slots[0].Annotations[SlotPropertyName.StartIndex]);
            Assert.IsTrue(sentence.Slots[0].Annotations.ContainsKey(SlotPropertyName.StringLength));
            Assert.AreEqual("3", sentence.Slots[0].Annotations[SlotPropertyName.StringLength]);
        }

        [TestMethod]
        public void TestParseSlotsOneTagWithPeriods()
        {
            TaggedData sentence = TaggedDataSplitter.ParseSlots("exactly [tag.name]one[/tag.name] tag", Wordbreaker);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(1, sentence.Slots.Count);
            Assert.AreEqual("tag.name", sentence.Slots[0].Name);
            Assert.IsTrue(sentence.Slots[0].Annotations.ContainsKey(SlotPropertyName.StartIndex));
            Assert.AreEqual("8", sentence.Slots[0].Annotations[SlotPropertyName.StartIndex]);
            Assert.IsTrue(sentence.Slots[0].Annotations.ContainsKey(SlotPropertyName.StringLength));
            Assert.AreEqual("3", sentence.Slots[0].Annotations[SlotPropertyName.StringLength]);
        }

        [TestMethod]
        public void TestParseSlotsShearedTags()
        {
            TaggedData sentence = TaggedDataSplitter.ParseSlots("[one]offset [two]nested[/one] tags[/two]", Wordbreaker);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(2, sentence.Slots.Count);

            Assert.AreEqual("one", sentence.Slots[0].Name);
            Assert.AreEqual("offset nested", sentence.Slots[0].Value);
            Assert.IsTrue(sentence.Slots[0].Annotations.ContainsKey(SlotPropertyName.StartIndex));
            Assert.AreEqual("0", sentence.Slots[0].Annotations[SlotPropertyName.StartIndex]);
            Assert.IsTrue(sentence.Slots[0].Annotations.ContainsKey(SlotPropertyName.StringLength));
            Assert.AreEqual("13", sentence.Slots[0].Annotations[SlotPropertyName.StringLength]);

            Assert.AreEqual("two", sentence.Slots[1].Name);
            Assert.AreEqual("nested tags", sentence.Slots[1].Value);
            Assert.IsTrue(sentence.Slots[1].Annotations.ContainsKey(SlotPropertyName.StartIndex));
            Assert.AreEqual("7", sentence.Slots[1].Annotations[SlotPropertyName.StartIndex]);
            Assert.IsTrue(sentence.Slots[1].Annotations.ContainsKey(SlotPropertyName.StringLength));
            Assert.AreEqual("11", sentence.Slots[1].Annotations[SlotPropertyName.StringLength]);
        }

        [TestMethod]
        public void TestParseSlotsShearedTags2()
        {
            TaggedData sentence = TaggedDataSplitter.ParseSlots("test [one]offset [two]nested[/one] tags[/two] blah", Wordbreaker);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(2, sentence.Slots.Count);

            Assert.AreEqual("one", sentence.Slots[0].Name);
            Assert.AreEqual("offset nested", sentence.Slots[0].Value);
            Assert.IsTrue(sentence.Slots[0].Annotations.ContainsKey(SlotPropertyName.StartIndex));
            Assert.AreEqual("5", sentence.Slots[0].Annotations[SlotPropertyName.StartIndex]);
            Assert.IsTrue(sentence.Slots[0].Annotations.ContainsKey(SlotPropertyName.StringLength));
            Assert.AreEqual("13", sentence.Slots[0].Annotations[SlotPropertyName.StringLength]);

            Assert.AreEqual("two", sentence.Slots[1].Name);
            Assert.AreEqual("nested tags", sentence.Slots[1].Value);
            Assert.IsTrue(sentence.Slots[1].Annotations.ContainsKey(SlotPropertyName.StartIndex));
            Assert.AreEqual("12", sentence.Slots[1].Annotations[SlotPropertyName.StartIndex]);
            Assert.IsTrue(sentence.Slots[1].Annotations.ContainsKey(SlotPropertyName.StringLength));
            Assert.AreEqual("11", sentence.Slots[1].Annotations[SlotPropertyName.StringLength]);
        }

        [TestMethod]
        public void TestParseSlotsEmptyTag()
        {
            TaggedData sentence = TaggedDataSplitter.ParseSlots("[no_content][/no_content]", Wordbreaker);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(1, sentence.Slots.Count);
            Assert.AreEqual("no_content", sentence.Slots[0].Name);
        }

        [TestMethod]
        public void TestParseSlotsNestedTags()
        {
            TaggedData sentence = TaggedDataSplitter.ParseSlots("[one]regular [two]nested[/two] tags[/one]", Wordbreaker);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(2, sentence.Slots.Count);

            Assert.AreEqual("two", sentence.Slots[0].Name);
            Assert.AreEqual("nested", sentence.Slots[0].Value);
            Assert.IsTrue(sentence.Slots[0].Annotations.ContainsKey(SlotPropertyName.StartIndex));
            Assert.AreEqual("8", sentence.Slots[0].Annotations[SlotPropertyName.StartIndex]);
            Assert.IsTrue(sentence.Slots[0].Annotations.ContainsKey(SlotPropertyName.StringLength));
            Assert.AreEqual("6", sentence.Slots[0].Annotations[SlotPropertyName.StringLength]);

            Assert.AreEqual("one", sentence.Slots[1].Name);
            Assert.AreEqual("regular nested tags", sentence.Slots[1].Value);
            Assert.IsTrue(sentence.Slots[1].Annotations.ContainsKey(SlotPropertyName.StartIndex));
            Assert.AreEqual("0", sentence.Slots[1].Annotations[SlotPropertyName.StartIndex]);
            Assert.IsTrue(sentence.Slots[1].Annotations.ContainsKey(SlotPropertyName.StringLength));
            Assert.AreEqual("19", sentence.Slots[1].Annotations[SlotPropertyName.StringLength]);
        }

        [TestMethod]
        public void TestParseTagsSimple()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("base case", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(2, sentence.Words.Count);
            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual("O", sentence.Words[1].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsInvalid()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("an [invalid tag]", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(3, sentence.Words.Count);
            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual("O", sentence.Words[1].Tags[0]);
            Assert.AreEqual("O", sentence.Words[2].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsSingle()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("exactly [tag]one[/tag] tag", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(3, sentence.Words.Count);
            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual("tag", sentence.Words[1].Tags[0]);
            Assert.AreEqual("O", sentence.Words[2].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsSheared()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("[one]offset [two]nested[/one] tags[/two]", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(3, sentence.Words.Count);
            Assert.AreEqual("one", sentence.Words[0].Tags[0]);
            Assert.AreEqual(2, sentence.Words[1].Tags.Count);
            Assert.AreEqual("one", sentence.Words[1].Tags[0]);
            Assert.AreEqual("two", sentence.Words[1].Tags[1]);
            Assert.AreEqual("two", sentence.Words[2].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsSheared2()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("junk [one]offset [two]nested[/one] tags[/two] junk", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(5, sentence.Words.Count);
            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual("one", sentence.Words[1].Tags[0]);
            Assert.AreEqual(2, sentence.Words[2].Tags.Count);
            Assert.AreEqual("one", sentence.Words[2].Tags[0]);
            Assert.AreEqual("two", sentence.Words[2].Tags[1]);
            Assert.AreEqual("two", sentence.Words[3].Tags[0]);
            Assert.AreEqual("O", sentence.Words[4].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsNested()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("junk [one]regular [two]nested[/two] tags[/one] junk", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(5, sentence.Words.Count);
            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual("one", sentence.Words[1].Tags[0]);
            Assert.AreEqual(2, sentence.Words[2].Tags.Count);
            Assert.AreEqual("two", sentence.Words[2].Tags[0]);
            Assert.AreEqual("one", sentence.Words[2].Tags[1]);
            Assert.AreEqual("one", sentence.Words[3].Tags[0]);
            Assert.AreEqual("O", sentence.Words[4].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsCloseNested()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("[one][two]nested[/two][/one]", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(1, sentence.Words.Count);
            Assert.AreEqual(2, sentence.Words[0].Tags.Count);
            Assert.AreEqual("one", sentence.Words[0].Tags[1]);
            Assert.AreEqual("two", sentence.Words[0].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsArbitraryCutoffs()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("This is [one]super[/one]ludicrous speed", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(5, sentence.Words.Count);
            Assert.AreEqual("This", sentence.Words[0].Word);
            Assert.AreEqual("is", sentence.Words[1].Word);
            Assert.AreEqual("super", sentence.Words[2].Word);
            Assert.AreEqual("ludicrous", sentence.Words[3].Word);
            Assert.AreEqual("speed", sentence.Words[4].Word);
            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual("O", sentence.Words[1].Tags[0]);
            Assert.AreEqual(1, sentence.Words[2].Tags.Count);
            Assert.AreEqual("one", sentence.Words[2].Tags[0]);
            Assert.AreEqual("O", sentence.Words[3].Tags[0]);
            Assert.AreEqual("O", sentence.Words[4].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsArbitraryCutoffsMultipleTokens()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("This is hyper[one]super extra[/one]ludicrous speed", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(7, sentence.Words.Count);
            Assert.AreEqual("This", sentence.Words[0].Word);
            Assert.AreEqual("is", sentence.Words[1].Word);
            Assert.AreEqual("hyper", sentence.Words[2].Word);
            Assert.AreEqual("super", sentence.Words[3].Word);
            Assert.AreEqual("extra", sentence.Words[4].Word);
            Assert.AreEqual("ludicrous", sentence.Words[5].Word);
            Assert.AreEqual("speed", sentence.Words[6].Word);
            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual("O", sentence.Words[1].Tags[0]);
            Assert.AreEqual("O", sentence.Words[2].Tags[0]);
            Assert.AreEqual(1, sentence.Words[3].Tags.Count);
            Assert.AreEqual("one", sentence.Words[3].Tags[0]);
            Assert.AreEqual(1, sentence.Words[4].Tags.Count);
            Assert.AreEqual("one", sentence.Words[4].Tags[0]);
            Assert.AreEqual("O", sentence.Words[5].Tags[0]);
            Assert.AreEqual("O", sentence.Words[6].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsInsertsWordsForEmptyTagValues()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("$Empty%[empty][/empty]are you$", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(4, sentence.Words.Count);
            Assert.AreEqual(4, sentence.Utterance.Indices.Count);
            Assert.AreEqual(5, sentence.Utterance.NonTokens.Count);

            Assert.AreEqual("Empty", sentence.Words[0].Word);
            Assert.AreEqual("", sentence.Words[1].Word);
            Assert.AreEqual("are", sentence.Words[2].Word);
            Assert.AreEqual("you", sentence.Words[3].Word);

            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual(1, sentence.Words[1].Tags.Count);
            Assert.AreEqual("empty", sentence.Words[1].Tags[0]);
            Assert.AreEqual("O", sentence.Words[2].Tags[0]);
            Assert.AreEqual("O", sentence.Words[3].Tags[0]);

            Assert.AreEqual(1, sentence.Utterance.Indices[0]);
            Assert.AreEqual(7, sentence.Utterance.Indices[1]);
            Assert.AreEqual(7, sentence.Utterance.Indices[2]);
            Assert.AreEqual(11, sentence.Utterance.Indices[3]);

            Assert.AreEqual("$", sentence.Utterance.NonTokens[0]);
            Assert.AreEqual("%", sentence.Utterance.NonTokens[1]);
            Assert.AreEqual("", sentence.Utterance.NonTokens[2]);
            Assert.AreEqual(" ", sentence.Utterance.NonTokens[3]);
            Assert.AreEqual("$", sentence.Utterance.NonTokens[4]);
        }

        [TestMethod]
        public void TestParseTagsInsertsWordsForEmptyTagValues2()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("NA[a][/a]MX", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(3, sentence.Words.Count);
            Assert.AreEqual(3, sentence.Utterance.Indices.Count);
            Assert.AreEqual(4, sentence.Utterance.NonTokens.Count);

            Assert.AreEqual("NA", sentence.Words[0].Word);
            Assert.AreEqual("", sentence.Words[1].Word);
            Assert.AreEqual("MX", sentence.Words[2].Word);

            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual(1, sentence.Words[1].Tags.Count);
            Assert.AreEqual("a", sentence.Words[1].Tags[0]);
            Assert.AreEqual("O", sentence.Words[2].Tags[0]);

            Assert.AreEqual(0, sentence.Utterance.Indices[0]);
            Assert.AreEqual(2, sentence.Utterance.Indices[1]);
            Assert.AreEqual(2, sentence.Utterance.Indices[2]);

            Assert.AreEqual("", sentence.Utterance.NonTokens[0]);
            Assert.AreEqual("", sentence.Utterance.NonTokens[1]);
            Assert.AreEqual("", sentence.Utterance.NonTokens[2]);
            Assert.AreEqual("", sentence.Utterance.NonTokens[3]);
        }

        [TestMethod]
        public void TestParseTagsTagInsideAToken()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("NA[a]B[/a]MX", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(3, sentence.Words.Count);
            Assert.AreEqual(3, sentence.Utterance.Indices.Count);
            Assert.AreEqual(4, sentence.Utterance.NonTokens.Count);

            Assert.AreEqual("NA", sentence.Words[0].Word);
            Assert.AreEqual("B", sentence.Words[1].Word);
            Assert.AreEqual("MX", sentence.Words[2].Word);

            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual(1, sentence.Words[1].Tags.Count);
            Assert.AreEqual("a", sentence.Words[1].Tags[0]);
            Assert.AreEqual("O", sentence.Words[2].Tags[0]);

            Assert.AreEqual(0, sentence.Utterance.Indices[0]);
            Assert.AreEqual(2, sentence.Utterance.Indices[1]);
            Assert.AreEqual(3, sentence.Utterance.Indices[2]);

            Assert.AreEqual("", sentence.Utterance.NonTokens[0]);
            Assert.AreEqual("", sentence.Utterance.NonTokens[1]);
            Assert.AreEqual("", sentence.Utterance.NonTokens[2]);
            Assert.AreEqual("", sentence.Utterance.NonTokens[3]);
        }

        [TestMethod]
        public void TestParseTagsOptionalLG()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("A meeting at [time_1]1:00[/time_1] [time_2][/time_2][time_3][/time_3][time_4][/time_4]today.", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(9, sentence.Words.Count);

            Assert.AreEqual("A", sentence.Words[0].Word);
            Assert.AreEqual("meeting", sentence.Words[1].Word);
            Assert.AreEqual("at", sentence.Words[2].Word);
            Assert.AreEqual("1", sentence.Words[3].Word);
            Assert.AreEqual("00", sentence.Words[4].Word);
            Assert.AreEqual("", sentence.Words[5].Word);
            Assert.AreEqual("", sentence.Words[6].Word);
            Assert.AreEqual("", sentence.Words[7].Word);
            Assert.AreEqual("today", sentence.Words[8].Word);

            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual("O", sentence.Words[1].Tags[0]);
            Assert.AreEqual("O", sentence.Words[2].Tags[0]);
            Assert.AreEqual("time_1", sentence.Words[3].Tags[0]);
            Assert.AreEqual("time_1", sentence.Words[4].Tags[0]);
            Assert.AreEqual("time_2", sentence.Words[5].Tags[0]);
            Assert.AreEqual("time_3", sentence.Words[6].Tags[0]);
            Assert.AreEqual("time_4", sentence.Words[7].Tags[0]);
            Assert.AreEqual("O", sentence.Words[8].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsWeatherSentence()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("Outside it is [temp]31[/temp]° [unit]F[/unit] and [condition]cloudy[/condition].", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(7, sentence.Words.Count);

            Assert.AreEqual("Outside", sentence.Words[0].Word);
            Assert.AreEqual("it", sentence.Words[1].Word);
            Assert.AreEqual("is", sentence.Words[2].Word);
            Assert.AreEqual("31", sentence.Words[3].Word);
            Assert.AreEqual("F", sentence.Words[4].Word);
            Assert.AreEqual("and", sentence.Words[5].Word);
            Assert.AreEqual("cloudy", sentence.Words[6].Word);

            Assert.AreEqual("O", sentence.Words[0].Tags[0]);
            Assert.AreEqual("O", sentence.Words[1].Tags[0]);
            Assert.AreEqual("O", sentence.Words[2].Tags[0]);
            Assert.AreEqual("temp", sentence.Words[3].Tags[0]);
            Assert.AreEqual("unit", sentence.Words[4].Tags[0]);
            Assert.AreEqual("O", sentence.Words[5].Tags[0]);
            Assert.AreEqual("condition", sentence.Words[6].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsAdjacentEmptySpans()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("[a][/a][b][/b][c][/c]", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(3, sentence.Words.Count);

            Assert.AreEqual("", sentence.Words[0].Word);
            Assert.AreEqual("", sentence.Words[1].Word);
            Assert.AreEqual("", sentence.Words[2].Word);

            Assert.AreEqual(1, sentence.Words[0].Tags.Count);
            Assert.AreEqual("a", sentence.Words[0].Tags[0]);
            Assert.AreEqual(1, sentence.Words[1].Tags.Count);
            Assert.AreEqual("b", sentence.Words[1].Tags[0]);
            Assert.AreEqual(1, sentence.Words[2].Tags.Count);
            Assert.AreEqual("c", sentence.Words[2].Tags[0]);
        }

        /// <summary>
        /// This would be a pretty extreme edge case that we don't need to care about supporting for now
        /// </summary>
        //[TestMethod]
        //public void TestParseTagsOverlappingEmptySpans()
        //{
        //    TaggedSentence sentence = TaggedDataSplitter.ParseTags("[c][a][/a][b][/b][/c]", Wordbreaker, true);
        //    Assert.IsNotNull(sentence);
        //    Assert.AreEqual(2, sentence.Words.Count);

        //    Assert.AreEqual("", sentence.Words[0].Word);
        //    Assert.AreEqual("", sentence.Words[1].Word);

        //    Assert.AreEqual(2, sentence.Words[0].Tags.Count);
        //    Assert.IsTrue(sentence.Words[0].Tags.Contains("a"));
        //    Assert.IsTrue(sentence.Words[0].Tags.Contains("c"));
        //    Assert.AreEqual(2, sentence.Words[1].Tags.Count);
        //    Assert.IsTrue(sentence.Words[1].Tags.Contains("b"));
        //    Assert.IsTrue(sentence.Words[1].Tags.Contains("c"));
        //}

        [TestMethod]
        public void TestParseTagsAdjacentEmptySpans2()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("[a][/a][b]test[/b][c][/c]", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(3, sentence.Words.Count);

            Assert.AreEqual("", sentence.Words[0].Word);
            Assert.AreEqual("test", sentence.Words[1].Word);
            Assert.AreEqual("", sentence.Words[2].Word);

            Assert.AreEqual(1, sentence.Words[0].Tags.Count);
            Assert.AreEqual("a", sentence.Words[0].Tags[0]);
            Assert.AreEqual(1, sentence.Words[1].Tags.Count);
            Assert.AreEqual("b", sentence.Words[1].Tags[0]);
            Assert.AreEqual(1, sentence.Words[2].Tags.Count);
            Assert.AreEqual("c", sentence.Words[2].Tags[0]);
        }

        [TestMethod]
        public void TestParseTagsAdjacentEmptySpans3()
        {
            TaggedSentence sentence = TaggedDataSplitter.ParseTags("[a][/a] [b]test[/b] [c][/c]", Wordbreaker, true);
            Assert.IsNotNull(sentence);
            Assert.AreEqual(3, sentence.Words.Count);

            Assert.AreEqual("", sentence.Words[0].Word);
            Assert.AreEqual("test", sentence.Words[1].Word);
            Assert.AreEqual("", sentence.Words[2].Word);

            Assert.AreEqual(1, sentence.Words[0].Tags.Count);
            Assert.AreEqual("a", sentence.Words[0].Tags[0]);
            Assert.AreEqual(1, sentence.Words[1].Tags.Count);
            Assert.AreEqual("b", sentence.Words[1].Tags[0]);
            Assert.AreEqual(1, sentence.Words[2].Tags.Count);
            Assert.AreEqual("c", sentence.Words[2].Tags[0]);
        }
    }
}
