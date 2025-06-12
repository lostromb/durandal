using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    public class NamedPipeServer : ISocketServer
    {
        private readonly string _pipeName;
        private readonly int _numThreads;
        private readonly ILogger _logger;
        private bool _running = false;
        private Queue<PipeServerThread> _listenThreads;
        private readonly IRealTimeProvider _realTime;
        private ISocketServerDelegate _serverDelegate = null;
        private int _disposed = 0;

        public NamedPipeServer(ILogger logger, string pipeName, int numThreads = 1)
        {
            _logger = logger;
            _numThreads = numThreads;
            _pipeName = pipeName;
            _realTime = DefaultRealTimeProvider.Singleton;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~NamedPipeServer()
        {
            Dispose(false);
        }
#endif

        public IEnumerable<ServerBindingInfo> Endpoints => new ServerBindingInfo[]
            {
                new ServerBindingInfo(_pipeName, port: null)
            };

        public bool Running => _running;

        public void RegisterSubclass(ISocketServerDelegate subclass)
        {
            _serverDelegate = subclass;
        }

        public async Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_running)
            {
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                throw new InvalidOperationException("Server is already running!");
            }

            _logger.Log("Starting named pipe server on pipe " + _pipeName);
            _listenThreads = new Queue<PipeServerThread>();
            SpawnNewListener();

            _running = true;
            return true;
        }

        private void SpawnNewListener()
        {
            // Prune old listeners first (?)
#pragma warning disable CA2000 // Dispose objects before losing scope
            PipeServerThread newListener = new PipeServerThread(_logger.Clone("NamedPipeServer"), _pipeName, _serverDelegate, _numThreads, SpawnNewListener, _realTime);
#pragma warning restore CA2000 // Dispose objects before losing scope
            _listenThreads.Enqueue(newListener);
            newListener.Start();
        }

        public Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_running)
            {
                _logger.Log("Stopping named pipe server on pipe " + _pipeName);
                _logger.Log("Stopping a named pipe server is not actually implemented yet", LogLevel.Wrn);
                //await _listenThread.Stop();
                //_listenThread.Dispose();
                _running = false;
            }

            return DurandalTaskExtensions.NoOpTask;
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
                StopServer(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
            }
        }

        private class PipeServerThread : IDisposable
        {
            private readonly ILogger _logger;
            private readonly string _pipeName;
            private readonly int _numThreads;
            private readonly ISocketServerDelegate _serverDelegate;
            private readonly Action _spawnNewListenerFunc;
            private readonly IRealTimeProvider _realTime;
            private NamedPipeServerStream _pipe;
            private int _disposed = 0;

            public PipeServerThread(ILogger logger, string pipeName, ISocketServerDelegate serverDelegate, int numThreads, Action spawnNewListenerFunc, IRealTimeProvider realTime)
            {
                _logger = logger;
                _numThreads = numThreads;
                _pipeName = pipeName;
                _serverDelegate = serverDelegate;
                _spawnNewListenerFunc = spawnNewListenerFunc;
                _realTime = realTime;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

            public void Start()
            {
                DurandalTaskExtensions.LongRunningTaskFactory.StartNew(Run);
            }

            private async Task Run()
            {
                ServerBindingInfo serverBinding = new ServerBindingInfo(_pipeName, port: null);
                IRealTimeProvider threadLocalTime = _realTime.Fork("NamedPipeServerThread");
                try
                {
                    _logger.Log("Starting to listen on pipe " + _pipeName);
                    _pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, _numThreads, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);
                    _pipe.WaitForConnection();

                    // Immediately start another background listener to wait for future client requests
                    _spawnNewListenerFunc();

                    try
                    {
                        _logger.Log("Got new named pipe connection", LogLevel.Vrb);
                        using (NamedPipeServerSocket serverSocket = new NamedPipeServerSocket(_pipeName, _pipe))
                        {
                            await _serverDelegate.HandleSocketConnection(serverSocket, serverBinding, CancellationToken.None, threadLocalTime).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        if (_pipe.IsConnected)
                        {
                            _pipe.Disconnect();
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    threadLocalTime.Merge();
                }
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
                    _pipe.Dispose();
                }
            }
        }
    }
}
