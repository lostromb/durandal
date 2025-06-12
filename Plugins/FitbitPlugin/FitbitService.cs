using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Plugins.Fitbit.Schemas;
using Durandal.Plugins.Fitbit.Schemas.Responses;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;
using Durandal.Common.Time;

namespace Durandal.Plugins.Fitbit
{
    /// <summary>
    /// Service adapter which talks to Fitbit API
    /// </summary>
    public class FitbitService
    {
        private static readonly Regex DATE_PARSE_REGEX = new Regex("(\\d{4})-(\\d{2})-(\\d{2})", RegexOptions.Compiled);
        private static readonly Regex TIME_PARSE_REGEX = new Regex("(\\d{2}):(\\d{2}):(\\d{2})", RegexOptions.Compiled);
        private static readonly Uri FITBIT_SERVICE_URI = new Uri("https://api.fitbit.com:443");

        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;

        public FitbitService (IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateHttpClient(FITBIT_SERVICE_URI, _logger.Clone("FitbitHttp"));
        }

        /// <summary>
        /// Retrieves the profile of the given user
        /// </summary>
        /// <returns></returns>
        public async Task<FitbitUser> GetUserProfile(string oauthToken, ILogger queryLogger, string userId = "-")
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/profile.json", userId)))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not retrieve user profile", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        UserProfileResponse response = JsonConvert.DeserializeObject<UserProfileResponse>(responseJson);
                        if (response.User == null)
                        {
                            queryLogger.Log("Fitbit user was null", LogLevel.Err);
                            if (!string.IsNullOrEmpty(responseJson))
                            {
                                queryLogger.Log(responseJson, LogLevel.Err);
                            }

                            return null;
                        }

                        return response.User;
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


