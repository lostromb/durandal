using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Statistics;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.Dialog.Services
{
    /// <summary>
    /// Entity resolver (plugin-facing) that invokes the generic entity resolver directly rather than calling a service or doing remoting
    /// </summary>
    public class DefaultEntityResolver : IEntityResolver
    {
        private readonly GenericEntityResolver _resolverImpl;

        public DefaultEntityResolver(GenericEntityResolver implementation)
        {
            _resolverImpl = implementation;
        }

        public async Task<IList<Hypothesis<T>>> ResolveEntity<T>(LexicalString input, IList<NamedEntity<T>> possibleValues, LanguageCode locale, ILogger traceLogger)
        {
            // Convert typed input to ordinals
            List<LexicalNamedEntity> genericInput = new List<LexicalNamedEntity>();

            for (int ordinal = 0; ordinal < possibleValues.Count; ordinal++)
            {
                genericInput.Add(new LexicalNamedEntity(ordinal, possibleValues[ordinal].KnownAs));
            }

            // Call internal resolver
            IList<Hypothesis<int>> ordinalHyps = await _resolverImpl.ResolveEntity(input, genericInput, locale, traceLogger).ConfigureAwait(false);

            // Convert responses back
            IList<Hypothesis<T>> returnVal = new List<Hypothesis<T>>();
            foreach (Hypothesis<int> ordinalHyp in ordinalHyps)
            {
                returnVal.Add(new Hypothesis<T>(possibleValues[ordinalHyp.Value].Handle, ordinalHyp.Conf));
            }

            traceLogger.Log("Entity resolver results: " + returnVal.Count + " hyps, highest confidence is " + (returnVal.Count == 0 ? 0 : returnVal[0].Conf));
            for (int top = 0; top < 5 && top < returnVal.Count; top++)
            {
                traceLogger.Log("Hypothesis " + top + ": " + returnVal[top].ToString(), privacyClass: DataPrivacyClassification.PrivateContent);
            }

            return returnVal;
        }
    }
}
