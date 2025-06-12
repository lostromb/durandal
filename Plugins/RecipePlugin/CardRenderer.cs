using AdaptiveCards;
using AdaptiveCards.Rendering;
using AdaptiveCards.Rendering.Html;
using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.File;
using Durandal.Plugins.Recipe.BigOven.Schemas;
using Durandal.Plugins.Recipe.Schemas;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Recipe
{
    public static class CardRenderer
    {
        public static async Task<string> RenderAdaptiveCardsAsHtml(AdaptiveCard card, IPluginServices pluginServices)
        {
            pluginServices.Logger.Log("Adaptive card is " + card.ToJson());

            // Load the host config from plugin file system
            AdaptiveHostConfig hostConfig = new AdaptiveHostConfig()
            {
                SupportsInteractivity = true,
            };

            VirtualPath hostConfigFile = pluginServices.PluginDataDirectory.Combine("adaptive-card-host-config.json");
            if (await pluginServices.FileSystem.ExistsAsync(hostConfigFile).ConfigureAwait(false))
            {
                using (Stream jsonReadStream = await pluginServices.FileSystem.OpenStreamAsync(hostConfigFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
                {
                    JsonSerializer deserializer = new JsonSerializer();
                    using (JsonReader reader = new JsonTextReader(new StreamReader(jsonReadStream)))
                    {
                        hostConfig = deserializer.Deserialize<AdaptiveHostConfig>(reader);
                    }
                }
            }

            AdaptiveCardRenderer renderer = new AdaptiveCardRenderer(hostConfig);
            RenderedAdaptiveCard rendered = renderer.RenderCard(card);

            return rendered.Html.ToString();
        }

        public static List<AdaptiveCard> RenderRecipeList(IList<RecipeData> recipes, IPluginServices services, string currentDomain)
        {
            List<AdaptiveCard> returnVal = new List<AdaptiveCard>();
            int recipeIndex = 1;
            foreach (var recipe in recipes)
            {
                DialogAction submitAction = new DialogAction()
                {
                    Domain = currentDomain,
                    Intent = Constants.INTENT_SELECT,
                    InteractionMethod = InputMethod.Tactile,
                    Slots = new List<SlotValue>()
                    {
                        new SlotValue(Constants.SLOT_SELECTION, recipeIndex.ToString(), SlotValueFormat.DialogActionParameter)
                    }
                };

                var submitActionData = new AdaptiveCardDialogAction(services.RegisterDialogAction(submitAction));
                AdaptiveCard adaptiveCard = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
                {
                    Body = new List<AdaptiveElement>()
                        {
                            new AdaptiveImage() { Url = recipe.ImageUrls.First(), AltText = recipe.Name, Size = AdaptiveImageSize.Large, SelectAction = new AdaptiveSubmitAction() { DataJson = JsonConvert.SerializeObject(submitActionData) } },
                            new AdaptiveTextBlock() { Text = recipe.Name, Weight = AdaptiveTextWeight.Bolder },
                            new AdaptiveTextBlock() { Text = $"{recipe.StarRating.ToString("n1", CultureInfo.CurrentCulture)} stars.", IsSubtle = true },
                        },
                    Actions = new List<AdaptiveAction>()
                        {
                            new AdaptiveOpenUrlAction() { Title = "More details", Url = recipe.WebUrl },
                            new AdaptiveSubmitAction() { Title = "Select", DataJson = JsonConvert.SerializeObject(submitActionData) },
                        },
                };

                returnVal.Add(adaptiveCard);
            }
            
            return returnVal;
        }

        public static AdaptiveCard RenderSingleRecipeIngredients(BigOvenRecipe recipeDetail, IPluginServices services, string currentDomain)
        {
            DialogAction showInstructionsAction = new DialogAction()
            {
                Domain = currentDomain,
                Intent = Constants.INTENT_GET_INSTRUCTIONS,
                InteractionMethod = InputMethod.Tactile,
                Slots = new List<SlotValue>()
            };

            var showInstructionsActionData = new AdaptiveCardDialogAction(services.RegisterDialogAction(showInstructionsAction));

            var ingredients = BuildIngredientsList(recipeDetail);
            if (ingredients == null)
            {
                return null;
            }

            AdaptiveCard adaptiveCard = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
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
                            DataJson = JsonConvert.SerializeObject(showInstructionsActionData)
                        },
                    }
            };

            return adaptiveCard;
        }

        public static AdaptiveCard RenderSingleRecipeInstructions(BigOvenRecipe recipeDetail, IPluginServices services, string currentDomain)
        {
            DialogAction showStepByStepAction = new DialogAction()
            {
                Domain = currentDomain,
                Intent = Constants.INTENT_GET_DETAILED_INSTRUCTIONS,
                InteractionMethod = InputMethod.Tactile,
                Slots = new List<SlotValue>()
            };

            var showStepByStepActionData = new AdaptiveCardDialogAction(services.RegisterDialogAction(showStepByStepAction));

            var instructions = BuildInstructionsList(recipeDetail);
            if (instructions == null)
            {
                return null;
            }

            AdaptiveCard adaptiveCard = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
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
                            DataJson = JsonConvert.SerializeObject(showStepByStepActionData)
                        },
                    }
            };

            return adaptiveCard;
        }

        private static List<AdaptiveElement> BuildIngredientsList(BigOvenRecipe recipeDetail)
        {
            // generate the Ingredients list from recipe details
            List<AdaptiveElement> ingredients = new List<AdaptiveElement>();
            if (recipeDetail == null || recipeDetail.Ingredients == null)
            {
                return null;
            }

            ingredients.Add(new AdaptiveTextBlock() { Text = recipeDetail.YieldNumber + " " + recipeDetail.YieldUnit, Weight = AdaptiveTextWeight.Default, Separator = true });

            AdaptiveTextBlock ingredient;

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

        public static List<AdaptiveElement> BuildInstructionsList(BigOvenRecipe recipeDetail)
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
