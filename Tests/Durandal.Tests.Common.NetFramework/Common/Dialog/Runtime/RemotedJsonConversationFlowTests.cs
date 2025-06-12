

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
                new CodeDomLGScriptCompiler(),
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
        public async Task TestConversationFlowRemoteJsonSandboxTimeoutProdConfig()
        {
            await SharedConversationFlowTests.TestConversationFlowSandboxTimeoutProdConfig(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSandboxExceptionProdConfig()
        {
            await SharedConversationFlowTests.TestConversationFlowSandboxExceptionProdConfig(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSandboxFailProdConfig()
        {
            await SharedConversationFlowTests.TestConversationFlowSandboxFailProdConfig(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSandboxTimeoutDevConfig()
        {
            await SharedConversationFlowTests.TestConversationFlowSandboxTimeoutDevConfig(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSandboxExceptionDevConfig()
        {
            await SharedConversationFlowTests.TestConversationFlowSandboxExceptionDevConfig(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSandboxDebuggabilityDevConfig()
        {
            await SharedConversationFlowTests.TestConversationFlowSandboxDebuggabilityDevConfig(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }

        [TestMethod]
        public async Task TestConversationFlowRemoteJsonSandboxFailDevConfig()
        {
            await SharedConversationFlowTests.TestConversationFlowSandboxFailDevConfig(_logger, BuildBasicPluginProvider, _defaultDialogParameters, _realTime, _mockPluginLoader);
        }
    }
}
