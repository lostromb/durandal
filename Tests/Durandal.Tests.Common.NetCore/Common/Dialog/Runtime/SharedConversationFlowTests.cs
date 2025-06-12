using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Dialog.Runtime
{
    using Durandal;
    using Durandal.API;
    using Durandal.Common;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.Remoting;
    using Durandal.Common.Remoting.Protocol;
    using Durandal.Common.Security;
    using Durandal.Common.Security.OAuth;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Test;
    using Durandal.Common.Utils;
    using Durandal.Common.Cache;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
using Durandal.Tests.Common.Dialog.Runtime;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// Shared implementations of test logic used by <see cref="ConversationFlowTests"/>, <see cref="RemotedBondConversationFlowTests\"/>, and <see cref="RemotedJsonConversationFlowTests\"/>.
    /// </summary>
    public static class SharedConversationFlowTests
    {
        #region Sanity checks

        /// <summary>
        /// Test that the result will skip when no reco results are passed
        /// </summary>
        public static async Task TestConversationFlowNoRecoResults(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(new List<RecoResult>()),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        #endregion

        #region Basic singleturn tests

        /// <summary>
        /// Tests that a single plugin can return a success
        /// </summary>
        public static async Task TestConversationFlowBasicSuccess(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "succeed", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that a single plugin can return a failure
        /// </summary>
        public static async Task TestConversationFlowBasicFailure(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "fail", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Failure, response.ResponseCode);
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(1, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that a single plugin can return a skip
        /// </summary>
        public static async Task TestConversationFlowBasicSkip(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "skip", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(1, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that a single plugin can return a success after higher-rated plugins skip
        /// </summary>
        public static async Task TestConversationFlowBasicFallthrough(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            List<RecoResult> results = new List<RecoResult>();
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.99f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.90f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.85f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "succeed", 0.80f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.75f));
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(results),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(3, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that a single plugin can return a failure after higher-rated plugins skip
        /// </summary>
        public static async Task TestConversationFlowBasicFallthrough2(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            List<RecoResult> results = new List<RecoResult>();
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.99f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.90f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.85f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "fail", 0.80f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.75f));
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(results),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Failure, response.ResponseCode);
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(3, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(1, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that queries that would normally succeed will instead skip if the confidence is too low
        /// </summary>
        public static async Task TestConversationFlowLowConfidenceSkip(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "succeed", 0.1f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that queries that would normally skip will still skip if the confidence is too low
        /// </summary>
        public static async Task TestConversationFlowLowConfidenceSkip2(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "skip", 0.1f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Test that queries that would normally fail will instead skip if the confidence is too low
        /// </summary>
        public static async Task TestConversationFlowLowConfidenceSkip3(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "fail", 0.1f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that if the first plugin returns a skip message, that it will be included in the final
        /// dialog result
        /// </summary>
        public static async Task TestConversationFlowSkipReturnsMessageFromTop(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            List<RecoResult> results = new List<RecoResult>();
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skipwithmsg", 0.99f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.90f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.85f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.75f));
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(results),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("This plugin has been skipped", response.DisplayedText);
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(4, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that if the 2nd+ plugin skips with a message, it will still be returned.
        /// </summary>
        public static async Task TestConversationFlowSkipReturnsMessageFromMiddle(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            List<RecoResult> results = new List<RecoResult>();
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.99f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skipwithmsg", 0.90f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.85f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.75f));
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(results),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("This plugin has been skipped", response.DisplayedText);
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(4, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that if the 2nd+ plugin skips with a message, it will still be returned and properly ordered
        /// </summary>
        public static async Task TestConversationFlowSkipReturnsMessageOrderedProperly(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            List<RecoResult> results = new List<RecoResult>();
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.99f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skipwithmsg", 0.90f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skipwithmsgbad", 0.85f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.75f));
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(results),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("This plugin has been skipped", response.DisplayedText);
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(4, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.FailTrigger.Get());
        }

        /// <summary>
        /// Tests that if we rely on the "return best skip result" logic, that response's conversation state
        /// affects the stack as expected
        /// </summary>
        /// <returns></returns>
        public static async Task TestConversationFlowSkipResponsesCanMaintainState(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            List<RecoResult> results = new List<RecoResult>();
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.99f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skipwithmsgmultiturn", 0.90f));
            results.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.75f));
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(results),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("0", response.DisplayedText);
            Assert.AreEqual(0, pluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(3, pluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicAnswer.FailTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(results),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("1", response.DisplayedText);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(results),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("2", response.DisplayedText);
        }

        /// <summary>
        /// Tests that the SpeechHypothesis that was selected by LU is what is provided
        /// to the plugin
        /// </summary>
        public static async Task TestConversationFlowOriginalUtteranceIsPassedThrough(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            SpeechRecognitionResult speechResult = new SpeechRecognitionResult();
            speechResult.RecognitionStatus = SpeechRecognitionStatus.Success;
            speechResult.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
            {
                SREngineConfidence = 0.7f,
                DisplayText = "what",
                LexicalForm = "what"
            });
            speechResult.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
            {
                SREngineConfidence = 0.4f,
                DisplayText = "incorrect",
                LexicalForm = "incorrect"
            });
            speechResult.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
            {
                SREngineConfidence = 0.35f,
                DisplayText = "the original query is here",
                LexicalForm = "the original query is here"
            });
            speechResult.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
            {
                SREngineConfidence = 0.3f,
                DisplayText = "wrong",
                LexicalForm = "wrong"
            });
            speechResult.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
            {
                SREngineConfidence = 0.2f,
                DisplayText = "kablammo",
                LexicalForm = "kablammo"
            });

            DialogEngineResponse response = await dialogEngine.Process(
                results: RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "originalquery", 1.0f, "the original query is here")),
                clientContext: DialogTestHelpers.GetTestClientContextTextQuery(),
                authLevel: ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                inputSource: InputMethod.Spoken,
                speechInput: speechResult);

            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        /// <summary>
        /// Tests that if a plugin returns invalid SSML markup, it can be caught and emitted as a warning so the developer
        /// will hopefully figure out why TTS doesn't work
        /// </summary>
        public static async Task TestConversationFlowInvalidSSMLIsCaught(
            DialogEngineParameters defaultDialogParameters,
            WeakPointer<IDurandalPluginProvider> pluginProvider,
            IRealTimeProvider realTime)
        {
            EventOnlyLogger warningLogger = new EventOnlyLogger();
            DialogEngineParameters dialogParams = defaultDialogParameters.Clone();
            dialogParams.Logger = warningLogger;
            dialogParams.PluginProvider = pluginProvider;
            DialogProcessingEngine dialogEngine = new DialogProcessingEngine(dialogParams);
            await dialogEngine.SetLoadedPlugins(new PluginStrongName[] { new PluginStrongName("basic", 0, 0) }, realTime);

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "bad_ssml", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            FilterCriteria warningFilter = new FilterCriteria()
            {
                Level = LogLevel.Wrn,
                SearchTerm = "unescaped characters"
            };
            List<LogEvent> warningEvents = warningLogger.History.FilterByCriteria(warningFilter).ToList();

            Assert.IsTrue(warningEvents.Count > 0);
        }

        /// <summary>
        /// Tests that a node in the conversation tree can refer to a function outside of the immediate plugin class
        /// </summary>
        public static async Task TestConversationFlowExternalPluginMethod(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_externalmodule", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("Static answer", response.DisplayedText);
        }

        #endregion

        #region Side_speech intent tests

        /// <summary>
        /// Test that side_speech/side_speech will trigger on first-turn side-speech when the ignoreSideSpeech flag is enabled
        /// </summary>
        public static async Task TestConversationFlowAcceptFirstTurnSideSpeech(
            ILogger logger,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader pluginLoader)
        {
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            await testEngine.LoadPlugin("basic", realTime);
            await testEngine.LoadPlugin("side_speech", realTime);

            DialogEngineResponse response = await testEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, pluginLoader.SideSpeechAnswer.SucceedHighTrigger.Get());
        }

        /// <summary>
        /// Test that a separate "side_speech_highconf" hyp is generated for high confidence first-turn side speech intents, when side speech is enabled on first-turn
        /// </summary>
        public static async Task TestConversationFlowAcceptFirstTurnSideSpeechHighConf(
            ILogger logger,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader pluginLoader)
        {
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            await testEngine.LoadPlugin("basic", realTime);
            await testEngine.LoadPlugin("side_speech", realTime);

            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("common", "side_speech", 0.9f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basictree", "start", 0.85f));

            DialogEngineResponse response = await testEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("side_speech", response.TriggeredDomain);
            Assert.AreEqual("side_speech_highconf", response.TriggeredIntent);
            Assert.AreEqual(1, pluginLoader.SideSpeechAnswer.SucceedHighTrigger.Get());
            Assert.AreEqual(0, pluginLoader.BasicTreeAnswer.StartTrigger.Get());
        }

        /// <summary>
        /// Test that high-confidence common/side speech will be ignored on the first turn when the ignoreSideSpeech flag is set
        /// </summary>
        public static async Task TestConversationFlowIgnoreFirstTurnSideSpeech(
            ILogger logger,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader pluginLoader)
        {
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            testConfig.IgnoreSideSpeech = true;
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            await testEngine.LoadPlugin("basic", realTime);
            await testEngine.LoadPlugin("side_speech", realTime);

            DialogEngineResponse response = await testEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, pluginLoader.SideSpeechAnswer.SucceedTrigger.Get());
        }

        /// <summary>
        /// Test that side_speech/side_speech will still trigger on first-turn regardless of its confidence
        /// </summary>
        public static async Task TestConversationFlowLowConfidenceFirstTurnSideSpeech(
            ILogger logger,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader pluginLoader)
        {
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            await testEngine.LoadPlugin("basic", realTime);
            await testEngine.LoadPlugin("side_speech", realTime);

            DialogEngineResponse response = await testEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 0.1f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, pluginLoader.SideSpeechAnswer.SucceedTrigger.Get());
        }

        /// <summary>
        /// Test that if a single high-confidence answer skips, side speech will trigger when the ignoreSideSpeech flag is disabled
        /// In this case, the dialog logic will create a 0-confidence side-speech hyp that still triggers because ignoreSideSpeech is false
        /// </summary>
        public static async Task TestConversationFlowSideSpeechCanTriggerAfterHighConfAnswerSkips(
            ILogger logger,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader pluginLoader)
        {
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            await testEngine.LoadPlugin("basic", realTime);
            await testEngine.LoadPlugin("side_speech", realTime);

            DialogEngineResponse response = await testEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "skip", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(DialogConstants.SIDE_SPEECH_DOMAIN, response.TriggeredDomain);
            Assert.AreEqual(DialogConstants.SIDE_SPEECH_INTENT, response.TriggeredIntent);
            Assert.AreEqual(1, pluginLoader.SideSpeechAnswer.SucceedTrigger.Get());
        }

        /// <summary>
        /// Test that if a single high-confidence answer skips, side speech will not trigger when the ignoreSideSpeech flag is enabled
        /// In this case, the dialog logic will create a 0-confidence side-speech hyp that is ignored
        /// </summary>
        public static async Task TestConversationFlowSideSpeechIsIgnoredAfterHighConfAnswerSkips(
            ILogger logger,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader pluginLoader)
        {
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            testConfig.IgnoreSideSpeech = true;
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            await testEngine.LoadPlugin("basic", realTime);
            await testEngine.LoadPlugin("side_speech", realTime);

            DialogEngineResponse response = await testEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "skip", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, pluginLoader.SideSpeechAnswer.SucceedTrigger.Get());
        }

        /// <summary>
        /// Test that the answerdomain/side_speech intent will trigger a response on 2nd-turn for answers that are configured to consume it.
        /// </summary>
        public static async Task TestConversationFlowConsumeValidSideSpeechOnSecondTurn(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, pluginLoader.BasicTreeAnswer.StartTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, pluginLoader.BasicTreeAnswer.SideSpeechTrigger.Get());
        }

        /// <summary>
        /// Test that the answerdomain/side_speech intent will trigger a response on 2nd-turn for answers that are configured to consume it,
        /// even if the side speech is very low confidence
        /// </summary>
        public static async Task TestConversationFlowRemoteBondConsumeValidSideSpeechOnSecondTurnLowConf(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, pluginLoader.BasicTreeAnswer.StartTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 0.1f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, pluginLoader.BasicTreeAnswer.SideSpeechTrigger.Get());
        }

        /// <summary>
        /// Tests that, if ignoreSideSpeech is true, a side speech top reco result will not affect
        /// the 2nd turn when in tenative mode
        /// </summary>
        public static async Task TestConversationFlowRemoteBondContinueTenativeConversationAfterHighConfidenceSideSpeech(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader pluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, pluginLoader.BasicTreeAnswer.StartTenativeTrigger.Get());

            List<RecoResult> turnTwoResults = new List<RecoResult>();
            turnTwoResults.Add(DialogTestHelpers.GetSimpleRecoResult("common", "side_speech", 0.9f));
            turnTwoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basictree", "start", 0.8f));

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(turnTwoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, pluginLoader.BasicTreeAnswer.SideSpeechTrigger.Get());
            Assert.AreEqual(1, pluginLoader.BasicTreeAnswer.StartTrigger.Get());
        }

        /// <summary>
        /// Tests that, if ignoreSideSpeech is false, a high-confidence side speech hyp will
        /// begin a new conversation with the side_speech domain
        /// </summary>
        public static async Task TestConversationFlowEndTenativeConversationAfterHighConfidenceSideSpeech(
            ILogger logger,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider)
        {
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            TestPluginLoader pluginLoader = BuildTestPluginLoader();
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(pluginLoader))
            {
                modifiedParams.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
                await testEngine.SetLoadedPlugins(new PluginStrongName[] { new PluginStrongName("basictree", 0, 0), new PluginStrongName("side_speech", 0, 0) }, realTime);

                DialogEngineResponse response = await testEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual(1, pluginLoader.BasicTreeAnswer.StartTenativeTrigger.Get());

                List<RecoResult> turnTwoResults = new List<RecoResult>();
                turnTwoResults.Add(DialogTestHelpers.GetSimpleRecoResult("common", "side_speech", 0.9f));
                turnTwoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basictree", "start", 0.8f));

                response = await testEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(turnTwoResults),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual(0, pluginLoader.BasicTreeAnswer.StartTrigger.Get());
                Assert.AreEqual(0, pluginLoader.BasicTreeAnswer.SideSpeechTrigger.Get());
                Assert.AreEqual(1, pluginLoader.SideSpeechAnswer.SucceedHighTrigger.Get());
            }
        }

        /// <summary>
        /// Tests that if we are having a conversation with the side_speech domain, additional
        /// side speech will continue the conversation
        /// </summary>
        public static async Task TestConversationFlowCanHaveMultiturnWithinSideSpeechDomain(
            ILogger logger,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader pluginLoader)
        {
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
            await testEngine.LoadPlugin("side_speech", realTime);

            DialogEngineResponse response = await testEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, pluginLoader.SideSpeechAnswer.SucceedHighTrigger.Get());

            response = await testEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(2, pluginLoader.SideSpeechAnswer.SucceedHighTrigger.Get());
        }

        /// <summary>
        /// Test that if a 2nd turn node consumes both side speech and a local intent, that side speech's confidence will
        /// be a capped appropriately and will properly defer to the local intent
        /// </summary>
        public static async Task TestConversationFlowSecondTurnSideSpeechIsCappedProperly(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            List<RecoResult> turnTwoResults = new List<RecoResult>();
            turnTwoResults.Add(DialogTestHelpers.GetSimpleRecoResult("common", "side_speech", 0.99f));
            turnTwoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basictree", "turn2", 0.9f));

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(turnTwoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, mockPluginLoader.BasicTreeAnswer.SideSpeechTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that if turn 1 is tenative multiturn inside of another domain, that side_speech_highconf can still trigger on turn 2
        /// </summary>
        public static async Task TestConversationFlowHighConfSideSpeechAfterTenativeMultiturn(
            ILogger logger,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider)
        {
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            TestPluginLoader pluginLoader = BuildTestPluginLoader();
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(pluginLoader))
            {
                modifiedParams.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
                await testEngine.SetLoadedPlugins(new PluginStrongName[] { new PluginStrongName("basictree", 0, 0), new PluginStrongName("side_speech", 0, 0) }, realTime);

                DialogEngineResponse response = await testEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("basictree", response.TriggeredDomain);
                Assert.AreEqual("start_tenative", response.TriggeredIntent);

                response = await testEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("side_speech", response.TriggeredDomain);
                Assert.AreEqual("side_speech_highconf", response.TriggeredIntent);
            }
        }

        /// <summary>
        /// Test that side_speech/side_speech will trigger on first-turn side-speech when the ignoreSideSpeech flag is enabled
        /// </summary>
        public static async Task TestConversationFlowRegularDomainCanServeAsSideSpeechDomain(
            ILogger logger,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider)
        {
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            testConfig.IgnoreSideSpeech = false;
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            modifiedParams.SideSpeechDomainName = "basictree";
            TestPluginLoader pluginLoader = BuildTestPluginLoader();
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(pluginLoader))
            {
                modifiedParams.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine testEngine = new DialogProcessingEngine(modifiedParams);
                await testEngine.LoadPlugin(new PluginStrongName("basictree", 0, 0), realTime);

                DialogEngineResponse response = await testEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual(1, pluginLoader.BasicTreeAnswer.StartTrigger.Get());

                response = await testEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual(1, pluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
            }
        }

        #endregion

        #region Multiturn tests
        // test basic transitions, tenative transitions, and common domain rewrite

        /// <summary>
        /// Test that we can take 2 valid turns through the engine
        /// </summary>
        public static async Task TestConversationFlowBasicMultiturn(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that basic multiturn (non-passive) will lock the dialog engine into a single domain
        /// regardless of other domain results
        /// </summary>
        public static async Task TestConversationFlowLockedIntoSingleMultiturnDomain(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            List<RecoResult> recoResults = new List<RecoResult>();

            // Send a bunch of bogus results with high probability
            // and then a really low probability for turn 2
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "succeed", 0.99f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 0.98f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "fail", 0.97f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basictree", "turn2", 0.01f));

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, mockPluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.BasicAnswer.FailTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that, after entering into a multiturn conversation, the dialog engine
        /// will honor the lock by ignoring any new conversation-starting intents when
        /// not in tenative multiturn mode
        /// </summary>
        public static async Task TestConversationFlowCannotRestartConversationAfterLocking(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            List<RecoResult> recoResults = new List<RecoResult>();

            // Send a bunch of bogus results with high probability
            // and then a really low probability for turn 2
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basictree", "start", 1.0f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basictree", "turn2", 0.01f));

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that tenative multiturn does not lock us into the domain and we are free to go elsewhere
        /// </summary>
        public static async Task TestConversationFlowCanExitDomainAfterTenativeMultiturn(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTenativeTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "succeed", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.BasicAnswer.FailTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that tenative multiturn allows us to do multiturn if we decide to keep going in the conversation
        /// </summary>
        public static async Task TestConversationFlowCanContinueDomainAfterTenativeMultiturn(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_tenative", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTenativeTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(0, mockPluginLoader.BasicAnswer.SucceedTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.BasicAnswer.SkipTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.BasicAnswer.FailTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that we can use common domain results in 2nd turn
        /// </summary>
        public static async Task TestConversationFlowBasicMultiturnUsingCommonDomain(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "confirm", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.ConfirmTrigger.Get());
        }

        /// <summary>
        /// Test that we can use common domain results in 2nd turn with low confidence
        /// </summary>
        public static async Task TestConversationFlowBasicMultiturnUsingCommonDomainLowConf(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "confirm", 0.1f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.ConfirmTrigger.Get());
        }

        /// <summary>
        /// Test that common domain is ignored unless the intents are registered in the tree
        /// </summary>
        public static async Task TestConversationFlowIgnoreInvalidCommonResultsOnMultiturn(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("common", "thanks", 1.0f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("common", "confirm", 0.9f));

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.ConfirmTrigger.Get());
        }

        /// <summary>
        /// Test that common domain results are ignored on the first turn
        /// </summary>
        public static async Task TestConversationFlowIgnoreCommonDomainOnFirstTurn(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "confirm", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
        }

        /// <summary>
        /// Ensures that dialog actions can be cached and executed on a later turn (this just ensures that they are written, it
        /// does not test the ProcessDialogAction method of the web service, nor the handling of ActionURIs)
        /// </summary>
        public static async Task TestConversationFlowMultiturnDialogActions(
            DialogProcessingEngine dialogEngine,
            TestPluginLoader mockPluginLoader,
            InMemoryCache<DialogAction> mockDialogActionCache,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_dialogaction", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.ResponseData.ContainsKey("actionKey"));
            string actionKey = response.ResponseData["actionKey"];
            RetrieveResult<DialogAction> cacheResult = await mockDialogActionCache.TryRetrieve(actionKey, logger, realTime);
            Assert.IsTrue(cacheResult.Success);
            DialogAction desiredAction = cacheResult.Result;

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList(desiredAction.Domain, desiredAction.Intent, 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Programmatic);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that unknown intents will make us fall off of the tree if no promiscuous edge is enabled
        /// </summary>
        public static async Task TestConversationFlowPromiscuousEdgeNegative(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_lock", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start_lock", response.SelectedRecoResult.Intent);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "weird", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
        }

        /// <summary>
        /// Test that unknown intents will make us fall off of the tree if no promiscuous edge is enabled
        /// </summary>
        public static async Task TestConversationFlowPromiscuousEdgePositive(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start", response.SelectedRecoResult.Intent);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2_promisc", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("turn2_promisc", response.SelectedRecoResult.Intent);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "weird", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("weird", response.SelectedRecoResult.Intent);
        }

        /// <summary>
        /// Test that we can take 2 valid turns through the engine when the first turn ditches
        /// the conversation tree and returns an explicit continuation
        /// </summary>
        public static async Task TestConversationFlowExplicitContinuations(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_continuations", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start_continuations", response.TriggeredIntent);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "continuations_turn_2", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("continuations_turn_2", response.TriggeredIntent);
        }

        /// <summary>
        /// Test that we can take 2 valid turns through the engine when the first turn ditches
        /// the conversation tree and returns an explicit continuation, and the turn 2 input is
        /// in the common domain
        /// </summary>
        public static async Task TestConversationFlowExplicitContinuationsWithCommonIntent(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_continuations", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start_continuations", response.TriggeredIntent);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "confirm", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("confirm", response.TriggeredIntent);
        }

        /// <summary>
        /// Test that continuations are allowed to be static
        /// </summary>
        public static async Task TestConversationFlowExplicitContinuationsStatic(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_continuations", 1.0f, "static")),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("start_continuations", response.TriggeredIntent);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "continuations_turn_2", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("continuations_turn_2", response.TriggeredIntent);
        }

        /// <summary>
        /// Test that continuations must not be anonymous
        /// </summary>
        public static async Task TestConversationFlowExplicitContinuationsCannotBeAnonymous(DialogProcessingEngine dialogEngine)
        {
            try
            {
                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_continuations", 1.0f, "lambda")),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Failure, response.ResponseCode);
            }
            catch (DialogException) { }
        }

        /// <summary>
        /// Test that continuations must not be private
        /// </summary>
        public static async Task TestConversationFlowExplicitContinuationsCannotBePrivate(DialogProcessingEngine dialogEngine)
        {

            try
            {
                DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_continuations", 1.0f, "private")),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

                Assert.AreEqual(Result.Failure, response.ResponseCode);
            }
            catch (DialogException) { }
        }

        #endregion

        #region Cross-domain tests

        /// <summary>
        /// Tests that a cross-domain call can succeed with no parameters
        /// </summary>
        public static async Task TestConversationFlowCrossDomainBasic(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "basic_a", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_b", response.TriggeredDomain);
            Assert.AreEqual("basic_b", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that a cross-domain call can succeed with no parameters
        /// </summary>
        public static async Task TestConversationFlowCrossDomainWithParameters(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "params_a", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_b", response.TriggeredDomain);
            Assert.AreEqual("params_b", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that a cross-domain call can succeed while passing parameters that come from the _current_ turn's slots
        /// </summary>
        public static async Task TestConversationFlowCrossDomainWithSlotParameters(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            RecoResult hyp = DialogTestHelpers.GetSimpleRecoResult("crossdomain_a", "slotparams_a", 1.0f);
            hyp.MostLikelyTags.Slots.Add(new SlotValue("param1", "value1", SlotValueFormat.TypedText));
            List<RecoResult> hyps = new List<RecoResult>();
            hyps.Add(hyp);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(hyps),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_b", response.TriggeredDomain);
            Assert.AreEqual("slotparams_b", response.TriggeredIntent);
        }
        
        public static async Task TestConversationFlowCrossDomainWithEntityParameters(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "entities_a", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_b", response.TriggeredDomain);
            Assert.AreEqual("entities_b", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that a cross-domain call fails if the request is not honored
        /// </summary>
        public static async Task TestConversationFlowCrossDomainUnsupportedRequest(DialogProcessingEngine dialogEngine)
        {
            try
            {
                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
                Assert.AreEqual("start", response.TriggeredIntent);

                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "unsupported_request_a", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);
                Assert.Fail("Should have thrown a DialogException");
            }
            catch (DialogException) { }
        }

        /// <summary>
        /// Tests that a cross-domain call fails if the response is not honored
        /// </summary>
        public static async Task TestConversationFlowCrossDomainUnsupportedResponse(DialogProcessingEngine dialogEngine)
        {
            try
            {
                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
                Assert.AreEqual("start", response.TriggeredIntent);

                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "unsupported_response_a", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);
                Assert.Fail("Should have thrown a DialogException");
            }
            catch (DialogException) { }
        }

        /// <summary>
        /// Tests that a cross-domain call fails if the target intent does not exist
        /// </summary>
        public static async Task TestConversationFlowCrossDomainNoTargetIntent(DialogProcessingEngine dialogEngine)
        {
            try
            {
                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
                Assert.AreEqual("start", response.TriggeredIntent);

                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "no_target_intent", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);
                Assert.Fail("Should have thrown a DialogException");
            }
            catch (DialogException) { }
        }

        /// <summary>
        /// Tests that a cross-domain call can succeed while passing parameters from the calling domain's session store
        /// </summary>
        public static async Task TestConversationFlowCrossDomainWithSessionStore(DialogProcessingEngine dialogEngine)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_a", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("crossdomain_a", "sessionstore_a", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("crossdomain_b", response.TriggeredDomain);
            Assert.AreEqual("sessionstore_b", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that the state of a super domain can continue with a callback after a subdomain finishes
        /// </summary>
        public static async Task TestConversationFlowCrossDomainCanReturnToSuperAfterSubStops(DialogProcessingEngine dialogEngine)
        {
            // Start super answer
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "continue_callback_a", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("continue_b", response.TriggeredIntent);

            // Talk to sub answer a bit
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_sub", "cancel_b", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("cancel_b", response.TriggeredIntent);

            // Now return to super answer after sub closes
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "carry_on", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsFalse(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("carry_on", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that it is possible to do "one-shot" calls to a subdomain, having control return immediately after the call.
        /// </summary>
        public static async Task TestConversationFlowCrossDomainOneShotInSubDomain(DialogProcessingEngine dialogEngine)
        {
            // Start super answer
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer. This should be the only turn the the subanswer processes.
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "cancel_callback_a", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("cancel_b", response.TriggeredIntent);

            // Now return to super answer after sub closes
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "carry_on", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsFalse(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("carry_on", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that the state of a super domain can continue with a callback after a subdomain finishes
        /// </summary>
        public static async Task TestConversationFlowCrossDomainCanUseCommonDomainTransitions(DialogProcessingEngine dialogEngine)
        {
            // Start super answer
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Use the common intent to trigger the domain transition
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList(DialogConstants.COMMON_DOMAIN, "commonintent", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("continue_b", response.TriggeredIntent);
        }
        
        /// <summary>
        /// Tests that a superdomain can transition completely to a subdomain and then have its state be clean afterwards
        /// </summary>
        public static async Task TestConversationFlowCrossDomainCanFinishTwoDomainsAtOnce(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            // Start super answer
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "continue_a", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("continue_b", response.TriggeredIntent);

            // Tell sub answer to finish. The conversation should be done now.
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_sub", "cancel_b", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsFalse(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("cancel_b", response.TriggeredIntent);

            // To make sure, start talking to some other answer entirely
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "succeed", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basic", response.TriggeredDomain);
            Assert.AreEqual("succeed", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that if a subdomain goes tenative, the user can reenter the superdomain by triggering one of its continuations.
        /// </summary>
        public static async Task TestConversationFlowCrossDomainCanReturnToSuperAfterSubIsTenative(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            // Start super answer
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "continue_callback_a", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("continue_b", response.TriggeredIntent);

            // Talk to sub answer a bit
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_sub", "tenative_b", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.IsFalse(response.NextTurnBehavior.IsImmediate);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("tenative_b", response.TriggeredIntent);

            // Now what sub answer is tenative, trigger the super answer again. The sub answer should go away
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "carry_on", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsFalse(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("carry_on", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that if a subdomain goes tenative, the user can reenter the superdomain by triggering one of its continuations, even if the superdomain is also tenative
        /// </summary>
        public static async Task TestConversationFlowCrossDomainCanReturnToTenativeSuperAfterSubIsTenative(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            // Start super answer
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "tenative_callback_a", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("continue_b", response.TriggeredIntent);

            // Talk to sub answer a bit
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_sub", "tenative_b", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.IsFalse(response.NextTurnBehavior.IsImmediate);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("tenative_b", response.TriggeredIntent);

            // Now what sub answer is tenative, trigger the super answer again. The sub answer should go away
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "carry_on", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsFalse(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("carry_on", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that a sub domain can invoke a callback to a super domain and pass slot values as part of its response
        /// </summary>
        public static async Task TestConversationFlowCrossDomainExplicitCallbackWithReturnSlots(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            // Start super answer
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "call_b_with_callback", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_sub", response.TriggeredDomain);
            Assert.AreEqual("callback_1", response.TriggeredIntent);

            // Give information to the subanswer. This turn executes the subanswer,
            // which in turn invokes the callback to the super, so the super answer is what should
            // generate the response.
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_sub", "callback_2", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("callback_intent_a", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that a super domain can call a subdomain and have it return in one shot, so the subdomain
        /// response is never actually surfaced.
        /// </summary>
        public static async Task TestConversationFlowCrossDomainExplicitCallbackWithReturnSlotsOneShot(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            // Start super answer
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("start", response.TriggeredIntent);

            // Call into sub answer, but in such a way that it returns immediately. This ends up executing 3 plugins in a row:
            // Super plugin with intent call_b_with_callback_oneshot, executing the CDR exchange
            // Sub plugin with intent callback_2, running logic, and returning a invoked dialog action
            // Super plugin again with intent callback_intent_a, which renders the actual result
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "call_b_with_callback_oneshot", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("cd_super", response.TriggeredDomain);
            Assert.AreEqual("callback_intent_a", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that if a subdomain tries to invoke a callback that doesn't exist, an error is returned.
        /// </summary>
        public static async Task TestConversationFlowCrossDomainExplicitCallbackToNonexistentIntent(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            try
            {
                // Start super answer
                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "start", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.IsTrue(response.NextTurnBehavior.Continues);
                Assert.AreEqual("cd_super", response.TriggeredDomain);
                Assert.AreEqual("start", response.TriggeredIntent);

                // Call into sub answer
                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_super", "call_b_with_callback", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.IsTrue(response.NextTurnBehavior.Continues);
                Assert.AreEqual("cd_sub", response.TriggeredDomain);
                Assert.AreEqual("callback_1", response.TriggeredIntent);

                // Attempt to invoke a nonexistent callback. This should raise an exception
                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("cd_sub", "callback_2_bad", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);
                Assert.Fail("Should have thrown a DialogException");
            }
            catch (DialogException) { }
        }

        #endregion

        #region Retry/noreco tests

        /// <summary>
        /// Test that, if we are locked-in, and pass an invalid query to a node that has no retry
        /// handler, that the entire turn will be skipped, and that we can still continue the conversation.
        /// </summary>
        public static async Task TestConversationFlowBasicRetry(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            // start a conversation
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("retrytree", "start_succeed", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.StartSucceedTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.RetryTreeAnswer.RetrySucceedTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.RetryTreeAnswer.Turn2Trigger.Get());

            // send some nonsense. retry should trigger
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "thanks", 0.6f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.StartSucceedTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.RetrySucceedTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.RetryTreeAnswer.Turn2Trigger.Get());
            Assert.IsTrue(response.NextTurnBehavior.Continues);

            // now send valid input to continue the conversation
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("retrytree", "turn2", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.StartSucceedTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.RetrySucceedTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that if a retry continuation returns SKIP, the conversation ends gracefully
        /// and the client still gets a SUCCESS response
        /// </summary>
        public static async Task TestConversationFlowRetryWithSkip(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            // start a conversation
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("retrytree", "start_skip", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.StartSkipTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.RetryTreeAnswer.RetrySkipTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.RetryTreeAnswer.Turn2Trigger.Get());

            // send some nonsense. retry should trigger and return "skip"
            // Ensure that this translates into the client recieving a "success" with continues = false
            // and the conversation state is eliminated
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "thanks", 0.6f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.StartSkipTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.RetrySkipTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.RetryTreeAnswer.Turn2Trigger.Get());
            Assert.IsFalse(response.NextTurnBehavior.Continues);

            // Trying to continue the conversation shouldn't work at this point
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("retrytree", "turn2", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.StartSkipTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.RetrySkipTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.RetryTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that, if a retry continuation returns FAIL, the conversation is dropped
        /// but the client gets a Success response
        /// </summary>
        public static async Task TestConversationFlowRetryWithFail(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            // start a conversation
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("retrytree", "start_fail", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.StartFailTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.RetryTreeAnswer.RetryFailTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.RetryTreeAnswer.Turn2Trigger.Get());

            // send some nonsense. retry should trigger but then fail
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "thanks", 0.6f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Failure, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.StartFailTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.RetryFailTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.RetryTreeAnswer.Turn2Trigger.Get());
            Assert.IsFalse(response.NextTurnBehavior.Continues);

            // Trying to continue the conversation shouldn't work at this point
            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("retrytree", "turn2", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.StartFailTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.RetryTreeAnswer.RetryFailTrigger.Get());
            Assert.AreEqual(0, mockPluginLoader.RetryTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that the common/noreco intent will trigger a non-retry response on 2nd-turn for answers that are configured to consume it.
        /// </summary>
        public static async Task TestConversationFlowExplicitConsumeNoRecoOnSecondTurn(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "noreco", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.NoRecoTrigger.Get());
        }

        /// <summary>
        /// Test that tenative multiturn allows us to do multiturn if we decide to keep going in the conversation
        /// </summary>
        public static async Task TestConversationFlowNorecoDoesNotBreakTenativeMultiturn(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_tenative", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTenativeTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "noreco", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Skip, response.ResponseCode);
            Assert.AreEqual(0, mockPluginLoader.BasicTreeAnswer.NoRecoTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTenativeTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Test that, if we are locked-in, and pass an invalid query to a node that has no retry
        /// handler, that the entire turn will be skipped, and that we can still continue the conversation.
        /// </summary>
        public static async Task TestConversationFlowCanRetryWithoutRetryHandler(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_lock", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartLockTrigger.Get());

            for (int c = 0; c < 3; c++)
            {
                // Common intent should be ignored by all plugins.
                // DE will then try a 2nd pass with noreco intent to see if a retry continuation is present.
                // Since there's not, it will return "skip" without deleting the conversation state.
                // In these cases, the client should maintain the last turn's multiturnbehavior, and prompt
                // the user for further clarification (as though a retry occurred)
                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "thanks", 0.6f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Skip, response.ResponseCode);
                Assert.AreEqual(0, mockPluginLoader.BasicTreeAnswer.NoRecoTrigger.Get());
                Assert.AreEqual(0, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());

                // This is important too - make sure multiturn state sent to the client is preserved, so the client is not left
                // in an inconsistent conversation
                Assert.IsTrue(response.NextTurnBehavior.Continues);
            }

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 0.9f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartLockTrigger.Get());
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
        }

        #endregion

        #region Conversation tree tests

        // test valid and invalid teleportation

        #endregion

        #region Session store tests

        /// <summary>
        /// Test that an object written to the object store is carried forward through turns
        /// </summary>
        public static async Task TestConversationFlowObjectStoreCarriesForward(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_objectstore", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        /// <summary>
        /// Test that an object written to the object store vanishes after leaving multiturn
        /// </summary>
        public static async Task TestConversationFlowObjectStoreDoesntPersist(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_objectstore", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_objectstore", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        /// <summary>
        /// Test that the PastTurns object stores valid turns in the proper order and that the past turns
        /// are pruned at an appropriate amount
        /// </summary>
        public static async Task TestConversationFlowPreviousTurnsAreStoredAndPruned(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f, "0")),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);

            for (int loop = 1; loop < 50; loop++)
            {
                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "loop", 1.0f, loop.ToString())),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
            }
        }

        #endregion

        #region Entity history tests

        /// <summary>
        /// Tests that a plugin can stash something in the entity history and fetch it back again
        /// </summary>
        public static async Task TestConversationFlowEntityHistorySameDomain(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_a", "write", 0.99f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("entities_a", response.TriggeredDomain);

            recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_a", "read", 0.99f));

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("entities_a", response.TriggeredDomain);
        }

        /// <summary>
        /// Tests that entities stored within the same domain expire after 50 turns
        /// </summary>
        public static async Task TestConversationFlowEntityHistorySameDomainEntitiesExpire(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_a", "write", 0.99f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("entities_a", response.TriggeredDomain);

            recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "succeed", 0.99f));

            for (int turn = 0; turn < 50; turn++)
            {
                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(recoResults),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("basic", response.TriggeredDomain);
            }

            recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_a", "read", 0.99f));

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Failure, response.ResponseCode);
        }

        /// <summary>
        /// Tests that if the dialog engine skips a whole bunch that the entity history only
        /// turns a maximum of one time
        /// </summary>
        public static async Task TestConversationFlowEntityHistoryOnlyTurnsOncePerRun(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_a", "write", 0.99f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("entities_a", response.TriggeredDomain);

            // Inject a whole pile of skip LU results on the reco results
            recoResults = new List<RecoResult>();
            for (int turn = 0; turn < 25; turn++)
            {
                recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "skip", 1.0f - (turn * 0.001f)));
            }

            // And finally a succeed trigger
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "succeed", 0.8f));

            response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(recoResults),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basic", response.TriggeredDomain);

            recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_a", "read", 0.99f));

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("entities_a", response.TriggeredDomain);
        }

        /// <summary>
        /// Tests that a plugin can stash something in the entity history and fetch it back again
        /// </summary>
        public static async Task TestConversationFlowEntityHistoryCrossDomain(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_a", "write", 0.99f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("entities_a", response.TriggeredDomain);

            recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_b", "read", 0.99f));

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("entities_b", response.TriggeredDomain);
        }

        /// <summary>
        /// Tests that entities stored within the same domain expire after 50 turns
        /// </summary>
        public static async Task TestConversationFlowEntityHistoryCrossDomainEntitiesExpire(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_a", "write", 0.99f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("entities_a", response.TriggeredDomain);

            recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "succeed", 0.99f));

            for (int turn = 0; turn < 50; turn++)
            {
                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(recoResults),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("basic", response.TriggeredDomain);
            }

            recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_b", "read", 0.99f));

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Failure, response.ResponseCode);
        }

        /// <summary>
        /// Tests that entities stored within the same domain don't expire if they are constantly touched
        /// </summary>
        public static async Task TestConversationFlowEntityHistoryCrossDomainEntitiesDontExpire(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_a", "write", 0.99f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("entities_a", response.TriggeredDomain);

            for (int turn = 0; turn < 25; turn++)
            {
                recoResults = new List<RecoResult>();
                recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("basic", "succeed", 0.99f));

                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(recoResults),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("basic", response.TriggeredDomain);

                recoResults = new List<RecoResult>();
                recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_b", "write", 0.99f));

                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(recoResults),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("entities_b", response.TriggeredDomain);
            }

            recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("entities_a", "read", 0.99f));

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("entities_a", response.TriggeredDomain);
        }

        #endregion

        #region User profile tests

        /// <summary>
        /// Test that an object written to the user profile is carried forward through turns
        /// </summary>
        public static async Task TestConversationFlowUserProfilePersistsBetweenSessions(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_userprofile_1", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_userprofile_2", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        /// <summary>
        /// Test that an object written to the user profile is isolated by domain
        /// </summary>
        public static async Task TestConversationFlowUserProfileIsDomainIsolated(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_userprofile_1", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basic", "emptyuserprofile", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        #endregion

        #region Conversation state tests

        /// <summary>
        /// Tests to make sure conversation state does not persist after returning from a terminating convo node.
        /// </summary>
        public static async Task TestConversationFlowConversationStateIsClearedAfterFinish(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader,
            IConversationStateCache conversationStateCache)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            RetrieveResult<Stack<ConversationState>> result = await conversationStateCache.TryRetrieveState(
                DialogTestHelpers.GetTestClientContextTextQuery().UserId,
                DialogTestHelpers.GetTestClientContextTextQuery().ClientId,
                logger,
                realTime);

            Assert.IsTrue(result.Success);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 0.99f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());

            result = await conversationStateCache.TryRetrieveState(
                DialogTestHelpers.GetTestClientContextTextQuery().UserId,
                DialogTestHelpers.GetTestClientContextTextQuery().ClientId,
                logger,
                realTime);
            Assert.IsFalse(result.Success);
        }

        /// <summary>
        /// Simulates a user (represented by user ID) transitioning a conversation across devices (represented by separate client IDs)
        /// </summary>
        public static async Task TestConversationFlowMultiturnOneUserTwoDevices(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            ClientContext turn1Context = DialogTestHelpers.GetTestClientContextTextQuery();
            turn1Context.ClientId = "Device1ClientId";

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                turn1Context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            ClientContext turn2Context = DialogTestHelpers.GetTestClientContextTextQuery();
            turn2Context.ClientId = "Device2ClientId";

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 0.99f)),
                turn2Context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Simulates two users (represented by different user IDs) having interleaved conversations on the same client
        /// (presumably set up as some kind of web service or public kiosk)
        /// </summary>
        public static async Task TestConversationFlowMultiturnTwoUsersOneDevice(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            ClientContext user1Context = DialogTestHelpers.GetTestClientContextTextQuery();
            user1Context.UserId = "User1UserId";

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                user1Context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            ClientContext user2Context = DialogTestHelpers.GetTestClientContextTextQuery();
            user2Context.UserId = "User2UserId";

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                user2Context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(2, mockPluginLoader.BasicTreeAnswer.StartTrigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 0.99f)),
                user1Context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(1, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 0.99f)),
                user2Context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual(2, mockPluginLoader.BasicTreeAnswer.Turn2Trigger.Get());
        }

        /// <summary>
        /// Simulates a user starting 2 conversations on 2 devices and having them remain separate
        /// </summary>
        public static async Task TestConversationFlowClientSpecificStatesAreSet(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            // User starts a conversation on one device

            ClientContext context = DialogTestHelpers.GetTestClientContextTextQuery();
            context.ClientId = "Device1ClientId";
            context.UserId = "User1UserId";

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f)),
                context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("start_tenative", response.TriggeredIntent);

            // And then another conversation on another device

            context = DialogTestHelpers.GetTestClientContextTextQuery();
            context.ClientId = "Device2ClientId";
            context.UserId = "User1UserId";

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f)),
                context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Spoken);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("start_tenative", response.TriggeredIntent);

            // Each device then continues the conversation programmatically

            context = DialogTestHelpers.GetTestClientContextTextQuery();
            context.ClientId = "Device1ClientId";
            context.UserId = "User1UserId";

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 1.0f)),
                context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Programmatic);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("turn2", response.TriggeredIntent);

            context = DialogTestHelpers.GetTestClientContextTextQuery();
            context.ClientId = "Device2ClientId";
            context.UserId = "User1UserId";

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "loop2", 1.0f)),
                context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Programmatic);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("loop2", response.TriggeredIntent);
        }

        /// <summary>
        /// Simulates a user have a conversation across devices while the first device also executes programmatic actions
        /// </summary>
        public static async Task TestConversationFlowClientSpecificStatesAreUsedAfterClientAction(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            // User starts a conversation on one device

            ClientContext context = DialogTestHelpers.GetTestClientContextTextQuery();
            context.ClientId = "Device1ClientId";
            context.UserId = "User1UserId";

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f)),
                context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("start_tenative", response.TriggeredIntent);

            // The client does some kind of action

            context = DialogTestHelpers.GetTestClientContextTextQuery();
            context.ClientId = "Device1ClientId";
            context.UserId = "User1UserId";

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "loop2", 1.0f)),
                context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Programmatic);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("loop2", response.TriggeredIntent);

            // The user then starts talking to another device

            context = DialogTestHelpers.GetTestClientContextTextQuery();
            context.ClientId = "Device2ClientId";
            context.UserId = "User1UserId";

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_tenative", 1.0f)),
                context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Spoken);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("start_tenative", response.TriggeredIntent);

            // The first device can still execute programmatically

            context = DialogTestHelpers.GetTestClientContextTextQuery();
            context.ClientId = "Device1ClientId";
            context.UserId = "User1UserId";

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "loop2", 1.0f)),
                context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Programmatic);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("loop2", response.TriggeredIntent);

            // While the user is still talking to device 2

            context = DialogTestHelpers.GetTestClientContextTextQuery();
            context.ClientId = "Device2ClientId";
            context.UserId = "User1UserId";

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 1.0f)),
                context,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Spoken);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("basictree", response.TriggeredDomain);
            Assert.AreEqual("turn2", response.TriggeredIntent);
        }

        #endregion

        #region Versioning tests

        /// <summary>
        /// Simulates a multiturn conversation in which the plugin version changes in mid conversation
        /// </summary>
        public static async Task TestConversationFlowConversationStateRecoversAfterBreakingVersionChange(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            BasicPluginLoader answerProvider = BuildBasicPluginLoader();
            answerProvider.RegisterPluginType(new VersioningPlugin1(1));
            answerProvider.RegisterPluginType(new VersioningPlugin2());
            DialogEngineParameters dialogParameters = defaultDialogParameters.Clone();
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(answerProvider))
            {
                dialogParameters.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(dialogParameters);
                await dialogEngine.LoadPlugin(new PluginStrongName("versioning_plugin", 1, 1), realTime);

                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("versioning", "turn1", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("Turn 1 Version 1.1", response.DisplayedText);

                // Load the new plugin and unload the old
                await dialogEngine.UnloadPlugin(new PluginStrongName("versioning_plugin", 1, 1), realTime);
                await dialogEngine.LoadPlugin(new PluginStrongName("versioning_plugin", 2, 2), realTime);

                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("versioning", "turn2", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                // The expected behavior in this particular case is that, when we try to trigger turn 2, the conversation state gets detected as invalid
                // resulting in empty dialog state, and then the plugin gets skipped since the transition directly to turn 2 is not allowed.
                // Other tree structures might yield different behaviors; but the main point here is that no exceptions get thrown during processing.
                Assert.AreEqual(Result.Skip, response.ResponseCode);
            }
        }

        public static async Task TestConversationFlowConversationStateContinuesAfterMajorVersionChange(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            BasicPluginLoader answerProvider = BuildBasicPluginLoader();
            answerProvider.RegisterPluginType(new VersioningPlugin1(1));
            answerProvider.RegisterPluginType(new VersioningPlugin2());
            DialogEngineParameters dialogParameters = defaultDialogParameters.Clone();
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(answerProvider))
            {
                dialogParameters.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(dialogParameters);
                await dialogEngine.LoadPlugin(new PluginStrongName("versioning_plugin", 1, 1), realTime);

                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("versioning", "turn1", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("Turn 1 Version 1.1", response.DisplayedText);

                // Load verson 2.2 of the plugin alongside the old one
                await dialogEngine.LoadPlugin(new PluginStrongName("versioning_plugin", 2, 2), realTime);

                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("versioning", "turn2", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("Turn 2 Version 1.1", response.DisplayedText);
            }
        }

        public static async Task TestConversationFlowConversationStateContinuesAfterMinorVersionChange(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            BasicPluginLoader answerProvider = BuildBasicPluginLoader();
            answerProvider.RegisterPluginType(new VersioningPlugin1(1));
            answerProvider.RegisterPluginType(new VersioningPlugin1(5));
            DialogEngineParameters dialogParameters = defaultDialogParameters.Clone();
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(answerProvider))
            {
                dialogParameters.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(dialogParameters);
                await dialogEngine.LoadPlugin(new PluginStrongName("versioning_plugin", 1, 1), realTime);

                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("versioning", "turn1", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("Turn 1 Version 1.1", response.DisplayedText);

                // Load verson 1.5 of the plugin and get rid of the old one
                await dialogEngine.UnloadPlugin(new PluginStrongName("versioning_plugin", 1, 1), realTime);
                await dialogEngine.LoadPlugin(new PluginStrongName("versioning_plugin", 1, 5), realTime);

                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("versioning", "turn2", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("Turn 2 Version 1.5", response.DisplayedText);
            }
        }

        public static async Task TestConversationFlowConversationStateContinuesAfterMinorVersionChangeSideBySide(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            BasicPluginLoader answerProvider = BuildBasicPluginLoader();
            answerProvider.RegisterPluginType(new VersioningPlugin1(1));
            answerProvider.RegisterPluginType(new VersioningPlugin1(5));
            DialogEngineParameters dialogParameters = defaultDialogParameters.Clone();
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(answerProvider))
            {
                dialogParameters.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(dialogParameters);
                await dialogEngine.LoadPlugin(new PluginStrongName("versioning_plugin", 1, 1), realTime);

                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("versioning", "turn1", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("Turn 1 Version 1.1", response.DisplayedText);

                // Load verson 1.5 of the plugin alongside the old one
                await dialogEngine.LoadPlugin(new PluginStrongName("versioning_plugin", 1, 5), realTime);

                response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("versioning", "turn2", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("Turn 2 Version 1.5", response.DisplayedText);
            }
        }

        public static async Task TestConversationFlowCantLoadTwoPluginsWithSameVersion(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            try
            {
                BasicPluginLoader answerProvider = BuildBasicPluginLoader();
                answerProvider.RegisterPluginType(new VersioningPlugin1(0));
                DialogEngineParameters dialogParameters = defaultDialogParameters.Clone();
                using (IDurandalPluginProvider provider = buildBasicPluginProvider(answerProvider))
                {
                    dialogParameters.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                    DialogProcessingEngine dialogEngine = new DialogProcessingEngine(dialogParameters);
                    await dialogEngine.LoadPlugin(new PluginStrongName("versioning_plugin", 1, 0), realTime);
                    await dialogEngine.LoadPlugin(new PluginStrongName("versioning_plugin", 1, 0), realTime);
                    Assert.Fail("Should have thrown an exception");
                }
            }
            catch (Exception) { }
        }

        #endregion

        #region Distributed environment tests

        /// <summary>
        /// Simulates a multiturn conversation that spans multiple DialogEngine instances which share a common session store
        /// </summary>
        public static async Task TestConversationFlowMultiturnCloudy(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            TestPluginLoader loader1 = new TestPluginLoader(new BasicDialogExecutor(true));
            DialogEngineParameters params1 = defaultDialogParameters.Clone();
            using (IDurandalPluginProvider provider1 = buildBasicPluginProvider(loader1))
            {
                params1.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider1);
                DialogProcessingEngine dialogEngine1 = new DialogProcessingEngine(params1);
                await dialogEngine1.LoadPlugin("basictree", realTime);

                TestPluginLoader loader2 = new TestPluginLoader(new BasicDialogExecutor(true));
                DialogEngineParameters params2 = defaultDialogParameters.Clone();
                using (IDurandalPluginProvider provider2 = buildBasicPluginProvider(loader2))
                {
                    params2.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider2);
                    DialogProcessingEngine dialogEngine2 = new DialogProcessingEngine(params2);
                    await dialogEngine2.LoadPlugin("basictree", realTime);

                    DialogEngineResponse response = await dialogEngine1.Process(
                        RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                        DialogTestHelpers.GetTestClientContextTextQuery(),
                        ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                        InputMethod.Typed);

                    Assert.AreEqual(Result.Success, response.ResponseCode);
                    Assert.AreEqual(1, loader1.BasicTreeAnswer.StartTrigger.Get());
                    Assert.AreEqual(0, loader2.BasicTreeAnswer.StartTrigger.Get());

                    response = await dialogEngine2.Process(
                        RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 1.0f)),
                        DialogTestHelpers.GetTestClientContextTextQuery(),
                        ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                        InputMethod.Typed);

                    Assert.AreEqual(Result.Success, response.ResponseCode);
                    Assert.AreEqual(0, loader1.BasicTreeAnswer.Turn2Trigger.Get());
                    Assert.AreEqual(1, loader2.BasicTreeAnswer.Turn2Trigger.Get());
                }
            }
        }

        /// <summary>
        /// Simulates a multiturn conversation that spans multiple DialogEngine instances which share a common session store,
        /// while the user also transitions between two different clients with the same userId
        /// </summary>
        public static async Task TestConversationFlowMultiturnTwoDevicesCloudy(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            TestPluginLoader loader1 = new TestPluginLoader(new BasicDialogExecutor(true));
            DialogEngineParameters params1 = defaultDialogParameters.Clone();
            using (IDurandalPluginProvider provider1 = buildBasicPluginProvider(loader1))
            {
                params1.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider1);
                DialogProcessingEngine dialogEngine1 = new DialogProcessingEngine(params1);
                await dialogEngine1.LoadPlugin("basictree", realTime);

                TestPluginLoader loader2 = new TestPluginLoader(new BasicDialogExecutor(true));
                DialogEngineParameters params2 = defaultDialogParameters.Clone();
                using (IDurandalPluginProvider provider2 = buildBasicPluginProvider(loader2))
                {
                    params2.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider2);
                    DialogProcessingEngine dialogEngine2 = new DialogProcessingEngine(params2);
                    await dialogEngine2.LoadPlugin("basictree", realTime);

                    ClientContext client1Context = DialogTestHelpers.GetTestClientContextTextQuery();
                    client1Context.ClientId = "Client1Id";
                    DialogEngineResponse response = await dialogEngine1.Process(
                        RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)),
                        client1Context,
                        ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                        InputMethod.Typed);

                    Assert.AreEqual(Result.Success, response.ResponseCode);
                    Assert.AreEqual(1, loader1.BasicTreeAnswer.StartTrigger.Get());
                    Assert.AreEqual(0, loader2.BasicTreeAnswer.StartTrigger.Get());

                    ClientContext client2Context = DialogTestHelpers.GetTestClientContextTextQuery();
                    client2Context.ClientId = "Client2Id";
                    response = await dialogEngine2.Process(
                        RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 1.0f)),
                        client2Context,
                        ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                        InputMethod.Typed);

                    Assert.AreEqual(Result.Success, response.ResponseCode);
                    Assert.AreEqual(0, loader1.BasicTreeAnswer.Turn2Trigger.Get());
                    Assert.AreEqual(1, loader2.BasicTreeAnswer.Turn2Trigger.Get());
                }
            }
        }

        /// <summary>
        /// Simulates a multiturn conversation that spans multiple DialogEngine instances which share a common dialog action cache,
        /// ensuring that the cached action can be executed on different machines
        /// </summary>
        public static async Task TestConversationFlowMultiturnDialogActionsCloudy(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            InMemoryCache<DialogAction> dialogActionCache = new InMemoryCache<DialogAction>();

            TestPluginLoader loader1 = new TestPluginLoader(
                new BasicDialogExecutor(true));
            using (MachineLocalPluginProvider provider1 = new MachineLocalPluginProvider(
                logger,
                loader1,
                NullFileSystem.Singleton,
                new NLPToolsCollection(),
                null,
                null,
                null,
                null,
                new NullHttpClientFactory(),
                null))
            {
                DialogEngineParameters params1 = defaultDialogParameters.Clone();
                params1.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider1);
                params1.DialogActionCache = new WeakPointer<ICache<DialogAction>>(dialogActionCache);
                DialogProcessingEngine dialogEngine1 = new DialogProcessingEngine(params1);
                await dialogEngine1.LoadPlugin("basictree", realTime);

                TestPluginLoader loader2 = new TestPluginLoader(
                    new BasicDialogExecutor(true));

                using (MachineLocalPluginProvider provider2 = new MachineLocalPluginProvider(
                    logger,
                    loader2,
                    NullFileSystem.Singleton,
                    new NLPToolsCollection(),
                    null,
                    null,
                    null,
                    null,
                    new NullHttpClientFactory(),
                    null))
                {
                    DialogEngineParameters params2 = defaultDialogParameters.Clone();
                    params2.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider2);
                    params2.DialogActionCache = new WeakPointer<ICache<DialogAction>>(dialogActionCache);
                    DialogProcessingEngine dialogEngine2 = new DialogProcessingEngine(params2);
                    await dialogEngine2.LoadPlugin("basictree", realTime);

                    DialogEngineResponse response = await dialogEngine1.Process(
                        RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_dialogaction", 1.0f)),
                        DialogTestHelpers.GetTestClientContextTextQuery(),
                        ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                        InputMethod.Typed);

                    Assert.AreEqual(Result.Success, response.ResponseCode);
                    Assert.IsTrue(response.ResponseData.ContainsKey("actionKey"));
                    string actionKey = response.ResponseData["actionKey"];
                    RetrieveResult<DialogAction> cacheResult = await dialogActionCache.TryRetrieve(actionKey, logger, realTime);
                    Assert.IsTrue(cacheResult.Success);
                    DialogAction desiredAction = cacheResult.Result;

                    response = await dialogEngine2.Process(
                        RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList(desiredAction.Domain, desiredAction.Intent, 1.0f)),
                        DialogTestHelpers.GetTestClientContextTextQuery(),
                        ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                        InputMethod.Programmatic);

                    Assert.AreEqual(Result.Success, response.ResponseCode);
                    Assert.AreEqual(0, loader1.BasicTreeAnswer.Turn2Trigger.Get());
                    Assert.AreEqual(1, loader2.BasicTreeAnswer.Turn2Trigger.Get());
                }
            }
        }

        #endregion

        #region Trigger tests

        /// <summary>
        /// Test that a trigger which is boosted will take priority over others, and also
        /// implicitly test that triggering works and that session store is available
        /// </summary>
        public static async Task TestConversationFlowBasicTrigger(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_a", "fail", 0.99f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_b", "boost", 0.75f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_b", response.TriggeredDomain);
        }

        /// <summary>
        /// Test that triggers for multiple plugins will all run in parallel
        /// </summary>
        public static async Task TestConversationFlowTriggersRunInParallel(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            // Each "trigger slowly" costs 10ms. So if this ran serially it would take 10 seconds.
            for (int c = 0; c < 1000; c++)
            {
                recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_slow", "trigger_slowly_" + c.ToString(), 0.99f));
            }

            Stopwatch timer = Stopwatch.StartNew();
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            timer.Stop();
            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_slow", response.TriggeredDomain);
            Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Test that a trigger which is boosted will take priority over others, and also
        /// implicitly test that triggering works and that session store is available
        /// </summary>
        public static async Task TestConversationFlowMultipleTriggersCauseDisambiguation(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_a", "boost", 0.99f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_b", "boost", 0.75f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("reflection", response.TriggeredDomain);
            Assert.AreEqual("disambiguate", response.TriggeredIntent);
            Assert.IsTrue(response.NextTurnBehavior.Continues);
        }

        /// <summary>
        /// Tests disambiguation triggers and we can successfully drop back from the reflection domain
        /// into the intended target domain
        /// </summary>
        public static async Task TestConversationFlowDisambiguationFull(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_a", "boost", 0.99f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_b", "boost", 0.75f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("reflection", response.TriggeredDomain);
            Assert.AreEqual("disambiguate", response.TriggeredIntent);
            Assert.IsTrue(response.NextTurnBehavior.Continues);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 0.84f, "trigger_b/boost")),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_b", response.TriggeredDomain);
            Assert.AreEqual("boost", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that session values which are stored at original trigger time are preserved after
        /// disambiguation returns
        /// </summary>
        public static async Task TestConversationFlowDisambiguationWithTriggerTimeSessionPreserved(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_a", "boost", 0.99f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_b", "boost_with_params", 0.75f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("reflection", response.TriggeredDomain);
            Assert.AreEqual("disambiguate", response.TriggeredIntent);
            Assert.IsTrue(response.NextTurnBehavior.Continues);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 0.84f, "trigger_b/boost_with_params")),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_b", response.TriggeredDomain);
            Assert.AreEqual("boost_with_params", response.TriggeredIntent);
        }

        public static async Task TestConversationFlowDisambiguationFallsBackWhenReflectionDomainCannotHandle(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineParameters dialogParams = defaultDialogParameters.Clone();
            IDurandalPluginLoader loader = BuildTestPluginLoader();
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(loader))
            {
                dialogParams.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(dialogParams);
                await dialogEngine.SetLoadedPlugins(new PluginStrongName[] { new PluginStrongName("trigger_a", 0, 0), new PluginStrongName("trigger_b", 0, 0) }, realTime);

                List<RecoResult> recoResults = new List<RecoResult>();
                recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_a", "boost", 0.99f));
                recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_b", "boost", 0.75f));

                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(recoResults),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
                Assert.AreEqual("trigger_a", response.TriggeredDomain);
                Assert.AreEqual("boost", response.TriggeredIntent);
            }
        }

        /// <summary>
        /// Tests that we properly handle multiple boosted intents with the same domain name
        /// </summary>
        public static async Task TestConversationFlowDisambiguationWithinSameDomain(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_a", "fail", 0.99f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_a", "boost", 0.85f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_b", "boost", 0.75f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("reflection", response.TriggeredDomain);
            Assert.AreEqual("disambiguate", response.TriggeredIntent);
            Assert.IsTrue(response.NextTurnBehavior.Continues);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 0.84f, "trigger_a/boost")),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_a", response.TriggeredDomain);
            Assert.AreEqual("boost", response.TriggeredIntent);
        }

        /// <summary>
        /// Tests that we properly handle multiple boosted intents with the same domain name,
        /// and where each trigger result requires a unique session store keyed to INTENT as well as domain
        /// </summary>
        public static async Task TestConversationFlowDisambiguationWithinSameDomainWithSideEffects(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            List<RecoResult> recoResults = new List<RecoResult>();
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_a", "fail", 0.99f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_a", "boost_with_params", 0.85f));
            recoResults.Add(DialogTestHelpers.GetSimpleRecoResult("trigger_a", "boost_with_params_2", 0.75f));

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("reflection", response.TriggeredDomain);
            Assert.AreEqual("disambiguate", response.TriggeredIntent);
            Assert.IsTrue(response.NextTurnBehavior.Continues);

            response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("common", "side_speech", 0.84f, "trigger_a/boost_with_params_2")),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("trigger_a", response.TriggeredDomain);
            Assert.AreEqual("boost_with_params_2", response.TriggeredIntent);
        }

        #endregion

        #region Other services tests

        public static async Task TestConversationFlowSpeechReco(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader,
            FakeSpeechRecognizerFactory fakeSpeechRecognizer)
        {
            fakeSpeechRecognizer.SetRecoResult("en-US", "this is the english reco result");
            fakeSpeechRecognizer.SetRecoResult("es-mx", "this is the spanish reco result");
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("test_remoting", "speech_reco", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);
            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        public static async Task TestConversationFlowSpeechSynth(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("test_remoting", "speech_synth", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);
            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        public static async Task TestConversationFlowCreateOAuthUri(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("test_remoting", "oauth_create_uri", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);
            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        public static async Task TestConversationFlowGetOAuthToken(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            FakeOAuthSecretStore fakeOAuthStore)
        {
            fakeOAuthStore.SetMockToken(new OAuthToken()
            {
                Token = "secret",
                TokenType = "Bearer",
                IssuedAt = realTime.Time - TimeSpan.FromHours(1),
                ExpiresAt = realTime.Time + TimeSpan.FromDays(30),
                RefreshToken = null
            });

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("test_remoting", "oauth_get_token", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);
            Assert.AreEqual(Result.Success, response.ResponseCode);
        }

        public static async Task TestConversationFlowDeleteOAuthToken(
            ILogger logger,
            DialogProcessingEngine dialogEngine,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            FakeOAuthSecretStore fakeOAuthStore)
        {
            fakeOAuthStore.SetMockToken(new OAuthToken()
            {
                Token = "secret",
                TokenType = "Bearer",
                IssuedAt = realTime.Time - TimeSpan.FromHours(1),
                ExpiresAt = realTime.Time + TimeSpan.FromDays(30),
                RefreshToken = null
            });

            DialogEngineResponse response = await dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("test_remoting", "oauth_delete_token", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);
            Assert.AreEqual(Result.Success, response.ResponseCode);

            RetrieveResult<OAuthState> getStateResult = await fakeOAuthStore.RetrieveState("userid", new PluginStrongName("id", 0, 0), new OAuthConfig());
            Assert.IsFalse(getStateResult.Success);
        }

        #endregion

        private static TestPluginLoader BuildTestPluginLoader()
        {
            return new TestPluginLoader(new BasicDialogExecutor(true));
        }

        private static BasicPluginLoader BuildBasicPluginLoader()
        {
            return new BasicPluginLoader(new BasicDialogExecutor(true), NullFileSystem.Singleton);
        }
    }
}
