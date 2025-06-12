using Durandal.Common.Audio;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.NAudio
{
    public static class NAudioExtensions
    {
        public static AudioChunkWaveProvider CreateWaveProvider(this AudioChunk chunk, object channelToken = null)
        {
            return new AudioChunkWaveProvider(chunk);
        }

        public static AudioChunkSampleProvider CreateSampleProvider(this AudioChunk chunk, object channelToken = null)
        {
            return new AudioChunkSampleProvider(chunk);
        }

        /*public static void WriteToFile(this AudioChunk input, string filename)
        {
            WriteToFile(input, filename);
        }

        /// <summary>
        /// Writes to an output stream. THE TARGET STREAM MUST SUPPORT SEEK OPERATIONS, BECAUSE RIFF!
        /// </summary>
        /// <param name="output"></param>
        public static void WriteToStream(this AudioChunk input, Stream output)
        {
            WriteToStream(input, output);
        }*/

        public static void WriteToFile(this AudioChunk input, string filename)
        {
            using (WaveFileWriter writer = new WaveFileWriter(filename,
                WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, input.SampleRate, 1, input.SampleRate * 2, 0, 16)))
            {
                int cursor = 0;
                while (cursor < input.Data.Length)
                {
                    int dataToWrite = Math.Min(1024, input.Data.Length - cursor);
                    writer.WriteSamples(input.Data, cursor, dataToWrite);
                    cursor += dataToWrite;
                }
                writer.Flush();
                writer.Close();
            }
        }

        public static void WriteToStream(this AudioChunk input, Stream output)
        {
            using (WaveFileWriter writer = new WaveFileWriter(output,
                WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, input.SampleRate, 1, input.SampleRate * 2, 0, 16)))
            {
                int cursor = 0;
                while (cursor < input.Data.Length)
                {
                    int dataToWrite = Math.Min(1024, input.Data.Length - cursor);
                    writer.WriteSamples(input.Data, cursor, dataToWrite);
                    cursor += dataToWrite;
                }
                writer.Flush();
                writer.Close();
            }
        }

        /// <summary>
        /// Uses WMF resamplers to interpolate audio at a very high quality. This operation can take about 5-20ms
        /// for average-sized audio pieces
        /// </summary>
        /// <param name="input"></param>
        /// <param name="targetSampleRate"></param>
        /// <returns></returns>
        public static AudioChunk ResampleWithWMF(this AudioChunk input, int targetSampleRate)
        {
            if (input.SampleRate == targetSampleRate)
                return input;

            MediaFoundationResampler resampler = new MediaFoundationResampler(input.CreateWaveProvider(), targetSampleRate);
            resampler.ResamplerQuality = 60;
            ISampleProvider provider = resampler.ToSampleProvider();
            int expectedDataLength = (int)((double)input.DataLength * (double)targetSampleRate / (double)input.SampleRate);
            float[] outBuf = new float[expectedDataLength];
            int cursor = 0;
            int samplesRead = 0;
            do
            {
                samplesRead = provider.Read(outBuf, cursor, 1024);
                cursor += samplesRead;
            }
            while (samplesRead > 0 && cursor < outBuf.Length);
            resampler.Dispose();

            short[] returnData = new short[outBuf.Length];
            for (int c = 0; c < returnData.Length; c++)
            {
                returnData[c] = (short)(outBuf[c] * short.MaxValue);
            }
            return new AudioChunk(returnData, targetSampleRate);
        }
    }
}
