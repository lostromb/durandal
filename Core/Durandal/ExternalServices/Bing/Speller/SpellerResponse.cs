using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Bing.Speller
{
#pragma warning disable CS0649
    public class SpellerResponse
    {
        public IList<FlaggedToken> flaggedTokens;
        public string correctionType;
    }
#pragma warning restore CS0649
}
