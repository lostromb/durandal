namespace Durandal.Tests.Common.Dialog
{
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Remoting;
    using Durandal.Common.Remoting.Protocol;
    using Durandal.Common.IO;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.File;
    using Durandal.Common.Cache;
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.MathExt;
    using Durandal.Common.Utils;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Test;
    using Durandal.Common.Audio.Components;
    using Durandal.Tests.Common.Audio;
    using Durandal.Common.ServiceMgmt;

    [TestClass]
    [DoNotParallelize]
    public class StreamingAudioTests
    {
        [TestMethod]
        public async Task TestStreamingAudioFromDialogHttp()
        {
            ILogger logger = new ConsoleLogger("HttpTests", LogLevel.All);
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            CancellationTokenSource testCancelizer = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            CancellationToken cancelToken = testCancelizer.Token;

            // Build a dialog http server
            InMemoryStreamingAudioCache audioCache = new InMemoryStreamingAudioCache();
            IDialogTransportProtocol transportProtocol = new DialogJsonTransportProtocol();
            InMemoryConfiguration baseConfig = new InMemoryConfiguration(logger.Clone("DialogBaseConfig"));
            DialogConfiguration dialogConfig = new DialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            DialogEngineParameters engineParameters = new DialogEngineParameters(dialogConfig, new WeakPointer<IDurandalPluginProvider>(new BasicPluginProvider()));
            using (DialogProcessingEngine engine = new DialogProcessingEngine(engineParameters))
            {
                DialogWebConfiguration webConfiguration = new DialogWebConfiguration(new WeakPointer<IConfiguration>(baseConfig));
                DialogWebParameters webParameters = new DialogWebParameters(webConfiguration, new WeakPointer<DialogProcessingEngine>(engine));
                DialogWebService service = DialogWebService.Create(webParameters, cancelToken).Await();
                DialogHttpServer dialogHttpServer = new DialogHttpServer(
                    service,
                    new WeakPointer<IThreadPool>(new TaskThreadPool()),
                    new NullHttpServer(),
                    logger.Clone("DialogHttpServer"),
                    NullFileSystem.Singleton,
                    new WeakPointer<ICache<CachedWebData>>(new InMemoryCache<CachedWebData>()),
                    new WeakPointer<ICache<ClientContext>>(new InMemoryCache<ClientContext>()),
                    audioCache,
                    new List<IDialogTransportProtocol>()
                    {
                        transportProtocol
                    },
                    NullMetricCollector.Singleton,
                    DimensionSet.Empty,
                    "UnitTestHost");

                await dialogHttpServer.StartServer("TestSocketServer", cancelToken, DefaultRealTimeProvider.Singleton);

                IHttpClient httpClient = new DirectHttpClient(dialogHttpServer);
                httpClient.SetReadTimeout(TimeSpan.FromSeconds(10));
                IDialogClient dialogClient = new DialogHttpClient(httpClient, logger.Clone("DialogHttpClient"), transportProtocol);

                AudioSampleFormat audioFormat = AudioSampleFormat.Stereo(44100);
                AudioSample inputSample = DialogTestHelpers.GenerateUtterance(audioFormat, 5000);
                AudioSample outputSample = null;
                string audioCacheKey = Guid.NewGuid().ToString("N");

                IRealTimeProvider writeTaskTime = lockStepTime.Fork("WriteTask");
                Task writeTask = Task.Run(async () =>
                {
                    try
                    {
                        logger.Log("Write thread started");
                        await writeTaskTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);

                        using (IAudioGraph writeGraph = new AudioGraph(AudioGraphCapabilities.None))
                        using (RawPcmEncoder pcmEncoder = new RawPcmEncoder(new WeakPointer<IAudioGraph>(writeGraph), audioFormat, null))
                        using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(writeGraph), inputSample, null))
                        using (NonRealTimeStream writeStream = await audioCache.CreateAudioWriteStream(
                            audioCacheKey,
                            pcmEncoder.Codec,
                            pcmEncoder.CodecParams,
                            logger.Clone("AudioWriteTask"),
                            writeTaskTime))
                        {
                            await pcmEncoder.Initialize(writeStream, false, cancelToken, writeTaskTime);
                            pcmEncoder.ConnectInput(sampleSource);
                            int samplesWritten = 0;
                            while (!sampleSource.PlaybackFinished)
                            {
                                samplesWritten += await sampleSource.WriteSamplesToOutput(1000, cancelToken, writeTaskTime);
                                logger.Log("Wrote " + samplesWritten + " samples");
                                await writeTaskTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);
                            }

                            await pcmEncoder.Finish(cancelToken, writeTaskTime);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e, LogLevel.Err);
                    }
                    finally
                    {
                        writeTaskTime.Merge();
                        logger.Log("Finished write task");
                    }
                });

                IRealTimeProvider readTaskTime = lockStepTime.Fork("ReadTask");
                Task readTask = Task.Run(async () =>
                {
                    try
                    {
                        logger.Log("Read thread started");
                        await readTaskTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);

                        using (IAudioGraph readGraph = new AudioGraph(AudioGraphCapabilities.None))
                        using (IAudioDataSource streamingAudioSource = await dialogClient.GetStreamingAudioResponse(
                            "/cache?audio=" + audioCacheKey,
                            logger.Clone("GetStreamingAudio"),
                            cancelToken,
                            readTaskTime))
                        using (RawPcmDecoder pcmDecoder = new RawPcmDecoder(new WeakPointer<IAudioGraph>(readGraph), streamingAudioSource.CodecParams, null))
                        {
                            await pcmDecoder.Initialize(streamingAudioSource.AudioDataReadStream, false, cancelToken, readTaskTime);
                            using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(readGraph), pcmDecoder.OutputFormat, null))
                            {
                                pcmDecoder.ConnectOutput(sampleTarget);

                                int samplesRead = 0;
                                while (!pcmDecoder.PlaybackFinished)
                                {
                                    samplesRead += await sampleTarget.ReadSamplesFromInput(60000, cancelToken, readTaskTime);
                                    logger.Log("Read " + samplesRead + " samples");
                                    await readTaskTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);
                                }

                                outputSample = sampleTarget.GetAllAudio();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e, LogLevel.Err);
                    }
                    finally
                    {
                        readTaskTime.Merge();
                        logger.Log("Finished read task");
                    }
                });

                lockStepTime.Step(TimeSpan.FromSeconds(5), 100);

                await readTask;
                await writeTask;

                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.9999f);
            }
        }

        [TestMethod]
        public async Task TestInMemoryStreamingAudioCache()
        {
            ILogger logger = new ConsoleLogger();
            InMemoryStreamingAudioCache cache = new InMemoryStreamingAudioCache();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger);
            CancellationTokenSource testCancelizer = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            CancellationToken cancelToken = testCancelizer.Token;
            AudioSampleFormat audioFormat = AudioSampleFormat.Mono(16000);
            AudioSample inputSample = DialogTestHelpers.GenerateUtterance(audioFormat, 4000);

            IRealTimeProvider writeThreadTime = lockStepTime.Fork("WriteThreadTime");
            Task writeTask = Task.Run(async () =>
            {
                try
                {
                    logger.Log("Write thread started");
                    await writeThreadTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);

                    using (IAudioGraph writeGraph = new AudioGraph(AudioGraphCapabilities.None))
                    using (RawPcmEncoder pcmEncoder = new RawPcmEncoder(new WeakPointer<IAudioGraph>(writeGraph), audioFormat, null))
                    using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(writeGraph), inputSample, null))
                    using (NonRealTimeStream writeStream = await cache.CreateAudioWriteStream(
                        "testkey",
                        pcmEncoder.Codec,
                        pcmEncoder.CodecParams,
                        logger.Clone("AudioWriteTask"),
                        writeThreadTime))
                    {
                        // Write 1000 samples of audio data every 10 milliseconds
                        // This write should take at least 640 milliseconds virtual time
                        await pcmEncoder.Initialize(writeStream, true, cancelToken, writeThreadTime);
                        pcmEncoder.ConnectInput(sampleSource);
                        int samplesWritten = 0;
                        while (!sampleSource.PlaybackFinished)
                        {
                            samplesWritten += await sampleSource.WriteSamplesToOutput(1000, cancelToken, writeThreadTime);
                            logger.Log("Wrote " + samplesWritten + " samples");
                            await writeThreadTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);
                        }

                        await pcmEncoder.Finish(cancelToken, writeThreadTime);
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    writeThreadTime.Merge();
                    logger.Log("Finished write task");
                }
            });

            int samplesPerChannelRead = 0;
            IRealTimeProvider readThreadTime = lockStepTime.Fork("ReadThreadTime");
            Task readTask = Task.Run(async () =>
            {
                try
                {
                    logger.Log("Read thread started");
                    await readThreadTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);

                    RetrieveResult<IAudioDataSource> rr = await cache.TryGetAudioReadStream("testkey", logger.Clone("StreamingAudioCache"), cancelToken, readThreadTime, TimeSpan.FromMilliseconds(100));
                    Assert.IsNotNull(rr);
                    Assert.IsTrue(rr.Success);
                    Assert.IsNotNull(rr.Result);
                    using (IAudioGraph readGraph = new AudioGraph(AudioGraphCapabilities.None))
                    using (IAudioDataSource cacheReadPipe = rr.Result)
                    using (RawPcmDecoder pcmDecoder = new RawPcmDecoder(new WeakPointer<IAudioGraph>(readGraph), rr.Result.CodecParams, null))
                    {
                        await pcmDecoder.Initialize(rr.Result.AudioDataReadStream, false, cancelToken, readThreadTime);
                        using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(readGraph), pcmDecoder.OutputFormat, null))
                        {
                            float[] scratch = new float[65536];
                            pcmDecoder.ConnectOutput(sampleTarget);
                            
                            while (!pcmDecoder.PlaybackFinished)
                            {
                                int readResult = await pcmDecoder.ReadAsync(scratch, 0, 65535, cancelToken, readThreadTime);
                                if (readResult > 0)
                                {
                                    samplesPerChannelRead += readResult;
                                    logger.Log("Read " + samplesPerChannelRead + " samples");
                                    await readThreadTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);
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
                    readThreadTime.Merge();
                    logger.Log("Finished read task");
                }
            });

            // Check that the initial samples pipe straight across with little delay
            lockStepTime.Step(TimeSpan.FromMilliseconds(50), 10);
            Assert.IsTrue(samplesPerChannelRead > 1500);

            lockStepTime.Step(TimeSpan.FromMilliseconds(500), 10);
            Assert.IsTrue(samplesPerChannelRead > 20000);

            // Now step through until we've written all of the data
            lockStepTime.Step(TimeSpan.FromSeconds(2), 10);

            await readTask;
            await writeTask;

            Assert.AreEqual(inputSample.LengthSamplesPerChannel, samplesPerChannelRead);
        }

        [TestMethod]
        public async Task TestInMemoryStreamingAudioCacheMultipleConcurrentReaders()
        {
            ILogger logger = new ConsoleLogger();
            InMemoryStreamingAudioCache cache = new InMemoryStreamingAudioCache();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger);
            CancellationTokenSource testCancelizer = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            CancellationToken cancelToken = testCancelizer.Token;
            AudioSampleFormat audioFormat = AudioSampleFormat.Mono(16000);
            AudioSample inputSample = DialogTestHelpers.GenerateUtterance(audioFormat, 4000);

            IRealTimeProvider writeThreadTime = lockStepTime.Fork("WriteThreadTime");
            Task writeTask = Task.Run(async () =>
            {
                try
                {
                    logger.Log("Write thread started");
                    await writeThreadTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);

                    using (IAudioGraph writeGraph = new AudioGraph(AudioGraphCapabilities.None))
                    using (RawPcmEncoder pcmEncoder = new RawPcmEncoder(new WeakPointer<IAudioGraph>(writeGraph), audioFormat, null))
                    using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(writeGraph), inputSample, null))
                    using (NonRealTimeStream writeStream = await cache.CreateAudioWriteStream(
                        "testkey",
                        pcmEncoder.Codec,
                        pcmEncoder.CodecParams,
                        logger.Clone("AudioWriteTask"),
                        writeThreadTime))
                    {
                        // Write 1000 samples of audio data every 10 milliseconds
                        // This write should take at least 640 milliseconds virtual time
                        await pcmEncoder.Initialize(writeStream, true, cancelToken, writeThreadTime);
                        pcmEncoder.ConnectInput(sampleSource);
                        int samplesWritten = 0;
                        while (!sampleSource.PlaybackFinished)
                        {
                            samplesWritten += await sampleSource.WriteSamplesToOutput(1000, cancelToken, writeThreadTime);
                            logger.Log("Wrote " + samplesWritten + " samples");
                            await writeThreadTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);
                        }

                        await pcmEncoder.Finish(cancelToken, writeThreadTime);
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    writeThreadTime.Merge();
                    logger.Log("Finished write task");
                }
            });

            List<Task<int>> readTasks = new List<Task<int>>();
            for (int reader = 0; reader < 5; reader++)
            {
                lockStepTime.Step(TimeSpan.FromMilliseconds(10));
                IRealTimeProvider readThreadTime = lockStepTime.Fork("ReadThreadTime" + reader);
                ILogger readThreadLogger = logger.Clone("ReadThread" + reader);
                readTasks.Add(Task<int>.Run(async () =>
                {
                    int samplesPerChannelRead = 0;

                    try
                    {
                        readThreadLogger.Log("Read thread started");
                        await readThreadTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);

                        RetrieveResult<IAudioDataSource> rr = await cache.TryGetAudioReadStream("testkey", readThreadLogger.Clone("StreamingAudioCache"), cancelToken, readThreadTime, TimeSpan.FromMilliseconds(100));
                        Assert.IsNotNull(rr);
                        Assert.IsTrue(rr.Success);
                        Assert.IsNotNull(rr.Result);
                        using (IAudioGraph readGraph = new AudioGraph(AudioGraphCapabilities.None))
                        using (IAudioDataSource cacheReadPipe = rr.Result)
                        using (RawPcmDecoder pcmDecoder = new RawPcmDecoder(new WeakPointer<IAudioGraph>(readGraph), rr.Result.CodecParams, null))
                        {
                            await pcmDecoder.Initialize(rr.Result.AudioDataReadStream, false, cancelToken, readThreadTime);
                            using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(readGraph), pcmDecoder.OutputFormat, null))
                            {
                                float[] scratch = new float[65536];
                                pcmDecoder.ConnectOutput(sampleTarget);

                                while (!pcmDecoder.PlaybackFinished)
                                {
                                    int readResult = await pcmDecoder.ReadAsync(scratch, 0, 65535, cancelToken, readThreadTime);
                                    if (readResult > 0)
                                    {
                                        samplesPerChannelRead += readResult;
                                        readThreadLogger.Log("Read " + samplesPerChannelRead + " samples");
                                        await readThreadTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        readThreadLogger.Log(e, LogLevel.Err);
                    }
                    finally
                    {
                        readThreadTime.Merge();
                        logger.Log("Finished read task");
                    }

                    return samplesPerChannelRead;
                }));
            }

            // Step through until we've written all of the data
            lockStepTime.Step(TimeSpan.FromSeconds(2), 10);

            await writeTask;
            foreach (var readTask in readTasks)
            {
                int samplesPerChannelRead = await readTask;
                Assert.AreEqual(inputSample.LengthSamplesPerChannel, samplesPerChannelRead);
            }
        }

        [TestMethod]
        public async Task TestInMemoryStreamingAudioCacheOverDialogHttpServer()
        {
            ILogger logger = new ConsoleLogger();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);
            AudioSample outputSample = null;

            IDialogTransportProtocol dialogProtocol = new DialogJsonTransportProtocol();

            //using (ISocketServer socketServer = new RawTcpSocketServer(
            //    new string[] { "http://localhost:63440" },
            //    _testLogger.Clone("SocketHttpServer"),
            //    DefaultRealTimeProvider.Singleton,
            //    NullMetricCollector.Singleton,
            //    DimensionSet.Empty,
            //    new TaskThreadPool()))
            //using (IHttpServer httpServer = new SocketHttpServer(socketServer, _testLogger.Clone("HttpServer")))
            //using (IHttpServer httpServer = new HttpListenerServer(new string[] { "http://localhost:63440" }, _testLogger.Clone("HttpServer"), new TaskThreadPool()))
            using (IHttpServer httpServer = new NullHttpServer())
            using (IStreamingAudioCache audioCache = new InMemoryStreamingAudioCache())
            using (DialogHttpServer dialogHttpServer = new DialogHttpServer(
                null,
                new WeakPointer<IThreadPool>(new TaskThreadPool()),
                httpServer,
                logger.Clone("DialogHttpServer"),
                new InMemoryFileSystem(),
                new WeakPointer<ICache<CachedWebData>>(new InMemoryCache<CachedWebData>()),
                new WeakPointer<ICache<ClientContext>>(new InMemoryCache<ClientContext>()),
                audioCache,
                new List<IDialogTransportProtocol>() { dialogProtocol },
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                "TestServer"))
            using (IDialogClient dialogHttpClient = new DialogHttpClient(
                //new PortableHttpClient(new Uri("http://localhost:63440"), _testLogger.Clone("DialogHttp")),
                new DirectHttpClient(dialogHttpServer),
                logger.Clone("DialogHttpClient"),
                dialogProtocol))
            {
                await dialogHttpServer.StartServer("TestServer", CancellationToken.None, realTime);
                IRandom random = new FastRandom();
                string key = "Test-" + Guid.NewGuid().ToString("N");
                logger.Log("Using test key " + key);

                //timer.Restart();
                //RetrieveResult<AudioTransportStream> retrieveResult = await audioCache.GetStreamAsync(key);
                //_logger.Log("Try get null stream " + timer.ElapsedMilliseconds);
                //Assert.IsFalse(retrieveResult.Success);

                ILogger queryLogger = logger.Clone("RedisStreamingAudio").CreateTraceLogger(Guid.NewGuid());

                Task writeTask = Task.Run(async () =>
                {
                    using (IAudioGraph inputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                    using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(inputGraph), inputSample, null))
                    using (AudioEncoder encoder = new RawPcmEncoder(new WeakPointer<IAudioGraph>(inputGraph), format, null))
                    using (NonRealTimeStream writeStream = await audioCache.CreateAudioWriteStream(key, encoder.Codec, encoder.CodecParams, queryLogger, realTime))
                    {
                        sampleSource.ConnectOutput(encoder);
                        await encoder.Initialize(writeStream, false, CancellationToken.None, realTime);
                        await sampleSource.WriteFully(CancellationToken.None, realTime);
                    }
                });

                Task readTask = Task.Run(async () =>
                {
                    using (IAudioGraph outputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                    using (IAudioDataSource decoderSource = await dialogHttpClient.GetStreamingAudioResponse("/cache?audio=" + key, queryLogger, CancellationToken.None, realTime))
                    {
                        if (decoderSource == null)
                        {
                            queryLogger.Log("Failed to initialize; failed to get streaming audio response", LogLevel.Err);
                            return;
                        }

                        using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(outputGraph), format, null))
                        using (AudioDecoder decoder = new RawPcmDecoder(new WeakPointer<IAudioGraph>(outputGraph), decoderSource.CodecParams, null))
                        {
                            AudioInitializationResult initResult = await decoder.Initialize(decoderSource.AudioDataReadStream, false, CancellationToken.None, realTime);
                            if (initResult != AudioInitializationResult.Success)
                            {
                                queryLogger.Log("Failed to initialize; result was " + initResult.ToString(), LogLevel.Err);
                                return;
                            }

                            decoder.ConnectOutput(sampleTarget);
                            await sampleTarget.ReadFully(CancellationToken.None, realTime);
                            outputSample = sampleTarget.GetAllAudio();
                        }
                    }
                });

                await writeTask;
                await readTask;

                Assert.IsNotNull(outputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.9999f);
            }
        }
    }
}
