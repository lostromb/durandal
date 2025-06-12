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
    public class BiquadFilterTests
    {
        // for writing new tests
        public async Task TestAudioBiquadFilter()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 40, format.SampleRateHz, 0, 0, format.NumChannels, 0.2f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.2f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (BiquadFilter filter = new PeakFilter(new WeakPointer<IAudioGraph>(graph), format, null, 2000, 10, 0.2f))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 7655);

                using (FileStream outStream = new FileStream(@"C:\Code\Durandal\Data\test.wav", FileMode.Create, FileAccess.Write))
                using (NonRealTimeStreamWrapper outNrtStream = new NonRealTimeStreamWrapper(outStream, false))
                using (AudioEncoder waveEncoder = new RiffWaveEncoder(new WeakPointer<IAudioGraph>(graph), format, null, NullLogger.Singleton))
                using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), outputSample, null))
                {
                    await waveEncoder.Initialize(outNrtStream, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    sampleSource.ConnectOutput(waveEncoder);
                    await sampleSource.WriteFully(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await waveEncoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample, 0.2f);

                float[] expectedOutputData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 40, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.75f);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.6f, 0.5f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.95f);
            }
        }

        [TestMethod]
        public async Task TestAudioLowpassFilterMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 40, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (BiquadFilter filter = new LowPassFilter(new WeakPointer<IAudioGraph>(graph), format, null, 500, 0.2f))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 7655);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                float[] expectedOutputData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 40, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.95f);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.05f, 0.5f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.95f);
            }
        }

        [TestMethod]
        public async Task TestAudioLowpassFilterStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 40, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 1, format.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (BiquadFilter filter = new LowPassFilter(new WeakPointer<IAudioGraph>(graph), format, null, 500, 0.2f))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 7655);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                float[] expectedOutputData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 40, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.95f);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.05f, 0.5f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.95f);
            }
        }

        [TestMethod]
        public async Task TestAudioHighpassFilterMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 40, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (BiquadFilter filter = new HighPassFilter(new WeakPointer<IAudioGraph>(graph), format, null, 500, 0.2f))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 7655);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                float[] expectedOutputData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.26f, 0.6f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.95f);
            }
        }

        [TestMethod]
        public async Task TestAudioHighpassFilterStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 40, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 40, format.SampleRateHz, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (BiquadFilter filter = new HighPassFilter(new WeakPointer<IAudioGraph>(graph), format, null, 500, 0.2f))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 7655);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                float[] expectedOutputData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.26f, 0.6f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.95f);
            }
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(8)]
        [DataRow(9)]
        [DataRow(11)]
        public async Task TestAudioHighpassFilterPackedChannels(int numChannels)
        {
            AudioSampleFormat format = AudioSampleFormat.Packed(48000, numChannels);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            for (int chan = 0; chan < format.NumChannels; chan++)
            {
                AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 40, format.SampleRateHz, 0, chan, format.NumChannels, 0.4f, 0.0f);
            }

            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (BiquadFilter filter = new HighPassFilter(new WeakPointer<IAudioGraph>(graph), format, null, 500, 0.2f))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 7655);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                float[] expectedOutputData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 2000, format.SampleRateHz, 0, 0, format.NumChannels, 0.26f, 0.6f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.95f);
            }
        }
    }
}
