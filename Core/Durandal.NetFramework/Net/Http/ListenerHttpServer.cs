using Durandal.Common.Tasks;

namespace Durandal.Common.Net.Http
{
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System;

    using Durandal.Common.Logger;
    using System.IO;
    using System.Collections.Generic;
    using Utils;
    using System.Threading.Tasks;
    using System.Text.RegularExpressions;
    using Durandal.Common.Time;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// A class that enables the ability to serve HTTP requests abstractly.
    /// You cannot inherit from this class; because of inversion-of-control, you have to create an IHttpServerDelegate class
    /// and then pass if to this instance's RegisterSubclass function. This allows the superclass to be abstract and the subclass to be common,
    /// which allows for better portability.
    /// This one is based upon System.Net.HttpListener, which allows for more robust connections and SSL,
    /// however, it comes at the cost of either configuring your server beforehand or requiring administrative privileges.
    /// </summary>
    public sealed class ListenerHttpServer : IHttpServer
    {
        private static readonly Regex UrlParseRegex = new Regex("(https?)://.+?:([0-9]+)");

        private readonly HttpListener _listener;
        private readonly ILogger _logger;
        private readonly IList<ServerBindingInfo> _endpoints;
        private readonly WeakPointer<IThreadPool> _threadPool;
        private readonly Uri _localAccessUri;

        // Used to signal when the server is fully started and stopped
        private readonly ManualResetEventAsync _startedSignal = new ManualResetEventAsync(false);
        private readonly ManualResetEventAsync _stoppedSignal = new ManualResetEventAsync(false);

        private CancellationTokenSource _cancellationSource;
        private Task _listenThread;
        private bool _startedOK = false;
        private IHttpServerDelegate _subclass;
        private int _disposed = 0;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints">The list of endpoints to listen on. For example, "http://*:80".
        /// Multiple ports and protocols can be used simultaneously</param>
        /// <param name="logger">A logger</param>
        /// <param name="threadPool">A thead pool to use for handling requests.</param>
        public ListenerHttpServer(IEnumerable<ServerBindingInfo> endpoints, ILogger logger, WeakPointer<IThreadPool> threadPool)
        {
            _listener = new HttpListener();
            _logger = logger;
            _threadPool = threadPool.AssertNonNull(nameof(threadPool));
            _endpoints = new List<ServerBindingInfo>(endpoints);
            _localAccessUri = HttpHelpers.FindBestLocalAccessUrl(_endpoints);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ListenerHttpServer()
        {
            Dispose(false);
        }
#endif

        public Uri LocalAccessUri
        {
            get
            {
                return _localAccessUri;
            }
        }

        /// <inheritdoc />
        public async Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_subclass == null)
            {
                throw new InvalidOperationException("Cannot start an HttpListenerServer without registering a subclass first");
            }

            _cancellationSource = new CancellationTokenSource();
            _listenThread = RunServerThread(_cancellationSource.Token);
            await _startedSignal.WaitAsync().ConfigureAwait(false);
            return _startedOK;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                if (_startedOK)
                {
                    _cancellationSource.Cancel();
                    Thread.Sleep(1000);
                    _listener.Abort();
                    _stoppedSignal.Wait();
                    //_listener.Dispose(); // I don't know what's going on, HttpListener is supposed to be disposable, but it's not.
                }

