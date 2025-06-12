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
    public class NullSocketServer : ISocketServer
    {
        private static readonly ServerBindingInfo[] ENDPOINTS = new ServerBindingInfo[0];
        private int _disposed = 0;

        public NullSocketServer()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public IEnumerable<ServerBindingInfo> Endpoints => ENDPOINTS;

        public bool Running
        {
            get; private set;
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

        public void RegisterSubclass(ISocketServerDelegate subclass) { }

        public Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            Running = true;
            return Task.FromResult(Running);
        }

        public Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            Running = false;
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
