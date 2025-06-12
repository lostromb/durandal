using Durandal.Common.Collections.Indexing;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Statistics.SharpEntropy;

namespace Durandal.Common.Statistics.Classification
{
    /// <summary>
    /// Defines a class which is able to train and produce implementations of IStatisticalClassifier based on sets
    /// of input training data. The actual implementation of the statistical model is abstracted.
    /// </summary>
    public interface IStatisticalTrainer
    {
        bool IsTrainingRequired(VirtualPath cacheFile);
        IStatisticalClassifier TrainClassifier(ITrainingEventReader eventReader, VirtualPath cacheFile, ICompactIndex<string> stringIndex, string modelName, float modelQuality = 1);
    }
}
