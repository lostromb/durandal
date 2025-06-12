namespace Durandal.Tests.Common.Dialog.Runtime
{
    using Durandal;
    using Durandal.API;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Test;
    using Durandal.Common.Time;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Shared implementations of test logic used by <see cref="ConversationFlowTests"/>, <see cref="RemotedBondConversationFlowTests\"/>, and <see cref="RemotedJsonConversationFlowTests\"/>.
    /// </summary>
    public static class SharedConversationFlowTests
    {
        #region Sandbox environment tests

        public static async Task TestConversationFlowSandboxTimeoutProdConfig(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            TestPluginLoader newPluginProvider = new TestPluginLoader(new SandboxedDialogExecutor(1000, false));
            logger = new ConsoleLogger("Main", LogLevel.Err | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);

            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(newPluginProvider))
            {
                modifiedParams.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
                await dialogEngine.LoadPlugin("sandbox", realTime);

                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("sandbox", "timeout", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Failure, response.ResponseCode);
                Assert.IsNotNull(response.ErrorMessage);
                Assert.IsTrue(response.ErrorMessage.Contains("violated its SLA"));
            }
        }

        public static async Task TestConversationFlowSandboxExceptionProdConfig(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            TestPluginLoader newPluginProvider = new TestPluginLoader(new SandboxedDialogExecutor(30000, false));
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;

            using (IDurandalPluginProvider provider = buildBasicPluginProvider(newPluginProvider))
            {
                modifiedParams.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
                await dialogEngine.LoadPlugin("sandbox", realTime);

                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("sandbox", "exception", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Failure, response.ResponseCode);
                Assert.IsNotNull(response.ErrorMessage);
                Assert.IsTrue(response.ErrorMessage.Contains("unhandled exception"));
            }
        }

        public static async Task TestConversationFlowSandboxFailProdConfig(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            TestPluginLoader newPluginProvider = new TestPluginLoader(new SandboxedDialogExecutor(30000, false));
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(newPluginProvider))
            {
                modifiedParams.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
                await dialogEngine.LoadPlugin("sandbox", realTime);

                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("sandbox", "fail", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Failure, response.ResponseCode);
            }
        }

        public static async Task TestConversationFlowSandboxTimeoutDevConfig(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            TestPluginLoader newPluginProvider = new TestPluginLoader(new BasicDialogExecutor(true));
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(newPluginProvider))
            {
                modifiedParams.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
                await dialogEngine.LoadPlugin("sandbox", realTime);

                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("sandbox", "timeout_small", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Success, response.ResponseCode);
            }
        }

        public static async Task TestConversationFlowSandboxExceptionDevConfig(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            TestPluginLoader newPluginProvider = new TestPluginLoader(new BasicDialogExecutor(false));
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(newPluginProvider))
            {
                modifiedParams.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
                await dialogEngine.LoadPlugin("sandbox", realTime);

                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("sandbox", "exception", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Failure, response.ResponseCode);
                Assert.IsNotNull(response.ErrorMessage);
                Assert.IsTrue(response.ErrorMessage.Contains("unhandled exception"));
            }
        }

        public static async Task TestConversationFlowSandboxDebuggabilityDevConfig(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            try
            {
                TestPluginLoader newPluginProvider = new TestPluginLoader(new BasicDialogExecutor(true));
                InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
                DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
                DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
                modifiedParams.Configuration = testConfig;
                using (IDurandalPluginProvider provider = new MachineLocalPluginProvider(
                    logger,
                    newPluginProvider,
                    NullFileSystem.Singleton,
                    new NLPToolsCollection(),
                   new DefaultEntityResolver(new GenericEntityResolver(new NLPToolsCollection())),
                    new NullSpeechSynth(),
                    NullSpeechRecoFactory.Singleton,
                    new Durandal.Common.Security.OAuth.OAuthManager(
                        "https://null",
                        new FakeOAuthSecretStore(),
                        NullMetricCollector.WeakSingleton,
                        DimensionSet.Empty),
                    new NullHttpClientFactory(),
                    null))
                {
                    modifiedParams.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                    DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
                    await dialogEngine.LoadPlugin("sandbox", realTime);

                    DialogEngineResponse response = await dialogEngine.Process(
                        RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("sandbox", "exception", 1.0f)),
                        DialogTestHelpers.GetTestClientContextTextQuery(),
                        ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                        InputMethod.Typed);

                    Assert.Fail();
                }
            }
            catch (NullReferenceException)
            {
            }
            catch (Exception)
            {
                Assert.Fail();
            }
        }

        public static async Task TestConversationFlowSandboxFailDevConfig(
            ILogger logger,
            Func<IDurandalPluginLoader, IDurandalPluginProvider> buildBasicPluginProvider,
            DialogEngineParameters defaultDialogParameters,
            IRealTimeProvider realTime,
            TestPluginLoader mockPluginLoader)
        {
            TestPluginLoader newPluginProvider = new TestPluginLoader(new BasicDialogExecutor(true));
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger);
            DialogConfiguration testConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            DialogEngineParameters modifiedParams = defaultDialogParameters.Clone();
            modifiedParams.Configuration = testConfig;
            using (IDurandalPluginProvider provider = buildBasicPluginProvider(newPluginProvider))
            {
                modifiedParams.PluginProvider = new WeakPointer<IDurandalPluginProvider>(provider);
                DialogProcessingEngine dialogEngine = new DialogProcessingEngine(modifiedParams);
                await dialogEngine.LoadPlugin("sandbox", realTime);

                DialogEngineResponse response = await dialogEngine.Process(
                    RankedHypothesis.ConvertRecoResultList(DialogTestHelpers.GetSimpleRecoResultList("sandbox", "fail", 1.0f)),
                    DialogTestHelpers.GetTestClientContextTextQuery(),
                    ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    InputMethod.Typed);

                Assert.AreEqual(Result.Failure, response.ResponseCode);
            }
        }

        #endregion
    }
}
