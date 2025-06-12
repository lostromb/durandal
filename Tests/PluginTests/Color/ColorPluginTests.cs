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
using System.Text.RegularExpressions;
using Durandal.Common.Utils;
using Durandal.Common.Net;
using Durandal.Plugins.Color;
using Durandal.Common.MathExt;
using Durandal.Common.File;
using Durandal.Common.Test.Builders;

namespace DialogTests.Plugins.AnimalSounds
{
    [TestClass]
    public class ColorPluginTests
    {
        private static ColorPlugin _plugin;
        private static InqueTestDriver _testDriver;
        private static FastRandom _rand;

        #region Test framework

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _rand = new FastRandom();
            _plugin = new ColorPlugin(_rand);
            string rootEnv = context.Properties["DurandalRootDirectory"]?.ToString();
            if (string.IsNullOrEmpty(rootEnv))
            {
                rootEnv = Environment.GetEnvironmentVariable("DURANDAL_ROOT");
                if (string.IsNullOrEmpty(rootEnv))
                {
                    throw new FileNotFoundException("Cannot find durandal environment directory, either from DurandalRootDirectory test property, or DURANDAL_ROOT environment variable.");
                }
            }

            InqueTestParameters testConfig = PluginTestCommon.CreateTestParameters(_plugin, "ColorPlugin.dupkg", new DirectoryInfo(rootEnv));
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
        public async Task TestFavoriteColor()
        {
            _rand.SeedRand(70);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is your favorite color", InputMethod.Typed)
                    .AddRecoResult("color", "favorite_color", 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("My favorite color is probably mountain meadow.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorSuggestion()
        {
            _rand.SeedRand(50);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color should I dye my hair", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_suggestion", 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("I would suggest old burgundy.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorRed()
        {
            _rand.SeedRand(99);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color is red", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_info", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("color", "red")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("Red is the color of a fire truck.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorGreen()
        {
            _rand.SeedRand(11);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color is green", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_info", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("color", "green")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("Green is the color of broccoli.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorChartreuse()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color is chartreuse", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_info", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("color", "chartreuse")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("Chartreuse is a light vivid color similar to yellow.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorCrimson()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color is crimson", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_info", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("color", "crimson")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("Crimson is a vivid color similar to red.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorLavender()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color is lavender", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_info", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("color", "lavender")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("Lavender is a light color similar to light purple.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorMauve()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color is mauve", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_info", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("color", "mauve")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("Mauve is a light color similar to light purple.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorSalmon()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color is salmon", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_info", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("color", "salmon")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("Salmon is a light vivid color similar to dark pink.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorAuburn()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color is auburn", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_info", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("color", "auburn")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("Auburn is a dark color between brown and dark red.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorCerise()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color is cerise", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_info", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("color", "cerise")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("Cerise is a vivid color similar to dark pink.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorCordovan()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color is cordovan", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_info", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("color", "cordovan")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);

            Assert.AreEqual("Cordovan is a dark color between dark brown and light brown.", response.ResponseText);
        }

        [TestMethod]
        public async Task TestColorUnknown()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what color is a giraffe", InputMethod.Typed)
                    .AddRecoResult("color", "get_color_info", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("color", "a giraffe")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Skip, response.ExecutionResult);
        }

        #endregion
    }
}
