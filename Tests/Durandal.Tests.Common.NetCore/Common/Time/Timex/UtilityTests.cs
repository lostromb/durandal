using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Calendar;
using Durandal.Common.Time.Timex.Client;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.Time.Timex.Constants;

namespace Durandal.Tests.Common.Time.Timex
{
    [TestClass]
    public class UtilityTests
    {
        [TestMethod]
        public void TestTimexGetFirstDayOfIsoWeek()
        {
            DateTime result = TimexHelpers.GetFirstDayOfIsoWeek(2017, 17);
            Assert.AreEqual("2017-04-24", result.ToString("yyyy-MM-dd"));
        }

        [TestMethod]
        public void TestTimexGetFirstDayOfIsoWeek2()
        {
            DateTime result = TimexHelpers.GetFirstDayOfIsoWeek(2000, 1);
            Assert.AreEqual("2000-01-03", result.ToString("yyyy-MM-dd"));
        }

        [TestMethod]
        public void TestTimexGetFirstDayOfIsoWeek3()
        {
            DateTime result = TimexHelpers.GetFirstDayOfIsoWeek(2002, 1);
            Assert.AreEqual("2001-12-31", result.ToString("yyyy-MM-dd"));
        }

        [TestMethod]
        public void TestTimexGetIso8601WeekOfYear()
        {
            int weekYear;
            DateTime time = new DateTime(2012, 1, 1);
            int week = TimexHelpers.GetIso8601WeekOfYear(time, out weekYear);
            Assert.AreEqual(52, week);
            Assert.AreEqual(2011, weekYear);
        }

        [TestMethod]
        public void TestTimexGetIso8601WeekOfYear2()
        {
            int weekYear;
            DateTime time = new DateTime(2012, 5, 1);
            int week = TimexHelpers.GetIso8601WeekOfYear(time, out weekYear);
            Assert.AreEqual(18, week);
            Assert.AreEqual(2012, weekYear);
        }

        [TestMethod]
        public void TestTimexGetIso8601WeekOfYear3()
        {
            int weekYear;
            DateTime time = new DateTime(2013, 12, 31);
            int week = TimexHelpers.GetIso8601WeekOfYear(time, out weekYear);
            Assert.AreEqual(1, week);
            Assert.AreEqual(2014, weekYear);
        }

        [TestMethod]
        public void TestTimexGetIso8601DayOfWeek()
        {
            Assert.AreEqual(4, TimexHelpers.GetIso8601DayOfWeek(new DateTime(2017, 4, 27)));
        }

        [TestMethod]
        public void TestTimexGetIso8601DayOfWeek2()
        {
            Assert.AreEqual(1, TimexHelpers.GetIso8601DayOfWeek(new DateTime(2017, 4, 24)));
        }