        public async Task<DailyActivityResponse> GetDailyActivities(string oauthToken, ILogger queryLogger, DateTimeOffset date, FitbitUser userProfile)
        {
            string dateString = date.ToOffset(TimeSpan.FromMilliseconds(userProfile.OffsetFromUTCMillis)).ToString("yyyy-MM-dd");
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/activities/date/{1}.json", userProfile.EncodedId, dateString)))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not retrieve daily activites", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        DailyActivityResponse response = JsonConvert.DeserializeObject<DailyActivityResponse>(responseJson);
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

        public async Task<FoodLogGetResponse> GetFoodLogs(string oauthToken, ILogger queryLogger, DateTimeOffset date, FitbitUser userProfile)
        {
            string dateString = date.ToOffset(TimeSpan.FromMilliseconds(userProfile.OffsetFromUTCMillis)).ToString("yyyy-MM-dd");
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/foods/log/date/{1}.json", userProfile.EncodedId, dateString)))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not retrieve food logs", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        FoodLogGetResponse response = JsonConvert.DeserializeObject<FoodLogGetResponse>(responseJson);
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

        public async Task<WaterLogGetResponse> GetWaterLogs(string oauthToken, ILogger queryLogger, DateTimeOffset date, FitbitUser userProfile)
        {
            string dateString = date.ToOffset(TimeSpan.FromMilliseconds(userProfile.OffsetFromUTCMillis)).ToString("yyyy-MM-dd");
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/foods/log/water/date/{1}.json", userProfile.EncodedId, dateString)))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not retrieve water logs", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        WaterLogGetResponse response = JsonConvert.DeserializeObject<WaterLogGetResponse>(responseJson);
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

        public async Task<WaterLogGetResponse> GetAlarms(string oauthToken, ILogger queryLogger, string trackerId, FitbitUser userProfile)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/devices/tracker/{1}/alarms.json", userProfile.EncodedId, trackerId)))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not retrieve device alarms", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        WaterLogGetResponse response = JsonConvert.DeserializeObject<WaterLogGetResponse>(responseJson);
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

        public async Task<FitnessGoals> GetDailyGoals(string oauthToken, ILogger queryLogger, FitbitUser userProfile)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/activities/goals/daily.json", userProfile.EncodedId)))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not retrieve daily goals", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        DailyGoalsResponse response = JsonConvert.DeserializeObject<DailyGoalsResponse>(responseJson);
                        if (response.Goals == null)
                        {
                            queryLogger.Log("Daily goals were null", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        return response.Goals;
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

        public async Task<IList<WeightLog>> GetWeightLogs(string oauthToken, ILogger queryLogger, DateTimeOffset baseDate, PeriodEnum period, FitbitUser userProfile)
        {
            string dateString = baseDate.ToOffset(TimeSpan.FromMilliseconds(userProfile.OffsetFromUTCMillis)).ToString("yyyy-MM-dd");
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/body/log/weight/date/{1}/{2}.json", userProfile.EncodedId, dateString, period.ToIsoString())))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not retrieve weight logs", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        WeightLogsResponse response = JsonConvert.DeserializeObject<WeightLogsResponse>(responseJson);
                        if (response.weight == null)
                        {
                            queryLogger.Log("Weight logs were null", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        // Convert logs from internal format to easier format
                        List<WeightLog> returnVal = new List<WeightLog>();

                        foreach (WeightLogInternal rawLog in response.weight)
                        {
                            Match dateMatch = DATE_PARSE_REGEX.Match(rawLog.Date);
                            Match timeMatch = TIME_PARSE_REGEX.Match(rawLog.Time);
                            if (!dateMatch.Success || !timeMatch.Success)
                            {
                                queryLogger.Log("Failed to parse date/time from weight log!", LogLevel.Wrn);
                                queryLogger.Log("Date=\"" + rawLog.Date + "\", Time=\"" + rawLog.Time + "\"", LogLevel.Wrn);
                                continue;
                            }

                            DateTimeOffset logTime = new DateTimeOffset(
                                int.Parse(dateMatch.Groups[1].Value),
                                int.Parse(dateMatch.Groups[2].Value),
                                int.Parse(dateMatch.Groups[3].Value),
                                int.Parse(timeMatch.Groups[1].Value),
                                int.Parse(timeMatch.Groups[2].Value),
                                int.Parse(timeMatch.Groups[3].Value),
                                TimeSpan.FromMilliseconds(userProfile.OffsetFromUTCMillis));

                            returnVal.Add(new WeightLog()
                            {
                                BMI = rawLog.BMI,
                                LogId = rawLog.LogId,
                                Source = rawLog.Source,
                                Weight = rawLog.Weight,
                                DateTime = logTime
                            });
                        }

                        return returnVal;
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

        public async Task<List<FitbitDevice>> GetUserDevices(string oauthToken, ILogger queryLogger, FitbitUser userProfile)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/devices.json", userProfile.EncodedId)))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not retrieve user's devices", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
                        {
                            DateParseHandling = DateParseHandling.DateTime,
                            DateFormatString = "yyyy-MM-ddTHH:mm:ss.fff"
                        };

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        List<FitbitDevice> response = JsonConvert.DeserializeObject<List<FitbitDevice>>(responseJson, serializerSettings);
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

        public async Task<FriendsLeaderboardResponse> GetFriendsLeaderboard(string oauthToken, ILogger queryLogger, FitbitUser userProfile)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/friends/leaderboard.json", userProfile.EncodedId)))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not retrieve user's leaderboard", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        FriendsLeaderboardResponse response = JsonConvert.DeserializeObject<FriendsLeaderboardResponse>(responseJson);
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

        public async Task<ActivityListResponse> GetActivitiesList(string oauthToken, ILogger queryLogger, FitbitUser userProfile, Pagination pagination)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/activities/list.json", userProfile.EncodedId)))
            {
                if (!string.IsNullOrEmpty(pagination.AfterDate))
                {
                    request.GetParameters.Add("afterDate", pagination.AfterDate);
                }
                else if (!string.IsNullOrEmpty(pagination.BeforeDate))
                {
                    request.GetParameters.Add("beforeDate", pagination.BeforeDate);
                }
                else
                {
                    throw new ArgumentException("Either afterDate or beforeDate must be specified");
                }

                if (pagination.Limit > 20)
                {
                    throw new ArgumentException("Limit has a max value of 20");
                }

                request.GetParameters.Add("limit", pagination.Limit.ToString());
                request.GetParameters.Add("offset", pagination.Offset.ToString());
                request.GetParameters.Add("sort", pagination.Sort);
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success)
                        {
                            queryLogger.Log("Could not retrieve user activity list", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        ActivityListResponse response = JsonConvert.DeserializeObject<ActivityListResponse>(responseJson);
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

        public async Task<AlarmResponse> GetDeviceAlarms(string oauthToken, ILogger queryLogger, FitbitUser userProfile, string trackerId)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/devices/tracker/{1}/alarms.json", userProfile.EncodedId, trackerId)))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not retrieve alarms", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        AlarmResponse response = JsonConvert.DeserializeObject<AlarmResponse>(responseJson);
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

        public async Task<bool> SetDeviceAlarm(string oauthToken, ILogger queryLogger, FitbitUser userProfile, string trackerId, TimeSpan time, bool enabled, bool recurring, IEnumerable<DayOfWeek> weekDays)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/devices/tracker/{1}/alarms.json", userProfile.EncodedId, trackerId), "POST"))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);

                DateTimeOffset timeWithOffset = new DateTimeOffset(1, 1, 1, time.Hours, time.Minutes, 0, TimeSpan.FromMilliseconds(userProfile.OffsetFromUTCMillis));
                Dictionary<string, string> postParameters = new Dictionary<string, string>();
                postParameters["time"] = timeWithOffset.ToString("HH:mmzzz");
                postParameters["enabled"] = enabled.ToString();
                postParameters["recurring"] = recurring.ToString();
                postParameters["weekDays"] = string.Join(",", weekDays).ToUpperInvariant();
                request.SetContent(postParameters);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not set a new alarm", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return false;
                        }

                        return true;
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

        public async Task<WaterLogPostResponse> LogWater(string oauthToken, ILogger queryLogger, FitbitUser userProfile, float milliliters, DateTimeOffset date)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/foods/log/water.json", userProfile.EncodedId), "POST"))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);

                Dictionary<string, string> postParameters = new Dictionary<string, string>();
                postParameters["amount"] = milliliters.ToString("F1");
                postParameters["date"] = date.ToOffset(TimeSpan.FromMilliseconds(userProfile.OffsetFromUTCMillis)).ToString("yyyy-MM-dd");
                postParameters["unit"] = "ml";
                request.SetContent(postParameters);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not post water data", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        WaterLogPostResponse response = JsonConvert.DeserializeObject<WaterLogPostResponse>(responseJson);
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

        public async Task<FoodSearchResponse> SearchFoods(string oauthToken, ILogger queryLogger, string query)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing("/1/foods/search.json", "POST"))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);

                Dictionary<string, string> postParameters = new Dictionary<string, string>();
                postParameters["query"] = query;
                request.SetContent(postParameters);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not retrieve food search response", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        FoodSearchResponse response = JsonConvert.DeserializeObject<FoodSearchResponse>(responseJson);
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

        public async Task<FoodLogPostResponse> LogFood(string oauthToken, ILogger queryLogger, FitbitUser userProfile, ulong foodId, ulong unitId, float amount, DateTimeOffset date)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format("/1/user/{0}/foods/log.json", userProfile.EncodedId), "POST"))
            {
                request.RequestHeaders.Add("Authorization", "Bearer " + oauthToken);

                Dictionary<string, string> postParameters = new Dictionary<string, string>();
                postParameters["foodId"] = foodId.ToString(); //ID of the food to be logged. Either foodId or foodName must be provided.
                postParameters["mealTypeId"] = "7"; //Meal type. 1=Breakfast; 2=Morning Snack; 3=Lunch; 4=Afternoon Snack; 5=Dinner; 7=Anytime.
                postParameters["unitId"] = unitId.ToString(); //ID of units used. Typically retrieved via a previous call to Get Food Logs, Search Foods, or Get Food Units.
                postParameters["amount"] = amount.ToString("F2"); //Amount consumed; in the format X.XX, in the specified unitId.
                postParameters["date"] = date.ToOffset(TimeSpan.FromMilliseconds(userProfile.OffsetFromUTCMillis)).ToString("yyyy-MM-dd"); //Log entry date; in the format yyyy-MM-dd.
                request.SetContent(postParameters);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger.Clone("FitbitHttp")))
                {
                    try
                    {
                        if (netResponse == null || !netResponse.Success || netResponse.Response.ResponseCode >= 400)
                        {
                            queryLogger.Log("Could not post food data", LogLevel.Err);
                            await LogHttpError(queryLogger, netResponse).ConfigureAwait(false);
                            return null;
                        }

                        string responseJson = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        FoodLogPostResponse response = JsonConvert.DeserializeObject<FoodLogPostResponse>(responseJson);
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

        private async Task LogHttpError(ILogger logger, NetworkResponseInstrumented<HttpResponse> response)
        {
            if (response != null && response.Response != null)
            {
                logger.Log("Got non-success HTTP response " + response.Response.ResponseCode + " " + response.Response.ResponseMessage, LogLevel.Err);
                string payload = await response.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(payload))
                {
                    logger.Log(payload, LogLevel.Err);
                }
            }
        }
    }
}
