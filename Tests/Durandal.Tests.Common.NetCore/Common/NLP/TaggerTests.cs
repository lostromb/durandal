using Durandal.API;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Feature;
using Durandal.Common.NLP.Tagging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Durandal.Common.Config;
using Durandal.Common.Logger;
using Durandal.Common.Collections.Indexing;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Statistics.Classification;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.Dialog;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.NLP
{
    [TestClass]
    public class TaggerTests
    {
        private static ILogger logger;
        private static IWordBreaker wordBreaker;
        private static ITagFeatureExtractor featureExtractor;
        private static CRFTagger tagger;
        private static IStatisticalTrainer modelTrainer;

        /// <summary>
        /// Train a trivial tagger based on some hardcoded training data
        /// </summary>
        /// <param name="context"></param>
        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            logger = new ConsoleLogger("Main", LogLevel.All);
            DictionaryCollection dictionaries = new DictionaryCollection(logger, null);
            featureExtractor = new EnglishTagFeatureExtractor(dictionaries);
            wordBreaker = new EnglishWordBreaker();
            ISet<string> possibleTags = new HashSet<string>();
            possibleTags.Add("name");
            possibleTags.Add("query");
            possibleTags.Add("firstname");
            MemoryStream featureStream = new MemoryStream();
            featureExtractor.ExtractTrainingFeatures(GetTrainingData(), featureStream, wordBreaker, possibleTags);
            string featureFile = Encoding.UTF8.GetString(featureStream.ToArray());
            ICompactIndex<string> index = BasicCompactIndex<string>.BuildStringIndex();
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(new VirtualPath("Training"), featureStream.ToArray());
            modelTrainer = new MaxEntClassifierTrainer(logger.Clone("CRFTrainer"), fileSystem);
            tagger = new CRFTagger(modelTrainer, logger, 0.5f, fileSystem, new WeakPointer<ICompactIndex<string>>(index), wordBreaker);
            IConfiguration domainConfig = new InMemoryConfiguration(logger);
            domainConfig.Set("alltags", new[] { "O", "name", "query" });
            domainConfig.Set("tags_testintent", new[] { "O", "name", "query", "firstname" });
            domainConfig.Set("nodenames_testintent", new[] { "stkn", "O", "name", "query", "namequery", "firstnamename", "firstnamenamequery" });
            domainConfig.Set("override/testintent/stkn", "O");
            tagger.TrainFromData(new VirtualPath("Training"), "testdomain", "testintent", new VirtualPath("Models"), domainConfig);
        }

        /// <summary>
        /// Retrieves the mock training data used for these tests
        /// </summary>
        /// <returns></returns>
        private static Stream GetTrainingData()
        {
            string trainingFile =
                "testdomain/testintent\tmy name is [name][firstname]Laracca[/firstname] Jones[/name]\r\n" +
                "testdomain/testintent\tsearch for [query]something[/query]\r\n" +
                "testdomain/testintent\tsearch for [query]pictures of [name][firstname]Laracca[/firstname] Jones[/name][/query]\r\n" +
                "testdomain/testintent\ta [query]banana [name]Purple[/query] Puma[/name]\r\n";
            return new MemoryStream(Encoding.UTF8.GetBytes(trainingFile), false);
        }

        /// <summary>
        /// tests single tags
        /// </summary>
        [TestMethod]
        public void TestBasicTagging()
        {
            Sentence utterance = wordBreaker.Break("my name is Laracca Jones");
            IList<TaggedData> taggerResult = tagger.Classify(utterance, featureExtractor, false);

            Assert.AreEqual(1, taggerResult.Count);
            IDictionary<string, SlotValue> slots = CreateSlotMapping(taggerResult[0]);
            Assert.AreEqual(2, slots.Count);
            VerifySlotValue(slots, "firstname", "Laracca");
            VerifySlotValue(slots, "name", "Laracca Jones");

            utterance = wordBreaker.Break("search for something");
            taggerResult = tagger.Classify(utterance, featureExtractor, false);

            Assert.AreEqual(1, taggerResult.Count);
            slots = CreateSlotMapping(taggerResult[0]);
            Assert.AreEqual(1, slots.Count);
            VerifySlotValue(slots, "query", "something");
        }

        /// <summary>
        /// tests nested and overlapping tags
        /// </summary>
        [TestMethod]
        public void TestCompoundTaggingNested()
        {
            Sentence utterance = wordBreaker.Break("search for pictures of Laracca Jones");
            IList<TaggedData> taggerResult = tagger.Classify(utterance, featureExtractor, false);

            Assert.AreEqual(1, taggerResult.Count);
            IDictionary<string, SlotValue> slots = CreateSlotMapping(taggerResult[0]);
            Assert.AreEqual(3, slots.Count);
            VerifySlotValue(slots, "query", "pictures of Laracca Jones");
            VerifySlotValue(slots, "firstname", "Laracca");
            VerifySlotValue(slots, "name", "Laracca Jones");
        }

        /// <summary>
        /// tests non-nested but still overlapping tags
        /// </summary>
        [TestMethod]
        public void TestCompoundTaggingOverlapping()
        {
            Sentence utterance = wordBreaker.Break("a banana Purple Puma");
            IList<TaggedData> taggerResult = tagger.Classify(utterance, featureExtractor, false);

            Assert.AreEqual(1, taggerResult.Count);
            IDictionary<string, SlotValue> slots = CreateSlotMapping(taggerResult[0]);
            Assert.AreEqual(2, slots.Count);
            VerifySlotValue(slots, "query", "banana Purple");
            VerifySlotValue(slots, "name", "Purple Puma");
        }
        
        /// <summary>
        /// Test that extra whitespace does not mess up the tag bounds or the string index calculation
        /// </summary>
        [TestMethod]
        public void TestBasicTaggingWithWhitespace()
        {
            Sentence utterance = wordBreaker.Break("  my    name  is       Laracca Jones      ");
            IList<TaggedData> taggerResult = tagger.Classify(utterance, featureExtractor, false);

            Assert.AreEqual(1, taggerResult.Count);
            IDictionary<string, SlotValue> slots = CreateSlotMapping(taggerResult[0]);
            Assert.AreEqual(2, slots.Count);
            VerifySlotValue(slots, "name", "Laracca Jones");
            VerifySlotValue(slots, "firstname", "Laracca");
            Assert.AreEqual(2, slots["name"].Annotations.Count);
            Assert.AreEqual("23", slots["name"].Annotations[SlotPropertyName.StartIndex]);
            Assert.AreEqual("13", slots["name"].Annotations[SlotPropertyName.StringLength]);
        }

        /// <summary>
        /// tests that the lexical value of slots is set properly whenever displayForm == lexicalForm
        /// </summary>
        [TestMethod]
        public void TestLexicalSlotValueNoAlignment()
        {
            Sentence utterance = wordBreaker.Break("my name is Laracca Jones");
            utterance.LexicalForm = "my name is Laracca Jones";
            IList<TaggedData> taggerResult = tagger.Classify(utterance, featureExtractor, false);

            Assert.AreEqual(1, taggerResult.Count);
            IDictionary<string, SlotValue> slots = CreateSlotMapping(taggerResult[0]);
            Assert.AreEqual(2, slots.Count);
            VerifySlotValue(slots, "name", "Laracca Jones");
            VerifySlotValue(slots, "firstname", "Laracca");

            Assert.AreEqual("Laracca Jones", slots["name"].LexicalForm);
            Assert.AreEqual("Laracca", slots["firstname"].LexicalForm);
        }

        /// <summary>
        /// tests that the lexical value of slots is set properly whenever displayForm is DIFFERENT from lexicalForm
        /// </summary>
        [TestMethod]
        public void TestLexicalSlotValueWithAlignmentA()
        {
            Sentence utterance = wordBreaker.Break("my name is Laracca Jones");
            utterance.LexicalForm = "my name is La Rocka Jones";
            IList<TaggedData> taggerResult = tagger.Classify(utterance, featureExtractor, false);

            Assert.AreEqual(1, taggerResult.Count);
            IDictionary<string, SlotValue> slots = CreateSlotMapping(taggerResult[0]);
            Assert.AreEqual(2, slots.Count);
            VerifySlotValue(slots, "firstname", "Laracca");
            VerifySlotValue(slots, "name", "Laracca Jones");

            Assert.AreEqual("La Rocka Jones", slots["name"].LexicalForm);
            Assert.AreEqual("La Rocka", slots["firstname"].LexicalForm);
        }

        [TestMethod]
        public void TestLexicalSlotValueWithAlignmentB()
        {
            Sentence utterance = wordBreaker.Break("my name is Laracca Jones");
            utterance.LexicalForm = "my name is Laracca Joenes";
            IList<TaggedData> taggerResult = tagger.Classify(utterance, featureExtractor, false);

            Assert.AreEqual(1, taggerResult.Count);
            IDictionary<string, SlotValue> slots = CreateSlotMapping(taggerResult[0]);
            Assert.AreEqual(2, slots.Count);
            VerifySlotValue(slots, "firstname", "Laracca");
            VerifySlotValue(slots, "name", "Laracca Jones");

            Assert.AreEqual("Laracca Joenes", slots["name"].LexicalForm);
            Assert.AreEqual("Laracca", slots["firstname"].LexicalForm);
        }

        private static void VerifySlotValue(IDictionary<string, SlotValue> slots, string expectedSlotName, string expectedSlotValue)
        {
            Assert.IsTrue(slots.ContainsKey(expectedSlotName));
            Assert.AreEqual(expectedSlotValue, slots[expectedSlotName].Value);
        }

        private static IDictionary<string, SlotValue> CreateSlotMapping(TaggedData tags)
        {
            IDictionary<string, SlotValue> returnVal = new Dictionary<string, SlotValue>();
            foreach (var slot in tags.Slots)
            {
                returnVal.Add(slot.Name, slot);
            }

            return returnVal;
        }
    }
}
