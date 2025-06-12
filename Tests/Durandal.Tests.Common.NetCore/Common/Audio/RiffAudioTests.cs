using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Test;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    [DeploymentItem("TestData/sample_ADPCM.wav")]
    [DeploymentItem("TestData/sample_ALAW.wav")]
    [DeploymentItem("TestData/sample_F32.wav")]
    [DeploymentItem("TestData/sample_IMA_ADPCM.wav")]
    [DeploymentItem("TestData/sample_RAW.raw")]
    [DeploymentItem("TestData/sample_S16.wav")]
    [DeploymentItem("TestData/sample_S24.wav")]
    [DeploymentItem("TestData/sample_S32.wav")]
    [DeploymentItem("TestData/sample_ULAW.wav")]
    public class RiffAudioTests
    {
        private static AudioSample ExpectedAudioSample;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            byte[] rawFile = System.IO.File.ReadAllBytes(@"sample_RAW.raw");
            float[] rawSamples = new float[rawFile.Length / 2];
            AudioMath.ConvertSamples_2BytesIntLittleEndianToFloat(rawFile, 0, rawSamples, 0, rawSamples.Length);
            ExpectedAudioSample = new AudioSample(rawSamples, AudioSampleFormat.Mono(16000));
        }

        [TestMethod]
        [DataRow(RiffWaveFormat.PCM_S16LE, "riff-pcm")]
        [DataRow(RiffWaveFormat.PCM_S24LE, "riff-pcm_s24le")]
        [DataRow(RiffWaveFormat.PCM_S32LE, "riff-pcm_s32le")]
        [DataRow(RiffWaveFormat.PCM_F32LE, "riff-pcm_f32le")]
        [DataRow(RiffWaveFormat.ALAW, "riff-alaw")]
        [DataRow(RiffWaveFormat.ULAW, "riff-ulaw")]
        [DataRow(RiffWaveFormat.ADPCM_IMA, "riff-adpcm_ima")]
        public async Task TestRiffAudio_WaveTargetToWaveSource(RiffWaveFormat waveFormat, string expectedCodecName)
        {
            ILogger logger = new ConsoleLogger();
            int paddingAtFileStart = 100;
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream waveFile = new MemoryStream())
            {
                byte[] padding = new byte[paddingAtFileStart];
                waveFile.Write(padding, 0, paddingAtFileStart);
                AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
                float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.7f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.7f, 0.5f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(422)))
                using (RiffWaveEncoder encoder = new RiffWaveEncoder(new WeakPointer<IAudioGraph>(graph), format, null, logger, waveFormat))
                {
                    await encoder.Initialize(waveFile, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual(expectedCodecName, encoder.Codec);
                    sampleSource.ConnectOutput(unreliableFilter);
                    unreliableFilter.ConnectOutput(encoder);
                    while (!sampleSource.PlaybackFinished)
                    {
                        await sampleSource.WriteSamplesToOutput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    await encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    sampleSource.DisconnectOutput();
                }

                waveFile.Seek(paddingAtFileStart, SeekOrigin.Begin);

                using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(waveFile, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                    decoder.ConnectOutput(sampleTarget);
                    while (!decoder.PlaybackFinished)
                    {
                        await sampleTarget.ReadSamplesFromInput(441, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    int allowedLengthDiff = waveFormat == RiffWaveFormat.ADPCM_IMA ? 400 : 0;

                    AudioSample outputSample = sampleTarget.GetAllAudio();

                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample, 0.99f, allowedLengthDiff);
                    decoder.DisconnectOutput();
                }
            }
        }

        [TestMethod]
        [DataRow(RiffWaveFormat.PCM_S16LE, "riff-pcm")]
        [DataRow(RiffWaveFormat.PCM_S24LE, "riff-pcm_s24le")]
        [DataRow(RiffWaveFormat.PCM_S32LE, "riff-pcm_s32le")]
        [DataRow(RiffWaveFormat.PCM_F32LE, "riff-pcm_f32le")]
        [DataRow(RiffWaveFormat.ALAW, "riff-alaw")]
        [DataRow(RiffWaveFormat.ULAW, "riff-ulaw")]
        public async Task TestRiffAudio_WaveTargetToWaveSourceSurroundSound(RiffWaveFormat sampleFormat, string expectedCodecName)
        {
            ILogger logger = new ConsoleLogger();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (MemoryStream waveFile = new MemoryStream())
            {
                AudioSampleFormat format = new AudioSampleFormat(48000, MultiChannelMapping.Surround_5_1ch);
                float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, 48000, 1000, 48000, 0, 0, format.NumChannels, 0.7f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 48000, 2000, 48000, 0, 1, format.NumChannels, 0.7f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 48000, 3000, 48000, 0, 2, format.NumChannels, 0.7f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 48000, 4000, 48000, 0, 3, format.NumChannels, 0.7f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 48000, 5000, 48000, 0, 4, format.NumChannels, 0.7f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 48000, 6000, 48000, 0, 5, format.NumChannels, 0.7f, 0.0f);
                AudioSample inputSample = new AudioSample(sampleData, format);
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
                using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(422)))
                using (RiffWaveEncoder encoder = new RiffWaveEncoder(new WeakPointer<IAudioGraph>(graph), format, null, logger, sampleFormat))
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

                waveFile.Seek(0, SeekOrigin.Begin);
                using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), null))
                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(waveFile, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                    Assert.AreEqual(format, decoder.OutputFormat);
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
        public async Task TestRiffAudio_DecodeWav_Int16PCM()
        {
            Assert.IsNotNull(ExpectedAudioSample);
            ILogger logger = new ConsoleLogger();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream fileIn = new FileStream(@"sample_S16.wav", FileMode.Open, FileAccess.Read))
            using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), null))
            {
                Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(fileIn, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                Assert.AreEqual(AudioSampleFormat.Mono(16000), decoder.OutputFormat);

                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, null))
                {
                    decoder.ConnectOutput(sampleTarget);
                    await sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(ExpectedAudioSample, outputSample, 0.999f);
                }
            }
        }

        [TestMethod]
        public async Task TestRiffAudio_DecodeWav_Int24PCM()
        {
            Assert.IsNotNull(ExpectedAudioSample);
            ILogger logger = new ConsoleLogger();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream fileIn = new FileStream(@"sample_S24.wav", FileMode.Open, FileAccess.Read))
            using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), null))
            {
                Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(fileIn, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                Assert.AreEqual(AudioSampleFormat.Mono(16000), decoder.OutputFormat);

                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, null))
                {
                    decoder.ConnectOutput(sampleTarget);
                    await sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(ExpectedAudioSample, outputSample, 0.999f);
                }
            }
        }

        [TestMethod]
        public async Task TestRiffAudio_DecodeWav_Int32PCM()
        {
            Assert.IsNotNull(ExpectedAudioSample);
            ILogger logger = new ConsoleLogger();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream fileIn = new FileStream(@"sample_S32.wav", FileMode.Open, FileAccess.Read))
            using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), null))
            {
                Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(fileIn, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                Assert.AreEqual(AudioSampleFormat.Mono(16000), decoder.OutputFormat);

                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, null))
                {
                    decoder.ConnectOutput(sampleTarget);
                    await sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(ExpectedAudioSample, outputSample, 0.999f);
                }
            }
        }

        [TestMethod]
        public async Task TestRiffAudio_DecodeWav_Float32PCM()
        {
            Assert.IsNotNull(ExpectedAudioSample);
            ILogger logger = new ConsoleLogger();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream fileIn = new FileStream(@"sample_F32.wav", FileMode.Open, FileAccess.Read))
            using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), null))
            {
                Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(fileIn, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                Assert.AreEqual(AudioSampleFormat.Mono(16000), decoder.OutputFormat);

                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, null))
                {
                    decoder.ConnectOutput(sampleTarget);
                    await sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(ExpectedAudioSample, outputSample, 0.999f);
                }
            }
        }

        [TestMethod]
        public async Task TestRiffAudio_DecodeWav_ADPCM()
        {
            Assert.IsNotNull(ExpectedAudioSample);
            ILogger logger = new ConsoleLogger();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream fileIn = new FileStream(@"sample_ADPCM.wav", FileMode.Open, FileAccess.Read))
            using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), null))
            {
                Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(fileIn, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                Assert.AreEqual(AudioSampleFormat.Mono(16000), decoder.OutputFormat);

                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, null))
                {
                    decoder.ConnectOutput(sampleTarget);
                    await sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(ExpectedAudioSample, outputSample, 0.999f);
                }
            }
        }

        [TestMethod]
        public async Task TestRiffAudio_DecodeWav_IMA_ADPCM()
        {
            Assert.IsNotNull(ExpectedAudioSample);
            ILogger logger = new ConsoleLogger();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream fileIn = new FileStream(@"sample_IMA_ADPCM.wav", FileMode.Open, FileAccess.Read))
            using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), null))
            {
                Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(fileIn, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                Assert.AreEqual(AudioSampleFormat.Mono(16000), decoder.OutputFormat);

                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, null))
                {
                    decoder.ConnectOutput(sampleTarget);
                    await sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(ExpectedAudioSample, outputSample, 0.99f, 256);
                }
            }
        }

        [TestMethod]
        public async Task TestRiffAudio_DecodeWav_ALaw()
        {
            Assert.IsNotNull(ExpectedAudioSample);
            ILogger logger = new ConsoleLogger();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream fileIn = new FileStream(@"sample_ALAW.wav", FileMode.Open, FileAccess.Read))
            using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), null))
            {
                Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(fileIn, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                Assert.AreEqual(AudioSampleFormat.Mono(16000), decoder.OutputFormat);

                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, null))
                {
                    decoder.ConnectOutput(sampleTarget);
                    await sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(ExpectedAudioSample, outputSample, 0.99f);
                }
            }
        }

        [TestMethod]
        public async Task TestRiffAudio_DecodeWav_ULaw()
        {
            Assert.IsNotNull(ExpectedAudioSample);
            ILogger logger = new ConsoleLogger();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream fileIn = new FileStream(@"sample_ULAW.wav", FileMode.Open, FileAccess.Read))
            using (RiffWaveDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), null))
            {
                Assert.AreEqual(AudioInitializationResult.Success, await decoder.Initialize(fileIn, false, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                Assert.AreEqual(AudioSampleFormat.Mono(16000), decoder.OutputFormat);

                using (BucketAudioSampleTarget sampleTarget = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, null))
                {
                    decoder.ConnectOutput(sampleTarget);
                    await sampleTarget.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    AudioSample outputSample = sampleTarget.GetAllAudio();
                    AudioTestHelpers.AssertSamplesAreSimilar(ExpectedAudioSample, outputSample, 0.99f);
                }
            }
        }
    }
}
