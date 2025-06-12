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
using Durandal.Common.Collections;
using System.Diagnostics;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Contains the core logic of aggregating and scheduling proactive metric collections, and exposes an abstract method to output the aggregate metrics to some other service
    /// </summary>
    public class MetricCollector : IMetricCollector
    {
        private const int MAX_CONTINUOUS_OBSERVATIONS = 10;
        private readonly TimeSpan _metricOutputInterval;
        private readonly TimeSpan _metricAggregationInterval;
        private readonly Counter<CounterInstance> _instantMetrics;
        private readonly FastConcurrentDictionary<CounterInstance, TimeWindowMovingAverage> _continousMetrics;
        private readonly FastConcurrentDictionary<CounterInstance, MovingPercentile> _percentiles;
        private readonly ISet<IMetricSource> _sources;
        private readonly ISet<IMetricOutput> _outputs;
        private readonly CancellationTokenSource _backgroundThreadCancelizer = new CancellationTokenSource();
        private readonly Task _backgroundThread;
        private readonly IRealTimeProvider _realTime;
        private readonly IRealTimeProvider _backgroundThreadRealTime;
        private readonly ILogger _logger;
        private readonly int _percentileSampleSize;
        private int _disposed = 0;

        /// <summary>
        /// Creates an metric collector which proactively collects metrics and aggregates them on a background thread.
        /// </summary>
        /// <param name="logger">A logger for collection messages</param>
        /// <param name="aggregationInterval">The interval between each metric collection, typically a few seconds</param>
        /// <param name="outputInterval">The interval between reports of aggregate metrics, typically every minute or so</param>
        /// <param name="realTime">A definition of real time (mostly for testing)</param>
        /// <param name="percentileSampleSize">The sample size to use for all collected percentiles</param>
        public MetricCollector(
            ILogger logger,
            TimeSpan aggregationInterval,
            TimeSpan outputInterval,
            IRealTimeProvider realTime = null,
            int percentileSampleSize = 200)
        {
            if (percentileSampleSize <= 0)
            {
                throw new ArgumentOutOfRangeException("Percentile sample size must be a positive integer");
            }

            _logger = logger;
            _percentileSampleSize = percentileSampleSize;
            _metricAggregationInterval = aggregationInterval;
            _metricOutputInterval = outputInterval;
            _realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            _backgroundThreadRealTime = _realTime.Fork("MetricCollectorBackgroundThread");
            _continousMetrics = new FastConcurrentDictionary<CounterInstance, TimeWindowMovingAverage>();
            _percentiles = new FastConcurrentDictionary<CounterInstance, MovingPercentile>();
            _instantMetrics = new Counter<CounterInstance>();
            _sources = new HashSet<IMetricSource>();
            _outputs = new HashSet<IMetricOutput>();
            _backgroundThreadCancelizer = new CancellationTokenSource();
            _backgroundThread = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(RunReportingThread);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~MetricCollector()
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

        private TimeWindowMovingAverage CreateNewMovingAverage()
        {
            return new TimeWindowMovingAverage(MAX_CONTINUOUS_OBSERVATIONS, _metricOutputInterval, _realTime);
        }

        private void ReportContinuous(CounterInstance instance, double value)
        {
            TimeWindowMovingAverage avg;
            _continousMetrics.TryGetValueOrSet(instance, out avg, CreateNewMovingAverage);

            Monitor.Enter(avg);
            try
            {
                avg.Add(value);
            }
            finally
            {
                Monitor.Exit(avg);
            }
        }
        
        public void ReportPercentile(string counter, DimensionSet dimensions, double value)
        {
            CounterInstance instance = new CounterInstance(counter, dimensions, CounterType.Percentile);
            ReportPercentile(instance, value);
        }

        private MovingPercentile CreateNewMovingPercentile()
        {
            return new MovingPercentile(_percentileSampleSize, 0.25, 0.50, 0.75, 0.95, 0.99);
        }

        private void ReportPercentile(CounterInstance instance, double value)
        {
            MovingPercentile avg;
            _percentiles.TryGetValueOrSet(instance, out avg, CreateNewMovingPercentile);

            lock (avg)
            {
                avg.Add(value);
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

        public void AddMetricOutput(IMetricOutput output)
        {
            lock (_outputs)
            {
                if (!_outputs.Contains(output))
                {
                    _outputs.Add(output);
                }
            }
        }

        public void RemoveMetricOutput(IMetricOutput output)
        {
            lock (_outputs)
            {
                if (_outputs.Contains(output))
                {
                    _outputs.Remove(output);
                }
            }
        }

        public IReadOnlyDictionary<CounterInstance, double?> GetCurrentMetrics()
        {
            // OPT: could this dictionary be cached locally, and then this method would just update the values
            // for each key and then return it as readonly so the cached dict is immutable to the caller?
            Dictionary<CounterInstance, double?> returnVal = new Dictionary<CounterInstance, double?>();
            var continuousEnumerator = _continousMetrics.GetValueEnumerator();
            while (continuousEnumerator.MoveNext())
            {
                returnVal.Add(continuousEnumerator.Current.Key, continuousEnumerator.Current.Value.Average);
            }

            var percentileEnumerator = _percentiles.GetValueEnumerator();
            while (percentileEnumerator.MoveNext())
            {
                lock (percentileEnumerator.Current.Value)
                {
                    foreach (var percentileClass in percentileEnumerator.Current.Value.GetPercentiles())
                    {
                        string counterName = $"{percentileEnumerator.Current.Key.CounterName}_p{percentileClass.Item1.ToString("F4").TrimEnd('0')}";
                        returnVal.Add(new CounterInstance(counterName, percentileEnumerator.Current.Key.Dimensions, CounterType.Percentile), percentileClass.Item2);
                    }
                }
            }

            return returnVal;
        }

        public IReadOnlyDictionary<CounterInstance, double?> GetCurrentMetrics(string metricName)
        {
            Dictionary<CounterInstance, double?> returnVal = new Dictionary<CounterInstance, double?>();
            var continuousEnumerator = _continousMetrics.GetValueEnumerator();
            while (continuousEnumerator.MoveNext())
            {
                if (string.Equals(continuousEnumerator.Current.Key.CounterName, metricName, StringComparison.Ordinal))
                {
                    returnVal.Add(continuousEnumerator.Current.Key, continuousEnumerator.Current.Value.Average);
                }
            }

            var percentileEnumerator = _percentiles.GetValueEnumerator();
            while (percentileEnumerator.MoveNext())
            {
                lock (percentileEnumerator.Current.Value)
                {
                    foreach (var percentileClass in percentileEnumerator.Current.Value.GetPercentiles())
                    {
                        string counterName = $"{percentileEnumerator.Current.Key.CounterName}_p{percentileClass.Item1.ToString("F4").TrimEnd('0')}";
                        if (string.Equals(counterName, metricName, StringComparison.Ordinal))
                        {
                            returnVal.Add(new CounterInstance(counterName, percentileEnumerator.Current.Key.Dimensions, CounterType.Percentile), percentileClass.Item2);
                        }
                    }
                }
            }

            return returnVal;
        }

        public double? GetCurrentMetric(string metricName)
        {
            var continuousEnumerator = _continousMetrics.GetValueEnumerator();
            while (continuousEnumerator.MoveNext())
            {
                if (string.Equals(continuousEnumerator.Current.Key.CounterName, metricName, StringComparison.Ordinal))
                {
                    return continuousEnumerator.Current.Value.Average;
                }
            }

            var percentileEnumerator = _percentiles.GetValueEnumerator();
            while (percentileEnumerator.MoveNext())
            {
                lock (percentileEnumerator.Current.Value)
                {
                    foreach (var percentileClass in percentileEnumerator.Current.Value.GetPercentiles())
                    {
                        string counterName = $"{percentileEnumerator.Current.Key.CounterName}_p{percentileClass.Item1.ToString("F4").TrimEnd('0')}";
                        if (string.Equals(counterName, metricName, StringComparison.Ordinal))
                        {
                            return percentileClass.Item2;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// This method is not available in the interface, it's mostly intended for testing.
        /// Resets all counters and removes all stored metrics.
        /// </summary>
        public void Reset()
        {
            _instantMetrics.Clear();
            _continousMetrics.Clear();
            _percentiles.Clear();
            _sources.Clear();
            _outputs.Clear();
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
            OutputAggregateMetrics
        }

        private async Task RunReportingThread()
        {
            _logger.Log("Background metric thread started");
            try
            {
                using (DeltaClock<ReportingOperation> reportingTimer = new DeltaClock<ReportingOperation>(_backgroundThreadRealTime))
                {
                    reportingTimer.ScheduleEvent(ReportingOperation.CollectProactiveMetrics, _metricAggregationInterval);
                    reportingTimer.ScheduleEvent(ReportingOperation.OutputAggregateMetrics, _metricOutputInterval);
                    while (!_backgroundThreadCancelizer.IsCancellationRequested)
                    {
                        ReportingOperation nextOperation = await reportingTimer.WaitForNextEventAsync(_backgroundThreadCancelizer.Token).ConfigureAwait(false);
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

                            // Aggregate instant metrics as well (converting them into rates)
                            double aggregationIntervalSeconds = _metricAggregationInterval.TotalSeconds;

                            foreach (var instantMetric in _instantMetrics)
                            {
                                float floatVal = instantMetric.Value;
                                if (floatVal != 0)
                                {
                                    TimeWindowMovingAverage continuousMetric;
                                    _continousMetrics.TryGetValueOrSet(instantMetric.Key, out continuousMetric, CreateNewMovingAverage);
                                    double value = (double)floatVal / aggregationIntervalSeconds;
                                    continuousMetric.Add(value);
                                    _instantMetrics.Set(instantMetric.Key, 0);
                                }
                            }

                            reportingTimer.ScheduleEvent(ReportingOperation.CollectProactiveMetrics, _metricAggregationInterval);
                        }
                        else if (nextOperation == ReportingOperation.OutputAggregateMetrics)
                        {
                            var metrics = GetCurrentMetrics();
                            List<Task> backgroundTasks = new List<Task>();
                            lock (_outputs)
                            {
                                foreach (var output in _outputs)
                                {
                                    try
                                    {
                                        backgroundTasks.Add(output.OutputAggregateMetrics(metrics));
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.Log(e);
                                    }
                                }
                            }

                            reportingTimer.ScheduleEvent(ReportingOperation.OutputAggregateMetrics, _metricOutputInterval);

                            foreach (var task in backgroundTasks)
                            {
                                try
                                {
                                    await task.ConfigureAwait(false);
                                }
                                catch (Exception e)
                                {
                                    _logger.Log(e);
                                }
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
                _logger.Log("Background metric thread stopped");
                _backgroundThreadCancelizer.Dispose();
            }
        }
    }
}
