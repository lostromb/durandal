

namespace Durandal.Plugins.Recipe
{
    using AdaptiveCards;
    using Durandal.API;
        using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Tasks;
    using Durandal.Plugins.Recipe.BigOven;
    using Durandal.Plugins.Recipe.BigOven.Schemas;
    using Durandal.Plugins.Recipe.NL;
    using Durandal.Plugins.Recipe.Schemas;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Common.Client.Actions;

    public class RecipePlugin : DurandalPlugin
    {
        private readonly IHttpClientFactory _overrideHttpClientFactory = null;
        private BigOvenService _service;
        private RecipeInstructionParser _instructionParser;

        public RecipePlugin() : base("recipe")
        {
            _overrideHttpClientFactory = null;
        }

        public RecipePlugin(IHttpClientFactory httpClient) : base("recipe")
        {
            _overrideHttpClientFactory = httpClient;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _service = new BigOvenService("07pJZqH7YZrY85Q42L11jp1U74Om368N", _overrideHttpClientFactory ?? services.HttpClientFactory, services.Logger.Clone("BigovenService"));
            _instructionParser = new RecipeInstructionParser(services.Logger, "en-US");

            //VirtualPath recipeTrainingFile = services.PluginDataDirectory.Combine("annotated_recipes.en-US.txt");
            //if (await services.FileSystem.ExistsAsync(recipeTrainingFile))
            //{
            //    _instructionParser.Train(await services.FileSystem.ReadLinesAsync(recipeTrainingFile));
            //}

            //VirtualPath recipeValidationFile = services.PluginDataDirectory.Combine("unannotated recipes.en-US.txt");
            //if (await services.FileSystem.ExistsAsync(recipeValidationFile))
            //{
            //    _instructionParser.TestEvaluate(await services.FileSystem.ReadLinesAsync(recipeValidationFile));
            //}

            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            // Conversation tree nodes
            IConversationNode findRecipeNode = tree.CreateNode(FindRecipe);
            IConversationNode expectingRecipeSearchNode = tree.CreateNode(null, "IConversationNode_ExpectingRecipeSearch");
            IConversationNode findRecipeDirectNode = tree.CreateNode(FindRecipeDirect);
            IConversationNode showSearchResultsNode = tree.CreateNode(null, "IConversationNode_ShowSearchResults");
            IConversationNode selectSearchResultNode = tree.CreateNode(SelectSearchResult);
            IConversationNode showRecipeIngredientsNode = tree.CreateNode(ShowIngredients, "IConversationNode_ShowRecipeIngredients");
            IConversationNode showRecipeInstructionsNode = tree.CreateNode(ShowInstructions, "IConversationNode_ShowRecipeInstructions");
            IConversationNode showRecipeStepByStepNode = tree.CreateNode(ShowInstructionsStepByStep, "IConversationNode_ShowInstructionsStepByStep");
            IConversationNode askToContinueNode = tree.CreateNode(null, "IConversationNode_AskToContinue");
            IConversationNode startNewRecipeNode = tree.CreateNode(StartNewRecipe);
            IConversationNode showHelpNode = tree.CreateNode(ShowHelp);
            IConversationNode fallbackNode = tree.CreateNode(ChitChat);
            IConversationNode selectRecipeStepNode = tree.CreateNode(SelectRecipe);

            // Transitions
            expectingRecipeSearchNode.CreateNormalEdge(DialogConstants.SIDE_SPEECH_INTENT, findRecipeDirectNode);
            expectingRecipeSearchNode.CreateCommonEdge(DialogConstants.SIDE_SPEECH_INTENT, findRecipeDirectNode);
            expectingRecipeSearchNode.CreateNormalEdge(Constants.INTENT_FIND_RECIPE, findRecipeNode);

            askToContinueNode.CreateCommonEdge("deny", startNewRecipeNode);
            askToContinueNode.CreateCommonEdge("confirm", showRecipeInstructionsNode);
            askToContinueNode.CreateCommonEdge(DialogConstants.SIDE_SPEECH_INTENT, showHelpNode);

            showSearchResultsNode.CreateNormalEdge(Constants.INTENT_SELECT, selectSearchResultNode);

            showRecipeIngredientsNode.CreateNormalEdge(Constants.INTENT_GET_INGREDIENTS, showRecipeIngredientsNode);
            showRecipeIngredientsNode.CreateNormalEdge(Constants.INTENT_GET_INSTRUCTIONS, showRecipeInstructionsNode);
            showRecipeIngredientsNode.CreateNormalEdge(Constants.INTENT_GET_DETAILED_INSTRUCTIONS, showRecipeStepByStepNode);
            //showRecipeInstructionsNode.CreateNormalEdge(Constants.INTENT_SELECT, selectRecipeStepNode); // TODO is it valid to ask "what's the first one" on the ingredients list?
            showRecipeIngredientsNode.CreateNormalEdge(Constants.INTENT_FIND_RECIPE, findRecipeNode);

            showRecipeInstructionsNode.CreateNormalEdge(Constants.INTENT_GET_INGREDIENTS, showRecipeIngredientsNode);
            showRecipeInstructionsNode.CreateNormalEdge(Constants.INTENT_GET_INSTRUCTIONS, showRecipeInstructionsNode);
            showRecipeInstructionsNode.CreateNormalEdge(Constants.INTENT_GET_DETAILED_INSTRUCTIONS, showRecipeStepByStepNode);
            showRecipeInstructionsNode.CreateNormalEdge(Constants.INTENT_SELECT, selectRecipeStepNode);
            showRecipeInstructionsNode.CreateNormalEdge(Constants.INTENT_FIND_RECIPE, findRecipeNode);

            showRecipeStepByStepNode.CreateNormalEdge(Constants.INTENT_GET_INGREDIENTS, showRecipeIngredientsNode);
            showRecipeStepByStepNode.CreateNormalEdge(Constants.INTENT_GET_INSTRUCTIONS, showRecipeInstructionsNode); // Debatable - this lets user transition from step-by-step to instructions overview by saying "show the instructions"
            showRecipeStepByStepNode.CreateNormalEdge(Constants.INTENT_SELECT, selectRecipeStepNode);
            showRecipeStepByStepNode.CreateNormalEdge(Constants.INTENT_FIND_RECIPE, findRecipeNode);

            // Conversation starters
            tree.AddStartState(Constants.INTENT_FIND_RECIPE, findRecipeNode);
            tree.AddStartState(Constants.INTENT_GET_INGREDIENTS, findRecipeNode);
            tree.AddStartState(Constants.INTENT_GET_INSTRUCTIONS, findRecipeNode);

            tree.AddStartState(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, fallbackNode);
            tree.AddStartState(DialogConstants.SIDE_SPEECH_INTENT, fallbackNode);
            tree.AddStartState(Constants.INTENT_HELP, showHelpNode);

            tree.AddStartState("test_adaptive_card", TestAdaptiveCards);

            return tree;
        }

