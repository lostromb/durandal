using Durandal.Plugins.Fitbit.Html;
using Durandal.Plugins.Fitbit.Schemas;
using Durandal.Plugins.Fitbit.Schemas.Responses;
using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Utils;
using Durandal.Common.Logger;
using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Calendar;
using Durandal.Common.Time.Timex.Client;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.UnitConversion;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.NLP;
using Durandal.Common.Statistics;

namespace Durandal.Plugins.Fitbit
{
    public static class Scenarios
    {
        public static async Task<FitbitUser> GetUserProfile(
            FitbitService service,
            QueryWithContext context,
            IPluginServices pluginServices,
            string authToken)
        {
            FitbitUser returnVal = null;

            // Look in user storage first
            // FIXME this can get stale
            if (pluginServices.LocalUserProfile.TryGetObject<FitbitUser>(Constants.SESSION_USER_PROFILE, out returnVal))
            {
                pluginServices.Logger.Log("Got cached user profile");
                return returnVal;
            }

            // Query Fitbit and store profile if not found
            returnVal = await service.GetUserProfile(authToken, pluginServices.Logger);

            if (returnVal == null)
            {
                return null;
            }

            pluginServices.LocalUserProfile.Put(Constants.SESSION_USER_PROFILE, returnVal);

            pluginServices.Logger.Log("Got live user profile");

            return returnVal;
        }

