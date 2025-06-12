using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.Time.Timex.Constants;
using Durandal.Common.Time.Timex.Enums;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
	/// This class represents a duration value that is represented in several different formats.
	/// The underlying storage type for all durations is a single number of seconds. When an instance
	/// of this class is constructed with a seconds value, it is reinterpreted into the more human-readable
	/// month, day, hour, minute, etc. components. So, for example, a duration of "3600" prints out as "PT1H", because
	/// it's exactly an hour long.
	/// However, sometimes we need to accommodate things such as "Several hours". For this reason, there is also a
	/// DurationUnit attribute that is stored. "Several hours" would then be constructed using rawValue = 0, DurationUnit = Hour, 
	/// and it would come out of the formatter as "PTXH".
	/// You can differentiate between these two constructions using the IsSpecific() method.
	/// </summary>
	public class DurationValue
    {
        /// Static string values
        private const string VAGUE_YEAR = "PXY";
        private const string VAGUE_MONTH = "PXM";
        private const string VAGUE_WEEK = "PXW";
        private const string VAGUE_DAY = "PXD";
        private const string VAGUE_HOUR = "PTXH";
        private const string VAGUE_MINUTE = "PTXM";
        private const string VAGUE_SECOND = "PTXS";
        private const string ZERO_DURATION = "PT0S";

        /// Properties
        public int Years { get; private set; }
        public int Months { get; private set; }
        public int Weeks { get; private set; }
        public int Days { get; private set; }
        public int Hours { get; private set; }
        public int Minutes { get; private set; }
        public int Seconds { get; private set; }

        // The raw value that was used to construct this object. For non-specific durations this will be 0.
        // This value corresponds to the number of seconds spanned by this duration.
        private long _rawSeconds;

        // If this duration is non-specific, like "a few days", this value stores the temporal unit that is being referred to (in this case, "Day")
        private TemporalUnit? _durationUnit;
        
        public DurationValue()
        {
            _rawSeconds = 0;
            Years = 0;
            Months = 0;
            Weeks = 0;
            Days = 0;
            Hours = 0;
            Minutes = 0;
            Seconds = 0;
            _durationUnit = null;
        }

        /// <summary>
        /// Constructs a new duration using a specified value in seconds.
        /// </summary>
        public DurationValue(long rawValueInSeconds) : this()
        {
            SetRawValue(rawValueInSeconds);
        }

        /// <summary>
        /// Constructs a new duration using a specified united value.
        /// unit.
        /// </summary>
        public DurationValue(long valueWithUnit, TemporalUnit? durationUnit) : this()
        {
            _durationUnit = durationUnit;
		    _rawSeconds = valueWithUnit;

            if (_durationUnit.HasValue)
            {
                if (_durationUnit.IsWeekday())
                {
                    throw new TimexException("Attempted to specify a duration = weekday in grammar");
                }

                // For backwards compatability
                // Old grammars will pass "{1, TemporalUnit_DAY}" and new grammars will pass {86400}
                // So we need to multiply the raw value by the value represented by its unit
                if (_durationUnit != TemporalUnit.Weekend && _durationUnit != TemporalUnit.Weekdays)
                {
                    _rawSeconds *= _durationUnit.ToDuration();
                }

		        // And correct the unit to make it conform to one that ISO can output
		        switch(_durationUnit)
		        {
                   case TemporalUnit.Century:
                        _durationUnit = TemporalUnit.Year;
                        break;
                    case TemporalUnit.Decade:
                        _durationUnit = TemporalUnit.Year;
                        break;
                    case TemporalUnit.Quarter:
                        _durationUnit = TemporalUnit.Month;
                        break;
                    case TemporalUnit.Fortnight:
                        _durationUnit = TemporalUnit.Day;
                        break;
		        }
            }

		    SetRawValue(_rawSeconds);
        }

        /// <summary>
        /// Prints out this duration in full ISO8601 format.
        /// </summary>
        public string FormatValue()
        {
            // The duration is unspecified: Just print "X" and the unit
		    if (_rawSeconds == 0 && _durationUnit != null)
		    {
			    switch (_durationUnit)
			    {
			    case TemporalUnit.Century:
			    case TemporalUnit.Decade:
			    case TemporalUnit.Year:
				    return VAGUE_YEAR;
			    case TemporalUnit.Month:
			    case TemporalUnit.Quarter:
				    return VAGUE_MONTH;
                case TemporalUnit.Week:
                case TemporalUnit.Fortnight:
                    return VAGUE_WEEK;
			    case TemporalUnit.Day:
				    return VAGUE_DAY;
			    case TemporalUnit.Hour:
				    return VAGUE_HOUR;
			    case TemporalUnit.Minute:
				    return VAGUE_MINUTE;
			    case TemporalUnit.Second:
				    return VAGUE_SECOND;
			    default:
				    throw new TimexException("Cannot create a vague duration with duration unit of " + _durationUnit.ToString());
			    }
		    }

		    if (_rawSeconds == 0)
		    {
			    // Suppose we have no duration value at all. Return "0 seconds".
			    return ZERO_DURATION;
		    }

		    // Print out the duration string dynamically based on the previously parsed year/month/day, etc. values
		    // This implementation doesn't treat weeks separately; it just considers them to be "P7D"
		    string returnVal = "P";

		    if (Years > 0)
		    {
			    returnVal += string.Format("{0}Y", Years);
		    }
		    if (Months > 0)
		    {
			    returnVal += string.Format("{0}M", Months);
		    }
            if (Weeks > 0)
            {
                returnVal += string.Format("{0}W", Weeks);
            }
		    if (Days > 0)
		    {
			    returnVal += string.Format("{0}D", Days);
		    }
		    if (Hours > 0 || Minutes > 0 || Seconds > 0)
		    {
			    returnVal += Iso8601.DateTimeDelimiter;
		    }
		    if (Hours > 0)
		    {
			    returnVal += string.Format("{0}H", Hours);
		    }
		    if (Minutes > 0)
		    {
			    returnVal += string.Format("{0}M", Minutes);
		    }
		    if (Seconds > 0)
		    {
			    returnVal += string.Format("{0}S", Seconds);
		    }

            return returnVal;
        }

        /// <summary>
        /// This property is kind of tricky to explain. Sometimes we have expressions like "2 days from now", which use a duration
        /// value of 172800 seconds. This prints out as "P2D", which is fine, but what if we wanted to know the value of the day
        /// that is being referred to? Suppose today is Jan 1st, 9:01 AM. If we just added the raw value of 172800 seconds to that,
        /// we'd end up with Jan 3rd, 9:01:00 AM. While that's technically accurate, it's too exact for the way that people tend to
        /// think of times. When I say "next year", I'm not thinking of an exact day, hour and minute value, I just want the year.
        /// That's what this function is for. It simply returns the value + unit of the largest duration factor, i.e. for "5 days and 7 minutes"
        /// it will just return {"5", DAY}.
        /// Using this functions, we can have nice round offset values that don't use exact values where none are implied.
        /// If a duration unit was specified in this object's constructor, this function will return that unit.
        /// </summary>
        public Tuple<int, TemporalUnit?> SimpleValue
        {
            get
            {
                // Workaround: If the unit is "weekend", there's no way to differentiate it from any other day/week units.
                // In this case, store the value in rawSeconds and override this return value.
                if (_durationUnit == TemporalUnit.Weekend)
                {
                    return new Tuple<int, TemporalUnit?>((int)_rawSeconds, TemporalUnit.Weekend);
                }
                else if (_durationUnit == TemporalUnit.Weekdays)
                {
                    return new Tuple<int, TemporalUnit?>((int)_rawSeconds, TemporalUnit.Weekdays);
                }
                else if (Years > 0)
                {
                    return new Tuple<int, TemporalUnit?>(Years, TemporalUnit.Year);
                }
                else if (Months > 0)
                {
                    return new Tuple<int, TemporalUnit?>(Months, TemporalUnit.Month);
                }
                else if (Weeks > 0)
                {
                    return new Tuple<int, TemporalUnit?>(Weeks, TemporalUnit.Week);
                }
                else if (Days > 0)
                {
                    return new Tuple<int, TemporalUnit?>(Days, TemporalUnit.Day);
                }
                else if (Hours > 0)
                {
                    return new Tuple<int, TemporalUnit?>(Hours, TemporalUnit.Hour);
                }
                else if (Minutes > 0)
                {
                    return new Tuple<int, TemporalUnit?>(Minutes, TemporalUnit.Minute);
                }

                return new Tuple<int, TemporalUnit?>(Seconds, TemporalUnit.Second);
            }
        }

        /// <summary>
        /// If this duration is a unit-only value (to represent an input like "a few minutes" or "several days"),
        /// this method returns false.
        /// </summary>
        public bool IsSpecific()
        {
            return _rawSeconds != 0 || _durationUnit == null;
        }

        public bool IsSet()
        {
            return _rawSeconds != 0 || _durationUnit != null;
        }

        // Converts the single RawSeconds value into the individual "bins" (weeks, months, days, minutes, etc.)
        // This populates all of the "Days", "Hours" ... fields
        private void SetRawValue(long rawValue)
        {
            _rawSeconds = rawValue;
		    if (rawValue > 0)
		    {
                long roundingCutoff = (!_durationUnit.HasValue || _durationUnit == TemporalUnit.Weekend || _durationUnit == TemporalUnit.Weekdays)
                    ? 0 : _durationUnit.ToDuration();
                long durationRemaining = rawValue;

                // Note: relying on truncation with integral division here.
                if (roundingCutoff == 0 || roundingCutoff >= TemporalUnit.Year.ToDuration())
                {
                    Years = (int)(durationRemaining / TemporalUnit.Year.ToDuration());
                    durationRemaining %= TemporalUnit.Year.ToDuration();
                }
                if (roundingCutoff == 0 || roundingCutoff >= TemporalUnit.Month.ToDuration())
                {
                    Months = (int)(durationRemaining / TemporalUnit.Month.ToDuration());
                    durationRemaining %= TemporalUnit.Month.ToDuration();
                }
                if (roundingCutoff == 0 || roundingCutoff >= TemporalUnit.Week.ToDuration())
                {
                    Weeks = (int)(durationRemaining / TemporalUnit.Week.ToDuration());
                    durationRemaining %= TemporalUnit.Week.ToDuration();
                }
                if (roundingCutoff == 0 || roundingCutoff >= TemporalUnit.Day.ToDuration())
                {
                    Days = (int)(durationRemaining / TemporalUnit.Day.ToDuration());
                    durationRemaining %= TemporalUnit.Day.ToDuration();
                }
                if (roundingCutoff == 0 || roundingCutoff >= TemporalUnit.Hour.ToDuration())
                {
                    Hours = (int)(durationRemaining / TemporalUnit.Hour.ToDuration());
                    durationRemaining %= TemporalUnit.Hour.ToDuration();
                }
                if (roundingCutoff == 0 || roundingCutoff >= TemporalUnit.Minute.ToDuration())
                {
                    Minutes = (int)(durationRemaining / TemporalUnit.Minute.ToDuration());
                    durationRemaining %= TemporalUnit.Minute.ToDuration();
                }
                Seconds = (int)durationRemaining;
		    }
        }
    }
}
