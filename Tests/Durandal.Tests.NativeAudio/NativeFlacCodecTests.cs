using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Parsers;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Tests.Common.Audio;
using Durandal.Extensions.NativeAudio.Codecs;
using Durandal.Common.Audio.Test;
using Durandal.Common.Utils.NativePlatform;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.NativeAudio
{
    [TestClass]
    public class NativeFlacCodecTests
    {
        private static bool _flacLibExists = false;

        [ClassInitialize]
        public static void InitializeTests(TestContext context)
        {
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            _flacLibExists = NativePlatformUtils.PrepareNativeLibrary("FLAC", DebugLogger.Default) == NativeLibraryStatus.Available;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            NativePlatformUtils.SetGlobalResolver(null);
        }

        [TestMethod]
        public async Task TestAudioNativeFlacCodecFullbandStereo()
        {
            if (!_flacLibExists)
            {
                Assert.Inconclusive("Native libFLAC not found");
            }

            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
            INativeFlacCodecProvider nativeFlac = NativeFlacCodecFactory.CreateFlacAdapterForCurrentPlatform(logger);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
                float[] sampleData = new float[48000 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48000, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48000, 0, 1, format.NumChannels, 0.3f, 0.5f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(6544)))
                using (NativeFlacEncoder encoder = new NativeFlacEncoder(
                    nativeFlac,
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("FlacCodec"),
                    complexity: 8))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("flac", encoder.Codec);
                    codecParams = encoder.CodecParams;
                    sampleSource.ConnectOutput(unreliableFilter);
                    unreliableFilter.ConnectOutput(encoder);
                    while (!sampleSource.PlaybackFinished)
                    {
                        await sampleSource.WriteSamplesToOutput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    await encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.DisconnectOutput();
                }

                Assert.AreEqual("samplerate=48000 q=8 channels=2 layout=2", codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (NativeFlacDecoder decoder = new NativeFlacDecoder(nativeFlac, new WeakPointer<IAudioGraph>(graph), codecParams, null, logger))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("flac", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(48000, outputSample.LengthSamplesPerChannel);
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.999f, 0);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeFlacCodecFullbandMono()
        {
            if (!_flacLibExists)
            {
                Assert.Inconclusive("Native libFLAC not found");
            }

            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(63431);
            INativeFlacCodecProvider nativeFlac = NativeFlacCodecFactory.CreateFlacAdapterForCurrentPlatform(logger);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Mono(48000);
                float[] sampleData = new float[48000 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48000, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(9877)))
                using (NativeFlacEncoder encoder = new NativeFlacEncoder(
                    nativeFlac,
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("FlacCodec"),
                    complexity: 8))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("flac", encoder.Codec);
                    codecParams = encoder.CodecParams;
                    sampleSource.ConnectOutput(unreliableFilter);
                    unreliableFilter.ConnectOutput(encoder);
                    while (!sampleSource.PlaybackFinished)
                    {
                        await sampleSource.WriteSamplesToOutput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    await encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.DisconnectOutput();
                }

                Assert.AreEqual("samplerate=48000 q=8 channels=1 layout=1", codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (NativeFlacDecoder decoder = new NativeFlacDecoder(nativeFlac, new WeakPointer<IAudioGraph>(graph), codecParams, null, logger))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("flac", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(48000, outputSample.LengthSamplesPerChannel);
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.999f, 0);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeFlacCodec16KhzMono()
        {
            if (!_flacLibExists)
            {
                Assert.Inconclusive("Native libFLAC not found");
            }

            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(66311);
            INativeFlacCodecProvider nativeFlac = NativeFlacCodecFactory.CreateFlacAdapterForCurrentPlatform(logger);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Mono(16000);
                float[] sampleData = new float[16000 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 16000, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(5675)))
                using (NativeFlacEncoder encoder = new NativeFlacEncoder(
                    nativeFlac,
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("FlacCodec"),
                    complexity: 8))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("flac", encoder.Codec);
                    codecParams = encoder.CodecParams;
                    sampleSource.ConnectOutput(unreliableFilter);
                    unreliableFilter.ConnectOutput(encoder);
                    while (!sampleSource.PlaybackFinished)
                    {
                        await sampleSource.WriteSamplesToOutput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    await encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.DisconnectOutput();
                }

                Assert.AreEqual("samplerate=16000 q=8 channels=1 layout=1", codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (NativeFlacDecoder decoder = new NativeFlacDecoder(nativeFlac, new WeakPointer<IAudioGraph>(graph), codecParams, null, logger))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("flac", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(16000, outputSample.LengthSamplesPerChannel);
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.999f, 0);
                    decoder.DisconnectOutput();
                }
            }
        }

        /// <summary>
        /// Process a long audio stream specifically to test the buffer shift logic inside of the codec
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestAudioNativeFlacCodec48KhzMonoLongSample()
        {
            if (!_flacLibExists)
            {
                Assert.Inconclusive("Native libFLAC not found");
            }

            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(66311);
            INativeFlacCodecProvider nativeFlac = NativeFlacCodecFactory.CreateFlacAdapterForCurrentPlatform(logger);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Mono(48000);
                float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 20];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, sampleData.Length, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(5675)))
                using (NativeFlacEncoder encoder = new NativeFlacEncoder(
                    nativeFlac,
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("FlacCodec"),
                    complexity: 0))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("flac", encoder.Codec);
                    codecParams = encoder.CodecParams;
                    sampleSource.ConnectOutput(unreliableFilter);
                    unreliableFilter.ConnectOutput(encoder);
                    while (!sampleSource.PlaybackFinished)
                    {
                        await sampleSource.WriteSamplesToOutput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    await encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.DisconnectOutput();
                }

                Assert.AreEqual("samplerate=48000 q=0 channels=1 layout=1", codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (NativeFlacDecoder decoder = new NativeFlacDecoder(nativeFlac, new WeakPointer<IAudioGraph>(graph), codecParams, null, logger))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("flac", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(960000, outputSample.LengthSamplesPerChannel);
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.999f, 0);
                    decoder.DisconnectOutput();
                }
            }
        }
    }
}