        public static async Task<PluginResult> ShowStepsSummary(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            if (timeInfo.DaysOffset.TotalDays > 0)
            {
                // Query is in the future.
                return await pluginServices.LanguageGenerator.GetPattern("QueryInFuture", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            DailyActivityResponse activities = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (activities == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return daily activity results"
                };
            }

            // Compile statistics
            int steps = activities.Summary.Steps;
            int stepsGoal = activities.Goals.Steps.GetValueOrDefault(Constants.DEFAULT_STEPS_GOAL);

            // Render result
            StepsCard card = new StepsCard();
            card.StepsTaken = steps;
            card.StepsToGoal = stepsGoal - steps;
            card.Percent = (int)(100f * (float)steps / (float)stepsGoal);
            card.DateString = timeInfo.QueryTime.ToString();

            ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("StepsTaken", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("steps", steps)
                .Sub("date_offset", timeInfo.DaysOffset);

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                ResponseHtml = card.Render(),
                MultiTurnResult = MultiTurnBehavior.None
            };

            returnVal = await pattern.ApplyToDialogResult(returnVal);
            return returnVal;
        }

        public static async Task<PluginResult> ShowCaloriesBurnedSummary(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            if (timeInfo.DaysOffset.TotalDays > 0)
            {
                // Query is in the future.
                return await pluginServices.LanguageGenerator.GetPattern("QueryInFuture", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            DailyActivityResponse activities = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (activities == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return daily activity results"
                };
            }

            // Compile statistics
            float caloriesBurned = activities.Summary.CaloriesOut;
            float caloriesGoal = activities.Goals.CaloriesOut.GetValueOrDefault(Constants.DEFAULT_CALORIES_OUT_GOAL);

            // Render result
            StepsCard card = new StepsCard();
            card.StepsTaken = 0;
            card.StepsToGoal = 10000;
            card.Percent = 0;
            card.DateString = timeInfo.QueryTime.ToString();

            ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("CaloriesBurned", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("calories", caloriesBurned)
                .Sub("date_offset", timeInfo.DaysOffset);

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                ResponseHtml = card.Render(),
                MultiTurnResult = MultiTurnBehavior.None
            };

            returnVal = await pattern.ApplyToDialogResult(returnVal);
            return returnVal;
        }

        public static async Task<PluginResult> ShowCaloriesLoggedSummary(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            if (timeInfo.DaysOffset.TotalDays > 0)
            {
                // Query is in the future.
                return await pluginServices.LanguageGenerator.GetPattern("QueryInFuture", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            FoodLogGetResponse foodLog = await service.GetFoodLogs(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (foodLog == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return food log results"
                };
            }
            
            int caloriesLogged = foodLog.Summary.Calories;

            ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("CaloriesLogged", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("calories", caloriesLogged)
                .Sub("date_offset", timeInfo.DaysOffset);

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.None
            };

            returnVal = await pattern.ApplyToDialogResult(returnVal);
            return returnVal;
        }

        public static async Task<PluginResult> ShowFloorsSummary(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            if (timeInfo.DaysOffset.TotalDays > 0)
            {
                // Query is in the future.
                return await pluginServices.LanguageGenerator.GetPattern("QueryInFuture", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            DailyActivityResponse activities = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (activities == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return daily activity results"
                };
            }

            // Compile statistics
            int floorsClimbed = activities.Summary.Floors;
            int floorsGoal = activities.Goals.Floors.GetValueOrDefault(Constants.DEFAULT_FLOORS_GOAL);

            // Render result
            StepsCard card = new StepsCard();
            card.StepsTaken = 0;
            card.StepsToGoal = 10000;
            card.Percent = 0;
            card.DateString = timeInfo.QueryTime.ToString();

            ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("StairsClimbed", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("floors", floorsClimbed)
                .Sub("date_offset", timeInfo.DaysOffset);

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                ResponseHtml = card.Render(),
                MultiTurnResult = MultiTurnBehavior.None
            };

            returnVal = await pattern.ApplyToDialogResult(returnVal);
            return returnVal;
        }

        public static async Task<PluginResult> ShowActiveMinutesSummary(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            if (timeInfo.DaysOffset.TotalDays > 0)
            {
                // Query is in the future.
                return await pluginServices.LanguageGenerator.GetPattern("QueryInFuture", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            DailyActivityResponse activities = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (activities == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return daily activity results"
                };
            }

            // Compile statistics
            float activeMinutes = activities.Summary.VeryActiveMinutes;

            // Render result
            StepsCard card = new StepsCard();
            card.StepsTaken = 0;
            card.StepsToGoal = 10000;
            card.Percent = 0;
            card.DateString = timeInfo.QueryTime.ToString();

            ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("ActiveMinutes", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("active_minutes", (int)activeMinutes)
                .Sub("date_offset", timeInfo.DaysOffset);

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                ResponseHtml = card.Render(),
                MultiTurnResult = MultiTurnBehavior.None
            };

            returnVal = await pattern.ApplyToDialogResult(returnVal);
            return returnVal;
        }

        public static async Task<PluginResult> ShowDistanceSummary(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string statSlotValue,
            string authToken)
        {
            if (timeInfo.DaysOffset.TotalDays > 0)
            {
                // Query is in the future.
                return await pluginServices.LanguageGenerator.GetPattern("QueryInFuture", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            DailyActivityResponse activities = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (activities == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return daily activity results"
                };
            }

            // Compile statistics
            double distance = 0;
            foreach (var distanceActivity in activities.Summary.Distances)
            {
                if (distanceActivity.Activity.Equals("total", StringComparison.OrdinalIgnoreCase))
                {
                    distance = distanceActivity.Distance;
                    break;
                }
            }

            double distanceGoal = activities.Goals.Distance.GetValueOrDefault((float)Constants.DEFAULT_DISTANCE_GOAL_KM);

            string distanceUnitName = "KILOMETERS";

            // Convert distance to miles if needed (if user specified or else default to their locale preferences)
            UnitSystem distanceUnitSystem = Helpers.GetUnitSystemForLocale(userProfile.DistanceUnit);
            if (string.Equals(statSlotValue, Constants.CANONICAL_STAT_MILES) || (string.Equals(statSlotValue, Constants.CANONICAL_STAT_DISTANCE) && distanceUnitSystem == UnitSystem.USImperial))
            {
                distance = Helpers.ConvertKilometersToMiles(distance, pluginServices.Logger);
                distanceGoal = Helpers.ConvertKilometersToMiles(distanceGoal, pluginServices.Logger);
                distanceUnitName = "MILES";
            }

            // Render result
            StepsCard card = new StepsCard();
            card.StepsTaken = 0;
            card.StepsToGoal = 10000;
            card.Percent = 0;
            card.DateString = timeInfo.QueryTime.ToString();

            ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("DistanceTraveled", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("act_type", "walked")
                .Sub("distance", distance)
                .Sub("distance_unit", distanceUnitName)
                .Sub("date_offset", timeInfo.DaysOffset);

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                ResponseHtml = card.Render(),
                MultiTurnResult = MultiTurnBehavior.None
            };

            returnVal = await pattern.ApplyToDialogResult(returnVal);
            return returnVal;
        }

        public static async Task<PluginResult> ShowOverallSummary(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            if (timeInfo.DaysOffset.TotalDays > 0)
            {
                // Query is in the future.
                return await pluginServices.LanguageGenerator.GetPattern("QueryInFuture", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            DailyActivityResponse activities = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (activities == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return daily activity results"
                };
            }

            // Compile statistics
            int stepsTaken = activities.Summary.Steps;
            int floorsClimbed = activities.Summary.Floors;
            int caloriesBurned = activities.Summary.CaloriesOut;
            int activeMinutes = activities.Summary.VeryActiveMinutes;
            double distanceTraveled = 0;
            foreach (var distanceActivity in activities.Summary.Distances)
            {
                if (distanceActivity.Activity.Equals("total", StringComparison.OrdinalIgnoreCase))
                {
                    distanceTraveled = distanceActivity.Distance;
                    break;
                }
            }

            double distanceGoal = activities.Goals.Distance.GetValueOrDefault((float)Constants.DEFAULT_DISTANCE_GOAL_KM);
            
            string distanceUnit = "KILOMETERS";

            // Convert distance to miles if specified in user's locale preferences
            UnitSystem distanceUnitSystem = Helpers.GetUnitSystemForLocale(userProfile.DistanceUnit);
            if (distanceUnitSystem == UnitSystem.USImperial)
            {
                distanceTraveled = Helpers.ConvertKilometersToMiles(distanceTraveled, pluginServices.Logger);
                distanceGoal = Helpers.ConvertKilometersToMiles(distanceGoal, pluginServices.Logger);
                distanceUnit = "MILES";
            }

            // Render result
            StepsCard card = new StepsCard();
            card.StepsTaken = 0;
            card.StepsToGoal = 10000;
            card.Percent = 0;
            card.DateString = timeInfo.QueryTime.ToString();

            ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("SummaryReadout", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("date_offset", timeInfo.DaysOffset)
                .Sub("distance_unit", distanceUnit)
                .Sub("steps", stepsTaken)
                .Sub("distance", distanceTraveled)
                .Sub("calories", caloriesBurned)
                .Sub("active_minutes", activeMinutes)
                .Sub("floors", floorsClimbed);

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                ResponseHtml = card.Render(),
                MultiTurnResult = MultiTurnBehavior.None,
                ResponseData = new Dictionary<string, string>()
            };

            returnVal = await pattern.ApplyToDialogResult(returnVal);

            return returnVal;
        }

        public static async Task<PluginResult> ShowMostRecentWeight(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            IList<WeightLog> weightLogs = await service.GetWeightLogs(authToken, pluginServices.Logger, timeInfo.UserLocalTime, PeriodEnum.OneMonth, userProfile);

            if (weightLogs == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return weight results"
                };
            }

            if (weightLogs.Count == 0)
            {
                return await pluginServices.LanguageGenerator.GetPattern("NoWeightLogged", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            WeightLog mostRecentLog = null;
            foreach (WeightLog log in weightLogs)
            {
                if (mostRecentLog == null || log.DateTime > mostRecentLog.DateTime)
                {
                    mostRecentLog = log;
                }
            }

            double weight = mostRecentLog.Weight;
            string lgPatternName = "CurrentWeightKilograms";

            // Convert kilograms if needed
            UnitSystem weightUnitSystem = Helpers.GetWeightUnitSystemForLocale(userProfile.WeightUnit);
            if (weightUnitSystem == UnitSystem.USImperial)
            {
                weight = Helpers.ConvertKilogramsToPounds(weight, pluginServices.Logger);
                lgPatternName = "CurrentWeightPounds";
            }
            else if (weightUnitSystem == UnitSystem.BritishImperial)
            {
                weight = Helpers.ConvertKilogramsToStone(weight, pluginServices.Logger);
                lgPatternName = "CurrentWeightStone";
            }

            TimeSpan dateOffset = mostRecentLog.DateTime.Date - timeInfo.UserLocalTime.Date;
            if (dateOffset.Ticks > 0)
            {
                // Should never happen but just in case
                dateOffset = TimeSpan.Zero;

            }
            ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern(lgPatternName, queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("date_offset", dateOffset)
                .Sub("weight", weight);

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.None
            };

            returnVal = await pattern.ApplyToDialogResult(returnVal);
            return returnVal;
        }

        public static async Task<PluginResult> ShowMostRecentBMI(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            IList<WeightLog> weightLogs = await service.GetWeightLogs(authToken, pluginServices.Logger, timeInfo.UserLocalTime, PeriodEnum.OneMonth, userProfile);

            if (weightLogs == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return weight results"
                };
            }

            if (weightLogs.Count == 0)
            {
                return await pluginServices.LanguageGenerator.GetPattern("NoWeightLogged", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            WeightLog mostRecentLog = null;
            foreach (WeightLog log in weightLogs)
            {
                if (mostRecentLog == null || log.DateTime > mostRecentLog.DateTime)
                {
                    mostRecentLog = log;
                }
            }

            double bmi = mostRecentLog.BMI;

            TimeSpan dateOffset = mostRecentLog.DateTime.Date - timeInfo.UserLocalTime.Date;
            if (dateOffset.Ticks > 0)
            {
                // Should never happen but just in case
                dateOffset = TimeSpan.Zero;
            }

            ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("CurrentBMI", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("date_offset", dateOffset)
                .Sub("bmi", bmi);

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.None
            };

            returnVal = await pattern.ApplyToDialogResult(returnVal);
            return returnVal;
        }

        public static async Task<PluginResult> ShowCurrentHeight(
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            string authToken)
        {
            double heightCentimeters = userProfile.Height;
            await DurandalTaskExtensions.NoOpTask;

            // Split based on whether we read out meters or feet
            UnitSystem heightUnitSystem = Helpers.GetUnitSystemForLocale(userProfile.HeightUnit);
            if (heightUnitSystem == UnitSystem.USImperial)
            {
                double heightFeet = Helpers.ConvertCentimetersToFeet(heightCentimeters, pluginServices.Logger);
                int feetComponent = (int)Math.Floor(heightFeet);
                int inchesComponent = (int)Math.Round((heightFeet - Math.Floor(heightFeet)) * 12);

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("CurrentHeightFeet", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("feet", feetComponent.ToString())
                    .Sub("inches", inchesComponent.ToString());

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
            else
            {
                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("CurrentHeightCentimeters", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("centimeters", heightCentimeters);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
        }

        public static Task<PluginResult> ShowCurrentAge(
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile)
        {
            ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("CurrentAge", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("age", userProfile.Age);

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.None
            };

            return pattern.ApplyToDialogResult(returnVal);
        }

        public static async Task<PluginResult> ShowBatteryLevel(
            FitbitService service, 
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            List<FitbitDevice> devices = await service.GetUserDevices(authToken, pluginServices.Logger, userProfile);

            if (devices.Count == 0)
            {
                return await pluginServices.LanguageGenerator.GetPattern("NoFitbitDevices", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }
            else if (devices.Count == 1)
            {
                FitbitDevice device = devices[0];

                // Calculate how long it's been since the device synced
                DateTimeOffset currentTime = timeInfo.UserLocalTime;
                DateTimeOffset lastSyncTime = currentTime;
                if (device.LastSyncTime.HasValue)
                {
                    lastSyncTime = new DateTimeOffset(device.LastSyncTime.Value, TimeSpan.FromMilliseconds(userProfile.OffsetFromUTCMillis));
                }

                TimeSpan timeSinceLastSync = currentTime - lastSyncTime;

                string deviceName = device.DeviceVersion;
                string batteryLevel = device.Battery.ToString().ToUpperInvariant();

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("BatteryLevelSingleDevice", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("device", deviceName)
                        .Sub("level", batteryLevel)
                        .Sub("time_since_last_sync", timeSinceLastSync);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
            else
            {
                // Sort devices in order battery level, lowest first, showing a maximum of 3 total devices
                List<FitbitDevice> sortedDevices = new List<FitbitDevice>(devices);
                sortedDevices.Sort((a, b) => (int)b.Battery - (int)a.Battery);

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("BatteryLevelMultiDevice", queryWithContext.ClientContext, pluginServices.Logger);
                for (int c = 0; c < 3; c++)
                {
                    string deviceName = string.Empty;
                    string batteryLevel = string.Empty;

                    if (c < sortedDevices.Count)
                    {
                        deviceName = sortedDevices[c].DeviceVersion;
                        batteryLevel = sortedDevices[c].Battery.ToString().ToUpperInvariant();
                    }
                    pattern = pattern.Sub("device." + (c + 1), deviceName)
                                    .Sub("level." + (c + 1), batteryLevel);
                }
                
                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
        }

        public static async Task<PluginResult> ShowStepGoal(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            // Same scenario for today + future goals
            if (timeInfo.DaysOffset.TotalDays >= 0)
            {
                FitnessGoals goals = await service.GetDailyGoals(authToken, pluginServices.Logger, userProfile);

                if (goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                if (!goals.Steps.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("StepsGoalNoGoalSet", queryWithContext.ClientContext, pluginServices.Logger)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("StepsGoal", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("steps", goals.Steps.Value);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
            else
            {
                // If user queries in the past, show historical goals
                DailyActivityResponse activitySummary = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

                if (activitySummary == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch activity summary"
                    };
                }

                FitnessGoals goals = activitySummary.Goals;

                if (goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                if (!goals.Steps.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("StepsGoalNoGoalSetPast", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("date_offset", timeInfo.DaysOffset)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("StepsGoalPast", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("date_offset", timeInfo.DaysOffset)
                    .Sub("steps", goals.Steps.Value);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
        }

        public static async Task<PluginResult> ShowStepGoalProgress(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            // Fetch activity summary which has goals as well
            DailyActivityResponse activitySummary = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (activitySummary == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Failed to fetch activity summary"
                };
            }
            
            if (timeInfo.IsToday)
            {
                // Query time is today. Show current progress and encouragement
                if (!activitySummary.Goals.Steps.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("StepsGoalNoGoalSet", queryWithContext.ClientContext, pluginServices.Logger)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                int stepsTaken = activitySummary.Summary.Steps;
                int stepsGoal = activitySummary.Goals.Steps.Value;
                int stepsRemaining = stepsGoal - stepsTaken;

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("StepsGoalProgress", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("difference", stepsRemaining)
                    .Sub("step_goal", stepsGoal);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
            else
            {
                // Not today. Show historical data
                if (!activitySummary.Goals.Steps.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("StepsGoalNoGoalSetPast", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("date_offset", timeInfo.DaysOffset)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                int stepsTaken = activitySummary.Summary.Steps;
                int stepsGoal = activitySummary.Goals.Steps.Value;
                int stepsRemaining = stepsGoal - stepsTaken;
                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("StepsGoalProgressDate", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("difference", stepsRemaining)
                    .Sub("date_offset", timeInfo.DaysOffset);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
        }

        public static async Task<PluginResult> ShowCalorieGoal(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            // Same scenario for today + future goals
            if (timeInfo.DaysOffset.TotalDays >= 0)
            {
                FitnessGoals goals = await service.GetDailyGoals(authToken, pluginServices.Logger, userProfile);

                if (goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                if (!goals.CaloriesOut.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("CaloriesGoalNoGoalSet", queryWithContext.ClientContext, pluginServices.Logger)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("CaloriesGoal", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("calories", goals.CaloriesOut.Value);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
            else
            {
                // If user queries in the past, show historical goals
                DailyActivityResponse activitySummary = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

                if (activitySummary == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch activity summary"
                    };
                }

                FitnessGoals goals = activitySummary.Goals;

                if (goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                if (!goals.CaloriesOut.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("CaloriesGoalNoGoalSetPast", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("date_offset", timeInfo.DaysOffset)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("CaloriesGoalPast", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("date_offset", timeInfo.DaysOffset)
                    .Sub("calories", goals.CaloriesOut.Value);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
        }

        public static async Task<PluginResult> ShowCalorieGoalProgress(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            // Fetch activity summary which has goals as well
            DailyActivityResponse activitySummary = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (activitySummary == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Failed to fetch activity summary."
                };
            }

            if (timeInfo.IsToday)
            {
                if (!activitySummary.Goals.CaloriesOut.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("CaloriesGoalNoGoalSet", queryWithContext.ClientContext, pluginServices.Logger)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                int caloriesBurned = activitySummary.Summary.CaloriesOut;
                int caloriesGoal = activitySummary.Goals.CaloriesOut.Value;
                int caloriesRemaining = caloriesGoal - caloriesBurned;
                string patternName;
                if (caloriesRemaining >= 1200)
                {
                    patternName = "CaloriesGoalProgressBelowFar";
                }
                else if (caloriesRemaining >= 400)
                {
                    patternName = "CaloriesGoalProgressBelowMedium";
                }
                else if (caloriesRemaining > 0)
                {
                    patternName = "CaloriesGoalProgressBelowNear";
                }
                else
                {
                    patternName = "CaloriesGoalProgressAbove";
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern(patternName, queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("difference", Math.Abs(caloriesRemaining))
                    .Sub("calorie_goal", caloriesGoal);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
            else
            {
                // Not today. Show historical data
                if (!activitySummary.Goals.CaloriesOut.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("CaloriesGoalNoGoalSetPast", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("date_offset", timeInfo.DaysOffset)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                int caloriesBurned = activitySummary.Summary.CaloriesOut;
                int caloriesGoal = activitySummary.Goals.CaloriesOut.Value;
                int caloriesRemaining = caloriesGoal - caloriesBurned;
                string patternName;
                if (caloriesRemaining > 0)
                {
                    patternName = "CaloriesGoalProgressBelowDate";
                }
                else
                {
                    patternName = "CaloriesGoalProgressAboveDate";
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern(patternName, queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("difference", Math.Abs(caloriesRemaining))
                    .Sub("date_offset", timeInfo.DaysOffset);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
        }

        public static async Task<PluginResult> ShowFloorGoal(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            // Same scenario for today + future goals
            if (timeInfo.DaysOffset.TotalDays >= 0)
            {
                FitnessGoals goals = await service.GetDailyGoals(authToken, pluginServices.Logger, userProfile);

                if (goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                if (!goals.Floors.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("FloorsGoalNoGoalSet", queryWithContext.ClientContext, pluginServices.Logger)
                        .ApplyToDialogResult(new PluginResult(Result.Success));
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("FloorsGoal", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("floors", goals.Floors.Value);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
            else
            {
                // If user queries in the past, show historical goals
                DailyActivityResponse activitySummary = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

                if (activitySummary == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch activity summary"
                    };
                }

                FitnessGoals goals = activitySummary.Goals;

                if (goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                if (!goals.Floors.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("FloorsGoalNoGoalSetPast", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("date_offset", timeInfo.DaysOffset)
                        .ApplyToDialogResult(new PluginResult(Result.Success));
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("FloorsGoalPast", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("date_offset", timeInfo.DaysOffset)
                    .Sub("floors", goals.Floors.Value);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
        }

        public static async Task<PluginResult> ShowFloorGoalProgress(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            DailyActivityResponse activitySummary = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (activitySummary == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Failed to fetch activity summary."
                };
            }

            if (timeInfo.IsToday)
            {
                if (!activitySummary.Goals.Floors.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("FloorsGoalNoGoalSet", queryWithContext.ClientContext, pluginServices.Logger)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                int floorsClimbed = activitySummary.Summary.Floors;
                int floorsGoal = activitySummary.Goals.Floors.Value;
                int floorsRemaining = floorsGoal - floorsClimbed;
                string patternName;
                if (floorsRemaining > 15)
                {
                    patternName = "FloorsGoalProgressBelowFar";
                }
                else if (floorsRemaining > 5)
                {
                    patternName = "FloorsGoalProgressBelowMedium";
                }
                else if (floorsRemaining > 0)
                {
                    patternName = "FloorsGoalProgressBelowNear";
                }
                else if (floorsRemaining == 0)
                {
                    patternName = "FloorsGoalProgressExact";
                }
                else
                {
                    patternName = "FloorsGoalProgressAbove";
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern(patternName, queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("difference", Math.Abs(floorsRemaining))
                    .Sub("floor_goal", floorsGoal);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
            else
            {
                // Not today. Show historical data
                if (!activitySummary.Goals.Floors.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("FloorsGoalNoGoalSetPast", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("date_offset", timeInfo.DaysOffset)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                int floorsClimbed = activitySummary.Summary.Floors;
                int floorsGoal = activitySummary.Goals.Floors.Value;
                int floorsRemaining = floorsGoal - floorsClimbed;
                string patternName;
                if (floorsRemaining > 0)
                {
                    patternName = "FloorsGoalProgressBelowDate";
                }
                else
                {
                    patternName = "FloorsGoalProgressAboveDate";
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern(patternName, queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("difference", Math.Abs(floorsRemaining))
                    .Sub("date_offset", timeInfo.DaysOffset);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
        }

        public static async Task<PluginResult> ShowDistanceGoal(
            string goalTypeSlot,
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            if (timeInfo.DaysOffset.TotalDays >= 0)
            {
                FitnessGoals goals = await service.GetDailyGoals(authToken, pluginServices.Logger, userProfile);

                if (goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                if (!goals.Distance.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("DistanceGoalNoGoalSet", queryWithContext.ClientContext, pluginServices.Logger)
                        .ApplyToDialogResult(new PluginResult(Result.Success));
                }

                // Convert goal to miles if needed
                double distanceGoal = goals.Distance.Value;

                string summaryPatternName = "KilometersGoal";

                // Convert distance to miles if specified in user's locale preferences
                UnitSystem distanceUnitSystem = Helpers.GetUnitSystemForLocale(userProfile.DistanceUnit);
                if (string.Equals(goalTypeSlot, Constants.CANONICAL_STAT_MILES) ||
                    (string.Equals(goalTypeSlot, Constants.CANONICAL_STAT_DISTANCE) && distanceUnitSystem == UnitSystem.USImperial))
                {
                    distanceGoal = Helpers.ConvertKilometersToMiles(distanceGoal, pluginServices.Logger);
                    summaryPatternName = "MilesGoal";
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern(summaryPatternName, queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("distance", distanceGoal);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
            else
            {
                // Show historical goal
                DailyActivityResponse activitySummary = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

                if (activitySummary == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch activity summary"
                    };
                }

                FitnessGoals goals = activitySummary.Goals;

                if (goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                if (!goals.Distance.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("DistanceGoalNoGoalSetPast", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("date_offset", timeInfo.DaysOffset)
                        .ApplyToDialogResult(new PluginResult(Result.Success));
                }

                // Convert goal to miles if needed
                double distanceGoal = goals.Distance.Value;

                string summaryPatternName = "KilometersGoalPast";

                // Convert distance to miles if specified in user's locale preferences
                UnitSystem distanceUnitSystem = Helpers.GetUnitSystemForLocale(userProfile.DistanceUnit);
                if (string.Equals(goalTypeSlot, Constants.CANONICAL_STAT_MILES) ||
                    (string.Equals(goalTypeSlot, Constants.CANONICAL_STAT_DISTANCE) && distanceUnitSystem == UnitSystem.USImperial))
                {
                    distanceGoal = Helpers.ConvertKilometersToMiles(distanceGoal, pluginServices.Logger);
                    summaryPatternName = "MilesGoalPast";
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern(summaryPatternName, queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("date_offset", timeInfo.DaysOffset)
                    .Sub("distance", distanceGoal);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
        }

        public static async Task<PluginResult> ShowDistanceGoalProgress(
            string goalTypeSlot,
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            DailyActivityResponse activitySummary = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (activitySummary == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Failed to fetch activity summary"
                };
            }

            if (timeInfo.IsToday)
            {
                if (!activitySummary.Goals.Distance.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("DistanceGoalNoGoalSet", queryWithContext.ClientContext, pluginServices.Logger)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                double distanceTraveled = 0;
                foreach (var distanceActivity in activitySummary.Summary.Distances)
                {
                    if (distanceActivity.Activity.Equals("total", StringComparison.OrdinalIgnoreCase))
                    {
                        distanceTraveled = distanceActivity.Distance;
                        break;
                    }
                }

                double distanceGoal = activitySummary.Goals.Distance.Value;
                double distanceRemaining = distanceGoal - distanceTraveled;

                string unit = "Kilometers";
                string summaryPatternName = "GoalProgressBelowFar";

                if (distanceRemaining < 0)
                {
                    summaryPatternName = "GoalProgressAbove";
                }
                else if (distanceRemaining < 1.5)
                {
                    summaryPatternName = "GoalProgressBelowNear";
                }
                else if (distanceRemaining < 3.0)
                {
                    summaryPatternName = "GoalProgressBelowMedium";
                }
                else
                {
                    summaryPatternName = "GoalProgressBelowFar";
                }

                // Convert distance to miles if specified in user's locale preferences
                UnitSystem distanceUnitSystem = Helpers.GetUnitSystemForLocale(userProfile.DistanceUnit);
                if (string.Equals(goalTypeSlot, Constants.CANONICAL_STAT_MILES) ||
                    (string.Equals(goalTypeSlot, Constants.CANONICAL_STAT_DISTANCE) && distanceUnitSystem == UnitSystem.USImperial))
                {
                    distanceRemaining = Helpers.ConvertKilometersToMiles(distanceRemaining, pluginServices.Logger);
                    unit = "Miles";
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern(unit + summaryPatternName, queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("distance", Math.Abs(distanceRemaining));

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
            else
            {
                // Not today. Show historical data
                if (!activitySummary.Goals.Distance.HasValue)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("DistanceGoalNoGoalSetPast", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("date_offset", timeInfo.DaysOffset)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }

                double distanceTraveled = 0;
                foreach (var distanceActivity in activitySummary.Summary.Distances)
                {
                    if (distanceActivity.Activity.Equals("total", StringComparison.OrdinalIgnoreCase))
                    {
                        distanceTraveled = distanceActivity.Distance;
                        break;
                    }
                }

                double distanceGoal = activitySummary.Goals.Distance.Value;
                double distanceRemaining = distanceGoal - distanceTraveled;
                string unit = "Kilometers";
                string patternName;
                if (distanceRemaining > 0)
                {
                    patternName = "GoalProgressBelowDate";
                }
                else
                {
                    patternName = "GoalProgressAboveDate";
                }

                UnitSystem distanceUnitSystem = Helpers.GetUnitSystemForLocale(userProfile.DistanceUnit);
                if (string.Equals(goalTypeSlot, Constants.CANONICAL_STAT_MILES) ||
                    (string.Equals(goalTypeSlot, Constants.CANONICAL_STAT_DISTANCE) && distanceUnitSystem == UnitSystem.USImperial))
                {
                    distanceRemaining = Helpers.ConvertKilometersToMiles(distanceRemaining, pluginServices.Logger);
                    unit = "Miles";
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern(unit + patternName, queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("distance", Math.Abs(distanceRemaining))
                    .Sub("date_offset", timeInfo.DaysOffset);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
        }

        public static async Task<PluginResult> ShowAllGoals(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            if (timeInfo.IsToday)
            {
                FitnessGoals goals = await service.GetDailyGoals(authToken, pluginServices.Logger, userProfile);

                if (goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                // Convert goal to miles if needed
                double distanceGoal = goals.Distance.GetValueOrDefault((float)Constants.DEFAULT_DISTANCE_GOAL_KM);
                int calorieGoal = goals.CaloriesOut.GetValueOrDefault(Constants.DEFAULT_CALORIES_OUT_GOAL);
                int floorsGoal = goals.Floors.GetValueOrDefault(Constants.DEFAULT_FLOORS_GOAL);
                int stepsGoal = goals.Steps.GetValueOrDefault(Constants.DEFAULT_STEPS_GOAL);

                string distanceUnitName = "KILOMETERS";

                // Convert distance to miles if specified in user's locale preferences
                UnitSystem distanceUnitSystem = Helpers.GetUnitSystemForLocale(userProfile.DistanceUnit);
                if (distanceUnitSystem == UnitSystem.USImperial)
                {
                    distanceGoal = Helpers.ConvertKilometersToMiles(distanceGoal, pluginServices.Logger);
                    distanceUnitName = "MILES";
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("GoalsReadout", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("steps", stepsGoal)
                    .Sub("distance", distanceGoal)
                    .Sub("floors", floorsGoal)
                    .Sub("calories", calorieGoal)
                    .Sub("distance_unit", distanceUnitName);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
            else
            {
                // In the past
                DailyActivityResponse response = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

                if (response == null || response.Goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                FitnessGoals goals = response.Goals;

                // Convert goal to miles if needed
                double distanceGoal = goals.Distance.GetValueOrDefault((float)Constants.DEFAULT_DISTANCE_GOAL_KM);
                int calorieGoal = goals.CaloriesOut.GetValueOrDefault(Constants.DEFAULT_CALORIES_OUT_GOAL);
                int floorsGoal = goals.Floors.GetValueOrDefault(Constants.DEFAULT_FLOORS_GOAL);
                int stepsGoal = goals.Steps.GetValueOrDefault(Constants.DEFAULT_STEPS_GOAL);

                string distanceUnitName = "KILOMETERS";

                // Convert distance to miles if specified in user's locale preferences
                UnitSystem distanceUnitSystem = Helpers.GetUnitSystemForLocale(userProfile.DistanceUnit);
                if (distanceUnitSystem == UnitSystem.USImperial)
                {
                    distanceGoal = Helpers.ConvertKilometersToMiles(distanceGoal, pluginServices.Logger);
                    distanceUnitName = "MILES";
                }

                ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("GoalsReadoutPast", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("date_offset", timeInfo.DaysOffset)
                    .Sub("steps", stepsGoal)
                    .Sub("distance", distanceGoal)
                    .Sub("floors", floorsGoal)
                    .Sub("calories", calorieGoal)
                    .Sub("distance_unit", distanceUnitName);

                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                };

                returnVal = await pattern.ApplyToDialogResult(returnVal);
                return returnVal;
            }
        }

        public static async Task<PluginResult> ShowAllGoalProgress(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            if (timeInfo.IsToday)
            {
                DailyActivityResponse response = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

                if (response == null || response.Goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                FitnessGoals goals = response.Goals;

                if (goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                double distanceGoal = goals.Distance.GetValueOrDefault((float)Constants.DEFAULT_DISTANCE_GOAL_KM);
                int calorieGoal = goals.CaloriesOut.GetValueOrDefault(Constants.DEFAULT_CALORIES_OUT_GOAL);
                int floorsGoal = goals.Floors.GetValueOrDefault(Constants.DEFAULT_FLOORS_GOAL);
                int stepsGoal = goals.Steps.GetValueOrDefault(Constants.DEFAULT_STEPS_GOAL);
                
                double distanceDone = 0;
                foreach (var distanceActivity in response.Summary.Distances)
                {
                    if (distanceActivity.Activity.Equals("total", StringComparison.OrdinalIgnoreCase))
                    {
                        distanceDone = distanceActivity.Distance;
                        break;
                    }
                }

                int caloriesDone = response.Summary.CaloriesOut;
                int floorsDone = response.Summary.Floors;
                int stepsDone = response.Summary.Steps;

                double distanceRemaining = distanceGoal - distanceDone;
                int caloriesRemaining = calorieGoal - caloriesDone;
                int floorsRemaining = floorsGoal - floorsDone;
                int stepsRemaining = stepsGoal - stepsDone;

                bool allGoalsMet =
                    distanceRemaining < 0 &&
                    caloriesRemaining < 0 &&
                    floorsRemaining < 0 &&
                    stepsRemaining < 0;
                
                // Convert distance to miles if specified in user's locale preferences
                UnitSystem distanceUnitSystem = Helpers.GetUnitSystemForLocale(userProfile.DistanceUnit);
                if (distanceUnitSystem == UnitSystem.USImperial)
                {
                    distanceRemaining = Helpers.ConvertKilometersToMiles(distanceRemaining, pluginServices.Logger);
                }

                if (allGoalsMet)
                {
                    // All goals met today
                    return await pluginServices.LanguageGenerator.GetPattern("GoalProgressSummaryAllGoalsMetToday", queryWithContext.ClientContext, pluginServices.Logger)
                        .ApplyToDialogResult(new PluginResult(Result.Success));
                }
                else
                {
                    // One or more goals not met today
                    ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("GoalProgressSummaryToday", queryWithContext.ClientContext, pluginServices.Logger);

                    int subs = 0;
                    if (stepsRemaining > 0)
                    {
                        pattern.Sub("list." + subs++,
                            (await pluginServices.LanguageGenerator.GetPattern("GoalStepsComponent", queryWithContext.ClientContext, pluginServices.Logger)
                            .Sub("steps", stepsRemaining)
                            .Render())
                            .Text);
                    }
                    if (caloriesRemaining > 0)
                    {
                        pattern.Sub("list." + subs++,
                            (await pluginServices.LanguageGenerator.GetPattern("GoalCaloriesComponent", queryWithContext.ClientContext, pluginServices.Logger)
                            .Sub("calories", caloriesRemaining)
                            .Render())
                            .Text);
                    }
                    if (floorsRemaining > 0)
                    {
                        pattern.Sub("list." + subs++,
                            (await pluginServices.LanguageGenerator.GetPattern("GoalFloorsComponent", queryWithContext.ClientContext, pluginServices.Logger)
                            .Sub("floors", floorsRemaining)
                            .Render())
                            .Text);
                    }
                    if (distanceRemaining > 0)
                    {
                        if (distanceUnitSystem == UnitSystem.USImperial)
                        {
                            pattern.Sub("list." + subs++,
                                (await pluginServices.LanguageGenerator.GetPattern("GoalDistanceComponentMiles", queryWithContext.ClientContext, pluginServices.Logger)
                                .Sub("distance", distanceRemaining)
                                .Render())
                                .Text);
                        }
                        else
                        {
                            pattern.Sub("list." + subs++,
                                (await pluginServices.LanguageGenerator.GetPattern("GoalDistanceComponentKilometers", queryWithContext.ClientContext, pluginServices.Logger)
                                .Sub("distance", distanceRemaining)
                                .Render())
                                .Text);
                        }
                    }

                    return await pattern.ApplyToDialogResult(new PluginResult(Result.Success));
                }
            }
            else
            {
                // In the past
                DailyActivityResponse response = await service.GetDailyActivities(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

                if (response == null || response.Goals == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Failed to fetch fitness goals"
                    };
                }

                FitnessGoals goals = response.Goals;

                double distanceGoal = goals.Distance.GetValueOrDefault((float)Constants.DEFAULT_DISTANCE_GOAL_KM);
                int calorieGoal = goals.CaloriesOut.GetValueOrDefault(Constants.DEFAULT_CALORIES_OUT_GOAL);
                int floorsGoal = goals.Floors.GetValueOrDefault(Constants.DEFAULT_FLOORS_GOAL);
                int stepsGoal = goals.Steps.GetValueOrDefault(Constants.DEFAULT_STEPS_GOAL);

                double distanceDone = 0;
                foreach (var distanceActivity in response.Summary.Distances)
                {
                    if (distanceActivity.Activity.Equals("total", StringComparison.OrdinalIgnoreCase))
                    {
                        distanceDone = distanceActivity.Distance;
                        break;
                    }
                }

                int caloriesDone = response.Summary.CaloriesOut;
                int floorsDone = response.Summary.Floors;
                int stepsDone = response.Summary.Steps;

                double distanceRemaining = distanceGoal - distanceDone;
                int caloriesRemaining = calorieGoal - caloriesDone;
                int floorsRemaining = floorsGoal - floorsDone;
                int stepsRemaining = stepsGoal - stepsDone;

                // Convert distance to miles if specified in user's locale preferences
                UnitSystem distanceUnitSystem = Helpers.GetUnitSystemForLocale(userProfile.DistanceUnit);
                if (distanceUnitSystem == UnitSystem.USImperial)
                {
                    distanceRemaining = Helpers.ConvertKilometersToMiles(distanceRemaining, pluginServices.Logger);
                }

                bool allGoalsMet =
                    distanceRemaining < 0 &&
                    caloriesRemaining < 0 &&
                    floorsRemaining < 0 &&
                    stepsRemaining < 0;

                if (allGoalsMet)
                {
                    // All goals met yesterdy
                    return await pluginServices.LanguageGenerator.GetPattern("GoalProgressSummaryAllGoalsMetPast", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("date_offset", timeInfo.DaysOffset)
                        .ApplyToDialogResult(new PluginResult(Result.Success));
                }
                else
                {
                    // Missing one or more goals in the past
                    ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("GoalProgressSummaryPast", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("date_offset", timeInfo.DaysOffset);

                    int subs = 0;
                    if (stepsRemaining > 0)
                    {
                        pattern.Sub("list." + subs++,
                            (await pluginServices.LanguageGenerator.GetPattern("GoalStepsComponent", queryWithContext.ClientContext, pluginServices.Logger)
                            .Sub("steps", stepsRemaining)
                            .Render())
                            .Text);
                    }
                    if (caloriesRemaining > 0)
                    {
                        pattern.Sub("list." + subs++,
                            (await pluginServices.LanguageGenerator.GetPattern("GoalCaloriesComponent", queryWithContext.ClientContext, pluginServices.Logger)
                            .Sub("calories", caloriesRemaining)
                            .Render())
                            .Text);
                    }
                    if (floorsRemaining > 0)
                    {
                        pattern.Sub("list." + subs++,
                            (await pluginServices.LanguageGenerator.GetPattern("GoalFloorsComponent", queryWithContext.ClientContext, pluginServices.Logger)
                            .Sub("floors", floorsRemaining)
                            .Render())
                            .Text);
                    }
                    if (distanceRemaining > 0)
                    {
                        if (distanceUnitSystem == UnitSystem.USImperial)
                        {
                            pattern.Sub("list." + subs++,
                                (await pluginServices.LanguageGenerator.GetPattern("GoalDistanceComponentMiles", queryWithContext.ClientContext, pluginServices.Logger)
                                .Sub("distance", distanceRemaining)
                                .Render())
                                .Text);
                        }
                        else
                        {
                            pattern.Sub("list." + subs++,
                                (await pluginServices.LanguageGenerator.GetPattern("GoalDistanceComponentKilometers", queryWithContext.ClientContext, pluginServices.Logger)
                                .Sub("distance", distanceRemaining)
                                .Render())
                                .Text);
                        }
                    }

                    return await pattern.ApplyToDialogResult(new PluginResult(Result.Success));
                }
            }
        }

        public static async Task<PluginResult> ShowFriendsLeaderboard(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            string authToken)
        {
            FriendsLeaderboardResponse leaderboard = await service.GetFriendsLeaderboard(authToken, pluginServices.Logger, userProfile);

            if (leaderboard == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return leaderboard response"
                };
            }

            FriendLeaderboardEntry myEntry = null;
            // Find out where I am on the ranking
            foreach (FriendLeaderboardEntry leaderboardEntry in leaderboard.Friends)
            {
                if (leaderboardEntry.User.EncodedId == userProfile.EncodedId)
                {
                    myEntry = leaderboardEntry;
                }
            }

            if (myEntry == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Your name was not found on the returned leaderboard"
                };
            }
            
            // Only supporting steps goal for now
            int rank = myEntry.Rank.Steps;
            
            return await pluginServices.LanguageGenerator.GetPattern("LeaderboardStepRank", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("rank", rank)
                .ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                });
        }

        public static async Task<PluginResult> ShowLastExercise(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            IRealTimeProvider timeProvider,
            string authToken)
        {
            // Resolve timex
            DateTimeOffset userTimeUtc = timeProvider.Time;
            DateTimeOffset userTimeLocal = userTimeUtc.ToOffset(TimeSpan.FromMilliseconds(userProfile.OffsetFromUTCMillis));
            
            Pagination pagination = new Pagination()
            {
                BeforeDate = userTimeLocal.ToString("yyyy-MM-dd"),
                Limit = 1,
                Sort = "asc"
            };

            ActivityListResponse activityList = await service.GetActivitiesList(authToken, pluginServices.Logger, userProfile, pagination);

            if (activityList == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return an activity list response"
                };
            }

            if (activityList.Activities == null ||
                activityList.Activities.Count == 0 ||
                !activityList.Activities[0].StartTime.HasValue)
            {
                return await pluginServices.LanguageGenerator.GetPattern("RecentActivityNoData", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        MultiTurnResult = MultiTurnBehavior.None
                    });
            }

            // Get statistics about the activity
            FitnessActivity activity = activityList.Activities[0];

            TimeSpan days_offset = activity.StartTime.Value.Date - userTimeLocal.Date;
            DateTime activityTime = activity.StartTime.Value.DateTime;
            int durationMinutes = (int)Math.Round(TimeSpan.FromMilliseconds(activity.Duration).TotalMinutes);
            int caloriesBurned = activity.Calories;

            return await pluginServices.LanguageGenerator.GetPattern("MostRecentActivitySummary", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("date_offset", days_offset)
                .Sub("time", activityTime)
                .Sub("duration", durationMinutes)
                .Sub("calories", caloriesBurned)
                .ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                });
        }

        public static async Task<PluginResult> ShowExerciseCount(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            IRealTimeProvider timeProvider,
            string authToken)
        {
            // Resolve timex
            DateTimeOffset userTimeUtc = timeProvider.Time;
            SimpleDateTimeRange queryTimeRange = null;
            DateTimeOffset userTimeLocal = userTimeUtc.ToOffset(TimeSpan.FromMilliseconds(userProfile.OffsetFromUTCMillis));

            // Parse the "date" slot to try and determine query time if different from local time
            SlotValue dateSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, Constants.SLOT_DATE);
            if (dateSlot != null)
            {
                TimexContext resolutionContext = new TimexContext()
                {
                    AmPmInferenceCutoff = 7,
                    IncludeCurrentTimeInPastOrFuture = false,
                    Normalization = Normalization.Past,
                    UseInference = true,
                    ReferenceDateTime = userTimeLocal.LocalDateTime
                };

                IList<TimexMatch> timexes = dateSlot.GetTimeMatches(TemporalType.All, resolutionContext);
                if (timexes.Count > 0)
                {
                    ExtendedDateTime edt = timexes[0].ExtendedDateTime;
                    DateAndTime parsedTimex = TimexValue.Parse(edt.FormatValue(), edt.FormatType(), "0", edt.FormatComment(), edt.FormatFrequency(), edt.FormatQuantity(), edt.FormatMod()).AsDateAndTime();
                    queryTimeRange = parsedTimex.InterpretAsNaturalTimeRange(resolutionContext.ReferenceDateTime, Normalization.Past, LocalizedWeekDefinition.StandardWeekDefinition);
                }
            }

            if (queryTimeRange == null)
            {
                // Default to the last 7 days.
                // BUGBUG won't this differ from the default "this week" if that resolves to the nearest calendar week?
                queryTimeRange = new SimpleDateTimeRange()
                {
                    Granularity = TemporalUnit.Week,
                    Start = userTimeLocal.Date.AddDays(-7),
                    End = userTimeLocal.Date
                };
            }

            // This is used to provide the right phrasing in the response
            string timeGranularity;
            if (queryTimeRange.Granularity == TemporalUnit.Day)
            {
                timeGranularity = "DAY";
            }
            else
            {
                timeGranularity = "WEEK";
            }

            // BUGBUG this can mess up statistics if there are more than 20 workouts within the period - need to fetch all pages if present
            Pagination pagination = new Pagination()
            {
                AfterDate = queryTimeRange.Start.ToString("yyyy-MM-dd"),
                Limit = 20,
                Sort = "asc"
            };

            ActivityListResponse activityList = await service.GetActivitiesList(authToken, pluginServices.Logger, userProfile, pagination);

            if (activityList == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return activity list response"
                };
            }

            TimeSpan daysOffset = queryTimeRange.Start.Date - userTimeLocal.Date;
            bool isByGranularity = !("DAY".Equals(timeGranularity) && daysOffset.TotalDays != 0);

            StaticAverage averageDuration = new StaticAverage();
            StaticAverage averageCalories = new StaticAverage();
            int numActivities = 0;

            foreach (FitnessActivity activity in activityList.Activities)
            {
                if (activity.StartTime.HasValue &&
                    activity.StartTime.Value >= queryTimeRange.Start &&
                    activity.StartTime.Value < queryTimeRange.End)
                {
                    numActivities++;
                    averageCalories.Add(activity.Calories);
                    averageDuration.Add(activity.Duration);
                }
            }

            int durationMinutes = (int)Math.Round(TimeSpan.FromMilliseconds(averageDuration.Average).TotalMinutes);
            int caloriesBurned = (int)averageCalories.Average;

            if (numActivities == 0)
            {
                if (isByGranularity)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("RecentActivitySummaryNoDataByGranularity", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("time_phrase", timeGranularity)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }
                else
                {
                    return await pluginServices.LanguageGenerator.GetPattern("RecentActivitySummaryNoDataByOffset", queryWithContext.ClientContext, pluginServices.Logger)
                        .Sub("date_offset", daysOffset)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.None
                        });
                }
            }
            
            if (isByGranularity)
            {
                return await pluginServices.LanguageGenerator.GetPattern("RecentActivitySummaryByGranularity", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("count", numActivities)
                    .Sub("duration", durationMinutes)
                    .Sub("calories", caloriesBurned)
                    .Sub("time_phrase", timeGranularity)
                    .ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        MultiTurnResult = MultiTurnBehavior.None
                    });
            }
            else
            {
                return await pluginServices.LanguageGenerator.GetPattern("RecentActivitySummaryByOffset", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("count", numActivities)
                    .Sub("duration", durationMinutes)
                    .Sub("calories", caloriesBurned)
                    .Sub("date_offset", daysOffset)
                    .ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        MultiTurnResult = MultiTurnBehavior.None
                    });
            }
        }

        public static async Task<PluginResult> ShowWaterLoggedSummary(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            if (timeInfo.DaysOffset.TotalDays > 0)
            {
                // Query is in the future.
                return await pluginServices.LanguageGenerator.GetPattern("QueryInFuture", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            WaterLogGetResponse waterLog = await service.GetWaterLogs(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (waterLog == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return water log results"
                };
            }

            float millilitersLogged = waterLog.Summary.Water;
            float litersLogged = millilitersLogged / 1000f;
            // FIXME the output unit should be localized

            ILGPattern pattern = pluginServices.LanguageGenerator.GetPattern("WaterLogged", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("water", litersLogged)
                .Sub("date_offset", timeInfo.DaysOffset);

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.None
            };

            returnVal = await pattern.ApplyToDialogResult(returnVal);
            return returnVal;
        }

        public static async Task<PluginResult> LogWater(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken)
        {
            // Parse inputs
            LexicalString unit = DialogHelpers.TryGetLexicalSlotValue(queryWithContext.Understanding, Constants.SLOT_UNIT);
            SlotValue quantitySlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, Constants.SLOT_QUANTITY);
            if (unit == null ||
                string.IsNullOrEmpty(unit.WrittenForm))
            {
                // Can't continue if no unit
                return await pluginServices.LanguageGenerator.GetPattern("RephraseThat", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            decimal quantity = 1M;
            // See if there is a quantity, default to 1
            if (quantitySlot != null &&
                quantitySlot.GetNumber().HasValue)
            {
                quantity = quantitySlot.GetNumber().Value;
            }

            // See if the unit is glasses or bottles and reinterpret them in terms of ounces
            List<NamedEntity<string>> specialUnits = new List<NamedEntity<string>>();
            specialUnits.Add(new NamedEntity<string>("GLASS", new LexicalString[] { new LexicalString("glass"), new LexicalString("glasses") }));
            specialUnits.Add(new NamedEntity<string>("BOTTLE", new LexicalString[] { new LexicalString("bottle"), new LexicalString("bottles"), new LexicalString("canteen"), new LexicalString("canteens") }));
            IList<Hypothesis<string>> specialUnitResolution = await pluginServices.EntityResolver.ResolveEntity(unit, specialUnits, queryWithContext.ClientContext.Locale, pluginServices.Logger);
            if (specialUnitResolution.Count > 0 &&
                specialUnitResolution[0].Conf > 0.8f)
            {
                string specialUnit = specialUnitResolution[0].Value;
                if (string.Equals("GLASS", specialUnit))
                {
                    unit = new LexicalString(UnitName.US_FLUID_OUNCE);
                    quantity = 8M;
                }
                else if (string.Equals("BOTTLE", specialUnit))
                {
                    unit = new LexicalString(UnitName.US_FLUID_OUNCE);
                    quantity = 18M;
                }
            }

            // First, see how much water is logged so far (this is to prevent race conditions)
            WaterLogGetResponse waterLog = await service.GetWaterLogs(authToken, pluginServices.Logger, timeInfo.QueryTime, userProfile);

            if (waterLog == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return water log results"
                };
            }

            float millilitersLoggedPrevious = waterLog.Summary.Water;
            float millilitersLoggedNow = -1;

            // Convert user input to milliliters
            List<UnitConversionResult> conversionResults = UnitConverter.Convert(unit.WrittenForm, UnitName.MILLILITER, quantity, pluginServices.Logger, UnitSystem.Metric);
            foreach (UnitConversionResult r in conversionResults)
            {
                if (r.ConversionType == UnitType.Volume)
                {
                    millilitersLoggedNow = (float)r.TargetUnitAmount;
                }
            }

            if (millilitersLoggedNow < 0)
            {
                // Assume the unit name is bad
                return await pluginServices.LanguageGenerator.GetPattern("UnknownUnit", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            // Then post the new log
            await service.LogWater(authToken, pluginServices.Logger, userProfile, millilitersLoggedNow, timeInfo.QueryTime);
            float millilitersLoggedTotal = millilitersLoggedPrevious + millilitersLoggedNow;

            // And return LG for the new amount
            return await pluginServices.LanguageGenerator.GetPattern("ShowWaterLoggedPostLog", queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("quantity", millilitersLoggedNow / 1000f)
                .Sub("date_offset", timeInfo.DaysOffset)
                .Sub("total", millilitersLoggedTotal / 1000f)
                .ApplyToDialogResult(new PluginResult(Result.Success));
        }

        public static async Task<PluginResult> LogFood(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            TimeResolutionInfo timeInfo,
            string authToken,
            string foodSlot)
        {
            // Parse inputs
            if (string.IsNullOrEmpty(foodSlot))
            {
                // Can't continue if no food name
                return await pluginServices.LanguageGenerator.GetPattern("RephraseThat", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            // Run search for foods
            FoodSearchResponse response = await service.SearchFoods(authToken, pluginServices.Logger, foodSlot);

            if (response.Foods.Count == 0)
            {
                return await pluginServices.LanguageGenerator.GetPattern("NoFoodsFound", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            // Cache parameters for the next turn
            LogFoodParameters param = new LogFoodParameters()
            {
                FoodId = response.Foods[0].FoodId,
                UnitId = response.Foods[0].DefaultUnit.Id,
                Amount = response.Foods[0].DefaultServingSize,
                Time = timeInfo.QueryTime,
                FoodName = foodSlot
            };

            pluginServices.SessionStore.Put("foodLogParams", param);

            // Prompt for confirmation
            return new PluginResult(Result.Success)
            {
                ResponseText = "OK, I will log that you ate " + foodSlot + ". Is this OK?",
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        private class LogFoodParameters
        {
            public ulong FoodId;
            public ulong UnitId;
            public float Amount;
            public DateTimeOffset Time;
            public string FoodName;
        }

        public static async Task<PluginResult> LogFoodConfirm(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            string authToken)
        {
            LogFoodParameters param = pluginServices.SessionStore.GetObject<LogFoodParameters>("foodLogParams");
            
            FoodLogPostResponse logResponse = await service.LogFood(authToken, pluginServices.Logger, userProfile, param.FoodId, param.UnitId, param.Amount, param.Time);

            return new PluginResult(Result.Success)
            {
                ResponseText = "OK, I logged that you ate " + param.FoodName
            };
        }

        public static async Task<PluginResult> LogFoodCancel(
            QueryWithContext queryWithContext,
            IPluginServices pluginServices)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success)
            {
                ResponseText = "OK, I'll forget that then"
            };
        }

        public static async Task<PluginResult> ShowAlarms(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser userProfile,
            string authToken)
        {
            // Get all devices
            IList<FitbitDevice> allDevices = await service.GetUserDevices(authToken, pluginServices.Logger, userProfile);
            if (allDevices == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "The Fitbit service did not return any user devices"
                };
            }

            if (allDevices.Count == 0)
            {
                return await pluginServices.LanguageGenerator.GetPattern("ShowAlarmsNoDevices", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            List<Alarm> allAlarms = new List<Alarm>();

            foreach (FitbitDevice device in allDevices)
            {
                AlarmResponse thisDeviceAlarms = await service.GetDeviceAlarms(authToken, pluginServices.Logger, userProfile, device.Id);
                if (thisDeviceAlarms == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Error response recieved while fetching devices for tracker"
                    };
                }

                allAlarms.AddRange(thisDeviceAlarms.TrackerAlarms);
            }

            if (allAlarms.Count == 0)
            {
                return await pluginServices.LanguageGenerator.GetPattern("ShowAlarmsNoAlarms", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            // Now collate all alarms into one list
            ILGPattern overallPattern = pluginServices.LanguageGenerator.GetPattern("ShowAlarmsList", queryWithContext.ClientContext, pluginServices.Logger);

            int count = 0;
            foreach (Alarm alarm in allAlarms)
            {
                if (!alarm.Enabled || alarm.Deleted)
                {
                    continue;
                }
                
                DateTimeOffset alarmTime = DateTimeOffset.ParseExact(alarm.Time, "HH:mmzzz", CultureInfo.InvariantCulture.DateTimeFormat);
                string daysOfWeek = string.Empty;
                if (alarm.WeekDays.Count == 1)
                {
                    daysOfWeek = alarm.WeekDays[0];
                }
                else if (alarm.WeekDays.Count == 2) // Hackish weekend detection
                {
                    daysOfWeek = "WEEKENDS";
                }
                else if (alarm.WeekDays.Count == 5) // Hackish weekday detection
                {
                    daysOfWeek = "WEEKDAYS";
                }

                ILGPattern subphrasePattern;

                if (alarm.Recurring)
                {
                    subphrasePattern = pluginServices.LanguageGenerator.GetPattern("ShowAlarmsSingleRecurring", queryWithContext.ClientContext, pluginServices.Logger);
                }
                else
                {
                    subphrasePattern = pluginServices.LanguageGenerator.GetPattern("ShowAlarmsSingle", queryWithContext.ClientContext, pluginServices.Logger);
                }

                subphrasePattern = subphrasePattern
                    .Sub("time", alarmTime)
                    .Sub("day", daysOfWeek);
                overallPattern = overallPattern.Sub("list." + count, (await subphrasePattern.Render()).Text);
                count++; // FIXME LG only supports a max list of 4 items for now
            }

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.None
            };

            returnVal = await overallPattern.ApplyToDialogResult(returnVal);
            return returnVal;
        }
        
        public static async Task<PluginResult> ShowHelp(
            QueryWithContext queryWithContext,
            IPluginServices pluginServices)
        {
            await DurandalTaskExtensions.NoOpTask;
            return await pluginServices.LanguageGenerator.GetPattern("HelpText", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
        }

        public static async Task<PluginResult> SetAlarm(
            FitbitService service,
            QueryWithContext queryWithContext,
            IPluginServices pluginServices,
            FitbitUser fitbitUserProfile,
            string oauthToken,
            IRealTimeProvider timeProvider)
        {
            const string alarmParamsKey = "ALARM_PARAMS";

            // See if we already have parameters
            AlarmSetParameters parameters = null;

            if (pluginServices.SessionStore.ContainsKey(alarmParamsKey))
            {
                parameters = pluginServices.SessionStore.GetObject<AlarmSetParameters>(alarmParamsKey);
            }

            if (parameters == null)
            {
                parameters = new AlarmSetParameters()
                {
                    AmPmResolved = false,
                    Hour = null,
                    Minute = 0,
                    DayOfWeek = AlarmDayOfWeek.None,
                    TrackerId = null,
                    Recurring = false
                };
            }

            // Make sure they have a tracker
            List<FitbitDevice> allDevices = await service.GetUserDevices(oauthToken, pluginServices.Logger, fitbitUserProfile);
            List<FitbitDevice> trackers = new List<FitbitDevice>();
            if (allDevices != null)
            {
                foreach (FitbitDevice device in allDevices)
                {
                    if (string.Equals(Constants.DEVICE_TYPE_TRACKER, device.Type))
                    {
                        trackers.Add(device);
                    }
                }
            }

            if (trackers.Count == 0)
            {
                return await pluginServices.LanguageGenerator.GetPattern("AlarmSetNoDevices", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            parameters.TrackerId = trackers[0].Id; // FIXME need to disambiguate this properly

            SlotValue timeSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, Constants.SLOT_TIME);
            SlotValue dateSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, Constants.SLOT_DATE);
            SlotValue meridianSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, Constants.SLOT_MERIDIAN);
            //SlotValue deviceSlot = DialogHelpers.TryGetSlot(queryWithContext.Result, Constants.SLOT_DEVICE);
            if (timeSlot != null)
            {
                parameters = UpdateAlarmWithNewTimexInfo(parameters, timeSlot, dateSlot, pluginServices.Logger);
            }
            if (meridianSlot != null)
            {
                parameters = UpdateAlarmWithMeridianInfo(parameters, meridianSlot, pluginServices.Logger);
            }

            // Write back parameters for the next turn
            pluginServices.SessionStore.Put(alarmParamsKey, parameters);

            // What parameters are missing?
            if (!parameters.Hour.HasValue)
            {
                if (parameters.DayOfWeek == AlarmDayOfWeek.None && !parameters.IsForWeekdays && !parameters.IsForWeekends)
                {
                    return await pluginServices.LanguageGenerator.GetPattern("AlarmSetPromptTime", queryWithContext.ClientContext, pluginServices.Logger)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.ContinueBasic
                        });
                }
                else
                {
                    return await pluginServices.LanguageGenerator.GetPattern("AlarmSetPromptTimeOfDay", queryWithContext.ClientContext, pluginServices.Logger)
                        .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            MultiTurnResult = MultiTurnBehavior.ContinueBasic
                        });
                }
            }

            DateTime alarmTime = new DateTime(1, 1, 1, parameters.Hour.Value, parameters.Minute.GetValueOrDefault(0), 0);

            if (!parameters.AmPmResolved)
            {
                return await pluginServices.LanguageGenerator.GetPattern("AlarmSetPromptAMPM", queryWithContext.ClientContext, pluginServices.Logger)
                    .Sub("time", alarmTime)
                    .ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        MultiTurnResult = MultiTurnBehavior.ContinueBasic
                    });
            }

            // Now actually set the alarm
            List<DayOfWeek> daysOfWeek = new List<DayOfWeek>();
            if (parameters.IsForWeekdays)
            {
                daysOfWeek.Add(DayOfWeek.Monday);
                daysOfWeek.Add(DayOfWeek.Tuesday);
                daysOfWeek.Add(DayOfWeek.Wednesday);
                daysOfWeek.Add(DayOfWeek.Thursday);
                daysOfWeek.Add(DayOfWeek.Friday);
            }
            else if (parameters.IsForWeekends)
            {
                daysOfWeek.Add(DayOfWeek.Saturday);
                daysOfWeek.Add(DayOfWeek.Sunday);
            }
            else if (parameters.DayOfWeek == AlarmDayOfWeek.None)
            {
                daysOfWeek.Add(DayOfWeek.Sunday);
                daysOfWeek.Add(DayOfWeek.Monday);
                daysOfWeek.Add(DayOfWeek.Tuesday);
                daysOfWeek.Add(DayOfWeek.Wednesday);
                daysOfWeek.Add(DayOfWeek.Thursday);
                daysOfWeek.Add(DayOfWeek.Friday);
                daysOfWeek.Add(DayOfWeek.Saturday);
            }
            else
            {
                daysOfWeek.Add((DayOfWeek)(((int)(parameters.DayOfWeek) % 7)));
            }

            await service.SetDeviceAlarm(
                oauthToken,
                pluginServices.Logger,
                fitbitUserProfile,
                parameters.TrackerId,
                new TimeSpan(parameters.Hour.Value, parameters.Minute.GetValueOrDefault(0), 0),
                true,
                parameters.Recurring,
                daysOfWeek);

            // And generate response LG
            DateTimeOffset userTimeUtc = timeProvider.Time;
            DateTimeOffset userTimeLocal = userTimeUtc.ToOffset(TimeSpan.FromMilliseconds(fitbitUserProfile.OffsetFromUTCMillis));
            bool needsSyncReminder = userTimeLocal.Hour >= 19; // Give the "sync before bed" reminder for any alarms set after 7 PM

            string patternName = null;
            if (parameters.Recurring && needsSyncReminder)
                patternName = "AlarmSetRecurringWithSyncReminder";
            else if (parameters.Recurring && !needsSyncReminder)
                patternName = "AlarmSetRecurring";
            else if (!parameters.Recurring && needsSyncReminder)
                patternName = "AlarmSetSingleWithSyncReminder";
            else if (!parameters.Recurring && !needsSyncReminder)
                patternName = "AlarmSetSingle";

            string daysOfWeekForLG;
            if (parameters.IsForWeekends)
            {
                daysOfWeekForLG = "WEEKENDS";
            }
            else if (parameters.IsForWeekdays)
            {
                daysOfWeekForLG = "WEEKDAYS";
            }
            else if (parameters.Recurring && parameters.DayOfWeek == AlarmDayOfWeek.None)
            {
                daysOfWeekForLG = "EVERYDAY";
            }
            else if (parameters.DayOfWeek != AlarmDayOfWeek.None)
            {
                daysOfWeekForLG = parameters.DayOfWeek.ToString().ToUpperInvariant();
            }
            else
            {
                daysOfWeekForLG = string.Empty;
            }

            return await pluginServices.LanguageGenerator.GetPattern(patternName, queryWithContext.ClientContext, pluginServices.Logger)
                .Sub("time", alarmTime)
                .Sub("days_of_week", daysOfWeekForLG)
                .ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None
                });
        }

        private static AlarmSetParameters UpdateAlarmWithNewTimexInfo(AlarmSetParameters parameters, SlotValue timeSlot, SlotValue dateSlot, ILogger logger)
        {
            TimexContext context = new TimexContext()
            {
                Normalization = Normalization.Future,
                UseInference = false,
                ReferenceDateTime = DateTime.UtcNow
            };

            List<TimexMatch> timexes = new List<TimexMatch>();
            if (timeSlot != null)
            {
                timexes.AddRange(timeSlot.GetTimeMatches(TemporalType.Time | TemporalType.Set, context));
            }
            if (dateSlot != null)
            {
                timexes.AddRange(dateSlot.GetTimeMatches(TemporalType.Time | TemporalType.Set, context));
            }

            foreach (TimexMatch match in timexes)
            {
                TimexValue timex = TimexValue.CreateFromExtendedDateTime(match.ExtendedDateTime);
                if (timex.GetTemporalType() == TemporalType.Set)
                {
                    Recurrence r = timex as Recurrence;
                    logger.Log("Got a recurrence");
                    if (r.GetFrequencyUnit() == TemporalUnit.Week)
                    {
                        foreach (var anchorField in r.GetAnchorValues())
                        {
                            if (anchorField.Key == AnchorField.Weekday)
                            {
                                parameters.IsForWeekdays = true;
                                parameters.IsForWeekends = false;
                                parameters.DayOfWeek = AlarmDayOfWeek.None;
                            }
                            else if (anchorField.Key == AnchorField.Weekend)
                            {
                                parameters.IsForWeekends = true;
                                parameters.IsForWeekdays = false;
                                parameters.DayOfWeek = AlarmDayOfWeek.None;
                            }
                            else if (anchorField.Key == AnchorField.DayOfWeek)
                            {
                                parameters.IsForWeekdays = false;
                                parameters.IsForWeekends = false;
                                int day;
                                if (int.TryParse(anchorField.Value, out day))
                                {
                                    parameters.DayOfWeek = (AlarmDayOfWeek)(day);
                                }
                            }
                            else if (anchorField.Key == AnchorField.Hour)
                            {
                                parameters.Hour = int.Parse(anchorField.Value);
                                logger.Log("Setting hour = " + parameters.Hour.Value);
                                if (!parameters.AmPmResolved)
                                {
                                    parameters.AmPmResolved = !r.GetAmPmAmbiguousFlag();
                                    logger.Log("Setting AMPM resolved = " + parameters.AmPmResolved);
                                }
                            }
                        }
                    }
                    //else
                    //{
                    //    parameters.IsForWeekdays = false;
                    //    parameters.IsForWeekends = false;
                    //    parameters.DayOfWeek = AlarmDayOfWeek.None;
                    //}

                    parameters.Recurring = true;
                    logger.Log("After processing recurrence, days of week = " + parameters.DayOfWeek);
                }
                else if (timex.GetTemporalType().HasFlag(TemporalType.Date) ||
                    timex.GetTemporalType().HasFlag(TemporalType.Time))
                {
                    DateAndTime dt = timex as DateAndTime;
                    if (!parameters.Hour.HasValue && dt.GetHour().HasValue)
                    {
                        parameters.Hour = dt.GetHour();
                        logger.Log("Setting hour = " + parameters.Hour.Value);
                        if (!parameters.AmPmResolved)
                        {
                            parameters.AmPmResolved = !dt.GetAmPmAmbiguousFlag();
                            logger.Log("Setting AMPM resolved = " + parameters.AmPmResolved);
                        }
                    }
                    if (!parameters.Minute.HasValue && dt.GetMinute().HasValue)
                    {
                        parameters.Minute = dt.GetMinute();
                        logger.Log("Setting minute = " + parameters.Minute.Value);
                    }
                    if (dt.GetDayOfWeek().HasValue)
                    {
                        int dayOfWeek = dt.GetDayOfWeek().Value;
                        parameters.DayOfWeek = (AlarmDayOfWeek)(dayOfWeek);
                        logger.Log("Setting day of week = " + parameters.DayOfWeek);
                    }
                }
            }

            return parameters;
        }

        private static AlarmSetParameters UpdateAlarmWithMeridianInfo(AlarmSetParameters parameters, SlotValue meridianSlot, ILogger logger)
        {
            if (!parameters.Hour.HasValue)
            {
                logger.Log("Attempting to set meridian when no hour has been stored", LogLevel.Wrn);
                return parameters;
            }
            if (parameters.AmPmResolved)
            {
                return parameters;
            }

            string meridian = meridianSlot.Value;
            if (string.Equals(meridian, "AM") && parameters.Hour.Value > 12)
            {
                parameters.Hour = parameters.Hour.Value - 12;
            }
            else if (string.Equals(meridian, "PM") && parameters.Hour.Value <= 12)
            {
                parameters.Hour = parameters.Hour.Value + 12;
            }
            // FIXME need to handle 12 AM/PM

            parameters.AmPmResolved = true;
            return parameters;
        }
    }
}
