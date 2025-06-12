using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Tasks
{
    public class BatchedDataProcessorConfig
    {
        /// <summary>
        /// The number of events to be treated as a single batch
        /// </summary>
        public int BatchSize { get; set; }

        /// <summary>
        /// The desired interval between batch processes
        /// </summary>
        public TimeSpan DesiredInterval { get; set; }

        /// <summary>
        /// >The minimum backoff time to wait after a failed batch process
        /// </summary>
        public TimeSpan MinimumBackoffTime { get; set; }

        /// <summary>
        /// The maximum backoff time to wait after a failed batch process
        /// </summary>
        public TimeSpan MaximumBackoffTime { get; set; }

        /// <summary>
        /// The maximum number of events to store in a backlog before culling them (or blocking on ingest)
        /// </summary>
        public int MaxBacklogSize { get; set; }

        /// <summary>
        /// Whether or not to allow ingested items to be dropped. Setting this to false can cause threads to block while they wait for a full queue to drain.
        /// </summary>
        public bool AllowDroppedItems { get; set; }

        /// <summary>
        /// The numer of simultaneous calls that can be made to the Process() method
        /// </summary>
        public int MaxSimultaneousProcesses { get; set; }

        //int batchSize = 100,
        //    int maxBacklogSize = 1000,
        //    int maxSimultaneousProcesses = 8,
        //    int desiredIntervalMs = 60000,
        //    int minBackoffTimeMs = 1000,
        //    int maxBackoffTimeMs = 300000,
        //    bool allowDroppedWorkItems = true
    }
}
