using Durandal.Common.Logger;
using Durandal.Common.UnitConversion;
using Durandal.ExternalServices.Darksky;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Weather
{
    public static class ConversionHelpers
    {
        public static void ConvertAllTemperatures(DarkskyWeatherResult weatherResults, string sourceUnit, string targetUnit, ILogger traceLogger)
        {
            if (weatherResults.Currently != null)
            {
                ConvertAllTemperatures(weatherResults.Currently, sourceUnit, targetUnit, traceLogger);
            }
            if (weatherResults.Daily != null && weatherResults.Daily.Data != null)
            {
                foreach (DarkskyWeatherDataPoint dataPoint in weatherResults.Daily.Data)
                {
                    ConvertAllTemperatures(dataPoint, sourceUnit, targetUnit, traceLogger);
                }
            }
            if (weatherResults.Hourly != null && weatherResults.Hourly.Data != null)
            {
                foreach (DarkskyWeatherDataPoint dataPoint in weatherResults.Hourly.Data)
                {
                    ConvertAllTemperatures(dataPoint, sourceUnit, targetUnit, traceLogger);
                }
            }
            if (weatherResults.Minutely != null && weatherResults.Minutely.Data != null)
            {
                foreach (DarkskyWeatherDataPoint dataPoint in weatherResults.Minutely.Data)
                {
                    ConvertAllTemperatures(dataPoint, sourceUnit, targetUnit, traceLogger);
                }
            }
        }

        public static void ConvertAllTemperatures(DarkskyWeatherDataPoint dataPoint, string sourceUnit, string targetUnit, ILogger traceLogger)
        {
            if (dataPoint != null)
            {
                dataPoint.ApparentTemperature = DoSingleConversion(dataPoint.ApparentTemperature, sourceUnit, targetUnit, traceLogger);
                dataPoint.ApparentTemperatureHigh = DoSingleConversion(dataPoint.ApparentTemperatureHigh, sourceUnit, targetUnit, traceLogger);
                dataPoint.ApparentTemperatureLow = DoSingleConversion(dataPoint.ApparentTemperatureLow, sourceUnit, targetUnit, traceLogger);
            }
        }

        public static double? DoSingleConversion(double? amount, string sourceUnit, string targetUnit, ILogger traceLogger)
        {
            if (!amount.HasValue)
            {
                return null;
            }

            List<UnitConversionResult> conversionResults = UnitConverter.Convert(sourceUnit, targetUnit, (decimal)amount.Value, traceLogger);
            if (conversionResults.Count > 0)
            {
                return (double)conversionResults[0].TargetUnitAmount;
            }

            return null;
        }
    }
}
