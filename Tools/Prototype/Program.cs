namespace Prototype
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.LG.Statistical;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.Alignment;
    using Durandal.Common.NLP.ApproxString;
    using Durandal.Common.NLP.Feature;
    using Durandal.Common.Collections.Indexing;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.NLP.Language.English;
    using Durandal.Common.NLP.Search;
    using Durandal.Common.Packages;
    using Durandal.Common.Remoting;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Speech.TTS.Bing;
    using Durandal.Common.Statistics;
    using Durandal.Common.Test;
    using Durandal.Common.Test.FVT;
    using Durandal.Common.Utils;
    using Durandal.Common.Cache;
    using Durandal.Common.IO;
    using Durandal.Common.MathExt;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Time.TimeZone;
    using Durandal.Common.UnitConversion;
    using Durandal.Extensions.BondProtocol;
    using Durandal.ExternalServices.Bing;
    using Durandal.ExternalServices.Bing.Speller;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Extensions.MySql;
    using System.Globalization;
    using Durandal.Common.Test.Builders;
    using Durandal.Common.Speech;
    using Durandal.Common.Audio.Components;
    using Durandal.Common.Collections;
    using Durandal.Common.IO.Json;
    using System.Diagnostics.Eventing.Reader;
    using Durandal.Common.Config.Accessors;
    using Durandal.Common.Security.OAuth;
    using Durandal.Common.Ontology;
    using Durandal;
    using Durandal.Common.Instrumentation.Profiling;
    using System.Net.NetworkInformation;
    using Newtonsoft.Json.Linq;
    using Durandal.Extensions.BassAudio;
    using Durandal.Common.Remoting.Protocol;
    using System.Security.Cryptography;
    using Durandal.Common.Security;
    using Durandal.Extensions.NativeAudio.Codecs;
    using Durandal.Extensions.NativeAudio.Components;
    using Durandal.Extensions.Sapi;
    using Durandal.Common.Audio.Components.Noise;
    using System.Security.Cryptography.X509Certificates;
    using Durandal.Extensions.NAudio.Devices;
    using System.Timers;
    using Durandal.Common.Net.Http2;
    using Durandal.Common.ServiceMgmt;
    using Durandal.ExternalServices.Twilio;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using Durandal.Common.Audio.Codecs.Opus;
    using Durandal.Common.Audio.Hardware;
    using Durandal.Extensions.NAudio;
    using Durandal.Common.Utils.NativePlatform;
    using Durandal.Common.Dialog.Services;

    public class Program
    {
        public static void Main(string[] args)
        {
            //NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            //NativePlatformUtils.PrepareNativeLibrary("fakelib", new ConsoleLogger("Main", LogLevel.All));
            //AssemblyReflector.ApplyAccelerators(typeof(NativeOpusAccelerator).Assembly, new ConsoleLogger("Main", LogLevel.All));
            //return;

            //PhoneProxy().Await();
            Console.WriteLine("Durandal codebase is now " + CountCodeLinesRecursive(new DirectoryInfo(@"C:\Code\Durandal")) + " lines of code");
            //var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(Benchmarks));
            //SpeechRecoTester tester = new SpeechRecoTester("");
            //tester.Run().Await();
            //PerfCounterTest().Await();
            //HttpInstrumentation().Await();
            //HttpV2Tests.TestSocketServer().Await();
            //TTSTest().Await();
            //NoiseGeneration().Await();
            //LGTestConsole().Await();
            //Timezones();
        }

        public static async Task PhoneProxy()
        {
            ILogger logger = new ConsoleLogger();
            IHttpClientFactory httpClientFactory = new PortableHttpClientFactory();
            TwilioInterface twilio = new TwilioInterface("7ed9f49f5a70501b36601b56e80e8e79", "ACfff69aa5bafa82e493afccf48d7ca380", httpClientFactory, logger.Clone("Twilio"));
            while (true)
            {
                Console.WriteLine("Enter text to send");
                string line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                Console.WriteLine("Sending message: " + line);
                await twilio.SendSMS("+14252926970", "+14254636482", line, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                await Task.Delay(TimeSpan.FromMinutes(10));
            }
        }

        public static async Task DriveSpeakers()
        {
            ILogger logger = new ConsoleLogger();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;

            IRandom rand = new FastRandom();
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);

            IAudioDriver audioDeviceDriver = new BassDeviceDriver(logger.Clone("BassDriver"));
            using (IAudioGraph micGraph = new AudioGraph(AudioGraphCapabilities.Concurrent | AudioGraphCapabilities.Instrumented, new StutterReportingInstrumentationDelegate(logger).HandleInstrumentation))
            using (IAudioGraph speakerGraph = new AudioGraph(AudioGraphCapabilities.Concurrent | AudioGraphCapabilities.Instrumented, new StutterReportingInstrumentationDelegate(logger).HandleInstrumentation))
            //using (FfmpegAudioSampleSource programAudio = await FfmpegAudioSampleSource.Create(new WeakPointer<IAudioGraph>(speakerGraph), format, "ProgramAudio", new FileInfo(@"C:\Code\Durandal\Data\Warning call.mp3")))
            //using (WaveOutPlayer speakers = new WaveOutPlayer(new WeakPointer<IAudioGraph>(speakerGraph), format, "Speakers", logger.Clone("Speakers")))
            //using (DirectSoundPlayer speakers = new DirectSoundPlayer(new WeakPointer<IAudioGraph>(speakerGraph), format, "Speakers", logger.Clone("Speakers"), "12d8bc4a-275a-4fed-8cbe-eeaa8ca5d7e9"))
            using (IAudioRenderDevice speakers = audioDeviceDriver.OpenRenderDevice(null, new WeakPointer<IAudioGraph>(speakerGraph), format, "Speakers"))
            {
                //programAudio.ConnectOutput(speakers);

                Task st = speakers.StartPlayback(realTime);
                await st;

                await Task.Delay(3000);
            }
        }

        public static async Task TestHttp2LocalClient()
        {
            ILogger logger = new ConsoleLogger("Test");
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            ISocketFactory socketFactory = new PooledTcpClientSocketFactory(logger.Clone("SocketFactory"), NullMetricCollector.Singleton, DimensionSet.Empty, ignoreCertErrors: true);

            TcpConnectionConfiguration connectionParams = new TcpConnectionConfiguration()
            {
                DnsHostname = "localhost",
                SslHostname = "localhost",
                Port = 60000,
                UseTLS = true,
                NoDelay = false,
                ReportHttp2Capability = true
            };

            List<Task> parallelTasks = new List<Task>();
            for (int thread = 0; thread < 8; thread++)
            {
                parallelTasks.Add(Task.Run(async () =>
                {
                    byte[] randomData = new byte[300000];
                    new FastRandom().NextBytes(randomData);
                    using (IHttpClient httpClient = new SocketHttpClient(
                        new WeakPointer<ISocketFactory>(socketFactory),
                        connectionParams,
                        logger.Clone("HttpClient"),
                        NullMetricCollector.WeakSingleton,
                        DimensionSet.Empty,
                        Http2SessionManager.Default,
                        new Http2SessionPreferences()))
                    {
                        RateLimiter rateLimiter = new RateLimiter(100, 100);
                        Stopwatch timer = new Stopwatch();
                        while (true)
                        {
                            timer.Restart();
                            try
                            {
                                HttpRequest request = HttpRequest.CreateOutgoing("/rand", "POST");
                                request.SetContent(randomData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                                Task<HttpResponse> requestTask = httpClient.SendRequestAsync(request);
                                while (!requestTask.IsFinished())
                                {
                                    await Task.Delay(50);
                                    if (timer.ElapsedMilliseconds > 5000)
                                    {
                                        timer.Restart();
                                        logger.Log("Request task is taking a while", LogLevel.Wrn);
                                    }
                                }

                                using (HttpResponse resp = await requestTask.ConfigureAwait(false))
                                {
                                    if (resp == null || resp.ResponseCode != 200)
                                    {
                                    }
                                    else
                                    {
                                        timer.Restart();
                                        Task<ArraySegment<byte>> readContentTask = resp.ReadContentAsByteArrayAsync(cancelToken, realTime);
                                        while (!readContentTask.IsFinished())
                                        {
                                            await Task.Delay(50);
                                            if (timer.ElapsedMilliseconds > 1000)
                                            {
                                                timer.Restart();
                                                logger.Log("Read content task is taking a while");
                                            }
                                        }

                                        ArraySegment<byte> content = await readContentTask.ConfigureAwait(false);
                                        logger.Log("Got HTTP response with length " + content.Count);
                                        await resp.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                                    }
                                }
                            }
                            catch (Exception)
                            {
                            }

                            timer.Stop();
                            rateLimiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                        }
                    }
                }));

                await Task.Delay(TimeSpan.FromMilliseconds(1000));
            }

            foreach (Task t in parallelTasks)
            {
                await t.ConfigureAwait(false);
            }
        }

        public static async Task NoiseGeneration()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream outputStream = new FileStream(@"C:\Code\Durandal\Data\noise.wav", FileMode.Create, FileAccess.Write))
            using (NonRealTimeStream nrtOutputWrapper = new NonRealTimeStreamWrapper(outputStream, false))
            using (AudioEncoder wavEncoder = new RiffWaveEncoder(new WeakPointer<IAudioGraph>(graph), format, "WaveWriter", NullLogger.Singleton))
            using (NoiseSampleSource sampleSource = new NoiseSampleSource(new WeakPointer<IAudioGraph>(graph), format,
                new RetroNoiseGenerator(format, maxAmplitude: 0.25f, frequency: 45),
                "NoiseSource"))
            {
                await wavEncoder.Initialize(nrtOutputWrapper, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                sampleSource.ConnectOutput(wavEncoder);
                for (int c = 0; c < 50; c++)
                {
                    await wavEncoder.ReadFromSource(4800, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await wavEncoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        public static async Task NativeOpusTest()
        {
            ILogger logger = new ConsoleLogger();
            new NativeOpusAccelerator().Apply(logger);
            OpusRawCodecFactory codecFactory = new OpusRawCodecFactory(logger.Clone("OpusCodec"));
            using (Stream waveIn = new FileStream(@"C:\Code\Durandal\Data\Sine Sweep 48k.wav", FileMode.Open, FileAccess.Read))
            {
                AudioSample inputSample = await AudioHelpers.ReadWaveFromStream(waveIn);
                await AudioHelpers.EncodeAudioSampleUsingCodec(inputSample, codecFactory, "opus", logger);
            }
        }

        public static async Task NativeFlacTest()
        {
            ILogger logger = new ConsoleLogger();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            NativeFlacCodecFactory codecFactory = new NativeFlacCodecFactory(logger.Clone("FlacCodec"));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FfmpegAudioSampleSource programAudio = await FfmpegAudioSampleSource.Create(new WeakPointer<IAudioGraph>(graph), "ProgramAudio", logger.Clone("Ffmpeg"), new FileInfo(@"C:\Code\Durandal\Data\Warning call.mp3")))
            using (AudioEncoder flacEncoder = codecFactory.CreateEncoder("flac", new WeakPointer<IAudioGraph>(graph), format, logger.Clone("FlacEncoder"), "FlacNativeEncoder"))
            using (Stream encoderOutStream = new FileStream(@"C:\Code\Durandal\Data\test.flac", FileMode.Create, FileAccess.Write))
            {
                await flacEncoder.Initialize(encoderOutStream, false, cancelToken, realTime);
                programAudio.ConnectOutput(flacEncoder);

                Stopwatch timer = Stopwatch.StartNew();
                while (!programAudio.PlaybackFinished)
                {
                    await programAudio.WriteSamplesToOutput(960, cancelToken, realTime);
                }

                await flacEncoder.Finish(cancelToken, realTime);
                timer.Stop();
                logger.Log("Time taken " + timer.ElapsedMillisecondsPrecise());
            }

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream inputStream = new FileStream(@"C:\Code\Durandal\Data\test.flac", FileMode.Open, FileAccess.Read))
            using (NonRealTimeStream nrtInputWrapper = new NonRealTimeStreamWrapper(inputStream, false))
            using (FileStream outputStream = new FileStream(@"C:\Code\Durandal\Data\flac_decode.wav", FileMode.Create, FileAccess.Write))
            using (NonRealTimeStream nrtOutputWrapper = new NonRealTimeStreamWrapper(outputStream, false))
            using (AudioEncoder wavEncoder = new RiffWaveEncoder(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Stereo(48000), "WaveWriter", logger))
            using (AudioDecoder flacDecoder = codecFactory.CreateDecoder("flac", "samplerate=48000 q=0 channels=2 layout=2", new WeakPointer<IAudioGraph>(graph), logger.Clone("FlacDecoder"), "FlacNativeDecoder"))
            {
                await flacDecoder.Initialize(nrtInputWrapper, false, cancelToken, realTime);
                await wavEncoder.Initialize(nrtOutputWrapper, false, cancelToken, realTime);
                flacDecoder.ConnectOutput(wavEncoder);
                await wavEncoder.ReadFully(cancelToken, realTime, TimeSpan.FromMilliseconds(100));
                await wavEncoder.Finish(cancelToken, realTime);
            }
        }

        public static async Task PerfCounterTest()
        {
            ILogger coreLogger = new ConsoleLogger();
            MetricCollector metrics = new MetricCollector(coreLogger.Clone("Metrics"), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            metrics.AddMetricOutput(new ConsoleMetricOutput());
            metrics.AddMetricSource(new WindowsPerfCounterReporter(coreLogger, DimensionSet.Empty, WindowsPerfCounterSet.BasicCurrentProcess));
            SystemThreadPoolObserver observer = new SystemThreadPoolObserver(metrics, DimensionSet.Empty, coreLogger.Clone("ThreadPoolObserver"));
            RateLimiter limiter = new RateLimiter(10, 10);
            await DurandalTaskExtensions.NoOpTask;
            while (true)
            {
                limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                Task.Run(() =>
                {
                    Stopwatch timer = Stopwatch.StartNew();
                    while (timer.ElapsedMilliseconds < 200)
                    {
                        Math.Sin(timer.ElapsedTicks);
                    }

                    timer.Stop();
                }).Forget(coreLogger);

                Task.Run(() =>
                {
                    Thread.Sleep(200);
                }).Forget(coreLogger);
            }
        }

        public static void BenchmarkThreadPool()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            //MovingPercentile lockPercentiles = new MovingPercentile(1000, 0.25, 0.5, 0.75, 0.95, 0.99, 0.999);
            using (IThreadPool threadPool = new CustomThreadPool(logger.Clone("ThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Lowest, "ThreadPool")) // 78% sync, 396 async, 65% mixed
            //using (IThreadPool threadPool = new TaskThreadPool()) // 85% sync, 1037 async, 87% mixed
            //using (IThreadPool threadPool = new SystemThreadPool(logger.Clone("ThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty)) // 88% sync, 2559 async, 86% mixed
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Stopwatch timer = Stopwatch.StartNew();
                const int NUM_WORK_ITEMS = 100000;
                for (int c = 0; c < NUM_WORK_ITEMS; c++)
                {
                    //Console.WriteLine(threadPool.GetStatus());
                    threadPool.EnqueueUserWorkItem(() =>
                    {
                        Stopwatch threadWork = Stopwatch.StartNew();
                        while (threadWork.ElapsedMilliseconds < 1)
                        {
                            Math.Sin(100);
                        }
                        threadWork.Stop();
                    });
                    //threadPool.EnqueueUserAsyncWorkItem(async () =>
                    //{
                    //    Stopwatch threadWork = Stopwatch.StartNew();
                    //    while (threadWork.ElapsedMilliseconds < 1)
                    //    {
                    //        await Task.Yield();
                    //    }
                    //    threadWork.Stop();
                    //});
                }
                while (threadPool.TotalWorkItems > 0)
                {
                    //Console.WriteLine(threadPool.GetStatus());
                }
                //SpinWait.SpinUntil(() => );
                timer.Stop();
                double throughput = (double)NUM_WORK_ITEMS / timer.ElapsedMillisecondsPrecise();
                Console.WriteLine("Queue rate was " + throughput + " work items per ms");
                Console.WriteLine("Efficiency was " + (100.0 * throughput / (double)Environment.ProcessorCount) + " %");
            }

            //Console.WriteLine("Final percentiles: " + lockPercentiles.ToString());

            //DateTimeOffset traceStart = DateTimeOffset.Parse("2021-04-03T03:50:52.766");
            //DateTimeOffset stutterStart = DateTimeOffset.Parse("2021-04-03T03:51:00.75525");
            //DateTimeOffset stutterEnd = DateTimeOffset.Parse("2021-04-03T03:51:01.83656");
            //Console.WriteLine((stutterStart - traceStart).PrintTimeSpan());
            //Console.WriteLine((stutterEnd - traceStart).PrintTimeSpan());

            //ReproAppdomainStutter().Await();
            //FileInfo profilerOutputFileName = new FileInfo(@"C:\Code\Durandal\target\Durandal.DialogEngine_2021-04-13T23-24-59.microprofile");
            //using (Stream binaryFileIn = new FileStream(profilerOutputFileName.FullName, FileMode.Open, FileAccess.Read))
            //using (Stream textFileOut = new FileStream(profilerOutputFileName.FullName + ".log", FileMode.Create, FileAccess.Write))
            //using (StreamWriter logWriter = new StreamWriter(textFileOut))
            //{
            //    MicroProfileReader reader = new MicroProfileReader(binaryFileIn);
            //    string nextMessage;
            //    do
            //    {
            //        nextMessage = reader.ReadNextEvent().Await();
            //        if (nextMessage != null)
            //        {
            //            logWriter.WriteLine(nextMessage);
            //        }
            //    }
            //    while (nextMessage != null);
            //}

            //PackagerTest();
            //AsyncMain().Await();
            //ProfileKeepalivePing().Await();
        }

        private static void CustomThreadPoolStuff()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            using (IThreadPool threadPool = new CustomThreadPool(
                logger.Clone("ThreadPool"),
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                ThreadPriority.Normal,
                "ThreadPool",
                4))
            {
                int workItemNum = 0;
                for (int c = 0; c < 8; c++)
                {
                    //threadPool.EnqueueUserWorkItem(() =>
                    //{
                    //    Console.WriteLine("Running sync work item on thread " + Thread.CurrentThread.ManagedThreadId);
                    //});
                    threadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        int thisWorkItem = Interlocked.Increment(ref workItemNum);
                        for (int stage = 0; stage < 10; stage++)
                        {
                            Console.WriteLine("Async work item " + thisWorkItem + " stage " + stage + " running on thread " + Thread.CurrentThread.ManagedThreadId + " and scheduler " + TaskScheduler.Current.Id);
                            await Task.Yield();
                        }
                    });
                }

                Thread.Sleep(5000);
            }
        }

        public static async Task ProfileKeepalivePing()
        {
            FileInfo profilerOutputFileName;
            ILogger coreLogger = new ConsoleLogger();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            DimensionSet dimensions = DimensionSet.Empty;
            using (FileMicroProfilerClient mpClient = await FileMicroProfilerClient.CreateAsync("Prototype"))
            {
                //MicroProfiler.Initialize(mpClient, coreLogger);
                profilerOutputFileName = mpClient.OutputFileName;
                MetricCollector metrics = new MetricCollector(coreLogger.Clone("Metrics"), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
                //metrics.AddMetricOutput(new ConsoleMetricOutput());
                metrics.AddMetricOutput(new FileMetricOutput(coreLogger.Clone("Metrics"), "Prototype"));
                BufferPool<byte>.Metrics = metrics;
                MovingPercentile keepAlivePercentiles = new MovingPercentile(1000, 0.25, 0.5, 0.75, 0.95, 0.99, 0.999);

                ISocket serverSocket = new MMIOServerSocket(coreLogger.Clone("MMIOServer"), new WeakPointer<IMetricCollector>(metrics), dimensions);
                ISocket clientSocket = new MMIOClientSocket(serverSocket.RemoteEndpointString, coreLogger.Clone("MMIOClient"), metrics, dimensions);
                //ISocket serverSocket = new AnonymousPipeServerSocket(65536, true);
                //ISocket clientSocket = new AnonymousPipeClientSocket(serverSocket.RemoteEndpointString, true);
                PostOffice serverPostOffice = new PostOffice(
                    serverSocket,
                    coreLogger.Clone("ServerPostOffice"),
                    TimeSpan.FromMilliseconds(1000),
                    isServer: true,
                    realTime: realTime,
                    metrics: new WeakPointer<IMetricCollector>(metrics),
                    metricDimensions: dimensions,
                    useDedicatedThread: true);

                PostOffice clientPostOffice = new PostOffice(
                    clientSocket,
                    coreLogger.Clone("ClientPostOffice"),
                    TimeSpan.FromMilliseconds(1000),
                    isServer: false,
                    realTime: realTime,
                    metrics: new WeakPointer<IMetricCollector>(metrics),
                    metricDimensions: dimensions,
                    useDedicatedThread: true);

                IRemoteDialogProtocol remotingProtocol = new BondRemoteDialogProtocol();

                RemoteDialogExecutorServer server = new RemoteDialogExecutorServer(
                    coreLogger.Clone("DialogExecutorServer"),
                    new WeakPointer<PostOffice>(serverPostOffice),
                    new BasicPluginLoader(new BasicDialogExecutor(false), NullFileSystem.Singleton),
                    new IRemoteDialogProtocol[] { remotingProtocol },
                    new WeakPointer<IThreadPool>(new TaskThreadPool()),
                    NullFileSystem.Singleton,
                    new NullHttpClientFactory(),
                    new CodeDomLGScriptCompiler(),
                    new NLPToolsCollection(),
                    new WeakPointer<IMetricCollector>(metrics),
                    dimensions);

                server.Start(realTime);

                Stopwatch latencyTimer = new Stopwatch();
                Stopwatch overallTimer = Stopwatch.StartNew();
                while (overallTimer.Elapsed < TimeSpan.FromSeconds(20))
                {
                    latencyTimer.Restart();
                    DateTimeOffset startTime = HighPrecisionTimer.GetCurrentUTCTime();
                    await SendKeepAlive(CancellationToken.None, realTime, coreLogger, remotingProtocol, clientPostOffice, metrics, dimensions).ConfigureAwait(false);
                    DateTimeOffset endTime = HighPrecisionTimer.GetCurrentUTCTime();
                    latencyTimer.Stop();
                    metrics.ReportPercentile("KeepAlive", DimensionSet.Empty, latencyTimer.ElapsedMillisecondsPrecise());
                    keepAlivePercentiles.Add(latencyTimer.ElapsedMillisecondsPrecise());
                }

                server.Stop();
                Console.WriteLine("Final percentiles: " + keepAlivePercentiles.ToString());
            }

            //using (Stream binaryFileIn = new FileStream(profilerOutputFileName.FullName, FileMode.Open, FileAccess.Read))
            //using (Stream textFileOut = new FileStream(profilerOutputFileName.FullName + ".log", FileMode.Create, FileAccess.Write))
            //using (StreamWriter logWriter = new StreamWriter(textFileOut))
            //{
            //    MicroProfileReader reader = new MicroProfileReader(binaryFileIn);
            //    string nextMessage;
            //    do
            //    {
            //        nextMessage = await reader.ReadNextEvent();
            //        if (nextMessage != null)
            //        {
            //            logWriter.WriteLine(nextMessage);
            //        }
            //    }
            //    while (nextMessage != null);
            //}
        }

        private static async Task<bool> SendKeepAlive(
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            ILogger logger,
            IRemoteDialogProtocol remotingProtocol,
            PostOffice postOffice,
            IMetricCollector metrics,
            DimensionSet metricDimensions)
        {
            KeepAliveRequest remoteRequest = new KeepAliveRequest();
            PooledBuffer<byte> serializedRequest = remotingProtocol.Serialize(remoteRequest, logger);
            MailboxId transientMailbox = postOffice.CreateTransientMailbox(realTime);
            MailboxMessage message = new MailboxMessage(transientMailbox, remotingProtocol.ProtocolId, serializedRequest);
            uint operationId = MicroProfiler.GenerateOperationId();
            MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_SendRequestStart, operationId);
            uint messageId = postOffice.GenerateMessageId();
            message.MessageId = messageId;
            long startTime = HighPrecisionTimer.GetCurrentTicks();
            await postOffice.SendMessage(message, cancelToken, realTime);
            MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_SendRequestFinish, operationId);
            if (cancelToken.IsCancellationRequested)
            {
                return false;
            }

            MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_RecvResponseStart, operationId);
            RetrieveResult<MailboxMessage> response = await postOffice.TryReceiveMessage(transientMailbox, cancelToken, TimeSpan.FromMilliseconds(1000), realTime).ConfigureAwait(false);
            long endTime = HighPrecisionTimer.GetCurrentTicks();
            MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_RecvResponseFinish, operationId);

            if (cancelToken.IsCancellationRequested)
            {
                return false;
            }

            if (response == null || !response.Success)
            {
                return false;
            }

            MailboxMessage responseMessage = response.Result;
            if (responseMessage == null || responseMessage.ReplyToId != messageId)
            {
                return false;
            }

            Tuple<object, Type> parsedResponse = remotingProtocol.Parse(responseMessage.Buffer, logger);
            if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<long>))
            {
                return false;
            }

            RemoteProcedureResponse<long> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<long>;
            if (finalResponse.Exception != null)
            {
                return false;
            }

            TimeSpan roundTripTime = TimeSpan.FromTicks(endTime - startTime);
            metrics.ReportPercentile(CommonInstrumentation.Key_Counter_KeepAlive_RoundTripTime, metricDimensions, roundTripTime.TotalMilliseconds);
            return true;
        }

        public static async Task CaptureLoopback()
        {
            ILogger logger = new ConsoleLogger();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;

            IRandom rand = new FastRandom();
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            IAudioDriver audioDeviceDriver = new WasapiDeviceDriver(logger.Clone("WasapiDriver"));
            using (IAudioGraph micGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioGraph speakerGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FfmpegAudioSampleSource programAudio = await FfmpegAudioSampleSource.Create(new WeakPointer<IAudioGraph>(speakerGraph), "ProgramAudio", logger.Clone("Ffmpeg"), new FileInfo(@"C:\Code\Durandal\Data\Warning call.mp3")))
            using (IAudioRenderDevice speakers = audioDeviceDriver.OpenRenderDevice(null, new WeakPointer<IAudioGraph>(speakerGraph), format, "Speakers"))
            using (IAudioCaptureDevice microphone = audioDeviceDriver.OpenCaptureDevice(null, new WeakPointer<IAudioGraph>(micGraph), format, "Microphone"))
            using (FeedbackDelayEstimator estimator = new FeedbackDelayEstimator(new WeakPointer<IAudioGraph>(micGraph), new WeakPointer<IAudioGraph>(speakerGraph), format, format, format, 48000, "Feedback", logger.Clone("Delay")))
            using (AudioEncoder alignedOutputEncoder = new RiffWaveEncoder(new WeakPointer<IAudioGraph>(micGraph), new AudioSampleFormat(48000, 2, MultiChannelMapping.Packed_2Ch), "AlignedOutputEncoder", logger.Clone("RiffOut")))
            using (Stream alignedDataStream = new FileStream(@"C:\Code\Durandal\Data\aligned.wav", FileMode.Create, FileAccess.Write))
            {
                await alignedOutputEncoder.Initialize(alignedDataStream, false, cancelToken, realTime).ConfigureAwait(false);
                programAudio.ConnectOutput(estimator.ProgramAudioInput);
                estimator.ProgramAudioOutput.ConnectOutput(speakers);

                microphone.ConnectOutput(estimator.MicrophoneInput);
                estimator.AlignedFeedbackOutput.ConnectOutput(alignedOutputEncoder);

                Task st = speakers.StartPlayback(realTime);
                Task mt = microphone.StartCapture(realTime);
                await st;
                await mt;

                await Task.Delay(30000);

                await speakers.StartPlayback(realTime);
                await microphone.StopCapture();
                await alignedOutputEncoder.Finish(cancelToken, realTime);
            }
        }

        private static ISocket CreateMMIOClientSocket(string socketConnectionString, ILogger logger, IMetricCollector metrics, DimensionSet metricDimensions)
        {
            return new MMIOClientSocket(socketConnectionString, logger.Clone("MMIOClient"), metrics, metricDimensions);
        }

        public static async Task TranscodeAudioToWave(string inputFile, string outputFile)
        {
            ILogger logger = new ConsoleLogger();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;

            IRandom rand = new FastRandom();
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);

            // Convert input file to an audio sample
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FfmpegAudioSampleSource source = await FfmpegAudioSampleSource.Create(new WeakPointer<IAudioGraph>(graph), "AudioSource", logger.Clone("Ffmpeg"), new FileInfo(inputFile)))
            using (BiquadFilter filter = new Durandal.Common.Audio.Components.LowPassFilter(new WeakPointer<IAudioGraph>(graph), format, "TestFilter", 300f))
            using (FileStream fileWriteStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
            using (NonRealTimeStreamWrapper nrtStream = new NonRealTimeStreamWrapper(fileWriteStream, false))
            using (AudioEncoder encoder = new RiffWaveEncoder(new WeakPointer<IAudioGraph>(graph), format, "WaveEncoder", logger))
            {
                await encoder.Initialize(nrtStream, false, cancelToken, realTime);
                source.ConnectOutput(filter);
                filter.ConnectOutput(encoder);
                await encoder.ReadFully(cancelToken, realTime, TimeSpan.FromMilliseconds(100));
            }
        }

        private static void GenerateSineWave(
           float[] buffer,
           int sampleRate,
           float frequencyHz,
           int numSamplesPerChannelToWrite,
           int startOffsetSamplesPerChannel = 0,
           int channel = 0,
           int numChannels = 1,
           float amplitude = 1,
           float phase = 0)
        {
            phase = phase * (float)Math.PI * 2;
            float phaseIncrement = (float)(Math.PI * 2) / (float)sampleRate * (float)frequencyHz;
            int writeIdx = (startOffsetSamplesPerChannel * numChannels) + channel;
            for (int c = 0; c < numSamplesPerChannelToWrite; c++)
            {
                buffer[writeIdx] += ((float)Math.Sin(phase) * amplitude);
                phase += phaseIncrement;
                writeIdx += numChannels;
            }
        }

        public static void ReproTtsStutter()
        {
            ILogger coreLogger = new ConsoleLogger();
            AudioSampleFormat audioFormat = AudioSampleFormat.Mono(16000);
            MetricCollector metrics = new MetricCollector(coreLogger.Clone("Metrics"), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            metrics.AddMetricOutput(new ConsoleMetricOutput());
            IThreadPool threadPool = new TaskThreadPool(new WeakPointer<IMetricCollector>(metrics), DimensionSet.Empty, "Speech");
            ISpeechSynth speechSynth = new SapiSpeechSynth(
                coreLogger.Clone("DialogTTS"),
                new WeakPointer<IThreadPool>(threadPool),
                audioFormat,
                new WeakPointer<IMetricCollector>(metrics),
                DimensionSet.Empty,
                speechPoolSize: 4);

            metrics.AddMetricOutput(new ConsoleMetricOutput());
            IRandom rand = new FastRandom();
            string[] ssml = new string[]
            {
                "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">I am not programmed to do that.</speak>",
                //"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">My name is Durandal.</speak>",
                //"<speak>Why was there a spider in the computer case? It was labeled \"Web server.\"</speak>",
                //"<speak>There is no road. I am the chicken. Time is an illusion and the universe is a hologram.</speak>",
                //"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">I am not programmed to do that.</speak>",
                //"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">I'm afraid I can't help you with that.</speak>",
                //"<speak>I just bought the world's worst thesaurus. Not only is it terrible, it's terrible. Also this is a really long phrase that I want to see how it gets handled in TTS</speak>",
            };

            IThreadPool backgroundThreadPool = new FixedCapacityThreadPool(
                new TaskThreadPool(),
                NullLogger.Singleton,
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                "FixedAudioPool",
                4,
                ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable);

            RateLimiter limiter = new RateLimiter(10, 10);
            while (true)
            {
                backgroundThreadPool.EnqueueUserAsyncWorkItem(async () =>
                {
                    Stopwatch timer = Stopwatch.StartNew();
                    SpeechSynthesisRequest synthRequest = new SpeechSynthesisRequest()
                    {
                        Ssml = ssml[rand.NextInt(0, ssml.Length)],
                        Locale = LanguageCode.EN_US,
                        VoiceGender = VoiceGender.Unspecified
                    };

                    using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
                    using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(audioGraph), audioFormat, "SpeechOutputBucket"))
                    using (IAudioSampleSource synthSource = await speechSynth.SynthesizeSpeechToStreamAsync(synthRequest, new WeakPointer<IAudioGraph>(audioGraph), CancellationToken.None, DefaultRealTimeProvider.Singleton, NullLogger.Singleton))
                    {
                        synthSource.ConnectOutput(bucket);
                        await bucket.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                        timer.Stop();
                        metrics.ReportPercentile("TTSTime", DimensionSet.Empty, timer.ElapsedMillisecondsPrecise());
                    }
                });
            }
        }

        private static async Task InsertTestUsers()
        {
            ILogger logger = new ConsoleLogger();
            string connectionString = "server=10.123.112.94;port=3306;database=durandaldev;user id=durandal;password=inkzvEID115;allowbatch=True;pooling=True;compress=True;characterset=utf8";
            MySqlConnectionPool pool = await MySqlConnectionPool.Create(connectionString, logger.Clone("ConnectionPool"), NullMetricCollector.Singleton, DimensionSet.Empty);
            MySqlFunctionalTestIdentityStore identityStore = new MySqlFunctionalTestIdentityStore(pool, NullMetricCollector.Singleton, DimensionSet.Empty, logger.Clone("IdentityStore"));
            await identityStore.InsertRandomIdentities(logger);
        }

        public static void HttpMetrics()
        {
            ILogger logger = new ConsoleLogger();
            MetricCollector metricCollector = new MetricCollector(logger.Clone("MetricCollector"), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
            DimensionSet dimensions = new DimensionSet(new MetricDimension("key", "value"));
            //WindowsPerfCounterReporter perfCounters = new WindowsPerfCounterReporter(logger.Clone("WindowsCounters"), dimensions);
            //metricCollector.AddMetricSource(perfCounters);
            metricCollector.AddMetricOutput(new ConsoleMetricOutput());

            PortableHttpClientFactory fac = new PortableHttpClientFactory(new WeakPointer<IMetricCollector>(metricCollector), dimensions);
            IHttpClient client = fac.CreateHttpClient(new Uri("https://www.bing.com"));
            while (true)
            {
                Thread.Sleep(1000);
                client.SendRequestAsync(HttpRequest.CreateOutgoing("/")).Await();
            }
        }

        public static async Task RunFvtTest()
        {
            ILogger logger = new ConsoleLogger("FVTTestDriver");
            IHttpClientFactory httpClientFactory = new PortableHttpClientFactory();
            IFunctionalTestIdentityStore identityStore = new BasicFunctionalTestIdentityStore();
            //ISocketFactory socketFactory = new PooledTcpClientSocketFactory(logger.Clone("SocketFactory"));
            //ISocketFactory socketFactory = new RawTcpSocketFactory(logger.Clone("SocketFactory"));
            //IHttpClientFactory httpClientFactory = new SocketHttpClientFactory(socketFactory, DefaultRealTimeProvider.Singleton);
            IHttpClient httpClient = httpClientFactory.CreateHttpClient(new Uri("https://durandal-ai.net:62292"), logger.Clone("HttpClient"));
            IDialogTransportProtocol dialogProtocol = new DialogBondTransportProtocol();
            FunctionalTestDriver driver = new FunctionalTestDriver(logger, httpClient, dialogProtocol, identityStore, TimeSpan.FromSeconds(1));
            FunctionalTest test = JsonConvert.DeserializeObject<FunctionalTest>(File.ReadAllText(@"D:\Code\Durandal\Plugins\BasicPlugins\fvt\chitchat hello.json"));
            for (int c = 0; c < 10; c++)
            {
                FunctionalTestResult allTestResult = await driver.RunTest(test, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                await Task.Delay(1000);
            }
        }

        public static void HotfixConfig()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            IFileSystem fs = new InMemoryFileSystem();
            VirtualPath iniFilePath = new VirtualPath("DurandalClient_config.ini");

            fs.WriteLines(iniFilePath, new string[]
                {
                    "[Type|String]",
                    "testKey=initial_value"
                });

            byte[] newContents = Encoding.UTF8.GetBytes("[Type|String]\r\ntestKey=external_value\r\n");

            IConfiguration config = IniFileConfiguration.Create(logger.Clone("Config"), iniFilePath, fs, DefaultRealTimeProvider.Singleton, true, true).Await();

            while (true)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    logger.Log("Value is currently " + config.GetString("testKey"));
                }
                else if (string.Equals("write", line))
                {
                    Task.Run(async () =>
                    {
                        logger.Log("Starting to save contents...");
                        using (Stream writeStream = fs.OpenStream(iniFilePath, FileOpenMode.Create, FileAccessMode.Write))
                        {
                            for (int c = 0; c < newContents.Length; c++)
                            {
                                writeStream.Write(newContents, c, 1);
                                writeStream.Flush();
                                await Task.Delay(100);
                            }
                        }

                        logger.Log("Finished saving contents.");
                    });
                }
                else
                {
                    config.Set("testKey", line);
                    logger.Log("Value changed to " + config.GetString("testKey"));
                }
            }
        }

        public static void PostOfficePerf()
        {
            ILogger logger = new ConsoleLogger();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            MetricCollector metricCollector = new MetricCollector(logger, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), realTime);
            metricCollector.AddMetricOutput(new ConsoleMetricOutput());
            DimensionSet rootDimensions = DimensionSet.Empty;
            WindowsPerfCounterReporter windowsPerf = new WindowsPerfCounterReporter(
                logger.Clone("WindowsMetrics"),
                rootDimensions,
                WindowsPerfCounterSet.BasicLocalMachine |
                WindowsPerfCounterSet.BasicCurrentProcess |
                WindowsPerfCounterSet.DotNetClrCurrentProcess);
            metricCollector.AddMetricSource(windowsPerf);

            const int numThreads = 1;
            const int mmioBufferSize = 100 * 1024 * 1024;
            const int messageSize = 1000;
            const int driverFrequencyHz = 200;
            ProducerThread[] producers = new ProducerThread[numThreads];
            ConsumerThread[] consumers = new ConsumerThread[numThreads];
            List<Task> allTasks = new List<Task>();

            //ISocket serverSocket = new MMIOServerSocket(logger.Clone("ServerSocket"), metricCollector, rootDimensions, mmioBufferSize);
            //ISocket clientSocket = new MMIOClientSocket(serverSocket.RemoteEndpointString, logger.Clone("ClientSocket"), metricCollector, rootDimensions);
            ISocket serverSocket = new AnonymousPipeServerSocket(mmioBufferSize);
            ISocket clientSocket = new AnonymousPipeClientSocket(serverSocket.RemoteEndpointString);
            //DirectSocketPair directSocket = DirectSocket.CreateSocketPair();
            //ISocket serverSocket = directSocket.ServerSocket;
            //ISocket clientSocket = directSocket.ClientSocket;
            //ISocket serverSocket = new Win32ServerSocket(logger.Clone("ServerSocket"));
            //ISocket clientSocket = new RawTcpSocket(serverSocket.RemoteEndpointString);
            
            PostOffice serverPostOffice = new PostOffice(serverSocket, logger.Clone("ServerPostOffice"), TimeSpan.FromSeconds(10), true, realTime, new WeakPointer<IMetricCollector>(metricCollector), rootDimensions);
            PostOffice clientPostOffice = new PostOffice(clientSocket, logger.Clone("ClientPostOffice"), TimeSpan.FromSeconds(10), false, realTime, new WeakPointer<IMetricCollector>(metricCollector), rootDimensions);

            for (int thread = 0; thread < numThreads; thread++)
            {
                IMetricCollector threadCollector = (thread == 0) ? (IMetricCollector)metricCollector : NullMetricCollector.Singleton;
                DimensionSet threadDimensions = rootDimensions;
                producers[thread] = new ProducerThread(logger.Clone("Producer" + thread), serverPostOffice, threadCollector, threadDimensions, CancellationToken.None, realTime, messageSize, driverFrequencyHz);
                consumers[thread] = new ConsumerThread(logger.Clone("Consumer" + thread), clientPostOffice, threadCollector, threadDimensions, CancellationToken.None, realTime);

                allTasks.Add(DurandalTaskExtensions.LongRunningTaskFactory.StartNew(producers[thread].Run));
                allTasks.Add(DurandalTaskExtensions.LongRunningTaskFactory.StartNew(consumers[thread].Run));
            }

            logger.Log("All tasks are running");

            FixedCapacityThreadPool backgroundThreadPool = new FixedCapacityThreadPool(
                new TaskThreadPool(),
                NullLogger.Singleton,
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                maxCapacity: 4,
                overschedulingBehavior: ThreadPoolOverschedulingBehavior.ShedExcessWorkItems);

            RateLimiter limiter = new RateLimiter(500, 1000);
            Stopwatch incrementTimer = Stopwatch.StartNew();
            while (true)
            {
                // Pulse the barrier every 100ms to keep all threads exactly in sync
                //startingGate.SignalAndWait();
                //if (incrementTimer.ElapsedMilliseconds > 500)
                //{
                //    Console.ForegroundColor = ConsoleColor.Red;
                //    Console.Write("X");
                //}
                //else if (incrementTimer.ElapsedMilliseconds > 150)
                //{
                //    Console.ForegroundColor = ConsoleColor.Yellow;
                //    Console.Write("O");
                //}
                //else
                //{

                //    Console.Write(".");
                //}
                //Console.ResetColor();
                //limiter.Limit();
                //metricCollector.ReportPercentile("Z-LoopTime", incrementTimer.ElapsedMillisecondsPrecise(), rootDimensions);
                //incrementTimer.Restart();
                // Simulate work being done on background threads
                //backgroundThreadPool.EnqueueUserWorkItem(() =>
                //{
                //    FastRandom rand = new FastRandom();
                //    for (int c = 0; c < 10000; c++)
                //    {
                //        byte[] garbage = new byte[rand.NextInt(1, 5000)];
                //        garbage[0] = 10;
                //    }
                //});
            }
        }

        public class ProducerThread
        {
            private readonly ILogger _logger;
            private readonly PostOffice _postOffice;
            private readonly WeakPointer<IMetricCollector> _metrics;
            private readonly DimensionSet _dimensions;
            private readonly IRealTimeProvider _realTime;
            private readonly CancellationToken _cancelToken;
            private readonly int _messageSize;
            private readonly int _driverFrequency;

            public ProducerThread(
                ILogger logger,
                PostOffice postOffice,
                IMetricCollector metrics,
                DimensionSet dimensions,
                CancellationToken cancelToken,
                IRealTimeProvider realTime,
                int messageSize,
                int driverFrequency)
            {
                _logger = logger;
                _postOffice = postOffice;
                _metrics = new WeakPointer<IMetricCollector>(metrics);
                _dimensions = dimensions;
                _realTime = realTime.Fork("ProducerThread");
                _cancelToken = cancelToken;
                _messageSize = messageSize;
                _driverFrequency = driverFrequency;
            }

            public async Task Run()
            {
                try
                {
                    _logger.Log("Running");
                    FastRandom rand = new FastRandom();
                    Stopwatch timer = new Stopwatch();
                    RateLimiter limiter = new RateLimiter(_driverFrequency, 100);

                    while (!_cancelToken.IsCancellationRequested)
                    {
                        // Create a random message
                        PooledBuffer<byte> buf = BufferPool<byte>.Rent(_messageSize);
                        rand.NextBytes(buf.Buffer, 0, _messageSize);

                        // Wait for start signal
                        //_startingGate.SignalAndWait(_cancelToken);
                        //limiter.Limit();

                        try
                        {
                            timer.Restart();
                            MailboxId box = _postOffice.CreateTransientMailbox(_realTime);
                            timer.Stop();
                            _metrics.Value.ReportPercentile("Producer Create Box", _dimensions, timer.ElapsedMillisecondsPrecise());
                            timer.Restart();

                            // Send it
                            BinaryHelpers.Int64ToByteArrayLittleEndian(HighPrecisionTimer.GetCurrentUTCTime().Ticks, buf.Buffer, 0);
                            MailboxMessage message1 = new MailboxMessage(box, 1, buf, _postOffice.GenerateMessageId());
                            await _postOffice.SendMessage(message1, _cancelToken, _realTime);
                            timer.Stop();
                            _metrics.Value.ReportPercentile("Producer Send", _dimensions, timer.ElapsedMillisecondsPrecise());

                            // And then receive the reply
                            MailboxMessage reply = await _postOffice.ReceiveMessage(box, _cancelToken, _realTime);
                            reply.DisposeOfBuffer();
                        }
                        catch (Exception)
                        {
                            _metrics.Value.ReportInstant("Producer error", _dimensions);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _logger.Log("Producer thread finished", LogLevel.Err);
                    _realTime.Merge();
                }
            }
        }

        public class ConsumerThread
        {
            private readonly ILogger _logger;
            private readonly PostOffice _postOffice;
            private readonly WeakPointer<IMetricCollector> _metrics;
            private readonly DimensionSet _dimensions;
            private readonly IRealTimeProvider _realTime;
            private readonly CancellationToken _cancelToken;

            public ConsumerThread(
                ILogger logger,
                PostOffice postOffice,
                IMetricCollector metrics,
                DimensionSet dimensions,
                CancellationToken cancelToken,
                IRealTimeProvider realTime)
            {
                _logger = logger;
                _postOffice = postOffice;
                _metrics = new WeakPointer<IMetricCollector>(metrics);
                _dimensions = dimensions;
                _realTime = realTime.Fork("ConsumerThread");
                _cancelToken = cancelToken;
            }

            public async Task Run()
            {
                try
                {
                    _logger.Log("Running");
                    FastRandom rand = new FastRandom();
                    Stopwatch timer = new Stopwatch();

                    while (!_cancelToken.IsCancellationRequested)
                    {
                        try
                        {
                            // Wait for producer to send message
                            timer.Restart();
                            MailboxId box = await _postOffice.WaitForMessagesOnNewMailbox(_cancelToken, _realTime);
                            timer.Stop();
                            _metrics.Value.ReportPercentile("Consumer Box Available", _dimensions, timer.ElapsedMillisecondsPrecise());
                            timer.Restart();
                            MailboxMessage incomingMessage = await _postOffice.ReceiveMessage(box, _cancelToken, _realTime);
                            timer.Stop();
                            double messageTransitTime = TimeSpan.FromTicks(HighPrecisionTimer.GetCurrentUTCTime().Ticks - BinaryHelpers.ByteArrayToInt64LittleEndian(incomingMessage.Buffer.Buffer, 0)).TotalMilliseconds;
                            _metrics.Value.ReportPercentile("Consumer Recv", _dimensions, timer.ElapsedMillisecondsPrecise());
                            _metrics.Value.ReportPercentile("Consumer Recv Transit Time", _dimensions, messageTransitTime);
                            incomingMessage.DisposeOfBuffer();

                            // Send a reply
                            PooledBuffer<byte> buf = BufferPool<byte>.Rent(100);
                            rand.NextBytes(buf.Buffer, 0, 100);
                            MailboxMessage reply = new MailboxMessage(box, incomingMessage.ProtocolId, buf, _postOffice.GenerateMessageId(), incomingMessage.MessageId);
                            await _postOffice.SendMessage(reply, _cancelToken, _realTime);
                        }
                        catch (Exception)
                        {
                            _metrics.Value.ReportInstant("Consumer error", _dimensions);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _logger.Log("Consumer thread finished", LogLevel.Err);
                    _realTime.Merge();
                }
            }
        }

        public static async Task IpaConsole()
        {
            Console.OutputEncoding = Encoding.UTF8;
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);

            IFileSystem fileSystem = new RealFileSystem(logger.Clone("FileSystem"), @"D:\Code\Durandal\target");

            IPronouncer pronouncer = await EnglishPronouncer.Create(
                new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\en-US\\cmu-pronounce-ipa.dict"),
                new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\english_pronounce.dat"),
                logger.Clone("Pronouncer"),
                fileSystem);

            IWordBreaker wordBreaker = new EnglishWordBreaker();
            IWordBreaker wholeWordBreaker = new EnglishWholeWordBreaker();
            
            while (true)
            {
                string input = Console.ReadLine();
                Sentence s = wholeWordBreaker.Break(input);
                string ipa = pronouncer.PronouncePhraseAsString(s.Words);
                Console.WriteLine(ipa);
            }
        }

        public static async Task NewSelection()
        {
            Console.OutputEncoding = Encoding.UTF8;
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            
            IFileSystem fileSystem = new RealFileSystem(logger.Clone("FileSystem"), @"C:\Code\Durandal\target");

            IPronouncer pronouncer = await EnglishPronouncer.Create(
                new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\en-US\\cmu-pronounce-ipa.dict"),
                new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\english_pronounce.dat"),
                logger.Clone("Pronouncer"),
                fileSystem);

            IWordBreaker wordBreaker = new EnglishWordBreaker();
            IWordBreaker wholeWordBreaker = new EnglishWholeWordBreaker();
            NLPToolsCollection nlpTools = new NLPToolsCollection();
            EditDistancePronunciation pronouncerEditDistance = new EditDistancePronunciation(pronouncer, wholeWordBreaker, LanguageCode.EN_US);
            ILGFeatureExtractor lgFeaturizer = new EnglishLGFeatureExtractor();
            nlpTools.Add(LanguageCode.EN_US, new NLPTools()
            {
                Pronouncer = pronouncer,
                WordBreaker = wholeWordBreaker,
                FeaturizationWordBreaker = wordBreaker,
                EditDistance = pronouncerEditDistance.Calculate,
                LGFeatureExtractor = lgFeaturizer,
                CultureInfoFactory = new WindowsCultureInfoFactory()
            });
            
            ICache<byte[]> featureIndexCache = new InMemoryCache<byte[]>();
            GenericEntityResolver resolver = new GenericEntityResolver(nlpTools, featureIndexCache);

            IList<string> business_names = File.ReadAllLines(@"C:\Code\Durandal\Data\mytime_businesses.txt").ToList();
            IList<LexicalNamedEntity> entities = new List<LexicalNamedEntity>();
            int ordinal = 0;
            foreach (string name in business_names)
            {
                entities.Add(new LexicalNamedEntity(ordinal++, new List<LexicalString>()
                {
                    new LexicalString(name, pronouncer.PronouncePhraseAsString(wholeWordBreaker.Break(name).Words))
                }));

                if (ordinal > 10)
                {
                    break;
                }
            }

            //for (int warmer = 0; warmer < 10; warmer++)
            //{
            //    IList<Hypothesis<string>> hyps = resolver.ResolveEntity("mr. le lawn care", entities, "en-US").Await();
            //}

            logger.Log("Resolving " + ordinal + " entities");
            
            Stopwatch timer = new Stopwatch();
            MovingAverage avg = new MovingAverage(20, 0);
            foreach (string name in business_names)
            {
                LexicalString input = new LexicalString(name, pronouncer.PronouncePhraseAsString(wholeWordBreaker.Break(name).Words));
                logger.Log("Matching " + input.ToString());
                timer.Restart();
                IList<Hypothesis<int>> hyps = resolver.ResolveEntity(input, entities, LanguageCode.EN_US, logger.Clone("EntityResolver")).Await();
                timer.Stop();
                avg.Add(timer.ElapsedMillisecondsPrecise());
                logger.Log(avg.Average);
                if (hyps.Count == 0)
                {
                    logger.Log("\tInput resolved to no hyps!", LogLevel.Wrn);
                }
                else
                {
                    foreach (Hypothesis<int> hyp in hyps)
                    {
                        logger.Log("\tMatched to " + hyp.Conf.ToString("F3") + "\t" + business_names[hyp.Value]);
                    }
                }
            }
        }

        public static void TestPostOffice()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            int bufSize = 1024 * 1024;
            int writeChunkSize = 40000;
            AnonymousPipeServerSocket server = new AnonymousPipeServerSocket(bufSize);
            AnonymousPipeClientSocket client = new AnonymousPipeClientSocket(server.RemoteEndpointString);
            int bufferSize = 0;
            IRandom rand = new FastRandom();
            SemaphoreSlim mutex = new SemaphoreSlim(1, 1);
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            PostOffice serverPostOffice = new PostOffice(server, logger.Clone("Server"), TimeSpan.FromSeconds(5), true);
            PostOffice clientPostOffice = new PostOffice(client, logger.Clone("Server"), TimeSpan.FromSeconds(5), false);

            Thread writerThread = new Thread(async () =>
            {
                PooledBuffer<byte> data = BufferPool<byte>.Rent(writeChunkSize);
                Stopwatch timer = new Stopwatch();
                while (true)
                {
                    rand.NextBytes(data.Buffer);
                    MailboxId box = serverPostOffice.CreateTransientMailbox(realTime);
                    MailboxMessage message = new MailboxMessage(box, 10, data);
                    timer.Restart();
                    await serverPostOffice.SendMessage(message, CancellationToken.None, realTime);
                    timer.Stop();
                    double time = timer.ElapsedMillisecondsPrecise();
                    await mutex.WaitAsync();
                    bufferSize += data.Length;
                    if (time > 10)
                    {
                        logger.Log("Write " + time.ToString("F2") + "\t" + bufferSize);
                    }
                    mutex.Release();
                }
            });

            Thread readerThread = new Thread(async () =>
            {
                Stopwatch timer = new Stopwatch();
                while (true)
                {
                    MailboxId boxId = await clientPostOffice.WaitForMessagesOnNewMailbox(CancellationToken.None, realTime);
                    timer.Restart();
                    MailboxMessage message = await clientPostOffice.ReceiveMessage(boxId, CancellationToken.None, realTime);
                    timer.Stop();
                    double time = timer.ElapsedMillisecondsPrecise();
                    await mutex.WaitAsync();
                    bufferSize -= message.Buffer.Length;
                    if (time > 10)
                    {
                        logger.Log("Read  " + time.ToString("F2") + "\t" + bufferSize);
                    }
                    mutex.Release();
                    //await Task.Delay(5);
                }
            });

            writerThread.Start();
            readerThread.Start();

            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        private static void TestVadUtteranceRecorder()
        {
            //ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            //IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            //IPocketSphinx sphinx = new PortablePocketSphinx(new WindowsFileSystem(logger, @"C:\Code\Durandal\Data\sphinx"), logger);
            //sphinx.Create("en-US-semi", "cmudict_SPHINX_40.txt", false);
            //sphinx.Reconfigure(new KeywordSpottingConfiguration()
            //{
            //    PrimaryKeyword = "ANTIQUES",
            //    PrimaryKeywordSensitivity = 5
            //});
            //sphinx.Start();
            //IAudioInputDevice mic = new NAudioMicrophone(48000);
            //mic.StartRecording();
            //mic.ClearBuffers();
            //ChunkedAudioStream stream = AudioUtils.RecordUtteranceUsingVad(mic, sphinx, realTime, 16000);
            //while (!stream.EndOfStream)
            //{
            //    stream.Read();
            //}
        }

        private class FakePlugin : DurandalPlugin
        {
            public FakePlugin() : base("MyTestPlugin") { }

            public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
            {
                await DurandalTaskExtensions.NoOpTask;
                return new PluginResult(Result.Success)
                {
                    ResponseText = "Doctor Grant, the phones are working"
                };
            }

            protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
            {
                return new PluginInformation()
                {
                    InternalName = "test plugin for debugging",
                    MajorVersion = 1,
                    MinorVersion = 0
                };
            }
        }

        public static async Task RemoteClientTest()
        {
            ILogger logger = new ConsoleLogger("Main");
            IRandom rand = new FastRandom();
            IThreadPool serverThreadPool = new TaskThreadPool();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            IDialogExecutor executor = new BasicDialogExecutor(true);
            IRemoteDialogProtocol protocol = new BondRemoteDialogProtocol();
            BasicPluginLoader loader = new BasicPluginLoader(executor, NullFileSystem.Singleton);
            IConfiguration baseConfig = new InMemoryConfiguration(logger.Clone("Config"));
            RemotingConfiguration remotingConfig = new RemotingConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            loader.RegisterPluginType(new FakePlugin());
            LocallyRemotedPluginProvider remotePluginProvider = new LocallyRemotedPluginProvider(
                logger,
                loader,
                protocol,
                remotingConfig,
                new WeakPointer<IThreadPool>(serverThreadPool),
                new NullSpeechSynth(),
                NullSpeechRecoFactory.Singleton,
                new Durandal.Common.Security.OAuth.OAuthManager("https://null", new FakeOAuthSecretStore(), NullMetricCollector.WeakSingleton, DimensionSet.Empty),
                new NullHttpClientFactory(),
                NullFileSystem.Singleton,
                null,
                new NLPToolsCollection(),
                null,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                serverSocketFactory: null,
                clientSocketFactory: null,
                realTime: realTime,
                useDebugTimeouts: false);

            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                // Create client and start executing queries
                PluginStrongName pluginId = new PluginStrongName("MyTestPlugin", 1, 0);
                ILogger queryLogger = logger.Clone("Query").CreateTraceLogger(Guid.NewGuid());
                await remotePluginProvider.LoadPlugin(pluginId, queryLogger, realTime);
                
                RateCounter successRate = new RateCounter(TimeSpan.FromSeconds(15));
                RateCounter failureRate = new RateCounter(TimeSpan.FromSeconds(15));
                for (int c = 0; c < 100000; c++)
                {
                    QueryWithContext qc = new QueryWithContext()
                    {
                        ClientContext = DialogTestHelpers.GetTestClientContextTextQuery(),
                        Understanding = DialogTestHelpers.GetSimpleRecoResult("my_domain", "my_intent", 1.0f, "hello"),
                    };

                    DialogProcessingResponse response = await remotePluginProvider.LaunchPlugin(
                        pluginId,
                        null,
                        false,
                        qc,
                        queryLogger,
                        new InMemoryDataStore(),
                        new UserProfileCollection(new InMemoryDataStore(), new InMemoryDataStore(), new InMemoryEntityHistory()),
                        new Durandal.Common.Ontology.KnowledgeContext(),
                        new List<ContextualEntity>(),
                        realTime);

                    if (response.PluginOutput != null &&
                        !string.IsNullOrEmpty(response.PluginOutput.ResponseText))
                    {
                        successRate.Increment();
                    }
                    else
                    {
                        failureRate.Increment();
                    }

                    if (c % 1000 == 0)
                    {
                        logger.Log(string.Format("Success: {0:F2}\t Failure: {1:F2}", successRate.Rate, failureRate.Rate));
                    }
                }
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Err);
            }

            timer.Stop();
            logger.Log("Total time was " + timer.Elapsed);
        }
        
        public static async Task RemoteTest()
        {
            CancellationTokenSource testKiller = new CancellationTokenSource();
            //testKiller.CancelAfter(TimeSpan.FromSeconds(30));
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            IRandom rand = new FastRandom();
            IThreadPool serverThreadPool = new TaskThreadPool();
            uint protocolId = 3223;
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();

            // Start thread to represent the server
            IRealTimeProvider serverTime = realTime.Fork("RemoteServer");
            Task serverTask = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(async () =>
            {
                try
                {
                    using (PostOffice postOffice = new PostOffice(socketPair.ServerSocket, logger.Clone("ServerPostOffice"), TimeSpan.FromSeconds(30), true, serverTime))
                    {
                        for (int c = 0; c < 100; c++)
                        {
                            if (testKiller.IsCancellationRequested)
                            {
                                break;
                            }

                            MailboxId newMailboxId = await postOffice.WaitForMessagesOnNewMailbox(testKiller.Token, serverTime);

                            for (int rep = 0; rep < 3; rep++)
                            {
                                // Receive the message on the box
                                logger.Log("Server waiting for message");
                                RetrieveResult<MailboxMessage> gotMessage = await postOffice.TryReceiveMessage(newMailboxId, testKiller.Token, TimeSpan.FromSeconds(10), serverTime);

                                logger.Log("Server got message, sending junk");
                                // Send a callback message on a new mailbox
                                MailboxId callbackBoxId = postOffice.CreateTransientMailbox(serverTime);
                                PooledBuffer<byte> data = BufferPool<byte>.Rent(10000);
                                rand.NextBytes(data.Buffer, 0, data.Length);
                                MailboxMessage outMessage = new MailboxMessage(callbackBoxId, 123456, data, 0, 0);
                                await postOffice.SendMessage(outMessage, testKiller.Token, serverTime);

                                // And echo back the original message
                                if (gotMessage.Success)
                                {
                                    logger.Log("Server sending response");
                                    await postOffice.SendMessage(gotMessage.Result, testKiller.Token, serverTime);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    await socketPair.ServerSocket.Disconnect(testKiller.Token, serverTime, NetworkDuplex.ReadWrite).ConfigureAwait(false);
                    serverTime.Merge();
                }
            });

            IRealTimeProvider clientTime = realTime.Fork("PostOfficeClient");
            Task clientTask = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(async () =>
            {
                try
                {
                    using (PostOffice clientPostOffice = new PostOffice(socketPair.ClientSocket, logger.Clone("ClientPostOffice"), TimeSpan.FromSeconds(30), false, clientTime))
                    {
                        for (int c = 0; c < 100; c++)
                        {
                            MailboxId boxId = clientPostOffice.CreateTransientMailbox(clientTime);
                            PooledBuffer<byte> data = BufferPool<byte>.Rent(10000);

                            for (int rep = 0; rep < 3; rep++)
                            {
                                rand.NextBytes(data.Buffer, 0, data.Length);
                                MailboxMessage outMessage = new MailboxMessage(boxId, protocolId, data, 0, 0);
                                logger.Log("Client sending message");
                                await clientPostOffice.SendMessage(outMessage, testKiller.Token, clientTime);
                                logger.Log("Client waiting for response");
                                RetrieveResult<MailboxMessage> inMessage = await clientPostOffice.TryReceiveMessage(boxId, testKiller.Token, TimeSpan.FromSeconds(10), clientTime);
                                logger.Log("Client got response");
                                //Assert.IsTrue(inMessage.Success);
                                //Assert.IsTrue(ArrayExtensions.ArrayEquals(outMessage.Payload, inMessage.Result.Payload));
                                //Assert.AreEqual(outMessage.ProtocolId, inMessage.Result.ProtocolId);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    await socketPair.ClientSocket.Disconnect(testKiller.Token, clientTime, NetworkDuplex.ReadWrite).ConfigureAwait(false);
                    clientTime.Merge();
                }
            });


            realTime.Step(TimeSpan.FromMilliseconds(10000), 10);

            testKiller.Cancel();
            await serverTask;
        }

        public static async Task HttpInstrumentation()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            RateLimiter limiter = new RateLimiter(1, 10);
            //ISocketFactory socketFactory = new PooledTcpClientSocketFactory(logger, NullMetricCollector.Singleton, DimensionSet.Empty);
            ISocketFactory socketFactory = new TcpClientSocketFactory(logger);

            //IHttpClient httpClient = new SocketHttpClient(new WeakPointer<ISocketFactory>(socketFactory), new Uri("http://marathon.bungie.org/"), logger, NullMetricCollector.WeakSingleton, DimensionSet.Empty);
            IHttpClient httpClient = new SocketHttpClient(
                new WeakPointer<ISocketFactory>(socketFactory),
                new Uri("http://durandal.dnsalias.net/"),
                logger,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                Http2SessionManager.Default,
                new Http2SessionPreferences());
            //IHttpClient httpClient = new PortableHttpClient(new Uri("http://durandal.dnsalias.net/"), logger);

            StaticAverage timer = new StaticAverage();
            int warmup = 10;
            while (true)
            {
                HttpRequest req = HttpRequest.CreateOutgoing("/buddy.xml");
                using (var instrumentedResponse = await httpClient.SendInstrumentedRequestAsync(req, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger))
                {
                    if (!instrumentedResponse.Success)
                    {
                        logger.Log("Request failed!", LogLevel.Err);
                    }
                    else
                    {
                        if (warmup == 0)
                        {
                            timer.Add(instrumentedResponse.EndToEndLatency);
                        }
                        else
                        {
                            warmup--;
                        }

                        logger.Log(timer.Average);
                    }
                }

                limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
            }
        }

        public static void Timezones()
        {
            ILogger logger = new ConsoleLogger();
            IFileSystem fileSystem = new RealFileSystem(logger.Clone("Filesystem"), @"C:\Code\Durandal\Bundles\dialog\data\IANA");
            TimeZoneResolver resolver = new TimeZoneResolver(logger.Clone("Resolver"));
            resolver.Initialize(fileSystem, VirtualPath.Root).Await();

            DateTimeOffset rangeStart = DateTimeOffset.UtcNow.AddDays(-30);
            DateTimeOffset rangeEnd = DateTimeOffset.UtcNow.AddDays(30);
            TimeZoneQueryResult result = resolver.CalculateLocalTime("America/Los_Angeles", rangeStart, logger);
            List<TimeZoneRuleEffectiveSpan> spans = resolver.CalculateTimeZoneRuleSpans("America/Los_Angeles",
                rangeStart,
                rangeEnd,
                logger);

            foreach (var span in spans)
            {
                Console.WriteLine("{0} -> {1}: {2}", span.RuleBoundaryBegin, span.RuleBoundaryEnd, span.DstOffset);
            }
        }
        
        public static void PageFileStorage()
        { 
            ILogger logger = new ConsoleLogger();
            IFileSystem fileManager = new RealFileSystem(logger);
            //IMemoryPageStorage pageStorage = new FileBackedPageStorage(fileManager, new VirtualPath("Pagefile.idx"), 32768);
            IMemoryPageStorage pageStorage = new BasicPageStorage();
            ICompactIndex<string> index = new BlockTransformCompactIndex<string>(new StringByteConverter(), pageStorage, 32768, 0);
            IRandom random = new FastRandom();
            StringBuilder builder = new StringBuilder();
            List<Compact<string>> compacts = new List<Compact<string>>();
            while (pageStorage.IndexSize < 100 * 1024 * 1024)
            {
                Console.Write("\r" + pageStorage.IndexSize + " bytes");
                for (int z = 0; z < 10; z++)
                {
                    builder.Append(random.NextInt());
                }
                compacts.Add(index.Store(builder.ToString()));
            }

            Console.WriteLine();
            SuperGarbageCollect();
            long bytes = GC.GetTotalMemory(false) / 1024;
            logger.Log("CLI reports that it uses " + bytes + " Kb", LogLevel.Std);

            // Now do a bunch of random retrieval
            Stopwatch timer = Stopwatch.StartNew();
            for (int c = 0; c < 1000; c++)
            {
                Compact<string> toRetrieve = compacts[random.NextInt(0, compacts.Count)];
                index.Retrieve(toRetrieve);
            }
            timer.Stop();
            double msPer = ((double)timer.ElapsedMilliseconds / 10000);
            Console.WriteLine("Average retrieve time was " + msPer);
        }

        public static void PackageUpEntireDirectory()
        {
            ILogger logger = new ConsoleLogger();
            DirectoryInfo rootPath = new DirectoryInfo(@"C:\Users\lostromb\Desktop\yerf");
            FileInfo targetFile = new FileInfo(@"C:\Users\lostromb\Desktop\yerf.dat");
            InMemoryFileSystem memoryFileSystem = new InMemoryFileSystem();
            RealFileSystem fileSystem = new RealFileSystem(logger, rootPath.FullName);
            FileHelpers.CopyAllFiles(fileSystem, VirtualPath.Root, memoryFileSystem, VirtualPath.Root, logger, true);

            // Dump serialized file system to a package
            using (Stream writeStream = new FileStream(targetFile.FullName, FileMode.Create, FileAccess.Write))
            {
                memoryFileSystem.Serialize(writeStream, true, false);
            }
        }

        public static async Task HttpPoolingTest()
        {
            // Create a server
            ILogger logger = new ConsoleLogger("ServerTest", LogLevel.All);
            logger.Log("Building server");
            IThreadPool serverThreadPool = new CustomThreadPool(logger.Clone("ServerThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ThreadPool", 8);
            RawTcpSocketServer socketServer = new RawTcpSocketServer(
                new ServerBindingInfo[] { new ServerBindingInfo(ServerBindingInfo.WILDCARD_HOSTNAME, 33333) },
                logger.Clone("RawTcpSocketServer"),
                DefaultRealTimeProvider.Singleton,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                new WeakPointer<IThreadPool>(serverThreadPool));
            SocketHttpServer httpServer = new SocketHttpServer(socketServer, logger.Clone("SocketHttpServer"), new CryptographicRandom(), NullMetricCollector.WeakSingleton, DimensionSet.Empty);
            httpServer.RegisterSubclass(new FakeHttpServer());
            logger.Log("Starting server");
            await httpServer.StartServer("Test", CancellationToken.None, DefaultRealTimeProvider.Singleton);

            logger.Log("Building client");
            ISocketFactory socketFactory = new PooledTcpClientSocketFactory(logger.Clone("PooledSocketFactory"), NullMetricCollector.Singleton, DimensionSet.Empty, System.Security.Authentication.SslProtocols.Tls12, true);
            SocketHttpClient socketClient = new SocketHttpClient(
                new WeakPointer<ISocketFactory>(socketFactory),
                new TcpConnectionConfiguration("localhost", 33333, false),
                logger.Clone("SocketHttpClient"),
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                Http2SessionManager.Default,
                new Http2SessionPreferences());
            while (true)
            {
                Durandal.Common.Net.Http.HttpRequest request = Durandal.Common.Net.Http.HttpRequest.CreateOutgoing("/");
                request.RequestHeaders.Add("Connection", "keep-alive");
                logger.Log("Making request");
                using (NetworkResponseInstrumented<Durandal.Common.Net.Http.HttpResponse> response = await socketClient.SendInstrumentedRequestAsync(request, CancellationToken.None, queryLogger: logger.Clone("HttpRequest")))
                {
                    logger.Log("Request done");
                    logger.Log(response.EndToEndLatency + " " + response.Success);
                    await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                }

                await Task.Delay(1000);
            }
        }

        private class FakeHttpServer : IHttpServerDelegate
        {
            private int _requestCount = 0;

            public Task HandleConnection(Durandal.Common.Net.Http.IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (++_requestCount % 3 == 0)
                {
                    throw new Exception("Failure!");
                }

                return context.WritePrimaryResponse(Durandal.Common.Net.Http.HttpResponse.OKResponse(), NullLogger.Singleton, cancelToken, realTime);
            }
        }

        public static async Task LGEnginePerf()
        {
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            VirtualPath templateFile = new VirtualPath("Templates/weather.ini");
            fileSystem.AddFile(templateFile, File.ReadAllBytes(@"C:\code\Durandal\Plugins\StandardAnswers\lg\weather.en-US.ini"));
            ILogger logger = new ConsoleLogger("LG", LogLevel.All);
            List<VirtualPath> allResources = new List<VirtualPath>();
            allResources.Add(templateFile);
            NLPToolsCollection nlTools = new NLPToolsCollection();
            NLPTools englishTools = new NLPTools()
                {
                    Pronouncer = null,
                    WordBreaker = new EnglishWholeWordBreaker(),
                    FeaturizationWordBreaker = new EnglishWordBreaker(),
                    EditDistance = StringUtils.NormalizedEditDistance,
                    LGFeatureExtractor = new EnglishLGFeatureExtractor(),
                    CultureInfoFactory = new WindowsCultureInfoFactory()
                };

            nlTools.Add(LanguageCode.EN_US, englishTools);

            ClientContext mockClientContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US,
                Capabilities = ClientCapabilities.DisplayUnlimitedText
            };
            
            StatisticalLGEngine engine = await StatisticalLGEngine.Create(fileSystem, logger, "weather", new CodeDomLGScriptCompiler(), allResources, nlTools);

            MemoryStream buf = new MemoryStream();
            fileSystem.Serialize(buf, true, false);
            byte[] data = buf.ToArray();
            logger.Log(data.Length);

            logger.Log("Warming up");
            
            for (int c = 0; c < 10; c++)
            {
                logger.Log(c);
                using (MemoryStream inFile = new MemoryStream(data, false))
                {
                    fileSystem = InMemoryFileSystem.Deserialize(inFile, false);
                    engine = await StatisticalLGEngine.Create(fileSystem, logger, "weather", new CodeDomLGScriptCompiler(), allResources, nlTools);
                    logger.Log((await (engine.GetPattern("CurrentLocalConditions", mockClientContext, logger).Sub("temp", "51").Sub("unit", "F").Sub("condition", "cloudy").Render())).Text);
                }
            }

            logger.Log("Testing");
            ILogger runtimeLogger = new EventOnlyLogger();

            Stopwatch timer = new Stopwatch();
            timer.Start();
            for (int c = 0; c < 10000; c++)
            {
                using (MemoryStream inFile = new MemoryStream(data, false))
                {
                    fileSystem = InMemoryFileSystem.Deserialize(inFile, false);
                    engine = await StatisticalLGEngine.Create(fileSystem, runtimeLogger, "weather", new CodeDomLGScriptCompiler(), allResources, nlTools);
                    await engine.GetPattern("CurrentLocalConditions", mockClientContext, runtimeLogger).Sub("temp", "51").Sub("unit", "F").Sub("condition", "cloudy").Render();
                }
            }
            timer.Stop();
            logger.Log((double)timer.ElapsedMilliseconds / 10000);
        }

        public static void TestUnitConvert()
        {
            ILogger logger = new ConsoleLogger();

            while (true)
            {
                string amountString = Console.ReadLine();
                string sourceUnit = Console.ReadLine();
                string destUnit = Console.ReadLine();
                decimal sourceAmount;
                if (decimal.TryParse(amountString, out sourceAmount))
                {
                    List<UnitConversionResult> results = UnitConverter.Convert(sourceUnit, destUnit, sourceAmount, logger);
                    foreach (UnitConversionResult result in results)
                    {
                        Console.WriteLine("{0} {1} => {2} {3} ({4})", result.SourceAmountString, result.SourceUnitName, result.TargetAmountString, result.TargetUnitName, result.ConversionType);
                    }
                }
            }
        }

        public static async Task TestPronouncer()
        {
            ILogger logger = new ConsoleLogger();
            //string[] ipaWords = File.ReadAllLines(@"C:\Users\LOSTROMB\Documents\Visual Studio 2015\Projects\Durandal\Data\sphinx\ipa_words.txt");
            //IpaWeights bestWeights = new IpaWeights();// IpaWeights.TrainWeights(logger, ipaWords);

            IWordBreaker wordbreaker = new EnglishWordBreaker();
            IFileSystem fileSystem = new RealFileSystem(logger.Clone("ResourceManager"), @"C:\Users\lostromb\Documents\Visual Studio 2015\Projects\Durandal\Data\sphinx");
            EnglishPronouncer pronouncer = await EnglishPronouncer.Create(new VirtualPath("cmu-pronounce-ipa.dict"), new VirtualPath("ipa-pron.cache"), logger.Clone("Pronouncer"), fileSystem);
            
            SuperGarbageCollect();
            long bytes = GC.GetTotalMemory(false) / 1024;
            logger.Log("CLI reports that it uses " + bytes + " Kb", LogLevel.Std);

            while (true)
            {
                string word1 = Console.ReadLine();
                Sentence sent1 = wordbreaker.Break(word1);
                string ipa1 = pronouncer.PronouncePhraseAsString(sent1.Words.ToArray());
                Console.WriteLine(ipa1);
                string word2 = Console.ReadLine();
                Sentence sent2 = wordbreaker.Break(word2);
                string ipa2 = pronouncer.PronouncePhraseAsString(sent2.Words.ToArray());
                Console.WriteLine(ipa2);
                float distance = InternationalPhoneticAlphabet.EditDistance(ipa1, ipa2, LanguageCode.EN_US);
                Console.WriteLine(ipa1 + " ~ " + ipa2 + "  " + distance);
            }
        }
        
        public static void MapCmuPhonemes()
        {
            Dictionary<string, string> mapping = new Dictionary<string, string>();
            mapping.Add("M", "m");
            mapping.Add("D", "d");
            mapping.Add("ER", "əɹ");
            mapping.Add("IH", "ɪ");
            mapping.Add("S", "s");
            mapping.Add("AH", "ə");
            mapping.Add("V", "v");
            mapping.Add("EH", "ɛ");
            mapping.Add("N", "n");
            mapping.Add("CH", "tʃ");
            mapping.Add("Z", "z");
            mapping.Add("AE", "æ");
            mapping.Add("L", "l");
            mapping.Add("K", "k");
            mapping.Add("EY", "eɪ");
            mapping.Add("T", "t");
            mapping.Add("NG", "ŋ");
            mapping.Add("SH", "ʃ");
            mapping.Add("TH", "θ");
            mapping.Add("R", "ɹ");
            mapping.Add("OW", "oʊ");
            mapping.Add("P", "p");
            mapping.Add("AY", "aɪ");
            mapping.Add("IY", "ɪ");
            mapping.Add("HH", "h");
            mapping.Add("AA", "ɑ");
            mapping.Add("W", "w");
            mapping.Add("B", "b");
            mapping.Add("G", "g");
            mapping.Add("Y", "y");
            mapping.Add("JH", "ʒ");
            mapping.Add("UW", "ʉ");
            mapping.Add("F", "f");
            mapping.Add("AW", "aʊ");
            mapping.Add("AO", "ɔ");
            mapping.Add("ZH", "ʐ");
            mapping.Add("UH", "ʊə");
            mapping.Add("DH", "ð");
            mapping.Add("OY", "ɔɪ");


            string[] allLines = File.ReadAllLines(@"C:\Bin\Data\sphinx\cmu-pronounce.dict");
            List<string> modifiedLines = new List<string>();
            foreach (string line in allLines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                int tab = line.IndexOf("  ");
                if (tab <= 0)
                {
                    Console.WriteLine("Bad line " + line);
                    continue;
                }
                if (line.Length <= tab + 2)
                {
                    Console.WriteLine("Bad line " + line);
                    continue;
                }

                string word = line.Substring(0, tab);
                string phonemes = line.Substring(tab + 2);
                string[] syllables = phonemes.Split(' ');
                for (int idx = 0; idx < syllables.Length; idx++)
                {
                    string syllable = syllables[idx];
                    string stress = string.Empty;
                    if (syllable.EndsWith("0") || syllable.EndsWith("1") || syllable.EndsWith("2"))
                    {
                        stress = syllable.Substring(syllable.Length - 1);
                        syllable = syllable.Substring(0, syllable.Length - 1);
                    }
                    if (mapping.ContainsKey(syllable))
                    {
                        syllables[idx] = mapping[syllable] + stress;
                    }
                    else
                    {
                        Console.WriteLine(syllable + " : found in " + line);
                    }
                }

                modifiedLines.Add(string.Format("{0}  {1}", word, string.Join(" ", syllables)));
            }

            File.WriteAllLines(@"C:\Bin\Data\sphinx\cmu-pronounce-ipa.dict", modifiedLines);
        }

        public static void IndexPerf()
        {
            FileInfo databaseFile = new FileInfo(@"S:\Documents\Various word documents\Austraeoh\txt\All.txt");
            long startMemory = GC.GetTotalMemory(true);
            long fileSize = databaseFile.Length;

            ICompactIndex<string> index;
            index = new BlockTransformCompactIndex<string>(new StringByteConverter(), new LZ4CompressedMemoryPageStorage(false, 100), 5000, 0);
            ///index = BasicCompactIndex<string>.BuildStringIndex();
            IWordBreaker wordBreaker = new EnglishWordBreaker();
            IList<Compact<string>> data = new List<Compact<string>>();

            Console.WriteLine("Loading data...");
            using (StreamReader input = new StreamReader(databaseFile.FullName))
            {
                while (!input.EndOfStream)
                {
                    string line = input.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        Sentence e = wordBreaker.Break(line);
                        foreach (string word in e.Words)
                        {
                            data.Add(index.Store(word));
                        }
                    }
                }
            }

            Console.WriteLine("Loaded " + data.Count + " words");

            Console.WriteLine("Running perf tests");
            
            Stopwatch timer = Stopwatch.StartNew();
            foreach (Compact<string> word in data)
            {
                index.Retrieve(word);
            }
            timer.Stop();
            Console.WriteLine("Average time was " + ((double)timer.ElapsedMilliseconds / (double)data.Count) + " ms");

            SuperGarbageCollect();

            double cliKb = (double)GC.GetTotalMemory(false) / 1024;
            double indexKb = (double)index.MemoryUse / 1024;

            long midMemory = GC.GetTotalMemory(true);
            long indexSize = midMemory - startMemory;
            Console.WriteLine("GC reports that the index uses " + indexSize + " bytes to store " + fileSize + " bytes (" + ((double)indexSize / (double)fileSize) + ")");

            index.Retrieve(index.GetNullIndex());
        }

        private static void SuperGarbageCollect()
        {
            for (int d = 0; d < 3; d++)
            {
                for (int c = 0; c < GC.MaxGeneration; c++)
                {
                    GC.Collect(c, GCCollectionMode.Forced, true);
                }
            }

            GC.WaitForFullGCComplete(-1);
        }

        public static void TestBingAnswers()
        { 
            //ILogger logger = new ConsoleLogger("Bing", LogLevel.All);
            //while (true)
            //{
            //    string q = Console.ReadLine();
            //    BingResponse factResponse = BingAPI.QueryBing(q, logger, "en-US").Await();
            //    if (factResponse.Facts != null)
            //    {
            //        foreach (var fact in factResponse.Facts)
            //        {
            //            Console.WriteLine("FACT");
            //            Console.WriteLine("Txt: " + fact.Text);
            //            Console.WriteLine("Sub: " + fact.Subtitle);
            //        }
            //    }
            //    if (factResponse.EntityReferences != null)
            //    {
            //        foreach (var entityId in factResponse.EntityReferences)
            //        {
            //            Console.WriteLine("Entity Reference " + entityId);
            //        }
            //    }
            //}
        }

        public static void SimpleIndex()
        {
            // Load some data
            ConsoleLogger logger = new ConsoleLogger();
            IDictionary<string, string> documents = new Dictionary<string, string>();
            using (StreamReader reader = new StreamReader(new FileStream(@"C:\Users\lostromb\Desktop\mytime.tsv", FileMode.Open, FileAccess.Read)))
            {
                while (!reader.EndOfStream)
                {
                    string nextLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(nextLine))
                    {
                        continue;
                    }

                    string[] parts = nextLine.Split('\t');
                    documents.Add(parts[0], parts[1]);
                }

                reader.Close();
            }

            logger.Log("Indexing " + documents.Count + " items");
            StringFeatureSearchIndex<string> index = new StringFeatureSearchIndex<string>(new EnglishNgramApproxStringFeatureExtractor(), logger);
            foreach (var kvp in documents)
            {
                index.Index(kvp.Value, kvp.Key + ":" + kvp.Value);
            }

            logger.Log("Benchmarking");
            Stopwatch timer = Stopwatch.StartNew();
            float correct = 0;
            float possible = 0;
            foreach (var document in documents)
            {
                possible++;
                IList<Hypothesis<string>> docs = index.Search(document.Value);
                if (docs.Count > 0 && docs[0].Value == document.Key)
                {
                    correct++;
                }
            }
            timer.Stop();
            float time = (float)timer.ElapsedMilliseconds / (float)documents.Count;
            logger.Log("Search time was " + time + "ms");
            logger.Log("Correctness was " + (correct / possible));

            logger.Log("Ready to search the database");

            while (true)
            {
                string input = Console.ReadLine();
                IList<Hypothesis<string>> docs = index.Search(input);
                if (docs.Count == 0)
                {
                    logger.Log("No results!");
                }
                else
                {
                    foreach (var doc in docs)
                    {
                        logger.Log(doc);
                    }
                }
            }
        }

        public static int CountCodeLinesRecursive(DirectoryInfo directory)
        {
            // Console.WriteLine("Durandal codebase is now " + CountCodeLinesRecursive(new DirectoryInfo(@"D:\Code\Durandal")) + " lines of code");
            int returnVal = 0;
            
            // Enumerate files
            foreach (FileInfo file in directory.EnumerateFiles())
            {
                if (!file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) &&
                    !file.Extension.Equals(".java", StringComparison.OrdinalIgnoreCase) &&
                    !file.Extension.Equals(".cpp", StringComparison.OrdinalIgnoreCase) &&
                    !file.Extension.Equals(".h", StringComparison.OrdinalIgnoreCase) &&
                    !file.Extension.Equals(".c", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (file.Name.Contains("TemporaryGeneratedFile"))
                    continue;
                if (file.Name.Contains("_types") || file.Name.Contains("_services") || file.Name.Contains("_proxies"))
                    continue;

                string[] lines = File.ReadAllLines(file.FullName);
                returnVal += lines.Length;
                Console.WriteLine(lines.Length + "\t" + file.FullName);
            }

            // Enumerate directories
            foreach (DirectoryInfo dir in directory.EnumerateDirectories())
            {
                if (dir.Name.Equals("obj") || dir.Name.Equals("objd") || dir.Name.Equals("bin") || dir.Name.Equals(".svn") || dir.FullName.Equals("C:\\Code\\Durandal\\packages", StringComparison.OrdinalIgnoreCase))
                    continue;

                returnVal += CountCodeLinesRecursive(dir);
            }

            return returnVal;
        }

        public static void TestSearchIndex()
        {
            // Load some data
            ConsoleLogger logger = new ConsoleLogger();
            IDictionary<int, string> documents = new Dictionary<int, string>();
            using (StreamReader reader = new StreamReader(new FileStream(@"C:\Users\lostromb\Desktop\mytime.tsv", FileMode.Open, FileAccess.Read)))
            {
                while (!reader.EndOfStream)
                {
                    string nextLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(nextLine))
                    {
                        continue;
                    }

                    string[] parts = nextLine.Split('\t');
                    documents.Add(int.Parse(parts[0]), parts[1]);
                }

                reader.Close();
            }

            logger.Log("Indexing " + documents.Count + " items");
            StringFeatureSearchIndex<int> searchIndex = new StringFeatureSearchIndex<int>(new EnglishNgramApproxStringFeatureExtractor(), logger);
            foreach (var kvp in documents)
            {
                searchIndex.Index(kvp.Value, kvp.Key);
            }
            

            logger.Log("Serializing");
            InMemoryFileSystem fakeResources = new InMemoryFileSystem();
            VirtualPath fakeCacheFile = new VirtualPath("cache.dat");
            searchIndex.Serialize(fakeResources, fakeCacheFile);

            searchIndex = StringFeatureSearchIndex<int>.Deserialize(fakeResources, fakeCacheFile, new EnglishNgramApproxStringFeatureExtractor(), logger);

            logger.Log("Benchmarking");
            Stopwatch timer = Stopwatch.StartNew();
            float correct = 0;
            float possible = 0;
            foreach (var document in documents)
            {
                possible++;
                IList<Hypothesis<int>> hyps = searchIndex.Search(document.Value);
                if (hyps.Count > 0 && hyps[0].Value == document.Key)
                {
                    correct++;
                }
            }
            timer.Stop();
            float time = (float)timer.ElapsedMilliseconds / (float)documents.Count;
            logger.Log("Search time was " + time + "ms");
            logger.Log("Correctness was " + (correct / possible));

            logger.Log("Ready to search the database");

           
            while (true)
            {
                string input = Console.ReadLine();
                IList<Hypothesis<int>> hyps = searchIndex.Search(input, 5);
                if (hyps.Count == 0)
                {
                    logger.Log("No results!");
                }
                else
                {
                    foreach (var hyp in hyps)
                    {
                        logger.Log(hyp.Conf + " " + documents[hyp.Value]);
                    }
                }
            }
        }

        public static void LgPerf()
        {
            string[] TestPattern = new string[]
            {
                "On the [day]1[/day]st, it will be [condition]clear[/condition], and [temp]15[/temp] degrees.",
                "On the [day]2[/day]nd, it will be [condition]cloudy[/condition], and [temp]22[/temp] degrees.",
                "On the [day]3[/day]rd, it will be [condition]sunny[/condition], and [temp]34[/temp] degrees.",
                "On the [day]4[/day]th, it will be [condition]overcast[/condition], and [temp]16[/temp] degrees.",
                "On the [day]5[/day]th, it will be [condition]partly cloudy[/condition], and [temp]49[/temp] degrees.",
                "On the [day]6[/day]th, it will be [condition]rainy[/condition], and [temp]35[/temp] degrees.",
                "On the [day]7[/day]th, it will be [condition]snowy[/condition], and [temp]1[/temp] degree.",
                "On the [day]8[/day]th, it will be [condition]sunny[/condition], and [temp]63[/temp] degrees.",
                "On the [day]9[/day]th, it will be [condition]mostly cloudy[/condition], and [temp]12[/temp] degrees.",
                "On the [day]23[/day]rd, it will be [condition]snowy[/condition], and [temp]1[/temp] degree.",
                "On the [day]22[/day]nd, it will be [condition]sunny[/condition], and [temp]1[/temp] degree.",
                "On the [day]31[/day]st, it will be [condition]cloudy[/condition], and [temp]10[/temp] degrees.",
                "On the [day]29[/day]th, it will be [condition]windy[/condition], and [temp]98[/temp] degrees.",
                "On the [day]15[/day]th, it will be [condition]cloudy[/condition], and [temp]77[/temp] degrees.",
                "On the [day]16[/day]th, it will be [condition]overcast[/condition], and [temp]52[/temp] degrees.",
                "On the [day]23[/day]rd, it will be [condition]clear[/condition], and [temp]44[/temp] degrees.",
                "On the [day]17[/day]th, it will be [condition]rainy[/condition], and [temp]101[/temp] degrees.",
                //"On the [day]10[/day]th, it will be [condition]partly sunny[/condition], and [temp]1[/temp] degree.",
                //"On the [day]26[/day]th, it will be [condition]foggy[/condition], and [temp]33[/temp] degrees.",
                //"On the [day]31[/day]st, it will be [condition]cloudy[/condition], and [temp]49[/temp] degrees.",
                //"On the [day]19[/day]th, it will be [condition]overcast[/condition], and [temp]93[/temp] degrees.",
                //"On the [day]25[/day]th, it will be [condition]sunny[/condition], and [temp]71[/temp] degrees.",
                //"On the [day]15[/day]th, it will be [condition]snowy[/condition], and [temp]54[/temp] degrees.",
                //"On the [day]33[/day]rd, it will be [condition]clear[/condition], and [temp]44[/temp] degrees.",
                "On the [day]32[/day]nd, it will be [condition]overcast[/condition], and [temp]24[/temp] degrees."
            };

            //string[] TestPattern = new string[]
            //{
            //    "Would you like an [item]egg roll[/item]?",
            //    "Would you like a [item]spring roll[/item]?",
            //    "Would you like some [item]peas[/item]?",
            //    "Would you like an [item]omelette[/item]?",
            //    "Would you like a [item]cheese[/item]?",
            //    "Would you like a [item]pepsi[/item]?",
            //    "Would you like an [item]orange[/item]?",
            //    "Would you like some [item]pants[/item]?",
            //    "Would you like a [item]bread slice[/item]?",
            //    "Would you like a [item]tuba[/item]?",
            //    "Would you like a [item]baby[/item]?",
            //    "Would you like a [item]snowman[/item]?",
            //    "Would you like some [item]frosties[/item]?"
            //};

            ILogger logger = new ConsoleLogger();
            IWordBreaker wordbreaker = new EnglishWordBreaker();
            StatisticalLGPhrase pattern = new StatisticalLGPhrase("TestPhrase", LanguageCode.EN_US, logger, wordbreaker, new EnglishLGFeatureExtractor());
            pattern.Initialize(TestPattern);
            IDictionary<string, string> subs = new Dictionary<string, string>();

            //while (true)
            //{
            //    string x = Console.ReadLine();
            //    subs.Clear();
            //    subs.Add("item", x);
            //    Console.WriteLine(pattern.Render(subs, false));
            //}

            string[] conditions = new string[4] { "partly cloudy", "rainy", "snowy", "clear" };

            int correct = 0;
            int tried = 0;
            for (int day = 1; day <= 100; day++)
            {
                for (int condition = 0; condition < conditions.Length; condition++)
                {
                    for (int temp = 0; temp <= 100; temp++)
                    {
                        StringBuilder expected = new StringBuilder();
                        expected.Append("On the ");
                        expected.Append(day);
                        subs.Add("day", day.ToString());
                        subs.Add("condition", conditions[condition]);
                        subs.Add("temp", temp.ToString());

                        string rendered = pattern.Render(subs, false, logger);

                        // Evaluate result
                        int lastDigit = day % 10;
                        int secondToLastDigit = (day % 100) / 10;
                        if (lastDigit > 0 && lastDigit < 4 && secondToLastDigit != 1)
                        {
                            if (lastDigit == 1)
                                expected.Append("st");
                            if (lastDigit == 2)
                                expected.Append("nd");
                            if (lastDigit == 3)
                                expected.Append("rd");
                        }
                        else
                        {
                            expected.Append("th");
                        }

                        expected.Append(", it will be ");
                        expected.Append(conditions[condition]);
                        expected.Append(", and ");
                        expected.Append(temp);
                        if (Math.Abs(temp) == 1)
                        {
                            expected.Append(" degree.");
                        }
                        else
                        {
                            expected.Append(" degrees.");
                        }

                        tried++;
                        if (string.Equals(rendered, expected.ToString()))
                        {
                            correct++;
                        }
                        else
                        {
                            //Console.WriteLine("E:" + expected.ToString());
                            //Console.WriteLine("R:" + rendered);
                        }

                        subs.Clear();
                    }
                }
            }

            Console.WriteLine("Accuracy: " + ((float)correct * 100f / tried));
        }

        private static async Task LGTestConsole()
        {
            ILogger logger = new ConsoleLogger("LG", LogLevel.All);
            byte[] file = File.ReadAllBytes(@"C:\Code\Durandal\Data\lgtest.ini");
            VirtualPath mockFileName = new VirtualPath("lg.en-US.ini");
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(mockFileName, file);
            List<VirtualPath> allFiles = new List<VirtualPath>();
            allFiles.Add(mockFileName);

            NLPToolsCollection nlTools = new NLPToolsCollection();
            nlTools.Add(
                LanguageCode.EN_US,
                new NLPTools()
                {
                    Pronouncer = null,
                    WordBreaker = new EnglishWholeWordBreaker(),
                    FeaturizationWordBreaker = new EnglishWordBreaker(),
                    EditDistance = StringUtils.NormalizedEditDistance,
                    LGFeatureExtractor = new EnglishLGFeatureExtractor(),
                    CultureInfoFactory = new WindowsCultureInfoFactory()
                });

            ClientContext mockContext = new ClientContext()
            {
                Locale = LanguageCode.EN_US
            };

            mockContext.AddCapabilities(ClientCapabilities.DisplayUnlimitedText);
            mockContext.AddCapabilities(ClientCapabilities.CanSynthesizeSpeech);

            StatisticalLGEngine engine = await StatisticalLGEngine.Create(fileSystem, logger, "testdomain", new CodeDomLGScriptCompiler(), allFiles, nlTools);
            VariantConfig[] patternNames = engine.GetAllPatternNames().ToArray();
            while (true)
            {
                Console.WriteLine("Select a pattern to render:");
                for (int c = 0; c < patternNames.Length; c++)
                {
                    StringBuilder variantName = new StringBuilder();
                    foreach (var variant in patternNames[c].Variants)
                    {
                        if (variantName.Length == 0)
                        {
                            variantName.Append("?");
                        }
                        else
                        {
                            variantName.Append("&");
                        }
                        variantName.Append(variant.Key);
                        variantName.Append("=");
                        variantName.Append(variant.Value);
                    }
                    Console.WriteLine("{0}) {1}", c, patternNames[c].Name + variantName.ToString());
                }

                string sel = Console.ReadLine();
                int selInt;
                if (!int.TryParse(sel, out selInt))
                {
                    break;
                }
                
                StatisticalLGPattern pattern = engine.GetPattern(patternNames[selInt].Name, mockContext, logger) as StatisticalLGPattern;
                string[] fieldNamesNeeded = pattern.SubstitutionFieldNames.ToArray();
                for (int c = 0; c < fieldNamesNeeded.Length; c++)
                {
                    Console.WriteLine("{0} = ?", fieldNamesNeeded[c]);
                    string val = Console.ReadLine();
                    pattern.Sub(fieldNamesNeeded[c], val);
                }
                
                Console.WriteLine((await pattern.Render()).Text);
            }
            
        }

        private static void TestLogger()
        {
            ISocketFactory fac = new TcpClientSocketFactory(new ConsoleLogger("SocketFactory"));
            ILogger remoteLogger = new RemoteInstrumentationLogger(
                new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(fac),
                    new TcpConnectionConfiguration("durandal.dnsalias.net", 62295, false),
                    NullLogger.Singleton,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    Http2SessionManager.Default,
                    new Http2SessionPreferences()),
                new InstrumentationBlobSerializer(),
                DefaultRealTimeProvider.Singleton,
                "Test",
                bootstrapLogger: new ConsoleLogger());
            int numTasks = 4;
            Stopwatch timer = Stopwatch.StartNew();
            Task[] tasks = new Task[numTasks];
            for (int t = 0; t < numTasks; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int c = 0; c < 1000; c++)
                    {
                        remoteLogger.Log("This is a test");
                    }
                });
            }
            Task.WaitAll(tasks);
            timer.Stop();
            Console.WriteLine(timer.ElapsedMilliseconds);
            timer.Restart();
            remoteLogger.Flush(CancellationToken.None, DefaultRealTimeProvider.Singleton, true);
            timer.Stop();
            Console.WriteLine(timer.ElapsedMilliseconds);
            Thread.Sleep(300000);
            Console.WriteLine("Done");
        }

        public static void DoWork(object a)
        {
            while (true)
            {
                Console.WriteLine("Work work");
                Thread.Sleep(10);
            }
        }

        //public static void CompactIndexTest()
        //{
        //    long startMemory = GC.GetTotalMemory(true);
        //    FileInfo databaseFile = new FileInfo(@"E:\TRAINING DATA\raw cortana query strings.txt");
        //    long fileSize = databaseFile.Length;

        //    ICompactIndex<string> index = new BlockTransformCompactIndex<string>(new StringByteConverter(), new LZ4CompressedMemoryPageStorage(false), 5000);
        //    ApproxStringMatchingIndexCompact matcher = new ApproxStringMatchingIndexCompact(new EnglishWholeWordApproxStringFeatureExtractor(), index, new ConsoleLogger());

        //    Console.WriteLine("Loading index...");
        //    using (StreamReader input = new StreamReader(databaseFile.FullName))
        //    {
        //        while (!input.EndOfStream)
        //        {
        //            string line = input.ReadLine();
        //            if (!string.IsNullOrEmpty(line))
        //            {
        //                matcher.Index(line);
        //            }
        //        }
        //    }

        //    SuperGarbageCollect();

        //    double cliKb = (double)GC.GetTotalMemory(false) / 1024;
        //    double indexKb = (double)index.MemoryUse / 1024;

        //    long midMemory = GC.GetTotalMemory(true);
        //    long indexSize = midMemory - startMemory;
        //    Console.WriteLine("GC reports that the index uses " + indexSize + " bytes to store " + fileSize + " bytes (" + ((double)indexSize / (double)fileSize) + ")");
        //    Console.WriteLine("Running perf tests");

        //    string[] lines = File.ReadAllLines(@"E:\TRAINING DATA\temp_validation.txt");

        //    Stopwatch timer = Stopwatch.StartNew();
        //    foreach (string line in lines)
        //    {
        //        matcher.Match(line, 1);
        //    }
        //    timer.Stop();
        //    Console.WriteLine("Average time was " + ((double)timer.ElapsedMilliseconds / (double)lines.Length) + " ms");

        //    SuperGarbageCollect();

        //    long finalMemory = GC.GetTotalMemory(true);
        //    indexSize = midMemory - startMemory;
        //    Console.WriteLine("GC reports that the index uses " + indexSize + " bytes to store " + fileSize + " bytes (" + ((double)indexSize / (double)fileSize) + ")");
        //}

        public static string GenerateRandString(Random r, int minLength = 120, int maxLength = 360)
        {
            int length = r.Next(120, 360);
            StringBuilder returnVal = new StringBuilder();
            for (int c = 0; c < length; c++)
            {
                returnVal.Append(r.Next(0, 9).ToString());
            }
            return returnVal.ToString();
        }

        //public static async Task SphinxDriver()
        //{
        //    ILogger logger = new ConsoleLogger();
        //    IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;

        //    KeywordSpottingConfiguration config = new KeywordSpottingConfiguration();
        //    config.PrimaryKeyword = "durandal";
        //    config.PrimaryKeywordSensitivity = 5;
        //    config.SecondaryKeywords = new List<string>();
        //    config.SecondaryKeywordSensitivity = 5;

        //    IFileSystem fileSystem = new WindowsFileSystem(logger, @"C:\Users\lostromb\Documents\Visual Studio 2015\Projects\Durandal\Data\sphinx");

        //    SphinxAudioTrigger sphinxTrigger = new SphinxAudioTrigger(
        //        new PortablePocketSphinx(fileSystem, logger.Clone("PocketSphinx")),
        //        new ConsoleLogger(),
        //        @"en-US-semi",
        //        @"cmudict_SPHINX_40.txt",
        //        config,
        //        true);

        //    //EvaluateTrigger(sphinxTrigger);
        //    //EvaluateTriggerOnChatter(sphinxTrigger);

        //    int inputSampleRate = 16000;
        //    sphinxTrigger.Triggered += (src, args) =>
        //        {
        //            Console.WriteLine("Got a trigger");
        //        };

        //    sphinxTrigger.Initialize();
        //    sphinxTrigger.Configure(config);

        //    IAudioInputDevice mic = new NAudioMicrophone(inputSampleRate, 1.0f);
        //    await mic.StartRecording();
        //    Console.WriteLine("Listening for triggers...");
        //    Stopwatch timer = Stopwatch.StartNew();
        //    for (int c = 0; c < 3000; c++)
        //    {
        //        AudioChunk next = await mic.ReadMicrophone(TimeSpan.FromMilliseconds(100), realTime);
        //        sphinxTrigger.SendAudio(next, realTime);
        //    }
        //    timer.Stop();
        //    Console.WriteLine(timer.ElapsedMilliseconds);

        //    sphinxTrigger.Dispose();
        //}

        public static void ApproxStringTest()
        {
            string[] inputs = File.ReadAllLines(@"C:\Users\lostromb\Documents\Visual Studio 2015\Projects\Durandal\Prototype\bin\TestStringIndex.txt");
            ApproxStringMatchingIndex index = new ApproxStringMatchingIndex(new EnglishWholeWordApproxStringFeatureExtractor(), LanguageCode.EN_US, new ConsoleLogger());
            foreach (string input in inputs)
            {
                index.Index(new LexicalString(input));
            }

            while (true)
            {
                string i = Console.ReadLine();
                LexicalString lex = new LexicalString(i);
                Stopwatch m = Stopwatch.StartNew();
                IList<Hypothesis<LexicalString>> results = null;
                for (int c = 0; c < 100; c++)
                {
                    results = index.Match(lex, 5);
                }
                m.Stop();
                Console.WriteLine(m.ElapsedMilliseconds);

                for (int c = 0; c < Math.Min(5, results.Count); c++)
                {
                    Console.WriteLine(results[c]);
                }
            }
        }

        public static void BondPerfTest()
        {
            Dictionary<string, string> sampleValues = new Dictionary<string, string>();
            sampleValues.Add("one", "one");
            sampleValues.Add("two", "two");
            sampleValues.Add("three", "three");
            sampleValues.Add("four", "four");
            
            DialogResponse testObject = new DialogResponse()
            {
                ResponseAudio = new AudioData()
                {
                    Codec = "pcm",
                    CodecParams = "samplerate=16000",
                    Data = new ArraySegment<byte>(new byte[1000])
                },
                AugmentedFinalQuery = "test query",
                ContinueImmediately = false,
                CustomAudioOrdering = AudioOrdering.Unspecified,
                ExecutionResult = Result.Success,
                ResponseHtml = "here is some sample HTML and it looks really good and stuff",
                IsRetrying = false,
                ProtocolVersion = 8,
                ResponseAction = "{ action stuff goes here }",
                ResponseData = sampleValues,
                StreamingAudioUrl = "http://sample.com",
                SuggestedRetryDelay = 1000,
                ResponseText = "yes",
                UrlScope = UrlScope.Local,
                ResponseUrl = "http://www.bing.com"
            };
            
            string jsonString = JsonConvert.SerializeObject(testObject);
            Console.WriteLine(jsonString);

            byte[] blob = BinaryHelpers.EMPTY_BYTE_ARRAY;

            // Warmup
            Console.WriteLine("Warming up...");
            for (int c = 0; c < 1000; c++)
            {
                blob = BondConverter.SerializeBond(testObject);
                BondConverter.DeserializeBond<DialogResponse>(blob, 0, blob.Length, out testObject);
            }

            const int numRuns = 1000000;

            // Run
            Console.WriteLine("Serialize");
            Stopwatch timer = new Stopwatch();
            timer.Start();
            for (int c = 0; c < numRuns; c++)
            {
                blob = BondConverter.SerializeBond(testObject);
            }
            timer.Stop();

            Console.WriteLine("Serialize = " + timer.ElapsedMilliseconds + "ms");
            Console.WriteLine(((double)timer.ElapsedMilliseconds / (double)numRuns) + "ms each");

            Console.WriteLine("Deserialize");
            timer.Restart();
            for (int c = 0; c < numRuns; c++)
            {
                BondConverter.DeserializeBond<DialogResponse>(blob, 0, blob.Length, out testObject);
            }
            timer.Stop();

            Console.WriteLine("Deserialize = " + timer.ElapsedMilliseconds + "ms");
            Console.WriteLine(((double)timer.ElapsedMilliseconds / (double)numRuns) + "ms each");
        }

        //public static void CurveTest()
        //{
        //    for (double x = 0; x < 1; x += (1d / 40d))
        //    {
        //        double input = x;
        //        double val = Durandal.Common.Audio.AudioMath.SmoothStep(input) * 119;
        //        for (int c = 0; c < (int)val; c++)
        //        {
        //            Console.Write("█");
        //        }
        //        Console.WriteLine();
        //    }
        //}

        public static void TranslatorTest()
        {
            ILogger logger = new ConsoleLogger();
            BingTranslator translator = new BingTranslator("", logger, new PortableHttpClientFactory(), DefaultRealTimeProvider.Singleton);
            while (true)
            {
                string input = Console.ReadLine();
                Stopwatch timer = new Stopwatch();
                timer.Start();
                Console.WriteLine(translator.TranslateText(input, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton, LanguageCode.Parse("es"), LanguageCode.Parse("en")).Await());
                timer.Stop();
                Console.WriteLine("Latency: " + timer.ElapsedMilliseconds);
            }
        }

        public static void FastMathTest()
        {
            double[] inputSetDouble = new double[100];
            float[] inputSetFloat = new float[100];
            Random rand = new Random();
            for (int c = 0; c < inputSetDouble.Length; c++)
            {
                inputSetDouble[c] = rand.NextDouble();
                inputSetFloat[c] = (float)inputSetDouble[c];
            }
            int numIterations = 1000000;

            Console.WriteLine("Testing Math.Log");
            Stopwatch timer = new Stopwatch();
            timer.Start();
            for (int iter = 0; iter < numIterations; iter++)
            {
                for (int c = 0; c < inputSetDouble.Length; c++)
                {
                    Math.Log(inputSetDouble[c]);
                }
            }
            timer.Stop();
            Console.WriteLine("Timer was " + timer.ElapsedMilliseconds);

            Console.WriteLine("Testing FastMath.Log");
            timer.Reset();
            timer.Start();
            for (int iter = 0; iter < numIterations; iter++)
            {
                for (int c = 0; c < inputSetFloat.Length; c++)
                {
                    FastMath.Log(inputSetFloat[c]);
                }
            }
            timer.Stop();
            Console.WriteLine("Timer was " + timer.ElapsedMilliseconds);

            Console.WriteLine("Testing Math.Exp");
            timer.Reset();
            timer.Start();
            for (int iter = 0; iter < numIterations; iter++)
            {
                for (int c = 0; c < inputSetDouble.Length; c++)
                {
                    Math.Exp(inputSetDouble[c]);
                }
            }
            timer.Stop();
            Console.WriteLine("Timer was " + timer.ElapsedMilliseconds);

            Console.WriteLine("Testing FastMath.Exp");
            timer.Reset();
            timer.Start();
            for (int iter = 0; iter < numIterations; iter++)
            {
                for (int c = 0; c < inputSetFloat.Length; c++)
                {
                    FastMath.Exp(inputSetFloat[c]);
                }
            }
            timer.Stop();
            Console.WriteLine("Timer was " + timer.ElapsedMilliseconds);
        }

        public static async Task TTSTest()
        {
            ILogger logger = new ConsoleLogger();
            NLPToolsCollection nlTools = new NLPToolsCollection();
            nlTools.Add(LanguageCode.EN_US,
                new NLPTools()
                {
                    WordBreaker = new EnglishWholeWordBreaker(),
                    SpeechTimingEstimator = new EnglishSpeechTimingEstimator()
                });

            ISocketFactory socketFactory = new PooledTcpClientSocketFactory(logger.Clone("SocketFactory"), NullMetricCollector.Singleton, DimensionSet.Empty);
            IHttpClientFactory httpClientFactory = new SocketHttpClientFactory(
                new WeakPointer<ISocketFactory>(socketFactory),
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                Http2SessionManager.Default);
            //IHttpClientFactory httpClientFactory = new PortableHttpClientFactory();

            ISpeechSynth synth = new BingSpeechSynth(
                logger,
                "region=westus2;key=null",
                httpClientFactory,
                DefaultRealTimeProvider.Singleton,
                nlTools);
            //ISpeechSynth synth = new SapiSpeechSynth(logger, new TaskThreadPool(), AudioSampleFormat.Mono(16000), NullMetricCollector.Singleton, DimensionSet.Empty, 1);
            //ISpeechSynth synth = new FixedSpeechSynth("en-US", TimeSpan.FromSeconds(3));
            bool useStreaming = true;

            IAudioDriver audioDeviceDriver = new WasapiDeviceDriver(logger.Clone("WasapiDriver"));
            AudioSampleFormat outputFormat = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph outputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioRenderDevice speaker = audioDeviceDriver.OpenRenderDevice(null, new WeakPointer<IAudioGraph>(outputGraph), outputFormat, "Speakers"))
            using (LinearMixerAutoConforming mixer = new LinearMixerAutoConforming(new WeakPointer<IAudioGraph>(outputGraph), outputFormat, "Mixer", true))
            {
                mixer.ConnectOutput(speaker);
                await speaker.StartPlayback(DefaultRealTimeProvider.Singleton);

                string input = string.Empty;
                while (!input.Equals("q"))
                {
                    input = Console.ReadLine();

                    try
                    {
                        SpeechSynthesisRequest synthRequest = new SpeechSynthesisRequest()
                        {
                            Plaintext = input,
                            Locale = LanguageCode.EN_US,
                            VoiceGender = VoiceGender.Unspecified
                        };

                        if (useStreaming)
                        {
                            Stopwatch timer = Stopwatch.StartNew();
                            IAudioSampleSource stream = await synth.SynthesizeSpeechToStreamAsync(synthRequest, new WeakPointer<IAudioGraph>(outputGraph), CancellationToken.None, DefaultRealTimeProvider.Singleton, logger.Clone("TTS"));
                            mixer.AddInput(stream, null, true);
                            timer.Stop();
                            logger.Log("Synth async latency " + timer.ElapsedMillisecondsPrecise() + "ms", LogLevel.Std);
                        }
                        else
                        {
                            Stopwatch timer = Stopwatch.StartNew();
                            SynthesizedSpeech speech = await synth.SynthesizeSpeechAsync(synthRequest, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger.Clone("TTS"));
                            AudioSample sample = await AudioHelpers.DecodeAudioDataUsingCodec(speech.Audio, new RawPcmCodecFactory(), logger.Clone("Decoder"));
                            mixer.AddInput(new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(outputGraph), sample, "SpeechSamplePlayer"), null, true);
                            timer.Stop();
                            logger.Log("Synth sync latency " + timer.ElapsedMillisecondsPrecise() + "ms", LogLevel.Std);
                            if (speech.Words != null)
                            {
                                foreach (var word in speech.Words)
                                {
                                    logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata,
                                        "{0:-10} {1:-10} {2}", word.Offset.PrintTimeSpan(), word.ApproximateLength.PrintTimeSpan(), word.Word);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }
            }
        }

        //public static async Task SecurityPerfTest2()
        //{
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.All);
        //    InMemoryPublicKeyStore serverKeyStore = new InMemoryPublicKeyStore();
        //    ClientAuthenticator client = new ClientAuthenticator(logger, new StandardRSADelegates());
        //    ServerAuthenticator server = new ServerAuthenticator(logger, new InMemoryPublicKeyStore(), new StandardRSADelegates());
        //    ClientIdentifier clientInfo = new ClientIdentifier("testUserId", "testUserName", "testClientId", "testClientName");
        //    client.LoadPrivateKey(clientInfo, ClientAuthenticationScope.Client, new StandardRSADelegates().GenerateRSAKey(1024));
        //    ClientKeyIdentifier keyId = clientInfo.GetKeyIdentifier(ClientAuthenticationScope.UserClient);
        //    await server.RegisterNewClient(clientInfo, ClientAuthenticationScope.UserClient, client.GetPublicKey(keyId));
        //    BigInteger challenge = await server.GenerateChallengeToken(keyId);
        //    client.StoreChallengeToken(challenge, keyId);
        //    BigInteger answer = client.DecryptChallengeToken(keyId);
        //    bool verified = await server.VerifyChallengeToken(keyId, answer);
        //    BigInteger secret = await server.GenerateSharedSecret(keyId);
        //    client.DecryptSharedSecret(secret, keyId);
        //    serverKeyStore.PromoteClient(keyId);

        //    int turns = 100;

        //    Stopwatch timer = Stopwatch.StartNew();
        //    for (int turn = 0; turn < turns; turn++)
        //    {
        //        client.GenerateUniqueRequestToken(keyId);
        //    }
        //    timer.Stop();
        //    Console.WriteLine(((double)timer.ElapsedMilliseconds / (double)turns) + "ms");

        //    timer.Restart();
        //    RequestToken token = client.GenerateUniqueRequestToken(keyId, TimeSpan.FromMinutes(30));
        //    for (int turn = 0; turn < turns; turn++)
        //    {
        //        await server.VerifyRequestToken(keyId, token);
        //    }
        //    timer.Stop();
        //    Console.WriteLine(((double)timer.ElapsedMilliseconds / (double)turns) + "ms");
        //}

        public static void SpellerTest()
        {
            ILogger logger = new ConsoleLogger();
            BingSpeller speller = new BingSpeller("", new PortableHttpClientFactory(), logger);
            while (true)
            {
                Console.Write(">>> ");
                string input = Console.ReadLine();
                Stopwatch timer = new Stopwatch();
                timer.Start();
                IList<Hypothesis<string>> suggestions = speller.SpellCorrect(input, LanguageCode.EN_US, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                timer.Stop();
                logger.Log(timer.ElapsedMilliseconds + "ms", LogLevel.Std);
                if (suggestions == null)
                {
                    logger.Log("Null!", LogLevel.Err);
                }
                if (suggestions.Count == 0)
                {
                    logger.Log("<<< " + input);
                }
                foreach (Hypothesis<string> suggestion in suggestions)
                {
                    logger.Log("<<< " + suggestion.Value + " (" + suggestion.Conf + ")");
                }
            }
        }

        public static async Task PackagerTest()
        {
            ILogger testLogger = new ConsoleLogger();
            IPackageLoader loader = new PortableZipPackageFileLoader(testLogger);
            DirectoryInfo projectDir = new DirectoryInfo(@"C:\Code\Durandal\Plugins\CorePlugins");
            FileInfo pluginDllPath = new FileInfo(@"C:\Code\Durandal\Plugins\CorePlugins\bin\Debug\CorePlugins.dll");
            FileInfo outputFileName = new FileInfo(@"C:\Code\Durandal\test.zip");
            IFileSystem projectFileSystem = new RealFileSystem(testLogger, projectDir.FullName);
            IFileSystem pluginFileSystem = new RealFileSystem(testLogger, pluginDllPath.DirectoryName);
            VirtualPath pluginVirtualFile = new VirtualPath(pluginDllPath.Name);
            ManifestFactory manifestFactory = new ManifestFactory(testLogger, projectFileSystem, VirtualPath.Root, new PluginReflector());
            PackageManifest manifest = await manifestFactory.BuildManifest(pluginFileSystem, pluginVirtualFile);
            PackageFactory factory = new PackageFactory(testLogger, projectFileSystem, VirtualPath.Root);
            VirtualPath pluginFile = new VirtualPath(pluginDllPath.FullName.Substring(projectDir.FullName.Length));
            RealFileSystem targetFileSystem = new RealFileSystem(NullLogger.Singleton, outputFileName.DirectoryName);
            testLogger.Log("Building package file " + outputFileName.FullName);
            factory.BuildPackage(manifest, targetFileSystem, new VirtualPath(outputFileName.Name), pluginFile).Await();
            testLogger.Log("Finished packing " + outputFileName.FullName);
        }

        //public static void BingSynthTest()
        //{
        //    ILogger logger = new ConsoleLogger();
        //    ISpeechSynth synthesizer = new BingSpeechSynth(logger, "fc928add4926448caf82fb3a0fee9627");
        //    string ipa = "dəɹəndəl";
        //    ipa = "dʊəɹəndəl";
        //    ipa = "dʊɹændəl";
        //    string ssml = "<speak>My name is <phoneme alphabet=\"ipa\" ph=\"" + WebUtility.HtmlEncode(ipa) + "\">Durandal</phoneme></speak>";
        //    AudioData data = synthesizer.SynthesizeSpeechAsync(ssml, "en-US", 16000, logger).Await().Audio;
        //    if (data != null)
        //    {
        //        NAudioMixer mixer = new NAudioMixer(logger.Clone("Mixer"));
        //        using (IAudioRenderDevice speakers = new WaveOutPlayer(mixer, logger.Clone("WaveOut")))
        //        {
        //            mixer.PlaySound(data.ToPCM());
        //            Thread.Sleep(5000);
        //        }
        //    }
        //}

        private class Song
        {
            public string Artist;
            public string Album;
            public string Title;
            public string Path;
        }

        private static void WinampFilter()
        {
            List<Song> songs = new List<Song>();
            using (StreamReader parser = new StreamReader(@"C:\Users\Logan Stromberg\Desktop\winamp_library.tsv"))
            {
                while (!parser.EndOfStream)
                {
                    string next = parser.ReadLine();
                    string[] parts = next.Split('\t');
                    if (parts.Length != 4)
                        continue;

                    songs.Add(new Song()
                    {
                        Artist = parts[0],
                        Album = parts[1],
                        Title = parts[2],
                        Path = parts[3]
                    });
                }
                parser.Close();
            }

            for (int song1 = 0; song1 < songs.Count; song1++)
            {
                Song song = songs[song1];
                for (int song2 = song1 + 1; song2 < songs.Count; song2++)
                {
                    Song otherSong = songs[song2];
                    // Title and artist are very similar
                    float titleDist = StringUtils.NormalizedEditDistance(song.Title, otherSong.Title);
                    float artistDist = StringUtils.NormalizedEditDistance(song.Artist, otherSong.Artist);
                    if ((titleDist < 0.10f && artistDist < 0.10f) ||
                        (titleDist < 0.01f && (string.IsNullOrWhiteSpace(song.Artist) || string.IsNullOrWhiteSpace(otherSong.Artist))))
                    {
                        // Same song reprised on different albums
                        if (titleDist < 0.01f && artistDist < 0.01f && !string.IsNullOrWhiteSpace(song.Album) && !string.IsNullOrWhiteSpace(otherSong.Album))
                            continue;

                        Console.WriteLine("{0}/{1} and {2}/{3}", song.Title, song.Artist, otherSong.Title, otherSong.Artist);
                    }
                }
            }
        }
    }
}
