using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe
{
    public static class Constants
    {
        public static readonly string DOMAIN_COOK = "cook";
        public static readonly string DOMAIN_BARTENDER = "bartender";
        public static readonly string INTENT_FIND_RECIPE = "find_recipe";
        public static readonly string INTENT_GET_INGREDIENTS = "get_ingredients";
        public static readonly string INTENT_GET_QUANTITY = "get_quantity";
        public static readonly string INTENT_GET_INSTRUCTIONS = "get_instructions";
        public static readonly string INTENT_GET_DETAILED_INSTRUCTIONS = "get_detailed_instructions";
        public static readonly string INTENT_HELP = "help";
        public static readonly string INTENT_SELECT = "select";

        public static readonly string SLOT_RECIPE_NAME = "recipe_name";
        public static readonly string SLOT_INGREDIENT = "ingredient";
        public static readonly string SLOT_INSTRUCTION_NUMBER = "instruction_number";
        public static readonly string SLOT_SELECTION = "selection";

        public static readonly string CONTEXT_SEARCH_RESULTS = "lastSearchResults";
        public static readonly string CONTEXT_CURRENT_STATE = "currentState";
    }
}
