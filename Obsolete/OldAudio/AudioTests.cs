using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.AudioV2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Audio.Sampling;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using System.Diagnostics;

namespace Durandal.CoreTests.Common
{
    [TestClass]
    public class AudioTests
    {
        private const string RawAudio = @"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";
        private const string SqrtCompressedAudio = @"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jAUIBwmIiAUEhgcHiAQHh4YIBoeBAoUOC4MHA4SEhYMGBYeFhY+IB4yKA40DC4YDggSDCgsCgQOCCQ2GCAuFAgKFhQeBj4uEBoaICASGhAUEBwKOhIOLCIeHAwQCCg4IBoaGCQMGDwiCjY+Ehg0PhoWFhgYJhgQFjIyOhgoGCYYEg4UECRCKjoSCgwMFhYQGhgWChwiEigYCjIsIEYmGB5EIEAQHh4kDBAaOgQ+ChIcHjY8KDAgJAocFhAICAQYFiY4IDoSGhQQGiQsRhIWFgoqNhIEIBoaMAgiChAUGCQ8Fh4aKi4sMCwiEjYqDBwwNBgKOjoQFgwYFhYWGAQMABQUHBIQBgYUDhoIFi4uEBYGGDgmDCoOJBgeEiYeEBwOOjAoOCggDgQWJhYGFBogFBYsJgYQEBAUDhIMJjIUGhAmFggSEgQWMig4FggeIAYWBBYMHhoQEgQKGA4UCDgqOgwSLDAuFB4ONh4cEDgkDjooHiA4MiI2IAoaFAgODBoIFAw0Mg4eNioaGCAWIBgWHCAOHA4MBiIIKAw4LjYiIioYDAgcKh4gDA4GEBQWKBYmNDAoFBwOKiAqDAgiKiAMKCQKDhgSDgQIDB4SCgoaBBwWHhYQEAoYCBAcHA4WGBwSHhIgGBwYNjI0HhQYFBoOGhAUBh4KCAoIGB4eGBgGBBoKGAQeGgQQABomDAgIGhwEIgYeCAwAGCIiFCYWFhoMHAoKFAgSDhgMEhQQHgwMGhwMFg4aJjIuChAiFCg6FBAMEBYOGhQYGg4UEg4UHgokGgwMHhQUFhYUDBIWEh4MDBAUGBogDBocDBoSGiAgIhAQHhImChAYCgwiHhYEGAIgEBwGIgoKDBQQCggUEBAKFBIaBBIIFhAaFhwYDBAIFgYiFBYKEhIaEhAQIAACChAgGhISFAYaEBwSCBISHBQYCgoaEA4YFBwEBhwQGiIQDBAcGh4IDAwSGAoUGhAIFgwOFAoQCBYIEhYSEAwSGBAGDAwKHBwaGB4WEh4YJhQYIiYQFBIaBBIUEAoEIh4cGhAMI";
        private const string DecompressedAudio = @"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";
        
        /// <summary>
        /// Tests that resampling will alter the length of an audio chunk
        /// </summary>
        //[TestMethod]
        //public void TestResampling()
        //{
        //    AudioChunk audio = new AudioChunk(RawAudio, 16000);
        //    Assert.AreEqual(16000, audio.SampleRate);
        //    Assert.AreEqual(2579, audio.DataLength);
        //    audio = audio.ResampleTo(44100);
        //    Assert.AreEqual(7108, audio.DataLength);
        //    Assert.AreEqual(44100, audio.SampleRate);
        //}

        /// <summary>
        /// Tests that sqrt compression works
        /// </summary>
        [TestMethod]
        public void TestAudioSqrtCodecBasicCompression()
        {
            IAudioCodec codec = new SquareDeltaCodec();
            AudioChunk audio = new AudioChunk(RawAudio, 16000);
            string encodeParams;
            byte[] compressed = AudioUtils.CompressAudioUsingStream(audio, codec, out encodeParams);
            Assert.AreEqual(2583, compressed.Length);
            Assert.IsTrue(string.Equals(SqrtCompressedAudio, Convert.ToBase64String(compressed)));
        }

        [TestMethod]
        public void TestAudioSqrtCodec()
        {
            Assert.IsTrue(SquareDeltaCodec.SelfTest());
        }

