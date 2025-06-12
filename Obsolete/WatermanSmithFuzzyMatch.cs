namespace Communication.Contacts.FuzzyMatching.WatermanSmith
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// naive and raw implementation of the waterman-smith algorithm
    /// https://en.wikipedia.org/wiki/Smith%E2%80%93Waterman_algorithm
    /// This implementation is more resembling the Needleman–Wunsch algorithm
    /// https://en.wikipedia.org/wiki/Needleman%E2%80%93Wunsch_algorithm
    /// </summary>
    public class WatermanSmithFuzzyMatch : IFuzzyMatchAlgorithm
    {
        /// <summary>
        /// Maximal score of the algorithm (normalizes)
        /// </summary>
        private const double MaxScore = 1.0;

        /// <summary>
        /// Number of segments - currently naively limited to 2
        /// </summary>
        private const int NumberOfSegments = 2;

        /// <summary>
        /// Characters to split string by
        /// </summary>
        private static readonly char[] StringSeparators = { ' ', '\t', '-' };

        /// <summary>
        /// Scoring matrix
        /// </summary>
        private static readonly double[,] Score = // new int[24,24]
        { // a     b     c     d     e     f     g     h     i     j     k     l     m     n     o     p     q     r     s     t     u     v     w     x     y     z
            /*a*/{ +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*b*/{ -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*c*/{ -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*d*/{ -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*e*/{ -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*f*/{ -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*g*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*h*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*i*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*g*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*k*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*l*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*m*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*n*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*o*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*p*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*q*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*r*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*s*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*t*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*u*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1, -0.1 },
            /*v*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1, -0.1 },
            /*w*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1, -0.1 },
            /*x*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1, -0.1 },
            /*y*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0, -0.1 },
            /*z*/{ -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, -0.1, +1.0 }
        };

        /// <summary>
        /// Gap score matrix
        /// </summary>
        private static readonly double[] GapScore =
            
            // a       b      c      d      e      f      g      h      i      j      k      l      m      n      o      p      q      r      s      t      u      v      w      x      y      z
            { -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11, -0.11 };

        /// <summary>
        /// the algorithm's minimal score 
        /// </summary>
        private readonly double minScore;

        /// <summary>
        /// cutoff value - used for normalization 
        /// </summary>
        private readonly double cutoff;

        /// <summary>
        /// score penalty for matching prefix substing (instead of full string)
        /// </summary>
        private readonly double prefixSubstringMatchingScoringPenalty;

        /// <summary>
        /// score penalty for matching suffix substing (instead of full string)
        /// </summary>
        private readonly double suffixSubstringMatchingScoringPenalty;

        /// <summary>
        /// score penalty for matching shuffeled segments string (instead of original string)
        /// </summary>
        private readonly double shuffledSegmentsMatchingScoringPenalty;

        public WatermanSmithFuzzyMatch()
        {
            this.ScoreThreshold = 0.2;
            this.minScore = 0.0;
            this.cutoff = 0.5;
            this.prefixSubstringMatchingScoringPenalty = 0.8;
            this.suffixSubstringMatchingScoringPenalty = 0.5;
            this.shuffledSegmentsMatchingScoringPenalty = 0.9;
        }

        /// <summary>
        /// Gets the lower bound for a match score that we consider valid with this specific algorithm
        /// </summary>
        public double ScoreThreshold { get; private set; }

        public double ScoreMatch(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            {
                return this.minScore;
            }

            var scores = new List<double>
            {
                // score for matching original strings
                this.WatermanSmithScore(s1, s2),

                // max score for s1 segments matched against original s2
                this.TryMatchSegments(s1, s2),

                // max score for s2 segments matched against original s1
                this.TryMatchSegments(s2, s1)
            };

            // return the highest score
            return scores.Max();
        }

        private double TryMatchSegments(string stringToSplit, string stringToMatchAgainst)
        {
            // split to segments 
            string[] segments = stringToSplit.Trim().Split(StringSeparators, NumberOfSegments, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != NumberOfSegments)
            {
                return this.minScore;
            }

            var scores = new List<double>
            {
                // penalized score for matching prefix
                this.WatermanSmithScore(segments[0], stringToMatchAgainst) * this.prefixSubstringMatchingScoringPenalty,
 
                // penalized score for matching suffix
                this.WatermanSmithScore(segments[1], stringToMatchAgainst) * this.suffixSubstringMatchingScoringPenalty,
                
                // penalized score for matching shuffled segments string
                this.WatermanSmithScore(segments[1] + segments[0], stringToMatchAgainst) * this.shuffledSegmentsMatchingScoringPenalty
            };

            return scores.Max();
        }

        private double WatermanSmithScore(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            {
                return this.minScore;
            }

            // Filter unsupported (not in a-z) chars
            int[] c1 = s1.ToLower(CultureInfo.InvariantCulture).ToCharArray().Where(c => c >= 'a' && c <= 'z').Select(c => c - 'a').ToArray();
            int[] c2 = s2.ToLower(CultureInfo.InvariantCulture).ToCharArray().Where(c => c >= 'a' && c <= 'z').Select(c => c - 'a').ToArray();

            // all the original string chars were not a-z
            if (c1.Length == 0 || c2.Length == 0)
            {
                return this.minScore;
            }

            // Build table 
            var table = new double[c1.Length + 1, c2.Length + 1];
            table[0, 0] = 0.0;

            // Populate table

            // Init
            for (var i = 1; i <= c1.Length; i++)
            {
                table[i, 0] = table[i - 1, 0] + GapScore[c1[i - 1]];
            }

            for (var i = 1; i <= c2.Length; i++)
            {
                table[0, i] = table[0, i - 1] + GapScore[c2[i - 1]];
            }

            // step
            for (var i = 0; i < c1.Length; i++)
            {
                for (var j = 0; j < c2.Length; j++)
                {
                    // decide whether to use a gap in any of the strings or compare the origin chars 
                    double s1CharVSs2GapScore = table[i + 1, j] + GapScore[c2[j]];
                    double s1GapVSs2CharScore = table[i, j + 1] + GapScore[c1[i]];

                    double maxGapScore = Math.Max(s1CharVSs2GapScore, s1GapVSs2CharScore);

                    double charVsCharScore = table[i, j] + Score[c1[i], c2[j]];

                    table[i + 1, j + 1] = Math.Max(charVsCharScore, maxGapScore);
                }
            }

            // normalize 
            double finalScore = table[c1.Length, c2.Length] / Math.Max(c1.Length, c2.Length);
            if (finalScore < this.cutoff)
            {
                return this.minScore;
            }

            double normalizedScore = (finalScore - this.cutoff) / (MaxScore - this.cutoff);
            return Math.Max(normalizedScore, this.minScore);
        }
    }
}