using Durandal;
using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.File;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.Test;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.UnitConversion;
using Durandal.Common.Ontology;
using Durandal.Plugins.Fitbit;
using Durandal.Plugins.Fitbit.Schemas;
using Durandal.Plugins.Fitbit.Schemas.Responses;
using Durandal.Plugins.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DialogTests.Plugin.SchemaDotOrg;
using Durandal.Common.Test.Builders;

namespace DialogTests.Plugins.Time
{
    [TestClass]
    public class TimeTests
    {
        private static ManualTimeProvider _time;
        private static TimePlugin _plugin;
        private static InqueTestDriver _testDriver;
        private static FastRandom _rand;

        #region Test framework

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _time = new ManualTimeProvider();
            _rand = new FastRandom();
            _plugin = new TimePlugin(_time, _rand);
            string rootEnv = context.Properties["DurandalRootDirectory"]?.ToString();
            if (string.IsNullOrEmpty(rootEnv))
            {
                rootEnv = Environment.GetEnvironmentVariable("DURANDAL_ROOT");
                if (string.IsNullOrEmpty(rootEnv))
                {
                    throw new FileNotFoundException("Cannot find durandal environment directory, either from DurandalRootDirectory test property, or DURANDAL_ROOT environment variable.");
                }
            }

            InqueTestParameters testConfig = PluginTestCommon.CreateTestParameters(_plugin, "TimePlugin.dupkg", new DirectoryInfo(rootEnv));
            _testDriver = new InqueTestDriver(testConfig);
            _testDriver.Initialize().Await();
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            _testDriver.Dispose();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _testDriver.ResetState();
        }

        #endregion

        #region Tests
        
