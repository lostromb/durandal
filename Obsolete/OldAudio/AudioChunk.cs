using System;
using System.Collections.Generic;

namespace Durandal.Common.Audio
{
    using System.IO;
    using Durandal.Common.Audio.Sampling;
    using Durandal.Common.Audio.Codecs.Opus.Common;
    public class AudioChunk
    {
        /// <summary>
        /// 16-bit PCM samples
        /// </summary>
        public short[] Data;

        /// <summary>
        /// The sample rate, in hertz
        /// </summary>
        public int SampleRate;

        /// <summary>
        /// An optional field describing the actual real time that this chunk was produced. Used for real-time microphone capture and delimiting logic
        /// </summary>
        public long Timestamp;

        /// <summary>
        /// Creates an empty 44.1khz audio sample
        /// </summary>
        public AudioChunk()
        {
            Data = new short[0];
            SampleRate = 44100;
        }

        /// <summary>
        /// Creates a new audio sample from a 2-byte little endian array
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="sampleRate"></param>
        public AudioChunk(byte[] rawData, int sampleRate)
            : this(AudioMath.BytesToShorts(rawData), sampleRate)
        {
        }

        /// <summary>
        /// Creates a new audio sample from a base64-encoded chunk representing a 2-byte little endian array
        /// </summary>
        /// <param name="base64Data"></param>
        /// <param name="sampleRate"></param>
        public AudioChunk(string base64Data, int sampleRate)
            : this(Convert.FromBase64String(base64Data), sampleRate)
        {
        }

        /// <summary>
        /// Creates a new audio sample from a linear set of 16-bit samples
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="sampleRate"></param>
        public AudioChunk(short[] rawData, int sampleRate)
        {
            Data = rawData;
            SampleRate = sampleRate;
        }

        // Old .wav loader code using NAudio wave reader
        // Dropped because it required all wave files to be well-formed, which isn't always the case
        /*public AudioChunk(Stream wavFileStream)
        {
            List<float[]> buffers = new List<float[]>();
            int length = 0;
            using (WaveFileReader reader = new WaveFileReader(wavFileStream))
            {
                SampleRate = reader.WaveFormat.SampleRate;
                while (reader.Position < reader.Length)
                {
                    float[] data = reader.ReadNextSampleFrame();
                    if (data == null)
                        break;
                    length += data.Length;
                    buffers.Add(data);
                }
            }

            Data = new short[length];
            int cursor = 0;
            float scale = (float)(short.MaxValue);
            foreach (float[] chunk in buffers)
            {
                for (int c = 0; c < chunk.Length; c++)
                {
                    Data[cursor + c] = (short)(chunk[c] * scale);
                }
                cursor += chunk.Length;
            }
            wavFileStream.Close();
        }*/

        public byte[] GetDataAsBytes()
        {
            return AudioMath.ShortsToBytes(Data);
        }

        public string GetDataAsBase64()
        {
            return Convert.ToBase64String(GetDataAsBytes());
        }

        public void Stamp(long time = -1)
        {
            if (time > 0)
                Timestamp = time;
            else
                Timestamp = DateTimeOffset.Now.Ticks / 10000;
        }

        /// <summary>
        /// This is very slow
        /// </summary>
        /// <param name="targetSampleRate"></param>
        /// <returns></returns>
        public AudioChunk ResampleTo(int targetSampleRate)
        {
            if (this.SampleRate == targetSampleRate)
                return this;
            SpeexResampler resampler = new SpeexResampler(1, this.SampleRate, targetSampleRate, 2);
            int out_len = (int)((long)this.DataLength * targetSampleRate / this.SampleRate);
            short[] resampledData = new short[out_len];
            int in_len = this.DataLength;
            resampler.Process(0, this.Data, 0, ref in_len, resampledData, 0, ref out_len);
            return new AudioChunk(resampledData, targetSampleRate);
        }

        /// <summary>
        /// This is very bad
        /// </summary>
        /// <param name="targetSampleRate"></param>
        /// <param name="algorithm"></param>
        /// <returns></returns>
        public AudioChunk ResampleToBad(int targetSampleRate, Resampler algorithm = Resampler.MAGIC)
        {
            switch (algorithm)
            {
                case Resampler.LINEAR:
                    return new AudioChunk(Linear.Resample(Data, SampleRate, targetSampleRate), targetSampleRate);
                case Resampler.LANCZOS3:
                    return new AudioChunk(Lanczos.Resample(Data, SampleRate, targetSampleRate), targetSampleRate);
                case Resampler.HERMITIAN:
                    return new AudioChunk(Hermitian.Resample(Data, SampleRate, targetSampleRate), targetSampleRate);
                case Resampler.MAGIC:
                    return new AudioChunk(Magic.Resample(Data, SampleRate, targetSampleRate), targetSampleRate);
                default:
                    return null;
            }
        }

