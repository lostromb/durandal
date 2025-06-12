using Durandal.API.Utils;
using Durandal.Common.NLP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prototype
{
    public class TokenString
    {
        public string OriginalText;
        public List<string> Tokens;

        public override string ToString()
        {
            return OriginalText;
        }
    }

    public class TemplatePiece
    {
    }

    public class TemplateFork : TemplatePiece
    {
        public List<TemplatePiece> Choices = new List<TemplatePiece>();
    }

    public class TemplateRun : TemplatePiece
    {
        public TokenString Tokens;
    }

    public class TemplateSlot : TemplatePiece
    {
        public List<TokenString> SlotValues = new List<TokenString>();
    }

    public class SubstringHyp
    {
        public int TokenCount;
        public int Offset;
        public int StartIdx;
        public int EndIdx;
        public List<string> Tokens = new List<string>();

        public override string ToString()
        {
            return string.Format("{0}-{1}: {2}", StartIdx, EndIdx, string.Join(",", Tokens));
        }
    }

    public class RLCS
    {
        public static List<TemplatePiece> LCS(List<TemplatePiece> template, TokenString input)
        {
            // Find out the longest common substring
            List<SubstringHyp> hypotheses = new List<SubstringHyp>();

            SubstringHyp nextHyp = new SubstringHyp();
            for (int startIdx = 0; startIdx < input.Tokens.Count; startIdx++)
            {
                // Iterate through all template pieces
                bool nextToken = true;
                int endIdx = startIdx;
                int tIdx = 0;
                TemplatePiece piece = null;
                while (nextToken)
                {
                    if (piece == null)
                    {
                        // find next template piece to match
                        if (tIdx == template.Count)
                        {
                            nextToken = false;
                            // add final hyp
                            if (nextHyp.TokenCount > 0)
                                hypotheses.Add(nextHyp);
                        }
                        else
                            piece = template[tIdx++];
                    }
                    if (piece != null)
                    {
                        if (piece is TemplateRun)
                        {
                            TemplateRun run = piece as TemplateRun;
                            for (int pIdx = 0; pIdx < run.Tokens.Tokens.Count; pIdx++)
                            {
                                if (endIdx == input.Tokens.Count)
                                {
                                    // Reached end of input but not end of template
                                    nextToken = false;
                                    if (nextHyp.TokenCount > 0)
                                    {
                                        hypotheses.Add(nextHyp);
                                        nextHyp = new SubstringHyp();
                                    }
                                }
                                else if (input.Tokens[endIdx].Equals(run.Tokens.Tokens[pIdx]))
                                {
                                    // Find any submatches within a run
                                    if (nextHyp.TokenCount == 0)
                                    {
                                        nextHyp.StartIdx = endIdx;
                                    }
                                    nextHyp.Tokens.Add(input.Tokens[endIdx++]);
                                    nextHyp.EndIdx = endIdx;
                                    nextHyp.TokenCount++;
                                }
                                else if (nextHyp.TokenCount > 0)
                                {
                                    // No match; submit current hyp if possible
                                    hypotheses.Add(nextHyp);
                                    nextHyp = new SubstringHyp();
                                }
                            }
                            piece = null;
                        }
                        else if (piece is TemplateFork)
                        {
                            TemplateFork fork = piece as TemplateFork;
                            // Only process a fork if the first token matches
                            piece = null;
                        }
                    }
                }
            }

            // If no substrings matched, we need to augment the template
            if (hypotheses.Count == 0)
            {
                Console.WriteLine("No substrings matched");
            }

            // Inspect substring matches to find the largest

            // Return the augmented template
            return template;
        }

        public static void Test()
        {
            // Read and tokenize inputs
            IWordBreaker breaker = new WordBreakerEnglish();
            List<TokenString> strings = new List<TokenString>();
            string[] lines = File.ReadAllLines(@"C:\Users\Logan Stromberg\Desktop\weather.txt");
            foreach (string line in lines)
            {
                TokenString newStr = new TokenString();
                newStr.OriginalText = line;
                newStr.Tokens = new List<string>(breaker.Break(line).Words);
                strings.Add(newStr);
            }

            // Find the shortest one
            strings.Sort((a, b) => { return a.OriginalText.Length - b.OriginalText.Length; });

            // Find the edit distance between that shortest one and all the others and sort using that order
            TokenString shortest = strings[0];

            strings.Sort((a, b) =>
            {
                return (int)Math.Sign(DurandalUtils.NormalizedEditDistance(shortest.OriginalText, a.OriginalText) - DurandalUtils.NormalizedEditDistance(shortest.OriginalText, b.OriginalText));
            });

            // Now create a pattern and start processing the inputs in order from least complex to most complex
            List<TemplatePiece> curTemplate = new List<TemplatePiece>();
            curTemplate.Add(new TemplateRun()
            {
                Tokens = shortest
            });

            for (int i = 1; i < strings.Count; i++)
            {
                // Augment the current template by applying the recursive longest-common-substring algorithm
                curTemplate = LCS(curTemplate, strings[i]);
            }

            Console.WriteLine("Done");
        }
    }
}
