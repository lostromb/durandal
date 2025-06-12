using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.API;
using System.IO;

namespace Durandal.Common.NLP.Feature
{
    public interface ITagFeatureExtractor
    {
        string[] ExtractFeatures(Sentence input, string[][] tagHistory, int wordIndex);
        void ExtractTrainingFeatures(Stream trainingFileStream, Stream outStream, IWordBreaker wordBreaker, ISet<string> possibleTags);
    }
}
