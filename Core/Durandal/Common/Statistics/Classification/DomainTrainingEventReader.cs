

using Durandal.Common.Statistics.SharpEntropy;
using Durandal.Common.NLP.Feature;

namespace Durandal.Common.Statistics.Classification
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Durandal.API;
    using Durandal.Common.NLP.Train;

    public class DomainTrainingEventReader : ITrainingEventReader
    {
        private bool readingPositives = true;
        private TrainingDataList<DomainIntentContextFeature> positive;
        private TrainingDataList<DomainIntentContextFeature> negative;
        private string _positiveDomain;
        private double _index = 0;
        private TrainingEvent _next;
        private ICrossTrainFilter<DomainIntentContextFeature> _negativeFilter;

        // The "Multiplier" defines the ratio of negative features to positive features.
        // It is calculated dynamically based on the amount of positive/negative training data available.
        // Lower numbers = more negative data used
        private double _multiplier;

        // Lower numbers here = more negative training data used
        private double DESIRED_MULTIPLIER = 4.0;
        private double MIN_MULTIPLIER = 2.5;

        public DomainTrainingEventReader(
            TrainingDataList<DomainIntentContextFeature> positiveTraining,
            TrainingDataList<DomainIntentContextFeature> negativeTraining,
            string domain,
            ICrossTrainFilter<DomainIntentContextFeature> negativeFilter)
        {
            _negativeFilter = negativeFilter;
            double adjustPositiveTrainingCount = Math.Max(1, positiveTraining.TrainingData.Count);
            _multiplier = negativeTraining.TrainingData.Count / adjustPositiveTrainingCount / DESIRED_MULTIPLIER;
            _index = 0;
            _multiplier = Math.Max(_multiplier, MIN_MULTIPLIER);
            positive = positiveTraining;
            negative = negativeTraining;
            _positiveDomain = domain;
            _next = GetNext();
        }

        public TrainingEvent ReadNextEvent()
        {
            TrainingEvent returnVal = _next;
            _next = GetNext();
            return returnVal;
        }

        private TrainingEvent GetNext()
        {
            if (ReachedEnd())
                return null;

            if (readingPositives)
            {
                while (!ReachedEnd())
                {
                    string[] context = positive.TrainingData[(int)_index].Context;
                    _index += 1;
                    return new TrainingEvent("1", context);
                }
            }
            else
            {
                while (!ReachedEnd())
                {
                    DomainIntentContextFeature feature = negative.TrainingData[(int)_index];
                    _index += _multiplier;
                    if (_negativeFilter.Passes(feature))
                    {
                        return new TrainingEvent("0", feature.Context);
                    }
                }
            }
            return null;
        }

        private bool ReachedEnd()
        {
            bool stillMore = false;
            if (readingPositives)
            {
                stillMore = positive.TrainingData.Count > 0 &&
                    ((int)_index) < positive.TrainingData.Count - 1;
                if (!stillMore)
                {
                    readingPositives = false;
                    _index = 0;
                }
            }
            if (!readingPositives)
            {
                stillMore = negative.TrainingData.Count > 0 &&
                    (int)_index < negative.TrainingData.Count - 1;
            }
            if (!stillMore)
            {
                CompactMemory();
            }
            return !stillMore;
        }

        public bool HasNext()
        {
            return _next != null;
        }

        public void CompactMemory()
        {
            positive = null;
            negative = null;
        }
    }
}
