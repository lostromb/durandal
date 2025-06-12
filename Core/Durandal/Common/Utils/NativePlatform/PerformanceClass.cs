using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Utils.NativePlatform
{
    /// <summary>
    /// Represents the approximate performance class of a processor, which
    /// can be used as a rough approximation of whether this program is running
    /// on a small embedded system, a medium-spec PC, or a high-end server.
    /// </summary>
    public enum PerformanceClass
    {
        /// <summary>
        /// Unknown performance level.
        /// </summary>
        Unknown,

        /// <summary>
        /// Low performance level comparable to an SOC board (e.g. raspberry pi) or a mobile phone
        /// </summary>
        Low,

        /// <summary>
        /// Medium performance level comparable to an outdated desktop PC
        /// </summary>
        Medium,

        /// <summary>
        /// High performance level comparable to a high end desktop or server
        /// </summary>
        High
    }
}
