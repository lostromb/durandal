using Durandal.Common.Audio;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    public static class AudioChunkFactory
    {
        /// <summary>
        /// Creates a new audio sample from a .WAV file stream
        /// </summary>
        /// <param name="wavFileStream"></param>
        public static AudioChunk CreateFromWavStream(Stream wavFileStream, IRealTimeProvider realTime)
        {
            List<short[]> buffers = new List<short[]>();
            int length = 0;
            using (SimpleWaveReader reader = new SimpleWaveReader(wavFileStream, realTime))
            {
                int sampleRate = reader.SampleRate;
                while (!reader.EndOfStream)
                {
                    short[] frame = reader.ReadNextSampleFrame();
                    if (frame == null)
                        break;
                    if (frame.Length == 0)
                        continue;
                    length += frame.Length;
                    buffers.Add(frame);
                }

                short[] data = new short[length];
                int cursor = 0;
                foreach (short[] chunk in buffers)
                {
                    Array.Copy(chunk, 0, data, cursor, chunk.Length);
                    cursor += chunk.Length;
                }

                return new AudioChunk(data, sampleRate);
            }
        }

        /// <summary>
        /// Creates a new audio sample from a raw audio stream
        /// </summary>
        /// <param name="wavFileStream"></param>
        public static AudioChunk CreateFromRawStream(Stream wavFileStream, int sampleRate)
        {
            using (MemoryStream bucket = new MemoryStream())
            {
                wavFileStream.CopyTo(bucket);
                return new AudioChunk(bucket.ToArray(), sampleRate);
            }
        }
    }
}
