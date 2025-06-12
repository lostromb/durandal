using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net
{
    /// <summary>
    /// <see cref="IServerSocketFactory"/> implementation which returns <see cref="AnonymousPipeServerSocket"/>.
    /// </summary>
    public class AnonymousPipeServerSocketFactory : IServerSocketFactory
    {
        private const int DEFAULT_BUFFER_SIZE = 256 * 1024; // 256Kb default buffer size

        public ISocket CreateServerSocket(ILogger logger, WeakPointer<IMetricCollector> metrics, DimensionSet metricDimensions, int minimumBufferSizeBytes = -1)
        {
            if (minimumBufferSizeBytes <= 0)
            {
                minimumBufferSizeBytes = DEFAULT_BUFFER_SIZE;
            }

            return new AnonymousPipeServerSocket(minimumBufferSizeBytes);
        }
    }
}
