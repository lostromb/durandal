using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Darksky
{
    /// <summary>
    /// Represents one or more portions of data we can request from the Darksky forecast API
    /// </summary>
    [Flags]
    public enum DarkskyRequestFeatures
    {
        None = 0,

        /// <summary>
        /// Request the current weather conditions
        /// </summary>
        CurrentWeather = 0x1 << 0,

        /// <summary>
        /// Request a minute-by-minute forecast for the next hour
        /// </summary>
        MinutelyWeather = 0x1 << 1,

        /// <summary>
        /// Request an hour-by-hour forecast for the next 48 hours
        /// </summary>
        HourlyWeather = 0x1 << 2,

        /// <summary>
        /// Request a daily forecast for the next 7 days
        /// </summary>
        DailyWeather = 0x1 << 3,

        /// <summary>
        /// Request any relevant severe weather results pertinent to the requested location
        /// </summary>
        Alerts = 0x1 << 4,

        /// <summary>
        /// Request miscellaneous metadata
        /// </summary>
        Flags = 0x1 << 5,

        /// <summary>
        /// If specified in addition to HourlyWeather, the hourly forecast will be extended to 168 hours instead of 48
        /// </summary>
        ExtendedHourlyForecast = 0x1 << 6,

        /// <summary>
        /// The default request data is current + daily weather plus metadata
        /// </summary>
        Default = CurrentWeather | DailyWeather | Flags,

        /// <summary>
        /// Request all possible available weather data (costly!)
        /// </summary>
        All = CurrentWeather | MinutelyWeather | HourlyWeather | DailyWeather | Alerts | Flags | ExtendedHourlyForecast
    }
}
