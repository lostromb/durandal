using System;
using System.IO;

namespace ManagedBass
{
    /// <summary>
    /// Represents a Wave file format.
    /// </summary>
    public class WaveFormat
    {
        /// <summary>
        /// Creates a new PCM 44.1Khz stereo 16 bit format
        /// </summary>
        public WaveFormat() : this(44100, 16, 2) { }

        /// <summary>
        /// Creates a new 16 bit wave format with the specified sample rate and channel count
        /// </summary>
        /// <param name="SampleRate">Sample Rate</param>
        /// <param name="Channels">Number of channels</param>
        public WaveFormat(int SampleRate, int Channels) : this(SampleRate, 16, Channels) { }

        /// <summary>
        /// Creates a new PCM format with the specified sample rate, bit depth and channels
        /// </summary>
        public WaveFormat(int SampleRate, int BitsPerSample, int Channels)
        {
            if (Channels < 1)
                throw new ArgumentOutOfRangeException("Channels", string.Format("Channels must be 1 or greater"));
            
            Encoding = WaveFormatTag.Pcm;
            this.Channels = Channels;
            this.SampleRate = SampleRate;
            this.BitsPerSample = BitsPerSample;
            ExtraSize = 0;

            BlockAlign = Channels * (BitsPerSample / 8);
            AverageBytesPerSecond = SampleRate * BlockAlign;
        }

        /// <summary>
        /// Creates a new 32 bit IEEE floating point wave format
        /// </summary>
        /// <param name="SampleRate">sample rate</param>
        /// <param name="Channels">number of channels</param>
        public static WaveFormat CreateIeeeFloat(int SampleRate, int Channels)
        {
            return new WaveFormat(SampleRate, 32, Channels)
            {
                Encoding = WaveFormatTag.IeeeFloat
            };
        }

        internal static WaveFormat FromParams(int Frequency, int Channels, Resolution Resolution)
        {
            switch (Resolution)
            {
                case Resolution.Byte:
                    return new WaveFormat(Frequency, 8, Channels);
                    
                case Resolution.Float:
                    return CreateIeeeFloat(Frequency, Channels);

                default:
                    return new WaveFormat(Frequency, Channels);
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="WaveFormat"/> from a <paramref name="Channel"/>.
        /// </summary>
        public static WaveFormat FromChannel(int Channel)
        {
            var info = Bass.ChannelGetInfo(Channel);

            return FromParams(info.Frequency, info.Channels, info.Resolution);
        }

        /// <summary>
        /// Gets or Sets the encoding Type used
        /// </summary>
        public WaveFormatTag Encoding { get; set; }

        /// <summary>
        /// Writes this WaveFormat object to a stream
        /// </summary>
        /// <param name="Writer">the output stream</param>
        public virtual void Serialize(BinaryWriter Writer)
        {
            Writer.Write((short)Encoding);
            Writer.Write((short)Channels);
            Writer.Write(SampleRate);
            Writer.Write(AverageBytesPerSecond);
            Writer.Write((short)BlockAlign);
            Writer.Write((short)BitsPerSample);
            Writer.Write((short)ExtraSize);
        }

        /// <summary>
        /// Gets or Sets the number of channels (1=mono,2=stereo etc)
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// Gets or Sets the sample rate (samples per second)
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Gets or Sets the average number of bytes used per second
        /// </summary>
        public int AverageBytesPerSecond { get; set; }

        /// <summary>
        /// Gets or Sets the block alignment
        /// </summary>
        public int BlockAlign { get; set; }

        /// <summary>
        /// Gets or Sets the number of bits per sample (usually 16 or 32, sometimes 24 or 8)
        /// Can be 0 for some codecs
        /// </summary>
        public int BitsPerSample { get; set; }

        /// <summary>
        /// Gets or Sets the number of extra bytes used by this waveformat.
        /// Often 0, except for compressed formats which store extra data after the WAVEFORMATEX header
        /// </summary>
        public int ExtraSize { get; set; }
    }
}