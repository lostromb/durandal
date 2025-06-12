using Durandal.Common.Logger;
using DurandalServices.ChannelProxy;
using DurandalServices.LogAggregator;
using DurandalServices.SpeechReco;
using DurandalServices.Instrumentation.Merger;
using DurandalServices.SSLConnector;
using System.Collections.Generic;
using System.Threading;
using Durandal.Common.File;
using Durandal.Common.Config;
using System;
using Durandal.API;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using DurandalServices.TriggerArbitrator;
using DurandalServices.Wayfinder;
using System.Threading.Tasks;
using Durandal.Common.Instrumentation;
using Durandal.Extensions.Azure.AppInsights;
using Durandal.Common.Time;
using System.Net;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils.NativePlatform;

namespace DurandalServices
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AsyncMain(args).Await();
        }

        public static async Task AsyncMain(string[] args)
        {
            string debugString = string.Empty;
#if DEBUG
            debugString = " (DEBUG)";
#endif
            Console.Title = string.Format("Durandal Services {0}{1}", SVNVersionInfo.VersionString, debugString);

            LogoUtil.PrintLogo("Services", Console.Out);
            ServicePointManager.Expect100Continue = false;

            DimensionSet coreDimensions = new DimensionSet(new MetricDimension[]
                {
                    new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceVersion, SVNVersionInfo.AssemblyVersion)
                });

            ILogger mainLogger = new ConsoleLogger("DurandalServices");
            IFileSystem configManager = new RealFileSystem(mainLogger.Clone("ConfigFileManager"));
            MetricCollector metrics = new MetricCollector(mainLogger, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60));
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            metrics.AddMetricSource(new WindowsPerfCounterReporter(
                mainLogger,
                coreDimensions,
                WindowsPerfCounterSet.BasicCurrentProcess |
                WindowsPerfCounterSet.DotNetClrCurrentProcess));

            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());

            // Get the global config
            IConfiguration globalConfig = await IniFileConfiguration.Create(mainLogger, new VirtualPath("serviceconfig.ini"), configManager, realTime, true, true);

            //IThreadPool globalThreadPool = new CustomThreadPool(mainLogger.Clone("ThreadPool"), metrics, coreDimensions, ThreadPriority.Normal, "ThreadPool", 16);
            IThreadPool globalThreadPool = new SystemThreadPool(mainLogger.Clone("ThreadPool"), metrics, coreDimensions);

            if (!string.IsNullOrEmpty(globalConfig.GetString("appInsightsConnectionString")))
            {
                metrics.AddMetricOutput(new AppInsightsMetricOutput(mainLogger.Clone("AppInsightsMetrics"), globalConfig.GetString("appInsightsConnectionString")));
            }

            IList<IService> services = new List<IService>();
            if (globalConfig.ContainsKey("enabledServices"))
            {
                foreach (string serviceName in globalConfig.GetStringList("enabledServices"))
                {
                    if (string.Equals(serviceName, "ChannelProxy", StringComparison.OrdinalIgnoreCase))
                    {
                        services.Add(new ChannelProxyService(mainLogger.Clone("ChannelProxyService"), configManager, new WeakPointer<IThreadPool>(globalThreadPool), new WeakPointer<IMetricCollector>(metrics), coreDimensions));
                    }
                    else if (string.Equals(serviceName, "LogAggregator", StringComparison.OrdinalIgnoreCase))
                    {
                        services.Add(new LogAggregatorService(mainLogger.Clone("LogAggregatorService"), configManager, new WeakPointer<IThreadPool>(globalThreadPool), realTime, new WeakPointer<IMetricCollector>(metrics), coreDimensions));
                    }
                    else if (string.Equals(serviceName, "SpeechReco", StringComparison.OrdinalIgnoreCase))
                    {
                        services.Add(new SpeechRecoService(mainLogger.Clone("SpeechRecoService"), configManager, new WeakPointer<IThreadPool>(globalThreadPool), new WeakPointer<IMetricCollector>(metrics), realTime, coreDimensions));
                    }
                    else if (string.Equals(serviceName, "InstrumentationMerge", StringComparison.OrdinalIgnoreCase))
                    {
                        services.Add(new InstrumentationMergeService(mainLogger.Clone("InstrumentationMergeService"), configManager, new WeakPointer<IThreadPool>(globalThreadPool), new WeakPointer<IMetricCollector>(metrics), coreDimensions));
                    }
                    else if (string.Equals(serviceName, "SSLConnector", StringComparison.OrdinalIgnoreCase))
                    {
                        services.Add(new SSLConnectorService(mainLogger.Clone("SSLConnector"), configManager, new WeakPointer<IThreadPool>(globalThreadPool), new WeakPointer<IMetricCollector>(metrics), coreDimensions));
                    }
                    else if (string.Equals(serviceName, "TriggerArbitrator", StringComparison.OrdinalIgnoreCase))
                    {
                        services.Add(new TriggerArbitratorService(mainLogger.Clone("TriggerArbitrator"), configManager, new WeakPointer<IThreadPool>(globalThreadPool), realTime, new WeakPointer<IMetricCollector>(metrics), coreDimensions));
                    }
                    else if (string.Equals(serviceName, "Wayfinder", StringComparison.OrdinalIgnoreCase))
                    {
                        services.Add(new WayfinderService(mainLogger.Clone("Wayfinder"), configManager, new WeakPointer<IThreadPool>(globalThreadPool), new WeakPointer<IMetricCollector>(metrics), coreDimensions));
                    }
                }
            }

            if (services.Count == 0)
            {
                mainLogger.Log("No services are configured to run. I will now exit.");
                return;
            }

            foreach (IService service in services)
            {
                await service.Start(CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }

            // Go to sleep until all services stop
            bool allRunning = true;
            while (allRunning)
            {
                Thread.Sleep(1000);
                allRunning = true;
                foreach (IService service in services)
                {
                    allRunning = allRunning && service.IsRunning();
                }
            }

            mainLogger.Log("All services have stopped; I am shutting down");
        }
    }
}
