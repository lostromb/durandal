using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.AppInsights
{
    /// <summary>
    /// Contains all information needed to point to a specific AppInsights instance
    /// </summary>
    public class AppInsightsConnectorConfiguration
    {
        /// <summary>
        /// The base URL to query for data. Usually "https://api.applicationinsights.io/v1/apps"
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// The secret API key used to query data (lowercase, no dashes)
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// The ID of the application you are querying (for programmatic access)
        /// </summary>
        public Guid AppId { get; set; }
        
        /// <summary>
        /// The azure subscription ID that this appinsights belongs to
        /// </summary>
        public Guid SubscriptionId { get; set; }

        /// <summary>
        /// The azure resource group that this insights belongs to
        /// </summary>
        public string ResourceGroup { get; set; }

        /// <summary>
        /// The common name of this appinsights instance
        /// </summary>
        public string ResourceName { get; set; }

        public InsightsAppType Type { get; set; }

        public AppInsightsConnectorConfiguration(Guid subscriptionId, string resourceGroup, string resourceName, string apiKey, Guid appId, InsightsAppType type, string baseUrl = "https://api.applicationinsights.io/v1/apps")
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new ArgumentException("Config must contain a valid base URL");
            }

            if (subscriptionId == Guid.Empty)
            {
                throw new ArgumentException("Config must contain a valid subscription guid");
            }

            if (string.IsNullOrEmpty(resourceGroup))
            {
                throw new ArgumentException("Config must contain a valid resource group");
            }

            if (string.IsNullOrEmpty(resourceName))
            {
                throw new ArgumentException("Config must contain a valid resource name");
            }

            //if (string.IsNullOrEmpty(apiKey))
            //{
            //    throw new ArgumentException("Config must contain a valid API key");
            //}

            //if (appId == Guid.Empty)
            //{
            //    throw new ArgumentException("Config must contain a valid app ID");
            //}


            BaseUrl = baseUrl;
            ApiKey = apiKey;
            AppId = appId;
            SubscriptionId = subscriptionId;
            ResourceGroup = resourceGroup;
            ResourceName = resourceName;
            Type = type;
        }
    }
}
