
namespace Durandal.Common.NLP.Language.English
{
    using System;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.API;
    using Durandal.Common.Utils;
    using LU;

    public class EnglishWordBreaker : IWordBreaker
    {
        //                                                   contractions   words             punctuation                sign  integers       decimals
        private static readonly Regex splitter = new Regex("(?:'?[a-zA-Z]+(?:\\-[a-zA-Z]*)?)|(?:[\\(\\)\\.\\?\\\"])|(?:(?:[-+])?(?:[0-9]+[\\.,])?[0-9]+)");

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
                LexicalForm = string.Empty
            };

            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder cleanedString = pooledSb.Builder;
                bool lastCharWasTerminator = false;
                bool lastCharWasInvalid = false;
                foreach (char c in input.Utterance)
                {
                    // Preserve letter characters and delimiters
                    if (char.IsLetterOrDigit(c) ||
                        c == '\'' ||
                        c == '-' ||
                        c == ':' ||
                        c == '\"' ||
                        c == '(' ||
                        c == ')')
                    {
                        cleanedString.Append(c);
                        lastCharWasTerminator = false;
                        lastCharWasInvalid = false;
                    }
                    else if (c == '.' || c == '?')
                    {
                        // Condense all ellipses/question marks together
                        if (!lastCharWasTerminator)
                            cleanedString.Append(c);
                        lastCharWasInvalid = false;
                        lastCharWasTerminator = true;
                    }
                    else // Replace everything else with spaces
                    {
                        if (!lastCharWasInvalid)
                            cleanedString.Append(' ');
                        lastCharWasInvalid = true;
                        lastCharWasTerminator = false;
                    }
                }

                // Match each word and punctuation inside the cleaned input
                MatchCollection matches = splitter.Matches(cleanedString.ToString());
                foreach (Match m in matches)
                {
                    returnVal.Words.Add(m.Value);
                }

                // Trim trailing periods and commas since they have no semantic value
                if (returnVal.Words.Count > 0)
                {
                    string lastWord = returnVal.Words[returnVal.Words.Count - 1];
                    if (lastWord.Equals(".") || lastWord.Equals(","))
                    {
                        returnVal.Words.RemoveAt(returnVal.Words.Count - 1);
                    }
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
}
