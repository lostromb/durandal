using Durandal.Extensions.BondProtocol;
using Durandal.Extensions.MySql;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using DurandalServices.Instrumentation;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Security;
using Durandal.Common.Time;
using Durandal.Common.Cache;
using Durandal.API;
using Durandal.Common.ServiceMgmt;

namespace DurandalServices.Wayfinder
{
    public class WayfinderService : BasicService
    {
        private WayfinderHttpServer _server;
        private ILogger _serviceLogger;

        public WayfinderService(ILogger serviceLogger, IFileSystem configManager, WeakPointer<IThreadPool> threadPool, WeakPointer<IMetricCollector> metrics, DimensionSet dimensions)
            : base("Wayfinder", serviceLogger, configManager, threadPool, metrics, dimensions)
        {
#if !DEBUG
            _serviceLogger = ServiceLogger.Clone(allowedLogLevels: LogLevel.Std | LogLevel.Err | LogLevel.Wrn);
#else
            _serviceLogger = ServiceLogger;
#endif

            if (!ServiceConfig.ContainsKey("connectionString") || string.IsNullOrEmpty(ServiceConfig.GetString("connectionString")))
            {
                _serviceLogger.Log("No connection string is specified - service will not run", LogLevel.Err);
            }
        }

        public override bool IsRunning()
        {
            return _server != null && _server.Running;
        }

        public override async Task Start(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (!ServiceConfig.ContainsKey("connectionString") || string.IsNullOrEmpty(ServiceConfig.GetString("connectionString")))
            {
                return;
            }

            MySqlConnectionPool connPool = InstrumentationConnectionPool.GetSharedPool(
                    ServiceConfig.GetString("connectionString"),
                    _serviceLogger.Clone("MySqlConnectionPool"),
                    Metrics.Value,
                    MetricDimensions,
                    ServiceConfig.GetBool("useNativePool", true));

            int servicePort = this.ServiceConfig.GetInt32("servicePort", 62296);
            string decryptionKey = this.ServiceConfig.GetString("piiDecryptionKey");
            List<PrivateKey> piiDecryptionKeys = null;
            ICache<CachedWebData> webDataCache = new InMemoryCache<CachedWebData>();
            if (!string.IsNullOrEmpty(decryptionKey))
            {
                piiDecryptionKeys = new List<PrivateKey>() { PrivateKey.ReadFromXml(decryptionKey) };
            }

            _server = new WayfinderHttpServer(
                servicePort,
                _serviceLogger.Clone("WayfinderHttp"),
                new WeakPointer<MySqlConnectionPool>(connPool),
                DefaultRealTimeProvider.Singleton,
                Metrics,
                MetricDimensions,
                webDataCache,
                ThreadPool,
                piiDecryptionKeys);
            await _server.StartServer("WayfinderHttp", cancelToken, realTime);

            _serviceLogger.Log("Started.");
        }

        public override async Task Stop(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _serviceLogger.Log("Stopping service...");
            if (_server != null && _server.Running)
            {
                await _server.StopServer(cancelToken, realTime);
            }
        }
    }
}
