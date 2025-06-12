namespace DialogTests
{
    using System;
    using System.Text;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Durandal.API;
    using Stromberg.Config;
    using Durandal.Common;
    using Stromberg.Logger;
    using Stromberg.Utils.IO;
    using Durandal.Common.Dialog;
    using System.Diagnostics;
    using Durandal.Common.Utils;
    using Durandal.API.Utils;
    using System.Threading.Tasks;

    [TestClass]
    public class ConversationFlowTests
    {
        private static DialogProcessingEngine _dialogEngine;
        private static TestAnswerProvider _mockAnswerProvider;
        private static InMemoryConversationStateCache _conversationStateCache;
        private static ILogger _logger;
        private static FileResourceManager _mockResourceManager;
        private static InMemoryPersistence<DialogAction> _mockDialogActionCache;
        private static DialogEngineParameters _defaultDialogParameters;
        
        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _mockAnswerProvider = new TestAnswerProvider();
            _conversationStateCache = new InMemoryConversationStateCache();
            _mockDialogActionCache = new InMemoryPersistence<DialogAction>();
            _logger = new ConsoleLogger("Main", LogLevel.Err | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn, false);
            _mockResourceManager = new FileResourceManager(_logger);
            _defaultDialogParameters = new DialogEngineParameters(TestCommon.GetTestDialogConfiguration(_logger), _mockAnswerProvider)
            {
                Logger = _logger,
                ResourceManager = _mockResourceManager,
                ConversationStateCache = _conversationStateCache,
                DialogActionCache = MockRegisterDialogAction
            };

            _dialogEngine = new DialogProcessingEngine(_defaultDialogParameters);
            _dialogEngine.LoadAnswer("basic");
            _dialogEngine.LoadAnswer("basictree");
            _dialogEngine.LoadAnswer("retrytree");
            _dialogEngine.LoadAnswer("clientcaps");
            _dialogEngine.LoadAnswer("crossdomain_a");
            _dialogEngine.LoadAnswer("crossdomain_b");
            _dialogEngine.LoadAnswer("cd_super");
            _dialogEngine.LoadAnswer("cd_sub");
            _dialogEngine.LoadAnswer("trigger_a");
            _dialogEngine.LoadAnswer("trigger_b");
            _dialogEngine.LoadAnswer("reflection");
        }

        [TestInitialize]
        public void InitializeTest()
        {
            _conversationStateCache.ClearAllConversationStates();
            _mockAnswerProvider.ResetAllPlugins();
            _mockDialogActionCache.Clear();
        }

        public static string MockRegisterDialogAction(DialogAction action)
        {
            string key = Guid.NewGuid().ToString("N");
            _mockDialogActionCache.Store(key, action);
            return key;
        }

#region Sanity checks

        /// <summary>
        /// Test that the result will skip when no reco results are passed
        /// </summary>
        [TestMethod]
        public void TestNoRecoResults()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                new List<RecoResult>(),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
        }

#endregion

#region Answer plugin API tests

        // Should probably move this to its own file

        // Test that nonexistent and non-loaded plugins cannot be triggered

        // Test that loading and unloading works properly

#endregion

