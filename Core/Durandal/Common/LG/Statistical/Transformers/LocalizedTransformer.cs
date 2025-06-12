using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using System.Globalization;
using Durandal.Common.NLP.Language;
using Durandal.Common.NLP;

namespace Durandal.Common.LG.Statistical.Transformers
{
    /// <summary>
    /// A slot transformer parent class which supplies CultureInfo to its subclass.
    /// Very useful for things that do date or number formatting
    /// </summary>
    public abstract class LocalizedTransformer : ISlotTransformer
    {
        private ICultureInfoFactory _cultureInfoFactory;

        public LocalizedTransformer(ICultureInfoFactory cultureInfoProvider)
        {
            _cultureInfoFactory = cultureInfoProvider;
        }

        public abstract string OriginalText
        {
            get;
        }

        public abstract string Name { get; }

        public string Apply(object input, LanguageCode locale, ILogger logger, NLPTools nlTools, StatisticalLGPattern pattern)
        {
            return ApplyLocalized(input, locale, _cultureInfoFactory.GetCultureInfoForLocale(locale), logger, nlTools, pattern);
        }

        protected abstract string ApplyLocalized(object input, LanguageCode locale, CultureInfo culture, ILogger logger, NLPTools nlTools, StatisticalLGPattern pattern);
    }
}
