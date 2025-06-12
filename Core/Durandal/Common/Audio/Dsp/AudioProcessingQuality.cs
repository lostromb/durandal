using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// A simplified enumeration to represent hints for processing quality for certain effects (for example, resampling).
    /// </summary>
    public enum AudioProcessingQuality
    {
        /// <summary>
        /// Prioritize the fastest possible execution
        /// </summary>
        Fastest,

        /// <summary>
        /// Balance between speed and quality
        /// </summary>
        Balanced,

        /// <summary>
        /// Prioritize high-quality output
        /// </summary>
        BestQuality,
    }
}
