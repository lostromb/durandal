using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// A reference to a common convention for expressing multichannel audio layouts,
    /// in other words, the mapping from audio channel index to spacial or logical positioning.
    /// 99% of the time this will be either <see cref="Monaural"/>  or <see cref="Stereo_L_R"/>.
    /// </summary>
    public enum MultiChannelMapping : ushort
    {
        // vorbis info:
        //https://ffmpeg.org/doxygen/6.0/channel__layout_8h_source.html

        /// <summary>
        /// Multichannel audio in an unknown or undefined format.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Mono (1-channel) audio.
        /// </summary>
        Monaural = 1,

        /// <summary>
        /// Simple stereo audio, with L-R channel order.
        /// </summary>
        Stereo_L_R = 2,

        /// <summary>
        /// "Inverse" stereo audio, with R-L channel order.
        /// </summary>
        Stereo_R_L = 3,

        /// <summary>
        /// 1-d surround with left, right, and center channels.
        /// </summary>
        LeftRightCenter = 10,

        /// <summary>
        /// 1-d surround with left, center, and right channels (vorbis layout)
        /// </summary>
        LeftCenterRight = 11,

        /// <summary>
        /// Quadraphonic surround sound - front left, front right, left rear, right rear.
        /// </summary>
        Quadraphonic = 40,

        /// <summary>
        /// Quadraphonic with side channels - front left, front right, left side, right side.
        /// </summary>
        Quadraphonic_side = 41,

        /// <summary>
        /// 4 channels, 3 in front and one rear (FL, FR, FC, R). Confusingly called "surround" in legacy winMM.
        /// </summary>
        Quadraphonic_rear = 42,

        /// <summary>
        /// 5-channel surround. FrontLeft, FrontRight, FrontCenter, RearLeft, RearRight
        /// </summary>
        Surround_5ch = 50,

        /// <summary>
        /// 5-channel surround with Vorbis/Opus speaker layout. FrontLeft, FrontRight, FrontCenter, RearLeft, RearRight
        /// The channel order is FrontLeft, FrontCenter, FrontRight, LeftRear, RightRear
        /// </summary>
        Surround_5ch_Vorbis_Layout = 51,

        /// <summary>
        /// 5-channel surround with LFE ("5.1") with AAC speaker layout (more common).
        /// The channel order is FrontLeft, FrontRight, FrontCenter, LFE, LeftRear, RightRear
        /// </summary>
        Surround_5_1ch = 53,

        /// <summary>
        /// 5-channel surround with LFE ("5.1") with Vorbis/Opus speaker layout.
        /// The channel order is FrontLeft, FrontCenter, FrontRight, LeftRear, RightRear, LFE
        /// </summary>
        Surround_5_1ch_Vorbis_Layout = 54,

        /// <summary>
        /// 5.1 channel surround but designating "side" instead of "rear" channels.
        /// The channel order is FrontLeft, FrontRight, FrontCenter, LFE, LeftSide, RightSide
        /// </summary>
        Surround_5_1ch_side = 56,

        /// <summary>
        /// 5.1 channel surround but designating "side" instead of "rear" channels.
        /// The channel order is FrontLeft, FrontCenter, FrontRight, LeftSide, RightSide, LFE
        /// </summary>
        Surround_5_1ch_side_Vorbis_Layout = 57,

        /// <summary>
        /// 6.1 channel surround.
        /// </summary>
        Surround_6_1ch = 61,

        /// <summary>
        /// 7.1 channel surround with AAC speaker layout.
        /// The channel order is FrontLeft, FrontRight, FrontCenter, LFE, BackLeft, BackRight, FrontLeftOfCenter, FrontRightOfCenter
        /// </summary>
        Surround_7_1ch = 71,

        /// <summary>
        /// 7.1 channel surround with Vorbis/Opus speaker layout.
        /// The channel order is FrontLeft, FrontCenter, FrontRight, RearLeft, RearRight, FrontLeftOfCenter, RightLeftOfCenter, LFE
        /// </summary>
        Surround_7_1ch_Vorbis_Layout = 72,

        /// <summary>
        /// 7.1 channel surround with AAC speaker layout and "side" instead of "left of center / right of center" channels
        /// The channel order is FrontLeft, FrontRight, FrontCenter, LFE, BackLeft, BackRight, LeftSide, RightSide
        /// </summary>
        Surround_7_1ch_side = 73,

        /// <summary>
        /// Packed 2-channel format, with no layout specified. 
        /// Typically used to represent a pair of monaural signals which are transmitted or processed at the same time.
        /// </summary>
        Packed_2Ch = 202,

        /// <summary>
        /// Packed 3-channel format, with no layout specified. 
        /// Can represent output from an array microphone or soundbar, in a case where exact information of the channel layout
        /// is provided separately.
        /// </summary>
        Packed_3Ch = 203,

        /// <summary>
        /// Packed 4-channel format, with no layout specified. 
        /// Can represent output from an array microphone or soundbar, in a case where exact information of the channel layout
        /// is provided separately.
        /// </summary>
        Packed_4Ch = 204,

        /// <summary>
        /// Packed 5-channel format, with no layout specified. 
        /// Can represent output from an array microphone or soundbar, in a case where exact information of the channel layout
        /// is provided separately.
        /// </summary>
        Packed_5Ch = 205,

        /// <summary>
        /// Packed 6-channel format, with no layout specified. 
        /// Can represent output from an array microphone or soundbar, in a case where exact information of the channel layout
        /// is provided separately.
        /// </summary>
        Packed_6Ch = 206,

        /// <summary>
        /// Packed 7-channel format, with no layout specified. 
        /// Can represent output from an array microphone or soundbar, in a case where exact information of the channel layout
        /// is provided separately.
        /// </summary>
        Packed_7Ch = 207,

        /// <summary>
        /// Packed 8-channel format, with no layout specified. 
        /// Can represent output from an array microphone or soundbar, in a case where exact information of the channel layout
        /// is provided separately.
        /// </summary>
        Packed_8Ch = 208,

        /// <summary>
        /// Packed 9-channel format, with no layout specified. 
        /// Can represent output from an array microphone or soundbar, in a case where exact information of the channel layout
        /// is provided separately.
        /// </summary>
        Packed_9Ch = 209,

        /// <summary>
        /// Packed 10-channel format, with no layout specified. 
        /// Can represent output from an array microphone or soundbar, in a case where exact information of the channel layout
        /// is provided separately.
        /// </summary>
        Packed_10Ch = 210,

        /// <summary>
        /// Packed 11-channel format, with no layout specified. 
        /// Can represent output from an array microphone or soundbar, in a case where exact information of the channel layout
        /// is provided separately.
        /// </summary>
        Packed_11Ch = 211,

        /// <summary>
        /// Packed 12-channel format, with no layout specified. 
        /// Can represent output from an array microphone or soundbar, in a case where exact information of the channel layout
        /// is provided separately.
        /// </summary>
        Packed_12Ch = 212,

        /// <summary>
        /// Ambisonics 4 channel (first-order B-signal) input in Ambix format (ACN channel ordering / SN3D normalization)
        /// </summary>
        Ambisonic_Ambix_FirstOrder = 1000,

        /// <summary>
        /// Ambisonics 9 channel (second-order B-signal) input in Ambix format (ACN channel ordering / SN3D normalization)
        /// </summary>
        Ambisonic_Ambix_SecondOrder = 1001,

        /// <summary>
        /// Ambisonics 16 channel (third-order B-signal) input in Ambix format (ACN channel ordering / SN3D normalization)
        /// </summary>
        Ambisonic_Ambix_ThirdOrder = 1002,

        /// <summary>
        /// A combination of Ambisonics 4 channel input in Ambix format (ACN channel ordering / SN3D normalization),
        /// plus two extra channels for non-diagenic L-R stereo.
        /// </summary>
        Ambisonic_Ambix_FirstOrder_PlusNonDiagenicStereo = 1003,
    }
}
