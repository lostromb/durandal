using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Statistics;
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
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    public class FeedbackTests
    {
        [TestMethod]
        public async Task TestAudioFeedbackCircuitBreaker()
        {
            ILogger logger = new ConsoleLogger();
            AudioSampleFormat format = AudioSampleFormat.Mono(44100);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (LinearMixer programAudioMixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget speakersSink = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (PassthroughAudioPipe loopbackPipe = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (VolumeFilter inputExcessiveGain = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (SineWaveSampleSource programGeneratedAudio = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (FeedbackSimulator feedbackLoop = new FeedbackSimulator(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(100)))
            using (FeedbackCircuitBreaker circuitBreaker = new FeedbackCircuitBreaker(new WeakPointer<IAudioGraph>(graph), format, null))
            using (PushPullBuffer circleGap = new PushPullBuffer(new WeakPointer<IAudioGraph>(graph), new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromSeconds(1)))
            using (SilencePaddingFilter silencePad = new SilencePaddingFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (AudioSplitter feedbackSplitter = new AudioSplitter(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                feedbackLoop.ConnectOutput(inputExcessiveGain);
                inputExcessiveGain.ConnectOutput(circuitBreaker.MicrophoneFilterInput);

                programAudioMixer.AddInput(programGeneratedAudio);
                programAudioMixer.AddInput(silencePad);
                programAudioMixer.ConnectOutput(circuitBreaker.SpeakerFilterInput);
                circuitBreaker.SpeakerFilterOutput.ConnectOutput(feedbackSplitter);
                feedbackSplitter.AddOutput(speakersSink);
                feedbackSplitter.AddOutput(feedbackLoop);
                circuitBreaker.MicrophoneFilterOutput.ConnectOutput(circleGap);
                circleGap.ConnectOutput(silencePad);

                inputExcessiveGain.SetVolumeDecibels(VolumeFilter.MIN_VOLUME_DBA);
                inputExcessiveGain.SetVolumeDecibels(30, TimeSpan.FromSeconds(5));

                // Slowly increase gain over 5 seconds
                int samplesRead = 0;
                int samplesToRead = 44100 * 5;
                while (samplesRead < samplesToRead)
                {
                    samplesRead += await speakersSink.ReadSamplesFromInput(Math.Min(100, samplesToRead - samplesRead), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                // Mixer operator realizes their mistake and cuts off gain; signal is expected to recover
                inputExcessiveGain.SetVolumeDecibels(VolumeFilter.MIN_VOLUME_DBA);
                samplesRead = 0;
                while (samplesRead < samplesToRead)
                {
                    samplesRead += await speakersSink.ReadSamplesFromInput(Math.Min(100, samplesToRead - samplesRead), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                AudioSample outputSample = speakersSink.GetAllAudio();
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(10), 0.2f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromSeconds(2), 0.2f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromSeconds(5), 0.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromSeconds(9), 0.2f, 0);
            }
        }

        [Ignore]
        [TestMethod]
        public async Task TestAudioFeedbackDelayEstimatorSandbox2()
        {
            AudioSampleFormat microphoneFormat = AudioSampleFormat.Mono(48000);
            AudioSampleFormat speakerFormat = AudioSampleFormat.Mono(48000);
            ILogger logger = new ConsoleLogger();

            FastRandom rand = new FastRandom(43666);
            int numSeconds = 10;

            // Generate speaker data consisting of a random sine waves
            float[] speakerData = new float[numSeconds * speakerFormat.SampleRateHz * speakerFormat.NumChannels];
            for (int band = 0; band < 10; band++)
            {
                AudioTestHelpers.GenerateSineWave(speakerData, speakerFormat.SampleRateHz, 100.0f + (rand.NextFloat() * 300.0f), numSeconds * speakerFormat.SampleRateHz, 0, 0, speakerFormat.NumChannels, 0.1f, rand.NextFloat());
            }

            AudioSample inputSample = new AudioSample(speakerData, speakerFormat);

            using (IAudioGraph speakerGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioGraph micGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource programSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(speakerGraph), inputSample, null))
            using (BucketAudioSampleTarget alignedSink = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(micGraph), new AudioSampleFormat(microphoneFormat.SampleRateHz, 2, MultiChannelMapping.Stereo_L_R), null))
            using (FeedbackDelayEstimator delayEstimator = new FeedbackDelayEstimator(new WeakPointer<IAudioGraph>(micGraph), new WeakPointer<IAudioGraph>(speakerGraph), microphoneFormat, speakerFormat, speakerFormat, 48000, null, logger))
            using (PushPullBuffer feedbackPushPull = new PushPullBuffer(new WeakPointer<IAudioGraph>(speakerGraph), new WeakPointer<IAudioGraph>(micGraph), microphoneFormat, null, TimeSpan.FromMilliseconds(100)))
            using (SilencePaddingFilter micSilencePad = new SilencePaddingFilter(new WeakPointer<IAudioGraph>(micGraph), microphoneFormat, null))
            using (ChannelMixer packedStereoConverter = new ChannelMixer(new WeakPointer<IAudioGraph>(micGraph), microphoneFormat.SampleRateHz, MultiChannelMapping.Packed_2Ch, MultiChannelMapping.Stereo_L_R, null))
            using (AudioDelayBuffer delay = new AudioDelayBuffer(new WeakPointer<IAudioGraph>(micGraph), microphoneFormat, null, TimeSpan.FromMilliseconds(50)))
            {
                programSource.ConnectOutput(delayEstimator.ProgramAudioInput);
                delayEstimator.ProgramAudioOutput.ConnectOutput(feedbackPushPull);
                feedbackPushPull.ConnectOutput(micSilencePad);
                micSilencePad.ConnectOutput(delay);
                delay.ConnectOutput(delayEstimator.MicrophoneInput);
                delayEstimator.AlignedFeedbackOutput.ConnectOutput(packedStereoConverter);
                packedStereoConverter.ConnectOutput(alignedSink);

                // Push audio through the graph in small increments
                int amountWritten;
                do
                {
                    amountWritten = 0;
                    int readSize = rand.NextInt(1, 500);
                    amountWritten = await programSource.WriteSamplesToOutput(readSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    amountWritten += await alignedSink.ReadSamplesFromInput(readSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                } while (amountWritten > 0);

                using (Stream alignedDataStream = new FileStream(@"C:\Code\Durandal\Data\aligned.wav", FileMode.Create, FileAccess.Write))
                {
                    await AudioHelpers.WriteWaveToStream(alignedSink.GetAllAudio(), alignedDataStream);
                }
            }
        }

        [TestMethod]
        public async Task TestAudioFeedbackDelayEstimatorMonoOutputMonoInput()
        {
            AudioSampleFormat microphoneFormat = AudioSampleFormat.Mono(48000);
            AudioSampleFormat speakerFormat = AudioSampleFormat.Mono(48000);
            ILogger logger = new ConsoleLogger();
            FastRandom rand = new FastRandom(43666);

            // Generate speaker data consisting of a random sine waves
            float[] speakerData = new float[10 * speakerFormat.SampleRateHz * speakerFormat.NumChannels];
            for (int band = 0; band < 10; band++)
            {
                AudioTestHelpers.GenerateSineWave(speakerData, speakerFormat.SampleRateHz, 100.0f + (rand.NextFloat() * 300.0f), speakerData.Length, 0, 0, speakerFormat.NumChannels, 0.1f, rand.NextFloat());
            }

            AudioSample inputSample = new AudioSample(speakerData, speakerFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource programSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (NullAudioSampleTarget programSink = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), microphoneFormat, null))
            using (AudioPeekBuffer peekBuffer = new AudioPeekBuffer(new WeakPointer<IAudioGraph>(graph), speakerFormat, null, TimeSpan.FromMilliseconds(150), rand))
            using (AudioConformer speakerToMicConformer = new AudioConformer(new WeakPointer<IAudioGraph>(graph), speakerFormat, microphoneFormat, null, logger.Clone("Resampler"), AudioProcessingQuality.BestQuality))
            using (FeedbackDelayEstimatorCore delayEstimator = new FeedbackDelayEstimatorCore(new WeakPointer<IAudioGraph>(graph), microphoneFormat, null, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), new WeakPointer<AudioPeekBuffer>(peekBuffer), logger))
            using (FeedbackSimulator feedbackSimulator = new FeedbackSimulator(new WeakPointer<IAudioGraph>(graph), microphoneFormat, null, TimeSpan.FromMilliseconds(25)))
            {
                programSource.ConnectOutput(peekBuffer);
                peekBuffer.ConnectOutput(feedbackSimulator);
                feedbackSimulator.ConnectOutput(speakerToMicConformer);
                speakerToMicConformer.ConnectOutput(delayEstimator);
                delayEstimator.ConnectOutput(programSink);

                // Push audio through the graph in small increments
                int amountWritten;
                do
                {
                    amountWritten = await programSink.ReadSamplesFromInput(rand.NextInt(1, 4800), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    amountWritten = await programSource.WriteSamplesToOutput(rand.NextInt(1, 4800), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                } while (amountWritten > 0);

                Hypothesis<TimeSpan> hyp = delayEstimator.GetEstimatedDelay();
                Console.WriteLine("Best hyp is " + hyp.Value.TotalMilliseconds + " with confidence " + hyp.Conf);
                Assert.AreEqual(25.0f, hyp.Value.TotalMilliseconds, 1.0f, "Delay estimation hypothesis was too inaccurate");
                Assert.IsTrue(hyp.Conf > 0.9f, "Delay estimation confidence was not high enough");
            }
        }

        [Ignore]
        [TestMethod]
        public async Task TestAudioFeedbackDelayEstimatorSandbox()
        {
            AudioSampleFormat microphoneFormat = AudioSampleFormat.Mono(48000);
            AudioSampleFormat speakerFormat = AudioSampleFormat.Mono(48000);
            ILogger logger = new ConsoleLogger();
            FastRandom rand = new FastRandom(6641);

            // The exact actual delay in these samples is about 35ms
            AudioSample speakerOutput;
            AudioSample micInput;
            using (Stream inStream = new FileStream(@"C:\Code\Durandal\Data\danger spkr.wav", FileMode.Open, FileAccess.Read))
            {
                speakerOutput = await AudioHelpers.ReadWaveFromStream(inStream);
            }
            using (Stream inStream = new FileStream(@"C:\Code\Durandal\Data\danger mic.wav", FileMode.Open, FileAccess.Read))
            {
                micInput = await AudioHelpers.ReadWaveFromStream(inStream);
            }

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource programSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), speakerOutput, null))
            using (FixedAudioSampleSource micSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), micInput, null))
            using (NullAudioSampleTarget micSink = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), microphoneFormat, null))
            using (NullAudioSampleTarget programSink = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), speakerFormat, null))
            using (AudioPeekBuffer peekBuffer = new AudioPeekBuffer(new WeakPointer<IAudioGraph>(graph), speakerFormat, null, TimeSpan.FromMilliseconds(1000)))
            using (FeedbackDelayEstimatorCore delayEstimator = new FeedbackDelayEstimatorCore(new WeakPointer<IAudioGraph>(graph), microphoneFormat, null, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(100), new WeakPointer<AudioPeekBuffer>(peekBuffer), logger))
            {
                programSource.ConnectOutput(peekBuffer);
                peekBuffer.ConnectOutput(programSink);

                micSource.ConnectOutput(delayEstimator);
                delayEstimator.ConnectOutput(micSink);

                // Push audio through the graph in small increments
                bool done = false;
                while (!done)
                {
                    int readSize = rand.NextInt(1, 480);
                    int samplesProcessed = 0;
                    samplesProcessed = await micSink.ReadSamplesFromInput(readSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    samplesProcessed += await micSource.WriteSamplesToOutput(readSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    if (samplesProcessed <= 0)
                    {
                        done = true;
                    }

                    samplesProcessed = 0;
                    samplesProcessed = await programSink.ReadSamplesFromInput(readSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    samplesProcessed = await programSource.WriteSamplesToOutput(readSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    if (samplesProcessed <= 0)
                    {
                        done = true;
                    }
                }
            }
        }
    }
}
