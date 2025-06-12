using System.Collections.Generic;
using Durandal;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.LU;
using Durandal.Common.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Durandal.Common.Test;
using Durandal.Common.NLP.Annotation;
using Durandal.Common.Config;
using Durandal.Common.File;
using Durandal.Common.Ontology;
using System;
using Durandal.Common.Tasks;
using Durandal.Common.MathExt;
using System.Diagnostics;
using Durandal.Common.Time;
using Durandal.Common.Collections;
using System.Threading;
using Durandal.Tests.Common.LU;
using Durandal.Common.NLP.Language;

namespace Durandal.Tests.Common.LU
{
    [TestClass]
    public class LanguageUnderstandingTests
    {
        private static ILogger _logger;

        private static ClientContext GetMockClientContext()
        {
            return new ClientContext()
            {
                ClientId = "testclient",
                Locale = LanguageCode.EN_US,
                ClientName = "test",
                UserId = "testuser",
                UTCOffset = -420,
                Capabilities = ClientCapabilities.DisplayUnlimitedText
            };
        }

        /// <summary>
        /// Tests that we can make a basic statistical model with one intent
        /// </summary>
        [TestMethod]
        public async Task TestLUTrainStatisticalDomainOneIntent()
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);

            FakeLUModel model = new FakeLUModel();
            model.Domain = "test";
            model.AddTraining("intent1", "this is a test");
            model.AddTraining("intent1", "test");
            model.AddTraining("intent1", "testing");
            model.AddTraining("intent1", "this is a test case");
            IList<FakeLUModel> models = new List<FakeLUModel>();
            models.Add(model);

            LanguageUnderstandingEngine _luEngine = await DialogTestHelpers.BuildLUEngine(_logger.Clone("LU"), models, DefaultRealTimeProvider.Singleton);