        [TestMethod]
        public void TestTimexGetIso8601DayOfWeek3()
        {
            Assert.AreEqual(7, TimexHelpers.GetIso8601DayOfWeek(new DateTime(2017, 4, 23)));
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeYear()
        {
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2014", "Date").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange();
            Assert.IsNotNull(range);
            Assert.AreEqual(new DateTime(2014, 1, 1, 0, 0, 0), range.Start);
            Assert.AreEqual(new DateTime(2015, 1, 1, 0, 0, 0), range.End);
            Assert.AreEqual(TemporalUnit.Year, range.Granularity);
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeMonth()
        {
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2014-06", "Date").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange();
            Assert.IsNotNull(range);
            Assert.AreEqual(new DateTime(2014, 6, 1, 0, 0, 0), range.Start);
            Assert.AreEqual(new DateTime(2014, 7, 1, 0, 0, 0), range.End);
            Assert.AreEqual(TemporalUnit.Month, range.Granularity);
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeDay()
        {
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2016-05-05", "Date").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange();
            Assert.IsNotNull(range);
            Assert.AreEqual(new DateTime(2016, 5, 5, 0, 0, 0), range.Start);
            Assert.AreEqual(new DateTime(2016, 5, 6, 0, 0, 0), range.End);
            Assert.AreEqual(TemporalUnit.Day, range.Granularity);
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeWeek()
        {
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2016-W32", "Date").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange(LocalizedWeekDefinition.StandardWeekDefinition);
            Assert.IsNotNull(range);
            Assert.AreEqual(new DateTime(2016, 8, 7, 0, 0, 0), range.Start);
            Assert.AreEqual(new DateTime(2016, 8, 14, 0, 0, 0), range.End);
            Assert.AreEqual(TemporalUnit.Week, range.Granularity);
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeWeekend()
        {
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2016-W32-WE", "Date").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange(LocalizedWeekDefinition.StandardWeekDefinition);
            Assert.IsNotNull(range);
            Assert.AreEqual(new DateTime(2016, 8, 13, 0, 0, 0), range.Start);
            Assert.AreEqual(new DateTime(2016, 8, 15, 0, 0, 0), range.End);
            Assert.AreEqual(TemporalUnit.Weekend, range.Granularity);
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeHour()
        {
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2016-05-11T04", "Time").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange();
            Assert.IsNotNull(range);
            Assert.AreEqual(new DateTime(2016, 5, 11, 4, 0, 0), range.Start);
            Assert.AreEqual(new DateTime(2016, 5, 11, 5, 0, 0), range.End);
            Assert.AreEqual(TemporalUnit.Hour, range.Granularity);
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeMinute()
        {
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2016-05-11T04:01", "Time").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange();
            Assert.IsNotNull(range);
            Assert.AreEqual(new DateTime(2016, 5, 11, 4, 1, 0), range.Start);
            Assert.AreEqual(new DateTime(2016, 5, 11, 4, 2, 0), range.End);
            Assert.AreEqual(TemporalUnit.Minute, range.Granularity);
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeSecond()
        {
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2016-05-11T04:01:33", "Time").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange();
            Assert.IsNotNull(range);
            Assert.AreEqual(new DateTime(2016, 5, 11, 4, 1, 33), range.Start);
            Assert.AreEqual(new DateTime(2016, 5, 11, 4, 1, 34), range.End);
            Assert.AreEqual(TemporalUnit.Second, range.Granularity);
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeCutoffYear()
        {
            DateTime cutoffTime = new DateTime(2014, 6, 7, 0, 0, 0);
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2014", "Date").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange(cutoffTime);
            Assert.IsNotNull(range);
            Assert.AreEqual(new DateTime(2014, 6, 7, 0, 0, 0), range.Start);
            Assert.AreEqual(new DateTime(2015, 1, 1, 0, 0, 0), range.End);
            Assert.AreEqual(TemporalUnit.Year, range.Granularity);
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeCutoffDayFuture()
        {
            DateTime cutoffTime = new DateTime(2016, 5, 7, 15, 0, 0);
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2016-05-07", "Date").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange(cutoffTime, Normalization.Future);
            Assert.IsNotNull(range);
            Assert.AreEqual(new DateTime(2016, 5, 7, 15, 0, 0), range.Start);
            Assert.AreEqual(new DateTime(2016, 5, 8, 0, 0, 0), range.End);
            Assert.AreEqual(TemporalUnit.Day, range.Granularity);
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeCutoffDayPast()
        {
            DateTime cutoffTime = new DateTime(2016, 5, 7, 15, 0, 0);
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2016-05-07", "Date").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange(cutoffTime, Normalization.Past);
            Assert.IsNotNull(range);
            Assert.AreEqual(new DateTime(2016, 5, 7, 0, 0, 0), range.Start);
            Assert.AreEqual(new DateTime(2016, 5, 7, 15, 0, 0), range.End);
            Assert.AreEqual(TemporalUnit.Day, range.Granularity);
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangeWeekOf()
        {
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2016-10-11", "Date", "0", "weekof").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange();
            Assert.IsNotNull(range);
            Assert.AreEqual(TemporalUnit.Week, range.Granularity);
            Assert.AreEqual("2016-10-09T00:00:00", range.Start.ToString("s"));
            Assert.AreEqual("2016-10-16T00:00:00", range.End.ToString("s"));
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangePresentRef()
        {
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("PRESENT_REF", "Time").AsDateAndTime();
            Assert.IsNotNull(time);
            DateTime referenceTime = new DateTime(2016, 11, 14, 7, 11, 22);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange(referenceTime, Normalization.Future);
            Assert.IsNotNull(range);
            Assert.AreEqual(TemporalUnit.Second, range.Granularity);
            Assert.AreEqual("2016-11-14T07:11:22", range.Start.ToString("s"));
            Assert.AreEqual("2016-11-14T07:11:23", range.End.ToString("s"));
        }

        [TestMethod]
        public void TestTimexInterpretNaturalRangePartOfDay()
        {
            DateAndTime time = Durandal.Common.Time.Timex.Client.TimexValue.Parse("2016-10-11TMO", "Time").AsDateAndTime();
            Assert.IsNotNull(time);
            SimpleDateTimeRange range = time.InterpretAsNaturalTimeRange();
            Assert.IsNotNull(range);
            Assert.AreEqual(TemporalUnit.Hour, range.Granularity);
            Assert.AreEqual("2016-10-11T08:00:00", range.Start.ToString("s"));
            Assert.AreEqual("2016-10-11T12:00:00", range.End.ToString("s"));
        }

        [TestMethod]
        public void TestTimexFirstOccurrenceDay()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-XX-XX", "Set", "0", "", "1day", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(14, firstOccurrence.GetDayOfMonth().GetValueOrDefault(-1));
            Assert.AreEqual(6, firstOccurrence.GetMonth().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrenceHour()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-XX-XXTXX", "Set", "0", "", "1hour", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(13, firstOccurrence.GetHour().GetValueOrDefault(-1));
            Assert.AreEqual(14, firstOccurrence.GetDayOfMonth().GetValueOrDefault(-1));
            Assert.AreEqual(6, firstOccurrence.GetMonth().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrenceMinute()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-XX-XXTXX:XX", "Set", "0", "", "1minute", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(0, firstOccurrence.GetMinute().GetValueOrDefault(-1));
            Assert.AreEqual(13, firstOccurrence.GetHour().GetValueOrDefault(-1));
            Assert.AreEqual(14, firstOccurrence.GetDayOfMonth().GetValueOrDefault(-1));
            Assert.AreEqual(6, firstOccurrence.GetMonth().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrenceMonth()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-XX", "Set", "0", "", "1month", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(6, firstOccurrence.GetMonth().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrenceWeek()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-WXX", "Set", "0", "", "1week", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(24, firstOccurrence.GetWeekOfYear().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrence6PM()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-XX-XX-T18", "Set", "0", "", "1day", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(18, firstOccurrence.GetHour().GetValueOrDefault(-1));
            Assert.AreEqual(14, firstOccurrence.GetDayOfMonth().GetValueOrDefault(-1));
            Assert.AreEqual(6, firstOccurrence.GetMonth().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrence6AM()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-XX-XX-T06", "Set", "0", "", "1day", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(6, firstOccurrence.GetHour().GetValueOrDefault(-1));
            Assert.AreEqual(15, firstOccurrence.GetDayOfMonth().GetValueOrDefault(-1));
            Assert.AreEqual(6, firstOccurrence.GetMonth().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrenceFriday()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-WXX-5", "Set", "0", "", "1week", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(17, firstOccurrence.GetDayOfMonth().GetValueOrDefault(-1));
            Assert.AreEqual(6, firstOccurrence.GetMonth().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrenceMornings()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-XX-XXTMO", "Set", "0", "", "1day", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(PartOfDay.Morning, firstOccurrence.GetPartOfDay());
            Assert.AreEqual(15, firstOccurrence.GetDayOfMonth().GetValueOrDefault(-1));
            Assert.AreEqual(6, firstOccurrence.GetMonth().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrenceFridayEvenings()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-WXX-5TEV", "Set", "0", "", "1week", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(PartOfDay.Evening, firstOccurrence.GetPartOfDay());
            Assert.AreEqual(17, firstOccurrence.GetDayOfMonth().GetValueOrDefault(-1));
            Assert.AreEqual(6, firstOccurrence.GetMonth().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrenceFriday8AM()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-WXX-5T08", "Set", "0", "", "1week", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(8, firstOccurrence.GetHour().GetValueOrDefault(-1));
            Assert.AreEqual(17, firstOccurrence.GetDayOfMonth().GetValueOrDefault(-1));
            Assert.AreEqual(6, firstOccurrence.GetMonth().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrenceWeekend()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-WXX-WE", "Set", "0", "", "1week", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(PartOfWeek.Weekend, firstOccurrence.GetPartOfWeek());
            Assert.AreEqual(24, firstOccurrence.GetWeekOfYear().GetValueOrDefault(-1));
            Assert.AreEqual(2016, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        [TestMethod]
        public void TestTimexFirstOccurrenceEveryDayInMarch()
        {
            TimexContext interpretationContext = new TimexContext()
            {
                UseInference = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2016, 6, 14, 13, 0, 0)
            };

            Recurrence recurrence = Durandal.Common.Time.Timex.Client.TimexValue.Parse("XXXX-03-XX", "Set", "0", "", "1day", "1").AsRecurrence();
            Assert.IsNotNull(recurrence);
            DateAndTime firstOccurrence = recurrence.GetFirstOccurrence(interpretationContext);
            Assert.IsNotNull(firstOccurrence);
            Assert.AreEqual(1, firstOccurrence.GetDayOfMonth().GetValueOrDefault(-1));
            Assert.AreEqual(3, firstOccurrence.GetMonth().GetValueOrDefault(-1));
            Assert.AreEqual(2017, firstOccurrence.GetYear().GetValueOrDefault(-1));
        }

        private static TimexContext GetDefaultMergeContext()
        {
            return new TimexContext()
            {
                AmPmInferenceCutoff = 7,
                IncludeCurrentTimeInPastOrFuture = true,
                Normalization = Normalization.Future,
                ReferenceDateTime = new DateTime(2012, 5, 11, 17, 22, 36),
                TemporalType = TemporalType.All,
                UseInference = false,
                WeekdayLogicType = WeekdayLogic.SimpleOffset
            };
        }

        [TestMethod]
        public void TestTimexCanMergeDayAndTime()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "DD", "20" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "hh", "15" }
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("XXXX-XX-20T15", merged[0].FormatValue());
        }

        [TestMethod]
        public void TestTimexCanMergeHourAndPartOfDay()
        {
            TimexContext context = GetDefaultMergeContext();
            context.UseInference = true;

            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "hh", "8" },
                    { "AMPM", "not_specified" }
                }, context),
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "POD", "EV" }
                }, context)
            });

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("2012-05-11T20", merged[0].FormatValue());
        }

        [TestMethod]
        public void TestTimexCanMergeDayAndPartOfDay()
        {
            TimexContext context = GetDefaultMergeContext();
            context.UseInference = true;

            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "DD", "15" },
                }, context),
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "POD", "MI" }
                }, context)
            });

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("2012-05-15TMI", merged[0].FormatValue());
        }

        [TestMethod]
        public void TestTimexCannotMergeMonthAndPartOfDay()
        {
            TimexContext context = GetDefaultMergeContext();
            context.UseInference = true;

            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "MM", "3" },
                }, context),
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "POD", "MI" }
                }, context)
            });

            Assert.AreEqual(2, merged.Count);
        }

        [TestMethod]
        public void TestTimexCanMergeDayAndMonth()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "DD", "20" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "MM", "2" }
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("XXXX-02-20", merged[0].FormatValue());
        }

        [TestMethod]
        public void TestTimexCannotMergeDecade()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "YYYY", "XX80" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "MM", "2" }
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(2, merged.Count);
        }

        [TestMethod]
        public void TestTimexCannotMergeWeekOfHour()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "hh", "15" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "MM", "2" },
                    { "DD", "10" },
                    { "WEEKOF", "true" }
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(2, merged.Count);
        }
        
        [TestMethod]
        public void TestTimexCanMergeWeekOfYear()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "YYYY", "2016" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "MM", "2" },
                    { "DD", "10" },
                    { "WEEKOF", "true" }
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("2016-02-10", merged[0].FormatValue());
            Assert.AreEqual("weekof", merged[0].FormatComment());
        }

        [TestMethod]
        public void TestTimexCanMergeWeekYear()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "YYYY", "2016" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "ww", "28" },
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("2016-W28", merged[0].FormatValue());
        }

        [TestMethod]
        public void TestTimexCanMergeYearMonth()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "YYYY", "2016" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "MM", "11" },
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("2016-11", merged[0].FormatValue());
        }

        [TestMethod]
        public void TestTimexCannotMergeYearDay()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "YYYY", "2016" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "DD", "22" },
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(2, merged.Count);
        }

        [TestMethod]
        public void TestTimexCannotMergeReference()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "YYYY", "2016" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "REF", "PAST_REF" },
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(2, merged.Count);
        }

        [TestMethod]
        public void TestTimexCannotMergeDayOfMonthAndDayOfWeek()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "D", "5" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "DD", "12" },
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(2, merged.Count);
        }

        [TestMethod]
        public void TestTimexCannotMergeTwoOffsets()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "OFFSET", "1" },
                    { "OFFSET_UNIT", "day" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "OFFSET", "2" },
                    { "OFFSET_UNIT", "hour" }
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(2, merged.Count);
        }

        [TestMethod]
        public void TestTimexCannotMergeOffset5PMTomorrow2017()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "hh", "17" },
                    { "YYYY", "2017" },
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "OFFSET", "1" },
                    { "OFFSET_UNIT", "day" }
                }, GetDefaultMergeContext())
            });
            
            Assert.AreEqual(2, merged.Count);
        }

        [TestMethod]
        public void TestTimexCannotMergeOffset5PMTomorrow()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Date, new Dictionary<string, string>()
                {
                    { "hh", "17" },
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "OFFSET", "1" },
                    { "OFFSET_UNIT", "day" }
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("2012-05-12T17", merged[0].FormatValue());
        }

        [TestMethod]
        public void TestTimexCannotMergeTimeAndDuration()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "hh", "15" },
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Duration, new Dictionary<string, string>()
                {
                    { "DURATION", "1" },
                    { "DURATION_UNIT", "hour" }
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(2, merged.Count);
        }

        [TestMethod]
        public void TestTimexCannotMergeTimeAndRecurrence()
        {
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(new List<ExtendedDateTime>()
            {
                ExtendedDateTime.Create(TemporalType.Set, new Dictionary<string, string>()
                {
                    { "FREQ", "1day" },
                    { "QUANT", "2" }
                }, GetDefaultMergeContext()),
                ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "hh", "15" },
                }, GetDefaultMergeContext())
            });

            Assert.AreEqual(2, merged.Count);
        }

        [TestMethod]
        public void TestTimexCannotMergeNulls()
        {
            ExtendedDateTime dummy = ExtendedDateTime.Create(TemporalType.Time, new Dictionary<string, string>()
                {
                    { "hh", "15" },
                }, GetDefaultMergeContext());

            Assert.IsFalse(DateTimeProcessors.CanBeMerged(null, null));
            Assert.IsFalse(DateTimeProcessors.CanBeMerged(dummy, null));
            Assert.IsFalse(DateTimeProcessors.CanBeMerged(null, dummy));
        }
    }
}