        public async Task<PluginResult> TestAdaptiveCards(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            BigOvenRecipe recipe = new BigOvenRecipe()
            {
                Title = "Meatballs",
                PhotoUrl = "http://food.fnr.sndimg.com/content/dam/images/food/fullset/2015/1/28/1/FNM_030115-Insert-No1-Italian-Meatballs_s4x3.jpg.rend.hgtvcom.1280.960.suffix/1422458377210.jpeg",
                YieldNumber = 1,
                YieldUnit = "meatball",
                Ingredients = new List<BigOvenIngredient>()
                {
                    new BigOvenIngredient()
                    {
                        DisplayQuantity = "1",
                        Name = "meatball",
                        Unit = "large"
                    },
                }
            };

            AdaptiveCard card = CardRenderer.RenderSingleRecipeIngredients(recipe, pluginServices, "recipe");
            string adaptiveCardElement = await CardRenderer.RenderAdaptiveCardsAsHtml(card, pluginServices).ConfigureAwait(false);

            string html = "<html><body><div style=\"width:500px\" >" + adaptiveCardElement + "</div></body></html>";

            pluginServices.Logger.Log(html);

            return new PluginResult(Result.Success)
            {
                ResponseText = "Here is an adaptive card",
                ResponseSsml = "Here is an adaptive card",
                ResponseHtml = html
            };
        }

        public async Task<PluginResult> ChitChat(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            if (pluginServices.LocalUserProfile.ContainsKey(Constants.CONTEXT_CURRENT_STATE))
            {
                BigOvenUserState userState = pluginServices.LocalUserProfile.GetObject<BigOvenUserState>(Constants.CONTEXT_CURRENT_STATE);
                string text = "Would you like to continue your " + userState.Recipe.Title + " recipe?";
                return new PluginResult(Result.Success)
                {
                    ResponseText = text,
                    ResponseSsml = text,
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                    ResultConversationNode = "ConversationNode_AskToContinue"
                };
            }
            else
            {
                string text = "Welcome to BigOven. You can search for recipes by name. What can I help you cook?";
                return new PluginResult(Result.Success)
                {
                    ResponseText = text,
                    ResponseSsml = text,
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                    ResultConversationNode = "ConversationNode_ExpectingRecipeSearch"
                };
            }
        }

