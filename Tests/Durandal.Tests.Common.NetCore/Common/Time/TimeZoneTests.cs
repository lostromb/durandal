using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Time.TimeZone;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Time
{
    [TestClass]
    public class TimeZoneTests
    {
        [TestMethod]
        public async Task TestTimeZoneLosAngelesBasic()
        {
            ILogger logger = new ConsoleLogger();
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(new VirtualPath("los_angeles"), LosAngelesZoneDef);
            fileSystem.AddFile(new VirtualPath("zone1970.tab"), LosAngelesZone1970);
            TimeZoneResolver resolver = new TimeZoneResolver(logger.Clone("Resolver"));
            await resolver.Initialize(fileSystem, VirtualPath.Root);
            TimeZoneQueryResult result = resolver.CalculateLocalTime("America/Los_Angeles", new DateTimeOffset(2015, 8, 11, 20, 11, 14, TimeSpan.Zero), logger);
            Assert.IsNotNull(result);
            Assert.AreEqual("2015-08-11T13:11:14 -07:00", result.LocalTime.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("01:00:00", result.DstOffset.ToString());
            Assert.AreEqual("-08:00:00", result.GmtOffset.ToString());
            Assert.AreEqual("PDT", result.TimeZoneAbbreviation);
        }

        [TestMethod]
        public async Task TestTimeZoneLosAngelesWartime()
        {
            ILogger logger = new ConsoleLogger();
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(new VirtualPath("los_angeles"), LosAngelesZoneDef);
            fileSystem.AddFile(new VirtualPath("zone1970.tab"), LosAngelesZone1970);
            TimeZoneResolver resolver = new TimeZoneResolver(logger.Clone("Resolver"));
            await resolver.Initialize(fileSystem, VirtualPath.Root);
            TimeZoneQueryResult result = resolver.CalculateLocalTime("America/Los_Angeles", new DateTimeOffset(1943, 8, 11, 20, 11, 14, TimeSpan.Zero), logger);
            Assert.IsNotNull(result);
            Assert.AreEqual("1943-08-11T13:11:14 -07:00", result.LocalTime.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("01:00:00", result.DstOffset.ToString());
            Assert.AreEqual("-08:00:00", result.GmtOffset.ToString());
            Assert.AreEqual("PWT", result.TimeZoneAbbreviation);
        }

        [TestMethod]
        public async Task TestTimeZoneLosAngelesPrehistoric()
        {
            ILogger logger = new ConsoleLogger();
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(new VirtualPath("los_angeles"), LosAngelesZoneDef);
            fileSystem.AddFile(new VirtualPath("zone1970.tab"), LosAngelesZone1970);
            TimeZoneResolver resolver = new TimeZoneResolver(logger.Clone("Resolver"));
            await resolver.Initialize(fileSystem, VirtualPath.Root);
            TimeZoneQueryResult result = resolver.CalculateLocalTime("America/Los_Angeles", new DateTimeOffset(1842, 8, 11, 20, 11, 14, TimeSpan.Zero), logger);
            Assert.IsNotNull(result);
            Assert.AreEqual("1842-08-11T12:18:14 -07:53", result.LocalTime.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("00:00:00", result.DstOffset.ToString());
            Assert.AreEqual("-07:52:58", result.GmtOffset.ToString());
            Assert.AreEqual("LMT", result.TimeZoneAbbreviation);
        }

        [TestMethod]
        public async Task TestTimeZoneLosAngeles2018()
        {
            ILogger logger = new ConsoleLogger();
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(new VirtualPath("los_angeles"), LosAngelesZoneDef);
            fileSystem.AddFile(new VirtualPath("zone1970.tab"), LosAngelesZone1970);
            TimeZoneResolver resolver = new TimeZoneResolver(logger.Clone("Resolver"));
            await resolver.Initialize(fileSystem, VirtualPath.Root);
            TimeZoneQueryResult result = resolver.CalculateLocalTime("America/Los_Angeles", new DateTimeOffset(2018, 9, 18, 23, 0, 0, TimeSpan.Zero), logger);
            Assert.IsNotNull(result);
            Assert.AreEqual("2018-09-18T16:00:00 -07:00", result.LocalTime.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("01:00:00", result.DstOffset.ToString());
            Assert.AreEqual("-08:00:00", result.GmtOffset.ToString());
            Assert.AreEqual("PDT", result.TimeZoneAbbreviation);
        }

        [TestMethod]
        public async Task TestTimeZoneLosAngelesMultiRuleset()
        {
            ILogger logger = new ConsoleLogger();
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(new VirtualPath("los_angeles"), LosAngelesZoneDef);
            fileSystem.AddFile(new VirtualPath("zone1970.tab"), LosAngelesZone1970);
            TimeZoneResolver resolver = new TimeZoneResolver(logger.Clone("Resolver"));
            await resolver.Initialize(fileSystem, VirtualPath.Root);
            DateTimeOffset rangeStart = new DateTimeOffset(1965, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
            DateTimeOffset rangeEnd = new DateTimeOffset(1968, 1, 1, 0, 0, 0, TimeSpan.FromHours(3));
            List<TimeZoneRuleEffectiveSpan> spans = resolver.CalculateTimeZoneRuleSpans("America/Los_Angeles",
                rangeStart,
                rangeEnd,
                logger);

            Assert.AreEqual(8, spans.Count);
            Assert.AreEqual("1964-12-31T14:00:00 -08:00", spans[0].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1965-04-25T01:00:00 -08:00", spans[0].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[0].GmtOffset.ToString());
            Assert.AreEqual("00:00:00", spans[0].DstOffset.ToString());
            Assert.AreEqual("PST", spans[0].TimeZoneAbbreviation);
            Assert.AreEqual("1965-04-25T02:00:00 -07:00", spans[1].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1965-10-31T02:00:00 -07:00", spans[1].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[1].GmtOffset.ToString());
            Assert.AreEqual("01:00:00", spans[1].DstOffset.ToString());
            Assert.AreEqual("PDT", spans[1].TimeZoneAbbreviation);
            Assert.AreEqual("1965-10-31T01:00:00 -08:00", spans[2].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1966-04-24T01:00:00 -08:00", spans[2].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[2].GmtOffset.ToString());
            Assert.AreEqual("00:00:00", spans[2].DstOffset.ToString());
            Assert.AreEqual("PST", spans[2].TimeZoneAbbreviation);
            Assert.AreEqual("1966-04-24T02:00:00 -07:00", spans[3].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1966-10-30T02:00:00 -07:00", spans[3].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[3].GmtOffset.ToString());
            Assert.AreEqual("01:00:00", spans[3].DstOffset.ToString());
            Assert.AreEqual("PDT", spans[3].TimeZoneAbbreviation);
            Assert.AreEqual("1966-10-30T01:00:00 -08:00", spans[4].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1967-01-01T00:00:00 -08:00", spans[4].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[4].GmtOffset.ToString());
            Assert.AreEqual("00:00:00", spans[4].DstOffset.ToString());
            Assert.AreEqual("PST", spans[4].TimeZoneAbbreviation);
            Assert.AreEqual("1967-01-01T00:00:00 -08:00", spans[5].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1967-04-30T02:00:00 -08:00", spans[5].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[5].GmtOffset.ToString());
            Assert.AreEqual("00:00:00", spans[5].DstOffset.ToString());
            Assert.AreEqual("PST", spans[5].TimeZoneAbbreviation);
            Assert.AreEqual("1967-04-30T03:00:00 -07:00", spans[6].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1967-10-29T02:00:00 -07:00", spans[6].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[6].GmtOffset.ToString());
            Assert.AreEqual("01:00:00", spans[6].DstOffset.ToString());
            Assert.AreEqual("PDT", spans[6].TimeZoneAbbreviation);
            Assert.AreEqual("1967-10-29T01:00:00 -08:00", spans[7].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1967-12-31T13:00:00 -08:00", spans[7].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[7].GmtOffset.ToString());
            Assert.AreEqual("00:00:00", spans[7].DstOffset.ToString());
            Assert.AreEqual("PST", spans[7].TimeZoneAbbreviation);
            AssertTimeSpansAreGapless(rangeStart, rangeEnd, spans);
        }

        [TestMethod]
        public async Task TestTimeZoneSpanCalculationLosAngeles()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(new VirtualPath("los_angeles"), LosAngelesZoneDef);
            fileSystem.AddFile(new VirtualPath("zone1970.tab"), LosAngelesZone1970);
            TimeZoneResolver resolver = new TimeZoneResolver(logger.Clone("Resolver"));
            await resolver.Initialize(fileSystem, VirtualPath.Root);
            DateTimeOffset rangeStart = new DateTimeOffset(1900, 2, 5, 0, 0, 0, TimeSpan.FromHours(2));
            DateTimeOffset rangeEnd = new DateTimeOffset(1948, 2, 5, 0, 0, 0, TimeSpan.FromHours(3));
            List<TimeZoneRuleEffectiveSpan> spans = resolver.CalculateTimeZoneRuleSpans("America/Los_Angeles",
                rangeStart,
                rangeEnd,
                logger);

            Assert.AreEqual(9, spans.Count);
            Assert.AreEqual("1900-02-04T14:00:00 -08:00", spans[0].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1918-03-31T02:00:00 -08:00", spans[0].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[0].GmtOffset.ToString());
            Assert.AreEqual("00:00:00", spans[0].DstOffset.ToString());
            Assert.AreEqual("PST", spans[0].TimeZoneAbbreviation);
            Assert.AreEqual("1918-03-31T03:00:00 -07:00", spans[1].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1918-10-27T02:00:00 -07:00", spans[1].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[1].GmtOffset.ToString());
            Assert.AreEqual("01:00:00", spans[1].DstOffset.ToString());
            Assert.AreEqual("PDT", spans[1].TimeZoneAbbreviation);
            Assert.AreEqual("1918-10-27T01:00:00 -08:00", spans[2].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1919-03-30T02:00:00 -08:00", spans[2].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[2].GmtOffset.ToString());
            Assert.AreEqual("00:00:00", spans[2].DstOffset.ToString());
            Assert.AreEqual("PST", spans[2].TimeZoneAbbreviation);
            Assert.AreEqual("1919-03-30T03:00:00 -07:00", spans[3].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1919-10-26T02:00:00 -07:00", spans[3].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[3].GmtOffset.ToString());
            Assert.AreEqual("01:00:00", spans[3].DstOffset.ToString());
            Assert.AreEqual("PDT", spans[3].TimeZoneAbbreviation);
            Assert.AreEqual("1919-10-26T01:00:00 -08:00", spans[4].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1942-02-09T02:00:00 -08:00", spans[4].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[4].GmtOffset.ToString());
            Assert.AreEqual("00:00:00", spans[4].DstOffset.ToString());
            Assert.AreEqual("PST", spans[4].TimeZoneAbbreviation);
            Assert.AreEqual("1942-02-09T03:00:00 -07:00", spans[5].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1945-08-14T23:00:00 +00:00", spans[5].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[5].GmtOffset.ToString());
            Assert.AreEqual("01:00:00", spans[5].DstOffset.ToString());
            Assert.AreEqual("PWT", spans[5].TimeZoneAbbreviation);
            Assert.AreEqual("1945-08-14T16:00:00 -07:00", spans[6].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1945-09-30T02:00:00 -07:00", spans[6].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[6].GmtOffset.ToString());
            Assert.AreEqual("01:00:00", spans[6].DstOffset.ToString());
            Assert.AreEqual("PPT", spans[6].TimeZoneAbbreviation);
            Assert.AreEqual("1945-09-30T01:00:00 -08:00", spans[7].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1946-01-01T00:00:00 -08:00", spans[7].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[7].GmtOffset.ToString());
            Assert.AreEqual("00:00:00", spans[7].DstOffset.ToString());
            Assert.AreEqual("PST", spans[7].TimeZoneAbbreviation);
            Assert.AreEqual("1946-01-01T00:00:00 -08:00", spans[8].RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("1948-02-04T13:00:00 -08:00", spans[8].RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz"));
            Assert.AreEqual("-08:00:00", spans[8].GmtOffset.ToString());
            Assert.AreEqual("00:00:00", spans[8].DstOffset.ToString());
            Assert.AreEqual("PST", spans[8].TimeZoneAbbreviation);
            AssertTimeSpansAreGapless(rangeStart, rangeEnd, spans);

            //for (int c = 0; c < spans.Count; c++)
            //{
            //    TimeZoneRuleEffectiveSpan span = spans[c];
            //    Console.WriteLine("Assert.AreEqual(\"" + span.RuleBoundaryBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz") + "\", spans[" + c + "].RuleBoundaryBegin.ToString(\"yyyy-MM-ddTHH:mm:ss zzz\"));");
            //    Console.WriteLine("Assert.AreEqual(\"" + span.RuleBoundaryEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz") + "\", spans[" + c + "].RuleBoundaryEnd.ToString(\"yyyy-MM-ddTHH:mm:ss zzz\"));");
            //    Console.WriteLine("Assert.AreEqual(\"" + span.GmtOffset.ToString() + "\", spans[" + c + "].GmtOffset.ToString());");
            //    Console.WriteLine("Assert.AreEqual(\"" + span.DstOffset.ToString() + "\", spans[" + c + "].DstOffset.ToString());");
            //    Console.WriteLine("Assert.AreEqual(\"" + span.TimeZoneAbbreviation + "\", spans[" + c + "].TimeZoneAbbreviation);");
            //}
        }

        private void AssertTimeSpansAreGapless(DateTimeOffset rangeBegin, DateTimeOffset rangeEnd, List<TimeZoneRuleEffectiveSpan> spans)
        {
            if (spans.Count == 0)
            {
                Assert.AreEqual(0, (rangeEnd - rangeBegin).Ticks);
                return;
            }

            Assert.AreEqual(0, (spans[0].RuleBoundaryBegin - rangeBegin).Ticks);
            for (int c = 0; c < spans.Count - 1; c++)
            {
                Assert.AreEqual(0, (spans[c + 1].RuleBoundaryBegin - spans[c].RuleBoundaryEnd).Ticks);
            }
            Assert.AreEqual(0, (rangeEnd - spans[spans.Count - 1].RuleBoundaryEnd).Ticks);
        }

        private static byte[] LosAngelesZoneDef
        {
            get
            {
                string zoneDefFile =
"Rule\tUS\t1918\t1919\t-\tMar\tlastSun\t2:00\t1:00\tD\r\n" +
"Rule\tUS\t1918\t1919\t-\tOct\tlastSun\t2:00\t0\tS\r\n" +
"Rule\tUS\t1942\tonly\t-\tFeb\t9\t2:00\t1:00\tW # War\r\n" +
"Rule\tUS\t1945\tonly\t-\tAug\t14\t23:00u\t1:00\tP # Peace\r\n" +
"Rule\tUS\t1945\tonly\t-\tSep\tlastSun\t2:00\t0\tS\r\n" +
"# comment\r\n" +
"Rule\tUS\t1967\t2006\t-\tOct\tlastSun\t2:00\t0\tS\r\n" +
"Rule\tUS\t1967\t1973\t-\tApr\tlastSun\t2:00\t1:00\tD\r\n" +
"Rule\tUS\t1974\tonly\t-\tJan\t6\t2:00\t1:00\tD\r\n" +
"  # who puts comments here I don't know\r\n" +
"Rule\tUS\t1975\tonly\t-\tFeb\t23\t2:00\t1:00\tD\r\n" +
"Rule\tUS\t1976\t1986\t-\tApr\tlastSun\t2:00\t1:00\tD\r\n" +
"Rule\tUS\t1987\t2006\t-\tApr\tSun>=1\t2:00\t1:00\tD\r\n" +
"Rule\tUS\t2007\tmax\t-\tMar\tSun>=8\t2:00\t1:00\tD\r\n" +
"Rule\tUS\t2007\tmax\t-\tNov\tSun>=1\t2:00\t0\tS\r\n" +
"\r\n" +
"Rule\tCA\t1948\tonly\t-\tMar\t14\t2:01\t1:00\tD\r\n" +
"Rule\tCA\t1949\tonly\t-\tJan\t 1\t2:00\t0\tS\r\n" +
"Rule\tCA\t1950\t1966\t-\tApr\tlastSun\t1:00\t1:00\tD\r\n" +
"Rule\tCA\t1950\t1961\t-\tSep\tlastSun\t2:00\t0\tS\r\n" +
"Rule\tCA\t1962\t1966\t-\tOct\tlastSun\t2:00\t0\tS\r\n" +
"\r\n" +
"Zone America/Los_Angeles -7:52:58 -\tLMT\t1883 Nov 18 12:07:02\r\n" +
"# here is a comment in the middle for whatever reason\r\n" +
"\t\t\t-8:00\tUS\tP%sT\t1946\r\n" +
"  # who puts comments here I don't know\r\n" +
"\t\t\t-8:00\tCA\tP%sT\t1967\r\n" +
"# here is a comment in the middle for whatever reason\r\n" +
"\t\t\t-8:00\tUS\tP%sT";
                return Encoding.UTF8.GetBytes(zoneDefFile);
            }
        }

        private static byte[] LosAngelesZone1970
        {
            get
            {
                string file = "US\t+340308-1181434\tAmerica/Los_Angeles\tPacific";
                return Encoding.UTF8.GetBytes(file);
            }
        }
    }
}
