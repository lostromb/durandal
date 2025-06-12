namespace Durandal.Common.Time.Timex.Client
{
    using Calendar;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.Common.Time.Timex.Enums;

    /// <summary>
    /// Provides common internal date/time related utilities like parsing/conversion functions
    /// </summary>
    internal static class DateTimeParserHelpers
    {
        /// <summary>
        /// Tests to see if the given input string is an integer value.
        /// </summary>
        internal static bool IsIntegerString(string input)
        {
            int dummyValue;
            return int.TryParse(input, out dummyValue);
        }

        /// <summary>
        /// Attempts to parse an integer string. If parsing failed, return null
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        internal static string TryParseIntOrNull(string val)
        {
            int scratch;
            if (int.TryParse(val, out scratch))
            {
                return val;
            }

            return null;
        }

        /// <summary>
        /// Parses a modifier value such as "APPROX" into its corresponding enum value.
        /// </summary>
        /// <param name="stringVal"></param>
        /// <returns></returns>
        internal static Modifier ParseModifier(string stringVal)
        {
            if (string.Equals(stringVal, "APPROX", StringComparison.OrdinalIgnoreCase))
            {
                return Modifier.Approximately;
            }
            else if (string.Equals(stringVal, "EQUAL_OR_LESS", StringComparison.OrdinalIgnoreCase))
            {
                return Modifier.EqualOrLess;
            }
            else if (string.Equals(stringVal, "EQUAL_OR_MORE", StringComparison.OrdinalIgnoreCase))
            {
                return Modifier.EqualOrMore;
            }
            else if (string.Equals(stringVal, "LESS_THAN", StringComparison.OrdinalIgnoreCase))
            {
                return Modifier.LessThan;
            }
            else if (string.Equals(stringVal, "MORE_THAN", StringComparison.OrdinalIgnoreCase))
            {
                return Modifier.MoreThan;
            }
            else if (string.Equals(stringVal, "MID", StringComparison.OrdinalIgnoreCase))
            {
                return Modifier.Mid;
            }
            else if (string.Equals(stringVal, "END", StringComparison.OrdinalIgnoreCase))
            {
                return Modifier.End;
            }
            else if (string.Equals(stringVal, "START", StringComparison.OrdinalIgnoreCase))
            {
                return Modifier.Start;
            }
            else if (string.Equals(stringVal, "BEFORE", StringComparison.OrdinalIgnoreCase))
            {
                return Modifier.Before;
            }
            else if (string.Equals(stringVal, "AFTER", StringComparison.OrdinalIgnoreCase))
            {
                return Modifier.After;
            }
            return Modifier.None;
        }

        /// <summary>
        /// Parses a season value such as "SP" into its corresponding enum value.
        /// </summary>
        /// <param name="stringVal"></param>
        /// <returns></returns>
        internal static Season ParseSeason(string stringVal)
        {
            if (string.Equals(stringVal, "WI", StringComparison.OrdinalIgnoreCase))
            {
                return Season.Winter;
            }
            else if (string.Equals(stringVal, "SU", StringComparison.OrdinalIgnoreCase))
            {
                return Season.Summer;
            }
            else if (string.Equals(stringVal, "SP", StringComparison.OrdinalIgnoreCase))
            {
                return Season.Spring;
            }
            else if (string.Equals(stringVal, "FA", StringComparison.OrdinalIgnoreCase))
            {
                return Season.Fall;
            }

            return Season.None;
        }

        /// <summary>
        /// Parses a partofweek value such as "WE" into its corresponding enum value.
        /// </summary>
        /// <param name="stringVal"></param>
        /// <returns></returns>
        internal static PartOfWeek ParsePartOfWeek(string stringVal)
        {
            if (string.Equals(stringVal, "WE", StringComparison.OrdinalIgnoreCase))
            {
                return PartOfWeek.Weekend;
            }
            else if (string.Equals(stringVal, "WD", StringComparison.OrdinalIgnoreCase))
            {
                return PartOfWeek.Weekdays;
            }

            return PartOfWeek.None;
        }

        /// <summary>
        /// Parses a quantity, such as "EACH" or "5" into its equivalent integer value.
        /// </summary>
        /// <param name="stringVal"></param>
        /// <returns></returns>
        internal static int ParseQuantity(string stringVal)
        {
            if (string.Equals(stringVal, "EACH", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(stringVal, "EVERY", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            int scratch;
            if (int.TryParse(stringVal, out scratch))
            {
                return scratch;
            }

            return 1;
        }

        /// <summary>
        /// Parses a string in the form of "4week" into a pair of unit + amount. This is used
        /// for parsing the "frequency" field of certain recurrences.
        /// </summary>
        /// <param name="freq"></param>
        /// <returns>The parsed values, or null if parsing failed.</returns>
        internal static Tuple<TemporalUnit, int> ParseFrequencyPair(string freq)
        {
            if (string.IsNullOrEmpty(freq))
            {
                return null;
            }

            int separatorIndex = 0;
            for (; separatorIndex < freq.Length && char.IsDigit(freq[separatorIndex]); separatorIndex++) { }

            if (separatorIndex >= freq.Length)
            {
                return null;
            }

            string numberPart = freq.Substring(0, separatorIndex);
            string unitPart = freq.Substring(separatorIndex);
            int number = int.Parse(numberPart, CultureInfo.InvariantCulture);
            TemporalUnit unit = TemporalUnit.Second;

            if (unitPart.Equals("second", StringComparison.OrdinalIgnoreCase))
            {
                unit = TemporalUnit.Second;
            }
            else if (unitPart.Equals("minute", StringComparison.OrdinalIgnoreCase))
            {
                unit = TemporalUnit.Minute;
            }
            else if (unitPart.Equals("hour", StringComparison.OrdinalIgnoreCase))
            {
                unit = TemporalUnit.Hour;
            }
            else if (unitPart.Equals("day", StringComparison.OrdinalIgnoreCase))
            {
                unit = TemporalUnit.Day;
            }
            else if (unitPart.Equals("week", StringComparison.OrdinalIgnoreCase))
            {
                unit = TemporalUnit.Week;
            }
            else if (unitPart.Equals("month", StringComparison.OrdinalIgnoreCase))
            {
                unit = TemporalUnit.Month;
            }
            else if (unitPart.Equals("year", StringComparison.OrdinalIgnoreCase))
            {
                unit = TemporalUnit.Year;
            }

            return new Tuple<TemporalUnit, int>(unit, number);
        }

        /// <summary>
        /// Parses an ISO duration value (in the form of "P3D" or "PT5H30M") into a set of values
        /// </summary>
        /// <param name="isoDurationString">The ISO duration value</param>
        /// <returns>A dictionary representing all of the duration units / values in the parsed string</returns>
        internal static IDictionary<TemporalUnit, int?> ParseIsoDuration(string isoDurationString)
        {
            IDictionary<TemporalUnit, int?> returnVal = new Dictionary<TemporalUnit, int?>();

            if (isoDurationString.Length < 3 || isoDurationString.EndsWith("T", StringComparison.Ordinal))
            {
                return returnVal;
            }

            // The old method used this regex for parsing. However, because of fxcop we basically can't use regexes anywhere
            //("^P([0-9X]{1,4}Y)?([0-9X]{1,2}M)?([0-9X]{1,3}W)?([0-9X]{1,3}D)?(?:T([0-9X]{1,4}H)?([0-9X]{1,3}M)?([0-9X]{1,3}S)?)?$");

            int idx = 0;
            if (isoDurationString[idx++] != 'P')
            {
                return returnVal;
            }
        
            bool parsingTimePairs = false;
            int curField = 0;
            while (idx < isoDurationString.Length)
            {
                // Did we hit a 'T'?
                if (isoDurationString[idx] == 'T')
                {
                    // transition into time fields, or error out
                    if (parsingTimePairs)
                    {
                        return returnVal;
                    }

                    parsingTimePairs = true;
                    idx++;
                }

                // Parse a single pair of properties (i.e. "30S")
                // this helper function automatically increments index and current field index
                int? value;
                TemporalUnit unit;
                if (!ParseDurationPair(isoDurationString, parsingTimePairs, ref idx, out value, out unit, ref curField))
                {
                    // Parsing failed.
                    return returnVal;
                }
                else
                {
                    returnVal.Add(unit, value);
                }
            }

            return returnVal;
        }

        private static bool ParseDurationPair(string input, bool timesAllowed, ref int index, out int? number, out TemporalUnit temporalUnit, ref int curField)
        {
            number = null;
            temporalUnit = TemporalUnit.Year;
            
            // Parse a numerical followed by a char value
            int originalStart = index;
            while (index < input.Length && (char.IsDigit(input[index]) || input[index] == 'X'))
            {
                index++;
            }
            
            if (originalStart == index)
            {
                // no digits found
                return false;
            }

            if (index == input.Length)
            {
                // no char afterwards
                return false;
            }

            string numericValue = input.Substring(originalStart, index - originalStart);
            int realValue;
            if (int.TryParse(numericValue, out realValue))
            {
                number = realValue;
            }

            char tempUnitChar = input[index++];
            // Resolve the temporal unit
            if (timesAllowed)
            {
                switch (tempUnitChar)
                {
                    case 'H':
                        temporalUnit = TemporalUnit.Hour;
                        // enforces order of fields
                        // e.g. can't have a minute before an hour field
                        if (curField >= 5)
                            return false;
                        curField = 5;
                        break;
                    case 'M':
                        temporalUnit = TemporalUnit.Minute;
                        if (curField >= 6)
                            return false;
                        curField = 6;
                        break;
                    case 'S':
                        temporalUnit = TemporalUnit.Second;
                        if (curField >= 7)
                            return false;
                        curField = 7;
                        break;
                    default:
                        return false;
                }
            }
            else
            {
                switch (tempUnitChar)
                {
                    case 'Y':
                        temporalUnit = TemporalUnit.Year;
                        if (curField >= 1)
                            return false;
                        curField = 1;
                        break;
                    case 'M':
                        temporalUnit = TemporalUnit.Month;
                        if (curField >= 2)
                            return false;
                        curField = 2;
                        break;
                    case 'W':
                        temporalUnit = TemporalUnit.Week;
                        if (curField >= 3)
                            return false;
                        curField = 3;
                        break;
                    case 'D':
                        temporalUnit = TemporalUnit.Day;
                        if (curField >= 4)
                            return false;
                        curField = 4;
                        break;
                    default:
                        return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Parses a string in the form "AF", "MO", etc. into its corresponding ISO part of day.
        /// </summary>
        /// <param name="podValue">The value to parse</param>
        /// <returns>The parsed enum value, or PartOfDay.None if parsing failed.</returns>
        internal static PartOfDay ParsePartOfDay(string podValue)
        {
            if (string.IsNullOrEmpty(podValue))
            {
                return PartOfDay.None;
            }

            if (string.Equals(podValue, "MO"))
            {
                return PartOfDay.Morning;
            }
            else if (string.Equals(podValue, "MI"))
            {
                return PartOfDay.MidDay;
            }
            else if (string.Equals(podValue, "AF"))
            {
                return PartOfDay.Afternoon;
            }
            else if (string.Equals(podValue, "EV"))
            {
                return PartOfDay.Evening;
            }
            else if (string.Equals(podValue, "NI"))
            {
                return PartOfDay.Night;
            }
            else if (string.Equals(podValue, "PM"))
            {
                return PartOfDay.Pm;
            }
            else if (string.Equals(podValue, "DT"))
            {
                return PartOfDay.DayTime;
            }

            return PartOfDay.None;
        }
    }
}
