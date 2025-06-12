using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Photon.Common.ICM
{
    public class AppInsightsAlertData
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("context")]
        public AppInsightsAlertContext Context { get; set; }
    }

    public class AppInsightsAlertContext
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("conditionType")]
        public string ConditionType { get; set; }

        [JsonProperty("condition")]
        public AppInsightsAlertCondition Condition { get; set; }

        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; }

        [JsonProperty("resourceGroupName")]
        public string ResourceGroupName { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("resourceType")]
        public string ResourceType { get; set; }

        [JsonProperty("resourceName")]
        public string ResourceName { get; set; }

        [JsonProperty("portalLink")]
        public string PortalLink { get; set; }
    }

    public class AppInsightsAlertCondition
    {
        [JsonProperty("webTestName")]
        public string WebTestName { get; set; }

        [JsonProperty("failureDetails")]
        public string FailureDetails { get; set; }

        [JsonProperty("metricName")]
        public string MetricName { get; set; }

        [JsonProperty("metricUnit")]
        public string MetricUnit { get; set; }

        [JsonProperty("metricValue")]
        public string MetricValue { get; set; }

        [JsonProperty("threshold")]
        public string Threshold { get; set; }

        [JsonProperty("windowSize")]
        public string WindowSize { get; set; }

        [JsonProperty("timeAggregation")]
        public string TimeAggregation { get; set; }

        [JsonProperty("operator")]
        public string Operator { get; set; }
    }
}