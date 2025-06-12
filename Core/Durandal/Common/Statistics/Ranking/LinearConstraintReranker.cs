using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.NLP.Feature;
using Durandal.Common.MathExt;

namespace Durandal.Common.Statistics.Ranking
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Dialog;
    using Durandal.Common.NLP.Train;

    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Durandal.Common.Collections.Indexing;

    public class LinearConstraintReranker : IReranker
    {
        // So it turns out that this ranker improves accuracy best when it makes tiny miniscule changes.
        // That makes this less of a "reranker" and more of a "judge between two hypotheses which are both 99.99% confident - which one should go on top"
        private const float WEIGHT = 0.0001f;

        // Culls feature weights that have a high standard deviation, determined by this number
        // Lower numbers = more evaluators will try and fire
        private const float STDEV_CUTOFF = 7.0f;
        
        private IDictionary<Compact<string>, float> _weights;
        private ICompactIndex<string> _stringIndex;
        private RankingFeatureExtractor _featureExtractor;
        private bool _trained = false;
        
        public LinearConstraintReranker(ICompactIndex<string> stringIndex)
        {
            _stringIndex = stringIndex;
            _featureExtractor = new RankingFeatureExtractor();
        }

        public static bool IsTrainingRequired(VirtualPath cacheDirectory, IFileSystem fileSystem)
        {
            return !fileSystem.Exists(cacheDirectory.Combine("ranking.features"));
        }

        public void Train(IEnumerable<StringWeightFeature> trainingInstances)
        {
            Counter<string> trainedOffsets = new Counter<string>();
            Counter<string> featureCounts = new Counter<string>();
            _weights = new Dictionary<Compact<string>, float>();
            Dictionary<string, StaticAverage> means = new Dictionary<string, StaticAverage>();
            Dictionary<string, StaticAverage> variances = new Dictionary<string, StaticAverage>();
            foreach (StringWeightFeature feat in trainingInstances)
            {
                trainedOffsets.Increment(feat.Name, (float)feat.Weight);
                featureCounts.Increment(feat.Name);
                if (!means.ContainsKey(feat.Name))
                    means[feat.Name] = new StaticAverage();
                means[feat.Name].Add(feat.Weight);
            }

            foreach (StringWeightFeature feat in trainingInstances)
            {
                double meanValue = means[feat.Name].Average;
                double deviation = meanValue - feat.Weight;

                if (!variances.ContainsKey(feat.Name))
                    variances[feat.Name] = new StaticAverage();
                variances[feat.Name].Add(deviation * deviation);
            }

            // Average up all the counts to make weights
            foreach (KeyValuePair<string, float> featWithCount in featureCounts)
            {
                double mean = means[featWithCount.Key].Average;
                double standardDeviation = Math.Sqrt(variances[featWithCount.Key].Average);
                
                // Discard it when the mean = 0
                if (mean != 0)
                {
                    double ratio = Math.Abs(standardDeviation / mean);

                    // If the standard deviation is too high, this feature is probably not very valuable so cut it
                    if (ratio < STDEV_CUTOFF)
                    {
                        Compact<string> compactFeat = _stringIndex.Store(featWithCount.Key);
                        _weights[compactFeat] = trainedOffsets.GetCount(featWithCount.Key) / featWithCount.Value;
                    }
                }
            }
            _trained = true;
        }

        public void Rerank(ref List<RecoResult> results)
        {
            if (!_trained)
                return;

            foreach (RecoResult result in results)
            {
                float offset = 0.0f;
                List<string> features = _featureExtractor.ExtractFeatures(result);
                if (features.Count > 0)
                {
                    foreach (string f in features)
                    {
                        Compact<string> feature = _stringIndex.GetIndex(f);
                        if (feature != _stringIndex.GetNullIndex())
                        {
                            if (_weights.ContainsKey(feature))
                            {
                                offset += _weights[feature];
                            }
                        }
                    }

                    // Clamp the result and apply it
                    result.Confidence += (offset / features.Count * WEIGHT);
                    result.Confidence = Math.Max(0, Math.Min(1, result.Confidence));
                }
            }
        }
    }
}
