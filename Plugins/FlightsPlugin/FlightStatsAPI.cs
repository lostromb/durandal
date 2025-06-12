using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using Durandal.Plugins.Flights.Schema;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Plugins.Flights
{
    public class FlightStatsAPI
    {
        // https://developer.flightstats.com/api-docs/flightstatus/v2/flight
        // "L" = landed, "A" = in air, "S" = scheduled

        private readonly string _appId;
        private readonly string _apiKey;
        private readonly IHttpClient _httpClient;

        public FlightStatsAPI(string appId, string apiKey, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _appId = appId;
            _apiKey = apiKey;
            _httpClient = httpClientFactory.CreateHttpClient("api.flightstats.com", 443, true, logger);
        }

        public async Task<FlightStatusAPIResponse> GetFlightStatus(string carrierId, string flightId, DateTimeOffset queryTime, ILogger queryLogger)
        {
            string todayDateString = queryTime.ToString("yyyy/MM/dd");
            string url = string.Format("/flex/flightstatus/rest/v2/json/flight/status/{0}/{1}/arr/{2}?appId={3}&appKey={4}", carrierId, flightId, todayDateString, _appId, _apiKey);
            using (HttpRequest request = HttpRequest.CreateOutgoing(url))
            using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(
                request,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton,
                NullLogger.Singleton).ConfigureAwait(false)) // need to use a null logger because our API key is in the URL
            {
                try
                {
                    if (!netResponse.Success)
                    {
                        queryLogger.Log("Null response from FlightStats API (no network connectivity?)", LogLevel.Err);
                        return null;
                    }

                    if (netResponse.Response.ResponseCode != 200)
                    {
                        queryLogger.Log("Non-success response " + netResponse.Response.ResponseCode + " from FlightStats API", LogLevel.Err);
                        return null;
                    }

                    //queryLogger.Log("Error response from FlightStats API", LogLevel.Err);
                    string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                    queryLogger.Log(responseJson);

                    if (string.IsNullOrEmpty(responseJson))
                    {
                        queryLogger.Log("Empty response from FlightStats API", LogLevel.Err);
                        return null;
                    }

                    FlightStatusAPIResponse response = JsonConvert.DeserializeObject<FlightStatusAPIResponse>(responseJson);

                    if (response.Error != null &&
                        response.Error.HttpStatusCode != 200)
                    {
                        queryLogger.Log("FlightStats API returned an error: " + response.Error.HttpStatusCode + " " + response.Error.ErrorCode + ": " + response.Error.ErrorMessage, LogLevel.Err);
                        return null;
                    }

                    return response;
                }
                finally
                {
                    if (netResponse != null)
                    {
                        await netResponse.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
