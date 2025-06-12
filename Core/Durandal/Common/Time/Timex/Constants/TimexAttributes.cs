using System;
using System.Diagnostics.CodeAnalysis;

namespace Durandal.Common.Time.Timex.Constants
{
    /// <summary>
    /// Attributes of TIMEX3 tag
    /// </summary>
    public static class TimexAttributes
    {
        public const string Offset = @"OFFSET";
        public const string OffsetAnchor = @"OFFSET_ANCHOR";
        public const string OffsetUnit = @"OFFSET_UNIT";
        public const string CompoundOffset = @"COMPOUND_OFFSET";
        public const string MinimumOffset = @"MIN_OFFSET";

        public const string Duration = @"DURATION";
        public const string DurationUnit = @"DURATION_UNIT";
        public const string RawDuration = @"DURATION_SECONDS";

        public const string Mod = @"MOD";
        public const string Frequency = @"FREQ";
        public const string FrequencyUnit = @"FREQ_UNIT";
        public const string Quantity = @"QUANT";

        [SuppressMessage("Microsoft.Naming", "CA1709", MessageId = "Pm")]
        public const string AmPm = @"AMPM";

        public const string WeekOf = @"WEEKOF";
        public const string RangeHint = @"RANGE_HINT";
    }
}