        /// <summary>
        /// Tests that sqrt decompression works
        /// </summary>
        [TestMethod]
        public void TestAudioSqrtCodecBasicDecompression()
        {
            IAudioCodec codec = new SquareDeltaCodec();
            AudioChunk audio = AudioUtils.DecompressAudioUsingStream(new ArraySegment<byte>(Convert.FromBase64String(SqrtCompressedAudio)), codec, string.Empty);
            Assert.AreEqual(2579, audio.DataLength);
            Assert.AreEqual(16000, audio.SampleRate);
        }

        /// <summary>
        /// Tests that sqrt compression using a stream works
        /// </summary>
        [TestMethod]
        public void TestAudioSqrtCodecBasicStreamCompression()
        {
            IAudioCodec codec = new SquareDeltaCodec();
            AudioChunk audio = new AudioChunk(RawAudio, 16000);
            IAudioCompressionStream compressor = codec.CreateCompressionStream(audio.SampleRate);
            using (MemoryStream outputBucket = new MemoryStream())
            {
                int cursor = 0;
                while (cursor < audio.DataLength)
                {
                    int toRead = Math.Min(512, audio.DataLength - cursor);
                    short[] chunk = new short[toRead];
                    Array.Copy(audio.Data, cursor, chunk, 0, toRead);
                    cursor += toRead;
                    byte[] output = compressor.Compress(new AudioChunk(chunk, audio.SampleRate));
                    outputBucket.Write(output, 0, output.Length);
                }

                byte[] footer = compressor.Close();
                Assert.IsNull(footer);

                outputBucket.Close();
                byte[] compressedData = outputBucket.ToArray();
                Assert.AreEqual(2583, compressedData.Length);
                Assert.IsTrue(string.Equals(SqrtCompressedAudio, Convert.ToBase64String(compressedData)));
            }
        }

        /// <summary>
        /// Tests that sqrt decompression using a stream works
        /// </summary>
        [TestMethod]
        public void TestAudioSqrtCodecBasicStreamDecompression()
        {
            IAudioCodec codec = new SquareDeltaCodec();
            byte[] compressedData = Convert.FromBase64String(SqrtCompressedAudio);
            IAudioDecompressionStream compressor = codec.CreateDecompressionStream("16000");

            BucketAudioStream audioOut = new BucketAudioStream();
            int sampleRate = 0;
            int cursor = 0;
            while (cursor < compressedData.Length)
            {
                int toRead = Math.Min(512, compressedData.Length - cursor);
                byte[] chunk = new byte[toRead];
                Array.Copy(compressedData, cursor, chunk, 0, toRead);
                cursor += toRead;
                AudioChunk output = compressor.Decompress(new ArraySegment<byte>(chunk));
                audioOut.Write(output.Data);
                sampleRate = output.SampleRate;
                Assert.AreEqual(16000, sampleRate);
            }

            AudioChunk footer = compressor.Close();
            Assert.IsNull(footer);

            AudioChunk finalAudio = new AudioChunk(audioOut.GetAllData(), sampleRate);
            string outputBase64 = finalAudio.GetDataAsBase64();
            Assert.AreEqual(2579, finalAudio.DataLength);
            Assert.IsTrue(string.Equals(DecompressedAudio, outputBase64));
        }

        /// <summary>
        /// Tests that sending wav data over a WaveStreamCompressor works
        /// </summary>
        [TestMethod]
        public void TestAudioWaveStreamCompressor()
        {
            ILogger fakeLogger = new ConsoleLogger();
            PCMCodec codec = new PCMCodec(fakeLogger);
            byte[] inputData = Convert.FromBase64String(RawAudio);
            IAudioCompressionStream compressor = codec.CreateCompressionStream(32000);
            Assert.IsTrue(compressor.GetEncodeParams().Contains("samplerate=32000"));

            using (MemoryStream outputBucket = new MemoryStream())
            {
                int cursor = 0;
                while (cursor < inputData.Length)
                {
                    // Write
                    int toRead = Math.Min(1024, inputData.Length - cursor);
                    byte[] chunk = new byte[toRead];
                    Array.Copy(inputData, cursor, chunk, 0, toRead);
                    AudioChunk inAudio = new AudioChunk(chunk, 32000);
                    byte[] compressed = compressor.Compress(inAudio);
                    outputBucket.Write(compressed, 0, compressed.Length);
                    cursor += toRead;
                }

                Assert.IsNull(compressor.Close());

                outputBucket.Close();

                byte[] outputData = outputBucket.ToArray();
                Assert.AreEqual(inputData.Length, outputData.Length, "Output data should have the same length as input");

                string outputBase64 = Convert.ToBase64String(outputData);
                Assert.IsTrue(string.Equals(RawAudio, outputBase64), "Output data should be identical");
            }
        }

