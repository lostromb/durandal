using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Flags which specify different sets of performance counters that can be enabled/disabled in a <see cref="WindowsPerfCounterReporter" />.
    /// </summary>
    [Flags]
    public enum WindowsPerfCounterSet : ulong
    {
        None = 0x0,

        /// <summary>
        /// Counters for CPU, memory, TCP, and disk usage for the entire machine
        /// </summary>
        BasicLocalMachine = 0x1 << 0,
        
        /// <summary>
        /// Counters for CPU and memory for the current process only
        /// </summary>
        BasicCurrentProcess = 0x1 << 1,

        /// <summary>
        /// .Net CLR statistics for the current process - garbage collection, exceptions, heaps, threads, contention, etc.
        /// </summary>
        DotNetClrCurrentProcess = 0x1 << 2,
    }
}
