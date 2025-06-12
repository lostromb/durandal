using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Plugins.Recipe.BigOven.Schemas;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Plugins.Recipe.Schemas;
using System.Threading;
using Durandal.Common.Time;

namespace Durandal.Plugins.Recipe.BigOven
{
    public class BigOvenService
    {
        private const string BaseUrl = "https://api2.bigoven.com";
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly string _apiKey;

        public BigOvenService(string apiKey, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateHttpClient(new Uri(BaseUrl), _logger);
            _apiKey = apiKey;

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException("Bigoven API key is null");
            }
        }

        /// <summary>
        /// Get Recipes based on query text
        /// </summary>
        /// <param name="query">query string</param>
        /// <returns>list of recipe</returns>
        public async Task<IList<RecipeData>> SearchRecipes(string query, ILogger queryLogger)
        {
            queryLogger = queryLogger ?? _logger;
            IList<RecipeData> recipes = new List<RecipeData>();
            queryLogger.Log("Start to call API to get recipes");

            try
            {
                using (HttpRequest request = HttpRequest.CreateOutgoing("/recipes", "GET"))
                {
                    request.GetParameters["title_kw"] = query;
                    request.GetParameters["rpp"] = "6";
                    request.GetParameters["champion"] = "1";
                    request.RequestHeaders.Add("X-BigOven-API-Key", _apiKey);
                    using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(
                        request,
                        CancellationToken.None,
                        DefaultRealTimeProvider.Singleton,
                        queryLogger).ConfigureAwait(false))
                    {
                        try
                        {
                            if (netResponse == null || netResponse.Response == null)
                            {
                                queryLogger.Log("Null response from BigOven service", LogLevel.Err);
                                return recipes;
                            }

                            HttpResponse response = netResponse.Response;
                            if (response.ResponseCode >= 300)
                            {
                                queryLogger.Log("Non-success response from BigOven service", LogLevel.Err);
                                queryLogger.Log(response.ResponseCode + " " + response.ResponseMessage, LogLevel.Err);
                                queryLogger.Log("Response body: " + await response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false), LogLevel.Err);
                                return recipes;
                            }

                            queryLogger.Log("BigOven API call took " + netResponse.EndToEndLatency + "ms", LogLevel.Vrb);

                            string rawResponse = await response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            queryLogger.Log("Response content was: " + rawResponse);
                            BigOvenRecipeSearchResponse recipesResponse = JsonConvert.DeserializeObject<BigOvenRecipeSearchResponse>(rawResponse);
                            foreach (BigOvenRecipeSearchResult searchResult in recipesResponse.Results)
                            {
                                recipes.Add(ConvertToUniversalSchema(searchResult));
                            }

                            if (recipesResponse.Results.Count > 0)
                            {
                                await GetRecipeDetail(recipesResponse.Results[0].RecipeID, queryLogger).ConfigureAwait(false);
                            }

                            // TODO: If no results, but there is a spell suggest, rerun the search using the spell suggestion
                            // TODO I could also use the speller LU annotator to search for all spelling variants in parallel

                            queryLogger.Log("Got recipes successfully!");
                        }
                        finally
                        {
                            if (netResponse != null)
                            {
                                await netResponse.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, LogLevel.Err);
                return recipes;
            }

            return recipes;
        }

        /// <summary>
        /// Get Recipe Detail from BigOven by recipe id
        /// </summary>
        /// <param name="recipeId">recipe id</param>
        /// <returns>Task recipe details</returns>
        public async Task<BigOvenRecipe> GetRecipeDetail(int recipeId, ILogger queryLogger)
        {
            queryLogger = queryLogger ?? _logger;
            BigOvenRecipe recipeDetail = null;
            queryLogger.Log("Start to call API to get recipe details");

            try
            {
                using (HttpRequest request = HttpRequest.CreateOutgoing("/recipe/" + recipeId, "GET"))
                {
                    request.RequestHeaders.Add("X-BigOven-API-Key", _apiKey);
                    using (HttpResponse response = await _httpClient.SendRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger).ConfigureAwait(false))
                    {
                        if (response == null)
                        {
                            queryLogger.Log("Null response from BigOven service", LogLevel.Err);
                            return recipeDetail;
                        }

                        try
                        {
                            if (response.ResponseCode >= 300)
                            {
                                queryLogger.Log("Non-success response from BigOven service", LogLevel.Err);
                                queryLogger.Log(response.ResponseCode + " " + response.ResponseMessage, LogLevel.Err);
                                queryLogger.Log("Response body: " + await response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false), LogLevel.Err);
                                return recipeDetail;
                            }

                            //queryLogger.Log("Response content was: " + rawResponse);
                            recipeDetail = await response.ReadContentAsJsonObjectAsync<BigOvenRecipe>(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            queryLogger.Log("INSTRUCTIONS: " + recipeDetail.Instructions);
                            queryLogger.Log("Got recipe detail successfully!");
                        }
                        finally
                        {
                            if (response != null)
                            {
                                await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                queryLogger.Log(ex, LogLevel.Err);
                return recipeDetail;
            }

            return recipeDetail;
        }

        private static RecipeData ConvertToUniversalSchema(BigOvenRecipeSearchResult input)
        {
            RecipeData output = new RecipeData();
            output.DataSource = RecipeSource.BigOvenSearch;
            output.SourceId = input.RecipeID.ToString();
            output.Name = input.Title;
            output.Cuisine = input.Cuisine;
            output.StarRating = input.StarRating;

            if (!string.IsNullOrEmpty(input.WebURL) &&
                Uri.IsWellFormedUriString(input.WebURL, UriKind.Absolute))
            {
                output.WebUrl = new Uri(input.WebURL);
            }

            output.ImageUrls = new List<Uri>();
            if (!string.IsNullOrEmpty(input.PhotoUrl) &&
                Uri.IsWellFormedUriString(input.PhotoUrl, UriKind.Absolute))
            {
                output.ImageUrls.Add(new Uri(input.PhotoUrl));
            }

            return output;
        }

        private static RecipeData ConvertToUniversalSchema(BigOvenRecipe input)
        {
            RecipeData output = new RecipeData();
            output.DataSource = RecipeSource.BigOven;
            output.SourceId = input.RecipeID.ToString();
            output.Name = input.Title;
            output.Description = input.Description;
            output.Cuisine = input.Cuisine;
            output.StarRating = Math.Round(input.StarRating * 2) / 2M;
            output.ActiveTime = TimeSpan.FromMinutes(input.ActiveMinutes);
            output.TotalTime = TimeSpan.FromMinutes(input.TotalMinutes);
            output.Instructions = ParseInstructions(input.Instructions);
            output.Ingredients = ConvertIngredients(input.Ingredients);

            if (!string.IsNullOrEmpty(input.WebURL) &&
                Uri.IsWellFormedUriString(input.WebURL, UriKind.Absolute))
            {
                output.WebUrl = new Uri(input.WebURL);
            }

            output.ImageUrls = new List<Uri>();
            if (!string.IsNullOrEmpty(input.PhotoUrl) &&
                Uri.IsWellFormedUriString(input.PhotoUrl, UriKind.Absolute))
            {
                output.ImageUrls.Add(new Uri(input.PhotoUrl));
            }

            return output;
        }

        private static IList<MeasuredIngredient> ConvertIngredients(IList<BigOvenIngredient> input)
        {
            List<MeasuredIngredient> returnVal = new List<MeasuredIngredient>();
            if (input == null)
            {
                return returnVal;
            }

            foreach (var ingredient in input)
            {
                if (ingredient != null)
                {
                    returnVal.Add(ConvertIngredient(ingredient));
                }
            }

            return returnVal;
        }

        private static MeasuredIngredient ConvertIngredient(BigOvenIngredient input)
        {
            if (input == null)
            {
                return null;
            }

            MeasuredIngredient returnVal = new MeasuredIngredient();
            returnVal.Name = input.Name;
            returnVal.IsOptional = false;
            returnVal.ExactAmount = input.Quantity;
            returnVal.Unit = input.Unit; // TODO canonicalize unit

            return returnVal;
        }

        private static IList<string> ParseInstructions(string instructionBlock)
        {
            List<string> returnVal = new List<string>();
            if (string.IsNullOrEmpty(instructionBlock))
            {
                return returnVal;
            }

            return returnVal;
        }
    }
}
