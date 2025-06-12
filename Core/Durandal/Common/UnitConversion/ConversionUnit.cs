using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.UnitConversion
{
    public class ConversionUnit
    {
        /// <summary>
        /// The name, or one name, that this unit can be referred to by. Should be one of the UnitName constants
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The type of unit this is (e.g. length, area, mass, etc.)
        /// </summary>
        public UnitType Type { get; private set; }

        /// <summary>
        /// The value of the unit relative to the "base" for that unit type. This is the conversion factor
        /// </summary>
        public decimal Value { get; private set; }

        /// <summary>
        /// Any flags that might affect the processing of this unit - normally used to indicate ambiguity
        /// </summary>
        public UnitFlags Flags { get; private set; }

        /// <summary>
        /// The unit system or systems in which this unit is used
        /// </summary>
        public UnitSystem Systems { get; private set; }

        /// <summary>
        /// The basis used for the relative value of this unit.
        /// For example, if this unit was for Centimeters, the value would be 0.01 and the basis would Meters
        /// By attempting to keep conversions within the same basis, we can increase the numerical stability of conversions.
        /// </summary>
        public UnitBasis Basis { get; private set; }

        /// <summary>
        /// If this unit has an "ambiguous" name, this will store the actual intended name of the unit.
        /// For example, the unit "C" will match two entries for "CUP" and "CELSIUS" - this field is used to distinguish the two.
        /// </summary>
        public string UnambiguousUnitName { get; private set; }

        private ConversionUnit(UnitType type, string name, decimal value, UnitBasis valueBasis, UnitSystem systems = UnitSystem.Unspecified, UnitFlags flags = UnitFlags.None, string intendedUnitName = null)
        {
            Name = name;
            Type = type;
            Value = value;
            Flags = flags;
            Systems = systems;
            Basis = valueBasis;
            UnambiguousUnitName = intendedUnitName ?? name;
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            ConversionUnit other = obj as ConversionUnit;

            if (other == null)
            {
                return false;
            }

            return string.Equals(Name, other.Name) &&
                Type == other.Type &&
                Flags == other.Flags &&
                Systems == other.Systems;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() + Type.GetHashCode() + Flags.GetHashCode() + Systems.GetHashCode();
        }

        internal static bool IsUnitKnown(string unitName)
        {
            if (string.IsNullOrEmpty(unitName))
            {
                return false;
            }

            foreach (ConversionUnit c in UnitConversions)
            {
                if (c.Name.Equals(unitName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Correctly resolve ambiguous units manually.
        /// Fun stuff: Since there are even more complex cases where the result of this can be ambiguous, we pass further ambiguity along in the form
        /// of a list of results!
        /// </summary>
        /// <param name="sourceUnit"></param>
        /// <param name="preferredUnitSystem"></param>
        /// <param name="conversionType"></param>
        /// <returns>A list of one or more resolved units</returns>
        internal static List<ConversionUnit> ResolveAmbiguousUnit(ConversionUnit sourceUnit, UnitSystem preferredUnitSystem, UnitType conversionType)
        {
            List<ConversionUnit> returnVal = new List<ConversionUnit>();
            if (!sourceUnit.Flags.HasFlag(UnitFlags.AmbiguousName) || string.Equals(sourceUnit.Name, sourceUnit.UnambiguousUnitName))
            {
                // need to potentially return a list of multiple units with the same name but different bases
                foreach (ConversionUnit unit in UnitConversions)
                {
                    if (string.Equals(sourceUnit.UnambiguousUnitName, unit.UnambiguousUnitName))
                    {
                        returnVal.Add(unit);
                    }
                }

                return returnVal;
            }

            HashSet<string> potentials = new HashSet<string>();
            foreach (ConversionUnit unit in UnitConversions)
            {
                if (string.Equals(sourceUnit.Name, unit.Name) && conversionType == unit.Type && !potentials.Contains(unit.UnambiguousUnitName))
                {
                    potentials.Add(unit.UnambiguousUnitName);
                }
            }

            foreach (ConversionUnit unit in UnitConversions)
            {
                if (potentials.Contains(unit.Name) && unit.Type == conversionType && unit.Systems.HasFlag(preferredUnitSystem))
                {
                    returnVal.Add(unit);
                }
            }

            if (returnVal.Count > 0)
            {
                return returnVal;
            }

            // Fallback if we couldn't find a unit in our preferred unit system
            foreach (ConversionUnit unit in UnitConversions)
            {
                if (potentials.Contains(unit.Name) && unit.Type == conversionType)
                {
                    returnVal.Add(unit);
                }
            }

            if (returnVal.Count == 0)
            {
                returnVal.Add(sourceUnit);
            }

            return returnVal;
        }
        
        internal static Dictionary<Tuple<UnitBasis, UnitBasis>, decimal> BasisConversions =
            new Dictionary<Tuple<UnitBasis, UnitBasis>, decimal>()
            {
                { new Tuple<UnitBasis, UnitBasis>(UnitBasis.Meter, UnitBasis.Inch), 39.3700787M },
                { new Tuple<UnitBasis, UnitBasis>(UnitBasis.Gram, UnitBasis.MassOunce), 0.03527396M },
                { new Tuple<UnitBasis, UnitBasis>(UnitBasis.Liter, UnitBasis.ImpFluidOunce), 35.1950642M },
                { new Tuple<UnitBasis, UnitBasis>(UnitBasis.Liter, UnitBasis.UsFluidOunce), 33.8140227M },
                { new Tuple<UnitBasis, UnitBasis>(UnitBasis.ImpFluidOunce, UnitBasis.UsFluidOunce), 0.96076036M },
                { new Tuple<UnitBasis, UnitBasis>(UnitBasis.SquareMeter, UnitBasis.SquareYard), 1.19599005M },
                { new Tuple<UnitBasis, UnitBasis>(UnitBasis.Month, UnitBasis.Second), 2628000M },
                { new Tuple<UnitBasis, UnitBasis>(UnitBasis.Day, UnitBasis.Second), 86400M },
                { new Tuple<UnitBasis, UnitBasis>(UnitBasis.Month, UnitBasis.Day), 30M },
                { new Tuple<UnitBasis, UnitBasis>(UnitBasis.Pascal, UnitBasis.Psi), 0.00014503774M },
                { new Tuple<UnitBasis, UnitBasis>(UnitBasis.MeterPerSecond, UnitBasis.FootPerSecond), 3.2808399M },
            };

        internal static ConversionUnit[] UnitConversions =
            new ConversionUnit[]
            {
                new ConversionUnit(UnitType.Length, UnitName.METER, 1M, UnitBasis.Meter, UnitSystem.Metric),
                new ConversionUnit(UnitType.Length, UnitName.MILLIMETER, 0.001M, UnitBasis.Meter, UnitSystem.Metric),
                new ConversionUnit(UnitType.Length, UnitName.CENTIMETER, 0.01M, UnitBasis.Meter, UnitSystem.Metric),
                new ConversionUnit(UnitType.Length, UnitName.KILOMETER, 1000M, UnitBasis.Meter, UnitSystem.Metric),
                new ConversionUnit(UnitType.Length, UnitName.INCH, 1M, UnitBasis.Inch, UnitSystem.Imperial),
                new ConversionUnit(UnitType.Length, UnitName.MILE, 63360M, UnitBasis.Inch, UnitSystem.Imperial),
                new ConversionUnit(UnitType.Length, UnitName.FOOT, 12M, UnitBasis.Inch, UnitSystem.Imperial),
                new ConversionUnit(UnitType.Length, UnitName.YARD, 36M, UnitBasis.Inch, UnitSystem.Imperial),
                

                new ConversionUnit(UnitType.Temperature, UnitName.FAHRENHEIT, 0, UnitBasis.Unknown, UnitSystem.Imperial, UnitFlags.NonScalar),
                new ConversionUnit(UnitType.Temperature, UnitName.CELSIUS, 0, UnitBasis.Unknown, UnitSystem.Metric, UnitFlags.NonScalar),
                new ConversionUnit(UnitType.Temperature, UnitName.KELVIN, 0, UnitBasis.Unknown, UnitSystem.Unspecified, UnitFlags.NonScalar),
                

                new ConversionUnit(UnitType.Mass, UnitName.GRAM, 1M, UnitBasis.Gram, UnitSystem.Metric),
                new ConversionUnit(UnitType.Mass, UnitName.KILOGRAM, 1000, UnitBasis.Gram, UnitSystem.Metric),
                new ConversionUnit(UnitType.Mass, UnitName.MILLIGRAM, 0.001M, UnitBasis.Gram, UnitSystem.Metric),
                new ConversionUnit(UnitType.Mass, UnitName.MASS_OUNCE, 1M, UnitBasis.MassOunce, UnitSystem.Imperial),
                new ConversionUnit(UnitType.Mass, UnitName.POUND, 16M, UnitBasis.MassOunce, UnitSystem.Imperial, UnitFlags.AmbiguousUsage), // "pound" is technically a force but is used as a mass
                new ConversionUnit(UnitType.Mass, UnitName.STONE, 224M, UnitBasis.MassOunce, UnitSystem.BritishImperial),
                

                new ConversionUnit(UnitType.Volume, UnitName.LITER, 1M, UnitBasis.Liter, UnitSystem.Metric),
                new ConversionUnit(UnitType.Volume, UnitName.MILLILITER, 0.001M, UnitBasis.Liter, UnitSystem.Metric),
                // Standard Imperial metrics
                new ConversionUnit(UnitType.Volume, UnitName.IMP_FLUID_OUNCE, 1M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial),
                new ConversionUnit(UnitType.Volume, UnitName.IMP_GALLON, 160M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial),
                new ConversionUnit(UnitType.Volume, UnitName.IMP_TEASPOON, 1/4.8M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial),
                new ConversionUnit(UnitType.Volume, UnitName.IMP_TABLESPOON, 0.625M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial),
                new ConversionUnit(UnitType.Volume, UnitName.IMP_QUART, 40M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial),
                new ConversionUnit(UnitType.Volume, UnitName.IMP_PINT, 20M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial),
                // These units are specific US variants of Imperial metrics
                new ConversionUnit(UnitType.Volume, UnitName.US_FLUID_OUNCE, 1M, UnitBasis.UsFluidOunce, UnitSystem.USImperial),
                new ConversionUnit(UnitType.Volume, UnitName.US_GALLON, 128M, UnitBasis.UsFluidOunce, UnitSystem.USImperial),
                new ConversionUnit(UnitType.Volume, UnitName.US_CUP, 8M, UnitBasis.UsFluidOunce, UnitSystem.USImperial),
                new ConversionUnit(UnitType.Volume, UnitName.US_TEASPOON, 1/8M, UnitBasis.UsFluidOunce, UnitSystem.USImperial),
                new ConversionUnit(UnitType.Volume, UnitName.US_TABLESPOON, 0.5M, UnitBasis.UsFluidOunce, UnitSystem.USImperial),
                new ConversionUnit(UnitType.Volume, UnitName.US_QUART, 32M, UnitBasis.UsFluidOunce, UnitSystem.USImperial),
                new ConversionUnit(UnitType.Volume, UnitName.US_PINT, 16M, UnitBasis.UsFluidOunce, UnitSystem.USImperial),
                

                new ConversionUnit(UnitType.Area, UnitName.SQUARE_METER, 1M, UnitBasis.SquareMeter, UnitSystem.Metric),
                new ConversionUnit(UnitType.Area, UnitName.HECTARE, 10000M, UnitBasis.SquareMeter, UnitSystem.Metric),
                new ConversionUnit(UnitType.Area, UnitName.SQUARE_MILLIMETER, 0.000001M, UnitBasis.SquareMeter, UnitSystem.Metric),
                new ConversionUnit(UnitType.Area, UnitName.SQUARE_CENTIMETER, 0.0001M, UnitBasis.SquareMeter, UnitSystem.Metric),
                new ConversionUnit(UnitType.Area, UnitName.SQUARE_KILOMETER, 1000000M, UnitBasis.SquareMeter, UnitSystem.Metric),
                new ConversionUnit(UnitType.Area, UnitName.SQUARE_YARD, 1M, UnitBasis.SquareYard, UnitSystem.Imperial),
                new ConversionUnit(UnitType.Area, UnitName.ACRE, 4840M, UnitBasis.SquareYard, UnitSystem.Imperial),
                new ConversionUnit(UnitType.Area, UnitName.SQUARE_MILE, 3097600M, UnitBasis.SquareYard, UnitSystem.Imperial),
                

                new ConversionUnit(UnitType.Time, UnitName.SECOND, 1M, UnitBasis.Second, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Time, UnitName.MILLISECOND, 0.001M, UnitBasis.Second, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Time, UnitName.MINUTE, 60M, UnitBasis.Second, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Time, UnitName.HOUR, 3600M, UnitBasis.Second, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Time, UnitName.DAY, 86400M, UnitBasis.Second, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Time, UnitName.WEEK, 604800M, UnitBasis.Second, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Time, UnitName.MONTH, 1M, UnitBasis.Month, UnitSystem.Unspecified, UnitFlags.TimeVariance),
                new ConversionUnit(UnitType.Time, UnitName.YEAR, 12M, UnitBasis.Month, UnitSystem.Unspecified, UnitFlags.TimeVariance),
                new ConversionUnit(UnitType.Time, UnitName.DECADE, 120M, UnitBasis.Month, UnitSystem.Unspecified, UnitFlags.TimeVariance),
                new ConversionUnit(UnitType.Time, UnitName.CENTURY, 1200M, UnitBasis.Month, UnitSystem.Unspecified, UnitFlags.TimeVariance),
                // very selective case for returning # of days in a year
                new ConversionUnit(UnitType.Time, UnitName.DAY, 1M, UnitBasis.Day, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Time, UnitName.YEAR, 365M, UnitBasis.Day, UnitSystem.Unspecified, UnitFlags.TimeVariance),


                new ConversionUnit(UnitType.Angle, UnitName.ARC_SECOND, 1M, UnitBasis.ArcSecond, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Angle, UnitName.ARC_MINUTE, 60M, UnitBasis.ArcSecond, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Angle, UnitName.DEGREE, 3600M, UnitBasis.ArcSecond, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Angle, UnitName.GRADIAN, 3240M, UnitBasis.ArcSecond, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Angle, UnitName.REVOLUTION, 1296000M, UnitBasis.ArcSecond, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Angle, UnitName.RADIAN, 1296000M / (decimal)(Math.PI * 2), UnitBasis.ArcSecond, UnitSystem.Unspecified, UnitFlags.NonExactBasis),
                

                new ConversionUnit(UnitType.Force, UnitName.NEWTON, 1M, UnitBasis.Newton, UnitSystem.Metric),
                new ConversionUnit(UnitType.Force, UnitName.DYNE, 0.00001M, UnitBasis.Newton, UnitSystem.Metric),
                new ConversionUnit(UnitType.Force, UnitName.POUND, 4.44822162M, UnitBasis.Newton, UnitSystem.Imperial, UnitFlags.AmbiguousUsage | UnitFlags.NonExactBasis),


                new ConversionUnit(UnitType.Pressure, UnitName.PASCAL, 1M, UnitBasis.Pascal, UnitSystem.Metric),
                new ConversionUnit(UnitType.Pressure, UnitName.BAR, 100000M, UnitBasis.Pascal, UnitSystem.Metric),
                new ConversionUnit(UnitType.Pressure, UnitName.MILLIBAR, 100M, UnitBasis.Pascal, UnitSystem.Metric),
                new ConversionUnit(UnitType.Pressure, UnitName.BARYE, 10M, UnitBasis.Pascal, UnitSystem.Metric),
                new ConversionUnit(UnitType.Pressure, UnitName.STANDARD_ATMOSPHERE, 101325M, UnitBasis.Pascal, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Pressure, UnitName.TECHNICAL_ATMOSPHERE, 98066.5M, UnitBasis.Pascal, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Pressure, UnitName.PSI, 1M, UnitBasis.Psi, UnitSystem.Imperial),
                new ConversionUnit(UnitType.Pressure, UnitName.TORR, 0.01933677M, UnitBasis.Psi, UnitSystem.Unspecified, UnitFlags.NonExactBasis),
                new ConversionUnit(UnitType.Pressure, UnitName.INCHES_MERCURY, 0.4911541522266M, UnitBasis.Psi, UnitSystem.Unspecified, UnitFlags.NonExactBasis),


                new ConversionUnit(UnitType.Energy, UnitName.JOULE, 1M, UnitBasis.Joule, UnitSystem.Metric),
                new ConversionUnit(UnitType.Energy, UnitName.KILOJOULE, 1000M, UnitBasis.Joule, UnitSystem.Metric),
                new ConversionUnit(UnitType.Energy, UnitName.CALORIE, 4.184M, UnitBasis.Joule, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Energy, UnitName.KILOCALORIE, 4184M, UnitBasis.Joule, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Energy, UnitName.KILOWATT_HOUR, 3600000M, UnitBasis.Joule, UnitSystem.Metric),
                new ConversionUnit(UnitType.Energy, UnitName.BTU, 1055.05585M, UnitBasis.Joule, UnitSystem.Imperial, UnitFlags.NonExactBasis),


                new ConversionUnit(UnitType.Power, UnitName.WATT, 1M, UnitBasis.Watt, UnitSystem.Metric),
                new ConversionUnit(UnitType.Power, UnitName.KILOWATT, 1000M, UnitBasis.Watt, UnitSystem.Metric),
                new ConversionUnit(UnitType.Power, UnitName.MEGAWATT, 1000000M, UnitBasis.Watt, UnitSystem.Metric),
                new ConversionUnit(UnitType.Power, UnitName.GIGAWATT, 1000000000M, UnitBasis.Watt, UnitSystem.Metric),
                new ConversionUnit(UnitType.Power, UnitName.HORSEPOWER, 745.699872M, UnitBasis.Watt, UnitSystem.Unspecified, UnitFlags.NonExactBasis),


                new ConversionUnit(UnitType.Speed, UnitName.METER_PER_SECOND, 1M, UnitBasis.MeterPerSecond, UnitSystem.Metric),
                new ConversionUnit(UnitType.Speed, UnitName.KILOMETER_PER_HOUR, 1/3.6M, UnitBasis.MeterPerSecond, UnitSystem.Metric),
                new ConversionUnit(UnitType.Speed, UnitName.MILLIMETER_PER_HOUR, 1/3600000M, UnitBasis.MeterPerSecond, UnitSystem.Metric),
                new ConversionUnit(UnitType.Speed, UnitName.FOOT_PER_SECOND, 1M, UnitBasis.FootPerSecond, UnitSystem.Unspecified),
                new ConversionUnit(UnitType.Speed, UnitName.MILE_PER_HOUR, 5280M / 3600M, UnitBasis.FootPerSecond, UnitSystem.Imperial),
                new ConversionUnit(UnitType.Speed, UnitName.KNOT, 0.51444444M, UnitBasis.MeterPerSecond, UnitSystem.Unspecified, UnitFlags.NonExactBasis),
                new ConversionUnit(UnitType.Speed, UnitName.MACH, 343M, UnitBasis.MeterPerSecond, UnitSystem.Unspecified),

                /////////////// Ambiguous entries go here ////////////////////

                // English speakers who say "pint", "gallon", "quart" may ambigously reference either US or British measurements
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_TSP, 1/8M, UnitBasis.UsFluidOunce, UnitSystem.USImperial, UnitFlags.AmbiguousName, UnitName.US_TEASPOON),
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_TSP, 1/4.8M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial, UnitFlags.AmbiguousName, UnitName.IMP_TEASPOON),
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_TBSP, 0.5M, UnitBasis.UsFluidOunce, UnitSystem.USImperial, UnitFlags.AmbiguousName, UnitName.US_TABLESPOON),
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_TBSP, 0.625M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial, UnitFlags.AmbiguousName, UnitName.IMP_TABLESPOON),
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_PINT, 16M, UnitBasis.UsFluidOunce, UnitSystem.USImperial, UnitFlags.AmbiguousName, UnitName.US_PINT),
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_PINT, 20M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial, UnitFlags.AmbiguousName, UnitName.IMP_PINT),
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_QUART, 32M, UnitBasis.UsFluidOunce, UnitSystem.USImperial, UnitFlags.AmbiguousName, UnitName.US_QUART),
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_QUART, 40M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial, UnitFlags.AmbiguousName, UnitName.IMP_QUART),
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_GALLON, 128M, UnitBasis.UsFluidOunce, UnitSystem.USImperial, UnitFlags.AmbiguousName, UnitName.US_GALLON),
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_GALLON, 160M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial, UnitFlags.AmbiguousName, UnitName.IMP_GALLON),
                
                // Ounces are even more ambiguous
                new ConversionUnit(UnitType.Mass, UnitName.AMBIG_ENG_OUNCE, 1M, UnitBasis.MassOunce, UnitSystem.Imperial, UnitFlags.AmbiguousName, UnitName.MASS_OUNCE),
                // Ambiguous volumetric ounces has another layer of ambiguity - US or UK system. So we make entries for both and assign different unit systems to them
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_OUNCE, 1M, UnitBasis.UsFluidOunce, UnitSystem.USImperial, UnitFlags.AmbiguousName, UnitName.US_FLUID_OUNCE),
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_OUNCE, 1M, UnitBasis.ImpFluidOunce, UnitSystem.BritishImperial, UnitFlags.AmbiguousName, UnitName.IMP_FLUID_OUNCE),
                

                // Ambiguous "C" can be interpreted as either cups or celsius
                new ConversionUnit(UnitType.Temperature, UnitName.AMBIG_ENG_CELSIUS, 0, UnitBasis.Unknown, UnitSystem.Metric, UnitFlags.AmbiguousName | UnitFlags.NonScalar, UnitName.CELSIUS),
                new ConversionUnit(UnitType.Volume, UnitName.AMBIG_ENG_CELSIUS, 8M, UnitBasis.UsFluidOunce, UnitSystem.USImperial, UnitFlags.AmbiguousName, UnitName.US_CUP),
            };
    }
}
