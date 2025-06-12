using Durandal.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe.NL
{
    /// <summary>
    /// Feature extractor for the statistical linebreak detector
    /// </summary>
    public static class LineBreakFeatureExtractor
    {
        private static readonly Regex WHITESPACE_MATCHER = new Regex("\\s{2,99}");
        private static readonly Regex NEWLINE_MATCHER = new Regex("[\\r\\n]+");
        private static readonly Regex MULTINEWLINE_MATCHER = new Regex("[\\r\\n]{3,99}");
        private static readonly Regex STOP_MATCHER = new Regex("[\\.\\!\\?]+");
        private static readonly Regex PAUSE_MATCHER = new Regex("[:,\\(\\)\\[\\]\\*]+");
        private static readonly Regex DIGIT_MATCHER = new Regex("(\\d+|first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth)[\\.\\)\\:]*");

        public static void ExtractFeatures(Sentence utterance, int wordIndex, ref List<string> context)
        {
            // The word itself
            string theWord = utterance.Words[wordIndex];
            context.Add("w0:" + SanitizeContext(theWord));

            if (theWord.Length > 0 && char.IsUpper(theWord[0]))
            {
                context.Add("cap");
            }
            if (DIGIT_MATCHER.Match(theWord).Success)
            {
                context.Add("digit");
            }

            // with our wordbreaker this should never happen
            if (STOP_MATCHER.Match(theWord).Success)
            {
                context.Add("stop");
            }

            if (PAUSE_MATCHER.Match(theWord).Success)
            {
                context.Add("pause");
            }
            
            if (NEWLINE_MATCHER.Match(utterance.NonTokens[wordIndex]).Success)
            {
                context.Add("nl-before");
            }
            if (NEWLINE_MATCHER.Match(utterance.NonTokens[wordIndex + 1]).Success)
            {
                context.Add("nl-after");
            }
            if (MULTINEWLINE_MATCHER.Match(utterance.NonTokens[wordIndex]).Success)
            {
                context.Add("mnl-before");
            }
            if (MULTINEWLINE_MATCHER.Match(utterance.NonTokens[wordIndex + 1]).Success)
            {
                context.Add("mnl-after");
            }
            if (WHITESPACE_MATCHER.Match(utterance.NonTokens[wordIndex]).Success)
            {
                context.Add("ws-before");
            }
            if (WHITESPACE_MATCHER.Match(utterance.NonTokens[wordIndex + 1]).Success)
            {
                context.Add("ws-after");
            }
            if (STOP_MATCHER.Match(utterance.NonTokens[wordIndex]).Success)
            {
                context.Add("stop-before");
            }
            if (STOP_MATCHER.Match(utterance.NonTokens[wordIndex + 1]).Success)
            {
                context.Add("stop-after");
            }
            if (PAUSE_MATCHER.Match(utterance.NonTokens[wordIndex]).Success)
            {
                context.Add("pause-before");
            }
            if (PAUSE_MATCHER.Match(utterance.NonTokens[wordIndex + 1]).Success)
            {
                context.Add("pause-after");
            }
            if (DIGIT_MATCHER.Match(utterance.NonTokens[wordIndex]).Success)
            {
                context.Add("digit-before");
            }
            if (DIGIT_MATCHER.Match(utterance.NonTokens[wordIndex + 1]).Success)
            {
                context.Add("digit-after");
            }

            // word context ahead +3 and behind -3
            for (int c = 1; c <= 3; c++)
            {
                if (wordIndex + c < utterance.Words.Count)
                {
                    context.Add("w" + c + ":" + SanitizeContext(utterance.Words[wordIndex + c]));
                }
                else
                {
                    context.Add("w" + c + ":ETKN");
                }

                if (wordIndex - c > 0)
                {
                    context.Add("w-" + c + ":" + SanitizeContext(utterance.Words[wordIndex - c]));
                }
                else
                {
                    context.Add("w-" + c + ":STKN");
                }
            }

            // nontoken context ahead +2 and behind -2
            for (int c = 0; c < 2; c++)
            {
                if (wordIndex + c + 1 < utterance.NonTokens.Count)
                {
                    context.Add("nt" + (c + 1) + ":" + SanitizeContext(utterance.NonTokens[wordIndex + c + 1]));
                }
                else
                {
                    context.Add("nt" + (c + 1) + ":ETKN");
                }

                if (wordIndex - c > 0)
                {
                    context.Add("nt-" + (c + 1) + ":" + SanitizeContext(utterance.NonTokens[wordIndex - c]));
                }
                else
                {
                    context.Add("nt-" + (c + 1) + ":STKN");
                }
            }
        }

        private static string SanitizeContext(string context)
        {
            return context.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace(" ", "_");
        }
    }
}
