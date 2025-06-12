using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Test;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Plugins.Fortune;
using Durandal.Plugins.SideSpeech;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Durandal.Common.Test.Builders;
using Durandal.Common.Client;
using System;
using System.IO;

namespace DialogTests.Plugins.BasicPlugins
{
    [TestClass]
    public class BasicPluginTests
    {
        private static SideSpeechPlugin _sideSpeechPlugin;
        private static InqueTestDriver _testDriver;
        private static FakeRandom _rand;

        #region Test framework

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _rand = new FakeRandom();
            _sideSpeechPlugin = new SideSpeechPlugin(_rand);
            string rootEnv = context.Properties["DurandalRootDirectory"]?.ToString();
            if (string.IsNullOrEmpty(rootEnv))
            {
                rootEnv = Environment.GetEnvironmentVariable("DURANDAL_ROOT");
                if (string.IsNullOrEmpty(rootEnv))
                {
                    throw new FileNotFoundException("Cannot find durandal environment directory, either from DurandalRootDirectory test property, or DURANDAL_ROOT environment variable.");
                }
            }

            InqueTestParameters testConfig = PluginTestCommon.CreateTestParameters(_sideSpeechPlugin, "BasicPlugins.dupkg", new DirectoryInfo(rootEnv));
            testConfig.SideSpeechDomain = _sideSpeechPlugin.LUDomain;
            _testDriver = new InqueTestDriver(testConfig);
            _testDriver.Initialize().Await();
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            _testDriver.Dispose();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _testDriver.ResetState();
        }

        #endregion

        #region Tests

        [TestMethod]
        public async Task TestBasicPluginsHelloAnonymous()
        {
            _rand.SetNextInteger(0);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "hello", InputMethod.Typed)
                    .AddRecoResult(DialogConstants.SIDE_SPEECH_DOMAIN, DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, response.SelectedRecoResult.Intent);
            Assert.AreEqual("Hi! What can I do for you?", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestBasicPluginsHelloIncrementingPhraseId()
        {
            FastRandom actualRandom = new FastRandom();
            _rand.SetNextInteger(0);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "hello", InputMethod.Typed)
                    .AddRecoResult(DialogConstants.SIDE_SPEECH_DOMAIN, DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, response.SelectedRecoResult.Intent);
            Assert.AreEqual("Hi! What can I do for you?", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            _rand.SetNextInteger(actualRandom.NextInt());
            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Hello!", response.ResponseText);

            _rand.SetNextInteger(actualRandom.NextInt());
            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Hi! How can I help?", response.ResponseText);

            _rand.SetNextInteger(actualRandom.NextInt());
            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Hi! What can I do for you?", response.ResponseText);
        }

        [TestMethod]
        public async Task TestBasicPluginsHelloPersonalizedGlobalUserProfile()
        {
            ILogger consoleLogger = new ConsoleLogger();
            _rand.SetNextInteger(1);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "hello", InputMethod.Typed)
                    .AddRecoResult(DialogConstants.SIDE_SPEECH_DOMAIN, DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, 0.95f)
                    .Build()
                .Build();

            RetrieveResult<UserProfileCollection> profilesResult = await _testDriver.UserProfileStorage.GetProfiles(UserProfileType.PluginGlobal, DialogTestHelpers.TEST_USER_ID, null, consoleLogger);
            UserProfileCollection profiles = profilesResult.Result;
            if (profiles.GlobalProfile == null)
            {
                profiles.GlobalProfile = new InMemoryDataStore();
            }

            profiles.GlobalProfile.Put(ClientContextField.UserGivenName, "Tyoctanius");
            await _testDriver.UserProfileStorage.UpdateProfiles(UserProfileType.PluginGlobal, profiles, DialogTestHelpers.TEST_USER_ID, null, consoleLogger);

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, response.SelectedRecoResult.Intent);
            Assert.AreEqual("Hello Tyoctanius!", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestBasicPluginsMontyPython()
        {
            _rand.SetNextInteger(0);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is your name", InputMethod.Typed)
                    .AddRecoResult(DialogConstants.SIDE_SPEECH_DOMAIN, "yourname", 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("yourname", response.SelectedRecoResult.Intent);
            Assert.AreEqual("My name is Durandal.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is your quest", InputMethod.Typed)
                    .AddRecoResult(DialogConstants.SIDE_SPEECH_DOMAIN, "yourquest", 0.95f)
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("yourquest", response.SelectedRecoResult.Intent);
            Assert.AreEqual("To seek the Holy Grail.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is the airspeed velocity of an unladen swallow", InputMethod.Typed)
                    .AddRecoResult(DialogConstants.SIDE_SPEECH_DOMAIN, "unladenswallow", 0.95f)
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("unladenswallow", response.SelectedRecoResult.Intent);
            Assert.AreEqual("...I don't know that!", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestBasicPluginsSiriButtPummel()
        {
            _rand.SetNextInteger(0);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "is siri as smart as you", InputMethod.Typed)
                    .AddRecoResult(DialogConstants.SIDE_SPEECH_DOMAIN, DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, response.SelectedRecoResult.Intent);
            Assert.AreEqual("I could beat up Siri any day of the week.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestBasicPluginsChitChatAccuracy()
        {
            _rand.SetNextInteger(0);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "hundreds and hundreds things many many people", InputMethod.Typed)
                    .AddRecoResult(DialogConstants.SIDE_SPEECH_DOMAIN, DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Skip, response.ExecutionResult);
        }

        #endregion
    }
}
