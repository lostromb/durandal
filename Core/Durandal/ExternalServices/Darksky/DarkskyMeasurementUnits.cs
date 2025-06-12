using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Darksky
{
    public enum DarkskyMeasurementUnit
    {
        /// <summary>
        /// Automatically select units based on geographic location
        /// </summary>
        Auto,

        /// <summary>
        /// SI units
        /// </summary>
        SI,

        /// <summary>
        /// American imperial units
        /// </summary>
        US,

        /// <summary>
        /// Same as SI, except that windSpeed and windGust are in kilometers per hour
        /// </summary>
        CA,

        /// <summary>
        /// Same as SI, except that nearestStormDistance and visibility are in miles, and windSpeed and windGust in miles per hour
        /// </summary>
        UK
    }


    public static class DarkskyMeasurementUnitsExtensions
    {
        public static string ToQueryParamString(this DarkskyMeasurementUnit units)
        {
            switch (units)
            {
                case DarkskyMeasurementUnit.Auto:
                    return "auto";
                case DarkskyMeasurementUnit.CA:
                    return "ca";
                case DarkskyMeasurementUnit.SI:
                    return "si";
                case DarkskyMeasurementUnit.UK:
                    return "uk2";
                case DarkskyMeasurementUnit.US:
                    return "us";
                default:
                    return "null";
            }
        }
    }
}
