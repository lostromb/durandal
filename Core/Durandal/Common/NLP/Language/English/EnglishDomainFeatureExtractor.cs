namespace Durandal.Common.NLP.Language.English
{
    using System.Collections.Generic;
    using System.IO;

    using Durandal.API;
    using Durandal.Common.Utils;
    using Durandal.Common.NLP.Train;

    using Durandal.Common.File;
    using Durandal.Common.NLP.Tagging;
    using Durandal.Common.NLP.Alignment;
    using Durandal.Common.NLP.Feature;
    using Durandal.Common.Collections;

    public class EnglishDomainFeatureExtractor : IDomainFeatureExtractor
    {
        private bool _isCaseSensitive = false;
        private DictionaryCollection _allDictionaries;
        private IWordBreaker _featurizationWordBreaker;

        public EnglishDomainFeatureExtractor(DictionaryCollection dictionaries, IWordBreaker featurizationWordBreaker)
        {
            _allDictionaries = dictionaries;
            _featurizationWordBreaker = featurizationWordBreaker;
        }

        public string[] ExtractFeatures(string originalText, IWordBreaker wordBreaker)
        {
            Sentence ngrams = wordBreaker.Break(originalText);
            return ExtractFeatures(ngrams);
        }

        public string[] ExtractFeatures(Sentence ngrams)
        {
            List<string> returnVal = new List<string>();
            Sentence normalizedSentence = ngrams;
            
            // Apply case sensitivity rules
            if (!_isCaseSensitive)
            {
                normalizedSentence = new Sentence();
                normalizedSentence.Indices = ngrams.Indices;
                normalizedSentence.OriginalText = ngrams.OriginalText;
                normalizedSentence.LexicalForm = ngrams.LexicalForm;
                normalizedSentence.Words = new List<string>();
                foreach (string word in ngrams.Words)
                {
                    normalizedSentence.Words.Add(word.ToLower());
                }
            }

            // Unigrams
            int placeNames = 0;
            int personNames = 0;
            //int uncommonWords = 0;
            foreach (string word in normalizedSentence.Words)
            {
                returnVal.Add("uni:" + word);
                /*if (!_allDictionaries.IsA(word, "commonwords"))
                {
                    uncommonWords++;
                }*/
                if (_allDictionaries.IsA(word, "firstnames") ||
                    _allDictionaries.IsA(word, "lastnames"))
                {
                    personNames++;
                }
                if (_allDictionaries.IsA(word, "placenames"))
                {
                    placeNames++;
                }
            }

            returnVal.Add("places:" + placeNames);
            returnVal.Add("names:" + personNames);
            //returnVal.Add("nodict" + uncommonWords);

            // Bigrams
            Sentence bigrams = AppendTokensToSentence(normalizedSentence);
            for (int c = 0; c < normalizedSentence.Length - 1; c++)
            {
                returnVal.Add("bi:" + normalizedSentence.Words[c] + normalizedSentence.Words[c + 1]);
            }

            // Trigrams
            Sentence trigrams = AppendTokensToSentence(bigrams);
            for (int c = 0; c < normalizedSentence.Length - 2; c++)
            {
                returnVal.Add("tri:" + normalizedSentence.Words[c] + normalizedSentence.Words[c + 1] + normalizedSentence.Words[c + 2]);
            }

            // The Metaphone string for the entire utterance (if it's short enough)
            string metaphone = DoubleMetaphone.Encode(normalizedSentence.OriginalText);
            if (metaphone.Length <= 6)
                returnVal.Add("phon:" + metaphone);

            // Number of words in the sentence
            returnVal.Add("l:" + normalizedSentence.Length);

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

        public TrainingEvent ExtractTrainingFeatures(TrainingUtterance utterance)
        {
            string[] features = ExtractFeatures(TaggedDataSplitter.StripTags(utterance.Utterance), _featurizationWordBreaker);
            DomainIntentContextFeature newFeature = new DomainIntentContextFeature(utterance.Domain, utterance.Intent, features);
            return new TrainingEvent(newFeature.Domain, newFeature.Context);
        }

        // TODO: Put this in an abstract superclass
        public void ExtractTrainingFeatures(Stream trainingFileStream, TextWriter outStream)
        {
            using (StreamReader fileIn = new StreamReader(trainingFileStream))
            {
                while (!fileIn.EndOfStream)
                {
                    string line = fileIn.ReadLine();
                    if (line == null)
                        continue;

                    TrainingUtterance utterance = new TrainingUtterance(line);

                    string[] features = ExtractFeatures(TaggedDataSplitter.StripTags(utterance.Utterance), _featurizationWordBreaker);
                    DomainIntentContextFeature newFeature = new DomainIntentContextFeature(utterance.Domain, utterance.Intent, features);
                    outStream.WriteLine(newFeature.ToString());
                }
                //fileIn.Close();
            }
        }
    }
}
