using Durandal.Common.Utils;
using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Darksky
{
    /// <summary>
    /// A data point object contains various properties, each representing the average (unless otherwise specified) of a particular
    /// weather phenomenon occurring during a period of time: an instant in the case of currently, a minute for minutely,
    /// an hour for hourly, and a day for daily. 
    /// </summary>
    public class DarkskyWeatherDataPoint
    {
        /// <summary>
        /// The apparent (or “feels like”) temperature in degrees.
        /// </summary>
        [JsonProperty("apparentTemperature")]
        public double? ApparentTemperature { get; set; }

        /// <summary>
        /// The daytime high apparent temperature.
        /// </summary>
        [JsonProperty("apparentTemperatureHigh")]
        public double? ApparentTemperatureHigh { get; set; }

        /// <summary>
        /// The UNIX time representing when the daytime high apparent temperature occurs.
        /// </summary>
        [JsonProperty("apparentTemperatureHighTime")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? ApparentTemperatureHighTime { get; set; }

        /// <summary>
        /// The overnight low apparent temperature.
        /// </summary>
        [JsonProperty("apparentTemperatureLow")]
        public double? ApparentTemperatureLow { get; set; }

        /// <summary>
        /// The UNIX time representing when the overnight low apparent temperature occurs.
        /// </summary>
        [JsonProperty("apparentTemperatureLowTime")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? ApparentTemperatureLowTime { get; set; }

        /// <summary>
        /// The percentage of sky occluded by clouds, between 0 and 1, inclusive.
        /// </summary>
        [JsonProperty("cloudCover")]
        public double? CloudCover { get; set; }

        /// <summary>
        /// The dew point in degrees Fahrenheit.
        /// In SI system the unit is celsius
        /// </summary>
        [JsonProperty("dewPoint")]
        public double? DewPoint { get; set; }

        /// <summary>
        /// The relative humidity, between 0 and 1, inclusive.
        /// </summary>
        [JsonProperty("humidity")]
        public double? Humidity { get; set; }

        /// <summary>
        /// A machine-readable text summary of this data point, suitable for selecting an icon for display.
        /// If defined, this property will have one of the following values:
        /// clear-day, clear-night, rain, snow, sleet, wind, fog, cloudy, partly-cloudy-day, or partly-cloudy-night. 
        /// </summary>
        [JsonProperty("icon")]
        public string Icon { get; set; }

        /// <summary>
        /// The fractional part of the lunation number during the given day: a value of 0 corresponds to a new moon, 0.25 to a first quarter moon, 0.5 to a full moon, and 0.75 to a last quarter moon.
        /// (The ranges in between these represent waxing crescent, waxing gibbous, waning gibbous, and waning crescent moons, respectively.)
        /// </summary>
        [JsonProperty("moonPhase")]
        public double? MoonPhase { get; set; }

        /// <summary>
        /// The approximate direction of the nearest storm in degrees, with true north at 0° and progressing clockwise.
        /// (If nearestStormDistance is zero, then this value will not be defined.)
        /// </summary>
        [JsonProperty("nearestStormBearing")]
        public double? NearestStormBearing { get; set; }

        /// <summary>
        /// The approximate distance to the nearest storm in miles.
        /// (A storm distance of 0 doesn’t necessarily refer to a storm at the requested location, but rather a storm in the vicinity of that location.)
        /// In SI system the unit is kilometers
        /// </summary>
        [JsonProperty("nearestStormDistance")]
        public double? NearestStormDistance { get; set; }

        /// <summary>
        /// The columnar density of total atmospheric ozone at the given time in Dobson units.
        /// </summary>
        [JsonProperty("ozone")]
        public double? Ozone { get; set; }

        /// <summary>
        /// The amount of snowfall accumulation expected to occur, in inches.
        /// (If no snowfall is expected, this property will not be defined.)
        /// In SI system the unit is centimeters
        /// </summary>
        [JsonProperty("precipAccumulation")]
        public double? PrecipAccumulation { get; set; }

        /// <summary>
        /// The intensity (in inches of liquid water per hour) of precipitation occurring at the given time.
        /// This value is conditional on probability (that is, assuming any precipitation occurs at all).
        /// In SI system the unit is millimeters per hour
        /// </summary>
        [JsonProperty("precipIntensity")]
        public double? PrecipIntensity { get; set; }

        /// <summary>
        /// The standard deviation of the distribution of precipIntensity.
        /// (We only return this property when the full distribution, and not merely the expected mean, can be estimated with accuracy.)
        /// </summary>
        [JsonProperty("precipIntensityError")]
        public double? PrecipIntensityError { get; set; }

        /// <summary>
        /// The maximum value of precipIntensity during a given day.
        /// In SI system the unit is millimeters per hour
        /// </summary>
        [JsonProperty("precipIntensityMax")]
        public double? PrecipIntensityMax { get; set; }

        /// <summary>
        /// The UNIX time of when precipIntensityMax occurs during a given day.
        /// </summary>
        [JsonProperty("precipIntensityMaxTime")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? PrecipIntensityMaxTime { get; set; }

        /// <summary>
        /// The probability of precipitation occurring, between 0 and 1, inclusive.
        /// </summary>
        [JsonProperty("precipProbability")]
        public double? PrecipProbability { get; set; }

        /// <summary>
        /// The type of precipitation occurring at the given time. If defined, this property will have one of the following values:
        /// "rain", "snow", or "sleet" (which refers to each of freezing rain, ice pellets, and “wintery mix”).
        /// If precipIntensity is zero, then this property will not be defined.
        /// Additionally, due to the lack of data in our sources, historical precipType information is usually estimated, rather than observed.
        /// </summary>
        [JsonProperty("precipType")]
        public string PrecipType { get; set; }

        /// <summary>
        /// The sea-level air pressure in millibars.
        /// In SI system the unit is hectopascals
        /// </summary>
        [JsonProperty("pressure")]
        public double? Pressure { get; set; }

        /// <summary>
        /// A human-readable text summary of this data point.
        /// </summary>
        [JsonProperty("summary")]
        public string Summary { get; set; }

        /// <summary>
        /// The UNIX time of when the sun will rise during a given day.
        /// </summary>
        [JsonProperty("sunriseTime")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? SunriseTime { get; set; }

        /// <summary>
        /// The UNIX time of when the sun will set during a given day.
        /// </summary>
        [JsonProperty("sunsetTime")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? SunsetTime { get; set; }

        /// <summary>
        /// The air temperature in degrees Fahrenheit.
        /// In SI system the unit is celsius
        /// </summary>
        [JsonProperty("temperature")]
        public double? Temperature { get; set; }

        /// <summary>
        /// The daytime high temperature.
        /// In SI system the unit is celsius
        /// </summary>
        [JsonProperty("temperatureHigh")]
        public double? TemperatureHigh { get; set; }

        /// <summary>
        /// The UNIX time representing when the daytime high temperature occurs.
        /// </summary>
        [JsonProperty("temperatureHighTime")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? TemperatureHighTime { get; set; }

        /// <summary>
        /// The overnight low temperature.
        /// In SI system the unit is celsius
        /// </summary>
        [JsonProperty("temperatureLow")]
        public double? TemperatureLow { get; set; }

        /// <summary>
        /// The UNIX time representing when the overnight low temperature occurs.
        /// </summary>
        [JsonProperty("temperatureLowTime")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? TemperatureLowTime { get; set; }

        /// <summary>
        /// The UNIX time at which this data point begins.
        /// minutely data point are always aligned to the top of the minute, hourly data point objects to the top of the hour, and daily data point objects to midnight of the day, all according to the local time zone.
        /// </summary>
        [JsonProperty("time")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset Time { get; set; }

        /// <summary>
        /// The UV index.
        /// </summary>
        [JsonProperty("uvIndex")]
        public double? UVIndex { get; set; }

        /// <summary>
        /// The UNIX time of when the maximum uvIndex occurs during a given day.
        /// </summary>
        [JsonProperty("uvIndexTime")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? UVIndexTime { get; set; }

        /// <summary>
        /// The average visibility in miles, capped at 10 miles.
        /// In SI system the unit is kilometers
        /// </summary>
        [JsonProperty("visibility")]
        public double? Visibility { get; set; }

        /// <summary>
        /// The direction that the wind is coming from in degrees, with true north at 0° and progressing clockwise. (If windSpeed is zero, then this value will not be defined.)
        /// </summary>
        [JsonProperty("windBearing")]
        public double? WindBearing { get; set; }

        /// <summary>
        /// The wind gust speed in miles per hour.
        /// In SI system the unit is kilometers per hour
        /// </summary>
        [JsonProperty("windGust")]
        public double? WindGust { get; set; }

        /// <summary>
        /// The time at which the maximum wind gust speed occurs during the day.
        /// </summary>
        [JsonProperty("windGustTime")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? WindGustTime { get; set; }

        /// <summary>
        /// The wind speed in miles per hour.
        /// In SI system the unit is kilometers per hour
        /// </summary>
        [JsonProperty("windSpeed")]
        public double? WindSpeed { get; set; }
    }
}
