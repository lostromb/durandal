

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
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Audio;
    using Durandal.Common.LG.Statistical;
using Durandal.Tests.Common.Dialog.Runtime;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;

    [TestClass]
    public class RemotedJsonConversationFlowTests
    {
        private static IRealTimeProvider _realTime;
        private static InMemoryCache<DialogAction> _mockDialogActionCache;
        private static InMemoryCache<CachedWebData> _mockWebDataCache;
        private static InMemoryConversationStateCache _conversationStateCache;
        private static InMemoryProfileStorage _mockUserProfilestore;
        private static TestPluginLoader _mockPluginLoader;
        private static FakeSpeechRecognizerFactory _fakeSpeechReco;
        private static FakeSpeechSynth _fakeSpeechSynth;
        private static FakeOAuthSecretStore _fakeOAuthStore;
        private static OAuthManager _oauthManager;
        private static IDurandalPluginProvider _mockPluginProvider;
        private static ILogger _logger;
        private static IConfiguration _baseConfig;
        private static RemotingConfiguration _remotingConfig;
        private static DialogProcessingEngine _dialogEngine;
        private static DialogEngineParameters _defaultDialogParameters;

        [ClassInitialize]
        public static void InitializeClass(TestContext context)
        {
            _logger = new ConsoleLogger("Main", LogLevel.Err | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            _realTime = DefaultRealTimeProvider.Singleton;
            _baseConfig = new InMemoryConfiguration(_logger.Clone("Config"));
            _remotingConfig = DialogTestHelpers.GetTestRemotingConfiguration(new WeakPointer<IConfiguration>(_baseConfig));
            _conversationStateCache = new InMemoryConversationStateCache();
            _mockDialogActionCache = new InMemoryCache<DialogAction>();
            _mockWebDataCache = new InMemoryCache<CachedWebData>();
            _mockUserProfilestore = new InMemoryProfileStorage();
            _fakeSpeechReco = new FakeSpeechRecognizerFactory(AudioSampleFormat.Mono(16000), true);
            _fakeSpeechSynth = new FakeSpeechSynth(LanguageCode.EN_US);
            _fakeOAuthStore = new FakeOAuthSecretStore();
            _oauthManager = new OAuthManager(
                "https://www.durandal.test",
                _fakeOAuthStore,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                new NullHttpClientFactory());
            _mockPluginLoader = new TestPluginLoader(new BasicDialogExecutor(true));
            _mockPluginProvider = BuildBasicPluginProvider(_mockPluginLoader);

            _defaultDialogParameters = new DialogEngineParameters(DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(_baseConfig)), new WeakPointer<IDurandalPluginProvider>(_mockPluginProvider))
            {
                Logger = _logger,
                ConversationStateCache = new WeakPointer<IConversationStateCache>(_conversationStateCache),
                UserProfileStorage = _mockUserProfilestore,
                DialogActionCache = new WeakPointer<ICache<DialogAction>>(_mockDialogActionCache),
                WebDataCache = new WeakPointer<ICache<CachedWebData>>(_mockWebDataCache)
            };

            _dialogEngine = new DialogProcessingEngine(_defaultDialogParameters);
            var availablePlugins = _mockPluginLoader.GetAllAvailablePlugins(_realTime).Await();
            _dialogEngine.LoadPlugins(availablePlugins, _realTime).Await();
        }

        [TestInitialize]
        public void InitializeTest()
        {
            _conversationStateCache.ClearAllConversationStates();
            _mockDialogActionCache.Clear();
            _mockUserProfilestore.ClearAllProfiles();
            _mockPluginLoader.ResetAllPlugins();
            _fakeSpeechReco.ClearRecoResults();
        }

        private static IDurandalPluginProvider BuildBasicPluginProvider(IDurandalPluginLoader loader)
        {
            return new LocallyRemotedPluginProvider(
                _logger,
                loader,
                new JsonRemoteDialogProtocol(),
                _remotingConfig,
                new WeakPointer<IThreadPool>(new TaskThreadPool()),
                _fakeSpeechSynth,
                _fakeSpeechReco,
                _oauthManager,
                new NullHttpClientFactory(),
                NullFileSystem.Singleton,
                new RoslynLGScriptCompiler(),
                new NLPToolsCollection(),
                new DefaultEntityResolver(new GenericEntityResolver(new NLPToolsCollection())),
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                serverSocketFactory: null,
                clientSocketFactory: null,
                realTime: DefaultRealTimeProvider.Singleton,
                useDebugTimeouts: false);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonNoRecoResults()
        {
            await SharedConversationFlowTests.TestConversationFlowNoRecoResults(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonBasicSuccess()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicSuccess(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonBasicFailure()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicFailure(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonBasicSkip()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicSkip(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonBasicFallthrough()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicFallthrough(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonBasicFallthrough2()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicFallthrough2(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonLowConfidenceSkip()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceSkip(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonLowConfidenceSkip2()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceSkip2(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonLowConfidenceSkip3()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceSkip3(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSkipReturnsMessageFromTop()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipReturnsMessageFromTop(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSkipReturnsMessageFromMiddle()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipReturnsMessageFromMiddle(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSkipReturnsMessageOrderedProperly()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipReturnsMessageOrderedProperly(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSkipResponsesCanMaintainState()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipResponsesCanMaintainState(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonOriginalUtteranceIsPassedThrough()
        {
            await SharedConversationFlowTests.TestConversationFlowOriginalUtteranceIsPassedThrough(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonInvalidSSMLIsCaught()
        {
            await SharedConversationFlowTests.TestConversationFlowInvalidSSMLIsCaught(_defaultDialogParameters, new WeakPointer<IDurandalPluginProvider>(_mockPluginProvider), _realTime);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonExternalPluginMethod()
        {
            await SharedConversationFlowTests.TestConversationFlowExternalPluginMethod(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonAcceptFirstTurnSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowAcceptFirstTurnSideSpeech(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonAcceptFirstTurnSideSpeechHighConf()
        {
            await SharedConversationFlowTests.TestConversationFlowAcceptFirstTurnSideSpeechHighConf(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonIgnoreFirstTurnSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowIgnoreFirstTurnSideSpeech(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonLowConfidenceFirstTurnSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceFirstTurnSideSpeech(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSideSpeechCanTriggerAfterHighConfAnswerSkips()
        {
            await SharedConversationFlowTests.TestConversationFlowSideSpeechCanTriggerAfterHighConfAnswerSkips(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSideSpeechIsIgnoredAfterHighConfAnswerSkips()
        {
            await SharedConversationFlowTests.TestConversationFlowSideSpeechIsIgnoredAfterHighConfAnswerSkips(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonConsumeValidSideSpeechOnSecondTurn()
        {
            await SharedConversationFlowTests.TestConversationFlowConsumeValidSideSpeechOnSecondTurn(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonConsumeValidSideSpeechOnSecondTurnLowConf()
        {
            await SharedConversationFlowTests.TestConversationFlowRemoteBondConsumeValidSideSpeechOnSecondTurnLowConf(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonContinueTenativeConversationAfterHighConfidenceSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowRemoteBondContinueTenativeConversationAfterHighConfidenceSideSpeech(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonEndTenativeConversationAfterHighConfidenceSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowEndTenativeConversationAfterHighConfidenceSideSpeech(_logger, _defaultDialogParameters, _realTime, BuildBasicPluginProvider);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCanHaveMultiturnWithinSideSpeechDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowCanHaveMultiturnWithinSideSpeechDomain(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSecondTurnSideSpeechIsCappedProperly()
        {
            await SharedConversationFlowTests.TestConversationFlowSecondTurnSideSpeechIsCappedProperly(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonHighConfSideSpeechAfterTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowHighConfSideSpeechAfterTenativeMultiturn(_logger, _defaultDialogParameters, _realTime, BuildBasicPluginProvider);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonRegularDomainCanServeAsSideSpeechDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowRegularDomainCanServeAsSideSpeechDomain(_logger, _defaultDialogParameters, _realTime, BuildBasicPluginProvider);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonBasicMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicMultiturn(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonLockedIntoSingleMultiturnDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowLockedIntoSingleMultiturnDomain(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCannotRestartConversationAfterLocking()
        {
            await SharedConversationFlowTests.TestConversationFlowCannotRestartConversationAfterLocking(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCanExitDomainAfterTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowCanExitDomainAfterTenativeMultiturn(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCanContinueDomainAfterTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowCanContinueDomainAfterTenativeMultiturn(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonBasicMultiturnUsingCommonDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicMultiturnUsingCommonDomain(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonBasicMultiturnUsingCommonDomainLowConf()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicMultiturnUsingCommonDomainLowConf(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonIgnoreInvalidCommonResultsOnMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowIgnoreInvalidCommonResultsOnMultiturn(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonIgnoreCommonDomainOnFirstTurn()
        {
            await SharedConversationFlowTests.TestConversationFlowIgnoreCommonDomainOnFirstTurn(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonMultiturnDialogActions()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnDialogActions(_dialogEngine, _mockPluginLoader, _mockDialogActionCache, _logger, _realTime);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonPromiscuousEdgeNegative()
        {
            await SharedConversationFlowTests.TestConversationFlowPromiscuousEdgeNegative(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonPromiscuousEdgePositive()
        {
            await SharedConversationFlowTests.TestConversationFlowPromiscuousEdgePositive(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonExplicitContinuations()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuations(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonExplicitContinuationsWithCommonIntent()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsWithCommonIntent(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonExplicitContinuationsStatic()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsStatic(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonExplicitContinuationsCannotBeAnonymous()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsCannotBeAnonymous(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonExplicitContinuationsCannotBePrivate()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsCannotBePrivate(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainBasic()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainBasic(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainWithParameters()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithParameters(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainWithSlotParameters()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithSlotParameters(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainWithEntityParameters()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithEntityParameters(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainUnsupportedRequest()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainUnsupportedRequest(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainUnsupportedResponse()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainUnsupportedResponse(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainNoTargetIntent()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainNoTargetIntent(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainWithSessionStore()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithSessionStore(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainCanReturnToSuperAfterSubStops()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanReturnToSuperAfterSubStops(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainOneShotInSubDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainOneShotInSubDomain(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainCanUseCommonDomainTransitions()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanUseCommonDomainTransitions(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainCanFinishTwoDomainsAtOnce()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanFinishTwoDomainsAtOnce(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainCanReturnToSuperAfterSubIsTenative()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanReturnToSuperAfterSubIsTenative(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainCanReturnToTenativeSuperAfterSubIsTenative()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanReturnToTenativeSuperAfterSubIsTenative(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainExplicitCallbackWithReturnSlots()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainExplicitCallbackWithReturnSlots(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainExplicitCallbackWithReturnSlotsOneShot()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainExplicitCallbackWithReturnSlotsOneShot(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCrossDomainExplicitCallbackToNonexistentIntent()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainExplicitCallbackToNonexistentIntent(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonBasicRetry()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicRetry(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonRetryWithSkip()
        {
            await SharedConversationFlowTests.TestConversationFlowRetryWithSkip(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonRetryWithFail()
        {
            await SharedConversationFlowTests.TestConversationFlowRetryWithFail(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonExplicitConsumeNoRecoOnSecondTurn()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitConsumeNoRecoOnSecondTurn(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonNorecoDoesNotBreakTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowNorecoDoesNotBreakTenativeMultiturn(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCanRetryWithoutRetryHandler()
        {
            await SharedConversationFlowTests.TestConversationFlowCanRetryWithoutRetryHandler(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonObjectStoreCarriesForward()
        {
            await SharedConversationFlowTests.TestConversationFlowObjectStoreCarriesForward(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonObjectStoreDoesntPersist()
        {
            await SharedConversationFlowTests.TestConversationFlowObjectStoreDoesntPersist(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonPreviousTurnsAreStoredAndPruned()
        {
            await SharedConversationFlowTests.TestConversationFlowPreviousTurnsAreStoredAndPruned(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonEntityHistorySameDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistorySameDomain(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonEntityHistorySameDomainEntitiesExpire()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistorySameDomainEntitiesExpire(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonEntityHistoryOnlyTurnsOncePerRun()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryOnlyTurnsOncePerRun(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonEntityHistoryCrossDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryCrossDomain(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonEntityHistoryCrossDomainEntitiesExpire()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryCrossDomainEntitiesExpire(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonEntityHistoryCrossDomainEntitiesDontExpire()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryCrossDomainEntitiesDontExpire(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonUserProfilePersistsBetweenSessions()
        {
            await SharedConversationFlowTests.TestConversationFlowUserProfilePersistsBetweenSessions(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonUserProfileIsDomainIsolated()
        {
            await SharedConversationFlowTests.TestConversationFlowUserProfileIsDomainIsolated(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonConversationStateIsClearedAfterFinish()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateIsClearedAfterFinish(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader, _conversationStateCache);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonMultiturnOneUserTwoDevices()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnOneUserTwoDevices(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonMultiturnTwoUsersOneDevice()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnTwoUsersOneDevice(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonClientSpecificStatesAreSet()
        {
            await SharedConversationFlowTests.TestConversationFlowClientSpecificStatesAreSet(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonClientSpecificStatesAreUsedAfterClientAction()
        {
            await SharedConversationFlowTests.TestConversationFlowClientSpecificStatesAreUsedAfterClientAction(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonConversationStateRecoversAfterBreakingVersionChange()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateRecoversAfterBreakingVersionChange(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonConversationStateContinuesAfterMajorVersionChange()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateContinuesAfterMajorVersionChange(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonConversationStateContinuesAfterMinorVersionChange()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateContinuesAfterMinorVersionChange(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonConversationStateContinuesAfterMinorVersionChangeSideBySide()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateContinuesAfterMinorVersionChangeSideBySide(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCantLoadTwoPluginsWithSameVersion()
        {
            await SharedConversationFlowTests.TestConversationFlowCantLoadTwoPluginsWithSameVersion(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonMultiturnCloudy()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnCloudy(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonMultiturnTwoDevicesCloudy()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnTwoDevicesCloudy(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonMultiturnDialogActionsCloudy()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnDialogActionsCloudy(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonBasicTrigger()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicTrigger(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonTriggersRunInParallel()
        {
            await SharedConversationFlowTests.TestConversationFlowTriggersRunInParallel(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonMultipleTriggersCauseDisambiguation()
        {
            await SharedConversationFlowTests.TestConversationFlowMultipleTriggersCauseDisambiguation(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonDisambiguationFull()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationFull(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonDisambiguationWithTriggerTimeSessionPreserved()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationWithTriggerTimeSessionPreserved(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonDisambiguationFallsBackWhenReflectionDomainCannotHandle()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationFallsBackWhenReflectionDomainCannotHandle(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonDisambiguationWithinSameDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationWithinSameDomain(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonDisambiguationWithinSameDomainWithSideEffects()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationWithinSameDomainWithSideEffects(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSpeechReco()
        {
            await SharedConversationFlowTests.TestConversationFlowSpeechReco(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader, _fakeSpeechReco);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSpeechSynth()
        {
            await SharedConversationFlowTests.TestConversationFlowSpeechSynth(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonCreateOAuthUri()
        {
            await SharedConversationFlowTests.TestConversationFlowCreateOAuthUri(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonGetOAuthToken()
        {
            await SharedConversationFlowTests.TestConversationFlowGetOAuthToken(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _fakeOAuthStore);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonDeleteOAuthToken()
        {
            await SharedConversationFlowTests.TestConversationFlowDeleteOAuthToken(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _fakeOAuthStore);
        }
    }
}