        /// <summary>
        /// Test resolution of local time based on client context time
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeLocalTimeUsingClientContextTime()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "TIME", "time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = -480;
            request.ClientContext.ReferenceDateTime = "2018-11-28T13:32:11";
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("It is now 1:32 PM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Test resolution of local time based on client context UTC offset only
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeLocalTimeUsingClientContextUTCOffset()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "TIME", "time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = -480;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("It is now 1:32 PM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Test resolution of local time based on client context location
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeLocalTimeUsingClientContextLocation()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "TIME", "time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = 47.601871;
            request.ClientContext.Longitude = -122.302294;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("The current time is 1:32 PM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Test resolution of local time based on client context IANA time zone name
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeLocalTimeUsingClientContextIanaTimezone()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "TIME", "time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;
            request.ClientContext.UserTimeZone = "Pacific/Guam";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("The current time is 7:32 AM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Test resolution of local time based on client context windows time zone name
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeLocalTimeUsingClientContextWindowsTimezone()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "TIME", "time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;
            request.ClientContext.UserTimeZone = "Central European Standard Time";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("The current time is 10:32 PM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Client doesn't send any context information at all
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeLocalTimeNoClientContext()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "TIME", "time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("The best I can tell you is that the UTC time is 9:32 PM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Test resolution of local date based on client context time
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeLocalDateUsingClientContextTime()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what day is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DATE", "day")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = -480;
            request.ClientContext.ReferenceDateTime = "2018-11-28T13:32:11";
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Today is Wednesday, November 28th.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Test resolution of local date based on client context UTC offset only
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeLocalDateUsingClientContextUTCOffset()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what day is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DATE", "day")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = -480;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Today is Wednesday, November 28th.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Test resolution of local date based on client context location
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeLocalDateUsingClientContextLocation()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what day is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DATE", "day")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = 47.601871;
            request.ClientContext.Longitude = -122.302294;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Today is Wednesday, November 28th.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Test resolution of local date based on client context IANA time zone name
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeLocalDateUsingClientContextIanaTimezone()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what day is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DATE", "day")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;
            request.ClientContext.UserTimeZone = "Pacific/Guam";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Today is Thursday, November 29th.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Test resolution of local date based on client context windows time zone name
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeLocalDateUsingClientContextWindowsTimezone()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what day is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DATE", "day")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;
            request.ClientContext.UserTimeZone = "Central European Standard Time";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Today is Wednesday, November 28th.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeLocalDateNoClientContext()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what day is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DATE", "day")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("I'm not sure about you, but my calendar says today is Wednesday, November 28th.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeLocalDayOfWeek()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what day of the week is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DAY_OF_WEEK", "day of the week")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = -480;
            request.ClientContext.ReferenceDateTime = "2018-11-28T13:32:11";
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Today is Wednesday, November 28th.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeLocalDayOfMonth()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what day of the month is it", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DAY_OF_MONTH", "day of the month")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = -480;
            request.ClientContext.ReferenceDateTime = "2018-11-28T13:32:11";
            request.ClientContext.Latitude = null;
            request.ClientContext.Longitude = null;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Today is Wednesday, November 28th.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Test basic world time (query location is on east coast US)
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeWorldTimeSeattle()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);

            KnowledgeContext kc = new KnowledgeContext();
            GeoCoordinates placeCoords = new GeoCoordinates(kc);
            placeCoords.Latitude_as_number.Value = 47.601871M;
            placeCoords.Longitude_as_number.Value = -122.302294M;
            Place locationEntity = new Place(kc);
            locationEntity.Geo_as_GeoCoordinates.SetValue(placeCoords);
            locationEntity.Name.Value = "Seattle";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it in seattle", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "TIME", "time")
                            .AddEntitySlot("location", "seattle", locationEntity)
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494;
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("In Seattle it is now 11:32 AM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// User is in US, asks for time in Japan, and the Japan time is tomorrow
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeWorldTimeAcrossDateBoundaryAhead()
        {
            _time.Time = new DateTimeOffset(2018, 08, 30, 23, 00, 00, TimeSpan.Zero);
            _rand.SeedRand(103);

            KnowledgeContext kc = new KnowledgeContext();
            GeoCoordinates placeCoords = new GeoCoordinates(kc);
            placeCoords.Latitude_as_number.Value = 36.155462M;
            placeCoords.Longitude_as_number.Value = 136.761013M;
            Place locationEntity = new Place(kc);
            locationEntity.Geo_as_GeoCoordinates.SetValue(placeCoords);
            locationEntity.Name.Value = "Japan";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it in japan", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "TIME", "time")
                            .AddEntitySlot("location", "japan", locationEntity)
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 47.601871;
            request.ClientContext.Longitude = -122.302294;
            request.ClientContext.UTCOffset = -420;
            request.ClientContext.ReferenceDateTime = "2018-08-30T16:00:00";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("In Japan it is now 8:00 AM tomorrow.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// User is in Japan, asks for time in US, and the US time is yesterday
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeWorldTimeAcrossDateBoundaryBehind()
        {
            _time.Time = new DateTimeOffset(2018, 08, 30, 23, 00, 00, TimeSpan.Zero);
            _rand.SeedRand(103);

            KnowledgeContext kc = new KnowledgeContext();
            GeoCoordinates placeCoords = new GeoCoordinates(kc);
            placeCoords.Latitude_as_number.Value = 47.601871M; // seattle
            placeCoords.Longitude_as_number.Value = -122.302294M;
            Place locationEntity = new Place(kc);
            locationEntity.Geo_as_GeoCoordinates.SetValue(placeCoords);
            locationEntity.Name.Value = "Seattle";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it in seattle", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "TIME", "time")
                            .AddEntitySlot("location", "seattle", locationEntity)
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 36.155462; // japan
            request.ClientContext.Longitude = 136.761013;
            request.ClientContext.UTCOffset = 540;
            request.ClientContext.ReferenceDateTime = "2018-08-31T08:00:00";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("In Seattle it is now 4:00 PM yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Test that the local time is correct just prior to a DST time change
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeWorldTimeSeattleAtDstBoundaryPre()
        {
            _time.Time = new DateTimeOffset(2018, 11, 04, 08, 20, 00, TimeSpan.Zero);
            _rand.SeedRand(103);

            KnowledgeContext kc = new KnowledgeContext();
            GeoCoordinates placeCoords = new GeoCoordinates(kc);
            placeCoords.Latitude_as_number.Value = 47.601871M;
            placeCoords.Longitude_as_number.Value = -122.302294M;
            Place locationEntity = new Place(kc);
            locationEntity.Geo_as_GeoCoordinates.SetValue(placeCoords);
            locationEntity.Name.Value = "Seattle";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it in seattle", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "TIME", "time")
                            .AddEntitySlot("location", "seattle", locationEntity)
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494;
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-11-04T04:20:00";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("In Seattle it is now 1:20 AM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// This test is set an hour after the previous test in UTC, but the local time is the same because DST just changed
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeWorldTimeSeattleAtDstBoundaryPost()
        {
            _time.Time = new DateTimeOffset(2018, 11, 04, 09, 20, 00, TimeSpan.Zero);
            _rand.SeedRand(103);

            KnowledgeContext kc = new KnowledgeContext();
            GeoCoordinates placeCoords = new GeoCoordinates(kc);
            placeCoords.Latitude_as_number.Value = 47.601871M;
            placeCoords.Longitude_as_number.Value = -122.302294M;
            Place locationEntity = new Place(kc);
            locationEntity.Geo_as_GeoCoordinates.SetValue(placeCoords);
            locationEntity.Name.Value = "Seattle";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it in seattle", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "TIME", "time")
                            .AddEntitySlot("location", "seattle", locationEntity)
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494;
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-11-04T05:20:00";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("In Seattle it is now 1:20 AM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }
        
        [TestMethod]
        public async Task TestTimeWorldTimeRelative()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);

            KnowledgeContext kc = new KnowledgeContext();
            GeoCoordinates seattleCoords = new GeoCoordinates(kc);
            seattleCoords.Latitude_as_number.Value = 47.601871M;
            seattleCoords.Longitude_as_number.Value = -122.302294M;
            Place seattleEntity = new Place(kc);
            seattleEntity.Geo_as_GeoCoordinates.SetValue(seattleCoords);
            seattleEntity.Name.Value = "Seattle";

            GeoCoordinates tokyoCoords = new GeoCoordinates(kc);
            tokyoCoords.Latitude_as_number.Value = 36.155462M;
            tokyoCoords.Longitude_as_number.Value = 136.761013M;
            Place tokyoEntity = new Place(kc);
            tokyoEntity.Geo_as_GeoCoordinates.SetValue(tokyoCoords);
            tokyoEntity.Name.Value = "Tokyo";
            
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time is it in seattle if it was 10 AM in Tokyo", InputMethod.Typed)
                    .AddRecoResult("time", "get_relative_world_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddTimexSlot("basis_time", "10 AM", ExtendedDateTime.Create(
                                TemporalType.Time,
                                new Dictionary<string, string>()
                                {
                                    ["hh"] = "10",
                                },
                                new TimexContext()
                                {
                                    ReferenceDateTime = _time.Time.UtcDateTime,
                                    UseInference = false
                                }))
                            .AddEntitySlot("query_location", "seattle", seattleEntity)
                            .AddEntitySlot("basis_location", "tokyo", tokyoEntity)
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494; // User context is in east coast US
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("get_relative_world_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("If it were 10:00 AM in Tokyo it would be 6:00 PM the previous day in Seattle.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWorldTimeRelativeCurrentLocationBasis()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);

            KnowledgeContext kc = new KnowledgeContext();

            GeoCoordinates tokyoCoords = new GeoCoordinates(kc);
            tokyoCoords.Latitude_as_number.Value = 36.155462M;
            tokyoCoords.Longitude_as_number.Value = 136.761013M;
            Place tokyoEntity = new Place(kc);
            tokyoEntity.Geo_as_GeoCoordinates.SetValue(tokyoCoords);
            tokyoEntity.Name.Value = "Tokyo";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time would it be in Tokyo if it was 10 AM here", InputMethod.Typed)
                    .AddRecoResult("time", "get_relative_world_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddTimexSlot("basis_time", "10 AM", ExtendedDateTime.Create(
                                TemporalType.Time,
                                new Dictionary<string, string>()
                                {
                                    ["hh"] = "10",
                                },
                                new TimexContext()
                                {
                                    ReferenceDateTime = _time.Time.UtcDateTime,
                                    UseInference = false
                                }))
                            .AddCanonicalizedSlot("basis_location", "CURRENT_LOCATION", "here")
                            .AddEntitySlot("query_location", "tokyo", tokyoEntity)
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494; // User context is in east coast US
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("get_relative_world_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("If it were 10:00 AM at your current location it would be 11:00 PM in Tokyo.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWorldTimeRelativeCurrentLocationTarget()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);

            KnowledgeContext kc = new KnowledgeContext();

            GeoCoordinates tokyoCoords = new GeoCoordinates(kc);
            tokyoCoords.Latitude_as_number.Value = 36.155462M;
            tokyoCoords.Longitude_as_number.Value = 136.761013M;
            Place tokyoEntity = new Place(kc);
            tokyoEntity.Geo_as_GeoCoordinates.SetValue(tokyoCoords);
            tokyoEntity.Name.Value = "Tokyo";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time would it be here if it was 10 AM in Tokyo", InputMethod.Typed)
                    .AddRecoResult("time", "get_relative_world_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddTimexSlot("basis_time", "10 AM", ExtendedDateTime.Create(
                                TemporalType.Time,
                                new Dictionary<string, string>()
                                {
                                    ["hh"] = "10",
                                },
                                new TimexContext()
                                {
                                    ReferenceDateTime = _time.Time.UtcDateTime,
                                    UseInference = false
                                }))
                            .AddCanonicalizedSlot("query_location", "CURRENT_LOCATION", "here")
                            .AddEntitySlot("basis_location", "tokyo", tokyoEntity)
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494; // User context is in east coast US
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("get_relative_world_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("If it were 10:00 AM in Tokyo it would be 9:00 PM the previous day at your current location.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWorldTimeRelativeAnaphoraBasis()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);

            KnowledgeContext kc = new KnowledgeContext();

            GeoCoordinates seattleCoords = new GeoCoordinates(kc);
            seattleCoords.Latitude_as_number.Value = 47.601871M;
            seattleCoords.Longitude_as_number.Value = -122.302294M;
            Place seattleEntity = new Place(kc);
            seattleEntity.Geo_as_GeoCoordinates.SetValue(seattleCoords);
            seattleEntity.Name.Value = "Seattle";
            await _testDriver.InjectEntityIntoGlobalContext(seattleEntity);

            GeoCoordinates tokyoCoords = new GeoCoordinates(kc);
            tokyoCoords.Latitude_as_number.Value = 36.155462M;
            tokyoCoords.Longitude_as_number.Value = 136.761013M;
            Place tokyoEntity = new Place(kc);
            tokyoEntity.Geo_as_GeoCoordinates.SetValue(tokyoCoords);
            tokyoEntity.Name.Value = "Tokyo";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time would it be in Tokyo if it was 10 AM over there", InputMethod.Typed)
                    .AddRecoResult("time", "get_relative_world_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddTimexSlot("basis_time", "10 AM", ExtendedDateTime.Create(
                                TemporalType.Time,
                                new Dictionary<string, string>()
                                {
                                    ["hh"] = "10",
                                },
                                new TimexContext()
                                {
                                    ReferenceDateTime = _time.Time.UtcDateTime,
                                    UseInference = false
                                }))
                            .AddCanonicalizedSlot("basis_location", "ANAPHORA", "over there")
                            .AddEntitySlot("query_location", "tokyo", tokyoEntity)
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494; // User context is in east coast US
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("get_relative_world_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("If it were 10:00 AM in Seattle it would be 2:00 AM the next day in Tokyo.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWorldTimeRelativeAnaphoraTarget()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);

            KnowledgeContext kc = new KnowledgeContext();

            GeoCoordinates seattleCoords = new GeoCoordinates(kc);
            seattleCoords.Latitude_as_number.Value = 47.601871M;
            seattleCoords.Longitude_as_number.Value = -122.302294M;
            Place seattleEntity = new Place(kc);
            seattleEntity.Geo_as_GeoCoordinates.SetValue(seattleCoords);
            seattleEntity.Name.Value = "Seattle";
            await _testDriver.InjectEntityIntoGlobalContext(seattleEntity);

            GeoCoordinates tokyoCoords = new GeoCoordinates(kc);
            tokyoCoords.Latitude_as_number.Value = 36.155462M;
            tokyoCoords.Longitude_as_number.Value = 136.761013M;
            Place tokyoEntity = new Place(kc);
            tokyoEntity.Geo_as_GeoCoordinates.SetValue(tokyoCoords);
            tokyoEntity.Name.Value = "Tokyo";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time would it be over there if it was 10 AM in Tokyo", InputMethod.Typed)
                    .AddRecoResult("time", "get_relative_world_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddTimexSlot("basis_time", "10 AM", ExtendedDateTime.Create(
                                TemporalType.Time,
                                new Dictionary<string, string>()
                                {
                                    ["hh"] = "10",
                                },
                                new TimexContext()
                                {
                                    ReferenceDateTime = _time.Time.UtcDateTime,
                                    UseInference = false
                                }))
                            .AddCanonicalizedSlot("query_location", "ANAPHORA", "over there")
                            .AddEntitySlot("basis_location", "tokyo", tokyoEntity)
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494; // User context is in east coast US
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("get_relative_world_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("If it were 10:00 AM in Tokyo it would be 6:00 PM the previous day in Seattle.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        /// <summary>
        /// Tests that if anaphora does not resolve any entities, the current user location will be used instead.
        /// TODO is that desirable? Or should we just skip?
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTimeWorldTimeRelativeAnaphoraDoesntExist()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);

            KnowledgeContext kc = new KnowledgeContext();

            GeoCoordinates tokyoCoords = new GeoCoordinates(kc);
            tokyoCoords.Latitude_as_number.Value = 36.155462M;
            tokyoCoords.Longitude_as_number.Value = 136.761013M;
            Place tokyoEntity = new Place(kc);
            tokyoEntity.Geo_as_GeoCoordinates.SetValue(tokyoCoords);
            tokyoEntity.Name.Value = "Tokyo";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time would it be over there if it was 10 AM in Tokyo", InputMethod.Typed)
                    .AddRecoResult("time", "get_relative_world_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddTimexSlot("basis_time", "10 AM", ExtendedDateTime.Create(
                                TemporalType.Time,
                                new Dictionary<string, string>()
                                {
                                    ["hh"] = "10",
                                },
                                new TimexContext()
                                {
                                    ReferenceDateTime = _time.Time.UtcDateTime,
                                    UseInference = false
                                }))
                            .AddCanonicalizedSlot("query_location", "ANAPHORA", "over there")
                            .AddEntitySlot("basis_location", "tokyo", tokyoEntity)
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494; // User context is in east coast US
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("get_relative_world_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("If it were 10:00 AM in Tokyo it would be 9:00 PM the previous day at your current location.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeGetNextDaylightSavingsTimeWinter()
        {
            _time.Time = new DateTimeOffset(2017, 1, 15, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "when does daylight savings time begin", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DAYLIGHT_SAVINGS", "daylight savings time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = 47.601871;
            request.ClientContext.Longitude = -122.302294;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("On March 12th the clock will skip forwards 1 hour.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeGetNextDaylightSavingsTimeNotInUse()
        {
            _time.Time = new DateTimeOffset(2017, 1, 15, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "when does daylight savings time begin", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DAYLIGHT_SAVINGS", "daylight savings time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = 36.385; // Tunisia
            request.ClientContext.Longitude = 9.9046;
            request.ClientContext.UserTimeZone = "Africa/Tunis";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("There is no daylight savings time in your area.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeGetNextDaylightSavingsTimeSummer()
        {
            _time.Time = new DateTimeOffset(2016, 6, 12, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "when does daylight savings time begin", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DAYLIGHT_SAVINGS", "daylight savings time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = 47.601871;
            request.ClientContext.Longitude = -122.302294;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("On November 6th the clock will skip backwards 1 hour.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeGetNextDaylightSavingsTimeInAFewDays()
        {
            _time.Time = new DateTimeOffset(2016, 11, 3, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "when does daylight savings time begin", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DAYLIGHT_SAVINGS", "daylight savings time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = 47.601871;
            request.ClientContext.Longitude = -122.302294;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("On Sunday at 2:00 AM the clock will skip backwards 1 hour.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeGetNextDaylightSavingsTimeToday()
        {
            _time.Time = new DateTimeOffset(2016, 11, 5, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(47);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "when does daylight savings time begin", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DAYLIGHT_SAVINGS", "daylight savings time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = null;
            request.ClientContext.ReferenceDateTime = null;
            request.ClientContext.Latitude = 47.601871;
            request.ClientContext.Longitude = -122.302294;

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("At 2:00 AM the clock will skip backwards 1 hour.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeTimerBasic()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "start a timer", InputMethod.Typed)
                    .AddRecoResult("time", "change_timer", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("action", "START", "start")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = -480;
            request.ClientContext.ReferenceDateTime = "2018-11-28T13:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("change_timer", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Timer started.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 51, TimeSpan.Zero);
            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "stop the timer", InputMethod.Typed)
                    .AddRecoResult("time", "change_timer", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("action", "STOP", "stop")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = -480;
            request.ClientContext.ReferenceDateTime = "2018-11-28T13:32:51";

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("change_timer", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Stopped timer after 40 seconds", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeTimerNotStarted()
        {
            _time.Time = new DateTimeOffset(2018, 11, 28, 21, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(103);
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "stop the timer", InputMethod.Typed)
                    .AddRecoResult("time", "change_timer", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("action", "STOP", "stop")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.UTCOffset = -480;
            request.ClientContext.ReferenceDateTime = "2018-11-28T13:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("change_timer", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Timer has not been started.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWhenIsThanksgiving()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(44);
            
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "when is thanksgiving", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddTimexSlot("time", "thanksgiving", ExtendedDateTime.Create(
                                TemporalType.Time,
                                new Dictionary<string, string>()
                                {
                                    ["OFFSET_ANCHOR"] = "11-01",
                                    ["OFFSET_UNIT"] = "thursday",
                                    ["OFFSET"] = "4",
                                },
                                new TimexContext()
                                {
                                    ReferenceDateTime = _time.Time.UtcDateTime,
                                    UseInference = false
                                }))
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494;
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Thanksgiving is on Thursday, November 22.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWhatDayOfMonthIsThansgiving()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(44);

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what day is thanksgiving", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DAY_OF_MONTH", "day")
                            .AddTimexSlot("time", "thanksgiving", ExtendedDateTime.Create(
                                TemporalType.Time,
                                new Dictionary<string, string>()
                                {
                                    ["OFFSET_ANCHOR"] = "11-01",
                                    ["OFFSET_UNIT"] = "thursday",
                                    ["OFFSET"] = "4",
                                },
                                new TimexContext()
                                {
                                    ReferenceDateTime = _time.Time.UtcDateTime,
                                    UseInference = false
                                }))
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494;
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Thanksgiving is on the 22nd.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWhatDayOfWeekIsThansgiving()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(44);

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what day of the week is thanksgiving", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DAY_OF_WEEK", "day of the week")
                            .AddTimexSlot("time", "thanksgiving", ExtendedDateTime.Create(
                                TemporalType.Time,
                                new Dictionary<string, string>()
                                {
                                    ["OFFSET_ANCHOR"] = "11-01",
                                    ["OFFSET_UNIT"] = "thursday",
                                    ["OFFSET"] = "4",
                                },
                                new TimexContext()
                                {
                                    ReferenceDateTime = _time.Time.UtcDateTime,
                                    UseInference = false
                                }))
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494;
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Thanksgiving falls on a Thursday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWhatDayOfWeekIsThe1st()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(44);

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what day of the week is the 1st", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("field", "DAY_OF_WEEK", "day of the week")
                            .AddTimexSlot("time", "the 1st", ExtendedDateTime.Create(
                                TemporalType.Time,
                                new Dictionary<string, string>()
                                {
                                    ["DD"] = "1",
                                },
                                new TimexContext()
                                {
                                    ReferenceDateTime = _time.Time.UtcDateTime,
                                    UseInference = false
                                }))
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494;
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("The 1st falls on a Saturday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWhenIsToolTime()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(44);

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "when is tool time", InputMethod.Typed)
                    .AddRecoResult("time", "query_time", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("time", "tool time")
                        .Build()
                    .Build()
                .Build();
            request.ClientContext.Latitude = 35.307494;
            request.ClientContext.Longitude = -77.612299;
            request.ClientContext.UTCOffset = -240;
            request.ClientContext.ReferenceDateTime = "2018-08-31T14:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_time", response.SelectedRecoResult.Intent);
            Assert.AreEqual("I don't know when tool time is.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWhatTimeZoneIsParisInAhead()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(97);

            KnowledgeContext kc = new KnowledgeContext();
            GeoCoordinates placeCoords = new GeoCoordinates(kc);
            placeCoords.Latitude_as_number.Value = 48.855118M;
            placeCoords.Longitude_as_number.Value = 2.346548M;
            Place locationEntity = new Place(kc);
            locationEntity.Geo_as_GeoCoordinates.SetValue(placeCoords);
            locationEntity.Name.Value = "Paris";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time zone is paris in", InputMethod.Typed)
                    .AddRecoResult("time", "query_timezone", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddEntitySlot("location", "paris", locationEntity)
                        .Build()
                    .Build()
                .Build();

            // User in USA
            request.ClientContext.Latitude = 47.601871;
            request.ClientContext.Longitude = -122.302294;
            request.ClientContext.UTCOffset = -420;
            request.ClientContext.ReferenceDateTime = "2018-08-31T11:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_timezone", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Paris is 9 hours ahead of you, and the local time there is 8:32 PM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWhatTimeZoneIsParisInBehind()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(122);

            KnowledgeContext kc = new KnowledgeContext();
            GeoCoordinates placeCoords = new GeoCoordinates(kc);
            placeCoords.Latitude_as_number.Value = 48.855118M;
            placeCoords.Longitude_as_number.Value = 2.346548M;
            Place locationEntity = new Place(kc);
            locationEntity.Geo_as_GeoCoordinates.SetValue(placeCoords);
            locationEntity.Name.Value = "Paris";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time zone is paris in", InputMethod.Typed)
                    .AddRecoResult("time", "query_timezone", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddEntitySlot("location", "paris", locationEntity)
                        .Build()
                    .Build()
                .Build();

            // User in Kyev
            request.ClientContext.Latitude = 50.452258;
            request.ClientContext.Longitude = 30.522766;
            request.ClientContext.UTCOffset = 120;
            request.ClientContext.ReferenceDateTime = "2018-08-31T22:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_timezone", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Paris is 1 hours behind you, and the local time there is 8:32 PM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestTimeWhatTimeZoneIsParisInSameZone()
        {
            _time.Time = new DateTimeOffset(2018, 08, 31, 18, 32, 11, TimeSpan.Zero);
            _rand.SeedRand(34);

            KnowledgeContext kc = new KnowledgeContext();
            GeoCoordinates placeCoords = new GeoCoordinates(kc);
            placeCoords.Latitude_as_number.Value = 48.855118M;
            placeCoords.Longitude_as_number.Value = 2.346548M;
            Place locationEntity = new Place(kc);
            locationEntity.Geo_as_GeoCoordinates.SetValue(placeCoords);
            locationEntity.Name.Value = "Paris";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what time zone is paris in", InputMethod.Typed)
                    .AddRecoResult("time", "query_timezone", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddEntitySlot("location", "paris", locationEntity)
                        .Build()
                    .Build()
                .Build();

            // User in France
            request.ClientContext.Latitude = 48.855118;
            request.ClientContext.Longitude = 2.346548;
            request.ClientContext.UTCOffset = -120;
            request.ClientContext.ReferenceDateTime = "2018-08-31T20:32:11";

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("query_timezone", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Paris is in the same timezone as you, and the local time is 8:32 PM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        #endregion
    }
}
