namespace DurandalServices.Instrumentation.Merger
{
    using Durandal.Common.Collections;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Extensions.Azure.AppInsights;
    using Durandal.Extensions.MySql;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class InstrumentationMergeService : BasicService
    {
        // Common stuff
        private readonly IThreadPool _sqlThreadPool;
        private readonly IStringDecrypterPii _piiDecrypter;
        private MySqlLogEventSource _logEventSource;
        private MySqlConnectionPool _connectionPool;
        private MySqlInstrumentation _instrumentationAdapter;
        private ILogger _serviceLogger;
        private bool _appInsightsUploadEnabled = true;

        // AppInsights upload
        private AppInsightsCustomEventUploader _appInsights;

        // Periodic instrumentation updater
        private CancellationTokenSource _cancelToken;
        private Task _backgroundThread;
        
        public InstrumentationMergeService(ILogger serviceLogger, IFileSystem configManager, WeakPointer<IThreadPool> threadPool, WeakPointer<IMetricCollector> metrics, DimensionSet dimensions)
            : base("InstrumentationMerge", serviceLogger, configManager, threadPool, metrics, dimensions)
        {
#if !DEBUG
            _serviceLogger = ServiceLogger.Clone(allowedLogLevels: LogLevel.Std | LogLevel.Err | LogLevel.Wrn);
#else
            _serviceLogger = ServiceLogger;
#endif

            if (!ServiceConfig.ContainsKey("mysqlConnectionString") || string.IsNullOrEmpty(ServiceConfig.GetString("mysqlConnectionString")))
            {
                _serviceLogger.Log("No mysql connection string is specified - service will not run", LogLevel.Err);
            }
            
            _sqlThreadPool = new FixedCapacityThreadPool(threadPool.Value, NullLogger.Singleton, metrics.Value, dimensions, "SqlLogFetchPool", 8, ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable);
            _piiDecrypter = new NullStringEncrypter();
        }

        public override bool IsRunning()
        {
            return _cancelToken != null && !_cancelToken.IsCancellationRequested;
        }

        public override async Task Start(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (!ServiceConfig.ContainsKey("mysqlConnectionString") || string.IsNullOrEmpty(ServiceConfig.GetString("mysqlConnectionString")))
            {
                _serviceLogger.Log("Configuration value \"mysqlConnectionString\" was not found!", LogLevel.Err);
                return;
            }
            if (!ServiceConfig.ContainsKey("appInsightsConnectionString") || string.IsNullOrEmpty(ServiceConfig.GetString("appInsightsConnectionString")))
            {
                _serviceLogger.Log("Configuration value \"appInsightsConnectionString\" was not found! Disabling app insights upload", LogLevel.Wrn);
                _appInsightsUploadEnabled = false;
            }

            if (_backgroundThread == null)
            {
                _serviceLogger.Log("Starting service...");

                _connectionPool = InstrumentationConnectionPool.GetSharedPool(
                    ServiceConfig.GetString("mysqlConnectionString"),
                    _serviceLogger.Clone("MySqlConnectionPool"),
                    Metrics.Value,
                    MetricDimensions,
                    ServiceConfig.GetBool("useNativePool", true));

                _logEventSource = new MySqlLogEventSource(_connectionPool, _serviceLogger);
                _instrumentationAdapter = new MySqlInstrumentation(_connectionPool, _serviceLogger.Clone("MySqlInstrumentation"), new InstrumentationBlobSerializer());
                    
                await _logEventSource.Initialize();
                await _instrumentationAdapter.Initialize();

                if (_appInsightsUploadEnabled)
                {
                    _appInsights = new AppInsightsCustomEventUploader(
                        _serviceLogger.Clone("AppInsightsInstrumentation"),
                        _instrumentationAdapter,
                        ServiceConfig.GetString("appInsightsConnectionString"),
                        Metrics.Value,
                        MetricDimensions,
                        _piiDecrypter);
                }

                _cancelToken = new CancellationTokenSource();
                _backgroundThread = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(async () => await RunLoop());

                _serviceLogger.Log("Started.");
            }
        }

        public override Task Stop(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _serviceLogger.Log("Stopping service...");
            _cancelToken.Cancel();
            _cancelToken.Dispose();
            return DurandalTaskExtensions.NoOpTask;
        }

        private async Task RunLoop()
        {
            while (!_cancelToken.Token.IsCancellationRequested)
            {
                int logBatchSize = ServiceConfig.GetInt32("logBatchSize", 10000);
                int traceBatchSize = ServiceConfig.GetInt32("traceBatchSize", 100);
                int secondsToWait = ServiceConfig.GetInt32("secondsBetweenBatches", 300);

                await MergeLogsIntoTraces(logBatchSize);

                if (_appInsightsUploadEnabled)
                {
                    await _appInsights.UploadTracesToAppInsights(traceBatchSize);
                }

                await Task.Delay(TimeSpan.FromSeconds(secondsToWait));
            }

            _backgroundThread = null;
        }

        private async Task MergeLogsIntoTraces(int batchSize)
        {
            try
            {
                ISet<Guid> traceIds = await _instrumentationAdapter.GetUnprocessedTraceIds(batchSize);
                foreach (Guid traceId in traceIds)
                {
                    _sqlThreadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        try
                        {
                            // See if there's already any data for this trace
                            UnifiedTrace existingData = await _instrumentationAdapter.GetTraceData(traceId, _piiDecrypter);

                            UnifiedTrace t = await GetUnifiedTrace(_logEventSource, traceId, ServiceLogger, existingData, _piiDecrypter);

                            if (t != null)
                            {
                                _serviceLogger.Log("Processing " + traceId, LogLevel.Vrb);
                                bool success = await _instrumentationAdapter.WriteTraceData(t);
                                if (success)
                                {
                                    // Fire and forget delete
                                    // FIXME this should ideally use a fix capacity thread pool
                                    Task bgTask = Task.Run(async () =>
                                    {
                                        await _instrumentationAdapter.DeleteLogs(traceId);
                                    });

                                    Metrics.Value.ReportInstant("Impression Merge Success / sec", MetricDimensions);
                                    _serviceLogger.Log("Success!", LogLevel.Vrb);
                                }
                                else
                                {
                                    Metrics.Value.ReportInstant("Impression Merge Failure / sec", MetricDimensions);
                                    _serviceLogger.Log("Failed to process instrumentation for traceid " + traceId, LogLevel.Wrn);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _serviceLogger.Log("Unhandled exception while merging logs for trace " + traceId, LogLevel.Err);
                            _serviceLogger.Log(e, LogLevel.Err);
                        }
                    });
                }
            }
            catch (Exception e)
            {
                _serviceLogger.Log("Unhandled exception while ingesting the log table", LogLevel.Err);
                _serviceLogger.Log(e, LogLevel.Err);
            }
        }

        private static async Task<UnifiedTrace> GetUnifiedTrace(ILogEventSource logReader, Guid traceId, ILogger logger, UnifiedTrace existingData, IStringDecrypterPii piiDecrypter)
        {
            FilterCriteria filter = new FilterCriteria()
            {
                Level = LogLevel.All,
                TraceId = traceId
            };

            logger.Log("Getting logs for traceId " + traceId, LogLevel.Vrb);
            
            List<LogEvent> events = new List<LogEvent>();

            // Start with the existing logs if any
            if (existingData != null)
            {
                events.FastAddRangeList(existingData.LogEvents);
            }

            // Union them with new database logs
            IEnumerable<LogEvent> fromDb = await logReader.GetLogEvents(filter);
            events.AddRange(fromDb);

            // Sort logs ascending
            events.Sort();
            
            // And build them into a proper trace again
            UnifiedTrace returnVal = UnifiedTrace.CreateFromLogData(traceId, events, logger, piiDecrypter);

            return returnVal;
        }
    }
}
