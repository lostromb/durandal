using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DurandalNet = Durandal.Common.Net;
using JavaNet = Java.Net;
using Durandal.Common.Utils.Tasks;
using Durandal.Common.Logger;
using System.Threading;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Net;

namespace Durandal.AndroidClient.Common
{
    public class AndroidSocketServer : DurandalNet.ISocketServer
    {
        private List<SocketEndpointListener> _endpointListenThreads;
        private IThreadPool _threadPool;
        private CancellationTokenSource _cancellationSource;
        protected readonly ILogger _logger;
        private volatile bool _isRunning;
        private int _maxConnections;
        private DurandalNet.ISocketServerDelegate _serverImpl;
        private IList<ServerBindingInfo> _endpoints;

        public AndroidSocketServer(IEnumerable<ServerBindingInfo> endpoints,
            ILogger logger,
            IThreadPool requestThreadPool = null)
        {
            _logger = logger;
            _endpoints = new List<ServerBindingInfo>(endpoints);
            _isRunning = false;
            _threadPool = requestThreadPool;
            if (_threadPool == null)
            {
                _maxConnections = 1;
            }
            else
            {
                _maxConnections = 2 * Math.Max(1, _threadPool.ThreadCount);
            }
        }

        /// <summary>
        /// Starts an HTTP socket server
        /// </summary>
        /// <param name="serverName">The debug name of this server</param>
        public async Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_serverImpl == null)
            {
                throw new InvalidOperationException("Cannot start a socket server because it is still abstract");
            }

            bool startedOk = true;
            _cancellationSource = new CancellationTokenSource();
            _endpointListenThreads = new List<SocketEndpointListener>();

            // Kind of a hack for now - we just assume the server can only bind to a particular port once
            HashSet<int> boundPorts = new HashSet<int>();

            foreach (ServerBindingInfo endpoint in _endpoints)
            {
                // Parse the protocol and endpoint
                JavaNet.InetAddress bindingIP;
                int bindingPort = endpoint.LocalIpPort.GetValueOrDefault(0);
                if (endpoint.UseTls)
                {
                    throw new NotImplementedException("SSL is not supported by this server");
                }

                IPAddress parsedAddress;
                if (!endpoint.IsWildcardEndpoint && IPAddress.TryParse(endpoint.LocalIpEndpoint, out parsedAddress))
                {
                    bindingIP = JavaNet.Inet4Address.GetByAddress(parsedAddress.GetAddressBytes());
                }
                else
                {
                    bindingIP = JavaNet.InetAddress.LocalHost;
                }

                if (boundPorts.Contains(bindingPort))
                {
                    _logger.Log("The socket server binding to " + endpoint + " is redundant and will be ignored", LogLevel.Wrn);
                    continue;
                }

                boundPorts.Add(bindingPort);

                _logger.Log("Binding socket server listener to " + endpoint, LogLevel.Vrb);
                SocketEndpointListener listener = new SocketEndpointListener(
                    _cancellationSource.Token,
                    bindingIP,
                    endpoint,
                    _logger,
                    _maxConnections,
                    _serverImpl.HandleSocketConnection,
                    _threadPool,
                    realTime.Fork("SocketListener"));
                _endpointListenThreads.Add(listener);
                startedOk = await listener.Start().ConfigureAwait(false) && startedOk;
            }

