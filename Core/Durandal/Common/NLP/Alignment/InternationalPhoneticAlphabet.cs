using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.NLP.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Alignment
{
    #region Lookup table and divergence algorithm

    /// <summary>
    /// Provides helper methods to compare the similarity of IPA strings
    /// </summary>
    public static class InternationalPhoneticAlphabet
    {
        // default weights - used for all comparisons internally
        private static readonly IpaWeights _weights = new IpaWeights();

        /// <summary>
        /// Normalized edit distance (Levenstein) algorithm for computing divergence of two IPA syllable strings
        /// </summary>
        /// <param name="one">An IPA string, such as "geɪtwʊəd"</param>
        /// <param name="two">An IPA string, such as "məkɹeɪkəɹ"</param>
        /// <param name="locale">The locale of the strings, which might affect the weighting of certain IPA features (such as accounting for tonal-sensitive languages like Mandarin)</param>
        /// <returns>A divergence value from 0 (identical) to 1 (maximum divergence)</returns>
        public static float EditDistance(string one, string two, LanguageCode locale)
        {
            return EditDistance(one, two, _weights);
        }

        /// <summary>
        /// Normalized edit distance (Levenstein) algorithm for computing divergence of two IPA syllable strings
        /// </summary>
        /// <param name="one"></param>
        /// <param name="two"></param>
        /// <param name="weights">The weights matrix to use</param>
        /// <returns>A divergence value</returns>
        internal static float EditDistance(string one, string two, IpaWeights weights)
        {
            string compareOne = one;
            string compareTwo = two;

            // The old magic box
            float[] gridA = new float[one.Length + 1];
            float[] gridB = new float[one.Length + 1];
            float[] distA = new float[one.Length + 1];
            float[] distB = new float[one.Length + 1];
            float[] temp;

            // Initialize the horizontal grid values
            float horizCost = 0;
            float vertCost = 0;
            IpaSyllable edgeSyllable;
            for (int x = 0; x <= one.Length; x++)
            {
                if (x > 0 && IpaSyllables.TryGetValue(compareOne[x - 1], out edgeSyllable))
                {
                    horizCost += edgeSyllable.InsertionCost(_weights);
                }

                gridA[x] = horizCost;
                distA[x] = x;
            }

            for (int y = 1; y <= two.Length; y++)
            {
                // Initialize the vertical grid value
                if (IpaSyllables.TryGetValue(compareTwo[y - 1], out edgeSyllable))
                {
                    vertCost += edgeSyllable.InsertionCost(weights);
                }

                gridB[0] = vertCost;
                distB[0] = y;

                // Iterate through the DP table
                for (int x = 1; x <= one.Length; x++)
                {
                    float diagWeight = gridA[x - 1];
                    float leftWeight = gridB[x - 1];
                    float upWeight = gridA[x];
                    char symbolOne = compareOne[x - 1];
                    char symbolTwo = compareTwo[y - 1];
                    IpaSyllable syllableOne = null;
                    IpaSyllable syllableTwo = null;
                    bool unrecognized = false;
                    if (!IpaSyllables.TryGetValue(symbolOne, out syllableOne) ||
                        !IpaSyllables.TryGetValue(symbolTwo, out syllableTwo))
                    {
                        // Character not recognized. Assume it is a diacritic mark, which we really don't distinguish.
                        // In this case force an insertion/deletion so this character doesn't match anything, but doesn't add any cost either
                        unrecognized = true;
                    }
                    else
                    {
                        if (symbolOne != symbolTwo)
                        {
                            diagWeight += syllableOne.Divergence(syllableTwo, weights);
                            leftWeight += syllableOne.InsertionCost(weights);
                            upWeight += syllableTwo.InsertionCost(weights);
                        }
                        else
                        {
                            leftWeight += syllableOne.InsertionCost(weights);
                            upWeight += syllableTwo.InsertionCost(weights);
                        }
                    }

                    if (!unrecognized && diagWeight < leftWeight && diagWeight < upWeight)
                    {
                        gridB[x] = diagWeight;
                        distB[x] = distA[x - 1] + 1;
                    }
                    else if (leftWeight < upWeight)
                    {
                        gridB[x] = leftWeight;
                        distB[x] = distB[x - 1] + 1;
                    }
                    else
                    {
                        gridB[x] = upWeight;
                        distB[x] = distA[x] + 1;
                    }
                }

                // Swap the buffers
                temp = gridA;
                gridA = gridB;
                gridB = temp;

                temp = distA;
                distA = distB;
                distB = temp;
            }

            // Extract the return value from the corner of the DP table
            float minWeight = gridA[one.Length];
            // old method: Normalize it based on the length of the path that was taken
            // this doesn't work too well when the strings are highly dimorphic in length
            //float pathLength = distA[one.Length];

            // new method: ideal path length is just the length of the shortest input string (since the ideal match would be 1:1 along the diagonal)
            float pathLength = Math.Min(one.Length, two.Length);

            if (pathLength == 0)
                return 0;

            return (FastMath.Sigmoid(minWeight / pathLength / 10f) * 2) - 1; // 10 is just a rough "normalize to 1.0" value
        }

        /// <summary>
        /// The dictionary of all IPA syllables and their properties.
        /// Taken from http://www.internationalphoneticalphabet.org/ipa-sounds/ipa-chart-with-sounds/
        /// </summary>
        private static readonly Dictionary<char, IpaSyllable> IpaSyllables = new Dictionary<char, IpaSyllable>
        {
            // Vowels
            { 'i', new IpaVowel(IpaVowelHeight.Close, IpaVowelBackness.Front, IpaVowelRoundness.Unrounded) },
            { 'y', new IpaVowel(IpaVowelHeight.Close, IpaVowelBackness.Front, IpaVowelRoundness.Rounded) },
            { 'ɨ', new IpaVowel(IpaVowelHeight.Close, IpaVowelBackness.Central, IpaVowelRoundness.Unrounded) },
            { 'ʉ', new IpaVowel(IpaVowelHeight.Close, IpaVowelBackness.Central, IpaVowelRoundness.Rounded) },
            { 'ɯ', new IpaVowel(IpaVowelHeight.Close, IpaVowelBackness.Back, IpaVowelRoundness.Unrounded) },
            { 'u', new IpaVowel(IpaVowelHeight.Close, IpaVowelBackness.Back, IpaVowelRoundness.Rounded) },
            { 'ɪ', new IpaVowel(IpaVowelHeight.NearClose, IpaVowelBackness.NearFront, IpaVowelRoundness.Unrounded) },
            { 'ʏ', new IpaVowel(IpaVowelHeight.NearClose, IpaVowelBackness.NearFront, IpaVowelRoundness.Rounded) },
            { 'ʊ', new IpaVowel(IpaVowelHeight.NearClose, IpaVowelBackness.NearBack, IpaVowelRoundness.Unrounded) },
            { 'e', new IpaVowel(IpaVowelHeight.CloseMid, IpaVowelBackness.Front, IpaVowelRoundness.Unrounded) },
            { 'ø', new IpaVowel(IpaVowelHeight.CloseMid, IpaVowelBackness.NearFront, IpaVowelRoundness.Rounded) },
            { 'ɘ', new IpaVowel(IpaVowelHeight.CloseMid, IpaVowelBackness.Central, IpaVowelRoundness.Unrounded) },
            { 'ɵ', new IpaVowel(IpaVowelHeight.CloseMid, IpaVowelBackness.NearBack, IpaVowelRoundness.Rounded) },
            { 'ɤ', new IpaVowel(IpaVowelHeight.CloseMid, IpaVowelBackness.Back, IpaVowelRoundness.Unrounded) },
            { 'o', new IpaVowel(IpaVowelHeight.CloseMid, IpaVowelBackness.Back, IpaVowelRoundness.Rounded) },
            { 'ə', new IpaVowel(IpaVowelHeight.Mid, IpaVowelBackness.NearBack, IpaVowelRoundness.Unrounded) },
            { 'ɛ', new IpaVowel(IpaVowelHeight.OpenMid, IpaVowelBackness.NearFront, IpaVowelRoundness.Unrounded) },
            { 'œ', new IpaVowel(IpaVowelHeight.OpenMid, IpaVowelBackness.Central, IpaVowelRoundness.Rounded) },
            { 'ɜ', new IpaVowel(IpaVowelHeight.OpenMid, IpaVowelBackness.Central, IpaVowelRoundness.Unrounded) },
            { 'ɞ', new IpaVowel(IpaVowelHeight.OpenMid, IpaVowelBackness.NearBack, IpaVowelRoundness.Rounded) },
            { 'ʌ', new IpaVowel(IpaVowelHeight.OpenMid, IpaVowelBackness.Back, IpaVowelRoundness.Unrounded) },
            { 'ɔ', new IpaVowel(IpaVowelHeight.OpenMid, IpaVowelBackness.Back, IpaVowelRoundness.Rounded) },
            { 'æ', new IpaVowel(IpaVowelHeight.NearOpen, IpaVowelBackness.NearFront, IpaVowelRoundness.Unrounded) },
            { 'ɐ', new IpaVowel(IpaVowelHeight.NearOpen, IpaVowelBackness.NearBack, IpaVowelRoundness.Unrounded) },
            { 'a', new IpaVowel(IpaVowelHeight.Open, IpaVowelBackness.Central, IpaVowelRoundness.Unrounded) },
            { 'ä', new IpaVowel(IpaVowelHeight.Open, IpaVowelBackness.NearBack, IpaVowelRoundness.Rounded) },
            { 'ɑ', new IpaVowel(IpaVowelHeight.Open, IpaVowelBackness.Back, IpaVowelRoundness.Unrounded) },
            { 'ɒ', new IpaVowel(IpaVowelHeight.Open, IpaVowelBackness.Back, IpaVowelRoundness.Rounded) },

            // Pulmonic Consonants
            { 'p', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Bilabial, IpaPulmonicVoice.Nonvoiced) },
            { 'b', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Bilabial, IpaPulmonicVoice.Voiced) },
            { 't', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Alveolar, IpaPulmonicVoice.Nonvoiced) },
            { 'd', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Alveolar, IpaPulmonicVoice.Voiced) },
            { 'ʈ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Retroflex, IpaPulmonicVoice.Nonvoiced) },
            { 'ɖ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Retroflex, IpaPulmonicVoice.Voiced) },
            { 'c', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Palatial, IpaPulmonicVoice.Nonvoiced) },
            { 'ɟ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Palatial, IpaPulmonicVoice.Voiced) },
            { 'k', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Velar, IpaPulmonicVoice.Nonvoiced) },
            { 'g', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Velar, IpaPulmonicVoice.Voiced) },
            { 'q', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Uvular, IpaPulmonicVoice.Nonvoiced) },
            { 'ɢ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Uvular, IpaPulmonicVoice.Voiced) },
            { 'ʔ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Plosive, IpaPulmonicLocation.Glottal, IpaPulmonicVoice.Nonvoiced) },
            { 'm', new IpaPulmonicConsonant(IpaPulmonicArticulation.Nasal, IpaPulmonicLocation.Bilabial, IpaPulmonicVoice.Voiced) },
            { 'ɱ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Nasal, IpaPulmonicLocation.Labiodental, IpaPulmonicVoice.Voiced) },
            { 'n', new IpaPulmonicConsonant(IpaPulmonicArticulation.Nasal, IpaPulmonicLocation.Alveolar, IpaPulmonicVoice.Voiced) },
            { 'ɳ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Nasal, IpaPulmonicLocation.Retroflex, IpaPulmonicVoice.Voiced) },
            { 'ɲ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Nasal, IpaPulmonicLocation.Palatial, IpaPulmonicVoice.Voiced) },
            { 'ŋ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Nasal, IpaPulmonicLocation.Velar, IpaPulmonicVoice.Voiced) },
            { 'ɴ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Nasal, IpaPulmonicLocation.Uvular, IpaPulmonicVoice.Voiced) },
            { 'ʙ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Trill, IpaPulmonicLocation.Bilabial, IpaPulmonicVoice.Voiced) },
            { 'r', new IpaPulmonicConsonant(IpaPulmonicArticulation.Trill, IpaPulmonicLocation.Alveolar, IpaPulmonicVoice.Voiced) },
            { 'ʀ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Trill, IpaPulmonicLocation.Uvular, IpaPulmonicVoice.Voiced) },
            { 'ɾ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Tap, IpaPulmonicLocation.Alveolar, IpaPulmonicVoice.Voiced) },
            { 'ɽ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Tap, IpaPulmonicLocation.Retroflex, IpaPulmonicVoice.Voiced) },
            { 'ɸ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Bilabial, IpaPulmonicVoice.Nonvoiced) },
            { 'β', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Bilabial, IpaPulmonicVoice.Voiced) },
            { 'f', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Labiodental, IpaPulmonicVoice.Nonvoiced) },
            { 'v', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Labiodental, IpaPulmonicVoice.Voiced) },
            { 'θ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Dental, IpaPulmonicVoice.Nonvoiced) },
            { 'ð', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Dental, IpaPulmonicVoice.Voiced) },
            { 's', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Alveolar, IpaPulmonicVoice.Nonvoiced) },
            { 'z', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Alveolar, IpaPulmonicVoice.Voiced) },
            { 'ʃ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Postalveolar, IpaPulmonicVoice.Nonvoiced) },
            { 'ʒ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Postalveolar, IpaPulmonicVoice.Voiced) },
            { 'ʂ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Retroflex, IpaPulmonicVoice.Nonvoiced) },
            { 'ʐ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Retroflex, IpaPulmonicVoice.Voiced) },
            { 'ç', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Palatial, IpaPulmonicVoice.Nonvoiced) },
            { 'ʝ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Palatial, IpaPulmonicVoice.Voiced) },
            { 'x', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Velar, IpaPulmonicVoice.Nonvoiced) },
            { 'ɣ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Velar, IpaPulmonicVoice.Voiced) },
            { 'χ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Uvular, IpaPulmonicVoice.Nonvoiced) },
            { 'ʁ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Uvular, IpaPulmonicVoice.Voiced) },
            { 'ħ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Pharyngeal, IpaPulmonicVoice.Nonvoiced) },
            { 'ʕ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Pharyngeal, IpaPulmonicVoice.Voiced) },
            { 'h', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Glottal, IpaPulmonicVoice.Nonvoiced) },
            { 'ɦ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Fricative, IpaPulmonicLocation.Glottal, IpaPulmonicVoice.Voiced) },
            { 'ɬ', new IpaPulmonicConsonant(IpaPulmonicArticulation.LateralFricative, IpaPulmonicLocation.Alveolar, IpaPulmonicVoice.Nonvoiced) },
            { 'ɮ', new IpaPulmonicConsonant(IpaPulmonicArticulation.LateralFricative, IpaPulmonicLocation.Alveolar, IpaPulmonicVoice.Voiced) },
            { 'ʋ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Approximant, IpaPulmonicLocation.Labiodental, IpaPulmonicVoice.Voiced) },
            // Note: Most IPA spellers, at least in English, seem to write out a plain "r" (trill) symbol when they actually mean the lateral approximant "ɹ".
            // This affects accuracy since a trill articulation is very wrong in that case
            { 'ɹ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Approximant, IpaPulmonicLocation.Alveolar, IpaPulmonicVoice.Voiced) },
            { 'ɻ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Approximant, IpaPulmonicLocation.Retroflex, IpaPulmonicVoice.Voiced) },
            { 'j', new IpaPulmonicConsonant(IpaPulmonicArticulation.Approximant, IpaPulmonicLocation.Palatial, IpaPulmonicVoice.Voiced) },
            { 'ɰ', new IpaPulmonicConsonant(IpaPulmonicArticulation.Approximant, IpaPulmonicLocation.Velar, IpaPulmonicVoice.Voiced) },
            { 'l', new IpaPulmonicConsonant(IpaPulmonicArticulation.LateralApproximant, IpaPulmonicLocation.Alveolar, IpaPulmonicVoice.Voiced) },
            { 'ɭ', new IpaPulmonicConsonant(IpaPulmonicArticulation.LateralApproximant, IpaPulmonicLocation.Retroflex, IpaPulmonicVoice.Voiced) },
            { 'ʎ', new IpaPulmonicConsonant(IpaPulmonicArticulation.LateralApproximant, IpaPulmonicLocation.Palatial, IpaPulmonicVoice.Voiced) },
            { 'ʟ', new IpaPulmonicConsonant(IpaPulmonicArticulation.LateralApproximant, IpaPulmonicLocation.Velar, IpaPulmonicVoice.Voiced) },

            // Non-pulmonic consonants. Kind of out of scope for this but we'll include them for completeness
            { 'ʘ', new IpaNonPulmonicConsonant(IpaNonPulmonicArticulation.Click) },
            { 'ǀ', new IpaNonPulmonicConsonant(IpaNonPulmonicArticulation.Click) },
            { 'ǃ', new IpaNonPulmonicConsonant(IpaNonPulmonicArticulation.Click) },
            { 'ǂ', new IpaNonPulmonicConsonant(IpaNonPulmonicArticulation.Click) },
            { 'ǁ', new IpaNonPulmonicConsonant(IpaNonPulmonicArticulation.Click) },
            { 'ɓ', new IpaNonPulmonicConsonant(IpaNonPulmonicArticulation.VoicedImplosive) },
            { 'ɗ', new IpaNonPulmonicConsonant(IpaNonPulmonicArticulation.VoicedImplosive) },
            { 'ʄ', new IpaNonPulmonicConsonant(IpaNonPulmonicArticulation.VoicedImplosive) },
            { 'ɠ', new IpaNonPulmonicConsonant(IpaNonPulmonicArticulation.VoicedImplosive) },
            { 'ʛ', new IpaNonPulmonicConsonant(IpaNonPulmonicArticulation.VoicedImplosive) }
        };
    }

    #endregion

    #region Enums

    internal enum IpaClass
    {
        Vowel = 0,
        PulmonicConsonant = 1,
        NonPulmonicConsonant = 2
    }

    internal enum IpaVowelHeight
    {
        Close = 0,
        NearClose = 1,
        CloseMid = 2,
        Mid = 3,
        OpenMid = 4,
        NearOpen = 5,
        Open = 6
    }

    internal enum IpaVowelBackness
    {
        Front = 0,
        NearFront = 1,
        Central = 2,
        NearBack = 3,
        Back = 4
    }

    internal enum IpaVowelRoundness
    {
        Unrounded = 0,
        Rounded = 1
    }

    internal enum IpaPulmonicLocation
    {
        Bilabial = 0,
        Labiodental = 1,
        Dental = 2,
        Alveolar = 3,
        Postalveolar = 4,
        Retroflex = 5,
        Palatial = 6,
        Velar = 7,
        Uvular = 8,
        Pharyngeal = 9,
        Glottal = 10
    }

    internal enum IpaPulmonicArticulation
    {
        Plosive = 0,
        Nasal = 1,
        Trill = 2,
        Tap = 3,
        Fricative = 4,
        LateralFricative = 5,
        Approximant = 6,
        LateralApproximant = 7
    }

    internal enum IpaPulmonicVoice
    {
        Nonvoiced = 0,
        Voiced = 1
    }

    internal enum IpaNonPulmonicArticulation
    {
        Click = 0,
        VoicedImplosive = 1,
        /*Ejective = 2 */
    }

    #endregion

    #region Syllable classes

    internal abstract class IpaSyllable
    {
        public IpaClass Class { get; private set; }

        public IpaSyllable(IpaClass ipaClass)
        {
            Class = ipaClass;
        }

        internal abstract float Divergence(IpaSyllable other, IpaWeights weights);

        internal float InsertionCost(IpaWeights weights)
        {
            switch (Class)
            {
                case IpaClass.Vowel:
                    return weights.VowelInsdelCost;
                case IpaClass.PulmonicConsonant:
                    return weights.PulmonicConsonantInsdelCost;
                case IpaClass.NonPulmonicConsonant:
                    return weights.NonPulmonicConsonantInsdelCost;
            }

            return 0f;
        }

        protected static float[][] DivergenceIpaClass = new float[][]
        {
            new float[] {  0f, 5f, 10f },
            new float[] { 5f,  0f,  3f },
            new float[] { 10f,  3f,  0f }
        };

        protected static float[][] DivergencePulmonicLocation = new float[][]
        {
            //             BL   LD    D  ALV   PO   RF   PA   VE   UV   PH   GL
            new float[] {   0,   2,   2,   3,   4,   4,   5,   5,   5,   4,   4   }, // BL
            new float[] {   2,   0,   2,   3,   4,   4,   5,   5,   5,   4,   4   }, // LD
            new float[] {   2,   2,   0,   1,   3,   5,   4,   6,   6,   6,   6   }, // D
            new float[] {   3,   3,   1,   0,   1,   2,   1,   4,   6,   6,   6   }, // ALV
            new float[] {   4,   4,   3,   1,   0,   1,   1,   2,   3,   4,   4   }, // PO
            new float[] {   4,   4,   5,   2,   1,   0,   2,   3,   3,   4,   4   }, // RF
            new float[] {   5,   5,   4,   1,   1,   2,   0,   3,   5,   5,   5   }, // PA
            new float[] {   5,   5,   6,   4,   2,   3,   3,   0,   1,   2,   2   }, // VE
            new float[] {   5,   5,   6,   6,   3,   3,   5,   1,   0,   1,   1   }, // UV
            new float[] {   4,   4,   6,   6,   4,   4,   5,   2,   1,   0,   1   }, // PH
            new float[] {   4,   4,   6,   6,   4,   4,   5,   2,   1,   1,   0   }, // GL
        };

        protected static float[][] DivergencePulmonicArticulation = new float[][]
        {
            //             PL   NA   TR   TA   FR   LF   AP   LA
            new float[] {   0,   2,   5,   4,   4,   6,   6,   5   }, // PL
            new float[] {   2,   0,   5,   4,   4,   6,   6,   4   }, // NA
            new float[] {   5,   5,   0,   2,   5,   5,   4,   5   }, // TR
            new float[] {   4,   4,   2,   0,   3,   4,   2,   2   }, // TA
            new float[] {   4,   4,   5,   3,   0,   2,   4,   5   }, // FR
            new float[] {   6,   6,   5,   4,   2,   0,   3,   4   }, // LF
            new float[] {   6,   6,   4,   2,   4,   3,   0,   1   }, // AP
            new float[] {   5,   4,   5,   2,   5,   4,   1,   0   }, // LA
        };

        protected static float[][] DivergencePulmonicVoice = new float[][]
        {
            new float[] { 0, 1 },
            new float[] { 1, 0 },
        };

        protected static float[][] DivergenceNonPulmonicArticulation = new float[][]
        {
            new float[] { 0, 6 },
            new float[] { 6, 0 },
        };

        protected static float[][] DivergenceVowelHeight = new float[][]
        {
            new float[] {   0,   1,   1,   2,   2,   3,   3   },
            new float[] {   1,   0,   1,   1,   3,   3,   4   },
            new float[] {   1,   1,   0,   1,   1,   2,   3   },
            new float[] {   2,   1,   1,   0,   1,   2,   1   },
            new float[] {   2,   3,   1,   1,   0,   1,   1   },
            new float[] {   3,   3,   2,   2,   1,   0,   1   },
            new float[] {   3,   4,   3,   1,   1,   1,   0   },
        };

        protected static float[][] DivergenceVowelBackness = new float[][]
        {
            new float[] {   0,   2,   2,   3,   3   },
            new float[] {   2,   0,   1,   2,   2   },
            new float[] {   2,   1,   0,   1,   3   },
            new float[] {   3,   2,   1,   0,   1   },
            new float[] {   3,   2,   3,   1,   0   },
        };

        protected static float[][] DivergenceVowelRoundness = new float[][]
        {
            new float[] { 0, 1 },
            new float[] { 1, 0 },
        };
    }

    internal class IpaPulmonicConsonant : IpaSyllable
    {
        public IpaPulmonicLocation Location { get; private set; }
        public IpaPulmonicArticulation Articulation { get; private set; }
        public IpaPulmonicVoice Voice { get; private set; }

        public IpaPulmonicConsonant(IpaPulmonicArticulation articulation, IpaPulmonicLocation location, IpaPulmonicVoice voice) : base(IpaClass.PulmonicConsonant)
        {
            Location = location;
            Articulation = articulation;
            Voice = voice;
        }

        internal override float Divergence(IpaSyllable other, IpaWeights weights)
        {
            if (this.Class != other.Class)
            {
                return DivergenceIpaClass[(int)this.Class][(int)other.Class] * weights.ClassWeight;
            }

            IpaPulmonicConsonant otherConsonant = other as IpaPulmonicConsonant;

            return DivergencePulmonicLocation[(int)this.Location][(int)otherConsonant.Location] * weights.PulmonicLocationWeight +
                DivergencePulmonicArticulation[(int)this.Articulation][(int)otherConsonant.Articulation] * weights.PulmonicArticulationWeight +
                DivergencePulmonicVoice[(int)this.Voice][(int)otherConsonant.Voice] * weights.PulmonicVoiceWeight;
        }
    }

    internal class IpaNonPulmonicConsonant : IpaSyllable
    {
        public IpaNonPulmonicArticulation Articulation { get; private set; }

        public IpaNonPulmonicConsonant(IpaNonPulmonicArticulation articulation) : base(IpaClass.NonPulmonicConsonant)
        {
            Articulation = articulation;
        }

        internal override float Divergence(IpaSyllable other, IpaWeights weights)
        {
            if (this.Class != other.Class)
            {
                return DivergenceIpaClass[(int)this.Class][(int)other.Class] * weights.ClassWeight;
            }

            IpaNonPulmonicConsonant otherConsonant = other as IpaNonPulmonicConsonant;

            return DivergenceNonPulmonicArticulation[(int)this.Articulation][(int)otherConsonant.Articulation] * weights.NonPulmonicArticulationWeight;
        }
    }

    internal class IpaVowel : IpaSyllable
    {
        public IpaVowelHeight Height { get; private set; }
        public IpaVowelBackness Backness { get; private set; }

        public IpaVowelRoundness Roundness { get; private set; }

        public IpaVowel(IpaVowelHeight height, IpaVowelBackness backness, IpaVowelRoundness roundness) : base(IpaClass.Vowel)
        {
            Height = height;
            Backness = backness;
            Roundness = roundness;
        }

        internal override float Divergence(IpaSyllable other, IpaWeights weights)
        {
            if (this.Class != other.Class)
            {
                return DivergenceIpaClass[(int)this.Class][(int)other.Class] * weights.ClassWeight;
            }

            IpaVowel otherVowel = other as IpaVowel;

            return DivergenceVowelHeight[(int)this.Height][(int)otherVowel.Height] * weights.VowelHeightWeight +
                DivergenceVowelBackness[(int)this.Backness][(int)otherVowel.Backness] * weights.VowelBacknessWeight +
                DivergenceVowelRoundness[(int)this.Roundness][(int)otherVowel.Roundness] * weights.VowelRoundnessWeight;
        }
    }

    #endregion

    #region Weight training algorithm

    /// <summary>
    /// Represents weight values to be used in IPA string comparison. Determines how different phonetic attributes contribute to overall divergence
    /// </summary>
    internal class IpaWeights
    {
        // these values were determined genetically
        public float ClassWeight = 3.8565f;
        public float PulmonicLocationWeight = 0.62256f;
        public float PulmonicArticulationWeight = 3.3927f;
        public float PulmonicVoiceWeight = 0.516f;
        public float VowelHeightWeight = 2.12074f;
        public float VowelBacknessWeight = 2.3688f;
        public float VowelRoundnessWeight = 0.112f;
        public float NonPulmonicArticulationWeight = 1.0f;
        public float VowelInsdelCost = 15f;
        public float PulmonicConsonantInsdelCost = 20.0f;
        public float NonPulmonicConsonantInsdelCost = 30.0f;

        private static IpaWeights RandomWeights(IRandom rand)
        {
            return new IpaWeights()
            {
                ClassWeight = 0.1f + (rand.NextFloat() * 10f),
                PulmonicLocationWeight = 0.1f + (rand.NextFloat() * 10f),
                PulmonicArticulationWeight = 0.1f + (rand.NextFloat() * 10f),
                PulmonicVoiceWeight = 0.1f + (rand.NextFloat() * 10f),
                VowelHeightWeight = 0.1f + (rand.NextFloat() * 10f),
                VowelBacknessWeight = 0.1f + (rand.NextFloat() * 10f),
                VowelRoundnessWeight = 0.1f + (rand.NextFloat() * 10f),
                VowelInsdelCost = 0.1f + (rand.NextFloat() * 10f),
                PulmonicConsonantInsdelCost = 0.1f + (rand.NextFloat() * 10f),
            };
        }

        private IpaWeights Mutate(IRandom rand, float mutateRate)
        {
            IpaWeights returnVal = new IpaWeights()
            {
                ClassWeight = this.ClassWeight,
                PulmonicLocationWeight = this.PulmonicLocationWeight,
                PulmonicArticulationWeight = this.PulmonicArticulationWeight,
                PulmonicVoiceWeight = this.PulmonicVoiceWeight,
                VowelHeightWeight = this.VowelHeightWeight,
                VowelBacknessWeight = this.VowelBacknessWeight,
                VowelRoundnessWeight = this.VowelRoundnessWeight,
                VowelInsdelCost = this.VowelInsdelCost,
                PulmonicConsonantInsdelCost = this.PulmonicConsonantInsdelCost
            };
            
            if (rand.NextFloat() < 0.2)
            {
                returnVal.ClassWeight = Math.Min(10.0f, Math.Max(0.1f, returnVal.ClassWeight + (mutateRate * (rand.NextFloat() - 0.5f))));
            }
            if (rand.NextFloat() < 0.2)
            {
                returnVal.PulmonicLocationWeight = Math.Min(10.0f, Math.Max(0.1f, returnVal.PulmonicLocationWeight + (mutateRate * (rand.NextFloat() - 0.5f))));
            }
            if (rand.NextFloat() < 0.2)
            {
                returnVal.PulmonicArticulationWeight = Math.Min(10.0f, Math.Max(0.1f, returnVal.PulmonicArticulationWeight + (mutateRate * (rand.NextFloat() - 0.5f))));
            }
            if (rand.NextFloat() < 0.2)
            {
                returnVal.PulmonicVoiceWeight = Math.Min(10.0f, Math.Max(0.1f, returnVal.PulmonicVoiceWeight + (mutateRate * (rand.NextFloat() - 0.5f))));
            }
            if (rand.NextFloat() < 0.2)
            {
                returnVal.VowelHeightWeight = Math.Min(10.0f, Math.Max(0.1f, returnVal.VowelHeightWeight + (mutateRate * (rand.NextFloat() - 0.5f))));
            }
            if (rand.NextFloat() < 0.2)
            {
                returnVal.VowelBacknessWeight = Math.Min(10.0f, Math.Max(0.1f, returnVal.VowelBacknessWeight + (mutateRate * (rand.NextFloat() - 0.5f))));
            }
            if (rand.NextFloat() < 0.2)
            {
                returnVal.VowelRoundnessWeight = Math.Min(10.0f, Math.Max(0.1f, returnVal.VowelRoundnessWeight + (mutateRate * (rand.NextFloat() - 0.5f))));
            }
            if (rand.NextFloat() < 0.2)
            {
                returnVal.VowelInsdelCost = Math.Min(20.0f, Math.Max(0.1f, returnVal.VowelInsdelCost + (mutateRate * (rand.NextFloat() - 0.5f))));
            }
            if (rand.NextFloat() < 0.2)
            {
                returnVal.PulmonicConsonantInsdelCost = Math.Min(20.0f, Math.Max(0.1f, returnVal.PulmonicConsonantInsdelCost + (mutateRate * (rand.NextFloat() - 0.5f))));
            }

            return returnVal;
        }

        //public static IpaWeights TrainWeights(ILogger logger, string[] ipaWords)
        //{
        //    List<Tuple<string, string>> nearList = new List<Tuple<string, string>>();
        //    nearList.Add(new Tuple<string, string>("koʊlrɑbi", "kɔl rɑvi")); // call ravi
        //    nearList.Add(new Tuple<string, string>("naɪt", "flaɪt")); // night flight
        //    nearList.Add(new Tuple<string, string>("rɪŋ", "kɪŋ")); // ring king
        //    nearList.Add(new Tuple<string, string>("yoʊkoʊkɑnoʊ", "yʉkʊəkaɪnoʊ")); // yoko Kano
        //    nearList.Add(new Tuple<string, string>("dərɛkʃənztəspɛnsər", "ɪrɛktɪnədɪspɛnsər")); // erectin a dispenser
        //    nearList.Add(new Tuple<string, string>("byʉgəl", "beɪgəl")); // bugle bagel
        //    nearList.Add(new Tuple<string, string>("bɑsɪŋseɪ", "pæsɪŋseɪ")); // ba sing se
        //    nearList.Add(new Tuple<string, string>("haɪnɪkɪ", "ɑmnɪkɪ")); // omnikey
        //    nearList.Add(new Tuple<string, string>("əŋkəlmɑnɪk", "nɑnpʊəlmɑnɪk")); //  nonpulmonic
        //    nearList.Add(new Tuple<string, string>("hælɪlæbzəsoʊʃɪəts", "ɑlɪləvzsoʊʃɪts")); // halley labs associates 
        //    nearList.Add(new Tuple<string, string>("lɪsɔndɑtpoʊnɪ", "ləsoʊldɑtpoʊnɪ")); // le soldat pony
        //    nearList.Add(new Tuple<string, string>("ɪgəl", "ɪgoʊ")); // eagle ego
        //    nearList.Add(new Tuple<string, string>("ðəstɔntənlɪk", "ðəstɔrmzɪnleɪk")); // the staunton lick
        //    nearList.Add(new Tuple<string, string>("ɪtælyənsɛntrəl", "eɪtaʊnɪnsɛntrəl")); // Italian central   a town in central
        //    nearList.Add(new Tuple<string, string>("ðəflaɪtlɪstɪnðəyɪr", "ðəflaɪtləsɪnðəɛr")); // the flight list in the year / the flightless in the air
        //    nearList.Add(new Tuple<string, string>("lɛmənfɔrsɪsaɪdsləmbər", "ləmɛntfɔrsɪsaɪdsləmbər")); // lemon for seaside's lumber / lament for seaside slumber
        //    nearList.Add(new Tuple<string, string>("wɪldərwʊəmən", "wɪlðəwʊəmən")); // wilderwoman / will the woman
        //    nearList.Add(new Tuple<string, string>("ɛmpɪrən", "ɪmpɪrɪəm")); // empyrean / imperium
        //    nearList.Add(new Tuple<string, string>("ðəhɑrdəstlɛʒər", "ðəhɑrtæsksplɛʐər")); // the hardest ledger / the heart asks pleasure
        //    nearList.Add(new Tuple<string, string>("groʊɪŋʃeɪks", "θroʊɪŋʃeɪps")); // growing shakes / throwing shapes
        //    nearList.Add(new Tuple<string, string>("waɪtlaɪteɪɛm", "kwaɪətlaɪteɪɛm")); // white late AM / quiet light AM
        //    nearList.Add(new Tuple<string, string>("tɪməvsæmzɛrɪn", "θɪməvsæməzɛrən")); // team of sam's erin / theme of samus aran
        //    nearList.Add(new Tuple<string, string>("vɔɪsmeɪlfraɪ", "əhɔɪsmɔlfraɪ")); // voicemail fry / ahoy small fry
        //    nearList.Add(new Tuple<string, string>("kəsɪnoʊkæləbər", "kəsɪnoʊkæləvɛrə")); // casino caliber / casino calavera
        //    nearList.Add(new Tuple<string, string>("hʉlɪgəlɪ", "fʉlɪkʉlɪ")); // who legally / fooly cooly
        //    nearList.Add(new Tuple<string, string>("reɪzərlaɪtglʉ", "reɪzərlaɪkblʉ")); // razor light glue / razorlike blue
        //    nearList.Add(new Tuple<string, string>("wɪləvyʉgɑtəfaɪt", "wɪləvyʉpɪtsɪkɑtoʊfaɪv")); // we love you gotta fight / we love pizzicato five


        //    Queue<Tuple<string, string>> farList = new Queue<Tuple<string, string>>();
        //    //farList.Add(new Tuple<string, string>("ɛvrɪbɑdi", "smɔl"));
        //    //farList.Add(new Tuple<string, string>("æŋgrɪ", "pɪsək"));
        //    //farList.Add(new Tuple<string, string>("prɪkəl", "kætrɪ"));
        //    //farList.Add(new Tuple<string, string>("gɑtðɪpeɪloʊd", "ɪnkɑmɪŋmɔrtɛr"));
        //    //farList.Add(new Tuple<string, string>("byʉgər", "broʊtʃ"));
        //    //farList.Add(new Tuple<string, string>("sədoʊnə", "sɪbərgər"));
        //    //farList.Add(new Tuple<string, string>("səfrəʒɪst", "ʃʊəgərkeɪn"));
        //    //farList.Add(new Tuple<string, string>("oʊnərʃɪp", "lɑspɑlməs"));
        //    //farList.Add(new Tuple<string, string>("lʉzər", "lɔrmən"));
        //    //farList.Add(new Tuple<string, string>("pæsbʊək", "rɪgər"));
        //    //farList.Add(new Tuple<string, string>("læʒwɑlɑrks", "nʉgeɪtleɪsɪk"));
        //    //farList.Add(new Tuple<string, string>("pɪnɪk", "rɪgər"));
        //    //farList.Add(new Tuple<string, string>("pɪnoʊkɪoʊ", "rɛzɪdɛnsɪz"));
        //    //farList.Add(new Tuple<string, string>("rɛzəgneɪʃənlætʃ", "haɪɪnəkərbɪ"));
        //    //farList.Add(new Tuple<string, string>("heɪstəlɪhætʃt", "ɪʒəptɑləʒɪaɪd"));
        //    //farList.Add(new Tuple<string, string>("gɪmɪleɪd", "mɛstrəlɛt"));
        //    //farList.Add(new Tuple<string, string>("fɪləbəstərzʒɛks", "mɛrɪgoʊraʊnd"));
        //    //farList.Add(new Tuple<string, string>("sɑnəprɪtʃərd", "sænfrænsɪskoʊ"));
        //    //farList.Add(new Tuple<string, string>("ʒɪoʊpɑlətɪks", "dətrɔɪtərz"));
        //    //farList.Add(new Tuple<string, string>("dɪtɑksɪfɪkeɪʃən", "lɑsveɪgəsmɑmz"));
        //    //farList.Add(new Tuple<string, string>("proʊlɪfəreɪʃən", "proʊmɪskyʉətɪ"));
        //    //farList.Add(new Tuple<string, string>("sɑrdɑnɪkəlɪ", "yʉroʊdɪpɑzɪts"));

        //    IRandom rand = new FastRandom();
        //    int NUM_GENERATIONS = 1000;
        //    int POOL_SIZE = 1000;

        //    Hypothesis<IpaWeights>[] genePool = new Hypothesis<IpaWeights>[POOL_SIZE];
        //    Hypothesis<IpaWeights>.DescendingComparator sorter = new Hypothesis<IpaWeights>.DescendingComparator();
            
        //    // Initialize the pool
        //    for (int c = 0; c < POOL_SIZE; c++)
        //    {
        //        genePool[c] = new Hypothesis<IpaWeights>(RandomWeights(rand), 0);
        //    }

        //    for (int generation = 0; generation < NUM_GENERATIONS; generation++)
        //    {
        //        //logger.Log("Generation " + generation);

        //        // Shuffle the negative test set
        //        if (farList.Count > 0)
        //        {
        //            farList.Dequeue();
        //        }

        //        // Just glob together random words to make negative phrases
        //        while (farList.Count < nearList.Count)
        //        {
        //            int numWords = rand.NextInt(1, 4);
        //            StringBuilder wordA = new StringBuilder();
        //            StringBuilder wordB = new StringBuilder();
        //            for (int c = 0; c < numWords; c++)
        //            {
        //                wordA.Append(ipaWords[rand.NextInt(ipaWords.Length)]);
        //            }

        //            while (wordB.Length == 0 || wordB.Length < wordA.Length - 4)
        //            {
        //                wordB.Append(ipaWords[rand.NextInt(ipaWords.Length)]);
        //            }

        //            farList.Enqueue(new Tuple<string, string>(wordA.ToString(), wordB.ToString()));
        //        }

        //        // Evaluate each gene
        //        foreach (Hypothesis<IpaWeights> candidate in genePool)
        //        {
        //            float avg = 0;
        //            float nearMax = 0;
        //            float farMin = float.MaxValue;
        //            foreach (Tuple<string, string> input in nearList)
        //            {
        //                float distance = InternationalPhoneticAlphabet.EditDistance(input.Item1, input.Item2, candidate.Value);
        //                avg += distance;
        //                nearMax = Math.Max(nearMax, distance);
        //            }
        //            float nearAvg = avg / nearList.Count;

        //            avg = 0;
        //            foreach (Tuple<string, string> input in farList)
        //            {
        //                float distance = InternationalPhoneticAlphabet.EditDistance(input.Item1, input.Item2, candidate.Value);
        //                avg += distance;
        //                farMin = Math.Min(farMin, distance);
        //            }
        //            float farAvg = avg / farList.Count;

        //            float avgRatio = float.MaxValue;
        //            if (nearAvg != 0)
        //            {
        //                avgRatio = farAvg / nearAvg;
        //            }

        //            float extremeRatio = float.MaxValue;
        //            if (nearMax != 0)
        //            {
        //                extremeRatio = farMin / nearMax;
        //            }

        //            float avgDist = farAvg - nearAvg;
        //            float extremeDistance = farMin - nearMax;

        //            candidate.Conf = ((avgRatio + extremeRatio) * 5f) + avgDist + extremeDistance;
        //        }

        //        // Sort and cull. Bottom half of pool replaced by mutations of the top half.
        //        // Mutation rate increases the lower in the pool you get
        //        Array.Sort(genePool, sorter);

        //        int halfPool = POOL_SIZE / 2;
        //        for (int c = 0; c < halfPool; c++)
        //        {
        //            float mutateRate = (((float)c) / ((float)halfPool)) * 5f;
        //            genePool[c + halfPool].Value = genePool[c].Value.Mutate(rand, mutateRate);
        //        }
                
        //        if (generation % 100 == 0)
        //        {
        //            IpaWeights bestCandidate = genePool[0].Value;
        //            logger.Log("Here are the best weights so far:", LogLevel.Wrn);
        //            logger.Log("ClassWeight = " + bestCandidate.ClassWeight);
        //            logger.Log("PulmonicLocationWeight = " + bestCandidate.PulmonicLocationWeight);
        //            logger.Log("PulmonicArticulationWeight = " + bestCandidate.PulmonicArticulationWeight);
        //            logger.Log("PulmonicVoiceWeight = " + bestCandidate.PulmonicVoiceWeight);
        //            logger.Log("VowelHeightWeight = " + bestCandidate.VowelHeightWeight);
        //            logger.Log("VowelBacknessWeight = " + bestCandidate.VowelBacknessWeight);
        //            logger.Log("VowelRoundnessWeight = " + bestCandidate.VowelRoundnessWeight);
        //            logger.Log("VowelInsdelCost = " + bestCandidate.VowelInsdelCost);
        //            logger.Log("PulmonicConsonantInsdelCost = " + bestCandidate.PulmonicConsonantInsdelCost);
        //        }
        //    }

        //    IpaWeights finalBestCandidate = genePool[0].Value;

        //    logger.Log("Here are the best weights:", LogLevel.Ins);
        //    logger.Log("ClassWeight = " + finalBestCandidate.ClassWeight);
        //    logger.Log("PulmonicLocationWeight = " + finalBestCandidate.PulmonicLocationWeight);
        //    logger.Log("PulmonicArticulationWeight = " + finalBestCandidate.PulmonicArticulationWeight);
        //    logger.Log("PulmonicVoiceWeight = " + finalBestCandidate.PulmonicVoiceWeight);
        //    logger.Log("VowelHeightWeight = " + finalBestCandidate.VowelHeightWeight);
        //    logger.Log("VowelBacknessWeight = " + finalBestCandidate.VowelBacknessWeight);
        //    logger.Log("VowelRoundnessWeight = " + finalBestCandidate.VowelRoundnessWeight);
        //    logger.Log("VowelInsdelCost = " + finalBestCandidate.VowelInsdelCost);
        //    logger.Log("PulmonicConsonantInsdelCost = " + finalBestCandidate.PulmonicConsonantInsdelCost);

        //    logger.Log("POSITIVE SET", LogLevel.Ins);
        //    foreach (Tuple<string, string> input in nearList)
        //    {
        //        logger.Log(input.Item1 + " ~ " + input.Item2 + " = " + InternationalPhoneticAlphabet.EditDistance(input.Item1, input.Item2, finalBestCandidate));
        //    }

        //    logger.Log("NEGATIVE SET", LogLevel.Ins);
        //    foreach (Tuple<string, string> input in farList)
        //    {
        //        logger.Log(input.Item1 + " ~ " + input.Item2 + " = " + InternationalPhoneticAlphabet.EditDistance(input.Item1, input.Item2, finalBestCandidate));
        //    }

        //    return finalBestCandidate;
        //}
    }

    #endregion
}
