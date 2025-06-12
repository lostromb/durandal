using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Time.TimeZone;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Time
{
    public static class TimeHelpers
    {
        /// <summary>
        /// Tries any and all methods to determine the current local time for a user
        /// </summary>
        /// <param name="context"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static UserTimeContext ExtractUserTimeContext(
            ClientContext context,
            ILogger logger,
            TimeZoneResolver timeZoneResolver,
            DateTimeOffset currentUtcTime)
        {
            if (context == null)
            {
                return null;
            }

            DateTimeOffset? userLocalTime = null;
            string userTimeZoneName = context.UserTimeZone;

            // Resolve based on user context timezone name
            if (!string.IsNullOrEmpty(userTimeZoneName) && timeZoneResolver != null)
            {
                logger.Log("Resolving user time using user time zone \"" + context.UserTimeZone + "\"");
                // attempt to convert from windows to IANA zone name if possible
                string ianaZone = TimeZoneHelpers.MapWindowsToIANATimeZone(context.UserTimeZone);
                ianaZone = string.IsNullOrEmpty(ianaZone) ? context.UserTimeZone : ianaZone;
                logger.Log("Interpreting user time zone as IANA zone \"" + ianaZone + "\"");
                TimeZoneQueryResult locationResult = timeZoneResolver.CalculateLocalTime(ianaZone, currentUtcTime, logger);
                if (locationResult != null)
                {
                    userLocalTime = locationResult.LocalTime;
                    userTimeZoneName = locationResult.TimeZoneName;
                }

                logger.Log("No results for timezone \"" + ianaZone + "\"");
            }

            // Resolve based on user context location
            if ((!userLocalTime.HasValue ||
                string.IsNullOrEmpty(userTimeZoneName)) &&
                context.Latitude.HasValue &&
                context.Longitude.HasValue &&
                timeZoneResolver != null)
            {
                // Resolve user time based on their location
                TimeZoneQueryResult locationResult = timeZoneResolver.CalculateLocalTime(new GeoCoordinate(context.Latitude.Value, context.Longitude.Value), currentUtcTime, logger);
                if (locationResult != null)
                {
                    userLocalTime = locationResult.LocalTime;
                    userTimeZoneName = locationResult.TimeZoneName;
                }
            }

            // Resolve based on user context UTC offset
            if (!userLocalTime.HasValue && context.UTCOffset.HasValue)
            {
                logger.Log("Resolving user time using UTCOffset " + context.UTCOffset.Value);
                userLocalTime = currentUtcTime.UtcDateTime.AddMinutes(context.UTCOffset.Value);
            }

            // Resolve based on user context reference date time
            if (!userLocalTime.HasValue && !string.IsNullOrEmpty(context.ReferenceDateTime))
            {
                logger.Log("Resolving user time using ReferenceDateTime " + context.ReferenceDateTime);
                userLocalTime = DateTimeOffset.Parse(context.ReferenceDateTime);
            }

            if (userLocalTime.HasValue)
            {
                return new UserTimeContext()
                {
                    UserTimeZone = userTimeZoneName,
                    UserLocalTime = userLocalTime.Value
                };
            }
            else
            {
                return null;
            }
        }
    }
}
