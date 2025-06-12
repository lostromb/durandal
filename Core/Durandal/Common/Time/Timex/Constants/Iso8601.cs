using System;
using System.Diagnostics.CodeAnalysis;

namespace Durandal.Common.Time.Timex.Constants
{
    /// <summary>
    /// Class contains constants and templates from ISO8601 specification that could be used in grammar file
    /// See wikipedia link for for ISO8601 specification details: http://en.wikipedia.org/wiki/ISO_8601
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1702", Scope = "Type")]
    [SuppressMessage("Microsoft.Naming", "CA1704", MessageId = "Iso")]
    public static class Iso8601
    {
        public const string Year = "YYYY";
        public const string Season = "S";
        public const string PartOfYear = "POY";
        public const string Month = "MM";
        public const string Day = "DD";
        public const string PartOfDay = "POD";
        public const string Week = "ww";

        [SuppressMessage("Microsoft.Naming", "CA1702")]
        public const string WeekDay = "D";

        public const string Hour = "hh";
        public const string Minute = "mm";
        public const string Second = "ss";
        public const string TimeZone = "TZ";
        public const string Reference = @"REF";

        public const string TimeDelimiter = ":";
        public const string DateDelimiter = "-";
        public const string DateTimeDelimiter = "T";

        public const string YearTemplate = "{0}";
        public const string SeasonTemplate = "{0}-{1}";
        public const string PartOfYearTemplate = "{0}-{1}";
        public const string MonthTemplate = "{0}-{1}";
        public const string WeekTemplate = "{0}-W{1}";

        [SuppressMessage("Microsoft.Naming", "CA1702")]
        public const string WeekEndTemplate = "{0}-W{1}-WE";

        [SuppressMessage("Microsoft.Naming", "CA1702")]
        public const string WeekDaysTemplate = "{0}-W{1}-WD";

        [SuppressMessage("Microsoft.Naming", "CA1702")]
        public const string WeekDayTemplate = "{0}-W{1}-{2}";

        public const string DayTemplate = "{0}-{1}-{2}";
        public const string WeekOfMonthTemplate = "{0}-{1}-W{2}";

        [SuppressMessage("Microsoft.Naming", "CA1702")]
        public const string WeekDayOfMonthTemplate = "{0}-{1}-W{2}-{3}";

        public const string TimeTemplate = "T{0}";
        public const string HourTemplate = "T{0}";
        public const string MinuteTemplate = "T{0}:{1}";
        public const string SecondTemplate = "T{0}:{1}:{2}";

        public const string DurationDateTemplate = "P{0}{1}";
        public const string DurationTimeTemplate = "PT{0}{1}";
    }
}
