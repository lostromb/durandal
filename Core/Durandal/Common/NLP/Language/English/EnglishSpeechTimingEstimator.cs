using Durandal.API;
using Durandal.Common.Speech.TTS;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.NLP.Language.English
{
    public class EnglishSpeechTimingEstimator : ISpeechTimingEstimator
    {
        private const float MIN_BREAK_TIME_SECONDS = 0.200f;

        public IList<SynthesizedWord> EstimatePhraseWeights(Sentence words, string ssml, TimeSpan totalSpeechTime)
        {
            // FIXME this class does not account for any SSML tags such as <break> or the "spell the word" say-as variant, etc.
            // FIXME The rudimentary sentence length multiplication highly undervalues digits which are read aloud
            // FIXME should probably use # of consonants as a more accurate estimate of the spoken length of a word

            List<SynthesizedWord> returnVal = new List<SynthesizedWord>();
            float totalUtteranceLengthMs = (float)totalSpeechTime.TotalMilliseconds;

            // Now calculate the total mass of all words
            float totalWordMass = 0;
            for (int wordIdx = 0; wordIdx < words.Length; wordIdx++)
            {
                // This is very English-specific and SAPI-specific logic. Each letter in a word is approximately 71 ms
                totalWordMass += (words.Words[wordIdx].Length * 0.071f);
            }
            for (int nonTokenIdx = 0; nonTokenIdx < words.Length; nonTokenIdx++) // Specifically ignore the last nontoken because we assume its silence will be trimmed by postSilenceSamples
            {
                string nonToken = words.NonTokens[nonTokenIdx];
                totalWordMass += GetMassOfNonTokenEnglish(nonToken);
            }

            // Now we can go through and calculate the timing of each word
            float weightToMsMultiplier = totalUtteranceLengthMs / totalWordMass;
            //float initialPause = GetMassOfNonTokenEnglish(words.NonTokens[0]) * weightToMsMultiplier;
            //if (initialPause > MIN_BREAK_TIME_SECONDS)
            //{
            //    returnVal.Add(new WeightedWord()
            //    {
            //        Word = null,
            //        ApproximateLength = TimeSpan.FromSeconds(initialPause)
            //    });
            //}

            float currentMs = 0;
            for (int wordIdx = 0; wordIdx < words.Length; wordIdx++)
            {
                string currentWord = words.Words[wordIdx];
                bool isLastWord = wordIdx == words.Length - 1;
                float wordWeight = currentWord.Length * 0.071f;
                float nonTokenWeight = isLastWord ? 0 : GetMassOfNonTokenEnglish(words.NonTokens[wordIdx + 1]);

                if (nonTokenWeight < MIN_BREAK_TIME_SECONDS)
                {
                    returnVal.Add(new SynthesizedWord()
                    {
                        Word = currentWord,
                        ApproximateLength = TimeSpan.FromMilliseconds((wordWeight + nonTokenWeight) * weightToMsMultiplier),
                        Offset = TimeSpan.FromMilliseconds(currentMs)
                    });
                    currentMs += (wordWeight + nonTokenWeight) * weightToMsMultiplier;
                }
                else
                {
                    returnVal.Add(new SynthesizedWord()
                    {
                        Word = currentWord,
                        ApproximateLength = TimeSpan.FromMilliseconds(wordWeight * weightToMsMultiplier),
                        Offset = TimeSpan.FromMilliseconds(currentMs)
                    });
                    currentMs += wordWeight * weightToMsMultiplier;
                    returnVal.Add(new SynthesizedWord()
                    {
                        Word = null,
                        ApproximateLength = TimeSpan.FromMilliseconds(nonTokenWeight * weightToMsMultiplier),
                        Offset = TimeSpan.FromMilliseconds(currentMs)
                    });
                    currentMs += nonTokenWeight * weightToMsMultiplier;
                }
            }

            return returnVal;
        }

        private static float GetMassOfNonTokenEnglish(string nonToken)
        {
            if (nonToken.StartsWith(".") || nonToken.StartsWith("?") || nonToken.StartsWith("!"))
            {
                return 0.900f;
            }
            else if (nonToken.StartsWith("—") || nonToken.StartsWith(" —")) // emdash
            {
                return 0.700f;
            }
            else if (nonToken.StartsWith(":") || nonToken.StartsWith(";"))
            {
                return 0.650f;
            }
            else if (nonToken.StartsWith("-") || nonToken.StartsWith(" -") ||
                nonToken.StartsWith("–") || nonToken.StartsWith(" –")) // endash and hyphen
            {
                return 0.500f;
            }
            else if (nonToken.StartsWith(","))
            {
                return 0.350f;
            }
            else
            {
                return 0;
            }
        }
    }
}
