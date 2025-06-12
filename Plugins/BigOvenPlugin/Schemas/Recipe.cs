using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigOven.Schemas
{
    public class Recipe
    {
        [JsonProperty(PropertyName = "@")]
        public const string ClassTypes = "bigoven.recipe";

        /// <summary>
        /// Gets or sets recipe ID
        /// </summary>
        public int RecipeID { get; set; }

        /// <summary>
        /// Gets or sets title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets Cuisine
        /// </summary>
        public string Cuisine { get; set; }

        /// <summary>
        /// Gets or sets Category
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets Subcategory
        /// </summary>
        public string Subcategory { get; set; }

        /// <summary>
        /// Gets or sets WebURL
        /// </summary>
        public string WebURL { get; set; }

        /// <summary>
        /// Gets or sets StarRating
        /// </summary>
        public float StarRating { get; set; }

        /// <summary>
        /// Gets or sets ReviewCount
        /// </summary>
        public float ReviewCount { get; set; }

        /// <summary>
        /// Gets or sets Servings
        /// </summary>
        public float Servings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is Bookmark
        /// </summary>
        public bool IsBookmark { get; set; }

        /// <summary>
        /// Gets or sets Total Tries
        /// </summary>
        public int TotalTries { get; set; }

        /// <summary>
        /// Gets or sets PhotoUrl
        /// </summary>
        public string PhotoUrl { get; set; }
    }
}
