using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Codecs.ADPCM;
using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Components.NetworkAudio;
using Durandal.Common.Audio.Test;
using Durandal.Common.Events;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Audio.Network
{
    [TestClass]
    public class NetworkAudioTests
    {
        [TestMethod]
        public async Task Test_SocketAudio_HalfDuplex()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            float[] sampleData = new float[sampleFormat.SampleRateHz * sampleFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, sampleFormat.NumChannels, 0.7f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, sampleFormat.NumChannels, 0.7f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, sampleFormat);

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioGraph recvGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                Task<NetworkAudioEndpoint> createWriteTask = SocketAudioEndpoint.CreateWriteOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "pcm",
                    sampleFormat,
                    "NetworkAudioSend",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("NetworkAudioSend"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton);
                Task<NetworkAudioEndpoint> createReadTask = SocketAudioEndpoint.CreateReadOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(recvGraph),
                    "NetworkAudioRecv",
                    new WeakPointer<ISocket>(socketPair.ServerSocket),
                    logger.Clone("NetworkAudioRecv"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton);

                using (NetworkAudioEndpoint writeEndpoint = await createWriteTask)
                using (NetworkAudioEndpoint readEndpoint = await createReadTask)
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(sendGraph), inputSample, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(recvGraph), readEndpoint.IncomingAudio.OutputFormat, null))
                {
                    sampleSource.ConnectOutput(writeEndpoint.OutgoingAudio);
                    sampleTarget.ConnectInput(readEndpoint.IncomingAudio);
                    await sampleSource.WriteFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await writeEndpoint.CloseWrite(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.99f);
                }
            }
        }

        [TestMethod]
        public async Task Test_SocketAudio_FullDuplex()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            using (CancellationTokenSource testKillerSource = new CancellationTokenSource())
            {
                testKillerSource.CancelAfter(TimeSpan.FromSeconds(5));
                CancellationToken testKiller = testKillerSource.Token;
                AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
                IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

                float[] sampleData = new float[sampleFormat.SampleRateHz * sampleFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, sampleFormat.NumChannels, 0.7f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, sampleFormat.NumChannels, 0.7f, 0.5f);
                AudioSample inputSample = new AudioSample(sampleData, sampleFormat);
                LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair();

                IRealTimeProvider threadTimeA = lockStepTime.Fork("ThreadA");
                IRealTimeProvider threadTimeB = lockStepTime.Fork("ThreadB");
                Task endpointATask = Task.Run(async () =>
                {
                    try
                    {
                        using (IAudioGraph inputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                        using (IAudioGraph outputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                        using (NetworkAudioEndpoint endpoint =
                            await SocketAudioEndpoint.CreateReadWriteEndpoint(
                                codecFactory,
                                new WeakPointer<IAudioGraph>(inputGraph),
                                new WeakPointer<IAudioGraph>(outputGraph),
                                "pcm",
                                sampleFormat,
                                "NetworkAudioA",
                                new WeakPointer<ISocket>(socketPair.ClientSocket),
                                logger.Clone("NetworkAudioA"),
                                testKiller,
                                threadTimeA))
                        using (FixedAudioSampleSource sampleSource =
                            new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(outputGraph), inputSample, null))
                        using (BucketAudioSampleTarget sampleTarget =
                            new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(inputGraph), endpoint.IncomingAudio.OutputFormat, null))
                        {
                            sampleSource.ConnectOutput(endpoint.OutgoingAudio);
                            sampleTarget.ConnectInput(endpoint.IncomingAudio);

                            await sampleSource.WriteFully(testKiller, threadTimeA).ConfigureAwait(false);
                            await endpoint.CloseWrite(testKiller, threadTimeA).ConfigureAwait(false);
                            // Lock step!
                            await threadTimeA.WaitAsync(TimeSpan.FromMilliseconds(100), testKiller).ConfigureAwait(false);
                            await sampleTarget.ReadFully(testKiller, threadTimeA).ConfigureAwait(false);
                            AudioSample outputSample = sampleTarget.GetAllAudio();
                            AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.99f);
                        }
                    }
                    finally
                    {
                        threadTimeA.Merge();
                    }
                });

                Task endpointBTask = Task.Run(async () =>
                {
                    try
                    {
                        using (IAudioGraph inputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                        using (IAudioGraph outputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                        using (NetworkAudioEndpoint endpoint =
                            await SocketAudioEndpoint.CreateReadWriteEndpoint(
                                codecFactory,
                                new WeakPointer<IAudioGraph>(inputGraph),
                                new WeakPointer<IAudioGraph>(outputGraph),
                                "pcm",
                                sampleFormat,
                                "NetworkAudioB",
                                new WeakPointer<ISocket>(socketPair.ServerSocket),
                                logger.Clone("NetworkAudioB"),
                                testKiller,
                                threadTimeB))
                        using (FixedAudioSampleSource sampleSource =
                            new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(outputGraph), inputSample, null))
                        using (BucketAudioSampleTarget sampleTarget =
                            new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(inputGraph), endpoint.IncomingAudio.OutputFormat, null))
                        {
                            sampleSource.ConnectOutput(endpoint.OutgoingAudio);
                            sampleTarget.ConnectInput(endpoint.IncomingAudio);

                            await sampleSource.WriteFully(testKiller, threadTimeB).ConfigureAwait(false);
                            await endpoint.CloseWrite(testKiller, threadTimeB).ConfigureAwait(false);
                            // Lock step!
                            await threadTimeB.WaitAsync(TimeSpan.FromMilliseconds(100), testKiller).ConfigureAwait(false);
                            await sampleTarget.ReadFully(testKiller, threadTimeB).ConfigureAwait(false);
                            AudioSample outputSample = sampleTarget.GetAllAudio();
                            AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.99f);
                        }
                    }
                    finally
                    {
                        threadTimeB.Merge();
                    }
                });

                lockStepTime.Step(TimeSpan.FromSeconds(1), 20);
                await endpointATask;
                await endpointBTask;
            }
        }

        [TestMethod]
        public async Task Test_SocketAudio_Error_TwoReadEndpoints()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioGraph recvGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                Task<NetworkAudioEndpoint> create1Task = SocketAudioEndpoint.CreateReadOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "Endpoint1",
                    new WeakPointer<ISocket>(socketPair.ServerSocket),
                    logger.Clone("Endpoint1"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton);
                Task<NetworkAudioEndpoint> create2Task = SocketAudioEndpoint.CreateReadOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(recvGraph),
                    "Endpoint2",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("Endpoint2"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton);

                try
                {
                    using (NetworkAudioEndpoint endpoint1 = await create1Task)
                    using (NetworkAudioEndpoint endpoint2 = await create2Task)
                    {
                        Assert.Fail("Expected an InvalidOperationException");
                    }
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public async Task Test_SocketAudio_Error_TwoWriteEndpoints()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioGraph recvGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                Task<NetworkAudioEndpoint> create1Task = SocketAudioEndpoint.CreateWriteOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "pcm",
                    sampleFormat,
                    "Endpoint1",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("Endpoint1"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton);
                Task<NetworkAudioEndpoint> create2Task = SocketAudioEndpoint.CreateWriteOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(recvGraph),
                    "pcm",
                    sampleFormat,
                    "Endpoint2",
                    new WeakPointer<ISocket>(socketPair.ServerSocket),
                    logger.Clone("Endpoint2"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton);

                try
                {
                    using (NetworkAudioEndpoint endpoint1 = await create1Task)
                    using (NetworkAudioEndpoint endpoint2 = await create2Task)
                    {
                        Assert.Fail("Expected an InvalidOperationException");
                    }
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public async Task Test_SocketAudio_NegotiateExtraReadCapability()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            float[] sampleData = new float[sampleFormat.SampleRateHz * sampleFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, sampleFormat.NumChannels, 0.7f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, sampleFormat.NumChannels, 0.7f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, sampleFormat);

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioGraph recvGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                Task<NetworkAudioEndpoint> createWriteTask = SocketAudioEndpoint.CreateReadWriteEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(recvGraph),
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "pcm",
                    sampleFormat,
                    "NetworkAudioSend",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("NetworkAudioSend"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton);
                Task<NetworkAudioEndpoint> createReadTask = SocketAudioEndpoint.CreateReadOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(recvGraph),
                    "NetworkAudioRecv",
                    new WeakPointer<ISocket>(socketPair.ServerSocket),
                    logger.Clone("NetworkAudioRecv"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton);

                using (NetworkAudioEndpoint writeEndpoint = await createWriteTask)
                using (NetworkAudioEndpoint readEndpoint = await createReadTask)
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(sendGraph), inputSample, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(recvGraph), readEndpoint.IncomingAudio.OutputFormat, null))
                {
                    Assert.AreEqual(NetworkDuplex.Read, readEndpoint.Duplex);
                    Assert.AreEqual(NetworkDuplex.Write, writeEndpoint.Duplex);
                    Assert.IsNull(writeEndpoint.IncomingAudio);
                    sampleSource.ConnectOutput(writeEndpoint.OutgoingAudio);
                    sampleTarget.ConnectInput(readEndpoint.IncomingAudio);
                    await sampleSource.WriteFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await writeEndpoint.CloseWrite(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.99f);
                }
            }
        }

        [TestMethod]
        public async Task Test_SocketAudio_NegotiateExtraWriteCapability()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            float[] sampleData = new float[sampleFormat.SampleRateHz * sampleFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, sampleFormat.NumChannels, 0.7f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, sampleFormat.NumChannels, 0.7f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, sampleFormat);

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioGraph recvGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                Task<NetworkAudioEndpoint> createWriteTask = SocketAudioEndpoint.CreateWriteOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "pcm",
                    sampleFormat,
                    "NetworkAudioSend",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("NetworkAudioSend"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton);
                Task<NetworkAudioEndpoint> createReadTask = SocketAudioEndpoint.CreateReadWriteEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(recvGraph),
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "pcm",
                    sampleFormat,
                    "NetworkAudioRecv",
                    new WeakPointer<ISocket>(socketPair.ServerSocket),
                    logger.Clone("NetworkAudioRecv"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton);

                using (NetworkAudioEndpoint writeEndpoint = await createWriteTask)
                using (NetworkAudioEndpoint readEndpoint = await createReadTask)
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(sendGraph), inputSample, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(recvGraph), readEndpoint.IncomingAudio.OutputFormat, null))
                {
                    Assert.AreEqual(NetworkDuplex.Read, readEndpoint.Duplex);
                    Assert.AreEqual(NetworkDuplex.Write, writeEndpoint.Duplex);
                    Assert.IsNull(readEndpoint.OutgoingAudio);
                    sampleSource.ConnectOutput(writeEndpoint.OutgoingAudio);
                    sampleTarget.ConnectInput(readEndpoint.IncomingAudio);
                    await sampleSource.WriteFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await writeEndpoint.CloseWrite(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.99f);
                }
            }
        }

        [TestMethod]
        public async Task Test_SocketAudio_Error_OutgoingCodecNotSupported()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();

            try
            {
                using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (var endpoint = await SocketAudioEndpoint.CreateWriteOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "some_exotic_codec",
                    sampleFormat,
                    "Endpoint1",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("Endpoint1"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton))
                {
                    Assert.Fail("Expected a NotSupportedException");
                }
            }
            catch (NotSupportedException e)
            {
                Assert.IsTrue(e.Message.Contains("some_exotic_codec"));
            }
        }

        [TestMethod]
        public async Task Test_SocketAudio_Error_RemoteEndpointClosesPrematurely()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            byte[] junkData = StringUtils.UTF8_WITHOUT_BOM.GetBytes("fart");
            await socketPair.ServerSocket.WriteAsync(junkData, 0, junkData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.Write);

            try
            {
                using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (var endpoint = await SocketAudioEndpoint.CreateWriteOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "pcm",
                    sampleFormat,
                    "Endpoint1",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("Endpoint1"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton))
                {
                    Assert.Fail("Expected an EndOfStreamException");
                }
            }
            catch (EndOfStreamException) { }
        }

        [TestMethod]
        public async Task Test_SocketAudio_Error_RemoteEndpointSendsWrongData()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            byte[] junkData = StringUtils.UTF8_WITHOUT_BOM.GetBytes("GET /index.html HTTP/1.1\r\nHost: zombo.com\r\n\r\n");
            await socketPair.ServerSocket.WriteAsync(junkData, 0, junkData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.Write);

            try
            {
                using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (var endpoint = await SocketAudioEndpoint.CreateWriteOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "pcm",
                    sampleFormat,
                    "Endpoint1",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("Endpoint1"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton))
                {
                    Assert.Fail("Expected an InvalidDataException");
                }
            }
            catch (InvalidDataException) { }
        }

        [TestMethod]
        public async Task Test_SocketAudio_Error_RemoteEndpointSendsTooLargeHeader()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            byte[] junkData = StringUtils.UTF8_WITHOUT_BOM.GetBytes("DurandalNetworkAudio");
            await socketPair.ServerSocket.WriteAsync(junkData, 0, junkData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            byte[] payloadLength = new byte[4];
            BinaryHelpers.Int32ToByteArrayLittleEndian(1025, payloadLength, 0);
            await socketPair.ServerSocket.WriteAsync(payloadLength, 0, payloadLength.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.Write);

            try
            {
                using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (var endpoint = await SocketAudioEndpoint.CreateWriteOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "pcm",
                    sampleFormat,
                    "Endpoint1",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("Endpoint1"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton))
                {
                    Assert.Fail("Expected an InvalidDataException");
                }
            }
            catch (InvalidDataException) { }
        }

        [TestMethod]
        public async Task Test_SocketAudio_Error_RemoteEndpointSendsNoDuplex()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            byte[] junkData = StringUtils.UTF8_WITHOUT_BOM.GetBytes("DurandalNetworkAudio");
            await socketPair.ServerSocket.WriteAsync(junkData, 0, junkData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            byte[] header = StringUtils.UTF8_WITHOUT_BOM.GetBytes("Codec=pcm\nCodecParams=quantum_entanglement=1\n");
            byte[] payloadLength = new byte[4];
            BinaryHelpers.Int32ToByteArrayLittleEndian(header.Length, payloadLength, 0);
            await socketPair.ServerSocket.WriteAsync(payloadLength, 0, payloadLength.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.WriteAsync(header, 0, header.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.Write);

            try
            {
                using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (var endpoint = await SocketAudioEndpoint.CreateReadOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "Endpoint1",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("Endpoint1"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton))
                {
                    Assert.Fail("Expected a FormatException");
                }
            }
            catch (FormatException e)
            {
                Assert.IsTrue(e.Message.Contains("Remote audio endpoint did not send correct duplex"));
            }
        }

        [TestMethod]
        public async Task Test_SocketAudio_Error_RemoteEndpointSendsInvalidDuplex()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            byte[] junkData = StringUtils.UTF8_WITHOUT_BOM.GetBytes("DurandalNetworkAudio");
            await socketPair.ServerSocket.WriteAsync(junkData, 0, junkData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            byte[] header = StringUtils.UTF8_WITHOUT_BOM.GetBytes("Duplex=three\nCodec=pcm\nCodecParams=quantum_entanglement=1\n");
            byte[] payloadLength = new byte[4];
            BinaryHelpers.Int32ToByteArrayLittleEndian(header.Length, payloadLength, 0);
            await socketPair.ServerSocket.WriteAsync(payloadLength, 0, payloadLength.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.WriteAsync(header, 0, header.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.Write);

            try
            {
                using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (var endpoint = await SocketAudioEndpoint.CreateReadOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "Endpoint1",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("Endpoint1"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton))
                {
                    Assert.Fail("Expected a FormatException");
                }
            }
            catch (FormatException e)
            {
                Assert.IsTrue(e.Message.Contains("Remote audio endpoint did not send correct duplex"));
            }
        }

        [TestMethod]
        public async Task Test_SocketAudio_Error_RemoteEndpointSendsInvalidCodec()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            byte[] junkData = StringUtils.UTF8_WITHOUT_BOM.GetBytes("DurandalNetworkAudio");
            await socketPair.ServerSocket.WriteAsync(junkData, 0, junkData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            byte[] header = StringUtils.UTF8_WITHOUT_BOM.GetBytes("Duplex=2\nCode=pcm\nCodecParams=quantum_entanglement=1\n");
            byte[] payloadLength = new byte[4];
            BinaryHelpers.Int32ToByteArrayLittleEndian(header.Length, payloadLength, 0);
            await socketPair.ServerSocket.WriteAsync(payloadLength, 0, payloadLength.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.WriteAsync(header, 0, header.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.Write);

            try
            {
                using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (var endpoint = await SocketAudioEndpoint.CreateReadOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "Endpoint1",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("Endpoint1"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton))
                {
                    Assert.Fail("Expected a FormatException");
                }
            }
            catch (FormatException e)
            {
                Assert.IsTrue(e.Message.Contains("Remote audio endpoint did not send codec or codec params"));
            }
        }

        [TestMethod]
        public async Task Test_SocketAudio_Error_RemoteEndpointSendsInvalidCodecParams()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            byte[] junkData = StringUtils.UTF8_WITHOUT_BOM.GetBytes("DurandalNetworkAudio");
            await socketPair.ServerSocket.WriteAsync(junkData, 0, junkData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            byte[] header = StringUtils.UTF8_WITHOUT_BOM.GetBytes("Duplex=2\nCodec=pcm\nCodecParmesan\n");
            byte[] payloadLength = new byte[4];
            BinaryHelpers.Int32ToByteArrayLittleEndian(header.Length, payloadLength, 0);
            await socketPair.ServerSocket.WriteAsync(payloadLength, 0, payloadLength.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.WriteAsync(header, 0, header.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.Write);

            try
            {
                using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (var endpoint = await SocketAudioEndpoint.CreateReadOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "Endpoint1",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("Endpoint1"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton))
                {
                    Assert.Fail("Expected a FormatException");
                }
            }
            catch (FormatException e)
            {
                Assert.IsTrue(e.Message.Contains("Remote audio endpoint did not send codec or codec params"));
            }
        }

        [TestMethod]
        public async Task Test_SocketAudio_Error_RemoteEndpointSendsUnsupportedCodec()
        {
            ILogger logger = new DebugLogger("Test", LogLevel.All);
            AudioSampleFormat sampleFormat = AudioSampleFormat.Stereo(48000);
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            byte[] junkData = StringUtils.UTF8_WITHOUT_BOM.GetBytes("DurandalNetworkAudio");
            await socketPair.ServerSocket.WriteAsync(junkData, 0, junkData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            byte[] header = StringUtils.UTF8_WITHOUT_BOM.GetBytes("Duplex=2\nCodec=some_exotic_codec\nCodecParams=quantum_entanglement=1\n");
            byte[] payloadLength = new byte[4];
            BinaryHelpers.Int32ToByteArrayLittleEndian(header.Length, payloadLength, 0);
            await socketPair.ServerSocket.WriteAsync(payloadLength, 0, payloadLength.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.WriteAsync(header, 0, header.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await socketPair.ServerSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.Write);

            try
            {
                using (IAudioGraph sendGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (var endpoint = await SocketAudioEndpoint.CreateReadOnlyEndpoint(
                    codecFactory,
                    new WeakPointer<IAudioGraph>(sendGraph),
                    "Endpoint1",
                    new WeakPointer<ISocket>(socketPair.ClientSocket),
                    logger.Clone("Endpoint1"),
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton))
                {
                    Assert.Fail("Expected a NotSupportedException");
                }
            }
            catch (NotSupportedException e)
            {
                Assert.IsTrue(e.Message.Contains("some_exotic_codec"));
            }
        }
    }
}
