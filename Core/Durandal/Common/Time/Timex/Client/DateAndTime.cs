namespace Durandal.Common.Time.Timex.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Globalization;
    using Durandal.Common.Time.Timex.Enums;
    using Constants;
    using Calendar;
    using System.Diagnostics.CodeAnalysis;

    public class DateAndTime : TimexValue
    {
        private static readonly IDictionary<PartOfDay, PartOfDayDefaultTimes> partOfDayDefaultTimes = new Dictionary<PartOfDay, PartOfDayDefaultTimes>
            {
                { PartOfDay.Morning, new PartOfDayDefaultTimes(PartOfDay.Morning, PartOfDay.Morning.ToApproximateHour(), PartOfDay.Noon.ToApproximateHour()) },
                { PartOfDay.Noon, new PartOfDayDefaultTimes(PartOfDay.Noon, PartOfDay.Noon.ToApproximateHour(), PartOfDay.Afternoon.ToApproximateHour()) },
                { PartOfDay.MidDay, new PartOfDayDefaultTimes(PartOfDay.MidDay, PartOfDay.MidDay.ToApproximateHour(), PartOfDay.Afternoon.ToApproximateHour()) },
                { PartOfDay.Afternoon, new PartOfDayDefaultTimes(PartOfDay.Afternoon, PartOfDay.Afternoon.ToApproximateHour(), PartOfDay.Evening.ToApproximateHour()) },
                { PartOfDay.Evening, new PartOfDayDefaultTimes(PartOfDay.Evening, PartOfDay.Evening.ToApproximateHour(), PartOfDay.Night.ToApproximateHour()) },
                { PartOfDay.Night, new PartOfDayDefaultTimes(PartOfDay.Night, PartOfDay.Night.ToApproximateHour(), PartOfDay.Midnight.ToApproximateHour()) }
            };

        public override TemporalType GetTemporalType()
        {
            TemporalType t = TemporalType.None;
            if (ContainsDateInfo())
            {
                t |= TemporalType.Date;
            }
            if (ContainsTimeInfo())
            {
                t |= TemporalType.Time;
            }

            return t;
        }

        #region Date Field Accessors

        /// <summary>
        /// Returns the year field of this time, or null if it is not specified.
        /// !!! NOTE THAT !!! for ISO week expressions such as 2016-W01, this method will return
        /// the ISO YEAR rather than the Gregorian year! This distinction can cause a lot of trouble
        /// at year boundaries!
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public int? GetYear()
        {
            return this.GetValueAsInt(DateTimeParts.Year);
        }

        /// <summary>
        /// Returns the month field of this time, or null if it is not specified
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public int? GetMonth()
        {
            return this.GetValueAsInt(DateTimeParts.Month);
        }

        /// <summary>
        /// Returns the day of month field of this time, or null if it is not specified
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public int? GetDayOfMonth()
        {
            return this.GetValueAsInt(DateTimeParts.Day);
        }

        /// <summary>
        /// Returns the (ISO) week of year field of this time, or null if it is not specified
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public int? GetWeekOfYear()
        {
            // If month is specified, it's not technically a week of year so suppress it
            if (!this.ParsedValues.ContainsKey(DateTimeParts.Month))
            {
                return this.GetValueAsInt(DateTimeParts.Week);
            }

            return null;
        }

        /// <summary>
        /// Returns the week of month field of this time, or null if it is not specified.
        /// The interpretation of this field is kind of hazy - generally it's assumed that you have
        /// a weekday also specified, in which case "W03-1" would refer to simply the 3rd
        /// monday of the month. "YYYY-MM-WWW" is technically undefined for now, but it's probably
        /// safe to use the ISO "first week containing a thursday" rule to resolve it.
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public int? GetWeekOfMonth()
        {
            // only return week of month if a month is specified
            if (this.ParsedValues.ContainsKey(DateTimeParts.Month))
            {
                return this.GetValueAsInt(DateTimeParts.Week);
            }

            return null;
        }

        /// <summary>
        /// Returns the day of week field of this time, or null if it is not specified.
        /// Monday is 1 and Sunday is 7, according to ISO
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public int? GetDayOfWeek()
        {
            return this.GetValueAsInt(DateTimeParts.WeekDay);
        }

        /// <summary>
        /// Returns the season field of this time, such as "WI" of "SP", or Season.None if it is not specified
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public Season GetSeason()
        {
            string stringVal = this.GetValueAsString(DateTimeParts.Season);

            if (string.IsNullOrEmpty(stringVal))
            {
                return Season.None;
            }

            return DateTimeParserHelpers.ParseSeason(stringVal);
        }

        /// <summary>
        /// Returns the part of year field of this time (such as H1 or Q3), or null if it is not specified
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public string GetPartOfYear()
        {
            return this.GetValueAsString(DateTimeParts.PartOfYear);
        }

        /// <summary>
        /// Returns the part of week field of this time (such as WE or WD for weekend / weekdays), or null if it is not specified
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public PartOfWeek GetPartOfWeek()
        {
            string stringVal = this.GetValueAsString(DateTimeParts.PartOfWeek);

            if (string.IsNullOrEmpty(stringVal))
            {
                return PartOfWeek.None;
            }

            return DateTimeParserHelpers.ParsePartOfWeek(stringVal);
        }

        /// <summary>
        /// Returns true if "weekof" was specified in this timex comment, indicating an expression such as
        /// "The week of september 30th"
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        [SuppressMessage("Microsoft.Naming", "CA1726")]
        public bool GetWeekOfFlag()
        {
            return string.Equals("true", this.GetValueAsString(DateTimeParts.WeekOfExpression));
        }

        /// <summary>
        /// Returns true if this object contains any year/month/day/week information
        /// </summary>
        /// <returns>True if this object contains date info</returns>
        public bool ContainsDateInfo()
        {
            return ParsedValues.ContainsKey(DateTimeParts.Year) ||
                   ParsedValues.ContainsKey(DateTimeParts.Month) ||
                   ParsedValues.ContainsKey(DateTimeParts.Day) ||
                   ParsedValues.ContainsKey(DateTimeParts.Week) ||
                   ParsedValues.ContainsKey(DateTimeParts.WeekDay) ||
                   ParsedValues.ContainsKey(DateTimeParts.Season) ||
                   ParsedValues.ContainsKey(DateTimeParts.PartOfYear) ||
                   ParsedValues.ContainsKey(DateTimeParts.PartOfWeek);
        }

        #endregion

        #region Time Field Accessors

        /// <summary>
        /// Returns the hour field of this time, or null if it is not specified
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public int? GetHour()
        {
            return this.GetValueAsInt(DateTimeParts.Hour);
        }

        /// <summary>
        /// Returns the minute field of this time, or null if it is not specified
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public int? GetMinute()
        {
            return this.GetValueAsInt(DateTimeParts.Minute);
        }

        /// <summary>
        /// Returns the second field of this time, or null if it is not specified
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public int? GetSecond()
        {
            return this.GetValueAsInt(DateTimeParts.Second);
        }

        /// <summary>
        /// Returns the timezone field of this time, or null if it is not specified
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public string GetTimeZone()
        {
            return this.GetValueAsString(DateTimeParts.TimeZone);
        }

        /// <summary>
        /// Returns the part of day field of this time (such as morning, evening, etc.), or PartOfDay.None if it is not specified
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public PartOfDay GetPartOfDay()
        {
            string stringVal = this.GetValueAsString(DateTimeParts.PartOfDay);

            if (string.IsNullOrEmpty(stringVal))
            {
                return PartOfDay.None;
            }

            return DateTimeParserHelpers.ParsePartOfDay(stringVal);
        }

        /// <summary>
        /// Returns a flag indicating that AM/PM was ambiguous in the original expression and was not fully resolved, therefore
        /// the client is responsible for providing some type of resolution logic.
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        [SuppressMessage("Microsoft.Naming", "CA1709", MessageId = "Pm")]
        [SuppressMessage("Microsoft.Naming", "CA1726")]
        public bool GetAmPmAmbiguousFlag()
        {
            return string.Equals("true", this.GetValueAsString(DateTimeParts.AmPmUnambiguous));
        }

        /// <summary>
        /// Returns the "reference" property of this expression. This means the input was a vague reference to "now", "the future", "some time ago", etc.
        /// and has no units.
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public DateTimeReference GetReference()
        {
            string val = GetValueAsString(DateTimeParts.Reference);

            DateTimeReference r;
            if (EnumExtensions.TryParse(val, out r))
            {
                return r;
            }

            return DateTimeReference.None;
        }

        /// <summary>
        /// Returns true if this object contains any hour/minute/second information
        /// </summary>
        /// <returns>True if this object contains time info</returns>
        public bool ContainsTimeInfo()
        {
            return ParsedValues.ContainsKey(DateTimeParts.Hour) ||
                   ParsedValues.ContainsKey(DateTimeParts.Minute) ||
                   ParsedValues.ContainsKey(DateTimeParts.Second) ||
                   ParsedValues.ContainsKey(DateTimeParts.PartOfDay) ||
                   ParsedValues.ContainsKey(DateTimeParts.Reference);
        }

        #endregion

        #region Parsers

        /// <summary>
        /// Parses an ISO date string in one of the following formats:
        /// 2013
        /// 2014-11
        /// 2012-Q1 (quarters)
        /// 2012-H1
        /// 2012-SP / WI / SU / FA (seasons)
        /// 2012-11-11
        /// 2012-W01
        /// 2012-W01-WE (weekend)
        /// 2012-W01-WD (weekday) (this should really be a recurrence, but oh well)
        /// 2012-W01-1
        /// 2012-11-W01-1
        /// The output will be stored inside this object's ParsedValues dictionary
        /// </summary>
        /// <param name="isoDateString">The ISO string to be parsed</param>
        [SuppressMessage("Microsoft.Maintainability", "CA1502")]
        internal void ParseDateValue(string isoDateString)
        {
            if (string.IsNullOrEmpty(isoDateString))
            {
                return;
            }

            if (isoDateString.EndsWith("_REF", StringComparison.Ordinal))
            {
                SetValue(DateTimeParts.Reference, isoDateString);
                return;
            }

            IList<string> dateTimeComponents = isoDateString.Split(new char[] { Iso8601.DateDelimiter[0] }, 4);

            if (dateTimeComponents.Count >= 1 &&
                dateTimeComponents[0].Length == 4 &&
                DateTimeParserHelpers.IsIntegerString(dateTimeComponents[0]))
            {
                SetValue(DateTimeParts.Year, dateTimeComponents[0]); // Capture a year value
            }

            if (dateTimeComponents.Count >= 2)
            {
                // Is it a regular numerical month?
                if (dateTimeComponents[1].Length == 2 &&
                    DateTimeParserHelpers.IsIntegerString(dateTimeComponents[1]))
                {
                    SetValue(DateTimeParts.Month, dateTimeComponents[1]);
                }
                else if (dateTimeComponents[1].Length >= 2)
                {
                    char firstLetter = dateTimeComponents[1][0];
                    string substring = dateTimeComponents[1].Substring(1);

                    if (dateTimeComponents[1].Length == 3 &&
                        firstLetter == 'W' &&
                        DateTimeParserHelpers.IsIntegerString(substring))
                    {
                        // Is it a week reference?
                        SetValue(DateTimeParts.Week, substring);
                    }
                    else if (dateTimeComponents[1].Length > 1 &&
                        (firstLetter == 'Q' || firstLetter == 'H') &&
                        DateTimeParserHelpers.IsIntegerString(substring))
                    {
                        // Is it a quarter/half reference?
                        SetValue(DateTimeParts.PartOfYear, dateTimeComponents[1]);
                    }
                    else if (dateTimeComponents[1].Equals("WI") ||
                        dateTimeComponents[1].Equals("SU") ||
                        dateTimeComponents[1].Equals("FA") ||
                        dateTimeComponents[1].Equals("SP"))
                    {
                        // Is it a season reference?
                        SetValue(DateTimeParts.Season, dateTimeComponents[1]);
                    }
                }
            }

            if (dateTimeComponents.Count >= 3)
            {
                // Is this a weekend or weekday reference?
                if (dateTimeComponents[2] == "WE" || dateTimeComponents[2] == "WD")
                {
                    SetValue(DateTimeParts.PartOfWeek, dateTimeComponents[2]);
                }
                else if (DateTimeParserHelpers.IsIntegerString(dateTimeComponents[2]))
                {
                    if (dateTimeComponents[2].Length == 2)
                    {
                        //It's a regular day reference ("the 21st" = "21")
                        SetValue(DateTimeParts.Day, dateTimeComponents[2]);
                    }
                    else if (dateTimeComponents[2].Length == 1)
                    {
                        // It's a day of week reference ("monday" = "1", etc)
                        SetValue(DateTimeParts.WeekDay, dateTimeComponents[2]);
                    }
                }
                else if (dateTimeComponents.Count == 4
                    && dateTimeComponents[2].StartsWith("W", StringComparison.Ordinal)
                    && dateTimeComponents[2].Length == 3)
                {
                    // It's a day-of-week-of-month expression, which means components[2] is something like "W06"
                    SetValue(DateTimeParts.Week, dateTimeComponents[2].Substring(1));
                }
            }

            if (dateTimeComponents.Count >= 4 &&
                DateTimeParserHelpers.IsIntegerString(dateTimeComponents[3]))
            {
                // Only occurs in "Nth weekday of month" format, 2012-11-W04-4
                SetValue(DateTimeParts.WeekDay, dateTimeComponents[3]);
            }
        }

        /// <summary>
        /// Parses an ISO time string in one of the following formats:
        /// 12
        /// 12:00
        /// 12:00:00
        /// 12:00:00-800
        /// MO / MI / AF / EV / NI / PM / DT (parts of day)
        /// All outputs will be dumped into the timex value dictionary as string values
        /// </summary>
        /// <param name="isoTimeString"></param>
        internal void ParseTimeValue(string isoTimeString)
        {
            if (string.IsNullOrEmpty(isoTimeString))
            {
                return;
            }

            if (isoTimeString.EndsWith("_REF", StringComparison.Ordinal))
            {
                SetValue(DateTimeParts.Reference, isoTimeString);
                return;
            }

            // Trim the leading "T" if the caller forgot to do so
            if (isoTimeString.StartsWith("T", StringComparison.Ordinal))
            {
                isoTimeString = isoTimeString.Substring(1);
            }

            // See if it is a "time of day" pattern
            if (isoTimeString.Equals("MO") ||
                isoTimeString.Equals("MI") ||
                isoTimeString.Equals("AF") ||
                isoTimeString.Equals("EV") ||
                isoTimeString.Equals("NI") ||
                isoTimeString.Equals("PM") ||
                isoTimeString.Equals("DT"))
            {
                SetValue(DateTimeParts.PartOfDay, isoTimeString);
                return;
            }

            // BUGBUG: We don't enfore the upper limit to values, so T99:99:99 is a technically a valid time

            // Now we can assume it is a 12:00:00-800 pattern. Split it into substrings
            string[] timeParts = isoTimeString.Split(':');

            if (timeParts.Length >= 1 && timeParts[0].Length == 2 && DateTimeParserHelpers.IsIntegerString(timeParts[0]))
            {
                // Hour field is present
                SetValue(DateTimeParts.Hour, timeParts[0]);
            }

            if (timeParts.Length >= 2 && timeParts[1].Length == 2 && DateTimeParserHelpers.IsIntegerString(timeParts[1]))
            {
                // Minute field is present
                SetValue(DateTimeParts.Minute, timeParts[1]);
            }

            if (timeParts.Length == 3)
            {
                // Second field is present, and potentially there is a timezone attached to it
                string secondsField = timeParts[2].Trim();
                if (timeParts[2].Length > 2)
                {
                    // Timezone is here. Split it out
                    // The library currently ignores time zone and assumes everything is local, so drop timezone
                    secondsField = timeParts[2].Substring(0, 2);

                    if (DateTimeParserHelpers.IsIntegerString(secondsField))
                    {
                        SetValue(DateTimeParts.Second, secondsField);
                        SetValue(DateTimeParts.TimeZone, timeParts[2].Substring(2));
                    }
                }
                else if (secondsField.Length == 2 && DateTimeParserHelpers.IsIntegerString(secondsField))
                {
                    SetValue(DateTimeParts.Second, secondsField);
                }
            }
        }

        #endregion

        /// <summary>
        /// Attempts to convert this time value into a regular C# DateTime.
        /// This will only be possible if every field is populated in descending order. That means that these are OK:
        /// 2014
        /// 2014-11
        /// 2014-11-10
        /// 2014-11-10T12
        /// 2014-11-10T12:00
        /// 2014-11:10T12:00:00
        /// 2014-W01-1
        /// But these are not since they are technically unresolved:
        /// T12
        /// T12:00
        /// T12:00:00
        /// XXXX-XX-10
        /// XXXX-11
        /// XXXX-XX-10T12
        /// 2012-W01 (needs a day of week)
        /// </summary>
        /// <returns></returns>
        [Obsolete("This property is deprecated because in some cases it results in values for some components of DateTime that were not contained in ParsedValues.  Use the InterpretAsNaturalTimeRange method instead and construct DateTime instances with the desired level of accuracy.")]
        public DateTime? TryConvertIntoCSharpDateTime()
        {
            // Do we have incomplete time/date information? (in this case, a minute field without an hour or something like that)
            // This means the time is not fully resolved, and we cannot continue
            if (!ParsedValues.ContainsKey(DateTimeParts.Year))
            {
                return null;
            }

            if ((ParsedValues.ContainsKey(DateTimeParts.Day)
                || ParsedValues.ContainsKey(DateTimeParts.Hour)
                || ParsedValues.ContainsKey(DateTimeParts.Minute)
                || ParsedValues.ContainsKey(DateTimeParts.Second)) &&
                !ParsedValues.ContainsKey(DateTimeParts.Month))
            {
                return null;
            }

            if ((ParsedValues.ContainsKey(DateTimeParts.Hour)
                || ParsedValues.ContainsKey(DateTimeParts.Minute)
                || ParsedValues.ContainsKey(DateTimeParts.Second)) &&
                !ParsedValues.ContainsKey(DateTimeParts.Day))
            {
                return null;
            }

            if ((ParsedValues.ContainsKey(DateTimeParts.Minute) || ParsedValues.ContainsKey(DateTimeParts.Second)) &&
                !ParsedValues.ContainsKey(DateTimeParts.Hour))
            {
                return null;
            }

            if (ParsedValues.ContainsKey(DateTimeParts.Second) && !ParsedValues.ContainsKey(DateTimeParts.Minute))
            {
                return null;
            }

            if (ParsedValues.ContainsKey(DateTimeParts.Week) && !ParsedValues.ContainsKey(DateTimeParts.WeekDay))
            {
                return null;
            }

            // Check if it's in year-week-day format. Parse it if so
            if (ParsedValues.ContainsKey(DateTimeParts.Year) &&
                ParsedValues.ContainsKey(DateTimeParts.Week) &&
                ParsedValues.ContainsKey(DateTimeParts.WeekDay))
            {
                int? week = this.GetValueAsInt(DateTimeParts.Week);
                int? weekDay = this.GetValueAsInt(DateTimeParts.WeekDay);

                // This should never happen, but check just in case
                if (!week.HasValue || weekDay.HasValue)
                {
                    return null;
                }

                // Resolve the week-weekday format by dumping the date in the middle of the year and then scrolling it
                // Note that in this case, the "Year" is not actually the Gregorian calendar year, but the ISO "WeekYear".
                // Mixing these up can cause edge cases with the first and last weeks of the year
                DateTime returnVal = new DateTime(
                    this.GetYear().GetValueOrDefault(0),
                    6,
                    1,
                    this.GetHour().GetValueOrDefault(0),
                    this.GetMinute().GetValueOrDefault(0),
                    this.GetSecond().GetValueOrDefault(0));

                // Seek to the correct week
                int resultYear;
                int offset = week.Value - TimexHelpers.GetIso8601WeekOfYear(returnVal, out resultYear);
                returnVal = returnVal.AddDays(offset * 7);

                // Seek to the correct day of week
                offset = weekDay.Value - TimexHelpers.GetIso8601DayOfWeek(returnVal);
                returnVal = returnVal.AddDays(offset);

                return returnVal;
            }

            // If we get here, we know that it is a relatively resolved time (it may be missing seconds or minutes fields, but that's OK, we'll set them to 0)
            // So we'll build as complete of a return value as we can and return it.
            return new DateTime(
                this.GetYear().GetValueOrDefault(0),
                this.GetMonth().GetValueOrDefault(1),
                this.GetDayOfMonth().GetValueOrDefault(1),
                this.GetHour().GetValueOrDefault(0),
                this.GetMinute().GetValueOrDefault(0),
                this.GetSecond().GetValueOrDefault(0));
        }

        /// <summary>
        /// Interprets this date/time object as a time span which represents the earliest and latest
        /// points which could be referred to by this time, according to its granularity.
        /// For example, "2015" will return 2015-01-01T00:00:00 -> 2016-01-01T00:00:00
        /// "April 11" will return 2015-04-11T00:00:00 -> 2015-04-12T00:00:00
        /// "3:00 PM" will return 2017-05-22T15:00:00 -> 2017-05-22T16:00:00.
        /// </summary>
        /// <param name="weekDefinition">A structure describing the structure of the week in the current locale</param>
        /// <param name="partOfDayDefaultTimes">A set of start-end hour specifications for various parts of a day</param>
        /// <returns>A time range</returns>
        [SuppressMessage("Microsoft.Design", "CA1026")]
        public SimpleDateTimeRange InterpretAsNaturalTimeRange(LocalizedWeekDefinition weekDefinition = null, IDictionary<PartOfDay, PartOfDayDefaultTimes> partOfDayDefaultTimes = null)
        {
            if (weekDefinition == null)
            {
                weekDefinition = LocalizedWeekDefinition.StandardWeekDefinition;
            }

            if (partOfDayDefaultTimes == null)
            {
                partOfDayDefaultTimes = DateAndTime.partOfDayDefaultTimes;
            }

            PartOfDay partOfDay = this.GetPartOfDay();

            DateTime startTime;
            DateTime endTime;

            // Find the start time
            if (GetYear().HasValue && GetWeekOfYear().HasValue)
            {
                // Date refers to a week or weekend
                startTime = TimexHelpers.GetFirstDayOfIsoWeek(GetYear().Value, GetWeekOfYear().Value).AddDays(weekDefinition.FirstDayOfWeek);
                if (string.Equals(GetPartOfWeek(), PartOfWeek.Weekend))
                {
                    startTime = startTime.AddDays(weekDefinition.FirstDayOfWeekend);
                }
            }
            else
            {
                startTime = new DateTime(
                    GetYear().GetValueOrDefault(1),
                    GetMonth().GetValueOrDefault(1),
                    GetDayOfMonth().GetValueOrDefault(1),
                    GetHour().GetValueOrDefault(partOfDayDefaultTimes.ContainsKey(partOfDay) ? partOfDayDefaultTimes[partOfDay].StartHour : 0),
                    GetMinute().GetValueOrDefault(0),
                    GetSecond().GetValueOrDefault(0));
            }

            if (GetWeekOfFlag())
            {
                // Date has the "weekof" flag. Find what week the date falls into
                int isoWeekYear;
                int isoWeek = TimexHelpers.GetIso8601WeekOfYear(startTime, out isoWeekYear);
                // And then set start time to the localized beginning of that week
                startTime = TimexHelpers.GetFirstDayOfIsoWeek(isoWeekYear, isoWeek).AddDays(weekDefinition.FirstDayOfWeek);
            }

            // Then calculate end time based on granularity
            TemporalUnit granularity;
            if (GetSecond().HasValue)
            {
                granularity = TemporalUnit.Second;
                endTime = startTime.AddSeconds(1);
            }
            else if (GetMinute().HasValue)
            {
                granularity = TemporalUnit.Minute;
                endTime = startTime.AddMinutes(1);
            }
            else if (GetHour().HasValue)
            {
                granularity = TemporalUnit.Hour;
                endTime = startTime.AddHours(1);
            }
            else if (partOfDay != PartOfDay.None && partOfDayDefaultTimes.ContainsKey(partOfDay))
            {
                granularity = TemporalUnit.Hour;
                endTime = startTime.AddHours(partOfDayDefaultTimes[partOfDay].EndHour - partOfDayDefaultTimes[partOfDay].StartHour);
            }
            else if (GetWeekOfFlag())
            {
                granularity = TemporalUnit.Week;
                endTime = startTime.AddDays(7);
            }
            else if (GetDayOfMonth().HasValue)
            {
                granularity = TemporalUnit.Day;
                endTime = startTime.AddDays(1);
            }
            else if (string.Equals(GetPartOfWeek(), PartOfWeek.Weekend))
            {
                granularity = TemporalUnit.Weekend;
                endTime = startTime.AddDays(weekDefinition.WeekendLength);
            }
            else if (GetWeekOfYear().HasValue)
            {
                granularity = TemporalUnit.Week;
                endTime = startTime.AddDays(7);
            }
            else if (GetMonth().HasValue)
            {
                granularity = TemporalUnit.Month;
                endTime = startTime.AddMonths(1);
            }
            else if (GetYear().HasValue)
            {
                granularity = TemporalUnit.Year;
                endTime = startTime.AddYears(1);
            }
            else
            {
                return null;
            }

            return new SimpleDateTimeRange()
            {
                Start = startTime,
                End = endTime,
                Granularity = granularity
            };
        }

        /// <summary>
        /// Interprets this datetime as a natural time range, except also take into account a reference time
        /// (usually a user's reference time) which is used to "chop" time spans that overlap.
        /// For example, with "today", and directionality == Future, this will return a range from
        /// the user's current time until the end of day. Likewise for "this year", "this week", etc.
        /// </summary>
        /// <param name="referenceTime">The reference time to use for cutting off the range</param>
        /// <param name="spanDirectionality">The directionality to apply for any cutoff</param>
        /// <param name="weekDefinition">A structure describing the structure of the week in the current locale</param>
        /// <returns>A time range</returns>
        [SuppressMessage("Microsoft.Design", "CA1026")]
        public SimpleDateTimeRange InterpretAsNaturalTimeRange(DateTime referenceTime, Normalization spanDirectionality = Normalization.Future, LocalizedWeekDefinition weekDefinition = null)
        {
            if (spanDirectionality == Normalization.Present)
            {
                throw new ArgumentException("Present directionality is not allowed in this method; you must specify past or future");
            }

            if (weekDefinition == null)
            {
                weekDefinition = LocalizedWeekDefinition.StandardWeekDefinition;
            }

            // Is the timex just PRESENT_REF? Then return the current time
            if (GetReference() == DateTimeReference.Present)
            {
                return new SimpleDateTimeRange()
                {
                    Start = referenceTime,
                    End = referenceTime.AddSeconds(1),
                    Granularity = TemporalUnit.Second
                };
            }

            SimpleDateTimeRange baseRange = InterpretAsNaturalTimeRange();

            // Does the reference time fall within the span?
            if (referenceTime > baseRange.Start && referenceTime < baseRange.End)
            {
                // If so, chop it based on the current inference direction
                if (spanDirectionality == Normalization.Past)
                {
                    return new SimpleDateTimeRange()
                    {
                        Start = baseRange.Start,
                        End = referenceTime,
                        Granularity = baseRange.Granularity
                    };
                }
                else
                {
                    return new SimpleDateTimeRange()
                    {
                        Start = referenceTime,
                        End = baseRange.End,
                        Granularity = baseRange.Granularity
                    };
                }
            }
            else
            {
                // No overlap, so leave the result unchanged
                return baseRange;
            }
        }
    }
}
