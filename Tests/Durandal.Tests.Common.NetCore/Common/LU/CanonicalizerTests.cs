using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.Config;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.NLP.Annotation;
using Durandal.Common.Test;
using Durandal.Common.Ontology;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Dialog;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.NLP.Language;

namespace Durandal.Tests.Common.LU
{
    [TestClass]
    public class CanonicalizerTests
    {
        private static ILogger _logger;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);
        }

        private static readonly string CanonicalFile1 =
"<?xml version=\"1.0\"?>" +
"<grammar version=\"1.0\">" +
"  <regex id=\"matcher\" expression=\"(on|off)\" />" +
"  " +
"  <normalization_rule id=\"normalizer\" strict=\"true\">" +
"    <item input=\"on\" output=\"ON\" />" +
"    <item input=\"off\" output=\"OFF\" />" +
"  </normalization_rule>" +
"  " +
"  <rule id=\"canonicalized_value\">" +
"    <item><ruleref uri=\"#matcher\" /><normalizer>normalizer</normalizer></item>" +
"  </rule>" +
"" +
"</grammar>";

        [TestMethod]
        public async Task TestCanonicalizerBasic()
        {
            // Set up a virtual file system first for canonical grammars
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(new VirtualPath("\\canonical\\en-US\\home_automation state.canonical.xml"), Encoding.UTF8.GetBytes(CanonicalFile1));

            CanonicalizationAnnotator canonicalizer = new CanonicalizationAnnotator(fileSystem, LanguageCode.EN_US, _logger);
            Assert.IsTrue(canonicalizer.Initialize());

            KnowledgeContext entityContext = new KnowledgeContext();

            RecoResult rr = new RecoResult()
            {
                Confidence = 0.95f,
                Domain = "home_automation",
                Intent = "change_state",
                Source = "LU",
                Utterance = new Sentence("turn the light on"),
                TagHyps = new List<TaggedData>()
                {
                    new TaggedData()
                    {
                        Utterance = "turn the light on",
                        Annotations = new Dictionary<string, string>(),
                        Confidence = 0.95f,
                        Slots = new List<SlotValue>()
                        {
                            new SlotValue("device", "light", SlotValueFormat.SpokenText),
                            new SlotValue("state", "on", SlotValueFormat.SpokenText)
                        }
                    }
                }
            };

            LURequest luRequest = BuildGenericLuRequest();

            IConfiguration modelConfig = new InMemoryConfiguration(_logger);
            IDictionary<string, string> canonicalConfig = new Dictionary<string, string>();
            canonicalConfig["change_state/state"] = "state";
            modelConfig.Set("Canonicalizers", canonicalConfig);

            object state = await canonicalizer.AnnotateStateless(rr, luRequest, modelConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await canonicalizer.CommitAnnotation(state, rr, luRequest, entityContext, modelConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);

            // Verify that the right slot was canonicalized
            SlotValue annotatedSlot = DialogHelpers.TryGetSlot(rr, "state");
            Assert.AreEqual("ON", annotatedSlot.Value);
            Assert.AreEqual(1, annotatedSlot.Annotations.Count);
            Assert.AreEqual("on", annotatedSlot.Annotations[SlotPropertyName.NonCanonicalValue]);
        }

        private static LURequest BuildGenericLuRequest()
        {
            return new LURequest()
            {
                Context = new ClientContext()
                {
                    Locale = LanguageCode.EN_US,
                    ClientId = "test",
                    UserId = "test",
                    Capabilities = ClientCapabilities.DisplayUnlimitedText,
                    ClientName = "unit test"
                }
            };
        }
    }
}