        public AudioChunk Amplify(float amount)
        {
            short[] amplifiedData = new short[DataLength];
            for (int c = 0; c < amplifiedData.Length; c++)
            {
                float newVal = (float)Data[c] * amount;
                if (newVal > short.MaxValue)
                    amplifiedData[c] = short.MaxValue;
                else if (newVal < short.MinValue)
                    amplifiedData[c] = short.MinValue;
                else
                    amplifiedData[c] = (short)newVal;
            }
            return new AudioChunk(amplifiedData, SampleRate);
        }

        public float Peak()
        {
            float highest = 0;
            for (int c = 0; c < Data.Length; c++)
            {
                float test = Math.Abs((float)Data[c]);
                if (test > highest)
                    highest = test;
            }
            return highest;
        }

        /// <summary>
        /// This method is broken because it only accounts for max absolute value of a sample, and because of a bug it maxes out at 0.5 for normal samples.
        /// However, it is still here because the dynamic utterance recorder was trained using evolutionary data that depends on this output,
        /// so I can't change the function without retraining that model.
        /// </summary>
        /// <returns></returns>
        public double VolumeOld()
        {
            double curVolume = 0;
            // No Enumerable.Average function for short values, so do it ourselves
            for (int c = 0; c < Data.Length; c++)
            {
                if (Data[c] == short.MinValue)
                    curVolume += short.MaxValue;
                else
                    curVolume += Math.Abs(Data[c]);
            }
            curVolume /= DataLength;
            return curVolume;
        }

        /// <summary>
        /// Tries to be a little more accurate in volume calculation rather than just instantaneous average.
        /// Returns the volume of this sample in units of RMS decibels relative to max saturation, ranging from -inf to 0
        /// </summary>
        /// <returns></returns>
        public double VolumeDb()
        {
            if (Data.Length == 0)
            {
                return double.NegativeInfinity;
            }
            
            // root mean square calculation
            double ms = 0;
            for (int idx = 0; idx < Data.Length; idx++)
            {
                double val = ((double)Data[idx] / short.MaxValue);
                ms += (val * val);
            }

            double rms = Math.Sqrt(ms / Data.Length);
            
            return Math.Log10(rms) * 10;
        }

        /// <summary>
        /// Calculates peak volume of this audio segment measured in decibels from -inf to 0
        /// </summary>
        /// <returns></returns>
        public double PeakVolumeDb()
        {
            double maxVol = 0;
            int idx = 0;
            while (idx < Data.Length)
            {
                double max = -1;
                double min = 1;
                for (int c = 0; c < 100 && idx < Data.Length; c++)
                {
                    double s = (double)Data[idx] / short.MaxValue;
                    if (s < min)
                        min = s;
                    if (s > max)
                        max = s;
                    idx++;
                }

                double thisVol = (max - min) / 2;
                if (thisVol > maxVol)
                    maxVol = thisVol;
            }

            return Math.Log10(maxVol) * 10;
        }

        public AudioChunk Normalize()
        {
            double volume = Peak();
            return Amplify(short.MaxValue / (float)volume);
        }

        /// <summary>
        /// Returns the total number of samples in this audio segment
        /// </summary>
        public int DataLength
        {
            get
            {
                return Data.Length;
            }
        }

        /// <summary>
        /// Returns the realtime length of this audio segment
        /// </summary>
        public TimeSpan Length
        {
            get
            {
                return TimeSpan.FromMilliseconds((double)Data.Length * 1000 / SampleRate);
            }
        }

        public AudioChunk Concatenate(AudioChunk other)
        {
            AudioChunk toConcatenate = other;
            //if (SampleRate != other.SampleRate)
            //{
            //    toConcatenate = other.ResampleToSlow(SampleRate);
            //}
            int combinedDataLength = DataLength + toConcatenate.DataLength;
            short[] combinedData = new short[combinedDataLength];
            Array.Copy(Data, combinedData, DataLength);
            Array.Copy(toConcatenate.Data, 0, combinedData, DataLength, toConcatenate.DataLength);
            return new AudioChunk(combinedData, SampleRate);
        }
    }
}
