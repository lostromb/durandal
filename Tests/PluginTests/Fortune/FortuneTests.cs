using Durandal.API;
using Durandal.Common.Test;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Plugins.Fortune;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Durandal.Common.Test.Builders;
using System.IO;
using System;

namespace DialogTests.Plugins.Fortune
{
    [TestClass]
    public class FortuneTests
    {
        private static FortunePlugin _plugin;
        private static InqueTestDriver _testDriver;
        private static FakeRandom _rand;

        #region Test framework

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _rand = new FakeRandom();
            _plugin = new FortunePlugin(_rand);
            string rootEnv = context.Properties["DurandalRootDirectory"]?.ToString();
            if (string.IsNullOrEmpty(rootEnv))
            {
                rootEnv = Environment.GetEnvironmentVariable("DURANDAL_ROOT");
                if (string.IsNullOrEmpty(rootEnv))
                {
                    throw new FileNotFoundException("Cannot find durandal environment directory, either from DurandalRootDirectory test property, or DURANDAL_ROOT environment variable.");
                }
            }

            InqueTestParameters testConfig = PluginTestCommon.CreateTestParameters(_plugin, "FortunePlugin.dupkg", new DirectoryInfo(rootEnv));
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
        public async Task TestFortuneTellMyFortune()
        {
            _rand.SetNextInteger(0);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "tell me my fortune", InputMethod.Typed)
                    .AddRecoResult("fortune", "tell_fortune", 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("tell_fortune", response.SelectedRecoResult.Intent);
            Assert.AreEqual("A bad workman quarrels with his tools.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFortuneTellAnother()
        {
            _rand.SetNextInteger(1);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "tell me my fortune", InputMethod.Typed)
                    .AddRecoResult("fortune", "tell_fortune", 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("tell_fortune", response.SelectedRecoResult.Intent);
            Assert.AreEqual("A bird in hand is safer than one overhead.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            _rand.SetNextInteger(2);
            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "another one", InputMethod.Typed)
                    .AddRecoResult("fortune", "tell_another", 0.95f)
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("tell_another", response.SelectedRecoResult.Intent);
            Assert.AreEqual("A chicken is just an egg's way of making more eggs.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        #endregion
    }
}