                _cancellationSource?.Dispose();
            }
        }

        public Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            Dispose();
            return DurandalTaskExtensions.NoOpTask;
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
                return _startedSignal.IsSet && !_stoppedSignal.IsSet;
            }
        }

        private async Task RunServerThread(CancellationToken cancelToken)
        {
            _startedOK = true;
            await Task.Yield();

            try
            {
                _listener.IgnoreWriteExceptions = true;
                _listener.Start();

                if (_endpoints.Count == 0)
                {
                    _logger.Log("No endpoints were specified for the HTTP server", LogLevel.Err);
                }
                else
                {
                    List<string> expandedHostnames = new List<string>();
                    foreach (ServerBindingInfo endpoint in _endpoints)
                    {
                        expandedHostnames.Clear();
                        if (endpoint.IsWildcardEndpoint)
                        {
                            // this is dumb but it's how the listener server works apparently
                            expandedHostnames.Add("*");
                            expandedHostnames.Add("localhost");
                            expandedHostnames.Add("127.0.0.1");
                        }
                        else
                        {
                            expandedHostnames.Add(endpoint.LocalIpEndpoint);
                        }

                        foreach (string hostName in expandedHostnames)
                        {
                            string alteredEndpoint;
                            if (endpoint.UseTls)
                            {
                                alteredEndpoint = string.Format("https://{0}:{1}/", hostName, endpoint.LocalIpPort.GetValueOrDefault(443));
                            }
                            else
                            {
                                alteredEndpoint = string.Format("http://{0}:{1}/", hostName, endpoint.LocalIpPort.GetValueOrDefault(80));
                            }

                            _logger.Log("Binding Listener HTTP server to " + alteredEndpoint, LogLevel.Std);
                            try
                            {
                                _listener.Prefixes.Add(alteredEndpoint);
                            }
                            catch (HttpListenerException e)
                            {
                                _logger.Log("FAILED to bind " + alteredEndpoint + " (you may have to run as administrator, or else the listening port is already in use)", LogLevel.Err);
                                _logger.Log(e, LogLevel.Err);
                                _startedOK = false;
                            }
                        }
                    }
                }

                // Signal server start
                _startedSignal.Set();

                while (!cancelToken.IsCancellationRequested)
                {
                    HttpListenerContext request = await _listener.GetContextAsync().ConfigureAwait(false);
                    NewConnection(request, cancelToken);
                }
            }
            catch (OperationCanceledException) { }
            //catch (HttpListenerException e)
            //{
            //    _logger.Log("Listener exception occurred inside the HTTP server", LogLevel.Err);
            //    _logger.Log(e, LogLevel.Err);
            //    _startedOK = false;
            //}
            //catch (InvalidOperationException e)
            //{
            //    _logger.Log("Invalid operation exception occurred inside the HTTP server", LogLevel.Err);
            //    _logger.Log(e, LogLevel.Err);
            //    _startedOK = false;
            //}
            catch (Exception e)
            {
                _logger.Log(e.GetType().Name + " occurred inside the HTTP server", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
                _startedOK = false;
            }
            finally
            {
                _logger.Log("Shutting down HTTP server");
                try { _listener.Close(); } catch (Exception) { }

                // Signal server stop
                _startedSignal.Set();
                _stoppedSignal.Set();
            }
        }

        private void NewConnection(HttpListenerContext request, CancellationToken cancelToken)
        {
            HttpServerThreadParameter threadParam = new HttpServerThreadParameter()
            {
                HttpContext = request,
                CancelToken = cancelToken
            };

            _threadPool.Value.EnqueueUserAsyncWorkItem(async () => await HandleConnectionInThread(threadParam).ConfigureAwait(false));
        }

        private class HttpServerThreadParameter
        {
            public HttpListenerContext HttpContext { get; set; }
            public CancellationToken CancelToken { get; set; }
        }

        private async Task HandleConnectionInThread(object threadParam)
        {
            HttpServerThreadParameter clientRequest = threadParam as HttpServerThreadParameter;
            if (clientRequest != null)
            {
                await HandleConnection(clientRequest.HttpContext, clientRequest.CancelToken).ConfigureAwait(false);
            }
        }

        private async Task HandleConnection(HttpListenerContext listenerContext, CancellationToken cancelToken)
        {
            try
            {
                if (listenerContext == null)
                {
                    _logger.Log("Could not respond to HTTP request. Request is null or invalid", LogLevel.Err);
                }
                else
                {
                    using (ListenerHttpServerContext durandalContext = new ListenerHttpServerContext(listenerContext, DefaultRealTimeProvider.Singleton))
                    {
                        _logger.Log(string.Format("Got an HTTP request {0} from {1}", durandalContext.HttpRequest.DecodedRequestFile, listenerContext.Request.RemoteEndPoint), LogLevel.Vrb);
                        await _subclass.HandleConnection(durandalContext, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                        // If the server delegate did not generate a response, return HTTP 500.
                        if (!durandalContext.PrimaryResponseStarted)
                        {
                            listenerContext.Response.StatusCode = 500;
                            listenerContext.Response.ContentType = HttpConstants.MIME_TYPE_UTF8_TEXT;
                            byte[] errorMessage = StringUtils.UTF8_WITHOUT_BOM.GetBytes("The server implementation did not generate a response");
                            listenerContext.Response.OutputStream.Write(errorMessage, 0, errorMessage.Length);
                        }
                    }
                }
            }
            catch (HttpListenerException e)
            {
                _logger.Log("An HttpListenerException arose in the web server", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
            catch (IOException e)
            {
                _logger.Log("An IOException arose in the web server", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
            catch (Exception e)
            {
                _logger.Log("An unhandled exception arose in the web server", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
            finally
            {
                try
                {
                    listenerContext.Response.Close();
                }
                catch (Exception e)
                {
                    _logger.Log("An unhandled exception arose while sending the HTTP response", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
            }
        }

        public void RegisterSubclass(IHttpServerDelegate subclass)
        {
            _subclass = subclass;
        }
    }
}
