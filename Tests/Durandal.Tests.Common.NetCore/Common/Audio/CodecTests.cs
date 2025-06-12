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
using Durandal.Common.Audio.Test;
using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Codecs.ADPCM;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    public class CodecTests
    {
        [TestMethod]
        public async Task TestAudioPcmS16Codec()
        {
            await TestCodec(new RawPcmCodecFactory(), "pcm").ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestAudioPcmS24Codec()
        {
            await TestCodec(new RawPcmCodecFactory(), "pcm_s24le").ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestAudioPcmS32Codec()
        {
            await TestCodec(new RawPcmCodecFactory(), "pcm_s32le").ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestAudioPcmF32Codec()
        {
            await TestCodec(new RawPcmCodecFactory(), "pcm_f32le").ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestAudioAlawCodec()
        {
            await TestCodec(new ALawCodecFactory(), "alaw", 0.99f).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestAudioUlawCodec()
        {
            await TestCodec(new ULawCodecFactory(), "ulaw", 0.99f).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestAudioSquareDeltaCodec()
        {
            await TestCodec(new SquareDeltaCodecFactory(), "sqrt").ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestAudioADPCMCodec()
        {
            await TestCodec(new AdpcmCodecFactory(), "adpcm_ima", 0.99f, 400).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestAudioSquareDeltaCodecLegacyCodecParams()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream waveFile = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Mono(44100);
                float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 995, 44100, 0, 0, format.NumChannels, 0.2f, 0.2f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 71, 44100, 0, 0, format.NumChannels, 0.2f, 0.6f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 1539, 44100, 0, 0, format.NumChannels, 0.4f, 0.8f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(4455)))
                using (SquareDeltaEncoder encoder = new SquareDeltaEncoder(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    await encoder.Initialize(waveFile, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.ConnectOutput(unreliableFilter);
                    unreliableFilter.ConnectOutput(encoder);
                    while (!sampleSource.PlaybackFinished)
                    {
                        await sampleSource.WriteSamplesToOutput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    await encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.DisconnectOutput();
                }

                string codecParams = "samplerate=44100";
                waveFile.Seek(0, SeekOrigin.Begin);
                using (SquareDeltaDecoder decoder = new SquareDeltaDecoder(new WeakPointer<IAudioGraph>(graph), codecParams, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(waveFile, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.99f);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioSquareDeltaCodecNullCodecParams()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream waveFile = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Mono(44100);
                float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 995, 44100, 0, 0, format.NumChannels, 0.2f, 0.2f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 71, 44100, 0, 0, format.NumChannels, 0.2f, 0.6f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 1539, 44100, 0, 0, format.NumChannels, 0.4f, 0.8f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(233)))
                using (SquareDeltaEncoder encoder = new SquareDeltaEncoder(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    await encoder.Initialize(waveFile, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.ConnectOutput(unreliableFilter);
                    unreliableFilter.ConnectOutput(encoder);
                    while (!sampleSource.PlaybackFinished)
                    {
                        await sampleSource.WriteSamplesToOutput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    await encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.DisconnectOutput();
                }

                string codecParams = null;
                waveFile.Seek(0, SeekOrigin.Begin);
                using (SquareDeltaDecoder decoder = new SquareDeltaDecoder(new WeakPointer<IAudioGraph>(graph), codecParams, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(waveFile, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.99f);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioRawTargetToRawSource()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream rawFile = new MemoryStream())
            {
                AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
                float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.7f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.7f, 0.5f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(112312)))
                using (RawPcmEncoder streamTarget = new RawPcmEncoder(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    await streamTarget.Initialize(rawFile, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.ConnectOutput(unreliableFilter);
                    unreliableFilter.ConnectOutput(streamTarget);
                    while (!sampleSource.PlaybackFinished)
                    {
                        await sampleSource.WriteSamplesToOutput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    sampleSource.DisconnectOutput();
                }

                rawFile.Seek(0, SeekOrigin.Begin);
                using (RawPcmDecoder streamSource = new RawPcmDecoder(new WeakPointer<IAudioGraph>(graph), format, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    await streamSource.Initialize(rawFile, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    streamSource.ConnectOutput(sampleTarget);
                    while (!streamSource.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.99f);
                    streamSource.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioOpusCodecFullbandStereo()
        {
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
        public async Task TestAudioOpusCodecFullbandStereoDegradeTo16KhzMono()
        {
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
                    Assert.AreEqual(decoder.OutputFormat, outputFormat);

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
        public async Task TestAudioOpusCodecFullbandMono()
        {
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
        public async Task TestAudioOpusCodec16KhzMono()
        {
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

        /// <summary>
        /// Process a long audio stream specifically to test the buffer shift logic inside of the codec
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestAudioOpusCodec48KhzMonoLongSample()
        {
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

        //[TestMethod]
        //public async Task TestAudioOpusCodecLegacyCompatibility()
        //{
        //    ILogger logger = new ConsoleLogger();
        //    IRandom rand = new FastRandom(2767);

        //    AudioSampleFormat format = AudioSampleFormat.Mono(16000);
        //    float[] sampleData = new float[16000];
        //    AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 16000, 0, 0, 1, 0.3f, 0.0f);
        //    AudioSample expectedOutputSample = new AudioSample(sampleData, format);
        //    short[] convertedSampleData = new short[sampleData.Length];
        //    AudioMath.ConvertSamples_FloatToInt16(sampleData, 0, convertedSampleData, 0, sampleData.Length);
        //    Durandal.Common.Audio.AudioChunk inputSample = new Durandal.Common.Audio.AudioChunk(convertedSampleData, format.SampleRateHz);
        //    string codecParams;

        //    Durandal.Common.Audio.Codecs.OpusAudioCodec legacyCodec = new Durandal.Common.Audio.Codecs.OpusAudioCodec(logger.Clone("OpusEncoder"), 10, 400);
        //    byte[] encodedLegacyData = Durandal.Common.Audio.AudioUtils.CompressAudioUsingStream(inputSample, legacyCodec.CreateCompressionStream(16000), out codecParams);

        //    IAudioGraph graph = new ConcurrentAudioGraph();
        //    using (MemoryStream encodedData = new MemoryStream(encodedLegacyData))
        //    {
        //        using (OpusRawDecoder decoder = new OpusRawDecoder(new WeakPointer<IAudioGraph>(graph), format, new NonRealTimeStreamWrapper(encodedData), false))
        //        using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format))
        //        {
        //            Assert.AreEqual("opus", decoder.Codec);
        //            Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(CancellationToken.None, DefaultRealTimeProvider.Singleton));

        //            decoder.ConnectOutput(sampleTarget);
        //            while (!decoder.PlaybackFinished)
        //            {
        //                await sampleTarget.ReadSamplesFromInput(rand.NextInt(1, 200) * rand.NextInt(1, 200), CancellationToken.None, DefaultRealTimeProvider.Singleton);
        //            }

        //            AudioSample outputSample = sampleTarget.GetAllAudio();
        //            Assert.AreEqual(16000, outputSample.LengthSamplesPerChannel);

        //            int lookahead = 104; // samples per channel delay introduced by opus codec in this configuration
        //            ArraySegment<float> trimmedOutputData = new ArraySegment<float>(
        //                outputSample.Data.Array,
        //                outputSample.Data.Offset + (lookahead * outputSample.Format.NumChannels),
        //                outputSample.Data.Count - (lookahead * outputSample.Format.NumChannels));
        //            AudioSample trimmedSample = new AudioSample(trimmedOutputData, outputSample.Format);

        //            AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, trimmedSample, 0.95f, 110);
        //            decoder.DisconnectOutput();
        //        }
        //    }
        //}


        [TestMethod]
        public async Task TestAudioOpusCodecRealtimeBudget()
        {
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

        [TestMethod]
        public async Task TestAudioOggOpusCodec16KMonoEvenLength()
        {
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
        public async Task TestAudioOggOpusCodec16KMonoOffLength()
        {
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
        public async Task TestAudioOggOpusCodec16KStereoEvenLength()
        {
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
        public async Task TestAudioOggOpusCodecFullbandMonoEvenLength()
        {
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
        public async Task TestAudioOggOpusCodecFullbandStereoEvenLength()
        {
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
        public async Task TestAudioOggOpusCodecFullbandSurroundQuad()
        {
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = new AudioSampleFormat(48000, MultiChannelMapping.Quadraphonic);
                float[] sampleData = new float[48007 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 1, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 2, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 3, format.NumChannels, 0.3f, 0.5f);
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

        [TestMethod]
        public async Task TestAudioOggOpusCodecFullbandSurround50()
        {
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = new AudioSampleFormat(48000, MultiChannelMapping.Surround_5ch_Vorbis_Layout);
                float[] sampleData = new float[48007 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 1, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 2, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 3, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 4, format.NumChannels, 0.3f, 0.5f);
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

        [TestMethod]
        public async Task TestAudioOggOpusCodecFullbandSurround51()
        {
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

        [TestMethod]
        public async Task TestAudioOggOpusCodecFullbandSurround71()
        {
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(412553);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedData = new MemoryStream())
            {
                AudioSampleFormat format = new AudioSampleFormat(48000, MultiChannelMapping.Surround_7_1ch_Vorbis_Layout);
                float[] sampleData = new float[48007 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 0, format.NumChannels, 0.3f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 1, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 2, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 3, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 4, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 5, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48007, 0, 6, format.NumChannels, 0.3f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 100, 48007, 0, 7, format.NumChannels, 0.3f, 0.5f);
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

        [TestMethod]
        [DataRow(11000, MultiChannelMapping.Monaural)]
        [DataRow(16000, MultiChannelMapping.Monaural)]
        [DataRow(24000, MultiChannelMapping.Monaural)]
        [DataRow(32000, MultiChannelMapping.Monaural)]
        [DataRow(44100, MultiChannelMapping.Monaural)]
        [DataRow(48000, MultiChannelMapping.Monaural)]
        [DataRow(11000, MultiChannelMapping.Stereo_L_R)]
        [DataRow(16000, MultiChannelMapping.Stereo_L_R)]
        [DataRow(24000, MultiChannelMapping.Stereo_L_R)]
        [DataRow(32000, MultiChannelMapping.Stereo_L_R)]
        [DataRow(44100, MultiChannelMapping.Stereo_L_R)]
        [DataRow(48000, MultiChannelMapping.Stereo_L_R)]
        [DataRow(44100, MultiChannelMapping.Quadraphonic)]
        [DataRow(48000, MultiChannelMapping.Quadraphonic)]
        [DataRow(44100, MultiChannelMapping.Surround_5_1ch)]
        [DataRow(48000, MultiChannelMapping.Surround_5_1ch)]
        [DataRow(44100, MultiChannelMapping.Surround_7_1ch)]
        [DataRow(48000, MultiChannelMapping.Surround_7_1ch)]
        public async Task TestAudioAdpcmCodecDifferentFormats(int sampleRate, MultiChannelMapping channelMapping)
        {
            IAudioCodecFactory codecFactory = new AdpcmCodecFactory();
            ILogger logger = new ConsoleLogger();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedDataOut = new MemoryStream())
            {
                IRandom rand = new FastRandom(9814034);
                AudioSampleFormat format = new AudioSampleFormat(sampleRate, channelMapping);
                float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
                for (int ch = 0; ch < format.NumChannels; ch++)
                {
                    AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, rand.NextInt(100, 2000), format.SampleRateHz, 0, ch, format.NumChannels, 0.2f, 0.0f);
                    AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, rand.NextInt(100, 2000), format.SampleRateHz, 0, ch, format.NumChannels, 0.2f, 0.2f);
                    AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, rand.NextInt(100, 2000), format.SampleRateHz, 0, ch, format.NumChannels, 0.2f, 0.6f);
                    AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, rand.NextInt(100, 2000), format.SampleRateHz, 0, ch, format.NumChannels, 0.4f, 0.8f);
                }

                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(76544)))
                using (AudioEncoder encoder = codecFactory.CreateEncoder("adpcm_ima", new WeakPointer<IAudioGraph>(graph), format, logger, null))
                {
                    await encoder.Initialize(encodedDataOut, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual("adpcm_ima", encoder.Codec);
                    codecParams = encoder.CodecParams;
                    sampleSource.ConnectOutput(unreliableFilter);
                    unreliableFilter.ConnectOutput(encoder);
                    while (!sampleSource.PlaybackFinished)
                    {
                        await sampleSource.WriteSamplesToOutput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    await encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.DisconnectOutput();
                }

                encodedDataOut.Seek(0, SeekOrigin.Begin);
                using (AudioDecoder decoder = codecFactory.CreateDecoder("adpcm_ima", codecParams, new WeakPointer<IAudioGraph>(graph), logger, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual("adpcm_ima", decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedDataOut, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(77, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    int allowedLengthDiff = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, TimeSpan.FromMilliseconds(10));
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.975f, allowedLengthDiff);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        public void TestAudioAdpcmCodecMax8Channels()
        {
            try
            {
                ILogger logger = new ConsoleLogger();
                using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (AudioEncoder encoder = new AdpcmCodecFactory().CreateEncoder("adpcm_ima", new WeakPointer<IAudioGraph>(graph), new AudioSampleFormat(48000, MultiChannelMapping.Packed_9Ch), logger, null))
                {
                    Assert.Fail("Should have thrown a NotSupportedException");
                }
            }
            catch (NotSupportedException) { }
        }

        private static async Task TestCodec(IAudioCodecFactory codecFactory, string codecName, float allowedSampleDiff = 0.999f, int allowedLengthDiff = 0)
        {
            ILogger logger = new ConsoleLogger();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream encodedDataOut = new MemoryStream())
            {
                int streamPadding = 100;
                byte[] junk = new byte[streamPadding];
                new FastRandom().NextBytes(junk);
                encodedDataOut.Write(junk, 0, streamPadding);
                AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
                float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 485, 44100, 0, 1, format.NumChannels, 0.2f, 0.5f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 995, 44100, 0, 0, format.NumChannels, 0.2f, 0.2f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 857, 44100, 0, 1, format.NumChannels, 0.2f, 0.9f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 71, 44100, 0, 0, format.NumChannels, 0.2f, 0.6f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 74, 44100, 0, 1, format.NumChannels, 0.2f, 0.4f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 1539, 44100, 0, 0, format.NumChannels, 0.4f, 0.8f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 1443, 44100, 0, 1, format.NumChannels, 0.4f, 0.1f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                string codecParams;
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(76544)))
                using (AudioEncoder encoder = codecFactory.CreateEncoder(codecName, new WeakPointer<IAudioGraph>(graph), format, logger, null))
                {
                    await encoder.Initialize(encodedDataOut, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual(codecName, encoder.Codec);
                    codecParams = encoder.CodecParams;
                    sampleSource.ConnectOutput(unreliableFilter);
                    unreliableFilter.ConnectOutput(encoder);
                    while (!sampleSource.PlaybackFinished)
                    {
                        await sampleSource.WriteSamplesToOutput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    await encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.DisconnectOutput();
                }

                encodedDataOut.Seek(streamPadding, SeekOrigin.Begin);
                using (AudioDecoder decoder = codecFactory.CreateDecoder(codecName, codecParams, new WeakPointer<IAudioGraph>(graph), logger, null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual(codecName, decoder.Codec);
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(encodedDataOut, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, allowedSampleDiff, allowedLengthDiff);
                    decoder.DisconnectOutput();
                }
            }
        }

        //using (FileStream debugOutStream = new FileStream(@"C:\code\Durandal\Data\Test2.wav", FileMode.Create, FileAccess.Write))
        //using (FixedAudioSampleSource debugSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), trimmedSample))
        //using (RiffWaveEncoder debugWaveEncoder = await RiffWaveEncoder.Create(new WeakPointer<IAudioGraph>(graph), debugOutStream, format, false))
        //{
        //    debugSampleSource.ConnectOutput(debugWaveEncoder);
        //    while (!debugSampleSource.PlaybackFinished)
        //    {
        //        await debugSampleSource.WriteSamplesToOutput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
        //    }

        //    await debugWaveEncoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
        //    debugSampleSource.DisconnectOutput();
        //}
    }
}
