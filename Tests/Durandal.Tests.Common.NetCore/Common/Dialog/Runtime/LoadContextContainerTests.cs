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
    using Durandal.Common.Audio;
    using System.IO;
    using Durandal.Common.Instrumentation;
    using System.Threading;
    using Durandal.Common.Net;
    using Durandal.Tests.Common.Dialog.Runtime;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;

    [TestClass]
    [DeploymentItem("TestData\\UnitTestPlugin.dupkg")]
    public class LoadContextContainerTests
    {
        private static DirectoryInfo _containerEnvironmentDir;
        private static IRealTimeProvider _realTime;
        private static InMemoryCache<DialogAction> _mockDialogActionCache;
        private static InMemoryCache<CachedWebData> _mockWebDataCache;
        private static InMemoryConversationStateCache _conversationStateCache;
        private static InMemoryProfileStorage _mockUserProfilestore;
        private static FakeSpeechRecognizerFactory _fakeSpeechReco;
        private static FakeSpeechSynth _fakeSpeechSynth;
        private static InMemoryOAuthSecretStore _fakeOAuthStore;
        private static OAuthManager _oauthManager;
        private static LoadContextIsolatedPluginProvider _mockPluginProvider;
        private static ILogger _logger;
        private static IConfiguration _baseConfig;
        private static RemotingConfiguration _remotingConfig;
        private static DialogProcessingEngine _dialogEngine;
        private static DialogEngineParameters _defaultDialogParameters;
        private static MetricCollector _metrics;
        private static FakeMetricOutput _metricOutput;

        [ClassInitialize]
        public static void InitializeClass(TestContext context)
        {
            _logger = new ConsoleLogger("Main", LogLevel.Err | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            _metrics = new MetricCollector(_logger.Clone("Metrics"), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200), DefaultRealTimeProvider.Singleton);
            _metricOutput = new FakeMetricOutput();
            _metrics.AddMetricOutput(_metricOutput);
            _baseConfig = new InMemoryConfiguration(_logger.Clone("Config"));
            _remotingConfig = DialogTestHelpers.GetTestRemotingConfiguration(new WeakPointer<IConfiguration>(_baseConfig));
            _remotingConfig.KeepAlivePingInterval = TimeSpan.FromMilliseconds(100);
            _remotingConfig.KeepAliveFailureThreshold = 0.1f;
            _remotingConfig.KeepAlivePingTimeout = TimeSpan.FromMilliseconds(100);
            _remotingConfig.IpcProtocol = "bond";
            _realTime = DefaultRealTimeProvider.Singleton;
            _conversationStateCache = new InMemoryConversationStateCache();
            _mockDialogActionCache = new InMemoryCache<DialogAction>();
            _mockWebDataCache = new InMemoryCache<CachedWebData>();
            _mockUserProfilestore = new InMemoryProfileStorage();
            _fakeSpeechReco = new FakeSpeechRecognizerFactory(AudioSampleFormat.Mono(16000), true);
            _fakeSpeechSynth = new FakeSpeechSynth(LanguageCode.Parse("en-US"));
            _fakeOAuthStore = new InMemoryOAuthSecretStore();
            _oauthManager = new OAuthManager(
                "https://www.durandal.test",
                _fakeOAuthStore,
                new WeakPointer<IMetricCollector>(_metrics),
                DimensionSet.Empty,
                new DirectHttpClientFactory(new MockOAuthTokenServer()));

            bool useDebugTimeouts = false;

            // If you are trying to step into container code using the debugger while running
            // one of these unit tests, uncomment this line. Otherwise timeouts will be really long and
            // might mess up some other stuff
            //useDebugTimeouts = Debugger.IsAttached;

            string durandalRootDirectory = null;

            if (string.IsNullOrEmpty(durandalRootDirectory))
            {
                durandalRootDirectory = context.Properties["DurandalRootDirectory"]?.ToString();
                if (!string.IsNullOrWhiteSpace(durandalRootDirectory))
                {
                    _logger.Log("Found base directory in test run settings: \"" + durandalRootDirectory + "\"");
                }
            }

            if (string.IsNullOrEmpty(durandalRootDirectory))
            {
                string rootEnv = Environment.GetEnvironmentVariable("DURANDAL_ROOT");
                if (rootEnv != null)
                {
                    durandalRootDirectory = rootEnv;
                    if (!string.IsNullOrWhiteSpace(durandalRootDirectory))
                    {
                        _logger.Log("Found base directory in environment variable: \"" + durandalRootDirectory + "\"");
                    }
                }
            }

            if (string.IsNullOrEmpty(durandalRootDirectory))
            {
                durandalRootDirectory = Environment.CurrentDirectory;
                _logger.Log("Using default base directory \"" + durandalRootDirectory +
                    "\". This is not recommended; please specify base directory using either \"DurandalRootDirectory\" test run setting or DURANDAL_ROOT environment variable", LogLevel.Wrn);
            }

            // This will run the test in the current system-wide durandal root environment, or an alternative
            // test environment if a different path was specified in test config
            _containerEnvironmentDir = new DirectoryInfo(durandalRootDirectory);
            DirectoryInfo packageDir = _containerEnvironmentDir.CreateSubdirectory(RuntimeDirectoryName.PACKAGE_DIR);
            FileInfo duPkgFile = new FileInfo("UnitTestPlugin.dupkg");
            duPkgFile.CopyTo(packageDir.FullName + "\\" + duPkgFile.Name, true);
            RealFileSystem virtualEnvironmentFileSystem = new RealFileSystem(_logger.Clone("GlobalFileSystem"), _containerEnvironmentDir.FullName);

            _mockPluginProvider = LoadContextIsolatedPluginProvider.Create(
                _logger.Clone("ContainerizedPluginProvider"),
                virtualEnvironmentFileSystem,
                new DirectoryInfo(durandalRootDirectory),
                new NullHttpClientFactory(),
                new DirectoryInfo("unit_test_appdomain_containers"),
                new DefaultEntityResolver(new GenericEntityResolver(new NLPToolsCollection())),
                _fakeSpeechSynth,
                _fakeSpeechReco,
                _oauthManager,
                _realTime,
                new BondRemoteDialogProtocol(),
                _metrics,
                DimensionSet.Empty,
                new MMIOServerSocketFactory(),
                _remotingConfig,
                useDebugTimeouts).Await();

            _defaultDialogParameters = new DialogEngineParameters(DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(_baseConfig)), new WeakPointer<IDurandalPluginProvider>(_mockPluginProvider))
            {
                Logger = _logger,
                ConversationStateCache = new WeakPointer<IConversationStateCache>(_conversationStateCache),
                UserProfileStorage = _mockUserProfilestore,
                DialogActionCache = new WeakPointer<ICache<DialogAction>>(_mockDialogActionCache),
                WebDataCache = new WeakPointer<ICache<CachedWebData>>(_mockWebDataCache)
            };

            _dialogEngine = new DialogProcessingEngine(_defaultDialogParameters);
            _dialogEngine.LoadPlugins(new List<PluginStrongName>() { new PluginStrongName("unit_test", 1, 0) }, _realTime).Await();
        }

        [ClassCleanup]
        public static void CleanupClass()
        {
            //_containerEnvironmentDir.Delete(true);
            _dialogEngine?.Dispose();
        }

        [TestInitialize]
        public void InitializeTest()
        {
            _conversationStateCache.ClearAllConversationStates();
            _mockDialogActionCache.Clear();
            _mockUserProfilestore.ClearAllProfiles();
            _fakeSpeechReco.ClearRecoResults();
            _fakeOAuthStore.Clear();
        }

        /// <summary>
        /// Tests that we can load a package into an appdomain isolated container and have it execute successfully
        /// </summary>
        [Ignore]
        [TestMethod]
        public async Task TestLoadContextContainerPluginBasicSuccess()
        {
            if (!_mockPluginProvider.HasDevRuntimeAvailable("netcore"))
            {
                Assert.Inconclusive("Dev runtime not found for loadcontext container. Will not run test in case the runtime version is out of date.");
                return;
            }

            DialogEngineResponse response = await _dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("unit_test", "basic_test", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("The test passed", response.DisplayedText);
        }

        [Ignore]
        [TestMethod]
        public async Task TestLoadContextContainerPluginOAuthService()
        {
            if (!_mockPluginProvider.HasDevRuntimeAvailable("netcore"))
            {
                Assert.Inconclusive("Dev runtime not found for process-level container. Will not run test in case the runtime version is out of date.");
                return;
            }

            DialogEngineResponse response = await _dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("unit_test", "oauth_1", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.IsFalse(string.IsNullOrEmpty(response.DisplayedText));
            string oauthUrl = response.DisplayedText;
            HttpRequest getUrlParser = HttpRequest.CreateOutgoing(oauthUrl);
            string callbackUrl = getUrlParser.GetParameters["redirect_uri"];
            string state = getUrlParser.GetParameters["state"];
            // Fetch the mock token on the backend
            HttpRequest fakeCallback = HttpRequest.CreateOutgoing(callbackUrl);
            fakeCallback.GetParameters["state"] = state;
            fakeCallback.GetParameters["code"] = "12345";
            await _oauthManager.HandleThirdPartyAuthCodeCallback(fakeCallback, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);

            response = await _dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("unit_test", "oauth_2", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);

            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("The oauth test passed", response.DisplayedText);
        }

        [Ignore]
        [TestMethod]
        public async Task TestLoadContextContainerPluginViewData()
        {
            if (!_mockPluginProvider.HasDevRuntimeAvailable("netcore"))
            {
                Assert.Inconclusive("Dev runtime not found for process-level container. Will not run test in case the runtime version is out of date.");
                return;
            }

            DialogEngineResponse response = await _dialogEngine.Process(
                RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("unit_test", "view_data", 1.0f)),
                DialogTestHelpers.GetTestClientContextTextQuery(),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                InputMethod.Typed);
            
            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("this is sample view data", response.PresentationHtml);
            Assert.AreEqual("/views/unit_test/sample_view.html", response.ActionURL);
            CachedWebData webData = await _dialogEngine.FetchPluginViewData("unit_test", "/sample_view.html", null, _logger).ConfigureAwait(false);
            Assert.IsNotNull(webData);
            Assert.IsNotNull(webData.Data);
            string responseHtml = Encoding.UTF8.GetString(webData.Data.Array, webData.Data.Offset, webData.Data.Count);
            Assert.AreEqual("this is sample view data", responseHtml);
        }

        /// <summary>
        /// Tests that keepalive is properly measured in appdomain containers
        /// </summary>
        [Ignore]
        [TestMethod]
        public async Task TestLoadContextContainerKeepAlive()
        {
            if (!_mockPluginProvider.HasDevRuntimeAvailable("netcore"))
            {
                Assert.Inconclusive("Dev runtime not found for process-level container. Will not run test in case the runtime version is out of date.");
                return;
            }

            // Wait for keepalive metrics to start
            Stopwatch testTimer = Stopwatch.StartNew();
            TimeSpan testTimeout = TimeSpan.FromSeconds(5);

            bool testHasTimedOut = false;
            bool metricsPresent = false;
            while (!metricsPresent && !testHasTimedOut)
            {
                await Task.Delay(50);
                testHasTimedOut = testTimer.Elapsed > testTimeout;
                metricsPresent = _metricOutput.MetricHasValue(CommonInstrumentation.Key_Counter_KeepAlive_RoundTripTime + "_p0.99");
            }

            Assert.IsTrue(metricsPresent);
            Assert.IsFalse(testHasTimedOut);
        }

        /// <summary>
        /// Tests that we can crash an appdomain container, continue sending traffic to it, and watch it autorecover
        /// as the watchdogs trigger the app domain to recycle internally.
        /// </summary>
        [Ignore]
        [TestMethod]
        public async Task TestLoadContextContainerRecoversAfterCrash()
        {
            if (!_mockPluginProvider.HasDevRuntimeAvailable("netcore"))
            {
                Assert.Inconclusive("Dev runtime not found for process-level container. Will not run test in case the runtime version is out of date.");
                return;
            }

#if DEBUG
            Stopwatch testTimeoutTimer = Stopwatch.StartNew();
            TimeSpan testTimeout = TimeSpan.FromSeconds(60);
            DimensionSet qosMetricDimensions = new DimensionSet(new MetricDimension[]
            {
                new MetricDimension("Container", "UnitTestPlugin"),
            });

            _logger.Log("Crashing container");
            // This signal is only acknowledged by debug builds of the code, for what should be obvious reasons
            await _mockPluginProvider._UnitTesting_CrashContainer(new PluginStrongName("unit_test", 1, 0), _realTime);

            _logger.Log("Waiting for container QoS to fall...");
            double? qos = 1.0;
            while (qos.GetValueOrDefault(1.0) >= 0.95 && testTimeoutTimer.Elapsed < testTimeout)
            {
                qos = _metricOutput.GetLatestMetricValue(CommonInstrumentation.Key_Counter_KeepAlive_QualityOfService, qosMetricDimensions);
                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }

            Assert.IsTrue(testTimeoutTimer.Elapsed < testTimeout, "Test timed out while waiting for container QoS to fall. Apparently the container didn't crash properly");

            // Now keep sending requests to it until the container recovers
            _logger.Log("Container has crashed; waiting for it to recover...");
            DialogEngineResponse response = null;
            while (response == null && testTimeoutTimer.Elapsed < testTimeout)
            {
                try
                {
                    Task<DialogEngineResponse> execTask = _dialogEngine.Process(
                        RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("unit_test", "basic_test", 1.0f)),
                        DialogTestHelpers.GetTestClientContextTextQuery(),
                        ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                        InputMethod.Typed);
                    Task delayTask = Task.Delay(3000);
                    Task winnerTask = await Task.WhenAny(delayTask, execTask).ConfigureAwait(false);
                    if (winnerTask == execTask)
                    {
                        response = await execTask.ConfigureAwait(false);
                    }

                    qos = _metricOutput.GetLatestMetricValue(CommonInstrumentation.Key_Counter_KeepAlive_QualityOfService, qosMetricDimensions);
                    _logger.Log("QOS is " + qos);
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Wrn);
                }
            }

            Assert.IsTrue(testTimeoutTimer.Elapsed < testTimeout, "Test timed out while waiting for container to recover");
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ResponseCode);
            Assert.AreEqual("The test passed", response.DisplayedText);
#else
            await DurandalTaskExtensions.NoOpTask;
            Assert.Inconclusive("This test must be run in debug builds of the code");
#endif
        }

        private class MockOAuthTokenServer : IHttpServerDelegate
        {
            public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse resp = await HandleConnectionInternal(serverContext.HttpRequest, cancelToken, realTime).ConfigureAwait(false);
                if (resp != null)
                {
                    try
                    {
                        await serverContext.WritePrimaryResponse(resp, _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                }
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This method returns an IDisposable so the caller should be responsible for disposal")]
            private async Task<HttpResponse> HandleConnectionInternal(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                HttpResponse response = HttpResponse.OKResponse();
                response.SetContent("{ \"access_token\": \"777\", \"expires_in\": 3600, \"token_type\": \"bearer\", \"scope\": \"read\" }", "application/json");
                return response;
            }
        }
    }
}
