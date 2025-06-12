//using Durandal;
//using Durandal.Plugins.Fitbit;
//using Durandal.API;
//using Durandal.Common.Config;
//using Durandal.Common.Dialog;
//using Durandal.Common.Logger;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Durandal.Common.Tasks;
////using Durandal.Common.Net.Http;
//using System.IO;
//using Durandal.Common.Time;
//using Durandal.Plugins.Fitbit.Schemas;
//using Durandal.Plugins.Fitbit.Schemas.Responses;
//using Durandal.Common.Time.Timex;
//using Durandal.Common.Time.Timex.Enums;
//using Durandal.Common.Utils.UnitConversion;
//using Durandal.Common.Test;
//using Durandal.Plugins.AnimalSounds;
//using System.Text.RegularExpressions;
//using Durandal.Common.Utils;
//using Durandal.Common.Net;
//using Durandal.Common.File;

//namespace DialogTests.Plugins.AnimalSounds
//{
//    [TestClass]
//    public class AnimalSoundTests
//    {
//        private static AnimalSoundsPlugin _plugin;
//        private static InqueTestDriver _testDriver;
//        private static readonly Regex IMAGE_URL_MATCHER = new Regex("<img.+?src=\\\"(.+?)\\\">");

//        #region Test framework

//        [ClassInitialize]
//        public static void Initialize(TestContext context)
//        {
//            _plugin = new AnimalSoundsPlugin();
//            InqueTestParameters testConfig = PluginTestCommon.CreateTestParameters(_plugin, "AnimalSoundsPlugin.dupkg");
//            _testDriver = new InqueTestDriver(testConfig);
//            _testDriver.Initialize().Await();
//        }

//        [ClassCleanup]
//        public static void Cleanup()
//        {
//            _testDriver.Dispose();
//        }

//        [TestInitialize]
//        public void TestInitialize()
//        {
//            _testDriver.ResetState();
//        }

//        #endregion

//        #region Tests

//        [TestMethod]
//        public async Task TestAnimalSoundsUnknownAnimal()
//        {
//            DialogRequest request =
//                new DialogRequestBuilder<DialogRequest>((x) => x, "what does the smog say", InputMethod.Typed)
//                    .AddRecoResult("animalsounds", "get_animal_sound", 0.95f)
//                        .AddTagHypothesis(0.95f)
//                            .AddBasicSlot("animal", "smog")
//                        .Build()
//                    .Build()
//                .Build();

//            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
//            Assert.IsNotNull(response);
//            Assert.AreEqual(Result.Skip, response.ExecutionResult);
//        }

//        [TestMethod]
//        public async Task TestAnimalSoundsMultiturn()
//        {
//            DialogRequest request =
//                new DialogRequestBuilder<DialogRequest>((x) => x, "what does the cow say", InputMethod.Spoken)
//                    .AddRecoResult("animalsounds", "get_animal_sound", 0.95f)
//                        .AddTagHypothesis(0.95f)
//                            .AddBasicSlot("animal", "cow")
//                        .Build()
//                    .Build()
//                .Build();

//            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
//            Assert.IsNotNull(response);
//            Assert.AreEqual("The cow says \"Moo!\"", response.ResponseText);
//            Assert.IsFalse(response.ContinueImmediately);

//            // Assert that there is custom audio
//            Assert.IsNotNull(response.ResponseAudio);
//            Assert.IsTrue(response.ResponseAudio.Data.Count > 0);

//            // Assert that HTML came back
//            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
//            // Assert that HTML contains an image link
//            string imageUrl = StringUtils.RegexRip(IMAGE_URL_MATCHER, response.ResponseHtml, 1);
//            Assert.IsFalse(string.IsNullOrEmpty(imageUrl));
//            // Assert that that image link refers to valid data
//            HttpRequest imageFetchRequest = HttpRequest.CreateOutgoing(imageUrl);
//            HttpResponse imageFetchResponse = await _testDriver.Client.MakeStaticResourceRequest(imageFetchRequest);
//            Assert.IsNotNull(imageFetchResponse);
//            Assert.AreEqual(200, imageFetchResponse.ResponseCode);

//            // Now turn 2
//            request =
//                new DialogRequestBuilder<DialogRequest>((x) => x, "what about a dog", InputMethod.Spoken)
//                    .AddRecoResult("animalsounds", "get_animal_sound_multiturn", 0.95f)
//                        .AddTagHypothesis(0.95f)
//                            .AddBasicSlot("animal", "dog")
//                        .Build()
//                    .Build()
//                .Build();

//            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
//            Assert.IsNotNull(response);
//            Assert.AreEqual("The dog says \"Woof!\"", response.ResponseText);
//            Assert.IsFalse(response.ContinueImmediately);
//            Assert.IsNotNull(response.ResponseAudio);
//            Assert.IsTrue(response.ResponseAudio.Data.Count > 0);
//            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
//            imageUrl = StringUtils.RegexRip(IMAGE_URL_MATCHER, response.ResponseHtml, 1);
//            Assert.IsFalse(string.IsNullOrEmpty(imageUrl));
//            imageFetchRequest = HttpRequest.CreateOutgoing(imageUrl);
//            imageFetchResponse = await _testDriver.Client.MakeStaticResourceRequest(imageFetchRequest);
//            Assert.IsNotNull(imageFetchResponse);
//            Assert.AreEqual(200, imageFetchResponse.ResponseCode);
//        }

