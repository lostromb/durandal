using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Monitoring
{
    /// <summary>
    /// Simple test result as reported by a single test run.
    /// This is the "public" version of this schema that is made available to monitor implementations.
    /// </summary>
    public class SingleTestResult
    {
        /// <summary>
        /// Indicates if this execution of the test resulted in a success.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// If the test failed, this supplies the failure message to be reported to the driver.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// If there is a case where a test requires extra time, for example to set up preconditions or do clean up afterwards, the test
        /// has the option of reporting its actual latency itself and then reporting it to the driver with this field. Keep in mind
        /// that this will not affect things like non-cooperative drivers where tests are forcibly canceled after a certain wallclock time.
        /// </summary>
        public TimeSpan? OverrideTestExecutionTime { get; set; }
    }
}
