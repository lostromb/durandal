using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.Instrumentation.Profiling
{
    /// <summary>
    /// Microprofile client which writes to a memory stream.
    /// This should only ever really be used for unit testing, otherwise you will run out of memory fast.
    /// </summary>
    public class MemoryMicroProfilerClient : StreamMicroProfilerClient
    {
        public MemoryMicroProfilerClient() : base(new MemoryStream())
        {
        }

        public MemoryStream BaseStream
        {
            get
            {
                return _stream as MemoryStream;
            }
        }
    }
}
