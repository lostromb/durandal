using Durandal.Common.Logger;
using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.NLP.Feature;
using Durandal.Common.NLP.Language;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Speech.TTS;

namespace Durandal.Common.NLP
{
    public class NLPTools
    {
        /// <summary>
        /// A delegate function that compares two strings and calculates the divergence between them.
        /// The returned value SHOULD be within the bound of 0 (strings match exactly) to 1 (theoretical maximum divergence).
        /// Thresholding on the returned value is permitted.
        /// </summary>
        /// <param name="one"></param>
        /// <param name="two"></param>
        /// <returns>The edit distance between the two strings</returns>
        public delegate float EditDistanceComparer(string one, string two);

        /// <summary>
        /// A class which is able to take arbitrary words and phrases and convert them to IPA (universal) representation.
        /// </summary>
        public IPronouncer Pronouncer { get; set; }

        /// <summary>
        /// A class which can take entire sentences and break them into their components.
        /// This specific variant should only be used for featurization, when you want to inspect
        /// abstract lexical properties of the sentence, not when you just want the words that are said.
        /// Used primarily in LU and statistical LG.
        /// </summary>
        public IWordBreaker FeaturizationWordBreaker { get; set; }

        /// <summary>
        /// A class which can take entire sentences and break them into their components.
        /// This specific variant should be used when the output will be somehow shown or spoken to the user.
        /// It outputs a simpler format containing whole words only.
        /// </summary>
        public IWordBreaker WordBreaker { get; set; }

        /// <summary>
        /// A delegate function which is able to calculate the lexical divergence between two words or phrases.
        /// Used in pseudointent matching and entity resolution.
        /// </summary>
        public EditDistanceComparer EditDistance { get; set; }

        public ILGFeatureExtractor LGFeatureExtractor { get; set; }
        
        public IDomainFeatureExtractor DomainFeaturizer { get; set; }

        public ITagFeatureExtractor TagFeaturizer { get; set; }

        public DictionaryCollection Dictionaries { get; set; }

        public ICultureInfoFactory CultureInfoFactory { get; set; }

        public ISpeechTimingEstimator SpeechTimingEstimator { get; set; }

        public long GetMemoryUse()
        {
            long returnVal = 0;
            if (Dictionaries != null)
            {
                returnVal += Dictionaries.GetMemoryUse();
            }

            return returnVal;
        }

        public static NLPTools BuildToolsForLocale(LanguageCode locale, ILogger logger, IFileSystem fileSystem, ICultureInfoFactory cultureInfoFactory)
        {
            NLPTools returnVal = new NLPTools();

            if (locale.ToBcp47Alpha2String().Equals("en-US", StringComparison.OrdinalIgnoreCase))
            {
                returnVal.Dictionaries = new DictionaryCollection(logger.Clone("DictionaryCollection"), fileSystem);
                returnVal.Dictionaries.Load("commonwords", locale, new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\en-US\\english.dict"), false, true);
                returnVal.Dictionaries.Load("firstnames", locale, new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\en-US\\firstnames.dict"), true, false);
                returnVal.Dictionaries.Load("lastnames", locale, new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\en-US\\lastnames.dict"), true, false);
                returnVal.Dictionaries.Load("placenames", locale, new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\en-US\\placenames.dict"), true, false);
                returnVal.FeaturizationWordBreaker = new EnglishWordBreaker();
                returnVal.WordBreaker = new EnglishWholeWordBreaker();
                returnVal.LGFeatureExtractor = new EnglishLGFeatureExtractor();
                returnVal.DomainFeaturizer = new EnglishDomainFeatureExtractor(returnVal.Dictionaries, returnVal.FeaturizationWordBreaker);
                returnVal.TagFeaturizer = new EnglishTagFeatureExtractor(returnVal.Dictionaries);
                returnVal.CultureInfoFactory = cultureInfoFactory;
                return returnVal;
            }
            else if (locale.ToBcp47Alpha2String().Equals("es-mx", StringComparison.OrdinalIgnoreCase))
            {
                returnVal.Dictionaries = null;
                returnVal.FeaturizationWordBreaker = new EnglishWordBreaker();
                returnVal.DomainFeaturizer = new SpanishDomainFeatureExtractor();
                returnVal.TagFeaturizer = new SpanishTagFeatureExtractor();
                returnVal.CultureInfoFactory = cultureInfoFactory;
                return returnVal;
            }
            else
            {
                logger.Log("Language tools do not exist for the locale " + locale, LogLevel.Err);
                return null;
            }
        }
    }
}
