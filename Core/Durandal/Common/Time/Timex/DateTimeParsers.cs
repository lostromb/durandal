using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Durandal.Common.Time.Timex.Calendar;
using Durandal.Common.Time.Timex.Constants;
using Durandal.Common.Time.Timex.Enums;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// Contains static helper methods to parse ISO date and time strings
    /// </summary>
    public static class DateTimeParsers
    {
        /// <summary>
        /// A static regex for parsing ISO duration strings and returning their individual components via capture groups.
        /// </summary>
        private static readonly Regex _isoDurationParser = new Regex("^P([0-9]{1,4}Y)?([0-9]{1,2}M)?([0-9]{1,3}W)?([0-9]{1,3}D)?(?:T([0-9]{1,4}H)?([0-9]{1,3}M)?([0-9]{1,3}S)?)?$");

        #region Public API methods

#if !CLIENT_ONLY

        /// <summary>
        /// Attempts to parse an ISO-formatted time string into an ExtendedDateTime value. This function only works for basic times, not recurrences or durations or anything
        /// </summary>
        /// <param name="isoString">The time string to be parsed ("2013-06-18T10:00")</param>
        /// <param name="context">The context to apply to the returned ExtendedDateTime value</param>
        /// <returns>The resulting ExtendedDateTime, or null if parsing failed</returns>
        public static ExtendedDateTime TryParseISOIntoExtendedDateTime(string isoString, TimexContext context)
        {
            return TryParseExtendedDateTime(EnumExtensions.ToString(TemporalType.Time), isoString);
        }

        /// <summary>
        /// Given all the constituent values of a TIMEX3 tag, attempt to recreate the ExtendedDateTime that was used to generate this tag.
        /// It is possible for some information to be lost in this parsing (particularly if the value has had inference applied already),
        /// but generally this is a safe way of accessing the exact meaning of the user's input and performing additional logic
        /// or resolution based on it (usually via a Reinterpret() call on the return value)
        /// </summary>
        /// <param name="type">The timex type "Time", "Date", "Set", or "Duration"</param>
        /// <param name="value">The value of the timex, usually ISO form ("2016-09-27T12:00")</param>
        /// <param name="mod">The modifier attribute from the timex tag</param>
        /// <param name="quant">The quantity attribute from the timex tag</param>
        /// <param name="freq">The frequency attribute from the timex tag</param>
        /// <param name="comment">The comment attribute from the timex tag</param>
        /// <param name="interpretationContext">An interpretation context to use for the created value. This does not modify the fields that are parsed, only their interpretation</param>
        /// <returns></returns>
        public static ExtendedDateTime TryParseExtendedDateTime(string type, string value, string mod = "", string quant = "", string freq = "", string comment = "", TimexContext interpretationContext = null)
        {
            if (interpretationContext == null)
            {
                interpretationContext = new TimexContext();
                interpretationContext.UseInference = false;
                interpretationContext.ReferenceDateTime = default(DateTime);
            }

            TemporalType timexType;
            if (string.IsNullOrEmpty(type) || !EnumExtensions.TryParse(type, out timexType))
            {
                timexType = TemporalType.None;
            }

            IDictionary<string, string> newTimexDictionary = null;

            if (timexType == TemporalType.Date || timexType == TemporalType.Time)
            {
                newTimexDictionary = ParseTimexDictionaryFromISODateTime(value);

                if (!string.IsNullOrEmpty(comment) && comment.Contains("ampm"))
                {
                    newTimexDictionary[TimexAttributes.AmPm] = "not_specified";
                }
            }
            else if (timexType == TemporalType.Duration)
            {
                newTimexDictionary = ParseTimexDictionaryFromISODuration(value);
            }
            else if (timexType == TemporalType.Set)
            {
                throw new NotImplementedException("Parsing recurrences is not yet supported");
            }

            if (newTimexDictionary != null && newTimexDictionary.Count > 0)
            {
                ExtendedDateTime returnVal = ExtendedDateTime.Create(timexType, newTimexDictionary, interpretationContext);
                return returnVal;
            }

            return null;
        }

#endif

        /// <summary>
        /// Attempts to parse an ISO-formatted time string into a DateTime object
        /// </summary>
        /// <param name="iso">The string to be parsed ("2013-06-18T10:00")</param>
        /// <param name="result">The place to store the returned date</param>
        /// <returns>The newly created value. If parsing failed, this will be null</returns>
        [SuppressMessage("Microsoft.Naming", "CA1704", MessageId = "iso")]
        [SuppressMessage("Microsoft.Naming", "CA1709", MessageId = "ISO")]
        public static bool TryParseISOIntoLocalDateTime(string iso, out DateTime result)
        {
            // Is it an empty string or a duration? (Starts with capital 'P')
            if (string.IsNullOrWhiteSpace(iso) || iso[0] == 'P')
            {
                // Don't parse
                result = new DateTime();
                return false;
            }

            try
            {
                // Split into date and time components
                IList<string> dateTimeComponents = iso.Split(new char[] { Iso8601.DateTimeDelimiter[0] }, 2);

                string timeComponent = string.Empty;
                string dateComponent = string.Empty;

                // This method requires at least a date component to be present. If there's just a time,
                // or splitting the string failed for some reason, return an error.

                if (dateTimeComponents.Count == 0)
                {
                    result = new DateTime();
                    return false;
                }

                dateComponent = dateTimeComponents.First();

                if (dateTimeComponents.Count == 2)
                {
                    timeComponent = dateTimeComponents.Last();
                }

                result = new DateTime();

                // And parse each extracted component
                TryParseISODatePortion(dateComponent, ref result);

                if (!string.IsNullOrWhiteSpace(timeComponent))
                {
                    TryParseISOTimePortion(timeComponent, ref result);
                }

                return true;
            }
            catch (ArgumentException) { }

            result = new DateTime();
            return false;
        }

        /// <summary>
        /// Attempts to parse an ISO8601 duration string in the form of "P1MT60S" (case-sensitive)
        /// into a corresponding TimeSpan. Note that his method uses the duration values specified
        /// in the TemporalUnit enum, which assumes fixed lengths of months and years (1 month = 30 days,
        /// 1 year = 365 days). Therefore, you would need a more advanced method to accurately handle durations
        /// longer than 1 month in the presence of irregular month lengths / leap years / etc..
        /// Or you can use the parser in the client library
        /// </summary>
        /// <param name="input">The input duration string</param>
        /// <param name="result">The storage for the result</param>
        /// <returns>True if parsing succeeded</returns>
        [SuppressMessage("Microsoft.Naming", "CA1709", MessageId = "ISO")]
        public static bool TryParseISODuration(string input, out TimeSpan result)
        {
            IList<Tuple<long?, TemporalUnit>> durationParts = ParseDurationPiecewise(input);

            long? totalSeconds = ConvertDurationPartsIntoRawSeconds(durationParts);

            if (!totalSeconds.HasValue)
            {
                result = default(TimeSpan);
                return false;
            }

            result = TimeSpan.FromMilliseconds(totalSeconds.Value * 1000);
            return true;
        }

        #endregion

#region EXTENDED DATETIME PARSERS

#if !CLIENT_ONLY

        /// <summary>
        /// Parses an ISO-formatted date/time string and breaks it down into TimexDictionary attributes that can be used
        /// to create a local_date_time or an ExtendedDateTime object. This method does not "fai" in the typical sense,
        /// it can only return an empty map. It is up to the caller to determine if an empty return value is an error or not.
        /// </summary>
        /// <param name="isoString">An ISO time string ("2013-11-02T12:30:00")</param>
        /// <returns>A map containing timex attributes extracted from the string</returns>
        private static IDictionary<string, string> ParseTimexDictionaryFromISODateTime(string isoString)
        {
            // Build a timexdictionary from the parsed values
            IDictionary<string, string> newTimexDictionary = new Dictionary<string, string>();

            // Check for invalid input
            if (isoString.Length < 3)
            {
                return newTimexDictionary;
            }

            string parsedString = isoString;

            // Is it a special reference? (PAST_REF, PRESENT_REF)
            // Capture it and remove it from the string (It is possible for the input to be "PRESENT_REFT04" or something, so we can't discount the entire input yet)
            IList<string> referenceStrings = new List<string>();
            referenceStrings.Add("PAST_REF");
            referenceStrings.Add("PRESENT_REF");
            referenceStrings.Add("FUTURE_REF");

            foreach (string iter in referenceStrings)
            {
                int refIndex = parsedString.IndexOf(iter, StringComparison.Ordinal);
                if (refIndex >= 0)
                {
                    newTimexDictionary[Iso8601.Reference] = iter;
                    // Splice it out
                    string newString = parsedString.Substring(0, refIndex);
                    newString += parsedString.Substring(refIndex + iter.Length);
                    parsedString = newString;
                }
            }

            // Split into date and time components
            bool containsTime = (parsedString.Length >= 1 && parsedString[0] == 'T') ||
                (parsedString.Length >= 13 && parsedString[10] == 'T'); // FIXME handle the "Daytime" case? 2016-09-27TDT
            IList<string> dateTimeComponents = parsedString.Split(new char[] { Iso8601.DateTimeDelimiter[0] }, 2);

            string timeComponent = string.Empty;
            string dateComponent = string.Empty;

            if (dateTimeComponents.Count == 1)
            {
                if (containsTime)
                {
                    timeComponent = dateTimeComponents.First();
                }
                else
                {
                    dateComponent = dateTimeComponents.First();
                }
            }
            else if (dateTimeComponents.Count == 2)
            {
                dateComponent = dateTimeComponents.First();
                timeComponent = dateTimeComponents.Last();
            }

            // And parse each extracted component
            if (!string.IsNullOrWhiteSpace(dateComponent))
            {
                TryParseISODatePortion(dateComponent, newTimexDictionary, TemporalType.Date);
            }

            if (!string.IsNullOrWhiteSpace(timeComponent))
            {
                TryParseISOTimePortion(timeComponent, newTimexDictionary);
            }

            return newTimexDictionary;
        }

        /// <summary>
        /// Parses an ISO-formatted duration string ("PT4H30M") and breaks it down into TimexDictionary attributes that can be used
        /// to create a local_date_time or an ExtendedDateTime object. This method does not "fai" in the typical sense,
        /// it can only return an empty map. It is up to the caller to determine if an empty return value is an error or not.
        /// </summary>
        /// <param name="isoString">An ISO time string ("2013-11-02T12:30:00")</param>
        /// <returns>A map containing timex attributes extracted from the string</returns>
        private static IDictionary<string, string> ParseTimexDictionaryFromISODuration(string isoString)
        {
            // Build a timexdictionary from the parsed values
            IDictionary<string, string> newTimexDictionary = new Dictionary<string, string>();

            // Check for invalid input
            if (isoString.Length < 3)
            {
                return newTimexDictionary;
            }

            // Is this not duration? (Starts with capital 'P')
            if (string.IsNullOrWhiteSpace(isoString) || isoString.Length < 3 || isoString[0] != 'P')
            {
                // Don't parse 
                return newTimexDictionary;
            }

            // Extract temporal components
            IList<Tuple<long?, TemporalUnit>> durationParts = ParseDurationPiecewise(isoString);

            // Test if it is basic or compound
            if (durationParts.Count == 1)
            {
                // Basic. Just pass the unit and value
                long? value = durationParts[0].Item1;
                TemporalUnit unit = durationParts[0].Item2;
                newTimexDictionary[TimexAttributes.Duration] = value.HasValue ? value.ToString() : "X";
                newTimexDictionary[TimexAttributes.DurationUnit] = unit.ToString();
            }
            else if (durationParts.Count >= 2)
            {
                // Compound. We can't represent this with just one unit, so convert it into seconds and use the RawDuration value
                long? rawDurationSeconds = ConvertDurationPartsIntoRawSeconds(durationParts);
                if (rawDurationSeconds.HasValue)
                {
                    newTimexDictionary[TimexAttributes.RawDuration] = rawDurationSeconds.Value.ToString();
                }
            }

            return newTimexDictionary;
        }
#endif

#endregion

#region ISO DATE functions

#if !CLIENT_ONLY
        /// <summary>
        /// Parses an ISO date string (in the form of "2013-11-01" or similar) and places the extracted
        /// values into the given timexDictionary. This method does not throw exceptions; failure is defined as "not
        /// placing the right values into the dictionary".
        /// </summary>
        /// <param name="parsedDateString">The date string extracted from ISO</param>
        /// <param name="timexDictionary">The dictionary to place the results into</param>
        /// <param name="timexType">The type of timex that is being parsed</param>
        private static void TryParseISODatePortion(string parsedDateString, IDictionary<string, string> timexDictionary, TemporalType timexType)
        {
            IList<string> dateTimeComponents = parsedDateString.Split(new char[] { Iso8601.DateDelimiter[0] }, 4);

            if (dateTimeComponents.Count >= 1 &&
                dateTimeComponents[0].Length == 4)
            {
                timexDictionary[Iso8601.Year] = dateTimeComponents[0]; // Capture a year value
            }
            if (dateTimeComponents.Count >= 2)
            {
                // If it's 2 chars long then it's either a month "12", a season "SU", or empty "XX"
                if (dateTimeComponents[1].Length == 2)
                {
                    Season testSeason;
                    if (EnumExtensions.TryParse(dateTimeComponents[1], out testSeason))
                    {
                        timexDictionary[Iso8601.Season] = dateTimeComponents[1];
                    }
                    else
                    {
                        timexDictionary[Iso8601.Month] = dateTimeComponents[1];
                    }
                }
                else if (dateTimeComponents[1].Length >= 2)
                {
                    // trim the first character from the string.
                    char firstLetter = dateTimeComponents[1][0];
                    string substring = dateTimeComponents[1].Substring(1);
                    // Is it a week reference?
                    if (dateTimeComponents[1].Length == 3 &&
                        firstLetter == 'W')
                    {
                        timexDictionary[Iso8601.Week] = substring;
                    }
                    // Is it a quarter reference?
                    else if (dateTimeComponents[1].Length > 1 &&
                        (firstLetter == 'Q' || firstLetter == 'H') &&
                        IsIntegerString(substring))
                    {
                        timexDictionary[Iso8601.PartOfYear] = dateTimeComponents[1];
                    }
                    // Is it a season reference?
                    else if (!IsIntegerString(substring))
                    {
                        timexDictionary[Iso8601.Season] = dateTimeComponents[1];
                    }
                }
            }
            if (dateTimeComponents.Count >= 3)
            {
                // Is this a weekend reference?
                if (dateTimeComponents[2] == "WE")
                {
                    timexDictionary[TimexAttributes.Offset] = "0";
                    timexDictionary[TimexAttributes.OffsetUnit] = "weekend";
                }
                else
                {
                    // Is it a week-of-month reference?
                    if (dateTimeComponents[2].Length == 3 &&
                        dateTimeComponents[2].StartsWith("W"))
                    {
                        timexDictionary[Iso8601.Week] = dateTimeComponents[2].TrimStart('W');
                    }
                    else if (dateTimeComponents[2].Length == 2) //It's a regular day reference ("the 21st" = "21")
                    {
                        timexDictionary[Iso8601.Day] = dateTimeComponents[2];
                    }
                    else if (dateTimeComponents[2].Length == 1) // It's a day of week reference ("monday" = "1", etc)
                    {
                        timexDictionary[Iso8601.WeekDay] = dateTimeComponents[2];
                    }
                }
            }
            if (dateTimeComponents.Count >= 4)
            {
                // Only occurs in "Nth weekday of month" format, 2012-11-W04-4
                timexDictionary[Iso8601.WeekDay] = dateTimeComponents[3];
            }

            // Look at the dictionary we just made and reinterpret it given the temporal type we're parsing to
            // (in other words, discard XXXX fields for dates and times, but derive frequency from them for sets)
            if (timexType == TemporalType.Date || timexType == TemporalType.Time)
            {
                // TODO Special handling for "19XX" decade, century, weird rare cases
                IList<string> fieldsToRemoveIfX = new List<string>(new string[] { Iso8601.Month, Iso8601.Day, Iso8601.WeekDay, Iso8601.Week, Iso8601.PartOfYear, Iso8601.Season, Iso8601.Year });

                foreach (string fieldToInspect in fieldsToRemoveIfX)
                {
                    if (timexDictionary.ContainsKey(fieldToInspect) && IsAllX(timexDictionary[fieldToInspect]))
                    {
                        timexDictionary.Remove(fieldToInspect);
                    }
                }
            }

            // Convert "week-in-month" offset references (almost always used for holidays like Thanksgiving) into a format that timex can parse using offset anchors
            if (timexDictionary.ContainsKey(Iso8601.Week) && timexDictionary.ContainsKey(Iso8601.Month) && timexDictionary.ContainsKey(Iso8601.WeekDay))
            {
                int dayOfWeekNum;
                if (int.TryParse(timexDictionary[Iso8601.WeekDay], out dayOfWeekNum) && dayOfWeekNum >= 1 && dayOfWeekNum <= 7)
                {
                    string offsetAnchor = timexDictionary[Iso8601.Month] + "-01"; // we assume it is anchored to the 1st day of the month if we are indexing "the nth week of the month"
                    string offsetUnit = EnumExtensions.ToString(TemporalUnitExtensions.ParseDayOfWeekNum(dayOfWeekNum));
                    string offset = timexDictionary[Iso8601.Week];
                    timexDictionary.Remove(Iso8601.Week);
                    timexDictionary.Remove(Iso8601.WeekDay);
                    timexDictionary.Remove(Iso8601.Month);
                    timexDictionary.Add(TimexAttributes.OffsetAnchor, offsetAnchor);
                    timexDictionary.Add(TimexAttributes.OffsetUnit, offsetUnit);
                    timexDictionary.Add(TimexAttributes.Offset, offset);
                }
            }
        }
#endif

        /// <summary>
        /// Parses an ISO date string (in the form of "2013-11-01" or similar) and sets the corresponding
        /// fields in the passed-in local_date_time. This method fails by throwing an new ArgumentException exception.
        /// </summary>
        /// <param name="parsedDateString">The date string extracted from ISO (i.e. "2012-01-01")</param>
        /// <param name="time">(out) The time object to be modified by this function</param>
        private static void TryParseISODatePortion(string parsedDateString, ref DateTime time)
        {
            int temp = 0;
            bool weekFormat = false;

            IList<string> dateTimeComponents = parsedDateString.Split(new char[] { Iso8601.DateDelimiter[0] }, 4);

            if (dateTimeComponents.Count >= 1)
            {
                if (dateTimeComponents[0].Length == 4 &&
                    int.TryParse(dateTimeComponents[0], out temp) &&
                    temp > 1400 && temp < 3000)
                {
                    time = time.AddYears(temp - time.Year); // Capture a year value
                }
                else
                {
                    throw new TimexException("year is invalid or out of range");
                }
            }
            if (dateTimeComponents.Count >= 2)
            {
                // Is it a regular numerical month?
                if (dateTimeComponents[1].Length == 2)
                {
                    if (int.TryParse(dateTimeComponents[1], out temp) &&
                        temp < 13 && temp > 0)
                    {
                        time = time.AddMonths(temp - time.Month);
                    }
                    else
                    {
                        throw new TimexException("month is invalid or out of range");
                    }
                }
                else if (dateTimeComponents[1].Length >= 2) // It's a week
                {
                    // trim the first character from the string.
                    string substring = dateTimeComponents[1].Substring(1);
                    // Is it a week reference?
                    if (dateTimeComponents[1].Length == 3 &&
                        dateTimeComponents[1][0] == 'W' &&
                        int.TryParse(substring, out temp) &&
                        temp < 55 && temp > 0)
                    {
                        // Adding one week will ensure that the week_number is reported to be the correct value
                        // (in some cases, like Jan 1st, it could be mistakenly treated as Week 53 of the previous year)
                        // This is safe because the day of month is set to be 1, so we can't accidentally cross over into the next year.
                        time = time.AddDays(7);
                        time = time.AddDays(7 * (temp - Durandal.Common.Time.Timex.Calendar.GregorianCalendar.GetISOWeekOfYear(time)));
                        weekFormat = true;
                    }
                    else
                    {
                        throw new TimexException("week is invalid or out of range");
                    }
                }
                else
                {
                    throw new TimexException("month is invalid or out of range");
                }
            }
            if (dateTimeComponents.Count >= 3)
            {
                if (int.TryParse(dateTimeComponents[2], out temp) &&
                    temp < 32 && temp > 0)
                {
                    if (dateTimeComponents[2].Length == 2) //It's a regular day reference ("the 21st" = "21")
                    {
                        time = time.AddDays(temp - time.Day);
                    }
                    else if (dateTimeComponents[2].Length == 1 && weekFormat) // It's a day of week reference ("monday" = "1", etc)
                    {
                        // Convert boost day_of_week to ISO
                        int dayOfWeek = (int)time.DayOfWeek == 0 ? 7 : (int)time.DayOfWeek;
                        dayOfWeek = dayOfWeek == 0 ? 7 : dayOfWeek;
                        time = time.AddDays(temp - dayOfWeek);
                    }
                    else
                    {
                        throw new TimexException("day is invalid of out of range");
                    }
                }
                else
                {
                    throw new TimexException("day is invalid or out of range");
                }
            }
        }

        #endregion

#region ISO TIME functions

#if !CLIENT_ONLY
        /// <summary>
        /// Parses an ISO time string (in the form of "T24:00:00" or similar) and places the extracted
        /// values into the given timexDictionary. This method does not throw exceptions; failure is defined as "not
        /// placing the right values into the dictionary".
        /// </summary>
        /// <param name="parsedTimeString">The time string extracted from ISO</param>
        /// <param name="timexDictionary">The dictionary to place the results into</param>
        private static void TryParseISOTimePortion(string parsedTimeString, IDictionary<string, string> timexDictionary)
        {
            IList<string> dateTimeComponents = parsedTimeString.Split(new char[] { Iso8601.TimeDelimiter[0] }, 4);

            // Inspect for timezone strings and extract one if found
            if (dateTimeComponents.Last().Length > 2)
            {
                // The last component is something like "00+0800"
                // Capture the last part of that
                timexDictionary[Iso8601.TimeZone] = dateTimeComponents.Last().Substring(2);
                // And preserve the intended value of the seconds field
                string secondsValue = dateTimeComponents.Last().Substring(0, 2);
                dateTimeComponents.RemoveAt(dateTimeComponents.Count - 1);
                dateTimeComponents.Add(secondsValue);
            }

            if (dateTimeComponents.Count >= 1 && dateTimeComponents[0].Length == 2)
            {
                // Is it a numerical hour?
                if (IsIntegerString(dateTimeComponents[0]))
                {
                    timexDictionary[Iso8601.Hour] = dateTimeComponents[0];
                }
                else // Assume that it's a part of day reference
                {
                    timexDictionary[Iso8601.PartOfDay] = dateTimeComponents[0];
                }
            }
            if (dateTimeComponents.Count >= 2)
            {
                if (dateTimeComponents[1].Length <= 2 &&
                    IsIntegerString(dateTimeComponents[1]))
                {
                    timexDictionary[Iso8601.Minute] = dateTimeComponents[1];
                }
            }
            if (dateTimeComponents.Count >= 3)
            {
                if (dateTimeComponents[2].Length <= 2 &&
                    IsIntegerString(dateTimeComponents[2]))
                {
                    timexDictionary[Iso8601.Second] = dateTimeComponents[2];
                }
            }
        }
#endif

        /// <summary>
        /// Parses an ISO time string (in the form of "T24:00:00" or similar) and sets the corresponding
        /// fields in the passed-in local_date_time. This method fails by throwing an new ArgumentException exception.
        /// </summary>
        /// <param name="parsedTimeString">The time string extracted from ISO</param>
        /// <param name="time">(out) The time object to be modified by this function</param>
        private static void TryParseISOTimePortion(string parsedTimeString, ref DateTime time)
        {
            int temp = 0;

            IList<string> dateTimeComponents = parsedTimeString.Split(new char[] { Iso8601.TimeDelimiter[0] }, 4);

            // Inspect for timezone strings and trim it if found
            if (dateTimeComponents.Last().Length > 4)
            {
                // The last component is something like "00+0800"
                // Trim out everything but the first 2 chars
                string secondsValue = dateTimeComponents.Last().Substring(0, 2);
                dateTimeComponents.RemoveAt(dateTimeComponents.Count - 1);
                dateTimeComponents.Add(secondsValue);
            }

            if (dateTimeComponents.Count > 3)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Encountered more time components in parsed time string {0} than expected.", parsedTimeString), nameof(parsedTimeString));
            }
            if (dateTimeComponents.Count >= 1)
            {
                if (dateTimeComponents[0].Length == 2 &&
                    int.TryParse(dateTimeComponents[0], out temp) &&
                    temp < 24 && temp >= 0)
                {
                    time = time.AddHours(temp - time.Hour);
                }
                else
                {
                    PartOfDay podCheck;
                    // If the 'hour" field is a part of day string (like "AF"), just ignore it.
                    if (!EnumExtensions.TryParse(dateTimeComponents[0], out podCheck))
                    {
                        throw new ArgumentException("hour");
                    }
                }
            }
            if (dateTimeComponents.Count >= 2)
            {
                if (dateTimeComponents[1].Length <= 2 &&
                    int.TryParse(dateTimeComponents[1], out temp) &&
                    temp < 60 && temp >= 0)
                {
                    time = time.AddMinutes(temp - time.Minute);
                }
                else
                {
                    throw new ArgumentException("min");
                }
            }
            if (dateTimeComponents.Count >= 3)
            {
                if (dateTimeComponents[2].Length <= 2 &&
                    int.TryParse(dateTimeComponents[2], out temp) &&
                    temp < 60 && temp >= 0)
                {
                    time = time.AddSeconds(temp - time.Second);
                }
                else
                {
                    throw new ArgumentException("sec");
                }
            }
        }

