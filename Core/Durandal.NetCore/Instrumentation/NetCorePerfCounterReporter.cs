using Durandal.Common.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Text;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.MathExt;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Metric source which collects the data reported by .Net Core's Runtime EventSource. This replaces
    /// the ".NET CLR ____" categories of counters that were previously reported to WMI by .Net Framework.
    /// </summary>
    public class NetCorePerfCounterReporter : EventListener, IMetricSource
    {
        private readonly IList<PerfCounterWrapper> _counters = new List<PerfCounterWrapper>();
        private readonly DimensionSet _dimensions = DimensionSet.Empty;

        public NetCorePerfCounterReporter(DimensionSet dimensions)
        {
            // Note: Because of some black magic in the runtime, OnEventSourceCreated gets called
            // before the constructor of this subclass begins. So you can't rely on any objects
            // that you create here to be instantiated before event sources start getting added.
            _dimensions = dimensions;
        }

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (string.Equals(source.Name, "System.Runtime", StringComparison.Ordinal))
            {
                EnableEvents(source, EventLevel.LogAlways);
                TryRegisterClrCounter(source, "_jitTimeCounter", CommonInstrumentation.Key_Counter_ClrTimeInJit, true, 1d / 1000d); // convert "ms per second" to "seconds per second", which turns it into a percentage
                TryRegisterClrCounter(source, "_monitorContentionCounter", CommonInstrumentation.Key_Counter_ClrContentionRate, true);
                TryRegisterClrCounter(source, "_threadPoolThreadCounter", CommonInstrumentation.Key_Counter_ClrProcessPhysicalThreads, false);
                TryRegisterClrCounter(source, "_exceptionCounter", CommonInstrumentation.Key_Counter_ClrExceptionsThrown, true);
                TryRegisterClrCounter(source, "_lohSizeCounter", CommonInstrumentation.Key_Counter_ClrLargeObjectHeapKb, false, 1d/ 1024d);
                TryRegisterClrCounter(source, "_gcTimeCounter", CommonInstrumentation.Key_Counter_ClrTimeInGc, false);
                TryRegisterClrCounter(source, "_committedCounter", CommonInstrumentation.Key_Counter_ClrTotalCommittedKb, true, 1024d); // .net core reports it in MB so scale in reverse
                TryRegisterClrCounter(source, "_gen0SizeCounter", CommonInstrumentation.Key_Counter_ClrGen0HeapSizeKb, false, 1d / 1024d);
                TryRegisterClrCounter(source, "_gen1SizeCounter", CommonInstrumentation.Key_Counter_ClrGen1HeapSizeKb, false, 1d / 1024d);
                TryRegisterClrCounter(source, "_gen2SizeCounter", CommonInstrumentation.Key_Counter_ClrGen2HeapSizeKb, false, 1d / 1024d);
                TryRegisterClrCounter(source, "_fragmentationCounter", CommonInstrumentation.Key_Counter_ClrGcFragmentation, false);
                TryRegisterClrCounter(source, "_threadPoolQueueCounter", CommonInstrumentation.Key_Counter_ClrThreadQueueLength, false);
                TryRegisterClrCounter(source, "_timerCounter", CommonInstrumentation.Key_Counter_ClrTimerCount, false);
                TryRegisterClrCounter(source, "_allocRateCounter", CommonInstrumentation.Key_Counter_ClrAllocationRateKb, true, 1d / 1024d);
            }

            // These are what the runtime uses internally to actually get the data for these counters.
            // Problem is, most of these methods are private or otherwise internal to the runtime,
            // so we can't just use them! Possible solution would be to try to use reflection to access
            // these internally, but then we might as well just use reflection to access the polling counters,
            // it's a more uniform interface that way....
            //x = () => RuntimeEventSourceHelper.GetCpuUsage();
            //x = () => (double)(Environment.WorkingSet / 1_000_000);
            //x = () => (double)(GC.GetTotalMemory(false) / 1_000_000);
            //x = () => GC.CollectionCount(0);
            //x = () => GC.CollectionCount(1);
            //x = () => GC.CollectionCount(2);
            //x = () => ThreadPool.ThreadCount;
            //x = () => Monitor.LockContentionCount;
            //x = () => ThreadPool.PendingWorkItemCount;
            //x = () => ThreadPool.CompletedWorkItemCount;
            //x = () => GC.GetTotalAllocatedBytes();
            //x = () => Timer.ActiveCount;
            //x = () => {
            //    var gcInfo = GC.GetGCMemoryInfo();
            //    return gcInfo.HeapSizeBytes != 0 ? gcInfo.FragmentedBytes * 100d / gcInfo.HeapSizeBytes : 0;
            //};
            //x = () => (double)(GC.GetGCMemoryInfo().TotalCommittedBytes / 1_000_000);
            //x = () => Exception.GetExceptionCount();
            //x = () => GC.GetLastGCPercentTimeInGC();
            //x = () => GC.GetGenerationSize(0);
            //x = () => GC.GetGenerationSize(1);
            //x = () => GC.GetGenerationSize(2);
            //x = () => GC.GetGenerationSize(3);
            //x = () => GC.GetGenerationSize(4);
            //x = () => System.Reflection.Assembly.GetAssemblyCount();
            //x = () => System.Runtime.JitInfo.GetCompiledILBytes();
            //x = () => System.Runtime.JitInfo.GetCompiledMethodCount();
            //x = () => System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds;
        }

        private void TryRegisterClrCounter(object eventSource, string memberName, string durandalMetricName, bool isIncrementingCounter = false, double scale = 1)
        {
            Func<double> metricSource = GetInternalPollingCounter(eventSource, memberName);
            if (metricSource == null)
            {
                return;
            }

            _counters.Add(new PerfCounterWrapper(metricSource, durandalMetricName, isIncrementingCounter, scale));
        }

        // FIXME: WHY DO I HAVE TO DO THIS
        // WHY IS THERE NO APPARENT WAY TO VIEW EVENT COUNTERS FOR A PROCESS, WITHIN THAT SAME PROCESS
        private Func<double> GetInternalPollingCounter(object eventSource, string memberName)
        {
            FieldInfo counterField = eventSource.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (counterField == null)
            {
                Console.WriteLine("Failed to find .Net runtime counter \"" + memberName + "\"");
                return null;
            }

            FieldInfo metricCollectorField = null;
            object counterImpl = counterField.GetValue(eventSource);
            if (counterImpl is PollingCounter)
            {
                PollingCounter counter = counterImpl as PollingCounter;
                metricCollectorField = counter.GetType().GetField("_metricProvider", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            else if (counterImpl is IncrementingPollingCounter)
            {
                IncrementingPollingCounter counter = counterImpl as IncrementingPollingCounter;
                metricCollectorField = counter.GetType().GetField("_totalValueProvider", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            if (metricCollectorField == null)
            {
                Console.WriteLine("Failed to find .Net runtime counter \"" + memberName + "\"");
                return null;
            }

            Func<double> metricCollector = metricCollectorField.GetValue(counterImpl) as Func<double>;
            if (metricCollector == null)
            {
                Console.WriteLine("Failed to find .Net runtime counter \"" + memberName + "\"");
                return null;
            }

            return metricCollector;
        }

        public void InitializeMetrics(IMetricCollector collector)
        {
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
            foreach (var counter in _counters)
            {
                double val = counter.MetricSource();
                if (double.IsNaN(val) || double.IsInfinity(val))
                {
                    reporter.ReportContinuous(counter.DurandalMetricName, _dimensions, 0);
                }
                else
                {
                    reporter.ReportContinuous(counter.DurandalMetricName, _dimensions, counter.MetricSource() * counter.Scale);
                }
            }
        }

        private class PerfCounterWrapper
        {
            private readonly Stopwatch _stopwatch;
            private readonly MovingAverage _smoother;
            private double _previousIncrement;

            public PerfCounterWrapper(Func<double> metricSource, string durandalMetricName, bool isIncrementingCounter, double scale)
            {
                if (scale == 0 || double.IsNaN(scale) || double.IsInfinity(scale))
                {
                    throw new ArgumentOutOfRangeException("Counter scale must be a rational nonzero number");
                }

                metricSource.AssertNonNull(nameof(metricSource));
                DurandalMetricName = durandalMetricName.AssertNonNullOrEmpty(nameof(durandalMetricName));
                Scale = scale;

                if (isIncrementingCounter)
                {
                    _stopwatch = new Stopwatch();
                    // this helps us smooth out spikes in data for things like exceptions that are stochastic
                    // average out the reported metric value based on previous 20 observations
                    // ideally this would be based on the interval that ReportMetrics() actually gets called
                    // so we can return the exact average based on the overall metric window. But this works well enough
                    _smoother = new MovingAverage(20, 0.0);
                    _previousIncrement = 0;
                    MetricSource = () =>
                    {
                        double elapsedSeconds = _stopwatch.ElapsedMillisecondsPrecise() / 1000d;
                        if (elapsedSeconds > 0)
                        {
                            double currentCounter = metricSource();
                            _smoother.Add(currentCounter - _previousIncrement);
                            _previousIncrement = currentCounter;
                        }

                        _stopwatch.Restart();
                        return _smoother.Average;
                    };
                }
                else
                {
                    MetricSource = metricSource;
                }
            }

            public Func<double> MetricSource { get; }
            public string DurandalMetricName { get; }
            public double Scale { get; }
        }
    }
}
