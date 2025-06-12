using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Client;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Durandal.Common.Time.TimeZone;
using Durandal.CommonViews;
using Durandal.Internal.CoreOntology.SchemaDotOrg;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Time
{
    public static class Scenarios
    {
        /// <summary>
        /// Returns the current date or time relative to the user's reference clock
        /// </summary>
        /// <param name="queryWithContext"></param>
        /// <param name="services"></param>
        /// <param name="requestedField"></param>
        /// <returns></returns>
        public static async Task<PluginResult> ResolveTimeByUserClock(
            ClientContext clientContext,
            IPluginServices services,
            string requestedField,
            IRandom rand,
            IRealTimeProvider realTime,
            TimeZoneResolver timeZoneResolver,
            UserTimeContext userTimeContext)
        {
            if (requestedField.Equals("TIME"))
            {
                return await ScenarioUserLocalTime(clientContext, services, rand, timeZoneResolver, userTimeContext, realTime.Time).ConfigureAwait(false);
            }
            else if (requestedField.Equals("DATE") || requestedField.Equals("DAY_OF_MONTH") || requestedField.Equals("DAY_OF_WEEK"))
            {
                return await ScenarioUserLocalDate(clientContext, services, rand, timeZoneResolver, userTimeContext, realTime.Time).ConfigureAwait(false);
            }
            else if (requestedField.Equals("DAYLIGHT_SAVINGS"))
            {
                return await ScenarioUserLocalDaylightSavingsTransition(clientContext, services, rand, timeZoneResolver, realTime.Time).ConfigureAwait(false);
            }
            else
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Unknown time field value: " + requestedField
                };
            }
        }

        public static Task<PluginResult> ScenarioUserLocalTime(ClientContext context, IPluginServices services, IRandom rand, TimeZoneResolver timeZoneResolver, UserTimeContext userTimeContext, DateTimeOffset utcDateTime)
        {
            userTimeContext = userTimeContext ?? TimeHelpers.ExtractUserTimeContext(context, services.Logger, timeZoneResolver, utcDateTime);

            ILGPattern pattern;
            DateTimeOffset returnedTime;
            if (userTimeContext != null)
            {
                pattern = services.LanguageGenerator.GetPattern("LocalTime", context, services.Logger, false, rand.NextInt());
                pattern.Sub("time", userTimeContext.UserLocalTime);
                returnedTime = userTimeContext.UserLocalTime;
            }
            else
            {
                pattern = services.LanguageGenerator.GetPattern("LocalTimeUnsure", context, services.Logger, false, rand.NextInt());
                pattern.Sub("time", utcDateTime);
                returnedTime = utcDateTime;
            }

            return pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                ResponseHtml = new ClockView()
                {
                    StartTime = returnedTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }.Render()
            });
        }

        public static async Task<PluginResult> ScenarioUserLocalDate(ClientContext context, IPluginServices services, IRandom rand, TimeZoneResolver timeZoneResolver, UserTimeContext userTimeContext, DateTimeOffset utcDateTime)
        {
            userTimeContext = userTimeContext ?? TimeHelpers.ExtractUserTimeContext(context, services.Logger, timeZoneResolver, utcDateTime);
            ILGPattern pattern;
            if (userTimeContext != null)
            {
                pattern = services.LanguageGenerator.GetPattern("LocalDate", context, services.Logger);
                pattern.Sub("date", userTimeContext.UserLocalTime);
            }
            else
            {
                pattern = services.LanguageGenerator.GetPattern("LocalDateUnsure", context, services.Logger);
                pattern.Sub("date", utcDateTime);
            }

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render().ConfigureAwait(false)).Text,
                    ClientContextData = context.ExtraClientContext
                }.Render()
            }).ConfigureAwait(false);
        }

        public static async Task<PluginResult> ScenarioUserLocalDaylightSavingsTransition(ClientContext context, IPluginServices services, IRandom rand, TimeZoneResolver timeZoneResolver, DateTimeOffset utcDateTime)
        {
            UserTimeContext userLocalTime = TimeHelpers.ExtractUserTimeContext(context, services.Logger, timeZoneResolver, utcDateTime);

            List<TimeZoneRuleEffectiveSpan> timeZoneSpans = timeZoneResolver.CalculateTimeZoneRuleSpans(userLocalTime.UserTimeZone, userLocalTime.UserLocalTime, utcDateTime.AddDays(365), services.Logger);
            if (timeZoneSpans == null || timeZoneSpans.Count < 2)
            {
                ILGPattern noDstPattern = services.LanguageGenerator.GetPattern("DaylightSavingsNotFound", context, services.Logger);
                return await noDstPattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await noDstPattern.Render().ConfigureAwait(false)).Text,
                        ClientContextData = context.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
            }

            DateTimeOffset nextRuleChangeTime = timeZoneSpans[0].RuleBoundaryEnd;
            TimeSpan nextRuleClockDifference = (timeZoneSpans[1].DstOffset + timeZoneSpans[1].GmtOffset) - (timeZoneSpans[0].DstOffset + timeZoneSpans[0].GmtOffset);

            ILGPattern pattern = services.LanguageGenerator.GetPattern("DaylightSavings", context, services.Logger)
                .Sub("local_time", userLocalTime.UserLocalTime)
                .Sub("dst_change_time", nextRuleChangeTime)
                .Sub("offset", nextRuleClockDifference);

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render().ConfigureAwait(false)).Text,
                    ClientContextData = context.ExtraClientContext
                }.Render()
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns the date or time of a named time entity that was found in the time slot, ignoring the user's clock
        /// </summary>
        /// <param name="queryWithContext"></param>
        /// <param name="services"></param>
        /// <param name="requestedField"></param>
        /// <param name="relativeTime"></param>
        /// <param name="timeSlotValue"></param>
        /// <returns></returns>
        public static async Task<PluginResult> ResolveTimeRelative(
            QueryWithContext queryWithContext,
            IPluginServices services,
            string requestedField,
            ExtendedDateTime relativeTime,
            string timeSlotValue,
            IRandom rand,
            IRealTimeProvider realTime,
            TimeZoneResolver timeZoneResolver, 
            UserTimeContext userTimeContext)
        {
            string isoTime = relativeTime.FormatValue();
            DateTime timexVal;
            if (!DateTimeParsers.TryParseISOIntoLocalDateTime(isoTime, out timexVal))
            {
                services.Logger.Log("Could not parse ISO time - it may be a weekend or other unhandled formulation", LogLevel.Err);
                services.Logger.Log(timeSlotValue + " -> " + isoTime, LogLevel.Err);
                return await ResolveTimeByUserClock(queryWithContext.ClientContext, services, requestedField, rand, realTime, timeZoneResolver, userTimeContext).ConfigureAwait(false);
            }

            DateTimeOffset answer = new DateTimeOffset(timexVal);

            // Is it "today"? If so, just return the default local time
            if (relativeTime.Offset.HasValue && relativeTime.Offset.Value == 0 &&
                relativeTime.OffsetUnit.HasValue && relativeTime.OffsetUnit == TemporalUnit.Day)
            {
                services.Logger.Log("Caught \"today\" case; dropping time slot and reverting to default date query handler", LogLevel.Vrb);
                return await ResolveTimeByUserClock(queryWithContext.ClientContext, services, requestedField, rand, realTime, timeZoneResolver, userTimeContext).ConfigureAwait(false);
            }

            ILGPattern pattern = null;

            if (requestedField.Equals("TIME"))
            {
                services.Logger.Log("User asked for \"what time is " + timeSlotValue + "\", which is unsupported", LogLevel.Err);
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Relative time scenario is unsupported"
                };
            }
            else if (requestedField.Equals("DATE"))
            {
                if (relativeTime.ExplicitSetParts.HasFlag(DateTimeParts.WeekDay))
                {
                    services.Logger.Log("The user mentioned a day of the week, so I won't mention one in the response");
                    pattern = services.LanguageGenerator.GetPattern("RelativeDate", queryWithContext.ClientContext, services.Logger, false, rand.NextInt());
                    pattern.Sub("query", timeSlotValue);
                    pattern.Sub("date", answer);
                }
                else
                {
                    pattern = services.LanguageGenerator.GetPattern("RelativeDate", queryWithContext.ClientContext, services.Logger, false, rand.NextInt());
                    pattern.Sub("query", timeSlotValue);
                    pattern.Sub("date", answer);
                }
            }
            else if (requestedField.Equals("DAY_OF_MONTH"))
            {
                pattern = services.LanguageGenerator.GetPattern("RelativeDayOfMonth", queryWithContext.ClientContext, services.Logger, false, rand.NextInt());
                pattern.Sub("query", timeSlotValue);
                pattern.Sub("day_of_month", answer);
            }
            else if (requestedField.Equals("DAY_OF_WEEK"))
            {
                pattern = services.LanguageGenerator.GetPattern("RelativeDayOfWeek", queryWithContext.ClientContext, services.Logger, false, rand.NextInt());
                pattern.Sub("query", timeSlotValue);
                pattern.Sub("day_of_week", answer);
            }
            else
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Unknown time field value: " + requestedField
                };
            }

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render().ConfigureAwait(false)).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            }).ConfigureAwait(false);
        }

        internal static async Task<PluginResult> ScenarioWorldTime(
            ClientContext clientContext,
            IPluginServices services,
            SchemaDotOrg.Place locationValue,
            string requestedField,
            IRandom rand,
            IRealTimeProvider realTime,
            TimeZoneResolver timeZoneResolver,
            UserTimeContext userTimeContext)
        {
            SchemaDotOrg.GeoCoordinates coords = await locationValue.Geo_as_GeoCoordinates.GetValue().ConfigureAwait(false);
            string locationName = locationValue.Name.Value;

            if (coords == null)
            {
                services.Logger.Log("The location \"" + locationName + " does not have coordinates (TODO resolve separately?)", LogLevel.Err);
                return await ResolveTimeByUserClock(clientContext, services, requestedField, rand, realTime, timeZoneResolver, userTimeContext).ConfigureAwait(false);
            }

            if (timeZoneResolver == null)
            {
                services.Logger.Log("Time zone resolver is not loaded!", LogLevel.Err);
                return await ResolveTimeByUserClock(clientContext, services, requestedField, rand, realTime, timeZoneResolver, userTimeContext).ConfigureAwait(false);
            }

            GeoCoordinate coords2 = new GeoCoordinate();
            coords2.Latitude = (double)coords.Latitude_as_number.Value;
            coords2.Longitude = (double)coords.Longitude_as_number.Value;
            TimeZoneQueryResult tzQueryResult = timeZoneResolver.CalculateLocalTime(coords2, realTime.Time, services.Logger);

            if (tzQueryResult == null ||
                string.IsNullOrEmpty(tzQueryResult.TimeZoneName))
            {
                ILGPattern lg = services.LanguageGenerator.GetPattern("DontKnowWorldTime", clientContext, services.Logger, false, rand.NextInt())
                    .Sub("location", locationName);
                return await lg.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await lg.Render().ConfigureAwait(false)).Text,
                        ClientContextData = clientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
            }

            UserTimeContext userLocalTime = TimeHelpers.ExtractUserTimeContext(clientContext, services.Logger, timeZoneResolver, realTime.Time);
            
            ILGPattern pattern = services.LanguageGenerator.GetPattern("WorldTime", clientContext, services.Logger, false, rand.NextInt())
                .Sub("location", locationName)
                .Sub("world_time", tzQueryResult.LocalTime)
                .Sub("local_time", userLocalTime == null ? realTime.Time : userLocalTime.UserLocalTime);

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                ResponseHtml = new ClockView()
                {
                    StartTime = tzQueryResult.LocalTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }.Render()
            }).ConfigureAwait(false);
        }

        internal static async Task<PluginResult> ScenarioWorldTimeDifference(
            ClientContext clientContext,
            IPluginServices services,
            SchemaDotOrg.Place basisLocation,
            SchemaDotOrg.Place queryLocation,
            IRandom rand,
            IRealTimeProvider realTime,
            TimeZoneResolver timeZoneResolver,
            ExtendedDateTime timeAtBasisLocation)
        {
            GeoCoordinate basisLocationCoords;
            string basisLocationName;
            GeoCoordinate queryLocationCoords;
            string queryLocationName;

            if (basisLocation == null)
            {
                basisLocationName = "CURRENT_LOCATION";
                if (!clientContext.Latitude.HasValue ||
                    !clientContext.Longitude.HasValue)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "No user location"
                    };
                }

                basisLocationCoords = new GeoCoordinate(clientContext.Latitude.Value, clientContext.Longitude.Value);
            }
            else
            {
                basisLocationName = basisLocation.Name.Value;
                SchemaDotOrg.GeoCoordinates basisLocationCoordinateEntity = await basisLocation.Geo_as_GeoCoordinates.GetValue().ConfigureAwait(false);
                if (basisLocationCoordinateEntity == null)
                {
                    services.Logger.Log("The location \"" + basisLocationName + " does not have coordinates", LogLevel.Err);
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "No place coordinates"
                    };
                }

                basisLocationCoords = new GeoCoordinate(
                    (double)basisLocationCoordinateEntity.Latitude_as_number.Value,
                    (double)basisLocationCoordinateEntity.Longitude_as_number.Value);
            }

            if (queryLocation == null)
            {
                queryLocationName = "CURRENT_LOCATION";
                if (!clientContext.Latitude.HasValue ||
                    !clientContext.Longitude.HasValue)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "No user location"
                    };
                }

                queryLocationCoords = new GeoCoordinate(clientContext.Latitude.Value, clientContext.Longitude.Value);
            }
            else
            {
                queryLocationName = queryLocation.Name.Value;
                SchemaDotOrg.GeoCoordinates queryLocationCoordinateEntity = await queryLocation.Geo_as_GeoCoordinates.GetValue().ConfigureAwait(false);
                if (queryLocationCoordinateEntity == null)
                {
                    services.Logger.Log("The location \"" + queryLocationName + " does not have coordinates", LogLevel.Err);
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "No place coordinates"
                    };
                }

                queryLocationCoords = new GeoCoordinate(
                    (double)queryLocationCoordinateEntity.Latitude_as_number.Value,
                    (double)queryLocationCoordinateEntity.Longitude_as_number.Value);
            }

            if (timeZoneResolver == null)
            {
                services.Logger.Log("Time zone resolver is not loaded!", LogLevel.Err);
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Time zone resolver not loaded"
                };
            }
            
            TimeZoneQueryResult basisLocationTimezone = timeZoneResolver.CalculateLocalTime(basisLocationCoords, realTime.Time, services.Logger);

            DateAndTime parsedDateAndTime = TimexValue.CreateFromExtendedDateTime(timeAtBasisLocation).AsDateAndTime();
            DateTimeOffset timeInBasisLocation = new DateTimeOffset(
                basisLocationTimezone.LocalTime.Year,
                basisLocationTimezone.LocalTime.Month,
                basisLocationTimezone.LocalTime.Day,
                parsedDateAndTime.GetHour().Value,
                parsedDateAndTime.GetMinute().GetValueOrDefault(0),
                parsedDateAndTime.GetSecond().GetValueOrDefault(0),
                basisLocationTimezone.LocalTime.Offset);
            
            TimeZoneQueryResult queryLocationLocalTime = timeZoneResolver.CalculateLocalTime(queryLocationCoords, timeInBasisLocation, services.Logger);
            TimeZoneQueryResult basisLocationLocalTime = timeZoneResolver.CalculateLocalTime(basisLocationTimezone.TimeZoneName, timeInBasisLocation, services.Logger);
            
            ILGPattern lgPattern = services.LanguageGenerator.GetPattern("WorldTimeDifference", clientContext, services.Logger, true);
            lgPattern.Sub("basis_time", basisLocationLocalTime.LocalTime);
            lgPattern.Sub("query_time", queryLocationLocalTime.LocalTime);
            lgPattern.Sub("basis_location", basisLocationName);
            lgPattern.Sub("query_location", queryLocationName);

            RenderedLG phraseResult = await lgPattern.Render().ConfigureAwait(false);

            return new PluginResult(Result.Success)
            {
                ResponseText = phraseResult.Text,
                ResponseSsml = phraseResult.Spoken,
                ResponseHtml = new MessageView()
                    {
                        Content = phraseResult.Text
                    }.Render()
            };
        }

        internal static async Task<PluginResult> ScenarioWorldTimezone(
            ClientContext clientContext,
            IPluginServices services,
            SchemaDotOrg.Place locationValue,
            IRandom rand,
            IRealTimeProvider realTime,
            TimeZoneResolver timeZoneResolver)
        {
            SchemaDotOrg.GeoCoordinates coords = await locationValue.Geo_as_GeoCoordinates.GetValue().ConfigureAwait(false);
            string locationName = locationValue.Name.Value;

            if (coords == null)
            {
                services.Logger.Log("The location \"" + locationName + " does not have coordinates (TODO resolve separately?)", LogLevel.Err);
                return await ResolveTimeByUserClock(clientContext, services, "TIME", rand, realTime, timeZoneResolver, null).ConfigureAwait(false);
            }

            if (timeZoneResolver == null)
            {
                services.Logger.Log("Time zone resolver is not loaded!", LogLevel.Err);
                return await ResolveTimeByUserClock(clientContext, services, "TIME", rand, realTime, timeZoneResolver, null).ConfigureAwait(false);
            }

            GeoCoordinate coords2 = new GeoCoordinate();
            coords2.Latitude = (double)coords.Latitude_as_number.Value;
            coords2.Longitude = (double)coords.Longitude_as_number.Value;
            TimeZoneQueryResult tzQueryResult = timeZoneResolver.CalculateLocalTime(coords2, realTime.Time, services.Logger);

            if (tzQueryResult == null ||
                string.IsNullOrEmpty(tzQueryResult.TimeZoneName))
            {
                ILGPattern lg = services.LanguageGenerator.GetPattern("DontKnowWorldTime", clientContext, services.Logger, false, rand.NextInt())
                    .Sub("location", locationName);
                return await lg.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await lg.Render().ConfigureAwait(false)).Text,
                        ClientContextData = clientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
            }

            UserTimeContext userLocalTime = TimeHelpers.ExtractUserTimeContext(clientContext, services.Logger, timeZoneResolver, realTime.Time);

            TimeSpan offsetDifference = (tzQueryResult.GmtOffset + tzQueryResult.DstOffset) - userLocalTime.UserLocalTime.Offset;
            string responsePhrase;
            if (offsetDifference > TimeSpan.Zero)
            {
                responsePhrase = locationName + " is " + (int)Math.Round(offsetDifference.TotalHours) + " hours ahead of you, and the local time there is " + tzQueryResult.LocalTime.ToString("h:mm tt") + ".";
            }
            else if (offsetDifference < TimeSpan.Zero)
            {
                responsePhrase = locationName + " is " + (int)Math.Round(offsetDifference.Negate().TotalHours) + " hours behind you, and the local time there is " + tzQueryResult.LocalTime.ToString("h:mm tt") + ".";
            }
            else
            {
                responsePhrase = locationName + " is in the same timezone as you, and the local time is " + tzQueryResult.LocalTime.ToString("h:mm tt") + ".";
            }

            return new PluginResult(Result.Success)
            {
                ResponseText = responsePhrase,
                ResponseSsml = responsePhrase,
                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                ResponseHtml = new ClockView()
                {
                    StartTime = tzQueryResult.LocalTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }.Render()
            };
        }
    }
}
