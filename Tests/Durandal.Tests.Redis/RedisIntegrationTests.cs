using Durandal.Common.Audio;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Cache;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Extensions.Redis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Dialog.Services;
using System.Threading;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Utils;
using Durandal.Common.Dialog.Web;
using Durandal.Common.File;
using Durandal.API;
using Durandal.Common.Net.Http;
using Durandal.Common.Dialog;
using Durandal.Common.Net;
using Durandal.Common.ServiceMgmt;
using Durandal.Tests.Common.Audio;

namespace Durandal.Tests.Redis
{
    [TestClass]
    [DoNotParallelize]
    public class RedisIntegrationTests
    {
        private static ILogger _testLogger;
        private static RedisConnectionPool _connectionPool = null;
        private static IRealTimeProvider _realTime = DefaultRealTimeProvider.Singleton;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _testLogger = new ConsoleLogger("Main", LogLevel.All);
            string connectionString = context.Properties["RedisConnectionString"]?.ToString();

            if (!string.IsNullOrEmpty(connectionString))
            {
                _connectionPool = RedisConnectionPool.Create(connectionString, _testLogger.Clone("RedisConnectionPool")).Await();
            }
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            _connectionPool?.Dispose();
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRedisCacheBasic()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No Redis connection string provided in test settings");
            }

            ICache<string> cache = new RedisCache<string>(
                new WeakPointer<RedisConnectionPool>(_connectionPool),
                NullMetricCollector.Singleton,
                new StringByteConverter(),
                _testLogger.Clone("RedisCache"));

            string guid = Guid.NewGuid().ToString("N");
            await cache.Store(guid, "mytestvalue", DateTimeOffset.Now.AddSeconds(10), null, true, _testLogger.Clone("RedisQuery"), _realTime);