        /// <summary>
        /// Tests that sending wav data over a WaveStreamDecompressor works
        /// </summary>
        [TestMethod]
        public void TestAudioWaveStreamDecompressor()
        {
            ILogger fakeLogger = new ConsoleLogger();
            PCMCodec codec = new PCMCodec(fakeLogger);
            byte[] inputData = Convert.FromBase64String(RawAudio);
            IAudioCompressionStream compressor = codec.CreateCompressionStream(32768);
            IAudioDecompressionStream decompressor = codec.CreateDecompressionStream(compressor.GetEncodeParams());

            IRandom rand = new FastRandom();
            using (MemoryStream outputBucket = new MemoryStream())
            {
                int cursor = 0;
                while (cursor < inputData.Length)
                {
                    // Write
                    int toRead = Math.Min(rand.NextInt(1, 1024), inputData.Length - cursor);
                    byte[] chunk = new byte[toRead];
                    Array.Copy(inputData, cursor, chunk, 0, toRead);
                    AudioChunk decompressed = decompressor.Decompress(new ArraySegment<byte>(chunk));
                    Assert.AreEqual(32768, decompressed.SampleRate, "Output sample rate should match input sample rate");
                    byte[] decompressedBytes = decompressed.GetDataAsBytes();
                    outputBucket.Write(decompressedBytes, 0, decompressedBytes.Length);
                    cursor += toRead;
                }

                Assert.IsNull(decompressor.Close());

                outputBucket.Close();

                byte[] outputData = outputBucket.ToArray();
                Assert.AreEqual(inputData.Length, outputData.Length, "Output data should have the same length as input");

                string outputBase64 = Convert.ToBase64String(outputData);
                Assert.IsTrue(string.Equals(RawAudio, outputBase64), "Output data should be identical");
            }
        }

        /// <summary>
        /// Tests that an AudioTransportStream can read/write audio, using basic sequential logic
        /// </summary>
        [TestMethod]
        public void TestAudioBasicAudioTransport()
        {
            AudioWritePipe stream = new AudioWritePipe();
            byte[] rawAudio = Convert.FromBase64String(RawAudio);
            int cursor = 0;

            while (cursor < rawAudio.Length)
            {
                int toRead = Math.Min(1024, rawAudio.Length - cursor);
                byte[] chunk = new byte[toRead];
                Array.Copy(rawAudio, cursor, chunk, 0, toRead);
                stream.Write(chunk, 0, chunk.Length);
                cursor += toRead;
            }

            stream.CloseWrite();

            byte[] output = new byte[911];
            using (AudioReadPipe readPipe = stream.GetReadPipe())
            {
                using (MemoryStream outputBucket = new MemoryStream())
                {
                    int bytesRead = -1;
                    while (bytesRead != 0)
                    {
                        bytesRead = readPipe.Read(output, 0, output.Length);
                        if (bytesRead > 0)
                        {
                            outputBucket.Write(output, 0, bytesRead);
                        }
                    }

                    outputBucket.Close();

                    string outputBase64 = Convert.ToBase64String(outputBucket.ToArray());
                    Assert.IsTrue(string.Equals(RawAudio, outputBase64));
                }
            }
        }

