using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.NLP.Language;
using System.Threading.Tasks;

namespace Durandal.Common.LG
{
    public interface ILGEngine
    {
        /// <summary>
        /// Retrieves a single pattern from the set of LG models available.
        /// </summary>
        /// <param name="patternName">The name of the pattern to retrieve</param>
        /// <param name="clientContext">The current query's context</param>
        /// <param name="logger">A query-specific logger (optional)</param>
        /// <param name="debug">Enable verbose debug logging</param>
        /// <param name="phraseNum">The phrase variant to select, if multiple variants of the phrase are specified. If null, a random phrase will be picked. The value will be % operated to the actual number of variants.</param>
        /// <returns>The desired pattern. If none exists, a non-null empty pattern will be returned.</returns>
        ILGPattern GetPattern(string patternName, ClientContext clientContext, ILogger logger = null, bool debug = false, int? phraseNum = null);

        /// <summary>
        /// Used for patterns that apply to UI elements or similar things where only text is needed
        /// </summary>
        /// <param name="patternName">The name of the pattern to retrieve</param>
        /// <param name="clientContext">The current query's context</param>
        /// <param name="logger">A query-specific logger (optional)</param>
        /// <param name="debug">Enable verbose debug logging</param>
        /// <param name="phraseNum">The phrase variant to select, if multiple variants of the phrase are specified. If null, a random phrase will be picked. The value will be % operated to the actual number of variants.</param>
        /// <returns>The "Text" field of the LG pattern, or empty string if the pattern is not found</returns>
        Task<string> GetText(string patternName, ClientContext clientContext, ILogger logger = null, bool debug = false, int? phraseNum = null);
        
        void RegisterCustomCode(string patternName, LgCommon.RunLanguageGeneration method, LanguageCode locale);
    }
}