using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    [Flags]
    public enum AudioGraphCapabilities : uint
    {
        /// <summary>
        /// No special capabilities on the audio graph
        /// </summary>
        None = 0x0,

        /// <summary>
        /// Audio reads, audio writes, component connections and disconnections are all guarded by a mutex.
        /// </summary>
        Concurrent = 0x1,

        /// <summary>
        /// Track statistics about inclusive component read / write times, and export the results to
        /// a delegate which could do things like report stutters, track average latencies, etc.
        /// </summary>
        Instrumented = 0x2,
    }
}
