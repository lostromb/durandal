namespace Durandal.Common.Time.Timex.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Xml;
    using Durandal.Common.Time.Timex.Enums;

    /// <summary>
    /// Represents an abstract time expression that can be manifest as one of several forms.
    /// This class contains a "bag of attributes" that contain the raw parsed time values.
    /// Subclasses of this (DateAndTime, Duration, Recurrence) will implement
    /// methods that actually
    /// </summary>
    public abstract class TimexValue
    {
        private IDictionary<DateTimeParts, string> parsedValues;

        protected IDictionary<DateTimeParts, string> ParsedValues
        {
            get
            {
                return this.parsedValues;
            }
        }

        protected TimexValue()
        {
            this.parsedValues = new Dictionary<DateTimeParts, string>();
        }

        /// <summary>
        /// Returns the type of time expression this represents. Based on the temporal type, you can then
        /// cast the Timex into one of its subclasses to start working with it.
        /// </summary>
        /// <returns>The temporal type of this time expression</returns>
        [SuppressMessage("Microsoft.Design", "CA1024")]
        public abstract TemporalType GetTemporalType();

        /// <summary>
        /// The timex id of this tag. Usually a 0-based integer
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704")]
        public string Tid
        {
            get;
            private set;
        }

        /// <summary>
        /// Any modifier that augments this timex, such as "APPROX" or "AFTER", which carries semantic meaning but does not affect resolution.
        /// </summary>
        public Modifier Mod
        {
            get;
            private set;
        }

        /// <summary>
        /// Casts this as a DateAndTime object if appropriate
        /// </summary>
        /// <returns>This value interpreted as a DateAndTime, or null</returns>
        public DateAndTime AsDateAndTime()
        {
            if (this.GetTemporalType().IsDateOrTime())
            {
                return this as DateAndTime;
            }

            return null;
        }

        /// <summary>
        /// Casts this as a Duration object if appropriate
        /// </summary>
        /// <returns>This value interpreted as a Duration, or null</returns>
        public Duration AsDuration()
        {
            if (this.GetTemporalType() == TemporalType.Duration)
            {
                return this as Duration;
            }

            return null;
        }

        /// <summary>
        /// Casts this as a Recurrence object if appropriate
        /// </summary>
        /// <returns>This value interpreted as a Recurrence, or null</returns>
        public Recurrence AsRecurrence()
        {
            if (this.GetTemporalType() == TemporalType.Set)
            {
                return this as Recurrence;
            }

            return null;
        }

#if !CLIENT_ONLY

        /// <summary>
        /// Converts an ExtendedDateTime into a corresponding client-namespace object (the exact return type depends on the value that goes in).
        /// </summary>
        /// <param name="edt"></param>
        /// <returns></returns>
        public static TimexValue CreateFromExtendedDateTime(ExtendedDateTime edt)
        {
            if (edt == null)
            {
                return null;
            }

            TimexValue returnVal = Parse(edt.FormatValue(), edt.FormatType(), "0", edt.FormatComment(), edt.FormatFrequency(), edt.FormatQuantity(), edt.FormatMod());
            return returnVal;
        }

#endif

        /// <summary>
        /// Parses a timex value from an XML tag, in the form of &lt;TIMEX3 tid="0" type="Date" value="2015-08-05"&gt;tomorrow&lt;/TIMEX3&gt;
        /// </summary>
        /// <param name="timex3Tag"></param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Security.Xml", "CA3053")]
        public static TimexValue ParseXmlTag(string timex3Tag)
        {
            if (string.IsNullOrEmpty(timex3Tag))
            {
                return null;
            }

            XmlReaderSettings settings = new XmlReaderSettings()
            {
                ConformanceLevel = ConformanceLevel.Fragment
            };

            TimexValue returnVal = null;

            try
            {
                using (XmlReader reader = XmlReader.Create(new StringReader(timex3Tag), settings))
                {
                    while (reader.Read())
                    {
                        if (returnVal == null &&
                            reader.NodeType == XmlNodeType.Element &&
                            "TIMEX3".Equals(reader.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            string type = reader.GetAttribute("type") ?? string.Empty;
                            string value = reader.GetAttribute("value") ?? string.Empty;
                            string mod = reader.GetAttribute("mod") ?? string.Empty;
                            string comment = reader.GetAttribute("comment") ?? string.Empty;
                            string freq = reader.GetAttribute("freq") ?? reader.GetAttribute("frequency") ?? string.Empty;
                            string quant = reader.GetAttribute("quant") ?? reader.GetAttribute("quantity") ?? string.Empty;
                            string tid = reader.GetAttribute("tid") ?? string.Empty;

                            returnVal = Parse(value, type, tid, comment, freq, quant, mod);
                        }
                    }
                }

                return returnVal;
            }
            catch (XmlException e)
            {
                throw new TimexException("Input XML is invalid: " + e.Message);
            }
        }

        /// <summary>
        /// Parses a timex value from a raw set of string values, which represent the different fields that
        /// are normally inside the XML tag.
        /// </summary>
        /// <param name="value">The timex value, which is an ISO time or duration string</param>
        /// <param name="type">The timex type, such as "Duration"</param>
        /// <param name="tid">The timex ID, such as "0"</param>
        /// <param name="comment">The timex comment, which can contain useful flags</param>
        /// <param name="freq">The timex frequency</param>
        /// <param name="quant">The timex quantity</param>
        /// <param name="mod">The timex modifier, such as "AFTER"</param>
        /// <returns>A single parsed Timex value, or null if parsing fails</returns>
        [SuppressMessage("Microsoft.Design", "CA1026")]
        [SuppressMessage("Microsoft.Naming", "CA1704")]
        public static TimexValue Parse(string value, string type, string tid = "0", string comment = "", string freq = "", string quant = "",
                                  string mod = "")
        {
            TimexValue returnVal = null;
            
            // First, try and determine what type of timex this is
            if (string.Equals(type, "Set", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(freq) ||
                !string.IsNullOrEmpty(quant))
            {
                // Does it have FREQ or QUANT? Then it is a set
                returnVal = ParseAsRecurrence(value, freq, quant, comment);
            }
            else if (!string.IsNullOrEmpty(value) && (string.Equals(type, "Duration", StringComparison.OrdinalIgnoreCase) || value.StartsWith("P", StringComparison.Ordinal)) && !value.EndsWith("_REF", StringComparison.Ordinal))
            {
                // Does its value start with "P"? Then it is a duration
                returnVal = ParseAsDuration(value);
            }
            else if (!string.IsNullOrEmpty(value))
            {
                returnVal = ParseAsDateTime(value, comment);

                // If no values were extracted by parsing, return null
                if (returnVal != null && returnVal.ParsedValues.Count == 0)
                {
                    returnVal = null;
                }
            }

            if (returnVal != null)
            {
                // Parse common fields like tid and mod
                if (!string.IsNullOrEmpty(tid))
                {
                    returnVal.Tid = tid;
                }

                if (!string.IsNullOrEmpty(mod))
                {
                    returnVal.Mod = DateTimeParserHelpers.ParseModifier(mod);
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Parses timex fields into a Duration object
        /// </summary>
        /// <param name="value">the ISO value, e.g. "P1D"</param>
        /// <returns>The parsed duration</returns>
        private static Duration ParseAsDuration(string value)
        {
            Duration returnVal = new Duration();

            returnVal.ParseDurationValue(value);

            return returnVal;
        }

        /// <summary>
        /// Parses timex fields into a Recurrence object
        /// </summary>
        /// <param name="value">timex value field</param>
        /// <param name="freq">timex freq field</param>
        /// <param name="quant">timex quant field</param>
        /// <param name="comment">timex comment field</param>
        /// <returns>The parsed recurrence, or null</returns>
        private static Recurrence ParseAsRecurrence(string value, string freq, string quant, string comment)
        {
            Recurrence returnVal = new Recurrence();

            if (!returnVal.Parse(value, freq, quant))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(comment))
            {
                if (comment.Contains("ampm"))
                {
                    returnVal.SetValue(DateTimeParts.AmPmUnambiguous, "true");
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Parses timex fields into a DateAndTime object
        /// </summary>
        /// <param name="value">the ISO value, e.g. "2014-11-12"</param>
        /// <param name="comment">The comment field from the timex tag</param>
        /// <returns>The parsed datetime</returns>
        private static DateAndTime ParseAsDateTime(string value, string comment)
        {
            DateAndTime returnVal = new DateAndTime();
            
            // Try and split the time into date and time portions
            int timeSeparatorIndex = value.IndexOf('T');
            bool isTimeReferenceOnly = value.Equals("PRESENT_REF") || value.Equals("PAST_REF") || value.Equals("FUTURE_REF");

            // Now parse them individually
            if (timeSeparatorIndex >= 0 && !isTimeReferenceOnly)
            {
                returnVal.ParseDateValue(value.Substring(0, timeSeparatorIndex));
                returnVal.ParseTimeValue(value.Substring(timeSeparatorIndex + 1));
            }
            else
            {
                returnVal.ParseDateValue(value);
            }

            // Also try and parse flags from the comments
            if (returnVal.ParsedValues.Count > 0 && !string.IsNullOrEmpty(comment))
            {
                if (comment.Contains("ampm"))
                {
                    returnVal.SetValue(DateTimeParts.AmPmUnambiguous, "true");
                }

                if (comment.Contains("weekof"))
                {
                    returnVal.SetValue(DateTimeParts.WeekOfExpression, "true");
                }
            }

            return returnVal;
        }

        protected void SetValue(DateTimeParts key, string value)
        {
            if (ParsedValues.ContainsKey(key))
            {
                ParsedValues.Remove(key);
            }

            ParsedValues[key] = value;
        }

        /// <summary>
        /// Retrives one of the fields in the parsed values dictionary as a string
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <returns>The parsed value</returns>
        protected string GetValueAsString(DateTimeParts key)
        {
            string returnVal;
            if (ParsedValues.TryGetValue(key, out returnVal))
            {
                return returnVal;
            }

            return null;
        }

        /// <summary>
        /// Retrives one of the fields in the parsed values dictionary as an int, or null if the key is not found
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <returns>The parsed value</returns>
        protected int? GetValueAsInt(DateTimeParts key)
        {
            string temp;
            int returnVal;
            if (ParsedValues.TryGetValue(key, out temp) && int.TryParse(temp, out returnVal))
            {
                return returnVal;
            }

            return null;
        }

        /// <summary>
        /// Retrives one of the fields in the parsed values dictionary as a long, or null if the key is not found
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <returns>The parsed value</returns>
        protected long? GetValueAsLong(DateTimeParts key)
        {
            string temp;
            long returnVal;
            if (ParsedValues.TryGetValue(key, out temp) && long.TryParse(temp, out returnVal))
            {
                return returnVal;
            }

            return null;
        }
    }
}
