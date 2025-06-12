using Durandal.Common.Collections;
using Durandal.Common.NLP.Language;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.NLP
{
    public class NLPToolsCollection : INLPToolsCollection
    {
        private readonly FastConcurrentDictionary<LanguageCode, NLPTools> _tools;

        public NLPToolsCollection()
        {
            _tools = new FastConcurrentDictionary<LanguageCode, NLPTools>();
        }

        public void Add(LanguageCode locale, NLPTools tools)
        {
            _tools[locale] = tools;
        }

        public bool TryGetNLPTools(LanguageCode locale, out NLPTools returnVal)
        {
            return _tools.TryGetValue(locale, out returnVal);
        }

        public bool TryGetNLPTools(LanguageCode locale, out NLPTools returnVal, out LanguageCode actualLocale)
        {
            actualLocale = locale;
            return _tools.TryGetValue(locale, out returnVal);
        }
    }
}
