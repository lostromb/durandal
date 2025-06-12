using Durandal.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Durandal.Common.Logger;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.NLP;
using Durandal.Common.Collections.Indexing;
using Durandal.Common.LG;
using Durandal.Common.Utils;
using Durandal.Common.NLP.Alignment;
using Durandal.Common.NLP.Feature;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.LG.Statistical;
using Durandal.Common.MathExt;
using Durandal.Common.NLP.Language;
using Durandal.Common.Tasks;

namespace Durandal.Tests.Common.LG
{
    [TestClass]
    public class LgStatisticalTests
    {
        private static ILogger _logger;
        private static ILGFeatureExtractor _featureExtractor;
        private static IWordBreaker _wordbreaker;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _logger = new ConsoleLogger("LGTests", LogLevel.All);
            _featureExtractor = new EnglishLGFeatureExtractor();
            _wordbreaker = new EnglishWordBreaker();
        }
        
        private static async Task<StatisticalLGEngine> BuildEngine(string templateFile, params LanguageCode[] locales)
        {
            byte[] file = Encoding.UTF8.GetBytes(templateFile);
            VirtualPath mockFileName = new VirtualPath("testdomain.ini");
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(mockFileName, file);
            List<VirtualPath> allFiles = new List<VirtualPath>();
            allFiles.Add(mockFileName);

            NLPToolsCollection nlTools = new NLPToolsCollection();
            foreach (LanguageCode locale in locales)
            {
                nlTools.Add(locale,
                    new NLPTools()
                    {
                        Pronouncer = null,
                        WordBreaker = new EnglishWholeWordBreaker(),
                        FeaturizationWordBreaker = new EnglishWordBreaker(),
                        EditDistance = EditDistanceDoubleMetaphone.Calculate,
                        LGFeatureExtractor = new EnglishLGFeatureExtractor(),
                        CultureInfoFactory = new WindowsCultureInfoFactory()
                    });
            }

            StatisticalLGEngine engine = await StatisticalLGEngine.Create(fileSystem, _logger, "testdomain", new CodeDomLGScriptCompiler(), allFiles, nlTools);
            return engine;
        }

        private static readonly string[] WeatherPattern = new string[]
        {
            "On the [day]1[/day]st, it will be [condition]cloudy[/condition], and [temp]15[/temp] degrees.",
            "On the [day]1[/day]st, it will be [condition]partly cloudy[/condition], and [temp]15[/temp] degrees.",
            "On the [day]1[/day]st, it will be [condition]sunny[/condition], and [temp]15[/temp] degrees.",
            "On the [day]2[/day]nd, it will be [condition]cloudy[/condition], and [temp]1[/temp] degree.",
            "On the [day]2[/day]nd, it will be [condition]cloudy[/condition], and [temp]22[/temp] degrees.",
            "On the [day]2[/day]nd, it will be [condition]clear[/condition], and [temp]1[/temp] degrees.",
            "On the [day]3[/day]rd, it will be [condition]sunny[/condition], and [temp]34[/temp] degrees.",
            "On the [day]3[/day]rd, it will be [condition]cloudy[/condition], and [temp]34[/temp] degrees.",
            "On the [day]3[/day]rd, it will be [condition]sunny[/condition], and [temp]1[/temp] degree.",
            "On the [day]3[/day]rd, it will be [condition]cloudy[/condition], and [temp]34[/temp] degrees.",
            "On the [day]4[/day]th, it will be [condition]overcast[/condition], and [temp]16[/temp] degrees.",
            "On the [day]5[/day]th, it will be [condition]partly cloudy[/condition], and [temp]44[/temp] degrees.",
            "On the [day]6[/day]th, it will be [condition]rainy[/condition], and [temp]35[/temp] degrees.",
            "On the [day]7[/day]th, it will be [condition]rainy[/condition], and [temp]1[/temp] degree.",
            "On the [day]8[/day]th, it will be [condition]sunny[/condition], and [temp]63[/temp] degrees.",
            "On the [day]9[/day]th, it will be [condition]mostly cloudy[/condition], and [temp]12[/temp] degrees.",
            "On the [day]10[/day]th, it will be [condition]overcast[/condition], and [temp]1[/temp] degree.",
            "On the [day]11[/day]th, it will be [condition]overcast[/condition], and [temp]0[/temp] degrees.",
            "On the [day]21[/day]st, it will be [condition]rainy[/condition], and [temp]0[/temp] degrees.",
            "On the [day]22[/day]nd, it will be [condition]clear[/condition], and [temp]34[/temp] degrees.",
            "On the [day]23[/day]rd, it will be [condition]snowy[/condition], and [temp]1[/temp] degree.",
            "On the [day]22[/day]nd, it will be [condition]rainy[/condition], and [temp]1[/temp] degree.",
            "On the [day]31[/day]st, it will be [condition]cloudy[/condition], and [temp]10[/temp] degrees.",
            "On the [day]32[/day]nd, it will be [condition]overcast[/condition], and [temp]24[/temp] degrees.",
            "On the [day]1[/day]st, it will be [condition]clear[/condition], and [temp]4[/temp] degrees.",
            "On the [day]31[/day]st, it will be [condition]cloudy[/condition], and [temp]0[/temp] degrees.",
            "On the [day]21[/day]st, it will be [condition]snowy[/condition], and [temp]1[/temp] degree.",
            "On the [day]21[/day]st, it will be [condition]partly cloudy[/condition], and [temp]54[/temp] degrees.",
            "On the [day]3[/day]rd, it will be [condition]foggy[/condition], and [temp]44[/temp] degrees.",
            "On the [day]23[/day]rd, it will be [condition]sunny[/condition], and [temp]34[/temp] degrees.",
            "On the [day]13[/day]th, it will be [condition]cloudy[/condition], and [temp]1[/temp] degree.",
            "On the [day]12[/day]th, it will be [condition]stormy[/condition], and [temp]74[/temp] degrees.",
            "On the [day]2[/day]nd, it will be [condition]snowy[/condition], and [temp]22[/temp] degrees.",
            "On the [day]2[/day]nd, it will be [condition]cloudy[/condition], and [temp]52[/temp] degrees.",
            "On the [day]22[/day]nd, it will be [condition]rainy[/condition], and [temp]76[/temp] degrees.",
            "On the [day]22[/day]nd, it will be [condition]sunny[/condition], and [temp]1[/temp] degree.",
            "On the [day]3[/day]rd, it will be [condition]cloudy[/condition], and [temp]34[/temp] degrees.",
            "On the [day]23[/day]rd, it will be [condition]snowy[/condition], and [temp]1[/temp] degree.",
            "On the [day]2[/day]nd, it will be [condition]overcast[/condition], and [temp]1[/temp] degree.",
            "On the [day]13[/day]th, it will be [condition]rainy[/condition], and [temp]34[/temp] degrees.",
            "On the [day]33[/day]rd, it will be [condition]cloudy[/condition], and [temp]1[/temp] degree.",
            "On the [day]23[/day]rd, it will be [condition]clear[/condition], and [temp]34[/temp] degrees.",
            "On the [day]1[/day]st, it will be [condition]clear[/condition], and [temp]1[/temp] degree.",
            "On the [day]19[/day]th, it will be [condition]icy[/condition], and [temp]1[/temp] degree.",
        };

