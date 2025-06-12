// -----------------------------------------------------------------------
// <copyright file="IProbe.cs" company="Microsoft">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Durandal.Common.Monitoring
{
    using Durandal.Common.Config;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface representing a single service test which runs at regular intervals.
    /// All tests must inherit from this interface.
    /// </summary>
    public interface IServiceMonitor : IDisposable
    {
        /// <summary>
        /// Performs optional initialization of this monitor before it starts to run
        /// </summary>
        /// <param name="environmentConfig">The local app configuration for the monitoring framework.</param>
        /// <param name="machineLocalGuid">A GUID which uniquely and deterministically identifies the machine the tests are running from. Can be used as a mock user ID</param>
        /// <param name="localFileSystem">A local file system, for fetching test data files.</param>
        /// <param name="httpClientFactory">A pooled implementation of IHttpClientFactory, for making external HTTP requests.</param>
        /// <param name="socketFactory">A pooled implementation of ISocketFactory, for making external socket requests.</param>
        /// <param name="metrics">Metric colector used by some components.</param>
        /// <param name="metricDimensions">Dimensions to send with metrics.</param>
        /// <returns></returns>
        Task<bool> Initialize(
            IConfiguration environmentConfig,
            Guid machineLocalGuid,
            IFileSystem localFileSystem,
            IHttpClientFactory httpClientFactory,
            WeakPointer<ISocketFactory> socketFactory,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions);

        /// <summary>
        /// Gets the name of this test case. Must be unique among all tests.
        /// </summary>
        string TestName { get; }

        /// <summary>
        /// Gets the name of this test case's suite.
        /// Alerts will often be triggered based on the health of an entire test suite, not the individual tests.
        /// </summary>
        string TestSuiteName { get; }

        /// <summary>
        /// Gets a description of what this test does
        /// </summary>
        string TestDescription { get; }

        /// <summary>
        /// If non-null, this is the pass rate threshold _below_ which this test would be considered failing, in the range of 0.0 -> 1.0
        /// For example, 0.7 would mean to fail if 30% of tests failed in the last few minutes
        /// </summary>
        float? PassRateThreshold { get; }

        /// <summary>
        /// If non-null, this is the latency threshold _above_ which this test would be considered failing, expressed as an absolute timespan.
        /// </summary>
        TimeSpan? LatencyThreshold { get; }

        /// <summary>
        /// Gets the interval that should separate each execution of this test
        /// </summary>
        TimeSpan QueryInterval { get; }

        /// <summary>
        /// Executes one turn of this test and generates a response
        /// </summary>
        /// <returns>The test response for this particular machine</returns>
        Task<SingleTestResult> Run(Guid traceId, CancellationToken cancelToken, IRealTimeProvider realTime);

        // Returns a potentially empty collection of analytics queries which can be used to drill down into this test's failures or traces
        //IEnumerable<AppInsightsQuery> RelatedAnalyticsQueries { get; }

        /// <summary>
        /// This is an optional key which can be used to prevent multiple tests from deadlocking each other.
        /// The test scheduler will make a best-effort attempt to prevent tests with the same ExclusivityKey
        /// from running at the same time. Default is null.
        /// </summary>
        string ExclusivityKey { get; }
    }
}
