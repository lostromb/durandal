using BigOven.Schemas;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BigOven
{
    public sealed class BigOvenService
    {
        private const string BaseUrl = "https://api2.bigoven.com";
        private const string ApiKey = "07pJZqH7YZrY85Q42L11jp1U74Om368N";
        private IHttpClient _httpClient;
        private ILogger _logger;

        public BigOvenService(IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateHttpClient(new Uri(BaseUrl), _logger);
        }

        /// <summary>
        /// Get Recipes based on query text
        /// </summary>
        /// <param name="query">query string</param>
        /// <returns>list of recipe</returns>
        public async Task<List<Recipe>> GetRecipes(string query)
        {
            List<Recipe> recipes = new List<Recipe>();
            _logger.Log("Start to call API to get recipes");

            try
            {
                HttpRequest request = HttpRequest.BuildFromUrlString(BaseUrl + "/recipes?title_kw=" + query + "&rpp=6&champion=1", "GET");
                SetApiKey(request, ApiKey);
                NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendRequestAsync(request, _logger);
                if (netResponse == null || !netResponse.Success)
                {
                    _logger.Log("Null response from BigOven service", LogLevel.Err);
                    return recipes;
                }

                HttpResponse response = netResponse.Response;
                if (response.ResponseCode >= 300)
                {
                    _logger.Log("Non-success response from BigOven service", LogLevel.Err);
                    _logger.Log(response.ResponseCode + " " + response.ResponseMessage, LogLevel.Err);
                    _logger.Log("Response body: " + response.GetPayloadAsString(), LogLevel.Err);
                    return recipes;
                }

                JObject recipesResponse = JObject.Parse(response.GetPayloadAsString());
                recipes = recipesResponse["Results"].ToObject<List<Recipe>>();
                _logger.Log("Response content was: " + recipesResponse);
                _logger.Log("Got recipes successfully!");
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
        public async Task<RecipeDetail> GetRecipeDetail(string recipeId)
        {
            RecipeDetail recipeDetail = new RecipeDetail();
            _logger.Log("Start to call API to get recipe details");

            try
            {
                HttpRequest request = HttpRequest.BuildFromUrlString(BaseUrl + "/recipes/" + recipeId, "GET");
                SetApiKey(request, ApiKey);
                NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendRequestAsync(request, _logger);
                if (netResponse == null || !netResponse.Success)
                {
                    _logger.Log("Null response from BigOven service", LogLevel.Err);
                    return recipeDetail;
                }

                HttpResponse response = netResponse.Response;
                if (response.ResponseCode >= 300)
                {
                    _logger.Log("Non-success response from BigOven service", LogLevel.Err);
                    _logger.Log(response.ResponseCode + " " + response.ResponseMessage, LogLevel.Err);
                    _logger.Log("Response body: " + response.GetPayloadAsString(), LogLevel.Err);
                    return recipeDetail;
                }

                recipeDetail = JsonConvert.DeserializeObject<RecipeDetail>(response.GetPayloadAsString());
                _logger.Log("Got recipe detail successfully!");
            }
            catch (Exception ex)
            {
                var reply = $"Oops! Something went wrong :( Technical Details: {ex.InnerException.Message})";
                _logger.Log(ex, LogLevel.Err);
                return recipeDetail;
            }

            return recipeDetail;
        }

        private static void SetApiKey(HttpRequest httpClient, string api_key)
        {
            httpClient.RequestHeaders.Add("X-BigOven-API-Key", api_key);
        }
    }
}