        /// <summary>
        /// Tests that an AudioTransportStream enforces various byte alignments and doesn't drop any data
        /// </summary>
        [TestMethod]
        public void TestAudioBasicAudioTransportByteAlignment()
        {
            IRandom rand = new FastRandom(20);

            foreach (int maxWriteSize in new int[] { 3000, 500, 3, 2 })
            {
                for (int alignment = 1; alignment <= 4; alignment++)
                {
                    AudioWritePipe stream = new AudioWritePipe(alignment);

                    int audioLength = alignment * 50000;
                    byte[] rawAudio = new byte[audioLength];
                    for (int c = 0; c < audioLength; c++)
                    {
                        rawAudio[c] = (byte)(c % 256);
                    }

                    int cursor = 0;

                    while (cursor < rawAudio.Length)
                    {
                        int toWrite = Math.Min(rand.NextInt(1, maxWriteSize), rawAudio.Length - cursor);
                        byte[] chunk = new byte[toWrite];
                        Array.Copy(rawAudio, cursor, chunk, 0, toWrite);
                        stream.Write(chunk, 0, chunk.Length);
                        cursor += toWrite;
                    }

                    stream.CloseWrite();

                    using (AudioReadPipe readPipe = stream.GetReadPipe())
                    {
                        byte[] output = new byte[audioLength];
                        int outCur = 0;
                        int bytesRead = -1;
                        while (bytesRead != 0 && outCur < audioLength)
                        {
                            int toRead = Math.Min(audioLength - outCur, rand.NextInt(1, 3000));
                            bytesRead = readPipe.Read(output, outCur, toRead);

                            if (bytesRead > 0)
                            {
                                outCur += bytesRead;
                            }
                        }

                        // Assert that every audio byte is exact
                        bool allMatched = true;
                        for (int c = 0; c < audioLength; c++)
                        {
                            allMatched = allMatched && (rawAudio[c] == output[c]);
                        }

                        Assert.IsTrue(allMatched, "Transport stream mismatch for byte alignment = " + alignment);
                    }
                }
            }
        }

        /// <summary>
        /// Tests that an AudioTransportStream can read/write audio, using interleaved (but still synchronous) logic
        /// </summary>
        [TestMethod]
        public void TestAudioBasicAudioTransportInterleaved()
        {
            AudioWritePipe stream = new AudioWritePipe();
            byte[] rawAudio = Convert.FromBase64String(RawAudio);
            int cursor = 0;
            byte[] output = new byte[2048];
            IRandom rand = new FastRandom(100);

            using (MemoryStream outputBucket = new MemoryStream())
            {
                using (AudioReadPipe readPipe = stream.GetReadPipe())
                {
                    while (cursor < rawAudio.Length)
                    {
                        // Write
                        int toWrite = Math.Min(1024, rawAudio.Length - cursor);
                        byte[] chunk = new byte[toWrite];
                        Array.Copy(rawAudio, cursor, chunk, 0, toWrite);
                        stream.Write(chunk, 0, chunk.Length);
                        cursor += toWrite;

                        // Then read back
                        int randomReadSize = rand.NextInt(1, toWrite);
                        int bytesReadBack = readPipe.Read(output, 0, randomReadSize);
                        outputBucket.Write(output, 0, bytesReadBack);
                    }

                    stream.CloseWrite();

                    int bytesRead = -1;
                    while (bytesRead != 0)
                    {
                        bytesRead = readPipe.Read(output, 0, output.Length);
                        if (bytesRead > 0)
                        {
                            outputBucket.Write(output, 0, bytesRead);
                        }
                    }

                    outputBucket.Close();

                    string outputBase64 = Convert.ToBase64String(outputBucket.ToArray());
                    Assert.IsTrue(string.Equals(RawAudio, outputBase64));
                }
            }
        }

        /// <summary>
        /// Tests that an AudioTransportStream can read/write and compress audio, using interleaved (but still synchronous) logic
        /// </summary>
        [TestMethod]
        public void TestAudioCompressedAudioTransport()
        {
            IAudioCodec codec = new SquareDeltaCodec();
            codec.Initialize();
            AudioWritePipe stream = new AudioCompressorPipe(codec, 16000);
            byte[] rawAudio = Convert.FromBase64String(RawAudio);
            int cursor = 0;
            byte[] output = new byte[1024];
            IRandom rand = new FastRandom(200);

            using (MemoryStream outputBucket = new MemoryStream())
            {
                using (AudioReadPipe readPipe = stream.GetReadPipe())
                {
                    while (cursor < rawAudio.Length)
                    {
                        // Write
                        int toWrite = Math.Min(1024, rawAudio.Length - cursor);
                        byte[] chunk = new byte[toWrite];
                        Array.Copy(rawAudio, cursor, chunk, 0, toWrite);
                        stream.Write(chunk, 0, chunk.Length);
                        cursor += toWrite;

                        // Then read back
                        int randomReadSize = rand.NextInt(1, toWrite);
                        int bytesReadBack = readPipe.Read(output, 0, randomReadSize);
                        outputBucket.Write(output, 0, bytesReadBack);
                    }

                    stream.CloseWrite();

                    int bytesRead = -1;
                    while (bytesRead != 0)
                    {
                        bytesRead = readPipe.Read(output, 0, output.Length);

                        if (bytesRead > 0)
                        {
                            outputBucket.Write(output, 0, bytesRead);
                        }
                    }

                    outputBucket.Close();

                    string outputBase64 = Convert.ToBase64String(outputBucket.ToArray());
                    Assert.IsTrue(string.Equals(SqrtCompressedAudio, outputBase64));
                }
            }
        }

