using System.Collections.Generic;
using Durandal.API;

namespace Durandal.Common.Dialog
{
    using Durandal.Common.Statistics.Ranking;

    public class ConstantWeightReranker : IReranker
    {
        private readonly IDictionary<string, float> _domainWeights = new Dictionary<string, float>();
        
        public ConstantWeightReranker(IDictionary<string, float> domainIntentWeights)
        {
            foreach (string key in domainIntentWeights.Keys)
            {
                _domainWeights[key] = domainIntentWeights[key];
            }
        }
        
        public void Rerank(ref List<RecoResult> results)
        {
            foreach (RecoResult result in results)
            {
                string key = result.Domain + "/" + result.Intent;
                if (_domainWeights.ContainsKey(key))
                {
                    result.Confidence *= _domainWeights[key];
                }
            }

            results.Sort();
        }
    }
}
