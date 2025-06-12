using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Extensions.Azure.AppInsights;
using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Service
{
    public class MetricCollectorServiceProvider : ServiceResolver<IMetricCollector>
    {
        private readonly WindowsPerfCounterReporter _windowsPerfCounters;
        private readonly GarbageCollectionObserver _gcObserver;
        private readonly SystemThreadPoolObserver _threadPoolObserver;
        private int _disposed;

        public MetricCollectorServiceProvider(
            IRealTimeProvider realTime,
            ILogger bootstrapLogger,
            DimensionSet coreMetricDimensions)
                : base(bootstrapLogger)
        {
            MetricCollector metrics = new MetricCollector(bootstrapLogger, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
#if NETFRAMEWORK
            _windowsPerfCounters = new WindowsPerfCounterReporter(
                bootstrapLogger,
                coreMetricDimensions,
                WindowsPerfCounterSet.BasicLocalMachine |
                WindowsPerfCounterSet.BasicCurrentProcess |
                WindowsPerfCounterSet.DotNetClrCurrentProcess);
#elif NETCOREAPP
            _windowsPerfCounters = new WindowsPerfCounterReporter(
                bootstrapLogger,
                coreMetricDimensions,
                WindowsPerfCounterSet.BasicLocalMachine |
                WindowsPerfCounterSet.BasicCurrentProcess);
            metrics.AddMetricSource(new NetCorePerfCounterReporter(coreMetricDimensions));
#endif
            metrics.AddMetricSource(_windowsPerfCounters);
            _gcObserver = new GarbageCollectionObserver(metrics, coreMetricDimensions);
            _threadPoolObserver = new SystemThreadPoolObserver(metrics, coreMetricDimensions, bootstrapLogger.Clone("ThreadPoolObserver"));
            //metrics.AddMetricOutput(new ConsoleMetricOutput());

            BufferPool<byte>.Metrics = metrics;
            BufferPool<byte>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "byte"));
            BufferPool<char>.Metrics = metrics;
            BufferPool<char>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "char"));
            BufferPool<float>.Metrics = metrics;
            BufferPool<float>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "float"));
            BufferPool<string>.Metrics = metrics;
            BufferPool<string>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "string"));

            SetServiceImplementation(metrics, TimeSpan.Zero);
        }

        public void Update()
        {
            //fileMetricOutput = new FileMetricOutput(coreLogger, Process.GetCurrentProcess().ProcessName, Path.Combine(rootRuntimeDirectory, "logs"), 10485760);
            //metrics.AddMetricOutput(fileMetricOutput);
            //if (!string.IsNullOrEmpty(mainConfig.GetString("appInsightsConnectionString")))
            //{
            //    coreLogger.Log("Enabling AppInsights metrics upload");
            //    appInsightsMetricOutput = new AppInsightsMetricOutput(coreLogger, mainConfig.GetString("appInsightsConnectionString"));
            //    metrics.AddMetricOutput(appInsightsMetricOutput);
            //}
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                if (disposing)
                {
                    _windowsPerfCounters?.Dispose();
                    _gcObserver?.Dispose();
                    _threadPoolObserver?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
