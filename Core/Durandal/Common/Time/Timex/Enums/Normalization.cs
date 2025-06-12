using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Time.Timex.Enums
{
    /// <summary>
    /// Normalization type
    /// </summary>
    public enum Normalization
    {
        /// <summary>
        /// Normalize to dates in current period
        /// </summary>
        Present,

        /// <summary>
        /// Normalize to future dates
        /// </summary>
        Future,

        /// <summary>
        /// Normalize to past dates
        /// </summary>
        Past
    }
}
