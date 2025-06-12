using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe.Schemas
{
    public class MeasuredIngredient
    {
        /// <summary>
        /// The name of this ingredient
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The exact amount of this ingredient to use, e.g. "2.5 (cups)"
        /// </summary>
        public decimal? ExactAmount { get; set; }

        /// <summary>
        /// Some recipes don't use exact amounts, such as "a few handfuls" of flour,
        /// in which case the phrase is stored as a string here and ExactAmount is null
        /// </summary>
        public string ApproximateAmount { get; set; }

        /// <summary>
        /// The unit that accompanies the amount.
        /// Ideally this is one of the ConversionUnit enum values such as US_LITER, but if the unit is unknown
        /// (e.g. "knob" of butter, "leg" of lamb) then that unit is carried across without modification here.
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// Indicates that this ingredient is optional in the preparation of the recipe
        /// </summary>
        public bool IsOptional { get; set; }
    }
}
