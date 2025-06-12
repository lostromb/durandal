using Durandal.Common.Logger;
using Durandal.Common.File;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.IO;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Net
{
    /// <summary>
    /// A socket factory that provides virtual "connections" directly to a socket server delegate running locally.
    /// </summary>
    public class DirectSocketFactory : ISocketFactory
    {
        private readonly ILogger _logger;
        private readonly ISocketServerDelegate _targetServer;
        private readonly IThreadPool _serverSocketThreadPool;
        private readonly CancellationTokenSource _cancelToken;
        private int _disposed = 0;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="server"></param>
        /// <param name="logger"></param>
        /// <param name="serverThreadPool"></param>
        public DirectSocketFactory(ISocketServerDelegate server, ILogger logger, IThreadPool serverThreadPool)
        {
            _targetServer = server;
            _serverSocketThreadPool = serverThreadPool;
            _logger = logger;
            _cancelToken = new CancellationTokenSource();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~DirectSocketFactory()
        {
            Dispose(false);
        }
#endif

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public Task<ISocket> Connect(
            string hostname,
            int port,
            bool secure,
            ILogger traceLogger = null, 
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            traceLogger = traceLogger ?? _logger;

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            IRealTimeProvider threadLocalRealTime = realTime.Fork("DirectSocketThread");
            ServerBindingInfo serverBinding = new ServerBindingInfo("localhost", port: null);

            // Spin off the server processing to a separate task
            _serverSocketThreadPool.EnqueueUserAsyncWorkItem(async () =>
            {
                try
                {
                    await _targetServer.HandleSocketConnection(socketPair.ServerSocket, serverBinding, _cancelToken.Token, threadLocalRealTime).ConfigureAwait(false);
                }
                finally
                {
                    traceLogger.Log("Closed server-side direct socket", LogLevel.Vrb);
                    socketPair.ServerSocket.Dispose();
                    threadLocalRealTime.Merge();
                }
            });

            return Task.FromResult<ISocket>(socketPair.ClientSocket);
        }

        public Task<ISocket> Connect(
            TcpConnectionConfiguration connectionConfig,
            ILogger traceLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            return Connect(null, 0, false, traceLogger, cancelToken, realTime);
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
                _cancelToken.Cancel();
                _cancelToken.Dispose();
            }
        }
    }
}
