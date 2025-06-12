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
    public class TimexTest
    {
        [TestMethod]
        public void TestTimexGrammarMetatag()
        {
            string grammarFile =
                    "<?xml version=\"1.0\"?>" + 
                    "<grammar version=\"1.0\">" + 
                    "  <meta />" + 
                    "  <meta name=\"bad\" />" + 
                    "  <meta content=\"bad\" />" + 
                    "  <meta something=\"bad\" />" + 
                    "  <meta name=\"WeekdayLogic\" />" + 
                    "  <meta content=\"WeekdayLogic\" />" + 
                    "  <meta name=\"WeekdayLogic\" content=\"bad\" />" + 
                    "  <meta name=\"WeekdayLogic\" content=\"Programmatic\" />" + 
                    "  <meta name=\"AmPmInferenceCutoff\" />" + 
                    "  <meta content=\"AmPmInferenceCutoff\" />" + 
                    "  <meta name=\"AmPmInferenceCutoff\" content=\"bad\" />" + 
                    "  <meta name=\"AmPmInferenceCutoff\" content=\"3\" />" + 
                    "  <meta name=\"IncludeCurrentTimeInPastOrFuture\" content=\"true\" />" + 
                    "</grammar>";

            MemoryStream grammarFileStream = new MemoryStream(Encoding.UTF8.GetBytes(grammarFile), false);

            TimexMatcher localTimex = new TimexMatcher(grammarFileStream);

            Assert.AreEqual(3, localTimex.GrammarSpecificContext.AmPmInferenceCutoff);
            Assert.AreEqual(WeekdayLogic.Programmatic, localTimex.GrammarSpecificContext.WeekdayLogicType);
            Assert.IsTrue(localTimex.GrammarSpecificContext.IncludeCurrentTimeInPastOrFuture);
        }

        /// <summary>
        /// Tests a bug where ranges that are derived from durations have the improper temporal type set in the result
        /// </summary>
        [TestMethod]
        public void TestTimexDurationTimeRange()
        {
            var anyContext = new TimexContext
            {
                TemporalType = TemporalType.All,
                ReferenceDateTime = new DateTime(2012, 1, 1, 0, 0, 0),
                Normalization = Normalization.Future,
                UseInference = true
            };

            IDictionary<string, string> timexDict = new Dictionary<string, string>();
            timexDict.Add(TimexAttributes.Duration, "2");
            timexDict.Add(TimexAttributes.DurationUnit, "hour");
            ExtendedDateTime duration = ExtendedDateTime.Create(TemporalType.Duration, timexDict, anyContext);
            List<ExtendedDateTime> allTimes = new List<ExtendedDateTime>();
            allTimes.Add(duration);
            DateTimeRange range = DateTimeProcessors.RunTimeRangeResolution(allTimes, anyContext);
            Assert.IsNotNull(range);
            Assert.IsNotNull(range.EndTime);
            Assert.IsTrue(range.StartsNow);
            Assert.AreEqual("2012-01-01T02", range.EndTime.ExtendedDateTime.FormatValue());
            Assert.AreEqual(TemporalType.Time, range.EndTime.ExtendedDateTime.TemporalType);
        }

        /// <summary>
        /// tests an obscure bug
        /// </summary>
        [TestMethod]
        public void TestTimexReparseWeekOffsetInDateTimeResolution()
        {
            var anyContext = new TimexContext
            {
                TemporalType = TemporalType.All,
                ReferenceDateTime = new DateTime(2017, 4, 25, 10, 0, 0),
                Normalization = Normalization.Future,
                UseInference = true
            };

            IDictionary<string, string> timexDict = new Dictionary<string, string>();
            timexDict.Add(TimexAttributes.Offset, "0");
            timexDict.Add(TimexAttributes.OffsetUnit, "week");
            ExtendedDateTime inputDate = ExtendedDateTime.Create(TemporalType.Date, timexDict, anyContext);
            IList<ExtendedDateTime> times = new List<ExtendedDateTime>();
            times.Add(inputDate);
            DateTimeRange range = DateTimeProcessors.RunTimeRangeResolution(times, anyContext);
            Assert.IsNotNull(range);
            Assert.IsNotNull(range.StartTime);
            Assert.AreEqual("2017-W17", range.StartTime.ExtendedDateTime.FormatValue());
            Assert.AreEqual(TemporalType.Date, range.StartTime.ExtendedDateTime.TemporalType);
        }

        /// <summary>
        /// Checks that invalid "weekend + hour" formulations are not emitted
        /// </summary>
        [TestMethod]
        public void TestTimexDisallowHoursAfterWeekend()
        {
            var anyContext = new TimexContext
            {
                TemporalType = TemporalType.Date | TemporalType.Time,
                ReferenceDateTime = new DateTime(2012, 1, 1, 0, 0, 0),
                UseInference = false
            };

            IDictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["OFFSET"] = "0";
            dictionary["OFFSET_UNIT"] = "weekend";
            dictionary["hh"] = "17";
            try
            {
                ExtendedDateTime edt = ExtendedDateTime.Create(TemporalType.Time, dictionary, anyContext);
                Assert.Fail("Should have thrown a TimexException");
            }
            catch (TimexException) { }

            dictionary = new Dictionary<string, string>();
            dictionary["OFFSET"] = "0";
            dictionary["OFFSET_UNIT"] = "week";
            dictionary["hh"] = "17";
            try
            {
                ExtendedDateTime edt = ExtendedDateTime.Create(TemporalType.Time, dictionary, anyContext);
                Assert.Fail("Should have thrown a TimexException");
            }
            catch (TimexException) { }
        }

        [TestMethod]
        public void TestTimexAllowHourRecurrencesEveryWeekend()
        {
            var anyContext = new TimexContext
            {
                TemporalType = TemporalType.Set,
                ReferenceDateTime = new DateTime(2012, 1, 1, 0, 0, 0),
                UseInference = false
            };

            IDictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["DURATION"] = "1";
            dictionary["DURATION_UNIT"] = "weekend";
            dictionary["hh"] = "17";
            ExtendedDateTime edt = ExtendedDateTime.Create(TemporalType.Set, dictionary, anyContext);
            Assert.AreEqual("XXXX-WXX-WET17", edt.FormatValue());
        }

        [TestMethod]
        public void TestTimexDefaultInterpretationOfVagueOffsetDurations()
        {
            var anyContext = new TimexContext
            {
                TemporalType = TemporalType.Date,
                ReferenceDateTime = new DateTime(2012, 1, 1, 0, 0, 0),
                UseInference = false
            };

            IDictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["OFFSET_UNIT"] = "day";
            ExtendedDateTime edt = ExtendedDateTime.Create(TemporalType.Date, dictionary, anyContext);
            Assert.AreEqual("2012-01-04", edt.FormatValue());
            Assert.AreEqual("APPROX", edt.FormatMod());
        }

        [TestMethod]
        public void TestTimexDefaultInterpretationOfVagueOffsetDurationsOverwritesModifier()
        {
            var anyContext = new TimexContext
            {
                TemporalType = TemporalType.Date,
                ReferenceDateTime = new DateTime(2012, 1, 1, 0, 0, 0),
                UseInference = false
            };

            IDictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["OFFSET_UNIT"] = "day";
            dictionary["MOD"] = "EQUAL_OR_LESS";
            ExtendedDateTime edt = ExtendedDateTime.Create(TemporalType.Date, dictionary, anyContext);
            Assert.AreEqual("2012-01-04", edt.FormatValue());
            Assert.AreEqual("APPROX", edt.FormatMod());
        }

        [TestMethod]
        public void TestTimexDefaultInterpretationOfVagueOffsetDurationsConfigurable()
        {
            var anyContext = new TimexContext
            {
                TemporalType = TemporalType.Date,
                ReferenceDateTime = new DateTime(2012, 1, 1, 0, 0, 0),
                UseInference = false
            };

            IDictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["OFFSET_UNIT"] = "day";
            anyContext.DefaultValueOfVagueOffset[TemporalUnit.Day] = 5;
            ExtendedDateTime edt = ExtendedDateTime.Create(TemporalType.Date, dictionary, anyContext);
            Assert.AreEqual("2012-01-06", edt.FormatValue());
            Assert.AreEqual("APPROX", edt.FormatMod());
        }

        [TestMethod]
        public void TestTimexConfigurableInterpretationOfVagueOffsetDurations()
        {
            var vagueOffsetDictionary = new Dictionary<TemporalUnit, int>();
            foreach (TemporalUnit unit in Enum.GetValues(typeof(TemporalUnit)))
            {
                if (unit == TemporalUnit.Minute)
                {
                    vagueOffsetDictionary[unit] = 10; // 10 minutes
                }
                else if (unit == TemporalUnit.Second)
                {
                    vagueOffsetDictionary[unit] = 60; // one minute
                }
                else
                {
                    vagueOffsetDictionary[unit] = 3; // 3 units for the rest of the units
                }
            }

            var anyContext = new TimexContext
            {
                TemporalType = TemporalType.Date,
                ReferenceDateTime = new DateTime(2012, 1, 1, 0, 0, 0),
                UseInference = false,
                DefaultValueOfVagueOffset = vagueOffsetDictionary
            };

            IDictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["OFFSET_UNIT"] = "minute";
            ExtendedDateTime edt = ExtendedDateTime.Create(TemporalType.Date, dictionary, anyContext);
            Assert.AreEqual("2012-01-01T00:10", edt.FormatValue());
            Assert.AreEqual("APPROX", edt.FormatMod());
        }
    }
}
