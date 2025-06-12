using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.NLP.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Train
{
    /// <summary>
    /// Applies an elaborate set of rules which determines / filters what NEGATIVE training features
    /// should be piped into the domain classifier model for a given domain. By crafting the input rule
    /// sets, you can create completely cross-trained models, or smaller model sets in any degree of isolation,
    /// thus allowing many different models to live together in the same runtime
    /// </summary>
    public class DomainCrossTrainFilter : ICrossTrainFilter<DomainIntentContextFeature>
    {
        // The domain that is being trained
        private readonly string _localDomain;

        // These rule collections map from key = local domain/intent to value = feature domain/intent
        private readonly IList<CrossTrainingRule> _rules;

        // Used to cache the results of evaluating the crossdomain rules
        private IDictionary<int, bool> _cache = new Dictionary<int, bool>();

        public DomainCrossTrainFilter(string localDomain, IList<CrossTrainingRule> trainingRules)
        {
            _localDomain = localDomain;
            _rules = trainingRules;
        }

        public bool Passes(DomainIntentContextFeature feature)
        {
            int key = feature.Domain.GetHashCode() ^ (feature.Intent.GetHashCode() << 2);
            if (_cache.ContainsKey(key))
            {
                return _cache[key];
            }

            // The constant rule should be that a domain cannot negate itself, so check that first
            bool returnVal = !feature.Domain.Equals(_localDomain);

            if (!returnVal)
            {
                _cache[key] = false;
                return false;
            }

            returnVal = false;
            foreach (CrossTrainingRule rule in _rules)
            {
                returnVal = rule.Evaluate(feature.Domain, feature.Intent, _localDomain, null).GetValueOrDefault(returnVal);
            }

            _cache[key] = returnVal;
            return returnVal;
        }
    }
}