        [TestMethod]
        public void TestStatisticalLgWeatherPattern()
        {
            IRandom random = new FastRandom(465595191);
            for (int c = 0; c < 10; c++)
            {
                StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _wordbreaker, _featureExtractor, debug: true);
                // Create a random pattern by sampling from the available training above.
                string[] sampledTraining = new string[(int)(WeatherPattern.Length * 0.9)];
                for (int z = 0; z < sampledTraining.Length; z++)
                {
                    sampledTraining[z] = WeatherPattern[random.NextInt(0, WeatherPattern.Length)];
                }

                pattern.Initialize(sampledTraining);
                IDictionary<string, string> subs = new Dictionary<string, string>();
                subs.Add("day", "2");
                subs.Add("condition", "overcast");
                subs.Add("temp", "1");
                Assert.AreEqual("On the 2nd, it will be overcast, and 1 degree.", pattern.Render(subs, false, _logger));
                subs.Clear();
                subs.Add("day", "3");
                subs.Add("condition", "sunny");
                subs.Add("temp", "32");
                Assert.AreEqual("On the 3rd, it will be sunny, and 32 degrees.", pattern.Render(subs, false, _logger));
                subs.Clear();
                subs.Add("day", "15");
                subs.Add("condition", "partly sunny");
                subs.Add("temp", "16");
                Assert.AreEqual("On the 15th, it will be partly sunny, and 16 degrees.", pattern.Render(subs, false, _logger));
            }
        }

        private static readonly string[] InsertTokenStartPattern = new string[]
        {
            "...The [slot]first[/slot] thing",
            "...The [slot]first[/slot] thing",
            "...The [slot]second[/slot] thing",
            "...The [slot]second[/slot] thing",
            "...The [slot]third[/slot] thing",
            "...The [slot]third[/slot] thing",
            "...The [slot]fourth[/slot] thing",
            "...The [slot]fourth[/slot] thing",
            "...The [slot]fifth[/slot] thing",
            "...The [slot]fifth[/slot] thing",
            "...[slot]1[/slot] thing",
            "...[slot]1[/slot] thing",
            "...[slot]2[/slot] things",
            "...[slot]2[/slot] things",
            "...[slot]3[/slot] things",
            "...[slot]3[/slot] things",
            "...[slot]4[/slot] things",
            "...[slot]4[/slot] things",
            "...[slot]5[/slot] things",
            "...[slot]5[/slot] things",
        };

        /// <summary>
        /// Tests that LG will conditionally insert tokens based on statistics (that is,
        /// tokens can be aligned with empty string within the same decision group)
        /// at the beginning of a sentence (before a slot starts the sentence)
        /// </summary>
        [TestMethod]
        public void TestStatisticalLgInsertTokensStartConditional()
        {
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _wordbreaker, _featureExtractor, debug: true);
            pattern.Initialize(InsertTokenStartPattern);
            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("slot", "fifth");
            Assert.AreEqual("...The fifth thing", pattern.Render(subs, false, _logger));
            subs.Clear();
            subs.Add("slot", "9");
            Assert.AreEqual("...9 things", pattern.Render(subs, false, _logger));
            subs.Clear();
            subs.Add("slot", "1");
            Assert.AreEqual("...1 thing", pattern.Render(subs, false, _logger));
        }

        private static readonly string[] InsertTokenEndPattern = new string[]
        {
            "Test [slot]first[/slot] thing!",
            "Test [slot]second[/slot] thing!",
            "Test [slot]third[/slot] thing!",
            "Test [slot]fourth[/slot] thing!",
            "Test [slot]1[/slot]...",
            "Test [slot]2[/slot]...",
            "Test [slot]3[/slot]...",
            "Test [slot]4[/slot]...",
        };

        /// <summary>
        /// Tests that LG will conditionally insert tokens based on statistics (that is,
        /// tokens can be aligned with empty string within the same decision group)
        /// on the end of a sentence (after a slot finishes the sentence)
        /// </summary>
        [TestMethod]
        public void TestStatisticalLgInsertTokensEndConditional()
        {
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _wordbreaker, _featureExtractor);
            pattern.Initialize(InsertTokenEndPattern);
            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("slot", "fifth");
            Assert.AreEqual("Test fifth thing!", pattern.Render(subs, false, _logger));
            subs.Clear();
            subs.Add("slot", "9");
            Assert.AreEqual("Test 9...", pattern.Render(subs, false, _logger));
            subs.Clear();
            subs.Add("slot", "1");
            Assert.AreEqual("Test 1...", pattern.Render(subs, false, _logger));
        }

        private static readonly string[] InsertTokenMiddlePattern = new string[]
        {
            "^[slot]seattle[/slot] at [time]5:PM[/time]$",
            "^[slot]seattle[/slot] at [time]3:PM[/time]$",
            "^[slot]seattle[/slot] at [time]2:PM[/time]$",
            "^[slot]seattle[/slot] at [time]8:PM[/time]$",
            "^[slot]seattle[/slot]%[time]tonight[/time]$",
            "^[slot]seattle[/slot]%[time]tonight[/time]$",
            "^[slot]seattle[/slot]%[time]tonight[/time]$",
            "^[slot]seattle[/slot]%[time]tonight[/time]$",
        };

        /// <summary>
        /// Tests that LG will conditionally insert tokens based on statistics (that is,
        /// tokens can be aligned with empty string within the same decision group)
        /// </summary>
        [TestMethod]
        public void TestStatisticalLgInsertTokensMiddleConditional()
        {
            IWordBreaker wordbreaker = new EnglishWordBreaker();
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _wordbreaker, _featureExtractor);
            pattern.Initialize(InsertTokenMiddlePattern);
            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("slot", "somewhere");
            subs.Add("time", "5:AM");
            Assert.AreEqual("^somewhere at 5:AM$", pattern.Render(subs, false, _logger));
            subs.Clear();
            subs.Add("slot", "somewhere");
            subs.Add("time", "tonight");
            Assert.AreEqual("^somewhere%tonight$", pattern.Render(subs, false, _logger));
        }

        private static readonly string[] SsmlPattern = new string[]
        {
            "This <sub alias=\"recipe\">recipe</sub> needs <emphasis>[ingredient_count]1[/ingredient_count] ingredient and has [step_count]5[/step_count] steps.</emphasis><break/>",
            "This <sub alias=\"recipe\">recipe</sub> needs <emphasis>[ingredient_count]2[/ingredient_count] ingredients and has [step_count]1[/step_count] step.</emphasis><break/>",
            "This <sub alias=\"recipe\">recipe</sub> needs <emphasis>[ingredient_count]3[/ingredient_count] ingredients and has [step_count]4[/step_count] steps.</emphasis><break/>",
            "This <sub alias=\"recipe\">recipe</sub> needs <emphasis>[ingredient_count]4[/ingredient_count] ingredients and has [step_count]1[/step_count] step.</emphasis><break/>",
            "This <sub alias=\"recipe\">recipe</sub> needs <emphasis>[ingredient_count]5[/ingredient_count] ingredients and has [step_count]8[/step_count] steps.</emphasis><break/>",
            "This <sub alias=\"recipe\">recipe</sub> needs <emphasis>[ingredient_count]6[/ingredient_count] ingredients and has [step_count]11[/step_count] steps.</emphasis><break/>",
            "This <sub alias=\"recipe\">recipe</sub> needs <emphasis>[ingredient_count]10[/ingredient_count] ingredients and has [step_count]10[/step_count] steps.</emphasis><break/>",
            "This <sub alias=\"recipe\">recipe</sub> needs <emphasis>[ingredient_count]11[/ingredient_count] ingredients and has [step_count]6[/step_count] steps.</emphasis><break/>"
        };

        /// <summary>
        /// Tests that SSML tags are stripped when rendering with useSSML = false
        /// </summary>
        [TestMethod]
        public void TestStatisticalLgStripSSML()
        {
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _wordbreaker, _featureExtractor);
            pattern.Initialize(SsmlPattern);
            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("ingredient_count", "2");
            subs.Add("step_count", "1");
            Assert.AreEqual("This recipe needs 2 ingredients and has 1 step.", pattern.Render(subs, false, _logger));
        }

        /// <summary>
        /// Tests that certain SSML tags come through
        /// </summary>
        [TestMethod]
        public void TestStatisticalLgPreserveSSML()
        {
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _wordbreaker, _featureExtractor);
            pattern.Initialize(SsmlPattern);
            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("ingredient_count", "2");
            subs.Add("step_count", "1");
            Assert.IsTrue(pattern.Render(subs, true, _logger).Contains("This <sub alias=\"recipe\">recipe</sub> needs <emphasis>2 ingredients and has 1 step.</emphasis><break/>"));
        }

        [TestMethod]
        public void TestStatisticalLgCanSerializeModel()
        {
            VirtualPath cacheModelName = new VirtualPath("Test.model");
            InMemoryFileSystem fakeResourceManager = new InMemoryFileSystem();

            IWordBreaker wordbreaker = new EnglishWordBreaker();
            // This call should serialize the model to cache automatically
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, wordbreaker, _featureExtractor);
            bool cached;
            pattern.Initialize(WeatherPattern, out cached, fakeResourceManager, cacheModelName);
            Assert.IsFalse(cached);
            pattern = null;
            
            // Now reload what we cached earlier
            pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, wordbreaker, _featureExtractor);
            pattern.Initialize(WeatherPattern, out cached, fakeResourceManager, cacheModelName);
            Assert.IsTrue(cached);

            // Try again using "force" mode which does not require training data
            pattern = null;
            pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _featureExtractor, fakeResourceManager, cacheModelName);

            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("day", "2");
            subs.Add("condition", "overcast");
            subs.Add("temp", "1");
            Assert.AreEqual("On the 2nd, it will be overcast, and 1 degree.", pattern.Render(subs, false, _logger));
            subs.Clear();
            subs.Add("day", "3");
            subs.Add("condition", "sunny");
            subs.Add("temp", "32");
            Assert.AreEqual("On the 3rd, it will be sunny, and 32 degrees.", pattern.Render(subs, false, _logger));
            subs.Clear();
            subs.Add("day", "15");
            subs.Add("condition", "partly sunny");
            subs.Add("temp", "16");
            Assert.AreEqual("On the 15th, it will be partly sunny, and 16 degrees.", pattern.Render(subs, false, _logger));
        }

        [TestMethod]
        public void TestStatisticalLgPreserveWhitespace()
        {
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _wordbreaker, _featureExtractor);
            pattern.Initialize(new string[] { "%This%is$[slot]a%test[/slot]$of%whitespace%", "%This%is$[slot]the%best%test[/slot]$of%whitespace%" });
            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("slot", "^a$test^");
            Assert.AreEqual("%This%is$^a$test^$of%whitespace%", pattern.Render(subs, false, _logger));
        }

        [TestMethod]
        public void TestStatisticalLgPreserveWhitespace2()
        {
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _wordbreaker, _featureExtractor);
            pattern.Initialize(new string[] { "%[slot1]New York[/slot1]$[slot2]city[/slot2]$is$[slot3]partly cloudy[/slot3]%", "%[slot1]San Francisco[/slot1]$[slot2]city[/slot2]$is$[slot3]foggy[/slot3]%" });
            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("slot1", "Des Moines");
            subs.Add("slot2", "county");
            subs.Add("slot3", "in space");
            Assert.AreEqual("%Des Moines$county$is$in space%", pattern.Render(subs, false, _logger));
        }

        [TestMethod]
        public void TestStatisticalLgPreserveWhitespace3()
        {
            IWordBreaker wordbreaker = new EnglishWordBreaker();
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _wordbreaker, _featureExtractor);
            pattern.Initialize(new string[] { "<!_ [slot1]New York[/slot1] [slot2]Knicks[/slot2] _!>", "<!_ [slot1]Seattle[/slot1] [slot2]Sonics[/slot2] _!>" });
            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("slot1", "Seattle");
            subs.Add("slot2", "Sonics");
            Assert.AreEqual("<!_ Seattle Sonics _!>", pattern.Render(subs, false, _logger));
        }
        
        [TestMethod]
        public void TestStatisticalLgEmptyPattern()
        {
            IWordBreaker wordbreaker = new EnglishWordBreaker();
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, wordbreaker, _featureExtractor);
            pattern.Initialize(new string[0]);
            IDictionary<string, string> subs = new Dictionary<string, string>();
            Assert.AreEqual("", pattern.Render(subs, false, _logger));
        }

        /// <summary>
        /// Verifies that SSML in the training will be properly output when rendering voice,
        /// stripped when rendering text, and its attributes can be substituted via normal mechanisms
        /// </summary>
        [TestMethod]
        public void TestStatisticalLgHandlesSsmlTags()
        {
            IWordBreaker wordbreaker = new EnglishWordBreaker();
            StatisticalLGPhrase pattern = new StatisticalLGPhrase(
                "TestPhrase",
                LanguageCode.EN_US,
                _logger,
                wordbreaker,
                _featureExtractor);
            pattern.Initialize(new string[] { "<p>I <prosody><prosody name=\"[emo]confident[/emo]\" value=\"0.4\"/>found</prosody> <say-as interpret-as=\"digits\">[slot1]3[/slot1]</say-as> results.</p>" });
            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("slot1", "10");
            // this verifies that the contents of the ssml tag itself can be augmented via substitution
            subs.Add("emo", "exuberant");
            Assert.AreEqual("I found 10 results.", pattern.Render(subs, false, _logger));
            string expected = "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">" +
                 "<p>I <prosody><prosody name=\"exuberant\" value=\"0.4\"/>found</prosody> <say-as interpret-as=\"digits\">10</say-as> results.</p></speak>";
            Assert.AreEqual(expected, pattern.Render(subs, true, _logger));
        }

        private static readonly string[] DatePattern = new string[]
        {
            "On [day]January 1[/day]st.",
            "On [day]February 2[/day]nd.",
            "On [day]March 3[/day]rd.",
            "On [day]April 4[/day]th.",
            "On [day]May 5[/day]th.",
            "On [day]June 6[/day]th.",
            "On [day]July 7[/day]th.",
            "On [day]August 8[/day]th.",
            "On [day]September 9[/day]th.",
            "On [day]October 21[/day]st.",
            "On [day]November 22[/day]nd.",
            "On [day]December 23[/day]rd.",
        };

        /// <summary>
        /// Tests that the number's plurality detector works even when the number is at the end of a tag value
        /// </summary>
        [TestMethod]
        public void TestStatisticalLgDatePattern()
        {
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _wordbreaker, _featureExtractor);
            pattern.Initialize(DatePattern);
            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("day", "March 2");
            Assert.AreEqual("On March 2nd.", pattern.Render(subs, false, _logger));
            subs.Clear();
            subs.Add("day", "October 31");
            Assert.AreEqual("On October 31st.", pattern.Render(subs, false, _logger));
            subs.Clear();
            subs.Add("day", "June 11");
            Assert.AreEqual("On June 11th.", pattern.Render(subs, false, _logger));
            subs.Clear();
            subs.Add("day", "April 22");
            Assert.AreEqual("On April 22nd.", pattern.Render(subs, false, _logger));
            subs.Clear();
            subs.Add("day", "November 28");
            Assert.AreEqual("On November 28th.", pattern.Render(subs, false, _logger));
        }

        //private static readonly string[] OverlappingTokenPattern = new string[]
        //{
        //    "You drank a [quantity]glass[/quantity] of water today.",
        //    "You drank a [quantity]glass[/quantity] of water today.",
        //    "You drank a [quantity]bottle[/quantity] of water today.",
        //    "You drank a [quantity]bottle[/quantity] of water today.",
        //    "You drank some [quantity]glasses[/quantity] of water today.",
        //    "You drank some [quantity]glasses[/quantity] of water today.",
        //    "You drank some [quantity]bottles[/quantity] of water today.",
        //    "You drank some [quantity]bottles[/quantity] of water today.",
        //    "You drank [quantity]a glass[/quantity] of water today.",
        //    "You drank [quantity]a glass[/quantity] of water today.",
        //    "You drank [quantity]a bottle[/quantity] of water today.",
        //    "You drank [quantity]a bottle[/quantity] of water today.",
        //    "You drank [quantity]some glasses[/quantity] of water today.",
        //    "You drank [quantity]some glasses[/quantity] of water today.",
        //    "You drank [quantity]some bottles[/quantity] of water today.",
        //    "You drank [quantity]some bottles[/quantity] of water today.",
        //};

        //[TestMethod]
        //public void TestStatisticalLgOverlappingTokenPattern()
        //{
        //    StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, _logger, _wordbreaker, _featureExtractor, true);
        //    pattern.Initialize(OverlappingTokenPattern);
        //    IDictionary<string, string> subs = new Dictionary<string, string>();
        //    subs.Add("quantity", "bottle");
        //    Assert.AreEqual("You drank a bottle of water today.", pattern.Render(subs, false, _logger));
        //    subs.Clear();
        //    subs.Add("quantity", "a bottle");
        //    Assert.AreEqual("You drank a bottle of water today.", pattern.Render(subs, false, _logger));
        //    subs.Clear();
        //    subs.Add("quantity", "some bottles");
        //    Assert.AreEqual("You drank some bottles of water today.", pattern.Render(subs, false, _logger));
        //    subs.Clear();
        //    subs.Add("quantity", "glass");
        //    Assert.AreEqual("You drank a glass of water today.", pattern.Render(subs, false, _logger));
        //    subs.Clear();
        //    subs.Add("quantity", "a glass");
        //    Assert.AreEqual("You drank a glass of water today.", pattern.Render(subs, false, _logger));
        //    subs.Clear();
        //    subs.Add("quantity", "some glasses");
        //    Assert.AreEqual("You drank some glasses of water today.", pattern.Render(subs, false, _logger));
        //}

        private static readonly string[] VariableListPattern = new string[]
        {
            "You have a meeting at [time_1]1:00[/time_1] [time_2][/time_2][time_3][/time_3]today.",
            "You have a meeting at [time_1]1:00[/time_1] [time_2][/time_2][time_3][/time_3]today.",
            "You have a meeting at [time_1]1:00[/time_1] [time_2][/time_2][time_3][/time_3]today.",
            "You have a meeting at [time_1]1:00[/time_1] [time_2][/time_2][time_3][/time_3]today.",
            "You have a meeting at [time_1]1:00[/time_1] [time_2][/time_2][time_3][/time_3]today.",
            "You have a meeting at [time_1]1:00[/time_1] [time_2][/time_2][time_3][/time_3]today.",
            "You have a meeting at [time_1]1:00[/time_1] [time_2][/time_2][time_3][/time_3]today.",
            "You have meetings at [time_1]1:00[/time_1] and [time_2]1:00[/time_2] [time_3][/time_3]today.",
            "You have meetings at [time_1]1:00[/time_1] and [time_2]1:00[/time_2] [time_3][/time_3]today.",
            "You have meetings at [time_1]1:00[/time_1] and [time_2]1:00[/time_2] [time_3][/time_3]today.",
            "You have meetings at [time_1]1:00[/time_1] and [time_2]1:00[/time_2] [time_3][/time_3]today.",
            "You have meetings at [time_1]1:00[/time_1] and [time_2]1:00[/time_2] [time_3][/time_3]today.",
            "You have meetings at [time_1]1:00[/time_1] and [time_2]1:00[/time_2] [time_3][/time_3]today.",
            "You have meetings at [time_1]1:00[/time_1] and [time_2]1:00[/time_2] [time_3][/time_3]today.",
            "You have meetings at [time_1]1:00[/time_1], [time_2]1:00[/time_2], and [time_3]1:00[/time_3] today.",
            "You have meetings at [time_1]1:00[/time_1], [time_2]1:00[/time_2], and [time_3]1:00[/time_3] today.",
            "You have meetings at [time_1]1:00[/time_1], [time_2]1:00[/time_2], and [time_3]1:00[/time_3] today.",
            "You have meetings at [time_1]1:00[/time_1], [time_2]1:00[/time_2], and [time_3]1:00[/time_3] today.",
            "You have meetings at [time_1]1:00[/time_1], [time_2]1:00[/time_2], and [time_3]1:00[/time_3] today.",
            "You have meetings at [time_1]1:00[/time_1], [time_2]1:00[/time_2], and [time_3]1:00[/time_3] today.",
            "You have meetings at [time_1]1:00[/time_1], [time_2]1:00[/time_2], and [time_3]1:00[/time_3] today.",
        };

        /// <summary>
        /// Tests that the omission of a tag (either empty or null) is a positive signal that can be used to alter the sentence structure
        /// </summary>
        [TestMethod]
        public void TestStatisticalLgVariableListPattern()
        {
            ILogger logger = new ConsoleLogger("LGTests", LogLevel.All);
            ILGFeatureExtractor featureExtractor = new EnglishLGFeatureExtractor();
            IWordBreaker wordbreaker = new EnglishWordBreaker();
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, logger, wordbreaker, featureExtractor);
            pattern.Initialize(VariableListPattern);
            IDictionary<string, string> subs = new Dictionary<string, string>();
            subs.Add("time_1", "7:00");
            Assert.AreEqual("You have a meeting at 7:00 today.", pattern.Render(subs, false, _logger));
            subs.Add("time_2", "4:30");
            Assert.AreEqual("You have meetings at 7:00 and 4:30 today.", pattern.Render(subs, false, _logger));
            subs.Add("time_3", "2:30");
            Assert.AreEqual("You have meetings at 7:00, 4:30, and 2:30 today.", pattern.Render(subs, false, _logger));
        }

        private static readonly string[] VariableListPattern2 = new string[]
        {
            "[lightness][/lightness][saturation]vivid[/saturation]",
            "[lightness]dark[/lightness] [saturation]vivid[/saturation]",
            "[lightness]light[/lightness] [saturation]vivid[/saturation]",
            "[lightness]dark[/lightness] [saturation]dull[/saturation]",
            "[lightness]light[/lightness] [saturation]dull[/saturation]",
            "[lightness]dark[/lightness] [saturation]vivid[/saturation]",
            "[lightness]light[/lightness] [saturation]dull[/saturation]",
            "[lightness]dark[/lightness] [saturation]vivid[/saturation]",
            "[lightness]dark[/lightness][saturation][/saturation]",
            "[lightness]light[/lightness][saturation][/saturation]",
            "[lightness]dark[/lightness][saturation][/saturation]",
            "[lightness]light[/lightness][saturation][/saturation]",
            "[lightness]dark[/lightness][saturation][/saturation]",
            "[lightness]light[/lightness][saturation][/saturation]",
            "[lightness]dark[/lightness][saturation][/saturation]",
            "[lightness][/lightness][saturation]vivid[/saturation]",
            "[lightness][/lightness][saturation]vivid[/saturation]",
            "[lightness][/lightness][saturation]dull[/saturation]",
            "[lightness][/lightness][saturation]dull[/saturation]",
            "[lightness][/lightness][saturation]dull[/saturation]",
            "[lightness][/lightness][saturation]vivid[/saturation]",
            "[lightness][/lightness][saturation][/saturation]",
            "[lightness][/lightness][saturation][/saturation]",
            "[lightness][/lightness][saturation][/saturation]",
            "[lightness][/lightness][saturation][/saturation]",
            "[lightness][/lightness][saturation][/saturation]",
            "[lightness][/lightness][saturation][/saturation]",
            "[lightness][/lightness][saturation][/saturation]",
        };
        
        [TestMethod]
        public void TestStatisticalLgVariableListPattern2()
        {
            ILogger logger = new ConsoleLogger("LGTests", LogLevel.All);
            ILGFeatureExtractor featureExtractor = new EnglishLGFeatureExtractor();
            IWordBreaker wordbreaker = new EnglishWordBreaker();
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, logger, wordbreaker, featureExtractor, true);
            pattern.Initialize(VariableListPattern2);
            IDictionary<string, string> subs = new Dictionary<string, string>();
            Assert.AreEqual("", pattern.Render(subs, false, _logger));
            subs.Add("lightness", "dark");
            Assert.AreEqual("dark", pattern.Render(subs, false, _logger));
            subs.Add("saturation", "vivid");
            Assert.AreEqual("dark vivid", pattern.Render(subs, false, _logger));
            subs.Remove("lightness");
            Assert.AreEqual("vivid", pattern.Render(subs, false, _logger));
        }

        [TestMethod]
        public void TestParseEntireStatisticalTemplateFile()
        {
            ParsedStatisticalLGTemplate parsedFile = ParsedStatisticalLGTemplate.ParseTemplate(
                ("#leading comment\r\n" +
                "\r\n" +
                "\r\n" +
                "  # leading whitespace too\r\n" +
                "[Engine:statistical] # this is a comment\r\n" +
                "[Locales:en-US,en-gb] #another comment\r\n" +
                "#what?\r\n" +
                "[Phrase:QueryStatePhrase]\r\n" +
                "TextModel=QueryStateTextModel\r\n" +
                "Text=Text goes here!\r\n" +
                "Spoken=<speak>Ssml goes here!</speak>\r\n" +
                "SpokenModel=QueryStateSpeechModel\r\n" +
                "Transformer-device=Uppercase\r\n" +
                "Transformer-state=NumberFormat( \"D4\" ), TrimLeft(\" \") ,Uppercase   \r\n" +
                "\r\n" +
                "  # yo I can put more comments # and such \r\n" +
                "[Model:QueryState]#defines a model\r\n" +
                "The[device] living room light[/ device] is currently[state] off[/ state].\r\n" +
                "The[device] living room lights[/ device] are currently[state]off[/ state].\r\n" +
                "The [device]christmas lights[/ device] are currently[state] on[/ state].\r\n" +
                "The [device]fan[/ device] is currently[state] on[/ state]. # this is not a comment!\r\n" +
                "#this is treated as a comment and not a model\r\n" +
                "\r\n" +
                "[TranslationTable:State] #defines a translation table\r\n" +
                "ON=#on\r\n" +
                "OFF=off")
                .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            Assert.IsNotNull(parsedFile);
            Assert.AreEqual("statistical", parsedFile.Engine);
            Assert.AreEqual(2, parsedFile.SupportedLocales.Count);
            Assert.AreEqual(LanguageCode.EN_US, parsedFile.SupportedLocales[0]);
            Assert.AreEqual(LanguageCode.EN_GB, parsedFile.SupportedLocales[1]);
            Assert.AreEqual(3, parsedFile.Blocks.Count);
            Assert.AreEqual(TemplateFileBlockType.Phrase, parsedFile.Blocks[0].BlockType);
            PhraseBlock phrase = parsedFile.Blocks[0] as PhraseBlock;
            Assert.AreEqual("QueryStatePhrase", phrase.Name);
            Assert.IsInstanceOfType(phrase.Properties[0], typeof(KeyValuePhraseProperty));
            Assert.AreEqual("TextModel", phrase.Properties[0].PropertyName);
            Assert.AreEqual("QueryStateTextModel", (phrase.Properties[0] as KeyValuePhraseProperty).Value);
            Assert.IsInstanceOfType(phrase.Properties[1], typeof(KeyValuePhraseProperty));
            Assert.AreEqual("Text", phrase.Properties[1].PropertyName);
            Assert.AreEqual("Text goes here!", (phrase.Properties[1] as KeyValuePhraseProperty).Value);
            Assert.IsInstanceOfType(phrase.Properties[2], typeof(KeyValuePhraseProperty));
            Assert.AreEqual("Spoken", phrase.Properties[2].PropertyName);
            Assert.AreEqual("<speak>Ssml goes here!</speak>", (phrase.Properties[2] as KeyValuePhraseProperty).Value);
            Assert.IsInstanceOfType(phrase.Properties[3], typeof(KeyValuePhraseProperty));
            Assert.AreEqual("SpokenModel", phrase.Properties[3].PropertyName);
            Assert.AreEqual("QueryStateSpeechModel", (phrase.Properties[3] as KeyValuePhraseProperty).Value);
            Assert.IsInstanceOfType(phrase.Properties[4], typeof(TransformerPhraseProperty));
            Assert.AreEqual("Transformer", phrase.Properties[4].PropertyName);
            Assert.AreEqual("device", (phrase.Properties[4] as TransformerPhraseProperty).SlotName);
            Assert.AreEqual(1, (phrase.Properties[4] as TransformerPhraseProperty).TransformChain.Count);
            Assert.IsInstanceOfType(phrase.Properties[5], typeof(TransformerPhraseProperty));
            Assert.AreEqual("Transformer", phrase.Properties[5].PropertyName);
            Assert.AreEqual("state", (phrase.Properties[5] as TransformerPhraseProperty).SlotName);
            Assert.AreEqual(3, (phrase.Properties[5] as TransformerPhraseProperty).TransformChain.Count);

            Assert.AreEqual(TemplateFileBlockType.Model, parsedFile.Blocks[1].BlockType);
            ModelBlock model = parsedFile.Blocks[1] as ModelBlock;
            Assert.AreEqual("QueryState", model.Name);
            Assert.AreEqual(4, model.TrainingLines.Count);
            Assert.AreEqual("The[device] living room light[/ device] is currently[state] off[/ state].", model.TrainingLines[0]);
            Assert.AreEqual("The [device]fan[/ device] is currently[state] on[/ state]. # this is not a comment!", model.TrainingLines[3]);

            Assert.AreEqual(TemplateFileBlockType.TranslationTable, parsedFile.Blocks[2].BlockType);
            TranslationTable table = parsedFile.Blocks[2] as TranslationTable;
            Assert.AreEqual("State", table.Name);
            Assert.AreEqual("#on", table.Mapping["ON"]);
            Assert.AreEqual("off", table.Mapping["OFF"]);
        }

        [TestMethod]
        public void TestParseStatisticalLeadingPhraseWhitespace()
        {
            ParsedStatisticalLGTemplate parsedFile = ParsedStatisticalLGTemplate.ParseTemplate(
                ("[Engine:statistical] \r\n" +
                "[Locales:en-US]\r\n" +
                "\r\n" +
                "[Phrase:QueryStatePhrase]\r\n" +
                "TextModel=QueryStateTextModel\r\n" +
                "\r\n" +
                "[Model:QueryState]\r\n" +
                "     The [device]living room light[/device] is currently [state]off[/state].\r\n" +
                "     The [device]living room lights[/device] are currently [state]off[/state].\r\n" +
                "     The [device]christmas lights[/device] are currently [state]on[/state].\r\n" +
                "     The [device]fan[/device] is currently [state]on[/state].\r\n" +
                "\r\n")
                .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            Assert.IsNotNull(parsedFile);

            Assert.AreEqual(TemplateFileBlockType.Model, parsedFile.Blocks[1].BlockType);
            ModelBlock model = parsedFile.Blocks[1] as ModelBlock;
            Assert.AreEqual("QueryState", model.Name);
            Assert.AreEqual(4, model.TrainingLines.Count);
            Assert.AreEqual("     The [device]living room light[/device] is currently [state]off[/state].", model.TrainingLines[0]);
        }

        [TestMethod]
        public void TestParseStatisticalLeadingPhraseWhitespace2()
        {
            ParsedStatisticalLGTemplate parsedFile = ParsedStatisticalLGTemplate.ParseTemplate(
                ("[Engine:statistical] \r\n" +
                "[Locales:en-US]\r\n" +
                "\r\n" +
                "[Phrase:QueryStatePhrase]\r\n" +
                "TextModel=QueryStateTextModel\r\n" +
                "\r\n" +
                "[Model:QueryState]\r\n" +
                "     The [device]living room light[/device] is currently [state]off[/state].")
                .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            Assert.IsNotNull(parsedFile);

            Assert.AreEqual(TemplateFileBlockType.Model, parsedFile.Blocks[1].BlockType);
            ModelBlock model = parsedFile.Blocks[1] as ModelBlock;
            Assert.AreEqual("QueryState", model.Name);
            Assert.AreEqual(1, model.TrainingLines.Count);
            Assert.AreEqual("     The [device]living room light[/device] is currently [state]off[/state].", model.TrainingLines[0]);
        }

        [TestMethod]
        public void TestParseStatisticalModelBlockSeparation()
        {
            ParsedStatisticalLGTemplate parsedFile = ParsedStatisticalLGTemplate.ParseTemplate(
                ("[Engine:statistical] \r\n" +
                "[Locales:en-US]\r\n" +
                "\r\n" +
                "[Model:QueryState]\r\n" +
                "The [device]living room light[/device] is currently [state]off[/state].\r\n" +
                "The [device]living room lights[/device] are currently [state]off[/state].\r\n" +
                "The [device]christmas lights[/device] are currently [state]on[/state].\r\n" +
                "The [device]fan[/device] is currently [state]on[/state].\r\n" +
                "[Phrase:QueryStatePhrase]\r\n" +
                "TextModel=QueryStateTextModel\r\n" +
                "\r\n")
                .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            Assert.IsNotNull(parsedFile);

            Assert.AreEqual(2, parsedFile.Blocks.Count);
            Assert.AreEqual(TemplateFileBlockType.Model, parsedFile.Blocks[0].BlockType);
            ModelBlock model = parsedFile.Blocks[0] as ModelBlock;
            Assert.AreEqual("QueryState", model.Name);
            Assert.AreEqual(4, model.TrainingLines.Count);
            Assert.AreEqual("The [device]living room light[/device] is currently [state]off[/state].", model.TrainingLines[0]);
            Assert.AreEqual(TemplateFileBlockType.Phrase, parsedFile.Blocks[1].BlockType);
        }

        private static readonly string WeatherTrainingFile =
            "[Engine:statistical]\r\n" +
            "[Locales:en-US]\r\n" +
            "\r\n" +
            "[Phrase:Conditions]\r\n" +
            "TextModel=Conditions\r\n" +
            "\r\n" +
            "[Model:Conditions]\r\n" +
            "On the [day]1[/day]st, it will be [condition]partly cloudy[/condition], and [temp]15[/temp] degrees.\r\n" +
            "On the [day]2[/day]nd, it will be [condition]cloudy[/condition], and [temp]22[/temp] degrees.\r\n" +
            "On the [day]3[/day]rd, it will be [condition]sunny[/condition], and [temp]34[/temp] degrees.\r\n" +
            "On the [day]4[/day]th, it will be [condition]overcast[/condition], and [temp]16[/temp] degrees.\r\n" +
            "On the [day]5[/day]th, it will be [condition]partly cloudy[/condition], and [temp]44[/temp] degrees.\r\n" +
            "On the [day]6[/day]th, it will be [condition]rainy[/condition], and [temp]35[/temp] degrees.\r\n" +
            "On the [day]7[/day]th, it will be [condition]snowy[/condition], and [temp]1[/temp] degree.\r\n" +
            "On the [day]8[/day]th, it will be [condition]sunny[/condition], and [temp]63[/temp] degrees.\r\n" +
            "On the [day]9[/day]th, it will be [condition]mostly cloudy[/condition], and [temp]12[/temp] degrees.\r\n" +
            "On the [day]23[/day]rd, it will be [condition]snowy[/condition], and [temp]1[/temp] degree.\r\n" +
            "On the [day]22[/day]nd, it will be [condition]sunny[/condition], and [temp]-1[/temp] degree.\r\n" +
            "On the [day]21[/day]st, it will be [condition]cloudy[/condition], and [temp]1[/temp] degree.\r\n" +
            "On the [day]31[/day]st, it will be [condition]overcast[/condition], and [temp]10[/temp] degrees.\r\n" +
            "On the [day]11[/day]th, it will be [condition]partly cloudy[/condition], and [temp]-1[/temp] degree.\r\n" +
            "On the [day]32[/day]nd, it will be [condition]overcast[/condition], and [temp]1[/temp] degree.";

        [TestMethod]
        public async Task TestStatisticalLgEngineSingleModel()
        {
            StatisticalLGEngine engine = await BuildEngine(WeatherTrainingFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("Conditions", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("On the 1st, it will be rainy, and 13 degrees.", (await pattern.Sub("day", 1).Sub("condition", "rainy").Sub("temp", 13).Render()).Text);
            Assert.AreEqual("On the 2nd, it will be partly cloudy, and 1 degree.", (await pattern.Sub("day", 2).Sub("condition", "partly cloudy").Sub("temp", 1).Render()).Text);
            Assert.AreEqual("On the 23rd, it will be rainy, and -1 degree.", (await pattern.Sub("day", 23).Sub("condition", "rainy").Sub("temp", -1).Render()).Text);
            Assert.AreEqual("On the 7th, it will be really really really cloudy, and 99 degrees.", (await pattern.Sub("day", 7).Sub("condition", "really really really cloudy").Sub("temp", 99).Render()).Text);
            Assert.AreEqual("On the 41st, it will be rainy, and 10 degrees.", (await pattern.Sub("day", 41).Sub("condition", "rainy").Sub("temp", 10).Render()).Text);
            Assert.AreEqual("On the 99th, it will be null, and 47 degrees.", (await pattern.Sub("day", 99).Sub("condition", "null").Sub("temp", 47).Render()).Text);
            Assert.AreEqual("On the 31st, it will be partly sunny, and 1 degree.", (await pattern.Sub("day", 31).Sub("condition", "partly sunny").Sub("temp", 1).Render()).Text);
        }

        private static readonly string WeatherTrainingFileMultiLocale =
            "[Engine:statistical]\r\n" +
            "[Locales:en-US,en-gb]\r\n" +
            "\r\n" +
            "[Phrase:Conditions]\r\n" +
            "TextModel=Conditions\r\n" +
            "\r\n" +
            "[Model:Conditions]\r\n" +
            "On the [day]1[/day]st, it will be [condition]partly cloudy[/condition], and [temp]15[/temp] degrees.\r\n" +
            "On the [day]2[/day]nd, it will be [condition]cloudy[/condition], and [temp]22[/temp] degrees.\r\n" +
            "On the [day]3[/day]rd, it will be [condition]sunny[/condition], and [temp]34[/temp] degrees.\r\n" +
            "On the [day]4[/day]th, it will be [condition]overcast[/condition], and [temp]16[/temp] degrees.\r\n" +
            "On the [day]5[/day]th, it will be [condition]partly cloudy[/condition], and [temp]44[/temp] degrees.\r\n" +
            "On the [day]6[/day]th, it will be [condition]rainy[/condition], and [temp]35[/temp] degrees.\r\n" +
            "On the [day]7[/day]th, it will be [condition]snowy[/condition], and [temp]1[/temp] degree.\r\n" +
            "On the [day]8[/day]th, it will be [condition]sunny[/condition], and [temp]63[/temp] degrees.\r\n" +
            "On the [day]9[/day]th, it will be [condition]mostly cloudy[/condition], and [temp]12[/temp] degrees.\r\n" +
            "On the [day]23[/day]rd, it will be [condition]snowy[/condition], and [temp]1[/temp] degree.\r\n" +
            "On the [day]22[/day]nd, it will be [condition]sunny[/condition], and [temp]-1[/temp] degree.\r\n" +
            "On the [day]21[/day]st, it will be [condition]cloudy[/condition], and [temp]1[/temp] degree.\r\n" +
            "On the [day]31[/day]st, it will be [condition]overcast[/condition], and [temp]10[/temp] degrees.\r\n" +
            "On the [day]11[/day]th, it will be [condition]partly cloudy[/condition], and [temp]-1[/temp] degree.\r\n" +
            "On the [day]32[/day]nd, it will be [condition]overcast[/condition], and [temp]1[/temp] degree.";

        [TestMethod]
        public async Task TestStatisticalLgEngineMultiLocale()
        {
            StatisticalLGEngine engine = await BuildEngine(
                WeatherTrainingFileMultiLocale,
                LanguageCode.EN_US,
                LanguageCode.EN_GB);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("Conditions", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("On the 1st, it will be rainy, and 13 degrees.", (await pattern.Sub("day", 1).Sub("condition", "rainy").Sub("temp", 13).Render()).Text);

            mockContext.Locale = LanguageCode.EN_GB;

            pattern = engine.GetPattern("Conditions", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("On the 1st, it will be rainy, and 13 degrees.", (await pattern.Sub("day", 1).Sub("condition", "rainy").Sub("temp", 13).Render()).Text);
        }

        /// <summary>
        /// Asserts that when the speech model falls back to a text model, the output is still rendered as ssml
        /// </summary>
        [TestMethod]
        public async Task TestStatisticalLgEngineTagsAllSsml()
        {
            StatisticalLGEngine engine = await BuildEngine(
                WeatherTrainingFileMultiLocale,
                LanguageCode.EN_US,
                LanguageCode.EN_GB);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("Conditions", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            RenderedLG output = await pattern.Sub("day", 1).Sub("condition", "rainy").Sub("temp", 13).Render();

            Assert.AreEqual("On the 1st, it will be rainy, and 13 degrees.", output.Text);
            Assert.AreEqual("<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">On the 1st, it will be rainy, and 13 degrees.</speak>", output.Spoken);
        }

        [TestMethod]
        public async Task TestStatisticalLgEngineMultiLocaleWithFallback()
        {
            foreach (LanguageCode actuallySupportedLocale in new LanguageCode[]
                { 
                    LanguageCode.EN_GB,
                    LanguageCode.EN_US
                })
            {
                StatisticalLGEngine engine = await BuildEngine(WeatherTrainingFileMultiLocale, actuallySupportedLocale);

                ClientContext mockContext = new ClientContext()
                {
                    Locale = LanguageCode.EN_US
                };

                mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
                mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

                ILGPattern pattern = engine.GetPattern("Conditions", mockContext, _logger);
                Assert.IsNotNull(pattern);
                Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
                Assert.AreEqual("On the 1st, it will be rainy, and 13 degrees.", (await pattern.Sub("day", 1).Sub("condition", "rainy").Sub("temp", 13).Render()).Text);

                mockContext.Locale = LanguageCode.EN_GB;

                pattern = engine.GetPattern("Conditions", mockContext, _logger);
                Assert.IsNotNull(pattern);
                Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
                Assert.AreEqual("On the 1st, it will be rainy, and 13 degrees.", (await pattern.Sub("day", 1).Sub("condition", "rainy").Sub("temp", 13).Render()).Text);
            }
        }

        private static readonly string SampleScenarioFile =
            "[Engine:statistical]\r\n" +
            "[Locales:en-US]\r\n" +
            "\r\n" +
            "[Phrase:StateChangedOnOff]\r\n" +
            "TextModel=StateChangedOnOff\r\n" +
            "Transformer-state=Translate(State)\r\n" +
            "[Phrase:StateChangedValue]\r\n" +
            "TextModel=StateChangedValueText\r\n" +
            "SpokenModel=StateChangedValueSpoken\r\n" +
            "\r\n" +
            "[Phrase:Unauthorized]\r\n" +
            "TextModel=UnauthorizedText\r\n" +
            "SpokenModel=UnauthorizedSpoken1\r\n" +
            "Image=unauthorized.png\r\n" +
            "[Phrase:Unauthorized]\r\n" +
            "TextModel=UnauthorizedText\r\n" +
            "SpokenModel=UnauthorizedSpoken2\r\n" +
            "Image=unauthorized.png\r\n" +
            "[Phrase:Unauthorized]\r\n" +
            "TextModel=UnauthorizedText\r\n" +
            "SpokenModel=UnauthorizedSpoken3\r\n" +
            "Image=unauthorized.png\r\n" +
            "\r\n" +
            "[Phrase:QueryState]\r\n" +
            "TextModel=QueryState\r\n" +
            "Transformer-state=Translate(State)\r\n" +
            "\r\n" +
            "[TranslationTable:State]\r\n" +
            "ON=on\r\n" +
            "OFF=off\r\n" +
            "\r\n" +
            "[Model:QueryState]\r\n" +
            "The [device]living room light[/device] is currently [state]off[/state].\r\n" +
            "The [device]living room lights[/device] are currently [state]off[/state].\r\n" +
            "The [device]christmas lights[/device] are currently [state]on[/state].\r\n" +
            "The [device]fan[/device] is currently [state]on[/state].\r\n" +
            "The [device]coffee maker[/device] is currently [state]off[/state].\r\n" +
            "The [device]light[/device] is currently [state]off[/state].\r\n" +
            "The [device]refridgerator[/device] is currently [state]on[/state].\r\n" +
            "The [device]baby monitor[/device] is currently [state]off[/state].\r\n" +
            "The [device]sprinkers[/device] are currently [state]on[/state].\r\n" +
            "\r\n" +
            "[Model:UnauthorizedText]\r\n" +
            "The SmartThings scenario works but it also controls my actual house right now, so I have to lock you out. Sorry!\r\n" +
            "\r\n" +
            "[Model:UnauthorizedSpoken1]\r\n" +
            "Sorry, you don't have the credentials to do that.\r\n" +
            "\r\n" +
            "[Model:UnauthorizedSpoken2]\r\n" +
            "I can't do that until I know who you are.\r\n" +
            "\r\n" +
            "[Model:UnauthorizedSpoken3]\r\n" +
            "You're not authorized to do that now.\r\n" +
            "\r\n" +
            "[Model:StateChangedOnOff]\r\n" +
            "I turned [state]off[/state] the [device]living room light[/device].\r\n" +
            "I turned [state]on[/state] the [device]coffee maker[/device].\r\n" +
            "\r\n" +
            "[Model:StateChangedValueText]\r\n" +
            "I set the [device]living room light[/device] to [value]100[/value]%.\r\n" +
            "\r\n" +
            "[Model:StateChangedValueSpoken]\r\n" +
            "I set the [device]living room light[/device] to [value]100[/value] percent.";

        [TestMethod]
        public async Task TestStatisticalLgEngineSampleScenario()
        {
            StatisticalLGEngine engine = await BuildEngine(SampleScenarioFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("StateChangedOnOff", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("I turned on the sprinklers.", (await pattern.Sub("state", "ON").Sub("device", "sprinklers").Render()).Text);
            Assert.AreEqual("I turned off the computer.", (await pattern.Sub("state", "OFF").Sub("device", "computer").Render()).Text);

            pattern = engine.GetPattern("Unauthorized", mockContext, _logger, true, 2);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("unauthorized.png", (await pattern.Render()).ExtraFields["Image"]);
            Assert.IsTrue((await pattern.Render()).Spoken.Contains("You're not authorized to do that now."));
        }

        [TestMethod]
        public async Task TestStatisticalLgEnginePhraseNumDeterminism()
        {
            StatisticalLGEngine engine = await BuildEngine(SampleScenarioFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("Unauthorized", mockContext, _logger, true, 0);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.IsTrue((await pattern.Render()).Spoken.Contains("Sorry, you don't have the credentials to do that."));

            pattern = engine.GetPattern("Unauthorized", mockContext, _logger, true, 1);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.IsTrue((await pattern.Render()).Spoken.Contains("I can't do that until I know who you are."));

            pattern = engine.GetPattern("Unauthorized", mockContext, _logger, true, 3);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.IsTrue((await pattern.Render()).Spoken.Contains("Sorry, you don't have the credentials to do that."));
        }

        private static readonly string TransformerTestFile =
            "[Engine:statistical]\r\n" +
            "[Locales:en-US]\r\n" +
            "\r\n" +
            "[Phrase:StateChanged]\r\n" +
            "TextModel=StateChanged\r\n" +
            "Transformer-state=Translate(State)\r\n" +
            "[Phrase:StateChangedCapital]\r\n" +
            "TextModel=StateChanged\r\n" +
            "Transformer-state=Translate(State),Capitalize\r\n" +
            "\r\n" +
            "[Model:StateChanged]\r\n" +
            "I turned the light [state]off[/state].\r\n" +
            "\r\n" +
            "[TranslationTable:State]\r\n" +
            "OFF=off\r\n" +
            "ON=on\r\n" +
            "\r\n" +
            "[Phrase:Steps]\r\n" +
            "TextModel=StepsModel\r\n" +
            "Transformer-steps=Uppercase\r\n" +
            "Transformer-cheer=Subphrase(Cheer)\r\n" +
            "[Phrase:Cheer]\r\n" +
            "TextModel=CheerModel\r\n" +
            "Transformer-remaining=Uppercase\r\n" +
            "[Model:StepsModel]\r\n" +
            "You took [steps]ten[/steps] steps today. [cheer][/cheer]\r\n" +
            "\r\n" +
            "[Model:CheerModel]\r\n" +
            "Only [remaining]five[/remaining] more to go!\r\n" +
            "\r\n" +
            "[Phrase:Formatters]\r\n" +
            "TextModel=FormattersModel\r\n" +
            "Transformer-number=NumberFormat(\"F3\"), TrimRight(\"0\") ,TrimRight(\".\")\r\n" +
            "Transformer-time=DateTimeFormat(\"h:mm tt\")\r\n" +
            "[Model:FormattersModel]\r\n" +
            "The number is [number]10[/number]. The time is [time]11:40 PM[/time]\r\n" +
            "\r\n" +
            "\r\n";

        [TestMethod]
        public async Task TestStatisticalLgEngineTranslationTables()
        {
            StatisticalLGEngine engine = await BuildEngine(TransformerTestFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("StateChanged", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("I turned the light on.", (await pattern.Sub("state", "ON").Render()).Text);
            Assert.AreEqual("I turned the light off.", (await pattern.Sub("state", "OFF").Render()).Text);
            // unknown values should just pass through
            Assert.AreEqual("I turned the light SIDEWAYS.", (await pattern.Sub("state", "SIDEWAYS").Render()).Text);
        }

        [TestMethod]
        public async Task TestStatisticalLgEngineChainedTransformers()
        {
            StatisticalLGEngine engine = await BuildEngine(TransformerTestFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("StateChangedCapital", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("I turned the light On.", (await pattern.Sub("state", "ON").Render()).Text);
            Assert.AreEqual("I turned the light Off.", (await pattern.Sub("state", "OFF").Render()).Text);
            Assert.AreEqual("I turned the light SIDEWAYS.", (await pattern.Sub("state", "SIDEWAYS").Render()).Text);
        }

        [TestMethod]
        public async Task TestStatisticalLgEngineSubphraseTransformer()
        {
            StatisticalLGEngine engine = await BuildEngine(TransformerTestFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("Steps", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("You took TEN steps today. Only FIVE more to go!", (await pattern.Sub("steps", "ten").Sub("remaining", "five").Render()).Text);
        }

        [TestMethod]
        public async Task TestStatisticalLgEngineFormatTransformers()
        {
            StatisticalLGEngine engine = await BuildEngine(TransformerTestFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("Formatters", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("The number is 230.434. The time is 11:03 AM", (await pattern.Sub("number", "230.4343217").Sub("time", "2017-01-01T11:03:23").Render()).Text);
            Assert.AreEqual("The number is 332. The time is 1:03 PM", (await pattern.Sub("number", "332.000017").Sub("time", "2017-01-01T13:03:23").Render()).Text);
        }

        private static readonly string SlotNameTestFile =
            "[Engine:statistical]\r\n" +
            "[Locales:en-US]\r\n" +
            "\r\n" +
            "[Phrase:Test]\r\n" +
            "TextModel=Test\r\n" +
            "Transformer-slot-1one=Uppercase\r\n" +
            "Transformer-slot.two=Uppercase\r\n" +
            "[Model:Test]\r\n" +
            "My [slot-1one]name[/slot-1one] is [slot.two]Bevin[/slot.two]\r\n" +
            "My [slot-1one]head[/slot-1one] is [slot.two]an egg[/slot.two]\r\n" +
            "My [slot-1one]face[/slot-1one] is [slot.two]blue[/slot.two]\r\n" +
            "My [slot-1one]hair[/slot-1one] is [slot.two]red[/slot.two]\r\n";

        [TestMethod]
        public async Task TestStatisticalLgEngineValidSlotNames()
        {
            StatisticalLGEngine engine = await BuildEngine(SlotNameTestFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("Test", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("My NAME is PAPYRUS", (await pattern.Sub("slot-1one", "name").Sub("slot.two", "papyrus").Render()).Text);
        }

        private static readonly string InlineModelTestFile =
            "[Engine:statistical]\r\n" +
            "[Locales:en-US]\r\n" +
            "\r\n" +
            "[Phrase:Test]\r\n" +
            "Text=This is a test\r\n" +
            "Spoken=<p>This is a test</p>\r\n" +
            "ShortText=Test\r\n";

        [TestMethod]
        public async Task TestStatisticalLgEngineInlineModels()
        {
            StatisticalLGEngine engine = await BuildEngine(InlineModelTestFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("Test", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("This is a test", (await pattern.Render()).Text);
            Assert.AreEqual("<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><p>This is a test</p></speak>", (await pattern.Render()).Spoken);
            Assert.AreEqual("Test", (await pattern.Render()).ShortText);
        }

        private static readonly string VariantConfigTestFile =
            "[Engine:statistical]\r\n" +
            "[Locales:en-US]\r\n" +
            "\r\n" +
            "[Phrase:Test]\r\n" +
            "TextModel=TestFallback\r\n" +
            "[Phrase:Test]\r\n" +
            "TextModel=TestCortanaUnknown\r\n" +
            "VariantConstraints=channel:cortana\r\n" +
            "[Phrase:Test]\r\n" +
            "TextModel=TestCortanaChat\r\n" +
            "VariantConstraints=channel:cortana,canvas:chat\r\n" +
            "[Phrase:Test]\r\n" +
            "TextModel=TestCortanaSpeak\r\n" +
            "VariantConstraints=channel:cortana,canvas:speak\r\n" +
            "[Model:TestFallback]\r\n" +
            "Fallback\r\n" +
            "\r\n" +
            "[Model:TestCortanaUnknown]\r\n" +
            "CortanaUnknown\r\n" +
            "\r\n" +
            "[Model:TestCortanaChat]\r\n" +
            "CortanaChat\r\n" +
            "\r\n" +
            "[Model:TestCortanaSpeak]\r\n" +
            "CortanaSpeak\r\n" +
            "\r\n";

        [TestMethod]
        public async Task TestStatisticalLgEnginePhraseVariants()
        {
            StatisticalLGEngine engine = await BuildEngine(VariantConfigTestFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern(
                "Test",
                mockContext,
                new Dictionary<string, string>
                {
                    { "channel", "telegram" },
                    { "canvas", "chat" },
                    { "hint", "yes" }
                },
                _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("Fallback", (await pattern.Render()).Text);

            pattern = engine.GetPattern(
                "Test",
                mockContext,
                new Dictionary<string, string>
                {
                    { "channel", "cortana" },
                    { "canvas", "somethingelse" },
                    { "hint", "yes" }
                },
                _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("CortanaUnknown", (await pattern.Render()).Text);

            pattern = engine.GetPattern(
                "Test",
                mockContext,
                new Dictionary<string, string>
                {
                    { "channel", "cortana" },
                    { "canvas", "chat" },
                    { "hint", "yes" }
                },
                _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("CortanaChat", (await pattern.Render()).Text);

            pattern = engine.GetPattern(
                "Test",
                mockContext,
                new Dictionary<string, string>
                {
                    { "channel", "cortana" },
                    { "canvas", "speak" },
                    { "hint", "yes" }
                },
                _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("CortanaSpeak", (await pattern.Render()).Text);
        }

        private static readonly string SubphraseTestFile =
            "[Engine:statistical]\r\n" +
            "[Locales:en-US]\r\n" +
            "\r\n" +
            "[Phrase:LastMessage]\r\n" +
            "TextModel=LastMessage\r\n" +
            "Transformer-time=Subphrase(CustomTime)\r\n" +
            "\r\n" +
            "[Model:LastMessage]\r\n" +
            "Your last message was [time]59 minutes ago[/time].\r\n" +
            "\r\n";

        [TestMethod]
        public async Task TestStatisticalLgEngineCustomPhrase()
        {
            StatisticalLGEngine engine = await BuildEngine(SubphraseTestFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            engine.RegisterCustomCode("CustomTime", CustomLgMethod, LanguageCode.EN_US);

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("CustomTime", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGCustomCodeWrapper));
            Assert.AreEqual("53 minutes ago", (await pattern.Sub("minutes_offset", -53).Render()).Text);
        }

        [TestMethod]
        public async Task TestStatisticalLgEngineCustomPhraseSubphrase()
        {
            StatisticalLGEngine engine = await BuildEngine(SubphraseTestFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            engine.RegisterCustomCode("CustomTime", CustomLgMethod, LanguageCode.EN_US);

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("LastMessage", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.IsInstanceOfType(pattern, typeof(StatisticalLGPattern));
            Assert.AreEqual("Your last message was 23 minutes ago.", (await pattern.Sub("minutes_offset", -23).Render()).Text);
        }

        private static RenderedLG CustomLgMethod(
            IDictionary<string, object> substitutions,
            ILogger logger,
            ClientContext clientContext)
        {
            int minutesOffset = 0;
            if (substitutions.ContainsKey("minutes_offset") && substitutions["minutes_offset"] is int)
            {
                minutesOffset = (int)substitutions["minutes_offset"];
            }

            return new RenderedLG()
            {
                Text = Math.Abs(minutesOffset) + " minutes ago"
            };
        }

        private static readonly string ScriptTestFile =
            "[Engine:statistical]\r\n" +
            "[Locales:en-US]\r\n" +
            "\r\n" +
            "[Phrase:LastMessage]\r\n" +
            "Script=GenerateLastMessage\r\n" +
            "Text=[returnVal][/returnVal]\r\n" +
            "\r\n" +
            "[Phrase:Distance]\r\n" +
            "Script=SelectDistanceResponse\r\n" +
            "\r\n" +
            "[Phrase:DistanceMiles]\r\n" +
            "Text=You ran [distance]5[/distance] miles.\r\n" +
            "\r\n" +
            "[Phrase:DistanceKilometers]\r\n" +
            "Text=You ran [distance]5[/distance] kilometers.\r\n" +
            "\r\n" +
            "[Script:GenerateLastMessage]\r\n" +
            "int value = (int)Substitutions[\"value\"];\r\n" +
            "if (value < 0)\r\n" +
            "    Substitutions[\"returnVal\"] = \"Your last message was \" + Math.Abs(value) + \" minutes ago.\";\r\n" +
            "else\r\n" +
            "    Substitutions[\"returnVal\"] = \"Your next message is in \" + value + \" minutes.\";\r\n" +
            "\r\n" +
            "[Script:SelectDistanceResponse]\r\n" +
            "string unit = (string)Substitutions[\"unit\"];\r\n" +
            "if (string.Equals(\"KILOMETER\", unit))\r\n" +
            "    PhraseName = \"DistanceKilometers\";\r\n" +
            "else\r\n" +
            "    PhraseName = \"DistanceMiles\";\r\n" +
            "\r\n";

        [TestMethod]
        public async Task TestStatisticalLgEngineScriptGeneratedOutput()
        {
            StatisticalLGEngine engine = await BuildEngine(ScriptTestFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            ILGPattern pattern = engine.GetPattern("LastMessage", mockContext, _logger);
            Assert.IsNotNull(pattern);
            Assert.AreEqual("Your last message was 50 minutes ago.", (await pattern.Sub("value", -50).Render()).Text);
            Assert.AreEqual("Your next message is in 25 minutes.", (await pattern.Sub("value", 25).Render()).Text);
        }

        [TestMethod]
        public async Task TestStatisticalLgEngineScriptPhraseSelection()
        {
            StatisticalLGEngine engine = await BuildEngine(ScriptTestFile, LanguageCode.EN_US);

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            EventOnlyLogger eventLogger = new EventOnlyLogger("LGTests", LogLevel.All);
            ILogger consoleLogger = new ConsoleLogger("LGTests", LogLevel.All);
            ILogger debugLogger = new AggregateLogger("LGTests", new TaskThreadPool(), eventLogger, consoleLogger);
            ILGPattern pattern = engine.GetPattern("Distance", mockContext, debugLogger, true);
            Assert.IsNotNull(pattern);
            Assert.AreEqual("You ran 5 kilometers.", (await pattern.Sub("distance", 5).Sub("unit", "KILOMETER").Render()).Text);
            Assert.AreEqual("You ran 5 miles.", (await pattern.Sub("distance", 5).Sub("unit", "MILE").Render()).Text);

            // We ran this template in debug mode, so assert that the logger generated the proper messages
            ILoggingHistory history = eventLogger.History;
            Assert.IsTrue(history.FilterByCriteria(new FilterCriteria()
            {
                ExactComponentName = "LGPattern-Distance"
            }).Count() > 0);
            Assert.IsTrue(history.FilterByCriteria(new FilterCriteria()
            {
                ExactComponentName = "LGPattern-DistanceMiles"
            }).Count() > 0);
            Assert.IsTrue(history.FilterByCriteria(new FilterCriteria()
            {
                ExactComponentName = "LGPattern-DistanceKilometers"
            }).Count() > 0);
        }

        private static readonly string ScriptErrorFile =
            "[Engine:statistical]\r\n" +
            "[Locales:en-US]\r\n" +
            "\r\n" +
            "[Script:Good]\r\n" +
            "int value = (int)Substitutions[\"value\"];\r\n" +
            "if (value < 0)\r\n" +
            "    Substitutions[\"returnVal\"] = \"Your last message was \" + Math.Abs(value) + \" minutes ago.\";\r\n" +
            "else\r\n" +
            "    Substitutions[\"returnVal\"] = \"Your next message is in \" + value + \" minutes.\";\r\n" +
            "[Script:Bad]\r\n" +
            "string unit = (string)Substitutions[\"unit\"];\r\n" +
            "if (string.Equals(\"KILOMETER\", unit))\r\n" +
            "    PhraseName = DistanceKilometers; // error here\r\n" +
            "else\r\n" +
            "    PhraseName = \"DistanceMiles\";\r\n" +
            "\r\n";

        [TestMethod]
        public async Task TestStatisticalLgEngineScriptCompileFailure()
        {
            try
            {
                StatisticalLGEngine engine = await BuildEngine(ScriptErrorFile, LanguageCode.EN_US);
                Assert.Fail("Should have thrown an exception");
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
                Assert.IsTrue(e.Message.Contains("Script name: Bad | Line: 5 | Message: The name \'DistanceKilometers\' does not exist"));
            }
        }
    }
}
