using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Durandal.Common.NLP.Language
{
    public class BasicCultureInfoFactory : ICultureInfoFactory
    {
        private ILogger _logger;

        public BasicCultureInfoFactory(ILogger logger)
        {
            _logger = logger;
        }

        public CultureInfo GetCultureInfoForLocale(LanguageCode locale)
        {
            string cultureName = locale.ToBcp47Alpha2String();

            try
            {
                CultureInfo returnVal = new CultureInfo(cultureName);
                if (returnVal.EnglishName.Contains("Unknown Locale"))
                {
                    _logger.Log("Culture info factory does not recognize locale \"" + cultureName + "\"; this will likely lead to a degraded experience", LogLevel.Wrn);
                }

                return returnVal;
            }
            catch (CultureNotFoundException)
            {
                _logger.Log("Culture info not found for locale \"" + cultureName + "\"; falling back to invariant culture", LogLevel.Err);
            }
            
            return CultureInfo.InvariantCulture;
        }
    }
}