#endregion

#region ISO DURATION functions

        /// <summary>
        /// Parses an ISO duration, and attempts to extract the fields for each temporal unit with their associated quantities.
        /// Sometimes the quantity is non-numeric (the string is "X"), in which case the quantity is null.
        /// Example: P1D returns [(1, Day)]. PT2H30M returns [(2, Hour), (30, Minute)], etc.
        /// </summary>
        /// <param name="input">The ISO input string</param>
        /// <returns>A non-null list of temporal parts to this duration</returns>
        private static IList<Tuple<long?, TemporalUnit>> ParseDurationPiecewise(string input)
        {
            List<Tuple<long?, TemporalUnit>> parts = new List<Tuple<long?, TemporalUnit>>();
            if (input.Length < 3 || input.EndsWith("T", StringComparison.Ordinal))
            {
                return parts;
            }

            Match durationMatch = _isoDurationParser.Match(input);
            if (!durationMatch.Success)
            {
                return parts;
            }

            for (int groupIndex = 1; groupIndex < durationMatch.Groups.Count; groupIndex++)
            {
                Group captureGroup = durationMatch.Groups[groupIndex];
                if (captureGroup.Success && captureGroup.Length >= 2)
                {
                    string quantity = captureGroup.Value.Substring(0, captureGroup.Length - 1);
                    TemporalUnit unit;
                    switch (groupIndex)
                    {
                        default:
                            return parts;
                        case 1:
                            unit = TemporalUnit.Year;
                            break;
                        case 2:
                            unit = TemporalUnit.Month;
                            break;
                        case 3:
                            unit = TemporalUnit.Week;
                            break;
                        case 4:
                            unit = TemporalUnit.Day;
                            break;
                        case 5:
                            unit = TemporalUnit.Hour;
                            break;
                        case 6:
                            unit = TemporalUnit.Minute;
                            break;
                        case 7:
                            unit = TemporalUnit.Second;
                            break;
                    }

                    long testParse;
                    if (long.TryParse(quantity, out testParse))
                    {
                        parts.Add(new Tuple<long?, TemporalUnit>(testParse, unit));
                    }
                    else
                    {
                        parts.Add(new Tuple<long?, TemporalUnit>(null, unit));
                    }
                }
            }

            return parts;
        }

        /// <summary>
        /// Given a set of duration pieces, attempt to convert them into a single value representing seconds.
        /// If any of the pieces are invalid (i.e. "X" or null) this return null
        /// </summary>
        /// <param name="durationParts">The piecewise parsed duration</param>
        /// <returns>The total length in seconds, or null</returns>
        private static long? ConvertDurationPartsIntoRawSeconds(IList<Tuple<long?, TemporalUnit>> durationParts)
        {
            long totalSeconds = 0;
            foreach (var tuple in durationParts)
            {
                if (!tuple.Item1.HasValue)
                {
                    // Could not parse the number that preceeded the unit, so this fragment must not be in the form of '###D'
                    return null;
                }

                totalSeconds += tuple.Item2.ToDuration() * tuple.Item1.Value;
            }

            return totalSeconds;
        }

        #endregion

#region Helpers

#if !CLIENT_ONLY
        /// <summary>
        /// Tests to see if the given input string is an integer value.
        /// </summary>
        private static bool IsIntegerString(string input)
        {
            int dummyValue = 0;
            return int.TryParse(input, out dummyValue);
        }

        /// <summary>
        /// Tests to see if a string is made entirely of the X character
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static bool IsAllX(string input)
        {
            foreach (char x in input)
            {
                if (x != 'X')
                    return false;
            }

            return true;
        }
#endif

#endregion
    }
}
