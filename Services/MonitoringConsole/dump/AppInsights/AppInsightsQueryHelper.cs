using Photon.Common.AppInsights.REST;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.AppInsights
{
    public static class AppInsightsQueryHelper
    {
        private static readonly IReadOnlyDictionary<AppInsightsDatabase, AppInsightsConnectorConfiguration> _endpointMapping = new Dictionary<AppInsightsDatabase, AppInsightsConnectorConfiguration>()
        {
            { AppInsightsDatabase.DoradoMonitoring, new AppInsightsConnectorConfiguration(
                subscriptionId: Guid.Parse("a4f207ac-6329-4af9-8245-6ce13c0fc425"),
                resourceGroup: "DoradoMonitoring",
                resourceName: "DoradoMonitoringInsights",
                apiKey: null,
                appId: Guid.Empty,
                type: InsightsAppType.Other) },
            
            { AppInsightsDatabase.BotMaster, new AppInsightsConnectorConfiguration(
                subscriptionId: Guid.Parse("a4f207ac-6329-4af9-8245-6ce13c0fc425"),
                resourceGroup: "prod-rg",
                resourceName: "runner-prod-insight",
                apiKey: "swuedhz3mj8vvythz5elf7vacbr48zt5i1qhmloe",
                appId: Guid.Parse("ee2930ad-fc9d-40b3-a1bc-eff206ac9810"),
                type: InsightsAppType.Other) },
            
            { AppInsightsDatabase.PyHost, new AppInsightsConnectorConfiguration(
                subscriptionId: Guid.Parse("a4f207ac-6329-4af9-8245-6ce13c0fc425"),
                resourceGroup: "prod-rg",
                resourceName: "pyhost-prod-insights",
                apiKey: null,
                appId: Guid.Empty,
                type: InsightsAppType.Other) },
            
            { AppInsightsDatabase.CSHost, new AppInsightsConnectorConfiguration(
                subscriptionId: Guid.Parse("a4f207ac-6329-4af9-8245-6ce13c0fc425"),
                resourceGroup: "prod-rg",
                resourceName: "DoradoCSHostServicesAppInsights",
                apiKey: null,
                appId: Guid.Empty,
                type: InsightsAppType.Web) },
            
            { AppInsightsDatabase.UXAPI, new AppInsightsConnectorConfiguration(
                subscriptionId: Guid.Parse("a4f207ac-6329-4af9-8245-6ce13c0fc425"),
                resourceGroup: "prod-rg",
                resourceName: "uxapi-ase-prod-svc",
                apiKey: "ki2n1eugx0adfxi5i2s3ptuolsgzq0xxjmernc6d",
                appId: Guid.Parse("98aa20e7-a9a6-4fef-b9d9-7d361c3bacaa"),
                type: InsightsAppType.Web) },

            { AppInsightsDatabase.BotFrameworkPortal, new AppInsightsConnectorConfiguration(
                subscriptionId: Guid.Parse("a4f207ac-6329-4af9-8245-6ce13c0fc425"),
                resourceGroup: "prod-rg",
                resourceName: "botframework-portal-insight",
                apiKey: "gsl71pilan0bxoht4foube4jwuu2374nbd101mmc",
                appId: Guid.Parse("2ad83af5-36bd-4b27-ae57-e6d7d45c0090"),
                type: InsightsAppType.Other) }
        };

        private static string Query_SingleTestPassRate = "customEvents\n" +
            "| where name == \"TestRan\"\n" +
            "| where timestamp > ago(24h)\n" +
            "| where customDimensions.TestName == \"{0}\"\n" +
            "| project Time = timestamp, PassRate = 100 * todouble(customDimensions.Success), Latency = todouble(customMeasurements.Latency), TraceId = customDimensions.TraceId, DC = tostring(customDimensions.DC)\n" +
            "| summarize avg(PassRate) by bin(Time, 10m)\n" +
            "| render timechart\n";

        private static string Query_SingleTestLatency = "customEvents\n" +
            "| where name == \"TestRan\"\n" +
            "| where timestamp > ago(24h)\n" +
            "| where customDimensions.TestName == \"{0}\"\n" +
            "| project Time = timestamp, PassRate = 100 * todouble(customDimensions.Success), Latency = todouble(customMeasurements.Latency), TraceId = customDimensions.TraceId, DC = tostring(customDimensions.DC)\n" +
            "| summarize percentiles(Latency, 25, 50, 75) by bin(Time, 10m)\n" +
            "| render timechart\n";

        private static string Query_SingleTestRecentFailures = "customEvents\n" +
            "| where name == \"TestRan\"\n" +
            "| where timestamp > ago(24h)\n" +
            "| where customDimensions.TestName == \"{0}\"\n" +
            "| project Time = timestamp, SuiteName = customDimensions.SuiteName, TestName = customDimensions.TestName, Passed = customDimensions.Success == \"1\", Latency = todouble(customMeasurements.Latency), TraceId = customDimensions.TraceId, ErrorMessage = customDimensions.ErrorMessage, DC = tostring(customDimensions.DC)\n" +
            "| where Passed == false\n" +
            "| order by Time desc";
        
        //private static string Query_ASPRequestSuccessRate = "requests\n" +
        //    "| where url == \"{0}\" and operation_SyntheticSource == \"Application Insights Availability Monitoring\"\n" +
        //    "| summarize count(resultCode) by resultCode, bin(timestamp, 5m)\n" +
        //    "| render timechart\n";

        //private static string Query_ASPRequestLatency = "requests\n" +
        //    "| where url == \"{0}\" and operation_SyntheticSource == \"Application Insights Availability Monitoring\"\n" +
        //    "| summarize count(resultCode) by resultCode, bin(timestamp, 5m)\n" +
        //    "| render timechart\n";

        private static string Query_GenericWebSuccessRate = "requests\n" +
            "| where url == \"{0}\"\n" +
            "| summarize count(resultCode) by resultCode, bin(timestamp, 10m)\n" +
            "| render timechart\n";

        private static string Query_GenericWebLatency = "requests\n" +
            "| where url == \"{0}\"\n" +
            "| summarize percentiles(duration, 25, 50, 75) by bin(timestamp, 10m)\n" +
            "| render timechart\n";

        private static string Query_BotletLogs = "customEvents\n" +
            "| where (customDimensions._s_bot_id == \"{0}\")\n" +
            "| project Time = timestamp, Name = name, Bot = customDimensions._s_bot_id, RequestUuid = customDimensions._request_uuid,\n" +
            "TraceId = customDimensions._s_trace_id, SessionId = customDimensions._s_session_id, Channel = customDimensions._s_channel,\n" +
            "Message = customDimensions.text, Latency = todouble(customMeasurements.duration), Data = customDimensions\n" +
            "| order by Time desc";

        private static string Query_SuiteLatency = "customEvents\n" +
            "| where name == \"TestRan\"\n" +
            "| where timestamp > ago(6h)\n" +
            "| where customDimensions.SuiteName == \"{0}\"\n" +
            "| project TestName = tostring(customDimensions.TestName), Time = timestamp, PassRate = 100 * todouble(customDimensions.Success), Latency = todouble(customMeasurements.Latency), DC = tostring(customDimensions.DC)\n" +
            "| summarize percentiles(Latency, 50) by TestName, bin(Time, 10m)\n" +
            "| render timechart";

        private static string Query_SuitePassRate = "customEvents\n" +
            "| where name == \"TestRan\"\n" +
            "| where timestamp > ago(6h)\n" +
            "| where customDimensions.SuiteName == \"{0}\"\n" +
            "| project TestName = tostring(customDimensions.TestName), Time = timestamp, PassRate = 100 * todouble(customDimensions.Success), Latency = todouble(customMeasurements.Latency), DC = tostring(customDimensions.DC)\n" +
            "| summarize avg(PassRate) by TestName, bin(Time, 10m)\n" +
            "| render timechart";

        /// <summary>
        /// Retrieves the static config for a well-known appinsights database
        /// </summary>
        /// <param name="knownDatabase"></param>
        /// <returns></returns>
        public static AppInsightsConnectorConfiguration GetDatabase(AppInsightsDatabase knownDatabase)
        {
            if (_endpointMapping.ContainsKey(knownDatabase))
            {
                return _endpointMapping[knownDatabase];
            }

            return null;
        }

        public static Uri GenerateQueryDeeplink(AppInsightsConnectorConfiguration targetDatabase, string query)
        {
            string apptype = targetDatabase.Type == InsightsAppType.Web ? "web" : "other";
            string endpointPattern = string.Format("https://analytics.applicationinsights.io/subscriptions/{0}/resourcegroups/{1}/components/{2}?q={{0}}&apptype={3}",
                targetDatabase.SubscriptionId.ToString(),
                targetDatabase.ResourceGroup,
                targetDatabase.ResourceName,
                apptype);

            string base64Query = CompressAndEncodeQuery(query);
            string formattedUrl = string.Format(endpointPattern, WebUtility.UrlEncode(base64Query));
            return new Uri(formattedUrl);
        }

        public static List<AppInsightsQuery> BuildDefaultQuerySetForTest(string testName)
        {
            
            List<AppInsightsQuery> returnVal = new List<AppInsightsQuery>();
            returnVal.Add(new AppInsightsQuery()
            {
                TargetDatabase = GetDatabase(AppInsightsDatabase.DoradoMonitoring),
                Label = "Test Pass Rate",
                QueryString = string.Format(Query_SingleTestPassRate, testName)
            });

            returnVal.Add(new AppInsightsQuery()
            {
                TargetDatabase = GetDatabase(AppInsightsDatabase.DoradoMonitoring),
                Label = "Test Latency",
                QueryString = string.Format(Query_SingleTestLatency, testName)
            });

            returnVal.Add(new AppInsightsQuery()
            {
                TargetDatabase = GetDatabase(AppInsightsDatabase.DoradoMonitoring),
                Label = "Recent Test Failures",
                QueryString = string.Format(Query_SingleTestRecentFailures, testName)
            });

            return returnVal;
        }

        public static AppInsightsQuery BuildQueryForWebRequestLatency(AppInsightsDatabase targetDatabase, string targetUrl)
        {
            return new AppInsightsQuery()
            {
                TargetDatabase = GetDatabase(targetDatabase),
                Label = "Server-side Latency",
                QueryString = string.Format(Query_GenericWebLatency, targetUrl)
            };
        }

        public static AppInsightsQuery BuildQueryForWebRequestFailures(AppInsightsDatabase targetDatabase, string targetUrl)
        {
            return new AppInsightsQuery()
            {
                TargetDatabase = GetDatabase(targetDatabase),
                Label = "Server-side Failures",
                QueryString = string.Format(Query_GenericWebSuccessRate, targetUrl)
            };
        }

        public static AppInsightsQuery BuildQueryForBotletLogs(string botletName)
        {
            return new AppInsightsQuery()
            {
                TargetDatabase = GetDatabase(AppInsightsDatabase.BotMaster),
                Label = "Bot Logs",
                QueryString = string.Format(Query_BotletLogs, botletName)
            };
        }

        public static AppInsightsQuery BuildQueryForTestSuitePassRate(string suiteName)
        {
            return new AppInsightsQuery()
            {
                TargetDatabase = GetDatabase(AppInsightsDatabase.DoradoMonitoring),
                Label = "Pass rate",
                QueryString = string.Format(Query_SuitePassRate, suiteName)
            };
        }

        public static AppInsightsQuery BuildQueryForTestSuiteLatency(string suiteName)
        {
            return new AppInsightsQuery()
            {
                TargetDatabase = GetDatabase(AppInsightsDatabase.DoradoMonitoring),
                Label = "Latencies",
                QueryString = string.Format(Query_SuiteLatency, suiteName)
            };
        }

        private static string CompressAndEncodeQuery(string query)
        {
            byte[] queryBytes = Encoding.UTF8.GetBytes(query);
            using (MemoryStream output = new MemoryStream())
            {
                using (GZipStream compressor = new GZipStream(output, CompressionMode.Compress))
                {
                    using (MemoryStream input = new MemoryStream(queryBytes, false))
                    {
                        input.CopyTo(compressor);
                        input.Close();
                        input.Dispose();
                        compressor.Flush();
                        compressor.Close();
                        compressor.Dispose();

                        byte[] data = output.ToArray();
                        return Convert.ToBase64String(data);
                    }
                }
            }
        }
    }
}
