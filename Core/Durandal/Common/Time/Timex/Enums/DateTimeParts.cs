using System;
using System.Diagnostics.CodeAnalysis;

namespace Durandal.Common.Time.Timex.Enums
{
    /// <summary>
    /// Represents DateTime parts that are specified in ExtendedDateTime structure
    /// </summary>
    [Flags]
    public enum DateTimeParts
    {
        None = 0,
        Second = 0x00000001,
        Minute = 0x00000002,
        Hour = 0x00000004,
        PartOfDay = 0x00000008,
        Day = 0x00000010,
        [SuppressMessage("Microsoft.Naming", "CA1702")]
        WeekDay = 0x00000020,
        Week = 0x00000040,
        Month = 0x00000080,
        Season = 0x00000100,
        PartOfYear = 0x00000200,
        DecadeYear = 0x00000400,
        Year = 0x00000800,
        Decade = 0x00001000,
        Century = 0x00002000,
        [SuppressMessage("Microsoft.Naming", "CA1704")]
        Millenium = 0x00004000,
        Reference = 0x00008000,
        TimeZone = 0x00010000,
        [SuppressMessage("Microsoft.Naming", "CA1709", MessageId = "Pm")]
        AmPmUnambiguous = 0x00020000,
        WeekOfExpression = 0x00040000,
        OffsetAnchor = 0x00080000,
        PartOfWeek = 0x00100000,
        Frequency = 0x00200000,
        FrequencyUnit = 0x00400000,
        Quantity = 0x00800000,
        All = 0x00FFFFFF
    }
}
