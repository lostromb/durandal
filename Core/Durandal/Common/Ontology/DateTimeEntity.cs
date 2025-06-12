using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Ontology
{
    /// <summary>
    /// Represents a date or time value with varying levels of specificity, according to ISO standard.
    /// </summary>
    public class DateTimeEntity
    {
        private static readonly char[] DATE_SEPARATOR = new char[] { '-' };

        /// <summary>
        /// The year value of a date
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// The month value of a date
        /// </summary>
        public int? Month { get; set; }

        /// <summary>
        /// The day of month value of a date
        /// </summary>
        public int? DayOfMonth { get; set; }

        /// <summary>
        /// The hour value of a time
        /// </summary>
        public int? Hour { get; set; }

        /// <summary>
        /// The minute value of a time
        /// </summary>
        public int? Minute { get; set; }
        
        /// <summary>
        /// The second value of a time
        /// </summary>
        public int? Second { get; set; }

        /// <summary>
        /// The ISO week of year value of a date
        /// </summary>
        public int? Week { get; set; }

        /// <summary>
        /// The ISO day of week value of a date. Monday == 0
        /// </summary>
        public int? DayOfWeek { get; set; }

        //public string PartOfDay { get; set; }
        //public string PartOfWeek { get; set; }
        //public string PartOfYear { get; set; }
        //public string Season { get; set; }

        /// <summary>
        /// The timezone as a string, in the form of "+0800", "-0400", or "Z"
        /// </summary>
        public string TimeZone { get; set; }
        
        /// <summary>
        /// Implementation of Timex.Equals
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            DateTimeEntity other = obj as DateTimeEntity;
            return Year == other.Year &&
                Month == other.Month &&
                DayOfMonth == other.DayOfMonth &&
                Hour == other.Hour &&
                Minute == other.Minute &&
                Second == other.Second &&
                Week == other.Week &&
                DayOfWeek == other.DayOfWeek &&
                string.Equals(TimeZone, other.TimeZone);
        }
        
        public override int GetHashCode()
        {
            uint returnVal = 0;
            if (Year.HasValue) returnVal |= ((uint)Year.Value * 0x37485AE1 ^ 0x834B5120);
            if (Month.HasValue) returnVal |= ((uint)Month.Value * 0x9DE54783 ^ 0x12895F30);
            if (DayOfMonth.HasValue) returnVal |= ((uint)DayOfMonth.Value * 0x6866E932 ^ 0x934C1230);
            if (Hour.HasValue) returnVal |= ((uint)Hour.Value * 0x0E231231 ^ 0x57562E52);
            if (Minute.HasValue) returnVal |= ((uint)Minute.Value * 0x93276402 ^ 0x058A5F00);
            if (Second.HasValue) returnVal |= ((uint)Second.Value * 0x57A12489 ^ 0x45590F75);
            if (Week.HasValue) returnVal |= ((uint)Week.Value * 0xA6893213 ^ 0x545B9787);
            if (DayOfWeek.HasValue) returnVal |= ((uint)DayOfWeek.Value * 0xD6254803 ^ 0x8D453451);
            if (TimeZone != null) returnVal |= (uint)TimeZone.GetHashCode();
            return (int)returnVal;
        }

        /// <summary>
        /// Returns this value formatted using ISO8601 standard
        /// </summary>
        /// <returns></returns>
        public string ToIso8601()
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder returnVal = pooledSb.Builder;
                // Print date portion
                if (Year.HasValue || Month.HasValue || DayOfMonth.HasValue || Week.HasValue || DayOfWeek.HasValue)
                {
                    if (Week.HasValue || DayOfWeek.HasValue)
                    {
                        // Print date in week-of-year form
                        if (Year.HasValue)
                        {
                            returnVal.AppendFormat("{0:D4}", Year.Value);
                        }
                        else
                        {
                            returnVal.Append("XXXX");
                        }
                        if (Week.HasValue)
                        {
                            returnVal.AppendFormat("-W{0:D2}", Week.Value);
                        }
                        else
                        {
                            returnVal.Append("-WXX");
                        }
                        if (DayOfWeek.HasValue)
                        {
                            returnVal.AppendFormat("-{0:D1}", DayOfWeek.Value);
                        }
                    }
                    else
                    {
                        // Print date in regular calendar form
                        if (Year.HasValue)
                        {
                            returnVal.AppendFormat("{0:D4}", Year.Value);
                        }
                        else
                        {
                            returnVal.Append("XXXX");
                        }
                        if (Month.HasValue)
                        {
                            returnVal.AppendFormat("-{0:D2}", Month.Value);
                        }
                        else
                        {
                            returnVal.Append("-XX");
                        }
                        if (DayOfMonth.HasValue)
                        {
                            returnVal.AppendFormat("-{0:D2}", DayOfMonth.Value);
                        }
                        else
                        {
                            returnVal.Append("-XX");
                        }
                    }
                }

                // Print time portion
                if (Hour.HasValue || Minute.HasValue || Second.HasValue)
                {
                    returnVal.Append("T");
                    if (Hour.HasValue)
                    {
                        returnVal.AppendFormat("{0:D2}", Hour.Value);
                    }
                    else
                    {
                        returnVal.Append("XX");
                    }
                    if (Minute.HasValue)
                    {
                        returnVal.AppendFormat(":{0:D2}", Minute.Value);
                    }
                    else
                    {
                        returnVal.Append(":XX");
                    }
                    if (Second.HasValue)
                    {
                        returnVal.AppendFormat(":{0:D2}", Second.Value);
                    }
                    else
                    {
                        returnVal.Append(":XX");
                    }
                    if (!string.IsNullOrEmpty(TimeZone))
                    {
                        returnVal.Append(TimeZone);
                    }
                }

                return returnVal.ToString();
            }
        }

        /// <summary>
        /// Parses a timex from an ISO8601 string
        /// </summary>
        /// <param name="iso"></param>
        /// <returns></returns>
        public static DateTimeEntity FromIso8601(string iso)
        {
            DateTimeEntity returnVal = new DateTimeEntity();
            int timeSeparatorIndex = iso.IndexOf('T');
            if (timeSeparatorIndex >= 0)
            {
                returnVal.ParseDateValue(iso.Substring(0, timeSeparatorIndex));
                returnVal.ParseTimeValue(iso.Substring(timeSeparatorIndex + 1));
            }
            else
            {
                returnVal.ParseDateValue(iso);
            }

            return returnVal;
        }

        private void ParseDateValue(string isoDateString)
        {
            if (string.IsNullOrEmpty(isoDateString))
            {
                return;
            }

            IList<string> dateTimeComponents = isoDateString.Split(DATE_SEPARATOR, 4);
            int parsedVal;

            if (dateTimeComponents.Count >= 1 &&
                dateTimeComponents[0].Length == 4 &&
                int.TryParse(dateTimeComponents[0], out parsedVal))
            {
                Year = parsedVal; // Capture a year value
            }

            if (dateTimeComponents.Count >= 2)
            {
                // Is it a regular numerical month?
                if (dateTimeComponents[1].Length == 2 &&
                    int.TryParse(dateTimeComponents[1], out parsedVal))
                {
                    Month = parsedVal;
                }
                else if (dateTimeComponents[1].Length >= 2)
                {
                    char firstLetter = dateTimeComponents[1][0];
                    string substring = dateTimeComponents[1].Substring(1);

                    if (dateTimeComponents[1].Length == 3 &&
                        firstLetter == 'W' &&
                        int.TryParse(substring, out parsedVal))
                    {
                        // Is it a week reference?
                        Week = parsedVal;
                    }
                }
            }

            if (dateTimeComponents.Count >= 3)
            {
                if (int.TryParse(dateTimeComponents[2], out parsedVal))
                {
                    if (dateTimeComponents[2].Length == 2)
                    {
                        //It's a regular day reference ("the 21st" = "21")
                        DayOfMonth = parsedVal;
                    }
                    else if (dateTimeComponents[2].Length == 1)
                    {
                        // It's a day of week reference ("monday" = "1", etc)
                        DayOfWeek = parsedVal;
                    }
                }
            }
        }

        private void ParseTimeValue(string isoTimeString)
        {
            if (string.IsNullOrEmpty(isoTimeString))
            {
                return;
            }

            // Trim the leading "T" if the caller forgot to do so
            if (isoTimeString.StartsWith("T"))
            {
                isoTimeString = isoTimeString.Substring(1);
            }

            // BUGBUG: We don't enfore the upper limit to values, so T99:99:99 is a technically a valid time

            // Now we can assume it is a 12:00:00-800 pattern. Split it into substrings
            string[] timeParts = isoTimeString.Split(':');
            int parsedVal;

            if (timeParts.Length >= 1 && timeParts[0].Length == 2 &&
                int.TryParse(timeParts[0], out parsedVal))
            {
                // Hour field is present
                Hour = parsedVal;
            }

            if (timeParts.Length >= 2 && timeParts[1].Length == 2 &&
                int.TryParse(timeParts[1], out parsedVal))
            {
                // Minute field is present
                Minute = parsedVal;
            }

            if (timeParts.Length == 3)
            {
                // Second field is present, and potentially there is a timezone attached to it
                string secondsField = timeParts[2].Trim();
                if (timeParts[2].Length > 2)
                {
                    // Timezone is here. Split it out
                    secondsField = timeParts[2].Substring(0, 2);

                    if (int.TryParse(secondsField, out parsedVal))
                    {
                        Second = parsedVal;
                    }

                    TimeZone = timeParts[2].Substring(2);
                }
                else if (secondsField.Length == 2 &&
                    int.TryParse(secondsField, out parsedVal))
                {
                    Second = parsedVal;
                }
            }
        }
    }
}