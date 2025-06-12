using Durandal.API;
using Durandal.Common.NLP.Language;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Alignment
{
    /// <summary>
    /// Implementation of edit distance using metaphone algorithm
    /// </summary>
    public static class EditDistanceMetaphone
    {
        public static float Calculate(string one, string two)
        {
            string metaphoneOne = Metaphone.Encode(one);
            string metaphoneTwo = Metaphone.Encode(two);
            return StringUtils.NormalizedEditDistance(metaphoneOne, metaphoneTwo);
        }
    }

    /// <summary>
    /// Implementation of edit distance using double metaphone algorithm
    /// </summary>
    public static class EditDistanceDoubleMetaphone
    {
        public static float Calculate(string one, string two)
        {
            string metaphoneOne = DoubleMetaphone.Encode(one);
            string metaphoneTwo = DoubleMetaphone.Encode(two);
            return StringUtils.NormalizedEditDistance(metaphoneOne, metaphoneTwo);
        }
    }

    /// <summary>
    /// Implementation of edit distance using NL pronunciation distance (in IPA)
    /// </summary>
    public class EditDistancePronunciation
    {
        private readonly IPronouncer _pronouncer;
        private readonly IWordBreaker _wordbreaker;
        private readonly LanguageCode _locale;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pronouncer">The pronouncer to use (to convert words to IPA)</param>
        /// <param name="wordbreaker">A whole word breaker (NOT a featurization breaker)</param>
        /// <param name="locale"></param>
        public EditDistancePronunciation(IPronouncer pronouncer, IWordBreaker wordbreaker, LanguageCode locale)
        {
            _pronouncer = pronouncer;
            _wordbreaker = wordbreaker;
            _locale = locale;
        }

        public float Calculate(string one, string two)
        {
            Sentence brokenOne = _wordbreaker.Break(one);
            string pronOne = _pronouncer.PronouncePhraseAsString(brokenOne.Words);
            Sentence brokenTwo = _wordbreaker.Break(two);
            string pronTwo = _pronouncer.PronouncePhraseAsString(brokenTwo.Words);
            return InternationalPhoneticAlphabet.EditDistance(pronOne, pronTwo, _locale);
        }
    }
}
