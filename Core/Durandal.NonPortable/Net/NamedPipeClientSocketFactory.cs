using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    public class NamedPipeClientSocketFactory : ISocketFactory
    {
        private int _disposed = 0;

        public NamedPipeClientSocketFactory()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~NamedPipeClientSocketFactory()
        {
            Dispose(false);
        }
#endif

        public Task<ISocket> Connect(
            string hostname,
            int port,
            bool secure,
            ILogger traceLogger = null, 
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            return Task.FromResult<ISocket>(new NamedPipeClientSocket(hostname));
        }

        public Task<ISocket> Connect(
            TcpConnectionConfiguration connectionConfig,
            ILogger traceLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            return Task.FromResult<ISocket>(new NamedPipeClientSocket(connectionConfig.DnsHostname));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
            }
        }
    }
}
