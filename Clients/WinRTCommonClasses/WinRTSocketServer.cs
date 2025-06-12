
/// <summary>
/// FIXME: In some configurations, AcceptAsync() does not return immediately, instead it waits 1 second before it dispatches the request to the delegate.
/// I believe this is because it waiting for additional data to be written to its buffer but then is timing out.
/// </summary>
namespace DurandalWinRT
{
    using System.Net;
    using System.Threading;
    using System;
    using Durandal.Common.Tasks;
    using Durandal.Common.Logger;
    using Durandal.Common.Utils;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Durandal.Common.Net;
    using Windows.Networking.Sockets;
    using Durandal.Common.Time;

    public class WinRTSocketServer : ISocketServer
    {
        protected readonly int _portNum;
        private EventWaitHandle newConnectionSignal;
        private TaskFactory _taskFac = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.LongRunning);
        private Task _listenThread;
        private IThreadPool _threadPool;
        private CancellationTokenSource cancellationSource;
        protected readonly ILogger _logger;
        private volatile bool _isRunning;
        private ManualResetEventAsync _startedSignal = new ManualResetEventAsync();
        private ManualResetEventAsync _stoppedSignal = new ManualResetEventAsync();
        private bool _startedOk = false;
        private ISocketServerDelegate _serverImpl;
        private int _disposed = 0;

        public WinRTSocketServer(int port, ILogger logger, IThreadPool requestThreadPool = null)
        {
            _logger = logger;
            _portNum = port;
            newConnectionSignal = new EventWaitHandle(false, EventResetMode.AutoReset);
            _isRunning = false;
            _threadPool = requestThreadPool;
        }

        ~WinRTSocketServer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Starts an HTTP socket server
        /// </summary>
        /// <param name="serverName">The debug name of this server</param>
        public async Task<bool> StartServer(string serverName)
        {
            if (_serverImpl == null)
            {
                throw new InvalidOperationException("Cannot start a socket server because it is still abstract");
            }

            if (_isRunning)
            {
                return true;
            }

            cancellationSource = new CancellationTokenSource();
            _listenThread = _taskFac.StartNew(() => RunServerThread(cancellationSource.Token));
            await _startedSignal.WaitAsync();

            _isRunning = true;
            return _startedOk;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            Durandal.Common.Utils.DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                StopServer();

                // FIXME Should the server have ownership of the thread pool?
                if (_threadPool != null)
                {
                    _threadPool.Dispose();
                }
            }
        }

        public Task StopServer()
        {
            _isRunning = false;
            cancellationSource.Cancel();
            return _stoppedSignal.WaitAsync(new CancellationTokenSource(1000).Token);
        }

        public void RegisterSubclass(ISocketServerDelegate subclass)
        {
            _serverImpl = subclass;
        }

        public IEnumerable<string> Endpoints
        {
            get
            {
                return new string[] { "http://localhost:" + _portNum };
            }
        }

        public bool Running
        {
            get
            {
                return _isRunning;
            }
        }

        private async Task RunServerThread(object cancelToken)
        {
            if (!(cancelToken is CancellationToken))
            {
                _logger.Log("Invalid cancellation token in http server thread", LogLevel.Err);
            }

            CancellationToken token = (CancellationToken)cancelToken;
            try
            {
                using (StreamSocketListener serverSocket = new StreamSocketListener())
                {
                    serverSocket.ConnectionReceived += NewConnection;
                    _logger.Log("Binding HTTP server to localhost:" + _portNum);
                    await serverSocket.BindEndpointAsync(new Windows.Networking.HostName("localhost"), _portNum.ToString());
                    _logger.Log("Binding HTTP server successful");
                    _startedOk = true;
                    _startedSignal.Set();
                    while (!token.IsCancellationRequested)
                    {
                        bool signaled;
                        do
                        {
                            signaled = newConnectionSignal.WaitOne(500);
                        } while (!signaled && !token.IsCancellationRequested);
                    }

                    serverSocket.Dispose();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                _logger.Log("Error: Exception occurred while starting HTTP server. The port may already be in use.", LogLevel.Err);
                _logger.Log(e.Message, LogLevel.Err);
                _startedOk = false;
            }
            finally
            {
                _startedSignal.Set();
                _stoppedSignal.Set();
            }
        }

        private void NewConnection(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            newConnectionSignal.Set();

            if (args.Socket != null)
            {
                if (_threadPool != null)
                {
                    _threadPool.EnqueueUserAsyncWorkItem(
                        new RequestThreadWrapper(
                                new WinRTSocket(args.Socket, _logger.Clone("WinRTSocket")),
                                _serverImpl.HandleSocketConnection
                            ).Run);
                }
                else
                {
                    _serverImpl.HandleSocketConnection(
                        new WinRTSocket(args.Socket, _logger.Clone("WinRTSocket")),
                        CancellationToken.None,
                        DefaultRealTimeProvider.Singleton
                        );
                }
            }
            else
            {
                _logger.Log("NewConnection called with null socket", LogLevel.Err);
            }
        }

        private delegate Task ConnectionHandler(ISocket clientSocket, CancellationToken cancelToken, IRealTimeProvider realTime);

        private class RequestThreadWrapper
        {
            private ISocket _socket;
            private ConnectionHandler _handler;

            public RequestThreadWrapper(ISocket socket, ConnectionHandler handler)
            {
                _socket = socket;
                _handler = handler;
            }
            
            public async Task Run()
            {
                try
                {
                    await _handler(_socket, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }
                catch (Exception) { } // FIXME catch-all error handler just to keep exceptions from percolating to the disposal thread and killing CLR
            }
        }
    }
}
