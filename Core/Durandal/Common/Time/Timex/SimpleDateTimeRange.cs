using System;
using Durandal.Common.Time.Timex.Enums;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// Represents a time range that attempts to align with the intuitive way that we
    /// normally think about times. By explicitly defining a span of DateTime
    /// objects, we can easily determine if a single point in time falls inside the user's
    /// conceptual idea of time. So for example, "tomorrow' would include the entire range
    /// from midnight to midnight of a single day, and "do I have any meetings tomorrow" can
    /// be easily computed by comparing the event times with that range.
    /// </summary>
    public class SimpleDateTimeRange
    {
        /// <summary>
        /// The exact start time of the range.
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// The exact end time of the range
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// The granularity which is calculated on the most specific
        /// unit that the user mentioned. If the original input was "today", for
        /// example, Granularity == Day. "3:00 PM" => Granularity == Hour, etc.
        /// </summary>
        public TemporalUnit Granularity { get; set; }
    }
}
