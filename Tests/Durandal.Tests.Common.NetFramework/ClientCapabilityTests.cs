using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DialogTests
{
    using Durandal;
    using Durandal.API;
    using Durandal.Common;

    using Stromberg.Config;
    using Stromberg.Logger;
    using Stromberg.Utils.IO;
    using Durandal.Common.Dialog.Core;
    using Stromberg.Utils;
    using Durandal.Common.Utils;
    using Durandal.API.Utils;
    using Durandal.Common.Dialog;
    using Durandal.Common.Net;
    using System.Net;
    using System.IO;
    using Durandal.Common.Audio.Interfaces;
    using Durandal.Common.Audio;
    using System.Threading.Tasks;
    using Durandal.Common.Audio.Codecs;
    using Stromberg.Net;

    /// <summary>
    /// Tests to ensure that the proper response is generate for many different possible configurations of the client.
    /// This test operates at the DialogWebService level, rather than the DialogProcessingEngine level, so it's a little more involved as it has the
    /// side effect of starting the full HTTP stack, voice synthesizers, etc.
    /// </summary>
    [TestClass]
    public class ClientCapabilityTests
    {
        private static DialogWebService _dialogEngine;
        private static TestAnswerProvider _mockAnswerProvider;
        private static IResourceManager _mockResourceManager;
        private static ILogger _logger;
        private static InMemoryCache<DialogAction> _mockDialogActionCache;
        private static InMemoryCache<CachedWebData> _mockWebDataCache;
        private static InMemoryConversationStateCache _mockConversationStateCache;
        private static InMemoryCache<ClientContext> _mockClientContextCache;
        private static InMemoryCache<string> _mockHtmlPageCache;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _mockAnswerProvider = new TestAnswerProvider();
            _logger = new ConsoleLogger("Main", LogLevel.Err | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn, false);
            _mockResourceManager = new FileResourceManager(_logger);
            _mockDialogActionCache = new InMemoryCache<DialogAction>();
            _mockWebDataCache = new InMemoryCache<CachedWebData>();
            _mockConversationStateCache = new InMemoryConversationStateCache();
            _mockClientContextCache = new InMemoryCache<ClientContext>();
            _mockHtmlPageCache = new InMemoryCache<string>();

            DialogConfiguration dialogEngineConfig = TestCommon.GetTestDialogConfiguration(_logger);
            DialogEngineParameters dialogParams = new DialogEngineParameters(dialogEngineConfig, _mockAnswerProvider)
            {
                Logger = _logger,
                DialogActionCache = MockRegisterDialogAction,
                WebCache = MockRegisterWebData,
                ConversationStateCache = new InMemoryConversationStateCache(),
                ResourceManager = new NullResourceManager()
            };
            DialogProcessingEngine dialogCore = new DialogProcessingEngine(dialogParams);
            ISet<string> answersToLoad = new HashSet<string>();
            answersToLoad.Add("clientcaps");
            answersToLoad.Add("basictree");
            dialogCore.LoadAnswers(answersToLoad);

            DialogWebConfiguration dialogWebConfig = TestCommon.GetTestDialogWebConfiguration(_logger);
            _dialogEngine = new DialogWebService(dialogWebConfig, dialogEngineConfig, _logger, _mockResourceManager);
            _dialogEngine.Initialize(dialogCore, null, _mockDialogActionCache, _mockConversationStateCache, _mockWebDataCache, _mockClientContextCache, _mockHtmlPageCache);
        }

        public static string MockRegisterDialogAction(DialogAction action)
        {
            string key = Guid.NewGuid().ToString("N");
            _mockDialogActionCache.Store(key, action, TimeSpan.FromSeconds(60));
            return key;
        }

        public static string MockRegisterWebData(CachedWebData data)
        {
            string key = Guid.NewGuid().ToString("N");
            _mockWebDataCache.Store(key, data, TimeSpan.FromSeconds(60));
            return key;
        }

        [TestInitialize]
        public void InitTest()
        {
            _mockClientContextCache.Clear();
            _mockWebDataCache.Clear();
            _mockDialogActionCache.Clear();
            _mockHtmlPageCache.Clear();
            _mockConversationStateCache.ClearAllConversationStates();
        }

        [ClassCleanup]
        public static void CleanupAllTests()
        {
            // Since we started an HTTP server in these tests, we need to stop them properly.
            _dialogEngine.Stop();
        }

        private static bool AudioIsNull(AudioData audio)
        {
            return audio == null || audio.Data == null || audio.Data.Count == 0;
        }

        /// <summary>
        /// Test that a client will full capabilities will recieve the proper answer data, with an HTML response
        /// </summary>
        [TestMethod]
        public void TestFullClientCapabilitiesHtml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.CanSynthesizeSpeech |
                                            ClientCapabilities.HasGps |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsCompressedAudio;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.IsFalse(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsFalse(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseSSML));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client will full capabilities will recieve the proper answer data, with a URL response
        /// </summary>
        [TestMethod]
        public void TestFullClientCapabilitiesUrl()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.CanSynthesizeSpeech |
                                            ClientCapabilities.HasGps |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsCompressedAudio;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "url", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.IsFalse(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseSSML));
            Assert.IsFalse(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client will full capabilities will recieve the proper answer data, with an HTML response
        /// </summary>
        [TestMethod]
        public void TestFullClientCapabilitiesHtmlWithoutTts()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.HasGps |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsCompressedAudio;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.IsFalse(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsFalse(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSSML));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsFalse(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client which cannot serve its own HTML will get a URL response to display web data
        /// </summary>
        [TestMethod]
        public void TestDialogCoreWillServeHtml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsFalse(string.IsNullOrEmpty(response.UrlToOpen));
        }

        /// <summary>
        /// Test that a client which CAN serve its own HTML will get an HTML response to display web data
        /// </summary>
        [TestMethod]
        public void TestDialogCoreWillPassthroughHtml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.IsFalse(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
        }

        /// <summary>
        /// Test that a client will NO capabilities will return no significant response
        /// </summary>
        [TestMethod]
        public void TestNoClientCapabilities()
        {
            ClientCapabilities capsToTest = ClientCapabilities.None;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.IsTrue(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client with no speakers will recieve no audio response
        /// </summary>
        [TestMethod]
        public void TestClientTextOnly()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.IsFalse(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
        }

        /// <summary>
        /// Test that a client with text + HTML support will recieve the text rendered as HTML
        /// </summary>
        [TestMethod]
        public void TestClientTextOnlyWithBasicHtml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayBasicHtml |
                                            ClientCapabilities.ServeHtml;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "textonly", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.IsFalse(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsFalse(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client with text + HTML5 support will recieve the text rendered as HTML
        /// </summary>
        [TestMethod]
        public void TestClientTextOnlyWithHtml5()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "textonly", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.IsFalse(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsFalse(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client with text + HTML support will NOT recieve the text rendered as HTML if it doesn't ask for it
        /// </summary>
        [TestMethod]
        public void TestClientTextOnlyNoHtml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayBasicHtml |
                                            ClientCapabilities.DoNotRenderTextAsHtml |
                                            ClientCapabilities.ServeHtml;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "textonly", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.IsFalse(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client with no display will recieve no visual response
        /// </summary>
        [TestMethod]
        public void TestClientAudioOnly()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.IsTrue(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsFalse(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client with no display will recieve no visual response
        /// </summary>
        [TestMethod]
        public void TestClientSsmlOnly()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.CanSynthesizeSpeech;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.IsTrue(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseSSML));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client which supports streaming audio will get a response when SSML is rendered server-side
        /// </summary>
        [TestMethod]
        public void TestClientStreamingSsml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsStreamingAudio;
            AudioTransportStream streamingAudio;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "textssml", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.Authorized,
                _logger,
                out streamingAudio);
            Assert.IsTrue(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSSML));
            Assert.IsNotNull(streamingAudio);
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client which supports streaming audio will get a response when audio is rendered server-side
        /// </summary>
        [TestMethod]
        public void TestClientStreamingAudio()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsStreamingAudio;
            AudioTransportStream streamingAudio;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "audioonly", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.Authorized,
                _logger,
                out streamingAudio);
            Assert.IsTrue(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSSML));
            Assert.IsNotNull(streamingAudio);
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client which supports streaming audio will get a response when audio is rendered server-side
        /// </summary>
        [TestMethod]
        public void TestClientStreamingWithCustomAudio()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsStreamingAudio;
            AudioTransportStream streamingAudio;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "customaudio", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.Authorized,
                _logger,
                out streamingAudio);
            Assert.IsTrue(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSSML));
            Assert.IsNotNull(streamingAudio);
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client with only an HTML display will only recieve HTML
        /// </summary>
        [TestMethod]
        public void TestClientHtmlOnly()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.IsTrue(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsFalse(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test the capabilities used by the Cortana interface client
        /// </summary>
        [TestMethod]
        public void TestCortanaCapabilities1()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.DisplayBasicText |
                                            ClientCapabilities.HasGps |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.CanSynthesizeSpeech |
                                            ClientCapabilities.DoNotRenderTextAsHtml;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.IsFalse(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsFalse(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseSSML));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test the capabilities used by the Cortana interface client
        /// </summary>
        [TestMethod]
        public void TestCortanaCapabilities2()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.DisplayBasicText |
                                            ClientCapabilities.HasGps |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.CanSynthesizeSpeech |
                                            ClientCapabilities.DoNotRenderTextAsHtml;
            ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "textssml", 1.0f),
                TestCommon.GetSimpleClientRequest(TestCommon.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.Authorized,
                _logger);
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.IsFalse(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseSSML));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.AudioToPlay));
        }

        /// <summary>
        /// Test that a client which supports streaming audio will get a response when audio is rendered server-side
        /// </summary>
        [TestMethod]
        public async Task TestStreamingAudioOverHttp()
        {
            DialogHttpClient dialogClient = new DialogHttpClient(new PortableHttpClient("localhost", 62292, _logger), _logger);
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsStreamingAudio;
            ClientRequest request = new ClientRequest()
            {
                ClientContext = TestCommon.GetTestClientContext(capsToTest),
                InputType = InputMethod.Spoken,
                UnderstandingData = TestCommon.GetSimpleRecoResultList("clientcaps", "audioonly", 1.0f)
            };

            NetworkResponseInstrumented<ClientResponse> netResponse = await dialogClient.MakeQueryRequest(request);
            Assert.IsNotNull(netResponse);
            Assert.IsNotNull(netResponse.Response);
            ClientResponse response = netResponse.Response;

            Assert.IsTrue(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay));
            Assert.IsTrue(string.IsNullOrEmpty(response.UrlToOpen));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSSML));
            Assert.IsFalse(string.IsNullOrEmpty(response.StreamingAudioUrl), "Response should have a streaming audio URL");

            AudioChunk downloadedAudio = TryGetStreamingResponseAudio(response.StreamingAudioUrl);

            Assert.IsNotNull(downloadedAudio, "Streaming audio must be non-null");
            Assert.IsTrue(downloadedAudio.SampleRate == AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE, "Sample rate should have been parsed from response");
            Assert.IsTrue(downloadedAudio.DataLength >= 2500, "Should have read a significant payload from the audio endpoint");
        }

        /// <summary>
        /// Test that dialog action URLs can be invoked over an http client and trigger the proper actions
        /// </summary>
        [TestMethod]
        public async Task TestDirectDialogActionsOverHttp()
        {
            DialogHttpClient dialogClient = new DialogHttpClient(new PortableHttpClient("localhost", 62292, _logger), _logger);
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers;
            ClientRequest request = new ClientRequest()
            {
                ClientContext = TestCommon.GetTestClientContext(capsToTest),
                InputType = InputMethod.Spoken,
                UnderstandingData = TestCommon.GetSimpleRecoResultList("basictree", "start_dialogaction", 1.0f)
            };

            NetworkResponseInstrumented<ClientResponse> netResponse = await dialogClient.MakeQueryRequest(request);
            Assert.IsNotNull(netResponse);
            Assert.IsNotNull(netResponse.Response);
            ClientResponse response = netResponse.Response;

            Assert.IsFalse(string.IsNullOrEmpty(response.TextToDisplay));
            Assert.IsTrue(response.ResponseData.ContainsKey("actionUrl"));
            string actionUrl = response.ResponseData["actionUrl"];

            request = new ClientRequest()
            {
                ClientContext = TestCommon.GetTestClientContext(capsToTest),
                InputType = InputMethod.Programmatic
            };

            NetworkResponseInstrumented<ClientResponse> actionResponse = dialogClient.MakeDialogActionRequest(request, actionUrl);
            Assert.IsNotNull(actionResponse);
            Assert.IsNotNull(actionResponse.Response);
            Assert.AreEqual(Result.Success, actionResponse.Response.ExecutionResult);
            Assert.AreEqual("turn2", actionResponse.Response.SelectedRecoResult.Intent);
        }

        /// <summary>
        /// Test that when plugins request single-use resources from the server, they are cached and transmitted properly
        /// </summary>
        [TestMethod]
        public async Task TestWebDataCache()
        {
            DialogHttpClient dialogClient = new DialogHttpClient(new PortableHttpClient("localhost", 62292, _logger), _logger);
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml;

            ClientRequest request = new ClientRequest()
            {
                ClientContext = TestCommon.GetTestClientContext(capsToTest),
                InputType = InputMethod.Spoken,
                UnderstandingData = TestCommon.GetSimpleRecoResultList("clientcaps", "cache", 1.0f)
            };

            NetworkResponseInstrumented<ClientResponse> netResponse = await dialogClient.MakeQueryRequest(request);
            Assert.IsNotNull(netResponse);
            Assert.IsNotNull(netResponse.Response);
            ClientResponse response = netResponse.Response;
            
            // Download the cached data and inspect it
            Assert.IsNotNull(response.UrlToOpen);
            WebClient downloader = new WebClient();
            string key = response.UrlToOpen.Substring(response.UrlToOpen.LastIndexOf('=') + 1);
            Assert.IsTrue(_mockWebDataCache.ContainsKey(key));
            string cachedData = downloader.DownloadString("http://localhost:62292" + response.UrlToOpen);
            Assert.AreEqual("Cached data", cachedData);

            // Make sure it was deleted from the cache after access
            // Assert.IsFalse(_mockWebDataCache.ContainsKey(key));
        }

        /// <summary>
        /// Test that when we invoke direct dialog actions, the conversation state is not persisted
        /// </summary>
        [TestMethod]
        public async Task TestDirectDialogActionsDontPersistState()
        {
            DialogHttpClient dialogClient = new DialogHttpClient(new PortableHttpClient("localhost", 62292, _logger), _logger);
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers;
            ClientRequest request = new ClientRequest()
            {
                ClientContext = TestCommon.GetTestClientContext(capsToTest),
                InputType = InputMethod.Programmatic,
                UnderstandingData = TestCommon.GetSimpleRecoResultList("basictree", "start", 1.0f)
            };

            NetworkResponseInstrumented<ClientResponse> netResponse = await dialogClient.MakeQueryRequest(request);
            Assert.IsNotNull(netResponse);
            Assert.IsNotNull(netResponse.Response);
            ClientResponse response = netResponse.Response;

            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.AreEqual("start", response.SelectedRecoResult.Intent);

            request = new ClientRequest()
            {
                ClientContext = TestCommon.GetTestClientContext(capsToTest),
                InputType = InputMethod.Programmatic,
                UnderstandingData = TestCommon.GetSimpleRecoResultList("basictree", "turn2", 1.0f)
            };

            netResponse = await dialogClient.MakeQueryRequest(request);
            Assert.IsNotNull(netResponse);
            Assert.IsNotNull(netResponse.Response);
            response = netResponse.Response;

            // We should not have been able to reach turn 2 programmatically, so response should have been skip
            Assert.AreEqual(Result.Skip, response.ExecutionResult);
        }

        private AudioChunk TryGetStreamingResponseAudio(string audioUrl)
        {
            PCMCodec codec = new PCMCodec(_logger);
            HttpWebRequest req = HttpWebRequest.CreateHttp("http://localhost:62292" + audioUrl);
            WebResponse audioStreamResponse = req.GetResponse();
            string contentType = audioStreamResponse.Headers[HttpResponseHeader.ContentType];
            string codecHeader = audioStreamResponse.Headers["X-Audio-Codec"] ?? contentType;
            string encodeParams = audioStreamResponse.Headers["X-Audio-Codec-Params"] ?? contentType;
            Assert.AreEqual("pcm", codecHeader);
            Assert.AreEqual("audio/wav; codec=\"audio/pcm\"; samplerate=" + AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE, contentType);
            IAudioDecompressionStream streamDecompressor = codec.CreateDecompressionStream(encodeParams);

            AudioChunk returnVal = null;
            int bytesRead = 1;
            byte[] buf = new byte[1024];
            using (Stream responseStream = audioStreamResponse.GetResponseStream())
            {
                try
                {
                    while (bytesRead > 0)
                    {
                        bytesRead = responseStream.Read(buf, 0, 1024);

                        if (bytesRead > 0)
                        {
                            byte[] realData = new byte[bytesRead];
                            Array.Copy(buf, 0, realData, 0, bytesRead);
                            AudioChunk audio = streamDecompressor.Decompress(realData);
                            Assert.IsNotNull(audio, "Decoded audio should be non-null");

                            if (returnVal == null)
                            {
                                returnVal = audio;
                            }
                            else
                            {
                                returnVal = returnVal.Concatenate(audio);
                            }
                        }
                    }

                    AudioChunk footer = streamDecompressor.Close();

                    if (returnVal == null)
                    {
                        returnVal = footer;
                    }
                    else if (footer != null)
                    {
                        returnVal = returnVal.Concatenate(footer);
                    }
                }
                finally
                {
                    responseStream.Close();
                }
            }
            audioStreamResponse.Close();

            return returnVal;
        }

        /// <summary>
        /// Runs a lot of conversations with lots of clients simultaneously to make sure nothing explodes
        /// </summary>
        [TestMethod]
        public void TestServerThreadSafety()
        {
            const int numTasks = 1000;
            Task<ClientResponse>[] tasks = new Task<ClientResponse>[numTasks];
            ConcurrentExclusiveSchedulerPair scheduler = new System.Threading.Tasks.ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, 8);

            for(int task = 0; task < numTasks; task++)
            {
                tasks[task] = new Task<ClientResponse>(() =>
                    {
                        ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.HasGps |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsCompressedAudio;
                        ClientContext context = TestCommon.GetTestClientContext(capsToTest);
                        context.ClientId = Guid.NewGuid().ToString("N");
                        context.UserId = Guid.NewGuid().ToString("N");

                        ClientResponse response = _dialogEngine.ProcessRecoResults(TestCommon.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                            TestCommon.GetSimpleClientRequest(context, "this is a unit test", InputMethod.Spoken),
                            ClientAuthenticationLevel.Authorized,
                            _logger);

                        return response;
                    });

                tasks[task].Start(scheduler.ConcurrentScheduler);
            }

            Task.WaitAll(tasks);

            for (int task = 0; task < numTasks; task++)
            {
                ClientResponse response = tasks[task].Result;
                Assert.AreEqual(Result.Success, response.ExecutionResult, "Execution result should be success");
                Assert.IsFalse(string.IsNullOrEmpty(response.TextToDisplay), "Should have response text");
                Assert.IsTrue(string.IsNullOrEmpty(response.HtmlToDisplay), "Should not have response html");
                Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSSML), "Should not have response ssml");
                Assert.IsFalse(string.IsNullOrEmpty(response.UrlToOpen), "Should have response url");
                Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl), "Should not have audio URL");
                Assert.IsFalse(AudioIsNull(response.AudioToPlay), "Should have audio to play");
            }
        }
    }
}
