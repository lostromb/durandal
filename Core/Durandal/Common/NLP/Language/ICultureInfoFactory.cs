using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Durandal.Common.NLP.Language
{
    public interface ICultureInfoFactory
    {
        CultureInfo GetCultureInfoForLocale(LanguageCode locale);
    }
}
