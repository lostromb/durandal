using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
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
    public class VolumeFilterTests
    {
        [TestMethod]
        public async Task TestAudioVolumeFadeOutLinearMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(44100);
            float[] sampleData = new float[44100];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                filter.SetVolumeLinear(0.0f, TimeSpan.FromSeconds(1));
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 1.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.5f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.0f, 0);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioVolumeFadeOutLinearStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[88200];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, 2, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, 2, 1.0f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                filter.SetVolumeLinear(0.0f, TimeSpan.FromSeconds(1));
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 1.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 1.0f, 1);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.5f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.5f, 1);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.0f, 1);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioVolumeFadeOutLogarithmicMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(44100);
            float[] sampleData = new float[44100];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                filter.SetVolumeDecibels(-24.0f, TimeSpan.FromSeconds(1));
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 1.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.25f, 0, 0.05f);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.06f, 0);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioVolumeFadeOutLogarithmicStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[88200];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, 2, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, 2, 1.0f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                filter.SetVolumeDecibels(-24.0f, TimeSpan.FromSeconds(1));
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 1.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 1.0f, 1);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.25f, 0, 0.05f);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.25f, 1, 0.05f);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.06f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.06f, 1);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioVolumeMinimumLogarithmicVolume()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(44100);
            float[] sampleData = new float[44100];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                filter.SetVolumeDecibels(VolumeFilter.MIN_VOLUME_DBA);
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 0.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.0f, 0);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioVolumeLinearAmplificationMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(44100);
            float[] sampleData = new float[44100];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                filter.SetVolumeLinear(8.0f);
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 0.8f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.8f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.8f, 0);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioVolumeLinearAmplificationStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                filter.SetVolumeLinear(8.0f);
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 0.8f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.8f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.8f, 0);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioVolumeLogarithmicAmplificationMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(44100);
            float[] sampleData = new float[44100];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                filter.SetVolumeDecibels(18.0f);
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 0.8f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.8f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.8f, 0);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioVolumeLogarithmicAmplificationStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                filter.SetVolumeDecibels(18.0f);
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 0.8f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.8f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 0.8f, 0);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public void TestAudioVolumeDecibelsNegativeInfinity()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(16000), null))
            {
                filter.SetVolumeDecibels(float.NegativeInfinity);
                Assert.AreEqual(VolumeFilter.MIN_VOLUME_DBA, filter.VolumeDecibels);
            }
        }

        [TestMethod]
        public void TestAudioVolumeDecibelsInfinity()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(16000), null))
            {
                filter.SetVolumeDecibels(float.PositiveInfinity);
                Assert.AreEqual(VolumeFilter.MAX_VOLUME_DBA, filter.VolumeDecibels);
            }
        }

        [TestMethod]
        public void TestAudioVolumeDecibelsNaN()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(16000), null))
            {
                try
                {
                    filter.SetVolumeDecibels(float.NaN);
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        public void TestAudioVolumeLinearNegative()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(16000), null))
            {
                try
                {
                    filter.SetVolumeLinear(-1.0f);
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        public void TestAudioVolumeLinearNaN()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(16000), null))
            {
                try
                {
                    filter.SetVolumeLinear(float.NaN);
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        public void TestAudioVolumeLinearInfinity()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(16000), null))
            {
                try
                {
                    filter.SetVolumeLinear(float.PositiveInfinity);
                    Assert.Fail("Should have thrown a ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        public async Task TestAudioVolumeFadeInDecibelAfterLinearZero()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(44100);
            float[] sampleData = new float[44100];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (VolumeFilter filter = new VolumeFilter(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                filter.SetVolumeLinear(0.0f);
                filter.SetVolumeDecibels(0.0f, TimeSpan.FromSeconds(1));
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(0), 0.0f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(500), 0.015f, 0);
                AudioTestHelpers.AssertPeakVolumeAtPoint(outputSample, TimeSpan.FromMilliseconds(1000), 1.0f, 0, 0.02f);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioVolumeMeterMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            float[] sampleData = new float[48000 * format.NumChannels * 2];
            AudioTestHelpers.GenerateSineWave(sampleData, 48000, 500, 48000, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (PassiveVolumeMeter filter = new PassiveVolumeMeter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(filter);
                filter.ConnectOutput(bucket);

                Assert.AreEqual(0, filter.GetAverageRmsVolume(), 0.01f);
                Assert.AreEqual(0, filter.GetChannelRmsVolume(0), 0.01f);
                Assert.AreEqual(0, filter.GetLoudestRmsVolume(), 0.01f);

                await bucket.ReadSamplesFromInput((int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, TimeSpan.FromMilliseconds(50)), CancellationToken.None, DefaultRealTimeProvider.Singleton);

                Assert.AreEqual(0.707f, filter.GetAverageRmsVolume(), 0.01f);
                Assert.AreEqual(0.707f, filter.GetChannelRmsVolume(0), 0.01f);
                Assert.AreEqual(0.707f, filter.GetLoudestRmsVolume(), 0.01f);

                await bucket.ReadSamplesFromInput((int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, TimeSpan.FromMilliseconds(950)), CancellationToken.None, DefaultRealTimeProvider.Singleton);

                Assert.AreEqual(0.707f, filter.GetAverageRmsVolume(), 0.01f);
                Assert.AreEqual(0.707f, filter.GetChannelRmsVolume(0), 0.01f);
                Assert.AreEqual(0.707f, filter.GetLoudestRmsVolume(), 0.01f);

                await bucket.ReadSamplesFromInput((int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, TimeSpan.FromMilliseconds(500)), CancellationToken.None, DefaultRealTimeProvider.Singleton);

                Assert.AreEqual(0, filter.GetAverageRmsVolume(), 0.01f);
                Assert.AreEqual(0, filter.GetChannelRmsVolume(0), 0.01f);
                Assert.AreEqual(0, filter.GetLoudestRmsVolume(), 0.01f);
            }
        }

        [TestMethod]
        public async Task TestAudioVolumeMeterStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[48000 * format.NumChannels * 2];
            AudioTestHelpers.GenerateSineWave(sampleData, 48000, 500, 48000, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (PassiveVolumeMeter filter = new PassiveVolumeMeter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(filter);
                filter.ConnectOutput(bucket);

                Assert.AreEqual(0, filter.GetAverageRmsVolume(), 0.01f);
                Assert.AreEqual(0, filter.GetChannelRmsVolume(0), 0.01f);
                Assert.AreEqual(0, filter.GetChannelRmsVolume(1), 0.01f);
                Assert.AreEqual(0, filter.GetLoudestRmsVolume(), 0.01f);

                await bucket.ReadSamplesFromInput((int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, TimeSpan.FromMilliseconds(50)), CancellationToken.None, DefaultRealTimeProvider.Singleton);

                Assert.AreEqual(0.353f, filter.GetAverageRmsVolume(), 0.01f);
                Assert.AreEqual(0.707f, filter.GetChannelRmsVolume(0), 0.01f);
                Assert.AreEqual(0, filter.GetChannelRmsVolume(1), 0.01f);
                Assert.AreEqual(0.707f, filter.GetLoudestRmsVolume(), 0.01f);

                await bucket.ReadSamplesFromInput((int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, TimeSpan.FromMilliseconds(950)), CancellationToken.None, DefaultRealTimeProvider.Singleton);

                Assert.AreEqual(0.353f, filter.GetAverageRmsVolume(), 0.01f);
                Assert.AreEqual(0.707f, filter.GetChannelRmsVolume(0), 0.01f);
                Assert.AreEqual(0, filter.GetChannelRmsVolume(1), 0.01f);
                Assert.AreEqual(0.707f, filter.GetLoudestRmsVolume(), 0.01f);

                await bucket.ReadSamplesFromInput((int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, TimeSpan.FromMilliseconds(500)), CancellationToken.None, DefaultRealTimeProvider.Singleton);

                Assert.AreEqual(0, filter.GetAverageRmsVolume(), 0.01f);
                Assert.AreEqual(0, filter.GetChannelRmsVolume(0), 0.01f);
                Assert.AreEqual(0, filter.GetChannelRmsVolume(1), 0.01f);
                Assert.AreEqual(0, filter.GetLoudestRmsVolume(), 0.01f);
            }
        }
    }
}
