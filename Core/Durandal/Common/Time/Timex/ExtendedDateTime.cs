using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Durandal.Common.IO;
using Durandal.Common.Time.Timex.Calendar;
using Durandal.Common.Time.Timex.Constants;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.Utils;

#pragma warning disable 0618
namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// Extended DateTime
    /// </summary>
    public class ExtendedDateTime
    {
        /// <summary>
        /// Flags are used to identify which parts of the ExtendedDateTime are specified
        /// </summary>
        public DateTimeParts SetParts { get; private set; }

        /// <summary>
        /// The parts that have been set in this ExtendedDateTime _at its construction_, for example, in the input "1:00", explicitSetParts contains the flags for "hour" and "minute".
        /// In some cases, the inference logic can set flags in SetParts based on certain assumptions that it makes. This field is unaffected by those changes.
        /// </summary>
        public DateTimeParts ExplicitSetParts { get; private set; }

        /// <summary>
        /// Contains actual DateTime information
        /// </summary>
        [Obsolete("This property is deprecated because in many cases its value is an incomplete date-time and hence it is inaccurate to use or otherwise rely on it.  Use the getters for the individual fields instead and start migrating your code to remove depending on this property.  It will be removed in a future release without any further notice.")]
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Part of the day
        /// </summary>
        public PartOfDay PartOfDay { get; private set; }

        /// <summary>
        /// Season (e.g. Winter, Spring, etc.)
        /// </summary>
        public Season Season { get; private set; }

        /// <summary>
        /// Part of the year (e.g. FirstHalf, SecondHalf, etc.)
        /// </summary>
        public PartOfYear PartOfYear { get; private set; }

        /// <summary>
        /// Temporal type (e.g. Date, Time, etc.)
        /// </summary>
        public TemporalType TemporalType { get; private set; }

        /// <summary>
        /// General reference to the present, future or past
        /// </summary>
        public DateTimeReference Reference { get; private set; }

        /// <summary>
        /// Holds TimeZone information string
        /// </summary>
        public string TimeZone { get; private set; }

        /// <summary>
        /// Offset from specified DateTime 
        /// </summary>
        public int? Offset { get; private set; }

        /// <summary>
        /// Day offset on top of any regular offset
        /// </summary>
        public int CompoundOffset { get; private set; }

        /// <summary>
        /// In cases like "Next monday", the nearest occurrence of that weekday will be used _unless_ it falls within this many days
        /// of the reference date. This should be made configurable later on, but for now it's meant to handle the US-English
        /// quirk where "Next ___day" is generally never interpreted as being tomorrow.
        /// </summary>
        public int MinOffset { get; private set; }

        /// <summary>
        /// Offset unit
        /// </summary>
        public TemporalUnit? OffsetUnit { get; private set; }

        /// <summary>
        /// Duration
        /// </summary>
        public DurationValue Duration { get; private set; }

        /// <summary>
        /// Duration unit (mirrored attribute from Duration field)
        /// </summary>
        public TemporalUnit? DurationUnit
        {
            get
            {
                if (Duration != null && Duration.IsSet())
                    return Duration.SimpleValue.Item2;
                else
                    return null;
            }
        }

        /// <summary>
        /// Captures temporal modifiers
        /// </summary>
        public Modifier? Modifier { get; private set; }

        /// <summary>
        /// Captures quantity for sets
        /// </summary>
        public int? Quantity { get; private set; }

        /// <summary>
        /// Frequency for sets
        /// </summary>
        public int? Frequency { get; private set; }

        /// <summary>
        /// Frequency unit
        /// </summary>
        public TemporalUnit? FrequencyUnit { get; private set; }

        /// <summary>
        /// The context that was present when this object was initialized
        /// </summary>
        public TimexContext Context { get; private set; }

        /// <summary>
        /// This flag is set to indicate that the input used to create this time object
        /// was invalid in some way; for example "Feb 37th" or "27:01:11"
        /// </summary>
        public bool InputDateWasInvalid { get; private set; }

        /// <summary>
        /// The time can be given as an offset from some known date, defined by this string. This can
        /// either be a special token like "EASTER", or a month/day pair in the form of "10-31"
        /// Typically this field is used exclusively by holidays
        /// </summary>
        public string OffsetAnchor { get; private set; }

        /// <summary>
        /// Remembers the original timex dictionary that was used to create this datetime. This data can be used to
        /// reinterpret its value in, for example, a different normalization context, or to serialize this object to a stream.
        /// </summary>
        public IDictionary<string, string> OriginalTimexDictionary { get; private set; }

        /// <summary>
        /// Returns true if the Timex grammar did not specify that this ExtendedDateTime is unsuitable as an anchor for a range.
        /// This can occur for cases like "at least 10 hours ago", or other cases for which ambiguous semantics render them unsuitable for inference.
        /// </summary>
        public bool ValidForRanges { get; private set; }

        // Caches the result of FormatValue() to prevent inference rules from being used multiple times
        private string _cachedValueString = "";

        #region Getters for individual fields

        /// <summary>
        /// Gets the year field of the set time, or null if not defined
        /// </summary>
        public int? Year
        {
            get
            {
                return SetParts.HasFlag(DateTimeParts.Year) ? (int?)DateTime.Year : null;
            }
        }

        /// <summary>
        /// Gets the month field of the set time, or null if not defined
        /// </summary>
        public int? Month
        {
            get
            {
                return SetParts.HasFlag(DateTimeParts.Month) ? (int?)DateTime.Month : null;
            }
        }

        /// <summary>
        /// Gets the year day of the set time, or null if not defined
        /// </summary>
        public int? Day
        {
            get
            {
                return SetParts.HasFlag(DateTimeParts.Day) ? (int?)DateTime.Day : null;
            }
        }

        /// <summary>
        /// Gets the hour field of the set time, or null if not defined
        /// </summary>
        public int? Hour
        {
            get
            {
                return SetParts.HasFlag(DateTimeParts.Hour) ? (int?)DateTime.Hour : null;
            }
        }

        /// <summary>
        /// Gets the minute field of the set time, or null if not defined
        /// </summary>
        public int? Minute
        {
            get
            {
                return SetParts.HasFlag(DateTimeParts.Minute) ? (int?)DateTime.Minute : null;
            }
        }

        /// <summary>
        /// Gets the second field of the set time, or null if not defined
        /// </summary>
        public int? Second
        {
            get
            {
                return SetParts.HasFlag(DateTimeParts.Second) ? (int?)DateTime.Second : null;
            }
        }

        /// <summary>
        /// Gets the week (ISO calendar) field of the set time, or null if not defined
        /// </summary>
        public int? Week
        {
            get
            {
                int resultYear;
                return SetParts.HasFlag(DateTimeParts.Week) ? (int?)TimexHelpers.GetIso8601WeekOfYear(DateTime, out resultYear) : null;
            }
        }

        /// <summary>
        /// Gets the day of week (ISO calendar) field of the set time, or null if not defined
        /// </summary>
        public int? WeekDay
        {
            get
            {
                return SetParts.HasFlag(DateTimeParts.WeekDay) ? (int?)TimexHelpers.GetIso8601DayOfWeek(DateTime) : null;
            }
        }

        #endregion

        #region Static Fields

        /// <summary>
        /// When hours need to be compared against times of day (like "afternoon"), there should be a leeway of this many hours
        /// This should only be used to make fairly certain inferences, such as "10:00 AM comes before 'tonight'"
        /// </summary>
        private const int TimeOfDayVagueness = 3;

        private const int DaysInOneWeek = 7;

        private const int DaysInOneHalfWeek = 3;

        private const string WeekofString = "weekof";
        private const string AmPmUnspecifiedString = "ampm";
        private const string ResolutionWasRelativeString = "relative";
        private const string OffsetAnchorTodayString = "TODAY";

        /// <summary>
        /// Used to match phrases like "1week" that are used to express frequencies in the grammar
        /// </summary>
        private static readonly Regex FrequencyMatcher = new Regex(@"(\d+)(\D*)");

        #endregion

        #region Constructors / Parsers

        /// <summary>
        /// Creates an ExtendedDateTime from a dictionary of timex parameters (generally containing values parsed by a locale-specific grammar)
        /// </summary>
        /// <param name="temporalType">Temporal type for the created value</param>
        /// <param name="timexDictionary">Dictionary to get values from</param>
        /// <param name="globalContext">Context to perform inference / resolution in</param>
        /// <returns>ExtendedDateTime object</returns>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "construction method")]
        [SuppressMessage("Microsoft.Maintainability", "CA1505:AvoidUnmaintainableCode", Justification = "construction method")]
        public static ExtendedDateTime Create(TemporalType temporalType,
            IDictionary<string, string> timexDictionary,
            TimexContext globalContext)
        {
            if (globalContext == null)
            {
                return null;
            }

            // Initialize the return value
            var returnVal = new ExtendedDateTime
                {
                    TemporalType = temporalType,
                    Context = new TimexContext(globalContext),
                    DateTime = new DateTime(globalContext.ReferenceDateTime.Ticks),
                    OriginalTimexDictionary = timexDictionary,
                    MinOffset = 1,
                    ValidForRanges = true
                };

            if (timexDictionary == null)
            {
                return returnVal;
            }

            // If no temporal type was given, infer it from the timexdictionary
            if (returnVal.TemporalType == TemporalType.None)
            {
                returnVal.TemporalType = TemporalTypeExtensions.InferTemporalType(timexDictionary);
            }

            ParseYear(timexDictionary, returnVal);
            ParseMonth(timexDictionary, returnVal);
            ParseDay(timexDictionary, returnVal);
            ParseHour(timexDictionary, returnVal);
            ParseMinute(timexDictionary, returnVal);
            ParseSecond(timexDictionary, returnVal);
            ParseMinOffset(timexDictionary, returnVal);
            ParseWeek(timexDictionary, returnVal);
            ParseWeekday(timexDictionary, returnVal);
            ParsePartOfDay(timexDictionary, returnVal);
            ParseTimezone(timexDictionary, returnVal);
            ParseModifier(timexDictionary, returnVal);
            ParseOffset(timexDictionary, returnVal);
            ParseReference(timexDictionary, returnVal);
            ParseSeason(timexDictionary, returnVal);
            ParsePartOfYear(timexDictionary, returnVal);
            ParseDuration(timexDictionary, returnVal);
            ParseQuantity(timexDictionary, returnVal);
            ParseFrequency(timexDictionary, returnVal);
            ParseWeekOf(timexDictionary, returnVal);
            ParseRangeHint(timexDictionary, returnVal);

            // Must make Ampm inference now because it could affect the SetParts
            InferAmPm(timexDictionary, returnVal);

            // Same thing with the OffsetAnchor. We can't defer this until FormatValue because it can have side effects
            returnVal.ResolveOffsetAnchor();

            // As the last step, set part-of-day start time if needed
            TrySetPartOfDayDefaultTimes(globalContext.PartOfDayDefaultTimes, returnVal);

            return returnVal;
        }

        private static void ParseYear(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.Year) &&
                !string.IsNullOrEmpty(timexDictionary[Iso8601.Year]))
            {
                int year;
                DateTimeParts yearParts;
                if (TryParseYear(timexDictionary[Iso8601.Year], out year, out yearParts))
                {
                    // Apply inference if DecadeYear is given - determine which millenium was intended
                    if (yearParts.HasFlag(DateTimeParts.DecadeYear) && returnVal.Context.UseInference)
                    {
                        int referenceYear = returnVal.Context.ReferenceDateTime.Year;

                        // Just find the nearest matching century.
                        // Don't apply normalization because it's not likely that the input will be ambiguous to the scale of centuries
                        year += (referenceYear / 100) * 100;
                        if (year - referenceYear > 50)
                        {
                            year -= 100;
                        }
                        else if (referenceYear - year > 50)
                        {
                            year += 100;
                        }

                        yearParts &= ~DateTimeParts.DecadeYear;
                        yearParts |= DateTimeParts.Year;
                    }

                    returnVal.DateTime = 
                        returnVal.DateTime.AddYears(year - returnVal.DateTime.Year);
                    returnVal.SetParts |= yearParts;
                    returnVal.ExplicitSetParts |= yearParts;
                }
            }
        }

        private static void ParseMonth(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.Month) &&
                !string.IsNullOrEmpty(timexDictionary[Iso8601.Month]))
            {
                int month;
                if (int.TryParse(timexDictionary[Iso8601.Month], out month))
                {
                    var oldYear = returnVal.DateTime.Year;
                    returnVal.DateTime =
                        returnVal.DateTime.AddMonths(month - returnVal.DateTime.Month);
                    returnVal.SetParts |= DateTimeParts.Month;
                    returnVal.ExplicitSetParts |= DateTimeParts.Month;

                    // Check if the month caused the year field to overflow
                    if (returnVal.DateTime.Year != oldYear)
                    {
                        returnVal.InputDateWasInvalid = true;
                    }
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse month attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?) ", timexDictionary[Iso8601.Month]));
                }
            }
        }

        private static void ParseDay(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.Day) &&
                !string.IsNullOrEmpty(timexDictionary[Iso8601.Day]))
            {
                int day;
                if (int.TryParse(timexDictionary[Iso8601.Day], out day))
                {
                    // Warning! Warning! This is a huge headache for everyone. Let me explain:
                    // We use a DateTime object to store the partial time. If no month is specified, it defaults to the reference month.
                    // But suppose, for example, that it was currently February, and the user referred to "the 31st", meaning January 31st. _we can't store "February" and "31st" simultaneously_
                    // The only way around it is to apply past/future inference rules right here.
                    if (returnVal.SetParts.HasFlag(DateTimeParts.Month) ||
                        (returnVal.Context.UseInference && returnVal.TemporalType != TemporalType.Set && returnVal.Context.Normalization == Normalization.Present))
                    {
                        int originalMonth = returnVal.DateTime.Month;
                        returnVal.DateTime = returnVal.DateTime.AddDays(day - returnVal.DateTime.Day);
                        // Check that the day is an actual day of the month
                        if (returnVal.DateTime.Month != originalMonth)
                        {
                            returnVal.InputDateWasInvalid = true;
                        }
                    }
                    else if (returnVal.Context.UseInference && returnVal.TemporalType != TemporalType.Set)
                    {
                        Normalization normalization = returnVal.Context.Normalization;
                        if (normalization == Normalization.Past &&
                            day < returnVal.Context.ReferenceDateTime.Day) // Past normalization within current month
                        {
                            returnVal.DateTime = returnVal.DateTime.AddDays(day - returnVal.DateTime.Day);
                        }
                        else // Month is ambiguous, but we have a day. Try and infer the month
                        {
                            DateTime inferredTime = new DateTime(returnVal.DateTime.Ticks);
                            int attempts = 3;
                            int originalMonth = inferredTime.Month;
                            bool includeCurrentDateInResolution = returnVal.Context.IncludeCurrentTimeInPastOrFuture; 
                        
                            if (normalization == Normalization.Future &&
                                (day < returnVal.Context.ReferenceDateTime.Day || (!includeCurrentDateInResolution && day == returnVal.Context.ReferenceDateTime.Day)))
                            {
                                // Make sure we don't go backwards within the current month
                                inferredTime = inferredTime.AddDays(45 - inferredTime.Day);
                            }

                            do
                            {
                                if (normalization == Normalization.Past)
                                {
                                    inferredTime = inferredTime.AddDays(0 - (((inferredTime.Month - originalMonth) * 30) + 15 + inferredTime.Day));
                                }
                                originalMonth = inferredTime.Month;
                                inferredTime = inferredTime.AddDays(day - inferredTime.Day);
                            }
                            while (inferredTime.Month != originalMonth && --attempts > 0);

                            returnVal.DateTime = inferredTime;

                            if (attempts == 0)
                            {
                                returnVal.InputDateWasInvalid = true;
                            }
                        }

                        returnVal.SetParts |= DateTimeParts.Month | DateTimeParts.Year;
                    }
                    else
                    {
                        // If month is unspecified, and we are in NoInference mode, set the month to January so it can contain all 31 days
                        returnVal.DateTime = returnVal.DateTime.AddMonths(1 - returnVal.DateTime.Month);
                        returnVal.DateTime = returnVal.DateTime.AddDays(day - returnVal.DateTime.Day);
                    }

                    if (day < 1 || day > 31)
                    {
                        returnVal.InputDateWasInvalid = true;
                    }
                    
                    returnVal.SetParts |= DateTimeParts.Day;
                    returnVal.ExplicitSetParts |= DateTimeParts.Day;
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse day attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?) ", timexDictionary[Iso8601.Day]));
                }
            }
        }

        private static void ParseHour(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.Hour) &&
                !string.IsNullOrEmpty(timexDictionary[Iso8601.Hour]))
            {
                int hour;
                if (int.TryParse(timexDictionary[Iso8601.Hour], out hour))
                {
                    int oldDay = returnVal.DateTime.Day;
                    returnVal.DateTime =
                        returnVal.DateTime.AddHours(hour - returnVal.DateTime.Hour);
                    returnVal.SetParts |= DateTimeParts.Hour;
                    returnVal.ExplicitSetParts |= DateTimeParts.Hour;

                    // Check if the hour caused the day field to overflow
                    if (returnVal.DateTime.Day != oldDay)
                    {
                        returnVal.InputDateWasInvalid = true;
                    }
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse hour attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?) ", timexDictionary[Iso8601.Hour]));
                }
            }
        }

        private static void ParseMinute(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.Minute) &&
                !string.IsNullOrEmpty(timexDictionary[Iso8601.Minute]))
            {
                int minute;
                if (int.TryParse(timexDictionary[Iso8601.Minute], out minute))
                {
                    var oldHour = returnVal.DateTime.Hour;
                    returnVal.DateTime =
                        returnVal.DateTime.AddMinutes(minute - returnVal.DateTime.Minute);
                    returnVal.SetParts |= DateTimeParts.Minute;
                    returnVal.ExplicitSetParts |= DateTimeParts.Minute;

                    // Check if the minute caused the hour field to overflow
                    if (returnVal.DateTime.Hour != oldHour)
                    {
                        returnVal.InputDateWasInvalid = true;
                    }
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse minute attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?) ", timexDictionary[Iso8601.Minute]));
                }
            }
        }

        private static void ParseSecond(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.Second) &&
                !string.IsNullOrEmpty(timexDictionary[Iso8601.Second]))
            {
                int second;
                if (int.TryParse(timexDictionary[Iso8601.Second], out second))
                {
                    var oldMinute = returnVal.DateTime.Minute;
                    returnVal.DateTime =
                        returnVal.DateTime.AddSeconds(second - returnVal.DateTime.Second);
                    returnVal.SetParts |= DateTimeParts.Second;
                    returnVal.ExplicitSetParts |= DateTimeParts.Second;

                    // Check if the second caused the minute field to overflow
                    if (returnVal.DateTime.Minute != oldMinute)
                    {
                        returnVal.InputDateWasInvalid = true;
                    }
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse second attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?) ", timexDictionary[Iso8601.Second]));
                }
            }
        }

        private static void ParseWeek(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.Week) &&
                !string.IsNullOrEmpty(timexDictionary[Iso8601.Week]))
            {
                int week;
                if (int.TryParse(timexDictionary[Iso8601.Week], out week))
                {
                    int isoWeek = Durandal.Common.Time.Timex.Calendar.GregorianCalendar.GetISOWeekOfYear(returnVal.DateTime);
                    int currentDtIsoYear = TimexHelpers.GetIso8601WeekYear(returnVal.DateTime.Year, returnVal.DateTime.Month, isoWeek);
                    if (currentDtIsoYear != returnVal.DateTime.Year &&
                        !returnVal.SetParts.HasFlag(DateTimeParts.Day) &&
                        !returnVal.SetParts.HasFlag(DateTimeParts.Month))
                    {
                        // Handle an edge case where the ISO year does not match the gregorian year.
                        // This can cause the values to be a year off for inputs like "YEAR=2017 WEEK=17" when no reference datetime is given
                        // In this case, we just shove the reference time forward or backward to the (hopefully the) middle of the proper year
                        if (currentDtIsoYear < returnVal.DateTime.Year)
                        {
                            returnVal.DateTime = returnVal.DateTime.AddMonths(6);
                        }
                        else
                        {
                            returnVal.DateTime = returnVal.DateTime.AddMonths(-6);
                        }
                    }

                    int currentDtIsoWeek = Durandal.Common.Time.Timex.Calendar.GregorianCalendar.GetISOWeekOfYear(returnVal.DateTime);
                    returnVal.DateTime =
                        returnVal.DateTime.AddDays((week - currentDtIsoWeek) * DaysInOneWeek);
                    returnVal.SetParts |= DateTimeParts.Week;
                    returnVal.ExplicitSetParts |= DateTimeParts.Week;
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse week attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?) ", timexDictionary[Iso8601.Week]));
                }
            }
        }

        private static void ParseWeekday(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.WeekDay) &&
                !string.IsNullOrEmpty(timexDictionary[Iso8601.WeekDay]))
            {
                int weekDay;
                if (int.TryParse(timexDictionary[Iso8601.WeekDay], out weekDay))
                {
                    if (!timexDictionary.ContainsKey(Iso8601.Week))
                    { 
                        // The week is unspecified. Partially resolve weekday using "floating" inference logic
                        returnVal.DateTime = returnVal.DateTime.AddYears(returnVal.Context.ReferenceDateTime.Year - returnVal.DateTime.Year);
                        returnVal.DateTime = returnVal.DateTime.AddMonths(returnVal.Context.ReferenceDateTime.Month - returnVal.DateTime.Month);
                        returnVal.DateTime = returnVal.DateTime.AddDays(returnVal.Context.ReferenceDateTime.Day - returnVal.DateTime.Day);
                        // Use the predefined helper method to set this date to the proper day of week
                        returnVal.DateTime = ApplyDayOfWeekOffset(returnVal.DateTime, (DayOfWeek)weekDay, 0, returnVal.Context.Normalization, returnVal.Context.WeekdayLogicType, returnVal.MinOffset);
                    }
                    else
                    {
                        // If the week is fixed already, just zoom to the proper day within that week
                        returnVal.DateTime = returnVal.DateTime.AddDays(weekDay - TimexHelpers.GetIso8601DayOfWeek(returnVal.DateTime));
                    }

                    // If we have resolved input like "Monday" to an actual inferred date, consider that date to be explicit in terms of year/month/day.
                    // This will make the value format properly later
                    if (returnVal.Context.UseInference && returnVal.TemporalType != TemporalType.Set)
                    {
                        returnVal.SetParts |= DateTimeParts.Year | DateTimeParts.Month | DateTimeParts.Day;
                    }
                    
                    returnVal.SetParts |= DateTimeParts.WeekDay;
                    returnVal.ExplicitSetParts |= DateTimeParts.WeekDay;
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse WeekDay attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[Iso8601.WeekDay]));
                }
            }
        }

        private static void ParsePartOfDay(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.PartOfDay) &&
                !string.IsNullOrEmpty(timexDictionary[Iso8601.PartOfDay]))
            {
                PartOfDay partOfDay;
                if (EnumExtensions.TryParse(timexDictionary[Iso8601.PartOfDay], out partOfDay))
                {
                    // Is it noon or midnight? In this case, we know the exact hour/minute/second
                    if (partOfDay == PartOfDay.Midnight && !returnVal.SetParts.HasFlag(DateTimeParts.Hour))
                    {
                        // We can't set Midnight to be 24:00:00 proper because then it would rollover to 00:00:00 of the next day,
                        // and the day field is fixed here. However, this value will be only used for past/future comparison
                        // (the printed T24:00:00 will come out later in the formatting method) so one second of difference is not serious.
                        returnVal.DateTime = returnVal.DateTime.AddSeconds(59 - returnVal.DateTime.Second);
                        returnVal.DateTime = returnVal.DateTime.AddMinutes(59 - returnVal.DateTime.Minute);
                        returnVal.DateTime = returnVal.DateTime.AddHours(23 - returnVal.DateTime.Hour);

                        // Set the hour/minute/second flags as explicit so that normalization and comparison is applied properly in the future.
                        returnVal.SetParts = returnVal.SetParts | DateTimeParts.Hour | DateTimeParts.Minute | DateTimeParts.Second | DateTimeParts.AmPmUnambiguous;
                    }
                    else if (partOfDay == PartOfDay.Noon && !returnVal.SetParts.HasFlag(DateTimeParts.Hour))
                    {
                        returnVal.DateTime = returnVal.DateTime.AddSeconds(0 - returnVal.DateTime.Second);
                        returnVal.DateTime = returnVal.DateTime.AddMinutes(0 - returnVal.DateTime.Minute);
                        returnVal.DateTime = returnVal.DateTime.AddHours(12 - returnVal.DateTime.Hour);
                        returnVal.SetParts = returnVal.SetParts | DateTimeParts.Hour | DateTimeParts.Minute | DateTimeParts.Second | DateTimeParts.AmPmUnambiguous;
                    }

                    // Set the hour field to the approximate time of day; this will help with comparisons later (possibly)
                    if (!returnVal.SetParts.HasFlag(DateTimeParts.Hour))
                    {
                        returnVal.DateTime = returnVal.DateTime.AddHours(partOfDay.ToApproximateHour() - returnVal.DateTime.Hour);
                    }

                    returnVal.PartOfDay = partOfDay;
                    returnVal.SetParts |= DateTimeParts.PartOfDay;
                    returnVal.ExplicitSetParts |= DateTimeParts.PartOfDay;
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse PartOfDay attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[Iso8601.PartOfDay]));
                }
            }
        }

        private static void ParseTimezone(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.TimeZone))
            {
                returnVal.TimeZone = timexDictionary[Iso8601.TimeZone];
                returnVal.SetParts |= DateTimeParts.TimeZone;
                returnVal.ExplicitSetParts |= DateTimeParts.TimeZone;
            }
        }

        private static void ParseOffset(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(TimexAttributes.OffsetUnit) &&
                !string.IsNullOrEmpty(timexDictionary[TimexAttributes.OffsetUnit]))
            {
                TemporalUnit offsetUnit;

                if (EnumExtensions.TryParse(timexDictionary[TimexAttributes.OffsetUnit], out offsetUnit))
                {
                    int offset;

                    if (timexDictionary.ContainsKey(TimexAttributes.Offset) &&
                        !string.IsNullOrEmpty(timexDictionary[TimexAttributes.Offset]))
                    {
                        if (int.TryParse(timexDictionary[TimexAttributes.Offset], out offset))
                        {
                            returnVal.Offset = offset;
                            returnVal.OffsetUnit = offsetUnit;

                            // Do some extra validation here: If the offset is "weekend", do not allow hour, minute, or second to be specified since that makes no sense
                            if ((offsetUnit == TemporalUnit.Week ||
                                offsetUnit == TemporalUnit.Weekend) &&
                                (timexDictionary.ContainsKey(Iso8601.Hour) ||
                                timexDictionary.ContainsKey(Iso8601.Minute) ||
                                timexDictionary.ContainsKey(Iso8601.Second)))
                            {
                                throw new TimexException("Cannot specify time information for expressions that are represented in units of weeks or weekends (e.g. \"5 PM this week\")");
                            }

                            // Special case: If we have specified a rule like "this week Monday", check to make sure that the proper week
                            // is enforced
                            if (offsetUnit == TemporalUnit.Week && returnVal.SetParts.HasFlag(DateTimeParts.WeekDay))
                            {
                                int isoYear;
                                int currentWeek = TimexHelpers.GetIso8601WeekOfYear(returnVal.DateTime, out isoYear);
                                int targetWeek = TimexHelpers.GetIso8601WeekOfYear(returnVal.Context.ReferenceDateTime, out isoYear);
                                if (targetWeek != currentWeek)
                                    returnVal.DateTime = returnVal.DateTime.AddDays((targetWeek - currentWeek) * 7);
                            }
                        }
                        else
                        {
                            throw new TimexException(string.Format("Could not parse Offset attribute \"{0}\" from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[TimexAttributes.Offset]));
                        }
                    }
                    else
                    {
                        // Actual offset is vague. Set it to the default value from the context and then add the "APPROX" modifier
                        returnVal.Offset = returnVal.Context.DefaultValueOfVagueOffset[offsetUnit];
                        returnVal.OffsetUnit = offsetUnit;
                        returnVal.Modifier = Enums.Modifier.Approximately;
                    }

                    if (timexDictionary.ContainsKey(TimexAttributes.OffsetAnchor) &&
                        !string.IsNullOrEmpty(timexDictionary[TimexAttributes.OffsetAnchor]))
                    {
                        returnVal.OffsetAnchor = timexDictionary[TimexAttributes.OffsetAnchor];
                        returnVal.SetParts |= DateTimeParts.OffsetAnchor;
                        returnVal.ExplicitSetParts |= DateTimeParts.OffsetAnchor;
                    }
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse OffsetUnit attribute \"{0}\" from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[TimexAttributes.OffsetUnit]));
                }
            }

            // COMPOUND OFFSET
            if (timexDictionary.ContainsKey(TimexAttributes.CompoundOffset) &&
                !string.IsNullOrEmpty(timexDictionary[TimexAttributes.CompoundOffset]))
            {
                int compoundOffset = 0;
                if (int.TryParse(timexDictionary[TimexAttributes.CompoundOffset], out compoundOffset))
                {
                    returnVal.CompoundOffset = compoundOffset;
                }
            }
        }

        private static void ParseReference(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.Reference) &&
                !string.IsNullOrEmpty(timexDictionary[Iso8601.Reference]))
            {
                DateTimeReference reference;
                if (EnumExtensions.TryParse(timexDictionary[Iso8601.Reference], out reference))
                {
                    returnVal.Reference = reference;
                    returnVal.SetParts |= DateTimeParts.Reference;
                    returnVal.ExplicitSetParts |= DateTimeParts.Reference;
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse Reference attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[Iso8601.Reference]));
                }
            }
        }

        private static void ParseSeason(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.Season) && !string.IsNullOrEmpty(timexDictionary[Iso8601.Season]))
            {
                Season season;
                if (EnumExtensions.TryParse(timexDictionary[Iso8601.Season], out season))
                {
                    returnVal.Season = season;
                    returnVal.SetParts |= DateTimeParts.Season;
                    returnVal.ExplicitSetParts |= DateTimeParts.Season;
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse Season attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[Iso8601.Season]));
                }
            }
        }

        private static void ParsePartOfYear(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(Iso8601.PartOfYear) && !string.IsNullOrEmpty(timexDictionary[Iso8601.PartOfYear]))
            {
                PartOfYear partOfYear;
                if (EnumExtensions.TryParse(timexDictionary[Iso8601.PartOfYear], out partOfYear))
                {
                    returnVal.PartOfYear = partOfYear;
                    returnVal.SetParts |= DateTimeParts.PartOfYear;
                    returnVal.ExplicitSetParts |= DateTimeParts.PartOfYear;
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse PartOfYear attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[Iso8601.PartOfYear]));
                }
            }
        }

        private static void ParseDuration(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            long duration = 0;
            TemporalUnit durationUnit = TemporalUnit.Second;

            // RAW DURATION
            if (timexDictionary.ContainsKey(TimexAttributes.RawDuration) &&
                !string.IsNullOrEmpty(timexDictionary[TimexAttributes.RawDuration]))
            {
                if (long.TryParse(timexDictionary[TimexAttributes.RawDuration], out duration))
                {
                    returnVal.Duration = new DurationValue(duration);
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse RawDuration attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[TimexAttributes.RawDuration]));
                }
            }
            else
            {
                if (timexDictionary.ContainsKey(TimexAttributes.Duration) &&
                    !string.IsNullOrEmpty(timexDictionary[TimexAttributes.Duration])) // OLD-STYLE DURATION
                {
                    // Try to parse the duration as an integer, but remember that it may be an "X" for unspecified durations.
				    // In this case, only the duration unit will be set.
                    if (!long.TryParse(timexDictionary[TimexAttributes.Duration], out duration))
                    {
                        duration = 0;
                    }
                }

                if (timexDictionary.ContainsKey(TimexAttributes.DurationUnit) &&
                    !string.IsNullOrEmpty(timexDictionary[TimexAttributes.DurationUnit]))
                {
                    if (!EnumExtensions.TryParse(timexDictionary[TimexAttributes.DurationUnit], out durationUnit))
                    {
                        throw new TimexException(string.Format("Could not parse DurationUnit attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[TimexAttributes.DurationUnit]));
                    }
                    returnVal.Duration = new DurationValue(duration, durationUnit);
                }
                else
                {
                    returnVal.Duration = new DurationValue(duration, null);
                }
            }

            // Detect if this timex is a set. If so, then this "duration" is actually a legacy expression for frequency unit.
            // Change the internal values here
            if (returnVal.TemporalType == TemporalType.Set)
            {
                Tuple<int, TemporalUnit?> parsedDuration = returnVal.Duration.SimpleValue;
                returnVal.Frequency = parsedDuration.Item1;
                returnVal.FrequencyUnit = parsedDuration.Item2;
                returnVal.Duration = new DurationValue(0, null);
            }
        }

        private static void ParseModifier(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(TimexAttributes.Mod) &&
                !string.IsNullOrEmpty(timexDictionary[TimexAttributes.Mod]))
            {
                Modifier modifier;
                if (EnumExtensions.TryParse(timexDictionary[TimexAttributes.Mod], out modifier))
                {
                    returnVal.Modifier = modifier;
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse Mod attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[TimexAttributes.Mod]));
                }
            }
        }

        private static void ParseQuantity(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            // Sets always have quantity 1 by default
            if (returnVal.TemporalType == TemporalType.Set)
            {
                returnVal.Quantity = 1;
            }
            
            if (timexDictionary.ContainsKey(TimexAttributes.Quantity) &&
                !string.IsNullOrEmpty(timexDictionary[TimexAttributes.Quantity]))
            {
                int quantity;
                if (int.TryParse(timexDictionary[TimexAttributes.Quantity], out quantity))
                {
                    returnVal.Quantity = quantity;
                }
                else if (string.Equals("EACH", timexDictionary[TimexAttributes.Quantity]) || string.Equals("EVERY", timexDictionary[TimexAttributes.Quantity]))
                {
                    // Old grammars can still specify QUANTITY = EACH or similar, but this is not actually a quantity, it's a frequency. It now gets handled by ParseFrequency
                    return;
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse Quantity attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[TimexAttributes.Quantity]));
                }
            }
        }

        private static void ParseFrequency(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (timexDictionary.ContainsKey(TimexAttributes.Frequency) &&
                 !string.IsNullOrEmpty(timexDictionary[TimexAttributes.Frequency]))
            {
                // Does the grammar also specify a freq_unit?
                if (timexDictionary.ContainsKey(TimexAttributes.FrequencyUnit) &&
                 !string.IsNullOrEmpty(timexDictionary[TimexAttributes.FrequencyUnit]))
                {
                    // Parse new-style frequencies that are split between FREQ and FREQ_UNIT fields
                    int frequency;
                    if (int.TryParse(timexDictionary[TimexAttributes.Frequency], out frequency))
                    {
                        returnVal.Frequency = frequency;
                    }
                    else
                    {
                        throw new TimexException(string.Format("Could not parse Frequency attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[TimexAttributes.Frequency]));
                    }
                    
                    TemporalUnit frequencyUnit;
                    if (EnumExtensions.TryParse(timexDictionary[TimexAttributes.FrequencyUnit], out frequencyUnit))
                    {
                        returnVal.FrequencyUnit = frequencyUnit;
                    }
                    else
                    {
                        throw new TimexException(string.Format("Could not parse FrequencyUnit attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[TimexAttributes.FrequencyUnit]));
                    }
                }
                else
                {
                    // "Old-style" frequencies are expressed in the format "8hour" ("Every 8 hours"), "10day" ("Every 10 days"), etc.
                    // Parse the left half as an integer and the right half as a TemporalUnit
                    Match match = FrequencyMatcher.Match(timexDictionary[TimexAttributes.Frequency]);
                    if (match.Success)
                    {
                        var frequencyString = match.Groups[1].Value;
                        int frequency;
                        if (int.TryParse(frequencyString, out frequency))
                        {
                            returnVal.Frequency = frequency;
                        }
                        else
                        {
                            throw new TimexException(string.Format("Could not parse Frequency attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[TimexAttributes.Frequency]));
                        }

                        if (match.Groups[2].Success)
                        {
                            var frequencyUnitString = match.Groups[2].Value;
                            TemporalUnit frequencyUnit;
                            if (EnumExtensions.TryParse(frequencyUnitString, out frequencyUnit))
                            {
                                returnVal.FrequencyUnit = frequencyUnit;
                            }

                            // If we can't parse the frequency unit, don't throw an exception. Unitless frequencies are allowed, e.g. FREQ=1 for events that only occur one time (wait, what?)
                        }
                    }
                }
            }
        }

        private static void ParseWeekOf(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            // Capture the "weekof" modifier
            if (timexDictionary.ContainsKey(TimexAttributes.WeekOf) &&
                !string.IsNullOrEmpty(timexDictionary[TimexAttributes.WeekOf]))
            {
                returnVal.SetParts |= DateTimeParts.WeekOfExpression;
                returnVal.ExplicitSetParts |= DateTimeParts.WeekOfExpression;
            }
        }

        private static void ParseRangeHint(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            // Capture the "range_hint" modifier
            if (timexDictionary.ContainsKey(TimexAttributes.RangeHint) &&
                !string.IsNullOrEmpty(timexDictionary[TimexAttributes.RangeHint]))
            {
                returnVal.ValidForRanges = false;
            }
        }

        private static void ParseMinOffset(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            // Capture the "min_offset" modifier
            if (timexDictionary.ContainsKey(TimexAttributes.MinimumOffset) &&
                   !string.IsNullOrEmpty(timexDictionary[TimexAttributes.MinimumOffset]))
            {
                int value = 0;
                if (int.TryParse(timexDictionary[TimexAttributes.MinimumOffset], out value))
                {
                    returnVal.MinOffset = value;
                }
                else
                {
                    throw new TimexException(string.Format("Could not parse MIN_OFFSET attribute {0} from timex dictionary! (Did you forget to Normalize() in the grammar?)", timexDictionary[TimexAttributes.MinimumOffset]));
                }
            }
        }

#endregion

        #region Formatters & Resolvers

        /// <summary>
        /// Given a timex dictionary with flags, augments the passed-in ExtendedDateTime to resolve it to the correct am/pm interpretation
        /// </summary>
        /// <param name="timexDictionary"></param>
        /// <param name="returnVal"></param>
        private static void InferAmPm(IDictionary<string, string> timexDictionary, ExtendedDateTime returnVal)
        {
            if (returnVal == null)
                throw new ArgumentNullException("returnVal");
            if (timexDictionary == null)
                throw new ArgumentNullException("timexDictionary");

            // Handle am/pm hour ambiguity here
            if (!returnVal.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous) &&
                timexDictionary.ContainsKey(Iso8601.Hour))
            {
                // The timex grammar can specify if "am" or "pm" was ambiguous in the input expression, using timex["AMPM"] = "not_specified"
                bool amPmMadeExplicitInGrammar = !timexDictionary.ContainsKey(TimexAttributes.AmPm);
                // The presence of DateTimeParts_Am_Pm indicates that the am/pm expression is unambiguous.
                // An hour value greater than 12 also triggers this
                if (amPmMadeExplicitInGrammar ||
                    (!amPmMadeExplicitInGrammar && returnVal.DateTime.Hour > 12))
                {
                    returnVal.SetParts |= DateTimeParts.AmPmUnambiguous;
                    returnVal.ExplicitSetParts |= DateTimeParts.AmPmUnambiguous;
                }
                else if (returnVal.Context.UseInference && returnVal.TemporalType != TemporalType.Set)
                {
                    // Make a reasonable inference if the context calls for it
                    int currentHour = returnVal.DateTime.Hour;
                    int referenceHour = returnVal.Context.ReferenceDateTime.Hour;
                    int currentMinuteOfDay = (currentHour * 60) + returnVal.DateTime.Minute;
                    int referenceMinuteOfDay = (referenceHour * 60) + returnVal.Context.ReferenceDateTime.Minute;
                    // TODO: Don't stop at minutes; make sure it has seconds granularity as well
                    // Catch the edge case for if input was "today"
                    bool zeroDayOffset = returnVal.OffsetUnit == TemporalUnit.Day && returnVal.Offset == 0;
                    // and the case where input is "tomorrow", "next saturday", etc.
                    bool positiveOffset = returnVal.OffsetUnit != null && returnVal.Offset != 0;
                    // Check if the input has explicitly said which day it refers to (this determines if am/pm inference can cross the day boundary)
                    bool dayIsFixed = returnVal.SetParts.HasFlag(DateTimeParts.Day) ||
                        returnVal.SetParts.HasFlag(DateTimeParts.WeekDay) ||
                        timexDictionary.ContainsKey(TimexAttributes.Offset);
                    // Put all of that together to determine if the inferred time should fall within the current day or not
                    bool dayIsToday = !positiveOffset && (zeroDayOffset || !dayIsFixed || returnVal.DateTime.DayOfYear == returnVal.Context.ReferenceDateTime.DayOfYear);
                
                    // Do we have PartOfDay information available? Use that
                    if (returnVal.SetParts.HasFlag(DateTimeParts.PartOfDay))
                    {
                        // Find the approximate hour that the part of day refers to, and then use that to determine
                        // if the current hour should be am or pm
                        int closestHour = returnVal.PartOfDay.ToApproximateHour();
                        int amDifference = Math.Abs(closestHour - currentHour);
                        if (amDifference > 12)  // This captures the wraparound, so, for example, "1:00 AM" can get close to "tonight"
                        {
                            amDifference = 24 - amDifference;
                        }
                        int pmDifference = Math.Abs(closestHour - (currentHour + 12));
                        if (pmDifference > 12)  // Again, capture the wraparound
                        {
                            pmDifference = 24 - pmDifference;
                        }
                        if (amDifference > pmDifference)
                        {
                            returnVal.DateTime = returnVal.DateTime.AddHours(12);
                        }

                        // Am/pm is pretty unambiguous in this case. Set the flag.
                        returnVal.SetParts |= DateTimeParts.AmPmUnambiguous;
                    }
                    // Use the past/future normalization flag to determine the next instance of that hour that will occur
                    // for example, "9" will resolve to 9 PM if reference time is 1 pm, but will resolve to 9 AM if reference time is 6 AM
                    else if (returnVal.Context.Normalization == Normalization.Future && dayIsToday)
                    {
                        // Difference of more than 12 hours - this means the user is probably referring to a time tomorrow
                        if (currentMinuteOfDay + (12 * 60) <= referenceMinuteOfDay)
                        {
                            // Prevent times like 1 AM from showing up
                            // (i.e. reference time is 6 pm, input was "2:00", make an inference that the user
                            // probably meant 2:00 pm tomorrow, and not 2:00 am the next morning)
                            if (currentHour < returnVal.Context.AmPmInferenceCutoff)
                            {
                                returnVal.DateTime = returnVal.DateTime.AddHours(12);
                            }
                        }
                        // Difference of less than 12 hours - this is the most typical case (e.g. we said "2" while it was 3:00 PM; switch AM to PM)
                        else if (currentMinuteOfDay <= referenceMinuteOfDay)
                        {
                            returnVal.DateTime = returnVal.DateTime.AddHours(12);
                        }
                        // Catch a single edge case where refTime = a little after midnight, and input was something like "12:55"
                        else if (currentHour == 12 && referenceHour == 0 && currentMinuteOfDay >= referenceMinuteOfDay)
                        {
                        	returnVal.DateTime = returnVal.DateTime.AddHours(-12);
                        }

                    }
                    else if (returnVal.Context.Normalization == Normalization.Past && dayIsToday)
                    {
                        // If, for example, input was "3:00" and reference time is 6:00 PM.
                        // Change from 3:00 AM to 3:00 PM to close up the gap
                        if ((currentMinuteOfDay + (12 * 60) <= referenceMinuteOfDay))
                        {
                            returnVal.DateTime = returnVal.DateTime.AddHours(12);
                        }
                        // Difference of more than 1 hour - flip the AM value to a PM in the previous day
                        else if (currentMinuteOfDay >= referenceMinuteOfDay && !dayIsFixed)
                        {
                            returnVal.DateTime = returnVal.DateTime.AddHours(12);
                        }
                    }
                    else if (returnVal.DateTime.Hour < returnVal.Context.AmPmInferenceCutoff)
                    {
                        // Present normalization (or day is different from current day)
                        // Use the context's ampm inference cutoff, numbers lower than the cutoff are interpreted as PM
                        returnVal.DateTime = returnVal.DateTime.AddHours(12);
                    }

                    // Note that the "unspecified_ampm" flag will still be passed along; it can help postprocessing
                }
            }
        }

        public string FormatType()
        {
            return EnumExtensions.ToString(TemporalType);
        }

        /// <summary>
        /// Returns the comment that should be associated with this datetime object. Typical usage
        /// allows this to transmit inference and uncertainties in the annotation.
        /// </summary>
        public string FormatComment()
        {
            var valueBuilder = new StringBuilder();
            bool wroteFirst = false;

            if (SetParts.HasFlag(DateTimeParts.WeekOfExpression))
            {
                if (wroteFirst)
                {
                    valueBuilder.Append(" ");
                }
                wroteFirst = true;

                valueBuilder.Append(WeekofString);
            }

            if ((!Context.UseInference || TemporalType == TemporalType.Set) &&
                SetParts.HasFlag(DateTimeParts.Hour) &&
                !SetParts.HasFlag(DateTimeParts.AmPmUnambiguous))
            {
                if (wroteFirst)
                {
                    valueBuilder.Append(" ");
                }
                wroteFirst = true;

                valueBuilder.Append(AmPmUnspecifiedString);
            }

            if (TemporalType == TemporalType.Time &&
                Offset.HasValue &&
                Offset.Value != 0 &&
                OffsetUnit.HasValue &&
                !OffsetUnit.IsWeekday() &&
                (SetParts == DateTimeParts.None || (SetParts.HasFlag(DateTimeParts.OffsetAnchor) && string.Equals(OffsetAnchor, OffsetAnchorTodayString))))
            {
                if (wroteFirst)
                {
                    valueBuilder.Append(" ");
                }
                wroteFirst = true;

                valueBuilder.Append(ResolutionWasRelativeString);
            }

            return valueBuilder.ToString();
        }

        /// <summary>
        /// Returns this datetime as a formatted string, according to whatever
        /// parameters were used to construct this object. I.e. for dates and times
        /// this will return 20XX-06-01T11:27:01
        /// For durations it will be "PT5h", etc. This is all according to iso 8601 format
        /// </summary>
        public string FormatValue()
        {
            if (string.IsNullOrEmpty(_cachedValueString))
            {
                // If it's a recurrence then use that formatter
                if (TemporalType == TemporalType.Set)
                {
                    _cachedValueString = FormatValueAsRecurrence();
                }

                // if duration is specified format value using duration
                else if (Duration.IsSet())
                {
                    _cachedValueString = Duration.FormatValue();
                }

                // if offset is specified format value using offset
                else if (Offset.HasValue && OffsetUnit.HasValue)
                {
                    _cachedValueString = FormatValueUsingOffset();
                }

                // by default format value using datetime
                else if (Context.UseInference)
                {
                    _cachedValueString = FormatValueUsingDateTime();
                }
                else
                {
                    _cachedValueString = FormatValueUsingDateTimeWithoutInference();
                }
            }

            return _cachedValueString;
        }

        /// <summary>
        /// Standard method to format this date as an ISO8601 string.
        /// </summary>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "formatting method with a large if else statement")]
        private string FormatValueUsingDateTime()
        {
            // get adjusted ReferenceDateTime based on normalization direction
            var adjustedReferenceDateTime = AdjustReferenceDateTime();

            var year = adjustedReferenceDateTime.Year.ToString("D4");
            if (SetParts.HasFlag(DateTimeParts.Year))
            {
                year = DateTime.Year.ToString("D4");
            }
            else if (SetParts.HasFlag(DateTimeParts.Decade))
            {
                year = DateTime.Year.ToString("D4").Substring(0, 3) + "X";
            }
            else if (SetParts.HasFlag(DateTimeParts.Century))
            {
                year = DateTime.Year.ToString("D4").Substring(0, 2) + "XX";
            }
            else if (SetParts.HasFlag(DateTimeParts.Millenium))
            {
                year = DateTime.Year.ToString("D4").Substring(0, 1) + "XXX";
            }
            else if (SetParts.HasFlag(DateTimeParts.DecadeYear))
            {
                year = "XX" + DateTime.Year.ToString("D4").Substring(2, 2);
            }
            var month = SetParts.HasFlag(DateTimeParts.Month)
                ? DateTime.Month.ToString("D2")
                : adjustedReferenceDateTime.Month.ToString("D2");
            var day = SetParts.HasFlag(DateTimeParts.Day)
                ? DateTime.Day.ToString("D2")
                : adjustedReferenceDateTime.Day.ToString("D2");
            var hour = SetParts.HasFlag(DateTimeParts.Hour)
                ? DateTime.Hour.ToString("D2")
                : adjustedReferenceDateTime.Hour.ToString("D2");
            var minute = SetParts.HasFlag(DateTimeParts.Minute)
                ? DateTime.Minute.ToString("D2")
                : adjustedReferenceDateTime.Minute.ToString("D2");
            var second = SetParts.HasFlag(DateTimeParts.Second)
                ? DateTime.Second.ToString("D2")
                : adjustedReferenceDateTime.Second.ToString("D2");

            int isoYear;
            int weekNum = SetParts.HasFlag(DateTimeParts.Week)
                ? TimexHelpers.GetIso8601WeekOfYear(DateTime, out isoYear)
                : TimexHelpers.GetIso8601WeekOfYear(adjustedReferenceDateTime, out isoYear);

            string week = weekNum.ToString("D2");

            int weekDayNum = SetParts.HasFlag(DateTimeParts.WeekDay) ?
                TimexHelpers.GetIso8601DayOfWeek(DateTime) :
                TimexHelpers.GetIso8601DayOfWeek(adjustedReferenceDateTime);

            // ISO8601 handles weeks and years a little unintuitively. Case in point: Jan 1 2012 is 2011-W52-7 ; it's considered part of year 2011.
            // For that reason, we need this block of code to resolve it. If the year, month, and week are all defined, convert
            // the "common" year (2012) into the ISO year (2011).
            // WeekYear stores the resulting ISO week number.
            int weekYearNum = adjustedReferenceDateTime.Year;
            string weekYear = year;
            int yearNumber;
            int monthNumber;
            if (SetParts.HasFlag(DateTimeParts.Year) &&
                SetParts.HasFlag(DateTimeParts.Month) &&
                int.TryParse(year, out yearNumber) &&
                int.TryParse(month, out monthNumber))
            {
                weekYearNum = TimexHelpers.GetIso8601WeekYear(yearNumber, monthNumber, weekNum);
                weekYear = weekYearNum.ToString("D4");
            }

            var valueBuilder = new StringBuilder();

            // date part
            if (SetParts.HasFlag(DateTimeParts.Day))
            {
                valueBuilder.AppendFormat(Iso8601.DayTemplate, year, month, day);
            }
            else if (SetParts.HasFlag(DateTimeParts.Month))
            {
                valueBuilder.AppendFormat(Iso8601.MonthTemplate, year, month);
            }
            else if (SetParts.HasFlag(DateTimeParts.WeekDay))
            {
                valueBuilder.Append(ConvertWeekFormatToStandard(weekYearNum, weekNum, weekDayNum));
            }
            else if (SetParts.HasFlag(DateTimeParts.Week))
            {
                valueBuilder.AppendFormat(Iso8601.WeekTemplate, weekYear, week);
            }
            else if (SetParts.HasFlag(DateTimeParts.PartOfYear))
            {
                valueBuilder.AppendFormat(Iso8601.PartOfYearTemplate, year,
                                          EnumExtensions.ToString(PartOfYear));
            }
            else if (SetParts.HasFlag(DateTimeParts.Season))
            {
                valueBuilder.AppendFormat(Iso8601.SeasonTemplate, year, EnumExtensions.ToString(Season));
            }
            else if (SetParts.HasFlag(DateTimeParts.Year) ||
                     SetParts.HasFlag(DateTimeParts.Decade) ||
                     SetParts.HasFlag(DateTimeParts.Century) ||
                     SetParts.HasFlag(DateTimeParts.Millenium) ||
                     SetParts.HasFlag(DateTimeParts.DecadeYear))
            {
                valueBuilder.AppendFormat(Iso8601.YearTemplate, year);
            }
            else if (SetParts.HasFlag(DateTimeParts.Reference))
            {
                valueBuilder.Append(EnumExtensions.ToString(Reference));
            }
            else
            {
                valueBuilder.AppendFormat(Iso8601.DayTemplate, year, month, day);
            }

            // time part
            if (SetParts.HasFlag(DateTimeParts.PartOfDay) && (PartOfDay == PartOfDay.Midnight || PartOfDay == PartOfDay.Noon))
            {
                valueBuilder.AppendFormat(Iso8601.TimeTemplate, EnumExtensions.ToString(PartOfDay));
            }
            else if (SetParts.HasFlag(DateTimeParts.Second))
            {
                valueBuilder.AppendFormat(Iso8601.SecondTemplate, hour, minute, second);
            }
            else if (SetParts.HasFlag(DateTimeParts.Minute))
            {
                valueBuilder.AppendFormat(Iso8601.MinuteTemplate, hour, minute);
            }
            else if (SetParts.HasFlag(DateTimeParts.Hour))
            {
                valueBuilder.AppendFormat(Iso8601.HourTemplate, hour);
            }
            else if (SetParts.HasFlag(DateTimeParts.PartOfDay))
            {
                valueBuilder.AppendFormat(Iso8601.TimeTemplate, EnumExtensions.ToString(PartOfDay));
            }

            if (SetParts.HasFlag(DateTimeParts.TimeZone))
            {
                valueBuilder.Append(TimeZone);
            }

            return valueBuilder.ToString();
        }

        /// <summary>
        /// Formats this date as an ISO8601 string, substituting "X" for any unknown fields.
        /// </summary>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "formatting method with a large if else statement")]
        private string FormatValueUsingDateTimeWithoutInference()
        {
            var year = "XXXX";
            if (SetParts.HasFlag(DateTimeParts.Year))
            {
                year = DateTime.Year.ToString("D4");
            }
            else if (SetParts.HasFlag(DateTimeParts.Decade))
            {
                year = DateTime.Year.ToString("D4").Substring(0, 3) + "X";
            }
            else if (SetParts.HasFlag(DateTimeParts.Century))
            {
                year = DateTime.Year.ToString("D4").Substring(0, 2) + "XX";
            }
            else if (SetParts.HasFlag(DateTimeParts.Millenium))
            {
                year = DateTime.Year.ToString("D4").Substring(0, 1) + "XXX";
            }
            else if (SetParts.HasFlag(DateTimeParts.DecadeYear))
            {
                year = "XX" + DateTime.Year.ToString("D4").Substring(2, 2);
            }
            var month = SetParts.HasFlag(DateTimeParts.Month)
                ? DateTime.Month.ToString("D2")
                : "XX";
            var day = SetParts.HasFlag(DateTimeParts.Day)
                ? DateTime.Day.ToString("D2")
                : "XX";
            var hour = SetParts.HasFlag(DateTimeParts.Hour)
                ? DateTime.Hour.ToString("D2")
                : "XX";
            var minute = SetParts.HasFlag(DateTimeParts.Minute)
                ? DateTime.Minute.ToString("D2")
                : "XX";
            var second = SetParts.HasFlag(DateTimeParts.Second)
                ? DateTime.Second.ToString("D2")
                : "XX";
            int isoYear;
            var week = SetParts.HasFlag(DateTimeParts.Week)
                ? TimexHelpers.GetIso8601WeekOfYear(DateTime, out isoYear).ToString("D2")
                : "XX";
            var weekDay = SetParts.HasFlag(DateTimeParts.WeekDay) ?
                TimexHelpers.GetIso8601DayOfWeek(DateTime).ToString("D1") :
                "X";

            // Resolve the "common" year into the ISO year that is calculated according to a specified
            // year/month/week value. WeekYear may therefore be different from the calendar year.
            int weekYearNum = 0;
            string weekYearString = year;

            int yearNumber;
            int monthNumber;
            int weekNumber = 0;
            if (int.TryParse(year, out yearNumber) &&
                int.TryParse(month, out monthNumber) &&
                int.TryParse(week, out weekNumber))
            {
                weekYearNum = TimexHelpers.GetIso8601WeekYear(yearNumber, monthNumber, weekNumber);
                weekYearString = weekYearNum.ToString("D4");
            }

            var valueBuilder = new StringBuilder();

            TemporalUnit? approximateRecurrenceUnit = FrequencyUnit;
            if (!approximateRecurrenceUnit.HasValue)
            {
                approximateRecurrenceUnit = DurationUnit;
            }

            // The following few cases are basically a hack to get recurrences to output in the "correct" way
            if (approximateRecurrenceUnit == TemporalUnit.Day) // "Every Day"
            {
                valueBuilder.AppendFormat(Iso8601.DayTemplate, "XXXX", "XX", "XX");
            }
            else if (approximateRecurrenceUnit == TemporalUnit.Month) // "Every Month" or "the 19th of each month"
            {
                if (SetParts.HasFlag(DateTimeParts.Day))
                {
                    valueBuilder.AppendFormat(Iso8601.DayTemplate, "XXXX", "XX", "XX");
                }
                else
                {
                    valueBuilder.AppendFormat(Iso8601.MonthTemplate, "XXXX", "XX");
                }
            }
            else if (approximateRecurrenceUnit == TemporalUnit.Year) // "Every Year" or "Every September"
            {
                if (SetParts.HasFlag(DateTimeParts.Month))
                {
                    if (SetParts.HasFlag(DateTimeParts.Day))
                    {
                        valueBuilder.AppendFormat(Iso8601.DayTemplate, "XXXX", "XX", day);
                    }
                    else
                    {
                        valueBuilder.AppendFormat(Iso8601.MonthTemplate, "XXXX", month);
                    }
                }
                else if (SetParts.HasFlag(DateTimeParts.Season))
                {
                    valueBuilder.AppendFormat(Iso8601.SeasonTemplate, "XXXX", EnumExtensions.ToString(Season));
                }
                else
                {
                    valueBuilder.AppendFormat(Iso8601.YearTemplate, "XXXX");
                }
            }
            else if (approximateRecurrenceUnit == TemporalUnit.Week) // "Every Week" or "Every Tuesday"
            {
                if (SetParts.HasFlag(DateTimeParts.WeekDay))
                {
                    valueBuilder.AppendFormat(Iso8601.WeekDayTemplate, "XXXX", "XX", weekDay);
                }
                else
                {
                    valueBuilder.AppendFormat(Iso8601.WeekTemplate, "XXXX", "XX");
                }
            }
            else if (approximateRecurrenceUnit == TemporalUnit.Weekend) // "Every Weekend"
            {
                valueBuilder.AppendFormat(Iso8601.WeekEndTemplate, "XXXX", "XX");
            }
            else if (approximateRecurrenceUnit == TemporalUnit.Weekdays) // "Every Weekday"
            {
                valueBuilder.AppendFormat(Iso8601.WeekDaysTemplate, "XXXX", "XX");
            }
            // Always output the entire date/time, to prevent adding ambiguity.
            // The only variant is if we are in day-of-week mode or not. This would determine
            // if we should use YYYY-MM-DD format or YYYY-WW-D format
            else if (SetParts.HasFlag(DateTimeParts.PartOfYear))
            {
                valueBuilder.AppendFormat(Iso8601.PartOfYearTemplate, year,
                                          EnumExtensions.ToString(PartOfYear));
            }
            else if (SetParts.HasFlag(DateTimeParts.Season))
            {
                valueBuilder.AppendFormat(Iso8601.SeasonTemplate, year, EnumExtensions.ToString(Season));
            }
            else if (SetParts.HasFlag(DateTimeParts.WeekDay) ||
                SetParts.HasFlag(DateTimeParts.Week))
            {
                // If all fields are specified, convert it out of the yyyy-ww-d format
                if (SetParts.HasFlag(DateTimeParts.Week) &&
                    SetParts.HasFlag(DateTimeParts.WeekDay) &&
                    SetParts.HasFlag(DateTimeParts.Year))
                {
                    valueBuilder.Append(ConvertWeekFormatToStandard(weekYearNum, weekNumber, TimexHelpers.GetIso8601DayOfWeek(DateTime)));
                }
                else if (SetParts.HasFlag(DateTimeParts.Week) &&
                    SetParts.HasFlag(DateTimeParts.Year))
                {
                    valueBuilder.AppendFormat(Iso8601.WeekTemplate, weekYearString, week);
                }
                else
                {
                    // I don't know what could hit this case
                    valueBuilder.AppendFormat(Iso8601.WeekDayTemplate, weekYearString, week, weekDay);
                }
            }
            else if (SetParts.HasFlag(DateTimeParts.Reference))
            {
                valueBuilder.Append(EnumExtensions.ToString(Reference));
            }
            else if (SetParts.HasFlag(DateTimeParts.Day))
            {
                valueBuilder.AppendFormat(Iso8601.DayTemplate, year, month, day);
            }
            else if (SetParts.HasFlag(DateTimeParts.Month))
            {
                valueBuilder.AppendFormat(Iso8601.MonthTemplate, year, month);
            }
            else if (SetParts.HasFlag(DateTimeParts.Year) ||
                        SetParts.HasFlag(DateTimeParts.Decade) ||
                        SetParts.HasFlag(DateTimeParts.Century) ||
                        SetParts.HasFlag(DateTimeParts.Millenium) ||
                        SetParts.HasFlag(DateTimeParts.DecadeYear))
            {
                valueBuilder.AppendFormat(Iso8601.YearTemplate, year);
            }

            // time part
            if (SetParts.HasFlag(DateTimeParts.PartOfDay) && (PartOfDay == PartOfDay.Midnight || PartOfDay == PartOfDay.Noon))
            {
                valueBuilder.AppendFormat(Iso8601.TimeTemplate, EnumExtensions.ToString(PartOfDay));
            }
            else if (SetParts.HasFlag(DateTimeParts.Second))
            {
                valueBuilder.AppendFormat(Iso8601.SecondTemplate, hour, minute, second);
            }
            else if (SetParts.HasFlag(DateTimeParts.Minute))
            {
                valueBuilder.AppendFormat(Iso8601.MinuteTemplate, hour, minute);
            }
            else if (SetParts.HasFlag(DateTimeParts.Hour))
            {
                valueBuilder.AppendFormat(Iso8601.HourTemplate, hour);
            }
            else if (SetParts.HasFlag(DateTimeParts.PartOfDay))
            {
                valueBuilder.AppendFormat(Iso8601.TimeTemplate, EnumExtensions.ToString(PartOfDay));
            }

            if (SetParts.HasFlag(DateTimeParts.TimeZone))
            {
                valueBuilder.Append(TimeZone);
            }

            return valueBuilder.ToString();
        }

        /// <summary>
        /// Formats this date as an ISO8601 string in the context of a recurrence
        /// </summary>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "formatting method with a large if else statement")]
        private string FormatValueAsRecurrence()
        {
            var year = "XXXX";
            if (SetParts.HasFlag(DateTimeParts.Year))
            {
                year = DateTime.Year.ToString("D4");
            }
            else if (SetParts.HasFlag(DateTimeParts.Decade))
            {
                year = DateTime.Year.ToString("D4").Substring(0, 3) + "X";
            }
            else if (SetParts.HasFlag(DateTimeParts.Century))
            {
                year = DateTime.Year.ToString("D4").Substring(0, 2) + "XX";
            }
            else if (SetParts.HasFlag(DateTimeParts.Millenium))
            {
                year = DateTime.Year.ToString("D4").Substring(0, 1) + "XXX";
            }
            else if (SetParts.HasFlag(DateTimeParts.DecadeYear))
            {
                year = "XX" + DateTime.Year.ToString("D4").Substring(2, 2);
            }
            var month = SetParts.HasFlag(DateTimeParts.Month)
                ? DateTime.Month.ToString("D2")
                : "XX";
            var day = SetParts.HasFlag(DateTimeParts.Day)
                ? DateTime.Day.ToString("D2")
                : "XX";
            var hour = SetParts.HasFlag(DateTimeParts.Hour)
                ? DateTime.Hour.ToString("D2")
                : "XX";
            var minute = SetParts.HasFlag(DateTimeParts.Minute)
                ? DateTime.Minute.ToString("D2")
                : "XX";
            var second = SetParts.HasFlag(DateTimeParts.Second)
                ? DateTime.Second.ToString("D2")
                : "XX";
            int isoYear;
            var week = SetParts.HasFlag(DateTimeParts.Week)
                ? TimexHelpers.GetIso8601WeekOfYear(DateTime, out isoYear).ToString("D2")
                : "XX";
            var weekDay = SetParts.HasFlag(DateTimeParts.WeekDay) ?
                TimexHelpers.GetIso8601DayOfWeek(DateTime).ToString("D1") :
                "X";

            // Resolve the "common" year into the ISO year that is calculated according to a specified
            // year/month/week value. WeekYear may therefore be different from the calendar year.
            int weekYearNum = 0;
            string weekYearString = year;

            int yearNumber;
            int monthNumber;
            int weekNumber = 0;
            if (int.TryParse(year, out yearNumber) &&
                int.TryParse(month, out monthNumber) &&
                int.TryParse(week, out weekNumber))
            {
                weekYearNum = TimexHelpers.GetIso8601WeekYear(yearNumber, monthNumber, weekNumber);
                weekYearString = weekYearNum.ToString("D4");
            }

            var valueBuilder = new StringBuilder();

            TemporalUnit? approximateRecurrenceUnit = FrequencyUnit;

            // The following few cases are basically a hack to get recurrences to output in the "correct" way
            
            if (approximateRecurrenceUnit == TemporalUnit.Day) // "Every Day"
            {
                valueBuilder.AppendFormat(Iso8601.DayTemplate, year, month, day);
            }
            else if (approximateRecurrenceUnit == TemporalUnit.Month) // "Every Month" or "the 19th of each month"
            {
                if (SetParts.HasFlag(DateTimeParts.Day))
                {
                    valueBuilder.AppendFormat(Iso8601.DayTemplate, year, month, day);
                }
                else
                {
                    valueBuilder.AppendFormat(Iso8601.MonthTemplate, year, month);
                }
            }
            else if (approximateRecurrenceUnit == TemporalUnit.Year) // "Every Year" or "Every September"
            {
                if (SetParts.HasFlag(DateTimeParts.Month))
                {
                    if (SetParts.HasFlag(DateTimeParts.Day))
                    {
                        valueBuilder.AppendFormat(Iso8601.DayTemplate, year, month, day);
                    }
                    else
                    {
                        valueBuilder.AppendFormat(Iso8601.MonthTemplate, year, month);
                    }
                }
                else if (SetParts.HasFlag(DateTimeParts.Season))
                {
                    valueBuilder.AppendFormat(Iso8601.SeasonTemplate, year, EnumExtensions.ToString(Season));
                }
                else
                {
                    valueBuilder.AppendFormat(Iso8601.YearTemplate, year);
                }
            }
            else if (approximateRecurrenceUnit == TemporalUnit.Week) // "Every Week" or "Every Tuesday"
            {
                if (SetParts.HasFlag(DateTimeParts.WeekDay))
                {
                    valueBuilder.AppendFormat(Iso8601.WeekDayTemplate, year, week, weekDay);
                }
                else
                {
                    valueBuilder.AppendFormat(Iso8601.WeekTemplate, year, week);
                }
            }
            else if (approximateRecurrenceUnit == TemporalUnit.Weekend) // "Every Weekend"
            {
                valueBuilder.AppendFormat(Iso8601.WeekEndTemplate, year, week);
            }
            else if (approximateRecurrenceUnit == TemporalUnit.Weekdays) // "Every Weekday"
            {
                valueBuilder.AppendFormat(Iso8601.WeekDaysTemplate, year, week);
            }
            // Always output the entire date/time, to prevent adding ambiguity.
            // The only variant is if we are in day-of-week mode or not. This would determine
            // if we should use YYYY-MM-DD format or YYYY-WW-D format
            else if (SetParts.HasFlag(DateTimeParts.PartOfYear))
            {
                valueBuilder.AppendFormat(Iso8601.PartOfYearTemplate, year,
                                          EnumExtensions.ToString(PartOfYear));
            }
            else if (SetParts.HasFlag(DateTimeParts.Season))
            {
                valueBuilder.AppendFormat(Iso8601.SeasonTemplate, year, EnumExtensions.ToString(Season));
            }
            else if (SetParts.HasFlag(DateTimeParts.WeekDay) ||
                SetParts.HasFlag(DateTimeParts.Week))
            {
                // If all fields are specified, convert it out of the yyyy-ww-d format
                if (SetParts.HasFlag(DateTimeParts.Week) &&
                    SetParts.HasFlag(DateTimeParts.WeekDay) &&
                    SetParts.HasFlag(DateTimeParts.Year))
                {
                    valueBuilder.Append(ConvertWeekFormatToStandard(weekYearNum, weekNumber, TimexHelpers.GetIso8601DayOfWeek(DateTime)));
                }
                else if (SetParts.HasFlag(DateTimeParts.Week) &&
                    SetParts.HasFlag(DateTimeParts.Year))
                {
                    valueBuilder.AppendFormat(Iso8601.WeekTemplate, weekYearString, week);
                }
                else
                {
                    // I don't know what could hit this case
                    valueBuilder.AppendFormat(Iso8601.WeekDayTemplate, weekYearString, week, weekDay);
                }
            }
            else if (SetParts.HasFlag(DateTimeParts.Reference))
            {
                valueBuilder.Append(EnumExtensions.ToString(Reference));
            }
            else if (SetParts.HasFlag(DateTimeParts.Day))
            {
                valueBuilder.AppendFormat(Iso8601.DayTemplate, year, month, day);
            }
            else if (SetParts.HasFlag(DateTimeParts.Month))
            {
                valueBuilder.AppendFormat(Iso8601.MonthTemplate, year, month);
            }
            else if (SetParts.HasFlag(DateTimeParts.Year) ||
                        SetParts.HasFlag(DateTimeParts.Decade) ||
                        SetParts.HasFlag(DateTimeParts.Century) ||
                        SetParts.HasFlag(DateTimeParts.Millenium) ||
                        SetParts.HasFlag(DateTimeParts.DecadeYear))
            {
                valueBuilder.AppendFormat(Iso8601.YearTemplate, year);
            }
            else
            {
                // Anything more granular than "daily" just appends the full "XXXX-XX-XX" date string here
                valueBuilder.AppendFormat(Iso8601.DayTemplate, "XXXX", "XX", "XX");
            }

            // time part
            if (SetParts.HasFlag(DateTimeParts.PartOfDay) && (PartOfDay == PartOfDay.Midnight || PartOfDay == PartOfDay.Noon))
            {
                valueBuilder.AppendFormat(Iso8601.TimeTemplate, EnumExtensions.ToString(PartOfDay));
            }
            else if (SetParts.HasFlag(DateTimeParts.Second) || approximateRecurrenceUnit == TemporalUnit.Second)
            {
                valueBuilder.AppendFormat(Iso8601.SecondTemplate, hour, minute, second);
            }
            else if (SetParts.HasFlag(DateTimeParts.Minute) || approximateRecurrenceUnit == TemporalUnit.Minute)
            {
                valueBuilder.AppendFormat(Iso8601.MinuteTemplate, hour, minute);
            }
            else if (SetParts.HasFlag(DateTimeParts.Hour) || approximateRecurrenceUnit == TemporalUnit.Hour)
            {
                valueBuilder.AppendFormat(Iso8601.HourTemplate, hour);
            }
            else if (SetParts.HasFlag(DateTimeParts.PartOfDay))
            {
                valueBuilder.AppendFormat(Iso8601.TimeTemplate, EnumExtensions.ToString(PartOfDay));
            }

            if (SetParts.HasFlag(DateTimeParts.TimeZone))
            {
                valueBuilder.Append(TimeZone);
            }

            return valueBuilder.ToString();
        }

        /// <summary>
        /// Compares two ExtendedDateTimes. This method will return a negative number if THIS time comes BEFORE the other time.
        /// If it comes after, return a positive number. If they are equal, return 0. This method performs comparison only against
        /// the SetParts that overlap between both ExtendedDateTimes, so it does not yet handle cases like parts of day being compared against hours.
        /// </summary>
        /// <returns>An integer representing the relative order of these two ExtendedDateTimes</returns>
        public int CompareTo(object other)
        {
            var otherTime = other as ExtendedDateTime;
            if (otherTime == null)
            {
                throw new ArgumentException("Attempted an invalid comparison with an ExtendedDateTime object");
            }

            return PiecewiseDateTimeCompare(otherTime.DateTime, otherTime.SetParts);
        }

        /// <summary>
        /// Performs a comparison between this (potentially underspecified) date and a given rigid date.
        /// The comparison is only done against fields that are actually set in this object. WeekDay references
        /// are resolved to that occurrence of the day of week on the reference date time's week_of_year.
        /// Returns a negative number if this object's m_dateTime field comes BEFORE comparisonDateTime.
        /// Returns 0 if the two dates are equal inasmuch as this date's fields are specified.
        /// If this date is specified in terms of a day of the week, then its date is interpreted as though
        /// using "present" normalization.
        /// </summary>
        /// <param name="comparisonDateTime">The absolute time to compare this against.</param>
        /// <param name="partsToCompare">(Optional) The DateTimeParts to factor into the comparison.</param>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Comparison must be performed across all fields")]
        private int PiecewiseDateTimeCompare(DateTime comparisonDateTime, DateTimeParts partsToCompare = DateTimeParts.All)
        {
            DateTimeParts overlappingParts = SetParts & partsToCompare;
            
            // Traverse through descending orders of magnitude until we find the first date/time field that is different.
            int returnVal = 0;
            if (returnVal == 0 && overlappingParts.HasFlag(DateTimeParts.Year))
            {
                returnVal = comparisonDateTime.Year - DateTime.Year;
            }

            if (returnVal == 0 && overlappingParts.HasFlag(DateTimeParts.Month))
            {
                returnVal = comparisonDateTime.Month - DateTime.Month;
            }

            if (returnVal == 0 && overlappingParts.HasFlag(DateTimeParts.Week))
            {
                int thisWeekYear;
                int thisWeekOfYear = TimexHelpers.GetIso8601WeekOfYear(DateTime, out thisWeekYear);
                int referenceWeekYear;
                int comparisonWeekOfYear = TimexHelpers.GetIso8601WeekOfYear(comparisonDateTime, out referenceWeekYear);

                if (overlappingParts.HasFlag(DateTimeParts.Year))
                {
                    returnVal = referenceWeekYear - thisWeekYear;

                    if (returnVal == 0)
                    {
                        returnVal = comparisonWeekOfYear - thisWeekOfYear;
                    }
                }
                else
                {
                    returnVal = comparisonWeekOfYear - thisWeekOfYear;
                }
            }

            if (returnVal == 0 && overlappingParts.HasFlag(DateTimeParts.Day))
            {
                returnVal = comparisonDateTime.Day - DateTime.Day;
            }

            if (returnVal == 0 && overlappingParts.HasFlag(DateTimeParts.WeekDay))
            {
                int dateTimeDayOfWeek = TimexHelpers.GetIso8601DayOfWeek(DateTime);
                int comparisonDayOfWeek = TimexHelpers.GetIso8601DayOfWeek(comparisonDateTime);

                // Handle underspecified day of week references. By this I mean, when attempting to compare "January 2 2012" with "Monday",
                // fill in the missing detail using the current reference datetime. Then do the comparison.
                int isoYear;
                int dateTimeWeekOfYear = overlappingParts.HasFlag(DateTimeParts.Week) ? TimexHelpers.GetIso8601WeekOfYear(DateTime, out isoYear) : TimexHelpers.GetIso8601WeekOfYear(Context.ReferenceDateTime, out isoYear);
                int comparisonDateTimeWeekOfYear = TimexHelpers.GetIso8601WeekOfYear(comparisonDateTime, out isoYear);

                int year = overlappingParts.HasFlag(DateTimeParts.Year) ? DateTime.Year : Context.ReferenceDateTime.Year;
                int month = overlappingParts.HasFlag(DateTimeParts.Month) ? DateTime.Month : Context.ReferenceDateTime.Month;

                int dateTimeYear = TimexHelpers.GetIso8601WeekYear(year,
                    month,
                    dateTimeWeekOfYear);
                int comparisonDateTimeYear = TimexHelpers.GetIso8601WeekYear(comparisonDateTime.Year,
                    comparisonDateTime.Month,
                    comparisonDateTimeWeekOfYear);

                // When referring to days of the week alone, remember that we could be crossing year and week boundaries, so we need to check all of them.
                returnVal = comparisonDateTimeYear - dateTimeYear;

                if (returnVal == 0)
                {
                    returnVal = comparisonDateTimeWeekOfYear - dateTimeWeekOfYear;
                }

                if (returnVal == 0)
                {
                    returnVal = comparisonDayOfWeek - dateTimeDayOfWeek;
                }
            }

            // Handle vague comparisons involving parts of day like "morning"
            if (returnVal == 0 && overlappingParts.HasFlag(DateTimeParts.PartOfDay) && !overlappingParts.HasFlag(DateTimeParts.Hour))
            {
                if (Context.Normalization == Normalization.Future)
                {
                    returnVal = comparisonDateTime.Hour - (PartOfDay.ToApproximateHour() + TimeOfDayVagueness);
                }
                else if (Context.Normalization == Normalization.Past)
                {
                    returnVal = comparisonDateTime.Hour - (PartOfDay.ToApproximateHour() - TimeOfDayVagueness);
                }
            }

            if (returnVal == 0 && overlappingParts.HasFlag(DateTimeParts.Hour))
            {
                returnVal = comparisonDateTime.Hour - DateTime.Hour;
            }

            if (returnVal == 0 && overlappingParts.HasFlag(DateTimeParts.Minute))
            {
                returnVal = comparisonDateTime.Minute - DateTime.Minute;
            }

            if (returnVal == 0 && overlappingParts.HasFlag(DateTimeParts.Second))
            {
                returnVal = comparisonDateTime.Second - DateTime.Second;
            }

            return Math.Min(1, Math.Max(-1, returnVal));
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "adjustment method with a large if else statement")]
        private DateTime AdjustReferenceDateTime()
        {
            var adjustedReferenceDateTime = Context.ReferenceDateTime;

            if (Context.Normalization == Normalization.Present)
            {
                return adjustedReferenceDateTime;
            }

            // If the adjusted reference date time is in the past relative to this, dateComparison will be 1.
            // If it is in the future, dateComparison will be -1.
            // If dates are equal (inasmuch as its fields are specified) this will be 0.
            int dateComparison = PiecewiseDateTimeCompare(adjustedReferenceDateTime);

            if (SetParts.HasFlag(DateTimeParts.Second) && !SetParts.HasFlag(DateTimeParts.Minute))
            {
                switch (Context.Normalization)
                {
                    case Normalization.Future:
                        if (dateComparison >= 0)
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddMinutes(1);
                        }
                        return adjustedReferenceDateTime;
                    case Normalization.Past:
                        if (dateComparison <= 0)
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddMinutes(-1);
                        }
                        return adjustedReferenceDateTime;
                }
            }

            if (SetParts.HasFlag(DateTimeParts.Minute) && !SetParts.HasFlag(DateTimeParts.Hour))
            {
                switch (Context.Normalization)
                {
                    case Normalization.Future:
                        if (dateComparison >= 0)
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddHours(1);
                        }
                        return adjustedReferenceDateTime;
                    case Normalization.Past:
                        if (dateComparison <= 0)
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddHours(-1);
                        }
                        return adjustedReferenceDateTime;
                }
            }

            if (SetParts.HasFlag(DateTimeParts.Hour) &&
                !(SetParts.HasFlag(DateTimeParts.Day) ||
                  SetParts.HasFlag(DateTimeParts.WeekDay)))
            {
                switch (Context.Normalization)
                {
                    case Normalization.Future:
                        if (dateComparison >= 0)
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddDays(1);
                        }
                        return adjustedReferenceDateTime;
                    case Normalization.Past:
                        if (dateComparison <= 0)
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddDays(-1);
                        }
                        return adjustedReferenceDateTime;
                }
            }

            bool includeCurrentDateInResolution = Context.IncludeCurrentTimeInPastOrFuture;

            if (SetParts.HasFlag(DateTimeParts.PartOfDay) &&
                !(SetParts.HasFlag(DateTimeParts.Day) ||
                  SetParts.HasFlag(DateTimeParts.WeekDay)))
            {
                switch (Context.Normalization)
                {
                    case Normalization.Future:
                        if (dateComparison > 0 || (!includeCurrentDateInResolution && dateComparison == 0))
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddDays(1);
                        }
                        return adjustedReferenceDateTime;
                    case Normalization.Past:
                        if (dateComparison < 0 || (!includeCurrentDateInResolution && dateComparison == 0))
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddDays(-1);
                        }
                        return adjustedReferenceDateTime;
                }
            }

            if (SetParts.HasFlag(DateTimeParts.Day) && !SetParts.HasFlag(DateTimeParts.Month))
            {
                switch (Context.Normalization)
                {
                    case Normalization.Future:
                        if (dateComparison > 0 || (!includeCurrentDateInResolution && dateComparison == 0))
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddMonths(1);
                        }
                        return adjustedReferenceDateTime;
                    case Normalization.Past:
                        if (dateComparison < 0 || (!includeCurrentDateInResolution && dateComparison == 0))
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddMonths(-1);
                        }
                        return adjustedReferenceDateTime;
                }
            }

            if (SetParts.HasFlag(DateTimeParts.WeekDay) && !SetParts.HasFlag(DateTimeParts.Day))
            {
                switch (Context.Normalization)
                {
                    case Normalization.Future:
                        if (dateComparison > 0 || (!includeCurrentDateInResolution && dateComparison == 0))
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddDays(DaysInOneWeek);
                        }
                        return adjustedReferenceDateTime;
                    case Normalization.Past:
                        if (dateComparison < 0 || (!includeCurrentDateInResolution && dateComparison == 0))
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddDays(0 - DaysInOneWeek);
                        }
                        return adjustedReferenceDateTime;
                }
            }

            if (SetParts.HasFlag(DateTimeParts.Week) && !SetParts.HasFlag(DateTimeParts.Year))
            {
                switch (Context.Normalization)
                {
                    case Normalization.Future:
                        if (dateComparison >= 0)
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddYears(1);
                        }
                        return adjustedReferenceDateTime;
                    case Normalization.Past:
                        if (dateComparison <= 0)
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddYears(-1);
                        }
                        return adjustedReferenceDateTime;
                }
            }

            if (SetParts.HasFlag(DateTimeParts.Month) && !SetParts.HasFlag(DateTimeParts.Year))
            {
                switch (Context.Normalization)
                {
                    case Normalization.Future:
                        if (dateComparison > 0 || (!includeCurrentDateInResolution && dateComparison == 0))
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddYears(1);
                        }
                        return adjustedReferenceDateTime;
                    case Normalization.Past:
                        if (dateComparison < 0 || (!includeCurrentDateInResolution && dateComparison == 0))
                        {
                            adjustedReferenceDateTime = adjustedReferenceDateTime.AddYears(-1);
                        }
                        return adjustedReferenceDateTime;
                }
            }

            return adjustedReferenceDateTime;
        }

        /// <summary>
        /// If this date is given in terms of an anchor time with an offset, resolve that into an absolute date and print it.
        /// </summary>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "formatting method with a large switch statement")]
        private string FormatValueUsingOffset()
        {
            // Abort if no offset it specified
            if (!Offset.HasValue || !OffsetUnit.HasValue)
                return string.Empty;

            // represent current instance DateTime with applied offset
            var dateTimeWithOffset = Context.ReferenceDateTime;
            if (SetParts.HasFlag(DateTimeParts.Year))
                dateTimeWithOffset = dateTimeWithOffset.AddYears(DateTime.Year - dateTimeWithOffset.Year);
            if (SetParts.HasFlag(DateTimeParts.Month))
                dateTimeWithOffset = dateTimeWithOffset.AddMonths(DateTime.Month - dateTimeWithOffset.Month);
            else if (SetParts.HasFlag(DateTimeParts.Week))
            {
                // This case should only be triggered by "Weekend" expressions, to make sure we are referring to the right weekend
                int isoYear;
                var targetWeek = TimexHelpers.GetIso8601WeekOfYear(DateTime, out isoYear);
                var startWeek = TimexHelpers.GetIso8601WeekOfYear(dateTimeWithOffset, out isoYear);
                dateTimeWithOffset = dateTimeWithOffset.AddDays((targetWeek - startWeek) * DaysInOneWeek);
            }
            if (SetParts.HasFlag(DateTimeParts.Day))
                dateTimeWithOffset = dateTimeWithOffset.AddDays(DateTime.Day - dateTimeWithOffset.Day);
            else if (SetParts.HasFlag(DateTimeParts.WeekDay))
                dateTimeWithOffset = dateTimeWithOffset.AddDays(TimexHelpers.GetIso8601DayOfWeek(DateTime) - TimexHelpers.GetIso8601DayOfWeek(dateTimeWithOffset));

            var valueBuilder = new StringBuilder();

            // For things like "century", "millenium", "fortnight", etc., just reuse the formatters for years and days
            TemporalUnit effectiveUnit = OffsetUnit.Value;
            int effectiveOffset = Offset.GetValueOrDefault();
            if (effectiveUnit == TemporalUnit.Decade)
            {
                effectiveUnit = TemporalUnit.Year;
                effectiveOffset *= 10;
            }
            else if (effectiveUnit == TemporalUnit.Century)
            {
                effectiveUnit = TemporalUnit.Year;
                effectiveOffset *= 100;
            }
            else if (effectiveUnit == TemporalUnit.Fortnight)
            {
                effectiveUnit = TemporalUnit.Day;
                effectiveOffset *= 14;
            }

            // date offset
            switch (effectiveUnit)
            {
                case TemporalUnit.Year:
                    {
                        dateTimeWithOffset = dateTimeWithOffset.AddYears(effectiveOffset);

                        if (SetParts.HasFlag(DateTimeParts.Day))
                        {
                            valueBuilder.AppendFormat(Iso8601.DayTemplate,
                                                      dateTimeWithOffset.Year.ToString("D4"),
                                                      dateTimeWithOffset.Month.ToString("D2"),
                                                      dateTimeWithOffset.Day.ToString("D2"));
                        }
                        else if (SetParts.HasFlag(DateTimeParts.Month))
                        {
                            valueBuilder.AppendFormat(Iso8601.MonthTemplate,
                                                        dateTimeWithOffset.Year.ToString("D4"),
                                                        dateTimeWithOffset.Month.ToString("D2"));
                        }
                        else if (SetParts.HasFlag(DateTimeParts.PartOfYear))
                        {
                            valueBuilder.AppendFormat(Iso8601.PartOfYearTemplate,
                                                      dateTimeWithOffset.Year.ToString("D4"),
                                                      EnumExtensions.ToString(PartOfYear));

                        }
                        else if (SetParts.HasFlag(DateTimeParts.Season))
                        {
                            valueBuilder.AppendFormat(Iso8601.SeasonTemplate,
                                                      dateTimeWithOffset.Year.ToString("D4"),
                                                      EnumExtensions.ToString(Season));
                        }
                        else if (Context.UseInference)
                        {
                            valueBuilder.AppendFormat(Iso8601.YearTemplate,
                                                      dateTimeWithOffset.Year.ToString("D4"));
                        }
                        else
                        {
                            valueBuilder.AppendFormat(Iso8601.YearTemplate,
                                                         dateTimeWithOffset.Year.ToString("D4"));
                        }

                        break;
                    }
                case TemporalUnit.Month:
                    {
                        // If useInference is on, we may have already crossed the boundary to the next month. Detect this and augment the offset accordingly
                        // For example, if it's April 16 and you say "the 2nd of next month", "2nd" resolves to May 2nd, so the month "offset" has already been applied
                        int monthOffsetAlreadyApplied = 0;
                        if (Context.UseInference)
                        {
                            // we have to take year boundaries into account if it crosses dec -> jan
                            monthOffsetAlreadyApplied = ((dateTimeWithOffset.Year * 12) + dateTimeWithOffset.Month) -
                                ((Context.ReferenceDateTime.Year * 12) + Context.ReferenceDateTime.Month);
                        }

                        dateTimeWithOffset = dateTimeWithOffset.AddMonths(effectiveOffset - monthOffsetAlreadyApplied);

                        if (SetParts.HasFlag(DateTimeParts.Day))
                        {
                            valueBuilder.AppendFormat(Iso8601.DayTemplate,
                                                      dateTimeWithOffset.Year.ToString("D4"),
                                                      dateTimeWithOffset.Month.ToString("D2"),
                                                      dateTimeWithOffset.Day.ToString("D2"));
                        }
                        else if (Context.UseInference)
                        {
                            valueBuilder.AppendFormat(Iso8601.MonthTemplate,
                                                      dateTimeWithOffset.Year.ToString("D4"),
                                                      dateTimeWithOffset.Month.ToString("D2"));
                        }
                        else
                        {
                            valueBuilder.AppendFormat(Iso8601.MonthTemplate,
                                                      dateTimeWithOffset.Year.ToString("D4"),
                                                      dateTimeWithOffset.Month.ToString("D2"));
                        }

                        break;
                    }
                case TemporalUnit.Week:
                    {
                        if (SetParts.HasFlag(DateTimeParts.Day) || SetParts.HasFlag(DateTimeParts.WeekDay))
                        {
                            // Standard case: Offset time by X weeks
                            // Factor in compound offset too
                            dateTimeWithOffset = dateTimeWithOffset.AddDays((DaysInOneWeek * effectiveOffset) + CompoundOffset);

                            valueBuilder.AppendFormat(Iso8601.DayTemplate,
                                                      dateTimeWithOffset.Year.ToString("D4"),
                                                      dateTimeWithOffset.Month.ToString("D2"),
                                                      dateTimeWithOffset.Day.ToString("D2"));
                        }
                        else if (SetParts.HasFlag(DateTimeParts.Month))
                        {
                            // Handle "First week of october" cases - month is specified, as well as a week# offset, and optionally a year

                            // In this case, make sure the offset is relative to the 1st day of the month
                            dateTimeWithOffset = dateTimeWithOffset.AddDays((1 - dateTimeWithOffset.Day) + (DaysInOneWeek * effectiveOffset));
                            
                            string year = (Context.UseInference || SetParts.HasFlag(DateTimeParts.Year)) ?
                                    dateTimeWithOffset.Year.ToString("D4") :
                                    "XXXX";

                            // Apply past/future normalization (although the best we can really do is a rough guess)
                            if (Context.UseInference && Context.Normalization == Normalization.Past)
                            {
                                // Find the start of the week that we're referring to (with -1 week of leeway)
                                DateTime testDate = new DateTime(dateTimeWithOffset.Ticks);
                                testDate = testDate.AddDays((effectiveOffset - 2) * DaysInOneWeek);

                                // This week falls definitively in the future. Move it back by 1 year.
                                if (testDate > Context.ReferenceDateTime)
                                {
                                    year = (dateTimeWithOffset.Year - 1).ToString("D4");
                                }
                            }
                            else if (Context.UseInference && Context.Normalization == Normalization.Future)
                            {
                                // Find the end of the week that we're referring to (with +1 week of leeway)
                                DateTime testDate = new DateTime(dateTimeWithOffset.Ticks);
                                testDate = testDate.AddDays(effectiveOffset * DaysInOneWeek);

                                // This week falls definitively in the past. Move it forward by 1 year.
                                if (testDate < Context.ReferenceDateTime)
                                {
                                    year = (dateTimeWithOffset.Year + 1).ToString("D4");
                                }
                            }

                            valueBuilder.AppendFormat(Iso8601.WeekOfMonthTemplate,
                                                      year,
                                                      dateTimeWithOffset.Month.ToString("D2"),
                                                      effectiveOffset.ToString("D2"));
                        }
                        else
                        {
                            // No day or month information, so just use week of year format
                            dateTimeWithOffset = dateTimeWithOffset.AddDays(DaysInOneWeek * effectiveOffset);
                            int isoYear;
                            var week = TimexHelpers.GetIso8601WeekOfYear(dateTimeWithOffset, out isoYear);
                            var weekYear = TimexHelpers.GetIso8601WeekYear(dateTimeWithOffset.Year, dateTimeWithOffset.Month, week);

                            valueBuilder.AppendFormat(Iso8601.WeekTemplate,
                                                      weekYear.ToString("D4"),
                                                      week.ToString("D2"));
                        }

                        break;
                    }
                case TemporalUnit.Weekend:
                    {
                        // Offset time by X weeks
                        dateTimeWithOffset = dateTimeWithOffset.AddDays(DaysInOneWeek * effectiveOffset);
                        int isoYear;
                        var week = TimexHelpers.GetIso8601WeekOfYear(dateTimeWithOffset, out isoYear);
                        var weekYear = TimexHelpers.GetIso8601WeekYear(dateTimeWithOffset.Year, dateTimeWithOffset.Month, week);

                        valueBuilder.AppendFormat(Iso8601.WeekEndTemplate,
                                                    weekYear.ToString("D4"),
                                                    week.ToString("D2"));

                        break;
                    }
                case TemporalUnit.Monday:
                case TemporalUnit.Tuesday:
                case TemporalUnit.Wednesday:
                case TemporalUnit.Thursday:
                case TemporalUnit.Friday:
                case TemporalUnit.Saturday:
                case TemporalUnit.Sunday:
                    {
                        valueBuilder.Append(FormatWeekdayOffsetHelper(dateTimeWithOffset, effectiveOffset, effectiveUnit.ConvertIntoDayOfWeek(), CompoundOffset));

                        break;
                    }
                case TemporalUnit.BusinessDay:
                    {
                        // Offset value by X business days
                        var offsetLength = Math.Abs(effectiveOffset);
                        var offsetSign = Math.Sign(effectiveOffset);

                        // Build a basic lookup table for all ISO days of week that are considered "weekend" in the current locale
                        int[] weekendDays = new int[Context.WeekDefinition.WeekendLength];
                        for (int weekendDay = 0; weekendDay < Context.WeekDefinition.WeekendLength; weekendDay++)
                        {
                            // The +1, -1 stuff here is so we can modulo between 1 and 7 rather than 0 and 6 (because ISO days are not base 0)
                            weekendDays[weekendDay] = (((Context.WeekDefinition.FirstDayOfWeekend + weekendDay) - 1) % DaysInOneWeek) + 1;
                        }

                        if (offsetLength == 0)
                        {
                            // One strange case - the offset is "0 business days".
                            // The best interpretation we can give is to return the current day if it a business day, otherwise
                            // zoom forwards or backwards depending on normalization direction
                            offsetSign = Context.Normalization == Normalization.Future || Context.Normalization == Normalization.Present ? 1 : -1;
                        }

                        for (int days = 0; days < offsetLength || (days == 0 && offsetLength == 0); days++)
                        {
                            if (offsetLength != 0)
                            {
                                dateTimeWithOffset = dateTimeWithOffset.AddDays(offsetSign);
                            }

                            int dayOfWeek = TimexHelpers.GetIso8601DayOfWeek(dateTimeWithOffset);

                            // Walk through weekend forwards, if needed
                            if (offsetSign > 0)
                            {
                                // this loop will keep nudging the day forward until it falls outside of the weekend
                                for (int weekendDayIdx = 0; weekendDayIdx < weekendDays.Length; weekendDayIdx++)
                                {
                                    if (dayOfWeek == weekendDays[weekendDayIdx])
                                    {
                                        dateTimeWithOffset = dateTimeWithOffset.AddDays(1);
                                        dayOfWeek = TimexHelpers.GetIso8601DayOfWeek(dateTimeWithOffset);
                                    }
                                }
                            }
                            // Walk through weekend backwards, if needed
                            else if (offsetSign < 0)
                            {
                                // this loop will keep nudging the day backwards until it falls outside of the weekend
                                for (int weekendDayIdx = weekendDays.Length - 1; weekendDayIdx >= 0; weekendDayIdx--)
                                {
                                    if (dayOfWeek == weekendDays[weekendDayIdx])
                                    {
                                        dateTimeWithOffset = dateTimeWithOffset.AddDays(-1);
                                        dayOfWeek = TimexHelpers.GetIso8601DayOfWeek(dateTimeWithOffset);
                                    }
                                }
                            }
                        }

                        valueBuilder.AppendFormat(Iso8601.DayTemplate,
                                                  dateTimeWithOffset.Year.ToString("D4"),
                                                  dateTimeWithOffset.Month.ToString("D2"),
                                                  dateTimeWithOffset.Day.ToString("D2"));

                        break;
                    }
                case TemporalUnit.Day:
                    {
                        // Offset value by X days
                        dateTimeWithOffset = dateTimeWithOffset.AddDays(effectiveOffset + CompoundOffset);
                        valueBuilder.AppendFormat(Iso8601.DayTemplate,
                                                  dateTimeWithOffset.Year.ToString("D4"),
                                                  dateTimeWithOffset.Month.ToString("D2"),
                                                  dateTimeWithOffset.Day.ToString("D2"));

                        break;
                    }
            }

            // time offset
            switch (effectiveUnit)
            {
                case TemporalUnit.Hour:
                    {
                        dateTimeWithOffset = dateTimeWithOffset.AddHours(effectiveOffset);

                        valueBuilder.AppendFormat(Iso8601.DayTemplate,
                                                  dateTimeWithOffset.Year.ToString("D4"),
                                                  dateTimeWithOffset.Month.ToString("D2"),
                                                  dateTimeWithOffset.Day.ToString("D2"));

                        valueBuilder.AppendFormat(Iso8601.HourTemplate,
                                                    dateTimeWithOffset.Hour.ToString("D2"));
                        break;
                    }
                case TemporalUnit.Minute:
                    {
                        dateTimeWithOffset = dateTimeWithOffset.AddMinutes(effectiveOffset);

                        valueBuilder.AppendFormat(Iso8601.DayTemplate,
                                                  dateTimeWithOffset.Year.ToString("D4"),
                                                  dateTimeWithOffset.Month.ToString("D2"),
                                                  dateTimeWithOffset.Day.ToString("D2"));

                        valueBuilder.AppendFormat(Iso8601.MinuteTemplate,
                                                dateTimeWithOffset.Hour.ToString("D2"),
                                                dateTimeWithOffset.Minute.ToString("D2"));

                        break;
                    }
                case TemporalUnit.Second:
                    {
                        dateTimeWithOffset = dateTimeWithOffset.AddSeconds(effectiveOffset);

                        valueBuilder.AppendFormat(Iso8601.DayTemplate,
                                                  dateTimeWithOffset.Year.ToString("D4"),
                                                  dateTimeWithOffset.Month.ToString("D2"),
                                                  dateTimeWithOffset.Day.ToString("D2"));
                        valueBuilder.AppendFormat(Iso8601.SecondTemplate,
                                                  dateTimeWithOffset.Hour.ToString("D2"),
                                                  dateTimeWithOffset.Minute.ToString("D2"),
                                                  dateTimeWithOffset.Second.ToString("D2"));

                        break;
                    }
                default:
                    {
                        // Append time (in specificity order) if applicable except for the cases of noon and midnight
                        if ((SetParts.HasFlag(DateTimeParts.Second) || SetParts.HasFlag(DateTimeParts.Minute) || SetParts.HasFlag(DateTimeParts.Hour)) &&
                            !(SetParts.HasFlag(DateTimeParts.PartOfDay) && (PartOfDay == PartOfDay.Noon || PartOfDay == PartOfDay.Midnight)))
                        {
                            if (SetParts.HasFlag(DateTimeParts.Second))
                            {
                                valueBuilder.AppendFormat(Iso8601.SecondTemplate,
                                    DateTime.Hour.ToString("D2"),
                                    DateTime.Minute.ToString("D2"),
                                    DateTime.Second.ToString("D2"));
                            }
                            else if (SetParts.HasFlag(DateTimeParts.Minute))
                            {
                                valueBuilder.AppendFormat(Iso8601.MinuteTemplate,
                                    DateTime.Hour.ToString("D2"),
                                    DateTime.Minute.ToString("D2"));
                            }
                            else if (SetParts.HasFlag(DateTimeParts.Hour))
                            {
                                valueBuilder.AppendFormat(Iso8601.HourTemplate,
                                    DateTime.Hour.ToString("D2"));
                            }
                        }
                        // Fall back on appending the part of day if applicable
                        else if (SetParts.HasFlag(DateTimeParts.PartOfDay))
                        {
                            valueBuilder.AppendFormat(Iso8601.TimeTemplate, EnumExtensions.ToString(PartOfDay));
                        }
                        break;
                    }
            }

            return valueBuilder.ToString();
        }

        /// <summary>
        /// A helper function to help when formatting offset dates that are relative to specific days of the week
        /// Code reuse to prevent 7 inline blocks of this code that differed by what day of week they specified
        /// </summary>
        private string FormatWeekdayOffsetHelper(DateTime dateTimeWithOffset,
            int offsetAmount,
            DayOfWeek offsetDay,
            int compoundOffset)
        {
            if (SetParts.HasFlag(DateTimeParts.Month) && !SetParts.HasFlag(DateTimeParts.Year))
            {
                DateTime inferredTargetTime = new DateTime(dateTimeWithOffset.Ticks);

                bool useInference = Context.UseInference;

                // This handles the case where the offset is a negative number of weeks, which if we
                // tried to write without inference would return invalid ISO. In this case the quickest fix is just to force inference
                if (offsetAmount < 0)
                {
                    useInference = true;
                }

                if (useInference)
                {
                    // Handle "Fourth thursday of November" cases when year is unspecified - this normally returns XXXX-MM-WWW-D
                    // Apply inference here and resolve to a standard YYYY-MM-DD format
                    inferredTargetTime = inferredTargetTime.AddDays(1 - inferredTargetTime.Day);

                    // Test resolve the offset into an actual value, so we know when this date is actually going to land when it resolves
                    // This lets us properly infer, for example, if Thanksgiving has passed or not, when the reference time is the middle of November
                    inferredTargetTime = ApplyDayOfWeekOffset(inferredTargetTime, offsetDay, offsetAmount);

                    // See if it passes normalization tests
                    if (Context.Normalization == Normalization.Future && Context.ReferenceDateTime > inferredTargetTime)
                    {
                        inferredTargetTime = inferredTargetTime.AddYears(1);
                        inferredTargetTime = inferredTargetTime.AddMonths(dateTimeWithOffset.Month - inferredTargetTime.Month);
                        inferredTargetTime = inferredTargetTime.AddDays(1 - inferredTargetTime.Day);
                        inferredTargetTime = ApplyDayOfWeekOffset(inferredTargetTime, offsetDay, offsetAmount);
                    }
                    else if (Context.Normalization == Normalization.Past && Context.ReferenceDateTime < inferredTargetTime)
                    {
                        inferredTargetTime = inferredTargetTime.AddYears(-1);
                        inferredTargetTime = inferredTargetTime.AddMonths(dateTimeWithOffset.Month - inferredTargetTime.Month);
                        inferredTargetTime = inferredTargetTime.AddDays(1 - inferredTargetTime.Day);
                        inferredTargetTime = ApplyDayOfWeekOffset(inferredTargetTime, offsetDay, offsetAmount);
                    }

                    if (compoundOffset != 0)// factor in compound offset (for "a week from next tuesday" cases)
                    {
                        inferredTargetTime = inferredTargetTime.AddDays(compoundOffset);
                    }

                    return string.Format(Iso8601.DayTemplate,
                        inferredTargetTime.Year.ToString("D4"),
                        inferredTargetTime.Month.ToString("D2"),
                        inferredTargetTime.Day.ToString("D2"));
                }
                else
                {
                    inferredTargetTime = ApplyDayOfWeekOffset(dateTimeWithOffset,
                        offsetDay,
                        offsetAmount,
                        Normalization.Present,
                        WeekdayLogic.Programmatic,
                        0);

                    if (compoundOffset != 0) // Factor in compound offset
                    {
                        inferredTargetTime = inferredTargetTime.AddDays(compoundOffset);
                    }

                    return string.Format(Iso8601.WeekDayOfMonthTemplate,
                        "XXXX",
                        DateTime.Month.ToString("D2"),
                        offsetAmount.ToString("D2"),
                        TimexHelpers.GetIso8601DayOfWeek(inferredTargetTime).ToString("D1"));
                }
            }
            else
            {
                WeekdayLogic logicType = SetParts.HasFlag(DateTimeParts.OffsetAnchor) ? WeekdayLogic.Programmatic : Context.WeekdayLogicType;
                DateTime modifiedDateTime = ApplyDayOfWeekOffset(
                    dateTimeWithOffset,
                    offsetDay,
                    offsetAmount,
                    Context.Normalization,
                    logicType,
                    MinOffset);

                if (compoundOffset != 0)
                {
                    modifiedDateTime = modifiedDateTime.AddDays(compoundOffset);
                }

                var dayOfWeek = TimexHelpers.GetIso8601DayOfWeek(modifiedDateTime);
                int weekYear;
                var week = TimexHelpers.GetIso8601WeekOfYear(modifiedDateTime, out weekYear);

                return ConvertWeekFormatToStandard(weekYear, week, dayOfWeek);
            }
        }

        public string FormatMod()
        {
            if (!Modifier.HasValue)
                return null;

            return EnumExtensions.ToString(Modifier.Value);
        }

        public string FormatFrequency()
        {
            if (!Frequency.HasValue)
                return null;

            var frequencyUnitString = string.Empty;
            if (FrequencyUnit.HasValue)
            {
                // Frequencies of "weekend" and "weekdays" aren't handled here; return "week" instead
                if (FrequencyUnit == TemporalUnit.Weekend || FrequencyUnit == TemporalUnit.Weekdays)
                {
                    frequencyUnitString = EnumExtensions.ToString(TemporalUnit.Week);
                }
                else
                {
                    frequencyUnitString = EnumExtensions.ToString(FrequencyUnit.Value);
                }
            }

            return Frequency + frequencyUnitString;
        }

        public string FormatQuantity()
        {
            if (!Quantity.HasValue)
                return null;

            return Quantity.Value.ToString();
        }

