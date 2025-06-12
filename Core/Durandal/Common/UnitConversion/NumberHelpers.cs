using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Durandal.Common.UnitConversion
{
    public static class NumberHelpers
    {
        private static readonly Regex TRAILING_ZERO_TRIMMER = new Regex("\\.0+$");

        public static string FormatNumber(decimal exactNumber, out decimal roundedValue, int sigFigs = 4)
        {
            // Calculate sig figs
            roundedValue = RoundToSigFigs(exactNumber, sigFigs);

            // Trim trailing zeroes from result string
            string stringValue = roundedValue.ToString();
            stringValue = StringUtils.RegexRemove(TRAILING_ZERO_TRIMMER, stringValue);

            if (stringValue.Contains("."))
            {
                stringValue = stringValue.TrimEnd('0');
            }

            return stringValue;
        }

        public static decimal RoundToSigFigs(decimal val, int figs)
        {
            if (figs < 1)
                throw new ArgumentException("Cannot round to less than 1 significant figure");

            // Convert the sign to positive, it just makes things simpler
            int sign = Math.Sign(val);
            val = Math.Abs(val);

            // Detect if we are "significantly" close to 0
            decimal zeroBound = (decimal)Math.Pow(10, 0 - figs);
            if (val < zeroBound)
            {
                return 0M;
            }

            // Iterate through digits to find the least significant figure,
            // constantly shifting digits to the left as we go
            decimal lowerBound = new decimal(Math.Pow(10, figs - 1));
            decimal upperBound = lowerBound * 10;
            int shift = 0;
            while (val < lowerBound)
            {
                shift--;
                val *= 10M;
            }
            while (val > upperBound)
            {
                shift++;
                val /= 10M;
            }

            // Everything beyond the decimal point is now considered insignificant, so round the digits and then undo the shift
            val = Math.Round(val);
            val = val * new decimal(Math.Pow(10, shift));

            // Reapply the sign
            return val * sign;
        }
    }
}