#region Basic singleturn tests

        /// <summary>
        /// Tests that a single plugin can return a success
        /// </summary>
        [TestMethod]
        public void TestBasicSuccess()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basic", "succeed", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that a single plugin can return a failure
        /// </summary>
        [TestMethod]
        public void TestBasicFailure()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basic", "fail", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Failure, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that a single plugin can return a skip
        /// </summary>
        [TestMethod]
        public void TestBasicSkip()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basic", "skip", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that a single plugin can return a success after higher-rated plugins skip
        /// </summary>
        [TestMethod]
        public void TestBasicFallthrough()
        {
            List<RecoResult> results = new List<RecoResult>();
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.99f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.90f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.85f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "succeed", 0.80f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.75f));
            DialogEngineResponse response = _dialogEngine.Process(
                results,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(3, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that a single plugin can return a failure after higher-rated plugins skip
        /// </summary>
        [TestMethod]
        public void TestBasicFallthrough2()
        {
            List<RecoResult> results = new List<RecoResult>();
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.99f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.90f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.85f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "fail", 0.80f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.75f));
            DialogEngineResponse response = _dialogEngine.Process(
                results,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Failure, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(3, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that queries that would normally succeed will instead skip if the confidence is too low
        /// </summary>
        [TestMethod]
        public void TestLowConfidenceSkip()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basic", "succeed", 0.1f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that queries that would normally skip will still skip if the confidence is too low
        /// </summary>
        [TestMethod]
        public void TestLowConfidenceSkip2()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basic", "skip", 0.1f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that queries that would normally fail will instead skip if the confidence is too low
        /// </summary>
        [TestMethod]
        public void TestLowConfidenceSkip3()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basic", "fail", 0.1f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that if the first plugin returns a skip message, that it will be included in the final
        /// dialog result
        /// </summary>
        [TestMethod]
        public void TestSkipReturnsMessageFromTop()
        {
            List<RecoResult> results = new List<RecoResult>();
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skipwithmsg", 0.99f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.90f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.85f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.75f));
            DialogEngineResponse response = _dialogEngine.Process(
                results,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.IsFalse(string.IsNullOrEmpty(response.DisplayedText));
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(4, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that if the 2nd+ plugin skips with a message, it will still be returned.
        /// </summary>
        [TestMethod]
        public void TestSkipReturnsMessageFromMiddle()
        {
            List<RecoResult> results = new List<RecoResult>();
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.99f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skipwithmsg", 0.90f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.85f));
            results.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.75f));
            DialogEngineResponse response = _dialogEngine.Process(
                results,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.IsFalse(string.IsNullOrEmpty(response.DisplayedText));
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(4, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
        }

#endregion

#region Side_speech intent tests

        /// <summary>
        /// Test that side_speech/side_speech will trigger on first-turn side-speech when the ignoreSideSpeech flag is enabled
        /// </summary>
        [TestMethod]
        public void TestAcceptFirstTurnSideSpeech()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            testEngine.LoadAnswer("basic");
            testEngine.LoadAnswer("side_speech");

            DialogEngineResponse response = testEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "side_speech", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.SideSpeechAnswer.SucceedHighTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.SideSpeechAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that a separate "side_speech_highconf" hyp is generated for high confidence first-turn side speech intents, when side speech is enabled on first-turn
        /// </summary>
        [TestMethod]
        public void TestAcceptFirstTurnSideSpeechHighConf()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            testEngine.LoadAnswer("basic");
            testEngine.LoadAnswer("side_speech");

            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(TestCommon.GetSimpleRecoResult("common", "side_speech", 0.9f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("basictree", "start", 0.85f));

            DialogEngineResponse response = testEngine.Process(
                recoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("common", response.TriggeredDomain);
            Assert.AreEqual("side_speech_highconf", response.TriggeredIntent);
            Assert.AreEqual(1, _mockAnswerProvider.SideSpeechAnswer.SucceedHighTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.SideSpeechAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that high-confidence common/side speech will be ignored on the first turn when the ignoreSideSpeech flag is set
        /// </summary>
        [TestMethod]
        public void TestIgnoreFirstTurnSideSpeech()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.IgnoreSideSpeech = true;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            testEngine.LoadAnswer("basic");
            testEngine.LoadAnswer("side_speech");

            DialogEngineResponse response = testEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "side_speech", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.SideSpeechAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.SideSpeechAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that side_speech/side_speech will still trigger on first-turn regardless of its confidence
        /// </summary>
        [TestMethod]
        public void TestLowConfidenceFirstTurnSideSpeech()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            testEngine.LoadAnswer("basic");
            testEngine.LoadAnswer("side_speech");

            DialogEngineResponse response = testEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "side_speech", 0.1f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.SideSpeechAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.SideSpeechAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that if a single high-confidence answer skips, side speech will trigger when the ignoreSideSpeech flag is disabled
        /// In this case, the dialog logic will create a 0-confidence side-speech hyp that still triggers because ignoreSideSpeech is false
        /// </summary>
        [TestMethod]
        public void TestSideSpeechCanTriggerAfterHighConfAnswerSkips()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            testEngine.LoadAnswer("basic");
            testEngine.LoadAnswer("side_speech");

            DialogEngineResponse response = testEngine.Process(
                TestCommon.GetSimpleRecoResultList("basic", "skip", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(DialogConstants.COMMON_DOMAIN, response.TriggeredDomain);
            Assert.AreEqual(DialogConstants.SIDE_SPEECH_INTENT, response.TriggeredIntent);
            Assert.AreEqual(1, _mockAnswerProvider.SideSpeechAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.SideSpeechAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that if a single high-confidence answer skips, side speech will not trigger when the ignoreSideSpeech flag is enabled
        /// In this case, the dialog logic will create a 0-confidence side-speech hyp that is ignored
        /// </summary>
        [TestMethod]
        public void TestSideSpeechIsIgnoredAfterHighConfAnswerSkips()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.IgnoreSideSpeech = true;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            testEngine.LoadAnswer("basic");
            testEngine.LoadAnswer("side_speech");

            DialogEngineResponse response = testEngine.Process(
                TestCommon.GetSimpleRecoResultList("basic", "skip", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);
            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.SideSpeechAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.SideSpeechAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that the answerdomain/side_speech intent will trigger a response on 2nd-turn for answers that are configured to consume it.
        /// </summary>
        [TestMethod]
        public void TestConsumeValidSideSpeechOnSecondTurn()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "side_speech", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.SideSpeechTrigger.Get());
        }

        /// <summary>
        /// Test that the answerdomain/side_speech intent will trigger a response on 2nd-turn for answers that are configured to consume it,
        /// even if the side speech is very low confidence
        /// </summary>
        [TestMethod]
        public void TestConsumeValidSideSpeechOnSecondTurnLowConf()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "side_speech", 0.1f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.SideSpeechTrigger.Get());
        }

        /// <summary>
        /// Tests that, if ignoreSideSpeech is true, a side speech top reco result will not affect
        /// the 2nd turn when in tenative mode
        /// </summary>
        [TestMethod]
        public void TestContinueTenativeConversationAfterHighConfidenceSideSpeech()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTenativeTrigger.Get());

            List<RecoResult> turnTwoResults = new List<RecoResult>();
            turnTwoResults.Add(TestCommon.GetSimpleRecoResult("common", "side_speech", 0.9f));
            turnTwoResults.Add(TestCommon.GetSimpleRecoResult("basictree", "start", 0.8f));

            response = _dialogEngine.Process(
                turnTwoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicTreeAnswer.SideSpeechTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());
        }

        /// <summary>
        /// Tests that, if ignoreSideSpeech is false, a high-confidence side speech hyp will
        /// begin a new conversation with the side_speech domain
        /// </summary>
        [TestMethod]
        public void TestEndTenativeConversationAfterHighConfidenceSideSpeech()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            testEngine.LoadAnswer("basictree");
            testEngine.LoadAnswer("side_speech");

            DialogEngineResponse response = testEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTenativeTrigger.Get());

            List<RecoResult> turnTwoResults = new List<RecoResult>();
            turnTwoResults.Add(TestCommon.GetSimpleRecoResult("common", "side_speech", 0.9f));
            turnTwoResults.Add(TestCommon.GetSimpleRecoResult("basictree", "start", 0.8f));

            response = testEngine.Process(
                turnTwoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicTreeAnswer.SideSpeechTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.SideSpeechAnswer.SucceedHighTrigger.Get());
        }

        /// <summary>
        /// Test that if a 2nd turn node consumes both side speech and a local intent, that side speech's confidence will
        /// be a capped appropriately and will properly defer to the local intent
        /// </summary>
        [TestMethod]
        public void TestSecondTurnSideSpeechIsCappedProperly()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_sidespeech", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            List<RecoResult> turnTwoResults = new List<RecoResult>();
            turnTwoResults.Add(TestCommon.GetSimpleRecoResult("common", "side_speech", 0.99f));
            turnTwoResults.Add(TestCommon.GetSimpleRecoResult("basictree", "turn2", 0.9f));

            response = _dialogEngine.Process(
                turnTwoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicTreeAnswer.SideSpeechTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());
        }

        #endregion
      
#region Multiturn tests
        // test basic transitions, tenative transitions, and common domain rewrite

        /// <summary>
        /// Test that we can take 2 valid turns through the engine
        /// </summary>
        [TestMethod]
        public void TestBasicMultiturn()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that basic multiturn (non-passive) will lock the dialog engine into a single domain
        /// regardless of other domain results
        /// </summary>
        [TestMethod]
        public void TestLockedIntoSingleMultiturnDomain()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            List<RecoResult> recoResults = new List<RecoResult>();

            // Send a bunch of bogus results with high probability
            // and then a really low probability for turn 2
            recoResults.Add(TestCommon.GetSimpleRecoResult("basic", "succeed", 0.99f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("basic", "skip", 0.98f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("basic", "fail", 0.97f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("basictree", "turn2", 0.01f));

            response = _dialogEngine.Process(
                recoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that, after entering into a multiturn conversation, the dialog engine
        /// will honor the lock by ignoring any new conversation-starting intents when
        /// not in tenative multiturn mode
        /// </summary>
        [TestMethod]
        public void TestCannotRestartConversationAfterLocking()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            List<RecoResult> recoResults = new List<RecoResult>();

            // Send a bunch of bogus results with high probability
            // and then a really low probability for turn 2
            recoResults.Add(TestCommon.GetSimpleRecoResult("basictree", "start", 1.0f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("basictree", "turn2", 0.01f));

            response = _dialogEngine.Process(
                recoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that tenative multiturn does not lock us into the domain and we are free to go elsewhere
        /// </summary>
        [TestMethod]
        public void TestCanExitDomainAfterTenativeMultiturn()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTenativeTrigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basic", "succeed", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that tenative multiturn allows us to do multiturn if we decide to keep going in the conversation
        /// </summary>
        [TestMethod]
        public void TestCanContinueDomainAfterTenativeMultiturn()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_tenative", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTenativeTrigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.BasicAnswer.FailTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that we can use common domain results in 2nd turn
        /// </summary>
        [TestMethod]
        public void TestBasicMultiturnUsingCommonDomain()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "confirm", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.ConfirmTrigger.Get());
        }

        /// <summary>
        /// Test that we can use common domain results in 2nd turn with low confidence
        /// </summary>
        [TestMethod]
        public void TestBasicMultiturnUsingCommonDomainLowConf()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "confirm", 0.1f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.ConfirmTrigger.Get());
        }

        /// <summary>
        /// Test that common domain is ignored unless the intents are registered in the tree
        /// </summary>
        [TestMethod]
        public void TestIgnoreInvalidCommonResultsOnMultiturn()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(TestCommon.GetSimpleRecoResult("common", "thanks", 1.0f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("common", "confirm", 0.9f));

            response = _dialogEngine.Process(
                recoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.ConfirmTrigger.Get());
        }

        /// <summary>
        /// Test that common domain results are ignored on the first turn
        /// </summary>
        [TestMethod]
        public void TestIgnoreCommonDomainOnFirstTurn()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "confirm", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
        }

        /// <summary>
        /// Ensures that dialog actions can be cached and executed on a later turn (this just ensures that they are written, it
        /// does not test the ProcessDialogAction method of the web service, nor the handling of ActionURIs)
        /// </summary>
        [TestMethod]
        public void TestMultiturnDialogActions()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_dialogaction", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.ResponseData.ContainsKey("actionKey"));
            string actionKey = response.ResponseData["actionKey"];
            RetrieveResult<DialogAction> cacheResult = _mockDialogActionCache.TryRetrieve(actionKey);
            Assert.IsTrue(cacheResult.Success);
            DialogAction desiredAction = cacheResult.Result;

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList(desiredAction.Domain, desiredAction.Intent, 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Programmatic,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that unknown intents will make us fall off of the tree if no promiscuous edge is enabled
        /// </summary>
        [TestMethod]
        public void TestPromiscuousEdgeNegative()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_lock", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start_lock", response.SelectedRecoResult.Intent);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "weird", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
        }

        /// <summary>
        /// Test that unknown intents will make us fall off of the tree if no promiscuous edge is enabled
        /// </summary>
        [TestMethod]
        public void TestPromiscuousEdgePositive()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start", response.SelectedRecoResult.Intent);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2_promisc", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("turn2_promisc", response.SelectedRecoResult.Intent);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "weird", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("weird", response.SelectedRecoResult.Intent);
        }

        /// <summary>
        /// Test that we can take 2 valid turns through the engine when the first turn ditches
        /// the conversation tree and returns an explicit continuation
        /// </summary>
        [TestMethod]
        public void TestExplicitContinuations()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_continuations", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start_continuations", response.TriggeredIntent);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "continuations_turn_2", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("continuations_turn_2", response.TriggeredIntent);
        }

        /// <summary>
        /// Test that we can take 2 valid turns through the engine when the first turn ditches
        /// the conversation tree and returns an explicit continuation, and the turn 2 input is
        /// in the common domain
        /// </summary>
        [TestMethod]
        public void TestExplicitContinuationsWithCommonIntent()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_continuations", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start_continuations", response.TriggeredIntent);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "confirm", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("confirm", response.TriggeredIntent);
        }

        /// <summary>
        /// Test that continuations are allowed to be static
        /// </summary>
        [TestMethod]
        public void TestExplicitContinuationsStatic()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_continuations", 1.0f, "static"),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start_continuations", response.TriggeredIntent);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "continuations_turn_2", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("continuations_turn_2", response.TriggeredIntent);
        }

        /// <summary>
        /// Test that continuations must not be anonymous
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DialogException))]
        public void TestExplicitContinuationsCannotBeAnonymous()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_continuations", 1.0f, "lambda"),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start_continuations", response.TriggeredIntent);
        }

        /// <summary>
        /// Test that continuations must not be private
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DialogException))]
        public void TestExplicitContinuationsCannotBePrivate()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_continuations", 1.0f, "private"),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start_continuations", response.TriggeredIntent);
        }

        #endregion

        #region Cross-domain tests

        /// <summary>
        /// Tests that a cross-domain call can succeed with no parameters
        /// </summary>
        [TestMethod]
        public void TestCrossDomainBasic()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("crossdomain_a", "basic_a", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_b", response.TriggeredDomain);
            Assert.AreEqual("basic_b", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that a cross-domain call can succeed with no parameters
        /// </summary>
        [TestMethod]
        public void TestCrossDomainWithParameters()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("crossdomain_a", "params_a", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_b", response.TriggeredDomain);
            Assert.AreEqual("params_b", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that a cross-domain call can succeed while passing parameters that come from the _current_ turn's slots
        /// </summary>
        [TestMethod]
        public void TestCrossDomainWithSlotParameters()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            RecoResult hyp = TestCommon.GetSimpleRecoResult("crossdomain_a", "slotparams_a", 1.0f);
            hyp.MostLikelyTags.Slots.Add(new SlotValue("param1", "value1", SlotValueFormat.TypedText));
            List<RecoResult> hyps = new List<RecoResult>();
            hyps.Add(hyp);

            response = _dialogEngine.Process(
                hyps,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_b", response.TriggeredDomain);
            Assert.AreEqual("slotparams_b", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that a cross-domain call fails if the request is not honored
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DialogException))]
        public void TestCrossDomainUnsupportedRequest()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);
            
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("crossdomain_a", "unsupported_request_a", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// Tests that a cross-domain call fails if the response is not honored
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DialogException))]
        public void TestCrossDomainUnsupportedResponse()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);
            
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("crossdomain_a", "unsupported_response_a", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// Tests that a cross-domain call fails if the target intent does not exist
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DialogException))]
        public void TestCrossDomainNoTargetIntent()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);
            
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("crossdomain_a", "no_target_intent", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// Tests that the state of a super domain can continue with a callback after a subdomain finishes
        /// </summary>
        [TestMethod]
        public void TestCrossDomainCanReturnToSuperAfterSubStops()
        {
            // Start super answer
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "continue_callback_a", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("continue_b", response.TriggeredIntent);

            // Talk to sub answer a bit
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_sub", "cancel_b", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("cancel_b", response.TriggeredIntent);

            // Now return to super answer after sub closes
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "carry_on", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsFalse(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("carry_on", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that it is possible to do "one-shot" calls to a subdomain, having control return immediately after the call.
        /// </summary>
        [TestMethod]
        public void TestCrossDomainOneShotInSubDomain()
        {
            // Start super answer
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer. This should be the only turn the the subanswer processes.
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "cancel_callback_a", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("cancel_b", response.TriggeredIntent);

            // Now return to super answer after sub closes
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "carry_on", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsFalse(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("carry_on", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that the state of a super domain can continue with a callback after a subdomain finishes
        /// </summary>
        [TestMethod]
        public void TestCrossDomainCanUseCommonDomainTransitions()
        {
            // Start super answer
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Use the common intent to trigger the domain transition
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList(DialogConstants.COMMON_DOMAIN, "commonintent", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("continue_b", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that a superdomain can transition completely to a subdomain and then have its state be clean afterwards
        /// </summary>
        [TestMethod]
        public void TestCrossDomainCanFinishTwoDomainsAtOnce()
        {
            // Start super answer
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "continue_a", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("continue_b", response.TriggeredIntent);

            // Tell sub answer to finish. The conversation should be done now.
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_sub", "cancel_b", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsFalse(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("cancel_b", response.TriggeredIntent);

            // To make sure, start talking to some other answer entirely
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basic", "succeed", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basic", response.TriggeredDomain);
            Assert.AreEqual("succeed", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that if a subdomain goes tenative, the user can reenter the superdomain by triggering one of its continuations.
        /// </summary>
        [TestMethod]
        public void TestCrossDomainCanReturnToSuperAfterSubIsTenative()
        {
            // Start super answer
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "continue_callback_a", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("continue_b", response.TriggeredIntent);

            // Talk to sub answer a bit
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_sub", "tenative_b", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.IsFalse(response.NextTurnBehavior.IsImmediate);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("tenative_b", response.TriggeredIntent);

            // Now what sub answer is tenative, trigger the super answer again. The sub answer should go away
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "carry_on", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsFalse(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("carry_on", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that if a subdomain goes tenative, the user can reenter the superdomain by triggering one of its continuations, even if the superdomain is also tenative
        /// </summary>
        [TestMethod]
        public void TestCrossDomainCanReturnToTenativeSuperAfterSubIsTenative()
        {
            // Start super answer
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "tenative_callback_a", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("continue_b", response.TriggeredIntent);

            // Talk to sub answer a bit
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_sub", "tenative_b", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.IsFalse(response.NextTurnBehavior.IsImmediate);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("tenative_b", response.TriggeredIntent);

            // Now what sub answer is tenative, trigger the super answer again. The sub answer should go away
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "carry_on", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsFalse(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("carry_on", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that a sub domain can invoke a callback to a super domain and pass slot values as part of its response
        /// </summary>
        [TestMethod]
        public void TestCrossDomainExplicitCallbackWithReturnSlots()
        {
            // Start super answer
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "call_b_with_callback", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("callback_1", response.TriggeredIntent);

            // Give information to the subanswer. This turn executes the subanswer,
            // which in turn invokes the callback to the super, so the super answer is what should
            // generate the response.
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_sub", "callback_2", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("callback_intent_a", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that a super domain can call a subdomain and have it return in one shot, so the subdomain
        /// response is never actually surfaced.
        /// </summary>
        [TestMethod]
        public void TestCrossDomainExplicitCallbackWithReturnSlotsOneShot()
        {
            // Start super answer
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer, but in such a way that it returns immediately. This ends up executing 3 plugins in a row:
            // Super plugin with intent call_b_with_callback_oneshot, executing the CDR exchange
            // Sub plugin with intent callback_2, running logic, and returning a invoked dialog action
            // Super plugin again with intent callback_intent_a, which renders the actual result
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "call_b_with_callback_oneshot", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("callback_intent_a", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that if a subdomain tries to invoke a callback that doesn't exist, an error is returned.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DialogException))]
        public void TestCrossDomainExplicitCallbackToNonexistentIntent()
        {
            // Start super answer
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_super", "call_b_with_callback", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("callback_1", response.TriggeredIntent);

            // Attempt to invoke a nonexistent callback. This should raise an exception
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("cd_sub", "callback_2_bad", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"));
        }

        #endregion

        #region Retry/noreco tests

        /// <summary>
        /// Test that, if we are locked-in, and pass an invalid query to a node that has no retry
        /// handler, that the entire turn will be skipped, and that we can still continue the conversation.
        /// </summary>
        [TestMethod]
        public void TestBasicRetry()
        {
            // start a conversation
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("retrytree", "start_succeed", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.StartSucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.RetryTreeAnswer.RetrySucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.RetryTreeAnswer.Turn2Trigger.Get());

            // send some nonsense. retry should trigger
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "thanks", 0.6f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.StartSucceedTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.RetrySucceedTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.RetryTreeAnswer.Turn2Trigger.Get());
            Assert.IsTrue(response.NextTurnBehavior.Continues);

            // now send valid input to continue the conversation
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("retrytree", "turn2", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.StartSucceedTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.RetrySucceedTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that if a retry continuation returns SKIP, the conversation ends gracefully
        /// and the client still gets a SUCCESS response
        /// </summary>
        [TestMethod]
        public void TestRetryWithSkip()
        {
            // start a conversation
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("retrytree", "start_skip", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.StartSkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.RetryTreeAnswer.RetrySkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.RetryTreeAnswer.Turn2Trigger.Get());

            // send some nonsense. retry should trigger and return "skip"
            // Ensure that this translates into the client recieving a "success" with continues = false
            // and the conversation state is eliminated
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "thanks", 0.6f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.StartSkipTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.RetrySkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.RetryTreeAnswer.Turn2Trigger.Get());
            Assert.IsFalse(response.NextTurnBehavior.Continues);

            // Trying to continue the conversation shouldn't work at this point
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("retrytree", "turn2", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.StartSkipTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.RetrySkipTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.RetryTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that, if a retry continuation returns FAIL, the conversation is dropped
        /// but the client gets a Success response
        /// </summary>
        [TestMethod]
        public void TestRetryWithFail()
        {
            // start a conversation
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("retrytree", "start_fail", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.StartFailTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.RetryTreeAnswer.RetryFailTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.RetryTreeAnswer.Turn2Trigger.Get());

            // send some nonsense. retry should trigger but then fail
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "thanks", 0.6f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Failure, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.StartFailTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.RetryFailTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.RetryTreeAnswer.Turn2Trigger.Get());
            Assert.IsFalse(response.NextTurnBehavior.Continues);

            // Trying to continue the conversation shouldn't work at this point
            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("retrytree", "turn2", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.StartFailTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.RetryTreeAnswer.RetryFailTrigger.Get());
            Assert.AreEqual(0, _mockAnswerProvider.RetryTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that the common/noreco intent will trigger a non-retry response on 2nd-turn for answers that are configured to consume it.
        /// </summary>
        [TestMethod]
        public void TestExplicitConsumeNoRecoOnSecondTurn()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "noreco", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.NoRecoTrigger.Get());
        }

        /// <summary>
        /// Test that tenative multiturn allows us to do multiturn if we decide to keep going in the conversation
        /// </summary>
        [TestMethod]
        public void TestNorecoDoesNotBreakTenativeMultiturn()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_tenative", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTenativeTrigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "noreco", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, _mockAnswerProvider.BasicTreeAnswer.NoRecoTrigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTenativeTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that, if we are locked-in, and pass an invalid query to a node that has no retry
        /// handler, that the entire turn will be skipped, and that we can still continue the conversation.
        /// </summary>
        [TestMethod]
        public void TestCanRetryWithoutRetryHandler()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_lock", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartLockTrigger.Get());

            for (int c = 0; c < 3; c++)
            {
                // Common intent should be ignored by all plugins.
                // DE will then try a 2nd pass with noreco intent to see if a retry continuation is present.
                // Since there's not, it will return "skip" without deleting the conversation state.
                // In these cases, the client should maintain the last turn's multiturnbehavior, and prompt
                // the user for further clarification (as though a retry occurred)
                response = _dialogEngine.Process(
                    TestCommon.GetSimpleRecoResultList("common", "thanks", 0.6f),
                    TestCommon.GetTestClientContext(),
                    ClientAuthenticationLevel.Authorized,
                    InputMethod.Typed,
                    Guid.NewGuid().ToString("N"),
                null);

                Assert.AreEqual(Result.Skip, response.ResponseCode);
                Assert.AreEqual(0, _mockAnswerProvider.BasicTreeAnswer.NoRecoTrigger.Get());
                Assert.AreEqual(0, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());

                // This is important too - make sure multiturn state sent to the client is preserved, so the client is not left
                // in an inconsistent conversation
                Assert.IsTrue(response.NextTurnBehavior.Continues);
            }

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 0.9f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartLockTrigger.Get());
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());
        }

#endregion

#region Conversation tree tests

        // test valid and invalid teleportation

#endregion

#region Session store tests

        /// <summary>
        /// Test that an object written to the object store is carried forward through turns
        /// </summary>
        [TestMethod]
        public void TestObjectStoreCarriesForward()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_objectstore", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        /// <summary>
        /// Test that an object written to the object store vanishes after leaving multiturn
        /// </summary>
        [TestMethod]
        public void TestObjectStoreDoesntPersist()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_objectstore", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_objectstore", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        /// <summary>
        /// Test that the PastTurns object stores valid turns in the proper order and that the past turns
        /// are pruned at an appropriate amount
        /// </summary>
        [TestMethod]
        public void TestPreviousTurnsAreStoredAndPruned()
        {
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f, "0"),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);

            for (int loop = 1; loop < 50; loop++)
            {
                response = _dialogEngine.Process(
                    TestCommon.GetSimpleRecoResultList("basictree", "loop", 1.0f, loop.ToString()),
                    TestCommon.GetTestClientContext(),
                    ClientAuthenticationLevel.Authorized,
                    InputMethod.Typed,
                    Guid.NewGuid().ToString("N"),
                    null);

                Assert.AreEqual(Result.Success, response.ResponseCode);
            }
        }

#endregion

#region Conversation state tests

        /// <summary>
        /// Tests to make sure conversation state does not persist after returning from a terminating convo node.
        /// </summary>
        [TestMethod]
        public void TestConversationStateIsClearedAfterFinish()
        {
            Stack<ConversationState> fakeState;
            
            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            Assert.IsTrue(_conversationStateCache.TryRetrieveState(TestCommon.GetTestClientContext().UserId, TestCommon.GetTestClientContext().ClientId, out fakeState, _logger));

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 0.99f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());

            Assert.IsFalse(_conversationStateCache.TryRetrieveState(TestCommon.GetTestClientContext().UserId, TestCommon.GetTestClientContext().ClientId, out fakeState, _logger));
        }
        
        /// <summary>
        /// Simulates a user (represented by user ID) transitioning a conversation across devices (represented by separate client IDs)
        /// </summary>
        [TestMethod]
        public void TestMultiturnOneUserTwoDevices()
        {
            ClientContext turn1Context = TestCommon.GetTestClientContext();
            turn1Context.ClientId = "Device1ClientId";

            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                turn1Context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            ClientContext turn2Context = TestCommon.GetTestClientContext();
            turn2Context.ClientId = "Device2ClientId";

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 0.99f),
                turn2Context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Simulates two users (represented by different user IDs) having interleaved conversations on the same client
        /// (presumably set up as some kind of web service or public kiosk)
        /// </summary>
        [TestMethod]
        public void TestMultiturnTwoUsersOneDevice()
        {
            ClientContext user1Context = TestCommon.GetTestClientContext();
            user1Context.UserId = "User1UserId";

            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                user1Context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            ClientContext user2Context = TestCommon.GetTestClientContext();
            user2Context.UserId = "User2UserId";

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                user2Context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(2, _mockAnswerProvider.BasicTreeAnswer.StartTrigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 0.99f),
                user1Context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 0.99f),
                user2Context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(2, _mockAnswerProvider.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Simulates a user starting 2 conversations on 2 devices and having them remain separate
        /// </summary>
        [TestMethod]
        public void TestClientSpecificStatesAreSet()
        {
            // User starts a conversation on one device

            ClientContext context = TestCommon.GetTestClientContext();
            context.ClientId = "Device1ClientId";
            context.UserId = "User1UserId";

            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f),
                context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("start_tenative", response.TriggeredIntent);

            // And then another conversation on another device

            context = TestCommon.GetTestClientContext();
            context.ClientId = "Device2ClientId";
            context.UserId = "User1UserId";

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f),
                context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Spoken,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("start_tenative", response.TriggeredIntent);

            // Each device then continues the conversation programmatically

            context = TestCommon.GetTestClientContext();
            context.ClientId = "Device1ClientId";
            context.UserId = "User1UserId";

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 1.0f),
                context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Programmatic,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("turn2", response.TriggeredIntent);

            context = TestCommon.GetTestClientContext();
            context.ClientId = "Device2ClientId";
            context.UserId = "User1UserId";

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "loop2", 1.0f),
                context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Programmatic,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("loop2", response.TriggeredIntent);
        }

        /// <summary>
        /// Simulates a user have a conversation across devices while the first device also executes programmatic actions
        /// </summary>
        [TestMethod]
        public void TestClientSpecificStatesAreUsedAfterClientAction()
        {
            // User starts a conversation on one device

            ClientContext context = TestCommon.GetTestClientContext();
            context.ClientId = "Device1ClientId";
            context.UserId = "User1UserId";

            DialogEngineResponse response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f),
                context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("start_tenative", response.TriggeredIntent);
            
            // The client does some kind of action

            context = TestCommon.GetTestClientContext();
            context.ClientId = "Device1ClientId";
            context.UserId = "User1UserId";

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "loop2", 1.0f),
                context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Programmatic,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("loop2", response.TriggeredIntent);

            // The user then starts talking to another device

            context = TestCommon.GetTestClientContext();
            context.ClientId = "Device2ClientId";
            context.UserId = "User1UserId";

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f),
                context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Spoken,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("start_tenative", response.TriggeredIntent);

            // The first device can still execute programmatically

            context = TestCommon.GetTestClientContext();
            context.ClientId = "Device1ClientId";
            context.UserId = "User1UserId";

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "loop2", 1.0f),
                context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Programmatic,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("loop2", response.TriggeredIntent);

            // While the user is still talking to device 2

            context = TestCommon.GetTestClientContext();
            context.ClientId = "Device2ClientId";
            context.UserId = "User1UserId";

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 1.0f),
                context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Spoken,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("turn2", response.TriggeredIntent);
        }

        #endregion

        #region Sandbox environment tests

        [TestMethod]
        public void TestSandboxTimeoutProdConfig()
        {
            _mockAnswerProvider = new TestAnswerProvider();
            _logger = new ConsoleLogger("Main", LogLevel.Err | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn, false);

            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.SandboxPlugins = true;
            testConfig.FailFastPlugins = false;
            testConfig.MaxPluginExecutionTime = 1000;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
            dialogEngine.LoadAnswer("sandbox");

            DialogEngineResponse response = dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("sandbox", "timeout", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Failure, response.ResponseCode);
            Assert.IsNotNull(response.ErrorMessage);
            Assert.IsTrue(response.ErrorMessage.Contains("violated its SLA"));
        }

        [TestMethod]
        public void TestSandboxExceptionProdConfig()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.SandboxPlugins = true;
            testConfig.FailFastPlugins = false;
            testConfig.MaxPluginExecutionTime = 1000;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
            dialogEngine.LoadAnswer("sandbox");

            DialogEngineResponse response = dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("sandbox", "exception", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Failure, response.ResponseCode);
            Assert.IsNotNull(response.ErrorMessage);
            Assert.IsTrue(response.ErrorMessage.Contains("unhandled exception"));
        }

        [TestMethod]
        public void TestSandboxFailProdConfig()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.SandboxPlugins = true;
            testConfig.FailFastPlugins = false;
            testConfig.MaxPluginExecutionTime = 1000;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
            dialogEngine.LoadAnswer("sandbox");

            DialogEngineResponse response = dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("sandbox", "fail", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Failure, response.ResponseCode);
        }

        [TestMethod]
        public void TestSandboxTimeoutDevConfig()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.SandboxPlugins = false;
            testConfig.FailFastPlugins = true;
            testConfig.MaxPluginExecutionTime = 1000;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
            dialogEngine.LoadAnswer("sandbox");

            DialogEngineResponse response = dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("sandbox", "timeout_small", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        [TestMethod]
        public void TestSandboxExceptionDevConfig()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.SandboxPlugins = false;
            testConfig.FailFastPlugins = false;
            testConfig.MaxPluginExecutionTime = 1000;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
            dialogEngine.LoadAnswer("sandbox");

            DialogEngineResponse response = dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("sandbox", "exception", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Failure, response.ResponseCode);
            Assert.IsNotNull(response.ErrorMessage);
            Assert.IsTrue(response.ErrorMessage.Contains("unhandled exception"));
        }

        [TestMethod]
        public void TestSandboxDebuggabilityDevConfig()
        {
            try
            {
                DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
                testConfig.SandboxPlugins = false;
                testConfig.FailFastPlugins = true;
                testConfig.MaxPluginExecutionTime = 1000;
                DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
                modifiedParams.Configuration = testConfig;
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
                dialogEngine.LoadAnswer("sandbox");

                DialogEngineResponse response = dialogEngine.Process(
                    TestCommon.GetSimpleRecoResultList("sandbox", "exception", 1.0f),
                    TestCommon.GetTestClientContext(),
                    ClientAuthenticationLevel.Authorized,
                    InputMethod.Typed,
                    Guid.NewGuid().ToString("N"),
                    null);

                Assert.Fail();
            }
            catch (NullReferenceException)
            {
            }
            catch (Exception)
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public void TestSandboxFailDevConfig()
        {
            DialogConfiguration testConfig = TestCommon.GetTestDialogConfiguration(_logger);
            testConfig.SandboxPlugins = false;
            testConfig.FailFastPlugins = true;
            testConfig.MaxPluginExecutionTime = 1000;
            DialogEngineParameters modifiedParams = _defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
            dialogEngine.LoadAnswer("sandbox");

            DialogEngineResponse response = dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("sandbox", "fail", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Failure, response.ResponseCode);
        }

#endregion

#region Distributed environment tests
        
        /// <summary>
        /// Simulates a multiturn conversation that spans multiple DialogEngine instances which share a common session store
        /// </summary>
        [TestMethod]
        public void TestMultiturnCloudy()
        {
            TestAnswerProvider provider1 = new TestAnswerProvider();
            DialogEngineParameters params1 = _defaultDialogParameters.Clone();
            params1.AnswerProvider = provider1;
            DialogProcessingEngine dialogEngine1 = new DialogProcessingEngine(params1);
            dialogEngine1.LoadAnswer("basictree");

            TestAnswerProvider provider2 = new TestAnswerProvider();
            DialogEngineParameters params2 = _defaultDialogParameters.Clone();
            params2.AnswerProvider = provider2;
            DialogProcessingEngine dialogEngine2 = new DialogProcessingEngine(params2);
            dialogEngine2.LoadAnswer("basictree");

            DialogEngineResponse response = dialogEngine1.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, provider1.BasicTreeAnswer.StartTrigger.Get());
            Assert.AreEqual(0, provider2.BasicTreeAnswer.StartTrigger.Get());

            response = dialogEngine2.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, provider1.BasicTreeAnswer.Turn2Trigger.Get());
            Assert.AreEqual(1, provider2.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Simulates a multiturn conversation that spans multiple DialogEngine instances which share a common session store,
        /// while the user also transitions between two different clients with the same userId
        /// </summary>
        [TestMethod]
        public void TestMultiturnTwoDevicesCloudy()
        {
            TestAnswerProvider provider1 = new TestAnswerProvider();
            DialogEngineParameters params1 = _defaultDialogParameters.Clone();
            params1.AnswerProvider = provider1;
            DialogProcessingEngine dialogEngine1 = new DialogProcessingEngine(params1);
            dialogEngine1.LoadAnswer("basictree");

            TestAnswerProvider provider2 = new TestAnswerProvider();
            DialogEngineParameters params2 = _defaultDialogParameters.Clone();
            params2.AnswerProvider = provider2;
            DialogProcessingEngine dialogEngine2 = new DialogProcessingEngine(params2);
            dialogEngine2.LoadAnswer("basictree");

            ClientContext client1Context = TestCommon.GetTestClientContext();
            client1Context.ClientId = "Client1Id";
            DialogEngineResponse response = dialogEngine1.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f),
                client1Context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, provider1.BasicTreeAnswer.StartTrigger.Get());
            Assert.AreEqual(0, provider2.BasicTreeAnswer.StartTrigger.Get());

            ClientContext client2Context = TestCommon.GetTestClientContext();
            client2Context.ClientId = "Client2Id";
            response = dialogEngine2.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "turn2", 1.0f),
                client2Context,
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, provider1.BasicTreeAnswer.Turn2Trigger.Get());
            Assert.AreEqual(1, provider2.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Simulates a multiturn conversation that spans multiple DialogEngine instances which share a common dialog action cache,
        /// ensuring that the cached action can be executed on different machines
        /// </summary>
        [TestMethod]
        public void TestMultiturnDialogActionsCloudy()
        {
            TestAnswerProvider provider1 = new TestAnswerProvider();
            DialogEngineParameters params1 = _defaultDialogParameters.Clone();
            params1.AnswerProvider = provider1;
            DialogProcessingEngine dialogEngine1 = new DialogProcessingEngine(params1);
            dialogEngine1.LoadAnswer("basictree");

            TestAnswerProvider provider2 = new TestAnswerProvider();
            DialogEngineParameters params2 = _defaultDialogParameters.Clone();
            params2.AnswerProvider = provider2;
            DialogProcessingEngine dialogEngine2 = new DialogProcessingEngine(params2);
            dialogEngine2.LoadAnswer("basictree");

            DialogEngineResponse response = dialogEngine1.Process(
                TestCommon.GetSimpleRecoResultList("basictree", "start_dialogaction", 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.ResponseData.ContainsKey("actionKey"));
            string actionKey = response.ResponseData["actionKey"];
            RetrieveResult<DialogAction> cacheResult = _mockDialogActionCache.TryRetrieve(actionKey);
            Assert.IsTrue(cacheResult.Success);
            DialogAction desiredAction = cacheResult.Result;

            response = dialogEngine2.Process(
                TestCommon.GetSimpleRecoResultList(desiredAction.Domain, desiredAction.Intent, 1.0f),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Programmatic,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, provider1.BasicTreeAnswer.Turn2Trigger.Get());
            Assert.AreEqual(1, provider2.BasicTreeAnswer.Turn2Trigger.Get());
        }

        #endregion

        #region Trigger tests

        /// <summary>
        /// Test that a trigger which is boosted will take priority over others, and also
        /// implicitly test that triggering works and that session store is available
        /// </summary>
        [TestMethod]
        public void TestBasicTrigger()
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_a", "fail", 0.99f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_b", "boost", 0.75f));

            DialogEngineResponse response = _dialogEngine.Process(
                recoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_b", response.TriggeredDomain);
        }

        /// <summary>
        /// Test that a trigger which is boosted will take priority over others, and also
        /// implicitly test that triggering works and that session store is available
        /// </summary>
        [TestMethod]
        public void TestMultipleTriggersCauseDisambiguation()
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_a", "boost", 0.99f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_b", "boost", 0.75f));

            DialogEngineResponse response = _dialogEngine.Process(
                recoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("reflection", response.TriggeredDomain);
            Assert.AreEqual("disambiguate", response.TriggeredIntent);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
        }

        /// <summary>
        /// Tests disambiguation triggers and we can successfully drop back from the reflection domain
        /// into the intended target domain
        /// </summary>
        [TestMethod]
        public void TestDisambiguationFull()
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_a", "boost", 0.99f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_b", "boost", 0.75f));

            DialogEngineResponse response = _dialogEngine.Process(
                recoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("reflection", response.TriggeredDomain);
            Assert.AreEqual("disambiguate", response.TriggeredIntent);
            Assert.IsTrue(response.NextTurnBehavior.Continues);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "side_speech", 0.84f, "trigger_b/boost"),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_b", response.TriggeredDomain);
            Assert.AreEqual("boost", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that session values which are stored at original trigger time are preserved after
        /// disambiguation returns
        /// </summary>
        [TestMethod]
        public void TestDisambiguationWithTriggerTimeSessionPreserved()
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_a", "boost", 0.99f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_b", "boost_with_params", 0.75f));

            DialogEngineResponse response = _dialogEngine.Process(
                recoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("reflection", response.TriggeredDomain);
            Assert.AreEqual("disambiguate", response.TriggeredIntent);
            Assert.IsTrue(response.NextTurnBehavior.Continues);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "side_speech", 0.84f, "trigger_b/boost_with_params"),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_b", response.TriggeredDomain);
            Assert.AreEqual("boost_with_params", response.TriggeredIntent);
        }

        [TestMethod]
        public void TestDisambiguationFallsBackWhenReflectionDomainCannotHandle()
        {
            DialogProcessingEngine dialogEngine = new DialogProcessingEngine(_defaultDialogParameters);
            dialogEngine.LoadAnswer("trigger_a");
            dialogEngine.LoadAnswer("trigger_b");

            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_a", "boost", 0.99f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_b", "boost", 0.75f));

            DialogEngineResponse response = dialogEngine.Process(
                recoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_a", response.TriggeredDomain);
            Assert.AreEqual("boost", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that we properly handle multiple boosted intents with the same domain name
        /// </summary>
        [TestMethod]
        public void TestDisambiguationWithinSameDomain()
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_a", "fail", 0.99f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_a", "boost", 0.85f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_b", "boost", 0.75f));

            DialogEngineResponse response = _dialogEngine.Process(
                recoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("reflection", response.TriggeredDomain);
            Assert.AreEqual("disambiguate", response.TriggeredIntent);
            Assert.IsTrue(response.NextTurnBehavior.Continues);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "side_speech", 0.84f, "trigger_a/boost"),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_a", response.TriggeredDomain);
            Assert.AreEqual("boost", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that we properly handle multiple boosted intents with the same domain name,
        /// and where each trigger result requires a unique session store keyed to INTENT as well as domain
        /// </summary>
        [TestMethod]
        public void TestDisambiguationWithinSameDomainWithSideEffects()
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_a", "fail", 0.99f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_a", "boost_with_params", 0.85f));
            recoResults.Add(TestCommon.GetSimpleRecoResult("trigger_a", "boost_with_params_2", 0.75f));

            DialogEngineResponse response = _dialogEngine.Process(
                recoResults,
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("reflection", response.TriggeredDomain);
            Assert.AreEqual("disambiguate", response.TriggeredIntent);
            Assert.IsTrue(response.NextTurnBehavior.Continues);

            response = _dialogEngine.Process(
                TestCommon.GetSimpleRecoResultList("common", "side_speech", 0.84f, "trigger_a/boost_with_params_2"),
                TestCommon.GetTestClientContext(),
                ClientAuthenticationLevel.Authorized,
                InputMethod.Typed,
                Guid.NewGuid().ToString("N"),
                null);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_a", response.TriggeredDomain);
            Assert.AreEqual("boost_with_params_2", response.TriggeredIntent);
        }

        #endregion
    }
}
