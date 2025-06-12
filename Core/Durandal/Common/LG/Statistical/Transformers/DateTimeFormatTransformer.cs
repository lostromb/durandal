using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.Logger;
using System.Globalization;
using Durandal.Common.NLP.Language;
using Durandal.Common.NLP;

namespace Durandal.Common.LG.Statistical.Transformers
{
    public class DateTimeFormatTransformer : ISlotTransformer
    {
        private string _formatString;

        public DateTimeFormatTransformer(string formatString)
        {
            _formatString = formatString;
        }

        public string Name
        {
            get
            {
                return "DateTimeFormat";
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

            // Parse the input string as a datetime
            DateTime time;
            if (input is DateTime)
            {
                logger.Log("A DateTime input was passed to DateTimeFormat transformer. This can cause problems with timezones, so please use DateTimeOffset if possible", LogLevel.Wrn);
                time = ((DateTime)input);
            }
            else if (input is DateTimeOffset)
            {
                time = ((DateTimeOffset)input).DateTime;
            }
            else if (input is string)
            {
                if (!DateTime.TryParse((string)input, out time))
                {
                    // Log an error and echo the input on failure
                    logger.Log("Could not parse the slot value \"" + input + "\" as a datetime! (Use ISO without timezone if possible)", LogLevel.Err);
                    return input.ToString();
                }
            }
            else
            {
                logger.Log("The input \"" + input.ToString() + "\" is not in any recognizable date/time format", LogLevel.Err);
                return input.ToString();
            }

            try
            {
                return time.ToString(_formatString, culture);
            }
            catch (Exception e)
            {
                logger.Log("Bad format string \"" + _formatString + "\" given to DateTimeFormatTransformer", LogLevel.Err);
                logger.Log(e, LogLevel.Err);
                return input.ToString();
            }
        }
    }
}
