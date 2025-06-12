using Durandal.Common.NLP.Language;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.NLP
{
    /// <summary>
    /// Represents a collection of language-specific tools (such as parsers, stemmers, formatters...) which can be
    /// fetched based on a specific locale, potentially with fallback to other locales if necessary.
    /// </summary>
    public interface INLPToolsCollection
    {
        /// <summary>
        /// Attempts to fetch NL tools for the given locale. This method may potentially fall back
        /// to a different locale if the requested one is not available (for example, fr-CA falling
        /// back to fr-FR or just fr).
        /// </summary>
        /// <param name="locale">The requested locale</param>
        /// <param name="returnVal">The discovered NL tools, if found</param>
        /// <param name="actualLocale">The actual locale of the return val, if fallback occurred</param>
        /// <returns>True if any compatible tools were found for this locale</returns>
        bool TryGetNLPTools(LanguageCode locale, out NLPTools returnVal, out LanguageCode actualLocale);
        bool TryGetNLPTools(LanguageCode locale, out NLPTools returnVal);
    }
}
