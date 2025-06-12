using Durandal;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.LU;
using Durandal.Common.Net;
using Durandal.Common.Test;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.NLP.Language;

namespace Durandal.Tests.E2E
{
    [TestClass]
    public class LUTests
    {
        /// Common Infrastructure
        private static ILogger _logger;

        // LU
        private static LanguageUnderstandingEngine _luEngine;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);

            FakeLUModel model = new FakeLUModel();
            model.Domain = "animalsounds";
            model.AddRegex("cowsay", "what does the (?<animal>.+?) say");
            IList<FakeLUModel> models = new List<FakeLUModel>();
            models.Add(model);

            _luEngine = DialogTestHelpers.BuildLUEngine(_logger.Clone("LU"), models, DefaultRealTimeProvider.Singleton).Await();
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            _luEngine.Dispose();
        }

        /// <summary>
        /// Tests that homophone analysis is properly done in the LU engine
        /// </summary>
        [TestMethod]
        public async Task TestLUProperlyHandleAmbiguousSpeech()
        {
            ILUClient luClient = new NativeLuClient(_luEngine);
            LURequest request = new LURequest();
            request.Context = new ClientContext()
            {
                ClientId = "testclient",
                Locale = LanguageCode.EN_US,
                ClientName = "test",
                UserId = "testuser",
                UTCOffset = -420,
                Capabilities = ClientCapabilities.DisplayUnlimitedText
            };
            request.SpeechInput = new SpeechRecognitionResult();
            request.SpeechInput.RecognitionStatus = SpeechRecognitionStatus.Success;
            request.SpeechInput.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
            {
                DisplayText = "What does the dog say",
                SREngineConfidence = 1.0f
            });
            request.SpeechInput.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
            {
                DisplayText = "What does the hog say",
                SREngineConfidence = 0.9f
            });
            request.SpeechInput.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
            {
                DisplayText = "What does the log say",
                SREngineConfidence = 0.8f
            });
            request.SpeechInput.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
            {
                DisplayText = "What does the smog say",
                SREngineConfidence = 0.7f
            });

            NetworkResponseInstrumented<LUResponse> response = await luClient.MakeQueryRequest(request, _logger);
            LUResponse luResponse = response.Response;
            Assert.IsNotNull(luResponse);
            Assert.AreEqual(4, luResponse.Results.Count);
            foreach (RecognizedPhrase recognizedPhrase in luResponse.Results)
            {
                Assert.AreEqual(1, recognizedPhrase.Recognition.Count);
                foreach (TaggedData tagHyp in recognizedPhrase.Recognition[0].TagHyps)
                {
                    Assert.AreEqual(1, tagHyp.Slots.Count);
                    SlotValue slot = tagHyp.Slots[0];
                    HashSet<string> alternates = new HashSet<string>();
                    alternates.Add(slot.Value);
                    alternates.UnionWith(slot.Alternates);
                    Assert.IsTrue(alternates.Contains("dog"));
                    Assert.IsTrue(alternates.Contains("hog"));
                    Assert.IsTrue(alternates.Contains("smog"));
                    Assert.IsTrue(alternates.Contains("log"));
                }
            }
        }
    }
}
