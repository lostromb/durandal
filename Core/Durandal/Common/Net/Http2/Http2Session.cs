using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2.Frames;
using Durandal.Common.Net.Http2.HPack;
using Durandal.Common.Net.Http2.Session;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2
{
    public partial class Http2Session : IDisposable
    {
        private const int RECENT_CANCELED_STREAMS_TO_TRACK = 10;

        /// <summary>
        /// The socket that this entire HTTP/2 session is operating on.
        /// </summary>
        private readonly ISocket _socket;

        /// <summary>
        /// A queue of commands that are dispatched to the FIFO write thread, which controls the outgoing socket
        /// </summary>
        private readonly Http2PriorityQueue _outgoingCommands = new Http2PriorityQueue();

        /// <summary>
        /// Signals (via cancellation) the shutdown of this entire session.
        /// </summary>
        private readonly CancellationTokenSource _sessionShutdown = new CancellationTokenSource();

        /// <summary>
        /// A logger for debugging.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Signaled when we have told the remote peer to close the connection and after everything is closed on our side.
        /// </summary>
        private readonly ManualResetEventAsync _gracefulShutdownSignal = new ManualResetEventAsync();

        /// <summary>
        /// The mapping from stream ID to all active streams in this session.
        /// </summary>
        private readonly FastConcurrentDictionary<int, Http2Stream> _activeStreams;

        /// <summary>
        /// A queue of recently canceled incoming streams, which we use just so we can ignore frames that the remote peer sent before it got our RST_STREAM message.
        /// The queue is used to determine the oldest entry to prune from the hash table below.
        /// </summary>
        private readonly ConcurrentQueue<int> _recentlyCanceledStreamQueue;

        /// <summary>
        /// The set of 10 or so most recently cancelled streams for the purpose of ignoring incoming data frames on those streams.
        /// </summary>
        private readonly FastConcurrentHashSet<int> _recentlyCanceledStreams;

        /// <summary>
        /// A dictionary of recent push promises, keyed by the base URL (that is, URL without any query parameters)
        /// The value is a the set of zero or more promises associated with that base URL.
        /// </summary>
        private readonly FastConcurrentDictionary<string, FastConcurrentHashSet<PushPromiseHeaders>> _pushPromises;


        private readonly ConcurrentQueue<PushPromiseHeaders> _recentPushPromises;

        /// <summary>
        /// Session preferences such as ping interval, timeouts, etc.
        /// Not to be confused with negotiated <see cref="Http2Settings"/>.
        /// </summary>
        private readonly Http2SessionPreferences _sessionPreferences;

        /// <summary>
        /// Stores the incoming flow control window for the connection as a whole.
        /// The size of this window is based on our own preferences, so the remote
        /// client settings shouldn't affect this. We are responsible for
        /// sending window updates to the remote peer when this gets low.
        /// The initial window size setting does not affect this.
        /// </summary>
        private readonly DataTransferWindow _overallConnectionIncomingFlowWindow;

        /// <summary>
        /// Stores the outgoing flow control window for the connection as a whole.
        /// This starts at 65536 credits and only increases when the remote peer
        /// sends window updates on stream 0. The initial window size setting does not affect this.
        /// </summary>
        private readonly DataTransferWindow _overallConnectionOutgoingFlowWindow;

        /// <summary>
        /// Signal which is set when overall connection flow window has credits added to it
        /// </summary>
        private readonly AutoResetEventAsync _overallConnectionOutgoingFlowWindowAvailable;

        /// <summary>
        /// If we are a server, this is the channel which notifies us of the stream IDs of newly incoming requests.
        /// </summary>
        private readonly BufferedChannel<int> _incomingRequestStreamIds;

        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;

        /// <summary>
        /// The server endpoint's HTTP authority string (usually just the host name)
        /// </summary>
        private string _remoteAuthority;

        /// <summary>
        /// If we are a server, this is the authority string of our own host
        /// </summary>
        private string _localAuthority;

        /// <summary>
        /// The current HTTP scheme (https, http...)
        /// </summary>
        private string _scheme;

        /// <summary>
        /// An atomic integer used to track stream IDs initiated by this client.
        /// The true stream ID is this value multiplied by 2, with a +1 or 0 offset depending on whether we are server or client.
        /// </summary>
        private int _nextStreamIndex = 0;

        /// <summary>
        /// Indicates whether this session is alive and able to send frames.
        /// </summary>
        private int _isActive = 0;

        /// <summary>
        /// Indicates whether this end of the session is the client or not.
        /// </summary>
        private bool _isClient;

        /// <summary>
        /// The ID of the stream initiated by the remote peer which we have processed most recently
        /// </summary>
        private int _lastProcessedRemoteStreamId = 0;

        /// <summary>
        /// The background thread which reads from the socket
        /// </summary>
        private Task _readTask;

        /// <summary>
        /// The background thread which writes to the socket
        /// </summary>
        private Task _writeTask;

        /// <summary>
        /// The locally configured settings for this session that have been ACKed by the remote peer
        /// </summary>
        private Http2Settings _localSettings;

        /// <summary>
        /// The locally configured settings for this session
        /// </summary>
        private Http2Settings _localSettingsDesired;

        /// <summary>
        /// The session settings most recently sent by the remote peer.
        /// </summary>
        private Http2Settings _remoteSettings;

        /// <summary>
        /// This field only exists on the client, and is signaled as soon as the server has sent its first
        /// SETTINGS frame. Used for certain cases where we need to check server extensions support before
        /// proceeding.
        /// </summary>
        private ManualResetEventAsync _remoteSentSettingsSignal;

        /// <summary>
        /// Whether this session is disposed.
        /// </summary>
        private int _disposed = 0;

        public Http2Session(
            ISocket socket,
            ILogger logger,
            Http2SessionPreferences preferences,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions)
        {
            _socket = socket.AssertNonNull(nameof(socket));
            _logger = logger.AssertNonNull(nameof(logger));
            _sessionPreferences = preferences.AssertNonNull(nameof(preferences));
            _activeStreams = new FastConcurrentDictionary<int, Http2Stream>();
            _recentlyCanceledStreamQueue = new ConcurrentQueue<int>();
            _recentlyCanceledStreams = new FastConcurrentHashSet<int>(RECENT_CANCELED_STREAMS_TO_TRACK * 2);
            _pushPromises = new FastConcurrentDictionary<string, FastConcurrentHashSet<PushPromiseHeaders>>();
            _recentPushPromises = new ConcurrentQueue<PushPromiseHeaders>();
            _overallConnectionIncomingFlowWindow = new DataTransferWindow(Http2Constants.DEFAULT_INITIAL_WINDOW_SIZE);
            _overallConnectionOutgoingFlowWindow = new DataTransferWindow(Http2Constants.DEFAULT_INITIAL_WINDOW_SIZE);
            _overallConnectionOutgoingFlowWindowAvailable = new AutoResetEventAsync();
            _incomingRequestStreamIds = new BufferedChannel<int>();
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~Http2Session()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Indicates whether this session is alive and able to process new streams.
        /// </summary>
        public bool IsActive => 
            _isActive == 1 &&
            // check the stream ID limit to make sure we can make new ones; default to int.maxValue minus a little wiggle room
            ((_nextStreamIndex * 2) - (_isClient ? 1 : 0)) < _sessionPreferences.MaxStreamId.GetValueOrDefault(int.MaxValue - 10000);

        /// <summary>
        /// Initiates this session in client mode, sending the connection prefix and beginning exchange of settings.
        /// </summary>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="desiredLocalSettings">The client settings to send to the remote peer</param>
        /// <param name="remoteAuthority">The remote authority to use for request headers</param>
        /// <param name="scheme">The connection URL scheme, e.g. "https"</param>
        /// <param name="localSettingsAlreadyApplied">Optional: defines local settings that have already been applied or pre-negotiated before this session.
        /// Only applies to HTTP/1.1 -> 2.0 upgrade for now as settings are passed in a separate header as part of the negotiation.</param>
        /// <returns>An async task</returns>
        public Task BeginClientSession(
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            Http2Settings desiredLocalSettings,
            string remoteAuthority,
            string scheme,
            Http2Settings localSettingsAlreadyApplied = null)
        {
            if (!AtomicOperations.ExecuteOnce(ref _isActive))
            {
                throw new InvalidOperationException("HTTP2 session is already started");
            }

            _isClient = true;
            _remoteAuthority = remoteAuthority.AssertNonNullOrEmpty(nameof(remoteAuthority));
            _scheme = scheme.AssertNonNullOrEmpty(nameof(scheme));

            // Only use the minimum supported settings until the server can ACK that it supports any changes to the default
            _remoteSettings = Http2Settings.ServerDefault();
            _localSettingsDesired = desiredLocalSettings.AssertNonNull(nameof(desiredLocalSettings));
            _localSettings = localSettingsAlreadyApplied ?? Http2Settings.Default();
            _remoteSentSettingsSignal = new ManualResetEventAsync();

            cancelToken.ThrowIfCancellationRequested();

            // Start the connection duplex
            IRealTimeProvider writeThreadTime = realTime.Fork("Http2SessionWrite");
            _writeTask = Task.Run(() => RunWriteThread(_sessionShutdown.Token, writeThreadTime, isClient: true));
            IRealTimeProvider readThreadTime = realTime.Fork("Http2SessionRead");
            _readTask = Task.Run(() => RunReadThread(_sessionShutdown.Token, readThreadTime));

            // Send connection prefix
            _outgoingCommands.Enqueue(new SendClientConnectionPrefixCommand(), HttpPriority.SessionControl);

            // And then send the local settings
            _outgoingCommands.Enqueue(new UpdateLocalSettingsCommand(_localSettingsDesired), HttpPriority.SessionControl);

            BeginPingLoop(_sessionPreferences.OutgoingPingInterval, realTime);

            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_H2_ClientSessionsInitiated, _metricDimensions);

            // We _could_ wait for the server to send settings here and confirm it is alive, but that would also slow down all initial request processing.
            // So we just return here immediately and if the server gives us wrong info, it will be handled later on during the outgoing request.
            return DurandalTaskExtensions.NoOpTask;
        }

        public async Task<HttpResponse> BeginClientSessionFromHttp1Upgrade(
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            Http2Settings localSettings,
            string remoteAuthority,
            string scheme)
        {
            await BeginClientSession(cancelToken, realTime, localSettings, remoteAuthority, scheme, localSettings).ConfigureAwait(false);

            // Make a special Stream 1 which will be used to receive the server's response to the HTTP/1.1 request.
            Http2Stream initialResponseStream = NewStream(1, StreamState.HalfClosedLocal);
            _activeStreams[1] = initialResponseStream;
            _nextStreamIndex++;

            // Wait for the remote peer to send a response
            if (!realTime.IsForDebug)
            {
                cancelToken.ThrowIfCancellationRequested();
                await initialResponseStream.ReceievedHeadersSignal.WaitAsync(cancelToken).ConfigureAwait(false);
            }
            else
            {
                // Unit testing path when we need to consume virtual time
                while (!initialResponseStream.ReceievedHeadersSignal.TryGetAndClear())
                {
                    cancelToken.ThrowIfCancellationRequested();
                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
                }
            }

            if (!initialResponseStream.ResponseStatusCode.HasValue)
            {
                throw new HttpRequestException("HTTP/2 response did not have a response code");
            }

            // Return an HttpResponse which wraps around the returned pipe stream
            return HttpResponse.CreateIncoming(
                initialResponseStream.ResponseStatusCode.Value,
                HttpHelpers.GetStatusStringForStatusCode(initialResponseStream.ResponseStatusCode.Value),
                initialResponseStream.Headers,
                new HttpContentStreamWrapper(initialResponseStream.ReadStream, ownsStream: true),
                new Http2ClientContext(new WeakPointer<Http2Stream>(initialResponseStream), new WeakPointer<Http2Session>(this)));
        }

        /// <summary>
        /// Initiates this session in server mode, sending the connection prefix and beginning exchange of settings.
        /// </summary>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="localSettings">The client settings to send to the remote peer</param>
        /// <param name="localAuthority">The local authority of this server (the hostname that it is running on)</param>
        /// <param name="scheme">The connection URL scheme, e.g. "https"</param>
        /// <param name="remoteSettings">Initial remote settings, if such settings were negotiated prior to the session start.</param>
        /// <returns>An async task</returns>
        public Task BeginServerSession(
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            Http2Settings localSettings,
            string localAuthority,
            string scheme,
            Http2Settings remoteSettings = null)
        {
            if (!AtomicOperations.ExecuteOnce(ref _isActive))
            {
                throw new InvalidOperationException("HTTP2 session is already started");
            }

            _isClient = false;
            _localAuthority = localAuthority.AssertNonNullOrEmpty(nameof(localAuthority));
            _scheme = scheme.AssertNonNullOrEmpty(nameof(scheme));

            // Only use the minimum supported settings until the server can ACK that it supports any changes to the default
            _remoteSettings = remoteSettings ?? Http2Settings.Default();
            _localSettingsDesired = localSettings.AssertNonNull(nameof(localSettings));
            _localSettings = Http2Settings.ServerDefault();
            if (localSettings.EnablePush)
            {
                throw new ArgumentException("Http2 server settings cannot set ENABLE_PUSH = 1");
            }

            cancelToken.ThrowIfCancellationRequested();

            // Start the connection duplex. The read thread will read and validate the client's connection prefix
            IRealTimeProvider writeThreadTime = realTime.Fork("Http2SessionWrite");
            _writeTask = Task.Run(() => RunWriteThread(_sessionShutdown.Token, writeThreadTime, isClient: true));
            IRealTimeProvider readThreadTime = realTime.Fork("Http2SessionRead");
            _readTask = Task.Run(() => RunReadThread(_sessionShutdown.Token, readThreadTime));

            BeginPingLoop(_sessionPreferences.OutgoingPingInterval, realTime);

            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_H2_ServerSessionsInitiated, _metricDimensions);

            // Send the local settings
            _outgoingCommands.Enqueue(new UpdateLocalSettingsCommand(_localSettingsDesired), HttpPriority.SessionControl);
            return DurandalTaskExtensions.NoOpTask;
        }

        /// <summary>
        /// Special initializer when upgrading a server request from HTTP/1.1 to 2.0.
        /// The order has to be:
        /// 1. Initialize the HTTP/2 response stream
        /// 2. Send an outgoing SETTINGS command
        /// 3. Read the entire incoming request body
        /// 4. Begin writing the response to that stream
        /// 5. Read the PRI * connection preface from the client
        /// 6. Enable the full session (read + write threads)
        /// </summary>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="localSettings">The client settings to send to the remote peer</param>
        /// <param name="localAuthority">The local authority of this server (the hostname that it is running on)</param>
        /// <param name="scheme">The connection URL scheme, e.g. "https"</param>
        /// <param name="remoteSettings">Initial remote settings, if such settings were negotiated prior to the session start.</param>
        /// <returns>An async task</returns>
        public void BeginUpgradedServerSessionPart1(
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            Http2Settings localSettings,
            string localAuthority,
            string scheme,
            Http2Settings remoteSettings = null)
        {
            if (!AtomicOperations.ExecuteOnce(ref _isActive))
            {
                throw new InvalidOperationException("HTTP2 session is already started");
            }

            _isClient = false;
            _localAuthority = localAuthority.AssertNonNullOrEmpty(nameof(localAuthority));
            _scheme = scheme.AssertNonNullOrEmpty(nameof(scheme));

            // Only use the minimum supported settings until the server can ACK that it supports any changes to the default
            _remoteSettings = remoteSettings ?? Http2Settings.Default();
            _localSettingsDesired = localSettings.AssertNonNull(nameof(localSettings));
            _localSettings = Http2Settings.ServerDefault();
            if (localSettings.EnablePush)
            {
                throw new ArgumentException("Http2 server settings cannot set ENABLE_PUSH = 1");
            }

            cancelToken.ThrowIfCancellationRequested();

#if HTTP2_DEBUG
            _logger.Log("Remote endpoint has set initial settings", LogLevel.Vrb);
            _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote EnablePush: {0}", _remoteSettings.EnablePush);
            _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote HeaderTableSize: {0}", _remoteSettings.HeaderTableSize);
            _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote InitialWindowSize: {0}", _remoteSettings.InitialWindowSize);
            _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote MaxConcurrentStreams: {0}", _remoteSettings.MaxConcurrentStreams);
            _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote MaxFrameSize: {0}", _remoteSettings.MaxFrameSize);
            _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote MaxHeaderListSize: {0}", _remoteSettings.MaxHeaderListSize);
#endif

            // Special stuff: need to initialize the stream for the response to the HTTP/1.1 request.
            Http2Stream initialResponseStream = NewStream(1, StreamState.HalfClosedRemote);
            _activeStreams[1] = initialResponseStream;
            _nextStreamIndex++;

            // Start the WRITE thread first. Don't enable read until after we've cleaned up the HTTP/1.1 stuff from the socket.
            IRealTimeProvider writeThreadTime = realTime.Fork("Http2SessionWrite");
            _writeTask = Task.Run(() => RunWriteThread(_sessionShutdown.Token, writeThreadTime, isClient: true));

            // Send the local settings
            _outgoingCommands.Enqueue(new UpdateLocalSettingsCommand(_localSettingsDesired), HttpPriority.SessionControl);
        }

        /// <summary>
        /// 2nd part of the initialization of a server-side H2 session that has upgraded from HTTP/1.1.
        /// This phase starts the read thread in the session. You should call it as soon as you are certain
        /// that the next thing in the socket will be an HTTP2 session initializer and initial settings frame.
        /// </summary>
        /// <param name="realTime"></param>
        public void BeginUpgradedServerSessionPart2(IRealTimeProvider realTime)
        {
            // Start the connection duplex. The read thread will read and validate the client's connection prefix
            IRealTimeProvider readThreadTime = realTime.Fork("Http2SessionRead");
            _readTask = Task.Run(() => RunReadThread(_sessionShutdown.Token, readThreadTime));

            BeginPingLoop(_sessionPreferences.OutgoingPingInterval, realTime);

            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_H2_ServerSessionsInitiated, _metricDimensions);
        }

        private void BeginPingLoop(TimeSpan pingInterval, IRealTimeProvider realTime)
        {
            CancellationToken cancelToken = _sessionShutdown.Token;
            IRealTimeProvider pingThreadtime = realTime.Fork("Http2Ping");
            Task.Run(async () =>
            {
                try
                {
                    while (!cancelToken.IsCancellationRequested)
                    {
                        await pingThreadtime.WaitAsync(pingInterval, cancelToken).ConfigureAwait(false);
                        if (!cancelToken.IsCancellationRequested)
                        {
                            _outgoingCommands.Enqueue(new SendPingCommand(), HttpPriority.Ping);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    _logger.Log(e);
                }
                finally
                {
                    pingThreadtime.Merge();
                }
            });
        }

        /// <summary>
        /// Applies new settings
        /// </summary>
        /// <param name="newSettings"></param>
        public void UpdateSettings(Http2Settings newSettings)
        {
            _localSettingsDesired = newSettings.AssertNonNull(nameof(newSettings));
            if (!_isClient)
            {
                _localSettingsDesired.EnablePush = false; // Server cannot specify EnablePush = true, only clients
            }

            _outgoingCommands.Enqueue(new UpdateLocalSettingsCommand(_localSettingsDesired), HttpPriority.SessionControl);
        }

        /// <summary>
        /// Initiates an HTTP/2 request over this session. The response object will have an internal handle to this session
        /// that will read the DATA frames as they come from the remote host.
        /// </summary>
        /// <param name="request">The request to send</param>
        /// <param name="traceLogger">A trace logger</param>
        /// <param name="cancelToken">A cancel token for the reques (to implement client timeout, etc.)</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An HTTP response, or null if this session is no longer active.</returns>
        public async Task<HttpResponse> MakeHttpRequest(HttpRequest request, ILogger traceLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (!_isClient)
            {
                throw new InvalidOperationException("Only an HTTP/2 client can send HTTP requests");
            }

            if (_isActive != 1)
            {
                traceLogger.Log("HTTP request aborted because session is shut down");
                return null;
            }

            ValidateOutgoingRequest(request, traceLogger);

            using (CancellationTokenSource joinedCancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_sessionShutdown.Token, cancelToken))
            {
                CancellationToken httpRequestCancelToken = joinedCancelTokenSource.Token;
                try
                {
                    // First - does this request match a recently received push promise?
                    FastConcurrentHashSet<PushPromiseHeaders> potentialPushPromises;
                    if ((string.Equals(request.RequestMethod, HttpConstants.HTTP_VERB_GET, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(request.RequestMethod, HttpConstants.HTTP_VERB_HEAD, StringComparison.OrdinalIgnoreCase)) &&
                        _pushPromises.TryGetValue(request.RequestFile, out potentialPushPromises))
                    {
                        // Check to see if any of the push promise frames match and if their streams exist.
                        // We have to lock the set of promises here because it's possible that there are two simultaneous requests for the same
                        // resource, and we can't let both of them try and read from the same stream.
                        Http2Stream promiseStream;
                        bool holdingDictLock = true;
                        Monitor.Enter(potentialPushPromises);
                        try
                        {
                            foreach (PushPromiseHeaders potentialPushPromise in potentialPushPromises)
                            {
                                if (potentialPushPromise.DoesRequestMatch(request) &&
                                    _activeStreams.TryGetValue(potentialPushPromise.PromisedStreamId, out promiseStream))
                                {
                                    // we are allowed to remove from the set we are iterating from because this is a FastConcurrentHashSet
                                    potentialPushPromises.Remove(potentialPushPromise);
                                    holdingDictLock = false;
                                    Monitor.Exit(potentialPushPromises);
                                    traceLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Fulfilling outgoing HTTP request {0} using an incoming push promise", request.RequestFile);
                                    return await ProcessHttpRequestUsingPromisedStream(request, promiseStream, httpRequestCancelToken, realTime);
                                }
                            }
                        }
                        finally
                        {
                            if (holdingDictLock)
                            {
                                Monitor.Exit(potentialPushPromises);
                            }
                        }
                    }

                    // Reserve the stream ID in the dictionary
                    int streamId = (Interlocked.Increment(ref _nextStreamIndex) * 2) - (_isClient ? 1 : 0);

                    // TODO enforce max_concurrent_streams settings
                    Http2Stream newStream = NewStream(streamId, StreamState.Open); 

                    _activeStreams[streamId] = newStream;
                    return await ProcessHttpRequestUsingNewStream(request, newStream, httpRequestCancelToken, realTime, endOfData: true).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    traceLogger.Log("HTTP request aborted because session was being canceled", LogLevel.Wrn);
                    return null;
                }
                catch (Exception ex)
                {
                    traceLogger.Log("HTTP request aborted because of unhandled exception", LogLevel.Wrn);
                    traceLogger.Log(ex, LogLevel.Wrn);
                    return null;
                }
            }
        }

        public async Task<IHttpServerContext> HandleIncomingHttpRequest(
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            IHttpServerDelegate serverImplementation)
        {
            int newRequestStreamId = await _incomingRequestStreamIds.ReceiveAsync(cancelToken, realTime).ConfigureAwait(false);
            Http2Stream stream;
            if (!_activeStreams.TryGetValue(newRequestStreamId, out stream))
            {
                logger.Log("Incoming request stream was canceled", LogLevel.Wrn);
                return null; // ?
            }

            SocketHttpServerContext2_0 returnVal = new SocketHttpServerContext2_0(
                new WeakPointer<Http2Session>(this),
                new WeakPointer<Http2Stream>(stream),
                logger,
                serverImplementation,
                _socket.RemoteEndpointString,
                realTime);
            return returnVal;
        }

        private static void ValidateOutgoingRequest(HttpRequest request, ILogger traceLogger)
        {
            // Validate the request. Remove headers that are specific to HTTP/1.1
            if (request.RequestHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING))
            {
                traceLogger.Log("Removing Transfer-Encoding header from outgoing HTTP/2 request", LogLevel.Wrn);
                request.RequestHeaders.Remove(HttpConstants.HEADER_KEY_TRANSFER_ENCODING);
            }

            if (request.RequestHeaders.ContainsKey(HttpConstants.HEADER_KEY_CONNECTION))
            {
                traceLogger.Log("Removing Connection header from outgoing HTTP/2 request", LogLevel.Wrn);
                request.RequestHeaders.Remove(HttpConstants.HEADER_KEY_CONNECTION);
            }

            if (request.RequestHeaders.ContainsKey(HttpConstants.HEADER_KEY_HOST))
            {
                // TODO Host header gets superceded by :authority
                traceLogger.Log("Removing Host header from outgoing HTTP/2 request", LogLevel.Wrn);
                request.RequestHeaders.Remove(HttpConstants.HEADER_KEY_HOST);
            }
        }

        /// <summary>
        /// Use case: You are a server and are sending a primary response to a request that came to your previously.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="responseStreamId"></param>
        /// <param name="traceLogger"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <param name="trailerNames">A potentially null list of trailer names to declare, which will be appended to the end of the response.</param>
        /// <param name="trailerDelegate">A potentially null delegate to invoke to fetch trailer values after the response content has finished writing.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal async Task WriteOutgoingResponse(
            HttpResponse response,
            int responseStreamId,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            IReadOnlyCollection<string> trailerNames,
            Func<string, Task<string>> trailerDelegate)
        {
            if (_isClient)
            {
                throw new InvalidOperationException("Only an HTTP/2 server can send HTTP responses");
            }

            if (_isActive != 1)
            {
                traceLogger.Log("HTTP response aborted because session is shut down");
                return;
            }

            // Validate the request. Remove headers that are specific to HTTP/1.1
            if (response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING))
            {
                traceLogger.Log("Removing Transfer-Encoding header from outgoing HTTP/2 response", LogLevel.Wrn);
                response.ResponseHeaders.Remove(HttpConstants.HEADER_KEY_TRANSFER_ENCODING);
            }

            if (response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_CONNECTION))
            {
                traceLogger.Log("Removing Connection header from outgoing HTTP/2 response", LogLevel.Wrn);
                response.ResponseHeaders.Remove(HttpConstants.HEADER_KEY_CONNECTION);
            }

            if (response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRAILER))
            {
                throw new ArgumentException("You may not set the \"Trailer\" HTTP header manually");
            }

            bool useTrailers = trailerNames != null && trailerNames.Count > 0;
            if (useTrailers && trailerDelegate == null)
            {
                throw new ArgumentNullException("A trailer delegate is required when declaring trailers");
            }

            if (useTrailers)
            {
                // Validate and declare trailers in the response Trailer field
                foreach (string trailerName in trailerNames)
                {
                    if (!HttpHelpers.IsValidTrailerName(trailerName))
                    {
                        throw new ArgumentException("An HTTP trailer may not be used to transmit message-framing, routing, authentication, request modifier, or content header names");
                    }

                    response.ResponseHeaders.Add(HttpConstants.HEADER_KEY_TRAILER, trailerName);
                }
            }

            // TODO enforce max_concurrent_streams settings
            Http2Stream stream;
            if (!_activeStreams.TryGetValue(responseStreamId, out stream))
            {
                // Stream was canceled immediately and removed.
                traceLogger.Log("HTTP response aborted because stream was canceled by remote peer");
                return;
            }

            using (CancellationTokenSource joinedCancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_sessionShutdown.Token, cancelToken))
            {
                CancellationToken httpRequestCancelToken = joinedCancelTokenSource.Token;
                try
                {
                    HttpContentStream outgoingDataStream = response.GetOutgoingContentStream();

                    // Queue the command to write the header block
                    // This MUST end up being at a higher priority than the data which follows
                    // Alternatively, we could use the stream event signal to wait until outgoing headers are sent before we start writing data...
                    bool noBodyContent = outgoingDataStream is EmptyHttpContentStream;
                    _outgoingCommands.Enqueue(
                        new SendResponseHeaderBlockCommand(
                            response.ResponseHeaders,
                            response.ResponseCode,
                            noBodyContent && !useTrailers,
                            responseStreamId),
                        HttpPriority.Headers);

                    if (!noBodyContent)
                    {
                        // And then start queueing data frames
                        // We ignore remote maximum frame size if it's larger than our scratch buffer pool size (64K)
                        // No need to allocate a giant buffer just because the remote peer says they support it.
                        using (PooledBuffer<byte> scratchBuffer = BufferPool<byte>.Rent(Math.Min(BufferPool<byte>.DEFAULT_BUFFER_SIZE, _remoteSettings.MaxFrameSize)))
                        {
                            // Wait on both the stream-local and global flow control windows until we have reserved
                            // at least some credits to be able to send outgoing data
                            int flowControlCreditAvailable = await WaitOnOutgoingFlowControlIfNeeded(
                                stream,
                                scratchBuffer.Length,
                                cancelToken,
                                realTime).ConfigureAwait(false);

                            try
                            {
                                if (stream.State == StreamState.HalfClosedLocal ||
                                    stream.State == StreamState.Closed)
                                {
                                    // Stream was reset by remote peer.
                                    CloseStream(stream);
                                    return;
                                }

                                int amountReadFromStream = await outgoingDataStream.ReadAsync(
                                    scratchBuffer.Buffer, 0, flowControlCreditAvailable, cancelToken, realTime).ConfigureAwait(false);

                                while (amountReadFromStream > 0)
                                {
                                    flowControlCreditAvailable -= amountReadFromStream;

                                    PooledBuffer<byte> dataPayload = BufferPool<byte>.Rent(amountReadFromStream);
                                    ArrayExtensions.MemCopy(scratchBuffer.Buffer, 0, dataPayload.Buffer, 0, amountReadFromStream);
#if HTTP2_DEBUG
                                    traceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Queueing response data, stream {0} size {1}", stream.StreamId, amountReadFromStream);
#endif
                                    _outgoingCommands.EnqueueData(
                                        new SendDataFrameCommand(
                                            Http2DataFrame.CreateOutgoing(
                                            dataPayload,
                                            stream.StreamId,
                                            endStream: false)),
                                        realTime);

                                    if (flowControlCreditAvailable == 0)
                                    {
                                        flowControlCreditAvailable = await WaitOnOutgoingFlowControlIfNeeded(
                                            stream,
                                            scratchBuffer.Length,
                                            cancelToken,
                                            realTime).ConfigureAwait(false);
                                    }

                                    if (stream.State == StreamState.HalfClosedLocal ||
                                        stream.State == StreamState.Closed)
                                    {
                                        CloseStream(stream);
                                        return;
                                    }

                                    amountReadFromStream = await outgoingDataStream.ReadAsync(
                                        scratchBuffer.Buffer, 0, flowControlCreditAvailable, cancelToken, realTime).ConfigureAwait(false);
                                }

                                if (useTrailers)
                                {
                                    // If there's trailers, send that as the final frame (or set of frames) in the stream.
                                    HttpHeaders trailers = new HttpHeaders(trailerNames.Count);
                                    foreach (string trailerName in trailerNames)
                                    {
                                        trailers.Add(trailerName, await trailerDelegate(trailerName).ConfigureAwait(false));
                                    }

                                    _outgoingCommands.EnqueueTrailers(
                                        new SendResponseTrailerBlockCommand(
                                            trailers,
                                            responseStreamId),
                                        realTime);
                                }
                                else
                                {
                                    // Always send an empty data frame with END_STREAM set to finish the data
                                    _outgoingCommands.EnqueueData(
                                        new SendDataFrameCommand(
                                        Http2DataFrame.CreateOutgoing(
                                            BufferPool<byte>.Rent(0),
                                            stream.StreamId,
                                            endStream: true)),
                                        realTime);
                                }
                            }
                            finally
                            {
                                if (flowControlCreditAvailable > 0)
                                {
                                    // return credits we didn't use
                                    stream.OutgoingFlowControlWindow.AugmentCredits(flowControlCreditAvailable);
                                    stream.OutgoingFlowControlAvailableSignal.Set();
                                    _overallConnectionOutgoingFlowWindow.AugmentCredits(flowControlCreditAvailable);
                                    _overallConnectionOutgoingFlowWindowAvailable.Set();
                                }
                            }
                        }
                    }
                    else // if (noBodyContent)
                    {
                        // Handle the odd case where there's no data but there is trailers. Whatever.
                        if (useTrailers)
                        {
                            HttpHeaders trailers = new HttpHeaders(trailerNames.Count);
                            foreach (string trailerName in trailerNames)
                            {
                                trailers.Add(trailerName, await trailerDelegate(trailerName).ConfigureAwait(false));
                            }

                            _outgoingCommands.EnqueueTrailers(
                                new SendResponseTrailerBlockCommand(
                                    trailers,
                                    responseStreamId),
                                realTime);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    traceLogger.Log("HTTP response aborted because session was being canceled", LogLevel.Wrn);
                    return;
                }
                catch (Exception ex)
                {
                    traceLogger.Log("HTTP response aborted because of unhandled exception", LogLevel.Wrn);
                    traceLogger.Log(ex, LogLevel.Wrn);
                    return;
                }
            }
        }

        /// <summary>
        /// Use case: You are a server and want to - prior to sending a primary response - signal
        /// a push promise to tell the client to expect some data on another stream.
        /// </summary>
        /// <param name="primaryResponseStreamId"></param>
        /// <param name="requestPatternHeaders"></param>
        /// <param name="requestPath"></param>
        /// <param name="requestMethod"></param>
        /// <param name="traceLogger"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        internal IPushPromiseStream InitializePushPromiseStream(
            int primaryResponseStreamId,
            HttpHeaders requestPatternHeaders,
            string requestPath,
            string requestMethod,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            if (_isClient)
            {
                throw new InvalidOperationException("Only an HTTP/2 server can initiate push promises");
            }

            if (_isActive != 1)
            {
                traceLogger.Log("HTTP push promise aborted because session is shut down", LogLevel.Wrn);
                return new NullPushPromiseStream();
            }

            if (!_remoteSettings.EnablePush)
            {
                traceLogger.Log("HTTP push promise aborted because the current connection does not support server push", LogLevel.Wrn);
                return new NullPushPromiseStream();
            }

            if (!string.Equals(HttpConstants.HTTP_VERB_GET, requestMethod, StringComparison.Ordinal) && !string.Equals(HttpConstants.HTTP_VERB_TRACE, requestMethod, StringComparison.Ordinal))
            {
                throw new Http2ProtocolException("Server push is only allowed for cacheable resources retrieved using GET or TRACE");
            }

            // Reserve a promised stream ID
            int promisedStreamId = (Interlocked.Increment(ref _nextStreamIndex) * 2);

            Http2Stream newPushStream = NewStream(promisedStreamId, StreamState.ReservedLocal);
            Http2Stream existingStream;
            if (_activeStreams.TryGetValueOrSet(promisedStreamId, out existingStream, newPushStream))
            {
                throw new Exception("An HTTP stream with the promised ID \"" + promisedStreamId + "\" already exists.");
            }

            // Queue up the frame for the push promise at an equal priority to primary response headers
            _outgoingCommands.Enqueue(
                new SendPushPromiseFrameCommand(
                    requestPatternHeaders,
                    requestPath,
                    requestMethod,
                    primaryResponseStreamId,
                    promisedStreamId),
                HttpPriority.Headers);

            return new PushPromiseStream(new WeakPointer<Http2Session>(this), newPushStream, requestPath, requestMethod);
        }

        internal async Task WriteOutgoingPushPromise(
            IHttpHeaders responseHeaders,
            int responseCode,
            Http2Stream promiseStream,
            NonRealTimeStream contentStream,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            // Queue up the response headers on the promise stream
            _outgoingCommands.Enqueue(
                new SendResponseHeaderBlockCommand(
                    responseHeaders,
                    responseCode,
                    endOfStream: false,
                    streamId: promiseStream.StreamId),
                HttpPriority.Headers);

            using (contentStream)
            using (PooledBuffer<byte> scratchBuffer = BufferPool<byte>.Rent(_remoteSettings.MaxFrameSize))
            {
                // Wait on both the stream-local and global flow control windows until we have reserved
                // at least some credits to be able to send outgoing data
                int flowControlCreditAvailable = await WaitOnOutgoingFlowControlIfNeeded(
                    promiseStream,
                    scratchBuffer.Length,
                    cancelToken,
                    realTime).ConfigureAwait(false);

                try
                {
                    if (promiseStream.State == StreamState.HalfClosedLocal ||
                        promiseStream.State == StreamState.Closed)
                    {
                        // Stream was reset by remote peer.
                        CloseStream(promiseStream);
                        return;
                    }

                    int amountReadFromStream = await contentStream.ReadAsync(
                        scratchBuffer.Buffer, 0, flowControlCreditAvailable, cancelToken, realTime).ConfigureAwait(false);

                    while (amountReadFromStream > 0)
                    {
                        flowControlCreditAvailable -= amountReadFromStream;

                        PooledBuffer<byte> dataPayload = BufferPool<byte>.Rent(amountReadFromStream);
                        ArrayExtensions.MemCopy(scratchBuffer.Buffer, 0, dataPayload.Buffer, 0, amountReadFromStream);
                        _outgoingCommands.EnqueueData(
                            new SendDataFrameCommand(
                                Http2DataFrame.CreateOutgoing(
                                dataPayload,
                                promiseStream.StreamId,
                                endStream: false)),
                            realTime);

                        if (flowControlCreditAvailable == 0)
                        {
                            flowControlCreditAvailable = await WaitOnOutgoingFlowControlIfNeeded(
                                promiseStream,
                                scratchBuffer.Length,
                                cancelToken,
                                realTime).ConfigureAwait(false);
                        }

                        if (promiseStream.State == StreamState.HalfClosedLocal ||
                            promiseStream.State == StreamState.Closed)
                        {
                            CloseStream(promiseStream);
                            return;
                        }

                        amountReadFromStream = await contentStream.ReadAsync(
                            scratchBuffer.Buffer, 0, flowControlCreditAvailable, cancelToken, realTime).ConfigureAwait(false);
                    }

                    // Always send an empty data frame with END_STREAM set to finish the data
                    _outgoingCommands.EnqueueData(
                        new SendDataFrameCommand(
                        Http2DataFrame.CreateOutgoing(
                            BufferPool<byte>.Rent(0),
                            promiseStream.StreamId,
                            endStream: true)),
                        realTime);

                    // TODO Trailers would go here
                }
                finally
                {
                    if (flowControlCreditAvailable > 0)
                    {
                        // return credits we didn't use
                        promiseStream.OutgoingFlowControlWindow.AugmentCredits(flowControlCreditAvailable);
                        promiseStream.OutgoingFlowControlAvailableSignal.Set();
                        _overallConnectionOutgoingFlowWindow.AugmentCredits(flowControlCreditAvailable);
                        _overallConnectionOutgoingFlowWindowAvailable.Set();
                    }

                    _activeStreams.Remove(promiseStream.StreamId);
                    CloseStream(promiseStream);
                }
            }
        }

        private async Task<HttpResponse> ProcessHttpRequestUsingNewStream(
            HttpRequest request,
            Http2Stream stream,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            bool endOfData)
        {
            HttpContentStream outgoingDataStream = request.GetOutgoingContentStream();
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Http_OutgoingRequests20, _metricDimensions);

            // Queue the command to write the header block
            // This MUST end up being at a higher priority than the data which follows
            // Alternatively, we could use the stream event signal to wait until outgoing headers are sent before we start writing data...
            bool noBodyContent = outgoingDataStream is EmptyHttpContentStream;
            _outgoingCommands.Enqueue(
                new SendRequestHeaderBlockCommand(
                    request.RequestHeaders,
                    request.BuildUri(),
                    request.RequestMethod,
                    stream.StreamId,
                    noBodyContent,
                    stream),
                HttpPriority.Headers);

            if (!noBodyContent)
            {
                // And then start queueing data frames
                // FIXME it is possible for max framesize to vary between data frames if remote peer sends settings update in the middle of this data transfer
                using (PooledBuffer<byte> scratchBuffer = BufferPool<byte>.Rent(Math.Min(BufferPool<byte>.DEFAULT_BUFFER_SIZE, _remoteSettings.MaxFrameSize)))
                {
                    // Wait on both the stream-local and global flow control windows until we have reserved
                    // at least some credits to be able to send outgoing data
                    int flowControlCreditAvailable = await WaitOnOutgoingFlowControlIfNeeded(
                        stream,
                        scratchBuffer.Length,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    try
                    {
                        if (stream.State == StreamState.HalfClosedLocal ||
                            stream.State == StreamState.Closed)
                        {
                            CloseStream(stream);
                            throw new HttpRequestException("HTTP/2 request was rejected by remote server");
                        }

                        int amountReadFromStream = await outgoingDataStream.ReadAsync(
                            scratchBuffer.Buffer, 0, flowControlCreditAvailable, cancelToken, realTime).ConfigureAwait(false);

                        while (amountReadFromStream > 0)
                        {
                            flowControlCreditAvailable -= amountReadFromStream;

                            PooledBuffer<byte> dataPayload = BufferPool<byte>.Rent(amountReadFromStream);
                            ArrayExtensions.MemCopy(scratchBuffer.Buffer, 0, dataPayload.Buffer, 0, amountReadFromStream);
                            _outgoingCommands.EnqueueData(
                                new SendDataFrameCommand(
                                    Http2DataFrame.CreateOutgoing(
                                    dataPayload,
                                    stream.StreamId,
                                    endStream: false)),
                                realTime);

                            if (flowControlCreditAvailable == 0)
                            {
                                flowControlCreditAvailable = await WaitOnOutgoingFlowControlIfNeeded(
                                    stream,
                                    scratchBuffer.Length,
                                    cancelToken,
                                    realTime).ConfigureAwait(false);
                            }

                            if (stream.State == StreamState.HalfClosedLocal ||
                                stream.State == StreamState.Closed)
                            {
                                CloseStream(stream);
                                throw new HttpRequestException("HTTP/2 request was rejected by remote server");
                            }

                            amountReadFromStream = await outgoingDataStream.ReadAsync(
                                scratchBuffer.Buffer, 0, flowControlCreditAvailable, cancelToken, realTime).ConfigureAwait(false);
                        }

                        if (endOfData)
                        {
                            // Send an empty data frame with END_STREAM set to finish the data
                            // This would be skipped for operations that will continue to use the stream
                            // after the request, for now that's only websockets over h2.
                            _outgoingCommands.EnqueueData(
                                new SendDataFrameCommand(
                                Http2DataFrame.CreateOutgoing(
                                    BufferPool<byte>.Rent(0),
                                    stream.StreamId,
                                    endStream: true)),
                                realTime);
                        }

                        // TODO Trailers would go here
                    }
                    finally
                    {
                        // bugbug: if there is an exception such as a canceled request, what happens to global flow control?
                        if (flowControlCreditAvailable > 0)
                        {
                            // return credits we didn't use
                            stream.OutgoingFlowControlWindow.AugmentCredits(flowControlCreditAvailable);
                            stream.OutgoingFlowControlAvailableSignal.Set();
                            _overallConnectionOutgoingFlowWindow.AugmentCredits(flowControlCreditAvailable);
                            _overallConnectionOutgoingFlowWindowAvailable.Set();
                        }
                    }
                }
            }

            // Wait for the remote peer to send a response
            if (!realTime.IsForDebug)
            {
                cancelToken.ThrowIfCancellationRequested();
                await stream.ReceievedHeadersSignal.WaitAsync(cancelToken).ConfigureAwait(false);
            }
            else
            {
                // Unit testing path when we need to consume virtual time
                while (!stream.ReceievedHeadersSignal.TryGetAndClear())
                {
                    cancelToken.ThrowIfCancellationRequested();
                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
                }
            }

            if (stream.State == StreamState.Closed)
            {
                throw new HttpRequestException("HTTP/2 request was rejected by remote server");
            }
            else if (!stream.ResponseStatusCode.HasValue)
            {
                throw new HttpRequestException("HTTP/2 response did not have a response code");
            }

            // Return an HttpResponse which wraps around the returned pipe stream
            return HttpResponse.CreateIncoming(
                stream.ResponseStatusCode.Value,
                HttpHelpers.GetStatusStringForStatusCode(stream.ResponseStatusCode.Value),
                stream.Headers,
                new Http2ContentStream(new WeakPointer<Http2Stream>(stream)),
                new Http2ClientContext(new WeakPointer<Http2Stream>(stream), new WeakPointer<Http2Session>(this)));
        }

        private async Task<HttpResponse> ProcessHttpRequestUsingPromisedStream(HttpRequest request, Http2Stream promiseStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Http_OutgoingRequests20Fulfilled, _metricDimensions);
            // Wait for the remote peer to send a response
            if (!realTime.IsForDebug)
            {
                cancelToken.ThrowIfCancellationRequested();
                await promiseStream.ReceievedHeadersSignal.WaitAsync(cancelToken).ConfigureAwait(false);
            }
            else
            {
                // Unit testing path when we need to consume virtual time
                while (!promiseStream.ReceievedHeadersSignal.TryGetAndClear())
                {
                    cancelToken.ThrowIfCancellationRequested();
                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
                }
            }

            if (!promiseStream.ResponseStatusCode.HasValue)
            {
                throw new HttpRequestException("HTTP/2 response did not have a response code");
            }

            // Return an HttpResponse which wraps around the returned pipe stream
            return HttpResponse.CreateIncoming(
                promiseStream.ResponseStatusCode.Value,
                HttpHelpers.GetStatusStringForStatusCode(promiseStream.ResponseStatusCode.Value),
                promiseStream.Headers,
                new HttpContentStreamWrapper(promiseStream.ReadStream, ownsStream: true),
                new Http2ClientContext(new WeakPointer<Http2Stream>(promiseStream), new WeakPointer<Http2Session>(this)));
        }

        /// <summary>
        /// Gracefully shuts down the HTTP session, first sending a GOAWAY frame to the remote peer
        /// with the given code and optional debug message. DON'T CALL THIS FROM THE WRITE THREAD.
        /// </summary>
        /// <param name="errorCode">The reason for the disconnect</param>
        /// <param name="debugMessage">An optional string to send to the remote peer for debugging</param>
        /// <returns>An async task that finishes once we have written the final frame to the socket</returns>
        public async Task Shutdown(Http2ErrorCode errorCode = Http2ErrorCode.NoError, string debugMessage = "")
        {
            if (AtomicOperations.GetAndClearFlag(ref _isActive))
            {
                if (errorCode != Http2ErrorCode.NoError)
                {
                    if (string.IsNullOrEmpty(debugMessage))
                    {
                        _logger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata, "HTTP2 session ended with error code {0}", errorCode.ToString());
                    }
                    else
                    {
                        _logger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata, "HTTP2 session ended with error code {0}: {1}", errorCode.ToString(), debugMessage);
                    }
                }

                _outgoingCommands.Enqueue(new CloseConnectionCommand(errorCode, debugMessage), HttpPriority.SessionControl);

                // Don't call this from the write thread otherwise this next line will deadlock.
                //using (CancellationTokenSource deadlockPreventer = new CancellationTokenSource(TimeSpan.FromSeconds(1))
                await _gracefulShutdownSignal.WaitAsync();
                _sessionShutdown.Cancel();
            }
        }

        private Http2Stream NewStream(int streamId, StreamState initialState)
        {
            return new Http2Stream()
            {
                OutgoingFlowControlWindow = new DataTransferWindow(_remoteSettings.InitialWindowSize),
                IncomingFlowControlWindow = new DataTransferWindow(_localSettings.InitialWindowSize),
                Headers = null,
                State = initialState,
                StreamId = streamId,
                ReceievedHeadersSignal = new AutoResetEventAsync(false),
                OutgoingFlowControlAvailableSignal = new AutoResetEventAsync(false),
                ResponseStatusCode = null,
                RequestPath = null,
                RequestMethod = null,
                RequestScheme = null,
                RequestAuthority = null,
            };
        }

        private void CloseStream(Http2Stream stream)
        {
            stream.State = StreamState.Closed;
            stream.OutgoingFlowControlAvailableSignal.Set();

            // a thread can be waiting on this if it is waiting for response headers to an outgoing request
            stream.ReceievedHeadersSignal.Set();
            // TODO reclaim stream resources
        }

        private void RudeShutdown()
        {
            _sessionShutdown.Cancel();
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
                RudeShutdown();
                _remoteSentSettingsSignal?.Set();
                _readTask?.Await();
                _writeTask?.Await();
                _socket?.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                _sessionShutdown?.Dispose();
            }
        }

        /// <summary>
        /// Used when an incoming response object is disposed before the full response body is read. HTTP2 is
        /// nice because we're allowed to just cancel the stream in this case.
        /// </summary>
        /// <param name="streamId">The ID of the stream</param>
        /// <param name="aborted">true if we didn't read the entire content of the response</param>
        internal void FinishIncomingResponseStream(int streamId, bool aborted)
        {
            _recentlyCanceledStreams.Add(streamId);
            _recentlyCanceledStreamQueue.Enqueue(streamId);
            if (_recentlyCanceledStreamQueue.ApproximateCount > RECENT_CANCELED_STREAMS_TO_TRACK)
            {
                int oldStreamId;
                if (_recentlyCanceledStreamQueue.TryDequeue(out oldStreamId))
                {
                    _recentlyCanceledStreams.Remove(oldStreamId);
                }
            }

            _activeStreams.Remove(streamId);
            if (aborted)
            {
                _outgoingCommands.Enqueue(
                    new ResetStreamCommand(streamId, Http2ErrorCode.Cancel),
                    HttpPriority.StreamControl);
            }
        }

        internal void FinishOutgoingResponseStream(int streamId)
        {
            Http2Stream stream;
            if (_activeStreams.TryGetValue(streamId, out stream))
            {
                stream.State = StreamState.Closed;
            }
        }

        /// <summary>
        /// Attempts to consume credits from the flow control window for a given stream,
        /// in addition to the overall connection flow control window
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="desiredCredits"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns>The number of credits that have been consumed</returns>
        private async ValueTask<int> WaitOnOutgoingFlowControlIfNeeded(
            Http2Stream stream,
            int desiredCredits,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            bool loopedLocal = false;
            bool loopedGlobal = false;
            int reservedCredits = 0;
            while (stream.State != StreamState.HalfClosedLocal &&
                stream.State != StreamState.Closed &&
                reservedCredits == 0)
            {
                int streamLocalCreditsRemaining = stream.OutgoingFlowControlWindow.AugmentCredits(0 - desiredCredits);
                int globalCreditsRemaining = _overallConnectionOutgoingFlowWindow.AugmentCredits(0 - desiredCredits);
                if (streamLocalCreditsRemaining >= 0 && globalCreditsRemaining >= 0)
                {
                    reservedCredits = desiredCredits;

                    // There is a race condition where multiple threads may have been waiting for the same window update
                    // but only one of them got signaled. So we signal on our way out to mitigate.
                    if (loopedLocal)
                    {
                        stream.OutgoingFlowControlAvailableSignal.Set();
                    }
                    if (loopedGlobal)
                    {
                        _overallConnectionOutgoingFlowWindowAvailable.Set();
                    }
                }
                else
                {
                    // We overdrafted one or both of our credits
                    int localCreditsReserved = streamLocalCreditsRemaining + desiredCredits;
                    int globalCreditsReserved = globalCreditsRemaining + desiredCredits;
                    if (localCreditsReserved > 0 && globalCreditsReserved > 0)
                    {
                        // We can partially satisfy the request. Return what we didn't use
                        int actualReservedCredits = Math.Min(localCreditsReserved, globalCreditsReserved);
                        stream.OutgoingFlowControlWindow.AugmentCredits(desiredCredits - actualReservedCredits);
                        stream.OutgoingFlowControlAvailableSignal.Set();
                        _overallConnectionOutgoingFlowWindow.AugmentCredits(desiredCredits - actualReservedCredits);
                        _overallConnectionOutgoingFlowWindowAvailable.Set();
                        reservedCredits = actualReservedCredits;
                    }
                    else if (localCreditsReserved <= 0)
                    {
                        // We can't satisfy the request because of the local flow control. Wait on that signal
                        _logger.Log("Waiting on outgoing flow control for stream " + stream.StreamId);
                        loopedLocal = true;
                        stream.OutgoingFlowControlWindow.AugmentCredits(desiredCredits);
                        _overallConnectionOutgoingFlowWindow.AugmentCredits(desiredCredits);

                        if (realTime.IsForDebug)
                        {
                            while (!stream.OutgoingFlowControlAvailableSignal.TryGetAndClear())
                            {
                                cancelToken.ThrowIfCancellationRequested();
                                await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            await stream.OutgoingFlowControlAvailableSignal.WaitAsync(cancelToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        // We can't satisfy the request because of the global flow control. Wait on that signal
                        _logger.Log("Waiting on outgoing global flow control");
                        loopedGlobal = true;
                        stream.OutgoingFlowControlWindow.AugmentCredits(desiredCredits);
                        _overallConnectionOutgoingFlowWindow.AugmentCredits(desiredCredits);

                        if (realTime.IsForDebug)
                        {
                            while (!_overallConnectionOutgoingFlowWindowAvailable.TryGetAndClear())
                            {
                                cancelToken.ThrowIfCancellationRequested();
                                await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            await _overallConnectionOutgoingFlowWindowAvailable.WaitAsync(cancelToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            return reservedCredits;
        }
    }
}
