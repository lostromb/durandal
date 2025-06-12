namespace Durandal.Common.LU
{
    using Durandal.Common.Config;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.LU;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Packages;
    using Durandal.API;
    using Durandal.Common;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Utils;
    using Durandal.Common.Tasks;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.IO;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;

    public class LUHttpServer : IHttpServerDelegate, IHttpServer, IMetricSource
    {
        /// <summary>
        /// Specifies the amount (in percent from 0 to 100) that the current CPU load metric needs to exceed in
        /// order for monitoring traffic to be ignored as a way of shedding system load.
        /// </summary>
        private const double CPU_OVERLOAD_THRESHOLD = 80.0;

        private readonly LanguageUnderstandingEngine _lu;
        private readonly IFileSystem _packageResourceManager;
        private readonly IPackageLoader _packageLoader;
        private readonly IConfiguration _serverConfig;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly string _hostName;
        private readonly ILogger _logger;
        private readonly IHttpServer _baseServer;
        private readonly IDictionary<string, ILUTransportProtocol> _protocols;
        private readonly ReaderWriterLockAsync _packageLock = new ReaderWriterLockAsync();
        private int _disposed = 0;

        public LUHttpServer(
            LanguageUnderstandingEngine lu,
            IConfiguration serverConfig,
            IHttpServer baseServer,
            ILogger logger,
            IEnumerable<ILUTransportProtocol> enabledProtocols,
            IFileSystem packageResourceManager,
            IPackageLoader packageLoader,
            IMetricCollector metrics,
            DimensionSet dimensions,
            string hostName)
        {
            _lu = lu;
            _serverConfig = serverConfig;
            _packageResourceManager = packageResourceManager;
            _packageLoader = packageLoader;
            _logger = logger;
            _metrics = new WeakPointer<IMetricCollector>(metrics ?? NullMetricCollector.Singleton);
            _dimensions = dimensions ?? DimensionSet.Empty;
            _metrics.Value.AddMetricSource(this);
            _baseServer = baseServer;
            _hostName = hostName;
            _protocols = new Dictionary<string, ILUTransportProtocol>();
            foreach (ILUTransportProtocol protocol in enabledProtocols)
            {
                _protocols[protocol.ProtocolName.ToLowerInvariant()] = protocol;
            }

            _baseServer.RegisterSubclass(this);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~LUHttpServer()
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

        public Uri LocalAccessUri
        {
            get
            {
                return _baseServer.LocalAccessUri;
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

        /// <summary>
        /// Ensure that the changes committer can write its final changes before shutdown
        /// </summary>
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
                _packageLock.Dispose();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This method returns an IDisposable so the caller should be responsible for disposal")]
        public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpRequest request = serverContext.HttpRequest;
            HttpResponse response = null;
            try 
            {
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.EndUserIdentifiableInformation, "{0} is requesting {1}", request.RemoteHost, request.RequestFile);
                if (request.RequestFile.Equals("/query"))
                {
                    _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_LU_WebRequestCount, _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_HttpAction, "Query")));
                    string protocol = "bond";
                    if (request.GetParameters.ContainsKey("format"))
                    {
                        protocol = request.GetParameters["format"].ToLowerInvariant();
                    }

                    if (_protocols.ContainsKey(protocol))
                    {
                        ArraySegment<byte> payload = await request.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                        response = await HandleQueryRequest(payload, _protocols[protocol], realTime).ConfigureAwait(false);
                    }
                    else
                    {
                        response = HttpResponse.BadRequestResponse("The transport protocol \"" + protocol + "\" is unknown or unsupported.");
                    }
                }
                else if (request.RequestFile.Equals("/status"))
                {
                    response = HttpResponse.OKResponse();
                    IDictionary<string, string> responseParams = new Dictionary<string,string>();
                    responseParams["Version"] = SVNVersionInfo.VersionString;
                    responseParams["Initialized"] = _lu.Initialized.ToString();
                    responseParams["LoadedModels"] = string.Join(",", _lu.LoadedModels);
                    responseParams["LastReloadTime"] = _lu.LastModelLoadTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    responseParams["Packages"] = _lu.Packages;
                    LURequest defaultRequest = new LURequest();
                    responseParams["ProtocolVersion"] = defaultRequest.ProtocolVersion.ToString();
                    response.SetContent(responseParams);
                }
                else if (request.RequestFile.Equals("/install"))
                {
                    if (_serverConfig.GetBool("enablePackageUpload", false))
                    {
                        ArraySegment<byte> requestPayload = await request.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                        if (requestPayload.Count == 0)
                        {
                            response = HttpResponse.BadRequestResponse("Empty package payload");
                        }
                        else if (!request.GetParameters.ContainsKey("package"))
                        {
                            response = HttpResponse.BadRequestResponse("You must specify the package=X parameters in the URL");
                        }
                        else
                        {
                            try
                            {
                                string packageName = request.GetParameters["package"];
                                await InstallPackage(packageName, requestPayload, realTime).ConfigureAwait(false);
                                response = HttpResponse.OKResponse();
                            }
                            catch (Exception e)
                            {
                                _logger.Log(e, LogLevel.Err);
                                response = HttpResponse.ServerErrorResponse(e);
                            }
                        }
                    }
                    else
                    {
                        response = HttpResponse.NotAuthorizedResponse();
                        response.SetContent("This server does not accept package uploads");
                    }
                }
                else if (request.RequestMethod.Equals("GET") && request.RequestFile.Equals("/metrics"))
                {
                    IReadOnlyDictionary<CounterInstance, double?> metrics = _metrics.Value.GetCurrentMetrics();
                    response = HttpResponse.OKResponse();
                    response.SetContentJson(metrics);
                }
                else
                {
                    response = HttpResponse.NotFoundResponse();
                }

                if (response != null)
                {
                    try
                    {
                        await serverContext.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Log("Caught unhandled exception while processing LU request", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
                if (response != null)
                {
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
        }

        private async Task InstallPackage(string packageName, ArraySegment<byte> packageData, IRealTimeProvider realTime)
        {
            ISet<LanguageCode> affectedLocales = new HashSet<LanguageCode>();

            int hLock = await _packageLock.EnterWriteLockAsync().ConfigureAwait(false);
            try
            {
                VirtualPath fileName = new VirtualPath(RuntimeDirectoryName.PACKAGE_DIR + "/" + packageName + ".dupkg");
                
                // And overwrite any existing file
                if (_packageResourceManager.Exists(fileName))
                {
                    _packageResourceManager.Delete(fileName);
                }

                using (Stream readStream = new MemoryStream(packageData.Array, packageData.Offset, packageData.Count, false))
                {
                    using (Stream writeStream = _packageResourceManager.OpenStream(fileName, FileOpenMode.Create, FileAccessMode.Write))
                    {
                        readStream.CopyTo(writeStream);
                    }
                }

                // Unpack the package
                PackageFile package = await PackageFile.Load(_packageResourceManager, fileName, _packageLoader, _logger).ConfigureAwait(false);
                PackageManifest manifest = package.GetManifest();

                // Inspect the package to see what locales are affected
                foreach (LUManifestEntry lu in manifest.LuComponents)
                {
                    foreach (string locale in lu.SupportedLocales)
                    {
                        LanguageCode parsedLocale = LanguageCode.Parse(locale);
                        if (!affectedLocales.Contains(parsedLocale))
                        {
                            affectedLocales.Add(parsedLocale);
                        }
                    }
                }

                package.Dispose();
            }
            finally
            {
                _packageLock.ExitWriteLock(hLock);
            }

            // Actually install the packages
            //await PackageInstaller.InstallNewOrUpdatedPackages(_logger, _packageResourceManager, _packageLoader, PackageComponent.LU, realTime);

            // Now queue up the load for the new stuff
            foreach (LanguageCode locale in affectedLocales)
            {
                _lu.LoadModels(locale, null, null);
            }
        }

        /// <summary>
        /// Returns true if the incoming request has the Monitoring query flag and the current metrics report that machine CPU usage is above 80%
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private bool ShouldDeprioritizeMonitoringTraffic(LURequest input)
        {
            if (input.RequestFlags.HasFlag(QueryFlags.Monitoring))
            {
                // Try and access the counter for the current machine CPU load
                double? currentMachineCpu = _metrics.Value.GetCurrentMetric(CommonInstrumentation.Key_Counter_MachineCpuUsage);
                return currentMachineCpu.GetValueOrDefault(0) > CPU_OVERLOAD_THRESHOLD;
            }
            else
            {
                return false;
            }
        }

        private async Task<HttpResponse> HandleQueryRequest(ArraySegment<byte> encodedInput, ILUTransportProtocol protocol, IRealTimeProvider realTime)
        {
            HttpResponse httpResponse;

            if (encodedInput.Count == 0)
            {
                string msg = "Empty input buffer";
                _logger.Log(msg, LogLevel.Err);
                httpResponse = HttpResponse.BadRequestResponse();
                httpResponse.SetContent(msg);
                return httpResponse;
            }

            try
            {
                Stopwatch e2eTimer = Stopwatch.StartNew();
                LURequest input = protocol.ParseLURequest(encodedInput, _logger.Clone("LUTransportProtocol"));

                // Validate the TraceId
                Guid instrumentationTraceId;
                if (string.IsNullOrEmpty(input.TraceId) || !Guid.TryParse(input.TraceId, out instrumentationTraceId))
                {
                    instrumentationTraceId = Guid.NewGuid();
                }

                if (ShouldDeprioritizeMonitoringTraffic(input))
                {
                    _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_QueriesDeprioritized, _dimensions);
                    httpResponse = HttpResponse.TooManyRequestsResponse();
                    return httpResponse;
                }

                ILogger queryLogger;

                // Create the master query logger for this query based on query flags that we receive.
                // First, handle special cases for monitoring
                if (input.RequestFlags.HasFlag(QueryFlags.LogNothing))
                {
                    if (input.RequestFlags.HasFlag(QueryFlags.Trace))
                    {
                        // For the special case of log nothing + tracing, create an eventonly logger that will simply buffer the generated messages internally without emitting them anywhere
                        queryLogger = new EventOnlyLogger(_logger.ComponentName,
                            validLogLevels: LogLevel.All,
                            maxLogLevels: LogLevel.All,
                            maxPrivacyClasses: DataPrivacyClassification.All,
                            defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
                            backgroundLogThreadPool: LoggerBase.DEFAULT_BACKGROUND_LOGGING_THREAD_POOL)
                                .CreateTraceLogger(instrumentationTraceId);
                    }
                    else
                    {
                        queryLogger = NullLogger.Singleton;
                    }
                }
                else
                {
                    queryLogger = _logger.CreateTraceLogger(instrumentationTraceId);
                }

                // Then augment the logger's valid log & PII levels
                if (input.RequestFlags.HasFlag(QueryFlags.Debug))
                {
                    queryLogger = queryLogger.Clone(allowedLogLevels: LogLevel.All);
                    queryLogger.Log("This event is flagged as debug; all verbose logs will be written", LogLevel.Vrb);
                }
                else
                {
                    queryLogger = queryLogger.Clone(allowedLogLevels: LogLevel.Err | LogLevel.Wrn | LogLevel.Std | LogLevel.Ins);
                }

                if (input.RequestFlags.HasFlag(QueryFlags.NoPII))
                {
                    queryLogger = queryLogger.Clone(allowedPrivacyClasses: DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PublicNonPersonalData);
                    queryLogger.Log("Incoming request has NoPII flag set - altering logger to forbid logging any personal identifiers");
                }

                queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_LU_InputPayload, encodedInput.Count), LogLevel.Ins);
                queryLogger.Log(CommonInstrumentation.GenerateObjectEntry("LU.Protocol", protocol.ProtocolName), LogLevel.Ins);
                queryLogger.Log(CommonInstrumentation.GenerateObjectEntry("LU.Host", _hostName), LogLevel.Ins);

                LUResponse response = await _lu.Classify(input, realTime, queryLogger).ConfigureAwait(false);
                if (response == null)
                {
                    e2eTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_LU_OutputPayload, 0), LogLevel.Ins);
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_LU_E2E, e2eTimer), LogLevel.Ins);

                    string msg = "No result came back from LU";
                    _logger.Log(msg, LogLevel.Err);
                    httpResponse = HttpResponse.ServerErrorResponse(msg);
                    return httpResponse;
                }

                httpResponse = HttpResponse.OKResponse();
                httpResponse.ResponseHeaders.Add(HttpConstants.HEADER_KEY_CONTENT_TYPE, protocol.MimeType);
                PooledBuffer<byte> responsePayload = protocol.WriteLUResponse(response, queryLogger.Clone("LUTransportProtocol"));

                e2eTimer.Stop();
                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_LU_E2E, e2eTimer), LogLevel.Ins);
                queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_LU_OutputPayload, responsePayload.Length), LogLevel.Ins);

                httpResponse.SetContent(responsePayload, protocol.MimeType);
                return httpResponse;
            }
            //catch (NullReferenceException e)
            //{
            //    string msg = "Attempted to serialize a null value!" + e.Message;
            //    _logger.Log(msg, LogLevel.Err);
            //    _logger.Log(e.StackTrace, LogLevel.Err);
            //    httpResponse.PayloadData = Encoding.UTF8.GetBytes(msg);
            //}
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
                httpResponse = HttpResponse.ServerErrorResponse(e);
                return httpResponse;
            }
        }

        public void RegisterSubclass(IHttpServerDelegate subclass)
        {
            throw new InvalidOperationException("Cannot subclass the LU server");
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
        }

        public void InitializeMetrics(IMetricCollector collector)
        {
        }
    }
}
