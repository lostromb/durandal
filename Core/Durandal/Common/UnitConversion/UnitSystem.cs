using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.UnitConversion
{
    /// <summary>
    /// Specifies the type of system a unit belongs to (metric, imperial, etc.)
    /// </summary>
    [Flags]
    public enum UnitSystem
    {
        Unspecified = 0x0,

        /// <summary>
        /// Metric, SI, or Scientific units
        /// </summary>
        Metric = 0x1 << 1,

        /// <summary>
        /// British or Commonwealth imperial units
        /// </summary>
        BritishImperial = 0x1 << 2,

        /// <summary>
        /// United-states variation on Imperial units
        /// </summary>
        USImperial = 0x1 << 3,

        /// <summary>
        /// Used as shorthand when British and US imperial units match
        /// </summary>
        Imperial = USImperial | BritishImperial
    }
}
