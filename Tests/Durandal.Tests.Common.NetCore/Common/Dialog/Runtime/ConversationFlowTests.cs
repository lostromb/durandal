

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
    using Durandal.Common.Audio;
using Durandal.Tests.Common.Dialog.Runtime;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.ServiceMgmt;

    [TestClass]
    public class ConversationFlowTests
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
        private static DialogProcessingEngine _dialogEngine;
        private static DialogEngineParameters _defaultDialogParameters;

        [ClassInitialize]
        public static void InitializeClass(TestContext context)
        {
            _logger = new ConsoleLogger("Main", LogLevel.Err | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            _baseConfig = new InMemoryConfiguration(_logger.Clone("Config"));
            _realTime = DefaultRealTimeProvider.Singleton;
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
            return new MachineLocalPluginProvider(
                _logger,
                loader,
                NullFileSystem.Singleton,
                new NLPToolsCollection(),
                new DefaultEntityResolver(new GenericEntityResolver(new NLPToolsCollection())),
                _fakeSpeechSynth,
                _fakeSpeechReco,
                _oauthManager,
                new NullHttpClientFactory(),
                null
                );
        }

        [TestMethod]
        public async Task TestConversationFlowNoRecoResults()
        {
            await SharedConversationFlowTests.TestConversationFlowNoRecoResults(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowBasicSuccess()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicSuccess(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowBasicFailure()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicFailure(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowBasicSkip()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicSkip(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowBasicFallthrough()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicFallthrough(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowBasicFallthrough2()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicFallthrough2(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowLowConfidenceSkip()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceSkip(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowLowConfidenceSkip2()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceSkip2(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowLowConfidenceSkip3()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceSkip3(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowSkipReturnsMessageFromTop()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipReturnsMessageFromTop(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowSkipReturnsMessageFromMiddle()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipReturnsMessageFromMiddle(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowSkipReturnsMessageOrderedProperly()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipReturnsMessageOrderedProperly(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowSkipResponsesCanMaintainState()
        {
            await SharedConversationFlowTests.TestConversationFlowSkipResponsesCanMaintainState(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowOriginalUtteranceIsPassedThrough()
        {
            await SharedConversationFlowTests.TestConversationFlowOriginalUtteranceIsPassedThrough(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowInvalidSSMLIsCaught()
        {
            await SharedConversationFlowTests.TestConversationFlowInvalidSSMLIsCaught(_defaultDialogParameters, new WeakPointer<IDurandalPluginProvider>(_mockPluginProvider), _realTime);
        }

        [TestMethod]
        public async Task TestConversationFlowExternalPluginMethod()
        {
            await SharedConversationFlowTests.TestConversationFlowExternalPluginMethod(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowAcceptFirstTurnSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowAcceptFirstTurnSideSpeech(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowAcceptFirstTurnSideSpeechHighConf()
        {
            await SharedConversationFlowTests.TestConversationFlowAcceptFirstTurnSideSpeechHighConf(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowIgnoreFirstTurnSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowIgnoreFirstTurnSideSpeech(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowLowConfidenceFirstTurnSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowLowConfidenceFirstTurnSideSpeech(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowSideSpeechCanTriggerAfterHighConfAnswerSkips()
        {
            await SharedConversationFlowTests.TestConversationFlowSideSpeechCanTriggerAfterHighConfAnswerSkips(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowSideSpeechIsIgnoredAfterHighConfAnswerSkips()
        {
            await SharedConversationFlowTests.TestConversationFlowSideSpeechIsIgnoredAfterHighConfAnswerSkips(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowConsumeValidSideSpeechOnSecondTurn()
        {
            await SharedConversationFlowTests.TestConversationFlowConsumeValidSideSpeechOnSecondTurn(_dialogEngine, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowConsumeValidSideSpeechOnSecondTurnLowConf()
        {
            await SharedConversationFlowTests.TestConversationFlowRemoteBondConsumeValidSideSpeechOnSecondTurnLowConf(_dialogEngine, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowContinueTenativeConversationAfterHighConfidenceSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowRemoteBondContinueTenativeConversationAfterHighConfidenceSideSpeech(_dialogEngine, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowEndTenativeConversationAfterHighConfidenceSideSpeech()
        {
            await SharedConversationFlowTests.TestConversationFlowEndTenativeConversationAfterHighConfidenceSideSpeech(_logger, _defaultDialogParameters, _realTime, BuildBasicPluginProvider);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCanHaveMultiturnWithinSideSpeechDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowCanHaveMultiturnWithinSideSpeechDomain(_logger, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowSecondTurnSideSpeechIsCappedProperly()
        {
            await SharedConversationFlowTests.TestConversationFlowSecondTurnSideSpeechIsCappedProperly(_dialogEngine, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowHighConfSideSpeechAfterTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowHighConfSideSpeechAfterTenativeMultiturn(_logger, _defaultDialogParameters, _realTime, BuildBasicPluginProvider);
        }
        
        [TestMethod]
        public async Task TestConversationFlowRegularDomainCanServeAsSideSpeechDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowRegularDomainCanServeAsSideSpeechDomain(_logger, _defaultDialogParameters, _realTime, BuildBasicPluginProvider);
        }
        
        [TestMethod]
        public async Task TestConversationFlowBasicMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicMultiturn(_dialogEngine, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowLockedIntoSingleMultiturnDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowLockedIntoSingleMultiturnDomain(_dialogEngine, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCannotRestartConversationAfterLocking()
        {
            await SharedConversationFlowTests.TestConversationFlowCannotRestartConversationAfterLocking(_dialogEngine, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowCanExitDomainAfterTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowCanExitDomainAfterTenativeMultiturn(_dialogEngine, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCanContinueDomainAfterTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowCanContinueDomainAfterTenativeMultiturn(_dialogEngine, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowBasicMultiturnUsingCommonDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicMultiturnUsingCommonDomain(_dialogEngine, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowBasicMultiturnUsingCommonDomainLowConf()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicMultiturnUsingCommonDomainLowConf(_dialogEngine, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowIgnoreInvalidCommonResultsOnMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowIgnoreInvalidCommonResultsOnMultiturn(_dialogEngine, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowIgnoreCommonDomainOnFirstTurn()
        {
            await SharedConversationFlowTests.TestConversationFlowIgnoreCommonDomainOnFirstTurn(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowMultiturnDialogActions()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnDialogActions(_dialogEngine, _mockPluginLoader, _mockDialogActionCache, _logger, _realTime);
        }
        
        [TestMethod]
        public async Task TestConversationFlowPromiscuousEdgeNegative()
        {
            await SharedConversationFlowTests.TestConversationFlowPromiscuousEdgeNegative(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowPromiscuousEdgePositive()
        {
            await SharedConversationFlowTests.TestConversationFlowPromiscuousEdgePositive(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowExplicitContinuations()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuations(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowExplicitContinuationsWithCommonIntent()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsWithCommonIntent(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowExplicitContinuationsStatic()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsStatic(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowExplicitContinuationsCannotBeAnonymous()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsCannotBeAnonymous(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowExplicitContinuationsCannotBePrivate()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitContinuationsCannotBePrivate(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainBasic()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainBasic(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainWithParameters()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithParameters(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainWithSlotParameters()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithSlotParameters(_dialogEngine);
        }

        [TestMethod]
        public async Task TestConversationFlowCrossDomainWithEntityParameters()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithEntityParameters(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainUnsupportedRequest()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainUnsupportedRequest(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainUnsupportedResponse()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainUnsupportedResponse(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainNoTargetIntent()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainNoTargetIntent(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainWithSessionStore()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainWithSessionStore(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainCanReturnToSuperAfterSubStops()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanReturnToSuperAfterSubStops(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainOneShotInSubDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainOneShotInSubDomain(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainCanUseCommonDomainTransitions()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanUseCommonDomainTransitions(_dialogEngine);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainCanFinishTwoDomainsAtOnce()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanFinishTwoDomainsAtOnce(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainCanReturnToSuperAfterSubIsTenative()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanReturnToSuperAfterSubIsTenative(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainCanReturnToTenativeSuperAfterSubIsTenative()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainCanReturnToTenativeSuperAfterSubIsTenative(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainExplicitCallbackWithReturnSlots()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainExplicitCallbackWithReturnSlots(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainExplicitCallbackWithReturnSlotsOneShot()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainExplicitCallbackWithReturnSlotsOneShot(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCrossDomainExplicitCallbackToNonexistentIntent()
        {
            await SharedConversationFlowTests.TestConversationFlowCrossDomainExplicitCallbackToNonexistentIntent(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowBasicRetry()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicRetry(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowRetryWithSkip()
        {
            await SharedConversationFlowTests.TestConversationFlowRetryWithSkip(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowRetryWithFail()
        {
            await SharedConversationFlowTests.TestConversationFlowRetryWithFail(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowExplicitConsumeNoRecoOnSecondTurn()
        {
            await SharedConversationFlowTests.TestConversationFlowExplicitConsumeNoRecoOnSecondTurn(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowNorecoDoesNotBreakTenativeMultiturn()
        {
            await SharedConversationFlowTests.TestConversationFlowNorecoDoesNotBreakTenativeMultiturn(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowCanRetryWithoutRetryHandler()
        {
            await SharedConversationFlowTests.TestConversationFlowCanRetryWithoutRetryHandler(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowObjectStoreCarriesForward()
        {
            await SharedConversationFlowTests.TestConversationFlowObjectStoreCarriesForward(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowObjectStoreDoesntPersist()
        {
            await SharedConversationFlowTests.TestConversationFlowObjectStoreDoesntPersist(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowPreviousTurnsAreStoredAndPruned()
        {
            await SharedConversationFlowTests.TestConversationFlowPreviousTurnsAreStoredAndPruned(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowEntityHistorySameDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistorySameDomain(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowEntityHistorySameDomainEntitiesExpire()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistorySameDomainEntitiesExpire(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowEntityHistoryOnlyTurnsOncePerRun()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryOnlyTurnsOncePerRun(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowEntityHistoryCrossDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryCrossDomain(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowEntityHistoryCrossDomainEntitiesExpire()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryCrossDomainEntitiesExpire(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowEntityHistoryCrossDomainEntitiesDontExpire()
        {
            await SharedConversationFlowTests.TestConversationFlowEntityHistoryCrossDomainEntitiesDontExpire(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowUserProfilePersistsBetweenSessions()
        {
            await SharedConversationFlowTests.TestConversationFlowUserProfilePersistsBetweenSessions(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowUserProfileIsDomainIsolated()
        {
            await SharedConversationFlowTests.TestConversationFlowUserProfileIsDomainIsolated(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowConversationStateIsClearedAfterFinish()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateIsClearedAfterFinish(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader, _conversationStateCache);
        }
        
        [TestMethod]
        public async Task TestConversationFlowMultiturnOneUserTwoDevices()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnOneUserTwoDevices(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowMultiturnTwoUsersOneDevice()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnTwoUsersOneDevice(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowClientSpecificStatesAreSet()
        {
            await SharedConversationFlowTests.TestConversationFlowClientSpecificStatesAreSet(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowClientSpecificStatesAreUsedAfterClientAction()
        {
            await SharedConversationFlowTests.TestConversationFlowClientSpecificStatesAreUsedAfterClientAction(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowConversationStateRecoversAfterBreakingVersionChange()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateRecoversAfterBreakingVersionChange(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowConversationStateContinuesAfterMajorVersionChange()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateContinuesAfterMajorVersionChange(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowConversationStateContinuesAfterMinorVersionChange()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateContinuesAfterMinorVersionChange(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowConversationStateContinuesAfterMinorVersionChangeSideBySide()
        {
            await SharedConversationFlowTests.TestConversationFlowConversationStateContinuesAfterMinorVersionChangeSideBySide(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowCantLoadTwoPluginsWithSameVersion()
        {
            await SharedConversationFlowTests.TestConversationFlowCantLoadTwoPluginsWithSameVersion(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowMultiturnCloudy()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnCloudy(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowMultiturnTwoDevicesCloudy()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnTwoDevicesCloudy(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowMultiturnDialogActionsCloudy()
        {
            await SharedConversationFlowTests.TestConversationFlowMultiturnDialogActionsCloudy(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowBasicTrigger()
        {
            await SharedConversationFlowTests.TestConversationFlowBasicTrigger(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowTriggersRunInParallel()
        {
            await SharedConversationFlowTests.TestConversationFlowTriggersRunInParallel(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowMultipleTriggersCauseDisambiguation()
        {
            await SharedConversationFlowTests.TestConversationFlowMultipleTriggersCauseDisambiguation(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowDisambiguationFull()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationFull(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowDisambiguationWithTriggerTimeSessionPreserved()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationWithTriggerTimeSessionPreserved(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowDisambiguationFallsBackWhenReflectionDomainCannotHandle()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationFallsBackWhenReflectionDomainCannotHandle(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowDisambiguationWithinSameDomain()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationWithinSameDomain(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowDisambiguationWithinSameDomainWithSideEffects()
        {
            await SharedConversationFlowTests.TestConversationFlowDisambiguationWithinSameDomainWithSideEffects(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
        
        [TestMethod]
        public async Task TestConversationFlowSpeechReco()
        {
            await SharedConversationFlowTests.TestConversationFlowSpeechReco(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader, _fakeSpeechReco);
        }

        [TestMethod]
        public async Task TestConversationFlowSpeechSynth()
        {
            await SharedConversationFlowTests.TestConversationFlowSpeechSynth(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowCreateOAuthUri()
        {
            await SharedConversationFlowTests.TestConversationFlowCreateOAuthUri(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowGetOAuthToken()
        {
            await SharedConversationFlowTests.TestConversationFlowGetOAuthToken(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _fakeOAuthStore);
        }

        [TestMethod]
        public async Task TestConversationFlowDeleteOAuthToken()
        {
            await SharedConversationFlowTests.TestConversationFlowDeleteOAuthToken(_logger, _dialogEngine, _defaultDialogParameters, _realTime, _fakeOAuthStore);
        }
    }
}
