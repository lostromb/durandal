using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Components;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Annotation;
using Durandal.Common.NLP.Language;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.Ontology;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.SR.Azure;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Speech.TTS.Bing;
using Durandal.Common.Statistics;
using Durandal.Common.Time;
using Durandal.ExternalServices;
using Durandal.ExternalServices.Bing;
using Durandal.ExternalServices.Bing.Search;
using Durandal.ExternalServices.Bing.Search.Schemas;
using Durandal.ExternalServices.Bing.Speller;
using Durandal.ExternalServices.Darksky;
using Durandal.Tests.EntitySchemas;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Integration
{
    [TestClass]
    public class ExternalServiceTests
    {
        private static string _speechApiKey;
        private static string _spellerApiKey;
        private static string _mapsApiKey;
        private static string _translateApiKey;
        private static string _bingInternalSearchApiKey;
        private static string _bingPublicSearchApiKey;
        private static string _darkskyApiKey;
        private static ILogger _logger;
        private static IHttpClientFactory _clientFactory;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);
            _clientFactory = new PortableHttpClientFactory();
            _speechApiKey = context.Properties["BingSpeechApiKey"]?.ToString();
            _spellerApiKey = context.Properties["BingSpellerApiKey"]?.ToString();
            _mapsApiKey = context.Properties["BingMapsApiKey"]?.ToString();
            _translateApiKey = context.Properties["BingTranslateApiKey"]?.ToString();
            _bingPublicSearchApiKey = context.Properties["BingSearchApiKey"]?.ToString();
            _bingInternalSearchApiKey = context.Properties["BingSearchApiKeyInternal"]?.ToString();
            _darkskyApiKey = context.Properties["DarkskyApiKey"]?.ToString();
        }

        //[TestMethod]
        //[Ignore]
        //[TestCategory("ExternalService")]
        //[DeploymentItem("TestData/ThisIsATest.opus")]
        //public async Task TestCortanaSpeechRecoTcpClient()
        //{
        //    if (string.IsNullOrWhiteSpace(_speechApiKey))
        //    {
        //        Assert.Inconclusive("No API key provided in test settings");
        //    }

        //    ISocketFactory socketFactory = new TcpClientSocketFactory(_logger.Clone("SRSocketFactory"));
        //    CortanaSpeechRecognizerFactory srFactory = new CortanaSpeechRecognizerFactory(socketFactory, _logger.Clone("SRFactory"), _speechApiKey, DefaultRealTimeProvider.Singleton);
        //    using (ISpeechRecognizer sr = await srFactory.CreateRecognitionStream("en-US", _logger.Clone("SRStream"), DefaultRealTimeProvider.Singleton))
        //    {
        //        Assert.IsNotNull(sr);
        //        using (FileStream audioFileIn = new FileStream("ThisIsATest.opus", FileMode.Open, FileAccess.Read))
        //        {
        //            OpusDecoder decoder = new OpusDecoder(16000, 1);
        //            OpusOggReadStream readStream = new OpusOggReadStream(decoder, audioFileIn);
        //            while (readStream.HasNextPacket)
        //            {
        //                short[] nextPacket = readStream.DecodeNextPacket();
        //                if (nextPacket == null || nextPacket.Length == 0)
        //                {
        //                    continue;
        //                }

        //                AudioChunk chunk = new AudioChunk(nextPacket, decoder.SampleRate);
        //                await sr.ContinueUnderstandSpeech(chunk);
        //            }
        //            Durandal.API.SpeechRecognitionResult recoResults = await sr.FinishUnderstandSpeech();
        //            Assert.IsNotNull(recoResults);
        //            Assert.IsTrue(recoResults.RecognizedPhrases.Count > 0);
        //        }
        //    }
        //}

        //[TestMethod]
        //[Ignore]
        //[TestCategory("ExternalService")]
        //[DeploymentItem("TestData/ThisIsATest.opus")]
        //public async Task TestCortanaSpeechRecoWin32Socket()
        //{
        //    if (string.IsNullOrWhiteSpace(_speechApiKey))
        //    {
        //        Assert.Inconclusive("No API key provided in test settings");
        //    }

        //    ISocketFactory socketFactory = new Win32SocketFactory(_logger.Clone("SRSocketFactory"));
        //    CortanaSpeechRecognizerFactory srFactory = new CortanaSpeechRecognizerFactory(socketFactory, _logger.Clone("SRFactory"), _speechApiKey, DefaultRealTimeProvider.Singleton);
        //    using (ISpeechRecognizer sr = await srFactory.CreateRecognitionStream("en-US", _logger.Clone("SRStream"), DefaultRealTimeProvider.Singleton))
        //    {
        //        Assert.IsNotNull(sr);
        //        using (FileStream audioFileIn = new FileStream("ThisIsATest.opus", FileMode.Open, FileAccess.Read))
        //        {
        //            OpusDecoder decoder = new OpusDecoder(16000, 1);
        //            OpusOggReadStream readStream = new OpusOggReadStream(decoder, audioFileIn);
        //            while (readStream.HasNextPacket)
        //            {
        //                short[] nextPacket = readStream.DecodeNextPacket();
        //                if (nextPacket == null || nextPacket.Length == 0)
        //                {
        //                    continue;
        //                }

        //                AudioChunk chunk = new AudioChunk(nextPacket, decoder.SampleRate);
        //                await sr.ContinueUnderstandSpeech(chunk);
        //            }
        //            Durandal.API.SpeechRecognitionResult recoResults = await sr.FinishUnderstandSpeech();
        //            Assert.IsNotNull(recoResults);
        //            Assert.IsTrue(recoResults.RecognizedPhrases.Count > 0);
        //        }
        //    }
        //}
        
        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestBingTts()
        {
            if (string.IsNullOrWhiteSpace(_speechApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }

            NLPToolsCollection nlTools = new NLPToolsCollection();
            nlTools.Add(LanguageCode.EN_US,
                new NLPTools()
                {
                    WordBreaker = new EnglishWholeWordBreaker(),
                    SpeechTimingEstimator = new EnglishSpeechTimingEstimator()
                });

            using (ISpeechSynth synth = new BingSpeechSynth(
                _logger.Clone("BingTTS"),
                _speechApiKey,
                new PortableHttpClientFactory(),
                DefaultRealTimeProvider.Singleton, nlTools))
            {
                Assert.IsTrue(synth.IsLocaleSupported(LanguageCode.EN_US));
                SpeechSynthesisRequest synthRequest = new SpeechSynthesisRequest()
                {
                    Plaintext = "Graham is my train name",
                    Locale = LanguageCode.EN_US,
                    VoiceGender = VoiceGender.Female
                };

                SynthesizedSpeech data = await synth.SynthesizeSpeechAsync(synthRequest, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.IsNotNull(data);
                Assert.IsNotNull(data.Audio);
                Assert.IsNotNull(data.Audio.Data);
                Assert.IsNotNull(data.Audio.Data.Array);
                Assert.IsTrue(data.Audio.Data.Count > 16000);
                Assert.IsNotNull(data.Words);
                Assert.AreEqual(5, data.Words.Count);
            }
        }
        
        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestBingSpeller()
        {
            if (string.IsNullOrWhiteSpace(_spellerApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }
            
            BingSpeller speller = new BingSpeller(_spellerApiKey, _clientFactory, _logger);
            IList<Hypothesis<string>> spellingHyps = await speller.SpellCorrect("you recieve this thing", LanguageCode.EN_US, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(spellingHyps);
            Assert.IsTrue(spellingHyps.Count > 0);
            Assert.AreEqual("you receive this thing", spellingHyps[0].Value);
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestPortableHttp()
        {
            ILogger logger = new ConsoleLogger();
            IHttpClientFactory factory = new PortableHttpClientFactory();
            IHttpClient httpClient = factory.CreateHttpClient("marathon.bungie.org", 80, false, logger);
            using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
            using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger))
            {
                try
                {
                    Assert.IsNotNull(responseWrapper);
                    Assert.IsTrue(responseWrapper.Success);
                    HttpResponse response = responseWrapper.Response;
                    Assert.IsTrue(response.ResponseCode == 200 || response.ResponseCode == 301 || response.ResponseCode == 302);
                    ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(byteData.Count > 1000);
                }
                finally
                {
                    if (responseWrapper != null)
                    {
                        await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestPortableHttps()
        {
            ILogger logger = new ConsoleLogger();
            IHttpClientFactory factory = new PortableHttpClientFactory();
            IHttpClient httpClient = factory.CreateHttpClient("marathon.bungie.org", 443, true, logger);
            using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
            using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger))
            {
                try
                {
                    Assert.IsNotNull(responseWrapper);
                    Assert.IsTrue(responseWrapper.Success);
                    HttpResponse response = responseWrapper.Response;
                    Assert.AreEqual(200, response.ResponseCode);
                    ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(byteData.Count > 1000);
                }
                finally
                {
                    if (responseWrapper != null)
                    {
                        await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestTcpClientSocketHttp1_1()
        {
            using (Http2SessionManager httpSessionManager = new Http2SessionManager())
            {
                ISocketFactory socketFactory = new TcpClientSocketFactory(_logger);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 80, false),
                    _logger,
                    new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(httpSessionManager),
                    new Http2SessionPreferences());
                httpClient.InitialProtocolVersion = HttpVersion.HTTP_1_1;
                using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger))
                {
                    try
                    {
                        Assert.IsNotNull(responseWrapper);
                        Assert.IsTrue(responseWrapper.Success);
                        HttpResponse response = responseWrapper.Response;
                        Assert.IsTrue(response.ResponseCode == 200 || response.ResponseCode == 302);
                        ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(byteData.Count > 1000);
                    }
                    finally
                    {
                        if (responseWrapper != null)
                        {
                            await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestTcpClientSocketHttp2()
        {
            using (Http2SessionManager httpSessionManager = new Http2SessionManager())
            {
                ISocketFactory socketFactory = new TcpClientSocketFactory(_logger);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 80, false),
                    _logger,
                    new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(httpSessionManager),
                    new Http2SessionPreferences());
                httpClient.InitialProtocolVersion = HttpVersion.HTTP_2_0;
                using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger))
                {
                    try
                    {
                        Assert.IsNotNull(responseWrapper);
                        Assert.IsTrue(responseWrapper.Success);
                        HttpResponse response = responseWrapper.Response;
                        Assert.IsTrue(response.ResponseCode == 200 || response.ResponseCode == 302);
                        ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(byteData.Count > 1000);
                    }
                    finally
                    {
                        if (responseWrapper != null)
                        {
                            await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestTcpClientSocketHttps1_1()
        {
            using (Http2SessionManager httpSessionManager = new Http2SessionManager())
            {
                ISocketFactory socketFactory = new TcpClientSocketFactory(_logger);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 443, true),
                    _logger,
                    new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(httpSessionManager),
                    new Http2SessionPreferences());
                httpClient.InitialProtocolVersion = HttpVersion.HTTP_1_1;
                using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger))
                {
                    try
                    {
                        Assert.IsNotNull(responseWrapper);
                        Assert.IsTrue(responseWrapper.Success);
                        HttpResponse response = responseWrapper.Response;
                        Assert.AreEqual(200, response.ResponseCode);
                        ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(byteData.Count > 1000);
                    }
                    finally
                    {
                        if (responseWrapper != null)
                        {
                            await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestTcpClientSocketHttpsDecoupled()
        {
            using (Http2SessionManager httpSessionManager = new Http2SessionManager())
            {
                const string remoteHost = "www.bing.com";
                System.Net.IPAddress[] bingAddresses = await System.Net.Dns.GetHostAddressesAsync(remoteHost).ConfigureAwait(false);
                Assert.IsNotNull(bingAddresses, "Could not resolve DNS address for " + remoteHost);
                IList<System.Net.IPAddress> ipv4Addresses = bingAddresses
                    .Where((t) => t.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .ToList();
                Assert.AreNotEqual(0, ipv4Addresses.Count, "Could not resolve IPv4 addresses for " + remoteHost);

                ILogger logger = new ConsoleLogger();
                TcpConnectionConfiguration config = new TcpConnectionConfiguration()
                {
                    DnsHostname = ipv4Addresses[0].ToString(),
                    Port = 443,
                    UseTLS = true,
                    SslHostname = remoteHost
                };

                HttpRequest request = HttpRequest.CreateOutgoing("/", "GET");
                using (ISocketFactory socketFactory = new TcpClientSocketFactory(logger, System.Security.Authentication.SslProtocols.None, ignoreCertErrors: false))
                using (IHttpClient httpClient = new SocketHttpClient(
                        new WeakPointer<ISocketFactory>(socketFactory),
                        config,
                        logger,
                        new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                        DimensionSet.Empty,
                        new WeakPointer<IHttp2SessionManager>(httpSessionManager),
                        new Http2SessionPreferences()))
                {
                    httpClient.InitialProtocolVersion = HttpVersion.HTTP_1_1;
                    using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger))
                    {
                        try
                        {
                            Assert.IsNotNull(responseWrapper);
                            Assert.IsTrue(responseWrapper.Success);
                            HttpResponse response = responseWrapper.Response;
                            Assert.AreEqual(200, response.ResponseCode);
                            ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            Assert.IsTrue(byteData.Count > 0);
                        }
                        finally
                        {
                            if (responseWrapper != null)
                            {
                                await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestWin32SocketHttp()
        {
            using (Http2SessionManager httpSessionManager = new Http2SessionManager())
            {
                ILogger logger = new ConsoleLogger();
                ISocketFactory socketFactory = new Win32SocketFactory(logger);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 80, false),
                    logger,
                    new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(httpSessionManager),
                    new Http2SessionPreferences());

                using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger))
                {
                    try
                    {
                        Assert.IsNotNull(responseWrapper);
                        Assert.IsTrue(responseWrapper.Success);
                        HttpResponse response = responseWrapper.Response;
                        Assert.IsTrue(response.ResponseCode == 200 || response.ResponseCode == 302);
                        ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(byteData.Count > 1000);
                    }
                    finally
                    {
                        if (responseWrapper != null)
                        {
                            await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestWin32SocketHttps()
        {
            using (Http2SessionManager httpSessionManager = new Http2SessionManager())
            {
                ILogger logger = new ConsoleLogger();
                ISocketFactory socketFactory = new Win32SocketFactory(logger);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 443, true),
                    logger,
                    new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(httpSessionManager),
                    new Http2SessionPreferences());
                using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger))
                {
                    try
                    {
                        Assert.IsNotNull(responseWrapper);
                        Assert.IsTrue(responseWrapper.Success);
                        HttpResponse response = responseWrapper.Response;
                        Assert.AreEqual(200, response.ResponseCode);
                        ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(byteData.Count > 1000);
                    }
                    finally
                    {
                        if (responseWrapper != null)
                        {
                            await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestLocationAnnotatorIntegration()
        {
            if (string.IsNullOrWhiteSpace(_mapsApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }

            LocationEntityAnnotator annotator = new LocationEntityAnnotator(_mapsApiKey, _clientFactory, _logger);
            Assert.IsTrue(annotator.Initialize());

            KnowledgeContext entityContext = new KnowledgeContext();

            RecoResult rr = new RecoResult()
            {
                Confidence = 0.95f,
                Domain = "places",
                Intent = "find",
                Source = "LU",
                Utterance = new Sentence("where is london"),
                TagHyps = new List<TaggedData>()
                {
                    new TaggedData()
                    {
                        Utterance = "where is london",
                        Annotations = new Dictionary<string, string>(),
                        Confidence = 0.95f,
                        Slots = new List<SlotValue>()
                        {
                            new SlotValue("place", "london", SlotValueFormat.SpokenText)
                        }
                    }
                }
            };

            LURequest luRequest = new LURequest()
            {
                Context = new ClientContext()
                {
                    Locale = LanguageCode.EN_US,
                    ClientId = "test",
                    UserId = "test",
                    Capabilities = ClientCapabilities.DisplayUnlimitedText,
                    ClientName = "unit test"
                }
            };

            IConfiguration modelConfig = new InMemoryConfiguration(_logger);
            modelConfig.Set("SlotAnnotator_LocationEntity", new string[] { "find/place" });

            object state = await annotator.AnnotateStateless(rr, luRequest, modelConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await annotator.CommitAnnotation(state, rr, luRequest, entityContext, modelConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);

            // Verify that a place entity came back
            SlotValue annotatedSlot = DialogHelpers.TryGetSlot(rr, "place");
            IList<ContextualEntity> placeEntities = annotatedSlot.GetEntities(entityContext);
            Assert.AreNotEqual(0, placeEntities.Count);
            Assert.IsTrue(placeEntities[0].Entity.IsA<Place>());
            Place resolvedPlace = placeEntities[0].Entity.As<Place>();
            
            Assert.AreEqual("London, Greater London, United Kingdom", resolvedPlace.Name.Value);
            PostalAddress address = resolvedPlace.Address_as_PostalAddress.ValueInMemory;
            Assert.IsNotNull(address);
            Assert.AreEqual("England", address.AddressRegion.Value);
            Assert.AreEqual("United Kingdom", address.AddressCountry_as_string.Value);
            GeoCoordinates coords = resolvedPlace.Geo_as_GeoCoordinates.ValueInMemory;
            Assert.IsNotNull(coords);
            Assert.IsTrue(coords.Latitude_as_number.Value.HasValue);
            Assert.IsTrue(coords.Longitude_as_number.Value.HasValue);
            GeoShape shape = resolvedPlace.Geo_as_GeoShape.ValueInMemory; 
            Assert.IsNotNull(shape);
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestBingTranslateLanguageDetect()
        {
            if (string.IsNullOrWhiteSpace(_translateApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }
            
            BingTranslator translator = new BingTranslator(_translateApiKey, _logger, _clientFactory, DefaultRealTimeProvider.Singleton);
            LanguageCode lang = await translator.DetectLanguage("This is a test of the emergency broadcast system.", _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.AreEqual(LanguageCode.ENGLISH, lang);
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestBingTranslate()
        {
            if (string.IsNullOrWhiteSpace(_translateApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }
            
            BingTranslator translator = new BingTranslator(_translateApiKey, _logger, _clientFactory, DefaultRealTimeProvider.Singleton);
            string translated = await translator.TranslateText(
                "This is a test of the emergency broadcast system.",
                _logger,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton,
                LanguageCode.Parse("ru"));
            ISet<string> validTranslations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Это тест системы экстренного вещания.",
                "Это тест системы аварийного вещания."
            };

            Assert.IsTrue(validTranslations.Contains(translated), $"{translated} is not found in the valid translations list.");
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestBingSearchCalculationInternalApi()
        {
            if (string.IsNullOrWhiteSpace(_bingInternalSearchApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }

            BingSearch search = new BingSearch(_bingInternalSearchApiKey, _clientFactory, _logger, BingApiVersion.V7Internal);
            BingResponse response = await search.Query("what is 2 + 2", _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton, LanguageCode.EN_US);
            Assert.IsNotNull(response);
            Assert.AreEqual("2 + 2", response.Computation.Expression);
            Assert.AreEqual("4", response.Computation.Value);
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestBingSearchCurrencyConvertInternalApi()
        {
            if (string.IsNullOrWhiteSpace(_bingInternalSearchApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }

            BingSearch search = new BingSearch(_bingInternalSearchApiKey, _clientFactory, _logger, BingApiVersion.V7Internal);
            BingResponse response = await search.Query("convert 10 dollars to euros", _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton, LanguageCode.EN_US);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Currency);
            Assert.IsNotNull(response.Currency.Value);
            Assert.AreEqual("USD", response.Currency.Value.FromCurrency);
            Assert.AreEqual("EUR", response.Currency.Value.ToCurrency);
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestBingSearchCalculationPublicApi()
        {
            if (string.IsNullOrWhiteSpace(_bingPublicSearchApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }

            BingSearch search = new BingSearch(_bingPublicSearchApiKey, _clientFactory, _logger, BingApiVersion.V7);
            BingResponse response = await search.Query("what is 2 + 2", _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton, LanguageCode.EN_US);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Computation);
            Assert.AreEqual("2 + 2", response.Computation.Expression);
            Assert.AreEqual("4", response.Computation.Value);
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestBingSearchEntitiesInternalApi()
        {
            if (string.IsNullOrWhiteSpace(_bingInternalSearchApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }

            BingSearch search = new BingSearch(_bingInternalSearchApiKey, _clientFactory, _logger, BingApiVersion.V7Internal);
            BingResponse response = await search.Query("tokyo", _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton, LanguageCode.EN_US);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.EntityReferences.Count);
            Entity entity = response.KnowledgeContext.GetEntityInMemory(response.EntityReferences[0]);
            Assert.IsNotNull(entity);
            Assert.IsTrue(entity.IsA<Place>());
            Place tokyo = entity.As<Place>();
            Assert.AreEqual("Tokyo", tokyo.Name.Value);
        }

        //[TestMethod]
        //[TestCategory("ExternalService")]
        //public async Task TestDarkskyGetForecastIntegration()
        //{
        //    if (string.IsNullOrWhiteSpace(_darkskyApiKey))
        //    {
        //        Assert.Inconclusive("No API key provided in test settings");
        //    }

        //    DarkskyApi api = new DarkskyApi(_clientFactory, _logger, _darkskyApiKey);
        //    GeoCoordinate greenville = new GeoCoordinate(35.623341, -77.434192);
        //    DarkskyWeatherResult result = await api.GetWeatherData(greenville, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger, DarkskyRequestFeatures.CurrentWeather, "en", DarkskyMeasurementUnit.SI);
        //    Assert.IsNotNull(result);
        //    Assert.IsNotNull(result.Currently);
        //    Assert.IsTrue(result.Currently.Temperature.HasValue);
        //}

        [TestMethod]
        [TestCategory("ExternalService")]
        [DeploymentItem("TestData/ThisIsATest.opus")]
        public async Task TestAzureSpeechRecoSystemWebSocketClient()
        {
            if (string.IsNullOrWhiteSpace(_speechApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }

            IWebSocketClientFactory socketFactory = new SystemWebSocketClientFactory();
            IHttpClientFactory tokenRefreshClientFactory = new PortableHttpClientFactory();
            AzureSpeechRecognizerFactory srFactory = new AzureSpeechRecognizerFactory(
                tokenRefreshClientFactory,
                socketFactory,
                _logger.Clone("SRFactory"),
                _speechApiKey,
                DefaultRealTimeProvider.Singleton);

            using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream audioFileIn = new FileStream("ThisIsATest.opus", FileMode.Open, FileAccess.Read))
            using (AudioDecoder opusDecoder = new OggOpusDecoder(audioGraph, null, null, new ManagedOpusCodecProvider()))
            using (ISpeechRecognizer sr = await srFactory.CreateRecognitionStream(audioGraph, null, LanguageCode.EN_US, _logger.Clone("SRStream"), CancellationToken.None, DefaultRealTimeProvider.Singleton))
            {
                AudioInitializationResult initResult = await opusDecoder.Initialize(new NonRealTimeStreamWrapper(audioFileIn, false), false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(AudioInitializationResult.Success, initResult);
                using (AudioConformer conformer = new AudioConformer(audioGraph, opusDecoder.OutputFormat, sr.InputFormat, null, ResamplerFactory.Default, DebugLogger.Default))
                {
                    opusDecoder.ConnectOutput(conformer);
                    conformer.ConnectOutput(sr);
                    await opusDecoder.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Durandal.API.SpeechRecognitionResult recoResults = await sr.FinishUnderstandSpeech(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.IsNotNull(recoResults);
                    Assert.IsTrue(recoResults.RecognizedPhrases.Count > 0);
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        [DeploymentItem("TestData/ThisIsATest.opus")]
        public async Task TestAzureSpeechRecoDurandalWebSocketClient()
        {
            if (string.IsNullOrWhiteSpace(_speechApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }

            ISocketFactory socketFactory = new Win32SocketFactory(_logger.Clone("SRSocketFactory"));
            IWebSocketClientFactory webSocketFactory = new WebSocketClientFactory(
                new WeakPointer<ISocketFactory>(socketFactory),
                Http2SessionManager.Default,
                new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                DimensionSet.Empty,
                new FastRandom());
            IHttpClientFactory tokenRefreshClientFactory = new PortableHttpClientFactory();
            AzureSpeechRecognizerFactory srFactory = new AzureSpeechRecognizerFactory(tokenRefreshClientFactory, webSocketFactory, _logger.Clone("SRFactory"), _speechApiKey, DefaultRealTimeProvider.Singleton);

            using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream audioFileIn = new FileStream("ThisIsATest.opus", FileMode.Open, FileAccess.Read))
            using (AudioDecoder opusDecoder = new OggOpusDecoder(audioGraph, null, null, new ManagedOpusCodecProvider()))
            using (ISpeechRecognizer sr = await srFactory.CreateRecognitionStream(audioGraph, null, LanguageCode.EN_US, _logger.Clone("SRStream"), CancellationToken.None, DefaultRealTimeProvider.Singleton))
            {
                AudioInitializationResult initResult = await opusDecoder.Initialize(new NonRealTimeStreamWrapper(audioFileIn, false), false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(AudioInitializationResult.Success, initResult);
                using (AudioConformer conformer = new AudioConformer(audioGraph, opusDecoder.OutputFormat, sr.InputFormat, null, ResamplerFactory.Default, DebugLogger.Default))
                {
                    opusDecoder.ConnectOutput(conformer);
                    conformer.ConnectOutput(sr);
                    await opusDecoder.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Durandal.API.SpeechRecognitionResult recoResults = await sr.FinishUnderstandSpeech(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.IsNotNull(recoResults);
                    Assert.IsTrue(recoResults.RecognizedPhrases.Count > 0);
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestLyricsAPI()
        {
            using (Http2SessionManager httpSessionManager = new Http2SessionManager())
            {
                ILogger logger = new ConsoleLogger();
                ISocketFactory socketFactory = new TcpClientSocketFactory(logger.Clone("SocketFactory"), ignoreCertErrors: true);
                IHttpClientFactory httpFactory = new SocketHttpClientFactory(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(httpSessionManager),
                    new Http2SessionPreferences());
                LyricsAPI api = new LyricsAPI(httpFactory, logger);
                string lyrics = await api.FetchLyrics("Billy Joel", "Uptown Girl", CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.IsNotNull(lyrics);
                logger.Log(lyrics);
                Assert.IsTrue(lyrics.ToLowerInvariant().Contains("uptown world"));
            }
        }
    }
}
