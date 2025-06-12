using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT
{
    public class ValidationResponse
    {
        public bool ValidationPassed { get; set; }
        public string FailureReason { get; set; }
    }
}
