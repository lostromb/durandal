using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Durandal.Common.Time
{
    /// <summary>
    /// Extensions and parsers on the TimeSpan type.
    /// </summary>
    public static class TimeSpanExtensions
    {
        // Regexes aren't the most performant in this case but they get the job done
        private static readonly Regex PARSER_DHMSF = new Regex("^(-)?(\\d+)\\.(\\d{2}):(\\d{2}):(\\d{2})(?:\\.(\\d{1,7}))?$");
        private static readonly Regex PARSER_HMSF = new Regex("^(-)?(\\d{1,2}):(\\d{2}):(\\d{2})(?:\\.(\\d{1,7}))?$");
        private static readonly Regex PARSER_MSF = new Regex("^(-)?(\\d{1,2}):(\\d{2})(?:\\.(\\d{1,7}))?$");
        private static readonly Regex PARSER_SF = new Regex("^(-)?(\\d{1,2})(?:\\.(\\d{1,7}))?$");
        private static readonly Regex PARSER_TICKS = new Regex("^(-)?(\\d{3,20})$");
        private static readonly FastRandom RANDOM = new FastRandom();

        /// <summary>
        /// More precise implementation of TimeSpan.FromMilliseconds, when you want tick-level accuracy.
        /// The default method will truncate to lowest (?) whole millisecond for whatever reason.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds</param>
        /// <returns>A <see cref="TimeSpan" /> representing exactly this number of milliseconds</returns>
        public static TimeSpan TimeSpanFromMillisecondsPrecise(double milliseconds)
        {
            return TimeSpan.FromTicks((long)(milliseconds * TimeSpan.TicksPerMillisecond));
        }

        /// <summary>
        /// Attempts to parse a TimeSpan from a variable timespan string, in the format "d.hh:mm:ss.fffffff".
        /// Values that are zero can be omitted, so "5:00" = 5 minutes, "12:00:00" = 12 hours, "0.01" = 10 milliseconds, etc.
        /// If you are familiar with ffmpeg it is the same format used there.
        /// </summary>
        /// <param name="stringVal">The timespan string, in the format "d.hh:mm:ss.fffffff"</param>
        /// <param name="returnVal">The parsed timespan</param>
        /// <returns>True if parsing succeeded</returns>
        public static bool TryParseTimeSpan(string stringVal, out TimeSpan returnVal)
        {
            try
            {
                returnVal = ParseTimeSpan(stringVal);
                return true;
            }
            catch (Exception)
            {
                returnVal = TimeSpan.Zero;
                return false;
            }
        }

        /// <summary>
        /// Parses a TimeSpan from a variable timespan string, in the format "d.hh:mm:ss.fffffff".
        /// Values that are zero can be omitted, so "5:00" = 5 minutes, "12:00:00" = 12 hours, "0.01" = 10 milliseconds, etc.
        /// If you are familiar with ffmpeg it is the same format used there.
        /// </summary>
        /// <param name="stringVal">The timespan string, in the format "d.hh:mm:ss.fffffff"</param>
        /// <returns>The parsed timespan</returns>
        public static TimeSpan ParseTimeSpan(string stringVal)
        {
            bool negative = false;
            string days = null;
            string hours = null;
            string minutes = null;
            string seconds = null;
            string fraction = null;
            long ticks = 0;

            Match m = PARSER_SF.Match(stringVal);
            if (m.Success)
            {
                negative = m.Groups[1].Success;
                seconds = m.Groups[2].Value;
                if (m.Groups[3].Success)
                {
                    fraction = m.Groups[3].Value;
                }
            }
            else
            {
                m = PARSER_MSF.Match(stringVal);
                if (m.Success)
                {
                    negative = m.Groups[1].Success;
                    minutes = m.Groups[2].Value;
                    seconds = m.Groups[3].Value;
                    if (m.Groups[4].Success)
                    {
                        fraction = m.Groups[4].Value;
                    }
                }
                else
                {
                    m = PARSER_HMSF.Match(stringVal);
                    if (m.Success)
                    {
                        negative = m.Groups[1].Success;
                        hours = m.Groups[2].Value;
                        minutes = m.Groups[3].Value;
                        seconds = m.Groups[4].Value;
                        if (m.Groups[5].Success)
                        {
                            fraction = m.Groups[5].Value;
                        }
                    }
                    else
                    {
                        m = PARSER_DHMSF.Match(stringVal);
                        if (m.Success)
                        {
                            negative = m.Groups[1].Success;
                            days = m.Groups[2].Value;
                            hours = m.Groups[3].Value;
                            minutes = m.Groups[4].Value;
                            seconds = m.Groups[5].Value;
                            if (m.Groups[6].Success)
                            {
                                fraction = m.Groups[6].Value;
                            }
                        }
                        else
                        {
                            throw new FormatException("Could not parse the string \"" + stringVal + "\" as a valid TimeSpan");

                            // Uncomment this if we want to handle the ticks-as-string format where 1 second = "10000000"
                            //m = PARSER_TICKS.Match(stringVal);
                            //if (m.Success)
                            //{
                            //    negative = m.Groups[1].Success;
                            //    ticks = long.Parse(m.Groups[2].Value);
                            //}
                            //else
                            //{
                            //    throw new FormatException("Could not parse the string \"" + stringVal + "\" as a valid TimeSpan");
                            //}
                        }
                    }
                }
            }

            if (days != null)
            {
                ticks += long.Parse(days) * TimeSpan.TicksPerDay;
            }
            if (hours != null)
            {
                ticks += long.Parse(hours) * TimeSpan.TicksPerHour;
            }
            if (minutes != null)
            {
                long parsedMins = long.Parse(minutes);
                if (hours != null && parsedMins > 59)
                {
                    throw new FormatException("Could not parse the string \"" + stringVal + "\" as a valid TimeSpan: Minutes field " + parsedMins + " out of range");
                }

                ticks += parsedMins * TimeSpan.TicksPerMinute;
            }
            if (seconds != null)
            {
                long parsedSeconds = long.Parse(seconds);
                if (minutes != null && parsedSeconds > 59)
                {
                    throw new FormatException("Could not parse the string \"" + stringVal + "\" as a valid TimeSpan: Seconds field " + parsedSeconds + " out of range");
                }

                ticks += parsedSeconds * TimeSpan.TicksPerSecond;
            }
            if (fraction != null)
            {
                ticks += (long.Parse(fraction) * (long)Math.Pow(10, 7 - fraction.Length));
            }

            if (negative)
            {
                ticks = 0 - ticks;
            }

            return new TimeSpan(ticks);
        }

        /// <summary>
        /// Formats a TimeSpan object into a variable-length variation of the "c" invariant timespan string.
        /// Valid formats include ddd.hh:mm:ss.fffffff, hh:mm:ss.ffff, mm:ss, ss, ss.fff, mm:ss.fff, etc.
        /// </summary>
        /// <param name="span"></param>
        /// <returns></returns>
        public static string PrintTimeSpan(this TimeSpan span)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                PrintTimeSpan(span, pooledSb.Builder);
                return pooledSb.Builder.ToString();
            }
        }

        public static void PrintTimeSpan(this TimeSpan span, StringBuilder builder)
        {
            if (span < TimeSpan.Zero)
            {
                builder.Append('-');
                span = span.Negate();
            }

            bool firstField = true;
            if (span.Days > 0)
            {
                builder.Append(span.Days);
                builder.Append('.');
                firstField = false;
            }

            if (!firstField || span.Hours > 0)
            {
                if (firstField)
                {
                    builder.Append(span.Hours);
                }
                else
                {
                    builder.AppendFormat("{0:D2}", span.Hours);
                }

                firstField = false;
                builder.Append(':');
            }

            if (!firstField || span.Minutes > 0)
            {
                if (firstField)
                {
                    builder.Append(span.Minutes);
                }
                else
                {
                    builder.AppendFormat("{0:D2}", span.Minutes);
                }

                firstField = false;
                builder.Append(':');
            }

            if (firstField)
            {
                builder.Append(span.Seconds);
            }
            else
            {
                builder.AppendFormat("{0:D2}", span.Seconds);
            }

            long fractionalTicks = span.Ticks % TimeSpan.TicksPerSecond;
            if (fractionalTicks > 0)
            {
                builder.Append('.');
                int fractionBegin = builder.Length;
                builder.AppendFormat("{0:D7}", fractionalTicks);
                // Trim trailing zeroes from the fraction as necessary
                while (builder[builder.Length - 1] == '0')
                {
                    builder.Remove(builder.Length - 1, 1);
                }
            }
        }

        /// <summary>
        /// Randomly varies this timespan by a certain factor, making it longer or shorter, but in a way
        /// that a series of variances over time averages out to the original span.
        /// </summary>
        /// <param name="span">The time span to be altered.</param>
        /// <param name="varianceAmount">The amount of variance to apply, from 0.0 to 1.0</param>
        /// <returns>A copy of this TimeSpan with the random variance applied.</returns>
        public static TimeSpan Vary(this TimeSpan span, double varianceAmount)
        {
            if (varianceAmount == 0)
            {
                return span;
            }
            else if (varianceAmount < 0 || varianceAmount > 1)
            {
                throw new ArgumentOutOfRangeException("Variance must be between 0 and 1");
            }

            return new TimeSpan(span.Ticks + (long)(span.Ticks * varianceAmount * ((RANDOM.NextDouble() * 2) - 1)));
        }
    }
}
