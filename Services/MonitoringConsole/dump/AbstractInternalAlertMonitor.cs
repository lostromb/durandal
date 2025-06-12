using Photon.Common.AppInsights;
using Photon.Common.Monitors;
using Photon.Common.Schemas;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Photon.Common.Config;

namespace Photon.Common.ICM
{
    internal class AbstractInternalAlertMonitor : IServiceMonitor
    {
        public const string INTERNAL_ALERT_SUITE_NAME = "InternalAlerting";

        private readonly string _testSuiteName;
        private Uri _monitorUrl;
        private Uri _escalationUrl;
        private readonly HttpClient _httpClient;

        public AbstractInternalAlertMonitor(string serviceBaseUrl, string targetSuiteName, string teamName)
        {
            if (string.IsNullOrEmpty(targetSuiteName))
            {
                throw new ArgumentNullException("targetSuiteName");
            }

            _testSuiteName = targetSuiteName;
            _monitorUrl = new Uri(serviceBaseUrl + "/api/status/suite/" + targetSuiteName);
            _escalationUrl = new Uri(serviceBaseUrl + "/api/internal_alert/team/" + teamName);
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public bool InitializeAsync(IEnvironmentConfiguration environmentConfig, GlobalTestContext globalContext)
        {
            return true;
        }

        public TimeSpan QueryInterval
        {
            get
            {
                return TimeSpan.FromMinutes(3);
            }
        }

        public string TestName
        {
            get
            {
                return INTERNAL_ALERT_SUITE_NAME + "-" + _testSuiteName;
            }
        }

        public string TestSuiteName
        {
            get
            {
                return INTERNAL_ALERT_SUITE_NAME;
            }
        }

        public virtual string TestDescription
        {
            get
            {
                return "Internal alerting monitor that raises alerts for failures in the " + _testSuiteName + " suite";
            }
        }

        public float? PassRateThreshold
        {
            get
            {
                return 50;
            }
        }

        public TimeSpan? LatencyThreshold
        {
            get
            {
                return TimeSpan.FromSeconds(10);
            }
        }

        public string ExclusivityKey
        {
            get
            {
                return "internal-alerts";
            }
        }

        public virtual IEnumerable<AppInsightsQuery> RelatedAnalyticsQueries
        {
            get
            {
                return new List<AppInsightsQuery>();
            }
        }

        public async Task<SingleTestResult> Run(TestContext context)
        {
            Stopwatch latencyTimer = Stopwatch.StartNew();
            try
            {
                HttpResponseMessage responseMessage = await _httpClient.GetAsync(_monitorUrl);
                if (responseMessage == null)
                {
                    return new SingleTestResult()
                    {
                        Success = false,
                        ErrorMessage = "Null HTTP response - Could not reach the URL " + _monitorUrl
                    };
                }

                if (!responseMessage.IsSuccessStatusCode)
                {
                    // Try and download response content
                    string responseString = null;
                    if (responseMessage.Content != null)
                    {
                        responseString = await responseMessage.Content.ReadAsStringAsync();
                    }

                    if (string.IsNullOrEmpty(responseString))
                    {
                        await NotifyAlertSystem();
                        return new SingleTestResult()
                        {
                            Success = false,
                            ErrorMessage = string.Format("Non-success status code {0} ({1}) received from service. The response was empty",
                            (int)responseMessage.StatusCode,
                            responseMessage.StatusCode.ToString())
                        };
                    }
                    else
                    {
                        await NotifyAlertSystem();
                        return new SingleTestResult()
                        {
                            Success = false,
                            ErrorMessage = string.Format("Non-success status code {0} ({1}) received from service. The response was this: \"{2}\"",
                            (int)responseMessage.StatusCode,
                            responseMessage.StatusCode.ToString(),
                            responseString)
                        };
                    }
                }

                return new SingleTestResult()
                {
                    Success = true
                };
            }
            catch (HttpResponseException e)
            {
                string responseCode = "Unknown";
                string responseContent = "(Empty response)";
                if (e.Response != null)
                {
                    responseCode = e.Response.StatusCode.ToString();
                    if (e.Response.Content != null)
                    {
                        responseContent = await e.Response.Content.ReadAsStringAsync();
                    }
                }

                await NotifyAlertSystem();
                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "Remote service responded with HTTP code " + responseCode + ". Response content follows: " + responseContent
                };
            }
            catch (TaskCanceledException)
            {
                latencyTimer.Stop();
                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "HTTP GET request timed out after " + latencyTimer.ElapsedMilliseconds + "ms."
                };
            }
        }

        private Task NotifyAlertSystem()
        {
            InternalAlertFailureDetails alertData = new InternalAlertFailureDetails()
            {
                TargetSuiteName = _testSuiteName
            };

            HttpContent content = new StringContent(JsonConvert.SerializeObject(alertData), Encoding.UTF8, "application/json");
            return _httpClient.PostAsync(_escalationUrl, content);
        }
    }
}
