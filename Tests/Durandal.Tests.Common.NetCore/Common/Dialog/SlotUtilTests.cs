
namespace Durandal.Tests.Common.Dialog
{
    using Durandal.API;
        using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.NLP.Language.English;
    using Durandal.Common.NLP.Tagging;
    using Durandal.Common.Statistics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    [TestClass]
    public class SlotUtilTests
    {
        /// <summary>
        /// Test that conversion of an augmented query from TaggedData to string works as expected
        /// </summary>
        [TestMethod]
        public void TestGenerateAugmentedQueryBasic()
        {
            string rawUtterance = "Play songs by [artist]so great and powerful[/artist]";
            TaggedData taggedData = TaggedDataSplitter.ParseSlots(rawUtterance, new EnglishWordBreaker());
            taggedData.Slots[0].Annotations.Add(SlotPropertyName.AugmentedValue, "SoGreatAndPowerful");
            string augmentedValue = DialogHelpers.ConvertTaggedDataToAugmentedQuery(taggedData);
            Assert.AreEqual("Play songs by SoGreatAndPowerful", augmentedValue);
        }

        [TestMethod]
        public void TestGenerateAugmentedQueryPreserveIndices()
        {
            string rawUtterance = "Play the song [song]murder murder[/song] by [artist]Tupac[/artist] now";
            TaggedData taggedData = TaggedDataSplitter.ParseSlots(rawUtterance, new EnglishWordBreaker());
            taggedData.Slots[0].Annotations.Add(SlotPropertyName.AugmentedValue, "Murder, Murder");
            taggedData.Slots[1].Annotations.Add(SlotPropertyName.AugmentedValue, "2Pac");
            string augmentedValue = DialogHelpers.ConvertTaggedDataToAugmentedQuery(taggedData);
            Assert.AreEqual("Play the song Murder, Murder by 2Pac now", augmentedValue);
        }

        [TestMethod]
        public void TestGenerateAugmentedQueryMultipleValues()
        {
            string rawUtterance = "This is test [one]test[/one] [two]test[/two] [three]test[/three] [four]test[/four]";
            TaggedData taggedData = TaggedDataSplitter.ParseSlots(rawUtterance, new EnglishWordBreaker());
            taggedData.Slots[0].Annotations.Add(SlotPropertyName.AugmentedValue, "111111");
            taggedData.Slots[1].Annotations.Add(SlotPropertyName.AugmentedValue, "2");
            taggedData.Slots[3].Annotations.Add(SlotPropertyName.AugmentedValue, "4");
            string augmentedValue = DialogHelpers.ConvertTaggedDataToAugmentedQuery(taggedData);
            Assert.AreEqual("This is test 111111 2 test 4", augmentedValue);
        }

        [TestMethod]
        public void TestGenerateAugmentedQueryAfterCanonicalization()
        {
            string rawUtterance = "How [condition]wet[/condition] is it outside?";
            TaggedData taggedData = TaggedDataSplitter.ParseSlots(rawUtterance, new EnglishWordBreaker());
            taggedData.Slots[0].Value = "RAIN";
            taggedData.Slots[0].Annotations.Add(SlotPropertyName.NonCanonicalValue, "wet");
            taggedData.Slots[0].Annotations.Add(SlotPropertyName.AugmentedValue, "WET");
            string augmentedValue = DialogHelpers.ConvertTaggedDataToAugmentedQuery(taggedData);
            Assert.AreEqual("How WET is it outside?", augmentedValue);
        }
        