        public async Task<PluginResult> StartNewRecipe(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            pluginServices.LocalUserProfile.Remove(Constants.CONTEXT_CURRENT_STATE);

            string text = "Alright, what can I help you cook?";
            return new PluginResult(Result.Success)
            {
                ResponseText = text,
                ResponseSsml = text,
                MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                ResultConversationNode = "ConversationNode_ExpectingRecipeSearch"
            };
        }

        public async Task<PluginResult> ShowHelp(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            string text = "I can help you find recipes and make them. Try saying things like 'How do I make pancakes'. I can also guide you through the instructions. Say 'next' or 'previous' to move between steps.";
            return new PluginResult(Result.Success)
            {
                ResponseText = text,
                ResponseSsml = text,
                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
            };
        }

        public async Task<PluginResult> FindRecipe(QueryWithContext queryWithContext, IPluginServices services)
        {
            string recipeName = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_RECIPE_NAME);
            if (string.IsNullOrEmpty(recipeName))
            {
                string text = "Sure, what recipe do you want me to search for?";
                return new PluginResult(Result.Success)
                {
                    ResponseText = text,
                    ResponseSsml = text,
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                    ResultConversationNode = "ConversationNode_ExpectingRecipeSearch"
                };
            }

            return await RunRecipeSearchInternal(recipeName, queryWithContext, services).ConfigureAwait(false);
        }

        public async Task<PluginResult> FindRecipeDirect(QueryWithContext queryWithContext, IPluginServices services)
        {
            string recipeName = queryWithContext.Understanding.Utterance.OriginalText;
            return await RunRecipeSearchInternal(recipeName, queryWithContext, services).ConfigureAwait(false);
        }

