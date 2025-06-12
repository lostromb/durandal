using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Config;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Instrumentation;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System.Threading;
using Durandal.Common.ServiceMgmt;

namespace DurandalServices
{
    public abstract class BasicService : IService
    {
        private readonly IFileSystem _fileSystem;

        public BasicService(
            string serviceName,
            ILogger logger,
            IFileSystem fileSystem,
            WeakPointer<IThreadPool> threadPool,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet dimensions)
        {
            ServiceName = serviceName.AssertNonNullOrEmpty(nameof(serviceName));
            ServiceLogger = logger.AssertNonNull(nameof(logger));
            _fileSystem = fileSystem.AssertNonNull(nameof(fileSystem));
            ThreadPool = threadPool.AssertNonNull(nameof(threadPool));
            Metrics = metrics.AssertNonNull(nameof(metrics));
            MetricDimensions = dimensions.AssertNonNull(nameof(dimensions));
            ServiceConfig = IniFileConfiguration.Create(ServiceLogger, new VirtualPath("serviceconfig_" + ServiceName + ".ini"), _fileSystem, DefaultRealTimeProvider.Singleton, false, true).Await();
        }

        public ILogger ServiceLogger
        {
            get;
            private set;
        }

        public WeakPointer<IThreadPool> ThreadPool
        {
            get;
            private set;
        }

        public WeakPointer<IMetricCollector> Metrics
        {
            get;
            private set;
        }

        public DimensionSet MetricDimensions
        {
            get;
            private set;
        }

        public IConfiguration ServiceConfig
        {
            get; private set;
        }

        public string ServiceName
        {
            get; private set;
        }

        public abstract bool IsRunning();

        public abstract Task Start(CancellationToken cancelToken, IRealTimeProvider realTime);

        public abstract Task Stop(CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}
