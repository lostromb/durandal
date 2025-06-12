using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Statistics.SharpEntropy;
using Durandal.Common.Collections.Indexing;

namespace Durandal.Common.Statistics.Classification
{
    public class MaxEntClassifierTrainer : IStatisticalTrainer
    {
        private ILogger _logger;
        private IFileSystem _fileSystem;

        public MaxEntClassifierTrainer(ILogger logger, IFileSystem fileSystem)
        {
            _logger = logger;
            _fileSystem = fileSystem;
        }

        public bool IsTrainingRequired(VirtualPath cacheFile)
        {
            return MaxEntClassifier.IsTrainingRequired(cacheFile, _fileSystem);
        }

        public IStatisticalClassifier TrainClassifier(ITrainingEventReader eventReader, VirtualPath cacheFile, ICompactIndex<string> stringIndex, string modelName, float modelQuality = 1)
        {
            MaxEntClassifier classifier = new MaxEntClassifier(stringIndex, _logger, _fileSystem, modelName);
            classifier.TrainFromData(eventReader, cacheFile);
            return classifier;
        }
    }
}
