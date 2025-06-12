using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
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
    public class ReverbTests
    {
        [TestMethod]
        public async Task TestAudioReverbFilterPerfectEcho()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 5];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 10, 4800, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 10, 4800, 0, 1, format.NumChannels, 1.0f, 0.5f);

            float[] expectedOutput = new float[format.SampleRateHz * format.NumChannels * 5];
            for (int second = 0; second < 5; second++)
            {
                AudioTestHelpers.GenerateSineWave(expectedOutput, format.SampleRateHz, 10, 4800, second * 48000, 0, format.NumChannels, 1.0f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutput, format.SampleRateHz, 10, 4800, second * 48000, 1, format.NumChannels, 1.0f, 0.5f);
            }

            AudioSample inputSample = new AudioSample(sampleData, format);
            AudioSample expectedOutputSample = new AudioSample(expectedOutput, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ReverbFilter filter = new ReverbFilter(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(1000), 1.0f, 1.0f))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 6545);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(outputSample, expectedOutputSample, 0.99f);
            }
        }

        [TestMethod]
        public async Task TestAudioReverbFilterImperfectReflectivity()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float reflectivity = 0.5f;
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 5];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 10, 4800, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 10, 4800, 0, 1, format.NumChannels, 1.0f, 0.5f);

            float[] expectedOutput = new float[format.SampleRateHz * format.NumChannels * 5];
            float amplitude = 1.0f;
            for (int second = 0; second < 5; second++)
            {
                AudioTestHelpers.GenerateSineWave(expectedOutput, format.SampleRateHz, 10, 4800, second * 48000, 0, format.NumChannels, amplitude, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutput, format.SampleRateHz, 10, 4800, second * 48000, 1, format.NumChannels, amplitude, 0.5f);
                amplitude *= reflectivity;
            }

            AudioSample inputSample = new AudioSample(sampleData, format);
            AudioSample expectedOutputSample = new AudioSample(expectedOutput, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ReverbFilter filter = new ReverbFilter(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(1000), reflectivity, 1.0f))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 36544);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(outputSample, expectedOutputSample, 0.99f);
            }
        }

        [TestMethod]
        public async Task TestAudioReverbFilterImperfectHardness()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels * 5];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 100, 4800, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 100, 4800, 0, 1, format.NumChannels, 1.0f, 0.5f);

            float[] expectedOutput = new float[format.SampleRateHz * format.NumChannels * 5];
            for (int second = 0; second < 5; second++)
            {
                AudioTestHelpers.GenerateSineWave(expectedOutput, format.SampleRateHz, 100, 4800, second * 48000, 0, format.NumChannels, 1.0f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutput, format.SampleRateHz, 100, 4800, second * 48000, 1, format.NumChannels, 1.0f, 0.5f);
            }

            AudioSample inputSample = new AudioSample(sampleData, format);
            AudioSample expectedOutputSample = new AudioSample(expectedOutput, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ReverbFilter filter = new ReverbFilter(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(1000), 1.0f, 0.2f))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 974344);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(outputSample, expectedOutputSample, 0.93f);
            }
        }
    }
}
