namespace Durandal.Common.NLP.Feature
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Durandal.API;
    using Durandal.Common.Utils;
    using Durandal.Common.NLP.Tagging;
    using Durandal.Common.NLP.Train;

    using Durandal.Common.File;
    using Durandal.Common.Collections;

    public class SpanishTagFeatureExtractor : ITagFeatureExtractor
    {
        private static Regex numberMatcher = new Regex("\\d");
        private static Regex symbolMatcher = new Regex("[^\\w ',\\.]");

        private void ExtractWordLevelFeatures(string word, string prefix, ref List<string> returnVal)
        {
            // Filter the word for numeric values and symbols (TODO)
            if (numberMatcher.IsMatch(word))
            {
                returnVal.Add(prefix + "num");
            }
            // Normalize all numbers to zero
            word = StringUtils.RegexReplace(numberMatcher, word, "0");
            if (symbolMatcher.IsMatch(word))
            {
                returnVal.Add(prefix + "sym");
            }

            returnVal.Add(prefix + "wd:" + word); // Current word

            if (char.IsUpper(word[0])) // Capital-case word
            {
                returnVal.Add(prefix + "cap");
            }

            if (word.Length > 3)
            {
                // Suffix
                returnVal.Add(prefix + "sf:" + word.Substring(word.Length - 3));
            }
        }

        private T TryGetValue<T>(IList<T> array, int index, T def)
        {
            if (index < 0 || index >= array.Count)
                return def;
            return array[index];
        }

        // Extract features on a per-word basis
        public string[] ExtractFeatures(Sentence input, string[][] tagHistory, int wordIndex)
        {
            List<string> returnVal = new List<string>();
            if (input.Words.Count == 0)
            {
                return returnVal.ToArray();
            }

            this.ExtractWordLevelFeatures(input.Words[wordIndex], string.Empty, ref returnVal);

            returnVal.Add("idx:" + wordIndex); // Word index
            // Negative word index (distance from the end of the sentence)
            returnVal.Add("nidx:" + (input.Length - wordIndex - 1));
            if (wordIndex > 1) // n - 2 word
            {
                this.ExtractWordLevelFeatures(input.Words[wordIndex - 2], "b2", ref returnVal);
            }
            else
            {
                returnVal.Add("b2wd:stkn");
            }
            if (wordIndex > 0) // n - 1 word
            {
                this.ExtractWordLevelFeatures(input.Words[wordIndex - 1], "b1", ref returnVal);
            }
            else
            {
                returnVal.Add("b1wd:stkn");
            }
            if (wordIndex < input.Length - 1) // n + 1 word
            {
                this.ExtractWordLevelFeatures(input.Words[wordIndex + 1], "f1", ref returnVal);
            }
            else
            {
                returnVal.Add("f1wd:etkn");
            }
            if (wordIndex < input.Length - 2) // n + 2 word
            {
                this.ExtractWordLevelFeatures(input.Words[wordIndex + 2], "f2", ref returnVal);
            }
            else
            {
                returnVal.Add("f2wd:etkn");
            }

            string[] emptyArray = new string[0];
            if (wordIndex > 1)
            {
                foreach (string tag in ArrayExtensions.TryGetArray(tagHistory, wordIndex - 2, emptyArray))
                {
                    if (!string.IsNullOrEmpty(tag))
                        returnVal.Add("b2tg:" + tag); // n - 2 tags
                }
            }
            else
            {
                returnVal.Add("b2tg:stkn");
            }
            if (wordIndex > 0)
            {
                foreach (string tag in ArrayExtensions.TryGetArray(tagHistory, wordIndex - 1, emptyArray))
                {
                    if (!string.IsNullOrEmpty(tag))
                        returnVal.Add("b1tg:" + tag); // n - 1 tags
                }
            }
            else
            {
                returnVal.Add("b1tg:stkn");
            }

            // Trigrams
            string[] neighborhood = new string[4];
            neighborhood[0] = this.TryGetValue(input.Words, wordIndex - 2, "stkn").ToLowerInvariant();
            neighborhood[1] = this.TryGetValue(input.Words, wordIndex - 1, "stkn").ToLowerInvariant();
            neighborhood[2] = this.TryGetValue(input.Words, wordIndex + 1, "etkn").ToLowerInvariant();
            neighborhood[3] = this.TryGetValue(input.Words, wordIndex + 2, "etkn").ToLowerInvariant();
            returnVal.Add("tri0:" + neighborhood[0] + neighborhood[1]);
            returnVal.Add("tri1:" + neighborhood[2] + neighborhood[3]);

            // Tag history length
            if (wordIndex > 0)
            {
                foreach (string tag in tagHistory[wordIndex - 1])
                {
                    int count = 1;
                    for (int index = wordIndex - 2; index >= 0; index--)
                    {
                        // TODO: This is way too slow!
                        if (ArrayContains(tagHistory[index], tag))
                            count += 1;
                        else
                            break;
                    }

                    returnVal.Add(string.Format("tl{0}:{1}", tag, count));
                }
            }

            return returnVal.ToArray();
        }

        private static bool ArrayContains<T>(T[] array, T val)
        {
            foreach (T item in array)
            {
                if (item.Equals(val))
                    return true;
            }
            return false;
        }

        // TODO: Put this in an abstract superclass
        public void ExtractTrainingFeatures(Stream trainingFileStream, Stream outStream, IWordBreaker wordBreaker, ISet<string> possibleTags)
        {
            using (StreamReader trainingIn = new StreamReader(trainingFileStream))
            {
                using (StreamWriter fileOut = new StreamWriter(outStream))
                {
                    while (!trainingIn.EndOfStream)
                    {
                        string line = trainingIn.ReadLine();
                        if (line == null)
                            continue;
                        string[] parts = line.Split('\t');
                        TaggedSentence taggedTrainingData = TaggedDataSplitter.ParseTags(parts[1], wordBreaker);
                        // Process each word in the set
                        string[][] tagList = new string[taggedTrainingData.Utterance.Words.Count][];
                        List<string> words = new List<string>();
                        int c = 0;
                        foreach (TaggedWord word in taggedTrainingData.Words)
                        {
                            words.Add(word.Word);
                            tagList[c++] = word.Tags.ToArray();
                        }
                        Sentence rawSentence = taggedTrainingData.Utterance;
                        string thisNode = "stkn";
                        if (taggedTrainingData.Words.Count > 0)
                        {
                            string[] features = this.ExtractFeatures(rawSentence, tagList, 0);
                            Array.Sort(tagList[0]);
                            string combinedTag = string.Join("+", tagList[0]);
                            foreach (string featureName in features)
                            {
                                TagFeature newFeature = new TagFeature(thisNode, combinedTag, featureName);
                                fileOut.WriteLine(newFeature.ToString());
                            }
                            thisNode = combinedTag;
                        }
                        for (int wordIndex = 0; wordIndex < taggedTrainingData.Words.Count; wordIndex++)
                        {
                            string[] features = this.ExtractFeatures(rawSentence, tagList, wordIndex);
                            string combinedTag = "etkn";
                            if (wordIndex < taggedTrainingData.Words.Count - 1)
                            {
                                Array.Sort(tagList[wordIndex + 1]);
                                combinedTag = string.Join("+", tagList[wordIndex + 1]);
                            }

                            foreach (string featureName in features)
                            {
                                TagFeature newFeature = new TagFeature(thisNode, combinedTag, featureName);
                                fileOut.WriteLine(newFeature.ToString());
                            }
                            thisNode = combinedTag;
                        }
                    }

                    //fileOut.Close();
                }

                //trainingIn.Close();
            }
        }
    }
}
