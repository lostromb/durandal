namespace Durandal.Tests.Common.Dialog.Runtime
{
    using Durandal;
    using Durandal.API;
        using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Security;
    using Durandal.Common.Speech;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Test;
    using Durandal.Common.Utils;
    using Durandal.Common.Cache;
    using Durandal.Common.File;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.IO;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Audio.Components;
    using Newtonsoft.Json;
    using Durandal.Common.Config;
using Durandal.Tests.Common.Dialog.Runtime;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Audio.Codecs.Opus;

    /// <summary>
    /// Tests to ensure that the proper response is generate for many different possible configurations of the client.
    /// This test operates at the DialogWebService level, rather than the DialogProcessingEngine level, so it's a little more involved as it has the
    /// side effect of starting the full HTTP stack, voice synthesizers, etc.
    /// </summary>
    [TestClass]
    public class ClientCapabilityTests
    {
        private static DialogWebService _dialogEngine;
        private static TestPluginLoader _mockPluginLoader;
        private static MachineLocalPluginProvider _mockPluginProvider;
        private static IFileSystem _mockFileSystem;
        private static ILogger _logger;
        private static IConfiguration _baseConfig;
        private static InMemoryCache<DialogAction> _mockDialogActionCache;
        private static InMemoryCache<CachedWebData> _mockWebDataCache;
        private static InMemoryConversationStateCache _mockConversationStateCache;
        private static InMemoryCache<ClientContext> _mockClientContextCache;
        private static NullHttpServer _dialogHttpServer;
        private static DirectHttpClient _dialogHttpClient;
        private static FakeSpeechRecognizerFactory _mockSpeechReco;
        private static FakeSpeechSynth _mockSpeechSynth;
        private static IRealTimeProvider _realTime;
        private static IDialogTransportProtocol _transportProtocol;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _logger = new ConsoleLogger("Main", LogLevel.Err | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            _baseConfig = new InMemoryConfiguration(_logger.Clone("Config"));
            _mockFileSystem = new InMemoryFileSystem();
            _realTime = DefaultRealTimeProvider.Singleton;
            _mockDialogActionCache = new InMemoryCache<DialogAction>();
            _mockWebDataCache = new InMemoryCache<CachedWebData>();
            _mockConversationStateCache = new InMemoryConversationStateCache();
            _mockClientContextCache = new InMemoryCache<ClientContext>();
            _mockSpeechReco = new FakeSpeechRecognizerFactory(AudioSampleFormat.Mono(16000));
            _mockSpeechSynth = new FakeSpeechSynth(LanguageCode.EN_US);
            _transportProtocol = new DialogLZ4JsonTransportProtocol();

            _mockPluginLoader = new TestPluginLoader(new BasicDialogExecutor(true));
            _mockPluginProvider = new MachineLocalPluginProvider(
                _logger,
                _mockPluginLoader,
                NullFileSystem.Singleton,
                null,
                null,
                null,
                null,
                null,
                null,
                null);


            DialogConfiguration dialogEngineConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(_baseConfig));
            DialogEngineParameters dialogParams = new DialogEngineParameters(dialogEngineConfig, new WeakPointer<IDurandalPluginProvider>(_mockPluginProvider))
            {
                Logger = _logger,
                ConversationStateCache = new WeakPointer<IConversationStateCache>(_mockConversationStateCache),
                DialogActionCache = new WeakPointer<ICache<DialogAction>>(_mockDialogActionCache),
                WebDataCache = new WeakPointer<ICache<CachedWebData>>(_mockWebDataCache),
            };

            DialogProcessingEngine dialogCore = new DialogProcessingEngine(dialogParams);
            ISet<string> answersToLoad = new HashSet<string>();
            answersToLoad.Add("clientcaps");
            answersToLoad.Add("basictree");
            dialogCore.LoadPlugins(answersToLoad, _realTime).Await();

            _dialogHttpServer = new NullHttpServer();
            _dialogHttpClient = new DirectHttpClient(_dialogHttpServer);

            DialogWebConfiguration dialogWebConfig = DialogTestHelpers.GetTestDialogWebConfiguration(new WeakPointer<IConfiguration>(_baseConfig));

            DialogWebParameters dialogWebParams = new DialogWebParameters(dialogWebConfig, new WeakPointer<DialogProcessingEngine>(dialogCore))
            {
                Logger = _logger.Clone("DialogWebService"),
                FileSystem = _mockFileSystem,
                LuConnection = null,
                ClientContextCache = new WeakPointer<ICache<ClientContext>>(_mockClientContextCache),
                ConversationStateCache = _mockConversationStateCache,
                WebDataCache = new WeakPointer<ICache<CachedWebData>>(_mockWebDataCache),
                DialogActionStore = new WeakPointer<ICache<DialogAction>>(_mockDialogActionCache),
                HttpServer = _dialogHttpServer,
                ProcessingThreadPool = new WeakPointer<IThreadPool>(new TaskThreadPool()),
                CodecFactory = new AggregateCodecFactory(new RawPcmCodecFactory(), new OpusRawCodecFactory(_logger.Clone("OpusCodec")), new SquareDeltaCodecFactory()),
                SpeechReco = _mockSpeechReco,
                SpeechSynth = _mockSpeechSynth,
                RealTimeProvider = _realTime
            };

            _dialogEngine = DialogWebService.Create(dialogWebParams, CancellationToken.None).Await();
        }

        [TestInitialize]
        public void InitTest()
        {
            _mockClientContextCache.Clear();
            _mockWebDataCache.Clear();
            _mockDialogActionCache.Clear();
            _mockConversationStateCache.ClearAllConversationStates();
        }

        [ClassCleanup]
        public static void CleanupAllTests()
        {
            // Since we started an HTTP server in these tests, we need to stop them properly.
            _dialogEngine.Dispose();
            _dialogHttpServer.Dispose();
            _dialogHttpClient.Dispose();
            _mockSpeechReco.Dispose();
            _mockSpeechSynth.Dispose();
        }

        private static bool AudioIsNull(AudioData audio)
        {
            return audio == null || audio.Data == null || audio.Data.Count == 0;
        }

        /// <summary>
        /// Test that a client will full capabilities will recieve the proper answer data, with an HTML response
        /// </summary>
        [TestMethod]
        public async Task TestFullClientCapabilitiesHtml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.CanSynthesizeSpeech |
                                            ClientCapabilities.ClientActions |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsCompressedAudio;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger,
                null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseSsml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that a client will full capabilities will recieve the proper answer data, with a URL response
        /// </summary>
        [TestMethod]
        public async Task TestFullClientCapabilitiesUrl()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.CanSynthesizeSpeech |
                                            ClientCapabilities.ClientActions |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsCompressedAudio;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "url", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseSsml));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that a client will full capabilities will recieve the proper answer data, with an HTML response
        /// </summary>
        [TestMethod]
        public async Task TestFullClientCapabilitiesHtmlWithoutTts()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.ClientActions |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsCompressedAudio;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSsml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsFalse(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that a client which cannot serve its own HTML will get a URL response to display web data
        /// </summary>
        [TestMethod]
        public async Task TestDialogCoreWillServeHtml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseUrl));
        }

        /// <summary>
        /// Test that a client which CAN serve its own HTML will get an HTML response to display web data
        /// </summary>
        [TestMethod]
        public async Task TestDialogCoreWillPassthroughHtml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
        }

        /// <summary>
        /// Test that a client will NO capabilities will return no significant response
        /// </summary>
        [TestMethod]
        public async Task TestNoClientCapabilities()
        {
            ClientCapabilities capsToTest = ClientCapabilities.None;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that a client with no speakers will recieve no audio response
        /// </summary>
        [TestMethod]
        public async Task TestClientTextOnly()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
        }

        /// <summary>
        /// Test that a client with text + HTML support will recieve the text rendered as HTML
        /// </summary>
        [TestMethod]
        public async Task TestClientTextOnlyWithBasicHtml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayBasicHtml |
                                            ClientCapabilities.ServeHtml;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "textonly", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that a client with text + HTML5 support will recieve the text rendered as HTML
        /// </summary>
        [TestMethod]
        public async Task TestClientTextOnlyWithHtml5()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "textonly", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that a client with text + HTML support will NOT recieve the text rendered as HTML if it doesn't ask for it
        /// </summary>
        [TestMethod]
        public async Task TestClientTextOnlyNoHtml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.DisplayBasicHtml |
                                            ClientCapabilities.DoNotRenderTextAsHtml |
                                            ClientCapabilities.ServeHtml;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "textonly", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that a client with no display will recieve no visual response
        /// </summary>
        [TestMethod]
        public async Task TestClientAudioOnly()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsFalse(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that a client with no display will recieve no visual response
        /// </summary>
        [TestMethod]
        public async Task TestClientSsmlOnly()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.CanSynthesizeSpeech;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseSsml));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that a client which supports streaming audio will get a response when SSML is rendered server-side
        /// </summary>
        [TestMethod]
        public async Task TestClientStreamingSsml()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsStreamingAudio;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "textssml", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSsml));
            Assert.IsNotNull(serviceResponse.OutputAudioStream);
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that a client which supports streaming audio will get a response when audio is rendered server-side
        /// </summary>
        [TestMethod]
        public async Task TestClientStreamingAudio()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsStreamingAudio;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "audioonly", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSsml));
            Assert.IsNotNull(serviceResponse.OutputAudioStream);
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that a client which supports streaming audio will get a response when audio is rendered server-side
        /// </summary>
        [TestMethod]
        public async Task TestClientStreamingWithCustomAudio()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsStreamingAudio;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "customaudio", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSsml));
            Assert.IsNotNull(serviceResponse.OutputAudioStream);
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that compressed audio returned from a plugin will be transcoded into the client's preferred format
        /// </summary>
        [TestMethod]
        public async Task TestClientCompressedAudio()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsCompressedAudio;
            DialogRequest request = DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken);
            request.PreferredAudioCodec = OpusRawCodecFactory.CODEC_NAME;
            request.PreferredAudioFormat = CommonCodecParamHelper.CreateCodecParams(AudioSampleFormat.Mono(48000));
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "compressedaudio", 1.0f),
                request,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsFalse(AudioIsNull(response.ResponseAudio));
            Assert.AreEqual("opus", response.ResponseAudio.Codec);
            Assert.IsTrue(response.ResponseAudio.CodecParams.Contains("samplerate=48000"));
            Assert.IsTrue(response.ResponseAudio.Data.Count > 5000);
        }

        /// <summary>
        /// Test that compressed audio returned from a plugin will be transcoded into PCM which matches the client's preferred format
        /// </summary>
        [TestMethod]
        public async Task TestClientCompressedAudioAdaptsToUncompressedClient()
        {
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers;
            DialogRequest request = DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken);
            request.PreferredAudioCodec = RawPcmCodecFactory.CODEC_NAME_PCM_S16LE;
            request.PreferredAudioFormat = CommonCodecParamHelper.CreateCodecParams(AudioSampleFormat.Mono(8000));
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "compressedaudio", 1.0f),
                request,
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsFalse(AudioIsNull(response.ResponseAudio));
            Assert.AreEqual("pcm", response.ResponseAudio.Codec);
            Assert.IsTrue(response.ResponseAudio.CodecParams.Contains("samplerate=8000"));
            Assert.AreEqual(16000, response.ResponseAudio.Data.Count);
        }

        /// <summary>
        /// Test that a client with only an HTML display will only recieve HTML
        /// </summary>
        [TestMethod]
        public async Task TestClientHtmlOnly()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test the capabilities used by the Cortana interface client
        /// </summary>
        [TestMethod]
        public async Task TestCortanaCapabilities1()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.DisplayBasicText |
                                            ClientCapabilities.ClientActions |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.CanSynthesizeSpeech |
                                            ClientCapabilities.DoNotRenderTextAsHtml;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseSsml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test the capabilities used by the Cortana interface client
        /// </summary>
        [TestMethod]
        public async Task TestCortanaCapabilities2()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.DisplayBasicText |
                                            ClientCapabilities.ClientActions |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.CanSynthesizeSpeech |
                                            ClientCapabilities.DoNotRenderTextAsHtml;
            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "textssml", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseSsml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl));
            Assert.IsTrue(AudioIsNull(response.ResponseAudio));
        }

        /// <summary>
        /// Test that dialog action URLs can be invoked over an http client and trigger the proper actions
        /// </summary>
        [TestMethod]
        public async Task TestDirectDialogActionsOverHttp()
        {
            _mockConversationStateCache.ClearAllConversationStates();
            DialogHttpClient dialogClient = new DialogHttpClient(_dialogHttpClient, _logger, _transportProtocol);
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers;
            DialogRequest request = new DialogRequest()
            {
                ClientContext = DialogTestHelpers.GetTestClientContext(capsToTest),
                InteractionType = InputMethod.Programmatic,
                LanguageUnderstanding = new List<RecognizedPhrase>()
                {
                    new RecognizedPhrase()
                    {
                        Recognition = DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_dialogaction", 1.0f)
                    }
                }
            };

            NetworkResponseInstrumented<DialogResponse> netResponse = await dialogClient.MakeQueryRequest(request, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(netResponse);
            Assert.IsNotNull(netResponse.Response);
            DialogResponse response = netResponse.Response;

            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(response.ResponseData.ContainsKey("actionKey"));
            string actionId = response.ResponseData["actionKey"];

            request = new DialogRequest()
            {
                ClientContext = DialogTestHelpers.GetTestClientContext(capsToTest),
                InteractionType = InputMethod.Programmatic
            };

            NetworkResponseInstrumented<DialogResponse> actionResponse = await dialogClient.MakeDialogActionRequest(request, actionId, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(actionResponse);
            Assert.IsNotNull(actionResponse.Response);
            Assert.AreEqual(Result.Success, actionResponse.Response.ExecutionResult);
            Assert.AreEqual("turn2", actionResponse.Response.SelectedRecoResult.Intent);
        }

        /// <summary>
        /// Test that a client which supports streaming audio will get a response when audio is rendered server-side
        /// </summary>
        [TestMethod]
        public async Task TestStreamingAudioOverHttp()
        {
            DialogHttpClient dialogClient = new DialogHttpClient(_dialogHttpClient, _logger, _transportProtocol);
            ClientCapabilities capsToTest = ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.SupportsStreamingAudio;
            DialogRequest request = new DialogRequest()
            {
                ClientContext = DialogTestHelpers.GetTestClientContext(capsToTest),
                InteractionType = InputMethod.Spoken,
                LanguageUnderstanding = new List<RecognizedPhrase>()
                {
                    new RecognizedPhrase()
                    {
                        Recognition = DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "audioonly", 1.0f)
                    }
                }
            };

            NetworkResponseInstrumented<DialogResponse> netResponse = await dialogClient.MakeQueryRequest(request, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(netResponse);
            Assert.IsNotNull(netResponse.Response);
            DialogResponse response = netResponse.Response;

            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseUrl));
            Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSsml));
            Assert.IsFalse(string.IsNullOrEmpty(response.StreamingAudioUrl), "Response should have a streaming audio URL");

            AudioSample downloadedAudio = await TryGetStreamingResponseAudio(response.StreamingAudioUrl, dialogClient, _logger);

            Assert.IsNotNull(downloadedAudio, "Streaming audio must be non-null");
            Assert.IsTrue(downloadedAudio.Format.SampleRateHz == 16000, "Sample rate should have been parsed from response");
            Assert.IsTrue(downloadedAudio.LengthSamplesPerChannel >= 2500, "Should have read a significant payload from the audio endpoint");
        }

        /// <summary>
        /// That that "tactile with audio" dialog actions properly return audio data
        /// </summary>
        [TestMethod]
        public async Task TestTactileDialogActions()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.DisplayBasicText |
                                            ClientCapabilities.ClientActions |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.DoNotRenderTextAsHtml;

            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_dialogactiontactile", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Typed),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;

            Assert.IsNotNull(response.ResponseData);
            Assert.IsTrue(response.ResponseData.ContainsKey("actionKey"));
            string actionId = response.ResponseData["actionKey"];
            
            serviceResponse = await _dialogEngine.ProcessDialogAction(
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), string.Empty, InputMethod.Unknown),
                actionId,
                DefaultRealTimeProvider.Singleton);
            response = serviceResponse.ClientResponse;

            // Assert that for tactile-with-audio input, we get audio back
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.AreEqual("turn2", response.SelectedRecoResult.Intent);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsNull(serviceResponse.OutputAudioStream);
            Assert.AreEqual(0, response.ResponseAudio.Data.Count);
        }

        /// <summary>
        /// Test that "tactile with audio" dialog actions properly return audio data
        /// </summary>
        [TestMethod]
        public async Task TestTactileDialogActionsWithAudio()
        {
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml |
                                            ClientCapabilities.DisplayBasicText |
                                            ClientCapabilities.ClientActions |
                                            ClientCapabilities.HasInternetConnection |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers |
                                            ClientCapabilities.DoNotRenderTextAsHtml;

            DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("basictree", "start_dialogactiontactile", 1.0f),
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken),
                ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                _logger, null);
            DialogResponse response = serviceResponse.ClientResponse;

            Assert.IsTrue(response.ResponseData.ContainsKey("actionKey"));
            string actionId = response.ResponseData["actionKey"];

            serviceResponse = await _dialogEngine.ProcessDialogAction(
                DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), string.Empty, InputMethod.Unknown),
                actionId,
                DefaultRealTimeProvider.Singleton);

            response = serviceResponse.ClientResponse;

            // Assert that for tactile-with-audio input, we get audio back
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.AreEqual("turn2", response.SelectedRecoResult.Intent);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
            Assert.IsNull(serviceResponse.OutputAudioStream);
            Assert.IsNotNull(response.ResponseAudio);
            Assert.AreNotEqual(0, response.ResponseAudio.Data.Count);
        }

        /// <summary>
        /// Test that when plugins request single-use resources from the server, they are cached and transmitted properly
        /// </summary>
        [TestMethod]
        public async Task TestWebDataCache()
        {
            DialogHttpClient dialogClient = new DialogHttpClient(_dialogHttpClient, _logger, _transportProtocol);
            ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
                                            ClientCapabilities.ServeHtml;

            DialogRequest request = new DialogRequest()
            {
                ClientContext = DialogTestHelpers.GetTestClientContext(capsToTest),
                InteractionType = InputMethod.Spoken,
                LanguageUnderstanding = new List<RecognizedPhrase>()
                {
                    new RecognizedPhrase()
                    {
                        Recognition = DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "cache", 1.0f)
                    }
                }
            };

            using (NetworkResponseInstrumented<DialogResponse> netResponse = await dialogClient.MakeQueryRequest(request, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton))
            {
                try
                {
                    Assert.IsNotNull(netResponse);
                    Assert.IsNotNull(netResponse.Response);
                    DialogResponse response = netResponse.Response;

                    // Download the cached data and inspect it
                    Assert.IsNotNull(response.ResponseUrl);
                    string parsedBasePath;
                    string parsedFragment;
                    HttpFormParameters getParams;
                    Assert.IsTrue(HttpHelpers.TryParseRelativeUrl(response.ResponseUrl, out parsedBasePath, out getParams, out parsedFragment));
                    string key = getParams["data"];
                    Assert.IsTrue(_mockWebDataCache.ContainsKey(key));

                    using (HttpRequest httpReq = HttpRequest.CreateOutgoing(response.ResponseUrl, "GET"))
                    using (NetworkResponseInstrumented<HttpResponse> netResp = await _dialogHttpClient.SendInstrumentedRequestAsync(httpReq))
                    {
                        Assert.IsTrue(netResp.Success);
                        Assert.IsNotNull(netResp.Response);
                        string cachedData = await netResp.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.AreEqual("Cached data", cachedData);
                        await netResp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
                finally
                {
                    if (netResponse != null)
                    {
                        await netResponse.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }

            // Make sure it was deleted from the cache after access
            // Assert.IsFalse(_mockWebDataCache.ContainsKey(key));
        }

        /// <summary>
        /// Test that when we invoke direct dialog actions, the conversation state is not persisted
        /// </summary>
        [TestMethod]
        public async Task TestDirectDialogActionsDontPersistState()
        {
            DialogHttpClient dialogClient = new DialogHttpClient(_dialogHttpClient, _logger, _transportProtocol);
            ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                            ClientCapabilities.HasMicrophone |
                                            ClientCapabilities.HasSpeakers;
            DialogRequest request = new DialogRequest()
            {
                ClientContext = DialogTestHelpers.GetTestClientContext(capsToTest),
                InteractionType = InputMethod.Programmatic,
                LanguageUnderstanding = new List<RecognizedPhrase>()
                {
                    new RecognizedPhrase()
                    {
                        Recognition = DialogTestHelpers.GetSimpleRecoResultList("basictree", "start", 1.0f)
                    }
                }
            };

            NetworkResponseInstrumented<DialogResponse> netResponse = await dialogClient.MakeQueryRequest(request, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(netResponse);
            Assert.IsNotNull(netResponse.Response);
            DialogResponse response = netResponse.Response;

            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.AreEqual("start", response.SelectedRecoResult.Intent);

            request = new DialogRequest()
            {
                ClientContext = DialogTestHelpers.GetTestClientContext(capsToTest),
                InteractionType = InputMethod.Programmatic,
                LanguageUnderstanding = new List<RecognizedPhrase>()
                {
                    new RecognizedPhrase()
                    {
                        Recognition = DialogTestHelpers.GetSimpleRecoResultList("basictree", "turn2", 1.0f)
                    }
                }
            };

            netResponse = await dialogClient.MakeQueryRequest(request, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(netResponse);
            Assert.IsNotNull(netResponse.Response);
            response = netResponse.Response;

            // We should not have been able to reach turn 2 programmatically, so response should have been skip
            Assert.AreEqual(Result.Skip, response.ExecutionResult);
        }

        //[TestMethod]
        //public async Task TestSpeechRerankerWorksFine()
        //{
        //    ClientCapabilities capsToTest = ClientCapabilities.DisplayHtml5 |
        //                                    ClientCapabilities.ServeHtml |
        //                                    ClientCapabilities.DisplayBasicText |
        //                                    ClientCapabilities.ClientActions |
        //                                    ClientCapabilities.HasInternetConnection |
        //                                    ClientCapabilities.HasMicrophone |
        //                                    ClientCapabilities.HasSpeakers |
        //                                    ClientCapabilities.DoNotRenderTextAsHtml;

        //    DialogRequest request = DialogTestHelpers.GetSimpleClientRequest(DialogTestHelpers.GetTestClientContext(capsToTest), "this is a unit test", InputMethod.Spoken);
        //    DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRegularQuery(request, null);
        //    DialogResponse response = serviceResponse.ClientResponse;

        //    Assert.AreEqual(Result.Success, response.ExecutionResult);
        //    Assert.AreEqual("turn2", response.SelectedRecoResult.Intent);
        //    Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
        //    Assert.IsFalse(string.IsNullOrEmpty(response.ResponseHtml));
        //    Assert.IsNull(serviceResponse.OutputAudioStream);
        //    Assert.IsNotNull(response.ResponseAudio);
        //    Assert.AreNotEqual(0, response.ResponseAudio.Data.Count);
        //}

        private async Task<AudioSample> TryGetStreamingResponseAudio(string audioUrl, IDialogClient dialogClient, ILogger logger)
        {
            using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (IAudioDataSource streamResult = await dialogClient.GetStreamingAudioResponse(audioUrl))
            {
                RawPcmCodecFactory pcmCodec = new RawPcmCodecFactory();
                if (!pcmCodec.CanDecode(streamResult.Codec))
                {
                    throw new FormatException("Response audio stream came in unexpected codec \"" + streamResult.Codec + "\"");
                }

                AudioDecoder decoder = pcmCodec.CreateDecoder(streamResult.Codec, streamResult.CodecParams, new WeakPointer<IAudioGraph>(audioGraph), logger, "ClientPcmDecoder");
                AudioInitializationResult initResult = await decoder.Initialize(streamResult.AudioDataReadStream, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                if (initResult != AudioInitializationResult.Success)
                {
                    throw new FormatException("PCM codec failed to initialize");
                }

                using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(audioGraph), decoder.OutputFormat, "ClientResponseBucket"))
                {
                    decoder.ConnectOutput(bucket);
                    await bucket.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    return bucket.GetAllAudio();
                }
            }
        }

        /// <summary>
        /// Runs a lot of conversations with lots of clients simultaneously to make sure nothing explodes
        /// </summary>
        [TestMethod]
        public void TestServerThreadSafety()
        {
            const int numTasks = 1000;
            StressTestThread[] tasks = new StressTestThread[numTasks];
            using (IThreadPool threadPool = new CustomThreadPool(_logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ThreadPool", 50, false))
            {
                for (int task = 0; task < numTasks; task++)
                {
                    tasks[task] = new StressTestThread();
                    threadPool.EnqueueUserAsyncWorkItem(tasks[task].Run);
                }

                for (int task = 0; task < numTasks; task++)
                {
                    DialogResponse response = tasks[task].Join(30000);
                    Assert.AreEqual(Result.Success, response.ExecutionResult, "Execution result should be success");
                    Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText), "Should have response text");
                    Assert.IsTrue(string.IsNullOrEmpty(response.ResponseHtml), "Should not have response html");
                    Assert.IsTrue(string.IsNullOrEmpty(response.ResponseSsml), "Should not have response ssml");
                    Assert.IsFalse(string.IsNullOrEmpty(response.ResponseUrl), "Should have response url");
                    Assert.IsTrue(string.IsNullOrEmpty(response.StreamingAudioUrl), "Should not have audio URL");
                    Assert.IsFalse(AudioIsNull(response.ResponseAudio), "Should have audio to play");
                }
            }
        }

        private class StressTestThread
        {
            private readonly ManualResetEventSlim _finished = new ManualResetEventSlim(false);
            private DialogResponse _returnVal;

            public async Task Run()
            {
                try
                {
                    ClientCapabilities capsToTest = ClientCapabilities.DisplayUnlimitedText |
                                      ClientCapabilities.DisplayHtml5 |
                                      ClientCapabilities.ClientActions |
                                      ClientCapabilities.HasInternetConnection |
                                      ClientCapabilities.HasMicrophone |
                                      ClientCapabilities.HasSpeakers |
                                      ClientCapabilities.SupportsCompressedAudio;
                    ClientContext context = DialogTestHelpers.GetTestClientContext(capsToTest);
                    context.ClientId = Guid.NewGuid().ToString("N");
                    context.UserId = Guid.NewGuid().ToString("N");

                    DialogWebServiceResponse serviceResponse = await _dialogEngine.ProcessRecoResults(DialogTestHelpers.GetSimpleRecoResultList("clientcaps", "html", 1.0f),
                        DialogTestHelpers.GetSimpleClientRequest(context, "this is a unit test", InputMethod.Spoken),
                        ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                        _logger);

                    _returnVal = serviceResponse.ClientResponse;
                }
                finally
                {
                    _finished.Set();
                }
            }

            public DialogResponse Join(int maxTime)
            {
                if (_finished.Wait(maxTime))
                {
                    return _returnVal;
                }
                else
                {
                    throw new AbandonedMutexException("Thread did not complete in time");
                }
            }
        }
    }
}
