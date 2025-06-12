using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.ExternalServices.Darksky
{
    /// <summary>
    /// Implements the Darksky weather API for retrieving current, historical, or forecasted weather conditions.
    /// https://darksky.net/dev/docs
    /// </summary>
    [Obsolete("DarkSky API is dead now")]
    public class DarkskyApi
    {
        private readonly string _apiKey;
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;

        /// <summary>
        /// Constructs a new Darksky API interface
        /// </summary>
        /// <param name="httpClientFactory">A factory for HTTP clients</param>
        /// <param name="logger">A logger</param>
        /// <param name="apiKey">The Darksky API key</param>
        public DarkskyApi(IHttpClientFactory httpClientFactory, ILogger logger, string apiKey)
        {
            _logger = logger ?? NullLogger.Singleton;
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException("Darksky API key is required");
            }

            _apiKey = apiKey;
            _httpClient = httpClientFactory.CreateHttpClient("api.darksky.net", 443, true, NullLogger.Singleton);
        }

        /// <summary>
        /// Retrieves weather information, either for current conditions, historical data, or a future forecast.
        /// </summary>
        /// <param name="coord">The geocoordinate which you would like the forecast for</param>
        /// <param name="cancelToken">A cancel token for the operation</param>
        /// <param name="realTime">A defintion of real time</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <param name="requestedData">The set of flags representing data sets you want to retrieve</param>
        /// <param name="language">The language code to use for localizing human-readable weather data</param>
        /// <param name="units">The units to use for observations</param>
        /// <param name="queryTime">If specified, retrieves either historical data or a future forecast</param>
        /// <returns></returns>
        public async Task<DarkskyWeatherResult> GetWeatherData(
            GeoCoordinate coord,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            ILogger queryLogger = null,
            DarkskyRequestFeatures requestedData = DarkskyRequestFeatures.Default,
            string language = "en",
            DarkskyMeasurementUnit units = DarkskyMeasurementUnit.Auto,
            DateTimeOffset? queryTime = null)
        {
            queryLogger = queryLogger ?? NullLogger.Singleton;

            List<string> excludeList = new List<string>();
            if (!requestedData.HasFlag(DarkskyRequestFeatures.CurrentWeather))
            {
                excludeList.Add("currently");
            }
            if (!requestedData.HasFlag(DarkskyRequestFeatures.MinutelyWeather))
            {
                excludeList.Add("minutely");
            }
            if (!requestedData.HasFlag(DarkskyRequestFeatures.HourlyWeather))
            {
                excludeList.Add("hourly");
            }
            if (!requestedData.HasFlag(DarkskyRequestFeatures.DailyWeather))
            {
                excludeList.Add("daily");
            }
            if (!requestedData.HasFlag(DarkskyRequestFeatures.Alerts))
            {
                excludeList.Add("alerts");
            }
            if (!requestedData.HasFlag(DarkskyRequestFeatures.Flags))
            {
                excludeList.Add("flags");
            }

            string excludeListString = excludeList.Count == 0 ? string.Empty : "&exclude=" + string.Join(",", excludeList);
            string extendParam = requestedData.HasFlag(DarkskyRequestFeatures.ExtendedHourlyForecast) ? "&extend=hourly" : string.Empty;

            string urlPath;
            if (!queryTime.HasValue)
            {
                // Current conditions request
                urlPath = string.Format("/forecast/{0}/{1},{2}?lang={3}&units={4}{5}{6}", _apiKey, coord.Latitude, coord.Longitude, language, units.ToQueryParamString(), excludeListString, extendParam);
            }
            else
            {
                // Time machine request
                long epochTime = (queryTime.Value - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)).Ticks / 10000000L;
                urlPath = string.Format("/forecast/{0}/{1},{2},{3}?lang={4}&units={5}{6}{7}", _apiKey, coord.Latitude, coord.Longitude, epochTime, language, units.ToQueryParamString(), excludeListString, extendParam);
            }

            using (HttpRequest apiRequest = HttpRequest.CreateOutgoing(urlPath))
            using (HttpResponse apiResponse = await _httpClient.SendRequestAsync(
                    apiRequest,
                    cancelToken,
                    realTime,
                    NullLogger.Singleton).ConfigureAwait(false)) // Use null logger here so we do not log our secret API key
            {
                if (apiResponse == null)
                {
                    queryLogger.Log("Null response from Darksky!", LogLevel.Err);
                    return null;
                }
                try
                {
                    if (apiResponse.ResponseCode != 200)
                    {
                        queryLogger.Log("Error response from Darksky!", LogLevel.Err);
                        string responseMessage = await apiResponse.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(responseMessage))
                        {
                            queryLogger.Log(responseMessage, LogLevel.Err);
                        }

                        return null;
                    }

                    DarkskyWeatherResult returnVal = await apiResponse.ReadContentAsJsonObjectAsync<DarkskyWeatherResult>(cancelToken, realTime);
                    return returnVal;
                }
                finally
                {
                    if (apiResponse != null)
                    {
                        await apiResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
