using System.Collections.Generic;
using Durandal.Common.NLP.Feature;

namespace Durandal.Common.Statistics.SharpEntropy
{
    public class BasicTrainingEventReader : ITrainingEventReader
    {
        private IList<TrainingEvent> _events;
        private int _index = 0;

        public BasicTrainingEventReader(IList<TrainingEvent> events)
        {
            _events = events;
        }

        public bool HasNext()
        {
            return _index < _events.Count;
        }

        public TrainingEvent ReadNextEvent()
        {
            return _events[_index++];
        }
    }
}
