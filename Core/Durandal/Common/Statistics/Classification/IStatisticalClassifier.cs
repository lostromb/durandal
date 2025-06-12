using Durandal.API;
using Durandal.Common.File;
using System.Collections.Generic;
using Durandal.Common.Statistics;

namespace Durandal.Common.Statistics.Classification
{
    /// <summary>
    /// Represents a statistical model that can classify an observation (expressed as a set of input features) into one of multiple classes
    /// </summary>
    public interface IStatisticalClassifier
    {
        List<Hypothesis<string>> ClassifyAll(string[] featureSet);

        Hypothesis<string>? Classify(string[] featureSet);

        long GetMemoryUse();
    }
}