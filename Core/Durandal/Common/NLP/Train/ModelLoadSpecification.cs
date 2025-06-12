using Durandal.Common.NLP.Language;
using Durandal.Common.Statistics.Ranking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Train
{
    /// <summary>
    /// Defines everything we need to know to tell LU to train a specific model with specific domains
    /// </summary>
    public class ModelLoadSpecification
    {
        public LanguageCode Locale;
        public IList<string> DomainsToInclude;
        public IReranker CustomReranker;

        public ModelLoadSpecification(LanguageCode locale, IList<string> domainsToInclude, IReranker customReranker = null)
        {
            Locale = locale;
            DomainsToInclude = domainsToInclude;
            CustomReranker = customReranker;
        }
    }
}
