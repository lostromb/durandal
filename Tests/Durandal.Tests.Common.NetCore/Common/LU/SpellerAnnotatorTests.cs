using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.Config;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Annotation;
using Durandal.Common.Tasks;
using Durandal.Common.Ontology;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.Dialog;
using Durandal.Tests.Common.LU;
using Durandal.Common.NLP.Language;

namespace Durandal.Tests.Common.LU
{
    [TestClass]
    public class SpellerAnnotatorTests
    {
        [TestMethod]
        public async Task TestBingSpellerSynthetic()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            FakeSpellerServer fakeSpellerServer = new FakeSpellerServer();
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            IHttpClientFactory httpClientFactory = new DirectHttpClientFactory(fakeSpellerServer);
            SpellerAnnotator annotator = new SpellerAnnotator("DummyKey", httpClientFactory, logger);
            Assert.IsTrue(annotator.Initialize());

            RecoResult rr = new RecoResult()
            {
                Confidence = 0.95f,
                Domain = "bing",
                Intent = "search",
                Source = "LU",
                Utterance = new Sentence("search for you recieve this thiung"),
                TagHyps = new List<TaggedData>()
                {
                    new TaggedData()
                    {
                        Utterance = "search for you recieve this thiung",
                        Annotations = new Dictionary<string, string>(),
                        Confidence = 0.95f,
                        Slots = new List<SlotValue>()
                        {
                            new SlotValue("query", "you recieve this thiung", SlotValueFormat.SpokenText)
                        }
                    }
                }
            };

            LURequest luRequest = new LURequest()
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

            IConfiguration modelConfig = new InMemoryConfiguration(logger);
            modelConfig.Set("SlotAnnotator_Speller", new string[] { "search/query" });

            object state = await annotator.AnnotateStateless(rr, luRequest, modelConfig, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await annotator.CommitAnnotation(state, rr, luRequest, new KnowledgeContext(), modelConfig, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);

            // Verify that a place entity came back
            SlotValue annotatedSlot = DialogHelpers.TryGetSlot(rr, "query");
            Assert.IsNotNull(annotatedSlot);
            IList<string> spellSuggestions = annotatedSlot.GetSpellSuggestions();
            Assert.IsNotNull(spellSuggestions);
            Assert.AreNotEqual(0, spellSuggestions.Count);
            Assert.AreEqual("you receive this thing", spellSuggestions[0]);
        }

        public class FakeSpellerServer : IHttpServerDelegate
        {
            public HttpResponse NextResponse;

            public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse resp = await HandleConnectionInternal(serverContext.HttpRequest, cancelToken, realTime).ConfigureAwait(false);
                await serverContext.WritePrimaryResponse(resp, NullLogger.Singleton, cancelToken, realTime).ConfigureAwait(false);
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This method returns an IDisposable so the caller should be responsible for disposal")]
            private async Task<HttpResponse> HandleConnectionInternal(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse returnVal = HttpResponse.OKResponse();
                if (request.RequestFile.Contains("spellcheck"))
                {
                    returnVal.SetContent("{ \"_type\": \"SpellCheck\", \"flaggedTokens\": [{\"offset\": 4, \"token\": \"recieve\", \"type\": \"UnknownToken\", \"suggestions\": [{\"suggestion\": \"receive\", \"score\": 1}]}, {\"offset\": 17, \"token\": \"thiung\", \"type\": \"UnknownToken\", \"suggestions\": [{\"suggestion\": \"thing\", \"score\": 1}]}], \"correctionType\": \"High\"}", "application/json");
                }
                else
                {
                    await DurandalTaskExtensions.NoOpTask;
                    returnVal = HttpResponse.NotFoundResponse();
                }

                return returnVal;
            }
        }
    }
}
