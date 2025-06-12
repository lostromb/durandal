using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Components;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Speech.SR;
using Durandal.Common.Time;
using Durandal.Extensions.CognitiveServices.Speech;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Integration
{
    [TestClass]
    public class AzureSpeechTests
    {
        private static string _speechApiKey;
        private static ILogger _logger;
        private static IHttpClientFactory _clientFactory;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);
            _clientFactory = new PortableHttpClientFactory();
            _speechApiKey = context.Properties["BingSpeechApiKey"]?.ToString();
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        [DeploymentItem("TestData/ThisIsATest.opus")]
        public async Task TestAzureSpeechRecoTcpClient()
        {
            if (string.IsNullOrWhiteSpace(_speechApiKey))
            {
                Assert.Inconclusive("No API key provided in test settings");
            }

            ISocketFactory socketFactory = new TcpClientSocketFactory(_logger.Clone("SRSocketFactory"));
            IHttpClientFactory tokenRefreshClientFactory = new PortableHttpClientFactory();
            AzureNativeSpeechRecognizerFactory srFactory = new AzureNativeSpeechRecognizerFactory(tokenRefreshClientFactory, _logger.Clone("SRFactory"), _speechApiKey, DefaultRealTimeProvider.Singleton);

            using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream audioFileIn = new FileStream("ThisIsATest.opus", FileMode.Open, FileAccess.Read))
            using (AudioDecoder opusDecoder = new OggOpusDecoder(new WeakPointer<IAudioGraph>(audioGraph), null, _logger.Clone("OpusCodec"), null))
            using (ISpeechRecognizer sr = await srFactory.CreateRecognitionStream(new WeakPointer<IAudioGraph>(audioGraph), null, LanguageCode.EN_US, _logger.Clone("SRStream"), CancellationToken.None, DefaultRealTimeProvider.Singleton))
            {
                Assert.IsNotNull(sr);
                AudioInitializationResult initResult = await opusDecoder.Initialize(new NonRealTimeStreamWrapper(audioFileIn, false), false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(AudioInitializationResult.Success, initResult);
                using (AudioConformer conformer = new AudioConformer(new WeakPointer<IAudioGraph>(audioGraph), opusDecoder.OutputFormat, sr.InputFormat, null, DebugLogger.Default))
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
    }
}
