using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using System.Net.Http;
using System.Threading;
using Durandal.Common.Time;
using System.IO;
using Durandal.Common.IO;
using Durandal.Common.Instrumentation;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Default implementation of an HTTP client based on the runtime's built-in HttpClient.
    /// </summary>
    public class PortableHttpClient : IHttpClient
    {
        private readonly Uri _targetServer;
        private readonly ILogger _logger;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private TimeSpan _timeout = TimeSpan.FromMilliseconds(10000);

        /// <summary>
        /// TODO This object could use further optimization as described in http://www.nimaara.com/2016/11/01/beware-of-the-net-httpclient/
        /// I believe the ideal design would be to replace this with a pool of singletons so requests to the same target server share one instance
        /// </summary>
        private readonly HttpClient _httpClient;

        private int _disposed = 0;

        public PortableHttpClient(
            Uri targetUri,
            ILogger logger,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            HttpMessageHandler sharedHttpClientHandler = null)
         {
            _logger = logger ?? NullLogger.Singleton;
            int targetPort = targetUri.Port;
            if (targetPort <= 0)
            {
                if (string.Equals(targetUri.Scheme, HttpConstants.SCHEME_HTTPS, StringComparison.OrdinalIgnoreCase))
                {
                    targetPort = HttpConstants.HTTPS_DEFAULT_PORT;
                }
                else
                {
                    targetPort = HttpConstants.HTTP_DEFAULT_PORT;
                }
            }

            _targetServer = new Uri(string.Format("{0}://{1}:{2}", targetUri.Scheme, targetUri.Host, targetPort));
            if (sharedHttpClientHandler == null)
            {
                _httpClient = new HttpClient();
            }
            else
            {
                _httpClient = new HttpClient(sharedHttpClientHandler, disposeHandler: false);
            }

            _httpClient.Timeout = _timeout;
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public PortableHttpClient(
            string targetServer,
            int port,
            bool useTLS,
            ILogger logger,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            HttpMessageHandler sharedHttpClientHandler = null)
        {
            _logger = logger ?? NullLogger.Singleton;
            _targetServer = new Uri(string.Format("{0}://{1}:{2}", useTLS ? HttpConstants.SCHEME_HTTPS : HttpConstants.SCHEME_HTTP, targetServer, port));

            if (sharedHttpClientHandler == null)
            {
                _httpClient = new HttpClient();
            }
            else
            {
                _httpClient = new HttpClient(sharedHttpClientHandler, disposeHandler: false);
            }

            _httpClient.Timeout = _timeout;
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PortableHttpClient()
        {
            Dispose(false);
        }
#endif

        public Uri ServerAddress
        {
            get
            {
                return _targetServer;
            }
        }

        public HttpVersion MaxSupportedProtocolVersion => HttpVersion.HTTP_1_1;

        public HttpVersion InitialProtocolVersion
        {
            get
            {
                return HttpVersion.HTTP_1_1;
            }
            set
            {
                // We don't currently support overriding the version right now. (HttpRequestMessage has a version field which you can set but it'll break if you set it to HTTP2)
            }
        }

        public void SetReadTimeout(TimeSpan timeout)
        {
            if (timeout.Ticks <= 0)
            {
                throw new ArgumentOutOfRangeException("Timeout cannot be zero");
            }

            _timeout = timeout;
            _httpClient.Timeout = _timeout;
        }

        public Task<HttpResponse> SendRequestAsync(HttpRequest request)
        {
            return SendRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, null);
        }

        public Task<NetworkResponseInstrumented<HttpResponse>> SendInstrumentedRequestAsync(HttpRequest request)
        {
            return SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, null);
        }

        public async Task<HttpResponse> SendRequestAsync(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime = null, ILogger queryLogger = null)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            NetworkResponseInstrumented<HttpResponse> resp = await SendInstrumentedRequestAsync(request, cancelToken, realTime, queryLogger).ConfigureAwait(false);
            return resp.UnboxAndDispose();
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        public async Task<NetworkResponseInstrumented<HttpResponse>> SendInstrumentedRequestAsync(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime = null, ILogger queryLogger = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            if (queryLogger == null)
            {
                queryLogger = _logger;
            }
            else
            {
                queryLogger = queryLogger.Clone(_logger.ComponentName);
            }

            Stopwatch timer = Stopwatch.StartNew();

            try
            {
                int requestSize = 13; // FIXME what is this magic number?
                string relativeUrlWithParameters = request.BuildUri();
                queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Outgoing web request: {0} {1}", request.RequestMethod, relativeUrlWithParameters);
                requestSize += relativeUrlWithParameters.Length;

                HttpResponseMessage response;
                using (HttpRequestMessage convertedRequest = new HttpRequestMessage(HttpHelpers.ParseHttpVerb(request.RequestMethod), new Uri(_targetServer, relativeUrlWithParameters)))
                {
                    NonRealTimeStream outgoingStream = request.GetOutgoingContentStream();
                    if (!(outgoingStream is EmptyStream) && (convertedRequest.Method == HttpMethod.Put || convertedRequest.Method == HttpMethod.Post))
                    {
                        //requestSize += request._payloadData.Count;

                        string desiredContentType;
                        if (!request.RequestHeaders.TryGetValue(HttpConstants.HEADER_KEY_CONTENT_TYPE, out desiredContentType))
                        {
                            desiredContentType = null;
                        }

                        if (string.Equals(desiredContentType, HttpConstants.MIME_TYPE_FORMDATA, StringComparison.OrdinalIgnoreCase))
                        {
                            using (RecyclableMemoryStream mm = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                            {
                                // FIXME terribly wasteful...
                                await outgoingStream.CopyToAsync(mm).ConfigureAwait(false);
                                ArraySegment<byte> block = new ArraySegment<byte>(mm.ToArray());
                                convertedRequest.Content = new FormUrlEncodedContent(HttpHelpers.GetFormDataFromPayload(request.RequestHeaders, block).ToSimpleDictionary());
                            }
                        }
                        else
                        {
                            convertedRequest.Content = new StreamContent(outgoingStream);

                            if (!string.IsNullOrEmpty(desiredContentType))
                            {
                                convertedRequest.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(desiredContentType);
                            }
                        }
                    }

                    foreach (var header in request.RequestHeaders)
                    {
                        foreach (string singleHeaderValue in header.Value)
                        {
                            requestSize += header.Key.Length + singleHeaderValue.Length + 4; // +4 for the ": " and "\r\n"
                        }

                        if (!header.Key.Equals(HttpConstants.HEADER_KEY_CONTENT_LENGTH, StringComparison.OrdinalIgnoreCase))
                        {
                            convertedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }

                    // Set the host header automatically
                    if (string.IsNullOrEmpty(convertedRequest.Headers.Host))
                    {
                        convertedRequest.Headers.Host = _targetServer.Host;
                    }

                    // As well as use keep-alive by default
                    string connectionHeader;
                    if (request.RequestHeaders.TryGetValue(HttpConstants.HEADER_KEY_CONNECTION, out connectionHeader))
                    {
                        convertedRequest.Headers.ConnectionClose = string.Equals(connectionHeader, HttpConstants.HEADER_VALUE_CONNECTION_CLOSE);
                    }
                    else
                    {
                        convertedRequest.Headers.ConnectionClose = false;
                        //convertedRequest.Headers.Connection.Add(HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE); // not strictly necessary in http 1.1
                    }

                    response = await _httpClient.SendAsync(convertedRequest, HttpCompletionOption.ResponseHeadersRead, cancelToken).ConfigureAwait(false);

                    // bugbug: since we don't know the version of the outgoing request until it's actually negotiated and thus
                    // completed the request, this instrumentation could get skipped if there is an exception thrown
                    if (response.Version.Major == 1)
                    {
                        if (response.Version.Minor == 1)
                        {
                            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Http_OutgoingRequests11, _metricDimensions);
                        }
                        else
                        {
                            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Http_OutgoingRequests10, _metricDimensions);
                        }
                    }
                    else if (response.Version.Major == 2)
                    {
                        _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Http_OutgoingRequests20, _metricDimensions);
                    }

                    HttpHeaders convertedHeaders = new HttpHeaders(response.Headers.Count());

                    // Copy content headers over
                    if (!response.Headers.TransferEncodingChunked.GetValueOrDefault(false) &&
                        response.Content.Headers.ContentLength.HasValue)
                    {
                        convertedHeaders.Add("Content-Length", response.Content.Headers.ContentLength.Value.ToString());
                    }
                    if (response.Content.Headers.ContentType != null)
                    {
                        convertedHeaders.Add(HttpConstants.HEADER_KEY_CONTENT_TYPE, response.Content.Headers.ContentType.ToString());
                    }

                    // Copy response headers over
                    foreach (var header in response.Headers)
                    {
                        foreach (var subheader in header.Value)
                        {
                            convertedHeaders.Add(header.Key, subheader);
                        }
                    }

                    Stream responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    
                    // Just use approximate metrics as a base since timing things is hard
                    double overallTime = timer.ElapsedMillisecondsPrecise();
                    double sendTime = overallTime / 4;
                    double remoteTime = overallTime / 2;
                    double recvTime = overallTime / 4;

                    // If the server sent an instrumentation header, we can actually get accurate numbers
                    string remoteServerTimeHeader;
                    TimeSpan remoteServerTime;
                    if (convertedHeaders.TryGetValue(HttpConstants.HEADER_KEY_SERVER_WORK_TIME, out remoteServerTimeHeader) &&
                        TimeSpanExtensions.TryParseTimeSpan(remoteServerTimeHeader, out remoteServerTime))
                    {
                        remoteTime = remoteServerTime.TotalMilliseconds;
                        sendTime = (overallTime - remoteTime) / 2;
                        recvTime = sendTime;
                    }

                    long? fixedContentLength = response.Content.Headers.ContentLength;

#pragma warning disable CA2000 // Dispose objects before losing scope
                    HttpResponse convertedResponse = HttpResponse.CreateIncoming(
                        (int)response.StatusCode,
                        response.ReasonPhrase,
                        convertedHeaders,
                        new HttpContentStreamWrapper(responseStream, ownsStream: true),
                        new PortableHttpClientContext(response));
#pragma warning restore CA2000 // Dispose objects before losing scope
                    try
                    {
                        NetworkResponseInstrumented<HttpResponse> returnVal = new NetworkResponseInstrumented<HttpResponse>(
                            convertedResponse,
                            requestSize,
                            (int)fixedContentLength.GetValueOrDefault(0),
                            sendTime,
                            remoteTime,
                            recvTime);
                        convertedResponse = null;
                        return returnVal;
                    }
                    finally
                    {
                        convertedResponse?.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                HttpResponse response = HttpResponse.ClientErrorResponse("Request timed out");
                return new NetworkResponseInstrumented<HttpResponse>(response, 0, 0, 0, 0, _timeout.TotalMilliseconds);
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            //catch (ProtocolViolationException e)
            //{
            //    HttpResponse response = HttpResponse.ClientErrorResponse();
            //    response.PayloadData = Encoding.UTF8.GetBytes("ProtocolViolationException " + e.Message);
            //    return new NetworkResponseInstrumented<HttpResponse>(response);
            //}
            catch (Exception e)
            {
                using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                {
                    StringBuilder message = pooledSb.Builder;
                    message.AppendFormat("Unhandled HTTP client exception: {0}: {1}.\r\n", e.GetType().Name, e.Message);
                    queryLogger.Log("Unhandled HTTP client exception", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                    Exception inner = e.InnerException;
                    int c = 0;
                    while (inner != null && c++ < 4)
                    {
                        message.AppendFormat("Inner exception: {0}: {1}.\r\n", inner.GetType().Name, inner.Message);
                        inner = inner.InnerException;
                    }

#pragma warning disable CA2000 // Dispose objects before losing scope
                    HttpResponse response = HttpResponse.ClientErrorResponse(message.ToString());
                    return new NetworkResponseInstrumented<HttpResponse>(response);
#pragma warning restore CA2000 // Dispose objects before losing scope
                }
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
                _httpClient.Dispose();
            }
        }
    }
}