        /// <summary>
        /// Tests that an AudioTransportStream can read/write and compress audio, using interleaved asynchoronous logic
        /// </summary>
        [TestMethod]
        public void TestAudioCompressedAudioTransportAsync()
        {
            for (int test = 0; test < 100; test++)
            {
                IAudioCodec codec = new SquareDeltaCodec();
                codec.Initialize();
                AudioWritePipe stream = new AudioCompressorPipe(codec, 16000);
                byte[] rawAudio = Convert.FromBase64String(RawAudio);
                int cursor = 0;
                byte[] output = new byte[1024];
                IRandom rand = new FastRandom();

                Task producerTask = new Task(() =>
                    {
                        while (cursor < rawAudio.Length)
                        {
                            // Write
                            int toWrite = Math.Min(1024, rawAudio.Length - cursor);
                            byte[] chunk = new byte[toWrite];
                            Array.Copy(rawAudio, cursor, chunk, 0, toWrite);
                            stream.Write(chunk, 0, chunk.Length);
                            cursor += toWrite;
                        }

                        stream.CloseWrite();
                    });

                Task<string> consumerTask = new Task<string>(() =>
                    {
                        using (MemoryStream outputBucket = new MemoryStream())
                        {
                            using (AudioReadPipe readPipe = stream.GetReadPipe())
                            {
                                int bytesRead = -1;
                                while (bytesRead != 0)
                                {
                                    int toRead = rand.NextInt(1, output.Length);
                                    bytesRead = readPipe.Read(output, 0, toRead);
                                    if (bytesRead > 0)
                                    {
                                        outputBucket.Write(output, 0, bytesRead);
                                    }
                                }

                                outputBucket.Close();

                                string outputBase64 = Convert.ToBase64String(outputBucket.ToArray());

                                return outputBase64;
                            }
                        }
                    });

                consumerTask.Start();
                producerTask.Start();

                Task.WaitAll(producerTask, consumerTask);

                Assert.IsTrue(string.Equals(SqrtCompressedAudio, consumerTask.Result));
            }
        }

        /// <summary>
        /// Tests that an AudioTransportStream can read/write and decompress audio, using interleaved (but still synchronous) logic
        /// </summary>
        [TestMethod]
        public void TestAudioDecompressedAudioTransport()
        {
            IAudioCodec codec = new SquareDeltaCodec();
            AudioWritePipe stream = new AudioDecompressorPipe(codec, string.Empty);
            byte[] rawAudio = Convert.FromBase64String(SqrtCompressedAudio);
            int cursor = 0;
            byte[] output = new byte[1024];
            IRandom rand = new FastRandom(200);

            using (MemoryStream outputBucket = new MemoryStream())
            {
                using (AudioReadPipe readPipe = stream.GetReadPipe())
                {
                    while (cursor < rawAudio.Length)
                    {
                        // Write
                        int toWrite = Math.Min(1024, rawAudio.Length - cursor);
                        byte[] chunk = new byte[toWrite];
                        Array.Copy(rawAudio, cursor, chunk, 0, toWrite);
                        stream.Write(chunk, 0, chunk.Length);
                        cursor += toWrite;

                        // Then read back
                        int toRead = rand.NextInt(1, toWrite);
                        int bytesReadBack = readPipe.Read(output, 0, toRead);
                        outputBucket.Write(output, 0, bytesReadBack);
                    }

                    stream.CloseWrite();

                    int bytesRead = -1;
                    while (bytesRead != 0)
                    {
                        bytesRead = readPipe.Read(output, 0, output.Length);
                        if (bytesRead > 0)
                        {
                            outputBucket.Write(output, 0, bytesRead);
                        }
                    }

                    outputBucket.Close();

                    string outputBase64 = Convert.ToBase64String(outputBucket.ToArray());
                    Assert.IsTrue(string.Equals(DecompressedAudio, outputBase64));
                }
            }
        }