            try
            {
                ILUClient luClient = new NativeLuClient(_luEngine);
                LURequest request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "this is a test";
                NetworkResponseInstrumented<LUResponse> response = await luClient.MakeQueryRequest(request, _logger);
                LUResponse luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("intent1", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence <= 0.999f);
            }
            finally
            {
                _luEngine.Dispose();
            }
        }

        /// <summary>
        /// Tests that we can make a basic statistical model with two intents
        /// </summary>
        [TestMethod]
        public async Task TestLUTrainStatisticalDomainTwoIntents()
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);

            FakeLUModel model = new FakeLUModel();
            model.Domain = "test";
            model.AddTraining("intent1", "this is a test");
            model.AddTraining("intent1", "test");
            model.AddTraining("intent1", "testing");
            model.AddTraining("intent1", "this is a test case");
            model.AddTraining("intent2", "my name is darth vader");
            model.AddTraining("intent2", "my name is louis prima");
            model.AddTraining("intent2", "my name is mr. x");
            model.AddTraining("intent2", "my name is abraham lincoln");
            IList<FakeLUModel> models = new List<FakeLUModel>();
            models.Add(model);

            LanguageUnderstandingEngine _luEngine = await DialogTestHelpers.BuildLUEngine(_logger.Clone("LU"), models, DefaultRealTimeProvider.Singleton);

            try
            {
                ILUClient luClient = new NativeLuClient(_luEngine);
                LURequest request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "this is a test";
                NetworkResponseInstrumented<LUResponse> response = await luClient.MakeQueryRequest(request, _logger);
                LUResponse luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("intent1", luResponse.Results[0].Recognition[0].Intent);

                request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "my name is michaelangelo";
                response = await luClient.MakeQueryRequest(request, _logger);
                luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("intent2", luResponse.Results[0].Recognition[0].Intent);
            }
            finally
            {
                _luEngine.Dispose();
            }
        }

        /// <summary>
        /// Tests that we can make a basic statistical model with one intent
        /// </summary>
        [TestMethod]
        public async Task TestLUTrainStatisticalDomainOneIntentMixedRegexes()
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);

            FakeLUModel model = new FakeLUModel();
            model.Domain = "test";
            model.AddTraining("intent1", "this is a test");
            model.AddTraining("intent1", "test");
            model.AddTraining("intent1", "testing");
            model.AddTraining("intent1", "this is a test case");
            model.AddRegex("intent1", "a test");
            IList<FakeLUModel> models = new List<FakeLUModel>();
            models.Add(model);

            LanguageUnderstandingEngine _luEngine = await DialogTestHelpers.BuildLUEngine(_logger.Clone("LU"), models, DefaultRealTimeProvider.Singleton);

            try
            {
                ILUClient luClient = new NativeLuClient(_luEngine);
                LURequest request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "this is a test";
                NetworkResponseInstrumented<LUResponse> response = await luClient.MakeQueryRequest(request, _logger);
                LUResponse luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("intent1", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence > 0.999f);
            }
            finally
            {
                _luEngine.Dispose();
            }
        }

        /// <summary>
        /// Tests that we can make a basic statistical model with one intent
        /// </summary>
        [TestMethod]
        public async Task TestLUTrainStatisticalDomainOneIntentRegexOnly()
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);

            FakeLUModel model = new FakeLUModel();
            model.Domain = "test";
            model.AddRegex("intent1", "a test");
            IList<FakeLUModel> models = new List<FakeLUModel>();
            models.Add(model);

            LanguageUnderstandingEngine _luEngine = await DialogTestHelpers.BuildLUEngine(_logger.Clone("LU"), models, DefaultRealTimeProvider.Singleton);

            try
            {
                ILUClient luClient = new NativeLuClient(_luEngine);
                LURequest request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "this is a test";
                NetworkResponseInstrumented<LUResponse> response = await luClient.MakeQueryRequest(request, _logger);
                LUResponse luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("intent1", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence > 0.999f);
            }
            finally
            {
                _luEngine.Dispose();
            }
        }

        /// <summary>
        /// Tests that we can make a basic statistical model with one intent
        /// </summary>
        [TestMethod]
        public async Task TestLUTrainStatisticalDomainTwoIntentsB()
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);

            FakeLUModel model = new FakeLUModel();
            model.Domain = "test";
            model.AddTraining("intent1", "would you like an egg roll");
            model.AddRegex("intent2", "a test");
            IList<FakeLUModel> models = new List<FakeLUModel>();
            models.Add(model);

            LanguageUnderstandingEngine _luEngine = await DialogTestHelpers.BuildLUEngine(_logger.Clone("LU"), models, DefaultRealTimeProvider.Singleton);

            try
            {
                ILUClient luClient = new NativeLuClient(_luEngine);
                LURequest request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "would you like an egg roll";
                NetworkResponseInstrumented<LUResponse> response = await luClient.MakeQueryRequest(request, _logger);
                LUResponse luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("intent1", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence <= 0.999f);

                request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "this is a test";
                response = await luClient.MakeQueryRequest(request, _logger);
                luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("intent2", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence > 0.999f);
            }
            finally
            {
                _luEngine.Dispose();
            }
        }

        /// <summary>
        /// Tests that different domains can specify "multiturn-only" intents and that they will not overlap with those of other domains
        /// </summary>
        [TestMethod]
        public async Task TestLUTrainStatisticalMultiturnOnlyIntents()
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);

            FakeLUModel commonDomain = new FakeLUModel();
            commonDomain.Domain = "common";
            commonDomain.AddTraining("side_speech", "yes");
            commonDomain.AddTraining("side_speech", "no");
            commonDomain.AddTraining("side_speech", "up");
            commonDomain.AddTraining("side_speech", "down");
            commonDomain.AddTraining("side_speech", "left");
            commonDomain.AddTraining("side_speech", "right");
            commonDomain.AddTraining("side_speech", "side to side");
            commonDomain.AddTraining("side_speech", "back in time");

            FakeLUModel jokeDomain = new FakeLUModel();
            jokeDomain.Domain = "joke";
            jokeDomain.AddTraining("start", "tell a joke");
            jokeDomain.AddTraining("start", "tell me a joke");
            jokeDomain.AddTraining("start", "tell me a joke please");
            jokeDomain.AddTraining("elaborate", "another one");
            jokeDomain.AddTraining("elaborate", "another one please");
            jokeDomain.AddTraining("elaborate", "tell me another");
            jokeDomain.AddTraining("repeat", "repeat that");
            jokeDomain.AddTraining("repeat", "can you repeat that");
            jokeDomain.AddTraining("repeat", "repeat that for me");

            IConfiguration jokeDomainConfig = new InMemoryConfiguration(_logger);
            jokeDomainConfig.Set("MultiturnIntents", new List<string>() { "elaborate", "repeat" });
            jokeDomain.DomainConfig = jokeDomainConfig;

            FakeLUModel fortuneDomain = new FakeLUModel();
            fortuneDomain.Domain = "fortune";
            fortuneDomain.AddTraining("start", "tell my fortune");
            fortuneDomain.AddTraining("start", "tell me my fortune");
            fortuneDomain.AddTraining("start", "tell me my fortune please");
            fortuneDomain.AddTraining("elaborate", "another one");
            fortuneDomain.AddTraining("elaborate", "another one please");
            fortuneDomain.AddTraining("elaborate", "tell me another");
            fortuneDomain.AddTraining("repeat", "repeat that");
            fortuneDomain.AddTraining("repeat", "can you repeat that");
            fortuneDomain.AddTraining("repeat", "repeat that for me");

            IConfiguration fortuneDomainConfig = new InMemoryConfiguration(_logger);
            fortuneDomainConfig.Set("MultiturnIntents", new List<string>() { "elaborate", "repeat" });
            fortuneDomain.DomainConfig = fortuneDomainConfig;

            IList<FakeLUModel> models = new List<FakeLUModel>();
            models.Add(jokeDomain);
            models.Add(fortuneDomain);
            models.Add(commonDomain);

            LanguageUnderstandingEngine _luEngine = await DialogTestHelpers.BuildLUEngine(_logger.Clone("LU"), models, DefaultRealTimeProvider.Singleton);

            try
            {
                ILUClient luClient = new NativeLuClient(_luEngine);
                LURequest request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "tell me a joke";
                NetworkResponseInstrumented<LUResponse> response = await luClient.MakeQueryRequest(request, _logger);
                LUResponse luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("joke", luResponse.Results[0].Recognition[0].Domain);
                Assert.AreEqual("start", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence > 0.9f);

                request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "tell me my fortune";
                request.ContextualDomains = new List<string>() { "joke", "fortune" };
                response = await luClient.MakeQueryRequest(request, _logger);
                luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("fortune", luResponse.Results[0].Recognition[0].Domain);
                Assert.AreEqual("start", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence > 0.9f);

                request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "another one";
                request.ContextualDomains = new List<string>() { "joke", "fortune" };
                response = await luClient.MakeQueryRequest(request, _logger);
                luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(3, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("elaborate", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence > 0.9f);
                Assert.AreEqual("elaborate", luResponse.Results[0].Recognition[1].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[1].Confidence > 0.9f);
                // Hyp 3 should be common domain

                request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "repeat that";
                request.ContextualDomains = new List<string>() { "joke", "fortune" };
                response = await luClient.MakeQueryRequest(request, _logger);
                luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(3, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("repeat", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence > 0.9f);
                Assert.AreEqual("repeat", luResponse.Results[0].Recognition[1].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[1].Confidence > 0.9f);
                // Hyp 3 should be common domain
                
                // Now try the previous tests again but with more granular contextual domains
                request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "tell me my fortune";
                request.ContextualDomains = new List<string>() { "fortune" };
                response = await luClient.MakeQueryRequest(request, _logger);
                luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("fortune", luResponse.Results[0].Recognition[0].Domain);
                Assert.AreEqual("start", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence > 0.9f);

                request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "another one";
                request.ContextualDomains = new List<string>() { "fortune" };
                response = await luClient.MakeQueryRequest(request, _logger);
                luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(2, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("elaborate", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence > 0.9f);
                Assert.AreEqual("side_speech", luResponse.Results[0].Recognition[1].Intent);

                // And ensure common domain always works
                request = new LURequest();
                request.Context = GetMockClientContext();
                request.TextInput = "yes";
                request.ContextualDomains = new List<string>() { "fortune", "joke" };
                response = await luClient.MakeQueryRequest(request, _logger);
                luResponse = response.Response;
                Assert.IsNotNull(luResponse);
                Assert.AreEqual(1, luResponse.Results.Count);
                Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                Assert.AreEqual("side_speech", luResponse.Results[0].Recognition[0].Intent);
                Assert.IsTrue(luResponse.Results[0].Recognition[0].Confidence > 0.9f);
            }
            finally
            {
                _luEngine.Dispose();
            }
        }

        /// <summary>
        /// Tests that asynchronous annotators all run in parallel
        /// </summary>
        [TestMethod]
        public async Task TestLUAsyncAnnotators()
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);

            FakeLUModel model = new FakeLUModel();
            model.Domain = "test";
            model.AddTraining("intent1", "this is a test");
            model.AddTraining("intent1", "test");
            model.AddTraining("intent1", "testing");
            model.AddTraining("intent1", "this is a test case");
            IList<FakeLUModel> models = new List<FakeLUModel>();
            models.Add(model);

            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                CancellationToken testCancel = cts.Token;

                LanguageUnderstandingEngine luEngine = await DialogTestHelpers.BuildLUEngine(_logger.Clone("LU"), models, DefaultRealTimeProvider.Singleton, new TimeDelayAnnotatorProvider());
                try
                {
                    LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(_logger.Clone("LockStepTime"));
                    IRealTimeProvider bgTaskTime = lockStepTime.Fork("BGTaskTime");
                    Task bgTask = Task.Run(async () =>
                    {
                        try
                        {
                            ILUClient luClient = new NativeLuClient(luEngine);
                            LURequest request = new LURequest();
                            request.Context = GetMockClientContext();
                            request.TextInput = "this is a test";
                            NetworkResponseInstrumented<LUResponse> response = await luClient.MakeQueryRequest(request, _logger, testCancel, bgTaskTime).ConfigureAwait(false);
                            LUResponse luResponse = response.Response;
                            Assert.IsNotNull(luResponse);
                            Assert.AreEqual(1, luResponse.Results.Count);
                            Assert.AreEqual(1, luResponse.Results[0].Recognition.Count);
                            Assert.AreEqual("intent1", luResponse.Results[0].Recognition[0].Intent);

                            Assert.AreEqual(8, luResponse.Results[0].Recognition[0].MostLikelyTags.Annotations.Count);
                        }
                        finally
                        {
                            bgTaskTime.Merge();
                        }
                    });

                    // Each annotator takes 50ms so give ourselves 90 ms of virtual time to run
                    lockStepTime.Step(TimeSpan.FromMilliseconds(90), 10);

                    await bgTask;
                }
                finally
                {
                    luEngine.Dispose();
                }
            }
        }

        private class TimeDelayAnnotator : IAnnotator
        {
            private string _name;

            public TimeDelayAnnotator(string name)
            {
                _name = name;
            }

            public string Name => _name;

            public async Task<object> AnnotateStateless(RecoResult input, LURequest originalRequest, IConfiguration modelConfig, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                await realTime.WaitAsync(TimeSpan.FromMilliseconds(50), cancelToken);
                return null;
            }

            public async Task CommitAnnotation(object asyncState, RecoResult result, LURequest originalRequest, KnowledgeContext entityContext, IConfiguration modelConfig, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                result.MostLikelyTags.Annotations.Add(_name, "testvalue");
                await DurandalTaskExtensions.NoOpTask;
            }

            public bool Initialize()
            {
                return true;
            }

            public void Reset()
            {
            }
        }

        private class TimeDelayAnnotatorProvider : IAnnotatorProvider
        {
            public IAnnotator CreateAnnotator(string name, LanguageCode locale, ILogger logger)
            {
                IAnnotator returnVal = new TimeDelayAnnotator(name);
                returnVal.Initialize();
                return returnVal;
            }

            public Durandal.Common.Collections.IReadOnlySet<string> GetAllAnnotators()
            {
                HashSet<string> returnVal = new HashSet<string>();
                for (int c = 0; c < 8; c++)
                {
                    returnVal.Add("Annotator" + c.ToString());
                }

                return new ReadOnlySetWrapper<string>(returnVal);
            }
        }
    }
}
