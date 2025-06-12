using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.Time.Scheduling;
using Durandal.Common.Logger;
using Durandal.Common.Instrumentation;
using Durandal.Common.Collections;
using Durandal.API;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting.Proxies
{
    /// <summary>
    /// Implements a metric collector which sends batches of metrics over a remote channel (post office)
    /// to be aggregated somewhere else.
    /// </summary>
    public class RemotedMetricCollector : IMetricCollector
    {
        private static readonly TimeSpan METRIC_OUTPUT_INTERVAL = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan METRIC_AGGREGATION_INTERVAL = TimeSpan.FromSeconds(5);

        // Instant metrics are just the raw count, reset to 0 after each reporting period
        private readonly Counter<CounterInstance> _instantMetrics;

        // Continuous metrics are reported as a static average spanning the aggregation interval
        private readonly FastConcurrentDictionary<CounterInstance, StaticAverage> _continousMetrics;

        // Percentiles are kept as a stream of observations
        private readonly FastConcurrentDictionary<CounterInstance, List<double>> _percentiles;
        private readonly ISet<IMetricSource> _sources;
        private readonly CancellationTokenSource _backgroundThreadCancelizer = new CancellationTokenSource();
        private readonly Task _backgroundThread;
        private readonly IRealTimeProvider _realTime;
        private readonly IRealTimeProvider _backgroundThreadRealTime;
        private readonly ILogger _logger;
        private readonly RemoteDialogMethodDispatcher _remoteDispatcher;
        private int _disposed = 0;

        /// <summary>
        /// Creates an metric collector which proactively collects metrics and reports them to a remote metric collector
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="remoteDispatcher">Dispatcher that we actually send the metrics to</param>
        /// <param name="realTime">A definition of real time (mostly for testing)</param>
        public RemotedMetricCollector(ILogger logger, RemoteDialogMethodDispatcher remoteDispatcher, IRealTimeProvider realTime = null)
        {
            _logger = logger;
            _realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            _remoteDispatcher = remoteDispatcher;
            _backgroundThreadRealTime = _realTime.Fork("RemotedMetricCollectorBackgroundThread");
            _continousMetrics = new FastConcurrentDictionary<CounterInstance, StaticAverage>();
            _percentiles = new FastConcurrentDictionary<CounterInstance, List<double>>(); 
            _instantMetrics = new Counter<CounterInstance>(); 
            _sources = new HashSet<IMetricSource>();
            _backgroundThreadCancelizer = new CancellationTokenSource();
            _backgroundThread = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(RunReportingThread);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RemotedMetricCollector()
        {
            Dispose(false);
        }
#endif

        public void ReportInstant(string counter, DimensionSet dimensions, int increment = 1)
        {
            CounterInstance instance = new CounterInstance(counter, dimensions, CounterType.Instant);
            ReportInstant(instance, increment);
        }

        private void ReportInstant(CounterInstance instance, int increment)
        {
            _instantMetrics.Increment(instance, increment);
        }

        public void ReportContinuous(string counter, DimensionSet dimensions, double value)
        {
            CounterInstance instance = new CounterInstance(counter, dimensions, CounterType.Continuous);
            ReportContinuous(instance, value);
        }

        private void ReportContinuous(CounterInstance instance, double value)
        {
            StaticAverage avg;
            if (!_continousMetrics.TryGetValue(instance, out avg))
            {
                avg = new StaticAverage();
                avg.Add(value);
                _continousMetrics[instance] = avg;
            }
            else
            {
                lock(avg)
                {
                    avg.Add(value);
                }
            }
        }

        public void ReportPercentile(string counter, DimensionSet dimensions, double value)
        {
            CounterInstance instance = new CounterInstance(counter, dimensions, CounterType.Percentile);
            ReportPercentile(instance, value);
        }

        private void ReportPercentile(CounterInstance instance, double value)
        {
            List<double> avg;
            if (!_percentiles.TryGetValue(instance, out avg))
            {
                avg = new List<double>();
                avg.Add(value);
                _percentiles[instance] = avg;
            }
            else
            {
                lock(avg)
                {
                    avg.Add(value);
                }
            }
        }

        public void AddMetricSource(IMetricSource reportable)
        {
            lock (_sources)
            {
                if (!_sources.Contains(reportable))
                {
                    _sources.Add(reportable);
                    reportable.InitializeMetrics(this);
                }
            }
        }

        public void RemoveMetricSource(IMetricSource reportable)
        {
            lock (_sources)
            {
                if (_sources.Contains(reportable))
                {
                    _sources.Remove(reportable);
                }
            }
        }

        // FIXME will these ever be implemented? WE would actually have to aggregate metrics here for them to even make sense....
        // Or else remote the calls to fetch the values, which would be costly....
        public IReadOnlyDictionary<CounterInstance, double?> GetCurrentMetrics()
        {
            return new Dictionary<CounterInstance, double?>();
        }

        public IReadOnlyDictionary<CounterInstance, double?> GetCurrentMetrics(string metricName)
        {
            return new Dictionary<CounterInstance, double?>();
        }

        public double? GetCurrentMetric(string metricName)
        {
            return null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Ensure that the changes committer can write its final changes before shutdown
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _backgroundThreadCancelizer.Cancel();
                _backgroundThread.Wait(5000);
                _backgroundThreadCancelizer.Dispose();
            }
        }

        private enum ReportingOperation
        {
            Nothing,
            CollectProactiveMetrics,
            SendMetricsOverSocket
        }

        private async Task RunReportingThread()
        {
            CancellationToken cancelToken = _backgroundThreadCancelizer.Token;
            _logger.Log("Background metric thread started");
            try
            {
                using (DeltaClock<ReportingOperation> reportingTimer = new DeltaClock<ReportingOperation>(_backgroundThreadRealTime))
                {
                    reportingTimer.ScheduleEvent(ReportingOperation.CollectProactiveMetrics, new TimeSpan(METRIC_AGGREGATION_INTERVAL.Ticks / 2L));
                    reportingTimer.ScheduleEvent(ReportingOperation.SendMetricsOverSocket, METRIC_OUTPUT_INTERVAL);
                    while (!cancelToken.IsCancellationRequested)
                    {
                        ReportingOperation nextOperation = await reportingTimer.WaitForNextEventAsync(cancelToken).ConfigureAwait(false);
                        if (nextOperation == ReportingOperation.Nothing)
                        {
                            // Clock has stopped. Shutdown this thread
                            break;
                        }
                        else if (nextOperation == ReportingOperation.CollectProactiveMetrics)
                        {
                            // Aggregate metrics from all reportables
                            lock (_sources)
                            {
                                foreach (IMetricSource reportable in _sources)
                                {
                                    try
                                    {
                                        reportable.ReportMetrics(this);
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.Log("Exception while reporting metrics", LogLevel.Err);
                                        _logger.Log(e, LogLevel.Err);
                                    }
                                }
                            }

                            reportingTimer.ScheduleEvent(ReportingOperation.CollectProactiveMetrics, METRIC_AGGREGATION_INTERVAL);
                        }
                        else if (nextOperation == ReportingOperation.SendMetricsOverSocket)
                        {
                            // Collect the metrics we are going to send
                            SerializedMetricEventList metricEventList = new SerializedMetricEventList();

                            foreach (var instant in _instantMetrics)
                            {
                                SerializedMetricEvent newEvent = new SerializedMetricEvent()
                                {
                                    CounterName = instant.Key.CounterName,
                                    MetricType = (int)CounterType.Instant,
                                    SerializedDimensions = instant.Key.Dimensions.ToString(),
                                    SerializedValues = new ArraySegment<byte>(new byte[4])
                                };
                                int intValue = (int)instant.Value;
                                BinaryHelpers.Int32ToByteArrayLittleEndian(intValue, newEvent.SerializedValues.Array, 0);
                                metricEventList.Events.Add(newEvent);
                            }

                            _instantMetrics.Clear();

                            foreach (var continuous in _continousMetrics)
                            {
                                SerializedMetricEvent newEvent = new SerializedMetricEvent()
                                {
                                    CounterName = continuous.Key.CounterName,
                                    MetricType = (int)CounterType.Continuous,
                                    SerializedDimensions = continuous.Key.Dimensions.ToString(),
                                    SerializedValues = new ArraySegment<byte>(new byte[8])
                                };

                                BinaryHelpers.DoubleToByteArrayLittleEndian(continuous.Value.Average, newEvent.SerializedValues.Array, 0);
                                continuous.Value.Reset();
                                metricEventList.Events.Add(newEvent);
                            }

                            foreach (var percentile in _percentiles)
                            {
                                lock (percentile.Value)
                                {
                                    int numObservations = percentile.Value.Count;
                                    SerializedMetricEvent newEvent = new SerializedMetricEvent()
                                    {
                                        CounterName = percentile.Key.CounterName,
                                        MetricType = (int)CounterType.Percentile,
                                        SerializedDimensions = percentile.Key.Dimensions.ToString(),
                                        SerializedValues = new ArraySegment<byte>(new byte[8 * numObservations])
                                    };

                                    for (int val = 0; val < numObservations; val++)
                                    {
                                        BinaryHelpers.DoubleToByteArrayLittleEndian(
                                            percentile.Value[val],
                                            newEvent.SerializedValues.Array,
                                            newEvent.SerializedValues.Offset + (val * 8));
                                    }

                                    percentile.Value.Clear();
                                    metricEventList.Events.Add(newEvent);
                                }
                            }

                            reportingTimer.ScheduleEvent(ReportingOperation.SendMetricsOverSocket, METRIC_OUTPUT_INTERVAL);

                            // Send the metrics over the wire
                            try
                            {
                                await _remoteDispatcher.Metric_Upload(metricEventList, _backgroundThreadRealTime, cancelToken).ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                _logger.Log(e, LogLevel.Err);
                            }
                        }
                        else
                        {
                            _logger.Log("Unknown reporting operation " + nextOperation.ToString(), LogLevel.Err);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
            }
            finally
            {
                _backgroundThreadRealTime.Merge();
                _logger.Log("Background remote metric thread stopped");
            }
        }
    }
}
