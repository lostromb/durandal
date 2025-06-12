using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Hardware;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Speech;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.SR.Azure;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Extensions.NAudio;
using Durandal.Extensions.NAudio.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Prototype
{
    public class SpeechRecoTester
    {
        private readonly ILogger _logger;
        private readonly IRealTimeProvider _realTime = DefaultRealTimeProvider.Singleton;
        private readonly CancellationToken _cancelToken = CancellationToken.None;
        private readonly IAudioGraph _audioGraph;
        private readonly AudioSampleFormat _micSampleFormat;
        private readonly IAudioCaptureDevice _microphone;
        private readonly AudioSplitterAutoConforming _splitter;
        private readonly IUtteranceRecorder _utteranceRecorder;
        private readonly ISpeechRecognizerFactory _srFactory;
        private readonly AutoResetEventAsync _recorderFinishedEvent;

        public SpeechRecoTester(string apiKey)
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);
            _realTime = DefaultRealTimeProvider.Singleton;
            _cancelToken = CancellationToken.None;
            _audioGraph = new AudioGraph(AudioGraphCapabilities.Concurrent);
            _micSampleFormat = AudioSampleFormat.Mono(16000);
            IAudioDriver audioDeviceDriver = new WaveDeviceDriver(_logger.Clone("WaveDriver"));
            _microphone = audioDeviceDriver.OpenCaptureDevice(null, new WeakPointer<IAudioGraph>(_audioGraph), _micSampleFormat, "Microphone");
            _splitter = new AudioSplitterAutoConforming(new WeakPointer<IAudioGraph>(_audioGraph), _micSampleFormat, "MicSplitter", _logger.Clone("Resampler"));
            _utteranceRecorder = new DynamicUtteranceRecorder(new WeakPointer<IAudioGraph>(_audioGraph), _micSampleFormat, "UtteranceRecorder", _logger.Clone("UtteranceRecorder"));
            _recorderFinishedEvent = new AutoResetEventAsync();

            // Build audio graph
            _microphone.ConnectOutput(_splitter);
            _splitter.AddOutput(_utteranceRecorder);

            _utteranceRecorder.UtteranceFinishedEvent.Subscribe(HandleSpeechRecoFinished);

            //_srFactory = new AzureNativeSpeechRecognizerFactory(new PortableHttpClientFactory(), _logger.Clone("AzureSpeech"), apiKey, DefaultRealTimeProvider.Singleton);

            ISocketFactory socketFactory = new TcpClientSocketFactory(_logger.Clone("SocketFactory"), System.Security.Authentication.SslProtocols.Tls12, true);
            IWebSocketClientFactory webSocketFactory = new SystemWebSocketClientFactory();
            _srFactory = new AzureSpeechRecognizerFactory(new PortableHttpClientFactory(), webSocketFactory, _logger.Clone("AzureSpeech"), apiKey, DefaultRealTimeProvider.Singleton);

            //_srFactory = new CortanaSpeechRecognizerFactory(factory, logger.Clone("CortanaSpeech"), "");
            //_srFactory = new OxfordSpeechRecognizerFactory(logger.Clone("OxfordSpeech"), "");
            //_srFactory = new AzureNativeSpeechRecognizerFactory(new PortableHttpClientFactory(), logger.Clone("AzureSpeech"), "", DefaultRealTimeProvider.Singleton);

            //ISocketFactory factory = new TcpClientSocketFactory(logger.Clone("SocketFactory"));
            //IThreadPool threadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, "RemoteSR", 8);
            //ISpeechRecognizerFactory recognizerFactory = new RemoteSpeechRecognizerFactory(codec, factory, threadPool, DefaultRealTimeProvider.Singleton, "localhost", 62290, logger.Clone("RemoteSRClient"));


            //// Host a remote speech reco endpoint right here
            //int proxyServicePort = 62290;
            //string proxyApiKey = "";
            //CodecCollection proxyCodecCollection = new CodecCollection(logger);
            //proxyCodecCollection.RegisterCodec(codec);
            //ISocketFactory proxySrSocketFactory = new TcpClientSocketFactory();
            //IHttpClientFactory proxyHttpClientFactory = new PortableHttpClientFactory();
            //AzureSpeechRecognizerFactory proxiedSpeechReco = new AzureSpeechRecognizerFactory(proxyHttpClientFactory, proxySrSocketFactory, logger.Clone("AzureSpeechReco"), proxyApiKey);

            //SRProxyServer srProxyServer = new SRProxyServer(
            //    new RawTcpSocketServer(
            //        new string[] { "sr://*:" + proxyServicePort },
            //        logger.Clone("RawTcpSocketServer"),
            //        threadPool),
            //    proxiedSpeechReco,
            //    proxyCodecCollection,
            //    logger.Clone("SRServer"),
            //    NullFileSystem.Singleton,
            //    DefaultRealTimeProvider.Singleton);

            //srProxyServer.StartServer("SRServer");
            //// end remote speech server setup
        }

        private async Task HandleSpeechRecoFinished(object source, RecorderStateEventArgs args, IRealTimeProvider realTime)
        {
            try
            {
                if (args.State == RecorderState.Error)
                {
                    _logger.Log("Utterance recorder signaled RecorderState.Error", LogLevel.Err);
                    await DurandalTaskExtensions.NoOpTask;
                }
                else if (args.State == RecorderState.FinishedNothingRecorded)
                {
                    _logger.Log("Utterance recorder signaled RecorderState.FinishedNothingRecorded", LogLevel.Wrn);
                }
                else if (args.State == RecorderState.Finished)
                {
                    _logger.Log("Utterance recorder signaled RecorderState.Finished");
                }
            }
            finally
            {
                _recorderFinishedEvent.Set();
            }
        }

        public async Task Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Stopwatch timer = new Stopwatch();
            await _microphone.StartCapture(_realTime);

            while (true)
            {
                Console.ReadKey();
                _logger.Log("Capture start");
                timer.Restart();
                using (ISpeechRecognizer recognizer = await _srFactory.CreateRecognitionStream(
                    new WeakPointer<IAudioGraph>(_audioGraph), "SpeechRecognizer", LanguageCode.EN_US, _logger.Clone("SR"), _cancelToken, _realTime))
                {
                    if (recognizer == null)
                    {
                        _logger.Log("Could not create recognizer", LogLevel.Err);
                        continue;
                    }

                    timer.Stop();
                    _logger.Log("Took " + timer.ElapsedMillisecondsPrecise() + "ms to start reco");

                    _splitter.AddOutput(recognizer);
                    _utteranceRecorder.Reset();
                    await _recorderFinishedEvent.WaitAsync();

                    timer.Restart();
                    SpeechRecognitionResult finalRecoResults = await recognizer.FinishUnderstandSpeech(_cancelToken, _realTime);
                    timer.Stop();
                    _logger.Log("Took " + timer.ElapsedMillisecondsPrecise() + "ms to finish reco");

                    if (finalRecoResults == null || finalRecoResults.RecognizedPhrases == null)
                    {
                        _logger.Log("RecoResults are null");
                    }
                    else if (finalRecoResults.RecognizedPhrases.Count == 0)
                    {
                        _logger.Log("RecoResults are empty");
                    }
                    else
                    {
                        foreach (var result in finalRecoResults.RecognizedPhrases)
                        {
                            _logger.Log(string.Format("{0} / {1} ({2})", result.DisplayText, result.IPASyllables ?? result.DisplayText, result.SREngineConfidence));
                        }
                    }

                    recognizer.DisconnectInput();
                }
            }
        }
    }
}
