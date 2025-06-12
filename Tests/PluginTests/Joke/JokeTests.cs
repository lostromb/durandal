using Durandal;
using Durandal.Plugins.Fitbit;
using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Logger;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Net.Http;
using System.IO;
using Durandal.Common.Time;
using Durandal.Plugins.Fitbit.Schemas;
using Durandal.Plugins.Fitbit.Schemas.Responses;
using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.UnitConversion;
using Durandal.Common.Test;
using Durandal.Plugins.Joke;
using Durandal.Common.File;
using Durandal.Common.Test.Builders;

namespace DialogTests.Plugins.Joke
{
    [TestClass]
    public class JokeTests
    {
        private static JokePlugin _plugin;
        private static InqueTestDriver _testDriver;

        #region Test framework

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _plugin = new JokePlugin();
            string rootEnv = context.Properties["DurandalRootDirectory"]?.ToString();
            if (string.IsNullOrEmpty(rootEnv))
            {
                rootEnv = Environment.GetEnvironmentVariable("DURANDAL_ROOT");
                if (string.IsNullOrEmpty(rootEnv))
                {
                    throw new FileNotFoundException("Cannot find durandal environment directory, either from DurandalRootDirectory test property, or DURANDAL_ROOT environment variable.");
                }
            }

            InqueTestParameters testConfig = PluginTestCommon.CreateTestParameters(_plugin, "JokePlugin.dupkg", new DirectoryInfo(rootEnv));
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
        public async Task TestJokeTellAJoke()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "tell me a joke", InputMethod.Typed)
                    .AddRecoResult("joke", "tell_a_joke", 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("tell_a_joke", response.SelectedRecoResult.Intent);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestJokeTellAJokeTellAnother()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "tell me a joke", InputMethod.Typed)
                    .AddRecoResult("joke", "tell_a_joke", 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("tell_a_joke", response.SelectedRecoResult.Intent);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "another one", InputMethod.Typed)
                    .AddRecoResult("joke", "tell_another", 0.95f)
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("tell_another", response.SelectedRecoResult.Intent);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestJokeTellAJokeElaboration()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "tell me a joke", InputMethod.Typed)
                    .AddRecoResult("joke", "tell_a_joke", 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("tell_a_joke", response.SelectedRecoResult.Intent);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "more", InputMethod.Typed)
                    .AddRecoResult("common", "elaboration", 0.95f)
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("elaboration", response.SelectedRecoResult.Intent);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestJokeKnockKnock()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "knock knock", InputMethod.Typed)
                    .AddRecoResult("joke", "knock_knock", 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Who's there?", response.ResponseText);
            Assert.IsTrue(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "Jaden", InputMethod.Typed)
                    .AddRecoResult("common", "side_speech", 0.95f)
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Jaden who?", response.ResponseText);
            Assert.IsTrue(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "I don't know you tell me", InputMethod.Typed)
                    .AddRecoResult("common", "side_speech", 0.95f)
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("I don't get it.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        #endregion
    }
}
