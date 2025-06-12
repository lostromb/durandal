using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe.Schemas
{
    public class RecipeData
    {
        /// <summary>
        /// The name of the recipe e.g. "French toast"
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the recipe
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The API source that produced this data (different from the attribution source)
        /// </summary>
        public RecipeSource DataSource { get; set; }

        /// <summary>
        /// The ID of this recipe according to the schema of the API provider.
        /// </summary>
        public string SourceId { get; set; }

        /// <summary>
        /// The duration of "hands-on" time required by a typical chef to prepare this recipe
        /// </summary>
        public TimeSpan ActiveTime { get; set; }

        /// <summary>
        /// The duration of "hands-off" time required by this recipe, typically represented by
        /// cooking something in an oven, but could also entail setting (as in gelatin), fermenting, freezing, or similar process.
        /// </summary>
        public TimeSpan CookTime { get; set; }

        /// <summary>
        /// Total estimated time required to prepare this recipe from start to finish
        /// </summary>
        public TimeSpan TotalTime { get; set; }

        /// <summary>
        /// The ordered list of plain text instructions to prepare this recipe
        /// </summary>
        public IList<string> Instructions { get; set; }

        /// <summary>
        /// The list of all ingredients that may be required by this recipe, with their associated amounts
        /// </summary>
        public IList<MeasuredIngredient> Ingredients { get; set; }

        /// <summary>
        /// Any general notes to consider during the preparation of this recipe which aren't explicitly called out in the instructions.
        /// Things like: "You may substitute...", "For best results....", "For dairy-free preparation...", "As a variation, try...", etc.
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// The cuisine of this recipe e.g. "Italian"
        /// </summary>
        public string Cuisine { get; set; }

        /// <summary>
        /// The typical course associated with this food: "Dessert", "Dinner", "Entree", "Breakfast", etc.
        /// </summary>
        public string Course { get; set; }

        /// <summary>
        /// The URL at which this recipe can be viewed on the web (typically the original source or attribution webpage)
        /// </summary>
        public Uri WebUrl { get; set; }

        /// <summary>
        /// A list of URLs pointing to images of this recipe
        /// </summary>
        public List<Uri> ImageUrls { get; set; }

        /// <summary>
        /// This recipe's star rating from 1 to 5, with 0 representing "unrated"
        /// </summary>
        public decimal StarRating { get; set; }
    }
}
