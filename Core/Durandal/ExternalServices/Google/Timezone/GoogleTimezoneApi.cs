using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Durandal.Common.Time.TimeZone;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;

namespace Durandal.ExternalServices.Google.Timezone
{
    public class GoogleTimezoneAPI
    {
        private readonly string _apiKey;
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;

        public GoogleTimezoneAPI(string apiKey, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _apiKey = apiKey;
            _logger = logger;
            _httpClient = httpClientFactory.CreateHttpClient("maps.googleapis.com", 443, true, logger);
        }

        public async Task<TimeZoneQueryResult> Query(GeoCoordinate coord, DateTimeOffset utcTime, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _logger.Log("Querying " + coord.Latitude + "," + coord.Longitude);
            using (HttpRequest request = HttpRequest.CreateOutgoing("/maps/api/timezone/json", "GET"))
            {
                request.GetParameters["location"] = string.Format("{0},{1}", coord.Latitude, coord.Longitude);
                request.GetParameters["timestamp"] = (utcTime - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalSeconds.ToString();
                request.GetParameters["key"] = _apiKey;

                using (NetworkResponseInstrumented<HttpResponse> resp = await _httpClient.SendInstrumentedRequestAsync(
                    request, cancelToken, realTime, NullLogger.Singleton).ConfigureAwait(false))
                {
                    try
                    {
                        if (!resp.Success)
                        {
                            _logger.Log("Request FAILED!", LogLevel.Err);
                            return null;
                        }

                        if (resp.Response.ResponseCode != 200)
                        {
                            _logger.Log("Request FAILED! Http code " + resp.Response.ResponseCode, LogLevel.Err);
                            _logger.Log(await resp.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false), LogLevel.Err);
                            return null;
                        }

                        GoogleResp tzResp = JsonConvert.DeserializeObject<GoogleResp>(await resp.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false));
                        if (tzResp == null)
                        {
                            _logger.Log("No timezone in response", LogLevel.Err);
                            return null;
                        }

                        if (string.Equals("ZERO_RESULTS", tzResp.status))
                        {
                            queryLogger.Log("No time zone found! Falling back to mariners' time", LogLevel.Wrn);
                            return TimeZoneHelpers.CalculateMarinersTime(coord, utcTime);
                        }
                        else if (!string.Equals("OK", tzResp.status))
                        {
                            _logger.Log("No timezone in response: status was " + tzResp.status, LogLevel.Err);
                            return null;
                        }

                        TimeSpan gmtOffset = TimeSpan.FromSeconds(tzResp.rawOffset);
                        TimeSpan dstOffset = TimeSpan.FromSeconds(tzResp.dstOffset);

                        return new TimeZoneQueryResult()
                        {
                            LocalTime = utcTime.ToOffset(gmtOffset + dstOffset),
                            GmtOffset = gmtOffset,
                            DstOffset = dstOffset,
                            QueryCoordinate = coord,
                            TimeZoneAbbreviation = tzResp.timeZoneId,
                            TimeZoneName = tzResp.timeZoneName,
                            TimeZoneBaseCoordinate = coord
                        };
                    }
                    finally
                    {
                        if (resp != null)
                        {
                            await resp.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

#pragma warning disable CS0649
        private class GoogleResp
        {
            public long dstOffset;
            public long rawOffset;
            public string status;
            public string timeZoneId;
            public string timeZoneName;
        }
#pragma warning restore CS0649
    }
}
