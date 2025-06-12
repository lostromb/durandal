using Durandal.Common.Audio;
using Durandal.Common.Audio.Beamforming;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Components.Noise;
using Durandal.Common.Audio.Test;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    public class ArrayMicrophoneTests
    {
        [TestMethod]
        public async Task TestAudioBasic3DProjectorBarSide()
        {
            AudioSampleFormat inputFormat = AudioSampleFormat.Mono(48000);
            AudioSampleFormat outputFormat = new AudioSampleFormat(48000, 4, MultiChannelMapping.Packed_4Ch);
            ArrayMicrophoneGeometry geometry = new ArrayMicrophoneGeometry(
                new Vector3f[]
                {
                    // theoretical bar microphone. In this test we expect to hear channel 4 first.
                    new Vector3f(-75, 0, 0),
                    new Vector3f(-25, 0, 0),
                    new Vector3f(25, 0, 0),
                    new Vector3f(75, 0, 0),
                });

            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (Basic3DProjector projector = new Basic3DProjector(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, MultiChannelMapping.Packed_4Ch, geometry, null))
            {
                projector.SourcePositionMeters = new Vector3f(1.5f, 0f, 0f);
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(graph, projector, projector, inputSample);

                float[] expectedOutputSampleData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100 - 21, 21, 0, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100 - 14, 14, 1, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100 - 7, 7, 2, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 3, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.99f);
            }
        }

        [TestMethod]
        public async Task TestAudioBasic3DProjectorBarCenter()
        {
            AudioSampleFormat inputFormat = AudioSampleFormat.Mono(48000);
            AudioSampleFormat outputFormat = new AudioSampleFormat(48000, 4, MultiChannelMapping.Packed_4Ch);
            ArrayMicrophoneGeometry geometry = new ArrayMicrophoneGeometry(
                new Vector3f[]
                {
                    // theoretical bar microphone.
                    new Vector3f(-75, 0, 0),
                    new Vector3f(-25, 0, 0),
                    new Vector3f(25, 0, 0),
                    new Vector3f(75, 0, 0),
                });

            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (Basic3DProjector projector = new Basic3DProjector(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, MultiChannelMapping.Packed_4Ch, geometry, null))
            {
                projector.SourcePositionMeters = new Vector3f(0f, 0f, 0f);
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(graph, projector, projector, inputSample);

                float[] expectedOutputSampleData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100 - 7, 7, 0, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 1, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 2, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100 - 7, 7, 3, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.99f);
            }
        }

        [TestMethod]
        public async Task TestAudioBasic3DProjectorRingCenter()
        {
            AudioSampleFormat inputFormat = AudioSampleFormat.Mono(48000);
            AudioSampleFormat outputFormat = new AudioSampleFormat(48000, 4, MultiChannelMapping.Packed_4Ch);
            ArrayMicrophoneGeometry geometry = new ArrayMicrophoneGeometry(
                new Vector3f[]
                {
                    // theoretical ring microphone.
                    new Vector3f(-50, -50, 0),
                    new Vector3f(-50, 50, 0),
                    new Vector3f(50, -50, 0),
                    new Vector3f(50, 50, 0),
                });

            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 44100, 500, 44100, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (Basic3DProjector projector = new Basic3DProjector(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, MultiChannelMapping.Packed_4Ch, geometry, null))
            {
                projector.SourcePositionMeters = new Vector3f(0f, 0f, 0f);
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(graph, projector, projector, inputSample);

                // with a speaker in the center we should hit all channels at once, so no transformation actually happens here
                float[] expectedOutputSampleData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 0, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 1, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 2, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, 44100, 500, 44100, 0, 3, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.99f);
            }
        }

        [Ignore]
        [TestMethod]
        public async Task TestAudioBeamformerBarSide()
        {
            ILogger logger = new ConsoleLogger("AudioTest");
            int sampleRate = 48000;
            AudioSampleFormat inputFormat = AudioSampleFormat.Mono(sampleRate);
            AudioSampleFormat intermediateFormat = new AudioSampleFormat(sampleRate, 4, MultiChannelMapping.Packed_4Ch);
            AudioSampleFormat outputFormat = AudioSampleFormat.Mono(sampleRate);
            ArrayMicrophoneGeometry geometry = new ArrayMicrophoneGeometry(
                new Vector3f[]
                {
                    new Vector3f(-75, 0, 0),
                    new Vector3f(-25, 0, 0),
                    new Vector3f(25, 0, 0),
                    new Vector3f(75, 0, 0),
                }
                //new Tuple<int, int>(0, 2),
                //new Tuple<int, int>(1, 2),
                //new Tuple<int, int>(0, 3)
                );

            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, sampleRate, 500, sampleRate, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (Basic3DProjector interestSignalProjector = new Basic3DProjector(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, intermediateFormat.ChannelMapping, geometry, null))
            using (NoiseSampleSource noiseGenerator = new NoiseSampleSource(new WeakPointer<IAudioGraph>(graph), inputFormat, new WhiteNoiseGenerator(inputFormat, 0.1f, 843451), null))
            //using (SineWaveSampleSource noiseGenerator = new SineWaveSampleSource(graph, inputFormat, null, 2351, 0.3f))
            using (Basic3DProjector noiseSignalProjector = new Basic3DProjector(new WeakPointer<IAudioGraph>(graph), inputFormat.SampleRateHz, intermediateFormat.ChannelMapping, geometry, null))
            using (LinearMixer roomMixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), intermediateFormat, null, false))
            using (BeamFormer beamformer = new BeamFormer(new WeakPointer<IAudioGraph>(graph), logger.Clone("Beamformer"), intermediateFormat.SampleRateHz, intermediateFormat.ChannelMapping, geometry, null, null))
            {
                noiseSignalProjector.SourcePositionMeters = new Vector3f(-1.5f, 0f, 0f);
                interestSignalProjector.SourcePositionMeters = new Vector3f(1.5f, 0f, 0f);
                beamformer.FocusPositionMeters = new Vector3f(1.5f, 0f, 0f);
                noiseGenerator.ConnectOutput(noiseSignalProjector);
                roomMixer.AddInput(interestSignalProjector);
                //roomMixer.AddInput(noiseSignalProjector);
                roomMixer.ConnectOutput(beamformer);
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(graph, interestSignalProjector, beamformer, inputSample, 7763);

                //using (FileStream debugOutStream = new FileStream(@"C:\Code\Durandal\Data\beamform-output.wav", FileMode.Create, FileAccess.Write))
                //{
                //    await AudioHelpers.WriteWaveToStream(outputSample, debugOutStream);
                //}

                float[] expectedOutputSampleData = new float[outputFormat.SampleRateHz * outputFormat.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputSampleData, sampleRate, 500, sampleRate, 0, 0, outputFormat.NumChannels, 0.4f, 0.0f);
                AudioSample expectedOutputSample = new AudioSample(expectedOutputSampleData, outputFormat);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutputSample, outputSample, 0.97f, maxLengthDeviationPerChannel: 1000);
            }
        }

        [TestMethod]
        public void TestAudioMicPairBadConstructorInputs()
        {
            MicPair pair;
            try
            {
                pair = new MicPair(-1, 1, new Vector3f(-10, 0, 0), new Vector3f(10, 0, 0), 10000, 15);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                pair = new MicPair(0, -1, new Vector3f(-10, 0, 0), new Vector3f(10, 0, 0), 10000, 15);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                pair = new MicPair(10, 10, new Vector3f(-10, 0, 0), new Vector3f(10, 0, 0), 10000, 15);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                pair = new MicPair(0, 1, Vector3f.Zero, Vector3f.Zero, 10000, 15);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                pair = new MicPair(0, 1, new Vector3f(-10, 0, 0), new Vector3f(10, 0, 0), 0, 15);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                pair = new MicPair(0, 1, new Vector3f(-10, 0, 0), new Vector3f(10, 0, 0), 10000, 0);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                pair = new MicPair(0, 1, new Vector3f(-10, 0, 0), new Vector3f(10, 0, 0), 10000, 91);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException) { }

            pair = new MicPair(0, 1, new Vector3f(-10, 0, 0), new Vector3f(10, 0, 0), 10000, 90);
        }

        [Ignore]
        [TestMethod]
        public void TestAudioMicPairConstructor()
        {
            MicPair pair = new MicPair(0, 1, new Vector3f(-20, 0, 0), new Vector3f(20, 0, 0), 96000, 10);
            Assert.AreEqual(0, pair.AIndex);
            Assert.AreEqual(1, pair.BIndex);
            Assert.AreEqual(40, pair.ElementSeparationMm);
            Assert.AreEqual(11.19f, pair.ElementSeparationSamples, 0.01f);
            Assert.AreEqual(1, pair.PrimaryAxis.X, 0.01f);
            Assert.AreEqual(0, pair.PrimaryAxis.Y, 0.01f);
            Assert.AreEqual(0, pair.PrimaryAxis.Z, 0.01f);
            Assert.AreEqual(19, pair.VectorAngleOffsets.Count);
            Assert.AreEqual(0.187f, pair.VectorAngleOffsets[0].Item1, 0.01f);
            Assert.AreEqual(11, pair.VectorAngleOffsets[0].Item2);
            Assert.AreEqual(0.466f, pair.VectorAngleOffsets[1].Item1, 0.01f);
            Assert.AreEqual(10, pair.VectorAngleOffsets[1].Item2);
            Assert.AreEqual(0.636f, pair.VectorAngleOffsets[2].Item1, 0.01f);
            Assert.AreEqual(9, pair.VectorAngleOffsets[2].Item2);
            Assert.AreEqual(0.774f, pair.VectorAngleOffsets[3].Item1, 0.01f);
            Assert.AreEqual(8, pair.VectorAngleOffsets[3].Item2);
            Assert.AreEqual(1.005f, pair.VectorAngleOffsets[4].Item1, 0.01f);
            Assert.AreEqual(6, pair.VectorAngleOffsets[4].Item2);
            Assert.AreEqual(1.205f, pair.VectorAngleOffsets[5].Item1, 0.01f);
            Assert.AreEqual(4, pair.VectorAngleOffsets[5].Item2);
            Assert.AreEqual(1.299f, pair.VectorAngleOffsets[6].Item1, 0.01f);
            Assert.AreEqual(3, pair.VectorAngleOffsets[6].Item2);
            Assert.AreEqual(1.391f, pair.VectorAngleOffsets[7].Item1, 0.01f);
            Assert.AreEqual(2, pair.VectorAngleOffsets[7].Item2);
            Assert.AreEqual(1.481f, pair.VectorAngleOffsets[8].Item1, 0.01f);
            Assert.AreEqual(1, pair.VectorAngleOffsets[8].Item2);
            Assert.AreEqual(1.570f, pair.VectorAngleOffsets[9].Item1, 0.01f);
            Assert.AreEqual(0, pair.VectorAngleOffsets[9].Item2);
            Assert.AreEqual(1.660f, pair.VectorAngleOffsets[10].Item1, 0.01f);
            Assert.AreEqual(-1, pair.VectorAngleOffsets[10].Item2);
            Assert.AreEqual(1.750f, pair.VectorAngleOffsets[11].Item1, 0.01f);
            Assert.AreEqual(-2, pair.VectorAngleOffsets[11].Item2);
            Assert.AreEqual(1.842f, pair.VectorAngleOffsets[12].Item1, 0.01f);
            Assert.AreEqual(-3, pair.VectorAngleOffsets[12].Item2);
            Assert.AreEqual(1.936f, pair.VectorAngleOffsets[13].Item1, 0.01f);
            Assert.AreEqual(-4, pair.VectorAngleOffsets[13].Item2);
            Assert.AreEqual(2.136f, pair.VectorAngleOffsets[14].Item1, 0.01f);
            Assert.AreEqual(-6, pair.VectorAngleOffsets[14].Item2);
            Assert.AreEqual(2.366f, pair.VectorAngleOffsets[15].Item1, 0.01f);
            Assert.AreEqual(-8, pair.VectorAngleOffsets[15].Item2);
            Assert.AreEqual(2.504f, pair.VectorAngleOffsets[16].Item1, 0.01f);
            Assert.AreEqual(-9, pair.VectorAngleOffsets[16].Item2);
            Assert.AreEqual(2.675f, pair.VectorAngleOffsets[17].Item1, 0.01f);
            Assert.AreEqual(-10, pair.VectorAngleOffsets[17].Item2);
            Assert.AreEqual(2.954f, pair.VectorAngleOffsets[18].Item1, 0.01f);
            Assert.AreEqual(-11, pair.VectorAngleOffsets[18].Item2);
        }

        [Ignore]
        [TestMethod]
        public void TestAudioMicPairGeneratesHighResolutionVectorAngles()
        {
            MicPair pair = new MicPair(0, 1, new Vector3f(-100, 0, 0), new Vector3f(100, 0, 0), 96000, 9.9f);
            Assert.AreEqual(19, pair.VectorAngleOffsets.Count);
        }

        [Ignore]
        [TestMethod]
        public void TestAudioMicPairGeneratesLowResolutionVectorAngles()
        {
            MicPair pair = new MicPair(0, 1, new Vector3f(-30, 0, 0), new Vector3f(30, 0, 0), 16000, 10);
            Assert.AreEqual(5, pair.VectorAngleOffsets.Count);
            Assert.AreEqual(0.774f, pair.VectorAngleOffsets[0].Item1, 0.01f);
            Assert.AreEqual(2, pair.VectorAngleOffsets[0].Item2);
            Assert.AreEqual(1.205f, pair.VectorAngleOffsets[1].Item1, 0.01f);
            Assert.AreEqual(1, pair.VectorAngleOffsets[1].Item2);
            Assert.AreEqual(1.570f, pair.VectorAngleOffsets[2].Item1, 0.01f);
            Assert.AreEqual(0, pair.VectorAngleOffsets[2].Item2);
            Assert.AreEqual(1.936f, pair.VectorAngleOffsets[3].Item1, 0.01f);
            Assert.AreEqual(-1, pair.VectorAngleOffsets[3].Item2);
            Assert.AreEqual(2.366f, pair.VectorAngleOffsets[4].Item1, 0.01f);
            Assert.AreEqual(-2, pair.VectorAngleOffsets[4].Item2);
        }
    }
}
