using Durandal.API;
using Durandal.Common.Cache;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http2;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Implementation of HTTP 2.0 protocol operating on a socket
    /// </summary>
    internal class SocketHttpServerContext2_0 : IHttpServerContext
    {
        private readonly WeakPointer<Http2Session> _session;
        private readonly bool _didUpgradeFromHttp11 = false;
        private readonly int _primaryRequestStreamId;
        private readonly ILogger _logger;
        private readonly IHttpServerDelegate _serverImplementation;
        private readonly DateTimeOffset _requestStartTime;
        private IList<IPushPromiseStream> _pushPromiseStreams;

        /// <inheritdoc/>
        public HttpVersion CurrentProtocolVersion => HttpVersion.HTTP_2_0;

        /// <inheritdoc/>
        public bool SupportsWebSocket => false;

        /// <inheritdoc/>
        public bool SupportsServerPush => true;

        /// <inheritdoc/>
        public bool SupportsTrailers { get; private set; }

        /// <inheritdoc/>
        public bool PrimaryResponseStarted { get; private set; }

        /// <inheritdoc/>
        public bool PrimaryResponseFinished { get; private set; }

        /// <inheritdoc/>
        public HttpRequest HttpRequest
        {
            get;
            private set;
        }

        /// <summary>
        /// Normal constructor for Http2.0 socket sessions.
        /// </summary>
        /// <param name="session">The H2 session that this server context is associated with</param>
        /// <param name="incomingRequestStream">The HTTP2 stream which will contain the incoming request data and headers</param>
        /// <param name="logger">A logger</param>
        /// <param name="serverImplementation">The server implementation handling this incoming request -
        /// used for making outgoing push promises, which are seen as virtual "incoming" requests to the local server.</param>
        /// <param name="remoteHost">The remote hostname</param>
        /// <param name="realTime">The current real time</param>
        public SocketHttpServerContext2_0(
            WeakPointer<Http2Session> session,
            WeakPointer<Http2Stream> incomingRequestStream,
            ILogger logger,
            IHttpServerDelegate serverImplementation,
            string remoteHost,
            IRealTimeProvider realTime)
        {
            _session = session.AssertNonNull(nameof(session));
            incomingRequestStream.AssertNonNull(nameof(incomingRequestStream));
            _logger = logger.AssertNonNull(nameof(logger));
            _requestStartTime = realTime.AssertNonNull(nameof(realTime)).Time;
            _pushPromiseStreams = null;
            _serverImplementation = serverImplementation;
            _primaryRequestStreamId = incomingRequestStream.Value.StreamId;

            string baseUrl;
            string urlFragment;
            HttpFormParameters queryParams;
            if (!HttpHelpers.TryParseRelativeUrl(
                incomingRequestStream.Value.RequestPath,
                out baseUrl,
                out queryParams,
                out urlFragment))
            {
                throw new Exception("Can't parse incoming HTTP request url");
            }

            SupportsTrailers = incomingRequestStream.Value.Headers.ContainsValue(
                HttpConstants.HEADER_KEY_TE,
                HttpConstants.HEADER_VALUE_TRAILERS,
                StringComparison.OrdinalIgnoreCase);

            HttpRequest = HttpRequest.CreateIncoming(
                incomingRequestStream.Value.Headers,
                baseUrl,
                incomingRequestStream.Value.RequestMethod,
                remoteHost,
                queryParams,
                urlFragment,
                new HttpContentStreamWrapper(incomingRequestStream.Value.ReadStream, ownsStream: true),
                HttpVersion.HTTP_2_0);
            PrimaryResponseStarted = false;
            PrimaryResponseFinished = false;
        }

        /// <summary>
        /// Constructor for HTTP/2.0 socket sessions that were upgraded from a previous HTTP/1.1 session.
        /// In this case, the incoming request is an ordinary HTTP/1.1 request, but the response will be
        /// written to an H2 session as normal.
        /// </summary>
        /// <param name="session">The H2 session that this server context is associated with</param>
        /// <param name="incomingRequest">The incoming HTTP/1.1 request</param>
        /// <param name="logger">A logger</param>
        /// <param name="serverImplementation">The server implementation handling this incoming request -
        /// used for making outgoing push promises, which are seen as virtual "incoming" requests to the local server.</param>
        public SocketHttpServerContext2_0(
            WeakPointer<Http2Session> session,
            HttpRequest incomingRequest,
            ILogger logger,
            IHttpServerDelegate serverImplementation)
        {
            _session = session.AssertNonNull(nameof(session));
            _logger = logger.AssertNonNull(nameof(logger));
            incomingRequest.AssertNonNull(nameof(incomingRequest));
            _pushPromiseStreams = null;
            _serverImplementation = serverImplementation;
            _primaryRequestStreamId = 1; // Stream ID 1 is hardcoded in the HTTP2 Upgrade spec

            SupportsTrailers = incomingRequest.RequestHeaders.ContainsValue(
                HttpConstants.HEADER_KEY_TE,
                HttpConstants.HEADER_VALUE_TRAILERS,
                StringComparison.OrdinalIgnoreCase);

            HttpRequest = incomingRequest;
            PrimaryResponseStarted = false;
            PrimaryResponseFinished = false;
            _didUpgradeFromHttp11 = true;
        }

        /// <inheritdoc/>
        public Task<IWebSocket> AcceptWebsocketUpgrade(CancellationToken cancelToken, IRealTimeProvider realTime, string subProtocol = null)
        {
            throw new NotSupportedException("Websocket is not yet implemented over HTTP 2.0 protocol. Use HTTP 1.1");
        }

        //public async Task FinishReadingEntireRequest(CancellationToken cancelToken, IRealTimeProvider realTime)
        //{
        //    // TODO: Call into the session, if stream is not finished, send RST_STREAM and close. Then delete local stream.
        //}

        /// <inheritdoc/>
        public Task WritePrimaryResponse(HttpResponse response, ILogger traceLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return WritePrimaryResponse(response, traceLogger, cancelToken, realTime, null, null);
        }

        /// <inheritdoc/>
        public async Task WritePrimaryResponse(
            HttpResponse response,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            IReadOnlyCollection<string> trailerNames,
            Func<string, Task<string>> trailerDelegate)
        {
            if (ReferenceEquals(response.GetOutgoingContentStream(), HttpRequest.GetIncomingContentStream()))
            {
                throw new InvalidOperationException("You can't pipe an HTTP input directly back to HTTP output");
            }

            if (_didUpgradeFromHttp11)
            {
                // We need to handle some housekeeping stuff right here
                // We have to make sure we've cleared the entire HTTP/1.1 request from the pipe;
                // then we tell the HTTP2 session to begin its read thread and start reading HTTP2 frames.
                // This has to be done RIGHT NOW before writing the actual response because, if it's
                // a large response, we need to rely on the client to send window updates before we
                // can actually send it all. So we can't wait until afterwards.
                await CleanupSocketAfterHttp11Upgrade(cancelToken, realTime);
            }

            // Generate instrumentation header
            TimeSpan totalRequestTime = realTime.Time - _requestStartTime;
            response.ResponseHeaders.Add(HttpConstants.HEADER_KEY_SERVER_WORK_TIME, totalRequestTime.PrintTimeSpan());

            try
            {
                PrimaryResponseStarted = true;
                await _session.Value.WriteOutgoingResponse(
                    response,
                    _primaryRequestStreamId,
                    traceLogger,
                    cancelToken,
                    realTime,
                    trailerNames,
                    trailerDelegate).ConfigureAwait(false);
                PrimaryResponseFinished = true;
            }
            finally
            {
                // If the provider of this response got it by making an outgoing request and piping the response to us,
                // then we need to close that response stream here
                if (response.Direction == NetworkDirection.Proxied)
                {
                    await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                }

                response?.Dispose();
                _session.Value.FinishOutgoingResponseStream(_primaryRequestStreamId);
            }

            // Now write all of our push promises in parallel
            if (_pushPromiseStreams != null && _pushPromiseStreams.Count > 0)
            {
                List<Task> promiseWriteTasks = new List<Task>(_pushPromiseStreams.Count);
                foreach (IPushPromiseStream promiseStream in _pushPromiseStreams)
                {
                    IPushPromiseStream promiseStreamClosure = promiseStream;
                    IRealTimeProvider realTimeClosure = realTime.Fork("PushPromiseTask");
                    promiseWriteTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // Simulate a request to the server using a DirectHttpRequestContext (because this is all in-proc)
                            using (HttpRequest request = HttpRequest.CreateOutgoing(promiseStreamClosure.PromisedRequestUrl, promiseStreamClosure.PromisedRequestMethod))
                            {
                                request.RemoteHost = "push.promise";
                                DirectHttpRequestContext directRequestContext = new DirectHttpRequestContext(request);
                                await _serverImplementation.HandleConnection(directRequestContext, cancelToken, realTimeClosure);

                                using (HttpResponse responseFromServer = directRequestContext.ClientResponse)
                                {
                                    _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata,
                                        "Writing outgoing push promise for resource {0}", promiseStreamClosure.PromisedRequestUrl);

                                    await promiseStreamClosure.WritePromiseResponse(
                                        responseFromServer.ResponseHeaders,
                                        200,
                                        responseFromServer.GetOutgoingContentStream(),
                                        _logger,
                                        cancelToken,
                                        realTimeClosure).ConfigureAwait(false);
                                }
                            }
                        }
                        finally
                        {
                            realTimeClosure.Merge();
                        }
                    }));
                }

                // Wait until all promise streams have been written
                foreach (Task t in promiseWriteTasks)
                {
                    await t.ConfigureAwait(false);
                }
            }
        }

        private async Task CleanupSocketAfterHttp11Upgrade(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Clean the entire request pipeline.
            // After the request will come the HTTP2 connection initializer and frames, but the
            // content stream won't read into those. After this point, we are in a full H2 session
            NonRealTimeStream incomingRequestStream = HttpRequest.GetIncomingContentStream();
            if (incomingRequestStream == null)
            {
                throw new NullReferenceException("Somehow the incoming request HTTP request stream got set to null; this shouldn't be possible");
            }

            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
            {
                int readSize = 1;
                while (readSize != 0)
                {
                    readSize = await incomingRequestStream.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length, cancelToken, realTime).ConfigureAwait(false);
                }
            }

            // Tell the session it can finally start its read thread.
            _session.Value.BeginUpgradedServerSessionPart2(realTime);
        }

        /// <inheritdoc/>
        public void PushPromise(
            string expectedRequestMethod,
            string expectedPath,
            HttpHeaders expectedRequestHeaders,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            IPushPromiseStream newStream = _session.Value.InitializePushPromiseStream(
                _primaryRequestStreamId,
                expectedRequestHeaders,
                expectedPath,
                expectedRequestMethod,
                _logger,
                cancelToken,
                realTime);

            // make sure the request stream is non-null
            if (newStream != null && !string.IsNullOrEmpty(newStream.PromisedRequestUrl))
            {
                if (_pushPromiseStreams == null)
                {
                    _pushPromiseStreams = new List<IPushPromiseStream>();
                }

                _pushPromiseStreams.Add(newStream);
            }
        }
    }
}