//        [TestMethod]
//        public async Task TestAnimalSoundsBear() { await RunAnimalTest("bear", "bear", "Grrrraaawr!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsBee() { await RunAnimalTest("bee", "bee", "Bzzzzzz!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsBee2() { await RunAnimalTest("bumble bee", "bee", "Bzzzzzz!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsBird() { await RunAnimalTest("bird", "bird", "Chirp!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsButterfly() { await RunAnimalTest("butterfly", "butterfly", ""); }
//        [TestMethod]
//        public async Task TestAnimalSoundsCat() { await RunAnimalTest("cat", "cat", "Meow!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsChicken() { await RunAnimalTest("chicken", "chicken", "Cluck!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsCow() { await RunAnimalTest("cow", "cow", "Moo!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsCoyote() { await RunAnimalTest("coyote", "coyote", "Awooooo!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsCricket() { await RunAnimalTest("cricket", "cricket", "Chirp!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsCrow() { await RunAnimalTest("crow", "crow", "Caw!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsDog() { await RunAnimalTest("dog", "dog", "Woof!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsDolphin() { await RunAnimalTest("dolphin", "dolphin", "Click!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsDonkey() { await RunAnimalTest("donkey", "donkey", "Hee-haw!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsDuck() { await RunAnimalTest("duck", "duck", "Quack!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsElephant() { await RunAnimalTest("elephant", "elephant", "Baroooo!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsFish() { await RunAnimalTest("fish", "fish", "Glub!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsFox() { await RunAnimalTest("fox", "fox", "Ring-a-ding-ding!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsFrog() { await RunAnimalTest("frog", "frog", "Ribbit!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsGiraffe() { await RunAnimalTest("giraffe", "giraffe", "Hummm!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsGoat() { await RunAnimalTest("goat", "goat", "Naaa!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsGoose() { await RunAnimalTest("goose", "goose", "Honk!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsHorse() { await RunAnimalTest("horse", "horse", "Neigh!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsJaguar() { await RunAnimalTest("jaguar", "jaguar", "Yoowwww!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsKitten() { await RunAnimalTest("kitten", "kitten", "Mew!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsLeopard() { await RunAnimalTest("leopard", "leopard", "Yoowwww!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsLion() { await RunAnimalTest("lion", "lion", "Roar!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsLizard() { await RunAnimalTest("lizard", "lizard", "Mlem!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsMonkey() { await RunAnimalTest("monkey", "monkey", "Hoo hoo hoo!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsOwl() { await RunAnimalTest("owl", "owl", "Hoot!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsPanda() { await RunAnimalTest("panda", "panda", "Grrrawwwrr!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsParrot() { await RunAnimalTest("parrot", "parrot", "Ca-CAW!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsPigeon() { await RunAnimalTest("pigeon", "pigeon", "Coo coo!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsPig() { await RunAnimalTest("pig", "pig", "Oink!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsPiggie() { await RunAnimalTest("piggie", "pig", "Oink!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsPlatypus() { await RunAnimalTest("platypus", "platypus", "Mrglmrglmrglmrgl!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsPony() { await RunAnimalTest("pony", "pony", "Neigh!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsPuppy() { await RunAnimalTest("puppy", "puppy", "Bark!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsRooster() { await RunAnimalTest("rooster", "rooster", "Cock-a-doodle-doo!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsSheep() { await RunAnimalTest("sheep", "sheep", "Baaaa!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsTiger() { await RunAnimalTest("tiger", "tiger", "Rawr!"); }
//        [TestMethod]
//        public async Task TestAnimalSoundsToad() { await RunAnimalTest("toad", "toad", "Croak!"); }

//        #endregion

//        private static async Task RunAnimalTest(string animalName, string animalNameCanonical, string expectedSaying)
//        {
//            DialogRequest request =
//                new DialogRequestBuilder<DialogRequest>((x) => x, "what does the " + animalName + " say", InputMethod.Spoken)
//                    .AddRecoResult("animalsounds", "get_animal_sound", 0.95f)
//                        .AddTagHypothesis(0.95f)
//                            .AddBasicSlot("animal", animalName)
//                        .Build()
//                    .Build()
//                .Build();

//            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
//            Assert.IsNotNull(response);
//            Assert.AreEqual("The " + animalNameCanonical + " says \"" + expectedSaying + "\"", response.ResponseText);
//            Assert.IsFalse(response.ContinueImmediately);

//            // Assert that there is custom audio
//            Assert.IsNotNull(response.ResponseAudio);
//            Assert.IsTrue(response.ResponseAudio.Data.Count > 0);

//            // Assert that HTML came back
//            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
//            // Assert that HTML contains an image link
//            string imageUrl = StringUtils.RegexRip(IMAGE_URL_MATCHER, response.ResponseHtml, 1);
//            Assert.IsFalse(string.IsNullOrEmpty(imageUrl));
//            // Assert that that image link refers to valid data
//            HttpRequest imageFetchRequest = HttpRequest.CreateOutgoing(imageUrl);
//            HttpResponse imageFetchResponse = await _testDriver.Client.MakeStaticResourceRequest(imageFetchRequest);
//            Assert.IsNotNull(imageFetchResponse);
//            Assert.AreEqual(200, imageFetchResponse.ResponseCode);
//        }
//    }
//}
