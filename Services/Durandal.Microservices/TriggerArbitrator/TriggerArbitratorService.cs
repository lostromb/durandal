
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
using Durandal.Common.Speech.Triggers;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Security;

namespace DurandalServices.TriggerArbitrator
{
    public class TriggerArbitratorService : BasicService
    {
        private IHttpServer _baseServer;
        private TriggerArbitratorServer _server;

        public TriggerArbitratorService(
            ILogger logger,
            IFileSystem configManager,
            WeakPointer<IThreadPool>threadPool,
            IRealTimeProvider realTime,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet dimensions)
            : base("TriggerArbitrator", logger, configManager, threadPool, metrics, dimensions)
        {
            int arbitrationTimeMs = this.ServiceConfig.GetInt32("arbitrationTimeMs", 4000);
            int numPartitions = this.ServiceConfig.GetInt32("numPartitions", 1);
            int servicePort = this.ServiceConfig.GetInt32("servicePort", 62290);

            _baseServer = new SocketHttpServer(
                new RawTcpSocketServer(
                    new ServerBindingInfo[] { ServerBindingInfo.WildcardHost(servicePort) },
                    logger,
                    realTime,
                    metrics,
                    dimensions,
                    threadPool),
                logger,
                new CryptographicRandom(),
                metrics,
                dimensions);
            _server = new TriggerArbitratorServer(_baseServer, ServiceLogger, TimeSpan.FromMilliseconds(arbitrationTimeMs), DefaultRealTimeProvider.Singleton, numPartitions);
        }

        public override bool IsRunning()
        {
            return _baseServer.Running;
        }

        public override async Task Start(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ServiceLogger.Log("Starting service...");
            await _baseServer.StartServer("TriggerArbitrator", cancelToken, realTime);
            ServiceLogger.Log("Started.");
        }

        public override Task Stop(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ServiceLogger.Log("Stopping service...");
            return _baseServer.StopServer(cancelToken, realTime);
        }
    }
}
