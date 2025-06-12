using Durandal.Common.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Net.Http;
using Durandal.Common.Net;
using Durandal.Common.Logger;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.Config;
using Durandal.Common.File;
using Durandal.Common.Monitoring.Monitors.Http;
using Durandal.Common.Utils;
using Durandal.Common.Instrumentation;
using Durandal.Common.Net.Http2;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Monitoring.Monitors.Http
{
    /// <summary>
    /// Defines an abstract test that sends one HTTP request and validates the response.
    /// This is a broad base class for all HTTP class, and can encompass GET, POST, or other requests.
    /// It can also bypass SSL certification failures for when we are hitting SSL endpoints that are normally
    /// behind traffic managers, so the domain and certificate name do not match.
    /// </summary>
    public abstract class AbstractHttpMonitor : IServiceMonitor
    {
        private readonly string _testName;
        private readonly string _testSuiteName;
        private readonly TimeSpan _queryInterval;
        private readonly TimeSpan _timeout;
        private readonly float? _passRateThreshold;
        private readonly TimeSpan? _latencyThreshold;
        private readonly string _sslHostname;
        private readonly Uri _targetUrl;
        private readonly string _requestMethod;

        private IHttpResponseValidator _validator;
        private IHttpClient _httpClient;
        private EventOnlyLogger _eventLogger;
        private WeakPointer<IMetricCollector> _metrics;
        private DimensionSet _metricDimensions;
        private int _disposed = 0;

        /// <summary>
        /// Creates a new HTTP request monitor.
        /// </summary>
        /// <param name="testName">The name of this test.</param>
        /// <param name="testSuiteName">The suite that this test belongs to</param>
        /// <param name="queryInterval">The recommended interval between test executions</param>
        /// <param name="targetUrl">The URl to make a request to</param>
        /// <param name="timeout">The HTTP timeout</param>
        /// <param name="passRateThreshold">The optional pass rate threshold for the test, from 0.0 to 1.0</param>
        /// <param name="latencyThreshold">The optional latency threshold for the test</param>
        /// <param name="requestMethod">The HTTP request method to use</param>
        /// <param name="sslHostname">The SSL hostname we are connecting to (the hostname to validate the certificate against)</param>
        protected AbstractHttpMonitor(
            string testName,
            string testSuiteName,
            TimeSpan queryInterval,
            string targetUrl,
            TimeSpan timeout,
            float? passRateThreshold = null,
            TimeSpan? latencyThreshold = null,
            string requestMethod = "GET",
            string sslHostname = null)
        {
            if (string.IsNullOrEmpty(testName))
            {
                throw new ArgumentNullException(nameof(testName));
            }
            if (string.IsNullOrEmpty(testSuiteName))
            {
                throw new ArgumentNullException(nameof(testSuiteName));
            }
            if (queryInterval.TotalSeconds < 5)
            {
                throw new ArgumentException("Test interval can be no smaller than 5 seconds");
            }
            if (string.IsNullOrEmpty(targetUrl))
            {
                throw new ArgumentNullException(nameof(targetUrl));
            }
            if (timeout == TimeSpan.Zero)
            {
                throw new ArgumentException("Timeout must be non-zero");
            }

            _requestMethod = requestMethod;
            _testName = testName;
            _testSuiteName = testSuiteName;
            _queryInterval = queryInterval;
            _targetUrl = new Uri(targetUrl);
            _timeout = timeout;
            _passRateThreshold = passRateThreshold;
            _latencyThreshold = latencyThreshold;
            _sslHostname = string.IsNullOrEmpty(sslHostname) ? _targetUrl.Authority : sslHostname;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AbstractHttpMonitor()
        {
            Dispose(false);
        }
#endif

        public Task<bool> Initialize(
            IConfiguration environmentConfig,
            Guid machineLocalGuid,
            IFileSystem localFileSystem,
            IHttpClientFactory httpClientFactory,
            WeakPointer<ISocketFactory> socketFactory,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions)
        {
            _eventLogger = new EventOnlyLogger("HttpGetMonitor");
            _validator = BuildValidators();

            bool isSsl = string.Equals("https", _targetUrl.Scheme, StringComparison.OrdinalIgnoreCase);
            int port = isSsl ? 443 : 80;
            if (!_targetUrl.IsDefaultPort)
            {
                port = _targetUrl.Port;
            }

            TcpConnectionConfiguration connectionConfig = new TcpConnectionConfiguration()
            {
                DnsHostname = _targetUrl.Host,
                Port = port,
                SslHostname = _sslHostname,
                UseTLS = isSsl,
                NoDelay = false
            };

            _metrics = metrics;
            _metricDimensions = metricDimensions;

            // Are we overriding the SSL hostname?
            if (!string.Equals(_targetUrl.Authority, _sslHostname))
            {
                // If so, we need to use a socket-level HTTP client
                _httpClient = new SocketHttpClient(
                    socketFactory,
                    connectionConfig,
                    _eventLogger,
                    _metrics,
                    metricDimensions,
                    Http2SessionManager.Default,
                    new Http2SessionPreferences());
            }
            else
            {
                // Otherwise, just use the framework-provided default.
                _httpClient = httpClientFactory.CreateHttpClient(_targetUrl);
            }

            _httpClient.SetReadTimeout(_timeout);
            return Task.FromResult(true);
        }

        public TimeSpan QueryInterval
        {
            get
            {
                return _queryInterval;
            }
        }

        public string TestName
        {
            get
            {
                return _testName;
            }
        }

        public string TestSuiteName
        {
            get
            {
                return _testSuiteName;
            }
        }

        public virtual string TestDescription
        {
            get
            { 
                return "Makes an HTTP " + _requestMethod + " request to " + _targetUrl + " and validates successful response";
            }
        }

        public float? PassRateThreshold
        {
            get
            {
                return _passRateThreshold;
            }
        }

        public TimeSpan? LatencyThreshold
        {
            get
            {
                return _latencyThreshold;
            }
        }

        public string ExclusivityKey
        {
            get
            {
                return "url:" + _targetUrl.Host;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _httpClient?.Dispose();
            }
        }

        public Task<SingleTestResult> Run(Guid traceId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return MakeRequestToUrl(traceId, cancelToken, realTime);
        }

        private async Task<SingleTestResult> MakeRequestToUrl(Guid traceId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpRequest request = HttpRequest.CreateOutgoing(_targetUrl.AbsolutePath, _requestMethod);
            request.RequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)");
            request.RequestHeaders.Add("X-Ms-User-Agent", "System Center Operations Manager (9538)");
            //request.RequestHeaders.Add("SyntheticTest-Id", TestName + "-" + TestSuiteName + " us-ca-sjc-azr");
            //request.RequestHeaders.Add("SyntheticTest-Location", "us-ca-sjc-azr");
            request.RequestHeaders.Add("SyntheticTest-RunId", traceId.ToString().ToLowerInvariant());

            request.SetContent(BuildRequestPayload(), HttpConstants.MIME_TYPE_OCTET_STREAM);

            IDictionary<string, string> extraHeaders = BuildExtraRequestHeaders();
            if (extraHeaders != null)
            {
                foreach (var header in extraHeaders)
                {
                    request.RequestHeaders.Add(header.Key, header.Value);
                }
            }

            ILogger tracingLogger = _eventLogger.CreateTraceLogger(traceId);

            try
            {
                using (NetworkResponseInstrumented<HttpResponse> networkResponse = await _httpClient.SendInstrumentedRequestAsync(
                    request,
                    cancelToken,
                    realTime,
                    tracingLogger).ConfigureAwait(false))
                {
                    if (networkResponse == null)
                    {
                        return new SingleTestResult()
                        {
                            Success = false,
                            ErrorMessage = "Null HTTP response - Internal error occurred. " + GetAnyErrors(_eventLogger, tracingLogger.TraceId)
                        };
                    }

                    HttpResponse responseMessage = networkResponse.Response;
                    if (responseMessage == null)
                    {
                        // Did it look like a timeout?
                        int thresholdMs = (int)(_timeout.TotalMilliseconds * 0.8);
                        if (networkResponse.EndToEndLatency > thresholdMs)
                        {
                            return new SingleTestResult()
                            {
                                Success = false,
                                ErrorMessage = "HTTP GET request timed out after " + networkResponse.EndToEndLatency + "ms."
                            };
                        }
                        else
                        {
                            return new SingleTestResult()
                            {
                                Success = false,
                                ErrorMessage = string.Format("Null HTTP response - Could not reach the URL {0} after {1} ms. {2}", _targetUrl, networkResponse.EndToEndLatency, GetAnyErrors(_eventLogger, tracingLogger.TraceId))
                            };
                        }
                    }

                    try
                    {
                        if (responseMessage.ResponseCode == 301)
                        {
                            // We need to manually follow redirects because I am dumb
                            return new SingleTestResult()
                            {
                                Success = false,
                                ErrorMessage = "Hit an HTTP 301 redirect that Logan hasn't implemented the handler for yet"
                            };
                        }

                        if (responseMessage.ResponseCode < 200 || responseMessage.ResponseCode > 299)
                        {
                            string stringResponse = await responseMessage.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);

                            if (string.IsNullOrEmpty(stringResponse))
                            {
                                return new SingleTestResult()
                                {
                                    Success = false,
                                    ErrorMessage = string.Format("Non-success status code {0} ({1}) received from service. The response was empty",
                                    responseMessage.ResponseCode,
                                    responseMessage.ResponseMessage)
                                };
                            }
                            else
                            {
                                return new SingleTestResult()
                                {
                                    Success = false,
                                    ErrorMessage = string.Format("Non-success status code {0} ({1}) received from service. The response was this: \"{2}\"",
                                    responseMessage.ResponseCode,
                                    responseMessage.ResponseMessage,
                                    stringResponse)
                                };
                            }
                        }

                        // Run validators and return their error response if one failed
                        ArraySegment<byte> binaryResponse = await responseMessage.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                        SingleTestResult validationResult = await _validator.Validate(responseMessage, binaryResponse).ConfigureAwait(false);
                        if (validationResult != null && !validationResult.Success)
                        {
                            return validationResult;
                        }

                        return new SingleTestResult()
                        {
                            Success = true,
                            OverrideTestExecutionTime = TimeSpanExtensions.TimeSpanFromMillisecondsPrecise(networkResponse.EndToEndLatency)
                        };
                    }
                    finally
                    {
                        await responseMessage.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    }
                }
            }
            //catch (HttpResponseException e)
            //{
            //    string responseCode = "Unknown";
            //    string responseContent = "(Empty response)";
            //    if (e.Response != null)
            //    {
            //        responseCode = e.Response.StatusCode.ToString();
            //        if (e.Response.Content != null)
            //        {
            //            responseContent = await e.Response.Content.ReadAsStringAsync();
            //        }
            //    }
            //    return new SingleTestResult()
            //    {
            //        Success = false,
            //        ErrorMessage = "Remote service responded with HTTP code " + responseCode + ". Response content follows: " + responseContent
            //    };
            //}
            catch (Exception e)
            {
                if (e.Message.Contains("the connected party did not properly respond after a period of time"))
                {
                    return new SingleTestResult()
                    {
                        Success = false,
                        ErrorMessage = "HTTP GET request timed out after " + (int)_timeout.TotalMilliseconds + "ms."
                    };
                }
                else
                {
                    // If it's not a timeout, throw it up so the test framework error handler can log it
                    //System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e).Throw();
                    //return null;
                    throw;
                }
            }
        }

        protected virtual IHttpResponseValidator BuildValidators()
        {
            return new HttpConjunctionValidator(new List<IHttpResponseValidator>());
        }

        protected virtual IDictionary<string, string> BuildExtraRequestHeaders()
        {
            return new Dictionary<string, string>();
        }

        protected virtual byte[] BuildRequestPayload()
        {
            return BinaryHelpers.EMPTY_BYTE_ARRAY;
        }

        private static string GetAnyErrors(EventOnlyLogger logger, Guid? traceId)
        {
            ILoggingHistory history = logger.History;
            if (history == null)
            {
                return string.Empty;
            }

            FilterCriteria criteria = new FilterCriteria()
            {
                Level = LogLevel.Err | LogLevel.Wrn,
                TraceId = traceId
            };

            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder messageBuilder = pooledSb.Builder;
                foreach (LogEvent message in history.FilterByCriteria(criteria))
                {
                    messageBuilder.AppendLine(message.ToShortStringLocalTime());
                }

                return messageBuilder.ToString();
            }
        }
    }
}
