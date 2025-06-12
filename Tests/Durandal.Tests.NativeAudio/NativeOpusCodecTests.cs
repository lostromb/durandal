using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Test;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Parsers;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using Durandal.Extensions.NativeAudio.Codecs;
using Durandal.Tests.Common.Audio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.NativeAudio
{
    [TestClass]
    public class NativeOpusCodecTests
    {
        private static bool _opusLibExists = false;

        [ClassInitialize]
        public static void InitializeTests(TestContext context)
        {
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            _opusLibExists = new NativeOpusAccelerator().Apply(DebugLogger.Default);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            NativePlatformUtils.SetGlobalResolver(null);
        }

        [TestMethod]
        public void TestAudioNativeOpusGetVersionString()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            string versionString = OpusCodecFactory.Provider.GetVersionString();
            Assert.IsFalse(string.IsNullOrWhiteSpace(versionString));
            Assert.IsTrue(versionString.Contains("libopus"));
        }

        [TestMethod]
        public async Task TestAudioNativeOpusCodecFullbandStereo()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
                float[] sampleData = new float[48007 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 1, format.NumChannels, 0.3f, 0.5f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(6544)))
                using (OpusRawEncoder encoder = new OpusRawEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("OpusCodec"),
                    10,
                    400,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusMode.MODE_CELT_ONLY,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusFramesize.OPUS_FRAMESIZE_5_MS))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("opus", encoder.Codec);
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

                Assert.AreEqual("samplerate=48000 q=10 framesize=5 channels=2 layout=2", codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (OpusRawDecoder decoder = new OpusRawDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, codecParams, maxSupportedOutputFormat: null, realTimeDecodingBudget: null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("opus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(48240, outputSample.LengthSamplesPerChannel);

                    int lookahead = 312; // samples per channel delay introduced by opus codec in this configuration
                    ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
                        outputSample.Data.Array,
                        outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
                        outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
                    AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, trimmedSample, 0.95f, 240);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeOpusCodecFullbandStereoDegradeTo16KhzMono()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat inputFormat = AudioSampleFormat.Stereo(48000);
                AudioSampleFormat outputFormat = AudioSampleFormat.Mono(16000);
                float[] sampleData = new float[48007 * inputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 440, 48007, 0, 0, inputFormat.NumChannels, 0.3f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 440, 48007, 0, 1, inputFormat.NumChannels, 0.3f, 0.0f);
                AudioSample inputSample = new AudioSample(sampleData, inputFormat);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), inputFormat, new FastRandom(6544)))
                using (OpusRawEncoder encoder = new OpusRawEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    inputFormat,
                    null,
                    logger.Clone("OpusCodec"),
                    10,
                    400,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusMode.MODE_CELT_ONLY,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusFramesize.OPUS_FRAMESIZE_5_MS))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("opus", encoder.Codec);
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

                Assert.AreEqual("samplerate=48000 q=10 framesize=5 channels=2 layout=2", codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (OpusRawDecoder decoder = new OpusRawDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, codecParams, maxSupportedOutputFormat: outputFormat, realTimeDecodingBudget: null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), outputFormat, null))
                {
                    Assert.AreEqual("opus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(16080, outputSample.LengthSamplesPerChannel);

                    int lookahead = 104; // samples per channel delay introduced by opus codec in this configuration
                    ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
                        outputSample.Data.Array,
                        outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
                        outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
                    AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

                    float[] expectedOutputSampleData = new float[16007 * outputFormat.NumChannels];
                    AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, outputFormat.SampleRateHz, 440, 16007, 0, 0, outputFormat.NumChannels, 0.3f, 0.0f);
                    AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);

                    AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, trimmedSample, 0.95f, 80);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeOpusCodecFullbandMono()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(63431);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Mono(48000);
                float[] sampleData = new float[48007 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(9877)))
                using (OpusRawEncoder encoder = new OpusRawEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("OpusCodec"),
                    10,
                    400,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusMode.MODE_CELT_ONLY,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusFramesize.OPUS_FRAMESIZE_5_MS))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("opus", encoder.Codec);
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

                Assert.AreEqual("samplerate=48000 q=10 framesize=5 channels=1 layout=1", codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (OpusRawDecoder decoder = new OpusRawDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, codecParams, maxSupportedOutputFormat: null, realTimeDecodingBudget: null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("opus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(48240, outputSample.LengthSamplesPerChannel);

                    int lookahead = 312; // samples per channel delay introduced by opus codec in this configuration
                    ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
                        outputSample.Data.Array,
                        outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
                        outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
                    AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, trimmedSample, 0.95f, 240);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeOpusCodec16KhzMono()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(66311);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Mono(16000);
                float[] sampleData = new float[16007 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 16007, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(5675)))
                using (OpusRawEncoder encoder = new OpusRawEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("OpusCodec"),
                    10,
                    400,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusMode.MODE_CELT_ONLY,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusFramesize.OPUS_FRAMESIZE_5_MS))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("opus", encoder.Codec);
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

                Assert.AreEqual("samplerate=16000 q=10 framesize=5 channels=1 layout=1", codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (OpusRawDecoder decoder = new OpusRawDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, codecParams, maxSupportedOutputFormat: null, realTimeDecodingBudget: null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("opus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(16080, outputSample.LengthSamplesPerChannel);

                    int lookahead = 104; // samples per channel delay introduced by opus codec in this configuration
                    ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
                        outputSample.Data.Array,
                        outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
                        outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
                    AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, trimmedSample, 0.95f, 80);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeOpusCodecRealtimeBudget()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(53775);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
                float[] sampleData = new float[48007 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 1, format.NumChannels, 0.3f, 0.5f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(6544)))
                using (OpusRawEncoder encoder = new OpusRawEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("OpusCodec"),
                    10,
                    400,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusMode.MODE_CELT_ONLY,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusFramesize.OPUS_FRAMESIZE_5_MS))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("opus", encoder.Codec);
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

                Assert.AreEqual("samplerate=48000 q=10 framesize=5 channels=2 layout=2", codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                // with a zero-length time budget, we should only decode max one packet at a time
                using (OpusRawDecoder decoder = new OpusRawDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, codecParams, maxSupportedOutputFormat: null, realTimeDecodingBudget: TimeSpan.Zero))
                using (NullAudioSampleTarget sampleTarget = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("opus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        int samplesRead = await sampleTarget.ReadSamplesFromInput(48000, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                        Assert.IsTrue(samplesRead == 240);
                    }

                    decoder.DisconnectOutput();
                }
            }
        }

        /// <summary>
        /// Process a long audio stream specifically to test the buffer shift logic inside of the codec
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestAudioNativeOpusCodec48KhzMonoLongSample()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(66311);
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
                using (OpusRawEncoder encoder = new OpusRawEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("OpusCodec"),
                    0,
                    400,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusMode.MODE_CELT_ONLY,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusFramesize.OPUS_FRAMESIZE_5_MS))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("opus", encoder.Codec);
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

                Assert.AreEqual("samplerate=48000 q=0 framesize=5 channels=1 layout=1", codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (OpusRawDecoder decoder = new OpusRawDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, codecParams, maxSupportedOutputFormat: null, realTimeDecodingBudget: null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("opus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(960000, outputSample.LengthSamplesPerChannel);

                    int lookahead = 104; // samples per channel delay introduced by opus codec in this configuration
                    ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
                        outputSample.Data.Array,
                        outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
                        outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
                    AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

                    AudioTestHelpers.AssertAudioSignalIsContinuous(trimmedSample, maxDelta: 0.05f);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeOggOpusCodec16KMonoEvenLength()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Mono(16000);
                float[] sampleData = new float[16000];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 16000, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(6544)))
                using (OggOpusEncoder encoder = new OggOpusEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("OpusCodec"),
                    complexity: 10))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("oggopus", encoder.Codec);
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

                Assert.AreEqual(string.Empty, codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (OggOpusDecoder decoder = new OggOpusDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("oggopus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(16000, outputSample.LengthSamplesPerChannel);

                    int lookahead = 104; // samples per channel delay introduced by opus codec in this configuration
                    ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
                        outputSample.Data.Array,
                        outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
                        outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
                    AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, trimmedSample, 0.95f, lookahead);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeOggOpusCodec16KMonoOffLength()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Mono(16000);
                float[] sampleData = new float[16177];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 16177, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(6544)))
                using (OggOpusEncoder encoder = new OggOpusEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("OpusCodec"),
                    complexity: 10))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("oggopus", encoder.Codec);
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

                Assert.AreEqual(string.Empty, codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (OggOpusDecoder decoder = new OggOpusDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("oggopus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(16320, outputSample.LengthSamplesPerChannel);

                    int lookahead = 104; // samples per channel delay introduced by opus codec in this configuration
                    ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
                        outputSample.Data.Array,
                        outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
                        outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
                    AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, trimmedSample, 0.95f, 320);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeOggOpusCodec16KStereoEvenLength()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Stereo(16000);
                float[] sampleData = new float[16000 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 16000, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 16000, 0, 1, format.NumChannels, 0.3f, 0.5f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(6544)))
                using (OggOpusEncoder encoder = new OggOpusEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("OpusCodec"),
                    complexity: 10))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("oggopus", encoder.Codec);
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

                Assert.AreEqual(string.Empty, codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (OggOpusDecoder decoder = new OggOpusDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("oggopus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(16000, outputSample.LengthSamplesPerChannel);

                    int lookahead = 104; // samples per channel delay introduced by opus codec in this configuration
                    ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
                        outputSample.Data.Array,
                        outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
                        outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
                    AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, trimmedSample, 0.95f, lookahead);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeOggOpusCodecFullbandMonoEvenLength()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Mono(48000);
                float[] sampleData = new float[48000];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48000, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(6544)))
                using (OggOpusEncoder encoder = new OggOpusEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("OpusCodec"),
                    complexity: 10))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("oggopus", encoder.Codec);
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

                Assert.AreEqual(string.Empty, codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (OggOpusDecoder decoder = new OggOpusDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("oggopus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(48000, outputSample.LengthSamplesPerChannel);

                    int lookahead = 312; // samples per channel delay introduced by opus codec in this configuration
                    ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
                        outputSample.Data.Array,
                        outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
                        outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
                    AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, trimmedSample, 0.95f, lookahead);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeOggOpusCodecFullbandStereoEvenLength()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
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
                using (OggOpusEncoder encoder = new OggOpusEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("OpusCodec"),
                    complexity: 10))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("oggopus", encoder.Codec);
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

                Assert.AreEqual(string.Empty, codecParams);
                encodedData.Seek(0, SeekOrigin.Begin);
                using (OggOpusDecoder decoder = new OggOpusDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("oggopus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(48000, outputSample.LengthSamplesPerChannel);

                    int lookahead = 312; // samples per channel delay introduced by opus codec in this configuration
                    ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
                        outputSample.Data.Array,
                        outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
                        outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
                    AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, trimmedSample, 0.95f, lookahead);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioNativeOggOpusCodecFullbandSurround51()
        {
            if (!_opusLibExists)
            {
                Assert.Inconclusive("Native libopus not found");
            }

            Assert.IsInstanceOfType(OpusCodecFactory.Provider, typeof(NativeOpusCodecFactory));
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = new AudioSampleFormat(48000, MultiChannelMapping.Surround_5_1ch_Vorbis_Layout);
                float[] sampleData = new float[48007 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 1, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 2, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 3, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 4, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 100, 48007, 0, 5, format.NumChannels, 0.3f, 0.5f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(6544)))
                using (OggOpusEncoder encoder = new OggOpusEncoder(
                    new WeakPointer<IAudioGraph>(graph),
                    format,
                    null,
                    logger.Clone("OpusCodec"),
                    10,
                    400,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusMode.MODE_AUTO,
                    Durandal.Common.Audio.Codecs.Opus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO))
                {
                    await encoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("oggopus", encoder.Codec);
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

                encodedData.Seek(0, SeekOrigin.Begin);
                using (OggOpusDecoder decoder = new OggOpusDecoder(new WeakPointer<IAudioGraph>(graph), null, logger, realTimeDecodingBudget: null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("oggopus", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedData, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    Assert.AreEqual(48960, outputSample.LengthSamplesPerChannel);

                    int lookahead = 312; // samples per channel delay introduced by opus codec in this configuration
                    ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
                        outputSample.Data.Array,
                        outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
                        outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
                    AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, trimmedSample, 0.95f, 960);
                    decoder.DisconnectOutput();
                }
            }
        }
    }
}
