using AdaptiveCards;
using BigOven.Schemas;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigOven
{
    public static class CardRenderer
    {
        public static List<AdaptiveCard> RenderRecipeList(IList<Recipe> recipes)
        {
            List<AdaptiveCard> returnVal = new List<AdaptiveCard>();
            foreach (var recipe in recipes)
            {
                var submitActionData = JObject.Parse("{ \"Type\": \"RecipeSelection\" }");
                submitActionData.Merge(JObject.FromObject(recipe));
                AdaptiveCard adaptiveCard = new AdaptiveCard()
                {
                    Body = new List<AdaptiveElement>()
                        {
                            new AdaptiveImage() { Url = new Uri(recipe.PhotoUrl), AltText = recipe.Title, Size = AdaptiveImageSize.Large, SelectAction = new AdaptiveSubmitAction() { DataJson = submitActionData.ToString() } },
                            new AdaptiveTextBlock() { Text = recipe.Title, Weight = AdaptiveTextWeight.Bolder },
                            new AdaptiveTextBlock() { Text = $"{recipe.StarRating.ToString("n1", CultureInfo.CurrentCulture)} stars. {recipe.ReviewCount} reviews. Total tries: {recipe.TotalTries}", IsSubtle = true },
                        },
                    Actions = new List<AdaptiveAction>()
                        {
                            new AdaptiveOpenUrlAction() { Title = "More details", Url = new Uri(recipe.WebURL) },
                            new AdaptiveSubmitAction() { Title = "Select", DataJson = submitActionData.ToString() },
                        },
                };

                returnVal.Add(adaptiveCard);
            }

            return returnVal;
        }

        public static AdaptiveCard RenderSingleRecipeIngredients(RecipeDetail recipeDetail)
        {
            var submitActionData = JObject.Parse("{ \"Type\": \"RecipeInstructionsSelection\" }");
            submitActionData.Merge(JObject.FromObject(recipeDetail));

            var ingredients = BuildIngredientsList(recipeDetail);
            if (ingredients == null)
            {
                return null;
            }

            AdaptiveCard adaptiveCard = new AdaptiveCard()
            {
                Body = new List<AdaptiveElement>()
                    {
                    new AdaptiveTextBlock() { Text = recipeDetail.Title, Weight = AdaptiveTextWeight.Bolder },
                    new AdaptiveColumnSet()
                        {
                            Columns = new List<AdaptiveColumn>()
                            {
                                new AdaptiveColumn()
                                {
                                    Items = ingredients,
                                    Width = "75",
                                },
                                new AdaptiveColumn()
                                {
                                    Items = new List<AdaptiveElement>()
                                    {
                                        new AdaptiveImage() { Url = new Uri(recipeDetail.PhotoUrl), AltText = recipeDetail.Title, Size = AdaptiveImageSize.Medium },
                                    },
                                    Width = "25",
                                },
                            },
                        },
                    },
                Actions = new List<AdaptiveAction>()
                    {
                        new AdaptiveSubmitAction()
                        {
                            Title = "Show Instructions",
                            DataJson = submitActionData.ToString(),
                        },
                    }
            };

            return adaptiveCard;
        }

        public static AdaptiveCard RenderSingleRecipeInstructions(RecipeDetail recipeDetail)
        {
            var submitActionData = JObject.Parse("{ \"Type\": \"RecipeInstructionsStepByStep\" }");
            submitActionData.Merge(JObject.FromObject(recipeDetail));

            var instructions = BuildInstructionsList(recipeDetail);
            if (instructions == null)
            {
                return null;
            }

            AdaptiveCard adaptiveCard = new AdaptiveCard()
            {
                Body = new List<AdaptiveElement>()
                    {
                    new AdaptiveTextBlock() { Text = recipeDetail.Title + " Instructions", Weight = AdaptiveTextWeight.Bolder },
                    new AdaptiveColumnSet()
                        {
                            Columns = new List<AdaptiveColumn>()
                            {
                                new AdaptiveColumn()
                                {
                                    Items = instructions,
                                },
                            },
                        },
                    },
                Actions = new List<AdaptiveAction>()
                    {
                        new AdaptiveSubmitAction()
                        {
                            Title = "Step By Step",
                            DataJson = submitActionData.ToString(),
                        },
                    }
            };

            return adaptiveCard;
        }

        private static List<AdaptiveElement> BuildIngredientsList(RecipeDetail recipeDetail)
        {
            // generate the Ingredients list from recipe details
            List<AdaptiveElement> ingredients = new List<AdaptiveElement>();
            if (recipeDetail == null || recipeDetail.Ingredients == null)
            {
                return null;
            }

            ingredients.Add(new AdaptiveTextBlock() { Text = recipeDetail.YieldNumber + " " + recipeDetail.YieldUnit, Weight = AdaptiveTextWeight.Default, Separator = true });

            dynamic ingredient;

            foreach (var i in recipeDetail.Ingredients)
            {
                if (i.Unit != null)
                {
                    ingredient = new AdaptiveTextBlock() { Text = i.DisplayQuantity + " " + i.Unit + " " + i.Name + "\n\r", Wrap = true, Spacing = AdaptiveSpacing.Padding, Weight = AdaptiveTextWeight.Bolder };
                }
                else
                {
                    ingredient = new AdaptiveTextBlock() { Text = i.DisplayQuantity + " " + i.Name + "\n\r", Wrap = true, Spacing = AdaptiveSpacing.Padding, Weight = AdaptiveTextWeight.Bolder };
                }

                ingredients.Add(ingredient);
            }

            return ingredients;
        }

        public static List<AdaptiveElement> BuildInstructionsList(RecipeDetail recipeDetail)
        {
            // check if there are \r\n in the instructions
            // if yes, not action is needed
            // if no, need add \r\n after each period
            int i = 1;

            List<AdaptiveElement> instructions = new List<AdaptiveElement>();
            if (recipeDetail == null || recipeDetail.Instructions == null)
            {
                return null;
            }

            if (!recipeDetail.Instructions.Contains("\r\n"))
            {
                var steps = recipeDetail.Instructions.Split('.');
                foreach (var step in steps)
                {
                    if (!string.IsNullOrEmpty(step))
                    {
                        instructions.Add(new AdaptiveTextBlock() { Text = i.ToString(CultureInfo.CurrentCulture) + ".  " + step + ".", Wrap = true, Weight = AdaptiveTextWeight.Bolder, Spacing = AdaptiveSpacing.Padding });
                        i++;
                    }
                }
            }
            else
            {
                string[] stringSeparators = new string[] { "\r\n" };
                var steps = recipeDetail.Instructions.Split(stringSeparators, StringSplitOptions.None);
                foreach (var step in steps)
                {
                    if (!string.IsNullOrEmpty(step))
                    {
                        instructions.Add(new AdaptiveTextBlock() { Text = i.ToString(CultureInfo.CurrentCulture) + ".  " + step, Wrap = true, Weight = AdaptiveTextWeight.Bolder, Spacing = AdaptiveSpacing.Padding });
                        i++;
                    }
                }
            }

            return instructions;
        }
    }
}
