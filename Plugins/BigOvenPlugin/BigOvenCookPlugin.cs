using Durandal.API;
using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.API.Data;
using Durandal.API.Utils;
using BigOven.Schemas;

namespace BigOven
{
    public class BigOvenCookPlugin : DurandalPlugin
    {
        private readonly IHttpClientFactory _overrideHttpClientFactory = null;
        private BigOvenService _service;

        public BigOvenCookPlugin()
            : base(Constants.DOMAIN_RECIPE)
        {
            _overrideHttpClientFactory = null;
        }

        public BigOvenCookPlugin(IHttpClientFactory httpClient) : base(Constants.DOMAIN_RECIPE)
        {
            _overrideHttpClientFactory = httpClient;
        }

        public override void OnLoad(PluginServices services)
        {
            _service = new BigOvenService(_overrideHttpClientFactory ?? services.HttpClientFactory, services.Logger.Clone("BigovenService"));
        }

        protected override ConversationTree BuildConversationTree(ConversationTree tree)
        {
            ConversationNode findRecipeNode = tree.CreateNode(FindRecipe);
            tree.AddStartState(Constants.INTENT_FIND_RECIPE, findRecipeNode);

            ConversationNode expectingRecipeSearchNode = tree.CreateNode(null, "ConversationNode_ExpectingRecipeSearch");
            ConversationNode findRecipeDirectNode = tree.CreateNode(FindRecipeDirect);
            expectingRecipeSearchNode.CreateCommonEdge("side_speech", findRecipeDirectNode);
            
            return tree;
        }

        public async Task<DialogResult> FindRecipe(QueryWithContext queryWithContext, PluginServices services)
        {
            string recipeName = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_RECIPE_NAME);
            if (string.IsNullOrEmpty(recipeName))
            {
                return new DialogResult(Result.Success)
                {
                    ResponseText = "Sure, what recipe do you want me to search for?",
                    ResponseSSML = "Sure, what recipe do you want me to search for?",
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                    ResultConversationNode = "ConversationNode_ExpectingRecipeSearch"
                };
            }

            return await ShowRecipeSearchResults(recipeName, queryWithContext, services);
        }

        public async Task<DialogResult> FindRecipeDirect(QueryWithContext queryWithContext, PluginServices services)
        {
            string recipeName = queryWithContext.Understanding.Utterance.OriginalText;
            return await ShowRecipeSearchResults(recipeName, queryWithContext, services);
        }

        private async Task<DialogResult> ShowRecipeSearchResults(string query, QueryWithContext queryWithContext, PluginServices services)
        {
            List<Recipe> searchResults = await _service.GetRecipes(query);

            return new DialogResult(Result.Success)
            {
                ResponseText = "I found some recipes"
            };
        }
    }
}
