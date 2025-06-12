using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe.BigOven.Schemas
{
    public class BigOvenIngredientInfo
    {
        /// <summary>
        /// Gets or sets Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets Department
        /// </summary>
        public string Department { get; set; }

        /// <summary>
        /// Gets or sets Master Ingredient ID
        /// </summary>
        public int MasterIngredientID { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is Usually OnHand
        /// </summary>
        public bool UsuallyOnHand { get; set; }
    }
}
