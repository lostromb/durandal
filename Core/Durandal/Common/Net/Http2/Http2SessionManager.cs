using Durandal.Common.Cache;
using Durandal.Common.Collections;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2
{
    public class Http2SessionManager : IHttp2SessionManager
    {
        private static readonly Http2SessionManager _singleton = new Http2SessionManager();

        /// <summary>
        /// Gets the default globally shared HTTP2 session manager.
        /// </summary>
        public static WeakPointer<IHttp2SessionManager> Default
        {
            get
            {
                return new WeakPointer<IHttp2SessionManager>(_singleton);
            }
        }

        private readonly FastConcurrentDictionary<Http2SessionKey, Queue<Http2Session>> _activeSessions =
            new FastConcurrentDictionary<Http2SessionKey, Queue<Http2Session>>();

        private int _disposed = 0;

        public Http2SessionManager()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~Http2SessionManager()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public async Task<Http2SessionInitiationResult> TryCreateH2Session(
            ISocketFactory socketFactory,
            TcpConnectionConfiguration connectionConfig,
            Http2SessionPreferences sessionPreferences,
            ILogger logger,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            if (!connectionConfig.ReportHttp2Capability)
            {
                // Http2 not supported on client. Just make a regular socket connection.
                ISocket plainSocket = await socketFactory.Connect(connectionConfig, logger, cancelToken, realTime).ConfigureAwait(false);
                return new Http2SessionInitiationResult(plainSocket);
            }

            Http2SessionKey sessionKey = new Http2SessionKey(connectionConfig.UseTLS ? "https" : "http", connectionConfig.DnsHostname, connectionConfig.Port);
           
            // Is there an active session already?
            Queue<Http2Session> sessionQueue;
            _activeSessions.TryGetValueOrSet(sessionKey, out sessionQueue, () => new Queue<Http2Session>());
            lock (sessionQueue)
            {
                while (sessionQueue.Count > 0)
                {
                    Http2Session topSession = sessionQueue.Peek();
                    if (topSession.IsActive)
                    {
                        return new Http2SessionInitiationResult(topSession);
                    }
                    else
                    {
                        sessionQueue.Dequeue();
                    }
                }
            }

            // Initiate a new connection outside of the lock - this is to mitigate the worst-case scenario where we try to establish h2
            // sessions but the server doesn't support it, we would contend fairly heavily on the session locks if they are held here
            ISocket newSocket = await socketFactory.Connect(connectionConfig, logger, cancelToken, realTime).ConfigureAwait(false);

            if (!newSocket.Features.ContainsKey(SocketFeature.NegotiatedHttp2Support))
            {
                // Server doesn't support h2, so just return the plain socket
                return new Http2SessionInitiationResult(newSocket);
            }
            else
            {
                Http2Session newSession;
                lock (sessionQueue)
                {
                    // Check again for the race condition - did another thread establish an h2 session while we were
                    // opening that socket just now?
                    // If so, close the socket we just opened, and reuse the existing session.
                    if (sessionQueue.Count > 0 && sessionQueue.Peek().IsActive)
                    {
                        newSocket.Disconnect(cancelToken, realTime, NetworkDuplex.ReadWrite, allowLinger: true).Forget(logger);
                        return new Http2SessionInitiationResult(sessionQueue.Peek());
                    }

                    // Definitely no existing session, so let's finally make a new one on the socket we just opened
                    newSession = new Http2Session(
                        newSocket,
                        logger.CreateTraceLogger(traceId: null, newComponentName: "H2ClientSession"),
                        sessionPreferences,
                        metrics,
                        metricDimensions);

                    sessionQueue.Enqueue(newSession);
                }

                Http2Settings settings = Http2Settings.Default();
                settings.MaxFrameSize = BufferPool<byte>.DEFAULT_BUFFER_SIZE;
                settings.InitialWindowSize = sessionPreferences.DesiredGlobalConnectionFlowWindow;

                await newSession.BeginClientSession(
                    cancelToken,
                    realTime,
                    settings,
                    connectionConfig.HostHeaderValue,
                    connectionConfig.UseTLS ? "https" : "http").ConfigureAwait(false);

                return new Http2SessionInitiationResult(newSession);
            }
        }

        /// <inheritdoc />
        public Http2Session CheckForExistingH2Session(
            TcpConnectionConfiguration connectionConfig,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            Http2SessionKey sessionKey = new Http2SessionKey(connectionConfig.UseTLS ? "https" : "http", connectionConfig.DnsHostname, connectionConfig.Port);

            Queue<Http2Session> sessionQueue;
            if (_activeSessions.TryGetValue(sessionKey, out sessionQueue))
            {
                lock (sessionQueue)
                {
                    while (sessionQueue.Count > 0)
                    {
                        Http2Session topSession = sessionQueue.Peek();
                        if (topSession.IsActive)
                        {
                            return topSession;
                        }
                        else
                        {
                            sessionQueue.Dequeue();
                        }
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<HttpResponse> CreateH2ClientSessionFromUpgrade(
            ISocket socket,
            TcpConnectionConfiguration connectionConfig,
            Http2SessionPreferences sessionPreferences,
            ILogger logger,
            Http2Settings clientSettings,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            Http2SessionKey sessionKey = new Http2SessionKey(HttpConstants.SCHEME_HTTP, connectionConfig.DnsHostname, connectionConfig.Port);

            Http2Session newSession = new Http2Session(
                socket,
                logger.CreateTraceLogger(traceId: null, newComponentName: "H2ClientSession"),
                sessionPreferences,
                metrics,
                metricDimensions);

            // There's a race condition here but we try and mitigate it the best we can.
            // If another thread establishes an H2 session while we are doing this upgrade, we end up with 2 active sessions.
            // But at the same time, we can't close this session and use the other because we have to return a response
            // to the original HTTP1.1. request. So in this worse case we end up with multiple sessions to the same host.
            // Amortized over time, the unused sessions should become inactive, so we assume this is fine.
            Queue<Http2Session> sessionQueue;
            _activeSessions.TryGetValueOrSet(sessionKey, out sessionQueue, () => new Queue<Http2Session>());

            lock (sessionQueue)
            {
                sessionQueue.Enqueue(newSession);
            }

            return await newSession.BeginClientSessionFromHttp1Upgrade(
                cancelToken,
                realTime,
                clientSettings,
                connectionConfig.HostHeaderValue,
                HttpConstants.SCHEME_HTTP).ConfigureAwait(false);
        }

        /// <summary>
        /// Used by unit tests to clear state in between runs, ensuring we have no long-running connections between test cases.
        /// </summary>
        public async Task ShutdownAllActiveSessions()
        {
            foreach (KeyValuePair<Http2SessionKey, Queue<Http2Session>> sessionQueue in _activeSessions)
            {
                foreach (Http2Session session in sessionQueue.Value)
                {
                    await session.Shutdown(Http2ErrorCode.NoError).ConfigureAwait(false);
                }
            }

            _activeSessions.Clear();
        }

        /// <inheritdoc/>
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
                ShutdownAllActiveSessions().Await();
            }
        }
    }
}
