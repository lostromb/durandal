namespace Durandal.Tests.Common.Time.Timex
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Durandal.Common.Time.Timex;
    using Durandal.Common.Time.Timex.Calendar;
    using Durandal.Common.Time.Timex.Client;
    using Durandal.Common.Time.Timex.Enums;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable 0618

    [TestClass]
    public class ClientParserTests
    {
        private static DateAndTime ParseDateAndTime(string timexTag)
        {
            var timex = TimexValue.ParseXmlTag(timexTag);
            Assert.IsNotNull(timex);
            Assert.IsTrue(timex.GetTemporalType().HasFlag(TemporalType.Date) || timex.GetTemporalType().HasFlag(TemporalType.Time));
            var returnVal = timex.AsDateAndTime();
            Assert.IsNotNull(returnVal);
            return returnVal;
        }

        private static Duration ParseDuration(string timexTag)
        {
            var timex = TimexValue.ParseXmlTag(timexTag);
            Assert.IsNotNull(timex);
            Assert.AreEqual(TemporalType.Duration, timex.GetTemporalType());
            var returnVal = timex.AsDuration();
            Assert.IsNotNull(returnVal);
            return returnVal;
        }

        private static Recurrence ParseRecurrence(string timexTag)
        {
            var timex = TimexValue.ParseXmlTag(timexTag);
            Assert.IsNotNull(timex);
            Assert.AreEqual(TemporalType.Set, timex.GetTemporalType());
            var returnVal = timex.AsRecurrence();
            Assert.IsNotNull(returnVal);
            return returnVal;
        }

        #region General Timex tests

        [TestMethod]
        public void TestTimexParseModifier()
        {
            var time = TimexValue.ParseXmlTag("<TIMEX3 value=\"T12\" mod=\"MID\" />");
            Assert.AreEqual(Modifier.Mid, time.Mod);
        }

        [TestMethod]
        public void TestTimexParseInvalidModifier()
        {
            var time = TimexValue.ParseXmlTag("<TIMEX3 value=\"T12\" mod=\"MOOOOOOO\" />");
            Assert.AreEqual(Modifier.None, time.Mod);
        }

        [TestMethod]
        public void TestTimexParseQuantity()
        {
            Recurrence recurrence = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"P1W\" quant=\"EACH\" />");
            Assert.AreEqual(1, recurrence.GetQuantity());

            recurrence = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"P1W\" quant=\"EVERY\" />");
            Assert.AreEqual(1, recurrence.GetQuantity());
        }

        [TestMethod]
        public void TestTimexParseTid()
        {
            var time = TimexValue.ParseXmlTag("<TIMEX3 value=\"T12\" tid=\"99\" />");
            Assert.AreEqual("99", time.Tid);
        }

        [TestMethod]
        public void TestTimexParseTidNonInt()
        {
            var time = TimexValue.ParseXmlTag("<TIMEX3 value=\"T12\" tid=\"time_99\" />");
            Assert.AreEqual("time_99", time.Tid);
        }

        [TestMethod]
        public void TestTimexParseMidday()
        {
            var time = TimexValue.ParseXmlTag("<TIMEX3 tid =\"1\" type=\"Time\" value=\"TMI\">Midday</TIMEX3>");
            DateAndTime dt = time.AsDateAndTime();
            Assert.AreEqual(PartOfDay.MidDay, dt.GetPartOfDay());
        }

        [TestMethod]
        public void TestTimexParseLastWeek()
        {
            var time = TimexValue.ParseXmlTag("<TIMEX3 tid =\"1\" type=\"Date\" value=\"2018-W41\">Last week</TIMEX3>");
            DateAndTime dt = time.AsDateAndTime();
            SimpleDateTimeRange x = dt.InterpretAsNaturalTimeRange(LocalizedWeekDefinition.StandardWeekDefinition);
            Assert.AreEqual(TemporalUnit.Week, x.Granularity);
            Assert.AreEqual("2018-10-07T00:00:00", x.Start.ToString("yyyy-MM-ddTHH:mm:ss"));
            Assert.AreEqual("2018-10-14T00:00:00", x.End.ToString("yyyy-MM-ddTHH:mm:ss"));
        }

        #endregion

        #region Time parser tests

        [TestMethod]
        public void TestTimexParseHourField()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"T12\" />");
            Assert.AreEqual(12, time.GetHour());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseInvalidHourField()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"T1H:09\" />");
            Assert.IsNull(time.GetHour());
            Assert.AreEqual(9, time.GetMinute());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseMinuteField()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"T01:11\" />");
            Assert.AreEqual(1, time.GetHour());
            Assert.AreEqual(11, time.GetMinute());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseInvalidMinuteField()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"T01:g1\" />");
            Assert.AreEqual(1, time.GetHour());
            Assert.IsNull(time.GetMinute());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseSecondField()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"T01:11:45\" />");
            Assert.AreEqual(1, time.GetHour());
            Assert.AreEqual(11, time.GetMinute());
            Assert.AreEqual(45, time.GetSecond());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseInvalidSecondField()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"T01:11:z5\" />");
            Assert.AreEqual(1, time.GetHour());
            Assert.AreEqual(11, time.GetMinute());
            Assert.IsNull(time.GetSecond());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseSecondFieldWithTimezone()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"T01:11:45+GMT\" />");
            Assert.AreEqual(1, time.GetHour());
            Assert.AreEqual(11, time.GetMinute());
            Assert.AreEqual(45, time.GetSecond());
            Assert.AreEqual("+GMT", time.GetTimeZone());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseSecondFieldWithTimezoneOffset()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"T01:11:45-800\" />");
            Assert.AreEqual(1, time.GetHour());
            Assert.AreEqual(11, time.GetMinute());
            Assert.AreEqual(45, time.GetSecond());
            Assert.AreEqual("-800", time.GetTimeZone());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParsePartOfDay()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"TDT\" />");
            Assert.AreEqual(PartOfDay.DayTime, time.GetPartOfDay());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseInvalidPartOfDay()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012-06-06TZZ\" />");
            Assert.AreEqual(PartOfDay.None, time.GetPartOfDay());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("2012-06-06T00:00:00", convertedTime.Value.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseTimeAmpmFlag()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"T07\" comment=\"unspecified_ampm\" />");
            Assert.AreEqual(7, time.GetHour());
            Assert.AreEqual(true, time.GetAmPmAmbiguousFlag());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseTimeWithVagueDate()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"XXXX-XX-11T09\" />");
            Assert.AreEqual(11, time.GetDayOfMonth());
            Assert.AreEqual(9, time.GetHour());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseTimeWithVagueHour()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"XXXX-XX-11TXX:10\" />");
            Assert.IsNull(time.GetYear());
            Assert.IsNull(time.GetMonth());
            Assert.AreEqual(11, time.GetDayOfMonth());
            Assert.IsNull(time.GetHour());
            Assert.AreEqual(10, time.GetMinute());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseTimeWithVagueMinute()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"XXXX-XX-11TXX:XX:10\" />");
            Assert.IsNull(time.GetYear());
            Assert.IsNull(time.GetMonth());
            Assert.AreEqual(11, time.GetDayOfMonth());
            Assert.IsNull(time.GetHour());
            Assert.IsNull(time.GetMinute());
            Assert.AreEqual(10, time.GetSecond());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseFathersDay11AM()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"XXXX-06-W03-7T11:00\" />");
            Assert.IsNull(time.GetYear());
            Assert.AreEqual(6, time.GetMonth());
            Assert.IsNull(time.GetWeekOfYear());
            Assert.AreEqual(3, time.GetWeekOfMonth());
            Assert.AreEqual(7, time.GetDayOfWeek());
            Assert.AreEqual(11, time.GetHour());
            Assert.AreEqual(0, time.GetMinute());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        #endregion

        #region Date parser tests

        [TestMethod]
        public void TestTimexParseYear()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012\" />");
            Assert.AreEqual(2012, time.GetYear());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("2012-01-01T00:00:00", convertedTime.Value.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseYearMonth()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012-11\" />");
            Assert.AreEqual(2012, time.GetYear());
            Assert.AreEqual(11, time.GetMonth());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("2012-11-01T00:00:00", convertedTime.Value.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseYearMonthDay()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012-09-20\" />");
            Assert.AreEqual(2012, time.GetYear());
            Assert.AreEqual(9, time.GetMonth());
            Assert.AreEqual(20, time.GetDayOfMonth());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("2012-09-20T00:00:00", convertedTime.Value.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseYearMonthDayWeekof()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012-09-22\" comment=\"weekof\" />");
            Assert.AreEqual(2012, time.GetYear());
            Assert.AreEqual(9, time.GetMonth());
            Assert.AreEqual(22, time.GetDayOfMonth());
            Assert.AreEqual(true, time.GetWeekOfFlag());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("2012-09-22T00:00:00", convertedTime.Value.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseYearWeek()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012-W14\" />");
            Assert.AreEqual(2012, time.GetYear());
            Assert.AreEqual(14, time.GetWeekOfYear());
        }

        [TestMethod]
        public void TestTimexParseYearWeekend()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012-W14-WE\" />");
            Assert.AreEqual(2012, time.GetYear());
            Assert.AreEqual(14, time.GetWeekOfYear());
            Assert.AreEqual(PartOfWeek.Weekend, time.GetPartOfWeek());
        }

        [TestMethod]
        public void TestTimexParseYearQuarter()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012-Q2\" />");
            Assert.AreEqual(2012, time.GetYear());
            Assert.AreEqual("Q2", time.GetPartOfYear());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("2012-01-01T00:00:00", convertedTime.Value.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseYearHalf()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012-H2\" />");
            Assert.AreEqual(2012, time.GetYear());
            Assert.AreEqual("H2", time.GetPartOfYear());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("2012-01-01T00:00:00", convertedTime.Value.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseYearSeason()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012-WI\" />");
            Assert.AreEqual(2012, time.GetYear());
            Assert.AreEqual(Season.Winter, time.GetSeason());
        }

        [TestMethod]
        public void TestTimexParseYearInvalidQuarter()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012-QA\" />");
            Assert.AreEqual(2012, time.GetYear());
            Assert.IsNull(time.GetPartOfYear());
        }

        [TestMethod]
        public void TestTimexParseInvalidPartOfYear()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2012-WE\" />");
            Assert.AreEqual(2012, time.GetYear());
            Assert.IsNull(time.GetPartOfYear());
        }

        [TestMethod]
        public void TestTimexParseVagueYear()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"XXXX-11\" />");
            Assert.IsNull(time.GetYear());
            Assert.AreEqual(11, time.GetMonth());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseVagueMonth()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"XXXX-XX-30\" />");
            Assert.IsNull(time.GetYear());
            Assert.IsNull(time.GetMonth());
            Assert.AreEqual(30, time.GetDayOfMonth());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseCombinedDateHour()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2011-07-06T17\" />");
            Assert.AreEqual(2011, time.GetYear());
            Assert.AreEqual(07, time.GetMonth());
            Assert.AreEqual(06, time.GetDayOfMonth());
            Assert.AreEqual(17, time.GetHour());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("2011-07-06T17:00:00", convertedTime.Value.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseCombinedDateHourMinute()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2011-07-06T17:14\" />");
            Assert.AreEqual(2011, time.GetYear());
            Assert.AreEqual(07, time.GetMonth());
            Assert.AreEqual(06, time.GetDayOfMonth());
            Assert.AreEqual(17, time.GetHour());
            Assert.AreEqual(14, time.GetMinute());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("2011-07-06T17:14:00", convertedTime.Value.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseCombinedDateHourMinuteSecond()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2011-07-06T17:14:59\" />");
            Assert.AreEqual(2011, time.GetYear());
            Assert.AreEqual(07, time.GetMonth());
            Assert.AreEqual(06, time.GetDayOfMonth());
            Assert.AreEqual(17, time.GetHour());
            Assert.AreEqual(14, time.GetMinute());
            Assert.AreEqual(59, time.GetSecond());
            DateTime? convertedTime = time.TryConvertIntoCSharpDateTime();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("2011-07-06T17:14:59", convertedTime.Value.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseTimeReference()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"PRESENT_REF\" />");
            Assert.AreEqual(DateTimeReference.Present, time.GetReference());
        }

        // This can happen for phrases like "earlier today", so make sure we can handle date + reference combined
        [TestMethod]
        public void TestTimexParseDateReference()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2016-10-11TPAST_REF\" />");
            Assert.AreEqual(2016, time.GetYear());
            Assert.AreEqual(10, time.GetMonth());
            Assert.AreEqual(11, time.GetDayOfMonth());
            Assert.AreEqual(DateTimeReference.Past, time.GetReference());
        }

        [TestMethod]
        public void TestTimexParseDateWeekOf()
        {
            var time = ParseDateAndTime("<TIMEX3 value=\"2016-10-11\" comment=\"weekof\"/>");
            Assert.AreEqual(2016, time.GetYear());
            Assert.AreEqual(10, time.GetMonth());
            Assert.AreEqual(11, time.GetDayOfMonth());
            Assert.AreEqual(true, time.GetWeekOfFlag());
        }

        #endregion

        #region Duration parser tests

        [TestMethod]
        public void TestTimexParseDurationYear()
        {
            var dur = ParseDuration("<TIMEX3 value=\"P3Y\" />");
            var components = dur.DurationComponents;
            Assert.IsTrue(components.ContainsKey(TemporalUnit.Year));
            Assert.IsTrue(components[TemporalUnit.Year].HasValue);
            Assert.AreEqual(3, components[TemporalUnit.Year].Value);
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("1095.00:00:00", convertedTime.Value.ToString());
            DateTime offsetTime = dur.ApplyOffsetToDateTime(new DateTime(2012, 1, 1, 0, 0, 0));
            Assert.AreEqual("2015-01-01T00:00:00", offsetTime.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseDurationMonth()
        {
            var dur = ParseDuration("<TIMEX3 value=\"P3M\" />");
            var components = dur.DurationComponents;
            Assert.IsTrue(components.ContainsKey(TemporalUnit.Month));
            Assert.IsTrue(components[TemporalUnit.Month].HasValue);
            Assert.AreEqual(3, components[TemporalUnit.Month].Value);
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("90.00:00:00", convertedTime.Value.ToString());
            DateTime offsetTime = dur.ApplyOffsetToDateTime(new DateTime(2012, 1, 1, 0, 0, 0));
            Assert.AreEqual("2012-04-01T00:00:00", offsetTime.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseDurationWeek()
        {
            var dur = ParseDuration("<TIMEX3 value=\"P3W\" />");
            var components = dur.DurationComponents;
            Assert.IsTrue(components.ContainsKey(TemporalUnit.Week));
            Assert.IsTrue(components[TemporalUnit.Week].HasValue);
            Assert.AreEqual(3, components[TemporalUnit.Week].Value);
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("21.00:00:00", convertedTime.Value.ToString());
            DateTime offsetTime = dur.ApplyOffsetToDateTime(new DateTime(2012, 1, 1, 0, 0, 0));
            Assert.AreEqual("2012-01-22T00:00:00", offsetTime.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseDurationDay()
        {
            var dur = ParseDuration("<TIMEX3 value=\"P3D\" />");
            var components = dur.DurationComponents;
            Assert.IsTrue(components.ContainsKey(TemporalUnit.Day));
            Assert.IsTrue(components[TemporalUnit.Day].HasValue);
            Assert.AreEqual(3, components[TemporalUnit.Day].Value);
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("3.00:00:00", convertedTime.Value.ToString());
            DateTime offsetTime = dur.ApplyOffsetToDateTime(new DateTime(2012, 1, 1, 0, 0, 0));
            Assert.AreEqual("2012-01-04T00:00:00", offsetTime.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseDurationHour()
        {
            var dur = ParseDuration("<TIMEX3 value=\"PT3H\" />");
            var components = dur.DurationComponents;
            Assert.IsTrue(components.ContainsKey(TemporalUnit.Hour));
            Assert.IsTrue(components[TemporalUnit.Hour].HasValue);
            Assert.AreEqual(3, components[TemporalUnit.Hour].Value);
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("03:00:00", convertedTime.Value.ToString());
            DateTime offsetTime = dur.ApplyOffsetToDateTime(new DateTime(2012, 1, 1, 0, 0, 0));
            Assert.AreEqual("2012-01-01T03:00:00", offsetTime.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseDurationMinute()
        {
            var dur = ParseDuration("<TIMEX3 value=\"PT3M\" />");
            var components = dur.DurationComponents;
            Assert.IsTrue(components.ContainsKey(TemporalUnit.Minute));
            Assert.IsTrue(components[TemporalUnit.Minute].HasValue);
            Assert.AreEqual(3, components[TemporalUnit.Minute].Value);
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("00:03:00", convertedTime.Value.ToString());
            DateTime offsetTime = dur.ApplyOffsetToDateTime(new DateTime(2012, 1, 1, 0, 0, 0));
            Assert.AreEqual("2012-01-01T00:03:00", offsetTime.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseDurationSecond()
        {
            var dur = ParseDuration("<TIMEX3 value=\"PT3S\" />");
            var components = dur.DurationComponents;
            Assert.IsTrue(components.ContainsKey(TemporalUnit.Second));
            Assert.IsTrue(components[TemporalUnit.Second].HasValue);
            Assert.AreEqual(3, components[TemporalUnit.Second].Value);
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("00:00:03", convertedTime.Value.ToString());
            DateTime offsetTime = dur.ApplyOffsetToDateTime(new DateTime(2012, 1, 1, 0, 0, 0));
            Assert.AreEqual("2012-01-01T00:00:03", offsetTime.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseDurationVagueness()
        {
            var dur = ParseDuration("<TIMEX3 value=\"PTXH\" />");
            Assert.IsTrue(dur.IsVague());
            var components = dur.DurationComponents;
            Assert.IsTrue(components.ContainsKey(TemporalUnit.Hour));
            Assert.IsFalse(components[TemporalUnit.Hour].HasValue);
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsFalse(convertedTime.HasValue);

        }

        [TestMethod]
        public void TestTimexParseDurationCompoundVagueness()
        {
            var dur = ParseDuration("<TIMEX3 value=\"PTXH30M\" />");
            Assert.IsTrue(dur.IsVague());
            var components = dur.DurationComponents;
            Assert.IsTrue(components.ContainsKey(TemporalUnit.Hour));
            Assert.IsTrue(components.ContainsKey(TemporalUnit.Minute));
            Assert.IsFalse(components[TemporalUnit.Hour].HasValue);
            Assert.IsTrue(components[TemporalUnit.Minute].HasValue);
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsFalse(convertedTime.HasValue);
        }

        [TestMethod]
        public void TestTimexParseDurationCompound1()
        {
            var dur = ParseDuration("<TIMEX3 value=\"P1W4D\" />");
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("11.00:00:00", convertedTime.Value.ToString());
            DateTime offsetTime = dur.ApplyOffsetToDateTime(new DateTime(2012, 1, 1, 0, 0, 0));
            Assert.AreEqual("2012-01-12T00:00:00", offsetTime.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseDurationCompound2()
        {
            var dur = ParseDuration("<TIMEX3 value=\"P1DT12H\" />");
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("1.12:00:00", convertedTime.Value.ToString());
            DateTime offsetTime = dur.ApplyOffsetToDateTime(new DateTime(2012, 1, 1, 0, 0, 0));
            Assert.AreEqual("2012-01-02T12:00:00", offsetTime.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseDurationCompound3()
        {
            var dur = ParseDuration("<TIMEX3 value=\"PT4H30M\" />");
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("04:30:00", convertedTime.Value.ToString());
            DateTime offsetTime = dur.ApplyOffsetToDateTime(new DateTime(2012, 1, 1, 0, 0, 0));
            Assert.AreEqual("2012-01-01T04:30:00", offsetTime.ToString("s"));
        }

        [TestMethod]
        public void TestTimexParseDurationHugeValues()
        {
            var dur = ParseDuration("<TIMEX3 value=\"P1000Y\" />");
            var components = dur.DurationComponents;
            Assert.IsTrue(components.ContainsKey(TemporalUnit.Year));
            Assert.IsTrue(components[TemporalUnit.Year].HasValue);
            Assert.AreEqual(1000, components[TemporalUnit.Year].Value);
            TimeSpan? convertedTime = dur.TryConvertIntoCSharpTimeSpan();
            Assert.IsTrue(convertedTime.HasValue);
            Assert.AreEqual("365000.00:00:00", convertedTime.Value.ToString());
        }

        #endregion

        #region Recurrence parser tests

        [TestMethod]
        public void TestTimexParseRecurrenceType1aMultiWeek()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"P2W\" quant=\"EVERY\" />");
            Assert.AreEqual(2, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType1aMultiDay()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"P2DT08 \" quant=\"EVERY\" />");
            Assert.AreEqual(2, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType1bDay()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"P1D\" quant=\"EVERY\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType1bWeek()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"P1W\" quant=\"EVERY\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType1bMonth()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"P1M\" quant=\"EVERY\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Month, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType1bYear()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"P1Y\" quant=\"EVERY\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Year, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType1bHour()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"PT1H\" quant=\"EVERY\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Hour, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType1bMinute()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"PT1M\" quant=\"EACH\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Minute, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType1bSecond()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"PT1S\" quant=\"EVERY\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Second, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType1bMultiDay()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"P4D\" quant=\"EVERY\" />");
            Assert.AreEqual(4, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType1bMultiHour()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"PT24H\" quant=\"EACH\" />");
            Assert.AreEqual(24, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Hour, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2Day()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-XX-XX\" quant=\"EVERY\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2Month()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-XX\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Month, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2Year()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Year, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2Week()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-WXX\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2Hour()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"TXX\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Hour, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2Minute()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"TXX:XX\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Minute, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2Second()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"TXX:XX:XX\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Second, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2Weekday()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-WXX-4\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.DayOfWeek));
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2Weekend()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-WXX-WE\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Weekend));
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryMorning()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-XX-XXTMO\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.PartOfDay));
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryFebruary()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-02\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Year, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Month));
            Assert.AreEqual("02", val.GetAnchorValues()[AnchorField.Month]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryFebruary14th()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-02-14\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Year, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(2, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Month));
            Assert.AreEqual("02", val.GetAnchorValues()[AnchorField.Month]);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Day));
            Assert.AreEqual("14", val.GetAnchorValues()[AnchorField.Day]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2The15thOfEachMonth()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-XX-15\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Month, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Day));
            Assert.AreEqual("15", val.GetAnchorValues()[AnchorField.Day]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryMonday()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-WXX-1\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.DayOfWeek));
            Assert.AreEqual("1", val.GetAnchorValues()[AnchorField.DayOfWeek]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryMondayMorning()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-WXX-1TMO\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(2, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.DayOfWeek));
            Assert.AreEqual("1", val.GetAnchorValues()[AnchorField.DayOfWeek]);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.PartOfDay));
            Assert.AreEqual("MO", val.GetAnchorValues()[AnchorField.PartOfDay]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryDayAt5()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-XX-XXT05\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Hour));
            Assert.AreEqual("05", val.GetAnchorValues()[AnchorField.Hour]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryDayAt5Ambiguous()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-XX-XXT05\" comment=\"ampm\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Hour));
            Assert.AreEqual("05", val.GetAnchorValues()[AnchorField.Hour]);
            Assert.IsTrue(val.GetAmPmAmbiguousFlag());
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryDayAt530()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-XX-XXT05:30\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(2, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Hour));
            Assert.AreEqual("05", val.GetAnchorValues()[AnchorField.Hour]);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Minute));
            Assert.AreEqual("30", val.GetAnchorValues()[AnchorField.Minute]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryWeekend()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-WXX-WE\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Weekend));
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryWeekday()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-WXX-WD\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Weekday));
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2SecondWeekOfEachMonth()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-XX-W02\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Month, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.WeekOfMonth));
            Assert.AreEqual("02", val.GetAnchorValues()[AnchorField.WeekOfMonth]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2BirthdayReminder()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-11-28T06:59:01\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Year, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(5, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Month));
            Assert.AreEqual("11", val.GetAnchorValues()[AnchorField.Month]);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Day));
            Assert.AreEqual("28", val.GetAnchorValues()[AnchorField.Day]);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Hour));
            Assert.AreEqual("06", val.GetAnchorValues()[AnchorField.Hour]);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Minute));
            Assert.AreEqual("59", val.GetAnchorValues()[AnchorField.Minute]);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Second));
            Assert.AreEqual("01", val.GetAnchorValues()[AnchorField.Second]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryDayInDecember()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-12-XX\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Month));
            Assert.AreEqual("12", val.GetAnchorValues()[AnchorField.Month]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryDayInDecember1998()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"1998-12-XX\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(2, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Year));
            Assert.AreEqual("1998", val.GetAnchorValues()[AnchorField.Year]);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Month));
            Assert.AreEqual("12", val.GetAnchorValues()[AnchorField.Month]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryDayInDecember1999()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"1999-12-XX\" freq=\"1day\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(2, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Year));
            Assert.AreEqual("1999", val.GetAnchorValues()[AnchorField.Year]);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.Month));
            Assert.AreEqual("12", val.GetAnchorValues()[AnchorField.Month]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2EveryOtherDay()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-XX-XX\" freq=\"2day\" />");
            Assert.AreEqual(2, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Day, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2TwiceAWeek()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-WXX\" freq=\"1week\" quant=\"2\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(2, val.GetQuantity());
            Assert.AreEqual(0, val.GetAnchorValues().Count);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType4TimesEveryOtherTuesday()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"XXXX-WXX-2\" freq=\"2week\" quant=\"4\" />");
            Assert.AreEqual(2, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(4, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.DayOfWeek));
            Assert.AreEqual("2", val.GetAnchorValues()[AnchorField.DayOfWeek]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2InvalidWeekday()
        {
            try
            {
                var timex = TimexValue.ParseXmlTag("<TIMEX3 type=\"Set\" value=\"XXXX-WXX-8\" />");
                Assert.Fail("Expected a TimexException");
            }
            catch (TimexException) { }
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType2InvalidHour()
        {
            try
            {
                var timex = TimexValue.ParseXmlTag("<TIMEX3 type=\"Set\" value=\"XXXX-XX-XXT48\" />");
                Assert.Fail("Expected a TimexException");
            }
            catch (TimexException) { }
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType3Thursday()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"2015-09-03\" quant=\"EVERY\" frequency=\"1week\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.DayOfWeek));
            Assert.AreEqual("4", val.GetAnchorValues()[AnchorField.DayOfWeek]);
        }

        [TestMethod]
        public void TestTimexParseRecurrenceType3Sunday()
        {
            var val = ParseRecurrence("<TIMEX3 type=\"Set\" value=\"2015-09-06\" quant=\"EVERY\" frequency=\"1week\" />");
            Assert.AreEqual(1, val.GetFrequencyValue());
            Assert.AreEqual(TemporalUnit.Week, val.GetFrequencyUnit());
            Assert.AreEqual(1, val.GetQuantity());
            Assert.AreEqual(1, val.GetAnchorValues().Count);
            Assert.IsTrue(val.GetAnchorValues().ContainsKey(AnchorField.DayOfWeek));
            Assert.AreEqual("7", val.GetAnchorValues()[AnchorField.DayOfWeek]);
        }

        #endregion

        #region EDT parser tests

        [TestMethod]
        public void TestTimexParseFullyResolvedDate()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Time;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "2016-08-12");
            Assert.IsNotNull(parsedVal);
            parsedVal = parsedVal.Reinterpret(timeContext);
            Assert.AreEqual("2016-08-12", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseFullyResolvedDateTime()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Time;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Time", "2016-08-12T08:00");
            Assert.IsNotNull(parsedVal);
            parsedVal = parsedVal.Reinterpret(timeContext);
            Assert.AreEqual("2016-08-12T08:00", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseTimeAndResolve()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Time;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Time", "T16:00");
            Assert.IsNotNull(parsedVal);
            parsedVal = parsedVal.Reinterpret(timeContext);
            Assert.AreEqual("2016-09-27T16:00", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseVague4OClockIntoEDTAndResolve()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Time;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Time", "XXXX-XX-XXT04:00", "", "", "", "ampm");
            Assert.IsNotNull(parsedVal);
            parsedVal = parsedVal.Reinterpret(timeContext);
            Assert.AreEqual("2016-09-27T16:00", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDateMissingDecade()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "19XX", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("19XX", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDateMonthOnly()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "XXXX-03", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2017-03", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDateYearSeason()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "2016-SU", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-SU", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDateYearWeek()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "2016-W14", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-W14", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParsePresentRef()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Time;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Time", "PRESENT_REF", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("PRESENT_REF", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDateDayOnly()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "XXXX-XX-30", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-09-30", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDateDayOfWeekOnly()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "XXXX-WXX-1", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-10-03", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDateWeekendUnresolved()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "XXXX-WXX-WE", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-W39-WE", parsedVal.FormatValue());
        }

        // Technically this case shouldn't happen, but it should still resolve properly
        [TestMethod]
        public void TestTimexParseDateDayOfWeek()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "2016-W04-1", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-01-25", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDateDayOfWeek2()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2009, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "2014-W01-2", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2013-12-31", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDateDayOfWeek3()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2009, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "2011-W52-7", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2012-01-01", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDateWeekend()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2014, 11, 21, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "2016-W04-WE", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-W04-WE", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDateMonthDayOnly()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Date;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Date", "XXXX-10-31", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-10-31", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseTimeHourOnly()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Time;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Time", "T04", "", "", "", "ampm", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-09-27T16", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseTimeMondayAfternoon()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Time;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Time", "XXXX-WXX-1TAF", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-10-03TAF", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseTime10PmMonday()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Time;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Time", "XXXX-WXX-1T20", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-10-03T20", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseTimeWithEmptyDate()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Time;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Time", "XXXX-XX-XXT17", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-09-27T17", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseTimeMorning()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Time;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Time", "TMO", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-09-28TMO", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseTimeThanksgivingAfternoon()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Time;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Time", "XXXX-11-W04-4T03", "", "", "", "ampm", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("2016-11-24T15", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDurationBasicDay()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Duration;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Duration", "P60D", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("P60D", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDurationBasicHour()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Duration;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Duration", "PT2H", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("PT2H", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDurationCompoundHour()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Duration;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Duration", "PT2H30M", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("PT2H30M", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexParseDurationCompoundHourMinuteSecond()
        {
            TimexContext timeContext = new TimexContext();
            timeContext.Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future;
            timeContext.UseInference = true;
            timeContext.TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.Duration;
            timeContext.ReferenceDateTime = new DateTime(2016, 9, 27, 13, 30, 0);

            ExtendedDateTime parsedVal = DateTimeParsers.TryParseExtendedDateTime("Duration", "PT2H30M12S", "", "", "", "", timeContext);
            Assert.IsNotNull(parsedVal);
            Assert.AreEqual("PT2H30M12S", parsedVal.FormatValue());
        }

        [TestMethod]
        public void TestTimexEDTTimeRangeResolution()
        {
            TimexContext timeContext = new TimexContext()
            {
                Normalization = Durandal.Common.Time.Timex.Enums.Normalization.Future,
                UseInference = true,
                WeekdayLogicType = Durandal.Common.Time.Timex.Enums.WeekdayLogic.SimpleOffset,
                TemporalType = Durandal.Common.Time.Timex.Enums.TemporalType.All,
                ReferenceDateTime = new DateTime(2016, 9, 27, 14, 07, 0),
                AmPmInferenceCutoff = 9
            };

            ExtendedDateTime start_date = DateTimeParsers.TryParseExtendedDateTime("Date", "2016-09-27", "", "", "", "", timeContext);
            Assert.IsNotNull(start_date);
            ExtendedDateTime start_time = DateTimeParsers.TryParseExtendedDateTime("Time", "T01:30", "", "", "", "ampm", timeContext);
            Assert.IsNotNull(start_time);
            ExtendedDateTime end_time = DateTimeParsers.TryParseExtendedDateTime("Time", "T03:30", "", "", "", "ampm", timeContext);
            Assert.IsNotNull(end_time);
            IList<ExtendedDateTime> toBeMerged = new List<ExtendedDateTime>(new ExtendedDateTime[] { start_date, start_time, end_time });
            IList<ExtendedDateTime> merged = DateTimeProcessors.MergePartialTimexMatches(toBeMerged);
            DateTimeRange range = DateTimeProcessors.RunTimeRangeResolution(merged, timeContext);
            Assert.IsNotNull(range);
            Assert.AreEqual("2016-09-27T13:30", range.StartTime.ExtendedDateTime.FormatValue());
            Assert.AreEqual("2016-09-27T15:30", range.EndTime.ExtendedDateTime.FormatValue());
        }



        [TestMethod]
        public void TestReparseYearWeek()
        {
            var anyContext = new TimexContext
            {
                TemporalType = TemporalType.All,
                ReferenceDateTime = new DateTime(2014, 1, 1, 0, 0, 0),
                UseInference = true
            };

            List<Tuple<int, int>> inputs = new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(2017, 17),
                new Tuple<int, int>(2020, 1),
                new Tuple<int, int>(2019, 52),
                new Tuple<int, int>(2015, 53),
                new Tuple<int, int>(2016, 1),
                new Tuple<int, int>(2017, 1),
            };

            foreach (var input in inputs)
            {
                IDictionary<string, string> dictionary = new Dictionary<string, string>();
                dictionary["YYYY"] = input.Item1.ToString(CultureInfo.InvariantCulture);
                dictionary["ww"] = input.Item2.ToString(CultureInfo.InvariantCulture);
                ExtendedDateTime edt = ExtendedDateTime.Create(TemporalType.Date, dictionary, anyContext);
                string expectedValue = string.Format("{0:D4}-W{1:D2}", input.Item1, input.Item2);
                Assert.AreEqual(expectedValue, edt.FormatValue());
            }
        }

        #endregion
    }
}

#pragma warning restore 0618