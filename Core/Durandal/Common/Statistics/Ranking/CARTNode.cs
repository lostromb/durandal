using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Statistics.Ranking
{
    using Durandal.Common.NLP.Train;

    using Durandal.Common.Collections.Indexing;

    /// <summary>
    /// Represents a single node in a decision tree (for the regression tree ranker).
    /// Every node represents a single decision to be made on an input feature
    /// </summary>
    public class CARTNode
    {
        public Compact<string> FeatureName;
        public IDictionary<string, float> Outcome;

        public string Serialize(ICompactIndex<string> index)
        {
            return index.Retrieve(FeatureName) + "\t" + this.SerializeOutcomes();
        }

        private string SerializeOutcomes()
        {
            IList<string> returnVal = new List<string>();
            foreach (var x in Outcome)
            {
                returnVal.Add(x.Key + "\t" + x.Value);
            }
            return string.Join("\t", returnVal);
        }

        public void Parse(string input, ICompactIndex<string> index)
        {
            Outcome = new Dictionary<string, float>();
            string[] parts = input.Split('\t');
            FeatureName = index.Store(parts[0]);
            for (int c = 1; c < parts.Length - 1; c += 2)
            {
                Outcome.Add(parts[c],  float.Parse(parts[c + 1]));
            }
        }
    }
}
