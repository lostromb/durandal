

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
    using Durandal.Common.MathExt;

    public class DomainTrainingEventReaderBalanced : ITrainingEventReader
    {
        private bool readingPositives = true;
        private TrainingDataList<DomainIntentContextFeature> positive;
        private TrainingDataList<DomainIntentContextFeature> negative;
        private string _positiveDomain;
        private int _index = 0;
        private TrainingEvent _next;
        private IRandom _rand;
        private ICrossTrainFilter<DomainIntentContextFeature> _negativeFilter;

        // The number of negative training instances to use for each positive one
        private const double NEGATIVE_MULTIPLIER = 5;
        private int positivesUsed = 0;
        private int negativesUsed = 0;
        private int negativeCap = 0;
        private int negativeAttempts = 0;

        public DomainTrainingEventReaderBalanced(
            TrainingDataList<DomainIntentContextFeature> positiveTraining,
            TrainingDataList<DomainIntentContextFeature> negativeTraining,
            string domain,
            ICrossTrainFilter<DomainIntentContextFeature> negativeFilter)
        {
            _index = 0;
            _negativeFilter = negativeFilter;
            _rand = new FastRandom(222);
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
                    string[] context = positive.TrainingData[_index].Context;
                    _index += 1;
                    positivesUsed += 1;
                    return new TrainingEvent("1", context);
                }
            }
            else
            {
                while (!ReachedEnd())
                {
                    // For negative data, use a constant stride to try and get a good spread of data
                    // FIXME: We should really just be able to detect if no training is valid beforehand, so we don't have to use
                    // negativeAttempts to see if anything is coming back
                    _index = (_index + 13);
                    if (_index >= negative.TrainingData.Count)
                    {
                        _index = _rand.NextInt(0, 13) % negative.TrainingData.Count;
                    }
                    negativeAttempts += 1;

                    DomainIntentContextFeature feature = negative.TrainingData[(int)_index];
                    if (_negativeFilter.Passes(feature))
                    {
                        negativesUsed += 1;
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
                    _index < positive.TrainingData.Count - 1;
                if (!stillMore)
                {
                    readingPositives = false;
                    _index = 0;

                    // Calculate the number of negative instances to use based on how many positives we read
                    negativeCap = (int)(positivesUsed * NEGATIVE_MULTIPLIER);
                }
            }
            if (!readingPositives)
            {
                stillMore = negative.TrainingData.Count > 0 && negativesUsed < negativeCap && negativeAttempts < (negativeCap * 5);
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
