using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe.BigOven.Schemas
{
    public class BigOvenRecipe
    {
        /// <summary>
        /// Gets or sets Recipe ID
        /// </summary>
        public int RecipeID { get; set; }

        /// <summary>
        /// Gets or sets Title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets Description
        /// </summary>
        public string Description { get; set; }

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
        /// Gets or sets Primary Ingredient
        /// </summary>
        public string PrimaryIngredient { get; set; }

        /// <summary>
        /// Gets or sets Star Rating
        /// </summary>
        public decimal StarRating { get; set; }

        /// <summary>
        /// Gets or sets WebURL
        /// </summary>
        public string WebURL { get; set; }

        /// <summary>
        /// Gets or sets ImageURL
        /// </summary>
        public string ImageURL { get; set; }

        /// <summary>
        /// Gets or sets Review Count
        /// </summary>
        public int ReviewCount { get; set; }

        /// <summary>
        /// Gets or sets Medal Count
        /// </summary>
        public int MedalCount { get; set; }

        /// <summary>
        /// Gets or sets Favorite Count
        /// </summary>
        public int FavoriteCount { get; set; }

        /// <summary>
        /// Gets or sets Instructions
        /// </summary>
        public string Instructions { get; set; }

        /// <summary>
        /// Gets or sets Yield Number
        /// </summary>
        public float YieldNumber { get; set; }

        /// <summary>
        /// Gets or sets Yield Unit
        /// </summary>
        public string YieldUnit { get; set; }

        /// <summary>
        /// Gets or sets Total Minutes
        /// </summary>
        public int TotalMinutes { get; set; }

        /// <summary>
        /// Gets or sets Active Minutes
        /// </summary>
        public int ActiveMinutes { get; set; }

        /// <summary>
        /// Gets or sets Creation Date
        /// </summary>
        public string CreationDate { get; set; }

        /// <summary>
        /// Gets or sets Last Modified
        /// </summary>
        public string LastModified { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is Bookmark
        /// </summary>
        public bool IsBookmark { get; set; }

        /// <summary>
        /// Gets or sets BookmarkURL
        /// </summary>
        public string BookmarkURL { get; set; }

        /// <summary>
        /// Gets or sets Bookmark Site Logo
        /// </summary>
        public string BookmarkSiteLogo { get; set; }

        /// <summary>
        /// Gets or sets Bookmark Image URL
        /// </summary>
        public string BookmarkImageURL { get; set; }

        /// <summary>
        /// Gets or sets Is RecipeScan
        /// </summary>
        public string IsRecipeScan { get; set; }

        /// <summary>
        /// Gets or sets Menu Count
        /// </summary>
        public int MenuCount { get; set; }

        /// <summary>
        /// Gets or sets Notes Count
        /// </summary>
        public int NotesCount { get; set; }

        /// <summary>
        /// Gets or sets Ad Tags
        /// </summary>
        public string AdTags { get; set; }

        /// <summary>
        /// Gets or sets Ingredients Text Block
        /// </summary>
        public string IngredientsTextBlock { get; set; }

        /// <summary>
        /// Gets or sets All Categories Text
        /// </summary>
        public string AllCategoriesText { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is Sponsored
        /// </summary>
        public bool IsSponsored { get; set; }

        /// <summary>
        /// Gets or sets Variant Of Recipe ID
        /// </summary>
        public string VariantOfRecipeID { get; set; }

        /// <summary>
        /// Gets or sets Collection
        /// </summary>
        public string Collection { get; set; }

        /// <summary>
        /// Gets or sets CollectionID
        /// </summary>
        public string CollectionID { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is AdminBoost
        /// </summary>
        public float? AdminBoost { get; set; }

        /// <summary>
        /// Gets or sets Verified Date Time
        /// </summary>
        public string VerifiedDateTime { get; set; }

        /// <summary>
        /// Gets or sets Max Image Square
        /// </summary>
        public string MaxImageSquare { get; set; }

        /// <summary>
        /// Gets or sets PhotoUrl
        /// </summary>
        public string PhotoUrl { get; set; }

        /// <summary>
        /// Gets or sets Verified By Class
        /// </summary>
        public string VerifiedByClass { get; set; }

        /// <summary>
        /// Gets or sets list of Ingredient
        /// </summary>
        public List<BigOvenIngredient> Ingredients { get; set; }

        /// <summary>
        /// Gets or sets Nutrition Info
        /// </summary>
        public BigOvenNutritionInfo NutritionInfo { get; set; }
    }
}
