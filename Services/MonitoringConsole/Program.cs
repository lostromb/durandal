using Durandal.Common.Config;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Monitoring;
using Durandal.Common.Monitoring.Monitors;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Security;
using Durandal.Common.Utils;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.MonitorConsole.Monitors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.File;
using Durandal.Common.Test.FVT;
using System.Reflection;
using Durandal.Common.ServiceMgmt;

namespace Durandal.MonitorConsole
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AsyncMain(args).Await();
        }

        public static async Task AsyncMain(string[] args)
        {
            ILogger logger = new ConsoleLogger("MonitorConsole", LogLevel.All);
            IConfiguration environmentConfig = new InMemoryConfiguration(logger.Clone("EnvironmentConfig"));
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            IMetricCollector metrics = new MetricCollector(logger.Clone("Metrics"), TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1), realTime);
            DimensionSet baseDimensions = DimensionSet.Empty;
            Guid machineLocalGuid = StringUtils.HashToGuid(Dns.GetHostName());

            VirtualPath fvtDirectory = new VirtualPath("/fvt");
            IFileSystem realFileSystem = new RealFileSystem(logger.Clone("FileSystem"));
            HybridFileSystem virtualFileSystem = new HybridFileSystem(NullFileSystem.Singleton);
            virtualFileSystem.AddRoute(fvtDirectory, realFileSystem);
            environmentConfig.Set("fvtDirectory", fvtDirectory.FullName);

            IThreadPool testThreadPool = new TaskThreadPool(new WeakPointer<IMetricCollector>(metrics), baseDimensions, "TestPool");
            IThreadPool httpThreadPool = new TaskThreadPool(new WeakPointer<IMetricCollector>(metrics), baseDimensions, "HttpPool");

            ISocketFactory testSocketFactory = new TcpClientSocketFactory();
            IHttpClientFactory functionalTestHttpClientFactory = new PortableHttpClientFactory(new WeakPointer<IMetricCollector>(metrics), baseDimensions);
            IFunctionalTestIdentityStore functionalTestIdentityStore = new BasicFunctionalTestIdentityStore();

            IList<IServiceMonitor> monitors = new List<IServiceMonitor>();
            AssemblyReflectionTestLoader reflectionLoader = new AssemblyReflectionTestLoader();
            await reflectionLoader.Load(monitors, logger);

            FvtFileTestLoader fvtLoader = new FvtFileTestLoader(
                new Uri("https://durandal-ai.net:62292"),
                new DialogJsonTransportProtocol(),
                new ImmutableLogger(logger.Clone("Dialog")), // TODO gotta fix this
                functionalTestIdentityStore);

            await fvtLoader.Load(monitors, logger);

            foreach (IServiceMonitor monitor in monitors)
            {
                // OPT these could run in parallel
                await monitor.Initialize(
                    environmentConfig,
                    machineLocalGuid,
                    virtualFileSystem,
                    functionalTestHttpClientFactory,
                    new WeakPointer<ISocketFactory>(testSocketFactory),
                    new WeakPointer<IMetricCollector>(metrics),
                    baseDimensions);
            }

            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
            InMemoryTestResultStore resultStore = new InMemoryTestResultStore(monitors, TimeSpan.FromMinutes(30));
            MonitorRunner runner = new MonitorRunner(
                monitors,
                resultStore,
                logger.Clone("TestRunner"),
                1,
                metrics,
                baseDimensions);

            using (IMonitorDriver driver = new NonCooperativeMonitorDriver(logger.Clone("MonitorDriver")))
            {
                Task backgroundTask = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(
                    () => runner.RunMonitorLoop(driver, cancelTokenSource.Token, realTime));

                RawTcpSocketServer socketServerBase = new RawTcpSocketServer(
                    new ServerBindingInfo[] { ServerBindingInfo.WildcardHost(40000) },
                    logger.Clone("SocketServer"),
                    realTime,
                    new WeakPointer<IMetricCollector>(metrics),
                    baseDimensions,
                    new WeakPointer<IThreadPool>(httpThreadPool));
                SocketHttpServer httpServerBase = new SocketHttpServer(
                    socketServerBase,
                    logger.Clone("HttpServer"),
                    new CryptographicRandom(),
                    new WeakPointer<IMetricCollector>(metrics),
                    baseDimensions);
                socketServerBase.RegisterSubclass(httpServerBase);
                MonitorHttpServer monitorServer = new MonitorHttpServer(string.Empty, resultStore, logger.Clone("MonitorController"));
                httpServerBase.RegisterSubclass(monitorServer);

                await httpServerBase.StartServer("HttpServer", cancelTokenSource.Token, realTime);

                while (!cancelTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(10000);
                    var allTestStatus = resultStore.GetAllSuitesStatus(TimeSpan.FromMinutes(10), DefaultRealTimeProvider.Singleton).Await();
                    foreach (var suiteResult in allTestStatus.Values)
                    {
                        foreach (var testResult in suiteResult.TestResults.Values)
                        {
                            Console.WriteLine("SUITE " + testResult.TestSuiteName + "\t TEST " + testResult.TestName + "\t PASSRATE " + testResult.PassRate.ToString("F2") + "\t LATENCY " + testResult.MedianLatency.TotalMilliseconds);
                        }
                    }
                }

                await backgroundTask;
            }
        }
    }
}
