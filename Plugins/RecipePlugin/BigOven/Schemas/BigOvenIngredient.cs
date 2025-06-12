namespace Durandal.Plugins.Recipe.BigOven.Schemas
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class BigOvenIngredient
    {
        /// <summary>
        /// Gets or sets Ingredient ID
        /// </summary>
        public int IngredientID { get; set; }

        /// <summary>
        /// Gets or sets Display Index
        /// </summary>
        public int DisplayIndex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is Heading
        /// </summary>
        public bool IsHeading { get; set; }

        /// <summary>
        /// Gets or sets Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets HTML Name
        /// </summary>
        public string HTMLName { get; set; }

        /// <summary>
        /// Gets or sets Quantity
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// Gets or sets Display Quantity
        /// </summary>
        public string DisplayQuantity { get; set; }

        /// <summary>
        /// Gets or sets Unit
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// Gets or sets Metric Quantity
        /// </summary>
        public decimal MetricQuantity { get; set; }

        /// <summary>
        /// Gets or sets Metric Display Quantity
        /// </summary>
        public string MetricDisplayQuantity { get; set; }

        /// <summary>
        /// Gets or sets Metric Unit
        /// </summary>
        public string MetricUnit { get; set; }

        /// <summary>
        /// Gets or sets Preparation Notes
        /// </summary>
        public string PreparationNotes { get; set; }

        /// <summary>
        /// Gets or sets Ingredient Info
        /// </summary>
        public BigOvenIngredientInfo IngredientInfo { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is Linked
        /// </summary>
        public bool IsLinked { get; set; }
    }
}