#endregion

        #region Utility

        /// <summary>
        /// Parses year string (e.g. 198X)
        /// </summary>
        /// <param name="yearString">Year string to parse</param>
        /// <param name="year">Year</param>
        /// <param name="parts">Parts that are set</param>
        /// <returns>True if year string is successfully parse; otherwise, false</returns>
        private static bool TryParseYear(string yearString, out int year, out DateTimeParts parts)
        {
            // default values
            year = 0;
            parts = DateTimeParts.None;

            if (yearString.Length != 4)
                return false;

            if (char.IsDigit(yearString[0]))
            {
                if (char.IsDigit(yearString[1]))
                {
                    if (char.IsDigit(yearString[2]))
                    {
                        if (char.IsDigit(yearString[3]))
                        {
                            parts |= DateTimeParts.Year; // 1987
                        }
                        else
                        {
                            parts |= DateTimeParts.Decade; // 198X
                        }
                    }
                    else
                    {
                        parts |= DateTimeParts.Century; // 19XX
                    }
                }
                else
                {
                    parts |= DateTimeParts.Millenium; // 1XXX
                }
            }
            else
            {
                if (char.IsDigit(yearString[2]) &&
                    char.IsDigit(yearString[3]))
                {
                    parts |= DateTimeParts.DecadeYear; // XX12
                }
            }

            // replace unknown parts with zeros as we already set up flags
            var yearStringBuilder = new StringBuilder();
            foreach (var c in yearString)
            {
                yearStringBuilder.Append(char.IsDigit(c) ? c : '0');
            }

            if (int.TryParse(yearStringBuilder.ToString(), out year))
            {
                // Catch a rare case where input was '00 - this can only refer to the year 2000
                if (year == 0 && parts.HasFlag(DateTimeParts.DecadeYear))
                {
                    parts |= DateTimeParts.Year;
                    return true;
                }

                // it's not possible to represent year '0000' with DateTime structure
                // so if exact year is not important set year to '0001'
                if (year == 0 && !parts.HasFlag(DateTimeParts.Year) && !parts.HasFlag(DateTimeParts.DecadeYear))
                {
                    year = 1;
                    return true;
                }

                if (year > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves the offsetanchor attribute of this datetime into an intended date. This function will
        /// read its OffsetAnchor value and set the appropriate day/month/year fields in the local dateTime.
        /// The OffsetAnchor string is given as a string, which is either a holiday keyword ("EasterSunday")
        /// or a month-day value in the format of MM-DD
        /// All variable holidays are expressed in this format. This function exists because resolution of
        /// holidays is dependent on inference flags, and we want to defer inferences from construction time
        /// until the output value actually needs formatting, both as a matter of principle and as a performance benefit.
        /// </summary>
        private void ResolveOffsetAnchor()
        {
            if (!SetParts.HasFlag(DateTimeParts.OffsetAnchor) || string.IsNullOrEmpty(OffsetAnchor))
            {
                return;
            }

            // Check for special holiday anchors
            Holiday specialHoliday;
            if (EnumExtensions.TryParse(OffsetAnchor, out specialHoliday))
            {
                // It's a special holiday like Easter. Use the holiday helper methods to resolve it.
                int offsetDays = (Offset.HasValue && OffsetUnit.HasValue && OffsetUnit.Value == TemporalUnit.Day) ? Offset.GetValueOrDefault() : 0;
                DateTime? resolvedTime = ComplexHolidays.ResolveComplexHoliday(specialHoliday, DateTime, Context, SetParts.HasFlag(DateTimeParts.Year), offsetDays);
			    if (!resolvedTime.HasValue)
			    {
				    // If inference hit an error or landed on an unsupported year (like "Easter 2155"), invalidate this object.
				    InputDateWasInvalid = true;
			    }
			    else
			    {
                    DateTime = resolvedTime.GetValueOrDefault();
				    SetParts |= DateTimeParts.Year | DateTimeParts.Month | DateTimeParts.Day;
			    }
            }
            else if (OffsetAnchor.Equals(OffsetAnchorTodayString)) // Is the anchor "TODAY"?
            {
                // If so, set the date to equal the reference date, and set the corresponding flags
                // This is needed so that, for example, if input was "in two weeks" it will return 2013-01-21 format
                // instead of 2013-W03 format later on (in FormatValueUsingOffset)
                DateTime = DateTime.AddYears(Context.ReferenceDateTime.Year - DateTime.Year);
                DateTime = DateTime.AddMonths(Context.ReferenceDateTime.Month - DateTime.Month);
                DateTime = DateTime.AddDays(Context.ReferenceDateTime.Day - DateTime.Day);
                SetParts |= DateTimeParts.Day | DateTimeParts.Month | DateTimeParts.Year;
            }
            else // The anchor is something like "November 1st" (11-01)
            {
                var offsetAnchorArray = OffsetAnchor.Split('-');

                int offsetAnchorItem;
                if (offsetAnchorArray.Length > 0)
                {
                    if (int.TryParse(offsetAnchorArray[0], out offsetAnchorItem))
                    {
                        DateTime = DateTime.AddMonths(offsetAnchorItem - DateTime.Month);
                        SetParts |= DateTimeParts.Month;
                    }
                }

                if (offsetAnchorArray.Length > 1)
                {
                    if (int.TryParse(offsetAnchorArray[1], out offsetAnchorItem))
                    {
                        DateTime = DateTime.AddDays(offsetAnchorItem - DateTime.Day);
                        SetParts |= DateTimeParts.Day;
                    }
                }

                if (CompoundOffset != 0)
                {
                    Context.UseInference = true;
                }

                // Apply future/past normalization here if the year is still unspecified. Adjust the year by +-1
                // This is a weird workaround that I had to implement. Basically, there can be cases like "Thanksgiving"
                // which are expressed as "11-01 + 4thursdays", and I wanted to make sure that normalization rules
                // were being applied properly to holidays in this format. However, there are also cases like "This November"
                // which come up as "11 + 0months", and for which we don't want to use normalization.
                // As a solution, normalization is applied if the year is unspecified, but not the day.
                if (!SetParts.HasFlag(DateTimeParts.Year) && offsetAnchorArray.Length > 1)
                {
                    // Test resolve the offset into an actual value, so we know when this date is actually going to land when it resolves
                    // This lets us properly infer, for example, if Thanksgiving has passed or not, when the reference time is the middle of November
                    DateTime = DateTime.AddYears(Context.ReferenceDateTime.Year - DateTime.Year);
                    DateTime inferredTargetTime = new DateTime(this.DateTime.Ticks);

                    if (OffsetUnit.IsWeekday())
                    {
                        inferredTargetTime = ApplyDayOfWeekOffset(inferredTargetTime,
                            OffsetUnit.GetValueOrDefault().ConvertIntoDayOfWeek(),
                            Offset.HasValue ? Offset.Value : 0,
                            Context.Normalization,
                            WeekdayLogic.Programmatic,
                            0);
                    }

                    inferredTargetTime = inferredTargetTime.AddDays(CompoundOffset);

                    if (Context.ReferenceDateTime < inferredTargetTime &&
                        Context.Normalization == Normalization.Past)
                    {
                        DateTime = DateTime.AddYears(-1);
                    }
                    else if (Context.ReferenceDateTime > inferredTargetTime &&
                             Context.Normalization == Normalization.Future)
                    {
                        DateTime = DateTime.AddYears(1);
                    }

                    if (Context.UseInference)
                    {
                        SetParts |= DateTimeParts.Year;
                    }
                }
            }
        }

        

        /// <summary>
        /// Sets specified weekday with offset. This method is intended
        /// to perform the operation of "Modify the passed-in date so that it falls on the Xth Y-day
        /// relative to its original position". I.e. "Move the date forward by 2 mondays" or
        /// "Move it back to last Saturday".
        /// If offset is 0, the return value will be set to the _nearest_ day of week relative to the reference
        /// date. This can be either past or future, whichever is closer.
        /// </summary>
        /// <param name="dateTimeWithOffset">(ref) Reference DateTime to be modified</param>
        /// <param name="dayOfWeek">Desired day of week to seek to</param>
        /// <param name="offset">Offset amount (i.e. "3 thursdays")</param>
        /// <param name="normalization">The normalization to apply</param>
        /// <param name="weekdayLogic"></param>
        /// <param name="minWeekdayOffset"></param>
        /// <returns>A new DateTime object with specified day of week and of specified offset</returns>
        /// TODO: Split this into delegate methods contained inside the WeekdayLogic class itself
        private static DateTime ApplyDayOfWeekOffset(DateTime dateTimeWithOffset,
            DayOfWeek dayOfWeek,
            int offset,
            Normalization normalization = Normalization.Future,
            WeekdayLogic weekdayLogic = WeekdayLogic.Programmatic,
            int minWeekdayOffset = 0)
        {
            // The direction and magnitude of the day offset
            var offsetSign = Math.Sign(offset);
            var offsetAbs = Math.Abs(offset);

            // The day of week of the original date (in ISO semantics- Sunday == 7)
            int referenceDayOfWeekNumber = TimexHelpers.GetIso8601DayOfWeek(dateTimeWithOffset);

            // The day of the week that we are targeting (again, ISO semantics)
            var dayOfWeekNumber = (int)dayOfWeek;
            if (dayOfWeekNumber == 0)
                dayOfWeekNumber = 7;

            // Offset value is 0. This means the input was something like "This monday".
            if (offsetAbs == 0)
            {
                int daysToOffset = dayOfWeekNumber - referenceDayOfWeekNumber;

                // Move to the _nearest_ monday relative to the reference date, not just the one that is in the current week
                if (dayOfWeekNumber - referenceDayOfWeekNumber > DaysInOneHalfWeek)
                {
                    daysToOffset -= DaysInOneWeek;
                }
                else if (referenceDayOfWeekNumber - dayOfWeekNumber > DaysInOneHalfWeek)
                {
                    daysToOffset += DaysInOneWeek;
                }

                // Apply past/future normalization
                if (normalization == Normalization.Past && daysToOffset >= 0)
                {
                    daysToOffset -= DaysInOneWeek;
                }
                if (normalization == Normalization.Future && daysToOffset <= 0)
                {
                    daysToOffset += DaysInOneWeek;
                }

                // And apply the calculated offset to the return value
                return dateTimeWithOffset.AddDays(daysToOffset);
            }
            else
            {
                // Set the day of week to the proper value (within the current week)
                int daysToOffset = dayOfWeekNumber - referenceDayOfWeekNumber;

                // Apply simple past/future normalization (just make sure that "next" is really "next", and vice versa)
                if (weekdayLogic == WeekdayLogic.Programmatic)
                {
                    // This case gets triggered by holidays like "Thanksgiving" (which is expressed as "next next next next thursday from november 1st").
                    // It needs to use slightly different criteria for normalization that is less dependent on natural language (note the difference in < and <=)
                    if (offsetSign > 0 && dayOfWeekNumber < referenceDayOfWeekNumber)
                    {
                        daysToOffset += DaysInOneWeek;
                    }
                    else if (offsetSign < 0 && dayOfWeekNumber > referenceDayOfWeekNumber)
                    {
                        daysToOffset -= DaysInOneWeek;
                    }
                }
                else
                {
                    // This is the regular case for things like "Next monday"
                    if (offsetSign > 0 && dayOfWeekNumber <= referenceDayOfWeekNumber)
                    {
                        daysToOffset += DaysInOneWeek;
                    }
                    else if (offsetSign < 0 && dayOfWeekNumber >= referenceDayOfWeekNumber)
                    {
                        daysToOffset -= DaysInOneWeek;
                    }

                    // If we are in week boundary mode, make sure that the inferred date is not within the current week
                    if (weekdayLogic == WeekdayLogic.WeekBoundary)
                    {
                        if (offsetSign > 0 && daysToOffset <= (DaysInOneWeek - referenceDayOfWeekNumber))
                        {
                            daysToOffset += DaysInOneWeek;
                        }
                        else if (offsetSign < 0 && daysToOffset + referenceDayOfWeekNumber > 0)
                        {
                            daysToOffset -= DaysInOneWeek;
                        }
                    }

                    // If the date is tomorrow or yesterday, increase the offset by one week (so "next monday" refers to 8 days in the future)
                    // This is the whole reason the "MINIMUM_OFFSET" attribute exists
                    if (offsetSign > 0 && daysToOffset <= minWeekdayOffset)
                    {
                        daysToOffset += DaysInOneWeek;
                    }
                    else if (offsetSign < 0 && daysToOffset >= 0 - minWeekdayOffset)
                    {
                        daysToOffset -= DaysInOneWeek;
                    }
                }

                // After the dateTimeWithOffset is on the proper day of the week, zoom it now to the proper week of year (for offsets greater than one week)
                if (offsetAbs > 1)
                {
                    daysToOffset += (DaysInOneWeek * offsetSign * (offsetAbs - 1));
                }

                // And apply the calculated offset to the return value
                return dateTimeWithOffset.AddDays(daysToOffset);
            }
        }

        /// <summary>
        /// Converts a date in the format of YEAR-WEEK#-DAYOFWEEK into a string of the standard
        /// format YYYY-MM-DD, using the Iso8601 Day template.
        /// </summary>
        /// <param name="weekYear">The Iso8601 year</param>
        /// <param name="week">The Iso8601 week</param>
        /// <param name="weekDay">The Iso8601 day of week (from 1 to 7)</param>
        /// <return>A date string formatted as YYYY-MM-DD</return>
        private static string ConvertWeekFormatToStandard(int weekYear, int week, int weekDay)
        {
            // Create a reference date at the middle of the specified weekyear
            var absoluteTime = new DateTime(weekYear, 6, 1);

            // Seek to the correct week
            int isoYear;
            int offset = week - TimexHelpers.GetIso8601WeekOfYear(absoluteTime, out isoYear);
            absoluteTime = absoluteTime.AddDays(offset * DaysInOneWeek);

            // Seek to the correct day of week
            offset = weekDay - TimexHelpers.GetIso8601DayOfWeek(absoluteTime);
            absoluteTime = absoluteTime.AddDays(offset);

            return string.Format(Iso8601.DayTemplate,
                absoluteTime.Year.ToString("D4"),
                absoluteTime.Month.ToString("D2"),
                absoluteTime.Day.ToString("D2"));
        }

        private static bool TrySetPartOfDayDefaultTimes(IDictionary<PartOfDay, PartOfDayDefaultTimes> partOfDayDefaultTimes, ExtendedDateTime returnVal)
        {
            if (partOfDayDefaultTimes == null ||
                partOfDayDefaultTimes.Count == 0 ||
                !partOfDayDefaultTimes.ContainsKey(returnVal.PartOfDay))
            {
                return false;
            }

            // Customization does not override explicit specification of hour (in the expression)
            if (returnVal.ExplicitSetParts.HasFlag(DateTimeParts.Hour))
            {
                return false;
            }

            // Customization only applies to offset unit (if any) of type day or when part-of-day is set
            if (!returnVal.OffsetUnit.IsDay() &&
                !returnVal.ExplicitSetParts.HasFlag(DateTimeParts.Day) &&
                !returnVal.ExplicitSetParts.HasFlag(DateTimeParts.WeekDay) &&
                returnVal.PartOfDay == PartOfDay.None)
            {
                return false;
            }

            int hour, minute, second;
            switch (returnVal.Modifier)
            {
                case Enums.Modifier.Start:
                    hour = partOfDayDefaultTimes[returnVal.PartOfDay].StartHour;
                    minute = partOfDayDefaultTimes[returnVal.PartOfDay].StartMinute.Value;
                    second = partOfDayDefaultTimes[returnVal.PartOfDay].StartSecond.Value;
                    break;

                case Enums.Modifier.Mid:
                    // Simplification: Take the mid hour
                    hour = (int)Math.Floor((partOfDayDefaultTimes[returnVal.PartOfDay].StartHour + partOfDayDefaultTimes[returnVal.PartOfDay].EndHour) / 2.0);
                    minute = 0;
                    second = 0;
                    break;

                case Enums.Modifier.End:
                    hour = partOfDayDefaultTimes[returnVal.PartOfDay].EndHour;
                    minute = partOfDayDefaultTimes[returnVal.PartOfDay].EndMinute.Value;
                    second = partOfDayDefaultTimes[returnVal.PartOfDay].EndSecond.Value;
                    break;

                default:
                    return false;
            }

            var timexDictionary = new Dictionary<string, string>()
            {
                { Iso8601.Hour, hour.ToString() },
                { Iso8601.Minute, minute.ToString() },
                { Iso8601.Second, second.ToString() }
            };

            ParseHour(timexDictionary, returnVal);
            ParseMinute(timexDictionary, returnVal);
            ParseSecond(timexDictionary, returnVal);

            return true;
        }

        /// <summary>
        /// Static function that combines two ExtendedDateTime objects, returning a new object
        /// that has been created from a union of the original TimexDictionaries. In the case of
        /// overlap (i.e. the intersection of the dictionaries != empty set) the values from the first object will hold precedence.
        /// </summary>
        /// <param name="first">The first time to be merged</param>
        /// <param name="second">The first time to be merged</param>
        /// <param name="type">The temporaltype of the newly merged EDT</param>
        /// <param name="context">The context to use for resolving the final date</param>
        /// <returns>A pointer to a newly created ExtendedDateTime object, created within the specified context</returns>
        public static ExtendedDateTime Merge(ExtendedDateTime first, ExtendedDateTime second, TemporalType type, TimexContext context)
        {
            if (first == null)
            {
                throw new ArgumentNullException("first");
            }

            if (second == null)
            {
                throw new ArgumentNullException("second");
            }
            
            var mergedTimexDictionary = new Dictionary<string, string>();
            foreach (string key in first.OriginalTimexDictionary.Keys)
            {
                mergedTimexDictionary.Add(key, first.OriginalTimexDictionary[key]);
            }
            foreach (string key in second.OriginalTimexDictionary.Keys)
            {
                if (!mergedTimexDictionary.ContainsKey(key))
                    mergedTimexDictionary.Add(key, second.OriginalTimexDictionary[key]);
            }
            if (mergedTimexDictionary.ContainsKey(TimexAttributes.RangeHint))
            {
                mergedTimexDictionary.Remove(TimexAttributes.RangeHint);
            }

            return Create(type, mergedTimexDictionary, context);
        }

        /// <summary>
        /// Clones this ExtendedDateTime, applying a new context during creation of the duplicate
        /// </summary>
        /// <param name="newContext">The new context to apply to the returned value</param>
        /// <returns>A new ExtendedDateTime that contains the same information as this one, but interpreted in a different context</returns>
        public ExtendedDateTime Reinterpret(TimexContext newContext)
        {
            return Create(TemporalType, OriginalTimexDictionary, newContext);
        }

        /// <summary>
        /// Returns true if this datetime is expressed as an anchor + offset. This is used for expressions like "today",
        /// "tomorrow", "Christmas", and others.
        /// </summary>
        public bool IsOffset()
        {
            return Offset != null;
        }

        /// <summary>
        /// Returns true if this datetime is of TemporalType.Time and contains only part-of-day information; in other words,
        /// the original input was "morning" or "afternoon" or similar.
        /// </summary>
        /// <returns></returns>
        public bool IsPartOfDayOnly()
        {
            return SetParts == DateTimeParts.PartOfDay && !IsOffset() && TemporalType == TemporalType.Time;
        }

        /// <summary>
        /// Simply inverts the hour value if this object; if it is before 12, make it PM. otherwise make it AM.
        /// If the hour field is not set, this method does nothing. The unspecified_am_pm flag is unaffected.
        /// </summary>
        public void FlipAmPm()
        {
            if (ExplicitSetParts.HasFlag(DateTimeParts.Hour))
            {
                int currentHour = DateTime.Hour;
                if (currentHour < 12)
                {
                    DateTime = DateTime.AddHours(12);
                }
                else
                {
                    DateTime = DateTime.AddHours(-12);
                }

                _cachedValueString = string.Empty;
            }
        }

        /// <summary>
        /// If this ExtendedDateTime is given as a duration (like P6H), convert into an absolute time
        /// by offsetting it from a given reference date time.
        /// </summary>
        /// <param name="newContext">The context to use for resolving the duration value</param>
        /// <returns>A new ExtendedDateTime that stores its value as an offset rather than a duration</returns>
        public ExtendedDateTime ConvertDurationIntoOffset(TimexContext newContext)
        {
            if (Duration.IsSet())
            {
                Tuple<int, TemporalUnit?> durationValue = Duration.SimpleValue;
                if (!durationValue.Item2.HasValue)
                    return null;

			    IDictionary<string, string> offsetDictionary = new Dictionary<string, string>();
                offsetDictionary[TimexAttributes.Offset] = string.Format("{0}", 
				    (newContext.Normalization == Normalization.Past ? (0 - durationValue.Item1) : durationValue.Item1));
                offsetDictionary[TimexAttributes.OffsetUnit] = EnumExtensions.ToString(durationValue.Item2.Value);
                // The temporal type of the return value will be either a date or a time; pass "none" will force the constructor to infer it automatically
                return ExtendedDateTime.Create(TemporalType.None, offsetDictionary, newContext);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Compares this ExtendedDateTime tenatively against a fixed datetime, only returning a nonzero value if it is
        /// GUARANTEED that this EDT comes before/after the given datetime. This means that the comparison is only done
        /// for year, month, day, hour, minute, and second, and then only if the corresponding SetParts are flagged.
        /// </summary>
        /// <param name="comparisonTime">The fixed datetime to compare against</param>
        public int IncompleteCompareTo(DateTime comparisonTime)
        {
            int returnVal = 0;

            if (SetParts.HasFlag(DateTimeParts.Year))
            {
                returnVal = DateTime.Year - comparisonTime.Year;
            }
            else
            {
                return returnVal;
            }

            if (returnVal == 0 && SetParts.HasFlag(DateTimeParts.Month))
            {
                returnVal = DateTime.Month - comparisonTime.Month;
            }
            else
            {
                return returnVal;
            }

            if (returnVal == 0 && SetParts.HasFlag(DateTimeParts.Day))
            {
                returnVal = DateTime.Day - comparisonTime.Day;
            }
            else
            {
                return returnVal;
            }

            if (returnVal == 0 && SetParts.HasFlag(DateTimeParts.Hour))
            {
                returnVal = DateTime.Hour - comparisonTime.Hour;
            }
            else
            {
                return returnVal;
            }

            if (returnVal == 0 && SetParts.HasFlag(DateTimeParts.Minute))
            {
                returnVal = DateTime.Minute - comparisonTime.Minute;
            }
            else
            {
                return returnVal;
            }

            if (returnVal == 0 && SetParts.HasFlag(DateTimeParts.Second))
            {
                returnVal = DateTime.Second - comparisonTime.Second;
            }
            else
            {
                return returnVal;
            }

            return returnVal;
        }

        private const uint SERIALIZED_MAGIC_NUMBER = 0x3E8A9B20U;
        private const uint CURRENT_VERSION = 2;

        /// <summary>
        /// Serializes this ExtendedDateTime to a binary blob
        /// </summary>
        /// <returns></returns>
        public byte[] Serialize()
        {
            using (RecyclableMemoryStream streamOut = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (BinaryWriter writer = new BinaryWriter(streamOut, StringUtils.UTF8_WITHOUT_BOM, false))
                {
                    // Magic number (28 bits) and version (4 bits)
                    writer.Write(SERIALIZED_MAGIC_NUMBER | CURRENT_VERSION);

                    // Serialize the context
                    this.Context.Serialize(writer);
                    
                    // And serialize the timex dictionary
                    writer.Write(OriginalTimexDictionary.Count);
                    foreach (var kvp in OriginalTimexDictionary)
                    {
                        writer.Write(kvp.Key);
                        writer.Write(kvp.Value);
                    }
                }

                return streamOut.ToArray();
            }
        }

        /// <summary>
        /// Deserializes this ExtendedDateTime from a binary blob, along with its original resolution context
        /// </summary>
        /// <param name="blob"></param>
        /// <returns>The parsed extendeddatetime</returns>
        public static ExtendedDateTime Deserialize(byte[] blob)
        {
            TemporalType type = TemporalType.None;
            Dictionary<string, string> timexDict = new Dictionary<string, string>();

            using (MemoryStream streamIn = new MemoryStream(blob, false))
            {
                using (BinaryReader reader = new BinaryReader(streamIn, StringUtils.UTF8_WITHOUT_BOM, false))
                {
                    uint magicNumber = reader.ReadUInt32();
                    if ((magicNumber & 0xFFFFFFF0U) != SERIALIZED_MAGIC_NUMBER)
                    {
                        throw new IOException("The value being deserialized is not an ExtendedDateTime");
                    }

                    uint version = magicNumber & 0x0000000FU;
                    if (version > CURRENT_VERSION)
                    {
                        throw new IOException("The given binary data cannot be deserialized by this library");
                    }

                    TimexContext context = TimexContext.Deserialize(reader);

                    int timexDictCount = reader.ReadInt32();
                    for (int c = 0; c < timexDictCount; c++)
                    {
                        string key = reader.ReadString();
                        string value = reader.ReadString();
                        timexDict[key] = value;
                    }

                    return Create(type, timexDict, context);
                }
            }
        }

        #endregion
    }
}

#pragma warning restore 0618