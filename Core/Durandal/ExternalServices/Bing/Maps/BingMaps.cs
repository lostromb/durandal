namespace Durandal.ExternalServices.Bing.Maps
{
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Statistics;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class BingMaps
    {
        private static readonly Regex zoomParamFinder = new Regex("zoom=([0-9]+)");

        private readonly IHttpClient _webClient;
        private readonly string _apiKey;

        public BingMaps(string apiKey, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _apiKey = apiKey;
            _webClient = httpClientFactory.CreateHttpClient("dev.virtualearth.net", 443, true, logger);
        }

        /// <summary>
        /// Gets a map image as a PNG binary file
        /// </summary>
        /// <param name="inputLocation">A structured input entity. For now schema.org/Place is the only accepted schema</param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="locale"></param>
        /// <param name="zoom">Logarithmic zoom level from 1 (planet) to 20 (street level)</param>
        /// <param name="mapWidth">Desired image width in pixels</param>
        /// <param name="mapHeight">Desired image height in pixels</param>
        /// <param name="imageryType">The type of imagery to request (satellite, simplified map, topo, etc.)</param>
        /// <returns></returns>
        public async Task<ArraySegment<byte>> GetMapImage(
            BingMapsPlace inputLocation,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            LanguageCode locale,
            int zoom = 14,
            int mapWidth = 1000,
            int mapHeight = 600,
            MapImageryType imageryType = MapImageryType.Road)
        {
            GeoCoordinate? centerCoord = inputLocation.CenterCoord;

            if (inputLocation.BoundingBoxLowerRight.HasValue &&
                    inputLocation.BoundingBoxUpperLeft.HasValue)
            {
                // Alter zoom based on the relative size of the bounding box, if present
                double radiusMeters = GeoMath.CalculateGeoDistance(inputLocation.BoundingBoxUpperLeft.Value, inputLocation.BoundingBoxLowerRight.Value) / 2;
                zoom = Math.Max(10, Math.Min(18, 20 - (int)Math.Round((Math.Log10(radiusMeters) / Math.Log10(5)))));
            }

            if (!centerCoord.HasValue || centerCoord.Value.Latitude == 0 || centerCoord.Value.Longitude == 0)
            {
                // No center coordinate, so we can't continue
                // TODO should really throw an exception here
                return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
            }

            using (HttpRequest req = HttpRequest.CreateOutgoing(string.Format("/REST/v1/Imagery/Map/Road/{0},{1}/{2}",
                centerCoord.Value.Latitude,
                centerCoord.Value.Longitude,
                zoom), "GET"))
            {
                req.GetParameters["pushpin"] = string.Format("{0},{1}",
                    centerCoord.Value.Latitude,
                    centerCoord.Value.Longitude);
                req.GetParameters["key"] = _apiKey;
                req.GetParameters["userIp"] = "127.0.0.1";
                req.GetParameters["ms"] = string.Format("{0},{1}",
                    mapWidth, mapHeight);
                if (locale != null)
                {
                    req.GetParameters["c"] = locale.ToBcp47Alpha2String();
                }

                using (HttpResponse resp = await _webClient.SendRequestAsync(
                    req,
                    cancelToken,
                    realTime,
                    NullLogger.Singleton).ConfigureAwait(false)) // Use a null logger so the client does not log the request URL which contains secret API key
                {
                    try
                    {
                        if (resp == null)
                        {
                            queryLogger.Log("Null response from maps service!", LogLevel.Err);
                            return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
                        }

                        if (resp.ResponseCode != 200)
                        {
                            queryLogger.Log("Error response from maps service! " + resp.ResponseCode + " " + resp.ResponseMessage, LogLevel.Err);
                            return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
                        }

                        return await resp.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (resp != null)
                        {
                            await resp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets a route between two points as a PNG binary image file
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <param name="queryLogger"></param>
        /// <param name="locale"></param>
        /// <param name="mapWidth"></param>
        /// <param name="mapHeight"></param>
        /// <param name="imageryType"></param>
        /// <returns></returns>
        public async Task<ArraySegment<byte>> GetRouteImage(
            BingMapsPlace origin,
            BingMapsPlace destination,
            ILogger queryLogger,
            LanguageCode locale,
            int mapWidth = 1000,
            int mapHeight = 600,
            MapImageryType imageryType = MapImageryType.Road)
        {
            if (!origin.CenterCoord.HasValue || origin.CenterCoord.Value.Latitude == 0 || origin.CenterCoord.Value.Longitude == 0)
            {
                return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
            }

            if (!destination.CenterCoord.HasValue || destination.CenterCoord.Value.Latitude == 0 || destination.CenterCoord.Value.Longitude == 0)
            {
                return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
            }

            return await GetRouteImage(origin, destination, queryLogger, locale, mapWidth, mapHeight).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a route between two points as a PNG binary image file
        /// </summary>
        /// <param name="originCoords"></param>
        /// <param name="destinationCoords"></param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="locale"></param>
        /// <param name="mapWidth"></param>
        /// <param name="mapHeight"></param>
        /// <param name="imageryType"></param>
        /// <returns></returns>
        public async Task<ArraySegment<byte>> GetRouteImage(
            GeoCoordinate originCoords,
            GeoCoordinate destinationCoords,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            LanguageCode locale,
            int mapWidth = 1000,
            int mapHeight = 600,
            MapImageryType imageryType = MapImageryType.Road)
        {
            using (HttpRequest req = HttpRequest.CreateOutgoing("/REST/v1/Imagery/Map/Road/Routes", "GET"))
            {
                req.GetParameters["wp.0"] = string.Format("{0},{1}",
                    originCoords.Latitude,
                    originCoords.Longitude);
                req.GetParameters["wp.1"] = string.Format("{0},{1}",
                    destinationCoords.Latitude,
                    destinationCoords.Longitude);
                req.GetParameters["key"] = _apiKey;
                req.GetParameters["userIp"] = "127.0.0.1";
                req.GetParameters["ms"] = string.Format("{0},{1}",
                    mapWidth, mapHeight);

                if (locale != null)
                {
                    req.GetParameters["c"] = locale.ToBcp47Alpha2String();
                }

                using (HttpResponse resp = await _webClient.SendRequestAsync(
                    req,
                    cancelToken,
                    realTime,
                    NullLogger.Singleton).ConfigureAwait(false)) // Send a null logger so the client does not log the request URL which contains API key
                {
                    try
                    {
                        if (resp == null)
                        {
                            queryLogger.Log("Null response from maps service!", LogLevel.Err);
                            return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
                        }

                        if (resp.ResponseCode != 200)
                        {
                            queryLogger.Log("Error response from maps service! " + resp.ResponseCode + " " + resp.ResponseMessage, LogLevel.Err);
                            return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
                        }

                        return await resp.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (resp != null)
                        {
                            await resp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Given a place with partial data (such as missing address line or missing coordinate), attempt to use bing maps to fill in the missing data.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="locale"></param>
        /// <param name="referenceCoord"></param>
        /// <returns></returns>
        public async Task<Hypothesis<BingMapsPlace>?> Hydrate(
            BingMapsPlace input,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            LanguageCode locale = null,
            GeoCoordinate? referenceCoord = null)
        {
            // Are we doing forward or reverse geocode?
            if (!input.CenterCoord.HasValue || input.CenterCoord.Value.Longitude == 0 || input.CenterCoord.Value.Latitude == 0)
            {
                List<Hypothesis<BingMapsPlace>> locationHyps = await ForwardGeocode(input, queryLogger, cancelToken, realTime, locale, referenceCoord).ConfigureAwait(false);

                // If no match, return error
                if (locationHyps == null || locationHyps.Count == 0)
                {
                    return null;
                }
                else
                {
                    // Just select the first location result and return it
                    return locationHyps[0];
                }
            }
            else
            {
                IList<Hypothesis<BingMapsPlace>> reverseGeocodeResults = await ReverseGeocode(input.CenterCoord.Value, queryLogger, cancelToken, realTime, locale).ConfigureAwait(false);
                if (reverseGeocodeResults == null || reverseGeocodeResults.Count == 0)
                {
                    return null;
                }

                return reverseGeocodeResults[0];
            }
        }

        /// <summary>
        /// Resolves a street address / postal code location into a set of formal place name hypotheses
        /// </summary>
        /// <param name="input"></param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="locale"></param>
        /// <param name="referenceCoord"></param>
        /// <returns></returns>
        public async Task<List<Hypothesis<BingMapsPlace>>> ForwardGeocode(
            BingMapsPlace input,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            LanguageCode locale = null,
            GeoCoordinate? referenceCoord = null)
        {
            // Try and combine as many details as we know into a single query
            string query = string.Empty;
            
            if (!string.IsNullOrWhiteSpace(input.StreetAddress))
            {
                query = query + " " + input.StreetAddress;
            }

            if (!string.IsNullOrWhiteSpace(input.Locality))
            {
                query = query + " " + input.Locality;
            }

            if (!string.IsNullOrWhiteSpace(input.PostalCode))
            {
                query = query + " " + input.PostalCode;
            }

            if (string.IsNullOrEmpty(query))
            {
                query = input.Name;
            }

            return await Query(query, queryLogger, cancelToken, realTime, locale, referenceCoord).ConfigureAwait(false);
        }

        public async Task<List<Hypothesis<BingMapsPlace>>> Query(
            string query,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            LanguageCode locale,
            GeoCoordinate? referenceCoord = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            // Format a URL to make a request to Bing map Api
            //string url = string.Format("/REST/v1/Locations?q={0}&key={1}&userIp=127.0.0.1",
            //        WebUtility.UrlEncode(query.Trim()), _apiKey);

            using (HttpRequest request = HttpRequest.CreateOutgoing("/REST/v1/Locations", "GET"))
            {
                request.GetParameters["q"] = query.Trim();
                request.GetParameters["key"] = _apiKey;
                request.GetParameters["userIp"] = "127.0.0.1";
                if (referenceCoord.HasValue)
                {
                    request.GetParameters["ul"] = string.Format("{0},{1}", referenceCoord.Value.Latitude, referenceCoord.Value.Longitude);
                }

                if (locale != null)
                {
                    request.GetParameters["c"] = locale.ToBcp47Alpha2String();
                }

                List<Hypothesis<BingMapsPlace>> returnVal = await QueryVirtualEarth(request, queryLogger, cancelToken, realTime).ConfigureAwait(false);
                return returnVal;
            }
        }

        /// <summary>
        /// Given a lat/long, attempt to fill in the rest of the address
        /// </summary>
        /// <param name="input"></param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="locale"></param>
        /// <returns></returns>
        public async Task<IList<Hypothesis<BingMapsPlace>>> ReverseGeocode(
            GeoCoordinate input,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            LanguageCode locale)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/REST/v1/Locations/{0},{1}",
                input.Latitude,
                input.Longitude), "GET"))
            {
                request.GetParameters["key"] = _apiKey;
                request.GetParameters["userIp"] = "127.0.0.1";

                if (locale != null)
                {
                    request.GetParameters["c"] = locale.ToBcp47Alpha2String();
                }

                List<Hypothesis<BingMapsPlace>> locationResults = await QueryVirtualEarth(request, queryLogger, cancelToken, realTime).ConfigureAwait(false);

                // Return the first location that matched, or the input if nothing was found
                if (locationResults == null || locationResults.Count == 0)
                {
                    return null;
                }

                return locationResults;
            }
        }

        private async Task<List<Hypothesis<BingMapsPlace>>> QueryVirtualEarth(HttpRequest request, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            List<Hypothesis<BingMapsPlace>> returnVal = new List<Hypothesis<BingMapsPlace>>();

            try
            {
                using (NetworkResponseInstrumented<HttpResponse> netResp = await _webClient.SendInstrumentedRequestAsync(
                    request,
                    cancelToken,
                    realTime,
                    NullLogger.Singleton).ConfigureAwait(false)) // Send a null logger so the client does not log the request URL which contains API key
                {
                    try
                    {
                        if (netResp == null || !netResp.Success)
                        {
                            queryLogger.Log("Null response from maps API!", LogLevel.Err);
                            return returnVal;
                        }

                        string responseString = await netResp.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);

                        if (netResp.Response.ResponseCode != 200)
                        {
                            queryLogger.Log("Non-success response from maps API: " + responseString, LogLevel.Err);
                            return returnVal;
                        }

                        // queryLogger.Log(responseString);

                        returnVal = ParseMapsResponse(responseString, "High");

                        if (returnVal == null || returnVal.Count == 0)
                        {
                            returnVal = ParseMapsResponse(responseString, "Medium");
                        }

                        return returnVal;
                    }
                    finally
                    {
                        if (netResp != null)
                        {
                            await netResp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                queryLogger.Log(ex, LogLevel.Err);
                return returnVal;
            }
        }

        private static List<Hypothesis<BingMapsPlace>> ParseMapsResponse(string responseJson, string desiredConfidenceLevel = "High")
        {
            List<Hypothesis<BingMapsPlace>> returnVal = new List<Hypothesis<BingMapsPlace>>();

            float thisItemConfidence;
            if (string.Equals(desiredConfidenceLevel, "High", StringComparison.OrdinalIgnoreCase))
            {
                thisItemConfidence = 0.9f;
            }
            else if (string.Equals(desiredConfidenceLevel, "Medium", StringComparison.OrdinalIgnoreCase))
            {
                thisItemConfidence = 0.6f;
            }
            else
            {
                thisItemConfidence = 0.3f;
            }

            var fullResponse = JsonConvert.DeserializeObject<JObject>(responseJson);

            JArray resourcesArray = fullResponse.SelectToken("$.resourceSets") as JArray;

            foreach (JToken resource in resourcesArray)
            {
                JArray responseArray = resource.SelectToken("resources") as JArray;
                foreach (JToken response in responseArray.Children())
                {
                    if (string.Equals(desiredConfidenceLevel, GetValueIfPresent<string>(response, "confidence")))
                    {
                        bool isAddress = "Address".Equals(GetValueIfPresent<string>(response, "entityType"));

                        BingMapsPlace newLocation = new BingMapsPlace();
                        newLocation.Name = GetValueIfPresent<string>(response, "name");
                        double? lat = GetValueIfPresent<double?>(response, "point.coordinates[0]", null);
                        double? lon = GetValueIfPresent<double?>(response, "point.coordinates[1]", null);
                        if (lat.HasValue && lon.HasValue)
                        {
                            newLocation.CenterCoord = new GeoCoordinate(lat.Value, lon.Value);
                        }

                        newLocation.Locality = GetValueIfPresent<string>(response, "address.locality");
                        newLocation.AdminDistrict = GetValueIfPresent<string>(response, "address.adminDistrict");
                        newLocation.CountryRegion = GetValueIfPresent<string>(response, "address.countryRegion");
                        newLocation.PostalCode = GetValueIfPresent<string>(response, "address.postalCode");
                        if (isAddress)
                        {
                            newLocation.StreetAddress = GetValueIfPresent<string>(response, "address.addressLine");
                        }

                        // Also set accuracy based on bounding box size
                        if (response.SelectToken("bbox") != null)
                        {
                            double bbox0 = GetValueIfPresent<double>(response, "bbox[0]");
                            double bbox1 = GetValueIfPresent<double>(response, "bbox[1]");
                            double bbox2 = GetValueIfPresent<double>(response, "bbox[2]");
                            double bbox3 = GetValueIfPresent<double>(response, "bbox[3]");
                            newLocation.BoundingBoxUpperLeft = new GeoCoordinate(bbox0, bbox1);
                            newLocation.BoundingBoxLowerRight = new GeoCoordinate(bbox2, bbox3);
                        }

                        returnVal.Add(new Hypothesis<BingMapsPlace>(newLocation, thisItemConfidence));
                    }
                }
            }

            return returnVal;
        }

        private static T GetValueIfPresent<T>(JToken token, string jPath, T defaultVal = default(T))
        {
            var t = token.SelectToken(jPath);
            if (t != null)
            {
                return t.Value<T>();
            }

            return defaultVal;
        }
    }
}
