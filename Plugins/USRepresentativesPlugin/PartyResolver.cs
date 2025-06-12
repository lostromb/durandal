using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.NLP;
using Durandal.Common.Statistics;
using Durandal.Common.NLP.Language;

namespace Durandal.Plugins.Plugins.USRepresentatives
{
    public static class PartyResolver
    {
        private static readonly List<NamedEntity<PoliticalParty>> PartyNameResolution = new List<NamedEntity<PoliticalParty>>()
        {
            new NamedEntity<PoliticalParty>(PoliticalParty.Democrat, new LexicalString[] { new LexicalString("Democrat"), new LexicalString("D"), new LexicalString("Dem") }),
            new NamedEntity<PoliticalParty>(PoliticalParty.Republican, new LexicalString[] { new LexicalString("Republican"), new LexicalString("R"), new LexicalString("Rep") }),
            new NamedEntity<PoliticalParty>(PoliticalParty.Independent, new LexicalString[] { new LexicalString("Independent"), new LexicalString("I"), new LexicalString("Ind") }),
            new NamedEntity<PoliticalParty>(PoliticalParty.Libertarian, new LexicalString[] { new LexicalString("Libertarian") })
        };

        /// <summary>
        /// Attempt to resolve the political party of a candidate based on its name, i.e. "democrat", "rep", "D", etc.
        /// </summary>
        /// <param name="partyName"></param>
        /// <param name="services"></param>
        /// <returns></returns>
        public static async Task<PoliticalParty> ResolveParty(LexicalString partyName, IPluginServices services)
        {
            IList<Hypothesis<PoliticalParty>> hyps = await services.EntityResolver.ResolveEntity(
                partyName,
                PartyNameResolution,
                LanguageCode.Parse("en-US"),
                services.Logger).ConfigureAwait(false);

            // No matches
            if (hyps.Count == 0)
            {
                return PoliticalParty.Unaffiliated;
            }

            // Match too low of confidence
            if (hyps[0].Conf < 0.8f)
            {
                return PoliticalParty.Unaffiliated;
            }

            return hyps[0].Value;
        }

        public static string PartyToString(PoliticalParty party)
        {
            return Enum.GetName(typeof(PoliticalParty), party);
        }
    }
}
