using Durandal.Common.Statistics.SharpEntropy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.NLP.Feature;
using Durandal.Common.NLP.Language.English;

namespace Durandal.Common.NLP.Train
{
    public class DomainTrainingStream : ITrainingEventReader
    {
        private ITrainingDataStream _innerStream;
        private IDomainFeatureExtractor _featureExtractor;

        public DomainTrainingStream(ITrainingDataStream trainingSource, IDomainFeatureExtractor featureExtractor)
        {
            _innerStream = trainingSource;
            _featureExtractor = featureExtractor;
        }

        public bool HasNext()
        {
            return _innerStream.MoveNext();
        }

        public TrainingEvent ReadNextEvent()
        {
            return _featureExtractor.ExtractTrainingFeatures(_innerStream.Current);
        }
    }
}
