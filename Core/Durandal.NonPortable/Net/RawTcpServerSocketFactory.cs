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
    /// <see cref="IServerSocketFactory"/> implementation which returns <see cref="RawTcpServerSocket"/>.
    /// </summary>
    public class RawTcpServerSocketFactory : IServerSocketFactory
    {
        public ISocket CreateServerSocket(ILogger logger, WeakPointer<IMetricCollector> metrics, DimensionSet metricDimensions, int minimumBufferSizeBytes = -1)
        {
            return new RawTcpServerSocket(logger.Clone("TcpSocketServer"));
        }
    }
}