        [TestMethod]
        public async Task TestBasicEntityResolution()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(new NLPToolsCollection()));
            LexicalString rawUtterance = new LexicalString("kitchen lights");
            IList<NamedEntity<string>> entities = new List<NamedEntity<string>>();
            entities.Add(new NamedEntity<string>("FRONT_PORCH", new List<LexicalString>() { new LexicalString("front porch light"), new LexicalString("porch light"), new LexicalString("front porch")}));
            entities.Add(new NamedEntity<string>("BACK_PORCH", new List<LexicalString>() { new LexicalString("back porch light"), new LexicalString("porch light"), new LexicalString("back porch") }));
            entities.Add(new NamedEntity<string>("KITCHEN", new List<LexicalString>() { new LexicalString("kitchen light"), new LexicalString("dining room light"), new LexicalString("kitchen") }));
            entities.Add(new NamedEntity<string>("BATHROOM", new List<LexicalString>() { new LexicalString("bathroom lights"), new LexicalString("bathroom light") }));
            IList<Hypothesis<string>> hyps = await resolver.ResolveEntity<string>(rawUtterance, entities, LanguageCode.EN_US, logger);
            Assert.IsTrue(hyps.Count > 0);
            Assert.AreEqual("KITCHEN", (string)(hyps[0].Value));
        }

        [TestMethod]
        public async Task TestLargeIndexEntityResolution()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(new NLPToolsCollection()));
            IList<NamedEntity<string>> entities = new List<NamedEntity<string>>();
            entities.Add(new NamedEntity<string>("TRUE", new List<LexicalString>() { new LexicalString("this statement is true") }));
            IRandom rand = new FastRandom();
            for (int c = 0; c < 1000; c++)
            {
                string[] knownAs = new string[rand.NextInt(1, 3)];
                for (int z = 0; z < knownAs.Length; z++)
                {
                    knownAs[z] = GenerateRandString(rand, 20, 80);
                }
            }

            IList<Hypothesis<string>> hyps = await resolver.ResolveEntity<string>(new LexicalString("this is true"), entities, LanguageCode.EN_US, logger);
            Assert.IsTrue(hyps.Count > 0);
            Assert.AreEqual("TRUE", (string)(hyps[0].Value));
        }

        [TestMethod]
        public void TestParseNumberSlotWholeNumber()
        {
            SlotValue slot = new SlotValue("number", "5", SlotValueFormat.SpokenText);
            slot.Annotations[SlotPropertyName.Number] = "5";
            decimal? parsedValue = slot.GetNumber();
            Assert.IsTrue(parsedValue.HasValue);
            Assert.AreEqual(5M, parsedValue.Value);
        }

        [TestMethod]
        public void TestParseNumberSlotDecimalNumber()
        {
            SlotValue slot = new SlotValue("number", "3.1415", SlotValueFormat.SpokenText);
            slot.Annotations[SlotPropertyName.Number] = "3.1415";
            decimal? parsedValue = slot.GetNumber();
            Assert.IsTrue(parsedValue.HasValue);
            Assert.AreEqual(3.1415M, parsedValue.Value);
        }

        [TestMethod]
        public void TestParseNumberSlotEvenFraction()
        {
            SlotValue slot = new SlotValue("number", "3/5", SlotValueFormat.SpokenText);
            slot.Annotations[SlotPropertyName.Number] = "3/5";
            decimal? parsedValue = slot.GetNumber();
            Assert.IsTrue(parsedValue.HasValue);
            Assert.AreEqual(0.6M, parsedValue.Value);
        }

        [TestMethod]
        public void TestParseNumberSlotOddFraction()
        {
            SlotValue slot = new SlotValue("number", "2/3", SlotValueFormat.SpokenText);
            slot.Annotations[SlotPropertyName.Number] = "2/3";
            decimal? parsedValue = slot.GetNumber();
            Assert.IsTrue(parsedValue.HasValue);
            Assert.AreEqual(2M / 3M, parsedValue.Value);
        }

        [TestMethod]
        public void TestParseNumberSlotEmptyAnnotation()
        {
            SlotValue slot = new SlotValue("number", "3/5", SlotValueFormat.SpokenText);
            decimal? parsedValue = slot.GetNumber();
            Assert.IsFalse(parsedValue.HasValue);
        }

        [TestMethod]
        public void TestParseNumberSlotInvalidNumber()
        {
            SlotValue slot = new SlotValue("number", "peaches", SlotValueFormat.SpokenText);
            slot.Annotations[SlotPropertyName.Number] = "peaches";
            decimal? parsedValue = slot.GetNumber();
            Assert.IsFalse(parsedValue.HasValue);
        }

        [TestMethod]
        public void TestParseNumberSlotInvalidFraction()
        {
            SlotValue slot = new SlotValue("number", "3/x", SlotValueFormat.SpokenText);
            slot.Annotations[SlotPropertyName.Number] = "3/x";
            decimal? parsedValue = slot.GetNumber();
            Assert.IsFalse(parsedValue.HasValue);
        }

        private static string GenerateRandString(IRandom r, int minLength = 120, int maxLength = 360)
        {
            int length = r.NextInt(120, 360);
            StringBuilder returnVal = new StringBuilder();
            for (int c = 0; c < length; c++)
            {
                returnVal.Append(r.NextInt(0, 9).ToString());
                if (r.NextInt() < 0.1)
                    returnVal.Append(" ");
            }
            return returnVal.ToString();
        }
    }
}
