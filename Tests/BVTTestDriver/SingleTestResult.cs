using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BVTTestDriver
{
    public class SingleTestResult
    {
        public string Domain;
        public TestResultCode ResultCode;
        public long Latency;
        public TestUtterance FailedInput;
        public string ActualDomainIntent;
        public bool ContainsTags;
        public float TaggerPrecision;
        public float TaggerRecall;
    }
}
