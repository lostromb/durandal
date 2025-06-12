using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Logger;
using System.IO;
using Durandal.Common.Audio.Codecs.Opus.Structs;
using Durandal.Common.Audio.Codecs.Opus.Ogg;
using Durandal.Common.Audio;

namespace Durandal.CoreTests.Common
{
    [TestClass]
    public class AudioCodecTests
    {
        [TestMethod]
        [DeploymentItem("TestData/ThisIsATest.opus")]
        public void TestOpusCodec()
        {
            ILogger logger = new ConsoleLogger();
            
            OpusAudioCodec codec = new OpusAudioCodec(logger);
            BucketAudioStream bucket = new BucketAudioStream();

            // Decode the test file into samples
            using (FileStream audioFileIn = new FileStream("ThisIsATest.opus", FileMode.Open, FileAccess.Read))
            {
                OpusDecoder decoder = new OpusDecoder(16000, 1);
                OpusOggReadStream readStream = new OpusOggReadStream(decoder, audioFileIn);
                while (readStream.HasNextPacket)
                {
                    short[] nextPacket = readStream.DecodeNextPacket();
                    if (nextPacket == null || nextPacket.Length == 0)
                    {
                        continue;
                    }

                    bucket.Write(nextPacket);
                }
            }

            AudioChunk inputAudio = new AudioChunk(bucket.GetAllData(), 16000);
            using (var compressor = codec.CreateCompressionStream(16000))
            {
                string encodeParams;
                byte[] compressedData = AudioUtils.CompressAudioUsingStream(inputAudio, compressor, out encodeParams);

                using (var decompressor = codec.CreateDecompressionStream(encodeParams))
                {
                    AudioChunk outputAudio = AudioUtils.DecompressAudioUsingStream(new ArraySegment<byte>(compressedData), decompressor);

                    Assert.AreEqual(inputAudio.SampleRate, outputAudio.SampleRate);
                    Assert.AreEqual(inputAudio.DataLength, outputAudio.DataLength, 100);
                }
            }
        }

        [TestMethod]
        [DeploymentItem("TestData/ThisIsATest.opus")]
        public void TestILBCCodec()
        {
            ILogger logger = new ConsoleLogger();

            ILBCAudioCodec codec = new ILBCAudioCodec(logger);
            BucketAudioStream bucket = new BucketAudioStream();

            // Decode the test file into samples
            using (FileStream audioFileIn = new FileStream("ThisIsATest.opus", FileMode.Open, FileAccess.Read))
            {
                OpusDecoder decoder = new OpusDecoder(16000, 1);
                OpusOggReadStream readStream = new OpusOggReadStream(decoder, audioFileIn);
                while (readStream.HasNextPacket)
                {
                    short[] nextPacket = readStream.DecodeNextPacket();
                    if (nextPacket == null || nextPacket.Length == 0)
                    {
                        continue;
                    }

                    bucket.Write(nextPacket);
                }
            }

            AudioChunk inputAudio = new AudioChunk(bucket.GetAllData(), 16000);
            using (var compressor = codec.CreateCompressionStream(16000))
            {
                string encodeParams;
                byte[] compressedData = AudioUtils.CompressAudioUsingStream(inputAudio, compressor, out encodeParams);

                using (var decompressor = codec.CreateDecompressionStream(encodeParams))
                {
                    AudioChunk outputAudio = AudioUtils.DecompressAudioUsingStream(new ArraySegment<byte>(compressedData), decompressor);

                    Assert.AreEqual(inputAudio.SampleRate, outputAudio.SampleRate);
                    Assert.AreEqual(inputAudio.DataLength, outputAudio.DataLength, 100);
                }
            }
        }
    }
}
