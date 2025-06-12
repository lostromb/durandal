using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.Logger;
using System.Globalization;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.LG.Statistical.Transformers
{
    public class NumberFormatTransformer : ISlotTransformer
    {
        private string _formatString;

        public NumberFormatTransformer(string formatString)
        {
            _formatString = formatString;
        }

        public string Name
        {
            get
            {
                return "NumberFormat";
            }
        }
        
        public string OriginalText
        {
            get
            {
                return Name + "(\"" + _formatString + "\")";
            }
        }

        public string Apply(object input, LanguageCode locale, ILogger logger, NLPTools nlTools, StatisticalLGPattern pattern)
        {
            CultureInfo culture = nlTools.CultureInfoFactory.GetCultureInfoForLocale(locale);

            // Parse the input string as a decimal
            decimal num;
            if (input is decimal)
            {
                num = ((decimal)input);
            }
            else if (input is float)
            {
                num = (decimal)(((float)input));
            }
            else if (input is double)
            {
                num = (decimal)(((double)input));
            }
            else if (input is int)
            {
                num = (decimal)(((int)input));
            }
            else if (input is string)
            {
                if (!decimal.TryParse(((string)input), NumberStyles.Number, CultureInfo.InvariantCulture, out num))
                {
                    // Log an error and echo the input on failure
                    logger.Log("Could not parse the slot value \"" + input + "\" as a number!", LogLevel.Err);
                    return input.ToString();
                }
            }

            else if (input is long)
            {
                num = (decimal)(((long)input));
            }
            else if (input is ulong)
            {
                num = (decimal)(((ulong)input));
            }
            else if (input is short)
            {
                num = (decimal)(((short)input));
            }
            else if (input is ushort)
            {
                num = (decimal)(((ushort)input));
            }
            else if (input is byte)
            {
                num = (decimal)(((byte)input));
            }
            else if (input is sbyte)
            {
                num = (decimal)(((sbyte)input));
            }
            else
            {
                logger.Log("The input \"" + input.ToString() + "\" is not in any recognizable number format", LogLevel.Err);
                return input.ToString();
            }

            try
            {
                return num.ToString(_formatString, culture);
            }
            catch (Exception e)
            {
                logger.Log("Bad format string \"" + _formatString + "\" given to NumberFormatTransformer", LogLevel.Err);
                logger.Log(e, LogLevel.Err);
                return input.ToString();
            }
        }
    }
}
