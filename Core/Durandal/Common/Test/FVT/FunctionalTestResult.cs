using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT
{
    public class FunctionalTestResult
    {
        public List<FunctionalTestTurnResult> TurnResults { get; set; }

        /// <summary>
        /// The sum of actual time spent making dialog requests (as opposed to setting up test identities, waiting between turns, and cleanup)
        /// </summary>
        public TimeSpan ActualTimeSpentInTests { get; set; }
    }
}
