using Durandal.API;
using Durandal.Common.Dialog.Services;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.Statistics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Ontology
{
    [TestClass]
    public class EntityResolverTests
    {
        private static readonly List<NamedEntity<int>> COCKTAILS = new List<NamedEntity<int>>
        {
            new NamedEntity<int>(17198, new List<LexicalString>() { new LexicalString("martini"), new LexicalString("vodka martini") }),
            new NamedEntity<int>(1931707, new List<LexicalString>() { new LexicalString("margarita") }),
            new NamedEntity<int>(165208, new List<LexicalString>() { new LexicalString("mojito") }),
            new NamedEntity<int>(1931727, new List<LexicalString>() { new LexicalString("old fashioned"), new LexicalString("old fashion") }),
            new NamedEntity<int>(1931731, new List<LexicalString>() { new LexicalString("sour") }),
            new NamedEntity<int>(702340, new List<LexicalString>() { new LexicalString("daiquiri") }),
            new NamedEntity<int>(1248205, new List<LexicalString>() { new LexicalString("manhattan") }),
            new NamedEntity<int>(157792, new List<LexicalString>() { new LexicalString("cosmopolitan") }),
            new NamedEntity<int>(1927728, new List<LexicalString>() { new LexicalString("pina colada") }),
            new NamedEntity<int>(892528, new List<LexicalString>() { new LexicalString("bloody mary") }),
            new NamedEntity<int>(459004, new List<LexicalString>() { new LexicalString("long island iced tea"), new LexicalString("long island ice tea") }),
            new NamedEntity<int>(744588, new List<LexicalString>() { new LexicalString("mint julep") }),
            new NamedEntity<int>(115279, new List<LexicalString>() { new LexicalString("negroni") }),
            new NamedEntity<int>(696865, new List<LexicalString>() { new LexicalString("moscow mule") }),
            new NamedEntity<int>(96936, new List<LexicalString>() { new LexicalString("highball") }),
            new NamedEntity<int>(244912, new List<LexicalString>() { new LexicalString("caipirinha") }),
            new NamedEntity<int>(594193, new List<LexicalString>() { new LexicalString("tequila sunrise") }),
            new NamedEntity<int>(164610, new List<LexicalString>() { new LexicalString("mai tai") }),
            new NamedEntity<int>(585600, new List<LexicalString>() { new LexicalString("whiskey sour") }),
            new NamedEntity<int>(1802475, new List<LexicalString>() { new LexicalString("white russian"), new LexicalString("white russia") }),
            new NamedEntity<int>(52850, new List<LexicalString>() { new LexicalString("sex on the beach") }),
            new NamedEntity<int>(1170018, new List<LexicalString>() { new LexicalString("gin and tonic") }),
            new NamedEntity<int>(246742, new List<LexicalString>() { new LexicalString("sazerac") }),
            new NamedEntity<int>(165648, new List<LexicalString>() { new LexicalString("cuba libre") }),
            new NamedEntity<int>(90546, new List<LexicalString>() { new LexicalString("gimlet") }),
            new NamedEntity<int>(26173, new List<LexicalString>() { new LexicalString("tom collins") }),
            new NamedEntity<int>(333422, new List<LexicalString>() { new LexicalString("pisco sour") }),
            new NamedEntity<int>(111737, new List<LexicalString>() { new LexicalString("mimosa") }),
            new NamedEntity<int>(169250, new List<LexicalString>() { new LexicalString("appletini") }),
            new NamedEntity<int>(1668892, new List<LexicalString>() { new LexicalString("americano") }),
            new NamedEntity<int>(1697308, new List<LexicalString>() { new LexicalString("spritz") }),
            new NamedEntity<int>(257067, new List<LexicalString>() { new LexicalString("sangria") }),
            new NamedEntity<int>(124571, new List<LexicalString>() { new LexicalString("champagne cocktail") }),
            new NamedEntity<int>(1030619, new List<LexicalString>() { new LexicalString("french 75") }),
            new NamedEntity<int>(51557, new List<LexicalString>() { new LexicalString("screwdriver") }),
            new NamedEntity<int>(210920, new List<LexicalString>() { new LexicalString("dark n stormy"), new LexicalString("dark and stormy"), new LexicalString("dark n'stormy") }),
            new NamedEntity<int>(380730, new List<LexicalString>() { new LexicalString("singapore sling") }),
            new NamedEntity<int>(481738, new List<LexicalString>() { new LexicalString("bellini") }),
            new NamedEntity<int>(717164, new List<LexicalString>() { new LexicalString("espresso martini") }),
            new NamedEntity<int>(164538, new List<LexicalString>() { new LexicalString("hurricane") }),
            new NamedEntity<int>(129255, new List<LexicalString>() { new LexicalString("sea breeze") }),
            new NamedEntity<int>(698577, new List<LexicalString>() { new LexicalString("hot toddy") }),
            new NamedEntity<int>(595277, new List<LexicalString>() { new LexicalString("blue hawaii") }),
            new NamedEntity<int>(439605, new List<LexicalString>() { new LexicalString("irish coffee") }),
            new NamedEntity<int>(151126, new List<LexicalString>() { new LexicalString("black russian") }),
            new NamedEntity<int>(853448,  new List<LexicalString>() { new LexicalString("pain killer"), new LexicalString("painkiller") }),
            new NamedEntity<int>(179479, new List<LexicalString>() { new LexicalString("kamikaze") }),
            new NamedEntity<int>(1927747, new List<LexicalString>() { new LexicalString("white lady") }),
            new NamedEntity<int>(168356, new List<LexicalString>() { new LexicalString("lemon drop")}),
            new NamedEntity<int>(124569, new List<LexicalString>() { new LexicalString("bramble") })
        };

        private static readonly List<NamedEntity<string>> CONTACTS = new List<NamedEntity<string>>
        {
            new NamedEntity<string>("Katie Snead", new List<LexicalString>() { new LexicalString("katie"), new LexicalString("snead"), new LexicalString("katie snead") }),
            new NamedEntity<string>("Brooke Arnold", new List<LexicalString>() { new LexicalString("brooke"), new LexicalString("arnold"), new LexicalString("brooke arnold") }),
            new NamedEntity<string>("Katie Castleton", new List<LexicalString>() { new LexicalString("katie"), new LexicalString("castleton"), new LexicalString("katie castleton") }),
            new NamedEntity<string>("Becka Wiser", new List<LexicalString>() { new LexicalString("becka"), new LexicalString("wiser"), new LexicalString("becka wiser") }),
        };

        private static readonly List<NamedEntity<string>> CONTACTS_PHONETIC = new List<NamedEntity<string>>
        {
            new NamedEntity<string>("Katie Snead", new List<LexicalString>() { new LexicalString("katie", "keɪtɪ"), new LexicalString("snead", "snɪd"), new LexicalString("katie snead", "keɪtɪsnɪd") }),
            new NamedEntity<string>("Brooke Arnold", new List<LexicalString>() { new LexicalString("brooke", "bɹʊək"), new LexicalString("arnold", "ɑɹnəld"), new LexicalString("brooke arnold", "bɹʊəkɑɹnəld") }),
            new NamedEntity<string>("Katie Castleton", new List<LexicalString>() { new LexicalString("katie", "keɪtɪ"), new LexicalString("castleton", "kæstlɛtən"), new LexicalString("katie castleton", "keɪtɪkæstlɛtən") }),
            new NamedEntity<string>("Becka Wiser", new List<LexicalString>() { new LexicalString("becka", "bɛskə"), new LexicalString("wiser", "waɪzəɹ"), new LexicalString("becka wiser", "bɛskəwaɪzəɹ") }),
        };

        [TestMethod]
        public async Task TestEntityResolverBasicShortList()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(await CreateFakeNlTools(logger)));
            IList<Hypothesis<string>> hyps = await resolver.ResolveEntity<string>(new LexicalString("Becka"), CONTACTS, LanguageCode.EN_US, logger);
            Assert.IsNotNull(hyps);
            Assert.AreNotEqual(0, hyps.Count);
            Assert.IsTrue(hyps[0].Conf > 0.85);
            Assert.AreEqual("Becka Wiser", hyps[0].Value);
        }
        
        [TestMethod]
        public async Task TestEntityResolverBasicAmbiguous()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(await CreateFakeNlTools(logger)));
            IList<Hypothesis<string>> hyps = await resolver.ResolveEntity<string>(new LexicalString("Katie"), CONTACTS, LanguageCode.EN_US, logger);
            Assert.IsNotNull(hyps);
            Assert.IsTrue(hyps.Count >= 2);
            Assert.IsTrue(hyps[0].Conf > 0.85);
            Assert.IsTrue(hyps[1].Conf > 0.85);
            Assert.AreEqual(hyps[0].Conf, hyps[1].Conf, 0.01f);
        }

        [TestMethod]
        public async Task TestEntityResolverBasicAmbiguousPhonetic1()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(await CreateFakeNlTools(logger)));
            IList<Hypothesis<string>> hyps = await resolver.ResolveEntity<string>(new LexicalString("Katie"), CONTACTS_PHONETIC, LanguageCode.EN_US, logger);
            Assert.IsNotNull(hyps);
            Assert.IsTrue(hyps.Count >= 2);
            Assert.IsTrue(hyps[0].Conf > 0.85);
            Assert.IsTrue(hyps[1].Conf > 0.85);
            Assert.AreEqual(hyps[0].Conf, hyps[1].Conf, 0.01f);
        }

        [TestMethod]
        public async Task TestEntityResolverBasicAmbiguousPhonetic2()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(await CreateFakeNlTools(logger)));
            IList<Hypothesis<string>> hyps = await resolver.ResolveEntity<string>(new LexicalString("Katie", "keɪtɪ"), CONTACTS_PHONETIC, LanguageCode.EN_US, logger);
            Assert.IsNotNull(hyps);
            Assert.IsTrue(hyps.Count >= 2);
            Assert.IsTrue(hyps[0].Conf > 0.85);
            Assert.IsTrue(hyps[1].Conf > 0.85);
            Assert.AreEqual(hyps[0].Conf, hyps[1].Conf, 0.01f);
        }

        [TestMethod]
        public async Task TestEntityResolverBasicPhoneticOnly()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(await CreateFakeNlTools(logger)));
            IList<Hypothesis<string>> hyps = await resolver.ResolveEntity<string>(new LexicalString("IgnoreThisStringItIsNotPhonetic", "bɹʉkə"), CONTACTS_PHONETIC, LanguageCode.EN_US, logger);
            Assert.IsNotNull(hyps);
            Assert.IsTrue(hyps.Count >= 1);
            Assert.AreEqual("Brooke Arnold", hyps[0].Value);
        }

        [TestMethod]
        public async Task TestEntityResolverLowQualityMatches()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(await CreateFakeNlTools(logger)));
            IList<Hypothesis<string>> hyps = await resolver.ResolveEntity<string>(new LexicalString("maximum strength skeleton man"), CONTACTS, LanguageCode.EN_US, logger);
            Assert.IsNotNull(hyps);
            Assert.IsTrue(hyps.Count >= 1);
            Assert.IsTrue(hyps[0].Conf < 0.6);

            // Assert there are no zero-conf hyps
            foreach (var hyp in hyps)
            {
                Assert.AreNotEqual(hyp.Conf, 0);
            }
        }

        [TestMethod]
        public async Task TestEntityResolverLowQualityMatchesPhonetic()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(await CreateFakeNlTools(logger)));
            IList<Hypothesis<string>> hyps = await resolver.ResolveEntity<string>(new LexicalString("maximum strength skeleton man", "mæksəməmstɹɛŋkθskɛlətənmæn"), CONTACTS_PHONETIC, LanguageCode.EN_US, logger);
            Assert.IsNotNull(hyps);
            Assert.IsTrue(hyps.Count >= 1);
            Assert.IsTrue(hyps[0].Conf < 0.6);

            // Assert there are no zero-conf hyps
            foreach (var hyp in hyps)
            {
                Assert.AreNotEqual(hyp.Conf, 0);
            }
        }

        [TestMethod]
        public async Task TestEntityResolverBasicLongList()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(await CreateFakeNlTools(logger)));
            IList<Hypothesis<int>> hyps = await resolver.ResolveEntity<int>(new LexicalString("margerita"), COCKTAILS, LanguageCode.EN_US, logger);
            Assert.IsNotNull(hyps);
            Assert.AreNotEqual(0, hyps.Count);
            Assert.IsTrue(hyps[0].Conf > 0.85);
            Assert.AreEqual(1931707, hyps[0].Value);
        }

        [TestMethod]
        public async Task TestEntityResolverVariableLengthLists()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(await CreateFakeNlTools(logger)));

            for (int listLength = 2; listLength <= COCKTAILS.Count; listLength++)
            {
                List<NamedEntity<int>> variableList = new List<NamedEntity<int>>();
                for (int c = 0; c < listLength; c++)
                {
                    variableList.Add(COCKTAILS[c]);
                }

                IList<Hypothesis<int>> hyps = await resolver.ResolveEntity<int>(new LexicalString("margerita"), variableList, LanguageCode.EN_US, logger);
                Assert.IsNotNull(hyps);
                Assert.AreNotEqual(0, hyps.Count);
                Assert.IsTrue(hyps[0].Conf > 0.85);
                Assert.AreEqual(1931707, hyps[0].Value);
            }
        }

        [TestMethod]
        public async Task TestEntityResolverDeduplicatesResults()
        {
            ILogger logger = new ConsoleLogger();
            IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(await CreateFakeNlTools(logger)));
            IList<Hypothesis<int>> hyps = await resolver.ResolveEntity<int>(new LexicalString("white russian"), COCKTAILS, LanguageCode.EN_US, logger);
            Assert.IsNotNull(hyps);
            Assert.AreNotEqual(0, hyps.Count);
            Assert.IsTrue(hyps[0].Conf > 0.85);
            Assert.AreEqual(1802475, hyps[0].Value);
            if (hyps.Count > 1)
            {
                Assert.IsTrue(hyps[1].Value != 1802475);
                Assert.IsTrue(hyps[1].Conf < 0.9f); // Assert that the 2nd place hypothesis has a confidence less than 1
            }
        }

        //[TestMethod]
        //public async Task TestEntityResolverDeduplicatesPhoneticResults()
        //{
        //    ILogger logger = new ConsoleLogger();
        //    IEntityResolver resolver = new DefaultEntityResolver(new GenericEntityResolver(await CreateFakeNlTools(logger)));
        //    IList<Hypothesis<string>> hyps = await resolver.ResolveEntity<string>(new LexicalString("Kate", "keɪt"), CONTACTS_PHONETIC, LanguageCode.EN_US, logger);
        //    Assert.IsNotNull(hyps);
        //    Assert.IsTrue(hyps.Count >= 1);
        //    Assert.IsTrue(hyps[0].Conf < 0.1);
        //}

        //[TestMethod]
        //public async Task TestEntityResolverUsingCachedIndex()
        //{
        //    ILogger logger = new ConsoleLogger();
        //    IEntityResolver resolver = new DefaultEntityResolver(new Dictionary<string, NLPTools>(), logger);
        //    List<string> allNames = new List<string>();
        //    foreach (var entity in COCKTAILS)
        //    {
        //        allNames.AddRange(entity.KnownAs);
        //    }

        //    byte[] serializedIndex = await resolver.BuildEntityResolutionIndex(allNames, LanguageCode.EN_US);
        //    Assert.IsNotNull(serializedIndex);
        //    Assert.AreNotEqual(0, serializedIndex.Length);
        //    IList<Hypothesis<int>> hyps = await resolver.ResolveEntityUsingCachedIndex<int>("margerita", COCKTAILS, LanguageCode.EN_US, serializedIndex);
        //    Assert.IsNotNull(hyps);
        //    Assert.AreNotEqual(0, hyps.Count);
        //    Assert.IsTrue(hyps[0].Conf > 0.85);
        //    Assert.AreEqual(1931707, hyps[0].Value);
        //}

        //[TestMethod]
        //public async Task TestEntityResolverInvalidCachedIndex()
        //{
        //    ILogger logger = new ConsoleLogger();
        //    IEntityResolver resolver = new DefaultEntityResolver(new Dictionary<string, NLPTools>(), logger);
        //    List<string> allNames = new List<string>();
        //    foreach (var entity in COCKTAILS)
        //    {
        //        allNames.AddRange(entity.KnownAs);
        //    }

        //    byte[] serializedIndex = new byte[100];
        //    new FastRandom(5).NextBytes(serializedIndex);

        //    try
        //    {
        //        IList<Hypothesis<int>> hyps = await resolver.ResolveEntityUsingCachedIndex<int>("margerita", COCKTAILS, LanguageCode.EN_US, serializedIndex);
        //        Assert.Fail("Should have thrown an invalid data exception");
        //    }
        //    catch (Exception)
        //    {
        //    }
        //}

        private static async Task<NLPToolsCollection> CreateFakeNlTools(ILogger logger)
        {
            NLPToolsCollection returnVal = new NLPToolsCollection();
            returnVal.Add(LanguageCode.EN_US,
                    new NLPTools()
                    {
                        WordBreaker = new EnglishWholeWordBreaker(),
                        Pronouncer = await EnglishPronouncer.Create(VirtualPath.Root, VirtualPath.Root, logger, NullFileSystem.Singleton)
                    });

            return returnVal;
        }
    }
}
