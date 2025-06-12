using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe.BigOven.Schemas
{
    public class BigOvenRecipeSearchResponse
    {
        public int ResultCount { get; set; }
        public IList<BigOvenRecipeSearchResult> Results { get; set; }
        public string SpellSuggest { get; set; }
    }
}
