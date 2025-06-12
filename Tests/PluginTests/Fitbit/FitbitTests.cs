using Durandal;
using Durandal.Plugins.Fitbit;
using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Logger;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Net.Http;
using System.IO;
using Durandal.Common.Time;
using Durandal.Plugins.Fitbit.Schemas;
using Durandal.Plugins.Fitbit.Schemas.Responses;
using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.UnitConversion;
using Durandal.Common.Test;
using Durandal.Common.File;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Test.Builders;

namespace DialogTests.Plugins.Fitbit
{
    [TestClass]
    public class FitbitTests
    {
        private static MockFitbitService _fakeFitbitService;
        private static FitbitAnswer _plugin;
        private static InqueTestDriver _testDriver;
        private static ManualTimeProvider _timeProvider;

        #region Test framework

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _timeProvider = new ManualTimeProvider();
            _fakeFitbitService = new MockFitbitService(new ConsoleLogger("FitbitServer"));
            _plugin = new FitbitAnswer(new DirectHttpClientFactory(_fakeFitbitService), _timeProvider);
            string rootEnv = context.Properties["DurandalRootDirectory"]?.ToString();
            if (string.IsNullOrEmpty(rootEnv))
            {
                rootEnv = Environment.GetEnvironmentVariable("DURANDAL_ROOT");
                if (string.IsNullOrEmpty(rootEnv))
                {
                    throw new FileNotFoundException("Cannot find durandal environment directory, either from DurandalRootDirectory test property, or DURANDAL_ROOT environment variable.");
                }
            }

            InqueTestParameters testConfig = PluginTestCommon.CreateTestParameters(_plugin, "FitbitPlugin.dupkg", new DirectoryInfo(rootEnv));
            testConfig.SideSpeechDomain = Constants.FITBIT_DOMAIN;
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
            _timeProvider.Time = new DateTimeOffset(2012, 3, 15, 18, 5, 0, TimeSpan.FromHours(-8)).ToUniversalTime();

