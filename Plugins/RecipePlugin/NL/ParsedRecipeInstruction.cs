using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe.NL
{
    /// <summary>
    /// Represents a single step of recipe instruction processed by NL parsers.
    /// This allows expressing fancier metadata such as nonspoken notes, resolved ingredient references, etc.
    /// </summary>
    public class ParsedRecipeInstruction
    {
        /// <summary>
        /// The text to be displayed for this step
        /// </summary>
        public string DisplayText { get; set; }

        /// <summary>
        /// The text to be spoken for this step (Words only, not fully qualified SSML)
        /// </summary>
        public string SpokenText { get; set; }

        /// <summary>
        /// A list of ingredients that are called for by this step, as they are given in the displayed text
        /// </summary>
        public IList<string> ReferencedIngredients { get; set; }

        /// <summary>
        /// A set of string index boundaries defining which parts of the displayed text (if any) are categorized as inline notes, which is the
        /// case for small asides or parenthesized optional steps. For example: "Lay the vegetables out on an oven-safe bakeware (or a roasting rack) and cook for 12 minutes"
        /// </summary>
        public IList<Tuple<int, int>> InlineNoteBounds { get; set; }
    }
}
