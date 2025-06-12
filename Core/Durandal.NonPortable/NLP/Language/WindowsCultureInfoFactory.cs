using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Language
{
    public class WindowsCultureInfoFactory : ICultureInfoFactory
    {
        public CultureInfo GetCultureInfoForLocale(LanguageCode locale)
        {
            CultureInfo returnVal = CultureInfo.GetCultureInfo(locale.ToBcp47Alpha2String());
            if (returnVal != null)
            {
                return returnVal;
            }

            // fallback using alpha-3 language code
            returnVal = CultureInfo.InvariantCulture;
            foreach (CultureInfo info in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                if (string.Equals(locale.Iso639_2, info.ThreeLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                {
                    returnVal = info;
                }
            }

            return returnVal;
        }
    }
}