        public async Task<PluginResult> SelectSearchResult(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!services.SessionStore.ContainsKey(Constants.CONTEXT_SEARCH_RESULTS))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Invalid state: selected non-existent search results (most likely from expired canvas)",
                    MultiTurnResult = MultiTurnBehavior.None
                };
            }

            string selection = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_SELECTION);

            if (string.IsNullOrEmpty(selection))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Attempting to select with no selection slot present",
                    MultiTurnResult = MultiTurnBehavior.None
                };
            }

            List<BigOvenRecipeSearchResult> pastSearchResults = services.SessionStore.GetObject<List<BigOvenRecipeSearchResult>>(Constants.CONTEXT_SEARCH_RESULTS);
            int ordinal = int.Parse(selection) - 1; // FIXME SUPER JANKY
            BigOvenRecipe details = await _service.GetRecipeDetail(pastSearchResults[ordinal].RecipeID, services.Logger).ConfigureAwait(false);
            if (details == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Service error: Could not fetch recipe details",
                };
            }

            BigOvenUserState userState = new BigOvenUserState()
            {
                Recipe = details,
                Step = null,
                ViewState = RecipeViewState.InstructionsList
            };

            return await ShowRecipeInternal(userState, queryWithContext, services).ConfigureAwait(false);
        }

        public async Task<PluginResult> ShowInstructions(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!services.LocalUserProfile.ContainsKey(Constants.CONTEXT_CURRENT_STATE))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Invalid state: no current recipe in session",
                    MultiTurnResult = MultiTurnBehavior.None
                };
            }

            BigOvenUserState userState = services.LocalUserProfile.GetObject<BigOvenUserState>(Constants.CONTEXT_CURRENT_STATE);
            userState.ViewState = RecipeViewState.InstructionsList;
            return await ShowRecipeInternal(userState, queryWithContext, services).ConfigureAwait(false);
        }

        public async Task<PluginResult> ShowIngredients(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!services.LocalUserProfile.ContainsKey(Constants.CONTEXT_CURRENT_STATE))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Invalid state: no current recipe in session",
                    MultiTurnResult = MultiTurnBehavior.None
                };
            }

            BigOvenUserState userState = services.LocalUserProfile.GetObject<BigOvenUserState>(Constants.CONTEXT_CURRENT_STATE);
            userState.ViewState = RecipeViewState.Ingredients;
            return await ShowRecipeInternal(userState, queryWithContext, services).ConfigureAwait(false);
        }

        public async Task<PluginResult> ShowInstructionsStepByStep(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!services.LocalUserProfile.ContainsKey(Constants.CONTEXT_CURRENT_STATE))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Invalid state: no current recipe in session",
                    MultiTurnResult = MultiTurnBehavior.None
                };
            }

            BigOvenUserState userState = services.LocalUserProfile.GetObject<BigOvenUserState>(Constants.CONTEXT_CURRENT_STATE);
            userState.ViewState = RecipeViewState.InstructionsIndividual;
            userState.Step = 0;
            return await ShowRecipeInternal(userState, queryWithContext, services).ConfigureAwait(false);
        }

        public async Task<PluginResult> SelectRecipe(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            if (!pluginServices.LocalUserProfile.ContainsKey(Constants.CONTEXT_CURRENT_STATE))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Invalid state: no current recipe in session",
                    MultiTurnResult = MultiTurnBehavior.None
                };
            }

            BigOvenUserState userState = pluginServices.LocalUserProfile.GetObject<BigOvenUserState>(Constants.CONTEXT_CURRENT_STATE);

            if (!userState.Step.HasValue)
            {
                userState.Step = 0;
            }

            userState.ViewState = RecipeViewState.InstructionsIndividual;

            // Look at the selection slot to see which step to show
            string selectionSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_SELECTION);
            int selectionOrdinal;
            if (string.Equals(selectionSlot, "NEXT"))
            {
                userState.Step = userState.Step.Value + 1;
            }
            else if (string.Equals(selectionSlot, "PREV"))
            {
                userState.Step = Math.Max(0, userState.Step.Value - 1);
            }
            else if (int.TryParse(selectionSlot, out selectionOrdinal))
            {
                userState.Step = selectionOrdinal - 1;
            }

            return await ShowRecipeInternal(userState, queryWithContext, pluginServices).ConfigureAwait(false);
        }

        private async Task<PluginResult> RunRecipeSearchInternal(string query, QueryWithContext queryWithContext, IPluginServices services)
        {
            IList<RecipeData> searchResults = await _service.SearchRecipes(query, services.Logger).ConfigureAwait(false);

            if (searchResults.Count == 0)
            {
                // No results...
                string text = "I didn't find any recipes for " + query;
                return new PluginResult(Result.Success)
                {
                    ResponseText = text,
                    ResponseSsml = text,
                    MultiTurnResult = MultiTurnBehavior.None
                };
            }
            else if (searchResults.Count == 1)
            {
                // Go directly to the recipe summary
                BigOvenRecipe details = await _service.GetRecipeDetail(int.Parse(searchResults[0].SourceId), services.Logger).ConfigureAwait(false);
                if (details == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Service error: Could not fetch recipe details",
                    };
                }

                BigOvenUserState userState = new BigOvenUserState()
                {
                    Recipe = details,
                    Step = null,
                    ViewState = RecipeViewState.Ingredients
                };

                return await ShowRecipeInternal(userState, queryWithContext, services).ConfigureAwait(false);
            }
            else
            {
                // Show all results and prompt for selection
                string text = "I found " + searchResults.Count + " recipes for " + query;

                // Stash search results for selection later
                services.SessionStore.Put(Constants.CONTEXT_SEARCH_RESULTS, searchResults);

                IList<AdaptiveCard> renderedCards = CardRenderer.RenderRecipeList(searchResults, services, this.PluginId);
                ShowAdaptiveCardAction clientAction = new ShowAdaptiveCardAction()
                {
                    Card = JArray.FromObject(renderedCards)
                };

                return new PluginResult(Result.Success)
                {
                    ResponseText = text,
                    ResponseSsml = text,
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                    ResultConversationNode = "ConversationNode_ShowSearchResults",
                    ClientAction = JsonConvert.SerializeObject(clientAction)
                };
            }
        }

        private async Task<PluginResult> ShowRecipeInternal(BigOvenUserState userState, QueryWithContext queryWithContext, IPluginServices services)
        {
            RecipeViewState viewState = userState.ViewState.GetValueOrDefault(RecipeViewState.InstructionsList);

            if (viewState == RecipeViewState.Ingredients)
            {
                // Show the recipe as a list of ingredients
                string text = "Here are the recipe ingredients";
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);

                AdaptiveCard renderedCard = CardRenderer.RenderSingleRecipeIngredients(userState.Recipe, services, this.PluginId);
                ShowAdaptiveCardAction clientAction = new ShowAdaptiveCardAction()
                {
                    Card = JObject.FromObject(renderedCard)
                };

                services.LocalUserProfile.Put(Constants.CONTEXT_CURRENT_STATE, userState);

                return new PluginResult(Result.Success)
                {
                    ResponseText = text,
                    ResponseSsml = text,
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                    ResultConversationNode = "ConversationNode_ShowRecipeIngredients",
                    ClientAction = JsonConvert.SerializeObject(clientAction)
                };
            }
            else if (viewState == RecipeViewState.InstructionsList)
            {
                // Show the recipe as a list of instructions
                string text = "Here are the recipe instructions";
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);

                AdaptiveCard renderedCard = CardRenderer.RenderSingleRecipeInstructions(userState.Recipe, services, this.PluginId);
                ShowAdaptiveCardAction clientAction = new ShowAdaptiveCardAction()
                {
                    Card = JObject.FromObject(renderedCard)
                };

                services.LocalUserProfile.Put(Constants.CONTEXT_CURRENT_STATE, userState);

                return new PluginResult(Result.Success)
                {
                    ResponseText = text,
                    ResponseSsml = text,
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                    ResultConversationNode = "ConversationNode_ShowRecipeInstructions",
                    ClientAction = JsonConvert.SerializeObject(clientAction)
                };
            }
            else if (viewState == RecipeViewState.InstructionsIndividual)
            {
                int step = userState.Step.GetValueOrDefault(0);
                // Show an individual instruction in the current recipe
                var instructions = CardRenderer.BuildInstructionsList(userState.Recipe);
                if (instructions == null)
                {
                    return new PluginResult(Result.Success)
                    {
                        ResponseText = "Error: No instructions."
                    };
                }

                var text_block = instructions[step] as AdaptiveTextBlock;

                // handle the case for bad previous step. just show the first step
                if (step < 0)
                {
                    step = 0;
                }

                // handle the case for bad next step. just show the last step
                if (step >= instructions.Count)
                {
                    return new PluginResult(Result.Success)
                    {
                        ResponseText = "You have reached the last step. You can start over or check previous step."
                    };
                }
                else
                {
                    DialogAction showStepByStepAction = new DialogAction()
                    {
                        Domain = this.PluginId,
                        Intent = Constants.INTENT_SELECT,
                        InteractionMethod = InputMethod.Tactile,
                        Slots = new List<SlotValue>()
                        {
                            new SlotValue(Constants.SLOT_SELECTION, "NEXT", SlotValueFormat.DialogActionParameter)
                        }
                    };

                    AdaptiveCardDialogAction nextStepDialogAction = new AdaptiveCardDialogAction(services.RegisterDialogAction(showStepByStepAction));

                    var instruction_body = new List<AdaptiveElement>();
                    instruction_body.Add(new AdaptiveTextBlock() { Text = text_block.Text, Wrap = true, Weight = AdaptiveTextWeight.Bolder, Spacing = AdaptiveSpacing.Padding });
                    var body = new List<AdaptiveElement>()
                    {
                        new AdaptiveColumnSet()
                        {
                            Columns = new List<AdaptiveColumn>()
                            {
                                new AdaptiveColumn()
                                {
                                    Items = instruction_body,
                                },
                            },
                        },
                    };
                    var action = new List<AdaptiveAction>()
                    {
                        new AdaptiveSubmitAction()
                        {
                            Title = "Next",
                            DataJson = JsonConvert.SerializeObject(nextStepDialogAction)
                        },
                    };
                    if (step + 1 == instructions.Count)
                    {
                        instruction_body.Add(new AdaptiveTextBlock() { Text = "Done!" });
                        action = null;
                    }

                    AdaptiveCard adaptiveCard = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
                    {
                        Body = body,
                        Actions = action,
                    };

                    ShowAdaptiveCardAction clientAction = new ShowAdaptiveCardAction()
                    {
                        Card = JObject.FromObject(adaptiveCard)
                    };

                    // save the user data if the step is not the last step
                    string responseText;
                    if (step < instructions.Count - 1)
                    {
                        userState.Step = step;
                        responseText = text_block.Text;
                    }
                    else
                    {
                        userState.Step = null;
                        responseText = "You have reached the last step. You can start over or check previous step.";
                    }

                    services.LocalUserProfile.Put(Constants.CONTEXT_CURRENT_STATE, userState);

                    return new PluginResult(Result.Success)
                    {
                        ResponseText = responseText,
                        ResponseSsml = responseText,
                        MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                        ResultConversationNode = "ConversationNode_ShowInstructionsStepByStep",
                        ClientAction = JsonConvert.SerializeObject(clientAction)
                    };
                }
            }
            else
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Invalid recipe view state"
                };
            }
        }
    }
}