            _fakeFitbitService.SetUserProfile(DefaultUserProfile);

            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = 30,
                CaloriesOut = 2846,
                Distance = 8.05f,
                Steps = 10000,
                Floors = 20
            });

            _fakeFitbitService.ClearActivitySummaries();
            _fakeFitbitService.AddActivitySummary(DefaultActivityResponse, "2012-03-15");

            _fakeFitbitService.ClearFoodLogSummaries();
            _fakeFitbitService.AddFoodLogSummary(new FoodLogGetResponse()
            {
                Summary = new FoodSummary()
                {
                    Calories = 912
                }
            }, "2012-03-15");

            _fakeFitbitService.ClearWaterLogSummaries();
            _fakeFitbitService.AddWaterLogSummary(new WaterLogGetResponse()
            {
                Summary = new WaterSummary()
                {
                    Water = 697f
                }
            }, "2012-03-15");

            _fakeFitbitService.ClearWeightLogs();
            _fakeFitbitService.ClearDevices();

            _fakeFitbitService.SetLeaderboard(DefaultLeaderboard);

            _fakeFitbitService.ClearActivities();
            _fakeFitbitService.ClearAlarms();
        }

        private FriendsLeaderboardResponse DefaultLeaderboard
        {
            get
            {
                return new FriendsLeaderboardResponse()
                {
                    HideMeFromLeaderboard = false,
                    Friends = new List<FriendLeaderboardEntry>()
                    {
                        new FriendLeaderboardEntry()
                        {
                            LastUpdateTime = null,
                            Rank = new LeaderboardRankEntry() { Steps = 1 },
                            Average = new LeaderboardActivityEntry() { Steps = 25000 },
                            Summary = new LeaderboardActivityEntry() { Steps = 25000 },
                            User = new FitbitUser() { DisplayName = "Mr. 1", EncodedId = "111" }
                        },
                        new FriendLeaderboardEntry()
                        {
                            LastUpdateTime = null,
                            Rank = new LeaderboardRankEntry() { Steps = 2 },
                            Average = new LeaderboardActivityEntry() { Steps = 20000 },
                            Summary = new LeaderboardActivityEntry() { Steps = 20000 },
                            User = new FitbitUser() { DisplayName = "Mr. 2", EncodedId = "222" }
                        },
                        new FriendLeaderboardEntry()
                        {
                            LastUpdateTime = null,
                            Rank = new LeaderboardRankEntry() { Steps = 3 },
                            Average = new LeaderboardActivityEntry() { Steps = 15000 },
                            Summary = new LeaderboardActivityEntry() { Steps = 15000 },
                            User = DefaultUserProfile
                        },
                        new FriendLeaderboardEntry()
                        {
                            LastUpdateTime = null,
                            Rank = new LeaderboardRankEntry() { Steps = 4 },
                            Average = new LeaderboardActivityEntry() { Steps = 10000 },
                            Summary = new LeaderboardActivityEntry() { Steps = 10000 },
                            User = new FitbitUser() { DisplayName = "Mr. 4", EncodedId = "444" }
                        },
                        new FriendLeaderboardEntry()
                        {
                            LastUpdateTime = null,
                            Rank = new LeaderboardRankEntry() { Steps = 5 },
                            Average = new LeaderboardActivityEntry() { Steps = 5000 },
                            Summary = new LeaderboardActivityEntry() { Steps = 5000 },
                            User = new FitbitUser() { DisplayName = "Mr. 5", EncodedId = "555" }
                        },
                    }
                };
            }
        }

        private FitbitUser DefaultUserProfile
        {
            get
            {
                return new FitbitUser()
                {
                    Age = 23,
                    Avatar = new Uri("https://static0.fitbit.com/images/profile/defaultProfile_100_male.png"),
                    Avatar150 = new Uri("https://static0.fitbit.com/images/profile/defaultProfile_150_male.png"),
                    Avatar640 = new Uri("https://static0.fitbit.com/images/profile/defaultProfile_640_male.png"),
                    AverageDailySteps = 0,
                    ClockTimeDisplayFormat = "12hour",
                    Country = "US",
                    DateOfBirth = "1995-01-01",
                    DisplayName = "Logan S.",
                    DisplayNameSetting = "name",
                    DistanceUnit = "en_US",
                    EncodedId = "4YQJ93",
                    FirstName = "Logan",
                    //FoodsLocale = "en_US",
                    //FullName = "",
                    Gender = "MALE",
                    //GlucoseUnit = "en_US",
                    Height = 182.9f,
                    HeightUnit = "en_US",
                    LastName = "Stromberg",
                    Locale = "en_US",
                    //MemberSince = "2016-10-06",
                    OffsetFromUTCMillis = -28800000L,
                    StartDayOfWeek = "SUNDAY",
                    //StrideLengthRunning = 90.3f,
                    //StrideLengthRunningType = "default",
                    //StrideLengthWalking = 75.9f,
                    //StrideLengthWalkingType = "default",
                    //SwimUnit = "en_US",
                    //Timezone = "America/Los Angeles",
                    WaterUnit = "METRIC",
                    //WaterUnitName = "ml",
                    Weight = 150f,
                    WeightUnit = "en_US"
                };
            }
        }

        private DailyActivityResponse DefaultActivityResponse
        {
            get
            {
                return new DailyActivityResponse()
                {
                    Activities = new List<FitnessActivity>(),
                    Goals = new FitnessGoals()
                    {
                        ActiveMinutes = 30,
                        Steps = 10000,
                        CaloriesOut = 2846,
                        Distance = 8.05f
                    },
                    Summary = new ActivitySummary()
                    {
                        ActiveScore = -1,
                        ActivityCalories = 0,
                        CaloriesBMR = 1838,
                        CaloriesOut = 1838,
                        Distances = new List<DistanceActivity>()
                        {
                            new DistanceActivity()
                            {
                                Activity = "total",
                                Distance = 4.15312498f
                            },
                            new DistanceActivity()
                            {
                                Activity = "tracker",
                                Distance = 4.15312498f
                            },
                            new DistanceActivity()
                            {
                                Activity = "loggedActivities",
                                Distance = 0
                            },
                            new DistanceActivity()
                            {
                                Activity = "moderatelyActive",
                                Distance = 0
                            },
                            new DistanceActivity()
                            {
                                Activity = "lightlyActive",
                                Distance = 0
                            },
                            new DistanceActivity()
                            {
                                Activity = "veryActive",
                                Distance = 1.0823f
                            },
                            new DistanceActivity()
                            {
                                Activity = "sedentaryActive",
                                Distance = 3.09323423f
                            }
                        },
                        FairlyActiveMinutes = 10,
                        LightlyActiveMinutes = 20,
                        MarginalCalories = 0,
                        SedentaryMinutes = 1045,
                        VeryActiveMinutes = 30,
                        Steps = 5433,
                        Floors = 13
                    }
                };
            }
        }

        private DailyActivityResponse AlternateActivityResponse
        {
            get
            {
                return new DailyActivityResponse()
                {
                    Activities = new List<FitnessActivity>(),
                    Goals = new FitnessGoals()
                    {
                        ActiveMinutes = 30,
                        Steps = 10000,
                        CaloriesOut = 2846,
                        Distance = 8.05f
                    },
                    Summary = new ActivitySummary()
                    {
                        ActiveScore = -1,
                        ActivityCalories = 0,
                        CaloriesBMR = 1918,
                        CaloriesOut = 1918,
                        Distances = new List<DistanceActivity>()
                            {
                                new DistanceActivity()
                                {
                                    Activity = "total",
                                    Distance = 3.12312498f
                                },
                                new DistanceActivity()
                                {
                                    Activity = "tracker",
                                    Distance = 3.12312498f
                                },
                                new DistanceActivity()
                                {
                                    Activity = "loggedActivities",
                                    Distance = 0
                                },
                                new DistanceActivity()
                                {
                                    Activity = "moderatelyActive",
                                    Distance = 0
                                },
                                new DistanceActivity()
                                {
                                    Activity = "lightlyActive",
                                    Distance = 0
                                },
                                new DistanceActivity()
                                {
                                    Activity = "veryActive",
                                    Distance = 1.0323f
                                },
                                new DistanceActivity()
                                {
                                    Activity = "sedentaryActive",
                                    Distance = 2.09323423f
                                }
                            },
                        FairlyActiveMinutes = 14,
                        LightlyActiveMinutes = 24,
                        MarginalCalories = 4,
                        SedentaryMinutes = 945,
                        VeryActiveMinutes = 50,
                        Steps = 9323,
                        Floors = 8
                    }
                };
            }
        }

        private ExtendedDateTime Yesterday
        {
            get
            {
                return ExtendedDateTime.Create(
                    TemporalType.Date,
                    new Dictionary<string, string>()
                    {
                        ["OFFSET"] = "-1",
                        ["OFFSET_UNIT"] = "day"
                    },
                    new TimexContext()
                    {
                        ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                        UseInference = false
                    });
            }
        }

        private ExtendedDateTime Tomorrow
        {
            get
            {
                return ExtendedDateTime.Create(
                    TemporalType.Date,
                    new Dictionary<string, string>()
                    {
                        ["OFFSET"] = "1",
                        ["OFFSET_UNIT"] = "day"
                    },
                    new TimexContext()
                    {
                        ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                        UseInference = false
                    });
            }
        }

        private ExtendedDateTime ThreeDaysAgo
        {
            get
            {
                return ExtendedDateTime.Create(
                    TemporalType.Date,
                    new Dictionary<string, string>()
                    {
                        ["OFFSET"] = "-3",
                        ["OFFSET_UNIT"] = "day"
                    },
                    new TimexContext()
                    {
                        ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                        UseInference = false
                    });
            }
        }

        private ExtendedDateTime Today
        {
            get
            {
                return ExtendedDateTime.Create(
                    TemporalType.Date,
                    new Dictionary<string, string>()
                    {
                        ["OFFSET"] = "0",
                        ["OFFSET_UNIT"] = "day"
                    },
                    new TimexContext()
                    {
                        ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                        UseInference = false
                    });
            }
        }

        private ExtendedDateTime ThisWeek
        {
            get
            {
                return ExtendedDateTime.Create(
                    TemporalType.Date,
                    new Dictionary<string, string>()
                    {
                        ["OFFSET"] = "0",
                        ["OFFSET_UNIT"] = "week"
                    },
                    new TimexContext()
                    {
                        ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                        UseInference = false
                    });
            }
        }

        private void AssertIsFutureResponse(string response)
        {
            Assert.IsTrue(
                string.Equals("That's up to you.", response) ||
                string.Equals("I can't predict the future.", response) ||
                string.Equals("That depends. Are you willing to step up?", response) ||
                string.Equals("The future is what you make of it.", response));
        }

        #endregion

        #region Tests

        [TestMethod]
        public async Task TestFitbitStepsTaken()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many steps", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_STEPS, "steps")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You walked 5433 steps so far today.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitStepsTakenYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            _fakeFitbitService.AddActivitySummary(AlternateActivityResponse, "2012-03-14");
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many steps did I take yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_STEPS, "steps")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You walked 9323 steps yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitStepsTakenTomorrow()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many steps will I take tomorrow", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_STEPS, "steps")
                        .AddTimexSlot(Constants.SLOT_DATE, "tomorrow", Tomorrow)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            AssertIsFutureResponse(response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitCaloriesBurned()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many calories", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_CALORIES, "calories")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You burned 1838 calories so far today.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitCaloriesBurnedYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            _fakeFitbitService.AddActivitySummary(AlternateActivityResponse, "2012-03-14");
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many calories did I burn yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_CALORIES, "calories")
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, "BURN", "burn")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You burned 1918 calories yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitCaloriesBurnedTomorrow()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many calories will I burn tomorrow", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_CALORIES, "calories")
                        .AddTimexSlot(Constants.SLOT_DATE, "tomorrow", Tomorrow)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            AssertIsFutureResponse(response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitFloorsClimbed()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many staircases", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_FLOORS, "staircases")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You climbed 13 floors so far today.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitFloorsClimbedYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            _fakeFitbitService.AddActivitySummary(AlternateActivityResponse, "2012-03-14");
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many staircases did I climb yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_FLOORS, "staircases")
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_CLIMB, "climb")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You climbed 8 floors yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitFloorsClimbedTomorrow()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many floors will I climb tomorrow", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_FLOORS, "floors")
                        .AddTimexSlot(Constants.SLOT_DATE, "tomorrow", Tomorrow)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            AssertIsFutureResponse(response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitActiveMinutes()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many active minutes", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_ACTIVE_MINUTES, "active_minutes")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You had 30 active minutes so far today.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitActiveMinutesYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            _fakeFitbitService.AddActivitySummary(AlternateActivityResponse, "2012-03-14");
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many active minutes did I have yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_ACTIVE_MINUTES, "active_minutes")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You had 50 active minutes yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitActiveMinutesTomorrow()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many active minutes will I have tomorrow", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_ACTIVE_MINUTES, "active_minutes")
                        .AddTimexSlot(Constants.SLOT_DATE, "tomorrow", Tomorrow)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            AssertIsFutureResponse(response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitMiledWalked()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many miles", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_MILES, "miles")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You walked 2.6 miles so far today.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitMiledWalkedYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            _fakeFitbitService.AddActivitySummary(AlternateActivityResponse, "2012-03-14");
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many miles did I walk yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_MILES, "miles")
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_WALK, "walk")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You walked 1.9 miles yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitMilesWalkedTomorrow()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many miles will I walk tomorrow", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_MILES, "miles")
                        .AddTimexSlot(Constants.SLOT_DATE, "tomorrow", Tomorrow)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            AssertIsFutureResponse(response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitKilometersWalked()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many kilometers", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_KILOMETERS, "kilometers")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You walked 4.2 kilometers so far today.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitKilometersWalkedYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            _fakeFitbitService.AddActivitySummary(AlternateActivityResponse, "2012-03-14");
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many kilometers did I walk yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_KILOMETERS, "kilometers")
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_WALK, "walk")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You walked 3.1 kilometers yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitKilometersWalkedTomorrow()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many kilometers will I walk tomorrow", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_KILOMETERS, "kilometers")
                        .AddTimexSlot(Constants.SLOT_DATE, "tomorrow", Tomorrow)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            AssertIsFutureResponse(response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitMilesWalkedFallbackWhenUnitUnspecified()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_US";
            profile.Locale = "en_US";
            _fakeFitbitService.SetUserProfile(profile);

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how far have I walked", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_DISTANCE, "far")
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_WALK, "walked")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You walked 2.6 miles so far today.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitKilometersWalkedFallbackWhenUnitUnspecified()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how far have I walked", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_DISTANCE, "far")
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_WALK, "walked")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You walked 4.2 kilometers so far today.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitActivitySummary()
        {
            _fakeFitbitService.ClearActivitySummaries();
            _fakeFitbitService.AddActivitySummary(AlternateActivityResponse, "2012-03-15");
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "show me my summary", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Today you walked 9323 steps, traveled 1.9 miles, burned 1918 calories, climbed 8 floors, and had 50 minutes of high activity.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetWeightNoWeightLogs()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how much do I weigh", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_WEIGHT, "weigh")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You have not logged your weight in the past 30 days.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetWeightPounds()
        {
            _fakeFitbitService.AddWeightLog(new WeightLogInternal()
            {
                Weight = 73.434523f,
                BMI = 23.57f,
                Date = "2012-03-13",
                Time = "23:59:59",
                LogId = 1330991999000L,
                Source = "API"
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how much do I weigh", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_WEIGHT, "weigh")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You weigh 161.9 pounds as of 2 days ago.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetWeightKilograms()
        {
            _fakeFitbitService.AddWeightLog(new WeightLogInternal()
            {
                Weight = 73.434523f,
                BMI = 23.57f,
                Date = "2012-03-14",
                Time = "23:59:59",
                LogId = 1330991999000L,
                Source = "API"
            });

            FitbitUser profile = DefaultUserProfile;
            profile.WeightUnit = "de_DE";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how much do I weigh", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_WEIGHT, "weigh")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You weigh 73.4 kilograms as of yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetWeightStone()
        {
            _fakeFitbitService.AddWeightLog(new WeightLogInternal()
            {
                Weight = 73.434523f,
                BMI = 23.57f,
                Date = "2012-03-14",
                Time = "23:59:59",
                LogId = 1330991999000L,
                Source = "API"
            });

            FitbitUser profile = DefaultUserProfile;
            profile.WeightUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how much do I weigh", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_WEIGHT, "weigh")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You weigh 11.6 stone as of yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetBMI()
        {
            _fakeFitbitService.AddWeightLog(new WeightLogInternal()
            {
                Weight = 73.434523f,
                BMI = 23.57f,
                Date = "2012-03-15",
                Time = "23:59:59",
                LogId = 1330991999000L,
                Source = "API"
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my body mass index", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_BMI, "body mass index")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your body mass index is 23.6 as of today.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetBMIPast()
        {
            _fakeFitbitService.AddWeightLog(new WeightLogInternal()
            {
                Weight = 73.434523f,
                BMI = 23.57f,
                Date = "2012-03-11",
                Time = "23:59:59",
                LogId = 1330991999000L,
                Source = "API"
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my body mass index", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_BMI, "body mass index")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your body mass index is 23.6 as of 4 days ago.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetHeightFeet()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.Height = 174;
            _fakeFitbitService.SetUserProfile(profile);

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how tall am I", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_HEIGHT, "tall")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You are 5 feet 9 inches tall.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetHeightFeetExact()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how tall am I", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_HEIGHT, "tall")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You are 6 feet tall.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetHeightCentimeters()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.HeightUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how tall am I", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_HEIGHT, "tall")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You are 183 centimeters tall.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetAge()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how old am I", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_AGE, "old")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You are 23 years old.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitCaloriesLogged()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many calories did I eat", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_CALORIES, "calories")
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "eat")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You logged 912 calories so far today.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitCaloriesLoggedYesterday()
        {
            _fakeFitbitService.AddFoodLogSummary(new FoodLogGetResponse()
            {
                Summary = new FoodSummary()
                {
                    Calories = 1
                }
            }, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many calories did I eat yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_CALORIES, "calories")
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "eat")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You logged 1 calorie yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }
        
        [TestMethod]
        public async Task TestFitbitShowStepGoal()
        {
            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = 30,
                CaloriesOut = 2100,
                Distance = 5.0f,
                Floors = 10,
                Steps = 12000
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my step goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your current goal is 12000 steps per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepGoalNoGoalPast()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = null;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what was my step goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You did not have a step goal set yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepGoalPast()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = 12000;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what was my step goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your goal yesterday was 12000 steps per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepGoalNoGoal()
        {
            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = null,
                CaloriesOut = null,
                Distance = null,
                Floors = null,
                Steps = null
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my step goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You do not have a step goal set.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoal()
        {
            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = 30,
                CaloriesOut = 2100,
                Distance = 5.0f,
                Floors = 10,
                Steps = 12000
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my calorie goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_CALORIES, "calorie")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your current goal is 2100 calories burned per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoalNoGoalPast()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.CaloriesOut = null;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what was my calorie goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_CALORIES, "calorie")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You did not have a calorie goal set yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoalPast()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.CaloriesOut = 1500;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what was my calorie goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_CALORIES, "calorie")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your goal yesterday was 1500 calories burned per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoalNoGoal()
        {
            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = null,
                CaloriesOut = null,
                Distance = null,
                Floors = null,
                Steps = null
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my calorie goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_CALORIES, "calorie")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You do not have a calorie goal set.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoal()
        {
            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = 30,
                CaloriesOut = 2100,
                Distance = 5.0f,
                Floors = 20,
                Steps = 12000
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my staircase goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_FLOORS, "staircase")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your current goal is 20 floors climbed per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalNoGoalPast()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Floors = null;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what was my staircase goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_FLOORS, "staircase")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You did not have a floors goal set yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalPast()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Floors = 17;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what was my staircase goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_FLOORS, "staircase")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your goal yesterday was 17 floors climbed per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalNoGoal()
        {
            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = null,
                CaloriesOut = null,
                Distance = null,
                Floors = null,
                Steps = null
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my staircase goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_FLOORS, "staircase")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You do not have a floors goal set.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalEnUs()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_US";
            profile.Locale = "en_US";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = 30,
                CaloriesOut = 2100,
                Distance = 5.0f,
                Floors = 10,
                Steps = 12000
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your current goal is 3.1 miles per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalEnGb()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = 30,
                CaloriesOut = 2100,
                Distance = 5.0f,
                Floors = 10,
                Steps = 12000
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your current goal is 5 kilometers per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowMilesGoal()
        {
            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = 30,
                CaloriesOut = 2100,
                Distance = 5.0f,
                Floors = 10,
                Steps = 12000
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my mileage goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_MILES, "mileage")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your current goal is 3.1 miles per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowMilesGoalNoGoalPast()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = null;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what was my mileage goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_MILES, "mileage")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You did not have a distance goal set yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowMilesGoalPast()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what was my mileage goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_MILES, "mileage")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your goal yesterday was 3.1 miles per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowKilometersGoal()
        {
            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = 30,
                CaloriesOut = 2100,
                Distance = 5.0f,
                Floors = 10,
                Steps = 12000
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my kilometer goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_KILOMETERS, "kilometer")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your current goal is 5 kilometers per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowKilometersGoalNoGoalPast()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = null;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what was my kilometers goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_KILOMETERS, "kilometers")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You did not have a distance goal set yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowKilometersGoalPast()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what was my kilometers goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_KILOMETERS, "kilometers")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your goal yesterday was 5 kilometers per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalNoGoal()
        {
            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = null,
                CaloriesOut = null,
                Distance = null,
                Floors = null,
                Steps = null
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what is my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You do not have a distance goal set.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowGoalSummaryMiles()
        {
            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = 30,
                CaloriesOut = 2100,
                Distance = 5.0f,
                Floors = 10,
                Steps = 12000
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what are my goals", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your current daily goals are to walk 12000 steps, travel 3.1 miles, burn 2100 calories, and climb 10 floors.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowGoalSummaryKilometers()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.SetDailyGoals(new FitnessGoals()
            {
                ActiveMinutes = 30,
                CaloriesOut = 2100,
                Distance = 5.0f,
                Floors = 10,
                Steps = 12000
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what are my goals", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your current daily goals are to walk 12000 steps, travel 5 kilometers, burn 2100 calories, and climb 10 floors.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowGoalSummaryPastMiles()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = 12500;
            act.Goals.ActiveMinutes = 31;
            act.Goals.CaloriesOut = 2105;
            act.Goals.Distance = 5.0f;
            act.Goals.Floors = 11;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what were my goals yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.95f)
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your daily goals yesterday were to walk 12500 steps, travel 3.1 miles, burn 2105 calories, and climb 11 floors.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowGoalSummaryPastKilometers()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);
            
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = 12500;
            act.Goals.ActiveMinutes = 31;
            act.Goals.CaloriesOut = 2105;
            act.Goals.Distance = 5.0f;
            act.Goals.Floors = 11;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "what were my goals yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_GOALS, 0.95f)
                    .AddTagHypothesis(0.95f)
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your daily goals yesterday were to walk 12500 steps, travel 5 kilometers, burn 2105 calories, and climb 11 floors.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepGoalProgressFar()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = 20000;
            act.Summary.Steps = 5433;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my step goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You'll need 14567 more steps to reach your daily goal of 20000.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepGoalProgressMedium()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = 10000;
            act.Summary.Steps = 5433;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my step goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("With just another 4567 steps you'll hit your daily goal of 10000.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepGoalProgressNear()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = 6000;
            act.Summary.Steps = 5433;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my step goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Only 567 steps left towards your goal! So close!", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepGoalProgressAbove()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = 4000;
            act.Summary.Steps = 5433;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my step goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You are 1433 steps above your daily goal of 4000. Congratulations!", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepGoalProgressNoGoal()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = null;
            act.Summary.Steps = 5433;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to step goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You do not have a step goal set.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepGoalProgressNoGoalYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = null;
            act.Summary.Steps = 5433;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to step goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You did not have a step goal set yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepGoalProgressBelowYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = 8000;
            act.Summary.Steps = 5000;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "did I meet my step goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You were short 3000 steps from your daily goal yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepGoalProgressAboveYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Steps = 8000;
            act.Summary.Steps = 12000;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "did I meet my step goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You exceeded your daily goal by 4000 steps yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoalProgressNear()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.CaloriesOut = 2100;
            act.Summary.CaloriesOut = 1838;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my calorie goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "CALORIES", "calorie")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You only need to burn 262 more calories to meet your goal! So close!", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoalProgressMedium()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.CaloriesOut = 2800;
            act.Summary.CaloriesOut = 1838;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my calorie goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "CALORIES", "calorie")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("With just another 962 calories burned you'll hit your daily goal of 2800.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoalProgressFar()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.CaloriesOut = 3500;
            act.Summary.CaloriesOut = 1838;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my calorie goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "CALORIES", "calorie")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You'll need to burn 1662 more calories to reach your daily goal of 3500.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoalProgressAbove()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.CaloriesOut = 1200;
            act.Summary.CaloriesOut = 1838;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my calorie goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "CALORIES", "calorie")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You burned 638 more calories than your daily goal of 1200. Congratulations!", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoalProgressNoGoal()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.CaloriesOut = null;
            act.Summary.CaloriesOut = 1838;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my calorie goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "CALORIES", "calorie")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You do not have a calorie goal set.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoalProgressNoGoalYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.CaloriesOut = null;
            act.Summary.CaloriesOut = 5433;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my calorie goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "CALORIES", "calorie")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You did not have a calorie goal set yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoalProgressBelowYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.CaloriesOut = 2500;
            act.Summary.CaloriesOut = 2000;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "did I meet my calorie goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "CALORIES", "calorie")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You were short 500 calories from your daily goal yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowCalorieGoalProgressAboveYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.CaloriesOut = 2000;
            act.Summary.CaloriesOut = 2800;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "did I meet my calorie goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "CALORIES", "calorie")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You exceeded your daily goal by 800 calories yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalProgressAbove()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Floors = 20;
            act.Summary.Floors = 25;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my staircase goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "FLOORS", "staircase")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You climbed 5 more floors than your daily goal of 20. Congratulations!", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalProgressAboveYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Floors = 20;
            act.Summary.Floors = 25;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my staircase goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "FLOORS", "staircase")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You exceeded your daily goal by 5 floors yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalProgressBelowYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Floors = 20;
            act.Summary.Floors = 15;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my staircase goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "FLOORS", "staircase")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You were short 5 floors from your daily goal yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalProgressFar()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Floors = 20;
            act.Summary.Floors = 1;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my staircase goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "FLOORS", "staircase")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You'll need to climb 19 more floors to reach your daily goal of 20.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalProgressMedium()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Floors = 20;
            act.Summary.Floors = 13;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my staircase goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "FLOORS", "staircase")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("With just another 7 floors climbed you'll hit your daily goal of 20.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalProgressNear()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Floors = 20;
            act.Summary.Floors = 19;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my staircase goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "FLOORS", "staircase")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You only need to climb 1 more floor to meet your goal! So close!", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalProgressExact()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Floors = 20;
            act.Summary.Floors = 20;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my staircase goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "FLOORS", "staircase")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You met your daily goal of 20 floors. Congratulations!", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalProgressNoGoal()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Floors = null;
            act.Summary.Floors = 12;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my staircase goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "FLOORS", "staircase")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You do not have a floors goal set.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowFloorsGoalProgressNoGoalYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Floors = null;
            act.Summary.Floors = 12;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my staircase goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, "FLOORS", "staircase")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You did not have a floors goal set yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressFarEnUs()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_US";
            profile.Locale = "en_US";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 0.2f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");
            
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You'll need to go 3 more miles to reach your goal.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressMediumEnUs()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_US";
            profile.Locale = "en_US";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 3.5f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You're just 0.9 miles away from your goal.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressCloseEnUs()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_US";
            profile.Locale = "en_US";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 4.0f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Only 0.6 more miles are left to hit your goal.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressAboveEnUs()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_US";
            profile.Locale = "en_US";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 6.0f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You are 0.6 miles above your goal.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressAboveYesterdayEnUs()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_US";
            profile.Locale = "en_US";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 6.0f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my distance goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You were 0.6 miles above your goal yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressBelowYesterdayEnUs()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_US";
            profile.Locale = "en_US";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 1.0f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my distance goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You were 2.5 miles below your goal yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressFarEnGb()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 0.2f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You'll need to go 4.8 more kilometers to reach your goal.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressMediumEnGb()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 3.5f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You're just 1.5 kilometers away from your goal.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressCloseEnGb()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 4.0f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Only 1 more kilometer is left to hit your goal.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressAboveEnGb()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 6.0f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You are 1 kilometer above your goal.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressAboveYesterdayEnGb()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 6.0f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my distance goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You were 1 kilometer above your goal yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressBelowYesterdayEnGb()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = 5.0f;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 1.0f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my distance goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You were 4 kilometers below your goal yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressNoGoal()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = null;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 1.0f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my distance goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You do not have a distance goal set.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowDistanceGoalProgressNoGoalYesterday()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Goals.Distance = null;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 1.0f }
            };
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my distance goal yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_DISTANCE, "distance")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You did not have a distance goal set yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowGoalProgressSummaryMiles()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_US";
            profile.Locale = "en_US";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Summary.CaloriesOut = 2000;
            act.Goals.CaloriesOut = 2500;
            act.Summary.Steps = 5000;
            act.Goals.Steps = 6000;
            act.Summary.Floors = 5;
            act.Goals.Floors = 15;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 2.5f }
            };
            act.Goals.Distance = 5;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my goals", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("To meet your goals today you'll need to walk 1000 steps, burn 500 calories, climb 10 floors, and travel 1.6 miles.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowGoalProgressSummaryPartial()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Summary.CaloriesOut = 3000;
            act.Goals.CaloriesOut = 2500;
            act.Summary.Steps = 5000;
            act.Goals.Steps = 6000;
            act.Summary.Floors = 5;
            act.Goals.Floors = 15;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 7.5f }
            };
            act.Goals.Distance = 5;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my goals", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("To meet your goals today you'll need to walk 1000 steps and climb 10 floors.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowGoalProgressSummaryKilometers()
        {
            FitbitUser profile = DefaultUserProfile;
            profile.DistanceUnit = "en_GB";
            profile.Locale = "en_GB";
            _fakeFitbitService.SetUserProfile(profile);

            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Summary.CaloriesOut = 2000;
            act.Goals.CaloriesOut = 2500;
            act.Summary.Steps = 5000;
            act.Goals.Steps = 6000;
            act.Summary.Floors = 5;
            act.Goals.Floors = 15;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 2.5f }
            };
            act.Goals.Distance = 5;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-15");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close am I to my goals", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("To meet your goals today you'll need to walk 1000 steps, burn 500 calories, climb 10 floors, and travel 2.5 kilometers.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowGoalProgressSummaryYesterdayNotMet()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Summary.CaloriesOut = 2000;
            act.Goals.CaloriesOut = 2500;
            act.Summary.Steps = 5000;
            act.Goals.Steps = 6000;
            act.Summary.Floors = 5;
            act.Goals.Floors = 15;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 2.5f }
            };
            act.Goals.Distance = 5;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my goals yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.95f)
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You did not meet your goals yesterday. You needed to walk 1000 steps, burn 500 calories, climb 10 floors, and travel 1.6 miles.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowGoalProgressSummaryYesterdayNotMetPartial()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Summary.CaloriesOut = 3000;
            act.Goals.CaloriesOut = 2500;
            act.Summary.Steps = 5000;
            act.Goals.Steps = 6000;
            act.Summary.Floors = 25;
            act.Goals.Floors = 15;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 2.5f }
            };
            act.Goals.Distance = 5;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my goals yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.95f)
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You did not meet your goals yesterday. You needed to walk 1000 steps and travel 1.6 miles.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowGoalProgressSummaryYesterdayMet()
        {
            _fakeFitbitService.ClearActivitySummaries();
            DailyActivityResponse act = DefaultActivityResponse;
            act.Summary.CaloriesOut = 3000;
            act.Goals.CaloriesOut = 2500;
            act.Summary.Steps = 10000;
            act.Goals.Steps = 6000;
            act.Summary.Floors = 25;
            act.Goals.Floors = 15;
            act.Summary.Distances = new List<DistanceActivity>()
            {
                new DistanceActivity() { Activity = "total", Distance = 7.5f }
            };
            act.Goals.Distance = 5;
            _fakeFitbitService.AddActivitySummary(act, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how close was I to my goals yesterday", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_REMAINING, 0.95f)
                    .AddTagHypothesis(0.95f)
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You met all your goals yesterday!", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        [Ignore]
        public async Task TestFitbitSetStepGoal()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set a new step goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_GOAL, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Okay, what is your new daily steps goal?", response.ResponseText);
            Assert.IsTrue(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set a new step goal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_GOAL, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .Build()
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Okay, you have a new goal of 12000 steps per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        [Ignore]
        public async Task TestFitbitSetStepGoalOneshot()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set a new step goal of 12000 steps", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_GOAL, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_GOAL_TYPE, Constants.CANONICAL_GOAL_STEPS, "step")
                        .AddBasicSlot(Constants.SLOT_GOAL_VALUE, "12000")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Okay, you have a new goal of 12000 steps per day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetBatteryLevelNoDevices()
        {
            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "what is my battery level", InputMethod.Typed)
            .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                .AddTagHypothesis(0.9f)
                    .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_BATTERY, "battery level")
                    .Build()
                .Build()
            .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You don't have any Fitbit devices.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetBatteryLevel()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Medium,
                DeviceVersion = "Charge HR",
                Id = "27072629",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "what is my battery level", InputMethod.Typed)
            .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                .AddTagHypothesis(0.9f)
                    .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_BATTERY, "battery level")
                    .Build()
                .Build()
            .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your Charge HR battery level is medium.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetBatteryLevelHaventSynced()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Medium,
                DeviceVersion = "Charge HR",
                Id = "27072629",
                LastSyncTime = new DateTime(2012, 3, 13, 09, 12, 33),
                Type = "TRACKER"
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "what is my battery level", InputMethod.Typed)
            .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                .AddTagHypothesis(0.9f)
                    .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_BATTERY, "battery level")
                    .Build()
                .Build()
            .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your Charge HR battery level is medium. However, this device also has not synced in the last 2 days and may be different.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetBatteryLevelTwoDevices()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.High,
                DeviceVersion = "Aria",
                Id = "1234",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "SCALE"
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "what is my battery level", InputMethod.Typed)
            .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                .AddTagHypothesis(0.9f)
                    .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_BATTERY, "battery level")
                    .Build()
                .Build()
            .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your Flex 2 battery is low, and your Aria battery is high.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitGetBatteryLevelThreeDevices()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Empty,
                DeviceVersion = "Flex",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Medium,
                DeviceVersion = "Aria",
                Id = "1234",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "SCALE"
            });

            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.High,
                DeviceVersion = "Ace",
                Id = "567",
                LastSyncTime = new DateTime(2012, 3, 15, 03, 12, 33),
                Type = "TRACKER"
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "what is my battery level", InputMethod.Typed)
            .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_MEASUREMENT, 0.95f)
                .AddTagHypothesis(0.9f)
                    .AddCanonicalizedSlot(Constants.SLOT_MEASUREMENT, Constants.CANONICAL_MEASUREMENT_BATTERY, "battery level")
                    .Build()
                .Build()
            .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Your Flex battery is empty, your Aria battery is medium, and your Ace battery is high.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitLogout()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "log me out", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_LOGOUT, 0.95f)
                    .Build()
                .Build();
            
            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You are now logged out of your Fitbit account.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            request = new DialogRequestBuilder<DialogRequest>((x) => x, "show me my fitness summary", InputMethod.Typed)
            .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                .Build()
            .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.IsTrue(response.ResponseText.Contains("Before using authenticated services"));
        }

        [TestMethod]
        public async Task TestFitbitLogoutNotLoggedIn()
        {
            _testDriver.MockOAuthToken = null;

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "log me out", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_LOGOUT, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You are not logged in to your Fitbit account.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            request = new DialogRequestBuilder<DialogRequest>((x) => x, "show me my fitness summary", InputMethod.Typed)
            .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                .Build()
            .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.IsTrue(response.ResponseText.Contains("Before using authenticated services"));
        }

        [TestMethod]
        public async Task TestFitbitActivitySummaryError()
        {
            _fakeFitbitService.ClearActivitySummaries();

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many steps", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_STEPS, "steps")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Failure, response.ExecutionResult);
        }

        [TestMethod]
        public async Task TestFitbitShowStepLeaderboardTop()
        {
            _fakeFitbitService.SetLeaderboard(new FriendsLeaderboardResponse()
            {
                HideMeFromLeaderboard = false,
                Friends = new List<FriendLeaderboardEntry>()
                {
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 1 },
                        Average = new LeaderboardActivityEntry() { Steps = 25000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 25000 },
                        User = DefaultUserProfile
                    },
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 2 },
                        Average = new LeaderboardActivityEntry() { Steps = 20000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 20000 },
                        User = new FitbitUser() { DisplayName = "Mr. 2", EncodedId = "222" }
                    },

                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 3 },
                        Average = new LeaderboardActivityEntry() { Steps = 15000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 15000 },
                        User = new FitbitUser() { DisplayName = "Mr. 3", EncodedId = "333" }
                    },
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 4 },
                        Average = new LeaderboardActivityEntry() { Steps = 10000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 10000 },
                        User = new FitbitUser() { DisplayName = "Mr. 4", EncodedId = "444" }
                    },
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 5 },
                        Average = new LeaderboardActivityEntry() { Steps = 5000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 5000 },
                        User = new FitbitUser() { DisplayName = "Mr. 5", EncodedId = "555" }
                    },
                }
            }); 

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "where am I on the leaderboard", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_LEADERBOARD, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You currently rank 1st on the leaderboard for step count.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepLeaderboardMiddle()
        {
            _fakeFitbitService.SetLeaderboard(new FriendsLeaderboardResponse()
            {
                HideMeFromLeaderboard = false,
                Friends = new List<FriendLeaderboardEntry>()
                {
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 1 },
                        Average = new LeaderboardActivityEntry() { Steps = 25000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 25000 },
                        User = new FitbitUser() { DisplayName = "Mr. 1", EncodedId = "111" }
                    },
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 2 },
                        Average = new LeaderboardActivityEntry() { Steps = 20000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 20000 },
                        User = new FitbitUser() { DisplayName = "Mr. 2", EncodedId = "222" }
                    },
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 3 },
                        Average = new LeaderboardActivityEntry() { Steps = 15000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 15000 },
                        User = DefaultUserProfile
                    },
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 4 },
                        Average = new LeaderboardActivityEntry() { Steps = 10000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 10000 },
                        User = new FitbitUser() { DisplayName = "Mr. 4", EncodedId = "444" }
                    },
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 5 },
                        Average = new LeaderboardActivityEntry() { Steps = 5000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 5000 },
                        User = new FitbitUser() { DisplayName = "Mr. 5", EncodedId = "555" }
                    },
                }
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "where am I on the leaderboard", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_LEADERBOARD, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You currently rank 3rd on the leaderboard for step count.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowStepLeaderboardBottom()
        {
            _fakeFitbitService.SetLeaderboard(new FriendsLeaderboardResponse()
            {
                HideMeFromLeaderboard = false,
                Friends = new List<FriendLeaderboardEntry>()
                {
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 1 },
                        Average = new LeaderboardActivityEntry() { Steps = 25000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 25000 },
                        User = new FitbitUser() { DisplayName = "Mr. 1", EncodedId = "111" }
                    },
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 2 },
                        Average = new LeaderboardActivityEntry() { Steps = 20000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 20000 },
                        User = new FitbitUser() { DisplayName = "Mr. 2", EncodedId = "222" }
                    },
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 3 },
                        Average = new LeaderboardActivityEntry() { Steps = 15000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 15000 },
                        User = new FitbitUser() { DisplayName = "Mr. 3", EncodedId = "333" }
                    },
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 4 },
                        Average = new LeaderboardActivityEntry() { Steps = 10000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 10000 },
                        User = new FitbitUser() { DisplayName = "Mr. 4", EncodedId = "444" }
                    },
                    new FriendLeaderboardEntry()
                    {
                        LastUpdateTime = null,
                        Rank = new LeaderboardRankEntry() { Steps = 5 },
                        Average = new LeaderboardActivityEntry() { Steps = 5000 },
                        Summary = new LeaderboardActivityEntry() { Steps = 5000 },
                        User = DefaultUserProfile
                    },
                }
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "where am I on the leaderboard", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_LEADERBOARD, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You currently rank 5th on the leaderboard for step count.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowExerciseCount()
        {
            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 13, 0, 0, 0, TimeSpan.Zero),
                Calories = 232,
                Duration = (long)(TimeSpan.FromMinutes(33).TotalMilliseconds)
            });

            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 10, 0, 0, 0, TimeSpan.Zero),
                Calories = 316,
                Duration = (long)(TimeSpan.FromMinutes(40).TotalMilliseconds)
            });

            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 12, 0, 0, 0, TimeSpan.Zero),
                Calories = 445,
                Duration = (long)(TimeSpan.FromMinutes(45).TotalMilliseconds)
            });

            // should not appear in this summary
            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 06, 0, 0, 0, TimeSpan.Zero),
                Calories = 10000,
                Duration = (long)(TimeSpan.FromMinutes(1000).TotalMilliseconds)
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "how many times did I work out", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_COUNT, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You worked out a total of 3 times this week. The average workout lasted 39 minutes and burned 331 calories.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowExerciseCountThisWeek()
        {
            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 13, 0, 0, 0, TimeSpan.Zero),
                Calories = 200,
                Duration = (long)(TimeSpan.FromMinutes(20).TotalMilliseconds)
            });

            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 12, 0, 0, 0, TimeSpan.Zero),
                Calories = 400,
                Duration = (long)(TimeSpan.FromMinutes(40).TotalMilliseconds)
            });

            // should not appear in this summary because of week boundaries
            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 9, 0, 0, 0, TimeSpan.Zero),
                Calories = 10000,
                Duration = (long)(TimeSpan.FromMinutes(1000).TotalMilliseconds)
            });

            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 06, 0, 0, 0, TimeSpan.Zero),
                Calories = 10000,
                Duration = (long)(TimeSpan.FromMinutes(1000).TotalMilliseconds)
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "how many times did I work out this week", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_COUNT, 0.95f)
                    .AddTagHypothesis(0.95f)
                        .AddTimexSlot(Constants.SLOT_DATE, "this week", ThisWeek)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You worked out a total of 2 times this week. The average workout lasted 30 minutes and burned 300 calories.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowExerciseCountToday()
        {
            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 15, 7, 0, 0, TimeSpan.Zero),
                Calories = 200,
                Duration = (long)(TimeSpan.FromMinutes(20).TotalMilliseconds)
            });

            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 14, 23, 10, 0, TimeSpan.Zero),
                Calories = 400,
                Duration = (long)(TimeSpan.FromMinutes(40).TotalMilliseconds)
            });
            
            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 9, 12, 0, 0, TimeSpan.Zero),
                Calories = 10000,
                Duration = (long)(TimeSpan.FromMinutes(1000).TotalMilliseconds)
            });

            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 06, 12, 0, 0, TimeSpan.Zero),
                Calories = 10000,
                Duration = (long)(TimeSpan.FromMinutes(1000).TotalMilliseconds)
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "how many times did I work out today", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_COUNT, 0.95f)
                    .AddTagHypothesis(0.95f)
                        .AddTimexSlot(Constants.SLOT_DATE, "today", Today)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You worked out a total of 1 time today. The average workout lasted 20 minutes and burned 200 calories.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowExerciseCountThreeDaysAgo()
        {
            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 13, 0, 0, 0, TimeSpan.Zero),
                Calories = 232,
                Duration = (long)(TimeSpan.FromMinutes(33).TotalMilliseconds)
            });

            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 12, 0, 0, 0, TimeSpan.Zero),
                Calories = 1,
                Duration = (long)(TimeSpan.FromMinutes(1).TotalMilliseconds)
            });


            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 11, 0, 0, 0, TimeSpan.Zero),
                Calories = 316,
                Duration = (long)(TimeSpan.FromMinutes(40).TotalMilliseconds)
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "how many times did I work out", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_COUNT, 0.95f)
                    .AddTagHypothesis(0.95f)
                        .AddTimexSlot(Constants.SLOT_DATE, "three days ago", ThreeDaysAgo)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You worked out a total of 1 time 3 days ago. The average workout lasted 1 minute and burned 1 calorie.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowExerciseCountNoResults()
        {
            // should not appear in this summary
            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 06, 0, 0, 0, TimeSpan.Zero),
                Calories = 10000,
                Duration = (long)(TimeSpan.FromMinutes(1000).TotalMilliseconds)
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "how many times did I work out", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_COUNT, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("It doesn't seem you've logged any workouts this week.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowExerciseCountThreeDaysAgoNoResults()
        {
            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "how many times did I work out three days ago", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_COUNT, 0.95f)
                    .AddTagHypothesis(0.95f)
                        .AddTimexSlot(Constants.SLOT_DATE, "three days ago", ThreeDaysAgo)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("It doesn't seem you've logged any workouts 3 days ago.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowLastExercise()
        {
            _fakeFitbitService.AddActivity(new FitnessActivity()
            {
                StartTime = new DateTimeOffset(2012, 03, 13, 17, 0, 0, TimeSpan.Zero),
                Calories = 3,
                Duration = (long)(TimeSpan.FromMinutes(14).TotalMilliseconds)
            });

            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "when was my last exercise", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                .AddTagHypothesis(0.9f)
                    .AddCanonicalizedSlot("order_ref", "PAST", "last")
                    .Build()
                .Build()
            .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You last exercised 2 days ago at 5:00 PM. It lasted 14 minutes, burning a total of 3 calories.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowLastExerciseNoResults()
        {
            DialogRequest request =
            new DialogRequestBuilder<DialogRequest>((x) => x, "when was my last exercise", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                .AddTagHypothesis(0.9f)
                    .AddCanonicalizedSlot(Constants.SLOT_ORDER_REF, Constants.CANONICAL_ORDER_REF_PAST, "last")
                    .Build()
                .Build()
            .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("It doesn't seem you've logged any workouts lately.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitSetAlarmOneshot()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set an alarm for every monday morning at 6 AM", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_ALARM, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddTimexSlot(Constants.SLOT_TIME, "every monday morning at 6 AM",
                            ExtendedDateTime.Create(
                            TemporalType.Set,
                            new Dictionary<string, string>()
                            {
                                ["QUANT"] = "EVERY",
                                ["D"] = "1",
                                ["FREQ"] = "1week",
                                ["POD"] = "MO",
                                ["hh"] = "6",
                                //["AMPM"] = "not_specified",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                        .Build()
                    .Build()
                .Build();

            //DialogRequest request =
            //    new DialogRequestBuilder<DialogRequest>((x) => x, "set an alarm for 5:00", InputMethod.Typed)
            //        .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_ALARM, 0.95f)
            //        .AddTagHypothesis(0.9f)
            //            .AddTimexSlot(Constants.SLOT_TIME, "5:00",
            //                ExtendedDateTime.Create(
            //                TemporalType.Time,
            //                new Dictionary<string, string>()
            //                {
            //                    ["hh"] = "5",
            //                    ["mm"] = "00",
            //                    ["AMPM"] = "not_specified",
            //                },
            //                new TimexContext()
            //                {
            //                    ReferenceDateTime = _timeProvider.Time.UtcDateTime,
            //                    UseInference = false
            //                }))
            //            .Build()
            //        .Build()
            //    .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Okay, your alarm is set for 6:00 AM every Monday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            List<Alarm> alarms = _fakeFitbitService.GetAlarms("123");
            Assert.AreEqual(1, alarms.Count);
            Alarm setAlarm = alarms[0];
            Assert.AreEqual(true, setAlarm.Enabled);
            Assert.AreEqual(true, setAlarm.Recurring);
            Assert.AreEqual("06:00-08:00", setAlarm.Time);
            Assert.AreEqual(1, setAlarm.WeekDays.Count);
            Assert.AreEqual("MONDAY", setAlarm.WeekDays[0]);
        }

        [TestMethod]
        public async Task TestFitbitSetAlarmTimeOnly()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set an alarm for 7:00 AM", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_ALARM, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddTimexSlot(Constants.SLOT_TIME, "7:00 AM",
                            ExtendedDateTime.Create(
                            TemporalType.Time,
                            new Dictionary<string, string>()
                            {
                                ["hh"] = "7",
                                ["mm"] = "00",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Okay, your alarm is set for 7:00 AM.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            List<Alarm> alarms = _fakeFitbitService.GetAlarms("123");
            Assert.AreEqual(1, alarms.Count);
            Alarm setAlarm = alarms[0];
            Assert.AreEqual(true, setAlarm.Enabled);
            Assert.AreEqual(false, setAlarm.Recurring);
            Assert.AreEqual("07:00-08:00", setAlarm.Time);
            Assert.AreEqual(7, setAlarm.WeekDays.Count);
        }

        [TestMethod]
        public async Task TestFitbitSetAlarmTimeOnlyWithSyncMessage()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _timeProvider.Time = new DateTimeOffset(2012, 3, 15, 20, 5, 0, TimeSpan.FromHours(-8)).ToUniversalTime();

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set an alarm for 7:00 AM", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_ALARM, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddTimexSlot(Constants.SLOT_TIME, "7:00 AM",
                            ExtendedDateTime.Create(
                            TemporalType.Time,
                            new Dictionary<string, string>()
                            {
                                ["hh"] = "7",
                                ["mm"] = "00",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Okay, your alarm is set for 7:00 AM. Be sure to sync your device before you go to sleep.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            List<Alarm> alarms = _fakeFitbitService.GetAlarms("123");
            Assert.AreEqual(1, alarms.Count);
            Alarm setAlarm = alarms[0];
            Assert.AreEqual(true, setAlarm.Enabled);
            Assert.AreEqual(false, setAlarm.Recurring);
            Assert.AreEqual("07:00-08:00", setAlarm.Time);
            Assert.AreEqual(7, setAlarm.WeekDays.Count);
        }

        [TestMethod]
        public async Task TestFitbitSetAlarmOneshotWithSyncMessage()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _timeProvider.Time = new DateTimeOffset(2012, 3, 15, 20, 5, 0, TimeSpan.FromHours(-8)).ToUniversalTime();

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set an alarm for every monday morning at 6 AM", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_ALARM, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddTimexSlot(Constants.SLOT_TIME, "every monday morning at 6 AM",
                            ExtendedDateTime.Create(
                            TemporalType.Set,
                            new Dictionary<string, string>()
                            {
                                ["QUANT"] = "EVERY",
                                ["D"] = "1",
                                ["FREQ"] = "1week",
                                ["POD"] = "MO",
                                ["hh"] = "6",
                                //["AMPM"] = "not_specified",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Okay, your alarm is set for 6:00 AM every Monday. Be sure to sync your device before you go to sleep.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            List<Alarm> alarms = _fakeFitbitService.GetAlarms("123");
            Assert.AreEqual(1, alarms.Count);
            Alarm setAlarm = alarms[0];
            Assert.AreEqual(true, setAlarm.Enabled);
            Assert.AreEqual(true, setAlarm.Recurring);
            Assert.AreEqual("06:00-08:00", setAlarm.Time);
            Assert.AreEqual(1, setAlarm.WeekDays.Count);
            Assert.AreEqual("MONDAY", setAlarm.WeekDays[0]);
        }

        [TestMethod]
        public async Task TestFitbitSetAlarmAmPmAmbigious()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set an alarm for 6:00 every day", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_ALARM, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddTimexSlot(Constants.SLOT_TIME, "6:00",
                            ExtendedDateTime.Create(
                            TemporalType.Time,
                            new Dictionary<string, string>()
                            {
                                ["hh"] = "6",
                                ["mm"] = "00",
                                ["AMPM"] = "not_specified",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                         .AddTimexSlot(Constants.SLOT_TIME, "every day",
                            ExtendedDateTime.Create(
                            TemporalType.Set,
                            new Dictionary<string, string>()
                            {
                                ["QUANT"] = "EVERY",
                                ["FREQ"] = "1day",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("6:00 in the morning or the night?", response.ResponseText);
            Assert.IsTrue(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "PM", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_ENTER_MERIDIAN, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MERIDIAN, Constants.CANONICAL_MERIDIAN_PM, "pm")
                        .Build()
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Okay, your alarm is set for 6:00 PM every day.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            List<Alarm> alarms = _fakeFitbitService.GetAlarms("123");
            Assert.AreEqual(1, alarms.Count);
            Alarm setAlarm = alarms[0];
            Assert.AreEqual(true, setAlarm.Enabled);
            Assert.AreEqual(true, setAlarm.Recurring);
            Assert.AreEqual("18:00-08:00", setAlarm.Time);
            Assert.AreEqual(7, setAlarm.WeekDays.Count);
        }

        [Ignore]
        [TestMethod]
        public async Task TestFitbitSetAlarmMultiDevice()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Charge",
                Id = "567",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set an alarm for 8:00 AM", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_ALARM, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddTimexSlot(Constants.SLOT_TIME, "8:00",
                            ExtendedDateTime.Create(
                            TemporalType.Time,
                            new Dictionary<string, string>()
                            {
                                ["hh"] = "8",
                                ["mm"] = "00",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Which device, the Flex 2 or the Charge?", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "the flex", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_ENTER_DEVICE, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddBasicSlot(Constants.SLOT_DEVICE, "flex")
                        .Build()
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Alarm is set for 8", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitSetAlarmWeekdaysOnly()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set an alarm for 8:00 AM each weekday morning", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_ALARM, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddTimexSlot(Constants.SLOT_TIME, "6:00",
                            ExtendedDateTime.Create(
                            TemporalType.Set,
                            new Dictionary<string, string>()
                            {
                                ["hh"] = "8",
                                ["mm"] = "00",
                                ["QUANT"] = "EVERY",
                                ["DURATION"] = "1",
                                ["DURATION_UNIT"] = "weekdays",
                                ["POD"] = "MO",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Okay, your alarm is set for 8:00 AM every weekday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            List<Alarm> alarms = _fakeFitbitService.GetAlarms("123");
            Assert.AreEqual(1, alarms.Count);
            Alarm setAlarm = alarms[0];
            Assert.AreEqual(true, setAlarm.Enabled);
            Assert.AreEqual(true, setAlarm.Recurring);
            Assert.AreEqual("08:00-08:00", setAlarm.Time);
            Assert.AreEqual(5, setAlarm.WeekDays.Count);
        }

        [TestMethod]
        public async Task TestFitbitSetAlarmNonRecurring()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set an alarm for 8:00 AM on Wednesday", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_ALARM, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddTimexSlot(Constants.SLOT_TIME, "8:00 AM",
                            ExtendedDateTime.Create(
                            TemporalType.Time,
                            new Dictionary<string, string>()
                            {
                                ["hh"] = "8",
                                ["mm"] = "00",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                        .AddTimexSlot(Constants.SLOT_DATE, "Wednesday",
                            ExtendedDateTime.Create(
                            TemporalType.Time,
                            new Dictionary<string, string>()
                            {
                                ["D"] = "3",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Okay, your alarm is set for 8:00 AM on Wednesday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            List<Alarm> alarms = _fakeFitbitService.GetAlarms("123");
            Assert.AreEqual(1, alarms.Count);
            Alarm setAlarm = alarms[0];
            Assert.AreEqual(true, setAlarm.Enabled);
            Assert.AreEqual(false, setAlarm.Recurring);
            Assert.AreEqual("08:00-08:00", setAlarm.Time);
            Assert.AreEqual(1, setAlarm.WeekDays.Count);
            Assert.AreEqual("WEDNESDAY", setAlarm.WeekDays[0]);
        }

        [TestMethod]
        public async Task TestFitbitSetAlarmMultiturn()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Charge",
                Id = "567",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "set an alarm", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_SET_ALARM, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            //Assert.AreEqual("Which device, the Flex 2 or the Charge?", response.TextToDisplay);
            //Assert.IsFalse(response.ContinueImmediately);

            //request =
            //    new DialogRequestBuilder<DialogRequest>((x) => x, "the flex", InputMethod.Typed)
            //        .AddRecoResult(Constants.FITBIT_DOMAIN, "enter_device", 0.95f)
            //        .AddTagHypothesis(0.9f)
            //            .AddBasicSlot("device", "flex")
            //            .Build()
            //        .Build()
            //    .Build();

            //response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            //Assert.IsNotNull(response);
            Assert.AreEqual("What time did you want to set the alarm for?", response.ResponseText);
            Assert.IsTrue(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "every tuesday", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_ENTER_TIME, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddTimexSlot(Constants.SLOT_TIME, "every tuesday",
                            ExtendedDateTime.Create(
                            TemporalType.Time,
                            new Dictionary<string, string>()
                            {
                                ["QUANT"] = "EVERY",
                                ["D"] = "2",
                                ["FREQ"] = "1week",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                        .Build()
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("What time of day?", response.ResponseText);
            Assert.IsTrue(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "8", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_ENTER_TIME, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddTimexSlot(Constants.SLOT_TIME, "8",
                            ExtendedDateTime.Create(
                            TemporalType.Time,
                            new Dictionary<string, string>()
                            {
                                ["hh"] = "8",
                                ["AMPM"] = "not_specified",
                            },
                            new TimexContext()
                            {
                                ReferenceDateTime = _timeProvider.Time.UtcDateTime,
                                UseInference = false
                            }))
                        .Build()
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("8:00 in the morning or the night?", response.ResponseText);
            Assert.IsTrue(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "AM", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_ENTER_MERIDIAN, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_MERIDIAN, Constants.CANONICAL_MERIDIAN_AM, "am")
                        .Build()
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Okay, your alarm is set for 8:00 AM every Tuesday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);

            List<Alarm> alarms = _fakeFitbitService.GetAlarms("123");
            Assert.AreEqual(1, alarms.Count);
            Alarm setAlarm = alarms[0];
            Assert.AreEqual(true, setAlarm.Enabled);
            Assert.AreEqual(true, setAlarm.Recurring);
            Assert.AreEqual("08:00-08:00", setAlarm.Time);
            Assert.AreEqual(1, setAlarm.WeekDays.Count);
            Assert.AreEqual("TUESDAY", setAlarm.WeekDays[0]);
        }

        [TestMethod]
        public async Task TestFitbitShowAlarmsNoDevices()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "find my alarms", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_FIND_ALARM, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You don't seem to have any Fitbit devices.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowAlarmsNoAlarms()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "find my alarms", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_FIND_ALARM, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You don't have any alarms set.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowAlarmsSingle()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 3,
                Enabled = true,
                Recurring = false,
                Time = "07:00+05:00",
                WeekDays = new List<string>() { "SUNDAY" }
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "find my alarms", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_FIND_ALARM, 0.95f)
                    .Build()
                .Build();
            
            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You currently have an alarm set for 7:00 AM on Sunday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowAlarmsSingleRecurring()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 3,
                Enabled = true,
                Recurring = true,
                Time = "08:15-08:00",
                WeekDays = new List<string>() { "WEDNESDAY" }
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "find my alarms", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_FIND_ALARM, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You currently have an alarm set for 8:15 AM every Wednesday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowAlarmsSingleRecurringWeekends()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 3,
                Enabled = true,
                Recurring = true,
                Time = "08:15-08:00",
                WeekDays = new List<string>() { "SATURDAY", "SUNDAY" }
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "find my alarms", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_FIND_ALARM, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You currently have an alarm set for 8:15 AM every weekend.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowAlarmsSingleRecurringWeekdays()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 3,
                Enabled = true,
                Recurring = true,
                Time = "18:00-04:00",
                WeekDays = new List<string>() { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY" }
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "find my alarms", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_FIND_ALARM, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You currently have an alarm set for 6:00 PM every weekday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowAlarmsMultiple()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 3,
                Enabled = true,
                Recurring = false,
                Time = "07:00+05:00",
                WeekDays = new List<string>() { "TUESDAY" }
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 3,
                Enabled = true,
                Recurring = false,
                Time = "08:00+05:00",
                WeekDays = new List<string>() { "THURSDAY" }
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "find my alarms", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_FIND_ALARM, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You currently have an alarm set for 7:00 AM on Tuesday and 8:00 AM on Thursday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitShowAlarmsAWholeLot()
        {
            _fakeFitbitService.AddDevice(new FitbitDevice()
            {
                Battery = BatteryLevel.Low,
                DeviceVersion = "Flex 2",
                Id = "123",
                LastSyncTime = new DateTime(2012, 3, 15, 07, 12, 33),
                Type = "TRACKER"
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 3,
                Enabled = true,
                Recurring = false,
                Time = "07:00+05:00",
                WeekDays = new List<string>() { "MONDAY" }
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 4,
                Enabled = true,
                Recurring = true,
                Time = "08:00+05:00",
                WeekDays = new List<string>() { "TUESDAY" }
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 5,
                Enabled = false,
                Recurring = true,
                Time = "09:00+05:00",
                WeekDays = new List<string>() { "WEDNESDAY" }
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 6,
                Enabled = true,
                Recurring = true,
                Time = "10:00+05:00",
                WeekDays = new List<string>() { "THURSDAY" }
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 7,
                Enabled = true,
                Recurring = true,
                Time = "11:00+05:00",
                WeekDays = new List<string>() { "FRIDAY" }
            });

            _fakeFitbitService.AddAlarm("123", new Alarm()
            {
                AlarmId = 8,
                Enabled = true,
                Recurring = false,
                Time = "12:00+05:00",
                WeekDays = new List<string>() { "SATURDAY" }
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "find my alarms", InputMethod.Typed)
                    .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_FIND_ALARM, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You currently have an alarm set for 7:00 AM on Monday, 8:00 AM every Tuesday, 10:00 AM every Thursday, and 11:00 AM every Friday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitSideSpeechHandler()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "you can visit griffin space jam dot com", InputMethod.Typed)
                .AddRecoResult(DialogConstants.COMMON_DOMAIN, DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, 0.95f)
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.IsTrue(response.ResponseText.Contains("Welcome to Fitbit. I can tell you about your steps, calories, distance, and more."));
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitWaterLogged()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how much water did I drink", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_WATER, "water")
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "drink")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You drank 0.7 liters of water today.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitWaterLoggedYesterday()
        {
            _fakeFitbitService.AddWaterLogSummary(new WaterLogGetResponse()
            {
                Summary = new WaterSummary()
                {
                    Water = 1232
                }
            }, "2012-03-14");

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how much water did I drink", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_GET_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_WATER, "water")
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "drink")
                        .AddTimexSlot(Constants.SLOT_DATE, "yesterday", Yesterday)
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You drank 1.2 liters of water yesterday.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitLogWater()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "log that I drank 8 ounces of water", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_LOG_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "drank")
                        .AddNumericSlot(Constants.SLOT_QUANTITY, "8", 8)
                        .AddCanonicalizedSlot(Constants.SLOT_UNIT, UnitName.AMBIG_ENG_OUNCE, "ounces")
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_WATER, "water")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("I recorded that you drank 0.2 liters of water today. That brings your total up to 0.9 liters.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitLogWaterGlasses()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "log that I drank a glass of water", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_LOG_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "drank")
                        .AddBasicSlot(Constants.SLOT_UNIT, "glass")
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_WATER, "water")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("I recorded that you drank 0.2 liters of water today. That brings your total up to 0.9 liters.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitLogWaterBottles()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "log that I drank a bottle of water", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_LOG_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "drank")
                        .AddBasicSlot(Constants.SLOT_UNIT, "bottle")
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_WATER, "water")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("I recorded that you drank 0.5 liters of water today. That brings your total up to 1.2 liters.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitLogWaterNoQuantity()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "log that I drank a quart of water", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_LOG_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "drank")
                        .AddCanonicalizedSlot(Constants.SLOT_UNIT, UnitName.AMBIG_ENG_QUART, "quart")
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_WATER, "water")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("I recorded that you drank 0.9 liters of water today. That brings your total up to 1.6 liters.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitLogWaterNoUnit()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "log that I drank 8 waters", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_LOG_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "drank")
                        .AddNumericSlot(Constants.SLOT_QUANTITY, "8", 8)
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_WATER, "waters")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("You'll have to rephrase that.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitLogWaterUnknownUnit()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "log that I drank 2 mouthfuls of water", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_LOG_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "drank")
                        .AddNumericSlot(Constants.SLOT_QUANTITY, "8", 8)
                        .AddBasicSlot(Constants.SLOT_UNIT, "mouthfuls")
                        .AddCanonicalizedSlot(Constants.SLOT_STAT_TYPE, Constants.CANONICAL_STAT_WATER, "waters")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("Can you rephrase that in units I can understand?", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitLogFood()
        {
            _fakeFitbitService.AddFood(new Food()
            {
                AccessLevel = "PUBLIC",
                Brand = string.Empty,
                Calories = 311,
                DefaultServingSize = 1,
                DefaultUnit = new ServingUnit()
                {
                    Id = 91,
                    Name = "cup",
                    Plural = "cups"
                },
                FoodId = 20616,
                Locale = "en_US",
                Name = "Oatmeal, Dry",
                Units = new List<ulong>() { 91, 147 }
            });
            
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "log that I ate oatmeal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_LOG_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "ate")
                        .AddBasicSlot(Constants.SLOT_FOOD, "Oatmeal")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("OK, I will log that you ate Oatmeal. Is this OK?", response.ResponseText);
            Assert.IsTrue(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "yes", InputMethod.Typed)
                    .AddRecoResult(DialogConstants.COMMON_DOMAIN, "confirm", 0.95f)
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("OK, I logged that you ate Oatmeal", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestFitbitLogFoodCancel()
        {
            _fakeFitbitService.AddFood(new Food()
            {
                AccessLevel = "PUBLIC",
                Brand = string.Empty,
                Calories = 311,
                DefaultServingSize = 1,
                DefaultUnit = new ServingUnit()
                {
                    Id = 91,
                    Name = "cup",
                    Plural = "cups"
                },
                FoodId = 20616,
                Locale = "en_US",
                Name = "Oatmeal, Dry",
                Units = new List<ulong>() { 91, 147 }
            });

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "log that I ate oatmeal", InputMethod.Typed)
                .AddRecoResult(Constants.FITBIT_DOMAIN, Constants.INTENT_LOG_ACTIVITY, 0.95f)
                    .AddTagHypothesis(0.9f)
                        .AddCanonicalizedSlot(Constants.SLOT_ACTIVITY_TYPE, Constants.CANONICAL_ACTIVITY_LOG, "ate")
                        .AddBasicSlot(Constants.SLOT_FOOD, "Oatmeal")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("OK, I will log that you ate Oatmeal. Is this OK?", response.ResponseText);
            Assert.IsTrue(response.ContinueImmediately);

            request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "no", InputMethod.Typed)
                    .AddRecoResult(DialogConstants.COMMON_DOMAIN, "deny", 0.95f)
                    .Build()
                .Build();

            response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual("OK, I'll forget that then", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        #endregion

        // CORE SCENARIOS

        // EXTRA SCENARIOS
        // TestSetNewGoal (inc. weight goal)
        // TestStepsTakenLastWeek (5 stats averaged over a large period)
        // TestWeightDelta (see how much weight has changed over time, like last week or month - how far back does data go?)
        // TestSleepQuality
        // TestHeartRateTimeSeries
        // how much did I weigh 3 months ago
        // when was the last time I synced
        // leaderboard for stats other than steps
        // TestShowHelpOnMultiturn (confusion count)
        // Multidevice set alarm
        // what time is my morning alarm
        // Go back and change parameters on an alarm before submit
        // Unit selection on log food (I ate 3 cups of oatmeal)
        // Make sure fitbit's cool catchphrases are output.
    }
}
