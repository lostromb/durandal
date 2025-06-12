namespace Durandal.Common.Time.Timex.Client
{
    using Calendar;
    using Constants;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Durandal.Common.Time.Timex.Enums;

    /// <summary>
    /// Represents a recurring series of events, separated by a particular fixed duration value.
    /// A recurrence is composed of three main parts:
    /// FREQUENCY: Defines the time span between each recurrence in the set, i.e. "2 weeks"
    /// QUANTITY: Defines how many times the event happens within the time span of FREQUENCY. Almost always == 1
    /// ANCHORS: These are time components that are used to "filter" recurrences. For example, "every day at 4" will limit
    /// recurrences to those that have hour == 4.
    /// </summary>
    public class Recurrence : TimexValue
    {
        private readonly IDictionary<AnchorField, string> anchors = new Dictionary<AnchorField, string>();

        /// <summary>
        /// Returns the type of time expression this represents. Based on the temporal type, you can then
        /// cast the Timex into one of its subclasses to start working with it.
        /// </summary>
        /// <returns>The temporal type of this time expression</returns>
        public override TemporalType GetTemporalType()
        {
            return TemporalType.Set;
        }

        #region Field accessors

        /// <summary>
        /// Returns the length of this recurrence's frequency. The frequency defines a certain span
        /// of time between each recurrence in the set. "Every day" has frequency P1D (one day).
        /// "Every month" has frequency P1M (one month), etc. Typically this will be "1", but things
        /// like "every other day" will have frequency == "P2D", same for "every 6 hours", etc.
        /// </summary>
        /// <returns>The integer amount of the frequency</returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public int? GetFrequencyValue()
        {
            return this.GetValueAsInt(DateTimeParts.Frequency);
        }

        /// <summary>
        /// The temporal unit attached to the frequency value. Note that this library only support single-unit frequencies,
        /// such as "5 days" or "1 week"; expressions like "every 1 hour and 30 minutes" are not handled, unless of course 
        /// you convert them into a base unit such as "90 minutes" first.
        /// </summary>
        /// <returns>The unit of frequency</returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public TemporalUnit? GetFrequencyUnit()
        {
            string stringVal = this.GetValueAsString(DateTimeParts.FrequencyUnit);
            if (stringVal == null)
            {
                return null;
            }

            TemporalUnit returnVal;
            if (Enum.TryParse(stringVal, out returnVal))
            {
                return returnVal;
            }

            return null;
        }

        /// <summary>
        /// Returns the set of anchor values for this recurrence. Anchor values convey the meaning of phrases
        /// like "every day at 8 AM". In that example, the frequency is "1 day" and AnchorField.Hour => "08".
        /// These values can be compounded in various ways, for example "every Monday at 9" has two anchors,
        /// AnchorField.DayOfWeek => "1" and AnchorField.Hour => "09". Not all recurrences have anchors; simple
        /// ones such as "Every day" contain only a frequency and quantity. These should be used to "filter" recurrences
        /// to a specific subset.
        /// </summary>
        /// <returns>The set of anchor values for this recurrence, if any exist</returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public IDictionary<AnchorField, string> GetAnchorValues()
        {
            return anchors;
        }

        /// <summary>
        /// Returns the quantity of this recurrence. This field indicates HOW MANY TIMES this event occurs within
        /// the time span specified by FREQUENCY. For example, "3 times a day" will have quant = 3 and freq = "1 day"
        /// The keywords "EACH" and "EVERY" are both parsed as "1", since there is no semantic difference between the two.
        /// </summary>
        /// <returns>The quantity of this recurrence</returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public int? GetQuantity()
        {
            return this.GetValueAsInt(DateTimeParts.Quantity);
        }

        /// <summary>
        /// If this recurrence is anchored to an hour of the day, this flag indicates whether that time
        /// is not fully resolved.
        /// </summary>
        /// <returns>A flag indicating the ambiguity of this recurrence's ampm anchor, if such an anchor exists</returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        [SuppressMessage("Microsoft.Naming", "CA1709", MessageId = "Pm")]
        [SuppressMessage("Microsoft.Naming", "CA1726")]
        public bool GetAmPmAmbiguousFlag()
        {
            return anchors.ContainsKey(AnchorField.Hour) &&
                string.Equals("true", this.GetValueAsString(DateTimeParts.AmPmUnambiguous));
        }

#if !CLIENT_ONLY

        /// <summary>
        /// Returns a DateAndTime value referring to the first instance of this recurrence
        /// in the given resolution context. If no interpretation is possible, this method returns null
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public DateAndTime GetFirstOccurrence(TimexContext context)
        {
            if (!context.UseInference)
            {
                throw new ArgumentException("You must set UseInference == True in the context before you can get the first occurrence of a set");
            }

            Dictionary<string, string> timexDict = new Dictionary<string, string>();
            TemporalType returnedTemporalType = TemporalType.Date;
            TemporalUnit? freqUnit = GetFrequencyUnit();

            if (!freqUnit.HasValue)
            {
                return null;
            }

            // Do we have anchor fields?
            if (anchors.Count == 0)
            {
                // If not, interpret the value as an offset with size = frequency unit
                timexDict[TimexAttributes.Offset] = "0";
                if (freqUnit.Value == TemporalUnit.Second)
                {
                    timexDict[TimexAttributes.OffsetUnit] = "second";
                    returnedTemporalType = TemporalType.Time;
                }
                if (freqUnit.Value == TemporalUnit.Minute)
                {
                    timexDict[TimexAttributes.OffsetUnit] = "minute";
                    returnedTemporalType = TemporalType.Time;
                }
                if (freqUnit.Value == TemporalUnit.Hour)
                {
                    timexDict[TimexAttributes.OffsetUnit] = "hour";
                    returnedTemporalType = TemporalType.Time;
                }
                if (freqUnit.Value == TemporalUnit.Day)
                {
                    timexDict[TimexAttributes.OffsetUnit] = "day";
                }
                if (freqUnit.Value == TemporalUnit.Week)
                {
                    timexDict[TimexAttributes.OffsetUnit] = "week";
                }
                if (freqUnit.Value == TemporalUnit.Month)
                {
                    timexDict[TimexAttributes.OffsetUnit] = "month";
                }
                if (freqUnit.Value == TemporalUnit.Year)
                {
                    timexDict[TimexAttributes.OffsetUnit] = "year";
                }
            }
            else
            {
                // Convert anchor fields into timex dict values directly
                if (anchors.ContainsKey(AnchorField.Second))
                {
                    timexDict[Iso8601.Second] = anchors[AnchorField.Second];
                    returnedTemporalType = TemporalType.Time;
                }
                if (anchors.ContainsKey(AnchorField.Minute))
                {
                    timexDict[Iso8601.Minute] = anchors[AnchorField.Minute];
                    returnedTemporalType = TemporalType.Time;
                }
                if (anchors.ContainsKey(AnchorField.Hour))
                {
                    timexDict[Iso8601.Hour] = anchors[AnchorField.Hour];
                    returnedTemporalType = TemporalType.Time;
                }
                if (anchors.ContainsKey(AnchorField.PartOfDay))
                {
                    timexDict[Iso8601.PartOfDay] = anchors[AnchorField.PartOfDay];
                    returnedTemporalType = TemporalType.Time;
                }
                if (anchors.ContainsKey(AnchorField.Day))
                {
                    timexDict[Iso8601.Day] = anchors[AnchorField.Day];
                    
                    // handle edge case for "every hour in _ day"
                    if (freqUnit.Value == TemporalUnit.Hour)
                    {
                        timexDict[Iso8601.Hour] = "0";
                    }
                }
                if (anchors.ContainsKey(AnchorField.Week))
                {
                    timexDict[Iso8601.Week] = anchors[AnchorField.Week];
                }
                if (anchors.ContainsKey(AnchorField.DayOfWeek))
                {
                    timexDict[Iso8601.WeekDay] = anchors[AnchorField.DayOfWeek];
                }
                if (anchors.ContainsKey(AnchorField.Month))
                {
                    timexDict[Iso8601.Month] = anchors[AnchorField.Month];

                    // handle edge case for "every day in _ month"
                    // fixme there are more edge cases like this by the way
                    if (freqUnit.Value == TemporalUnit.Day)
                    {
                        timexDict[Iso8601.Day] = "1";
                    }
                }
                if (anchors.ContainsKey(AnchorField.Year))
                {
                    timexDict[Iso8601.Year] = anchors[AnchorField.Year];

                    // handle edge case for "every month in _ year"
                    if (freqUnit.Value == TemporalUnit.Month)
                    {
                        timexDict[Iso8601.Month] = "1";
                    }
                }

                if (anchors.ContainsKey(AnchorField.Weekend))
                {
                    timexDict[TimexAttributes.Offset] = "0";
                    timexDict[TimexAttributes.OffsetUnit] = "weekend";
                }
                else if (anchors.ContainsKey(AnchorField.Weekday))
                {
                    timexDict[TimexAttributes.Offset] = "0";
                    timexDict[TimexAttributes.OffsetUnit] = "weekdays";
                }
            }

            ExtendedDateTime edt = ExtendedDateTime.Create(returnedTemporalType, timexDict, context);

            // Convert the EDT into a DateAndTime
            TimexValue convertedTimex = CreateFromExtendedDateTime(edt);
            if (convertedTimex == null)
            {
                return null;
            }

            return convertedTimex.AsDateAndTime();
        }

#endif

#endregion

#region Parsers

        /// <summary>
        /// Parses timex xml fields into values inside this Recurrence object. If parsing failed, this returns false.
        /// </summary>
        /// <param name="value">The timex value, which is an ISO time or duration string</param>
        /// <param name="freq">The timex frequency</param>
        /// <param name="quant">The timex quantity</param>
        /// <returns>True if parsing succeeded</returns>
        internal bool Parse(string value, string freq, string quant)
        {
            if (string.IsNullOrEmpty(value))
            {
                // All sets must have a value
                return false;
            }

            // First, try to determine what form it is in
            // There are three potential classes of sets:
            // 1 a. flight = lutest , "Every week at 5 pm" => value = "P1WT17" quant = "EVERY"
            // 1 b. "Every day (Inference on, deprecated)" => value="P1D" quant="EVERY"
            // 2. "Every day (Inference off)" => value="XXXX-XX-XX" quant="EVERY"
            // 3. "Every monday (Inference on, deprecated)" => value="2015-09-07" quant="EVERY" frequency="1week"
            // Case 2 is by far the most complicated, and it's the one primarily used in the timex resolver.
            if (value.StartsWith("P", StringComparison.Ordinal))
            {
                if (value.Contains("T") && !value.Contains("H") && !value.Contains("M") && !value.Contains("S"))
                {
                    if (!ParseType1aRecurrence(value))
                    {
                        return false;
                    }

                }
                // It is the first form.
                // In this case, VALUE is an ISO duration, QUANT can be anything, and other fields are ignored
                else
                {
                    if (!ParseType1bRecurrence(value))
                    {
                        return false;
                    }
                }
            }
            else if (value.Contains("X"))
            {
                // It is the second form. This is the most common form. The frequency is given by freq tags, and anchors are specified in the value string
                if (!ParseType2Recurrence(value, freq))
                {
                    return false;
                }
            }
            else
            {
                // It is the third form. Currently this only happens for occurrences based on a weekday, with inference mode turned on
                if (!ParseType3Recurrence(value, freq))
                {
                    return false;
                }
            }

            int quantity = DateTimeParserHelpers.ParseQuantity(quant);
            this.SetValue(DateTimeParts.Quantity, quantity.ToString(CultureInfo.InvariantCulture));

            return true;
        }

        /// <summary>
        /// Parses a "type 1b" recurrence, which comes in the form type="Set" value="P1D" quant="EVERY"
        /// </summary>
        /// <param name="value">The string value</param>
        /// <returns>true if parsing succeeded</returns>
        private bool ParseType1bRecurrence(string value)
        {
            IDictionary<TemporalUnit, int?> frequencyFields = DateTimeParserHelpers.ParseIsoDuration(value);
            if (frequencyFields == null || frequencyFields.Count != 1)
            {          
                return false;       
            }

            var firstItem = frequencyFields.First();
            this.SetValue(DateTimeParts.FrequencyUnit, firstItem.Key.ToString());
            this.SetValue(DateTimeParts.Frequency, firstItem.Value.ToString());

            return true;
        }


        /// <summary>
        /// Parses a "type 1a" recurrence
        /// 1 a. flight = lutest , "Every week at 5 pm" => value="P1WT17" quant="EVERY"
        /// </summary>
        /// <param name="value">The string value</param>
        /// <returns>true if parsing succeeded</returns>
        [SuppressMessage("Microsoft.Maintainability", "CA1502")]
        private bool ParseType1aRecurrence(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string datePortion = string.Empty;
            string timePortion = string.Empty;

            if (value.Contains("T"))
            {
                if (value.StartsWith("T", StringComparison.Ordinal))
                {
                    timePortion = value.Substring(1);
                }
                else
                {
                    char[] splitChars = { 'T' };
                    string[] parts = value.Split(splitChars, 2);
                    if (parts.Length != 2)
                    {
                        return false;
                    }

                    datePortion = parts[0];
                    timePortion = parts[1];
                }
            }
            else
            {
                //no need to parse if no time is present
                return false;
            }

            if (!string.IsNullOrEmpty(datePortion))
            {
                IDictionary<TemporalUnit, int?> frequencyFields = DateTimeParserHelpers.ParseIsoDuration(value);
                if (frequencyFields == null || frequencyFields.Count != 1)
                {
                    return false;
                }

                var firstItem = frequencyFields.First();
                this.SetValue(DateTimeParts.FrequencyUnit, firstItem.Key.ToString());
                this.SetValue(DateTimeParts.Frequency, firstItem.Value.ToString());
            }

            IDictionary<AnchorField, string> returnVal = new Dictionary<AnchorField, string>();

            if (!string.IsNullOrEmpty(timePortion))
            {
                string[] timeParts = timePortion.Split(':');

                if (timeParts.Length >= 1)
                {
                    // Parse hour/partofday field
                    int scratch;
                    if (int.TryParse(timeParts[0], out scratch))
                    {
                        returnVal.Add(AnchorField.Hour, timeParts[0]);
                    }
                    else if ("XX".Equals(timeParts[0]))
                    {
                        returnVal.Add(AnchorField.Hour, null);
                    }
                    else
                    {
                        returnVal.Add(AnchorField.PartOfDay, timeParts[0]);
                    }
                }

                if (timeParts.Length >= 2)
                {
                    // Parse minute field
                    returnVal.Add(AnchorField.Minute, DateTimeParserHelpers.TryParseIntOrNull(timeParts[1]));
                }

                if (timeParts.Length >= 3)
                {
                    // Parse second field
                    returnVal.Add(AnchorField.Second, DateTimeParserHelpers.TryParseIntOrNull(timeParts[2]));
                }
            }

            // Validate all fields
            int fieldValue;
            if (returnVal.ContainsKey(AnchorField.Second) && returnVal[AnchorField.Second] != null && (!int.TryParse(returnVal[AnchorField.Second], out fieldValue) || fieldValue < 0 || fieldValue > 59))
            {
                throw new TimexException("The field value Second = " + returnVal[AnchorField.Second] + " is not a number or is outside the expected range");
            }
            if (returnVal.ContainsKey(AnchorField.Minute) && returnVal[AnchorField.Minute] != null && (!int.TryParse(returnVal[AnchorField.Minute], out fieldValue) || fieldValue < 0 || fieldValue > 59))
            {
                throw new TimexException("The field value Minute = " + returnVal[AnchorField.Minute] + " is not a number or is outside the expected range");
            }
            if (returnVal.ContainsKey(AnchorField.Hour) && returnVal[AnchorField.Hour] != null && (!int.TryParse(returnVal[AnchorField.Hour], out fieldValue) || fieldValue < 0 || fieldValue > 24))
            {
                throw new TimexException("The field value Hour = " + returnVal[AnchorField.Hour] + " is not a number or is outside the expected range");
            }

            foreach (var field in returnVal)
            {
                if (field.Value != null)
                {
                    anchors.Add(field.Key, field.Value);
                }
            }

            return true;
        }

        /// <summary>
        /// Parses a "type 2" recurrence, which comes in the form type="Set" value="XXXX-XX-XX" quant="EVERY"
        /// </summary>
        /// <param name="value">The string value</param>
        /// <param name="freq">The frequency field from the timex tag</param>
        /// <returns>true if parsing succeeded</returns>
        [SuppressMessage("Microsoft.Maintainability", "CA1502")]
        private bool ParseType2Recurrence(string value, string freq)
        {
            // First, find which values are specified
            IDictionary<AnchorField, string> specifiedFields = ParseUnderspecifiedValue(value);

            // Now determine the unspecified field with the highest granularity. This becomes our frequency
            int frequency = 1;
            TemporalUnit? frequencyUnit = null;
            
            if (!string.IsNullOrEmpty(freq))
            {
                // Does the tag specify freq="" on its own? If so, prefer that
                TemporalUnit parsedFrequencyUnit;

                // Split the numeric and letter part of the frequency string apart
                int freqBound = 0;
                while (freqBound < freq.Length && char.IsDigit(freq[freqBound]))
                {
                    freqBound++;
                }
                
                if (freqBound > 0 && freqBound < freq.Length && int.TryParse(freq.Substring(0, freqBound), out frequency) && EnumExtensions.TryParse(freq.Substring(freqBound), out parsedFrequencyUnit))
                {
                    frequencyUnit = parsedFrequencyUnit;
                }
                else
                {
                    frequency = 1;
                    frequencyUnit = null;
                }
            }
            
            if (frequencyUnit == null)
            {
                // Otherwise determine it from the most granular temporal unit in the value
                if (specifiedFields.ContainsKey(AnchorField.Second) && specifiedFields[AnchorField.Second] == null)
                {
                    // Sample input: "XXXX-XX-XX:TXX:XX:XX"
                    frequencyUnit = TemporalUnit.Second;
                }
                else if (specifiedFields.ContainsKey(AnchorField.Minute) && specifiedFields[AnchorField.Minute] == null)
                {
                    // Sample input: "XXXX-XX-XX:TXX:XX"
                    frequencyUnit = TemporalUnit.Minute;
                }
                else if (specifiedFields.ContainsKey(AnchorField.Hour) && specifiedFields[AnchorField.Hour] == null)
                {
                    // Sample input: "XXXX-XX-XX:TXX"
                    frequencyUnit = TemporalUnit.Hour;
                }
                else if (specifiedFields.ContainsKey(AnchorField.Day) && specifiedFields[AnchorField.Day] == null)
                {
                    // Sample input: "XXXX-XX-XX"
                    frequencyUnit = TemporalUnit.Day;
                }
                else if (specifiedFields.ContainsKey(AnchorField.Week) && specifiedFields[AnchorField.Week] == null)
                {
                    // Sample input: "XXXX-WXX"
                    frequencyUnit = TemporalUnit.Week;
                }
                else if (specifiedFields.ContainsKey(AnchorField.Month) && specifiedFields[AnchorField.Month] == null)
                {
                    // Sample input: "XXXX-XX"
                    frequencyUnit = TemporalUnit.Month;
                }
                else if (specifiedFields.ContainsKey(AnchorField.Year) && specifiedFields[AnchorField.Year] == null)
                {
                    // Sample input: "XXXX"
                    frequencyUnit = TemporalUnit.Year;
                }
            }

            if (frequencyUnit == null)
            {
                return false;
            }

            this.SetValue(DateTimeParts.Frequency, frequency.ToString(CultureInfo.InvariantCulture));
            this.SetValue(DateTimeParts.FrequencyUnit, frequencyUnit.ToString());

            // Any fields which have non-null values will become anchors
            foreach (var field in specifiedFields)
            {
                if (field.Value != null)
                {
                    anchors.Add(field.Key, field.Value);
                }
            }

            return true;
        }

        /// <summary>
        /// Parses a "type 3" recurrence, which comes in the form type="Set" value="2015-09-07" quant="EVERY" frequency="1week"
        /// </summary>
        /// <param name="value">The string value</param>
        /// <param name="freq">The frequency field from the timex tag</param>
        /// <returns>true if parsing succeeded</returns>
        private bool ParseType3Recurrence(string value, string freq)
        {
            // The value field then contains a valid ISO date. Parse it.
            DateTime parsedTimeValue;
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", null, DateTimeStyles.None, out parsedTimeValue))
            {
                return false;
            }

            Tuple<TemporalUnit, int> frequencyFields = DateTimeParserHelpers.ParseFrequencyPair(freq);
            if (frequencyFields == null)
            {
                return false;
            }

            // Find the day of week this is anchored to, and set that field
            anchors.Add(AnchorField.DayOfWeek, TimexHelpers.GetIso8601DayOfWeek(parsedTimeValue).ToString(CultureInfo.InvariantCulture));

            this.SetValue(DateTimeParts.FrequencyUnit, frequencyFields.Item1.ToString());
            this.SetValue(DateTimeParts.Frequency, frequencyFields.Item2.ToString(CultureInfo.InvariantCulture));

            return true;
        }

        /// <summary>
        /// Accepts a string in the form of an underspecified ISO time, such as "XXXX-XX-15",
        /// and parses its components into a dictionary of individual fields.
        /// Underspecified fields (those that are just XXXX) will have null values in the dictionary,
        /// but the keys will still be present.
        /// </summary>
        /// <param name="value">The iso time value</param>
        /// <returns>The dictionary of parsed fields</returns>
        [SuppressMessage("Microsoft.Maintainability", "CA1502")]
        private static IDictionary<AnchorField, string> ParseUnderspecifiedValue(string value)
        {
            IDictionary<AnchorField, string> returnVal = new Dictionary<AnchorField, string>();
            
            if (string.IsNullOrEmpty(value))
            {
                return returnVal;
            }

            string datePortion = string.Empty;
            string timePortion = string.Empty;

            if (value.Contains("T"))
            {
                if (value.StartsWith("T", StringComparison.Ordinal))
                {
                    timePortion = value.Substring(1);
                }
                else
                {
                    char[] splitChars = {'T'};
                    string[] parts = value.Split(splitChars, 2);
                    if (parts.Length != 2)
                    {
                        return returnVal;
                    }

                    datePortion = parts[0];
                    timePortion = parts[1];
                }
            }
            else
            {
                datePortion = value;
            }

            if (!string.IsNullOrEmpty(datePortion))
            {
                string[] dateParts = datePortion.Split('-');
                if (dateParts.Length >= 1)
                {
                    // Parse year field
                    returnVal.Add(AnchorField.Year, DateTimeParserHelpers.TryParseIntOrNull(dateParts[0]));
                }

                if (dateParts.Length >= 2)
                {
                    // Parse either the month or week of the year
                    if (dateParts[1].Length == 3 && dateParts[1].StartsWith("W", StringComparison.Ordinal))
                    {
                        returnVal.Add(AnchorField.Week, DateTimeParserHelpers.TryParseIntOrNull(dateParts[1].Substring(1)));
                    }
                    else
                    {
                        returnVal.Add(AnchorField.Month, DateTimeParserHelpers.TryParseIntOrNull(dateParts[1]));
                    }
                }

                if (dateParts.Length >= 3)
                {
                    // Parse either the day of month, the week of month, the day of week, "WE", or "WD"
                    if (dateParts[1].Length == 3 && dateParts[1].StartsWith("W", StringComparison.Ordinal))
                    {
                        if ("WE".Equals(dateParts[2]))
                        {
                            // Weekend
                            returnVal.Add(AnchorField.Weekend, "1");
                        }
                        else if ("WD".Equals(dateParts[2]))
                        {
                            // Weekdays
                            returnVal.Add(AnchorField.Weekday, "1");
                        }
                        else
                        {
                            // Day of week
                            int scratch;
                            if (int.TryParse(dateParts[2], out scratch)/* && scratch >= 1 && scratch <= 7*/)
                            {
                                returnVal.Add(AnchorField.DayOfWeek, dateParts[2]);
                            }
                            else
                            {
                                returnVal.Add(AnchorField.DayOfWeek, null);
                            }
                        }
                    }
                    else if (dateParts[2].Length == 3 && dateParts[2].StartsWith("W", StringComparison.Ordinal))
                    {
                        // Week of month
                        returnVal.Add(AnchorField.WeekOfMonth, DateTimeParserHelpers.TryParseIntOrNull(dateParts[2].Substring(1)));
                    }
                    else
                    {
                        // Day of month
                        returnVal.Add(AnchorField.Day, DateTimeParserHelpers.TryParseIntOrNull(dateParts[2]));
                    }
                }

                if (dateParts.Length == 4)
                {
                    // This only happens for "XXXX-ZZ-WYY-X" expressions, meaning "the Xth day of the Yth week of the Zth month"
                    returnVal.Add(AnchorField.DayOfWeek, DateTimeParserHelpers.TryParseIntOrNull(dateParts[3]));
                }
            }

            if (!string.IsNullOrEmpty(timePortion))
            {
                string[] timeParts = timePortion.Split(':');

                if (timeParts.Length >= 1)
                {
                    // Parse hour/partofday field
                    int scratch;
                    if (int.TryParse(timeParts[0], out scratch))
                    {
                        returnVal.Add(AnchorField.Hour, timeParts[0]);
                    }
                    else if ("XX".Equals(timeParts[0]))
                    {
                        returnVal.Add(AnchorField.Hour, null);
                    }
                    else
                    {
                        returnVal.Add(AnchorField.PartOfDay, timeParts[0]);
                    }
                }

                if (timeParts.Length >= 2)
                {
                    // Parse minute field
                    returnVal.Add(AnchorField.Minute, DateTimeParserHelpers.TryParseIntOrNull(timeParts[1]));
                }

                if (timeParts.Length >= 3)
                {
                    // Parse second field
                    returnVal.Add(AnchorField.Second, DateTimeParserHelpers.TryParseIntOrNull(timeParts[2]));
                }
            }

            // Validate all fields
            int fieldValue;
            if (returnVal.ContainsKey(AnchorField.Second) && returnVal[AnchorField.Second] != null && (!int.TryParse(returnVal[AnchorField.Second], out fieldValue) || fieldValue < 0 || fieldValue > 59))
            {
                throw new TimexException("The field value Second = " + returnVal[AnchorField.Second] + " is not a number or is outside the expected range");
            }
            if (returnVal.ContainsKey(AnchorField.Minute) && returnVal[AnchorField.Minute] != null && (!int.TryParse(returnVal[AnchorField.Minute], out fieldValue) || fieldValue < 0 || fieldValue > 59))
            {
                throw new TimexException("The field value Minute = " + returnVal[AnchorField.Minute] + " is not a number or is outside the expected range");
            }
            if (returnVal.ContainsKey(AnchorField.Hour) && returnVal[AnchorField.Hour] != null && (!int.TryParse(returnVal[AnchorField.Hour], out fieldValue) || fieldValue < 0 || fieldValue > 24))
            {
                throw new TimexException("The field value Hour = " + returnVal[AnchorField.Hour] + " is not a number or is outside the expected range");
            }
            if (returnVal.ContainsKey(AnchorField.Day) && returnVal[AnchorField.Day] != null && (!int.TryParse(returnVal[AnchorField.Day], out fieldValue) || fieldValue < 1 || fieldValue > 31))
            {
                throw new TimexException("The field value Day = " + returnVal[AnchorField.Day] + " is not a number or is outside the expected range");
            }
            if (returnVal.ContainsKey(AnchorField.DayOfWeek) && returnVal[AnchorField.DayOfWeek] != null && (!int.TryParse(returnVal[AnchorField.DayOfWeek], out fieldValue) || fieldValue < 1 || fieldValue > 7))
            {
                throw new TimexException("The field value DayOfWeek = " + returnVal[AnchorField.DayOfWeek] + " is not a number or is outside the expected range");
            }
            if (returnVal.ContainsKey(AnchorField.Week) && returnVal[AnchorField.Week] != null && (!int.TryParse(returnVal[AnchorField.Week], out fieldValue) || fieldValue < 1 || fieldValue > 53))
            {
                throw new TimexException("The field value Week = " + returnVal[AnchorField.Week] + " is not a number or is outside the expected range");
            }
            if (returnVal.ContainsKey(AnchorField.Month) && returnVal[AnchorField.Month] != null && (!int.TryParse(returnVal[AnchorField.Month], out fieldValue) || fieldValue < 1 || fieldValue > 12))
            {
                throw new TimexException("The field value Month = " + returnVal[AnchorField.Month] + " is not a number or is outside the expected range");
            }

            return returnVal;
        }

        #endregion
    }
}
