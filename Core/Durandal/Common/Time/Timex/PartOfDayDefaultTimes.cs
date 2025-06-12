using System;
using Durandal.Common.Time.Timex.Enums;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// Enables specifying default times for different points in time of various parts of a day
    /// </summary>
    public class PartOfDayDefaultTimes
    {
        /// <summary>
        /// End-hour marker
        /// </summary>
        public int EndHour { get; private set; }

        /// <summary>
        /// End-minute marker (optional) -- defaults to 0 if not specified
        /// </summary>
        public int? EndMinute { get; private set; } = null;

        /// <summary>
        /// End marker (optional) -- defaults to 0 if not specified
        /// </summary>
        public int? EndSecond { get; private set; } = null;

        /// <summary>
        /// Part of a day whose time is being specified
        /// </summary>
        public PartOfDay PartOfDay { get; private set; }

        /// <summary>
        /// Start-hour marker
        /// </summary>
        public int StartHour { get; private set; }

        /// <summary>
        /// Start-minute marker (optional) -- defaults to 0 if not specified
        /// </summary>
        public int? StartMinute { get; private set; } = null;

        /// <summary>
        /// Second marker (optional) -- defaults to 0 if not specified
        /// </summary>
        public int? StartSecond { get; private set; } = null;

        public PartOfDayDefaultTimes(PartOfDay partOfDay, int startHour, int endHour, int startMinute = 0, int startSecond = 0, int endMinute = 0, int endSecond = 0)
        {
            this.ValidateHour(startHour);
            this.ValidateHour(endHour);

            if (endHour <= startHour)
            {
                throw new ArgumentException("End hour must be larger than start hour.", nameof(endHour));
            }

            this.ValidateMinute(startMinute);
            this.ValidateMinute(endMinute);
            this.ValidateSecond(startSecond);
            this.ValidateSecond(endSecond);

            this.PartOfDay = partOfDay;
            this.StartHour = startHour;
            this.StartMinute = startMinute;
            this.StartSecond = startSecond;
            this.EndHour = endHour;
            this.EndMinute = endMinute;
            this.EndSecond = endSecond;
        }

        private void ValidateHour(int hour)
        {
            if (hour < 0 || hour > 24)
            {
                throw new ArgumentOutOfRangeException(nameof(hour), "Specify an hour between 0 and 24 for part-of-day time.");
            }
        }

        private void ValidateMinute(int minute)
        {
            if (minute < 0 || minute > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(minute), "Specify a minute between 0 and 60 for part-of-day time.");
            }
        }

        private void ValidateSecond(int second)
        {
            if (second < 0 || second > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(second), "Specify a second between 0 and 60 for part-of-day time.");
            }
        }
    }
}
