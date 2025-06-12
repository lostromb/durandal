using System;

namespace Durandal.API
{
    /// <summary>
    /// A set of flags that specifies certain capabilities of the Durandal client
    /// </summary>
    [Flags]
    public enum QueryFlags : uint
    {
        None = 0x0,

        /// <summary>
        /// The query is a debugging query and should ideally generate more verbose information
        /// </summary>
        Debug = 0x1 << 0,

        /// <summary>
        /// The query is a tracing query and should return instrumentation data as part of its response
        /// </summary>
        Trace = 0x1 << 1,

        /// <summary>
        /// The query is for monitoring and should not be considered production traffic (meaning it can be deprioritized if needed)
        /// </summary>
        Monitoring = 0x1 << 2,

        /// <summary>
        /// Personally identifiable information should not be logged as a result of this query
        /// </summary>
        NoPII = 0x1 << 3,

        /// <summary>
        /// Do not log anything for this query
        /// </summary>
        LogNothing = 0x1 << 4,

        // <summary>
        // Do not encrypt any PII for this query.
        // </summary>
        //PlaintextPII = 0x1 << 5,
    }
}
