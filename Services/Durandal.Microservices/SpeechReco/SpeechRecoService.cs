using Durandal.Common.Audio.Codecs;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.SR.Remote;
using Durandal.Common.Utils;
using Durandal.Common.File;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Speech.SR.Azure;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using Durandal.Common.Instrumentation;
using Durandal.Common.Audio;
using Durandal.Extensions.CognitiveServices.Speech;
using System.Threading;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.Audio.Codecs.Opus;

namespace DurandalServices.SpeechReco
{
    public class SpeechRecoService : BasicService
    {
        private readonly SRProxyServer _server;
        private readonly IAudioCodecFactory _codecs;
        private readonly IFileSystem _audioFileLogManager;

        public SpeechRecoService(ILogger logger, IFileSystem configManager, WeakPointer<IThreadPool> threadPool, WeakPointer<IMetricCollector> metrics, IRealTimeProvider realTime, DimensionSet dimensions)
            : base("SpeechReco", logger, configManager, threadPool, metrics, dimensions)
        {
            int servicePort = ServiceConfig.GetInt32("listeningPort", 62290);

            _codecs = new AggregateCodecFactory(
                new RawPcmCodecFactory(),
                new SquareDeltaCodecFactory(),
                new ULawCodecFactory(),
                new ALawCodecFactory(),
                new OpusRawCodecFactory(logger.Clone("OpusCodec")),
                new OggOpusCodecFactory());

            _audioFileLogManager = NullFileSystem.Singleton;

            ISpeechRecognizerFactory factory;
            string providerName = ServiceConfig.GetString("srProvider");
            string apiKey = ServiceConfig.GetString("srApiKey");
            //if (string.Equals("oxford", providerName) && !string.IsNullOrEmpty(apiKey))
            //{
            //    factory = new OxfordSpeechRecognizerFactory(ServiceLogger.Clone("SRImpl"), apiKey);
            //}
            //else if (string.Equals("cortana", providerName) && !string.IsNullOrEmpty(apiKey))
            //{
            //    ISocketFactory srSocketFactory = new TcpClientSocketFactory();
            //    factory = new CortanaSpeechRecognizerFactory(srSocketFactory, ServiceLogger.Clone("SRImpl"), apiKey, realTime);
            //}
            if (string.Equals("azure", providerName) && !string.IsNullOrEmpty(apiKey))
            {
                IWebSocketClientFactory srSocketFactory = new SystemWebSocketClientFactory();
                IHttpClientFactory httpClientFactory = new PortableHttpClientFactory(metrics, dimensions);
                factory = new AzureSpeechRecognizerFactory(httpClientFactory, srSocketFactory, ServiceLogger.Clone("SRImpl"), apiKey, realTime);
            }
            else if (string.Equals("azure-native", providerName) && !string.IsNullOrEmpty(apiKey))
            {
                IHttpClientFactory httpClientFactory = new PortableHttpClientFactory(metrics, dimensions);
                factory = new AzureNativeSpeechRecognizerFactory(httpClientFactory, ServiceLogger.Clone("SRImpl"), apiKey, realTime);
            }
            else
            {
                factory = NullSpeechRecoFactory.Singleton;
            }

            _server = new SRProxyServer(
                new RawTcpSocketServer(
                    new ServerBindingInfo[] { ServerBindingInfo.WildcardHost(servicePort) },
                    ServiceLogger.Clone("SRServer"),
                    realTime,
                    Metrics,
                    MetricDimensions,
                    threadPool),
                factory,
                _codecs,
                ServiceLogger.Clone("SRServer"),
                MetricDimensions);
        }

        public override async Task Start(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ServiceLogger.Log("Starting service...");
            await _server.StartServer("SRServer", cancelToken, realTime);
            ServiceLogger.Log("Started.");
        }

        public override Task Stop(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ServiceLogger.Log("Stopping service...");
            return _server.StopServer(cancelToken, realTime);
        }

        public override bool IsRunning()
        {
            return _server.Running;
        }
    }
}
