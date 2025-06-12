using Durandal.BondProtocol;
using Durandal.Common.Database;
using Durandal.Common.Database.MySql;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Utils.IO;
using Durandal.Common.Utils.Tasks;
using DurandalServices.Instrumentation.Analytics.Charting;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DurandalServices.Instrumentation.Analytics
{
    public class AnalyticsService : BasicService
    {
        private MySqlInstrumentation _instrumentationAdapter;
        private AnalyticsHttpServer _server;
        private AnalyticsChartGenerator _htmlProducer;

        public AnalyticsService(ILogger serviceLogger, IResourceManager configManager, IThreadPool threadPool) : base("Analytics", serviceLogger, configManager, threadPool)
        {
            if (!ServiceConfig.ContainsKey("connectionString") || string.IsNullOrEmpty(ServiceConfig.GetString("connectionString")))
            {
                ServiceLogger.Log("No connection string is specified - service will not run", LogLevel.Err);
            }

#if !DEBUG
            ServiceLogger.ValidLevels = LogLevel.Std | LogLevel.Err | LogLevel.Wrn;
#endif
        }

        public override bool IsRunning()
        {
            return _server != null && _server.Running;
        }

        public override void Start()
        {
            lock (this)
            {
                if (!ServiceConfig.ContainsKey("connectionString") || string.IsNullOrEmpty(ServiceConfig.GetString("connectionString")))
                {
                    return;
                }

                _instrumentationAdapter = new MySqlInstrumentation(
                    InstrumentationConnectionPool.GetSharedPool(
                        ServiceConfig.GetString("connectionString"),
                        ServiceLogger.Clone("MySqlConnectionPool"),
                        ServiceConfig.GetBool("useNativePool", true)),
                    ServiceLogger.Clone("SqlAnalytics"),
                    new BondByteConverterInstrumentationEventList());

                _htmlProducer = new AnalyticsChartGenerator(_instrumentationAdapter);
                _server = new AnalyticsHttpServer(_htmlProducer, 62296, ServiceLogger.Clone("AnalyticsHttp"), ThreadPool);
                _server.StartServer("AnalyticsHttp");

                ServiceLogger.Log("Started.");
            }
        }

        public override void Stop()
        {
            ServiceLogger.Log("Stopping service...");
            if (_server != null && _server.Running)
            {
                _server.StopServer();
            }
        }
    }
}