            RetrieveResult<string> fetchResult = await cache.TryRetrieve(guid, _testLogger.Clone("RedisQuery"), _realTime, TimeSpan.FromMilliseconds(5000));
            Assert.IsTrue(fetchResult.Success);
            Assert.AreEqual("mytestvalue", fetchResult.Result);
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRedisCacheNonExistentKey()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No Redis connection string provided in test settings");
            }

            ICache<string> cache = new RedisCache<string>(
                new WeakPointer<RedisConnectionPool>(_connectionPool),
                NullMetricCollector.Singleton,
                new StringByteConverter(),
                _testLogger.Clone("RedisCache"));

            RetrieveResult<string> fetchResult = await cache.TryRetrieve("notexist", _testLogger.Clone("RedisQuery"), null);
            Assert.IsFalse(fetchResult.Success);

            fetchResult = await cache.TryRetrieve("notexist", _testLogger.Clone("RedisQuery"), _realTime, TimeSpan.FromMilliseconds(1000));
            Assert.IsFalse(fetchResult.Success);
            Assert.IsTrue(fetchResult.LatencyMs > 1000);
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRedisCacheDelete()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No Redis connection string provided in test settings");
            }

            ICache<string> cache = new RedisCache<string>(
                new WeakPointer<RedisConnectionPool>(_connectionPool),
                NullMetricCollector.Singleton,
                new StringByteConverter(),
                _testLogger.Clone("RedisCache"));

            string guid = Guid.NewGuid().ToString("N");
            await cache.Store(guid, "mytestvalue", DateTimeOffset.Now.AddSeconds(10), null, true, _testLogger.Clone("RedisQuery"), _realTime);

            RetrieveResult<string> fetchResult = await cache.TryRetrieve(guid, _testLogger.Clone("RedisQuery"), _realTime, TimeSpan.FromMilliseconds(5000));
            Assert.IsTrue(fetchResult.Success);
            Assert.AreEqual("mytestvalue", fetchResult.Result);

            await cache.Delete(guid, false, _testLogger.Clone("RedisDelete")).ConfigureAwait(false);

            fetchResult = await cache.TryRetrieve(guid, _testLogger.Clone("RedisQuery"), _realTime, TimeSpan.FromMilliseconds(1000));
            Assert.IsFalse(fetchResult.Success);
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRedisCacheMultipleReads()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No Redis connection string provided in test settings");
            }

            ICache<string> cache = new RedisCache<string>(
                new WeakPointer<RedisConnectionPool>(_connectionPool),
                NullMetricCollector.Singleton,
                new StringByteConverter(),
                _testLogger.Clone("RedisCache"));

            string guid = Guid.NewGuid().ToString("N");
            await cache.Store(guid, "mytestvalue", DateTimeOffset.Now.AddSeconds(10), null, true, _testLogger.Clone("RedisQuery"), _realTime);

            List<Task> tasks = new List<Task>();
            for (int c = 0; c < 10; c++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    RetrieveResult<string> fetchResult = await cache.TryRetrieve(guid, _testLogger.Clone("RedisQuery"), _realTime, TimeSpan.FromMilliseconds(5000));
                    Assert.IsTrue(fetchResult.Success);
                    Assert.AreEqual("mytestvalue", fetchResult.Result);
                }));
            }

            foreach (Task t in tasks)
            {
                await t.ConfigureAwait(false);
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRedisStreamingAudioCache()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No Redis connection string provided in test settings");
            }

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);
            AudioSample outputSample = null;

            IStreamingAudioCache audioCache = new RedisStreamingAudioCache(new WeakPointer<RedisConnectionPool>(_connectionPool));
            //await audioCache.Initialize();

            for (int loop = 0; loop < 10; loop++)
            {
                string key = "Test-" + Guid.NewGuid().ToString("N");
                _testLogger.Log("Using test key " + key);

                //timer.Restart();
                //RetrieveResult<AudioTransportStream> retrieveResult = await audioCache.GetStreamAsync(key);
                //_logger.Log("Try get null stream " + timer.ElapsedMilliseconds);
                //Assert.IsFalse(retrieveResult.Success);

                ILogger queryLogger = _testLogger.Clone("RedisStreamingAudio").CreateTraceLogger(Guid.NewGuid());

                Task writeTask = Task.Run(async () =>
                {
                    using (IAudioGraph inputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                    using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(inputGraph), inputSample, null))
                    using (AudioEncoder encoder = new RawPcmEncoder(new WeakPointer<IAudioGraph>(inputGraph), format, null))
                    using (NonRealTimeStream writeStream = await audioCache.CreateAudioWriteStream(key, encoder.Codec, encoder.CodecParams, queryLogger, _realTime))
                    {
                        sampleSource.ConnectOutput(encoder);
                        await encoder.Initialize(writeStream, false, CancellationToken.None, _realTime);
                        await sampleSource.WriteFully(CancellationToken.None, _realTime);
                    }
                });

                Task readTask = Task.Run(async () =>
                {
                    RetrieveResult<IAudioDataSource> retrieveResult = await audioCache.TryGetAudioReadStream(key, queryLogger, CancellationToken.None, _realTime, TimeSpan.FromSeconds(5));
                    if (!retrieveResult.Success)
                    {
                        queryLogger.Log("Failed to get initial stream", LogLevel.Err);
                        return;
                    }

                    using (IAudioGraph outputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                    using (IAudioDataSource decoderSource = retrieveResult.Result)
                    using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(outputGraph), format, null))
                    using (AudioDecoder decoder = new RawPcmDecoder(new WeakPointer<IAudioGraph>(outputGraph), decoderSource.CodecParams, null))
                    {
                        AudioInitializationResult initResult = await decoder.Initialize(decoderSource.AudioDataReadStream, false, CancellationToken.None, _realTime);
                        if (initResult != AudioInitializationResult.Success)
                        {
                            queryLogger.Log("Failed to initialize; result was " + initResult.ToString(), LogLevel.Err);
                            return;
                        }

                        decoder.ConnectOutput(sampleTarget);
                        await sampleTarget.ReadFully(CancellationToken.None, _realTime);
                        outputSample = sampleTarget.GetAllAudio();
                    }
                });

                await writeTask;
                await readTask;

                Assert.IsNotNull(outputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.9999f);
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRedisStreamingAudioCacheMultiRead()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No Redis connection string provided in test settings");
            }

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);
            AudioSample outputSample = null;

            IStreamingAudioCache audioCache = new RedisStreamingAudioCache(new WeakPointer<RedisConnectionPool>(_connectionPool));
            //await audioCache.Initialize();

            for (int loop = 0; loop < 10; loop++)
            {
                string key = "Test-" + Guid.NewGuid().ToString("N");
                _testLogger.Log("Using test key " + key);

                //timer.Restart();
                //RetrieveResult<AudioTransportStream> retrieveResult = await audioCache.GetStreamAsync(key);
                //_logger.Log("Try get null stream " + timer.ElapsedMilliseconds);
                //Assert.IsFalse(retrieveResult.Success);

                ILogger queryLogger = _testLogger.Clone("RedisStreamingAudio").CreateTraceLogger(Guid.NewGuid());

                Task writeTask = Task.Run(async () =>
                {
                    using (IAudioGraph inputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                    using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(inputGraph), inputSample, null))
                    using (AudioEncoder encoder = new RawPcmEncoder(new WeakPointer<IAudioGraph>(inputGraph), format, null))
                    using (NonRealTimeStream writeStream = await audioCache.CreateAudioWriteStream(key, encoder.Codec, encoder.CodecParams, queryLogger, _realTime))
                    {
                        sampleSource.ConnectOutput(encoder);
                        await encoder.Initialize(writeStream, false, CancellationToken.None, _realTime);
                        await sampleSource.WriteFully(CancellationToken.None, _realTime);
                    }
                });

                List<Task> readTasks = new List<Task>();
                for (int c = 0; c < 5; c++)
                {
                    readTasks.Add(Task.Run(async () =>
                    {
                        RetrieveResult<IAudioDataSource> retrieveResult = await audioCache.TryGetAudioReadStream(key, queryLogger, CancellationToken.None, _realTime, TimeSpan.FromSeconds(5));
                        Assert.IsTrue(retrieveResult.Success, "Failed to get initial stream");

                        using (IAudioGraph outputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                        using (IAudioDataSource decoderSource = retrieveResult.Result)
                        using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(outputGraph), format, null))
                        using (AudioDecoder decoder = new RawPcmDecoder(new WeakPointer<IAudioGraph>(outputGraph), decoderSource.CodecParams, null))
                        {
                            AudioInitializationResult initResult = await decoder.Initialize(decoderSource.AudioDataReadStream, false, CancellationToken.None, _realTime);
                            Assert.AreEqual(AudioInitializationResult.Success, initResult, "Failed to initialize; result was " + initResult.ToString());
                            decoder.ConnectOutput(sampleTarget);
                            await sampleTarget.ReadFully(CancellationToken.None, _realTime);
                            outputSample = sampleTarget.GetAllAudio();
                            Assert.IsNotNull(outputSample);
                            AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.9999f);
                        }
                    }));
                }

                await writeTask;
                foreach (Task readTask in readTasks)
                {
                    await readTask;
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRedisStreamingAudioCacheTinySample()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No Redis connection string provided in test settings");
            }

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[15 * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 15, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 15, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);
            AudioSample outputSample = null;

            IStreamingAudioCache audioCache = new RedisStreamingAudioCache(new WeakPointer<RedisConnectionPool>(_connectionPool));
            //await audioCache.Initialize();

            string key = "Test-" + Guid.NewGuid().ToString("N");
            _testLogger.Log("Using test key " + key);

            ILogger queryLogger = _testLogger.Clone("RedisStreamingAudio").CreateTraceLogger(Guid.NewGuid());

            Task writeTask = Task.Run(async () =>
            {
                using (IAudioGraph inputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(inputGraph), inputSample, null))
                using (AudioEncoder encoder = new RawPcmEncoder(new WeakPointer<IAudioGraph>(inputGraph), format, null))
                using (NonRealTimeStream writeStream = await audioCache.CreateAudioWriteStream(key, encoder.Codec, encoder.CodecParams, queryLogger, _realTime))
                {
                    sampleSource.ConnectOutput(encoder);
                    await encoder.Initialize(writeStream, false, CancellationToken.None, _realTime);
                    await sampleSource.WriteFully(CancellationToken.None, _realTime);
                }
            });

            Task readTask = Task.Run(async () =>
            {
                RetrieveResult<IAudioDataSource> retrieveResult = await audioCache.TryGetAudioReadStream(key, queryLogger, CancellationToken.None, _realTime, TimeSpan.FromSeconds(5));
                if (!retrieveResult.Success)
                {
                    queryLogger.Log("Failed to get initial stream", LogLevel.Err);
                    return;
                }

                using (IAudioGraph outputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (IAudioDataSource decoderSource = retrieveResult.Result)
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(outputGraph), format, null))
                using (AudioDecoder decoder = new RawPcmDecoder(new WeakPointer<IAudioGraph>(outputGraph), decoderSource.CodecParams, null))
                {
                    AudioInitializationResult initResult = await decoder.Initialize(decoderSource.AudioDataReadStream, false, CancellationToken.None, _realTime);
                    if (initResult != AudioInitializationResult.Success)
                    {
                        queryLogger.Log("Failed to initialize; result was " + initResult.ToString(), LogLevel.Err);
                        return;
                    }

                    decoder.ConnectOutput(sampleTarget);
                    await sampleTarget.ReadFully(CancellationToken.None, _realTime);
                    outputSample = sampleTarget.GetAllAudio();
                }
            });

            await writeTask;
            await readTask;

            Assert.IsNotNull(outputSample);
            AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.9999f);
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRedisStreamingAudioCacheRiff()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No Redis connection string provided in test settings");
            }

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);
            AudioSample outputSample = null;

            IStreamingAudioCache audioCache = new RedisStreamingAudioCache(new WeakPointer<RedisConnectionPool>(_connectionPool));
            //await audioCache.Initialize();

            string key = "Test-" + Guid.NewGuid().ToString("N");
            _testLogger.Log("Using test key " + key);

            //timer.Restart();
            //RetrieveResult<AudioTransportStream> retrieveResult = await audioCache.GetStreamAsync(key);
            //_logger.Log("Try get null stream " + timer.ElapsedMilliseconds);
            //Assert.IsFalse(retrieveResult.Success);

            ILogger queryLogger = _testLogger.Clone("RedisStreamingAudio").CreateTraceLogger(Guid.NewGuid());

            Task writeTask = Task.Run(async () =>
            {
                using (IAudioGraph inputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(inputGraph), inputSample, null))
                using (AudioEncoder encoder = new RiffWaveEncoder(new WeakPointer<IAudioGraph>(inputGraph), format, null, queryLogger))
                using (NonRealTimeStream writeStream = await audioCache.CreateAudioWriteStream(key, encoder.Codec, encoder.CodecParams, queryLogger, _realTime))
                {
                    sampleSource.ConnectOutput(encoder);
                    await encoder.Initialize(writeStream, false, CancellationToken.None, _realTime);
                    await sampleSource.WriteFully(CancellationToken.None, _realTime);
                }
            });

            Task readTask = Task.Run(async () =>
            {
                RetrieveResult<IAudioDataSource> retrieveResult = await audioCache.TryGetAudioReadStream(key, queryLogger, CancellationToken.None, _realTime, TimeSpan.FromSeconds(5));
                if (!retrieveResult.Success)
                {
                    queryLogger.Log("Failed to get initial stream", LogLevel.Err);
                    return;
                }

                using (IAudioGraph outputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (IAudioDataSource decoderSource = retrieveResult.Result)
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(outputGraph), format, null))
                using (AudioDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(outputGraph), null))
                {
                    AudioInitializationResult initResult = await decoder.Initialize(decoderSource.AudioDataReadStream, false, CancellationToken.None, _realTime);
                    if (initResult != AudioInitializationResult.Success)
                    {
                        queryLogger.Log("Failed to initialize; result was " + initResult.ToString(), LogLevel.Err);
                        return;
                    }

                    decoder.ConnectOutput(sampleTarget);
                    await sampleTarget.ReadFully(CancellationToken.None, _realTime);
                    outputSample = sampleTarget.GetAllAudio();
                }
            });

            await writeTask;
            await readTask;

            Assert.IsNotNull(outputSample);
            AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.9999f);
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRedisStreamingAudioCacheOverDialogHttpServer()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No Redis connection string provided in test settings");
            }

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
            using (IStreamingAudioCache audioCache = new RedisStreamingAudioCache(new WeakPointer<RedisConnectionPool>(_connectionPool)))
            using (DialogHttpServer dialogHttpServer = new DialogHttpServer(
                null,
                new WeakPointer<IThreadPool>(new TaskThreadPool()),
                httpServer,
                _testLogger.Clone("DialogHttpServer"),
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
                _testLogger.Clone("DialogHttpClient"),
                dialogProtocol))
            {
                await dialogHttpServer.StartServer("TestServer", CancellationToken.None, DefaultRealTimeProvider.Singleton);
                string key = "Test-" + Guid.NewGuid().ToString("N");
                _testLogger.Log("Using test key " + key);

                //timer.Restart();
                //RetrieveResult<AudioTransportStream> retrieveResult = await audioCache.GetStreamAsync(key);
                //_logger.Log("Try get null stream " + timer.ElapsedMilliseconds);
                //Assert.IsFalse(retrieveResult.Success);

                ILogger queryLogger = _testLogger.Clone("RedisStreamingAudio").CreateTraceLogger(Guid.NewGuid());

                Task writeTask = Task.Run(async () =>
                {
                    using (IAudioGraph inputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                    using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(inputGraph), inputSample, null))
                    using (AudioEncoder encoder = new RiffWaveEncoder(new WeakPointer<IAudioGraph>(inputGraph), format, null, queryLogger))
                    using (NonRealTimeStream writeStream = await audioCache.CreateAudioWriteStream(key, encoder.Codec, encoder.CodecParams, queryLogger, _realTime))
                    {
                        sampleSource.ConnectOutput(encoder);
                        await encoder.Initialize(writeStream, false, CancellationToken.None, _realTime);
                        await sampleSource.WriteFully(CancellationToken.None, _realTime);
                    }
                });

                Task readTask = Task.Run(async () =>
                {
                    using (IAudioGraph outputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                    using (IAudioDataSource decoderSource = await dialogHttpClient.GetStreamingAudioResponse("/cache?audio=" + key, queryLogger, CancellationToken.None, _realTime))
                    {
                        if (decoderSource == null)
                        {
                            queryLogger.Log("Failed to initialize; failed to get streaming audio response", LogLevel.Err);
                            return;
                        }

                        using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(outputGraph), format, null))
                        using (AudioDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(outputGraph), null))
                        {
                            AudioInitializationResult initResult = await decoder.Initialize(decoderSource.AudioDataReadStream, false, CancellationToken.None, _realTime);
                            if (initResult != AudioInitializationResult.Success)
                            {
                                queryLogger.Log("Failed to initialize; result was " + initResult.ToString(), LogLevel.Err);
                                return;
                            }

                            decoder.ConnectOutput(sampleTarget);
                            await sampleTarget.ReadFully(CancellationToken.None, _realTime);
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

        [Ignore]
        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRedisCacheSlowWithAbsoluteExpireTime()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No Redis connection string provided in test settings");
            }

            ICache<string> cache = new RedisCache<string>(
                new WeakPointer<RedisConnectionPool>(_connectionPool),
                NullMetricCollector.Singleton,
                new StringByteConverter(),
                _testLogger.Clone("RedisCache"));

            string key = Guid.NewGuid().ToString("N");
            string value = Guid.NewGuid().ToString("N");

            // Set a key to expire in 10 seconds with no TTL
            await cache.Store(key, value, DateTimeOffset.Now.AddSeconds(10), null, true, _testLogger.Clone("RedisQuery"), _realTime);

            // Wait 5 seconds then fetch it
            await Task.Delay(5000);
            RetrieveResult<string> fetchResult = await cache.TryRetrieve(key, _testLogger.Clone("RedisQuery"), _realTime, TimeSpan.FromMilliseconds(5000));
            Assert.IsTrue(fetchResult.Success);
            Assert.AreEqual(value, fetchResult.Result);

            // Then 6 more seconds. It should have expired by now.
            await Task.Delay(6000);
            fetchResult = await cache.TryRetrieve(key, _testLogger.Clone("RedisQuery"), _realTime, TimeSpan.FromMilliseconds(5000));
            Assert.IsFalse(fetchResult.Success);
        }

        [Ignore]
        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRedisCacheSlowWithTTL()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No Redis connection string provided in test settings");
            }

            ICache<string> cache = new RedisCache<string>(
                new WeakPointer<RedisConnectionPool>(_connectionPool),
                NullMetricCollector.Singleton,
                new StringByteConverter(),
                _testLogger.Clone("RedisCache"));

            string key = Guid.NewGuid().ToString("N");
            string value = Guid.NewGuid().ToString("N");

            // Set a key to expire with a 5 second TTL
            await cache.Store(key, value, null, TimeSpan.FromSeconds(5), true, _testLogger.Clone("RedisQuery"), _realTime);

            // Keep touching it every 2 seconds. It should stay alive.
            for (int c = 0; c < 8; c++)
            {
                await Task.Delay(2000);
                RetrieveResult<string> fetchResult = await cache.TryRetrieve(key, _testLogger.Clone("RedisQuery"), _realTime, TimeSpan.FromMilliseconds(5000));
                Assert.IsTrue(fetchResult.Success);
                Assert.AreEqual(value, fetchResult.Result);
            }
        }
    }
}
