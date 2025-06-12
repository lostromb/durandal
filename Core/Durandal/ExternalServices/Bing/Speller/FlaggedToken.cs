using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Bing.Speller
{
#pragma warning disable CS0649
    public class FlaggedToken
    {
        public int offset;
        public string token;
        public string type;
        public IList<SpellSuggestion> suggestions;
    }
#pragma warning restore CS0649
}
