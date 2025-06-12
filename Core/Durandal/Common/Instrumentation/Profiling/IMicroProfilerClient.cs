using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Instrumentation.Profiling
{
    public interface IMicroProfilerClient : IDisposable
    {
        void SendProfilingData(byte[] data, int offset, int count);
        void Flush();
    }
}
