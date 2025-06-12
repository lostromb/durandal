using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Tests.Common.Audio;
using Durandal.Common.Audio.Test;
using Durandal.Common.MathExt;
using Durandal.Extensions.NativeAudio;
using Durandal.Common.Utils.NativePlatform;
using System.IO;
using Durandal.Common.Audio.Codecs;
using System;
using Durandal.Common.Audio.Codecs.Opus.Common;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.NativeAudio
{
    [TestClass]
    public class NativeResamplerTests
    {
        private static bool _speexLibExists = false;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            _speexLibExists = new NativeSpeexResamplerAccelerator().Apply(DebugLogger.Default);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            new NativeSpeexResamplerAccelerator().Unapply(DebugLogger.Default);
            NativePlatformUtils.SetGlobalResolver(null);
        }

        [TestMethod]
        [DataRow(AudioProcessingQuality.Fastest)]
        [DataRow(AudioProcessingQuality.BestQuality)]
        public async Task TestAudioResamplerFilterDownsampleMono(AudioProcessingQuality quality)
        {
            if (!_speexLibExists)
            {
                Assert.Inconclusive("Native libspeexdsp not found");
            }

            ILogger logger = new ConsoleLogger();
            using (IResampler test = ResamplerFactory.Create(1, 16000, 48000, AudioProcessingQuality.Balanced, logger.Clone("Resampler")))
            {
                Assert.IsNotInstanceOfType(test, typeof(SpeexResampler));
            }

            AudioSampleFormat inputFormat = AudioSampleFormat.Mono(96052);
            AudioSampleFormat outputFormat = AudioSampleFormat.Mono(44144);
            float[] sampleData = new float[inputFormat.SampleRateHz];
            AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 500, inputFormat.SampleRateHz, 0, 0, inputFormat.NumChannels, 0.8f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ResamplingFilter filter = new ResamplingFilter(new WeakPointer<IAudioGraph>(graph), null, 1, MultiChannelMapping.Monaural, inputFormat.SampleRateHz, outputFormat.SampleRateHz, logger, quality))
            {
                AudioSample outputSample = await PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 7655);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);

                float[] expectedOutputData = new float[outputFormat.SampleRateHz];
                int filterLatencySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(outputFormat.SampleRateHz, filter.AlgorithmicDelay);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, outputFormat.SampleRateHz, 500, outputFormat.SampleRateHz - filterLatencySamplesPerChannel, filterLatencySamplesPerChannel, 0, outputFormat.NumChannels, 0.8f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.97f);
            }
        }

        [TestMethod]
        [DataRow(AudioProcessingQuality.Fastest)]
        [DataRow(AudioProcessingQuality.BestQuality)]
        public async Task TestAudioResamplerFilterDownsampleStereo(AudioProcessingQuality quality)
        {
            if (!_speexLibExists)
            {
                Assert.Inconclusive("Native libspeexdsp not found");
            }

            ILogger logger = new ConsoleLogger();
            using (IResampler test = ResamplerFactory.Create(1, 16000, 48000, AudioProcessingQuality.Balanced, logger.Clone("Resampler")))
            {
                Assert.IsNotInstanceOfType(test, typeof(SpeexResampler));
            }

            AudioSampleFormat inputFormat = AudioSampleFormat.Stereo(96052);
            AudioSampleFormat outputFormat = AudioSampleFormat.Stereo(44144);
            float[] sampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 500, inputFormat.SampleRateHz, 0, 0, inputFormat.NumChannels, 0.8f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 500, inputFormat.SampleRateHz, 0, 1, inputFormat.NumChannels, 0.8f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ResamplingFilter filter = new ResamplingFilter(new WeakPointer<IAudioGraph>(graph), null, 2, MultiChannelMapping.Stereo_L_R, inputFormat.SampleRateHz, outputFormat.SampleRateHz, logger, quality))
            {
                AudioSample outputSample = await PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 63454);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);

                float[] expectedOutputData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                int filterLatencySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(outputFormat.SampleRateHz, filter.AlgorithmicDelay);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, outputFormat.SampleRateHz, 500, outputFormat.SampleRateHz - filterLatencySamplesPerChannel, filterLatencySamplesPerChannel, 0, outputFormat.NumChannels, 0.8f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, outputFormat.SampleRateHz, 500, outputFormat.SampleRateHz - filterLatencySamplesPerChannel, filterLatencySamplesPerChannel, 1, outputFormat.NumChannels, 0.8f, 0.5f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.97f);
            }
        }

        [TestMethod]
        [DataRow(AudioProcessingQuality.Fastest)]
        [DataRow(AudioProcessingQuality.Balanced)]
        [DataRow(AudioProcessingQuality.BestQuality)]
        public async Task TestAudioResamplerFilterUpsampleMono(AudioProcessingQuality quality)
        {
            if (!_speexLibExists)
            {
                Assert.Inconclusive("Native libspeexdsp not found");
            }

            ILogger logger = new ConsoleLogger();
            using (IResampler test = ResamplerFactory.Create(1, 16000, 48000, AudioProcessingQuality.Balanced, logger.Clone("Resampler")))
            {
                Assert.IsNotInstanceOfType(test, typeof(SpeexResampler));
            }

            AudioSampleFormat inputFormat = AudioSampleFormat.Mono(44144);
            AudioSampleFormat outputFormat = AudioSampleFormat.Mono(96052);
            float[] sampleData = new float[inputFormat.SampleRateHz];
            AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 500, inputFormat.SampleRateHz, 0, 0, inputFormat.NumChannels, 0.8f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, inputFormat);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ResamplingFilter filter = new ResamplingFilter(new WeakPointer<IAudioGraph>(graph), null, 1, MultiChannelMapping.Monaural, inputFormat.SampleRateHz, outputFormat.SampleRateHz, logger, quality))
            {
                AudioSample outputSample = await PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 763334);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);

                float[] expectedOutputData = new float[outputFormat.SampleRateHz];
                int filterLatencySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(outputFormat.SampleRateHz, filter.AlgorithmicDelay);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, outputFormat.SampleRateHz, 500, outputFormat.SampleRateHz - filterLatencySamplesPerChannel, filterLatencySamplesPerChannel, 0, outputFormat.NumChannels, 0.8f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.98f, 12);
            }
        }

        [TestMethod]
        [DataRow(AudioProcessingQuality.Fastest)]
        [DataRow(AudioProcessingQuality.Balanced)]
        [DataRow(AudioProcessingQuality.BestQuality)]
        public async Task TestAudioResamplerFilterUpsampleStereo(AudioProcessingQuality quality)
        {
            if (!_speexLibExists)
            {
                Assert.Inconclusive("Native libspeexdsp not found");
            }

            ILogger logger = new ConsoleLogger();
            using (IResampler test = ResamplerFactory.Create(1, 16000, 48000, AudioProcessingQuality.Balanced, logger.Clone("Resampler")))
            {
                Assert.IsNotInstanceOfType(test, typeof(SpeexResampler));
            }

            AudioSampleFormat inputFormat = AudioSampleFormat.Stereo(44144);
            AudioSampleFormat outputFormat = AudioSampleFormat.Stereo(96052);
            float[] sampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 500, inputFormat.SampleRateHz, 0, 0, inputFormat.NumChannels, 0.8f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 500, inputFormat.SampleRateHz, 0, 1, inputFormat.NumChannels, 0.8f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, inputFormat);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ResamplingFilter filter = new ResamplingFilter(new WeakPointer<IAudioGraph>(graph), null, 2, MultiChannelMapping.Stereo_L_R, inputFormat.SampleRateHz, outputFormat.SampleRateHz, logger, quality))
            {
                AudioSample outputSample = await PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 421354);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);

                float[] expectedOutputData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                int filterLatencySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(outputFormat.SampleRateHz, filter.AlgorithmicDelay);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, outputFormat.SampleRateHz, 500, outputFormat.SampleRateHz - filterLatencySamplesPerChannel, filterLatencySamplesPerChannel, 0, outputFormat.NumChannels, 0.8f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, outputFormat.SampleRateHz, 500, outputFormat.SampleRateHz - filterLatencySamplesPerChannel, filterLatencySamplesPerChannel, 1, outputFormat.NumChannels, 0.8f, 0.5f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.98f, 12);
            }
        }

        [TestMethod]
        [DataRow(AudioProcessingQuality.Fastest)]
        [DataRow(AudioProcessingQuality.BestQuality)]
        public async Task TestAudioConformerDownsampleStereoToMono(AudioProcessingQuality quality)
        {
            if (!_speexLibExists)
            {
                Assert.Inconclusive("Native libspeexdsp not found");
            }

            ILogger logger = new ConsoleLogger();
            using (IResampler test = ResamplerFactory.Create(1, 16000, 48000, AudioProcessingQuality.Balanced, logger.Clone("Resampler")))
            {
                Assert.IsNotInstanceOfType(test, typeof(SpeexResampler));
            }

            AudioSampleFormat inputFormat = AudioSampleFormat.Stereo(96052);
            AudioSampleFormat outputFormat = AudioSampleFormat.Mono(44144);
            float[] sampleData = new float[inputFormat.SampleRateHz * 2];
            AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 500, inputFormat.SampleRateHz, 0, 0, inputFormat.NumChannels, 0.8f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 500, inputFormat.SampleRateHz, 0, 1, inputFormat.NumChannels, 0.8f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, inputFormat);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioConformer filter = new AudioConformer(new WeakPointer<IAudioGraph>(graph), inputFormat, outputFormat, null, logger, quality))
            {
                AudioSample outputSample = await PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 7655);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);

                float[] expectedOutputData = new float[outputFormat.SampleRateHz];
                int filterLatencySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(outputFormat.SampleRateHz, filter.AlgorithmicDelay);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, outputFormat.SampleRateHz, 500, outputFormat.SampleRateHz - filterLatencySamplesPerChannel, filterLatencySamplesPerChannel, 0, outputFormat.NumChannels, 0.8f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.97f);
            }
        }

        [TestMethod]
        [DataRow(AudioProcessingQuality.Fastest)]
        [DataRow(AudioProcessingQuality.Balanced)]
        [DataRow(AudioProcessingQuality.BestQuality)]
        public async Task TestAudioConformerFilterUpsampleMonoToStereo(AudioProcessingQuality quality)
        {
            if (!_speexLibExists)
            {
                Assert.Inconclusive("Native libspeexdsp not found");
            }

            ILogger logger = new ConsoleLogger();
            using (IResampler test = ResamplerFactory.Create(1, 16000, 48000, AudioProcessingQuality.Balanced, logger.Clone("Resampler")))
            {
                Assert.IsNotInstanceOfType(test, typeof(SpeexResampler));
            }

            AudioSampleFormat inputFormat = AudioSampleFormat.Mono(44144);
            AudioSampleFormat outputFormat = AudioSampleFormat.Stereo(96052);
            float[] sampleData = new float[inputFormat.SampleRateHz];
            AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 500, inputFormat.SampleRateHz, 0, 0, inputFormat.NumChannels, 0.8f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, inputFormat);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioConformer filter = new AudioConformer(new WeakPointer<IAudioGraph>(graph), inputFormat, outputFormat, null, logger, quality))
            {
                AudioSample outputSample = await PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 421354);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);

                float[] expectedOutputData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                int filterLatencySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(outputFormat.SampleRateHz, filter.AlgorithmicDelay);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, outputFormat.SampleRateHz, 500, outputFormat.SampleRateHz - filterLatencySamplesPerChannel, filterLatencySamplesPerChannel, 0, outputFormat.NumChannels, 0.8f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, outputFormat.SampleRateHz, 500, outputFormat.SampleRateHz - filterLatencySamplesPerChannel, filterLatencySamplesPerChannel, 1, outputFormat.NumChannels, 0.8f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.98f, 12);
            }
        }

        [TestMethod]
        public async Task TestAudioResamplerFilterLargeWrites()
        {
            if (!_speexLibExists)
            {
                Assert.Inconclusive("Native libspeexdsp not found");
            }

            ILogger logger = new ConsoleLogger();
            using (IResampler test = ResamplerFactory.Create(1, 16000, 48000, AudioProcessingQuality.Balanced, logger.Clone("Resampler")))
            {
                Assert.IsNotInstanceOfType(test, typeof(SpeexResampler));
            }

            AudioSampleFormat inputFormat = AudioSampleFormat.Stereo(44100);
            AudioSampleFormat outputFormat = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 500, inputFormat.SampleRateHz, 0, 0, inputFormat.NumChannels, 0.8f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, inputFormat.SampleRateHz, 500, inputFormat.SampleRateHz, 0, 1, inputFormat.NumChannels, 0.8f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, inputFormat);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ResamplingFilter filter = new ResamplingFilter(new WeakPointer<IAudioGraph>(graph), null, 2, MultiChannelMapping.Stereo_L_R, 44100, 48000, logger, AudioProcessingQuality.BestQuality))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), outputFormat, null))
            {
                source.ConnectOutput(filter);
                filter.ConnectOutput(target);
                while (!source.PlaybackFinished)
                {
                    await source.WriteSamplesToOutput(65535, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                AudioSample outputSample = target.GetAllAudio();
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);

                float[] expectedOutputData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                int filterLatencySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(outputFormat.SampleRateHz, filter.AlgorithmicDelay);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, outputFormat.SampleRateHz, 500, outputFormat.SampleRateHz - filterLatencySamplesPerChannel, filterLatencySamplesPerChannel, 0, outputFormat.NumChannels, 0.8f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, outputFormat.SampleRateHz, 500, outputFormat.SampleRateHz - filterLatencySamplesPerChannel, filterLatencySamplesPerChannel, 1, outputFormat.NumChannels, 0.8f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.95f);
            }
        }

        private static async Task<AudioSample> PushAudioThroughGraph(
            WeakPointer<IAudioGraph> graph,
            IAudioSampleTarget graphIn,
            IAudioSampleSource graphOut,
            AudioSample inputSample,
            int srand = -1)
        {
            IRandom rand;
            if (srand == -1)
            {
                rand = new FastRandom();
            }
            else
            {
                rand = new FastRandom(srand);
            }

            using (FixedAudioSampleSource source = new FixedAudioSampleSource(graph, inputSample, null))
            using (SimulatedUnreliableAudioSource unreliability = new SimulatedUnreliableAudioSource(graph, graphIn.InputFormat, rand, 0.3f))
            using (PollutedBufferPipe bufferPolluter1 = new PollutedBufferPipe(graph, graphIn.InputFormat))
            using (PollutedBufferPipe bufferPolluter2 = new PollutedBufferPipe(graph, graphOut.OutputFormat))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(graph, graphOut.OutputFormat, null))
            {
                source.ConnectOutput(unreliability);
                unreliability.ConnectOutput(bufferPolluter1);
                bufferPolluter1.ConnectOutput(graphIn);
                graphOut.ConnectOutput(bufferPolluter2);
                bufferPolluter2.ConnectOutput(target);

                while (!source.PlaybackFinished)
                {
                    await target.ReadSamplesFromInput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await source.WriteSamplesToOutput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                source.DisconnectOutput();
                target.DisconnectInput();
                return target.GetAllAudio();
            }
        }
    }
}
