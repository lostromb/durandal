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
    public class ChannelMixerTests
    {
        [TestMethod]
        public async Task TestAudioChannelMixerMonoPassthrough()
        {
            AudioSampleFormat inputFormat = AudioSampleFormat.Mono(44100);
            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelMixer filter = new ChannelMixer(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, MultiChannelMapping.Monaural, MultiChannelMapping.Monaural, null))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioSampleFormat outputFormat = AudioSampleFormat.Mono(44100);
                float[] expectedOutputSampleData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 0, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioChannelMixerStereoPassthrough()
        {
            AudioSampleFormat inputFormat = AudioSampleFormat.Stereo(44100);
            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 1, inputFormat.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelMixer filter = new ChannelMixer(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, MultiChannelMapping.Stereo_L_R, MultiChannelMapping.Stereo_L_R, null))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioSampleFormat outputFormat = AudioSampleFormat.Stereo(44100);
                float[] expectedOutputSampleData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 0, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 1, outputFormat.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioChannelMixerStereoToMono()
        {
            AudioSampleFormat inputFormat = AudioSampleFormat.Stereo(44100);
            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 920, 44100, 0, 1, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelMixer filter = new ChannelMixer(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, MultiChannelMapping.Stereo_L_R, MultiChannelMapping.Monaural, null))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioSampleFormat outputFormat = AudioSampleFormat.Mono(44100);
                float[] expectedOutputSampleData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 0, outputFormat.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 920, 44100, 0, 0, outputFormat.NumChannels, 0.2f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioChannelMixerMonoToStereo()
        {
            AudioSampleFormat inputFormat = AudioSampleFormat.Mono(44100);
            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelMixer filter = new ChannelMixer(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, MultiChannelMapping.Monaural, MultiChannelMapping.Stereo_L_R, null))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioSampleFormat outputFormat = AudioSampleFormat.Stereo(44100);
                float[] expectedOutputSampleData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 0, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 1, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioChannelMixerStereoChannelSwap()
        {
            AudioSampleFormat inputFormat = new AudioSampleFormat(44100, 2, MultiChannelMapping.Stereo_L_R);
            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 1, inputFormat.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelMixer filter = new ChannelMixer(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, MultiChannelMapping.Stereo_L_R, MultiChannelMapping.Stereo_R_L, null))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioSampleFormat outputFormat = new AudioSampleFormat(44100, 2, MultiChannelMapping.Stereo_R_L);
                float[] expectedOutputSampleData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 1, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 0, outputFormat.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioChannelMixerStereoToPacked2Ch()
        {
            AudioSampleFormat inputFormat = new AudioSampleFormat(44100, 2, MultiChannelMapping.Stereo_L_R);
            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 1, inputFormat.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelMixer filter = new ChannelMixer(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, MultiChannelMapping.Stereo_L_R, MultiChannelMapping.Packed_2Ch, null))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioSampleFormat outputFormat = new AudioSampleFormat(44100, 2, MultiChannelMapping.Packed_2Ch);
                float[] expectedOutputSampleData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 0, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 1, outputFormat.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioChannelMixerPacked2Ch_ToStereo()
        {
            AudioSampleFormat inputFormat = new AudioSampleFormat(44100, 2, MultiChannelMapping.Packed_2Ch);
            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 1, inputFormat.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelMixer filter = new ChannelMixer(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, MultiChannelMapping.Packed_2Ch, MultiChannelMapping.Stereo_L_R, null))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioSampleFormat outputFormat = new AudioSampleFormat(44100, 2, MultiChannelMapping.Stereo_L_R);
                float[] expectedOutputSampleData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 0, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 1, outputFormat.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample);
            }
        }

        [TestMethod]
        public void TestAudioChannelMixerUnsupportedMapping()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            {
                try
                {
                    ChannelMixer filter = new ChannelMixer(new WeakPointer<IAudioGraph>(graph), 48000, MultiChannelMapping.Quadraphonic, MultiChannelMapping.Ambisonic_Ambix_SecondOrder, null);
                    Assert.Fail("Should have thrown a NotImplementedException");
                }
                catch (NotImplementedException) { }
            }
        }
    }
}
