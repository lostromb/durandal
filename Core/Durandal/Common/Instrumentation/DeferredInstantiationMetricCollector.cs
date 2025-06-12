using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// This class is used to solve circular references, if you want to collect metrics on something, but the metric collector itself
    /// depends on the thing you want to observe. This class holds a deferred reference to an IMetricCollector that can be set after creation
    /// </summary>
    public class DeferredInstantiationMetricCollector : IMetricCollector
    {
        private readonly HashSet<IMetricSource> _deferredSources = new HashSet<IMetricSource>();
        private readonly object _sourceLock = new object();
        private IMetricCollector _reference = null;
        private int _disposed = 0;

        public DeferredInstantiationMetricCollector()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~DeferredInstantiationMetricCollector()
        {
            Dispose(false);
        }
#endif

        public void SetCollectorImplementation(IMetricCollector implementation)
        {
            if (implementation == null)
            {
                throw new ArgumentNullException("Implementation of IMetricCollector cannot be set to null");
            }

            lock (_sourceLock)
            {
                if (_reference != null)
                {
                    throw new InvalidOperationException("Implementation of IMetricCollector cannot be set more than once");
                }

                _reference = implementation;

                foreach (IMetricSource deferredSource in _deferredSources)
                {
                    _reference.AddMetricSource(deferredSource);
                }

                _deferredSources.Clear();
            }
        }

        public void AddMetricSource(IMetricSource reportable)
        {
            lock (_sourceLock)
            {
                if (_reference != null)
                {
                    _reference.AddMetricSource(reportable);
                }
                else
                {
                    _deferredSources.Add(reportable);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                lock (_sourceLock)
                {
                    if (_reference != null)
                    {
                        _reference.Dispose();
                    }
                }
            }
        }

        public IReadOnlyDictionary<CounterInstance, double?> GetCurrentMetrics()
        {
            if (_reference != null)
            {
                return _reference.GetCurrentMetrics();
            }
            else
            {
                return new Dictionary<CounterInstance, double?>();
            }
        }

        public void RemoveMetricSource(IMetricSource reportable)
        {
            lock (_sourceLock)
            {
                if (_reference != null)
                {
                    _reference.RemoveMetricSource(reportable);
                }
                else
                {
                    _deferredSources.Remove(reportable);
                }
            }
        }

        public void ReportContinuous(string counter, DimensionSet dimensions, double value)
        {
            if (_reference != null)
            {
                _reference.ReportContinuous(counter, dimensions, value);
            }
        }

        public void ReportInstant(string counter, DimensionSet dimensions, int increment = 1)
        {
            if (_reference != null)
            {
                _reference.ReportInstant(counter, dimensions, increment);
            }
        }

        public void ReportPercentile(string counter, DimensionSet dimensions, double value)
        {
            if (_reference != null)
            {
                _reference.ReportPercentile(counter, dimensions, value);
            }
        }

        public IReadOnlyDictionary<CounterInstance, double?> GetCurrentMetrics(string metricName)
        {
            if (_reference != null)
            {
                return _reference.GetCurrentMetrics(metricName);
            }
            else
            {
                return new Dictionary<CounterInstance, double?>();
            }
        }

        public double? GetCurrentMetric(string metricName)
        {
            if (_reference != null)
            {
                return _reference.GetCurrentMetric(metricName);
            }
            else
            {
                return null;
            }
        }
    }
}
