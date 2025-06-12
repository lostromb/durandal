using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Tasks
{
    public enum ThreadPoolOverschedulingBehavior
    {
        /// <summary>
        /// The thread pool will block on enqueue for as long as the pool is over capacity.
        /// If scheduler param is specified, it will be the maximum amount of time to block before allowing the task to begin.
        /// </summary>
        BlockUntilThreadsAvailable,

        /// <summary>
        /// The thread pool will ignore incoming work items if it is at capacity.
        /// </summary>
        ShedExcessWorkItems,

        /// <summary>
        /// The thread pool will block on enqueue if the pool is over capacity, for a time equal to (scheduler param * number of overscheduled threads)
        /// Thus, the throttling will increase linearly in proportion to the saturation level of the pool.
        /// </summary>
        LinearThrottle,

        /// <summary>
        /// The thread pool will block on enqueue if the pool is over capacity, for a time equal to scheduler param * (number of overscheduled threads ^ 2)
        /// Thus, the throttling will increase quadratically in proportion to the saturation level of the pool.
        /// </summary>
        QuadraticThrottle
    }
}
