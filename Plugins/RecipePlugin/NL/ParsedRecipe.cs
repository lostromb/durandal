using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe.NL
{
    /// <summary>
    /// Represents recipe instructions that have been processed by NL parsers.
    /// </summary>
    public class ParsedRecipe
    {
        public IList<ParsedRecipeInstruction> Instructions { get; set; }

        public string RecipeNotes { get; set; }
    }
}
