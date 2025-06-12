using Durandal.Common.Utils;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Durandal.Common.Collections;

namespace Durandal.Common.UnitConversion
{
    /// <summary>
    /// Performs mathematical conversion between physical units of measurement e.g. distance, volume, pressure, etc.
    /// </summary>
    public static class UnitConverter
    {
        /// <summary>
        /// Converts from a target unit to a source unit
        /// </summary>
        /// <param name="sourceUnitName">A string representing a canonicalized source unit, such as "MILLIMETER"</param>
        /// <param name="targetUnitName">A string representing a canonicalized target unit, such as "METER". Can also be a measurement system such as "METRIC" or "IMPERIAL"</param>
        /// <param name="amountOfSource">The amount of the source units to convert.</param>
        /// <param name="logger">A logger for the operation (for emitting warnings, etc.)</param>
        /// <param name="preferredUnitSystem">The unit system that is preferred in the current locale (not required but helps resolve ambiguous cases)</param>
        /// <returns>A unit conversion result</returns>
        public static List<UnitConversionResult> Convert(string sourceUnitName, string targetUnitName, decimal amountOfSource, ILogger logger = null, UnitSystem preferredUnitSystem = UnitSystem.Unspecified)
        {
            if (string.IsNullOrEmpty(sourceUnitName))
            {
                throw new ArgumentNullException("sourceUnit");
            }

            if (string.IsNullOrEmpty(targetUnitName))
            {
                throw new ArgumentNullException("targetUnit");
            }

            if (logger == null)
            {
                logger = NullLogger.Singleton;
            }
            
            List<UnitConversionResult> results = new List<UnitConversionResult>();

            HashSet<UnitType> possibleSourceTypes = new HashSet<UnitType>();
            HashSet<UnitType> possibleTargetTypes = new HashSet<UnitType>();
            HashSet<string> conversionsAlreadyPerformed = new HashSet<string>();
            
            // Detect if we are converting between units (i.e. feet to inches) or between systems (i.e. feet to metric)
            UnitSystem targetSystem = UnitSystem.Unspecified;
            if (string.Equals(targetUnitName, UnitSystemName.METRIC))
            {
                targetSystem = UnitSystem.Metric;
            }
            if (string.Equals(targetUnitName, UnitSystemName.BRITISH_IMPERIAL))
            {
                targetSystem = UnitSystem.BritishImperial;
            }
            if (string.Equals(targetUnitName, UnitSystemName.US_IMPERIAL))
            {
                targetSystem = UnitSystem.USImperial;
            }
            if (string.Equals(targetUnitName, UnitSystemName.IMPERIAL))
            {
                if (preferredUnitSystem == UnitSystem.USImperial || preferredUnitSystem == UnitSystem.BritishImperial)
                {
                    logger.Log("Converting to \"imperial\" units is ambiguous, falling back to preferred system " + preferredUnitSystem, LogLevel.Wrn);
                    targetSystem = preferredUnitSystem;
                }
                else
                {
                    // This emits a warning because we are making locale assumptions
                    logger.Log("Converting to \"imperial\" units is ambiguous - please specify either US or British variants", LogLevel.Wrn);
                    targetSystem = UnitSystem.Imperial;
                }
            }

            // Find out what type of conversion we want to perform (length, weight, etc.)
            foreach (ConversionUnit unit in ConversionUnit.UnitConversions)
            {
                if (unit.Name.Equals(sourceUnitName))
                {
                    if (!possibleSourceTypes.Contains(unit.Type))
                    {
                        possibleSourceTypes.Add(unit.Type);
                    }
                }

                if (targetSystem != UnitSystem.Unspecified)
                {
                    // Target unit is an entire measurement system rather than a specific unit
                    if ((targetSystem & unit.Systems) != 0 &&
                        //!unit.Flags.HasFlag(UnitFlags.AMBIGUOUS_NAME) &&
                        !possibleTargetTypes.Contains(unit.Type))
                    {
                        possibleTargetTypes.Add(unit.Type);
                    }
                }
                else
                {
                    // Target is a specific unit; try and find it
                    if (unit.Name.Equals(targetUnitName) &&
                        //!unit.Flags.HasFlag(UnitFlags.AMBIGUOUS_NAME) &&
                        !possibleTargetTypes.Contains(unit.Type))
                    {
                        possibleTargetTypes.Add(unit.Type);
                    }
                }
            }

            if (possibleSourceTypes.Count == 0)
            {
                logger.Log("Don't understand the source unit \"" + sourceUnitName + "\"", LogLevel.Err);
                return results;
            }

            if (possibleTargetTypes.Count == 0)
            {
                logger.Log("Don't understand the target unit \"" + targetUnitName + "\"", LogLevel.Err);
                return results;
            }

            possibleSourceTypes.IntersectWith(possibleTargetTypes);

            if (possibleSourceTypes.Count == 0)
            {
                // No results means these units can't be converted
                logger.Log("Incompatible units " + sourceUnitName + " -> " + targetUnitName + " given to unit converter", LogLevel.Wrn);
                return results;
            }

            foreach (UnitType conversionType in possibleSourceTypes)
            {
                // Now find exactly what the source and target units are
                ConversionUnit sourceUnit = null;
                ConversionUnit targetUnit = null;
                foreach (ConversionUnit unit in ConversionUnit.UnitConversions)
                {
                    if (unit.Type == conversionType && unit.Name.Equals(sourceUnitName))
                    {
                        if (sourceUnit != null)
                        {
                            if (unit.Flags.HasFlag(UnitFlags.AmbiguousName) &&
                                preferredUnitSystem == UnitSystem.Unspecified)
                            {
                                logger.Log("Passing forward ambiguity of source unit " + unit.Name + " because there is no preferred unit system set");
                                sourceUnit = unit;
                            }
                        }
                        else
                        {
                            sourceUnit = unit;
                        }
                    }
                    if (unit.Type == conversionType && unit.Name.Equals(targetUnitName))
                    {
                        if (targetUnit != null)
                        {
                            if (unit.Flags.HasFlag(UnitFlags.AmbiguousName) &&
                                preferredUnitSystem == UnitSystem.Unspecified)
                            {
                                logger.Log("Passing forward ambiguity of target unit " + unit.Name + " because there is no preferred unit system set");
                                targetUnit = unit;
                            }
                        }
                        else
                        {
                            targetUnit = unit;
                        }
                    }
                }

                if (targetUnit == null && targetSystem != UnitSystem.Unspecified)
                {
                    // Handle conversion between different systems here.
                    PerformConversionBetweenSystems(sourceUnit, targetSystem, amountOfSource, conversionType, logger, results, conversionsAlreadyPerformed, preferredUnitSystem);
                }
                else if (sourceUnit == null || targetUnit == null)
                {
                    // This should never happen, but catch anyway
                    logger.Log("Could not find conversion parameters for the given units " + sourceUnitName + "-> " + targetUnitName, LogLevel.Err);
                    return results;
                }
                else
                {
                    try
                    {
                        List<ConversionUnit> unambiguousSourceUnits = ConversionUnit.ResolveAmbiguousUnit(sourceUnit, preferredUnitSystem, conversionType);
                        List<ConversionUnit> unambiguousTargetUnits = ConversionUnit.ResolveAmbiguousUnit(targetUnit, preferredUnitSystem, conversionType);
                        foreach (ConversionUnit unambiguousSourceUnit in unambiguousSourceUnits)
                        {
                            if (unambiguousSourceUnit.Flags.HasFlag(UnitFlags.AmbiguousName))
                            {
                                continue;
                            }
                            foreach (ConversionUnit unambiguousTargetUnit in unambiguousTargetUnits)
                            {
                                if (unambiguousTargetUnit.Flags.HasFlag(UnitFlags.AmbiguousName))
                                {
                                    continue;
                                }

                                string conversionName = unambiguousSourceUnit.Name + "-" + unambiguousTargetUnit.Name + ":" + unambiguousSourceUnit.Basis + "-" + unambiguousTargetUnit.Basis;
                                if (!conversionsAlreadyPerformed.Contains(conversionName))
                                {
                                    UnitConversionResult singleResult = PerformSingleConversion(unambiguousSourceUnit, unambiguousTargetUnit, amountOfSource, conversionType, logger);
                                    if (singleResult != null)
                                    {
                                        results.Add(singleResult);
                                    }

                                    conversionsAlreadyPerformed.Add(conversionName);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log("Caught an exception during unit conversion: " + e.Message, LogLevel.Err);
                    }
                }
            }

            // If we have multiple conversions of the same type but with different bases, favor the conversion performed within the same basis
            // (i.e. pick the one that is not flagged as approximate, if one is available)
            results = FavorNonApproximateConversions(results);

            return results;
        }

        private static List<UnitConversionResult> FavorNonApproximateConversions(List<UnitConversionResult> results)
        {
            if (results.Count > 1)
            {
                IDictionary<string, List<UnitConversionResult>> dedupedResults = new Dictionary<string, List<UnitConversionResult>>();
                foreach (var result in results)
                {
                    string name = result.SourceUnitName + "-" + result.TargetUnitName;
                    if (!dedupedResults.ContainsKey(name))
                    {
                        dedupedResults[name] = new List<UnitConversionResult>();
                    }

                    dedupedResults[name].Add(result);
                }

                results.Clear();
                foreach (List<UnitConversionResult> resultGroup in dedupedResults.Values)
                {
                    if (resultGroup.Count == 1)
                    {
                        results.FastAddRangeList(resultGroup);
                    }
                    else
                    {
                        bool foundNonApproxResult = false;
                        foreach (UnitConversionResult result in resultGroup)
                        {
                            if (!result.IsApproximate)
                            {
                                foundNonApproxResult = true;
                                results.Add(result);
                                break;
                            }
                        }

                        if (!foundNonApproxResult)
                        {
                            // No non-approximate conversion available, so try to find one that rounds to even units
                            bool foundEvenNumberResult = false;
                            foreach (UnitConversionResult result in resultGroup)
                            {
                                if (result.TargetUnitAmount == Math.Floor(result.TargetUnitAmount))
                                {
                                    foundEvenNumberResult = true;
                                    results.Add(result);
                                    break;
                                }
                            }

                            if (!foundEvenNumberResult)
                            {
                                // Finally, fall back to the first one that matches
                                results.Add(results[0]);
                            }
                        }
                    }
                }
            }

            return results;
        }

        private static void PerformConversionBetweenSystems(
            ConversionUnit sourceUnit,
            UnitSystem targetSystem,
            decimal amountOfSource,
            UnitType conversionType,
            ILogger logger,
            List<UnitConversionResult> results,
            HashSet<string> conversionsAlreadyPerformed,
            UnitSystem preferredUnitSystem)
        {
            // Find out all the potential units that are in the specified target system and unit type
            List<ConversionUnit> potentialUnits = new List<ConversionUnit>();
            foreach (ConversionUnit unit in ConversionUnit.UnitConversions)
            {
                if (unit.Flags.HasFlag(UnitFlags.TimeVariance) ||
                    unit.Flags.HasFlag(UnitFlags.AmbiguousName) ||
                    unit.Flags.HasFlag(UnitFlags.NonScalar) ||
                    unit.Type != conversionType)
                {
                    continue;
                }

                potentialUnits.Add(unit);
            }

            // Try all of them to see what the "prettiest" result is
            if (potentialUnits.Count == 0)
            {
                logger.Log("Could not find a suitable target unit for the conversion of " + sourceUnit.Name + " (" + conversionType + ") -> " + targetSystem.ToString(), LogLevel.Err);
                return;
            }

            List<UnitSystem> potentialTargetSystems = new List<UnitSystem>();
            // Pass forward "imperial" ambiguity here by doing multiple passes
            if (targetSystem == UnitSystem.Imperial)
            {
                if (preferredUnitSystem == UnitSystem.USImperial ||
                    preferredUnitSystem == UnitSystem.BritishImperial)
                {
                    potentialTargetSystems.Add(preferredUnitSystem);
                }
                else
                {
                    potentialTargetSystems.Add(UnitSystem.USImperial);
                    potentialTargetSystems.Add(UnitSystem.BritishImperial);
                }
            }
            else
            {
                potentialTargetSystems.Add(targetSystem);
            }

            bool conversionWasPerformed = false;
            foreach (UnitSystem targetSystemHyp in potentialTargetSystems)
            {
                UnitSystem unitSystemToResolveTo = preferredUnitSystem;
                if (unitSystemToResolveTo == UnitSystem.Unspecified)
                {
                    unitSystemToResolveTo = targetSystemHyp;
                }
                
                List<ConversionUnit> unambiguousSourceUnits = ConversionUnit.ResolveAmbiguousUnit(sourceUnit, unitSystemToResolveTo, conversionType);

                foreach (ConversionUnit unambiguousSourceUnit in unambiguousSourceUnits)
                {
                    decimal bestDecimalPlaces = decimal.MaxValue;
                    ConversionUnit targetUnit = null;
                    foreach (ConversionUnit potentialUnit in potentialUnits)
                    {
                        if (potentialUnit.Systems.HasFlag(targetSystemHyp) && !potentialUnit.Flags.HasFlag(UnitFlags.AmbiguousName))
                        {
                            // use the absolute deviation from 1.0 as the metric - we want to find the target unit that prints out the prettiest
                            decimal? amount = PerformLinearConversion(amountOfSource, unambiguousSourceUnit, potentialUnit, logger);
                            if (amount.HasValue)
                            {
                                if (amount.Value == 0)
                                {
                                    amount = 10000000000000M;
                                }
                                else if (amount.Value < 1)
                                {
                                    // using 100 here skews us in favor of showing larger numbers rather than smaller ones. So it would come out as "85 milliliters" rather than "0.085 liters"
                                    amount = 100 / amount.Value;
                                }

                                if (amount.Value <= bestDecimalPlaces)
                                {
                                    bestDecimalPlaces = amount.Value;
                                    targetUnit = potentialUnit;
                                }
                            }
                            else
                            {
                                logger.Log("Failed to convert from " + sourceUnit.Name + " -> " + potentialUnit.Name, LogLevel.Err);
                            }
                        }
                    }

                    if (targetUnit != null)
                    {
                        try
                        {
                            string conversionName = unambiguousSourceUnit.Name + "-" + targetUnit.Name;
                            if (!conversionsAlreadyPerformed.Contains(conversionName))
                            {
                                UnitConversionResult singleResult = PerformSingleConversion(unambiguousSourceUnit, targetUnit, amountOfSource, conversionType, logger);
                                if (singleResult != null)
                                {
                                    results.Add(singleResult);
                                    conversionWasPerformed = conversionWasPerformed || singleResult.ConversionWasRequired;
                                }

                                conversionsAlreadyPerformed.Add(conversionName);
                            }
                        }
                        catch (Exception e)
                        {
                            logger.Log("Caught an exception during unit conversion: " + e.Message, LogLevel.Err);
                        }
                    }
                }
            }

            // Detect if we did multiple redundant conversions (such as fluid ounces -> fluid ounces)
            if (conversionWasPerformed && results.Count > 0)
            {
                // If so, remove the redundant ones
                List<UnitConversionResult> filteredResults = new List<UnitConversionResult>();
                foreach (UnitConversionResult result in results)
                {
                    if (result.ConversionWasRequired)
                    {
                        filteredResults.Add(result);
                    }
                }

                results.Clear();
                results.FastAddRangeList(filteredResults);
            }
        }

        private static UnitConversionResult PerformSingleConversion(
            ConversionUnit sourceUnit,
            ConversionUnit targetUnit,
            decimal amountOfSource,
            UnitType conversionType,
            ILogger logger)
        {
            if (sourceUnit.Flags.HasFlag(UnitFlags.AmbiguousName))
            {
                throw new ArgumentException("Cannot convert an ambiguous source unit " + sourceUnit.Name + " to " + targetUnit.Name);
            }
            if (targetUnit.Flags.HasFlag(UnitFlags.AmbiguousName))
            {
                throw new ArgumentException("Cannot convert an ambiguous target unit " + targetUnit.Name + " from " + sourceUnit.Name);
            }

            UnitConversionResult result = new UnitConversionResult();
            result.SourceUnitAmount = amountOfSource;
            result.ConversionType = conversionType;
            result.SourceUnitName = sourceUnit.Name;
            result.TargetUnitName = targetUnit.Name;

            // Regular unit -> unit conversion happens here
            // See if this conversion triggers any special cases
            UnitFlags unifiedFlags = sourceUnit.Flags | targetUnit.Flags;
            if (sourceUnit.Flags.HasFlag(UnitFlags.TimeVariance) ^ targetUnit.Flags.HasFlag(UnitFlags.TimeVariance))
            {
                // time variance applies if we have crossed the "day -> month" threshold - we determine this by XORing the time variance flags
                result.HasTimeVariance = true;
            }
            if (sourceUnit.Basis != targetUnit.Basis ||
                unifiedFlags.HasFlag(UnitFlags.NonExactBasis))
            {
                result.IsApproximate = true;
            }

            if (unifiedFlags.HasFlag(UnitFlags.NonScalar))
            {
                // Handle nonscalar conversion (i.e. temperature conversion)
                if (conversionType == UnitType.Temperature)
                {
                    result.TargetUnitAmount = ConvertTemperature(sourceUnit, targetUnit, amountOfSource);
                }
                else
                {
                    logger.Log("Don't know how to convert nonscalar units " + sourceUnit.Name + " -> " + targetUnit.Name, LogLevel.Err);
                    return null;
                }
            }
            else
            {
                // Linear conversion which applies in most cases
                decimal? conversionResult = PerformLinearConversion(amountOfSource, sourceUnit, targetUnit, logger);
                if (conversionResult.HasValue)
                {
                    result.TargetUnitAmount = conversionResult.Value;
                }
                else
                {
                    logger.Log("Failed to convert from " + sourceUnit.Name + " -> " + targetUnit.Name, LogLevel.Err);
                    return null;
                }
            }

            // Time variance is also triggered if we have fractional portions of time, such as "0.7 years"
            if (unifiedFlags.HasFlag(UnitFlags.TimeVariance) && (result.TargetUnitAmount != Math.Floor(result.TargetUnitAmount)))
            {
                result.HasTimeVariance = true;
                result.IsApproximate = true;
            }
            
            decimal roundedVal;
            // TODO: Preserve the # of sig figs given in the input?
            result.TargetAmountString = NumberHelpers.FormatNumber(result.TargetUnitAmount, out roundedVal, 4);
            result.SourceAmountString = NumberHelpers.FormatNumber(result.SourceUnitAmount, out roundedVal, 4);
            result.ConversionWasRequired = !string.Equals(result.SourceUnitName, result.TargetUnitName);

            return result;
        }

        /// <summary>
        /// Does the logic of linear unit conversion which is what applies in most cases
        /// </summary>
        /// <param name="amountOfSource"></param>
        /// <param name="sourceUnit"></param>
        /// <param name="targetUnit"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static decimal? PerformLinearConversion(decimal amountOfSource, ConversionUnit sourceUnit, ConversionUnit targetUnit, ILogger logger)
        {
            if (sourceUnit.Basis == targetUnit.Basis)
            {
                return sourceUnit.Value * amountOfSource / targetUnit.Value;
            }
            else
            {
                // If units are not in the same basis, find the basis conversion and add it to the equation
                decimal? basisConversionFactor = GetBasisConversionFactor(sourceUnit.Basis, targetUnit.Basis);
                if (!basisConversionFactor.HasValue)
                {
                    logger.Log(string.Format("Cannot convert between basis measurements for {0} ({1}) -> {2} ({3})",
                        sourceUnit.Name, sourceUnit.Basis.ToString(), targetUnit.Name, targetUnit.Basis.ToString()), LogLevel.Err);
                    return null;
                }

                return sourceUnit.Value * amountOfSource / targetUnit.Value * basisConversionFactor.Value;
            }
        }

        private static decimal? GetBasisConversionFactor(UnitBasis from, UnitBasis to)
        {
            decimal basisConversionFactor;
            Tuple<UnitBasis, UnitBasis> key = new Tuple<UnitBasis, UnitBasis>(from, to);
            if (!ConversionUnit.BasisConversions.TryGetValue(key, out basisConversionFactor))
            {
                key = new Tuple<UnitBasis, UnitBasis>(to, from);
                if (!ConversionUnit.BasisConversions.TryGetValue(key, out basisConversionFactor))
                {
                    return null;
                }
                else
                {
                    basisConversionFactor = 1 / basisConversionFactor;
                }
            }

            return basisConversionFactor;
        }

        private static decimal ConvertTemperature(ConversionUnit source, ConversionUnit target, decimal value)
        {
            if (string.Equals(source.Name, target.Name))
            {
                return value;
            }

            // Convert source to kelvin
            decimal kelvin_deg = value;
            if (source.Name.Equals(UnitName.CELSIUS))
            {
                kelvin_deg = value + 273.15M;
            }
            else if (source.Name.Equals(UnitName.FAHRENHEIT))
            {
                kelvin_deg = (value + 459.67M) * 5 / 9;
            }

            // Then convert kelvin to target
            decimal result_deg = kelvin_deg;
            if (target.Name.Equals(UnitName.CELSIUS))
            {
                result_deg = kelvin_deg - 273.15M;
            }
            else if (target.Name.Equals(UnitName.FAHRENHEIT))
            {
                result_deg = (kelvin_deg * 1.8M) - 459.67M;
            }

            return result_deg;
        }
    }
}
