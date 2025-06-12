using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.UnitConversion
{
    /// <summary>
    /// Hardcoded names for different measurement units
    /// </summary>
    public static class UnitName
    {
        // LENGTH
        public static readonly string METER = "METER";
        public static readonly string MILLIMETER = "MILLIMETER";
        public static readonly string CENTIMETER = "CENTIMETER";
        public static readonly string KILOMETER = "KILOMETER";
        public static readonly string MILE = "MILE";
        public static readonly string FOOT = "FOOT";
        public static readonly string INCH = "INCH";
        public static readonly string YARD = "YARD";

        // TEMPERATURE
        public static readonly string FAHRENHEIT = "FAHRENHEIT";
        public static readonly string CELSIUS = "CELSIUS";
        public static readonly string KELVIN = "KELVIN";

        // MASS
        public static readonly string GRAM = "GRAM";
        public static readonly string KILOGRAM = "KILOGRAM";
        public static readonly string POUND = "POUND";
        public static readonly string MILLIGRAM = "MILLIGRAM";
        public static readonly string MASS_OUNCE = "MASS_OUNCE";
        public static readonly string STONE = "STONE";

        // VOLUME
        public static readonly string LITER = "LITER";
        public static readonly string MILLILITER = "MILLILITER";

        public static readonly string IMP_GALLON = "IMP_GALLON";
        public static readonly string IMP_FLUID_OUNCE = "IMP_FLUID_OUNCE";
        public static readonly string IMP_TEASPOON = "IMP_TEASPOON";
        public static readonly string IMP_TABLESPOON = "IMP_TABLESPOON";
        public static readonly string IMP_QUART = "IMP_QUART";
        public static readonly string IMP_PINT = "IMP_PINT";

        public static readonly string US_GALLON = "US_GALLON";
        public static readonly string US_FLUID_OUNCE = "US_FLUID_OUNCE";
        public static readonly string US_CUP = "US_CUP";
        public static readonly string US_TEASPOON = "US_TEASPOON";
        public static readonly string US_TABLESPOON = "US_TABLESPOON";
        public static readonly string US_QUART = "US_QUART";
        public static readonly string US_PINT = "US_PINT";

        // AREA
        public static readonly string SQUARE_METER = "SQUARE_METER";
        public static readonly string ACRE = "ACRE";
        public static readonly string HECTARE = "HECTARE";
        public static readonly string SQUARE_MILLIMETER = "SQUARE_MILLIMETER";
        public static readonly string SQUARE_CENTIMETER = "SQUARE_CENTIMETER";
        public static readonly string SQUARE_KILOMETER = "SQUARE_KILOMETER";
        public static readonly string SQUARE_YARD = "SQUARE_YARD";
        public static readonly string SQUARE_MILE = "SQUARE_MILE";

        // TIME
        public static readonly string SECOND = "SECOND";
        public static readonly string MILLISECOND = "MILLISECOND";
        public static readonly string MINUTE = "MINUTE";
        public static readonly string HOUR = "HOUR";
        public static readonly string DAY = "DAY";
        public static readonly string WEEK = "WEEK";
        public static readonly string MONTH = "MONTH";
        public static readonly string YEAR = "YEAR";
        public static readonly string DECADE = "DECADE";
        public static readonly string CENTURY = "CENTURY";

        // ANGLE
        public static readonly string REVOLUTION = "REVOLUTION";
        public static readonly string DEGREE = "DEGREE";
        public static readonly string RADIAN = "RADIAN";
        public static readonly string GRADIAN = "GRADIAN";
        public static readonly string ARC_MINUTE = "ARC_MINUTE";
        public static readonly string ARC_SECOND = "ARC_SECOND";

        // FORCE
        public static readonly string NEWTON = "NEWTON";
        public static readonly string DYNE = "DYNE";
        //public static readonly string POUND = "POUND"; // already specified earlier

        // PRESSURE
        public static readonly string PSI = "PSI";
        public static readonly string PASCAL = "PASCAL";
        public static readonly string BARYE = "BARYE";
        public static readonly string TORR = "TORR";
        public static readonly string STANDARD_ATMOSPHERE = "STANDARD_ATMOSPHERE";
        public static readonly string TECHNICAL_ATMOSPHERE = "TECHNICAL_ATMOSPHERE";
        public static readonly string BAR = "BAR";
        public static readonly string MILLIBAR = "MILLIBAR";
        public static readonly string INCHES_MERCURY = "INCHES_MERCURY";

        // ENERGY
        public static readonly string KILOJOULE = "KILOJOULE";
        public static readonly string JOULE = "JOULE";
        public static readonly string CALORIE = "CALORIE";
        public static readonly string KILOCALORIE = "KILOCALORIE";
        public static readonly string KILOWATT_HOUR = "KILOWATT_HOUR";
        public static readonly string BTU = "BTU";

        // POWER
        public static readonly string KILOWATT = "KILOWATT";
        public static readonly string WATT = "WATT";
        public static readonly string MEGAWATT = "MEGAWATT";
        public static readonly string GIGAWATT = "GIGAWATT";
        public static readonly string HORSEPOWER = "HORSEPOWER";

        // SPEED
        public static readonly string KILOMETER_PER_HOUR = "KILOMETER_PER_HOUR";
        public static readonly string MILLIMETER_PER_HOUR = "MILLIMETER_PER_HOUR";
        public static readonly string METER_PER_SECOND = "METER_PER_SECOND";
        public static readonly string MILE_PER_HOUR = "MILE_PER_HOUR";
        public static readonly string FOOT_PER_SECOND = "FOOT_PER_SECOND";
        public static readonly string KNOT = "KNOT";
        public static readonly string MACH = "MACH";


        /// <summary>
        /// Represents the ambiguity between US and British gallons
        /// </summary>
        public static readonly string AMBIG_ENG_GALLON = "AMBIG_ENG_GALLON";

        /// <summary>
        /// Represents the ambiguity between US and British pints
        /// </summary>
        public static readonly string AMBIG_ENG_PINT = "AMBIG_ENG_PINT";

        /// <summary>
        /// Represents the ambiguity between US and British teaspoons
        /// </summary>
        public static readonly string AMBIG_ENG_TSP = "AMBIG_ENG_TSP";

        /// <summary>
        /// Represents the ambiguity between US and British tablespoons
        /// </summary>
        public static readonly string AMBIG_ENG_TBSP = "AMBIG_ENG_TBSP";

        /// <summary>
        /// Represents the ambiguity between US and British quarts
        /// </summary>
        public static readonly string AMBIG_ENG_QUART = "AMBIG_ENG_QUART";

        /// <summary>
        /// Represents the English-language ambiguity between fluid and mass ounces
        /// </summary>
        public static readonly string AMBIG_ENG_OUNCE = "AMBIG_ENG_OUNCE";

        /// <summary>
        /// Represents the English-language ambiguity between Celsius "C" and abbreviated cups "C"
        /// </summary>
        public static readonly string AMBIG_ENG_CELSIUS = "AMBIG_ENG_CELSIUS";
    }
}
