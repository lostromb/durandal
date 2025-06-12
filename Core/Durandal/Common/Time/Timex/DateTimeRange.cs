using System.Collections.Generic;

namespace Durandal.Common.Time.Timex
{
    using Durandal.Common.Time.Timex.Enums;
    
    /// <summary>
    /// A container for a pair of TimexMatches that are inferred to represent a range.
    /// One or both of the fields may be null.
    /// </summary>
    public struct DateTimeRange : System.IEquatable<DateTimeRange>
    {
        public TimexMatch StartTime
        {
            get; set;
        }

        public TimexMatch EndTime
        {
            get; set;
        }

        public bool StartsNow
	    {
	        get
	        {
                return IsPresentRef(StartTime);
	        }
	    }

        public bool EndsNow
        {
            get
            {
                return IsPresentRef(EndTime);
            }
        }

        ///<summary>
        /// Determines if the given TimexMatch is non-null, and contains a present reference
        ///</summary>
        private static bool IsPresentRef(TimexMatch match)
        {
            return match != null && match.ExtendedDateTime.Reference == DateTimeReference.Present;
        }

        private sealed class StartTimeEndTimeEqualityComparer : IEqualityComparer<DateTimeRange>
        {
            public bool Equals(DateTimeRange x, DateTimeRange y)
            {
                return Equals(x.StartTime, y.StartTime) && Equals(x.EndTime, y.EndTime);
            }

            public int GetHashCode(DateTimeRange obj)
            {
                unchecked
                {
                    return ((obj.StartTime != null ? obj.StartTime.GetHashCode() : 0)*397) ^ (obj.EndTime != null ? obj.EndTime.GetHashCode() : 0);
                }
            }
        }

        public bool Equals(DateTimeRange other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return Equals(StartTime, other.StartTime) && Equals(EndTime, other.EndTime);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is DateTimeRange && Equals((DateTimeRange) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((StartTime != null ? StartTime.GetHashCode() : 0)*397) ^ (EndTime != null ? EndTime.GetHashCode() : 0);
            }
        }

        public static bool operator ==(DateTimeRange left, DateTimeRange right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(DateTimeRange left, DateTimeRange right)
        {
            return !(left == right);
        }
    }
}
