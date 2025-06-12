using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
    public class CompressorTests
    {
        [TestMethod]
        public void TestAudioCompressorArgumentValidation()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                try
                {
                    DynamicRangeCompressor filter = new DynamicRangeCompressor(
                        WeakPointer<IAudioGraph>.Null,
                        AudioSampleFormat.Mono(16000),
                        nodeCustomName: null,
                        targetPeakAmplitude: 1.0f,
                        lookAhead: TimeSpan.FromMilliseconds(0),
                        attack: TimeSpan.FromMilliseconds(50),
                        sustain: TimeSpan.FromMilliseconds(1000),
                        release: TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Should have thrown a ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    DynamicRangeCompressor filter = new DynamicRangeCompressor(
                        new WeakPointer<IAudioGraph>(graph),
                        null,
                        nodeCustomName: null,
                        targetPeakAmplitude: 1.0f,
                        lookAhead: TimeSpan.FromMilliseconds(0),
                        attack: TimeSpan.FromMilliseconds(50),
                        sustain: TimeSpan.FromMilliseconds(1000),
                        release: TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Should have thrown a ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    DynamicRangeCompressor filter = new DynamicRangeCompressor(
                        new WeakPointer<IAudioGraph>(graph),
                        AudioSampleFormat.Mono(16000),
                        nodeCustomName: null,
                        targetPeakAmplitude: -1.0f,
                        lookAhead: TimeSpan.FromMilliseconds(0),
                        attack: TimeSpan.FromMilliseconds(50),
                        sustain: TimeSpan.FromMilliseconds(1000),
                        release: TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    DynamicRangeCompressor filter = new DynamicRangeCompressor(
                        new WeakPointer<IAudioGraph>(graph),
                        AudioSampleFormat.Mono(16000),
                        nodeCustomName: null,
                        targetPeakAmplitude: 1.0f,
                        lookAhead: TimeSpan.FromMilliseconds(-50),
                        attack: TimeSpan.FromMilliseconds(50),
                        sustain: TimeSpan.FromMilliseconds(1000),
                        release: TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    DynamicRangeCompressor filter = new DynamicRangeCompressor(
                        new WeakPointer<IAudioGraph>(graph),
                        AudioSampleFormat.Mono(16000),
                        nodeCustomName: null,
                        targetPeakAmplitude: 1.0f,
                        lookAhead: TimeSpan.FromMilliseconds(0),
                        attack: TimeSpan.FromMilliseconds(-50),
                        sustain: TimeSpan.FromMilliseconds(1000),
                        release: TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    DynamicRangeCompressor filter = new DynamicRangeCompressor(
                        new WeakPointer<IAudioGraph>(graph),
                        AudioSampleFormat.Mono(16000),
                        nodeCustomName: null,
                        targetPeakAmplitude: 1.0f,
                        lookAhead: TimeSpan.FromMilliseconds(0),
                        attack: TimeSpan.FromMilliseconds(50),
                        sustain: TimeSpan.FromMilliseconds(-1000),
                        release: TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    DynamicRangeCompressor filter = new DynamicRangeCompressor(
                        new WeakPointer<IAudioGraph>(graph),
                        AudioSampleFormat.Mono(16000),
                        nodeCustomName: null,
                        targetPeakAmplitude: 1.0f,
                        lookAhead: TimeSpan.FromMilliseconds(0),
                        attack: TimeSpan.FromMilliseconds(50),
                        sustain: TimeSpan.FromMilliseconds(1000),
                        release: TimeSpan.FromMilliseconds(-1000));
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        public async Task TestAudioCompressorTransientPeakNoLookahead()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            // base tone
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);

            // loud transient at 1s mark lasting about 100ms
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 4800, 48000, 0, format.NumChannels, 1.7f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 4800, 48000, 1, format.NumChannels, 1.7f, 0.5f);

            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (DynamicRangeCompressor filter = new DynamicRangeCompressor(
                new WeakPointer<IAudioGraph>(graph),
                format,
                nodeCustomName: null,
                targetPeakAmplitude: 1.0f,
                lookAhead: TimeSpan.FromMilliseconds(0),
                attack: TimeSpan.FromMilliseconds(50),
                sustain: TimeSpan.FromMilliseconds(1000),
                release: TimeSpan.FromMilliseconds(1000)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 324533);

                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(980));
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(1150), TimeSpan.FromMilliseconds(4000));

                // Constant volume at start
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(400), 0.4f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(400), 0.4f, 1);

                // Sharp clip at the peak
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 2.1f, 0, 0.1f);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 2.1f, 1, 0.1f);

                // Suppression takes effect (sustain)
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1200), 0.1f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1200), 0.1f, 1);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1800), 0.1f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1800), 0.1f, 1);

                // midway through release
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(2500), 0.19f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(2500), 0.19f, 1);

                // And return to normal
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(3500), 0.4f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(3500), 0.4f, 1);
            }
        }

        [TestMethod]
        public async Task TestAudioCompressorTransientPeakWithLookahead()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            // base tone
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);

            // loud transient at 1s mark lasting about 100ms
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 4800, 48000, 0, format.NumChannels, 1.7f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 4800, 48000, 1, format.NumChannels, 1.7f, 0.5f);

            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (DynamicRangeCompressor filter = new DynamicRangeCompressor(
                new WeakPointer<IAudioGraph>(graph),
                format,
                nodeCustomName: null,
                targetPeakAmplitude: 1.0f,
                lookAhead: TimeSpan.FromMilliseconds(100),
                attack: TimeSpan.FromMilliseconds(50),
                sustain: TimeSpan.FromMilliseconds(1000),
                release: TimeSpan.FromMilliseconds(1000)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 45777554);

                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(980));
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(1150), TimeSpan.FromMilliseconds(4000));

                // Constant volume at start
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(400), 0.4f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(400), 0.4f, 1);

                // Pre-suppression
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(950), 0.2f, 0, 0.1f);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(950), 0.2f, 1, 0.1f);

                // Peak is already suppressed
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.6f, 0, 0.1f);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.6f, 1, 0.1f);

                // Suppression takes effect (sustain)
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1200), 0.1f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1200), 0.1f, 1);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1800), 0.1f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1800), 0.1f, 1);

                // midway through release
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(2500), 0.22f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(2500), 0.22f, 1);

                // And return to normal
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(3500), 0.4f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(3500), 0.4f, 1);
            }
        }

        [TestMethod]
        public async Task TestAudioCompressorConstantLoudness()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            // base tone that's really loud; should continuously trigger attacks
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 48000 * 4, 0, 0, format.NumChannels, 1.2f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 48000 * 4, 0, 1, format.NumChannels, 1.2f, 0.5f);

            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (DynamicRangeCompressor filter = new DynamicRangeCompressor(
                new WeakPointer<IAudioGraph>(graph),
                format,
                nodeCustomName: null,
                targetPeakAmplitude: 1.0f,
                lookAhead: TimeSpan.FromMilliseconds(0),
                attack: TimeSpan.FromMilliseconds(50),
                sustain: TimeSpan.FromMilliseconds(1000),
                release: TimeSpan.FromMilliseconds(1000)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 854566);

                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(50), 0.4f);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(4000), 0.17f);

                // Loud at start
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1), 1.2f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1), 1.2f, 1);

                // Then sustained suppression throughout
                for (int ms = 100; ms < 4000; ms += 20)
                {
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.47f, 0, 0.02f);
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.47f, 1, 0.02f);
                }
            }
        }
        
        /// <summary>
        /// Tests that if the signal is loud from the very start, and lookahead is enabled, the compression is applied even before the first sample is output.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestAudioCompressorConstantLoudnessWithLookahead()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            // base tone that's really loud; should continuously trigger attacks
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 48000 * 4, 0, 0, format.NumChannels, 1.2f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 48000 * 4, 0, 1, format.NumChannels, 1.2f, 0.5f);

            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (DynamicRangeCompressor filter = new DynamicRangeCompressor(
                new WeakPointer<IAudioGraph>(graph),
                format,
                nodeCustomName: null,
                targetPeakAmplitude: 1.0f,
                lookAhead: TimeSpan.FromMilliseconds(60),
                attack: TimeSpan.FromMilliseconds(50),
                sustain: TimeSpan.FromMilliseconds(1000),
                release: TimeSpan.FromMilliseconds(1000)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 237644);

                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(4000), 0.17f);

                // Sustained suppression throughout
                for (int ms = 0; ms < 4000; ms += 20)
                {
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.47f, 0, 0.02f);
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.47f, 1, 0.02f);
                }
            }
        }

        [TestMethod]
        public async Task TestAudioCompressorSingleChannelTransient()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            // base tone
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);

            // loud transient at 1s mark lasting about 100ms. This time it only happens on the left channe.
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 4800, 48000, 0, format.NumChannels, 1.7f, 0.0f);

            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (DynamicRangeCompressor filter = new DynamicRangeCompressor(
                new WeakPointer<IAudioGraph>(graph),
                format,
                nodeCustomName: null,
                targetPeakAmplitude: 1.0f,
                lookAhead: TimeSpan.FromMilliseconds(0),
                attack: TimeSpan.FromMilliseconds(50),
                sustain: TimeSpan.FromMilliseconds(1000),
                release: TimeSpan.FromMilliseconds(1000)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 346645);

                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(980));
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(1150), TimeSpan.FromMilliseconds(4000));

                // Constant volume at start
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(400), 0.4f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(400), 0.4f, 1);

                // Sharp clip at the peak but only on left channel
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 2.1f, 0, 0.1f);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.4f, 1, 0.1f);

                // Suppression takes effect (sustain)
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1200), 0.1f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1200), 0.1f, 1);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1800), 0.1f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1800), 0.1f, 1);

                // midway through release
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(2500), 0.19f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(2500), 0.19f, 1);

                // And return to normal
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(3500), 0.4f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(3500), 0.4f, 1);
            }
        }
        
        [TestMethod]
        public async Task TestAudioCompressorFlushAndContinue()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            // base tone that's really loud; should continuously trigger attacks
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 48000 * 4, 0, 0, format.NumChannels, 1.2f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 48000 * 4, 0, 1, format.NumChannels, 1.2f, 0.5f);

            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (DynamicRangeCompressor filter = new DynamicRangeCompressor(
                new WeakPointer<IAudioGraph>(graph),
                format,
                nodeCustomName: null,
                targetPeakAmplitude: 1.0f,
                lookAhead: TimeSpan.FromMilliseconds(100),
                attack: TimeSpan.FromMilliseconds(50),
                sustain: TimeSpan.FromMilliseconds(1000),
                release: TimeSpan.FromMilliseconds(1000)))
            {
                FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null);
                BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), filter.OutputFormat, null);
                source.ConnectOutput(filter);
                target.ConnectInput(filter);

                IRandom rand = new FastRandom(189956);
                while (!source.PlaybackFinished)
                {
                    await source.WriteSamplesToOutput(rand.NextInt(1, 4800), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await filter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                AudioSample outputSample = target.GetAllAudio();
                
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(50), 0.4f);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(4000), 0.17f);

                // Sustained suppression throughout
                for (int ms = 0; ms < 4000; ms += 20)
                {
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.52f, 0, 0.05f);
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.52f, 1, 0.05f);
                }
            }
        }

        [TestMethod]
        public void TestAudioGateArgumentValidation()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                try
                {
                    AudioGate filter = new AudioGate(
                        WeakPointer<IAudioGraph>.Null,
                        AudioSampleFormat.Mono(16000),
                        nodeCustomName: null,
                        gateThresholdRmsDba: -40.0f,
                        lookAhead: TimeSpan.FromMilliseconds(0),
                        attack: TimeSpan.FromMilliseconds(50),
                        sustain: TimeSpan.FromMilliseconds(1000),
                        release: TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Should have thrown a ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    AudioGate filter = new AudioGate(
                        new WeakPointer<IAudioGraph>(graph),
                        null,
                        nodeCustomName: null,
                        gateThresholdRmsDba: -40.0f,
                        lookAhead: TimeSpan.FromMilliseconds(0),
                        attack: TimeSpan.FromMilliseconds(50),
                        sustain: TimeSpan.FromMilliseconds(1000),
                        release: TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Should have thrown a ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    AudioGate filter = new AudioGate(
                        new WeakPointer<IAudioGraph>(graph),
                        AudioSampleFormat.Mono(16000),
                        nodeCustomName: null,
                        gateThresholdRmsDba: -40.0f,
                        lookAhead: TimeSpan.FromMilliseconds(-50),
                        attack: TimeSpan.FromMilliseconds(50),
                        sustain: TimeSpan.FromMilliseconds(1000),
                        release: TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    AudioGate filter = new AudioGate(
                        new WeakPointer<IAudioGraph>(graph),
                        AudioSampleFormat.Mono(16000),
                        nodeCustomName: null,
                        gateThresholdRmsDba: -40.0f,
                        lookAhead: TimeSpan.FromMilliseconds(50),
                        attack: TimeSpan.FromMilliseconds(-50),
                        sustain: TimeSpan.FromMilliseconds(1000),
                        release: TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    AudioGate filter = new AudioGate(
                        new WeakPointer<IAudioGraph>(graph),
                        AudioSampleFormat.Mono(16000),
                        nodeCustomName: null,
                        gateThresholdRmsDba: -40.0f,
                        lookAhead: TimeSpan.FromMilliseconds(50),
                        attack: TimeSpan.FromMilliseconds(50),
                        sustain: TimeSpan.FromMilliseconds(-1000),
                        release: TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    AudioGate filter = new AudioGate(
                        new WeakPointer<IAudioGraph>(graph),
                        AudioSampleFormat.Mono(16000),
                        nodeCustomName: null,
                        gateThresholdRmsDba: -40.0f,
                        lookAhead: TimeSpan.FromMilliseconds(50),
                        attack: TimeSpan.FromMilliseconds(50),
                        sustain: TimeSpan.FromMilliseconds(1000),
                        release: TimeSpan.FromMilliseconds(-1000));
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        public async Task TestAudioGateTransientPeakNoLookahead()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            // base tone
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 0, format.NumChannels, 0.02f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 1, format.NumChannels, 0.02f, 0.5f);

            // loud transient at 1s mark lasting about 100ms
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 4800, 48000, 0, format.NumChannels, 0.9f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 4800, 48000, 1, format.NumChannels, 0.9f, 0.5f);

            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioGate filter = new AudioGate(
                new WeakPointer<IAudioGraph>(graph),
                format,
                nodeCustomName: null,
                gateThresholdRmsDba: -24.0f,
                lookAhead: TimeSpan.FromMilliseconds(0),
                attack: TimeSpan.FromMilliseconds(50),
                sustain: TimeSpan.FromMilliseconds(1000),
                release: TimeSpan.FromMilliseconds(1000)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 6885);

                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(980));
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(1150), TimeSpan.FromMilliseconds(4000));

                // Silence at start
                AudioTestHelpers.AssertPeakVolumeAtRange(outputSample, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000), 0.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtRange(outputSample, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000), 0.0f, 1);

                // Peak is clipped a bit
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.0f, 1);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1070), 0.92f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1070), 0.92f, 1);

                // Sustain
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1200), 0.02f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1200), 0.02f, 1);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1800), 0.02f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1800), 0.02f, 1);

                // midway through release
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(2500), 0.01f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(2500), 0.01f, 1);

                // And return to normal
                AudioTestHelpers.AssertPeakVolumeAtRange(outputSample, TimeSpan.FromMilliseconds(2500), TimeSpan.FromMilliseconds(4000), 0.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtRange(outputSample, TimeSpan.FromMilliseconds(2500), TimeSpan.FromMilliseconds(4000), 0.0f, 1);
            }
        }

        [TestMethod]
        public async Task TestAudioGateTransientPeakWithLookahead()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            // base tone
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 0, format.NumChannels, 0.02f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 1, format.NumChannels, 0.02f, 0.5f);

            // loud transient at 1s mark lasting about 100ms
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 4800, 48000, 0, format.NumChannels, 0.9f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 4800, 48000, 1, format.NumChannels, 0.9f, 0.5f);

            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioGate filter = new AudioGate(
                new WeakPointer<IAudioGraph>(graph),
                format,
                nodeCustomName: null,
                gateThresholdRmsDba: -24.0f,
                lookAhead: TimeSpan.FromMilliseconds(100),
                attack: TimeSpan.FromMilliseconds(50),
                sustain: TimeSpan.FromMilliseconds(1000),
                release: TimeSpan.FromMilliseconds(1000)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 42345);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(980));
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(1150), TimeSpan.FromMilliseconds(4000));

                // Silence at start
                AudioTestHelpers.AssertPeakVolumeAtRange(outputSample, TimeSpan.Zero, TimeSpan.FromMilliseconds(900), 0.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtRange(outputSample, TimeSpan.Zero, TimeSpan.FromMilliseconds(900), 0.0f, 1);

                AudioTestHelpers.AssertPeakVolumeAtRange(outputSample, TimeSpan.Zero, TimeSpan.FromMilliseconds(960), 0.02f, 0);
                AudioTestHelpers.AssertPeakVolumeAtRange(outputSample, TimeSpan.Zero, TimeSpan.FromMilliseconds(960), 0.02f, 1);
                // Peak is not clipped
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.92f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.92f, 1);

                // Sustain
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1200), 0.02f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1200), 0.02f, 1);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1800), 0.02f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1800), 0.02f, 1);

                // midway through release
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(2500), 0.01f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(2500), 0.01f, 1);

                // And return to normal
                AudioTestHelpers.AssertPeakVolumeAtRange(outputSample, TimeSpan.FromMilliseconds(2500), TimeSpan.FromMilliseconds(4000), 0.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtRange(outputSample, TimeSpan.FromMilliseconds(2500), TimeSpan.FromMilliseconds(4000), 0.0f, 1);
            }
        }

        [TestMethod]
        public async Task TestAudioGateFlushAndContinue()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 48000 * 4, 0, 0, format.NumChannels, 0.8f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, 48000 * 4, 0, 1, format.NumChannels, 0.8f, 0.5f);

            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioGate filter = new AudioGate(
                new WeakPointer<IAudioGraph>(graph),
                format,
                nodeCustomName: null,
                gateThresholdRmsDba: -40.0f,
                lookAhead: TimeSpan.FromMilliseconds(0),
                attack: TimeSpan.FromMilliseconds(50),
                sustain: TimeSpan.FromMilliseconds(1000),
                release: TimeSpan.FromMilliseconds(1000)))
            {
                FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null);
                BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), filter.OutputFormat, null);
                source.ConnectOutput(filter);
                target.ConnectInput(filter);

                IRandom rand = new FastRandom();
                while (!source.PlaybackFinished)
                {
                    await source.WriteSamplesToOutput(rand.NextInt(1, 480), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await filter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                AudioSample outputSample = target.GetAllAudio();
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(4000), 0.21f);

                for (int ms = 100; ms < 4000; ms += 20)
                {
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.8f, 0, 0.02f);
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.8f, 1, 0.02f);
                }
            }
        }

        [TestMethod]
        public async Task TestAudioGateConstantLoudness()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 0, format.NumChannels, 0.8f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 1, format.NumChannels, 0.8f, 0.5f);

            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioGate filter = new AudioGate(
                new WeakPointer<IAudioGraph>(graph),
                format,
                nodeCustomName: null,
                gateThresholdRmsDba: -40.0f,
                lookAhead: TimeSpan.FromMilliseconds(0),
                attack: TimeSpan.FromMilliseconds(50),
                sustain: TimeSpan.FromMilliseconds(1000),
                release: TimeSpan.FromMilliseconds(1000)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 8544566);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(4000), 0.21f);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 0.0f, 0, 0.02f);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 0.0f, 1, 0.02f);

                // The signal should just be monotonous
                for (int ms = 60; ms < 4000; ms += 20)
                {
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.8f, 0, 0.02f);
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.8f, 1, 0.02f);
                }
            }
        }

        [TestMethod]
        public async Task TestAudioGateConstantLoudnessWithLookahead()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 0, format.NumChannels, 0.8f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, 48000 * 4, 0, 1, format.NumChannels, 0.8f, 0.5f);

            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioGate filter = new AudioGate(
                new WeakPointer<IAudioGraph>(graph),
                format,
                nodeCustomName: null,
                gateThresholdRmsDba: -40.0f,
                lookAhead: TimeSpan.FromMilliseconds(60),
                attack: TimeSpan.FromMilliseconds(50),
                sustain: TimeSpan.FromMilliseconds(1000),
                release: TimeSpan.FromMilliseconds(1000)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 345222);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(4000), 0.21f);
                // The signal should just be monotonous
                for (int ms = 0; ms < 4000; ms += 20)
                {
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.8f, 0, 0.02f);
                    AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(ms), 0.8f, 1, 0.02f);
                }
            }
        }

        //using (Stream outputStream = new FileStream(@"C:\code\Durandal\Data\Test.wav", FileMode.Create, FileAccess.Write))
        //        using (WaveStreamSampleTarget fileOut = await WaveStreamSampleTarget.Create(new WeakPointer<IAudioGraph>(graph), outputStream, format))
        //        {
        //            FixedAudioSampleSource outSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), outputSample);
        //            outSampleSource.ConnectOutput(fileOut);
        //            while (!outSampleSource.PlaybackFinished)
        //            {
        //                await outSampleSource.WriteSamplesToOutput(1000, CancellationToken.None, DefaultRealTimeProvider.Singleton);
        //            }
        //        }
    }
}
