
namespace Durandal.Common.NLP.Language.English
{
    using System;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.API;
    using Durandal.Common.Utils;
    using LU;

    /// <summary>
    /// Breaks English words, considering only whole words at a time. That is, it won't break apart hypthenated-words or apos'tro'phed words.
    /// </summary>
    public class EnglishWholeWordBreaker : IWordBreaker
    {
        private static readonly Regex matcher = new Regex("[a-zA-Z0-9'\\-]+");

        public Sentence Break(string input)
        {
            return Break(new LUInput()
            {
                Utterance = input,
                LexicalForm = string.Empty
            });
        }

        public Sentence Break(LUInput input)
        {
            Sentence returnVal = new Sentence()
            {
                OriginalText = input.Utterance,
                LexicalForm = input.LexicalForm
            };
            
            foreach (Match match in matcher.Matches(input.Utterance))
            {
                returnVal.Words.Add(match.Value);
            }

            // Recalculate the indices relative to the original input, not the cleaned string, otherwise they'll be out of alignment later
            int indexBase = 0;
            int lastIndex = 0;
            foreach (string word in returnVal.Words)
            {
                indexBase = input.Utterance.IndexOf(word, indexBase, StringComparison.Ordinal);
                if (indexBase < 0)
                {
                    throw new FormatException("Wordbreaker could not create reified word indices for phrase \"" + input + "\"");
                }

                returnVal.Indices.Add(indexBase);

                // Also capture the non-token characters
                returnVal.NonTokens.Add(input.Utterance.Substring(lastIndex, indexBase - lastIndex));

                indexBase += word.Length;
                lastIndex = indexBase;
            }

            returnVal.NonTokens.Add(input.Utterance.Substring(lastIndex));

            return returnVal;
        }
    }
}
