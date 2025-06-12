
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Utils;
using System.Threading;
using Durandal.Common.Instrumentation;
using Durandal.Common.File;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;

namespace DurandalServices.LogAggregator
{
    public class LogAggregatorService : BasicService
    {
        private AggregatorServer _server;

        public LogAggregatorService(ILogger logger, IFileSystem configManager, WeakPointer<IThreadPool> threadPool, IRealTimeProvider realTime, WeakPointer<IMetricCollector> metrics, DimensionSet dimensions)
            : base("LogAggregator", logger, configManager, threadPool, metrics, dimensions)
        {
            string prodConnectionString = string.Empty;
            string devConnectionString = string.Empty;

            if (!ServiceConfig.ContainsKey("connectionString"))
            {
                ServiceLogger.Log("No connection string is specified - service will not run", LogLevel.Err);
            }

            prodConnectionString = ServiceConfig.GetString("connectionString", string.Empty, new SmallDictionary<string, string>() { { "stream", "prod" } });
            devConnectionString = ServiceConfig.GetString("connectionString", string.Empty, new SmallDictionary<string, string>() { { "stream", "dev" } });

            _server = new AggregatorServer(
                ServiceConfig.GetInt32("listeningPort", 62295),
                ServiceLogger,
                prodConnectionString,
                devConnectionString,
                ServiceConfig.GetBool("useNativePool", true),
                realTime,
                Metrics,
                MetricDimensions,
                ThreadPool);
        }

        public override bool IsRunning()
        {
            return _server.Running;
        }

        public override async Task Start(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ServiceLogger.Log("Starting service...");
            await _server.StartServer("LogAggregator", cancelToken, realTime);
            ServiceLogger.Log("Started.");
        }

        public override Task Stop(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ServiceLogger.Log("Stopping service...");
            return _server.StopServer(cancelToken, realTime);
        }
    }
}
