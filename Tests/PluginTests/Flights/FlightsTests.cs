using Durandal;
using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using Durandal.Common.Test;
using Durandal.Common.Test.Builders;
using Durandal.Common.Time;
using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.UnitConversion;
using Durandal.Plugins.Fitbit;
using Durandal.Plugins.Fitbit.Schemas;
using Durandal.Plugins.Fitbit.Schemas.Responses;
using Durandal.Plugins.Flights;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DialogTests.Plugins.Flights
{
    [TestClass]
    public class FlightsTests
    {
        private static FlightsPlugin _plugin;
        private static InqueTestDriver _testDriver;
        private static FakeFlightStatsService _mockService;
        private static ManualTimeProvider _realTime;
    
        #region Test framework

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _mockService = new FakeFlightStatsService();
            _realTime = new ManualTimeProvider();
            _realTime.Time = new DateTimeOffset(2019, 01, 10, 16, 00, 00, TimeSpan.Zero);
            _plugin = new FlightsPlugin(new DirectHttpClientFactory(_mockService), _realTime);
            string rootEnv = context.Properties["DurandalRootDirectory"]?.ToString();
            if (string.IsNullOrEmpty(rootEnv))
            {
                rootEnv = Environment.GetEnvironmentVariable("DURANDAL_ROOT");
                if (string.IsNullOrEmpty(rootEnv))
                {
                    throw new FileNotFoundException("Cannot find durandal environment directory, either from DurandalRootDirectory test property, or DURANDAL_ROOT environment variable.");
                }
            }

            InqueTestParameters testConfig = PluginTestCommon.CreateTestParameters(_plugin, "FlightsPlugin.dupkg", new DirectoryInfo(rootEnv));
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
            _mockService.HttpResponseCode = 404;
            _mockService.RawSearchResponse = null;
        }

        #endregion

        #region Tests
        [TestMethod]
        public async Task TestFlightsGetStatusBasic()
        {
            _realTime.Time = new DateTimeOffset(2019, 03, 27, 16, 05, 11, TimeSpan.Zero);
            _mockService.HttpResponseCode = 200;
            _mockService.RawSearchResponse = "{\"request\":{\"airline\":{\"requestedCode\":\"DL\",\"fsCode\":\"DL\"},\"flight\":{\"requested\":\"1368\",\"interpreted\":\"1368\"},\"date\":{\"year\":\"2019\",\"month\":\"3\",\"day\":\"27\",\"interpreted\":\"2019-03-27\"},\"utc\":{\"interpreted\":false},\"airport\":{},\"codeType\":{},\"extendedOptions\":{},\"url\":\"https://api.flightstats.com/flex/flightstatus/rest/v2/json/flight/status/DL/1368/arr/2019/03/27\"},\"appendix\":{\"airlines\":[{\"fs\":\"KL\",\"iata\":\"KL\",\"icao\":\"KLM\",\"name\":\"KLM\",\"phoneNumber\":\"1-800-447-4747\",\"active\":true},{\"fs\":\"AF\",\"iata\":\"AF\",\"icao\":\"AFR\",\"name\":\"Air France\",\"phoneNumber\":\"1-800-237-2747\",\"active\":true},{\"fs\":\"DL\",\"iata\":\"DL\",\"icao\":\"DAL\",\"name\":\"Delta Air Lines\",\"phoneNumber\":\"1-800-221-1212\",\"active\":true},{\"fs\":\"KE\",\"iata\":\"KE\",\"icao\":\"KAL\",\"name\":\"Korean Air\",\"phoneNumber\":\"1-800-438-5000\",\"active\":true},{\"fs\":\"VS\",\"iata\":\"VS\",\"icao\":\"VIR\",\"name\":\"Virgin Atlantic\",\"active\":true},{\"fs\":\"9W\",\"iata\":\"9W\",\"icao\":\"JAI\",\"name\":\"Jet Airways (India)\",\"phoneNumber\":\"0808 101 1199 (UK toll free reservations)\",\"active\":true}],\"airports\":[{\"fs\":\"SAN\",\"iata\":\"SAN\",\"icao\":\"KSAN\",\"faa\":\"SAN\",\"name\":\"San Diego International Airport\",\"street1\":\"Airport Drive\",\"street2\":\"\",\"city\":\"San Diego\",\"cityCode\":\"SAN\",\"stateCode\":\"CA\",\"postalCode\":\"92101\",\"countryCode\":\"US\",\"countryName\":\"United States\",\"regionName\":\"North America\",\"timeZoneRegionName\":\"America/Los_Angeles\",\"weatherZone\":\"CAZ043\",\"localTime\":\"2019-03-27T08:48:49.938\",\"utcOffsetHours\":-7.0,\"latitude\":32.731938,\"longitude\":-117.197312,\"elevationFeet\":14,\"classification\":1,\"active\":true,\"delayIndexUrl\":\"https://api.flightstats.com/flex/delayindex/rest/v1/json/airports/SAN?codeType=fs\",\"weatherUrl\":\"https://api.flightstats.com/flex/weather/rest/v1/json/all/SAN?codeType=fs\"},{\"fs\":\"SEA\",\"iata\":\"SEA\",\"icao\":\"KSEA\",\"faa\":\"SEA\",\"name\":\"Seattle-Tacoma International Airport\",\"city\":\"Seattle\",\"cityCode\":\"SEA\",\"stateCode\":\"WA\",\"postalCode\":\"98158\",\"countryCode\":\"US\",\"countryName\":\"United States\",\"regionName\":\"North America\",\"timeZoneRegionName\":\"America/Los_Angeles\",\"weatherZone\":\"WAZ001\",\"localTime\":\"2019-03-27T08:48:49.938\",\"utcOffsetHours\":-7.0,\"latitude\":47.443839,\"longitude\":-122.301732,\"elevationFeet\":429,\"classification\":1,\"active\":true,\"delayIndexUrl\":\"https://api.flightstats.com/flex/delayindex/rest/v1/json/airports/SEA?codeType=fs\",\"weatherUrl\":\"https://api.flightstats.com/flex/weather/rest/v1/json/all/SEA?codeType=fs\"}],\"equipments\":[{\"iata\":\"738\",\"name\":\"Boeing 737-800 Passenger\",\"turboProp\":false,\"jet\":true,\"widebody\":false,\"regional\":false}]},\"flightStatuses\":[{\"flightId\":994374368,\"carrierFsCode\":\"DL\",\"flightNumber\":\"1368\",\"departureAirportFsCode\":\"SEA\",\"arrivalAirportFsCode\":\"SAN\",\"departureDate\":{\"dateLocal\":\"2019-03-27T15:21:00.000\",\"dateUtc\":\"2019-03-27T22:21:00.000Z\"},\"arrivalDate\":{\"dateLocal\":\"2019-03-27T18:12:00.000\",\"dateUtc\":\"2019-03-28T01:12:00.000Z\"},\"status\":\"S\",\"schedule\":{\"flightType\":\"J\",\"serviceClasses\":\"JY\",\"restrictions\":\"\",\"downlines\":[{\"fsCode\":\"SEA\",\"flightId\":994374388}]},\"operationalTimes\":{\"publishedDeparture\":{\"dateLocal\":\"2019-03-27T15:21:00.000\",\"dateUtc\":\"2019-03-27T22:21:00.000Z\"},\"publishedArrival\":{\"dateLocal\":\"2019-03-27T18:12:00.000\",\"dateUtc\":\"2019-03-28T01:12:00.000Z\"},\"scheduledGateDeparture\":{\"dateLocal\":\"2019-03-27T15:21:00.000\",\"dateUtc\":\"2019-03-27T22:21:00.000Z\"},\"flightPlanPlannedDeparture\":{\"dateLocal\":\"2019-03-27T15:28:00.000\",\"dateUtc\":\"2019-03-27T22:28:00.000Z\"},\"scheduledGateArrival\":{\"dateLocal\":\"2019-03-27T18:12:00.000\",\"dateUtc\":\"2019-03-28T01:12:00.000Z\"},\"flightPlanPlannedArrival\":{\"dateLocal\":\"2019-03-27T17:49:00.000\",\"dateUtc\":\"2019-03-28T00:49:00.000Z\"}},\"codeshares\":[{\"fsCode\":\"9W\",\"flightNumber\":\"8351\",\"relationship\":\"L\"},{\"fsCode\":\"AF\",\"flightNumber\":\"9264\",\"relationship\":\"L\"},{\"fsCode\":\"KE\",\"flightNumber\":\"3081\",\"relationship\":\"L\"},{\"fsCode\":\"KL\",\"flightNumber\":\"6288\",\"relationship\":\"L\"},{\"fsCode\":\"VS\",\"flightNumber\":\"3327\",\"relationship\":\"L\"}],\"flightDurations\":{\"scheduledBlockMinutes\":171,\"scheduledAirMinutes\":141,\"scheduledTaxiOutMinutes\":7,\"scheduledTaxiInMinutes\":23},\"airportResources\":{\"departureTerminal\":\"B\",\"departureGate\":\"3\",\"arrivalTerminal\":\"2\",\"arrivalGate\":\"46\"},\"flightEquipment\":{\"scheduledEquipmentIataCode\":\"738\",\"actualEquipmentIataCode\":\"738\",\"tailNumber\":\"N394DA\"}},{\"flightId\":994374388,\"carrierFsCode\":\"DL\",\"flightNumber\":\"1368\",\"departureAirportFsCode\":\"SAN\",\"arrivalAirportFsCode\":\"SEA\",\"departureDate\":{\"dateLocal\":\"2019-03-27T18:57:00.000\",\"dateUtc\":\"2019-03-28T01:57:00.000Z\"},\"arrivalDate\":{\"dateLocal\":\"2019-03-27T22:02:00.000\",\"dateUtc\":\"2019-03-28T05:02:00.000Z\"},\"status\":\"S\",\"schedule\":{\"flightType\":\"J\",\"serviceClasses\":\"JY\",\"restrictions\":\"\",\"uplines\":[{\"fsCode\":\"SEA\",\"flightId\":994374368}]},\"operationalTimes\":{\"publishedDeparture\":{\"dateLocal\":\"2019-03-27T18:57:00.000\",\"dateUtc\":\"2019-03-28T01:57:00.000Z\"},\"publishedArrival\":{\"dateLocal\":\"2019-03-27T22:02:00.000\",\"dateUtc\":\"2019-03-28T05:02:00.000Z\"},\"scheduledGateDeparture\":{\"dateLocal\":\"2019-03-27T18:57:00.000\",\"dateUtc\":\"2019-03-28T01:57:00.000Z\"},\"flightPlanPlannedDeparture\":{\"dateLocal\":\"2019-03-27T19:00:00.000\",\"dateUtc\":\"2019-03-28T02:00:00.000Z\"},\"scheduledGateArrival\":{\"dateLocal\":\"2019-03-27T22:02:00.000\",\"dateUtc\":\"2019-03-28T05:02:00.000Z\"},\"flightPlanPlannedArrival\":{\"dateLocal\":\"2019-03-27T21:13:00.000\",\"dateUtc\":\"2019-03-28T04:13:00.000Z\"}},\"flightDurations\":{\"scheduledBlockMinutes\":185,\"scheduledAirMinutes\":133,\"scheduledTaxiOutMinutes\":3,\"scheduledTaxiInMinutes\":49},\"airportResources\":{\"departureTerminal\":\"2\",\"departureGate\":\"46\",\"arrivalTerminal\":\"S\",\"arrivalGate\":\"12\"},\"flightEquipment\":{\"scheduledEquipmentIataCode\":\"738\",\"actualEquipmentIataCode\":\"738\",\"tailNumber\":\"N394DA\"}}]}";
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "status of delta flight 1368", InputMethod.Typed)
                    .AddRecoResult("flights", "get_status", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("airline", "delta")
                            .AddBasicSlot("flight_num", "1368")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.AreEqual("get_status", response.SelectedRecoResult.Intent);
            Assert.AreEqual("Delta Air Lines flight 1368 will be landing in Seattle at 10:02 PM local time.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFlightsAPIErrorResponse()
        {
            _mockService.HttpResponseCode = 200;
            _mockService.RawSearchResponse = "{\"request\":{\"airline\":{\"requestedCode\":\"DL\",\"fsCode\":\"DL\"},\"flight\":{\"requested\":\"1368\",\"interpreted\":\"1368\"},\"date\":{\"year\":\"2019\",\"month\":\"3\",\"day\":\"27\",\"interpreted\":\"2019-03-27\"},\"utc\":{\"interpreted\":false},\"airport\":{},\"codeType\":{},\"extendedOptions\":{},\"url\":\"https://api.flightstats.com/flex/flightstatus/rest/v2/json/flight/status/DL/1368/arr/2019/03/27\"},\"appendix\":{\"airlines\":[]},\"error\":{\"httpStatusCode\":403,\"errorCode\":\"AUTH_FAILURE\",\"errorId\":\"82e28dcb-7ed0-43b5-9da5-2d2ab8015119\",\"errorMessage\":\"Authorization failed. application is not active\"},\"flightStatuses\":[]}";
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "status of delta flight 1368", InputMethod.Typed)
                    .AddRecoResult("flights", "get_status", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("airline", "delta")
                            .AddBasicSlot("flight_num", "1368")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Failure, response.ExecutionResult);
        }
        
        #endregion

        public class FakeFlightStatsService : IHttpServerDelegate
        {
            private readonly ILogger _logger;

            public FakeFlightStatsService()
            {
                RawSearchResponse = string.Empty;
                HttpResponseCode = 200;
                _logger = new ConsoleLogger("FlightsServer");
            }

            public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse resp = await HandleConnectionInternal(serverContext.HttpRequest, cancelToken, realTime).ConfigureAwait(false);
                if (resp != null)
                {
                    try
                    {
                        await serverContext.WritePrimaryResponse(resp, _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                }
            }

            private Task<HttpResponse> HandleConnectionInternal(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse response = HttpResponse.CreateOutgoing();
                response.ResponseCode = HttpResponseCode;
                if (!string.IsNullOrEmpty(RawSearchResponse))
                {
                    response.SetContent(RawSearchResponse, HttpConstants.MIME_TYPE_JSON);
                }

                return Task.FromResult(response);
            }
            
            public string RawSearchResponse { get; set; }
            public int HttpResponseCode { get; set; }
        }
    }
}
