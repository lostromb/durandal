using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.API;
using System.IO;
using Durandal.Common.File;
using Durandal.Common.NLP.Train;

namespace Durandal.Common.NLP.Feature
{
    public interface IDomainFeatureExtractor
    {
        /// <summary>
        /// Extracts runtime features (context) from a wordbroken sentence
        /// </summary>
        /// <param name="ngrams"></param>
        /// <returns></returns>
        string[] ExtractFeatures(Sentence ngrams);

        /// <summary>
        /// Batch extracts features, reading utterances from trainingFileStream and writing to outStream
        /// </summary>
        /// <param name="trainingFileStream"></param>
        /// <param name="outStream"></param>
        void ExtractTrainingFeatures(Stream trainingFileStream, TextWriter outStream);

        /// <summary>
        /// Extracts training features from one sentence
        /// </summary>
        /// <param name="utterance"></param>
        /// <returns></returns>
        TrainingEvent ExtractTrainingFeatures(TrainingUtterance utterance);
    }
}
