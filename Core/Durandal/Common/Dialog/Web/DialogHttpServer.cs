


namespace Durandal.Common.Dialog.Web
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Security;
    using Durandal.Common.Security.OAuth;
    using Durandal.Common.Utils;
    using Durandal.Common.Cache;
    using Durandal.Common.IO;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.ServiceMgmt;


    /// <summary>
    /// This is the server that runs on DIALOG.
    /// It accepts DurandalClientRequests on the /query endpoint, and custom dialog action requests on the /action
    /// endpoint. It also has a browser-facing view on the /cache and /views endpoints.
    /// </summary>
    public class DialogHttpServer : IHttpServerDelegate, IServer, IMetricSource
    {
        // fixme I should really revisit how this client context cache thing works....
        private const int CLIENT_CONTEXT_CACHE_TIME_SECONDS = 24 * 60 * 60;
        private static readonly TimeSpan MAX_HTML_CACHE_WAIT_TIME = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MAX_ACTION_CACHE_WAIT_TIME = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MAX_WEBDATA_CACHE_WAIT_TIME = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MAX_AUDIOSTREAM_CACHE_WAIT_TIME = TimeSpan.FromSeconds(5);

        private readonly FixedCapacityThreadPool _backgroundTaskPool;
        
        private readonly DialogWebService _core;
        private readonly IHttpServer _baseServer;
        private readonly ILogger _logger;
        private readonly WeakPointer<ICache<CachedWebData>> _webDataCache;
        private readonly WeakPointer<ICache<ClientContext>> _contextCache;
        private readonly IFileSystem _fileSystem;
        private readonly IStreamingAudioCache _audioEndpoints;
        private readonly IDictionary<string, IDialogTransportProtocol> _protocols;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly string _hostName;
        private int _disposed = 0;

        public DialogHttpServer(
            DialogWebService core,
            WeakPointer<IThreadPool> workerThreadPool,
            IHttpServer server,
            ILogger logger,
            IFileSystem fileSystem,
            WeakPointer<ICache<CachedWebData>> dataCache,
            WeakPointer<ICache<ClientContext>> contextCache,
            IStreamingAudioCache audioCache,
            IEnumerable<IDialogTransportProtocol> enabledProtocols,
            IMetricCollector metrics,
            DimensionSet dimensions,
            string hostName)
        {
            _core = core;
            _baseServer = server;
            _baseServer.RegisterSubclass(this);
            _webDataCache = dataCache;
            _logger = logger;
            _audioEndpoints = audioCache;
            _contextCache = contextCache;
            _fileSystem = fileSystem;
            _protocols = new Dictionary<string, IDialogTransportProtocol>();
            _metrics = new WeakPointer<IMetricCollector>(metrics ?? NullMetricCollector.Singleton);
            _dimensions = dimensions ?? DimensionSet.Empty;
            _metrics.Value.AddMetricSource(this);
            _hostName = hostName;

            foreach (IDialogTransportProtocol protocol in enabledProtocols)
            {
                _protocols[protocol.ProtocolName.ToLowerInvariant()] = protocol;
            }
            
            _backgroundTaskPool = new FixedCapacityThreadPool(
                workerThreadPool.Value,
                _logger.Clone("DialogHttpBgTasks"),
                _metrics.Value,
                _dimensions,
                "DialogHttpBgTasks",
                8,
                ThreadPoolOverschedulingBehavior.QuadraticThrottle,
                TimeSpan.FromMilliseconds(10));

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~DialogHttpServer()
        {
            Dispose(false);
        }
#endif

        public IEnumerable<ServerBindingInfo> Endpoints
        {
            get
            {
                return _baseServer.Endpoints;
            }
        }

        public bool Running
        {
            get
            {
                return _baseServer.Running;
            }
        }

        public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Store log messages from early on in processing so we can potentially associate them with a trace logger later on.
            List<LogEvent> deferredLogMessages = new List<LogEvent>();
            HttpRequest clientRequest = serverContext.HttpRequest;
            Action<HttpResponse> postResponseAction = null; // used for instrumentation after final response payload has been serialized + sent

            try
            {
                HttpResponse response;
                deferredLogMessages.Add(new LogEvent(
                    _logger.ComponentName,
                    string.Format("{0} is requesting {1} {2} {3}", clientRequest.RemoteHost, clientRequest.RequestMethod, clientRequest.DecodedRequestFile, serverContext.CurrentProtocolVersion.ProtocolString),
                    LogLevel.Std,
                    HighPrecisionTimer.GetCurrentUTCTime(),
                    traceId: null,
                    privacyClassification: DataPrivacyClassification.EndUserIdentifiableInformation));

                if (clientRequest.DecodedRequestFile.Equals("/query", StringComparison.OrdinalIgnoreCase))
                {
                    response = await HandleHttpQuery(clientRequest, serverContext, realTime, deferredLogMessages, cancelToken).ConfigureAwait(false);
                }
                else if (clientRequest.DecodedRequestFile.StartsWith("/action", StringComparison.OrdinalIgnoreCase) && !clientRequest.RequestMethod.Equals("PUT"))
                {
                    Tuple<HttpResponse, Action<HttpResponse>> tuple = await HandleHttpAction(clientRequest, serverContext, realTime, deferredLogMessages, cancelToken).ConfigureAwait(false);
                    response = tuple.Item1;
                    postResponseAction = tuple.Item2;
                }
                else
                {
                    if (clientRequest.DecodedRequestFile.Equals("/cache", StringComparison.OrdinalIgnoreCase) && clientRequest.GetParameters.ContainsKey("page"))
                    {
                        response = await HandleHttpCachePage(clientRequest, realTime, deferredLogMessages).ConfigureAwait(false);
                    }
                    else if (clientRequest.DecodedRequestFile.Equals("/cache", StringComparison.OrdinalIgnoreCase) && clientRequest.GetParameters.ContainsKey("audio"))
                    {
                        response = await HandleHttpCacheAudio(clientRequest, cancelToken, realTime, deferredLogMessages).ConfigureAwait(false);
                    }
                    else if (clientRequest.DecodedRequestFile.Equals("/cache", StringComparison.OrdinalIgnoreCase) && clientRequest.GetParameters.ContainsKey("data"))
                    {
                        response = await HandleHttpCacheData(clientRequest, realTime, deferredLogMessages).ConfigureAwait(false);
                    }
                    else if (clientRequest.DecodedRequestFile.StartsWith("/action") && clientRequest.RequestMethod.Equals("PUT"))
                    {
                        Tuple<HttpResponse, Action<HttpResponse>> tuple = await HandleHttpActionPut(clientRequest, cancelToken, realTime, deferredLogMessages).ConfigureAwait(false);
                        response = tuple.Item1;
                        postResponseAction = tuple.Item2;
                    }
                    else if (clientRequest.DecodedRequestFile.StartsWith("/views", StringComparison.OrdinalIgnoreCase))
                    {
                        // AFTER TURN 1+ - Client's browser loads resources
                        _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Dialog_WebRequestCount, _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_HttpAction, "Views")));
                        response = await SendStaticViewFileVersioned(clientRequest.DecodedRequestFile, _logger, realTime, GetClientIfModifiedSinceHeader(clientRequest)).ConfigureAwait(false);
                    }
                    else if (clientRequest.DecodedRequestFile.StartsWith("/reset", StringComparison.OrdinalIgnoreCase))
                    {
                        response = await HandleHttpReset(clientRequest, deferredLogMessages, cancelToken, realTime).ConfigureAwait(false);
                    }
                    else if (clientRequest.RequestMethod.Equals("GET") && clientRequest.DecodedRequestFile.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
                    {
                        response = HandleHttpMetrics(clientRequest, realTime, deferredLogMessages);
                    }
                    else if (clientRequest.DecodedRequestFile.Equals("/status", StringComparison.OrdinalIgnoreCase))
                    {
                        response = HttpResponse.OKResponse();
                        response.SetContent(_core.GetStatus());
                    }
                    else if (clientRequest.DecodedRequestFile.StartsWith("/js/", StringComparison.OrdinalIgnoreCase))
                    {
                        response = HttpResponse.MethodNotAllowedResponse();
                    }
                    else if (clientRequest.RequestMethod.Equals("GET") && clientRequest.DecodedRequestFile.Equals("/"))
                    {
                        // A client web browser is navigating to the "homepage".
                        // Present the browser view
                        response = await SendStaticViewFileVersioned("/views/common/browser.html", _logger, realTime, GetClientIfModifiedSinceHeader(clientRequest)).ConfigureAwait(false);
                    }
                    else if (clientRequest.RequestMethod.Equals("GET") && clientRequest.DecodedRequestFile.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        response = HandleHttpGetRobots(clientRequest, realTime, deferredLogMessages);
                    }
                    else if (clientRequest.DecodedRequestFile.Equals("/favicon.ico"))
                    {
                        // Present a favicon for browsers
                        response = await SendStaticViewFileVersioned("/views/common/favicon.ico", _logger, realTime, GetClientIfModifiedSinceHeader(clientRequest)).ConfigureAwait(false);
                    }
                    else
                    {
                        response = HttpResponse.NotFoundResponse();
                    }
                }

                if (response != null)
                {
                    try
                    {
                        // Write the primary dialog response
                        await serverContext.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                        if (postResponseAction != null)
                        {
                            postResponseAction(response);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                }
            }
            catch (IOException e)
            {
                _logger.Log("Caught unhandled exception while processing dialog request", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);

                try
                {
                    await serverContext.WritePrimaryResponse(HttpResponse.ServerErrorResponse(e), _logger, cancelToken, realTime).ConfigureAwait(false);
                }
                catch (Exception e2)
                {
                    _logger.Log(e2, LogLevel.Err);
                }
            }
        }

        private async Task<HttpResponse> HandleHttpQuery(
            HttpRequest clientRequest,
            IHttpServerContext httpContext,
            IRealTimeProvider realTime,
            List<LogEvent> deferredLogMessages,
            CancellationToken cancelToken)
        {
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Dialog_WebRequestCount, _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_HttpAction, "Query")));
            // Execute the main query workflow
            string protocolName = "bond";
            if (clientRequest.GetParameters.ContainsKey("format"))
            {
                protocolName = clientRequest.GetParameters["format"].ToLowerInvariant();
            }

            if (_protocols.ContainsKey(protocolName))
            {
                return await HandleQuery(clientRequest, httpContext, _protocols[protocolName], realTime, deferredLogMessages, cancelToken).ConfigureAwait(false);
            }
            else
            {
                LogDeferredMessages(deferredLogMessages, _logger);
                string msg = "Could not parse query request: Unknown or unsupported protocol \"" + protocolName + "\"!";
                _logger.Log(msg, LogLevel.Err);
                return HttpResponse.BadRequestResponse(msg);
            }
        }

        private async Task<HttpResponse> HandleHttpCachePage(
            HttpRequest clientRequest,
            IRealTimeProvider realTime,
            List<LogEvent> deferredLogMessages)
        {
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Dialog_WebRequestCount, _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_HttpAction, "Cache-Page")));
            // AFTER TURN 1+ - Client's web browser talks to local cache server
            // Execute the HTTP server workflow
            string pageKey = clientRequest.GetParameters["page"];
            ILogger queryLogger = _logger;
            if (clientRequest.GetParameters.ContainsKey("trace"))
            {
                queryLogger = _logger.CreateTraceLogger(CommonInstrumentation.TryParseTraceIdGuid(clientRequest.GetParameters["trace"]));
            }

            LogDeferredMessages(deferredLogMessages, queryLogger);
            HttpResponse response = HttpResponse.OKResponse();
            RetrieveResult<CachedWebData> cacheResult = await _webDataCache.Value.TryRetrieve(pageKey, queryLogger, realTime, MAX_HTML_CACHE_WAIT_TIME).ConfigureAwait(false);

            // If page is null, it has expired from the cache or never existed
            if (cacheResult == null || !cacheResult.Success || cacheResult.Result.Data == null || cacheResult.Result.Data.Array == null)
            {
                queryLogger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata, "Cached HTML page with key {0} has expired", pageKey);
                string responseHtml = "<html><body bgcolor=\"black\"><font color=\"white\">The requested page has expired from the server.</font></body></html>";
                response.SetContent(responseHtml, "text/html");
            }
            else
            {
                response.SetContent(cacheResult.Result.Data, cacheResult.Result.MimeType);
                queryLogger.Log(CommonInstrumentation.GenerateInstancedSizeEntry(CommonInstrumentation.Key_Size_Store_HtmlCache, pageKey, cacheResult.Result.Data.Count), LogLevel.Ins);
                queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_Store_HtmlCacheRead, pageKey, cacheResult.LatencyMs), LogLevel.Ins);
            }

            return response;
        }

        private async Task<HttpResponse> HandleHttpCacheAudio(
            HttpRequest clientRequest,
            CancellationToken cancelToken,
            IRealTimeProvider realTime, 
            List<LogEvent> deferredLogMessages)
        {
            // This is accessed when an async process on the client reads the response audio as a stream.
            // Each instance of the endpoint can be invoked only once, since it is backed by a single use buffer.
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Dialog_WebRequestCount, _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_HttpAction, "Cache-Audio")));
            string cacheKey = clientRequest.GetParameters["audio"];
            ILogger queryLogger = _logger;
            if (clientRequest.GetParameters.ContainsKey("trace"))
            {
                queryLogger = _logger.CreateTraceLogger(CommonInstrumentation.TryParseTraceIdGuid(clientRequest.GetParameters["trace"]));
            }

            LogDeferredMessages(deferredLogMessages, queryLogger);
            queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Starting to fetch audio stream with key {0}", cacheKey);
            RetrieveResult<IAudioDataSource> cacheResult = await _audioEndpoints.TryGetAudioReadStream(cacheKey, queryLogger, cancelToken, realTime, MAX_AUDIOSTREAM_CACHE_WAIT_TIME).ConfigureAwait(false);
            if (cacheResult.Success)
            {
                queryLogger.Log("Audio fetch from cache succeeded; opening pipe to client");
                IAudioDataSource audioStream = cacheResult.Result;
                HttpResponse response = HttpResponse.OKResponse();
                response.ResponseHeaders["Cache-Control"] = "private, max-age=30"; // Audio streams are cached for 30 seconds
                string codec = audioStream.Codec;
                string codecParams = audioStream.CodecParams;
                RateMonitoringNonRealTimeStream rateMonitorStream = new RateMonitoringNonRealTimeStream(audioStream.AudioDataReadStream, queryLogger, "DialogReadFromAudioCache");
                response.SetContent(rateMonitorStream, HttpHelpers.GetHeaderValueForAudioFormat(codec, codecParams));
                //response.SetContent(audioStream.AudioDataReadStream, HttpHelpers.GetHeaderValueForAudioFormat(codec, codecParams));
                
                response.ResponseHeaders["X-Audio-Codec"] = codec;
                if (!string.IsNullOrEmpty(codecParams))
                {
                    response.ResponseHeaders["X-Audio-Codec-Params"] = codecParams;
                }

                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_StreamingAudioBeginRead, cacheResult.LatencyMs), LogLevel.Ins);
                return response;
            }
            else
            {
                string errorMessage = "Cached audio stream with key " + cacheKey + " has failed to retrieve";
                queryLogger.Log(errorMessage, LogLevel.Err);
                return HttpResponse.NotFoundResponse();
            }
        }

        private async Task<HttpResponse> HandleHttpCacheData(
            HttpRequest clientRequest,
            IRealTimeProvider realTime,
            List<LogEvent> deferredLogMessages)
        {
            // This means the client's browser is requesting some cached, transient resource
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Dialog_WebRequestCount, _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_HttpAction, "Cache-Data")));
            string key = clientRequest.GetParameters["data"];
            ILogger queryLogger = _logger;
            if (clientRequest.GetParameters.ContainsKey("trace"))
            {
                queryLogger = _logger.CreateTraceLogger(CommonInstrumentation.TryParseTraceIdGuid(clientRequest.GetParameters["trace"]));
            }

            LogDeferredMessages(deferredLogMessages, queryLogger);
            RetrieveResult<CachedWebData> data = await _webDataCache.Value.TryRetrieve(key, queryLogger, realTime, MAX_WEBDATA_CACHE_WAIT_TIME).ConfigureAwait(false);
            // If data is null, it has expired from the cache or never existed
            if (data == null || !data.Success)
            {
                queryLogger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata, "Cached resource with key {0} has expired", key);
                return HttpResponse.NotFoundResponse();
            }
            else
            {
                HttpResponse response = HttpResponse.OKResponse();
                response.SetContent(data.Result.Data, data.Result.MimeType);
                response.ResponseHeaders["Cache-Control"] = "private, max-age=" + data.Result.LifetimeSeconds;
                queryLogger.Log(CommonInstrumentation.GenerateInstancedSizeEntry(CommonInstrumentation.Key_Size_Store_WebCache, key, data.Result.Data.Count), LogLevel.Ins);
                queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_Store_WebCacheRead, key, data.LatencyMs), LogLevel.Ins);
                return response;
            }
        }

        private async Task<PooledBuffer<byte>> HackishConvertStreamToPooledBufferAsync(NonRealTimeStream inputStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            using (RecyclableMemoryStream output = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            using (PooledBuffer<byte> pooledBuf = BufferPool<byte>.Rent())
            {
                int readSize = 1;
                while (readSize > 0)
                {
                    readSize = await inputStream.ReadAsync(pooledBuf.Buffer, 0, pooledBuf.Buffer.Length, cancelToken, realTime).ConfigureAwait(false);
                    if (readSize > 0)
                    {
                        output.Write(pooledBuf.Buffer, 0, readSize);
                    }
                }

                return output.ToPooledBuffer();
            }
        }

        private async Task<Tuple<HttpResponse, Action<HttpResponse>>> HandleHttpAction(
            HttpRequest clientRequest,
            IHttpServerContext httpContext,
            IRealTimeProvider realTime,
            List<LogEvent> deferredLogMessages,
            CancellationToken cancelToken)
        {
            // BEGIN TURN 2+ - Client's browser executes a dialog action
            // The key to access the associated DialogAction is encoded in the URL
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Dialog_WebRequestCount, _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_HttpAction, "Action")));

            string actionKey;
            if (!clientRequest.GetParameters.TryGetValue("key", out actionKey))
            {
                LogDeferredMessages(deferredLogMessages, _logger);
                _logger.Log("Dialog action endpoint hit, but there is no \"key\" parameter on the URL!", LogLevel.Err);
                return new Tuple<HttpResponse, Action<HttpResponse>>(HttpResponse.BadRequestResponse("Missing \"key\" parameter from action URL"), null);
            }

            DialogRequest request;
            IDialogTransportProtocol protocol = null;

            // This is true when a client (usually a web browser) makes a request by doing a direct GET on the action link.
            // In these cases, we must fall back to cached client context in order to process the request
            bool contextFreeRequest = false;
            try
            {
                Stopwatch e2eLatencyTimer = Stopwatch.StartNew();

                // Is there an actual request being made?
                string protocolName = "bond";
                if (clientRequest.GetParameters.ContainsKey("format"))
                {
                    protocolName = clientRequest.GetParameters["format"];
                }
                if (_protocols.ContainsKey(protocolName))
                {
                    protocol = _protocols[protocolName];
                }

                if (clientRequest.RequestMethod.Equals("POST"))
                {
                    if (protocol == null)
                    {
                        LogDeferredMessages(deferredLogMessages, _logger);
                        string msg = "Could not parse client action request: Unknown or unsupported protocol \"" + protocolName + "\"!";
                        _logger.Log(msg, LogLevel.Err);
                        return new Tuple<HttpResponse, Action<HttpResponse>>(HttpResponse.BadRequestResponse(msg), null);
                    }
                    else
                    {
                        PooledBuffer<byte> binaryData = await HackishConvertStreamToPooledBufferAsync(clientRequest.GetIncomingContentStream(), cancelToken, realTime).ConfigureAwait(false);
                        request = protocol.ParseClientRequest(binaryData, _logger);
                    }
                }
                else
                {
                    deferredLogMessages.Add(new LogEvent(
                        _logger.ComponentName,
                        "Dialog action was not triggered using POST method. Assuming that client has no context...",
                        LogLevel.Std,
                        HighPrecisionTimer.GetCurrentUTCTime(),
                        null,
                        DataPrivacyClassification.SystemMetadata));
                    if (clientRequest.GetParameters.ContainsKey("client"))
                    {
                        contextFreeRequest = true;
                        string clientId = clientRequest.GetParameters["client"];
                        RetrieveResult<ClientContext> cachedContext = await _contextCache.Value.TryRetrieve(clientId, _logger, realTime, MAX_ACTION_CACHE_WAIT_TIME).ConfigureAwait(false);
                        if (cachedContext == null || !cachedContext.Success)
                        {
                            LogDeferredMessages(deferredLogMessages, _logger);
                            _logger.Log("There is no cached client context for client " + clientId + "; this request cannot be honored", LogLevel.Err);
                            return new Tuple<HttpResponse, Action<HttpResponse>>(HttpResponse.BadRequestResponse(
                                "The dialog server has no stored context for your client and cannot process this request"),
                                null);
                        }
                        else
                        {
                            request = new DialogRequest()
                            {
                                ClientContext = cachedContext.Result,
                            };
                        }
                    }
                    else
                    {
                        LogDeferredMessages(deferredLogMessages, _logger);
                        _logger.Log("Failed to execute action with key " + actionKey, LogLevel.Err);
                        return new Tuple<HttpResponse, Action<HttpResponse>>(HttpResponse.BadRequestResponse("No \"client\" parameter in URL: cannot determine your client context"), null);
                    }
                }

                // Validate or generate input trace id
                if (string.IsNullOrEmpty(request.TraceId) || request.TraceId.Length > 48)
                {
                    request.TraceId = CommonInstrumentation.FormatTraceId(Guid.NewGuid());
                }

                ILogger queryLogger = _logger.CreateTraceLogger(CommonInstrumentation.TryParseTraceIdGuid(request.TraceId));

                LogDeferredMessages(deferredLogMessages, queryLogger);
                queryLogger.Log("Starting to execute cached action with key " + actionKey);
                PooledBuffer<byte> clientRequestPayload = await HackishConvertStreamToPooledBufferAsync(clientRequest.GetIncomingContentStream(), cancelToken, realTime).ConfigureAwait(false);
                queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Dialog_InputPayload, clientRequestPayload.Length), LogLevel.Ins);
                queryLogger.Log("{ \"DialogEventType\": \"DialogAction\" }", LogLevel.Ins);

                // Pass the parsed request to the client
                DialogWebServiceResponse serverResponse = await _core.ProcessDialogAction(request, actionKey, realTime).ConfigureAwait(false);
                HttpResponse finalHttpResponse;

                if (serverResponse == null || serverResponse.ClientResponse == null)
                {
                    return new Tuple<HttpResponse, Action<HttpResponse>>(HttpResponse.ServerErrorResponse("No dialog response was generated for the dialog action request"), null);
                }

                // Cache streaming audio, if applicable
                if (serverResponse.OutputAudioStream != null)
                {
                    serverResponse.ClientResponse.StreamingAudioUrl = CreateStreamingAudioPipe(serverResponse, httpContext, queryLogger, cancelToken, realTime);
                }

                if (contextFreeRequest)
                {
                    // HACKHACK to get direct browser support
                    // Return a redirection link to the proper new page, dump the dialog result into a payload
                    if (string.IsNullOrEmpty(serverResponse.ClientResponse.ResponseUrl))
                    {
                        queryLogger.Log("A context-free dialog request (from a web browser clicking an action link) triggered a URL redirection, but there is no URL to redirect to! This usually means that the dialog action target was considered invalid for the current multiturn context, or there is no HTML target page to go to", LogLevel.Err);
                        return new Tuple<HttpResponse, Action<HttpResponse>>(HttpResponse.ServerErrorResponse("Dialog action did not return a URL to navigate to"), null);
                    }
                    else
                    {
                        finalHttpResponse =  HttpResponse.RedirectResponse(serverResponse.ClientResponse.ResponseUrl);

                        if (protocol != null)
                        {
                            finalHttpResponse.SetContent(protocol.WriteClientResponse(serverResponse.ClientResponse, _logger), protocol.MimeType);
                            if (!string.IsNullOrEmpty(protocol.ContentEncoding))
                            {
                                finalHttpResponse.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_ENCODING] = protocol.ContentEncoding;
                            }
                        }
                    }
                }
                else
                {
                    finalHttpResponse = HttpResponse.OKResponse();
                    // Serialize the response to match the format of the request
                    finalHttpResponse.SetContent(protocol.WriteClientResponse(serverResponse.ClientResponse, _logger), protocol.MimeType);
                    if (!string.IsNullOrEmpty(protocol.ContentEncoding))
                    {
                        finalHttpResponse.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_ENCODING] = protocol.ContentEncoding;
                    }
                }

                // If we are on HTTP2 then proactively push the cached HTML page that is contained in the response
                if (httpContext.SupportsServerPush && !string.IsNullOrEmpty(serverResponse.ClientResponse.ResponseUrl))
                {
                    httpContext.PushPromise(HttpConstants.HTTP_VERB_GET, serverResponse.ClientResponse.ResponseUrl, null, cancelToken, realTime);
                }

                e2eLatencyTimer.Stop();
                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_E2E, e2eLatencyTimer), LogLevel.Ins);
                return new Tuple<HttpResponse, Action<HttpResponse>>(finalHttpResponse, (HttpResponse finalResponse) =>
                {
                    queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Dialog_OutputPayload, finalResponse.GetOutgoingContentStream().Position), LogLevel.Ins);
                });
            }
            catch (FormatException e)
            {
                _logger.Log(e, LogLevel.Err);
                return new Tuple<HttpResponse, Action<HttpResponse>>(HttpResponse.BadRequestResponse(e), null);
            }
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
                return new Tuple<HttpResponse, Action<HttpResponse>>(HttpResponse.ServerErrorResponse(e), null);
            }
        }

        private async Task<Tuple<HttpResponse, Action<HttpResponse>>> HandleHttpActionPut(
            HttpRequest clientRequest,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            List<LogEvent> deferredLogMessages)
        {
            // BEGIN TURN 2+ - Client's browser executes an SPA dialog action
            // This is the same behavior as GET /action except that instead of exposing
            // the entire response, it operates on a dictionary input -> dictionary output, which is more
            // amenable to AJAX / JSON and doesn't create any unnecessary contracts
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Dialog_WebRequestCount, _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_HttpAction, "SPA-Action")));
            
            string actionKey;
            if (!clientRequest.GetParameters.TryGetValue("key", out actionKey))
            {
                LogDeferredMessages(deferredLogMessages, _logger);
                _logger.Log("Dialog action endpoint hit, but there is no \"key\" parameter on the URL!", LogLevel.Err);
                return new Tuple<HttpResponse, Action<HttpResponse>>(HttpResponse.BadRequestResponse("Missing \"key\" parameter from action URL"), null);
            }

            DialogRequest request;
            try
            {
                Stopwatch e2eLatencyTimer = Stopwatch.StartNew();
                deferredLogMessages.Add(new LogEvent(
                        _logger.ComponentName,
                        "SPA dialog action triggered using cached context",
                        LogLevel.Std,
                        HighPrecisionTimer.GetCurrentUTCTime(),
                        null,
                        DataPrivacyClassification.SystemMetadata));

                if (clientRequest.GetParameters.ContainsKey("client"))
                {
                    string clientId = clientRequest.GetParameters["client"];
                    RetrieveResult<ClientContext> cachedContext = await _contextCache.Value.TryRetrieve(clientId, _logger, realTime, MAX_ACTION_CACHE_WAIT_TIME).ConfigureAwait(false);
                    if (cachedContext == null || !cachedContext.Success)
                    {
                        LogDeferredMessages(deferredLogMessages, _logger);
                        _logger.Log("There is no cached client context for client " + clientId + "; this request cannot be honored", LogLevel.Err);
                        return new Tuple<HttpResponse, Action<HttpResponse>>(
                            HttpResponse.BadRequestResponse("The dialog server has no stored context for your client and cannot process this request"),
                            null);
                    }
                    else
                    {
                        request = new DialogRequest()
                        {
                            ClientContext = cachedContext.Result,
                        };
                    }
                }
                else
                {
                    LogDeferredMessages(deferredLogMessages, _logger);
                    _logger.Log("Failed to execute action with key " + actionKey, LogLevel.Err);
                    return new Tuple<HttpResponse, Action<HttpResponse>>(
                        HttpResponse.BadRequestResponse("No \"client\" parameter in URL: cannot determine your client context"),
                        null);
                }

                // Validate or generate input trace id
                if (string.IsNullOrEmpty(request.TraceId) || request.TraceId.Length > 48)
                {
                    request.TraceId = CommonInstrumentation.FormatTraceId(Guid.NewGuid());
                }

                ILogger queryLogger = _logger.CreateTraceLogger(CommonInstrumentation.TryParseTraceIdGuid(request.TraceId));

                LogDeferredMessages(deferredLogMessages, queryLogger);
                PooledBuffer<byte> reqPayload = await HackishConvertStreamToPooledBufferAsync(clientRequest.GetIncomingContentStream(), cancelToken, realTime).ConfigureAwait(false);
                queryLogger.Log("Starting to execute SPA action with key " + actionKey);
                queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Dialog_InputPayload, reqPayload.Length), LogLevel.Ins);
                queryLogger.Log("{ \"DialogEventType\": \"DialogAction\" }", LogLevel.Ins);

                // Parse a dictionary of payload request data
                if (reqPayload.Length > 0)
                {
                    string contentType;
                    if (clientRequest.RequestHeaders.TryGetValue(HttpConstants.HEADER_KEY_CONTENT_TYPE, out contentType))
                    {
                        if (string.Equals(HttpConstants.MIME_TYPE_JSON, contentType, StringComparison.OrdinalIgnoreCase))
                        {
                            string jsonBody = Encoding.UTF8.GetString(reqPayload.Buffer, 0, reqPayload.Length);
                            request.RequestData = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonBody);
                        }
                        else if (string.Equals(HttpConstants.MIME_TYPE_FORMDATA, contentType, StringComparison.OrdinalIgnoreCase))
                        {
                            HttpFormParameters weakDict = await clientRequest.ReadContentAsFormDataAsync(cancelToken, realTime).ConfigureAwait(false);
                            IDictionary<string, string> strongDict = weakDict.ToSimpleDictionary();
                            request.RequestData = strongDict;
                        }
                    }
                    else
                    {
                        queryLogger.Log("Content-Type header not set on SPA request data! Request data will not be parsed", LogLevel.Err);
                    }
                }

                // Pass the parsed request to the client
                DialogWebServiceResponse serverResponse = await _core.ProcessDialogAction(request, actionKey, realTime).ConfigureAwait(false);
                HttpResponse response;

                if (serverResponse != null)
                {
                    response = HttpResponse.OKResponse();
                    DialogActionSpaResponse jsonResponse = new DialogActionSpaResponse()
                    {
                        Success = true,
                        Message = null,
                        Data = serverResponse.ClientResponse.ResponseData
                    };

                    response.SetContentJson(jsonResponse);
                }
                else
                {
                    // Some error happened
                    response = HttpResponse.ServerErrorResponse();
                    DialogActionSpaResponse jsonResponse = new DialogActionSpaResponse()
                    {
                        Success = false,
                        Message = "No dialog response was generated for the dialog action SPA request"
                    };

                    response.SetContentJson(jsonResponse);
                }

                e2eLatencyTimer.Stop();
                
                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_E2E, e2eLatencyTimer), LogLevel.Ins);
                return new Tuple<HttpResponse, Action<HttpResponse>>(
                    response,
                    (HttpResponse finalResponse) =>
                    {
                        queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Dialog_OutputPayload, finalResponse.GetOutgoingContentStream().Position), LogLevel.Ins);
                    });
            }
            catch (FormatException e)
            {
                _logger.Log(e, LogLevel.Err);
                return new Tuple<HttpResponse, Action<HttpResponse>>(HttpResponse.BadRequestResponse(e), null);
            }
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
                return new Tuple<HttpResponse, Action<HttpResponse>>(HttpResponse.ServerErrorResponse(e), null);
            }
        }

        private async Task<HttpResponse> HandleHttpReset(
            HttpRequest clientRequest,
            List<LogEvent> deferredLogMessages,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            LogDeferredMessages(deferredLogMessages, _logger);

            // The client wishes to start a new conversation, or recover after a client-side error
            IHttpFormParameters formData;

            if (string.Equals(clientRequest.RequestMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                formData = await clientRequest.ReadContentAsFormDataAsync(cancelToken, realTime).ConfigureAwait(false);
            }
            else
            {
                formData = clientRequest.GetParameters;
            }

            if (formData != null && formData.ContainsKey("userid") && formData.ContainsKey("clientid"))
            {
                string userId = formData["userid"];
                string clientId = formData["clientid"];
                _core.ResetClientState(userId, clientId, _logger);
                return HttpResponse.OKResponse();
            }
            else
            {
                return HttpResponse.BadRequestResponse(
                    "You must specify \"userid\" and \"clientid\" in GET/POST url parameters for this call");
            }
        }

        private HttpResponse HandleHttpMetrics(
            HttpRequest clientRequest,
            IRealTimeProvider realTime,
            List<LogEvent> deferredLogMessages)
        {
            LogDeferredMessages(deferredLogMessages, _logger);
            IReadOnlyDictionary<CounterInstance, double?> metrics = _metrics.Value.GetCurrentMetrics();
            HttpResponse response = HttpResponse.OKResponse();
            response.SetContentJson(metrics);
            return response;
        }

        private HttpResponse HandleHttpGetRobots(
            HttpRequest clientRequest,
            IRealTimeProvider realTime,
            List<LogEvent> deferredLogMessages)
        {
            LogDeferredMessages(deferredLogMessages, _logger);
            HttpResponse response = HttpResponse.OKResponse();
            response.SetContent("User-agent: *\r\nDisallow: /\r\n");
            return response;
        }

        private DateTimeOffset? GetClientIfModifiedSinceHeader(HttpRequest clientRequest)
        {
            DateTimeOffset cacheTime;
            if (clientRequest.RequestHeaders.ContainsKey("If-Modified-Since"))
            {
                // Check against the file's modified time
                if (DateTimeOffset.TryParse(clientRequest.RequestHeaders["If-Modified-Since"], out cacheTime))
                {
                    return cacheTime;
                }
            }

            return null;
        }

        private async Task<HttpResponse> SendStaticViewFileVersioned(
            string requestFilePath,
            ILogger queryLogger,
            IRealTimeProvider realTime,
            DateTimeOffset? ifModifiedSince = null)
        {
            string viewFilePath = requestFilePath.Replace('/', '\\').Substring(7); // "/views/".Length == 7
            int pluginIdSeparator = viewFilePath.IndexOf('\\');
            if (pluginIdSeparator < 0)
            {
                return HttpResponse.BadRequestResponse("Invalid static view path " + viewFilePath);
            }

            string pluginId = viewFilePath.Substring(0, pluginIdSeparator);
            string restOfViewPath = viewFilePath.Substring(pluginIdSeparator);

            // We used to just fetch the file from local filesystem here.
            // However, since there is the possibility that the actual plugin package is installed somewhere far away from here
            // (best case, inside the same runtime folder as dialog, but worst case in some strange temp container directory or remote service host)
            // we have to route the view data request through the dialog core

            CachedWebData viewData = await _core.FetchPluginViewData(pluginId, restOfViewPath, ifModifiedSince, queryLogger, realTime).ConfigureAwait(false);
            if (viewData == null)
            {
                return HttpResponse.NotFoundResponse();
            }

            // If lifetime is positive then it means the client cache is still valid
            if (viewData.LifetimeSeconds > 0)
            {
                return HttpResponse.NotModifiedResponse();
            }
            
            HttpResponse response = HttpResponse.OKResponse();
            queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.PublicNonPersonalData, "Sending {0} with content type {1}", requestFilePath, viewData.MimeType);
            response.ResponseHeaders["Cache-Control"] = "public, max-age=300";
            response.SetContent(viewData.Data, viewData.MimeType);
            return response;
        }

        private async Task<HttpResponse> SendSpecificFile(VirtualPath localFile, ILogger logger, DateTimeOffset? ifModifiedSince = null)
        {
            HttpResponse response;
            string responseType = HttpHelpers.ResolveMimeType(localFile.Name);
            if (_fileSystem.Exists(localFile))
            {
                // Does the client say they have it cached?
                FileStat localFileStat = await _fileSystem.StatAsync(localFile).ConfigureAwait(false);
                bool isCachedOnClient = localFileStat != null && ifModifiedSince.HasValue && ifModifiedSince.Value > localFileStat.LastWriteTime;

                if (isCachedOnClient)
                {
                    response = HttpResponse.NotModifiedResponse();
                }
                else
                {
                    response = HttpResponse.OKResponse();
                    logger.Log(string.Format("Sending {0} with content type {1}", localFile, responseType), LogLevel.Vrb);
                    response.ResponseHeaders["Cache-Control"] = "public, max-age=300";
                    // Hand off ownership of the disposable file stream here to the HTTP response object
                    Stream fileStream = await _fileSystem.OpenStreamAsync(localFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false);
                    try
                    {
                        response.SetContent(fileStream, responseType);
                        fileStream = null;
                    }
                    finally
                    {
                        fileStream?.Dispose();
                    }
                }
            }
            else
            {
                logger.Log("Client requested a nonexistent file " + localFile.FullName, LogLevel.Wrn);
                response = HttpResponse.NotFoundResponse();
            }

            return response;
        }

        /// <summary>
        /// Determines if the remote web request came from the local machine, and if so, sets the IsOnLocalMachine context flag
        /// </summary>
        /// <param name="webRequest"></param>
        /// <param name="context"></param>
        /// <param name="queryLogger"></param>
        private static void SetClientIsLocalFlag(HttpRequest webRequest, ClientContext context, ILogger queryLogger)
        {
            // Set the "Client is local" flag based on the host field of the HTTP request
            if (webRequest.RemoteHost.StartsWith("127.0.0.1") || webRequest.RemoteHost.StartsWith("[::1]")) // FIXME account more formulations here
            {
                queryLogger.Log("Setting IsOnLocalMachine flag");
                context.AddCapabilities(ClientCapabilities.IsOnLocalMachine);
            }
            else
            {
                // Forbid this flag from being set by the client
                context.RemoveCapabilities(ClientCapabilities.IsOnLocalMachine);
            }
        }

        private string CreateStreamingAudioPipe(
            DialogWebServiceResponse response,
            IHttpServerContext httpContext,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            string cacheId = Guid.NewGuid().ToString("N");

            // fire and forget
            Stopwatch audioStreamTimer = Stopwatch.StartNew();
            IRealTimeProvider backgroundAudioRealTime = realTime.Fork("BackgroundAudioStreamWrite");
            AudioEncoder outputAudioEncoder = response.OutputAudioStream;
            queryLogger.Log("Starting background task to write streaming audio to cache");
            PipeStream audioPipe = new PipeStream();
            NonRealTimeStream pipeWriteStream = audioPipe.GetWriteStream();
            NonRealTimeStream pipeReadStream = audioPipe.GetReadStream();

            string responseUrl;
            if (queryLogger.TraceId.HasValue)
            {
                responseUrl = string.Format("/cache?audio={0}&trace={1}", cacheId, CommonInstrumentation.FormatTraceId(queryLogger.TraceId));
            }
            else
            {
                responseUrl = string.Format("/cache?audio={0}", cacheId);
            }

            if (httpContext.SupportsServerPush)
            {
                httpContext.PushPromise("GET", responseUrl, null, cancelToken, realTime);
            }

            _backgroundTaskPool.EnqueueUserAsyncWorkItem(async () =>
            {
                try
                {
                    audioStreamTimer.Stop();
                    queryLogger.Log("Audio write task has started");
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_StreamingAudioBeginWrite, audioStreamTimer), LogLevel.Ins);

                    Stopwatch audioCommitTimer = Stopwatch.StartNew();
                    using (PooledBuffer<byte> scratchForEncodedAudio = BufferPool<byte>.Rent())
                    using (NonRealTimeStream audioStorageStream = await _audioEndpoints.CreateAudioWriteStream(
                        cacheId,
                        outputAudioEncoder.Codec,
                        outputAudioEncoder.CodecParams,
                        queryLogger,
                        realTime).ConfigureAwait(false))
                    {
                        RateMonitoringNonRealTimeStream rateMonitorStream = new RateMonitoringNonRealTimeStream(pipeWriteStream, queryLogger, "DialogWriteToAudioCache");
                        AudioInitializationResult encoderInitialize = await outputAudioEncoder.Initialize(
                            rateMonitorStream,
                            true,
                            cancelToken,
                            backgroundAudioRealTime).ConfigureAwait(false);
                        queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_StreamingAudioInitialize, audioCommitTimer), LogLevel.Ins);

                        if (encoderInitialize != AudioInitializationResult.Success)
                        {
                            queryLogger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata,
                                "Audio encoder failed to initialize with code {0}", encoderInitialize.ToString());
                            return;
                        }

                        // we need to copy from cursorForAudioCache to audiostoragestream
                        // use a small buffer size of 100ms to keep the stream moving in real time, but still enough to not have
                        // significant network overhead (if we are transmitting small Opus packets of ~25 bytes each per 20ms of audio)
                        int backgroundAudioProcessingSizeSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(
                            outputAudioEncoder.InputFormat.SampleRateHz, TimeSpan.FromMilliseconds(100));

                        // Kind of a complicated design... because we are pushing and pulling from two ends of a pipe, we don't
                        // want to block on pipe reads because that will deadlock. So we have this interleaved task to do
                        // the read, and we just pull from it whenever it says it is ready (and then do blocking reads after
                        // we have finished writing all the data)
                        Task<int> pipeTask = pipeReadStream.ReadAsync(
                            scratchForEncodedAudio.Buffer,
                            0,
                            scratchForEncodedAudio.Buffer.Length,
                            cancelToken,
                            backgroundAudioRealTime);

                        while (!outputAudioEncoder.Input.PlaybackFinished)
                        {
                            // Copy any amount of audio samples from audio encoder -> pipe (splitter)
                            await outputAudioEncoder.ReadFromSource(
                                backgroundAudioProcessingSizeSamplesPerChannel,
                                cancelToken,
                                backgroundAudioRealTime);

                            // And then copy from splitter -> cache write stream (non-blocking, only if data is available)
                            if (pipeTask.IsFinished())
                            {
                                int encodedBytesProduced = await pipeTask.ConfigureAwait(false);
                                if (encodedBytesProduced > 0)
                                {
                                    await audioStorageStream.WriteAsync(scratchForEncodedAudio.Buffer, 0, encodedBytesProduced).ConfigureAwait(false);
                                }

                                pipeTask = pipeReadStream.ReadAsync(
                                    scratchForEncodedAudio.Buffer,
                                    0,
                                    scratchForEncodedAudio.Buffer.Length,
                                    cancelToken,
                                    backgroundAudioRealTime);
                            }
                        }

                        await outputAudioEncoder.Finish(cancelToken, backgroundAudioRealTime).ConfigureAwait(false);
                        outputAudioEncoder.Dispose();

                        // If there's any extra data appended to the stream here we need to write that too
                        int finalEncodedBytesProduced = await pipeTask.ConfigureAwait(false);
                        while (finalEncodedBytesProduced > 0)
                        {
                            await audioStorageStream.WriteAsync(scratchForEncodedAudio.Buffer, 0, finalEncodedBytesProduced).ConfigureAwait(false);
                            finalEncodedBytesProduced = await pipeReadStream.ReadAsync(
                                scratchForEncodedAudio.Buffer,
                                0,
                                scratchForEncodedAudio.Buffer.Length,
                                cancelToken,
                                backgroundAudioRealTime).ConfigureAwait(false);
                        }

                        audioCommitTimer.Stop();
                        queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_StreamingAudioWrite, audioCommitTimer), LogLevel.Ins);
                        queryLogger.Log("Audio write task has finished");
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log("Unhandled exception while writing streaming audio", LogLevel.Err);
                    queryLogger.Log(e);
                }
                finally
                {
                    outputAudioEncoder?.Dispose();
                    pipeReadStream?.Dispose();
                    backgroundAudioRealTime.Merge();
                }
            });

            queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Started background task to write streaming audio to cache. Response stream URL is {0}", responseUrl);
            return responseUrl;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This method returns an IDisposable so the caller should be responsible for disposal")]
        private async Task<HttpResponse> HandleQuery(
            HttpRequest httpRequest,
            IHttpServerContext httpContext,
            IDialogTransportProtocol protocol,
            IRealTimeProvider realTime,
            List<LogEvent> deferredLogMessages,
            CancellationToken cancelToken)
        {
            Guid traceId;
            DialogRequest parsedRequest = null;
            PooledBuffer<byte> inputData = await HackishConvertStreamToPooledBufferAsync(httpRequest.GetIncomingContentStream(), cancelToken, realTime).ConfigureAwait(false);
            bool contextFreeRequest = false;

            // If it is coming through GET and has a q={} parameter, it is a deeplinked query
            if (httpRequest.RequestMethod.Equals("GET"))
            {
                if (httpRequest.GetParameters.ContainsKey("q") && httpRequest.GetParameters.ContainsKey("client"))
                {
                    string stringQuery = httpRequest.GetParameters["q"];
                    string clientId = httpRequest.GetParameters["client"];
                    deferredLogMessages.Add(new LogEvent(
                        _logger.ComponentName,
                        "This appears to be a deeplinked query: q is \"" + stringQuery + "\", clientId is \"" + clientId + "\"",
                        LogLevel.Std,
                        HighPrecisionTimer.GetCurrentUTCTime(),
                        traceId: null,
                        privacyClassification: DataPrivacyClassification.PrivateContent));
                    RetrieveResult<ClientContext> cachedContext = await _contextCache.Value.TryRetrieve(clientId, _logger, realTime, MAX_ACTION_CACHE_WAIT_TIME).ConfigureAwait(false);
                    if (cachedContext == null || !cachedContext.Success)
                    {
                        LogDeferredMessages(deferredLogMessages, _logger);
                        _logger.Log("There is no cached client context for client " + clientId + "; this request cannot be honored", LogLevel.Err, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                        return HttpResponse.BadRequestResponse("The dialog server has no stored context for your client and cannot process this request");
                    }
                    else
                    {
                        contextFreeRequest = true;
                        parsedRequest = new DialogRequest()
                        {
                            ClientContext = cachedContext.Result,
                            InteractionType = InputMethod.Tactile,
                            TextInput = stringQuery
                        };
                    }
                }
                else
                {
                    LogDeferredMessages(deferredLogMessages, _logger);
                    _logger.Log("Received a query by GET but with no query string parameter", LogLevel.Wrn);
                    return HttpResponse.BadRequestResponse("No query string; expecting &q=query in URL");
                }
            }
            else
            {
                if (inputData.Length == 0)
                {
                    LogDeferredMessages(deferredLogMessages, _logger);
                    _logger.Log("Received a query with no payload data! Nothing to return", LogLevel.Wrn);
                    return HttpResponse.BadRequestResponse("No request data");
                }

                try
                {
                    parsedRequest = protocol.ParseClientRequest(inputData, _logger.Clone("DialogTransportProtocol"));
                }
                catch (Exception e)
                {
                    LogDeferredMessages(deferredLogMessages, _logger);
                    _logger.Log("The input request is null or could not be parsed! Possibly a bad schema or invalid request?", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    return HttpResponse.BadRequestResponse("Could not parse request: " + e.Message);
                }
            }
            
            // Validate trace ID
            if (string.IsNullOrEmpty(parsedRequest.TraceId) || !Guid.TryParse(parsedRequest.TraceId, out traceId))
            {
                traceId = Guid.NewGuid();
                parsedRequest.TraceId = CommonInstrumentation.FormatTraceId(traceId);
            }
            
            ILogger queryLogger = _logger.CreateTraceLogger(traceId);

            LogDeferredMessages(deferredLogMessages, queryLogger);
            queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Dialog_InputPayload, inputData.Length), LogLevel.Ins);
            queryLogger.Log(CommonInstrumentation.GenerateObjectEntry("Dialog.Protocol", protocol.ProtocolName), LogLevel.Ins);
            queryLogger.Log(CommonInstrumentation.GenerateObjectEntry("Dialog.Host", _hostName), LogLevel.Ins);
            Stopwatch e2eLatencyTimer = new Stopwatch();
            e2eLatencyTimer.Start();
            SetClientIsLocalFlag(httpRequest, parsedRequest.ClientContext, queryLogger);
            await _contextCache.Value.Store(parsedRequest.ClientContext.ClientId, parsedRequest.ClientContext, null, TimeSpan.FromSeconds(CLIENT_CONTEXT_CACHE_TIME_SECONDS), true, queryLogger, realTime).ConfigureAwait(false);
            DialogWebServiceResponse dialogResponse = null;

            // Try-catch all around the entire dialog interface here
            try
            {
                dialogResponse = await _core.ProcessRegularQuery(parsedRequest, cancelToken, realTime).ConfigureAwait(false);

                // Create an audio endpoint on this server if we are using streaming audio in the response
                if (dialogResponse.OutputAudioStream != null)
                {
                    dialogResponse.ClientResponse.StreamingAudioUrl = CreateStreamingAudioPipe(dialogResponse, httpContext, queryLogger, cancelToken, realTime);
                }
            }
            catch (Exception e)
            {
                queryLogger.Log("Unhandled exception in dialog core!", LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
                return HttpResponse.ServerErrorResponse(e);
            }

            if (contextFreeRequest)
            {
                // HACKHACK to get direct browser support for /query
                // Return a redirection link to the proper new page, dump the dialog result into a payload
                if (string.IsNullOrEmpty(dialogResponse.ClientResponse.ResponseUrl))
                {
                    queryLogger.Log("A context-free query (from a web browser clicking a /query link) triggered a URL redirection, but there is no URL to redirect to!", LogLevel.Err);
                    return HttpResponse.ServerErrorResponse("Dialog action did not return a URL to navigate to");
                }
                else
                {
                    HttpResponse redirectResp = HttpResponse.RedirectResponse(dialogResponse.ClientResponse.ResponseUrl);

                    if (protocol != null)
                    {
                        redirectResp.SetContent(protocol.WriteClientResponse(dialogResponse.ClientResponse, _logger), protocol.MimeType);
                        if (!string.IsNullOrEmpty(protocol.ContentEncoding))
                        {
                            redirectResp.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_ENCODING] = protocol.ContentEncoding;
                        }
                    }

                    // If we are on HTTP2 then proactively push the page that we are redirecting to
                    if (httpContext.SupportsServerPush)
                    {
                        httpContext.PushPromise(HttpConstants.HTTP_VERB_GET, dialogResponse.ClientResponse.ResponseUrl, null, cancelToken, realTime);
                    }

                    return redirectResp;
                }
            }
            else
            {
                try
                {
                    PooledBuffer<byte> serializedResponse;
                    if (dialogResponse == null || dialogResponse.ClientResponse == null)
                    {
                        serializedResponse = BufferPool<byte>.Rent(0);
                    }
                    else
                    {
                        serializedResponse = protocol.WriteClientResponse(dialogResponse.ClientResponse, queryLogger);

                        // If we are on HTTP2 then proactively push the cached HTML page that is contained in the response
                        if (httpContext.SupportsServerPush && !string.IsNullOrEmpty(dialogResponse.ClientResponse.ResponseUrl))
                        {
                            httpContext.PushPromise(HttpConstants.HTTP_VERB_GET, dialogResponse.ClientResponse.ResponseUrl, null, cancelToken, realTime);
                        }
                    }

                    e2eLatencyTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Dialog_OutputPayload, serializedResponse.Length), LogLevel.Ins);
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_E2E, e2eLatencyTimer), LogLevel.Ins);

                    HttpResponse resp = HttpResponse.OKResponse();
                    resp.SetContent(serializedResponse, protocol.MimeType);
                    if (!string.IsNullOrEmpty(protocol.ContentEncoding))
                    {
                        resp.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_ENCODING] = protocol.ContentEncoding;
                    }

                    return resp;
                }
                catch (NullReferenceException e)
                {
                    _logger.Log("Attempted to serialize a null value! This usually means that one of the strings in the server response was never set", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    return HttpResponse.ServerErrorResponse(e);
                }
                catch (Exception e)
                {
                    _logger.Log("Could not serialize dialog response!", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    return HttpResponse.ServerErrorResponse(e);
                }
            }
        }

        public Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _baseServer.StartServer(serverName, cancelToken, realTime);
        }

        public Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _baseServer.StopServer(cancelToken, realTime);
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
                _baseServer.Dispose();
                _backgroundTaskPool.Dispose();
            }
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
        }

        public void InitializeMetrics(IMetricCollector collector)
        {
        }

        /// <summary>
        /// This is used to "retroactively" log messages that happened before we knew what the trace ID for a certain request was.
        /// In this case, we store the log events that would have been generated, with their original timestamp, and then this method
        /// actually writes them to a trace logger once we have one.
        /// </summary>
        /// <param name="logEvents">The events to write retroactively.</param>
        /// <param name="traceLogger">A tracing logger</param>
        private static void LogDeferredMessages(IEnumerable<LogEvent> logEvents, ILogger traceLogger)
        {
            foreach (LogEvent e in logEvents)
            {
                e.TraceId = traceLogger.TraceId;
                traceLogger.Log(e);
            }
        }
    }
}