            _isRunning = true;
            return startedOk;
        }

        public void Dispose()
        {
            StopServer(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
            if (_threadPool != null)
            {
                _threadPool.Dispose();
            }
            
            _cancellationSource.Dispose();
        }

        public async Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _isRunning = false;
            _cancellationSource.Cancel();
            foreach (SocketEndpointListener listener in _endpointListenThreads)
            {
                await listener.WaitForStop(cancelToken);
            }
        }

        public void RegisterSubclass(DurandalNet.ISocketServerDelegate subclass)
        {
            _serverImpl = subclass;
        }

        public IEnumerable<ServerBindingInfo> Endpoints
        {
            get
            {
                return _endpoints;
            }
        }

        public bool Running
        {
            get
            {
                return _isRunning;
            }
        }

        /// <summary>
        /// This class actually hosts a listener socket on a specific instance of host + port
        /// A virtual server can bing do multiple endpoints and thus can have multiple of these listeners
        /// </summary>
        private class SocketEndpointListener
        {
            private readonly CancellationToken local_cancelToken;
            private readonly ILogger local_logger;
            private readonly JavaNet.InetAddress local_bindingIP;
            private readonly ServerBindingInfo local_bindingInfo;
            private readonly ConnectionHandler local_connectionHandler;
            private readonly int local_maxConnections;
            private readonly IThreadPool local_threadPool;
            private readonly IRealTimeProvider local_realTime;
            
            private readonly ManualResetEventAsync _startedSignal = new ManualResetEventAsync();
            private readonly ManualResetEventAsync _stoppedSignal = new ManualResetEventAsync();
            private bool _startedOk = false;
            private Thread _listenThread;

            public SocketEndpointListener(
                CancellationToken cancelToken,
                JavaNet.InetAddress endpoint,
                ServerBindingInfo bindingInfo,
                ILogger logger,
                int maxConnections,
                ConnectionHandler connectionHandler,
                IThreadPool threadPool,
                IRealTimeProvider realTime)
            {
                local_cancelToken = cancelToken;
                local_bindingIP = endpoint;
                local_bindingInfo = bindingInfo;
                local_logger = logger;
                local_maxConnections = maxConnections;
                local_connectionHandler = connectionHandler;
                local_threadPool = threadPool;
                local_realTime = realTime;
            }

            public async Task<bool> Start()
            {
                _listenThread = new Thread(Run);
                _listenThread.IsBackground = true;
                _listenThread.Name = "SocketListener:" + local_bindingInfo.LocalIpPort.Value;
                _listenThread.Start(local_cancelToken);
                await _startedSignal.WaitAsync().ConfigureAwait(false);
                return _startedOk;
            }

            public async Task WaitForStop(CancellationToken cancelToken)
            {
                await _stoppedSignal.WaitAsync(cancelToken);
            }

            private void Run(object dummy)
            {
                try
                {
                    using (JavaNet.ServerSocket serverSocket = new JavaNet.ServerSocket(local_bindingInfo.LocalIpPort.Value, local_maxConnections, local_bindingIP))
                    {
                        _startedOk = true;
                        _startedSignal.Set();
                        while (!local_cancelToken.IsCancellationRequested)
                        {
                            JavaNet.Socket socket = serverSocket.Accept();

                            if (socket == null)
                            {
                                local_logger.Log("No pool handle. Something is wonky in http socket server", LogLevel.Wrn);
                                continue;
                            }

                            if (socket.IsConnected)
                            {
                                DurandalNet.ISocket socketWrapper = new AndroidSocket(socket);

                                if (local_threadPool != null)
                                {
                                    local_threadPool.EnqueueUserAsyncWorkItem(
                                        new RequestThreadWrapper(
                                            socketWrapper,
                                            local_logger,
                                            local_connectionHandler,
                                            local_cancelToken,
                                            local_bindingInfo,
                                            local_realTime.Fork("SocketServerHandler")
                                        ).Run);
                                }
                                else
                                {
                                    try
                                    {
                                        local_connectionHandler(socketWrapper, local_bindingInfo, local_cancelToken, local_realTime).Await();
                                    }
                                    catch (Exception e)
                                    {
                                        local_logger.Log(e, LogLevel.Err);
                                    }
                                }
                            }
                            else
                            {
                                local_logger.Log("Unexpected error in accept socket. Socket is not connected", LogLevel.Err);
                            }
                        }

                        serverSocket.Close();
                    }
                }
                catch (System.OperationCanceledException) { }
                catch (JavaNet.SocketException e)
                {
                    local_logger.Log("Error: Socket exception occurred while starting HTTP server. The port may already be in use.", LogLevel.Err);
                    local_logger.Log(e, LogLevel.Err);
                    _startedOk = false;
                }
                finally
                {
                    _startedSignal.Set();
                    _stoppedSignal.Set();
                }
            }
        }

        private delegate Task ConnectionHandler(ISocket clientSocket, ServerBindingInfo bindPoint, CancellationToken cancelToken, IRealTimeProvider realTime);

        private class RequestThreadWrapper
        {
            private readonly DurandalNet.ISocket _socket;
            private readonly ConnectionHandler _handler;
            private readonly ILogger _logger;
            private readonly CancellationToken _serverShutdownToken;
            private readonly ServerBindingInfo _bindPoint;
            private readonly IRealTimeProvider _threadLocalTime;

            public RequestThreadWrapper(
                DurandalNet.ISocket socket,
                ILogger logger,
                ConnectionHandler handler,
                CancellationToken cancelToken,
                ServerBindingInfo bindPoint,
                IRealTimeProvider threadLocalTime)
            {
                _socket = socket;
                _handler = handler;
                _logger = logger;
                _serverShutdownToken = cancelToken;
                _bindPoint = bindPoint;
                _threadLocalTime = threadLocalTime;
            }

            public async Task Run()
            {
                try
                {
                    await _handler(_socket, _bindPoint, _serverShutdownToken, _threadLocalTime).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _threadLocalTime.Merge();
                }
            }
        }
    }
}