using Durandal.Plugins.Recipe.BigOven.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe
{
    public class BigOvenUserState
    {
        public BigOvenRecipe Recipe { get; set; }
        public int? Step { get; set; }
        public RecipeViewState? ViewState { get; set; }
    }
}
