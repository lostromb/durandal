using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Specifies the intended speaker location for an audio channel, used when identifying
    /// where a channel should be played from in real space.
    /// </summary>
    public enum SpeakerLocation
    {
        /// <summary>
        /// Unknown speaker location.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// "C". Front, listener level.
        /// </summary>
        FrontCenter = 1,

        /// <summary>
        /// "L". Front left 20 degrees, listener level.
        /// </summary>
        FrontLeft = 2,

        /// <summary>
        /// "R". Front right 20 degrees, listener level.
        /// </summary>
        FrontRight = 3,

        /// <summary>
        /// Front left 45 degrees, listener level.
        /// </summary>
        FrontLeftWide = 4,

        /// <summary>
        /// Front right 45 degrees, listener level.
        /// </summary>
        FrontRightWide = 5,

        /// <summary>
        /// Left 90 degrees, listener level.
        /// </summary>
        LeftSide = 6,

        /// <summary>
        /// Right 90 degrees, listener level.
        /// </summary>
        RightSide = 7,

        /// <summary>
        /// Left 135 degrees, listener level.
        /// </summary>
        LeftRear = 8,

        /// <summary>
        /// Right 135 degrees, listener level.
        /// </summary>
        RightRear = 9,

        /// <summary>
        /// Directly behind, listener level.
        /// </summary>
        RearCenter = 10,

        /// <summary>
        /// "LFE". Low frequency or primary subwoofer channel.
        /// </summary>
        LowFrequency = 11,

        /// <summary>
        /// "LFE2". Low frequency or subwoofer channel 2.
        /// </summary>
        LowFrequency2 = 12,

        /// <summary>
        /// Front left 45 degrees, overhead.
        /// </summary>
        LeftTopFrontOverhead = 13,

        /// <summary>
        /// Front right 45 degrees, overhead.
        /// </summary>
        RightTopFrontOverhead = 14,

        /// <summary>
        /// Left 90 degrees, overhead.
        /// </summary>
        LeftMiddleOverhead = 15,

        /// <summary>
        /// Right 90 degrees, overhead.
        /// </summary>
        RightMiddleOverhead = 16,

        /// <summary>
        /// Left 135 degrees, overhead.
        /// </summary>
        LeftRearOverhead = 17,

        /// <summary>
        /// Right 135 degrees, overhead.
        /// </summary>
        RightRearOverhead = 18,

        /// <summary>
        /// Left 160 degrees, listener level.
        /// </summary>
        RearLeftOfCenter = 19,

        /// <summary>
        /// Right 160 degrees, listener level.
        /// </summary>
        RearRightOfCenter = 20,

        /// <summary>
        /// Monaural audio directly played into both listener's ears.
        /// </summary>
        MonauralInEar = 21,

        /// <summary>
        /// Directly played into listener's left ear.
        /// </summary>
        LeftInEar = 22,

        /// <summary>
        /// Directly played into listener's right ear.
        /// </summary>
        RightInEar = 23,
    }
}
