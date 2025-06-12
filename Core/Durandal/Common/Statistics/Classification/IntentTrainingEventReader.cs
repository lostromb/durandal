using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.Statistics.SharpEntropy;
using Durandal.Common.NLP.Feature;

namespace Durandal.Common.Statistics.Classification
{
    using Durandal.API;
    using Durandal.Common.NLP.Train;

    public class IntentTrainingEventReader : ITrainingEventReader
    {
        private bool readingPositives = true;
        private TrainingDataList<DomainIntentContextFeature> positive;
        private TrainingDataList<DomainIntentContextFeature> negative;
        private string _positiveDomain = string.Empty;
        private string _positiveIntent = string.Empty;
        private double _index = 0;
        private TrainingEvent _next;

        // The "Multiplier" defines the ratio of negative features to positive features.
        // It is calculated dynamically based on the amount of positive/negative training data available.
        // Lower numbers = more negative data used
        private double _multiplier;

        // Lower numbers here = more negative training data used
        private double DESIRED_MULTIPLIER = 5.0;
        private double MIN_MULTIPLIER = 2.5;

        public IntentTrainingEventReader(TrainingDataList<DomainIntentContextFeature> positiveTraining,
            TrainingDataList<DomainIntentContextFeature> negativeTraining,
            string targetDomain,
            string targetIntent)
        {
            // FIXME? This doesn't actually take into account the amount of negative data that is _considered_ by the trainer, only what is available.
            // However, using constant scale multipliers seems to make things much worse
            _multiplier = negativeTraining.TrainingData.Count / (double)(positiveTraining.TrainingData.Count + 1) / DESIRED_MULTIPLIER;
            _index = 0;
            _multiplier = Math.Max(_multiplier, MIN_MULTIPLIER);
            positive = positiveTraining;
            negative = negativeTraining;
            _positiveDomain = targetDomain;
            _positiveIntent = targetIntent;
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
            while (!ReachedEnd())
            {
                if (readingPositives)
                {
                    DomainIntentContextFeature feature = positive.TrainingData[(int)_index];
                    _index += 1;
                    if (feature.Domain.Equals(_positiveDomain) &&
                        feature.Intent.Equals(_positiveIntent))
                    {
                        return new TrainingEvent("1", feature.Context);
                    }
                }
                else
                {
                    DomainIntentContextFeature feature = negative.TrainingData[(int)_index];
                    _index += _multiplier;
                    if ((feature.Domain.Equals(_positiveDomain) && !feature.Intent.Equals(_positiveIntent))/* || 
                        (!_positiveDomain.Equals(DialogConstants.COMMON_DOMAIN) && feature.Domain.Equals(DialogConstants.COMMON_DOMAIN))*/)
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
                stillMore = (int)_index < positive.TrainingData.Count - 1;
                if (!stillMore)
                {
                    readingPositives = false;
                    _index = 0;
                }
            }
            if (!readingPositives)
            {
                stillMore = (int)_index < negative.TrainingData.Count - 1;
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

        private void CompactMemory()
        {
            positive = null;
            negative = null;
        }
    }
}
