using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Durandal.Common.NLP.Language
{
    public class InvariantCultureInfoFactory : ICultureInfoFactory
    {
        public CultureInfo GetCultureInfoForLocale(LanguageCode locale)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
