using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    public class AudioSample
    {
        private readonly int _hashCode;

        public ArraySegment<float> Data { get; private set; }

        public AudioSampleFormat Format { get; private set; }

        public int LengthSamplesPerChannel
        {
            get
            {
                return Data.Count / Format.NumChannels;
            }
        }

        public TimeSpan Duration
        {
            get
            {
                return AudioMath.ConvertSamplesPerChannelToTimeSpan(Format.SampleRateHz, (long)Data.Count / Format.NumChannels);
            }
        }

        public AudioSample(float[] data, AudioSampleFormat format)
            : this (new ArraySegment<float>(data), format)
        {
        }

        public AudioSample(ArraySegment<float> data, AudioSampleFormat format)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (data.Count % format.NumChannels != 0)
            {
                throw new ArgumentException("Audio sample data contains partial samples (data length not an even multiple of channel count)");
            }

            Data = data;
            Format = format;

            // Calculate the hash code of the entire input data. This will be used for equality comparison
            _hashCode = 0;
            int idx = data.Offset;
            for (int c = 0; c < data.Count; c++)
            {
                _hashCode ^= data.Array[idx++].GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format("(AudioSample, {0} samples ({1:F3} seconds), {2}", LengthSamplesPerChannel, Duration.TotalSeconds, Format.ToString());
        }

        /// <summary>
        /// Equality comparison is based on deep equality of audio data, using the data's hash code as an approximator (for speed)
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            
            AudioSample other = (AudioSample)obj;
            return _hashCode == other._hashCode;
        }
        
        public override int GetHashCode()
        {
            return _hashCode;
        }

        /// <summary>
        /// Returns the volume of this sample in units of RMS decibels relative to max saturation, ranging from -inf to 0
        /// </summary>
        /// <returns></returns>
        public double VolumeDb()
        {
            if (Data.Count == 0)
            {
                return double.NegativeInfinity;
            }

            // root mean square calculation
            double ms = 0;
            for (int idx = 0; idx < Data.Count; idx++)
            {
                double val = ((double)Data.Array[Data.Offset + idx] / short.MaxValue);
                ms += (val * val);
            }

            double rms = Math.Sqrt(ms / Data.Count);

            return Math.Log10(rms) * 10;
        }
    }
}
