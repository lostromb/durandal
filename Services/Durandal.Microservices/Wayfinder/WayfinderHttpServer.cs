using Durandal.Common.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using System.IO;
using System.Drawing;
using Durandal.Common.Net.Http;
using Durandal.Extensions.MySql;
using Durandal.Common.Instrumentation;
using Durandal.Extensions.BondProtocol;
using System.Net;
using System.Threading;
using Durandal.Common.Security;
using Durandal.Common.Time;
using Durandal.Common.Cache;
using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace DurandalServices.Wayfinder
{
    public class WayfinderHttpServer : IHttpServerDelegate, IServer
    {
        private readonly ILogger _logger;
        private readonly IHttpServer _baseServer;
        private readonly IInstrumentationRepository _instrumentation;
        private readonly ILogEventSource _logEventSource;
        private readonly IStringDecrypterPii _piiDecrypter;
        private readonly ICache<CachedWebData> _webDataCache;
        private int _disposed = 0;

        public WayfinderHttpServer(
            int port,
            ILogger logger,
            WeakPointer<MySqlConnectionPool> connectionPool,
            IRealTimeProvider realTime,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet dimensions,
            ICache<CachedWebData> webDataCache,
            WeakPointer<IThreadPool> requestThreadPool,
            IList<PrivateKey> piiDecryptionKeys = null)
        {
            _logger = logger;
            _webDataCache = webDataCache.AssertNonNull(nameof(webDataCache));
            _baseServer = new SocketHttpServer(
                new RawTcpSocketServer(
                    new ServerBindingInfo[] { new ServerBindingInfo(ServerBindingInfo.WILDCARD_HOSTNAME, port) },
                    logger,
                    realTime,
                    metrics,
                    dimensions,
                    requestThreadPool),
                logger,
                new CryptographicRandom(),
                metrics,
                dimensions);
            _baseServer.RegisterSubclass(this);

            _logEventSource = new MySqlLogEventSource(connectionPool.Value, logger);
            _instrumentation = new MySqlInstrumentation(connectionPool.Value, logger.Clone("MySqlInstrumentation"), new InstrumentationBlobSerializer());
            if (piiDecryptionKeys == null)
            {
                _piiDecrypter = new NullStringEncrypter();
                _logger.Log("PII decryption is disabled");
            }
            else
            {
                _piiDecrypter = new RsaStringDecrypterPii(new StandardRSADelegates(), new SystemAESDelegates(), piiDecryptionKeys, realTime);
                _logger.Log("PII decryption is enabled using private RSA key(s)");
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~WayfinderHttpServer()
        {
            Dispose(false);
        }
#endif

        public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpResponse resp = await HandleConnectionInternal(serverContext.HttpRequest, cancelToken, realTime).ConfigureAwait(false);
            if (resp != null)
            {
                try
                {
                    await serverContext.WritePrimaryResponse(resp, _logger, cancelToken, realTime).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This method returns an IDisposable so the caller should be responsible for disposal")]
        private async Task<HttpResponse> HandleConnectionInternal(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpResponse dynamicResponse = await HandleDynamicPageRequests(request, realTime);
            if (dynamicResponse != null)
            {
                return dynamicResponse;
            }

            return HandleStaticPageRequests(request);
        }

        private async Task<HttpResponse> HandleDynamicPageRequests(HttpRequest request, IRealTimeProvider realTime)
        {
            HttpResponse response = null;

            if (request.RequestFile.Equals("/"))
            {
                string renderedPage = new IndexPage().Render();
                response = HttpResponse.OKResponse();
                response.SetContent(renderedPage, "text/html");
            }
            else if (request.RequestFile.Equals("/trace"))
            {
                if (!request.GetParameters.ContainsKey("traceId"))
                {
                    response = HttpResponse.BadRequestResponse("No traceId provided");
                }
                else
                {
                    Guid traceId = Guid.Parse(request.GetParameters["traceId"]);
                    UnifiedTrace trace = await FetchTrace(traceId);

                    // Create a timeline image of the trace and store it in the cache
                    string timelineImageUrl = null;
                    if ((trace?.Latencies?.Values?.Any((a) => (a?.Values.Any((b) => b.StartTime.HasValue)).GetValueOrDefault(false))).GetValueOrDefault(false))
                    {
                        byte[] timelinePng = TimelineRenderer.RenderTraceTimelineToPng(trace, _logger.Clone("TimelineRenderer"));
                        string imageKey = Guid.NewGuid().ToString("N");
                        await _webDataCache.Store(imageKey, new CachedWebData(timelinePng, "image/png"), null, TimeSpan.FromSeconds(30), true, _logger.Clone("WebDataCache"), realTime);
                        timelineImageUrl = "/cache?id=" + imageKey;
                    }

                    string formattedInstrumentationObject = trace == null ? string.Empty : WebUtility.HtmlEncode(trace.InstrumentationObject.ToString(Newtonsoft.Json.Formatting.Indented));
                    string renderedPage = new TracePage()
                    {
                        TraceId = CommonInstrumentation.FormatTraceId(traceId),
                        Trace = trace,
                        FormattedInstrumentationObject = formattedInstrumentationObject,
                        TimelineImageUrl = timelineImageUrl
                    }.Render();
                    response = HttpResponse.OKResponse();
                    response.SetContent(renderedPage, "text/html");
                }
            }
            else if (request.RequestFile.Equals("/cache"))
            {
                string cacheId;
                if (!request.GetParameters.TryGetValue("id", out cacheId))
                {
                    response = HttpResponse.BadRequestResponse("Missing id");
                }
                else
                {
                    RetrieveResult<CachedWebData> rr = await _webDataCache.TryRetrieve(cacheId, _logger.Clone("WebCache"), realTime, TimeSpan.FromSeconds(5));
                    if (!rr.Success)
                    {
                        response = HttpResponse.NotFoundResponse();
                    }
                    else
                    {
                        response = HttpResponse.OKResponse();
                        response.SetContent(rr.Result.Data, rr.Result.MimeType);
                    }
                }
            }

            return response;
        }

        private async Task<UnifiedTrace> FetchTrace(Guid traceId)
        {
            UnifiedTrace trace = null;

            trace = await GetUnifiedTraceFromTable(_instrumentation, traceId, _logger, _piiDecrypter);
            if (trace == null)
            {
                _logger.Log("No trace came back from instrumentation table, checking instant logs...");

                trace = await GetUnifiedTraceFromLogs(_logEventSource, traceId, _logger, _piiDecrypter);
                if (trace == null)
                {
                    _logger.Log("No traces found anywhere.");
                }
            }

            return trace;
        }

        public static async Task<UnifiedTrace> GetUnifiedTraceFromTable(IInstrumentationRepository instrumentationAdapter, Guid traceId, ILogger programLogger, IStringDecrypterPii piiDecrypter)
        {
            programLogger.Log("Getting instrumentation for traceId " + traceId);
            UnifiedTrace returnVal = await instrumentationAdapter.GetTraceData(traceId, piiDecrypter);
            return returnVal;
        }

        public static async Task<UnifiedTrace> GetUnifiedTraceFromLogs(ILogEventSource logReader, Guid traceId, ILogger programLogger, IStringDecrypterPii piiDecrypter)
        {
            FilterCriteria filter = new FilterCriteria()
            {
                Level = LogLevel.All,
                TraceId = traceId
            };

            programLogger.Log("Getting logs for traceId " + traceId);
            IEnumerable<LogEvent> events = await logReader.GetLogEvents(filter);
            UnifiedTrace returnVal = UnifiedTrace.CreateFromLogData(traceId, events, programLogger, piiDecrypter);

            return returnVal;
        }

        private HttpResponse HandleStaticPageRequests(HttpRequest request)
        {
            HttpResponse response = null;

            // Resolve the URL TODO: Security!!!!
            string resolvedURL = Environment.CurrentDirectory + "\\static" + request.RequestFile.Replace('/', '\\');
            // Determine the file type to use in the header
            string responseType = HttpHelpers.ResolveMimeType(resolvedURL);

            FileInfo targetFile = new FileInfo(resolvedURL);
            if (targetFile.Exists)
            {
                bool isCachedOnClient = false;
                DateTimeOffset cacheTime;

                // Does the client say they have it cached?
                if (request.RequestHeaders.ContainsKey("If-Modified-Since"))
                {
                    // Check against the file's modified time
                    if (DateTimeOffset.TryParse(request.RequestHeaders["If-Modified-Since"], out cacheTime))
                    {
                        isCachedOnClient = cacheTime > targetFile.LastWriteTime;
                    }
                }

                if (isCachedOnClient)
                {
                    response = HttpResponse.NotModifiedResponse();
                }
                else
                {
                    response = HttpResponse.OKResponse();
                    _logger.Log("Sending " + resolvedURL + " with content type " + responseType, LogLevel.Vrb);
                    response.ResponseHeaders["Cache-Control"] = "max-age=300"; // We assume that /view data is pretty much static, so tell the client to cache it more aggressively
                    response.SetContent(File.ReadAllBytes(resolvedURL), responseType);
                }
            }
            else
            {
                _logger.Log("Client requested a nonexistent file " + request.RequestFile, LogLevel.Wrn);
                response = HttpResponse.NotFoundResponse();
            }

            return response;
        }

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
            if (!Durandal.Common.Utils.AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _baseServer.Dispose();
            }
        }
    }
}
