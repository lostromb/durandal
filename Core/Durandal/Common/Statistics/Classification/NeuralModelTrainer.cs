using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Collections.Indexing;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Statistics.SharpEntropy;

namespace Durandal.Common.Statistics.Classification
{
    public class NeuralModelTrainer : IStatisticalTrainer
    {
        private ILogger _logger;
        private IFileSystem _cacheManager;

        public NeuralModelTrainer(ILogger logger, IFileSystem cacheManager = null)
        {
            _logger = logger;
            _cacheManager = cacheManager;
        }

        public bool IsTrainingRequired(VirtualPath cacheFile)
        {
            return true;
        }

        public IStatisticalClassifier TrainClassifier(ITrainingEventReader eventReader, VirtualPath cacheFile, ICompactIndex<string> stringIndex, string modelName, float modelQuality = 1)
        {
            NeuralModel returnVal = new NeuralModel(_logger);
            returnVal.Train(eventReader, cacheFile, _cacheManager, modelQuality);
            return returnVal;
        }
    }
}
