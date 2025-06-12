using Durandal.Common.Time;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Defines the format of a stream or buffer of audio samples.
    /// </summary>
    public class AudioSampleFormat : IEquatable<AudioSampleFormat>
    {
        private static readonly IReadOnlyDictionary<MultiChannelMapping, SpeakerLocation[]> SPEAKER_LOCATIONS = new Dictionary<MultiChannelMapping, SpeakerLocation[]>()
        {
            #region A big speaker mapping table
            { MultiChannelMapping.Monaural, new SpeakerLocation[] {
                SpeakerLocation.FrontCenter } },
            { MultiChannelMapping.Stereo_L_R, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontRight } },
            { MultiChannelMapping.Stereo_R_L, new SpeakerLocation[] {
                SpeakerLocation.FrontRight, SpeakerLocation.FrontLeft } },
            { MultiChannelMapping.LeftCenterRight, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontCenter, SpeakerLocation.FrontRight } },
            { MultiChannelMapping.LeftRightCenter, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontRight, SpeakerLocation.FrontCenter } },
            { MultiChannelMapping.Quadraphonic, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontRight,
                SpeakerLocation.LeftRear, SpeakerLocation.RightRear,} },
            { MultiChannelMapping.Quadraphonic_rear, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontRight,
                SpeakerLocation.FrontCenter, SpeakerLocation.RearCenter,} },
            { MultiChannelMapping.Quadraphonic_side, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontRight,
                SpeakerLocation.LeftSide, SpeakerLocation.RightSide,} },
            { MultiChannelMapping.Surround_5ch, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontRight,
                SpeakerLocation.FrontCenter,
                SpeakerLocation.LeftRear, SpeakerLocation.RightRear } },
            { MultiChannelMapping.Surround_5_1ch, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontRight,
                SpeakerLocation.FrontCenter, SpeakerLocation.LowFrequency,
                SpeakerLocation.LeftRear, SpeakerLocation.RightRear } },
            { MultiChannelMapping.Surround_5_1ch_side, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontRight,
                SpeakerLocation.FrontCenter, SpeakerLocation.LowFrequency,
                SpeakerLocation.LeftSide, SpeakerLocation.RightSide } },
            { MultiChannelMapping.Surround_5_1ch_Vorbis_Layout, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontCenter,
                SpeakerLocation.FrontRight, SpeakerLocation.LeftRear,
                SpeakerLocation.RightRear, SpeakerLocation.LowFrequency } },
            { MultiChannelMapping.Surround_5_1ch_side_Vorbis_Layout, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontCenter,
                SpeakerLocation.FrontRight, SpeakerLocation.LeftSide,
                SpeakerLocation.RightSide, SpeakerLocation.LowFrequency } },
            // Can't actually find a definitive layout for this since it would be mainly used for DTS, which
            // apparently has different flavors of 6.1 for DTS-ES
            //{ MultiChannelMapping.Surround_6_1ch, new SpeakerLocation[] {
            //    SpeakerLocation.FrontLeft, SpeakerLocation.FrontRight,
            //    SpeakerLocation.FrontCenter, SpeakerLocation.LowFrequency1,
            //    SpeakerLocation.LeftRear, SpeakerLocation.RightRear } },
            { MultiChannelMapping.Surround_7_1ch, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontRight,
                SpeakerLocation.FrontCenter, SpeakerLocation.LowFrequency,
                SpeakerLocation.LeftRear, SpeakerLocation.RightRear,
                SpeakerLocation.FrontLeftWide, SpeakerLocation.FrontRightWide} },
            { MultiChannelMapping.Surround_7_1ch_side, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontRight,
                SpeakerLocation.FrontCenter, SpeakerLocation.LowFrequency,
                SpeakerLocation.LeftRear, SpeakerLocation.RightRear,
                SpeakerLocation.LeftSide, SpeakerLocation.RightSide} },
            { MultiChannelMapping.Surround_7_1ch_Vorbis_Layout, new SpeakerLocation[] {
                SpeakerLocation.FrontLeft, SpeakerLocation.FrontCenter,
                SpeakerLocation.FrontRight, SpeakerLocation.LeftRear,
                SpeakerLocation.RightRear, SpeakerLocation.FrontLeftWide,
                SpeakerLocation.FrontRightWide, SpeakerLocation.LowFrequency} },
            { MultiChannelMapping.Ambisonic_Ambix_FirstOrder_PlusNonDiagenicStereo, new SpeakerLocation[] {
                SpeakerLocation.Unknown, SpeakerLocation.Unknown,
                SpeakerLocation.Unknown, SpeakerLocation.Unknown,
                SpeakerLocation.LeftInEar, SpeakerLocation.RightInEar} },
            #endregion
        };

        /// <summary>
        /// Constructs a new instance of the <see cref="AudioSampleFormat"/> class.
        /// </summary>
        /// <param name="samplesPerSecond">The sample rate in hertz</param>
        /// <param name="channelMapping">The channel mapping family. Channel count will be derived from this.</param>
        public AudioSampleFormat(int samplesPerSecond, MultiChannelMapping channelMapping) : this(samplesPerSecond, GetNumChannelsForLayout(channelMapping), channelMapping)
        {
        }

        /// <summary>
        /// Constructs a new instance of the <see cref="AudioSampleFormat"/> class.
        /// </summary>
        /// <param name="samplesPerSecond">The sample rate in hertz</param>
        /// <param name="numChannels">The total number of channels (default monaural)</param>
        /// <param name="channelMapping">The channel mapping family (default monaural)</param>
        public AudioSampleFormat(int samplesPerSecond, int numChannels = 1, MultiChannelMapping channelMapping = MultiChannelMapping.Monaural)
        {
            if (samplesPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(samplesPerSecond));
            }

            if (numChannels <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numChannels));
            }

            if (channelMapping != MultiChannelMapping.Unknown && numChannels != GetNumChannelsForLayout(channelMapping))
            {
                throw new ArgumentOutOfRangeException("Channel count " + numChannels + " does not match the number required by channel mapping " + channelMapping);
            }

            SampleRateHz = samplesPerSecond;
            NumChannels = numChannels;
            ChannelMapping = channelMapping;
        }

        /// <summary>
        /// The sample rate in hertz or samples per second.
        /// This is samples <i>per channel</i>, per second, just to be clear.
        /// </summary>
        public int SampleRateHz { get; private set; }

        /// <summary>
        /// The number of channels.
        /// </summary>
        public int NumChannels { get; private set; }

        /// <summary>
        /// The convention used for interpreting multiple channels into some spacial arrangement, such as surround sound.
        /// </summary>
        public MultiChannelMapping ChannelMapping { get; private set; }

        public bool Equals(AudioSampleFormat other)
        {
            return SampleRateHz == other.SampleRateHz &&
                NumChannels == other.NumChannels &&
                ChannelMapping == other.ChannelMapping;
        }
        
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return Equals((AudioSampleFormat)obj);
        }
        
        public override int GetHashCode()
        {
            return (4234 * SampleRateHz.GetHashCode()) ^
                (91241 * NumChannels.GetHashCode()) ^
                (235 * ChannelMapping.GetHashCode());
        }

        public override string ToString()
        {
            return string.Format("{0} channel(s) {1}hz {2}",
                NumChannels,
                SampleRateHz,
                ChannelMapping.ToString());
        }

        /// <summary>
        /// Gets the speaker mapping for this sample format, which maps from
        /// channel indexes to physical speaker locations.
        /// </summary>
        /// <returns>A collection of speaker locations with entry count
        /// equal to number of channels in this format.</returns>
        public IReadOnlyCollection<SpeakerLocation> GetSpeakerMapping()
        {
            SpeakerLocation[] locations;
            if (!SPEAKER_LOCATIONS.TryGetValue(ChannelMapping, out locations))
            {
                locations = new SpeakerLocation[NumChannels];
                locations.AsSpan().Fill(SpeakerLocation.Unknown);
            }

            return locations;
        }

        /// <summary>
        /// Given a speaker location, find the channel index for that speaker's location within
        /// this mapping, or -1 if not found.
        /// </summary>
        /// <param name="location">The speaker location to check for.</param>
        /// <returns>The channel index for that speaker, or -1 if not found.</returns>
        public sbyte GetChannelIndexForSpeaker(SpeakerLocation location)
        {
            SpeakerLocation[] locations;
            if (!SPEAKER_LOCATIONS.TryGetValue(ChannelMapping, out locations))
            {
                return -1;
            }

            for (sbyte idx = 0; idx < locations.Length; idx++)
            {
                if (locations[idx] == location)
                {
                    return idx;
                }
            }

            return -1;
        }

        public static void AssertFormatsAreEqual(AudioSampleFormat input, AudioSampleFormat output)
        {
            if (input == null && output == null)
            {
                throw new ArgumentNullException("Audio sample format mismatch: Input/Output formats are both null");
            }
            else if (input == null)
            {
                throw new ArgumentNullException("Audio sample format mismatch: Input format is null");
            }
            else if (output == null)
            {
                throw new ArgumentNullException("Audio sample format mismatch: Output format is null");
            }
            else if (!input.Equals(output))
            {
                throw new FormatException("Audio sample format mismatch: Input=" + input.ToString() + ", Output=" + output.ToString());
            }
        }

        public static AudioSampleFormat Mono(int sampleRate)
        {
            return new AudioSampleFormat(sampleRate, 1, MultiChannelMapping.Monaural);
        }

        public static AudioSampleFormat Stereo(int sampleRate)
        {
            return new AudioSampleFormat(sampleRate, 2, MultiChannelMapping.Stereo_L_R);
        }

        public static AudioSampleFormat Packed(int sampleRate, int numChannels)
        {
            switch (numChannels)
            {
                case 1:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Monaural);
                case 2:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Packed_2Ch);
                case 3:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Packed_3Ch);
                case 4:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Packed_4Ch);
                case 5:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Packed_5Ch);
                case 6:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Packed_6Ch);
                case 7:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Packed_7Ch);
                case 8:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Packed_8Ch);
                case 9:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Packed_9Ch);
                case 10:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Packed_10Ch);
                case 11:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Packed_11Ch);
                case 12:
                    return new AudioSampleFormat(sampleRate, numChannels, MultiChannelMapping.Packed_12Ch);
                default:
                    throw new ArgumentOutOfRangeException(nameof(numChannels), $"Unsupported packed channel count {numChannels}");
            }
        }

        public static int GetNumChannelsForLayout(MultiChannelMapping layout)
        {
            switch (layout)
            {
                case MultiChannelMapping.Monaural:
                    return 1;
                case MultiChannelMapping.Stereo_L_R:
                case MultiChannelMapping.Stereo_R_L:
                case MultiChannelMapping.Packed_2Ch:
                    return 2;
                case MultiChannelMapping.Packed_3Ch:
                    return 3;
                case MultiChannelMapping.Ambisonic_Ambix_FirstOrder:
                case MultiChannelMapping.Quadraphonic:
                case MultiChannelMapping.Packed_4Ch:
                    return 4;
                case MultiChannelMapping.Surround_5ch:
                case MultiChannelMapping.Surround_5ch_Vorbis_Layout:
                case MultiChannelMapping.Packed_5Ch:
                    return 5;
                case MultiChannelMapping.Surround_5_1ch:
                case MultiChannelMapping.Surround_5_1ch_Vorbis_Layout:
                case MultiChannelMapping.Surround_5_1ch_side:
                case MultiChannelMapping.Surround_5_1ch_side_Vorbis_Layout:
                case MultiChannelMapping.Ambisonic_Ambix_FirstOrder_PlusNonDiagenicStereo:
                case MultiChannelMapping.Packed_6Ch:
                    return 6;
                case MultiChannelMapping.Packed_7Ch:
                case MultiChannelMapping.Surround_6_1ch:
                    return 7;
                case MultiChannelMapping.Packed_8Ch:
                case MultiChannelMapping.Surround_7_1ch:
                case MultiChannelMapping.Surround_7_1ch_Vorbis_Layout:
                    return 8;
                case MultiChannelMapping.Packed_9Ch:
                case MultiChannelMapping.Ambisonic_Ambix_SecondOrder:
                    return 9;
                case MultiChannelMapping.Packed_10Ch:
                    return 10;
                case MultiChannelMapping.Packed_11Ch:
                    return 11;
                case MultiChannelMapping.Packed_12Ch:
                    return 12;
                case MultiChannelMapping.Ambisonic_Ambix_ThirdOrder:
                    return 16;
                default:
                    throw new ArgumentException("The channel mapping " + layout.ToString() + " is unknown");
            }
        }

        /// <summary>
        /// Determines if the given channel layout is a packed format; that is, one containing multiple channels with no explicit relation between them.
        /// </summary>
        /// <param name="layout">The layout to check.</param>
        /// <returns>True if the given layout is a packed format.</returns>
        public static bool IsPackedChannelLayout(MultiChannelMapping layout)
        {
            return layout >= MultiChannelMapping.Packed_2Ch && layout <= MultiChannelMapping.Packed_12Ch;
        }
    }
}
