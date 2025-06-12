using Durandal.Common.NLP.Feature;
using Durandal.Common.MathExt;

namespace Durandal.Common.Statistics.Ranking
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    using Durandal.API;
    using Durandal.Common.NLP.Train;

    using Durandal.Common.Logger;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Durandal.Common.Collections.Indexing;

    /// <summary>
    /// Implements a ghetto CART decision tree ranker to nudge recoresults into a more fitting ranking.
    /// </summary>
    public class RegressionTreeReranker : IReranker
    {
        /// <summary>
        /// This determines how much entropy will be required to generate a decision tree node that makes a
        /// choice between two outcomes. This should range from 0.0 to 1.0. Higher numbers will mean that
        /// more decision tree nodes will be made in the tree, each new one making a slightly less devisive
        /// choice than the last.
        /// </summary>
        private const float ENTROPY_THRESHOLD = 0.040f;

        private IDictionary<Compact<string>, CARTNode> _rootNodes;
        private ILogger _logger;
        private ICompactIndex<string> _stringIndex;
        private IFileSystem _fileSystem;
        private VirtualPath _cacheFileName;
        
        public RegressionTreeReranker(ILogger logger, ICompactIndex<string> stringIndex, IFileSystem fileSystem, VirtualPath cacheDirectory)
        {
            _rootNodes = new Dictionary<Compact<string>, CARTNode>();
            _logger = logger;
            _stringIndex = stringIndex;
            _fileSystem = fileSystem;
            _cacheFileName = cacheDirectory.Combine("ranking.model");
        }

        public void Train(IEnumerable<RankingFeature> features)
        {
            if (this.TryLoadCache())
            {
                _logger.Log("Using cached model");
            }
            else
            {
                // Recreate utterances from the training data
                IList<RankedUtterance> allUtterances = this.GetUtterances(features);

                // Start iterating through feature names to see which ones can become tree roots
                ISet<string> allFeatures = this.GetFeatureNames(features);

                Counter<string> allOutcomeCounts = new Counter<string>();
                foreach (RankedUtterance r in allUtterances)
                {
                    allOutcomeCounts.Increment(r.ActualDomainIntent);
                }

                ISet<string> allOutcomes = new HashSet<string>();
                foreach (var kvp in allOutcomeCounts)
                {
                    if (!allOutcomes.Contains(kvp.Key))
                    {
                        allOutcomes.Add(kvp.Key);
                    }
                }

                foreach (string rootFeature in allFeatures)
                {
                    // Get the set of outcomes for training instances containing this feature
                    IList<RankedUtterance> positiveOutcomes = GetUtterancesWithFeature(allUtterances, rootFeature);

                    // Find the entropy of the set
                    IDictionary<string, float> outcomes = this.GenerateOutcomeList(positiveOutcomes, allOutcomeCounts);
                    float entropy = CalculateEntropy(outcomes, allOutcomes);
                    if (entropy < ENTROPY_THRESHOLD)
                    {
                        CARTNode newRootNode = new CARTNode();
                        newRootNode.FeatureName = _stringIndex.Store(rootFeature);
                        newRootNode.Outcome = outcomes;
                        _rootNodes.Add(newRootNode.FeatureName, newRootNode);
                    }
                }
                this.WriteCache();
            }

            _logger.Log("Created " + _rootNodes.Count + " decision trees after training");
        }

        private bool TryLoadCache()
        {
            if (!_fileSystem.Exists(_cacheFileName))
            {
                return false;
            }

            using (StreamReader input = new StreamReader(_fileSystem.OpenStream(_cacheFileName, FileOpenMode.Open, FileAccessMode.Read)))
            {
                while (!input.EndOfStream)
                {
                    string nextLine = input.ReadLine();
                    if (string.IsNullOrEmpty(nextLine))
                    {
                        continue;
                    }

                    CARTNode value = new CARTNode();
                    value.Parse(nextLine, _stringIndex);
                    _rootNodes.Add(value.FeatureName, value);
                }

                return true;
            }
        }

        private void WriteCache()
        {
            using (StreamWriter output = new StreamWriter(_fileSystem.OpenStream(_cacheFileName, FileOpenMode.Create, FileAccessMode.Write)))
            {
                foreach (var x in _rootNodes)
                {
                    output.WriteLine(x.Value.Serialize(_stringIndex));
                }
            }
        }

        private IDictionary<string, float> GenerateOutcomeList(IList<RankedUtterance> positiveOutcomes, Counter<string> allOutcomeCounts)
        {
            IDictionary<string, float> returnVal = new Dictionary<string, float>();
            
            if (positiveOutcomes.Count == 0)
                return returnVal;
            
            Counter<string> histogram = new Counter<string>();
            foreach (RankedUtterance e in positiveOutcomes)
            {
                histogram.Increment(e.ActualDomainIntent);
            }

            foreach (KeyValuePair<string, float> itemWithCount in histogram)
            {
                // Normalize the outcome counts by the total number of training features with that same domain
                // This will prevent training templates with lots of data from hogging too much weight
                float normalizedValue = itemWithCount.Value / allOutcomeCounts.GetCount(itemWithCount.Key);
                returnVal.Add(itemWithCount.Key, normalizedValue);
            }

            return returnVal;
        }

        private float CalculateEntropy(IDictionary<string, float> outcomeSet, ISet<string> allOutcomes)
        {
            if (outcomeSet.Count == 0)
            {
                return 0.0f;
            }

            List<float> numbers = new List<float>();
            foreach (float n in outcomeSet.Values)
            {
                numbers.Add(n);
            }
            numbers.Sort();
            numbers.Reverse();

            float variance = 0;
            for (int c = 0; c < numbers.Count; c++)
            {
                if (c == 0)
                    variance += (1.0f - numbers[c]) * (1.0f - numbers[c]);
                else
                    variance += (numbers[c]) * (numbers[c]);
            }
            
            /*foreach (string item in allOutcomes)
            {
                if (outcomeSet.ContainsKey(item))
                {
                    float t = outcomeSet[item];
                    variance += (t * t);
                }
            }*/

            float entropy = (float)Math.Sqrt(variance);

            return entropy;
        }

        private IList<RankedUtterance> GetUtterancesWithFeature (IList<RankedUtterance> utterances,
                                                                    string feature)
        {
            IList<RankedUtterance> returnVal = new List<RankedUtterance>();
            foreach (RankedUtterance u in utterances)
            {
                if (u.Features.Contains(feature))
                {
                    returnVal.Add(u);
                }
            }
            return returnVal;
        }

        private void DivideUtterancesByFeature(IList<RankedUtterance> utterances,
                                                string feature,
                                                ref IList<RankedUtterance> positive,
                                                ref IList<RankedUtterance> negative)
        {
            foreach (RankedUtterance u in utterances)
            {
                if (u.Features.Contains(feature))
                {
                    positive.Add(u);
                }
                else
                {
                    negative.Add(u);
                }
            }
        }

        private IEnumerable<RankedUtterance> GetUtterancesWithFeatures(IList<RankedUtterance> utterances,
                                                                    IEnumerable<string> features)
        {
            IList<RankedUtterance> returnVal = new List<RankedUtterance>();
            foreach (RankedUtterance u in utterances)
            {
                bool matches = true;
                foreach (string feat in features)
                {
                    if (!u.Features.Contains(feat))
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches)
                    returnVal.Add(u);
            }
            return returnVal;
        }

        private ISet<string> GetFeatureNames(IEnumerable<RankingFeature> features)
        {
            ISet<string> returnVal = new HashSet<string>();
            foreach (RankingFeature feat in features)
            {
                if (!returnVal.Contains(feat.FeatureName))
                {
                    returnVal.Add(feat.FeatureName);
                }
            }
            return returnVal;
        }

        private IList<RankedUtterance> GetUtterances(IEnumerable<RankingFeature> features)
        {
            IDictionary<int, RankedUtterance> idMapping = new Dictionary<int, RankedUtterance>();
            foreach (RankingFeature feat in features)
            {
                if (!idMapping.ContainsKey(feat.UtteranceId))
                {
                    idMapping[feat.UtteranceId] = new RankedUtterance(feat.Outcome);
                }
                RankedUtterance utterance = idMapping[feat.UtteranceId];
                if (!utterance.Features.Contains(feat.FeatureName))
                    utterance.Features.Add(feat.FeatureName);
            }
            return new List<RankedUtterance>(idMapping.Values);
        }

        private IDictionary<string, float> ConvertToOutcomes(List<RecoResult> results)
        {
            IDictionary<string, float> returnVal = new Dictionary<string, float>();
            foreach (RecoResult r in results)
            {
                returnVal.Add(r.Domain + "/" + r.Intent, r.Confidence);
            }
            return returnVal;
        }
        
        public void Rerank(ref List<RecoResult> results)
        {
            IDictionary<string, float> defaultOutcomes = this.ConvertToOutcomes(results);
            
            RankingFeatureExtractor e = new RankingFeatureExtractor();
            Counter<string> outcomeCounter = new Counter<string>();
            float triggerCount = 0;
            foreach (RecoResult r in results)
            {
                List<string> features = e.ExtractFeatures(r);
                foreach (string feature in features)
                {
                    Compact<string> featureKey = _stringIndex.GetIndex(feature);
                    if (_rootNodes.ContainsKey(featureKey))
                    {
                        foreach (var outcome in _rootNodes[featureKey].Outcome)
                        {
                            outcomeCounter.Increment(outcome.Key, outcome.Value);
                        }
                        triggerCount += 1;
                    }
                    else
                    {
                        foreach (var outcome in defaultOutcomes)
                        {
                            outcomeCounter.Increment(outcome.Key, outcome.Value);
                        }
                        triggerCount += 1;
                    }
                }
            }

            IDictionary<string, float> finalOutcome = new Dictionary<string, float>();
            foreach (KeyValuePair<string, float> outcomeWithCount in outcomeCounter)
            {
                finalOutcome[outcomeWithCount.Key] = outcomeWithCount.Value / triggerCount;
            }

            // Now apply the ranked confidences to the existing list. LU will re-sort the list for us.
            foreach (RecoResult r in results)
            {
                string domainIntent = r.Domain + "/" + r.Intent;
                if (finalOutcome.ContainsKey(domainIntent))
                {
                    r.Confidence = finalOutcome[domainIntent];
                }
            }
        }
    }
}