        /// <summary>
        /// Tests that an AudioTransportStream can read/write and decompress audio, using interleaved asynchoronous logic
        /// </summary>
        [TestMethod]
        public void TestAudioDecompressedAudioTransportAsync()
        {
            for (int test = 0; test < 100; test++)
            {
                IAudioCodec codec = new SquareDeltaCodec();
                AudioWritePipe stream = new AudioDecompressorPipe(codec, string.Empty);
                byte[] rawAudio = Convert.FromBase64String(SqrtCompressedAudio);
                int cursor = 0;
                byte[] output = new byte[1024];
                IRandom rand = new FastRandom();

                Task producerTask = new Task(() =>
                {
                    while (cursor < rawAudio.Length)
                    {
                        // Write
                        int toWrite = Math.Min(1024, rawAudio.Length - cursor);
                        byte[] chunk = new byte[toWrite];
                        Array.Copy(rawAudio, cursor, chunk, 0, toWrite);
                        stream.Write(chunk, 0, chunk.Length);
                        cursor += toWrite;
                    }

                    stream.CloseWrite();
                });

                Task<string> consumerTask = new Task<string>(() =>
                {
                    using (MemoryStream outputBucket = new MemoryStream())
                    {
                        using (AudioReadPipe readPipe = stream.GetReadPipe())
                        {
                            int bytesRead = -1;
                            while (bytesRead != 0)
                            {
                                int toRead = rand.NextInt(1, output.Length);
                                bytesRead = readPipe.Read(output, 0, toRead);
                                if (bytesRead > 0)
                                {
                                    outputBucket.Write(output, 0, bytesRead);
                                }
                            }

                            outputBucket.Close();

                            string outputBase64 = Convert.ToBase64String(outputBucket.ToArray());
                            return outputBase64;
                        }
                    }
                });

                consumerTask.Start();
                producerTask.Start();

                Task.WaitAll(producerTask, consumerTask);

                Assert.IsTrue(string.Equals(DecompressedAudio, consumerTask.Result));
            }
        }

        private class FakeAudioSampleProvider : IAudioSampleProvider
        {
            private readonly float[] _buffer;
            private int _index;

            public FakeAudioSampleProvider(float[] buffer)
            {
                _buffer = buffer;
                _index = 0;
            }

            public Task<int> ReadSamples(float[] target, int offset, int count, IRealTimeProvider realTime)
            {
                int toRead = Math.Min(count, _buffer.Length - _index);
                Array.Copy(_buffer, _index, target, offset, toRead);
                _index += toRead;
                return Task.FromResult(toRead);
            }
        }

        /// <summary>
        /// Tests that the resampling sample provider does not introduce discontinuities into the audio when passing through audio
        /// </summary>
        [TestMethod]
        public async Task TestAudioResamplingSampleProviderPassthrough()
        {
            float[] inputs = new float[100000];
            for (int c = 0; c < inputs.Length; c++)
            {
                inputs[c] = (float)((Math.Sin((float)c / 100f) * 0.4f) + (Math.Sin((float)c / 217f) * 0.4f));
            }
            float[] outputs = new float[100000];

            IAudioSampleProvider provider = new FakeAudioSampleProvider(inputs);
            ResamplingSampleProvider resampler = new ResamplingSampleProvider(provider, new Durandal.Common.Audio.Codecs.Opus.Common.SpeexResampler(1, 16000, 16000, 4), TimeSpan.FromMilliseconds(1000));

            int outCursor = 0;
            IRandom rand = new FastRandom(20);
            while (outCursor < outputs.Length)
            {
                int readSize = rand.NextInt(1, 800);
                int actuallyRead = await resampler.ReadSamples(outputs, outCursor, readSize, DefaultRealTimeProvider.Singleton);
                if (actuallyRead == 0)
                {
                    break;
                }

                outCursor += actuallyRead;
            }

            // Ensure that the output size looks correct
            Assert.AreEqual(inputs.Length, outCursor);

            // Inspect the resampled curve to ensure that there are no gaps or clicks
            float maxDisc = 0;
            for (int c = 1; c < outCursor - 1; c++)
            {
                float disc = Math.Abs(outputs[c] - ((outputs[c + 1] + outputs[c - 1]) / 2f));
                maxDisc = Math.Max(maxDisc, disc);
                Assert.IsTrue(disc < 0.001f, "Discontinuity of " + disc + " is too large");
            }
        }

