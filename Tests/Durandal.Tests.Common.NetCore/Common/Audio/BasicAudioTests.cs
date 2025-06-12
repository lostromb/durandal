using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Events;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Test;
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
    public class BasicAudioTests
    {
        [TestMethod]
        public void TestAudioSourceCantConnectIfSampleRateDoesntMatch()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), new AudioSample(new float[0], AudioSampleFormat.Mono(48000)), null))
            using (PassthroughAudioPipe target = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(44100), null))
            {
                try
                {
                    source.ConnectOutput(target);
                    Assert.Fail("Should have thrown a FormatException");
                }
                catch (FormatException) { }
            }
        }

        [TestMethod]
        public void TestAudioSourceCantConnectIfChannelCountDoesntMatch()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), new AudioSample(new float[0], AudioSampleFormat.Mono(48000)), null))
            using (PassthroughAudioPipe target = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Stereo(48000), null))
            {
                try
                {
                    source.ConnectOutput(target);
                    Assert.Fail("Should have thrown a FormatException");
                }
                catch (FormatException) { }
            }
        }

        [TestMethod]
        public void TestAudioTargetCantConnectIfSampleRateDoesntMatch()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe source = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(44100), null))
            {
                try
                {
                    source.ConnectOutput(target);
                    Assert.Fail("Should have thrown a FormatException");
                }
                catch (FormatException) { }
            }
        }

        [TestMethod]
        public void TestAudioTargetCantConnectIfChannelCountDoesntMatch()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe source = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Stereo(48000), null))
            {
                try
                {
                    source.ConnectOutput(target);
                    Assert.Fail("Should have thrown a FormatException");
                }
                catch (FormatException) { }
            }
        }
        
        [TestMethod]
        public void TestAudioSourceCanConnectToTargetIfTargetAlreadyConnectedElsewhere()
        {
            AudioSample inputSample = new AudioSample(new float[0], AudioSampleFormat.Mono(48000));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source1 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (FixedAudioSampleSource source2 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                target.ConnectInput(source1);
                source2.ConnectOutput(target);
                Assert.AreEqual(null, source1.Output);
                Assert.AreEqual(source2, target.Input);
                Assert.AreEqual(target, source2.Output);
                source1.DisconnectOutput();
                source2.DisconnectOutput();
                target.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioSourceCanConnectToTargetIfSourceAlreadyConnectedElsewhere()
        {
            AudioSample inputSample = new AudioSample(new float[0], AudioSampleFormat.Mono(48000));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                source.ConnectOutput(target1);
                source.ConnectOutput(target2);
                Assert.AreEqual(target2, source.Output);
                Assert.AreEqual(source, target2.Input);
                Assert.AreEqual(null, target1.Input);
                source.DisconnectOutput();
                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioFilterCanConnectToTargetIfTargetAlreadyConnectedElsewhere()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe source1 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (PassthroughAudioPipe source2 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                target.ConnectInput(source1);
                source2.ConnectOutput(target);
                Assert.AreEqual(null, source1.Output);
                Assert.AreEqual(source2, target.Input);
                Assert.AreEqual(target, source2.Output);
                source1.DisconnectOutput();
                source2.DisconnectOutput();
                target.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioFilterCanConnectToTargetIfFilterAlreadyConnectedElsewhere()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe source = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                source.ConnectOutput(target1);
                source.ConnectOutput(target2);
                Assert.AreEqual(target2, source.Output);
                Assert.AreEqual(source, target2.Input);
                Assert.AreEqual(null, target1.Input);
                source.DisconnectOutput();
                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioSourceCanConnectToFilterIfFilterAlreadyConnectedElsewhere()
        {
            AudioSample inputSample = new AudioSample(new float[0], AudioSampleFormat.Mono(48000));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source1 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (FixedAudioSampleSource source2 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (PassthroughAudioPipe target = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                target.ConnectInput(source1);
                source2.ConnectOutput(target);
                Assert.AreEqual(null, source1.Output);
                Assert.AreEqual(source2, target.Input);
                Assert.AreEqual(target, source2.Output);
                source1.DisconnectOutput();
                source2.DisconnectOutput();
                target.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioSourceCanConnectToFilterIfSourceAlreadyConnectedElsewhere()
        {
            AudioSample inputSample = new AudioSample(new float[0], AudioSampleFormat.Mono(48000));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (PassthroughAudioPipe target1 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (PassthroughAudioPipe target2 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                source.ConnectOutput(target1);
                source.ConnectOutput(target2);
                Assert.AreEqual(target2, source.Output);
                Assert.AreEqual(source, target2.Input);
                Assert.AreEqual(null, target1.Input);
                source.DisconnectOutput();
                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioFilterCanConnectToFilterIfInputFilterAlreadyConnectedElsewhere()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe source1 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (PassthroughAudioPipe source2 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (PassthroughAudioPipe target = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                target.ConnectInput(source1);
                source2.ConnectOutput(target);
                Assert.AreEqual(null, source1.Output);
                Assert.AreEqual(source2, target.Input);
                Assert.AreEqual(target, source2.Output);
                source1.DisconnectOutput();
                source2.DisconnectOutput();
                target.DisconnectInput();
            }
        }
        
        [TestMethod]
        public void TestAudioTargetCanConnectToSourceIfTargetAlreadyConnectedElsewhere()
        {
            AudioSample inputSample = new AudioSample(new float[0], AudioSampleFormat.Mono(48000));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source1 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (FixedAudioSampleSource source2 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                target.ConnectInput(source1);
                target.ConnectInput(source2);
                Assert.AreEqual(null, source1.Output);
                Assert.AreEqual(source2, target.Input);
                Assert.AreEqual(target, source2.Output);
                source1.DisconnectOutput();
                source2.DisconnectOutput();
                target.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioTargetCanConnectToSourceIfSourceAlreadyConnectedElsewhere()
        {
            AudioSample inputSample = new AudioSample(new float[0], AudioSampleFormat.Mono(48000));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (PassthroughAudioPipe target1 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (PassthroughAudioPipe target2 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                source.ConnectOutput(target1);
                target2.ConnectInput(source);
                Assert.AreEqual(target2, source.Output);
                Assert.AreEqual(source, target2.Input);
                Assert.AreEqual(null, target1.Input);
                source.DisconnectOutput();
                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioTargetCanConnectToFilterIfFilterAlreadyConnectedElsewhere()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe source = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                source.ConnectOutput(target1);
                target2.ConnectInput(source);
                Assert.AreEqual(target2, source.Output);
                Assert.AreEqual(source, target2.Input);
                Assert.AreEqual(null, target1.Input);
                source.DisconnectOutput();
                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioTargetCanConnectToFilterfTargetAlreadyConnectedElsewhere()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe source1 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (PassthroughAudioPipe source2 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                target.ConnectInput(source1);
                target.ConnectInput(source2);
                Assert.AreEqual(null, source1.Output);
                Assert.AreEqual(source2, target.Input);
                Assert.AreEqual(target, source2.Output);
                source1.DisconnectOutput();
                source2.DisconnectOutput();
                target.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioFilterCanConnectToSourceIfFilterAlreadyConnectedElsewhere()
        {
            AudioSample inputSample = new AudioSample(new float[0], AudioSampleFormat.Mono(48000));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source1 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (FixedAudioSampleSource source2 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (PassthroughAudioPipe target = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                target.ConnectInput(source1);
                target.ConnectInput(source2);
                Assert.AreEqual(null, source1.Output);
                Assert.AreEqual(source2, target.Input);
                Assert.AreEqual(target, source2.Output);
                source1.DisconnectOutput();
                source2.DisconnectOutput();
                target.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioFilterCanConnectToSourceIfSourceAlreadyConnectedElsewhere()
        {
            AudioSample inputSample = new AudioSample(new float[0], AudioSampleFormat.Mono(48000));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (PassthroughAudioPipe target1 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (PassthroughAudioPipe target2 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                source.ConnectOutput(target1);
                target2.ConnectInput(source);
                Assert.AreEqual(target2, source.Output);
                Assert.AreEqual(source, target2.Input);
                Assert.AreEqual(null, target1.Input);
                source.DisconnectOutput();
                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }
        
        [TestMethod]
        public async Task TestAudioFilterCantReconnectInputAfterPlaybackFinishes()
        {
            AudioSample inputSample = new AudioSample(new float[0], AudioSampleFormat.Mono(48000));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source1 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (SilenceAudioSampleSource source2 = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (PassthroughAudioPipe filter = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                source1.ConnectOutput(filter);
                source2.ConnectOutput(filter);
                filter.ConnectInput(source1);
                filter.ConnectOutput(target);
                Assert.AreEqual(-1, await target.ReadSamplesFromInput(1, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                try
                {
                    filter.ConnectInput(source2);
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }

                try
                {
                    source2.ConnectOutput(filter);
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }

                filter.DisconnectInput();
                // Assert that we still return -1 playback finished even after the source is disconnected
                Assert.AreEqual(-1, await target.ReadSamplesFromInput(1, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                source1.DisconnectOutput();
                source2.DisconnectOutput();
                filter.DisconnectInput();
                filter.DisconnectOutput();
            }
        }

        [TestMethod]
        public void TestAudioAudioSampleFormatNullComparison()
        {
            try
            {
                AudioSampleFormat.AssertFormatsAreEqual(null, null);
                Assert.Fail("Should have thrown a ArgumentNullException");
            }
            catch (ArgumentNullException) { }

            try
            {
                AudioSampleFormat.AssertFormatsAreEqual(AudioSampleFormat.Mono(16000), null);
                Assert.Fail("Should have thrown a ArgumentNullException");
            }
            catch (ArgumentNullException) { }

            try
            {
                AudioSampleFormat.AssertFormatsAreEqual(null, AudioSampleFormat.Mono(16000));
                Assert.Fail("Should have thrown a ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestAudioAudioSampleFormatComparisonDifferentSampleRate()
        {
            try
            {
                AudioSampleFormat.AssertFormatsAreEqual(AudioSampleFormat.Mono(16000), AudioSampleFormat.Mono(32000));
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void TestAudioAudioSampleFormatComparisonDifferentChannelCount()
        {
            try
            {
                AudioSampleFormat.AssertFormatsAreEqual(AudioSampleFormat.Mono(16000), AudioSampleFormat.Stereo(16000));
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void TestAudioAudioSampleFormatComparisonDifferentChannelLayout()
        {
            try
            {
                AudioSampleFormat.AssertFormatsAreEqual(new AudioSampleFormat(16000, 2, MultiChannelMapping.Stereo_L_R), new AudioSampleFormat(16000, 2, MultiChannelMapping.Stereo_R_L));
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void TestAudioAudioSampleFormatComparisonEquality()
        {
            AudioSampleFormat.AssertFormatsAreEqual(AudioSampleFormat.Mono(16000), AudioSampleFormat.Mono(16000));
            AudioSampleFormat.AssertFormatsAreEqual(AudioSampleFormat.Mono(48000), AudioSampleFormat.Mono(48000));
            AudioSampleFormat.AssertFormatsAreEqual(AudioSampleFormat.Stereo(16000), AudioSampleFormat.Stereo(16000));
            AudioSampleFormat.AssertFormatsAreEqual(AudioSampleFormat.Stereo(48000), AudioSampleFormat.Stereo(48000));
            AudioSampleFormat.AssertFormatsAreEqual(new AudioSampleFormat(16000, 2, MultiChannelMapping.Stereo_L_R), new AudioSampleFormat(16000, 2, MultiChannelMapping.Stereo_L_R));
            Assert.AreEqual(AudioSampleFormat.Mono(16000).GetHashCode(), AudioSampleFormat.Mono(16000).GetHashCode());
            Assert.AreNotEqual(AudioSampleFormat.Mono(32000).GetHashCode(), AudioSampleFormat.Mono(16000).GetHashCode());
            Assert.AreNotEqual(AudioSampleFormat.Stereo(16000).GetHashCode(), AudioSampleFormat.Mono(16000).GetHashCode());
            Assert.AreNotEqual(new AudioSampleFormat(16000, 2, MultiChannelMapping.Stereo_L_R).GetHashCode(), new AudioSampleFormat(16000, 2, MultiChannelMapping.Stereo_R_L).GetHashCode());
        }

        [TestMethod]
        public void TestAudioAudioSampleFormatCreatePacked()
        {
            for (int channels = 1; channels <= 12; channels++)
            {
                AudioSampleFormat format = AudioSampleFormat.Packed(48000, channels);
                Assert.IsNotNull(format);
                Assert.AreEqual(48000, format.SampleRateHz);
                Assert.AreEqual(channels, format.NumChannels);
                if (channels > 1)
                {
                    Assert.IsTrue(AudioSampleFormat.IsPackedChannelLayout(format.ChannelMapping));
                }
            }

            try
            {
                AudioSampleFormat.Packed(48000, 0);
                Assert.Fail("Expected an ArgumentOutOfRangeException");
            } catch (ArgumentOutOfRangeException) { }

            try
            {
                AudioSampleFormat.Packed(48000, 13);
                Assert.Fail("Expected an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void TestAudioAudioSampleFormatGetChannelMapping51()
        {
            AudioSampleFormat format = new AudioSampleFormat(48000, MultiChannelMapping.Surround_5_1ch);
            IReadOnlyCollection<SpeakerLocation> mapping = format.GetSpeakerMapping();
            Assert.IsNotNull(mapping);
            Assert.AreEqual(format.NumChannels, mapping.Count);
            Assert.AreEqual(0, format.GetChannelIndexForSpeaker(SpeakerLocation.FrontLeft));
            Assert.AreEqual(1, format.GetChannelIndexForSpeaker(SpeakerLocation.FrontRight));
            Assert.AreEqual(2, format.GetChannelIndexForSpeaker(SpeakerLocation.FrontCenter));
            Assert.AreEqual(3, format.GetChannelIndexForSpeaker(SpeakerLocation.LowFrequency));
            Assert.AreEqual(4, format.GetChannelIndexForSpeaker(SpeakerLocation.LeftRear));
            Assert.AreEqual(5, format.GetChannelIndexForSpeaker(SpeakerLocation.RightRear));
        }

        [TestMethod]
        public void TestAudioAudioSampleFormatGetChannelMappingPacked()
        {
            AudioSampleFormat format = new AudioSampleFormat(48000, MultiChannelMapping.Packed_3Ch);
            IReadOnlyCollection<SpeakerLocation> mapping = format.GetSpeakerMapping();
            Assert.IsNotNull(mapping);
            Assert.AreEqual(format.NumChannels, mapping.Count);
            foreach (var loc in mapping)
            {
                Assert.AreEqual(SpeakerLocation.Unknown, loc);
            }

            Assert.AreEqual(-1, format.GetChannelIndexForSpeaker(SpeakerLocation.FrontLeft));
            Assert.AreEqual(-1, format.GetChannelIndexForSpeaker(SpeakerLocation.FrontRight));
            Assert.AreEqual(-1, format.GetChannelIndexForSpeaker(SpeakerLocation.FrontCenter));
        }

        [TestMethod]
        public async Task TestAudioPipeReadsZeroIfInputDisconnected()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe pipe = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                target.ConnectInput(pipe);
                int samplesRead = await target.ReadSamplesFromInput(4800, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(0, samplesRead);
                AudioSample output = target.GetAllAudio();
                Assert.AreEqual(0, output.LengthSamplesPerChannel);
                target.DisconnectInput();
            }
        }

        [TestMethod]
        public async Task TestAudioPipeMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(44100);
            float[] sampleData = new float[44100];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe filter = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioPipeStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[88200];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 1.0f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe filter = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioPositionMeasuringPipeMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 500, 1.0f))
            using (PositionMeasuringAudioPipe filter = new PositionMeasuringAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(filter);
                filter.ConnectOutput(target);
                int readSize = await target.ReadSamplesFromInput(480, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(480, readSize);
                Assert.AreEqual(TimeSpan.FromMilliseconds(10), filter.Position);
                readSize = await target.ReadSamplesFromInput(480, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(480, readSize);
                Assert.AreEqual(TimeSpan.FromMilliseconds(20), filter.Position);
                await source.WriteSamplesToOutput(480, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(TimeSpan.FromMilliseconds(30), filter.Position);
                await source.WriteSamplesToOutput(480, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(TimeSpan.FromMilliseconds(40), filter.Position);
            }
        }

        [TestMethod]
        public async Task TestAudioPositionMeasuringPipeStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 500, 1.0f))
            using (PositionMeasuringAudioPipe filter = new PositionMeasuringAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(filter);
                filter.ConnectOutput(target);
                int readSize = await target.ReadSamplesFromInput(480, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(480, readSize);
                Assert.AreEqual(TimeSpan.FromMilliseconds(10), filter.Position);
                readSize = await target.ReadSamplesFromInput(480, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(480, readSize);
                Assert.AreEqual(TimeSpan.FromMilliseconds(20), filter.Position);
                await source.WriteSamplesToOutput(480, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(TimeSpan.FromMilliseconds(30), filter.Position);
                await source.WriteSamplesToOutput(480, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(TimeSpan.FromMilliseconds(40), filter.Position);
            }
        }

        [TestMethod]
        public async Task TestAudioBucketLargeRead()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            int sampleLengthPerChannel = 10 * format.SampleRateHz;
            float[] sampleData = new float[sampleLengthPerChannel * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, sampleLengthPerChannel, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, sampleLengthPerChannel, 0, 1, format.NumChannels, 1.0f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                sampleSource.ConnectOutput(bucket);
                int actualReadSize = await bucket.ReadSamplesFromInput(sampleLengthPerChannel, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(sampleLengthPerChannel, actualReadSize);
                actualReadSize = await bucket.ReadSamplesFromInput(sampleLengthPerChannel, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(-1, actualReadSize);
                AudioSample capturedSample = bucket.GetAllAudio();
                AudioTestHelpers.AssertAudioSignalIsContinuous(capturedSample);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, capturedSample);
            }
        }

        [TestMethod]
        public async Task TestAudioBucketLargeWrite()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            int sampleLengthPerChannel = 10 * format.SampleRateHz;
            float[] sampleData = new float[sampleLengthPerChannel * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, sampleLengthPerChannel, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, sampleLengthPerChannel, 0, 1, format.NumChannels, 1.0f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                sampleSource.ConnectOutput(bucket);
                int actualWriteSize = await sampleSource.WriteSamplesToOutput(sampleLengthPerChannel, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(sampleLengthPerChannel, actualWriteSize);
                AudioSample capturedSample = bucket.GetAllAudio();
                AudioTestHelpers.AssertAudioSignalIsContinuous(capturedSample);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, capturedSample);
            }
        }

        [TestMethod]
        public async Task TestAudioBucketRandomReads()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            IRandom rand = new FastRandom(964345);
            int sampleLengthPerChannel = 10 * format.SampleRateHz;
            float[] sampleData = new float[sampleLengthPerChannel * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, sampleLengthPerChannel, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, sampleLengthPerChannel, 0, 1, format.NumChannels, 1.0f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                sampleSource.ConnectOutput(bucket);
                int samplesPerChannelActuallyRead = 0;
                while (samplesPerChannelActuallyRead < sampleLengthPerChannel)
                {
                    int thisReadMaxSize = Math.Min(sampleLengthPerChannel - samplesPerChannelActuallyRead, rand.NextInt(4000, 100000));
                    int thisReadSize = await sampleSource.WriteSamplesToOutput(thisReadMaxSize, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(thisReadSize > 0);
                    samplesPerChannelActuallyRead += thisReadSize;
                }

                AudioSample capturedSample = bucket.GetAllAudio();
                AudioTestHelpers.AssertAudioSignalIsContinuous(capturedSample);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, capturedSample);
            }
        }

        [TestMethod]
        public async Task TestAudioBucketRandomWrites()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            IRandom rand = new FastRandom(163289);
            int sampleLengthPerChannel = 10 * format.SampleRateHz;
            float[] sampleData = new float[sampleLengthPerChannel * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, sampleLengthPerChannel, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 500, sampleLengthPerChannel, 0, 1, format.NumChannels, 1.0f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                sampleSource.ConnectOutput(bucket);
                int samplesPerChannelActuallyWritten = 0;
                while (samplesPerChannelActuallyWritten < sampleLengthPerChannel)
                {
                    int thisWriteSize = Math.Min(sampleLengthPerChannel - samplesPerChannelActuallyWritten, rand.NextInt(4000, 100000));
                    samplesPerChannelActuallyWritten += await sampleSource.WriteSamplesToOutput(thisWriteSize, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                }

                AudioSample capturedSample = bucket.GetAllAudio();
                AudioTestHelpers.AssertAudioSignalIsContinuous(capturedSample);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, capturedSample);
            }
        }

        [TestMethod]
        public async Task TestAudioBlockRectifierMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(44100);
            float[] sampleData = new float[44100];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioBlockRectifier filter = new AudioBlockRectifier(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(5)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioBlockRectifierStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[88200];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 1.0f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioBlockRectifier filter = new AudioBlockRectifier(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(5)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 182312);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioBlockRectifierBufferMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(44100);
            float[] sampleData = new float[44100];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioBlockRectifierBuffer filter = new AudioBlockRectifierBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(5)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioBlockRectifierBufferStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[88200];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 1.0f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioBlockRectifierBuffer filter = new AudioBlockRectifierBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(5)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 182312);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }
        [TestMethod]
        public async Task TestAudioBlockFillerMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(44100);
            float[] sampleData = new float[44100];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioBlockFiller filter = new AudioBlockFiller(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioBlockFillerStereo()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[88200];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 1.0f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioBlockFiller filter = new AudioBlockFiller(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 182312);
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioExceptionCircuitBreakerPull()
        {
            EventOnlyLogger logger = new EventOnlyLogger();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            EventRecorder<EventArgs> recorder = new EventRecorder<EventArgs>();
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ExceptionalAudioSampleSource exceptionSource = new ExceptionalAudioSampleSource(new WeakPointer<IAudioGraph>(graph), format))
            using (AudioExceptionCircuitBreaker circuitBreaker = new AudioExceptionCircuitBreaker(new WeakPointer<IAudioGraph>(graph), format, null, logger.Clone("CircuitBreaker")))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                exceptionSource.ConnectOutput(circuitBreaker);
                circuitBreaker.ConnectOutput(target);
                circuitBreaker.ExceptionRaisedEvent.Subscribe(recorder.HandleEventAsync);
                int samplesRead = await target.ReadSamplesFromInput(1, CancellationToken.None, realTime);
                Assert.AreEqual(-1, samplesRead);
                RetrieveResult<CapturedEvent<EventArgs>> rr = await recorder.WaitForEvent(CancellationToken.None, realTime, TimeSpan.FromMilliseconds(10));
                Assert.IsTrue(rr.Success);

                Assert.IsTrue(logger.History.FilterByCriteria(new FilterCriteria()
                    {
                        Level = LogLevel.Err
                    }).Any());

                samplesRead = await target.ReadSamplesFromInput(1, CancellationToken.None, realTime);
                Assert.AreEqual(-1, samplesRead);
            }
        }

        // Sample source which throws an exception
        private class ExceptionalAudioSampleSource : AbstractAudioSampleSource
        {
            public override bool PlaybackFinished => false;

            public ExceptionalAudioSampleSource(WeakPointer<IAudioGraph> graph, AudioSampleFormat format) : base(graph, nameof(ExceptionalAudioSampleSource), nodeCustomName: null)
            {
                OutputFormat = format;
            }

            protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        [TestMethod]
        public async Task TestAudioExceptionCircuitBreakerPush()
        {
            EventOnlyLogger logger = new EventOnlyLogger();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            EventRecorder<EventArgs> recorder = new EventRecorder<EventArgs>();
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 1.0f))
            using (AudioExceptionCircuitBreaker circuitBreaker = new AudioExceptionCircuitBreaker(new WeakPointer<IAudioGraph>(graph), format, null, logger.Clone("CircuitBreaker")))
            using (ExceptionalAudioSampleTarget exceptionTarget = new ExceptionalAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format))
            {
                source.ConnectOutput(circuitBreaker);
                circuitBreaker.ConnectOutput(exceptionTarget);
                circuitBreaker.ExceptionRaisedEvent.Subscribe(recorder.HandleEventAsync);
                await source.WriteSamplesToOutput(1, CancellationToken.None, realTime);
                RetrieveResult<CapturedEvent<EventArgs>> rr = await recorder.WaitForEvent(CancellationToken.None, realTime, TimeSpan.FromMilliseconds(10));
                Assert.IsTrue(rr.Success);

                Assert.IsTrue(logger.History.FilterByCriteria(new FilterCriteria()
                {
                    Level = LogLevel.Err
                }).Any());

                await source.WriteSamplesToOutput(1, CancellationToken.None, realTime);
            }
        }

        // Sample target which throws an exception
        private class ExceptionalAudioSampleTarget : AbstractAudioSampleTarget
        {
            public ExceptionalAudioSampleTarget(WeakPointer<IAudioGraph> graph, AudioSampleFormat format) : base(graph, nameof(ExceptionalAudioSampleTarget), nodeCustomName: null)
            {
                InputFormat = format;
            }

            protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new ArgumentOutOfRangeException();
            }
        }
    }
}
