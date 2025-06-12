using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Metric collector that pulls from WMI counters on the local (Windows) machine. This can pull
    /// general metrics for the current machine and process, as well as (in .Net Framework only)
    /// the CLR metrics reported by the .net runtime.
    /// </summary>
#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    public class WindowsPerfCounterReporter : IMetricSource, IDisposable
    {
        private readonly IList<PerfCounterWrapper> _counters;
        private readonly ILogger _logger;
        private readonly DimensionSet _dimensions;
        private readonly StringBuilder _counterNameBuilder;
        private Dictionary<string, PerformanceCounterCategory> _allCounterCategories = null; // lazy map from category names -> category objects
        private Dictionary<string, string[]> _allCategoryInstances = null; // lazy map from category names -> list of instances in that category
        private Dictionary<int, string> _processIdToInstanceMapping = null; // lazy map from process ID to instance name for that process
        private bool _firstRun = true;
        private Pdh.SafePDH_HQUERY _hQuery;
        private Win32Error _err;
        private int _disposed = 0;

        public WindowsPerfCounterReporter(
            ILogger logger,
            DimensionSet dimensions,
            WindowsPerfCounterSet countersToCreate)
        {
            _counters = new List<PerfCounterWrapper>();
            _logger = logger;
            _dimensions = dimensions;
            _counterNameBuilder = new StringBuilder(Pdh.PDH_MAX_COUNTER_PATH);

            // Create native counter query object
            _err = Pdh.PdhOpenQuery(null, IntPtr.Zero, out _hQuery);
            _err.ThrowIfFailed("Failed to initialize PDH query for system performance counters.");

            // And create all counters requested by the user
            _logger.Log("Starting to create WMI perf counters");
            CreateSpecifiedCounters(countersToCreate);
            _logger.Log("Finished creating perf counters");

            // Clean up caches that we don't need anymore
            _allCounterCategories = null;
            _allCategoryInstances = null;
            _processIdToInstanceMapping = null;

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        ~WindowsPerfCounterReporter()
        {
            Dispose(false);
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
            if (_hQuery == null)
            {
                _logger.Log("Failed to update performance query: PDH_HQUERY handle not valid", LogLevel.Err);
                return;
            }

            if (_counters == null || _counters.Count == 0)
            {
                // Nothing to report.
                return;
            }

            try
            {
                _err = Pdh.PdhCollectQueryData(_hQuery);

                if (_err != Win32Error.ERROR_SUCCESS)
                {
                    _logger.Log("Failed to update performance query: " + _err.ToString(), LogLevel.Err);
                    return;
                }

                foreach (PerfCounterWrapper counter in _counters)
                {
                    Pdh.CounterType counterType;
                    Pdh.PDH_FMT_COUNTERVALUE counterValue;
                    _err = Pdh.PdhGetFormattedCounterValue(counter.NativeCounter, Pdh.PDH_FMT.PDH_FMT_DOUBLE | Pdh.PDH_FMT.PDH_FMT_NOCAP100, out counterType, out counterValue);
                    if (_err == Win32Error.ERROR_SUCCESS)
                    {
                        reporter.ReportContinuous(counter.DurandalMetricName, _dimensions, counterValue.doubleValue * counter.Scale);
                    }
                    else if (!_firstRun)
                    {
                        try
                        {
                            if (_err != Win32Error.PDH_CALC_NEGATIVE_VALUE)
                            {
                                _logger.Log(string.Format("Failed to query performance counter {0}: {1} 0x{2:X8}", counter.SystemCounterName, _err.ToString(), (uint)_err), LogLevel.Err);
                            }
                        }
                        catch (Exception)
                        {
                            // sometimes the formatter for _err.ToString() itself fails, which is pretty bogus
                            _logger.Log(string.Format("Failed to query performance counter {0}: Unknown error 0x{1:X8}", counter.SystemCounterName, (uint)_err), LogLevel.Err);
                        }
                    }
                }

                _firstRun = false;
            }
            catch (Exception e)
            {
                // Suppress errors on first run since some counters don't report data until the second collection
                if (!_firstRun)
                {
                    _logger.Log("Exception while querying Win32 performance counters", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
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

            foreach (var counter in _counters)
            {
                Pdh.PdhRemoveCounter(counter.NativeCounter);
                counter.NativeCounter = null;
            }

            Pdh.PdhCloseQuery(_hQuery);
            _hQuery = null;
            _counters.Clear();

            if (disposing)
            {
            }
        }

        // used for experimentation
        private static void ListAllCounters()
        {
            PerformanceCounterCategory[] categories = PerformanceCounterCategory.GetCategories();

            ISet<string> interestingCategories = new HashSet<string>()
            {
                ".NET CLR Networking",
                ".NET CLR Networking 4.0.0.0",
                //"Processor",
                //"LogicalDisk",
                //"Network Adapter",
                //"Network Interface",
                //"ASP.NET",
                //"Objects",
                //".NET CLR Exceptions",
                //"HTTP Service",
                //".NET CLR Memory",
                //".NET CLR Jit",
            };

            foreach (PerformanceCounterCategory category in categories.OrderBy((c) => c.CategoryName))
            {
                if (!interestingCategories.Contains(category.CategoryName))
                {
                    continue;
                }

                string[] instances = category.GetInstanceNames();
                if (instances.Any())
                {
                    foreach (string instance in instances)
                    {
                        if (category.InstanceExists(instance))
                        {
                            PerformanceCounter[] countersOfCategory = category.GetCounters(instance);
                            foreach (PerformanceCounter pc in countersOfCategory)
                            {
                                Console.WriteLine("Category: {0}, instance: {1}, counter: {2}", pc.CategoryName, instance, pc.CounterName);
                                pc?.Dispose();
                            }
                        }
                    }
                }
                else
                {
                    PerformanceCounter[] countersOfCategory = category.GetCounters();
                    foreach (PerformanceCounter pc in countersOfCategory)
                    {
                        Console.WriteLine("Category: {0}, counter: {1}", pc.CategoryName, pc.CounterName);
                        pc?.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Parses the <see cref="WindowsPerfCounterSet"/> given by the caller and constructs all counters which match the desired sets.
        /// </summary>
        /// <param name="counterSet">The sets of counters to request</param>
        private void CreateSpecifiedCounters(WindowsPerfCounterSet counterSet)
        {
            // Get some system info first
            Process currentProcess = Process.GetCurrentProcess();

            ulong systemMemoryKilobytes;
            if (!Kernel32.GetPhysicallyInstalledSystemMemory(out systemMemoryKilobytes))
            {
                systemMemoryKilobytes = 4 * 1024 * 1024;
                _logger.Log("Could not fetch accurate system memory statistics; assuming machine has 4Gb installed", LogLevel.Wrn);
            }

            _logger.Log("kernel32 reports the total system memory size as " + systemMemoryKilobytes + "kB", LogLevel.Std);
            //TryCreateCounter("Objects", "Threads", null, "Machine Threads", null, _counters);

            if (counterSet.HasFlag(WindowsPerfCounterSet.BasicLocalMachine))
            {
                TryCreateCounter("Processor", "% Processor Time", "_Total", CommonInstrumentation.Key_Counter_MachineCpuUsage, _counters);
                TryCreateCounter("Memory", "Available KBytes", CommonInstrumentation.Key_Counter_MachineMemoryAvailable, _counters, 100d / systemMemoryKilobytes);
                TryCreateCounter("LogicalDisk", "% Free Space", "_Total", CommonInstrumentation.Key_Counter_MachineFreeDiskSpace, _counters);
                TryCreateCounter("LogicalDisk", "% Disk Time", "_Total", CommonInstrumentation.Key_Counter_MachineDiskUsage, _counters);
                TryCreateCounter("LogicalDisk", "% Disk Read Time", "_Total", CommonInstrumentation.Key_Counter_MachineDiskReadTime, _counters);
                TryCreateCounter("LogicalDisk", "% Disk Write Time", "_Total", CommonInstrumentation.Key_Counter_MachineDiskWriteTime, _counters);
                TryCreateCounter("TCPv4", "Connections Established", CommonInstrumentation.Key_Counter_TCPV4Connections, _counters);
                TryCreateCounter("TCPv6", "Connections Established", CommonInstrumentation.Key_Counter_TCPV6Connections, _counters);
            }

            if (counterSet.HasFlag(WindowsPerfCounterSet.BasicCurrentProcess))
            {
                TryCreateCounter("Process", "% Processor Time", currentProcess, CommonInstrumentation.Key_Counter_ProcessCpuUsage, _counters, 1d / (double)Environment.ProcessorCount);
                TryCreateCounter("Process", "Working Set - Private", currentProcess, CommonInstrumentation.Key_Counter_ProcessMemoryPrivateWorkingSet, _counters, 1d / 1024d);
            }

            if (counterSet.HasFlag(WindowsPerfCounterSet.DotNetClrCurrentProcess))
            {
                TryCreateCounter(".NET CLR Jit", "% Time in Jit", currentProcess, CommonInstrumentation.Key_Counter_ClrTimeInJit, _counters);
                TryCreateCounter(".NET CLR LocksAndThreads", "Contention Rate / sec", currentProcess, CommonInstrumentation.Key_Counter_ClrContentionRate, _counters);
                TryCreateCounter(".NET CLR LocksAndThreads", "# of current logical Threads", currentProcess, CommonInstrumentation.Key_Counter_ClrProcessLogicalThreads, _counters);
                TryCreateCounter(".NET CLR LocksAndThreads", "# of current physical Threads", currentProcess, CommonInstrumentation.Key_Counter_ClrProcessPhysicalThreads, _counters);
                TryCreateCounter(".NET CLR Exceptions", "# of Exceps Thrown / sec", currentProcess, CommonInstrumentation.Key_Counter_ClrExceptionsThrown, _counters);
                TryCreateCounter(".NET CLR Memory", "Large Object Heap size", currentProcess, CommonInstrumentation.Key_Counter_ClrLargeObjectHeapKb, _counters, 1d / 1024d);
                TryCreateCounter(".NET CLR Memory", "% Time in GC", currentProcess, CommonInstrumentation.Key_Counter_ClrTimeInGc, _counters);
                TryCreateCounter(".NET CLR Memory", "# Total committed Bytes", currentProcess, CommonInstrumentation.Key_Counter_ClrTotalCommittedKb, _counters, 1d / 1024d);
                TryCreateCounter(".NET CLR Memory", "Gen 0 heap size", currentProcess, CommonInstrumentation.Key_Counter_ClrGen0HeapSizeKb, _counters, 1d / 1024d);
                TryCreateCounter(".NET CLR Memory", "Gen 1 heap size", currentProcess, CommonInstrumentation.Key_Counter_ClrGen1HeapSizeKb, _counters, 1d / 1024d);
                TryCreateCounter(".NET CLR Memory", "Gen 2 heap size", currentProcess, CommonInstrumentation.Key_Counter_ClrGen2HeapSizeKb, _counters, 1d / 1024d);
                TryCreateCounter(".NET CLR Loading", "Total Appdomains", currentProcess, CommonInstrumentation.Key_Counter_ClrTotalAppdomains, _counters);
                TryCreateCounter(".NET CLR Loading", "Total Classes Loaded", currentProcess, CommonInstrumentation.Key_Counter_ClrTotalClassesLoaded, _counters);
            }
        }

        /// <summary>
        /// Looks up a category name and resolves it to an actual category object using a cache
        /// </summary>
        /// <param name="categoryName"></param>
        /// <returns></returns>
        private PerformanceCounterCategory GetCounterCategory(string categoryName)
        {
            if (_allCounterCategories == null)
            {
                _allCounterCategories = new Dictionary<string, PerformanceCounterCategory>(StringComparer.OrdinalIgnoreCase);

                // populate lazy cache
                foreach (PerformanceCounterCategory category in PerformanceCounterCategory.GetCategories())
                {
                    _allCounterCategories[category.CategoryName] = category;
                }
            }

            PerformanceCounterCategory returnVal;
            if (!_allCounterCategories.TryGetValue(categoryName, out returnVal))
            {
                _logger.Log("Could not find performance counter category \"" + categoryName + "\"", LogLevel.Wrn);
                returnVal = null;
            }

            return returnVal;
        }

        /// <summary>
        /// Gets the list of all counter instances for a particular category, 
        /// </summary>
        /// <param name="categoryName"></param>
        /// <returns></returns>
        private string[] GetCounterInstances(string categoryName)
        {
            if (_allCategoryInstances == null)
            {
                _allCategoryInstances = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            }

            PerformanceCounterCategory actualCategory;
            if (!_allCounterCategories.TryGetValue(categoryName, out actualCategory))
            {
                _logger.Log("Category " + categoryName + " not found", LogLevel.Wrn);
                return null;
            }

            if (!_allCategoryInstances.ContainsKey(categoryName))
            {
                _allCategoryInstances[categoryName] = actualCategory.GetInstanceNames();
            }

            return _allCategoryInstances[categoryName];
        }

        private string GetProcessIdToInstanceMapping(string processName, int processId)
        {
            if (_processIdToInstanceMapping == null)
            {
                _processIdToInstanceMapping = new Dictionary<int, string>();
                PerformanceCounterCategory actualCategory = GetCounterCategory("Process");
                if (actualCategory == null)
                {
                    return null;
                }

                string[] instances = GetCounterInstances(actualCategory.CategoryName);
                if (instances == null || instances.Length == 0)
                {
                    return null;
                }

                foreach (string instance in instances)
                {
                    string thisInstanceProcess = instance;
                    if (thisInstanceProcess.Contains("#"))
                    {
                        thisInstanceProcess = thisInstanceProcess.Substring(0, thisInstanceProcess.IndexOf('#'));
                    }

                    if (string.Equals(thisInstanceProcess, processName, StringComparison.OrdinalIgnoreCase))
                    {
                        PerformanceCounter[] countersOfCategory = actualCategory.GetCounters(instance);
                        foreach (PerformanceCounter pc in countersOfCategory)
                        {
                            try
                            {
                                if (string.Equals("ID Process", pc.CounterName, StringComparison.Ordinal))
                                {
                                    int pid = (int)pc.RawValue;
                                    _processIdToInstanceMapping[pid] = instance;
                                }
                            }
                            finally
                            {
                                pc?.Dispose();
                            }
                        }
                    }
                }
            }

            string returnVal;
            if (!_processIdToInstanceMapping.TryGetValue(processId, out returnVal))
            {
                returnVal = null;
            }

            return returnVal;
        }

        private string GetFullCounterPathNonInstanced(string categoryName, string counterName)
        {
            // _logger.Log("Accessing windows perf counter " + categoryName + ":" + counterName + (string.IsNullOrEmpty(instanceName) ? string.Empty : ":" + instanceName), LogLevel.Vrb);
            PerformanceCounterCategory actualCategory = GetCounterCategory(categoryName);

            if (actualCategory == null)
            {
                return null;
            }

            PerformanceCounter actualCounter = null;
            try
            {
                string[] instances = GetCounterInstances(actualCategory.CategoryName);

                if (instances == null || instances.Length > 0)
                {
                    _logger.Log("Cannot fetch non-instanced value of instanced counter " + counterName, LogLevel.Wrn);
                    return null;
                }

                PerformanceCounter[] countersOfCategory = actualCategory.GetCounters();
                foreach (PerformanceCounter pc in countersOfCategory)
                {
                    if (string.Equals(counterName, pc.CounterName))
                    {
                        actualCounter = pc;
                    }
                    else
                    {
                        pc?.Dispose();
                    }
                }

                if (actualCounter == null)
                {
                    return null;
                }

                // Convert the handy C# counter object into a native Pdh handle
                // We do this because the C# accessor uses the registry, which allocates and disposes
                // of registry handles on each query to the counter. Since we query counters quite
                // frequently, this quickly becomes very wasteful.
                Pdh.PDH_COUNTER_PATH_ELEMENTS pathElements = new Pdh.PDH_COUNTER_PATH_ELEMENTS()
                {
                    szObjectName = actualCounter.CategoryName,
                    szCounterName = actualCounter.CounterName,
                    szInstanceName = string.IsNullOrEmpty(actualCounter.InstanceName) ? null : actualCounter.InstanceName,
                    szMachineName = null,
                    szParentInstance = null,
                };

                uint maxPathSize = Pdh.PDH_MAX_COUNTER_PATH;
                _counterNameBuilder.Clear();
                _err = Pdh.PdhMakeCounterPath(pathElements, _counterNameBuilder, ref maxPathSize, Pdh.PDH_PATH.PDH_PATH_DEFAULT);

                if (_err != Win32Error.ERROR_SUCCESS)
                {
                    _logger.Log("Failed to access system performance counter " + categoryName + ":" + counterName + ". Error " + _err.ToString(), LogLevel.Wrn);
                    return null;
                }

                return _counterNameBuilder.ToString();
            }
            finally
            {
                actualCounter?.Dispose();
            }
        }

        private string GetFullCounterPathGenericInstanced(string categoryName, string counterName, string instanceName)
        {
            // _logger.Log("Accessing windows perf counter " + categoryName + ":" + counterName + (string.IsNullOrEmpty(instanceName) ? string.Empty : ":" + instanceName), LogLevel.Vrb);
            PerformanceCounterCategory actualCategory = GetCounterCategory(categoryName);

            if (actualCategory == null)
            {
                return null;
            }

            PerformanceCounter actualCounter = null;
            try
            {
                string[] instances = GetCounterInstances(actualCategory.CategoryName);

                if (instances == null || instances.Length == 0)
                {
                    _logger.Log("Cannot fetch instanced value of non-instanced counter " + counterName, LogLevel.Wrn);
                    return null;
                }

                Array.Sort(instances);
                if (string.IsNullOrEmpty(instanceName))
                {
                    _logger.Log("Cannot fetch non-instanced value of instanced counter " + counterName, LogLevel.Wrn);
                    return null;
                }

                foreach (string instance in instances)
                {
                    if (!string.Equals(instance, instanceName))
                    {
                        continue;
                    }

                    if (actualCounter != null)
                    {
                        break;
                    }

                    if (actualCategory.InstanceExists(instance))
                    {
                        PerformanceCounter[] countersOfCategory = actualCategory.GetCounters(instance);
                        foreach (PerformanceCounter pc in countersOfCategory)
                        {
                            if (string.Equals(counterName, pc.CounterName))
                            {
                                actualCounter = pc;
                            }
                            else
                            {
                                pc?.Dispose();
                            }
                        }
                    }
                }

                if (actualCounter == null)
                {
                    return null;
                }

                // Convert the handy C# counter object into a native Pdh handle
                // We do this because the C# accessor uses the registry, which allocates and disposes
                // of registry handles on each query to the counter. Since we query counters quite
                // frequently, this quickly becomes very wasteful.
                Pdh.PDH_COUNTER_PATH_ELEMENTS pathElements = new Pdh.PDH_COUNTER_PATH_ELEMENTS()
                {
                    szObjectName = actualCounter.CategoryName,
                    szCounterName = actualCounter.CounterName,
                    szInstanceName = string.IsNullOrEmpty(actualCounter.InstanceName) ? null : actualCounter.InstanceName,
                    szMachineName = null,
                    szParentInstance = null,
                };

                uint maxPathSize = Pdh.PDH_MAX_COUNTER_PATH;
                _counterNameBuilder.Clear();
                _err = Pdh.PdhMakeCounterPath(pathElements, _counterNameBuilder, ref maxPathSize, Pdh.PDH_PATH.PDH_PATH_DEFAULT);

                if (_err != Win32Error.ERROR_SUCCESS)
                {
                    _logger.Log("Failed to access system performance counter " + categoryName + ":" + counterName + (string.IsNullOrEmpty(instanceName) ? string.Empty : ":" + instanceName) + ". Error " + _err.ToString(), LogLevel.Wrn);
                    return null;
                }

                return _counterNameBuilder.ToString();
            }
            finally
            {
                actualCounter?.Dispose();
            }
        }

        private string GetFullCounterPathProcessIdInstance(string categoryName, string counterName, Process processInfo)
        {
            string instanceName = GetProcessIdToInstanceMapping(processInfo.ProcessName, processInfo.Id);
            if (string.IsNullOrEmpty(instanceName))
            {
                _logger.Log("Failed to resolve process info \"" + processInfo.ProcessName + ":" + processInfo.Id + "\" into a counter instance", LogLevel.Wrn);
                return null;
            }

            return GetFullCounterPathGenericInstanced(categoryName, counterName, instanceName);
        }

        /// <summary>
        /// Attempts to create a single counter wrapper and add it to the given counter set.
        /// If the specified counter doesn't exist or is of the incorrect type, this returns false.
        /// </summary>
        /// <param name="categoryName"></param>
        /// <param name="counterName"></param>
        /// <param name="durandalMetricName"></param>
        /// <param name="target"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        private bool TryCreateCounter(
            string categoryName,
            string counterName,
            string durandalMetricName,
            IList<PerfCounterWrapper> target,
            double scale = 1)
        {
            string fullCounterName = GetFullCounterPathNonInstanced(categoryName, counterName);
            if (fullCounterName != null)
            {
                return TryCreateCounterInternal(fullCounterName, durandalMetricName, target, scale);
            }
            else
            {
                _logger.Log("Failed to access system performance counter " + categoryName + ":" + counterName, LogLevel.Wrn);
                return false;
            }
        }

        /// <summary>
        /// Attempts to create a single counter wrapper and add it to the given counter set.
        /// If the specified counter doesn't exist or is of the incorrect type, this returns false.
        /// </summary>
        /// <param name="categoryName"></param>
        /// <param name="counterName"></param>
        /// <param name="instanceName"></param>
        /// <param name="durandalMetricName"></param>
        /// <param name="target"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        private bool TryCreateCounter(
            string categoryName,
            string counterName,
            string instanceName,
            string durandalMetricName,
            IList<PerfCounterWrapper> target,
            double scale = 1)
        {
            string fullCounterName = GetFullCounterPathGenericInstanced(categoryName, counterName, instanceName);
            if (fullCounterName != null)
            {
                return TryCreateCounterInternal(fullCounterName, durandalMetricName, target, scale);
            }
            else
            {
                _logger.Log("Failed to access system performance counter " + categoryName + ":" + counterName + (string.IsNullOrEmpty(instanceName) ? string.Empty : ":" + instanceName), LogLevel.Wrn);
                return false;
            }
        }

        /// <summary>
        /// Attempts to create a single counter wrapper and add it to the given counter set.
        /// If the specified counter doesn't exist or is of the incorrect type, this returns false.
        /// </summary>
        /// <param name="categoryName"></param>
        /// <param name="counterName"></param>
        /// <param name="processInstanceInfo"></param>
        /// <param name="durandalMetricName"></param>
        /// <param name="target"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        private bool TryCreateCounter(
            string categoryName,
            string counterName,
            Process processInstanceInfo,
            string durandalMetricName,
            IList<PerfCounterWrapper> target,
            double scale = 1)
        {
            string fullCounterName = GetFullCounterPathProcessIdInstance(categoryName, counterName, processInstanceInfo);
            if (fullCounterName != null)
            {
                return TryCreateCounterInternal(fullCounterName, durandalMetricName, target, scale);
            }
            else
            {
                _logger.Log("Failed to access system performance counter " + categoryName + ":" + counterName + ":" + processInstanceInfo.ProcessName + ":pid#" + processInstanceInfo.Id, LogLevel.Wrn);
                return false;
            }
        }

        private static double ConstantOneScale() { return 1; }

        /// <summary>
        /// Attempts to create an actual low-level counter instance given a fully resolved counter name
        /// </summary>
        /// <param name="fullCounterName">The fully resolved counter name</param>
        /// <param name="durandalMetricName">The name of the Durandal metric that this counter will be reported as</param>
        /// <param name="target">The list of perf counters to add the result to</param>
        /// <param name="scale">Counter scale</param>
        /// <returns>True if creation succeeded</returns>
        private bool TryCreateCounterInternal(
            string fullCounterName,
            string durandalMetricName,
            IList<PerfCounterWrapper> target,
            double scale = 1)
        {
            Pdh.SafePDH_HCOUNTER hCounter;
            _err = Pdh.PdhAddEnglishCounter(_hQuery, fullCounterName, IntPtr.Zero, out hCounter);

            if (_err != Win32Error.ERROR_SUCCESS)
            {
                _logger.Log("Failed to access system performance counter " + fullCounterName + ". Error " + _err.ToString(), LogLevel.Wrn);
                hCounter?.Dispose();
                return false;
            }

            PerfCounterWrapper wrapper = new PerfCounterWrapper()
            {
                NativeCounter = hCounter,
                DurandalMetricName = durandalMetricName,
                SystemCounterName = _counterNameBuilder.ToString(),
                Scale = scale
            };

            target.Add(wrapper);
            return true;
        }

        public void InitializeMetrics(IMetricCollector collector)
        {
        }

        private class PerfCounterWrapper
        {
            public Pdh.SafePDH_HCOUNTER NativeCounter;
            public string SystemCounterName;
            public string DurandalMetricName;
            public double Scale;
        }
    }
}
