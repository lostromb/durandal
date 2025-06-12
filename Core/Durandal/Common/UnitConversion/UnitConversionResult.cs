using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.UnitConversion
{
    public class UnitConversionResult
    {
        /// <summary>
        /// The exact amount of the source
        /// </summary>
        public decimal SourceUnitAmount = 0;

        /// <summary>
        /// The exact amount of the target
        /// </summary>
        public decimal TargetUnitAmount = 0;

        /// <summary>
        /// The type of conversion that was performed (length, mass, volume, etc.)
        /// </summary>
        public UnitType ConversionType = UnitType.Unknown;

        /// <summary>
        /// The pretty-printed value of the source unit amount
        /// </summary>
        public string SourceAmountString;

        /// <summary>
        /// The pretty-printed value of the target unit amount
        /// </summary>
        public string TargetAmountString;

        /// <summary>
        /// The name of the unit that was converted FROM, in canonical form (i.e. it's one of the UnitName values)
        /// </summary>
        public string SourceUnitName;

        /// <summary>
        /// The name of the unit that was converted TO, in canonical form (i.e. it's one of the UnitName values)
        /// </summary>
        public string TargetUnitName;

        /// <summary>
        /// Indicates that the conversion between unit bases occurred which means the result should be treated as approximate.
        /// </summary>
        public bool IsApproximate = false;

        /// <summary>
        /// This flag indicates that this conversion was a time operation that
        /// crossed a variable-length boundary. For example, "How many days are in a month"
        /// cannot be a precise conversion because of the variable nature of month lengths.
        /// </summary>
        public bool HasTimeVariance = false;

        /// <summary>
        /// Indicates that conversion actually happened. For things like "convert 10 meters to metric", this will be false.
        /// </summary>
        public bool ConversionWasRequired = false;
    }
}
