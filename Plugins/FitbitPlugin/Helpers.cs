using Durandal.Plugins.Fitbit.Schemas;
using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.Logger;
using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Client;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.Time;
using Durandal.Common.UnitConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Dialog;

namespace Durandal.Plugins.Fitbit
{
    public static class Helpers
    {
        public static double ConvertMetersToFeet(double meters, ILogger logger)
        {
            List<UnitConversionResult> conversionResults = UnitConverter.Convert(UnitName.METER, UnitName.FOOT, (decimal)meters, logger, UnitSystem.USImperial);
            foreach (UnitConversionResult r in conversionResults)
            {
                if (r.ConversionType == UnitType.Length)
                {
                    return (double)r.TargetUnitAmount;
                }
            }

            throw new ArithmeticException("Failed unit conversion");
        }

        public static double ConvertCentimetersToFeet(double meters, ILogger logger)
        {
            List<UnitConversionResult> conversionResults = UnitConverter.Convert(UnitName.CENTIMETER, UnitName.FOOT, (decimal)meters, logger, UnitSystem.USImperial);
            foreach (UnitConversionResult r in conversionResults)
            {
                if (r.ConversionType == UnitType.Length)
                {
                    return (double)r.TargetUnitAmount;
                }
            }

            throw new ArithmeticException("Failed unit conversion");
        }

        public static double ConvertKilometersToMiles(double meters, ILogger logger)
        {
            List<UnitConversionResult> conversionResults = UnitConverter.Convert(UnitName.KILOMETER, UnitName.MILE, (decimal)meters, logger, UnitSystem.USImperial);
            foreach (UnitConversionResult r in conversionResults)
            {
                if (r.ConversionType == UnitType.Length)
                {
                    return (double)r.TargetUnitAmount;
                }
            }

            throw new ArithmeticException("Failed unit conversion");
        }

        public static double ConvertKilogramsToPounds(double kilograms, ILogger logger)
        {
            List<UnitConversionResult> conversionResults = UnitConverter.Convert(UnitName.KILOGRAM, UnitName.POUND, (decimal)kilograms, logger, UnitSystem.USImperial);
            foreach (UnitConversionResult r in conversionResults)
            {
                if (r.ConversionType == UnitType.Mass)
                {
                    return (double)r.TargetUnitAmount;
                }
            }

            throw new ArithmeticException("Failed unit conversion");
        }

        public static double ConvertKilogramsToStone(double kilograms, ILogger logger)
        {
            List<UnitConversionResult> conversionResults = UnitConverter.Convert(UnitName.KILOGRAM, UnitName.STONE, (decimal)kilograms, logger, UnitSystem.BritishImperial);
            foreach (UnitConversionResult r in conversionResults)
            {
                if (r.ConversionType == UnitType.Mass)
                {
                    return (double)r.TargetUnitAmount;
                }
            }

            throw new ArithmeticException("Failed unit conversion");
        }

        public static UnitSystem GetUnitSystemForLocale(string locale)
        {
            // Supported Fitbit locales:
            // en_AU fr_FR de_DE ja_JP en_NZ es_ES en_GB en_US

            if (string.Equals(locale, "en_US", StringComparison.OrdinalIgnoreCase))
            {
                return UnitSystem.USImperial;
            }
            // FIXME do any of these countries default to metric for anything else?
            else if (string.Equals(locale, "en_AU", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(locale, "en_GB", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(locale, "en_NZ", StringComparison.OrdinalIgnoreCase))
            {
                return UnitSystem.BritishImperial;
            }
            else
            {
                return UnitSystem.Metric;
            }
        }

        public static UnitSystem GetWeightUnitSystemForLocale(string locale)
        {
            if (string.Equals(locale, "en_US", StringComparison.OrdinalIgnoreCase))
            {
                return UnitSystem.USImperial;
            }
            // FIXME only en_GB uses stone for weight.....right?
            else if (string.Equals(locale, "en_GB", StringComparison.OrdinalIgnoreCase))
            {
                return UnitSystem.BritishImperial;
            }
            else
            {
                return UnitSystem.Metric;
            }
        }

        public static TimeResolutionInfo ResolveDate(FitbitUser fitbitUserProfile, QueryWithContext queryWithContext, IRealTimeProvider timeProvider)
        {
            DateTimeOffset userTimeUtc = timeProvider.Time;

            TimeResolutionInfo returnVal = new TimeResolutionInfo();
            returnVal.UserLocalTime = userTimeUtc.ToOffset(TimeSpan.FromMilliseconds(fitbitUserProfile.OffsetFromUTCMillis));
            returnVal.QueryTime = returnVal.UserLocalTime;

            // Parse the "date" slot to try and determine query time if different from local time
            SlotValue dateSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, Constants.SLOT_DATE);
            if (dateSlot != null)
            {
                TimexContext timeContext = new TimexContext()
                {
                    AmPmInferenceCutoff = 7,
                    IncludeCurrentTimeInPastOrFuture = false,
                    Normalization = Normalization.Past,
                    UseInference = true,
                    ReferenceDateTime = returnVal.UserLocalTime.LocalDateTime
                };

                IList<TimexMatch> timexes = dateSlot.GetTimeMatches(TemporalType.All, timeContext);
                if (timexes.Count > 0)
                {
                    ExtendedDateTime edt = timexes[0].ExtendedDateTime;
                    DateAndTime parsedTimex = TimexValue.Parse(edt.FormatValue(), edt.FormatType(), "0", edt.FormatComment(), edt.FormatFrequency(), edt.FormatQuantity(), edt.FormatMod()).AsDateAndTime();
                    SimpleDateTimeRange resolvedTime = parsedTimex.InterpretAsNaturalTimeRange();
                    if (resolvedTime != null)
                    {
                        returnVal.QueryTime = new DateTimeOffset(resolvedTime.Start, returnVal.UserLocalTime.Offset);
                    }
                }
            }

            return returnVal;
        }
    }
}
