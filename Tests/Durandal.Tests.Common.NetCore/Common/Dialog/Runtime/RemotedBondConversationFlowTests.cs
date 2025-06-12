

namespace Durandal.Tests.Common.Dialog.Runtime
{
    using Durandal.Extensions.BondProtocol;
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
    public class RemotedBondConversationFlowTests
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
                new BondRemoteDialogProtocol(),
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
        public async Task TestConversationFlowRemoteBondNoRecoResults()
        {
            await SharedConversationFlowTests.TestConversationFlowNoRecoResults(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondBasicSuccess()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicSuccess(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondBasicFailure()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicFailure(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondBasicSkip()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicSkip(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondBasicFallthrough()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicFallthrough(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondBasicFallthrough2()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicFallthrough2(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondLowConfidenceSkip()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceSkip(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondLowConfidenceSkip2()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceSkip2(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondLowConfidenceSkip3()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceSkip3(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondSkipReturnsMessageFromTop()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipReturnsMessageFromTop(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondSkipReturnsMessageFromMiddle()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipReturnsMessageFromMiddle(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondSkipReturnsMessageOrderedProperly()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipReturnsMessageOrderedProperly(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondSkipResponsesCanMaintainState()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipResponsesCanMaintainState(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondOriginalUtteranceIsPassedThrough()
        {
            await SharedConversationFlowTests.TestConversationFlowOriginalUtteranceIsPassedThrough(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondInvalidSSMLIsCaught()
        {
            await SharedConversationFlowTests.TestConversationFlowInvalidSSMLIsCaught(_defaultDialogParameters, new WeakPointer<IDurandalPluginProvider>(_mockPluginProvider), _realTime);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondExternalPluginMethod()
        {
            await SharedConversationFlowTests.TestConversationFlowExternalPluginMethod(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondAcceptFirstTurnSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowAcceptFirstTurnSideSpeech(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondAcceptFirstTurnSideSpeechHighConf()
        {
            await SharedConversationFlowTests.TestConversationFlowAcceptFirstTurnSideSpeechHighConf(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondIgnoreFirstTurnSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowIgnoreFirstTurnSideSpeech(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondLowConfidenceFirstTurnSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceFirstTurnSideSpeech(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondSideSpeechCanTriggerAfterHighConfAnswerSkips()
        {
            await SharedConversationFlowTests.TestConversationFlowSideSpeechCanTriggerAfterHighConfAnswerSkips(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondSideSpeechIsIgnoredAfterHighConfAnswerSkips()
        {
            await SharedConversationFlowTests.TestConversationFlowSideSpeechIsIgnoredAfterHighConfAnswerSkips(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondConsumeValidSideSpeechOnSecondTurn()
        {
            await SharedConversationFlowTests.TestConversationFlowConsumeValidSideSpeechOnSecondTurn(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondConsumeValidSideSpeechOnSecondTurnLowConf()
        {
            await SharedConversationFlowTests.TestConversationFlowRemoteBondConsumeValidSideSpeechOnSecondTurnLowConf(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondContinueTenativeConversationAfterHighConfidenceSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowRemoteBondContinueTenativeConversationAfterHighConfidenceSideSpeech(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondEndTenativeConversationAfterHighConfidenceSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowEndTenativeConversationAfterHighConfidenceSideSpeech(_logger, _defaultDialogParameters, _realTime, BuildBasicPluginProvider);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCanHaveMultiturnWithinSideSpeechDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowCanHaveMultiturnWithinSideSpeechDomain(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondSecondTurnSideSpeechIsCappedProperly()
        {
            await SharedConversationFlowTests.TestConversationFlowSecondTurnSideSpeechIsCappedProperly(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondHighConfSideSpeechAfterTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowHighConfSideSpeechAfterTenativeMultiturn(_logger, _defaultDialogParameters, _realTime, BuildBasicPluginProvider);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondRegularDomainCanServeAsSideSpeechDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowRegularDomainCanServeAsSideSpeechDomain(_logger, _defaultDialogParameters, _realTime, BuildBasicPluginProvider);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondBasicMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicMultiturn(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondLockedIntoSingleMultiturnDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowLockedIntoSingleMultiturnDomain(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCannotRestartConversationAfterLocking()
        {
            await SharedConversationFlowTests.TestConversationFlowCannotRestartConversationAfterLocking(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCanExitDomainAfterTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowCanExitDomainAfterTenativeMultiturn(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCanContinueDomainAfterTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowCanContinueDomainAfterTenativeMultiturn(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondBasicMultiturnUsingCommonDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicMultiturnUsingCommonDomain(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondBasicMultiturnUsingCommonDomainLowConf()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicMultiturnUsingCommonDomainLowConf(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondIgnoreInvalidCommonResultsOnMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowIgnoreInvalidCommonResultsOnMultiturn(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondIgnoreCommonDomainOnFirstTurn()
        {
            await SharedConversationFlowTests.TestConversationFlowIgnoreCommonDomainOnFirstTurn(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondMultiturnDialogActions()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnDialogActions(_dialogEngine, _mockPluginLoader, _mockDialogActionCache, _logger, _realTime);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondPromiscuousEdgeNegative()
        {
            await SharedConversationFlowTests.TestConversationFlowPromiscuousEdgeNegative(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondPromiscuousEdgePositive()
        {
            await SharedConversationFlowTests.TestConversationFlowPromiscuousEdgePositive(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondExplicitContinuations()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuations(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondExplicitContinuationsWithCommonIntent()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsWithCommonIntent(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondExplicitContinuationsStatic()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsStatic(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondExplicitContinuationsCannotBeAnonymous()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsCannotBeAnonymous(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondExplicitContinuationsCannotBePrivate()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsCannotBePrivate(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainBasic()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainBasic(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainWithParameters()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithParameters(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainWithSlotParameters()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithSlotParameters(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainWithEntityParameters()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithEntityParameters(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainUnsupportedRequest()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainUnsupportedRequest(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainUnsupportedResponse()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainUnsupportedResponse(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainNoTargetIntent()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainNoTargetIntent(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainWithSessionStore()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithSessionStore(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainCanReturnToSuperAfterSubStops()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanReturnToSuperAfterSubStops(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainOneShotInSubDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainOneShotInSubDomain(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainCanUseCommonDomainTransitions()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanUseCommonDomainTransitions(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainCanFinishTwoDomainsAtOnce()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanFinishTwoDomainsAtOnce(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainCanReturnToSuperAfterSubIsTenative()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanReturnToSuperAfterSubIsTenative(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainCanReturnToTenativeSuperAfterSubIsTenative()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanReturnToTenativeSuperAfterSubIsTenative(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainExplicitCallbackWithReturnSlots()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainExplicitCallbackWithReturnSlots(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainExplicitCallbackWithReturnSlotsOneShot()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainExplicitCallbackWithReturnSlotsOneShot(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCrossDomainExplicitCallbackToNonexistentIntent()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainExplicitCallbackToNonexistentIntent(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondBasicRetry()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicRetry(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondRetryWithSkip()
        {
            await SharedConversationFlowTests.TestConversationFlowRetryWithSkip(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondRetryWithFail()
        {
            await SharedConversationFlowTests.TestConversationFlowRetryWithFail(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondExplicitConsumeNoRecoOnSecondTurn()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitConsumeNoRecoOnSecondTurn(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondNorecoDoesNotBreakTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowNorecoDoesNotBreakTenativeMultiturn(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCanRetryWithoutRetryHandler()
        {
            await SharedConversationFlowTests.TestConversationFlowCanRetryWithoutRetryHandler(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondObjectStoreCarriesForward()
        {
            await SharedConversationFlowTests.TestConversationFlowObjectStoreCarriesForward(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondObjectStoreDoesntPersist()
        {
            await SharedConversationFlowTests.TestConversationFlowObjectStoreDoesntPersist(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondPreviousTurnsAreStoredAndPruned()
        {
            await SharedConversationFlowTests.TestConversationFlowPreviousTurnsAreStoredAndPruned(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondEntityHistorySameDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistorySameDomain(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondEntityHistorySameDomainEntitiesExpire()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistorySameDomainEntitiesExpire(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondEntityHistoryOnlyTurnsOncePerRun()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryOnlyTurnsOncePerRun(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondEntityHistoryCrossDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryCrossDomain(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondEntityHistoryCrossDomainEntitiesExpire()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryCrossDomainEntitiesExpire(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondEntityHistoryCrossDomainEntitiesDontExpire()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryCrossDomainEntitiesDontExpire(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondUserProfilePersistsBetweenSessions()
        {
            await SharedConversationFlowTests.TestConversationFlowUserProfilePersistsBetweenSessions(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondUserProfileIsDomainIsolated()
        {
            await SharedConversationFlowTests.TestConversationFlowUserProfileIsDomainIsolated(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondConversationStateIsClearedAfterFinish()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateIsClearedAfterFinish(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader, _conversationStateCache);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondMultiturnOneUserTwoDevices()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnOneUserTwoDevices(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondMultiturnTwoUsersOneDevice()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnTwoUsersOneDevice(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondClientSpecificStatesAreSet()
        {
            await SharedConversationFlowTests.TestConversationFlowClientSpecificStatesAreSet(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondClientSpecificStatesAreUsedAfterClientAction()
        {
            await SharedConversationFlowTests.TestConversationFlowClientSpecificStatesAreUsedAfterClientAction(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondConversationStateRecoversAfterBreakingVersionChange()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateRecoversAfterBreakingVersionChange(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondConversationStateContinuesAfterMajorVersionChange()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateContinuesAfterMajorVersionChange(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondConversationStateContinuesAfterMinorVersionChange()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateContinuesAfterMinorVersionChange(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondConversationStateContinuesAfterMinorVersionChangeSideBySide()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateContinuesAfterMinorVersionChangeSideBySide(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCantLoadTwoPluginsWithSameVersion()
        {
            await SharedConversationFlowTests.TestConversationFlowCantLoadTwoPluginsWithSameVersion(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondMultiturnCloudy()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnCloudy(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondMultiturnTwoDevicesCloudy()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnTwoDevicesCloudy(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondMultiturnDialogActionsCloudy()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnDialogActionsCloudy(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondBasicTrigger()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicTrigger(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondTriggersRunInParallel()
        {
            await SharedConversationFlowTests.TestConversationFlowTriggersRunInParallel(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondMultipleTriggersCauseDisambiguation()
        {
            await SharedConversationFlowTests.TestConversationFlowMultipleTriggersCauseDisambiguation(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondDisambiguationFull()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationFull(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondDisambiguationWithTriggerTimeSessionPreserved()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationWithTriggerTimeSessionPreserved(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondDisambiguationFallsBackWhenReflectionDomainCannotHandle()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationFallsBackWhenReflectionDomainCannotHandle(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondDisambiguationWithinSameDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationWithinSameDomain(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondDisambiguationWithinSameDomainWithSideEffects()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationWithinSameDomainWithSideEffects(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondSpeechReco()
        {
            await SharedConversationFlowTests.TestConversationFlowSpeechReco(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader, _fakeSpeechReco);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondSpeechSynth()
        {
            await SharedConversationFlowTests.TestConversationFlowSpeechSynth(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondCreateOAuthUri()
        {
            await SharedConversationFlowTests.TestConversationFlowCreateOAuthUri(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondGetOAuthToken()
        {
            await SharedConversationFlowTests.TestConversationFlowGetOAuthToken(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _fakeOAuthStore);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteBondDeleteOAuthToken()
        {
            await SharedConversationFlowTests.TestConversationFlowDeleteOAuthToken(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _fakeOAuthStore);
        }
    }
}
