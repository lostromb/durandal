using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Photon.Common.AppInsights;
using Photon.Common.Schemas;
using Photon.Common.AppInsights.REST;
using Photon.Common.Config;

namespace Photon.Common.Monitors
{
    /// <summary>
    /// Monitor base class which regularly queries an App Insight REST endpoint, retrieves metrics, and
    /// then evaluates success / failure based on the values of those metrics.
    /// </summary>
    public abstract class AppInsightsMetricMonitor : IServiceMonitor
    {
        private AppInsightsAnalyticsReader _analyticsSource;
        private string _testName;
        private string _suiteName;

        public AppInsightsMetricMonitor(string testName, string testSuiteName)
        {
            _testName = testName;
            _suiteName = testSuiteName;
        }

        public string ExclusivityKey => "AppInsights:" + AnalyticsConfig.AppId;

        public TimeSpan? LatencyThreshold => null;

        public float? PassRateThreshold => 70f;

        // Appinsights will throttle more than 1 qps so we fix it at 1 minute interval for all monitors here
        public TimeSpan QueryInterval => TimeSpan.FromSeconds(60); 

        public IEnumerable<AppInsightsQuery> RelatedAnalyticsQueries
        {
            get
            {
                AppInsightsConnectorConfiguration analyticsConfig = AnalyticsConfig;
                List<AppInsightsQuery> returnVal = AppInsightsQueryHelper.BuildDefaultQuerySetForTest(_testName);

                returnVal.Add(new AppInsightsQuery()
                {
                    TargetDatabase = AnalyticsConfig,
                    Label = "Run This Query",
                    QueryString = AnalyticsQuery
                });

                return returnVal;
            }
        }
        
        public string TestName => _testName;

        public string TestSuiteName => _suiteName;

        public bool InitializeAsync(IEnvironmentConfiguration configuration, GlobalTestContext context)
        {
            _analyticsSource = new AppInsightsAnalyticsReader(AnalyticsConfig);
            return true;
        }

        public async Task<SingleTestResult> Run(TestContext context)
        {
            string query = AnalyticsQuery;
            try
            {
                Table resultTable = await _analyticsSource.Query(query);
                if (resultTable == null)
                {
                    return new SingleTestResult()
                    {
                        ErrorMessage = "Query resulted in a null table: " + query,
                        Success = false
                    };
                }

                if (resultTable.Columns == null || resultTable.Columns.Count == 0)
                {
                    return new SingleTestResult()
                    {
                        ErrorMessage = "Query resulted in a table with no columns: " + query,
                        Success = false
                    };
                }

                if (resultTable.Rows == null)
                {
                    return new SingleTestResult()
                    {
                        ErrorMessage = "Query resulted in a table with null rows (should never happen): " + query,
                        Success = false
                    };
                }

                return ProcessAnalyticsResults(resultTable);
            }
            catch (AppInsightsException e)
            {
                return new SingleTestResult()
                {
                    ErrorMessage = e.Message,
                    Success = false
                };
            }
        }

        public virtual string TestDescription
        {
            get
            {
                return "Queries app insights metrics app \"" + AnalyticsConfig.ResourceName + "\" with this query:\r\n" + AnalyticsQuery;
            }
        }

        // Methods below this are implemented in subclass
        
        /// <summary>
        /// Gets the configuration object used to connect to this tests' app insights configuration,
        /// including app ID and api key
        /// </summary>
        protected abstract AppInsightsConnectorConfiguration AnalyticsConfig { get; }

        /// <summary>
        /// The raw query that should be executed against the analytics instance
        /// </summary>
        protected abstract string AnalyticsQuery { get; }

        /// <summary>
        /// Evaluates the results of the analytics query and determines success/failure based on test-specific logic
        /// </summary>
        /// <param name="table">The table of insights results</param>
        /// <returns>The overall result of this test run</returns>
        protected abstract SingleTestResult ProcessAnalyticsResults(Table table);
    }
}
