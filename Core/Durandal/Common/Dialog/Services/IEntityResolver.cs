using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;
using Durandal.Common.Statistics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog.Services
{
    public interface IEntityResolver
    {
        /// <summary>
        /// Attempts to match a user's input to a set of possible named entity candidates. The input is generally
        /// assumed to be speech. The actual mechanism for the resolution is decided by the runtime.
        /// </summary>
        /// <typeparam name="T">The type of entity to select against</typeparam>
        /// <param name="input">The user's input</param>
        /// <param name="possibleValues">A list of all possible values to be selected against</param>
        /// <param name="locale">The current locale</param>
        /// <param name="traceLogger"></param>
        /// <returns>A set of selection hypotheses</returns>
        Task<IList<Hypothesis<T>>> ResolveEntity<T>(LexicalString input, IList<NamedEntity<T>> possibleValues, LanguageCode locale, ILogger traceLogger);
    }
}