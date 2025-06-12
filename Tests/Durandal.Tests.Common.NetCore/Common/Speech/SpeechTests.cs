using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.SR.Remote;
using Durandal.Common.Speech.Triggers;
using Durandal.Common.File;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Durandal.Common.Speech;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.Speech.TTS;
using Durandal.Common.NLP;
using Durandal.Common.Instrumentation;
using Durandal.API;
using Durandal.Common.Audio.Components;
using Durandal.Common.IO;
using Durandal.Common.Monitoring;
using Durandal.Common.Utils;
using Durandal.Common.Test;
using Durandal.Common.Events;
using Durandal.Tests.Common.Speech;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Speech
{
    [TestClass]
    public class SpeechTests
    {
        //what the feather is happening in this test
        //T1 Create TimeProvider 1
        //T1 Give TimeProvider 1 to DirectSocketFactory
        //T1 Give TimeProvider 1 to RemoteSpeechRecognizer and fork to 2
        //T2 CSPSocketChannel starts thread 2

        //T1 RemoteSpeechReco connects
        //T1 DirectSocketFactory makes client-side socket with TimeProvider 2
        //T3 DirectSocketFactory starts thread 3 for server-side socket
        //T3 Server-side socket gets TimeProvider 3
        //T3 RemoteSocketServer handles the incoming socket connection
        //T3 Server-side socket begins to block on server Socket.Read (TimeProvider 3)

        //T2 CSPSocketChannel begins to block on client Socket.Read (TimeProvider 2)

        //T1 Test driver calls ContinueUnderstandSpeech
        //T1 Client socket starts writing speech data

        //T1 Call FinishUnderstandSpeech
        //T1 RSR wait on TimeProvider 1 until final results come in from CSPSocketChannel

        //[TestMethod]
        //public void TestRemoteSR()
        //{
        //    ILogger logger = new DetailedConsoleLogger();
        //    LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
        //    IRealTimeProvider thread1TimeFork = realTime.Fork();
        //    IAudioCodec codec = new PCMCodec();
        //    CodecCollection codecs = new CodecCollection(logger.Clone("Codecs"));
        //    codecs.RegisterCodec(codec);
        //    TestSpeechRecognizerFactory fakeSpeecoReco = new TestSpeechRecognizerFactory();
        //    SRProxyServer server = new SRProxyServer(new NullSocketServer(), fakeSpeecoReco, codecs, logger.Clone("SRProxyServer"), new NullResourceManager());

        //    using (IThreadPool runnerThreadPool = new CustomThreadPool(logger.Clone("ThreadPool"), "ThreadPool", 8))
        //    {
        //        ISocketFactory socketFactory = new DirectSocketFactory(server, logger.Clone("DirectSocketFactory"), runnerThreadPool, thread1TimeFork, true);
        //        RemoteSpeechRecognizer client = new RemoteSpeechRecognizer(codec, socketFactory, runnerThreadPool, thread1TimeFork, "fakehost", 0, logger.Clone("SRProxyClient"));

        //        ManualResetEventSlim testThreadFinished = new ManualResetEventSlim(false);
        //        IList<SpeechRecoResult> finalResults = null;
        //        // Because of convoluted reasons regarding spinwaiting in lockstep here, we have to run the actual speech recognition pass in a separate thread,
        //        // making this test require 4 threads in total.
        //        runnerThreadPool.EnqueueUserAsyncWorkItem(async () =>
        //        {
        //            try
        //            {
        //                logger.Log("Starting speech reco...");
        //                fakeSpeecoReco.SetRecoResult("this is a test");

        //                await client.StartUnderstandSpeech("en-US");
        //                for (int c = 0; c < 20; c++)
        //                {
        //                    AudioChunk fakeData = new AudioChunk(new short[3200], 16000);
        //                    logger.Log("Sending audio...");
        //                    await client.ContinueUnderstandSpeech(fakeData);
        //                    await thread1TimeFork.WaitAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None);
        //                }

        //                logger.Log("Finishing speech reco...");
        //                finalResults = await client.FinishUnderstandSpeech();
        //                testThreadFinished.Set();
        //            }
        //            finally
        //            {
        //                thread1TimeFork.Merge();
        //            }
        //        });

        //        realTime.Step(TimeSpan.FromMilliseconds(1000), 10);
        //        Assert.IsTrue(testThreadFinished.Wait(TimeSpan.FromSeconds(15)), "Threads are in deadlock");
        //        Assert.IsNotNull(finalResults);
        //        Assert.AreEqual(1, finalResults.Count);
        //        Assert.AreEqual("this is a test", finalResults[0].NormalizedText);
        //    }
        //}

        [TestMethod]
        public async Task TestTriggerArbitrationBasic()
        {
            ILogger logger = new ConsoleLogger();
            NullHttpServer serverBase = new NullHttpServer();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);
            TriggerArbitratorServer arbitratorServer = new TriggerArbitratorServer(serverBase, logger, TimeSpan.FromSeconds(5), realTime, 10);
            IHttpClient httpClient = new DirectHttpClient(arbitratorServer);
            HttpTriggerArbitrator arbitratorClient = new HttpTriggerArbitrator(httpClient, TimeSpan.FromMilliseconds(250), "group1");

            bool triggered = await arbitratorClient.ArbitrateTrigger(logger, realTime);
            Assert.IsTrue(triggered);

            for (int c = 0; c < 40; c++)
            {
                realTime.Step(TimeSpan.FromMilliseconds(10));
                triggered = await arbitratorClient.ArbitrateTrigger(logger, realTime);
                Assert.IsFalse(triggered);
            }

            realTime.Step(TimeSpan.FromSeconds(5));
            triggered = await arbitratorClient.ArbitrateTrigger(logger, realTime);
            Assert.IsTrue(triggered);
        }

        [TestMethod]
        public async Task TestTriggerArbitrationParallel()
        {
            ILogger logger = new ConsoleLogger();
            NullHttpServer serverBase = new NullHttpServer();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);
            TriggerArbitratorServer arbitratorServer = new TriggerArbitratorServer(serverBase, logger, TimeSpan.FromSeconds(5), realTime, 5);
            IHttpClient httpClient = new DirectHttpClient(arbitratorServer);
            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();

            using (IThreadPool clientThreads = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestPool", 10, false))
            {
                List<TriggerArbitrationThread> threads = new List<TriggerArbitrationThread>();

                for (int c = 0; c < 10; c++)
                {
                    TriggerArbitrationThread thread = new TriggerArbitrationThread(
                        cancelTokenSource.Token,
                        realTime,
                        logger,
                        new HttpTriggerArbitrator(httpClient, TimeSpan.FromMilliseconds(250), "group" + c),
                        c);
                    threads.Add(thread);
                    clientThreads.EnqueueUserAsyncWorkItem(thread.Run);
                }

                while (clientThreads.TotalWorkItems < 10)
                {
                    await Task.Delay(10);
                }

                realTime.Step(TimeSpan.FromSeconds(22), 500);
                cancelTokenSource.Cancel();

                while (clientThreads.TotalWorkItems > 0)
                {
                    await Task.Delay(10);
                }

                // Clients should have triggered a total of 50 times (10 clients, 5 triggers with 5 second intervals)
                foreach (TriggerArbitrationThread thread in threads)
                {
                    Assert.AreEqual(5, thread.TriggersSucceeded);
                }
            }
        }

        private class TriggerArbitrationThread
        {
            private readonly IRealTimeProvider _realTime;
            private readonly CancellationToken _cancelToken;
            private readonly ILogger _logger;
            private readonly ITriggerArbitrator _arbitratorClient;
            private readonly int _threadId;
            private int _triggersSucceeded;

            public TriggerArbitrationThread(
                CancellationToken cancelToken,
                IRealTimeProvider realTime,
                ILogger logger,
                ITriggerArbitrator arbitrator,
                int threadId)
            {
                _realTime = realTime.Fork("TriggerArbitrationThread");
                _cancelToken = cancelToken;
                _logger = logger;
                _arbitratorClient = arbitrator;
                _threadId = threadId;
                _triggersSucceeded = 0;
            }

            public async Task Run()
            {
                try
                {
                    while (!_cancelToken.IsCancellationRequested)
                    {
                        await _realTime.WaitAsync(TimeSpan.FromMilliseconds(100), _cancelToken);
                        bool triggered = await _arbitratorClient.ArbitrateTrigger(_logger, _realTime);
                        if (triggered)
                        {
                            _triggersSucceeded++;
                        }
                    }
                }
                finally
                {
                    _realTime.Merge();
                }
            }

            public int TriggersSucceeded => _triggersSucceeded;
        }

        [TestMethod]
        public async Task TestTriggerArbitrationTimeout()
        {
            ILogger logger = new ConsoleLogger();
            IHttpClient httpClient = new NullHttpClient();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            HttpTriggerArbitrator arbitratorClient = new HttpTriggerArbitrator(httpClient, TimeSpan.FromMilliseconds(10), "group");

            bool triggered = await arbitratorClient.ArbitrateTrigger(logger, realTime);
            Assert.IsTrue(triggered);
            triggered = await arbitratorClient.ArbitrateTrigger(logger, realTime);
            Assert.IsTrue(triggered);
        }

        //for (int c = 0; c < alignmentResults.Count; c++)
        //{
        //    SynthesizedWord word = alignmentResults[c];
        //    Console.WriteLine("Assert.AreEqual(\"{0}\", alignmentResults[{1}].Word);", word.Word, c);
        //    Console.WriteLine("Assert.AreEqual(\"{0}\", alignmentResults[{1}].Offset));", word.Offset), c);
        //}

        [TestMethod]
        public async Task TestFixedLengthUtteranceRecorder16Khz()
        {
            ILogger logger = new ConsoleLogger();
            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));

            EventRecorder<RecorderStateEventArgs> eventRecorder = new EventRecorder<RecorderStateEventArgs>();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.6f))
            using (VolumeFilter volume = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (StaticUtteranceRecorder recorder = new StaticUtteranceRecorder(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromSeconds(4), logger))
            {
                source.ConnectOutput(volume);
                volume.ConnectOutput(recorder);
                recorder.UtteranceFinishedEvent.Subscribe(eventRecorder.HandleEventAsync);

                source.BeginActivelyWriting(logger.Clone("WriteThread"), lockStepTime, true);

                RetrieveResult<CapturedEvent<RecorderStateEventArgs>> rr;

                for (int loop = 0; loop < 3; loop++)
                {
                    recorder.Reset();
                    lockStepTime.Step(TimeSpan.FromMilliseconds(3900));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsFalse(rr.Success);

                    lockStepTime.Step(TimeSpan.FromMilliseconds(100));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(rr.Success);
                    Assert.AreEqual(RecorderState.Finished, rr.Result.Args.State);

                    // Assert that only one event fired
                    lockStepTime.Step(TimeSpan.FromMilliseconds(100));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsFalse(rr.Success);
                }
            }
        }

        [TestMethod]
        public async Task TestFixedLengthUtteranceRecorder48Khz()
        {
            ILogger logger = new ConsoleLogger();
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));

            EventRecorder<RecorderStateEventArgs> eventRecorder = new EventRecorder<RecorderStateEventArgs>();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.6f))
            using (VolumeFilter volume = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (StaticUtteranceRecorder recorder = new StaticUtteranceRecorder(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromSeconds(4), logger))
            {
                source.ConnectOutput(volume);
                volume.ConnectOutput(recorder);
                recorder.UtteranceFinishedEvent.Subscribe(eventRecorder.HandleEventAsync);

                source.BeginActivelyWriting(logger.Clone("WriteThread"), lockStepTime, true);

                RetrieveResult<CapturedEvent<RecorderStateEventArgs>> rr;

                for (int loop = 0; loop < 3; loop++)
                {
                    recorder.Reset();
                    lockStepTime.Step(TimeSpan.FromMilliseconds(3900));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsFalse(rr.Success);

                    lockStepTime.Step(TimeSpan.FromMilliseconds(100));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(rr.Success);
                    Assert.AreEqual(RecorderState.Finished, rr.Result.Args.State);

                    // Assert that only one event fired
                    lockStepTime.Step(TimeSpan.FromMilliseconds(100));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsFalse(rr.Success);
                }
            }
        }

        [TestMethod]
        public async Task TestFixedLengthUtteranceRecorderSilence()
        {
            ILogger logger = new ConsoleLogger();
            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));

            EventRecorder<RecorderStateEventArgs> eventRecorder = new EventRecorder<RecorderStateEventArgs>();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.001f))
            using (VolumeFilter volume = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (StaticUtteranceRecorder recorder = new StaticUtteranceRecorder(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromSeconds(4), logger))
            {
                source.ConnectOutput(volume);
                volume.ConnectOutput(recorder);
                recorder.UtteranceFinishedEvent.Subscribe(eventRecorder.HandleEventAsync);

                source.BeginActivelyWriting(logger.Clone("WriteThread"), lockStepTime, true);

                RetrieveResult<CapturedEvent<RecorderStateEventArgs>> rr;

                for (int loop = 0; loop < 3; loop++)
                {
                    recorder.Reset();
                    lockStepTime.Step(TimeSpan.FromMilliseconds(3900));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsFalse(rr.Success);

                    lockStepTime.Step(TimeSpan.FromMilliseconds(100));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(rr.Success);
                    Assert.AreEqual(RecorderState.FinishedNothingRecorded, rr.Result.Args.State);

                    // Assert that only one event fired
                    lockStepTime.Step(TimeSpan.FromMilliseconds(100));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsFalse(rr.Success);
                }
            }
        }

        [TestMethod]
        [DeploymentItem("TestData/TestUtterance4sec.wav")]
        public async Task TestDynamicUtteranceRecorder16Khz()
        {
            ILogger logger = new ConsoleLogger();
            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));

            AudioSample utterance;
            using (FileStream waveStream = new FileStream("TestUtterance4Sec.wav", FileMode.Open, FileAccess.Read))
            using (NonRealTimeStreamWrapper nrtStream = new NonRealTimeStreamWrapper(waveStream, false))
            {
                utterance = await AudioHelpers.DecodeAudioStream(nrtStream, new RiffWaveCodecFactory(), "riff-pcm", string.Empty, logger);
            }

            EventRecorder<RecorderStateEventArgs> eventRecorder = new EventRecorder<RecorderStateEventArgs>();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource sineWave = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.001f))
            using (LinearMixerAutoConforming mixer = new LinearMixerAutoConforming(new WeakPointer<IAudioGraph>(graph), format, null, true, logger.Clone("Mixer")))
            using (DynamicUtteranceRecorder recorder = new DynamicUtteranceRecorder(new WeakPointer<IAudioGraph>(graph), format, null, logger.Clone("UtteranceRecorder")))
            {
                mixer.ConnectOutput(recorder);
                mixer.AddInput(sineWave);
                recorder.UtteranceFinishedEvent.Subscribe(eventRecorder.HandleEventAsync);

                sineWave.BeginActivelyWriting(logger.Clone("WriteThread"), lockStepTime, true);

                RetrieveResult<CapturedEvent<RecorderStateEventArgs>> rr;

                for (int loop = 0; loop < 3; loop++)
                {
                    mixer.AddInput(new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), utterance, null));
                    recorder.Reset();

                    lockStepTime.Step(TimeSpan.FromMilliseconds(4700));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsFalse(rr.Success);

                    lockStepTime.Step(TimeSpan.FromMilliseconds(100));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(rr.Success);
                    Assert.AreEqual(RecorderState.Finished, rr.Result.Args.State);

                    // Assert that only one event fired
                    lockStepTime.Step(TimeSpan.FromMilliseconds(500));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsFalse(rr.Success);
                }
            }
        }

        [TestMethod]
        [DeploymentItem("TestData/TestUtterance4sec.wav")]
        public async Task TestDynamicUtteranceRecorder48Khz()
        {
            ILogger logger = new ConsoleLogger();
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));

            AudioSample utterance;
            using (FileStream waveStream = new FileStream("TestUtterance4Sec.wav", FileMode.Open, FileAccess.Read))
            using (NonRealTimeStreamWrapper nrtStream = new NonRealTimeStreamWrapper(waveStream, false))
            {
                utterance = await AudioHelpers.DecodeAudioStream(nrtStream, new RiffWaveCodecFactory(), "riff-pcm", string.Empty, logger);
            }

            EventRecorder<RecorderStateEventArgs> eventRecorder = new EventRecorder<RecorderStateEventArgs>();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource sineWave = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.001f))
            using (LinearMixerAutoConforming mixer = new LinearMixerAutoConforming(new WeakPointer<IAudioGraph>(graph), format, null, true, logger.Clone("Mixer")))
            using (DynamicUtteranceRecorder recorder = new DynamicUtteranceRecorder(new WeakPointer<IAudioGraph>(graph), format, null, logger.Clone("UtteranceRecorder")))
            {
                mixer.ConnectOutput(recorder);
                mixer.AddInput(sineWave);
                recorder.UtteranceFinishedEvent.Subscribe(eventRecorder.HandleEventAsync);

                sineWave.BeginActivelyWriting(logger.Clone("WriteThread"), lockStepTime, true);

                RetrieveResult<CapturedEvent<RecorderStateEventArgs>> rr;

                for (int loop = 0; loop < 3; loop++)
                {
                    mixer.AddInput(new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), utterance, null), null, true);
                    recorder.Reset();

                    lockStepTime.Step(TimeSpan.FromMilliseconds(4700));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsFalse(rr.Success);

                    lockStepTime.Step(TimeSpan.FromMilliseconds(150));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(rr.Success);
                    Assert.AreEqual(RecorderState.Finished, rr.Result.Args.State);

                    // Assert that only one event fired
                    lockStepTime.Step(TimeSpan.FromMilliseconds(500));
                    rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsFalse(rr.Success);
                }
            }
        }

        private static AudioSample ReadRawAudioFile(string fileName, AudioSampleFormat sampleFormat)
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream inputStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (NonRealTimeStream nrtStream = new NonRealTimeStreamWrapper(inputStream, false))
            using (RawPcmDecoder decoder = new RawPcmDecoder(new WeakPointer<IAudioGraph>(graph), sampleFormat, null))
            using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), sampleFormat, null))
            {
                decoder.Initialize(nrtStream, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                decoder.ConnectOutput(sampleTarget);
                sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                return sampleTarget.GetAllAudio();
            }
        }

        [TestMethod]
        [DeploymentItem("TestData/speech_synth_sapi_1.raw")]
        public void TestAuralexicalAlignmentSapi1()
        {
            AudioSample audio = ReadRawAudioFile("speech_synth_sapi_1.raw", AudioSampleFormat.Mono(16000));
            IWordBreaker wordBreaker = new EnglishWholeWordBreaker();
            ISpeechTimingEstimator timingEstimator = new EnglishSpeechTimingEstimator();
            string ssml = "I found these 3 results: John Clark Steakhouse, The Broiler, Walt Disney Presents The Crab Shack Featuring Star Wars. Which one did you want?";
            IList<SynthesizedWord> alignmentResults = SpeechUtils.EstimateSynthesizedWordTimings(audio, ssml, wordBreaker, timingEstimator, new DebugLogger());
            Assert.IsNotNull(alignmentResults);

            //for (int c = 0; c < alignmentResults.Count; c++)
            //{
            //    SynthesizedWord word = alignmentResults[c];
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", alignmentResults[{1}].Word);", word.Word, c);
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", TimeSpanToString(alignmentResults[{1}].Offset));", word.Offset, c);
            //}

            Assert.AreEqual("I", alignmentResults[0].Word);
            Assert.AreEqual("00:00:00.0900000", TimeSpanToString(alignmentResults[0].Offset));
            Assert.AreEqual("found", alignmentResults[1].Word);
            Assert.AreEqual("00:00:00.1733223", TimeSpanToString(alignmentResults[1].Offset));
            Assert.AreEqual("these", alignmentResults[2].Word);
            Assert.AreEqual("00:00:00.5899342", TimeSpanToString(alignmentResults[2].Offset));
            Assert.AreEqual("3", alignmentResults[3].Word);
            Assert.AreEqual("00:00:01.0065460", TimeSpanToString(alignmentResults[3].Offset));
            Assert.AreEqual("results", alignmentResults[4].Word);
            Assert.AreEqual("00:00:01.0898684", TimeSpanToString(alignmentResults[4].Offset));
            Assert.AreEqual("John", alignmentResults[5].Word);
            Assert.AreEqual("00:00:02.0453750", TimeSpanToString(alignmentResults[5].Offset));
            Assert.AreEqual("Clark", alignmentResults[6].Word);
            Assert.AreEqual("00:00:02.3516645", TimeSpanToString(alignmentResults[6].Offset));
            Assert.AreEqual("Steakhouse", alignmentResults[7].Word);
            Assert.AreEqual("00:00:02.7345263", TimeSpanToString(alignmentResults[7].Offset));
            Assert.AreEqual("The", alignmentResults[8].Word);
            Assert.AreEqual("00:00:03.8700000", TimeSpanToString(alignmentResults[8].Offset));
            Assert.AreEqual("Broiler", alignmentResults[9].Word);
            Assert.AreEqual("00:00:04.0778999", TimeSpanToString(alignmentResults[9].Offset));
            Assert.AreEqual("Walt", alignmentResults[10].Word);
            Assert.AreEqual("00:00:04.9236250", TimeSpanToString(alignmentResults[10].Offset));
            Assert.AreEqual("Disney", alignmentResults[11].Word);
            Assert.AreEqual("00:00:05.2085454", TimeSpanToString(alignmentResults[11].Offset));
            Assert.AreEqual("Presents", alignmentResults[12].Word);
            Assert.AreEqual("00:00:05.6359257", TimeSpanToString(alignmentResults[12].Offset));
            Assert.AreEqual("The", alignmentResults[13].Word);
            Assert.AreEqual("00:00:06.2057661", TimeSpanToString(alignmentResults[13].Offset));
            Assert.AreEqual("Crab", alignmentResults[14].Word);
            Assert.AreEqual("00:00:06.4194560", TimeSpanToString(alignmentResults[14].Offset));
            Assert.AreEqual("Shack", alignmentResults[15].Word);
            Assert.AreEqual("00:00:06.7043764", TimeSpanToString(alignmentResults[15].Offset));
            Assert.AreEqual("Featuring", alignmentResults[16].Word);
            Assert.AreEqual("00:00:07.0605268", TimeSpanToString(alignmentResults[16].Offset));
            Assert.AreEqual("Star", alignmentResults[17].Word);
            Assert.AreEqual("00:00:07.7015971", TimeSpanToString(alignmentResults[17].Offset));
            Assert.AreEqual("Wars", alignmentResults[18].Word);
            Assert.AreEqual("00:00:07.9865175", TimeSpanToString(alignmentResults[18].Offset));
            Assert.AreEqual("Which", alignmentResults[19].Word);
            Assert.AreEqual("00:00:09.1059375", TimeSpanToString(alignmentResults[19].Offset));
            Assert.AreEqual("one", alignmentResults[20].Word);
            Assert.AreEqual("00:00:09.4389580", TimeSpanToString(alignmentResults[20].Offset));
            Assert.AreEqual("did", alignmentResults[21].Word);
            Assert.AreEqual("00:00:09.6387705", TimeSpanToString(alignmentResults[21].Offset));
            Assert.AreEqual("you", alignmentResults[22].Word);
            Assert.AreEqual("00:00:09.8385830", TimeSpanToString(alignmentResults[22].Offset));
            Assert.AreEqual("want", alignmentResults[23].Word);
            Assert.AreEqual("00:00:10.0383955", TimeSpanToString(alignmentResults[23].Offset));
        }

        [TestMethod]
        [DeploymentItem("TestData/speech_synth_sapi_1.raw")]
        public void TestAuralexicalAlignmentCompletelyDifferentShorter()
        {
            AudioSample audio = ReadRawAudioFile("speech_synth_sapi_1.raw", AudioSampleFormat.Mono(16000));
            IWordBreaker wordBreaker = new EnglishWholeWordBreaker();
            ISpeechTimingEstimator timingEstimator = new EnglishSpeechTimingEstimator();
            string ssml = "Try and flex like me, boy, you don't know my recipe.";
            IList<SynthesizedWord> alignmentResults = SpeechUtils.EstimateSynthesizedWordTimings(audio, ssml, wordBreaker, timingEstimator, new DebugLogger());
            Assert.IsNotNull(alignmentResults);

            //for (int c = 0; c < alignmentResults.Count; c++)
            //{
            //    SynthesizedWord word = alignmentResults[c];
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", alignmentResults[{1}].Word);", word.Word, c);
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", TimeSpanToString(alignmentResults[{1}].Offset));", word.Offset, c);
            //}

            Assert.AreEqual("Try", alignmentResults[0].Word);
            Assert.AreEqual("00:00:00.0900000", TimeSpanToString(alignmentResults[0].Offset));
            Assert.AreEqual("and", alignmentResults[1].Word);
            Assert.AreEqual("00:00:00.2384179", TimeSpanToString(alignmentResults[1].Offset));
            Assert.AreEqual("flex", alignmentResults[2].Word);
            Assert.AreEqual("00:00:00.3868359", TimeSpanToString(alignmentResults[2].Offset));
            Assert.AreEqual("like", alignmentResults[3].Word);
            Assert.AreEqual("00:00:00.5847265", TimeSpanToString(alignmentResults[3].Offset));
            Assert.AreEqual("me", alignmentResults[4].Word);
            Assert.AreEqual("00:00:00.7826171", TimeSpanToString(alignmentResults[4].Offset));
            Assert.AreEqual("boy", alignmentResults[5].Word);
            Assert.AreEqual("00:00:00.8815625", TimeSpanToString(alignmentResults[5].Offset));
            Assert.AreEqual("you", alignmentResults[6].Word);
            Assert.AreEqual("00:00:02.0453750", TimeSpanToString(alignmentResults[6].Offset));
            Assert.AreEqual("don't", alignmentResults[7].Word);
            Assert.AreEqual("00:00:02.2636062", TimeSpanToString(alignmentResults[7].Offset));
            Assert.AreEqual("know", alignmentResults[8].Word);
            Assert.AreEqual("00:00:02.6273249", TimeSpanToString(alignmentResults[8].Offset));
            Assert.AreEqual("my", alignmentResults[9].Word);
            Assert.AreEqual("00:00:02.9183000", TimeSpanToString(alignmentResults[9].Offset));
            Assert.AreEqual("recipe", alignmentResults[10].Word);
            Assert.AreEqual("00:00:03.0637875", TimeSpanToString(alignmentResults[10].Offset));
        }

        [TestMethod]
        [DeploymentItem("TestData/speech_synth_sapi_1.raw")]
        public void TestAuralexicalAlignmentCompletelyDifferentLonger()
        {
            AudioSample audio = ReadRawAudioFile("speech_synth_sapi_1.raw", AudioSampleFormat.Mono(16000));
            IWordBreaker wordBreaker = new EnglishWholeWordBreaker();
            ISpeechTimingEstimator timingEstimator = new EnglishSpeechTimingEstimator();
            string ssml = "Dedicated to the dedicated few, over-medicated with an underrated view. Stuck down, big ups to the buck-short, the harsh, the snitched, the not-rich, the miscourt. Those who demonstrate when the rest just play, Or penetrate like today's that day. Dedicated to the sweat in the face of a man misplaced who finds his own lane";
            IList<SynthesizedWord> alignmentResults = SpeechUtils.EstimateSynthesizedWordTimings(audio, ssml, wordBreaker, timingEstimator, new DebugLogger());
            Assert.IsNotNull(alignmentResults);

            //for (int c = 0; c < alignmentResults.Count; c++)
            //{
            //    SynthesizedWord word = alignmentResults[c];
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", alignmentResults[{1}].Word);", word.Word, c);
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", TimeSpanToString(alignmentResults[{1}].Offset));", word.Offset, c);
            //}

            Assert.AreEqual("Dedicated", alignmentResults[0].Word);
            Assert.AreEqual("00:00:00.0900000", TimeSpanToString(alignmentResults[0].Offset));
            Assert.AreEqual("to", alignmentResults[1].Word);
            Assert.AreEqual("00:00:01.2704714", TimeSpanToString(alignmentResults[1].Offset));
            Assert.AreEqual("the", alignmentResults[2].Word);
            Assert.AreEqual("00:00:01.5327980", TimeSpanToString(alignmentResults[2].Offset));
            Assert.AreEqual("dedicated", alignmentResults[3].Word);
            Assert.AreEqual("00:00:01.9262885", TimeSpanToString(alignmentResults[3].Offset));
            Assert.AreEqual("few", alignmentResults[4].Word);
            Assert.AreEqual("00:00:03.1067600", TimeSpanToString(alignmentResults[4].Offset));
            Assert.AreEqual("over-medicated", alignmentResults[5].Word);
            Assert.AreEqual("00:00:03.8700000", TimeSpanToString(alignmentResults[5].Offset));
            Assert.AreEqual("with", alignmentResults[6].Word);
            Assert.AreEqual("00:00:04.5324072", TimeSpanToString(alignmentResults[6].Offset));
            Assert.AreEqual("an", alignmentResults[7].Word);
            Assert.AreEqual("00:00:04.7216665", TimeSpanToString(alignmentResults[7].Offset));
            Assert.AreEqual("underrated", alignmentResults[8].Word);
            Assert.AreEqual("00:00:04.8162958", TimeSpanToString(alignmentResults[8].Offset));
            Assert.AreEqual("view", alignmentResults[9].Word);
            Assert.AreEqual("00:00:05.2894438", TimeSpanToString(alignmentResults[9].Offset));
            Assert.AreEqual("Stuck", alignmentResults[10].Word);
            Assert.AreEqual("00:00:05.4787031", TimeSpanToString(alignmentResults[10].Offset));
            Assert.AreEqual("down", alignmentResults[11].Word);
            Assert.AreEqual("00:00:06.3724272", TimeSpanToString(alignmentResults[11].Offset));
            Assert.AreEqual("big", alignmentResults[12].Word);
            Assert.AreEqual("00:00:07.0874062", TimeSpanToString(alignmentResults[12].Offset));
            Assert.AreEqual("ups", alignmentResults[13].Word);
            Assert.AreEqual("00:00:07.3172211", TimeSpanToString(alignmentResults[13].Offset));
            Assert.AreEqual("to", alignmentResults[14].Word);
            Assert.AreEqual("00:00:07.5470361", TimeSpanToString(alignmentResults[14].Offset));
            Assert.AreEqual("the", alignmentResults[15].Word);
            Assert.AreEqual("00:00:07.7002456", TimeSpanToString(alignmentResults[15].Offset));
            Assert.AreEqual("buck-short", alignmentResults[16].Word);
            Assert.AreEqual("00:00:07.9300605", TimeSpanToString(alignmentResults[16].Offset));
            Assert.AreEqual("the", alignmentResults[17].Word);
            Assert.AreEqual("00:00:08.6961093", TimeSpanToString(alignmentResults[17].Offset));
            Assert.AreEqual("harsh", alignmentResults[18].Word);
            Assert.AreEqual("00:00:08.7186542", TimeSpanToString(alignmentResults[18].Offset));
            Assert.AreEqual("the", alignmentResults[19].Word);
            Assert.AreEqual("00:00:08.7932734", TimeSpanToString(alignmentResults[19].Offset));
            Assert.AreEqual("snitched", alignmentResults[20].Word);
            Assert.AreEqual("00:00:08.8158183", TimeSpanToString(alignmentResults[20].Offset));
            Assert.AreEqual("the", alignmentResults[21].Word);
            Assert.AreEqual("00:00:08.9129814", TimeSpanToString(alignmentResults[21].Offset));
            Assert.AreEqual("not-rich", alignmentResults[22].Word);
            Assert.AreEqual("00:00:08.9355263", TimeSpanToString(alignmentResults[22].Offset));
            Assert.AreEqual("the", alignmentResults[23].Word);
            Assert.AreEqual("00:00:09.0326894", TimeSpanToString(alignmentResults[23].Offset));
            Assert.AreEqual("miscourt", alignmentResults[24].Word);
            Assert.AreEqual("00:00:09.0552343", TimeSpanToString(alignmentResults[24].Offset));
            Assert.AreEqual("Those", alignmentResults[25].Word);
            Assert.AreEqual("00:00:09.2106113", TimeSpanToString(alignmentResults[25].Offset));
            Assert.AreEqual("who", alignmentResults[26].Word);
            Assert.AreEqual("00:00:09.2481855", TimeSpanToString(alignmentResults[26].Offset));
            Assert.AreEqual("demonstrate", alignmentResults[27].Word);
            Assert.AreEqual("00:00:09.2707304", TimeSpanToString(alignmentResults[27].Offset));
            Assert.AreEqual("when", alignmentResults[28].Word);
            Assert.AreEqual("00:00:09.3533935", TimeSpanToString(alignmentResults[28].Offset));
            Assert.AreEqual("the", alignmentResults[29].Word);
            Assert.AreEqual("00:00:09.3834531", TimeSpanToString(alignmentResults[29].Offset));
            Assert.AreEqual("rest", alignmentResults[30].Word);
            Assert.AreEqual("00:00:09.4059980", TimeSpanToString(alignmentResults[30].Offset));
            Assert.AreEqual("just", alignmentResults[31].Word);
            Assert.AreEqual("00:00:09.4360576", TimeSpanToString(alignmentResults[31].Offset));
            Assert.AreEqual("play", alignmentResults[32].Word);
            Assert.AreEqual("00:00:09.4661171", TimeSpanToString(alignmentResults[32].Offset));
            Assert.AreEqual("Or", alignmentResults[33].Word);
            Assert.AreEqual("00:00:09.5332216", TimeSpanToString(alignmentResults[33].Offset));
            Assert.AreEqual("penetrate", alignmentResults[34].Word);
            Assert.AreEqual("00:00:09.5482509", TimeSpanToString(alignmentResults[34].Offset));
            Assert.AreEqual("like", alignmentResults[35].Word);
            Assert.AreEqual("00:00:09.6158847", TimeSpanToString(alignmentResults[35].Offset));
            Assert.AreEqual("today's", alignmentResults[36].Word);
            Assert.AreEqual("00:00:09.6459443", TimeSpanToString(alignmentResults[36].Offset));
            Assert.AreEqual("that", alignmentResults[37].Word);
            Assert.AreEqual("00:00:09.6985478", TimeSpanToString(alignmentResults[37].Offset));
            Assert.AreEqual("day", alignmentResults[38].Word);
            Assert.AreEqual("00:00:09.7286074", TimeSpanToString(alignmentResults[38].Offset));
            Assert.AreEqual("Dedicated", alignmentResults[39].Word);
            Assert.AreEqual("00:00:09.8464111", TimeSpanToString(alignmentResults[39].Offset));
            Assert.AreEqual("to", alignmentResults[40].Word);
            Assert.AreEqual("00:00:09.9140449", TimeSpanToString(alignmentResults[40].Offset));
            Assert.AreEqual("the", alignmentResults[41].Word);
            Assert.AreEqual("00:00:09.9290742", TimeSpanToString(alignmentResults[41].Offset));
            Assert.AreEqual("sweat", alignmentResults[42].Word);
            Assert.AreEqual("00:00:09.9516191", TimeSpanToString(alignmentResults[42].Offset));
            Assert.AreEqual("in", alignmentResults[43].Word);
            Assert.AreEqual("00:00:09.9891933", TimeSpanToString(alignmentResults[43].Offset));
            Assert.AreEqual("the", alignmentResults[44].Word);
            Assert.AreEqual("00:00:10.0042226", TimeSpanToString(alignmentResults[44].Offset));
            Assert.AreEqual("face", alignmentResults[45].Word);
            Assert.AreEqual("00:00:10.0267675", TimeSpanToString(alignmentResults[45].Offset));
            Assert.AreEqual("of", alignmentResults[46].Word);
            Assert.AreEqual("00:00:10.0568271", TimeSpanToString(alignmentResults[46].Offset));
            Assert.AreEqual("a", alignmentResults[47].Word);
            Assert.AreEqual("00:00:10.0718564", TimeSpanToString(alignmentResults[47].Offset));
            Assert.AreEqual("man", alignmentResults[48].Word);
            Assert.AreEqual("00:00:10.0793710", TimeSpanToString(alignmentResults[48].Offset));
            Assert.AreEqual("misplaced", alignmentResults[49].Word);
            Assert.AreEqual("00:00:10.1019160", TimeSpanToString(alignmentResults[49].Offset));
            Assert.AreEqual("who", alignmentResults[50].Word);
            Assert.AreEqual("00:00:10.1695498", TimeSpanToString(alignmentResults[50].Offset));
            Assert.AreEqual("finds", alignmentResults[51].Word);
            Assert.AreEqual("00:00:10.1920947", TimeSpanToString(alignmentResults[51].Offset));
            Assert.AreEqual("his", alignmentResults[52].Word);
            Assert.AreEqual("00:00:10.2296689", TimeSpanToString(alignmentResults[52].Offset));
            Assert.AreEqual("own", alignmentResults[53].Word);
            Assert.AreEqual("00:00:10.2522138", TimeSpanToString(alignmentResults[53].Offset));
            Assert.AreEqual("lane", alignmentResults[54].Word);
            Assert.AreEqual("00:00:10.2747587", TimeSpanToString(alignmentResults[54].Offset));
        }

        [TestMethod]
        [DeploymentItem("TestData/speech_synth_sapi_2.raw")]
        public void TestAuralexicalAlignmentSapi2()
        {
            AudioSample audio = ReadRawAudioFile("speech_synth_sapi_2.raw", AudioSampleFormat.Mono(16000));
            IWordBreaker wordBreaker = new EnglishWholeWordBreaker();
            ISpeechTimingEstimator timingEstimator = new EnglishSpeechTimingEstimator();
            string ssml = "This is a test: 1, 2 --- 3; 4, 5! 6 - 7. This concludes the test.";
            IList<SynthesizedWord> alignmentResults = SpeechUtils.EstimateSynthesizedWordTimings(audio, ssml, wordBreaker, timingEstimator, new DebugLogger());
            Assert.IsNotNull(alignmentResults);

            //for (int c = 0; c < alignmentResults.Count; c++)
            //{
            //    SynthesizedWord word = alignmentResults[c];
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", alignmentResults[{1}].Word);", word.Word, c);
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", TimeSpanToString(alignmentResults[{1}].Offset));", word.Offset, c);
            //}

            Assert.AreEqual("This", alignmentResults[0].Word);
            Assert.AreEqual("00:00:00.1002500", TimeSpanToString(alignmentResults[0].Offset));
            Assert.AreEqual("is", alignmentResults[1].Word);
            Assert.AreEqual("00:00:00.4145454", TimeSpanToString(alignmentResults[1].Offset));
            Assert.AreEqual("a", alignmentResults[2].Word);
            Assert.AreEqual("00:00:00.5716931", TimeSpanToString(alignmentResults[2].Offset));
            Assert.AreEqual("test", alignmentResults[3].Word);
            Assert.AreEqual("00:00:00.6502670", TimeSpanToString(alignmentResults[3].Offset));
            Assert.AreEqual("1", alignmentResults[4].Word);
            Assert.AreEqual("00:00:01.3323125", TimeSpanToString(alignmentResults[4].Offset));
            Assert.AreEqual("2", alignmentResults[5].Word);
            Assert.AreEqual("00:00:02.0785000", TimeSpanToString(alignmentResults[5].Offset));
            Assert.AreEqual("---", alignmentResults[6].Word);
            Assert.AreEqual("00:00:02.2623999", TimeSpanToString(alignmentResults[6].Offset));
            Assert.AreEqual("3", alignmentResults[7].Word);
            Assert.AreEqual("00:00:02.8141000", TimeSpanToString(alignmentResults[7].Offset));
            Assert.AreEqual("4", alignmentResults[8].Word);
            Assert.AreEqual("00:00:03.3654375", TimeSpanToString(alignmentResults[8].Offset));
            Assert.AreEqual("5", alignmentResults[9].Word);
            Assert.AreEqual("00:00:04.1600625", TimeSpanToString(alignmentResults[9].Offset));
            Assert.AreEqual("6", alignmentResults[10].Word);
            Assert.AreEqual("00:00:05.5051875", TimeSpanToString(alignmentResults[10].Offset));
            Assert.AreEqual("-", alignmentResults[11].Word);
            Assert.AreEqual("00:00:05.8306459", TimeSpanToString(alignmentResults[11].Offset));
            Assert.AreEqual("7", alignmentResults[12].Word);
            Assert.AreEqual("00:00:06.1561044", TimeSpanToString(alignmentResults[12].Offset));
            Assert.AreEqual("This", alignmentResults[13].Word);
            Assert.AreEqual("00:00:07.3152500", TimeSpanToString(alignmentResults[13].Offset));
            Assert.AreEqual("concludes", alignmentResults[14].Word);
            Assert.AreEqual("00:00:07.5860126", TimeSpanToString(alignmentResults[14].Offset));
            Assert.AreEqual("the", alignmentResults[15].Word);
            Assert.AreEqual("00:00:08.1952285", TimeSpanToString(alignmentResults[15].Offset));
            Assert.AreEqual("test", alignmentResults[16].Word);
            Assert.AreEqual("00:00:08.3983007", TimeSpanToString(alignmentResults[16].Offset));
        }

        [TestMethod]
        [DeploymentItem("TestData/speech_synth_bing_1.raw")]
        public void TestAuralexicalAlignmentBing1()
        {
            AudioSample audio = ReadRawAudioFile("speech_synth_bing_1.raw", AudioSampleFormat.Mono(16000));
            IWordBreaker wordBreaker = new EnglishWholeWordBreaker();
            ISpeechTimingEstimator timingEstimator = new EnglishSpeechTimingEstimator();
            string ssml = "I found these 3 results: John Clark Steakhouse, The Broiler, Walt Disney Presents The Crab Shack Featuring Star Wars. Which one did you want?";
            IList<SynthesizedWord> alignmentResults = SpeechUtils.EstimateSynthesizedWordTimings(audio, ssml, wordBreaker, timingEstimator, new DebugLogger());
            Assert.IsNotNull(alignmentResults);

            //for (int c = 0; c < alignmentResults.Count; c++)
            //{
            //    SynthesizedWord word = alignmentResults[c];
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", alignmentResults[{1}].Word);", word.Word, c);
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", TimeSpanToString(alignmentResults[{1}].Offset));", word.Offset, c);
            //}

            Assert.AreEqual("I", alignmentResults[0].Word);
            Assert.AreEqual("00:00:00.1014375", TimeSpanToString(alignmentResults[0].Offset));
            Assert.AreEqual("found", alignmentResults[1].Word);
            Assert.AreEqual("00:00:00.1787532", TimeSpanToString(alignmentResults[1].Offset));
            Assert.AreEqual("these", alignmentResults[2].Word);
            Assert.AreEqual("00:00:00.5653322", TimeSpanToString(alignmentResults[2].Offset));
            Assert.AreEqual("3", alignmentResults[3].Word);
            Assert.AreEqual("00:00:00.9519113", TimeSpanToString(alignmentResults[3].Offset));
            Assert.AreEqual("results", alignmentResults[4].Word);
            Assert.AreEqual("00:00:01.0292270", TimeSpanToString(alignmentResults[4].Offset));
            Assert.AreEqual("John", alignmentResults[5].Word);
            Assert.AreEqual("00:00:01.8275000", TimeSpanToString(alignmentResults[5].Offset));
            Assert.AreEqual("Clark", alignmentResults[6].Word);
            Assert.AreEqual("00:00:02.1080659", TimeSpanToString(alignmentResults[6].Offset));
            Assert.AreEqual("Steakhouse", alignmentResults[7].Word);
            Assert.AreEqual("00:00:02.4587731", TimeSpanToString(alignmentResults[7].Offset));
            Assert.AreEqual("The", alignmentResults[8].Word);
            Assert.AreEqual("00:00:03.4175625", TimeSpanToString(alignmentResults[8].Offset));
            Assert.AreEqual("Broiler", alignmentResults[9].Word);
            Assert.AreEqual("00:00:03.5929875", TimeSpanToString(alignmentResults[9].Offset));
            Assert.AreEqual("Walt", alignmentResults[10].Word);
            Assert.AreEqual("00:00:04.2578125", TimeSpanToString(alignmentResults[10].Offset));
            Assert.AreEqual("Disney", alignmentResults[11].Word);
            Assert.AreEqual("00:00:04.5275888", TimeSpanToString(alignmentResults[11].Offset));
            Assert.AreEqual("Presents", alignmentResults[12].Word);
            Assert.AreEqual("00:00:04.9322539", TimeSpanToString(alignmentResults[12].Offset));
            Assert.AreEqual("The", alignmentResults[13].Word);
            Assert.AreEqual("00:00:05.4718071", TimeSpanToString(alignmentResults[13].Offset));
            Assert.AreEqual("Crab", alignmentResults[14].Word);
            Assert.AreEqual("00:00:05.6741396", TimeSpanToString(alignmentResults[14].Offset));
            Assert.AreEqual("Shack", alignmentResults[15].Word);
            Assert.AreEqual("00:00:05.9439160", TimeSpanToString(alignmentResults[15].Offset));
            Assert.AreEqual("Featuring", alignmentResults[16].Word);
            Assert.AreEqual("00:00:06.2811367", TimeSpanToString(alignmentResults[16].Offset));
            Assert.AreEqual("Star", alignmentResults[17].Word);
            Assert.AreEqual("00:00:06.8881337", TimeSpanToString(alignmentResults[17].Offset));
            Assert.AreEqual("Wars", alignmentResults[18].Word);
            Assert.AreEqual("00:00:07.1579101", TimeSpanToString(alignmentResults[18].Offset));
            Assert.AreEqual("Which", alignmentResults[19].Word);
            Assert.AreEqual("00:00:08.2883750", TimeSpanToString(alignmentResults[19].Offset));
            Assert.AreEqual("one", alignmentResults[20].Word);
            Assert.AreEqual("00:00:08.5959960", TimeSpanToString(alignmentResults[20].Offset));
            Assert.AreEqual("did", alignmentResults[21].Word);
            Assert.AreEqual("00:00:08.7805693", TimeSpanToString(alignmentResults[21].Offset));
            Assert.AreEqual("you", alignmentResults[22].Word);
            Assert.AreEqual("00:00:08.9651425", TimeSpanToString(alignmentResults[22].Offset));
            Assert.AreEqual("want", alignmentResults[23].Word);
            Assert.AreEqual("00:00:09.1497158", TimeSpanToString(alignmentResults[23].Offset));
        }

        [TestMethod]
        [DeploymentItem("TestData/speech_synth_bing_2.raw")]
        public void TestAuralexicalAlignmentBing2()
        {
            AudioSample audio = ReadRawAudioFile("speech_synth_bing_2.raw", AudioSampleFormat.Mono(16000));
            IWordBreaker wordBreaker = new EnglishWholeWordBreaker();
            ISpeechTimingEstimator timingEstimator = new EnglishSpeechTimingEstimator();
            string ssml = "This is a test: 1, 2 --- 3; 4, 5! 6 - 7. This concludes the test.";
            IList<SynthesizedWord> alignmentResults = SpeechUtils.EstimateSynthesizedWordTimings(audio, ssml, wordBreaker, timingEstimator, new DebugLogger());
            Assert.IsNotNull(alignmentResults);

            //for (int c = 0; c < alignmentResults.Count; c++)
            //{
            //    SynthesizedWord word = alignmentResults[c];
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", alignmentResults[{1}].Word);", word.Word, c);
            //    Console.WriteLine("Assert.AreEqual(\"{0}\", TimeSpanToString(alignmentResults[{1}].Offset));", word.Offset, c);
            //}

            Assert.AreEqual("This", alignmentResults[0].Word);
            Assert.AreEqual("00:00:00.1100625", TimeSpanToString(alignmentResults[0].Offset));
            Assert.AreEqual("is", alignmentResults[1].Word);
            Assert.AreEqual("00:00:00.4125625", TimeSpanToString(alignmentResults[1].Offset));
            Assert.AreEqual("a", alignmentResults[2].Word);
            Assert.AreEqual("00:00:00.5638125", TimeSpanToString(alignmentResults[2].Offset));
            Assert.AreEqual("test", alignmentResults[3].Word);
            Assert.AreEqual("00:00:00.6394375", TimeSpanToString(alignmentResults[3].Offset));
            Assert.AreEqual("1", alignmentResults[4].Word);
            Assert.AreEqual("00:00:01.2000000", TimeSpanToString(alignmentResults[4].Offset));
            Assert.AreEqual("2", alignmentResults[5].Word);
            Assert.AreEqual("00:00:01.8055000", TimeSpanToString(alignmentResults[5].Offset));
            Assert.AreEqual("---", alignmentResults[6].Word);
            Assert.AreEqual("00:00:01.9818750", TimeSpanToString(alignmentResults[6].Offset));
            Assert.AreEqual("3", alignmentResults[7].Word);
            Assert.AreEqual("00:00:02.5110000", TimeSpanToString(alignmentResults[7].Offset));
            Assert.AreEqual("4", alignmentResults[8].Word);
            Assert.AreEqual("00:00:02.9428125", TimeSpanToString(alignmentResults[8].Offset));
            Assert.AreEqual("5", alignmentResults[9].Word);
            Assert.AreEqual("00:00:03.6014375", TimeSpanToString(alignmentResults[9].Offset));
            Assert.AreEqual("6", alignmentResults[10].Word);
            Assert.AreEqual("00:00:04.8822500", TimeSpanToString(alignmentResults[10].Offset));
            Assert.AreEqual("-", alignmentResults[11].Word);
            Assert.AreEqual("00:00:05.1455209", TimeSpanToString(alignmentResults[11].Offset));
            Assert.AreEqual("7", alignmentResults[12].Word);
            Assert.AreEqual("00:00:05.4087919", TimeSpanToString(alignmentResults[12].Offset));
            Assert.AreEqual("This", alignmentResults[13].Word);
            Assert.AreEqual("00:00:06.5281250", TimeSpanToString(alignmentResults[13].Offset));
            Assert.AreEqual("concludes", alignmentResults[14].Word);
            Assert.AreEqual("00:00:06.7744125", TimeSpanToString(alignmentResults[14].Offset));
            Assert.AreEqual("the", alignmentResults[15].Word);
            Assert.AreEqual("00:00:07.3285595", TimeSpanToString(alignmentResults[15].Offset));
            Assert.AreEqual("test", alignmentResults[16].Word);
            Assert.AreEqual("00:00:07.5132753", TimeSpanToString(alignmentResults[16].Offset));
        }

        private static string TimeSpanToString(TimeSpan ts)
        {
            return ts.ToString();
        }
    }
}
