namespace Durandal.Common.NLP.Feature
{
    using System.Collections.Generic;
    using System.IO;

    using Durandal.API;
    using Durandal.Common.Utils;
    using Durandal.Common.NLP.Train;

    using Durandal.Common.File;
    using Durandal.Common.NLP.Tagging;
    using System;
    using Durandal.Common.Collections;

    public class SpanishDomainFeatureExtractor : IDomainFeatureExtractor
    {
        public string[] ExtractFeatures(string originalText, IWordBreaker wordBreaker)
        {
            Sentence ngrams = wordBreaker.Break(originalText);
            return this.ExtractFeatures(ngrams);
        }

        public string[] ExtractFeatures(Sentence ngrams)
        {
            List<string> returnVal = new List<string>();

            // Unigrams
            foreach (string word in ngrams.Words)
            {
                returnVal.Add("uni:" + word);
            }

            // Bigrams
            Sentence bigrams = this.AppendTokensToSentence(ngrams);
            for (int c = 0; c < ngrams.Length - 1; c++)
            {
                returnVal.Add("bi:" + ngrams.Words[c] + ngrams.Words[c + 1]);
            }

            // Trigrams
            Sentence trigrams = this.AppendTokensToSentence(bigrams);
            for (int c = 0; c < ngrams.Length - 2; c++)
            {
                returnVal.Add("tri:" + ngrams.Words[c] + ngrams.Words[c + 1] + ngrams.Words[c + 2]);
            }

            // Number of words in the sentence
            returnVal.Add("l:" + ngrams.Length);

            return returnVal.ToArray();
        }

        private Sentence AppendTokensToSentence(Sentence input)
        {
            Sentence returnVal = new Sentence();
            returnVal.Words.Add("stkn");
            returnVal.Words.FastAddRangeList(input.Words);
            returnVal.Words.Add("etkn");
            return returnVal;
        }

        public void ExtractTrainingFeatures(Stream trainingFileStream, TextWriter outStream, IWordBreaker wordBreaker)
        {
            using (StreamReader fileIn = new StreamReader(trainingFileStream))
            {
                while (!fileIn.EndOfStream)
                {
                    string line = fileIn.ReadLine();
                    if (line == null)
                        continue;

                    TrainingUtterance utterance = new TrainingUtterance(line);

                    string[] features = this.ExtractFeatures(TaggedDataSplitter.StripTags(utterance.Utterance), wordBreaker);
                    DomainIntentContextFeature newFeature = new DomainIntentContextFeature(utterance.Domain, utterance.Intent, features);
                    outStream.WriteLine(newFeature.ToString());
                }

                //fileIn.Close();
            }
        }

        public void ExtractTrainingFeatures(Stream trainingFileStream, TextWriter outStream)
        {
            throw new NotImplementedException();
        }

        public TrainingEvent ExtractTrainingFeatures(TrainingUtterance utterance)
        {
            throw new NotImplementedException();
        }
    }
}
