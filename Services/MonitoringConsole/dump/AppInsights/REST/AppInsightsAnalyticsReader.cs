using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.AppInsights.REST
{
    /// <summary>
    /// An analytics connector which queries the Application Insights REST API for its data
    /// </summary>
    public class AppInsightsAnalyticsReader
    {
        private AppInsightsConnectorConfiguration _configuration;
        private HttpClient _httpClient;

        /// <summary>
        /// Creates a new instance of the ApInsightsAnalyticsConnector class, with a specific endpoint configuration
        /// </summary>
        /// <param name="config">The configuration which specifies endpoint, appid, etc.</param>
        public AppInsightsAnalyticsReader(AppInsightsConnectorConfiguration config)
        {
            _configuration = config;
            if (string.IsNullOrEmpty(_configuration.ApiKey))
            {
                throw new ArgumentException("REST API key is null for accessing Appinsights database " + config.ResourceName);
            }
            if (_configuration.AppId == Guid.Empty)
            {
                throw new ArgumentException("REST AppId is null for accessing Appinsights database " + config.ResourceName);
            }

            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Retrieves a set of analytics events associated with certain query parameters
        /// </summary>
        /// <param name="query">The analytics query to use</param>
        /// <returns>A non-null list of table data that was returned from the data source</returns>
        public async Task<Table> Query(string query)
        {
            try
            {
                string formattedQuery = WebUtility.UrlEncode(query).Replace("%20", "+");
                string finalUrl = string.Format("{0}/{1}/query?query={2}", _configuration.BaseUrl, _configuration.AppId, formattedQuery);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, finalUrl);
                request.Headers.Add("x-api-key", _configuration.ApiKey);
                HttpResponseMessage response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    throw new AppInsightsException("Non-success status code " + response.StatusCode + " returned from " + finalUrl);
                }

                string responseJson = await response.Content.ReadAsStringAsync();

                QueryResponse apInsights = JsonConvert.DeserializeObject<QueryResponse>(responseJson);
                
                if (apInsights == null || apInsights.Tables == null || apInsights.Tables.Count == 0)
                {
                    throw new AppInsightsException("No tables returned for query " + finalUrl);
                }

                // Table 0 is the only one we care about because it has the actual query results
                return apInsights.Tables[0];
            }
            catch (HttpRequestException ex)
            {
                throw new AppInsightsException("Network error while querying AppInsights endpoint! " + ex.Message);
            }
            catch (JsonException ex)
            {
                throw new AppInsightsException("Could not parse JSON response from AppInsights endpoint! " + ex.Message);
            }
        }
    }
}
