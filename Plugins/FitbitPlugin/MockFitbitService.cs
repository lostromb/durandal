using Durandal.Plugins.Fitbit.Schemas;
using Durandal.Plugins.Fitbit.Schemas.Responses;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace Durandal.Plugins.Fitbit
{
    public class MockFitbitService : IHttpServerDelegate
    {
        private static readonly Regex PathProfile = new Regex("/1/user/.+?/profile.json");
        private static readonly Regex PathActivitiesDate = new Regex("/1/user/.+?/activities/date/(.+?).json");
        private static readonly Regex PathActivitiesGoalsDaily = new Regex("/1/user/.+?/activities/goals/daily.json");
        private static readonly Regex PathBodyLogWeightDate = new Regex("/1/user/.+?/body/log/weight/date/(.+?)/(.+?).json");
        private static readonly Regex PathDevices = new Regex("/1/user/.+?/devices.json");
        private static readonly Regex PathFriendsLeaderboard = new Regex("/1/user/.+?/friends/leaderboard.json");
        private static readonly Regex PathActivitiesList = new Regex("/1/user/.+?/activities/list.json");
        private static readonly Regex PathDevicesTrackerAlarms = new Regex("/1/user/.+?/devices/tracker/(.+?)/alarms.json");
        private static readonly Regex PathFoodsLog = new Regex("/1/user/.+?/foods/log.json");
        private static readonly Regex PathFoodsLogDate = new Regex("/1/user/.+?/foods/log/date/(.+?).json");
        private static readonly Regex PathFoodsLogWaterDate = new Regex("/1/user/.+?/foods/log/water/date/(.+?).json");
        private static readonly Regex PathFoodsLogWater = new Regex("/1/user/.+?/foods/log/water.json");
        private static readonly Regex PathFoodsSearch = new Regex("/1/foods/search.json");

        private FitbitUser _userProfile;
        private FitnessGoals _dailyGoals;
        private FriendsLeaderboardResponse _leaderboard;
        private readonly Dictionary<string, DailyActivityResponse> _activitySummaries = new Dictionary<string, DailyActivityResponse>();
        private readonly List<WeightLogInternal> _weightLogs = new List<WeightLogInternal>();
        private readonly List<FitbitDevice> _devices = new List<FitbitDevice>();
        private readonly List<FitnessActivity> _activities = new List<FitnessActivity>();
        private readonly List<Food> _knownFoods = new List<Food>();
        private readonly Dictionary<string, List<Alarm>> _alarms = new Dictionary<string, List<Alarm>>();
        private readonly Dictionary<string, FoodLogGetResponse> _foodLogSummaries = new Dictionary<string, FoodLogGetResponse>();
        private readonly Dictionary<string, WaterLogGetResponse> _waterLogSummaries = new Dictionary<string, WaterLogGetResponse>();
        private readonly ILogger _logger;

        private readonly JsonSerializer _serializerSettings = new JsonSerializer()
        {
            DateParseHandling = DateParseHandling.DateTime,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss.fff"
        };

        public MockFitbitService(ILogger logger)
        {
            _logger = logger;
        }

        public void SetUserProfile(FitbitUser profile)
        {
            _userProfile = profile;
        }

        public void SetDailyGoals(FitnessGoals goals)
        {
            _dailyGoals = goals;
        }

        public void SetLeaderboard(FriendsLeaderboardResponse board)
        {
            _leaderboard = board;
        }

        public void ClearActivitySummaries()
        {
            _activitySummaries.Clear();
        }

        public void AddActivitySummary(DailyActivityResponse summary, string dateString)
        {
            _activitySummaries[dateString] = summary;
        }

        public void ClearFoodLogSummaries()
        {
            _foodLogSummaries.Clear();
        }

        public void AddFoodLogSummary(FoodLogGetResponse summary, string dateString)
        {
            _foodLogSummaries[dateString] = summary;
        }

        public void ClearWaterLogSummaries()
        {
            _waterLogSummaries.Clear();
        }

        public void AddWaterLogSummary(WaterLogGetResponse summary, string dateString)
        {
            _waterLogSummaries[dateString] = summary;
        }

        public void ClearWeightLogs()
        {
            _weightLogs.Clear();
        }

        public void AddWeightLog(WeightLogInternal log)
        {
            _weightLogs.Add(log);
        }

        public void ClearDevices()
        {
            _devices.Clear();
        }

        public void AddDevice(FitbitDevice device)
        {
            _devices.Add(device);
        }

        public void ClearActivities()
        {
            _activities.Clear();
        }

        public void AddActivity(FitnessActivity activity)
        {
            _activities.Add(activity);
        }

        public List<Alarm> GetAlarms(string trackerId)
        {
            if (_alarms.ContainsKey(trackerId))
            {
                return _alarms[trackerId];
            }

            return new List<Alarm>();
        }

        public void ClearAlarms()
        {
            _alarms.Clear();
        }

        public void AddAlarm(string trackerId, Alarm alarm)
        {
            if (!_alarms.ContainsKey(trackerId))
            {
                _alarms[trackerId] = new List<Alarm>();
            }

            _alarms[trackerId].Add(alarm);
        }

        public void AddFood(Food food)
        {
            _knownFoods.Add(food);
        }

        public void ClearFoods()
        {
            _knownFoods.Clear();
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

        private async Task<HttpResponse> HandleConnectionInternal(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await Task.Yield();

            if ("GET".Equals(request.RequestMethod) && PathProfile.IsMatch(request.DecodedRequestFile))
            {
                return GetProfile();
            }

            if ("GET".Equals(request.RequestMethod) && PathActivitiesDate.IsMatch(request.DecodedRequestFile))
            {
                Match m = PathActivitiesDate.Match(request.DecodedRequestFile);
                string dateString = m.Groups[1].Value;
                return GetActivities(dateString);
            }

            if ("GET".Equals(request.RequestMethod) && PathActivitiesGoalsDaily.IsMatch(request.DecodedRequestFile))
            {
                return GetActivitiesGoalsDaily();
            }

            if ("GET".Equals(request.RequestMethod) && PathBodyLogWeightDate.IsMatch(request.DecodedRequestFile))
            {
                Match m = PathBodyLogWeightDate.Match(request.DecodedRequestFile);
                string baseDate = m.Groups[1].Value;
                string period = m.Groups[2].Value;
                return GetBodyLogWeight(baseDate, period);
            }

            if ("GET".Equals(request.RequestMethod) && PathDevices.IsMatch(request.DecodedRequestFile))
            {
                return GetDevices();
            }

            if ("GET".Equals(request.RequestMethod) && PathFriendsLeaderboard.IsMatch(request.DecodedRequestFile))
            {
                return GetFriendsLeaderboard();
            }

            if ("GET".Equals(request.RequestMethod) && PathActivitiesList.IsMatch(request.DecodedRequestFile))
            {
                return GetActivitiesList(request.GetParameters);
            }

            if ("GET".Equals(request.RequestMethod) && PathDevicesTrackerAlarms.IsMatch(request.DecodedRequestFile))
            {
                Match m = PathDevicesTrackerAlarms.Match(request.DecodedRequestFile);
                string trackerId = m.Groups[1].Value;
                return GetDevicesTrackerAlarms(trackerId);
            }

            if ("POST".Equals(request.RequestMethod) && PathDevicesTrackerAlarms.IsMatch(request.DecodedRequestFile))
            {
                Match m = PathDevicesTrackerAlarms.Match(request.DecodedRequestFile);
                string trackerId = m.Groups[1].Value;
                return PostDevicesTrackerAlarms(await request.ReadContentAsFormDataAsync(cancelToken, realTime), trackerId);
            }

            if ("GET".Equals(request.RequestMethod) && PathFoodsLogDate.IsMatch(request.DecodedRequestFile))
            {
                Match m = PathFoodsLogDate.Match(request.DecodedRequestFile);
                string dateString = m.Groups[1].Value;
                return GetFoodsLog(dateString);
            }
            if ("POST".Equals(request.RequestMethod) && PathFoodsLog.IsMatch(request.DecodedRequestFile))
            {
                return PostFoodsLog(await request.ReadContentAsFormDataAsync(cancelToken, realTime));
            }

            if ("GET".Equals(request.RequestMethod) && PathFoodsLogWaterDate.IsMatch(request.DecodedRequestFile))
            {
                Match m = PathFoodsLogWaterDate.Match(request.DecodedRequestFile);
                string dateString = m.Groups[1].Value;
                return GetFoodsLogWater(dateString);
            }

            if ("POST".Equals(request.RequestMethod) && PathFoodsLogWater.IsMatch(request.DecodedRequestFile))
            {
                return PostFoodsLogWater(await request.ReadContentAsFormDataAsync(cancelToken, realTime));
            }

            if ("POST".Equals(request.RequestMethod) && PathFoodsSearch.IsMatch(request.DecodedRequestFile))
            {
                return PostFoodsSearch(await request.ReadContentAsFormDataAsync(cancelToken, realTime));
            }

            return HttpResponse.NotFoundResponse();
        }

        private HttpResponse GetProfile()
        {
            UserProfileResponse returnVal = new UserProfileResponse()
            {
                User = _userProfile
            };

            HttpResponse response = HttpResponse.OKResponse();
            response.SetContentJson(returnVal);
            return response;
        }

        /// <summary>
        /// Gets activity summary
        /// </summary>
        /// <param name="dateString">A date string in ISO format 2012-04-11</param>
        /// <returns></returns>
        private HttpResponse GetActivities(string dateString)
        {
            if (!_activitySummaries.ContainsKey(dateString))
            {
                return HttpResponse.NotFoundResponse();
            }

            DailyActivityResponse returnVal = _activitySummaries[dateString];
            //returnVal.Goals = _dailyGoals;

            HttpResponse response = HttpResponse.OKResponse();
            response.SetContentJson(returnVal);
            return response;
        }

        private HttpResponse GetActivitiesGoalsDaily()
        {
            HttpResponse response = HttpResponse.OKResponse();
            DailyGoalsResponse returnVal = new DailyGoalsResponse()
            {
                Goals = _dailyGoals
            };

            response.SetContentJson(returnVal);
            return response;
        }

        private HttpResponse GetBodyLogWeight(string dateString, string periodString)
        {
            TimeSpan period = TimeSpan.FromHours(1);
            if (string.Equals(periodString, "1d"))
            {
                period = TimeSpan.FromDays(1);
            }
            else if (string.Equals(periodString, "7d") || string.Equals(periodString, "1w"))
            {
                period = TimeSpan.FromDays(7);
            }
            else if (string.Equals(periodString, "30d") || string.Equals(periodString, "1m"))
            {
                period = TimeSpan.FromDays(30);
            }
            else
            {
                return HttpResponse.BadRequestResponse();
            }

            WeightLogsResponse returnVal = new WeightLogsResponse()
            {
                weight = new List<WeightLogInternal>() { }
            };

            // Find all logs that match the criteria
            DateTime endDate = DateTime.ParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture.DateTimeFormat);
            DateTime startDate = endDate.Subtract(period);
            foreach (var rawLog in _weightLogs)
            {
                DateTime logDate = DateTime.ParseExact(rawLog.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture.DateTimeFormat);
                if (logDate > startDate)
                {
                    returnVal.weight.Add(rawLog);
                }
            }
            
            HttpResponse response = HttpResponse.OKResponse();
            response.SetContentJson(returnVal);
            return response;
        }

        private HttpResponse GetDevices()
        {
            HttpResponse response = HttpResponse.OKResponse();
            response.SetContentJson(_devices, _serializerSettings);
            return response;
        }

        private HttpResponse GetFriendsLeaderboard()
        {
            HttpResponse response = HttpResponse.OKResponse();
            response.SetContentJson(_leaderboard, _serializerSettings);
            return response;
        }

        private HttpResponse GetDevicesTrackerAlarms(string trackerId)
        {
            HttpResponse response = HttpResponse.OKResponse();

            AlarmResponse responseObject = new AlarmResponse();
            if (_alarms.ContainsKey(trackerId))
            {
                responseObject.TrackerAlarms = _alarms[trackerId];
            }
            else
            {
                responseObject.TrackerAlarms = new List<Alarm>();
            }

            response.SetContentJson(responseObject, _serializerSettings);
            return response;
        }

        private HttpResponse PostDevicesTrackerAlarms(HttpFormParameters postParameters, string trackerId)
        {
            if (!postParameters.ContainsKey("time") ||
                !postParameters.ContainsKey("enabled") ||
                !postParameters.ContainsKey("recurring") ||
                !postParameters.ContainsKey("weekDays"))
            {
                return HttpResponse.BadRequestResponse();
            }

            IRandom rand = new FastRandom();
            Alarm createdAlarm = new Alarm()
            {
                Time = postParameters["time"],
                Enabled = bool.Parse(postParameters["enabled"]),
                Recurring = bool.Parse(postParameters["recurring"]),
                AlarmId = (ulong)rand.NextInt(),
                Deleted = false,
                SnoozeCount = 0,
                SnoozeLength = 0,
                SyncedToDevice = false,
                Vibe = "",
                WeekDays = postParameters["weekDays"].Split(',').ToList()
            };
            
            if (!_alarms.ContainsKey(trackerId))
            {
                _alarms[trackerId] = new List<Alarm>();
            }
            _alarms[trackerId].Add(createdAlarm);

            return HttpResponse.OKResponse();
        }

        private HttpResponse GetActivitiesList(IHttpFormParameters queryParameters)
        {
            List<FitnessActivity> matchingActivities = new List<FitnessActivity>();

            HttpResponse response = HttpResponse.OKResponse();

            Pagination pagination = new Pagination();
            if (queryParameters.ContainsKey("afterDate"))
            {
                pagination.AfterDate = queryParameters["afterDate"];
            }
            if (queryParameters.ContainsKey("beforeDate"))
            {
                pagination.BeforeDate = queryParameters["beforeDate"];
            }
            if (queryParameters.ContainsKey("limit"))
            {
                pagination.Limit = int.Parse(queryParameters["limit"]);
            }
            if (queryParameters.ContainsKey("offset"))
            {
                pagination.Offset = int.Parse(queryParameters["offset"]);
            }
            if (queryParameters.ContainsKey("sort"))
            {
                pagination.Sort = queryParameters["sort"];
            }

            if (!string.IsNullOrEmpty(pagination.AfterDate))
            {
                DateTime afterDate = DateTime.ParseExact(pagination.AfterDate, "yyyy-MM-dd", CultureInfo.InvariantCulture.DateTimeFormat);
                foreach (var activity in _activities)
                {
                    if (activity.StartTime.HasValue && activity.StartTime.Value.DateTime.Date >= afterDate)
                    {
                        matchingActivities.Add(activity);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(pagination.BeforeDate))
            {
                DateTime beforeDate = DateTime.ParseExact(pagination.BeforeDate, "yyyy-MM-dd", CultureInfo.InvariantCulture.DateTimeFormat);
                foreach (var activity in _activities)
                {
                    if (activity.StartTime.HasValue && activity.StartTime.Value.DateTime.Date <= beforeDate)
                    {
                        matchingActivities.Add(activity);
                    }
                }
            }
            else
            {
                return HttpResponse.BadRequestResponse();
            }

            // Apply sort, offset, and limit
            matchingActivities.Sort((a, b) =>
            {
                DateTimeOffset ad = a.StartTime.GetValueOrDefault();
                DateTimeOffset bd = b.StartTime.GetValueOrDefault();
                bool asc = string.Equals("asc", pagination.Sort);
                if (ad == bd)
                {
                    return 0;
                }
                else if (ad < bd)
                {
                    return asc ? -1 : 1;
                }
                else
                {
                    return asc ? 1 : -1;
                }
            });

            ActivityListResponse activityResponse = new ActivityListResponse()
            {
                Activities = matchingActivities.Skip(pagination.Offset).Take(pagination.Limit).ToList(),
                Pagination = pagination
            };

            response.SetContentJson(activityResponse, _serializerSettings);
            return response;
        }

        private HttpResponse GetFoodsLog(string dateString)
        {
            if (!_foodLogSummaries.ContainsKey(dateString))
            {
                return HttpResponse.NotFoundResponse();
            }

            FoodLogGetResponse returnVal = _foodLogSummaries[dateString];

            HttpResponse response = HttpResponse.OKResponse();
            response.SetContentJson(returnVal);
            return response;
        }

        private HttpResponse GetFoodsLogWater(string dateString)
        {
            WaterLogGetResponse returnVal;
            if (!_waterLogSummaries.TryGetValue(dateString, out returnVal))
            {
                returnVal = new WaterLogGetResponse()
                {
                    Summary = new WaterSummary()
                    {
                        Water = 0
                    }
                };
            }

            HttpResponse response = HttpResponse.OKResponse();
            response.SetContentJson(returnVal);
            return response;
        }

        private HttpResponse PostFoodsLog(HttpFormParameters postParameters)
        {
            if (!postParameters.ContainsKey("foodId") ||
                !postParameters.ContainsKey("mealTypeId") ||
                !postParameters.ContainsKey("unitId") ||
                !postParameters.ContainsKey("amount") ||
                !postParameters.ContainsKey("date"))
            {
                return HttpResponse.BadRequestResponse();
            }
            
            ulong foodId = ulong.Parse(postParameters["foodId"]);
            ulong unitId = ulong.Parse(postParameters["unitId"]);
            float amount = float.Parse(postParameters["amount"]);
            string date = postParameters["date"];
            int mealTypeId = int.Parse(postParameters["mealTypeId"]);

            // Find food in database
            Food food = null;
            foreach (Food f in _knownFoods)
            {
                if (f.FoodId == foodId)
                {
                    food = f;
                    break;
                }
            }

            if (food == null)
            {
                return HttpResponse.NotFoundResponse();
            }

            IRandom rand = new FastRandom();
            FoodLog createdLog = new FoodLog()
            {
                LogId = (ulong)rand.NextInt(),
                LogDate = date,
                IsFavorite = false,
                LoggedFood = food,
                NutritionalValues = new NutritionalValue()
            };

            // Update our locally stored summary
            FoodLogGetResponse relevantSummary;
            if (!_foodLogSummaries.TryGetValue(date, out relevantSummary))
            {
                relevantSummary = new FoodLogGetResponse();
            }

            if (relevantSummary.Foods == null)
            {
                relevantSummary.Foods = new List<Food>();
            }

            relevantSummary.Foods.Add(food);

            _foodLogSummaries[date] = relevantSummary;
            
            // And return the single created entry
            FoodLogPostResponse returnVal = new FoodLogPostResponse()
            {
                FoodLog = createdLog
            };

            HttpResponse response = HttpResponse.OKResponse();
            response.SetContentJson(returnVal);
            return response;
        }

        private HttpResponse PostFoodsSearch(HttpFormParameters postParameters)
        {
            if (!postParameters.ContainsKey("query"))
            {
                return HttpResponse.BadRequestResponse();
            }
            
            string query = postParameters["query"];

            // Find food in database
            FoodSearchResponse returnVal = new FoodSearchResponse()
            {
                Foods = new List<Food>()
            };

            foreach (Food f in _knownFoods)
            {
                if (f.Name.Contains(query))
                {
                    returnVal.Foods.Add(f);
                }
            }

            HttpResponse response = HttpResponse.OKResponse();
            response.SetContentJson(returnVal);
            return response;
        }

        private HttpResponse PostFoodsLogWater(HttpFormParameters postParameters)
        {
            if (!postParameters.ContainsKey("amount") ||
                !postParameters.ContainsKey("date"))
            {
                return HttpResponse.BadRequestResponse();
            }

            // assume milliliters as unit
            float amountMl = float.Parse(postParameters["amount"]);
            string date = postParameters["date"];

            IRandom rand = new FastRandom();
            WaterLog createdLog = new WaterLog()
            {
                LogId = (ulong)rand.NextInt(),
                Amount = amountMl
            };

            // Update our locally stored summary
            WaterLogGetResponse relevantSummary;
            if (!_waterLogSummaries.TryGetValue(date, out relevantSummary))
            {
                relevantSummary = new WaterLogGetResponse();
            }

            relevantSummary.Summary.Water += amountMl;

            _waterLogSummaries[date] = relevantSummary;

            // And return the single created entry
            WaterLogPostResponse returnVal = new WaterLogPostResponse()
            {
                WaterLog = createdLog
            };

            HttpResponse response = HttpResponse.OKResponse();
            response.SetContentJson(returnVal);
            return response;
        }
    }
}