        /// <summary>
        /// Tests that the resampling sample provider does not introduce discontinuities into the audio when upsampling
        /// </summary>
        [TestMethod]
        public async Task TestAudioResamplingSampleProviderUpsampling()
        {
            float[] inputs = new float[100000];
            for (int c = 0; c < inputs.Length; c++)
            {
                inputs[c] = (float)((Math.Sin((float)c / 100f) * 0.4f) + (Math.Sin((float)c / 217f) * 0.4f));
            }
            float[] outputs = new float[400000];

            IAudioSampleProvider provider = new FakeAudioSampleProvider(inputs);
            ResamplingSampleProvider resampler = new ResamplingSampleProvider(provider, new Durandal.Common.Audio.Codecs.Opus.Common.SpeexResampler(1, 16000, 44100, 4), TimeSpan.FromMilliseconds(1000));

            int outCursor = 0;
            IRandom rand = new FastRandom(20);
            while (outCursor < outputs.Length)
            {
                int readSize = rand.NextInt(1, 800);
                int actuallyRead = await resampler.ReadSamples(outputs, outCursor, readSize, DefaultRealTimeProvider.Singleton);
                if (actuallyRead == 0)
                {
                    break;
                }

                outCursor += actuallyRead;
            }

            // Ensure that the output size looks correct
            int expectedSize = (int)(inputs.LongLength * 44100L / 16000L);
            Assert.IsTrue(Math.Abs(expectedSize - outCursor) < 50);

            // Inspect the resampled curve to ensure that there are no gaps or clicks
            float maxDisc = 0;
            for (int c = 1; c < outCursor - 1; c++)
            {
                float disc = Math.Abs(outputs[c] - ((outputs[c + 1] + outputs[c - 1]) / 2f));
                maxDisc = Math.Max(maxDisc, disc);
                Assert.IsTrue(disc < 0.001f, "Discontinuity of " + disc + " is too large");
            }
        }

        /// <summary>
        /// Tests that the resampling sample provider does not introduce discontinuities into the audio when downsampling
        /// </summary>
        [TestMethod]
        public async Task TestAudioResamplingSampleProviderDownsampling()
        {
            float[] inputs = new float[400000];
            for (int c = 0; c < inputs.Length; c++)
            {
                inputs[c] = (float)((Math.Sin((float)c / 300f) * 0.4f) + (Math.Sin((float)c / 717f) * 0.4f));
            }
            float[] outputs = new float[150000];

            IAudioSampleProvider provider = new FakeAudioSampleProvider(inputs);
            ResamplingSampleProvider resampler = new ResamplingSampleProvider(provider, new Durandal.Common.Audio.Codecs.Opus.Common.SpeexResampler(1, 44100, 16000, 4), TimeSpan.FromMilliseconds(1000));

            int outCursor = 0;
            IRandom rand = new FastRandom(20);
            while (outCursor < outputs.Length)
            {
                int readSize = rand.NextInt(1, 800);
                int actuallyRead = await resampler.ReadSamples(outputs, outCursor, readSize, DefaultRealTimeProvider.Singleton);
                if (actuallyRead == 0)
                {
                    break;
                }

                outCursor += actuallyRead;
            }

            // Ensure that the output size looks correct
            int expectedSize = (int)(inputs.LongLength * 16000L / 44100L);
            Assert.IsTrue(Math.Abs(expectedSize - outCursor) < 50);

            // Inspect the resampled curve to ensure that there are no gaps or clicks
            float maxDisc = 0;
            for (int c = 1; c < outCursor - 1; c++)
            {
                float disc = Math.Abs(outputs[c] - ((outputs[c + 1] + outputs[c - 1]) / 2f));
                maxDisc = Math.Max(maxDisc, disc);
                Assert.IsTrue(disc < 0.002f, "Discontinuity of " + disc + " is too large");
            }
        }
    }
}
