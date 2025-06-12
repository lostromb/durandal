using System;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// Provides enumerated values to use to set timex options.
    /// </summary>
    [Flags]
    public enum TimexOptions
    {
        /// <summary>
        /// Specifies that no options are set
        /// </summary>
        None = 0x0,

        /// <summary>
        /// Specifies that internal timex regular expressions are compiled. 
        /// This yields faster execution but increases startup time.
        /// Unused in the PCL version of the library
        /// </summary>
        Compiled = 0x1,

        /// <summary>
        /// Specifies that the grammar regexes should be case sensitive.
        /// This can make them slightly faster but a lot more annoying to debug.
        /// </summary>
        CaseSensitive = 0x2
    }
}
