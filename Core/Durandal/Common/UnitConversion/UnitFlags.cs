using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.UnitConversion
{
    [Flags]
    public enum UnitFlags
    {
        /// <summary>
        /// Nothing special about this unit
        /// </summary>
        None = 0x0,

        /// <summary>
        /// The name of this unit can refer to different things - example being "ounce" as either mass or volume.
        /// Setting this flag also requires you to set the "unambiguousname" property of the unit, so the
        /// code can resolve the ambiguity at runtime
        /// </summary>
        AmbiguousName = 0x1 << 1,

        /// <summary>
        /// This unit is commonly used in a way that is not strictly proper - currently this only applies to
        /// the usage of "pounds" as a mass when it is technically a force. Unlike AmbiguousName, this flag
        /// does not denote any language-specific ambiguity (i.e. the unit name is exact in all cases)
        /// </summary>
        AmbiguousUsage = 0x1 << 2,

        /// <summary>
        /// This unit is a measurement of time whose length can vary based on many calendar factors, and therefore
        /// the value returned should be considered approximate
        /// </summary>
        TimeVariance = 0x1 << 3,

        /// <summary>
        /// This unit requires special handling because its measurement is not scalar.
        /// Only applies to temperature conversion which requires an offset in addition to scale.
        /// </summary>
        NonScalar = 0x1 << 4,

        /// <summary>
        /// Indicates that this unit is expressed in terms of a basis that doesn't actually evenly multiply for conversion.
        /// For example, there are 4 quarts in a gallon, but 1.34102 kilojoules in one horsepower
        /// </summary>
        NonExactBasis = 0x1 << 5
    }
}
