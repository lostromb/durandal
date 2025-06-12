using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
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
    /// HTTP server context backed by an ASP.NET controller (and wrapping the context provided by that).
    /// </summary>
    public class AspHttpServerContext : IHttpServerContext, IDisposable
    {
        private readonly HttpContext _aspContext;
        private readonly DateTimeOffset _requestStartTime;
        private int _disposed = 0;

        public AspHttpServerContext(HttpContext aspContext, IRealTimeProvider realTime)
        {
            _aspContext = aspContext.AssertNonNull(nameof(aspContext));
            _requestStartTime = realTime.AssertNonNull(nameof(realTime)).Time;
            HttpRequest = ConvertAspRequestToDurandalRequest(_aspContext.Request);
            CurrentProtocolVersion = HttpVersion.ParseHttpVersion(_aspContext.Request.Protocol);
            PrimaryResponseStarted = false;
            PrimaryResponseFinished = false;
            SupportsTrailers = aspContext.Response.SupportsTrailers();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AspHttpServerContext()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc/>
        public HttpVersion CurrentProtocolVersion
        {
            get; private set;
        }

        /// <inheritdoc/>
        public bool SupportsWebSocket => true;

        /// <inheritdoc/>
        public bool SupportsServerPush => false;

        /// <inheritdoc/>
        public bool SupportsTrailers{ get; private set; }

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

        /// <inheritdoc/>
        public async Task<IWebSocket> AcceptWebsocketUpgrade(CancellationToken cancelToken, IRealTimeProvider realTime, string subProtocol = null)
        {
            // AcceptWebSocketAsync will generate the appropriate HTTP response message back to the client,
            // either 101 Switching Protocols or some other error response.
            PrimaryResponseStarted = true;
            System.Net.WebSockets.WebSocket socket = await _aspContext.WebSockets.AcceptWebSocketAsync(subProtocol);
            PrimaryResponseFinished = true;
            return new SystemWebSocketWrapper(socket);
        }

        /// <inheritdoc/>
        public Task WritePrimaryResponse(HttpResponse response, ILogger traceLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return WritePrimaryResponse(response, traceLogger, cancelToken, realTime, trailerNames: null, trailerDelegate: null);
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

            if (trailerNames != null && trailerDelegate == null)
            {
                throw new ArgumentNullException("A trailer delegate is required when declaring trailers");
            }

            if (response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRAILER))
            {
                throw new ArgumentException("You may not set the \"Trailer\" HTTP header manually");
            }

            try
            {
                PrimaryResponseStarted = true;
                _aspContext.Response.StatusCode = response.ResponseCode;
                //_aspContext.Response.StatusDescription = response.ResponseMessage;

                if (!SupportsTrailers && trailerNames != null && trailerNames.Count > 0)
                {
                    traceLogger.Log("HTTP response trailers are declared but not supported in this request context. Trailers will not be sent.", LogLevel.Wrn);
                }

                bool useTrailers = SupportsTrailers && trailerNames != null && trailerNames.Count > 0;
                if (useTrailers)
                {
                    foreach (string trailerName in trailerNames)
                    {
                        _aspContext.Response.DeclareTrailer(trailerName);
                    }
                }

                foreach (var headerKvp in response.ResponseHeaders)
                {
                    foreach (string singleHeaderValue in headerKvp.Value)
                    {
                        if (headerKvp.Key.Equals(HttpConstants.HEADER_KEY_CONTENT_LENGTH, StringComparison.OrdinalIgnoreCase))
                        {
                            _aspContext.Response.ContentLength = long.Parse(singleHeaderValue);
                        }
                        else if (headerKvp.Key.Equals(HttpConstants.HEADER_KEY_CONTENT_TYPE, StringComparison.OrdinalIgnoreCase))
                        {
                            _aspContext.Response.ContentType = singleHeaderValue;
                        }
                        else if (headerKvp.Key.Equals(HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE, StringComparison.OrdinalIgnoreCase))
                        {
                            //_aspContext.Response.KeepAlive = bool.Parse(singleHeaderValue);
                        }
                        else if (headerKvp.Key.Equals(HttpConstants.HEADER_KEY_CONNECTION, StringComparison.OrdinalIgnoreCase) &&
                            singleHeaderValue.Equals(HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE, StringComparison.OrdinalIgnoreCase))
                        {
                            //_aspContext.Response.KeepAlive = true;
                        }
                        else if (headerKvp.Key.Equals(HttpConstants.HEADER_KEY_TRAILER, StringComparison.OrdinalIgnoreCase))
                        {
                            // Already handled above...
                        }
                        else if (headerKvp.Value.Count == 1)
                        {
                            _aspContext.Response.Headers.Add(headerKvp.Key, new Microsoft.Extensions.Primitives.StringValues(singleHeaderValue));
                        }
                        else
                        {
                            // FIXME if only there was a better way to handle multi-value headers...
                            _aspContext.Response.Headers.Add(headerKvp.Key, new Microsoft.Extensions.Primitives.StringValues(headerKvp.Value.ToArray()));
                            break;
                        }
                    }
                }

                // Generate instrumentation header
                TimeSpan totalRequestTime = realTime.Time - _requestStartTime;
                _aspContext.Response.Headers.Add(HttpConstants.HEADER_KEY_SERVER_WORK_TIME, totalRequestTime.PrintTimeSpan());

                using (HttpContentStream streamedResponse = response.GetOutgoingContentStream())
                {
                    if (streamedResponse != null && !(streamedResponse is EmptyHttpContentStream))
                    {
                        // _aspContext.Response.SendChunked = true;
                        await streamedResponse.CopyToAsyncPooled(_aspContext.Response.Body, cancelToken, realTime).ConfigureAwait(false);
                    }
                }

                if (useTrailers)
                {
                    foreach (string trailerName in trailerNames)
                    {
                        string trailerValue = await trailerDelegate(trailerName).ConfigureAwait(false);
                        _aspContext.Response.AppendTrailer(trailerName, new StringValues(trailerValue));
                    }
                }

                PrimaryResponseFinished = true;
            }
            finally
            {
                if (response.Direction == NetworkDirection.Proxied)
                {
                    await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                }

                response?.Dispose();
            }
        }

        /// <inheritdoc/>
        public void PushPromise(
            string expectedRequestMethod,
            string expectedPath,
            HttpHeaders expectedRequestHeaders,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            throw new NotSupportedException("Server push is not yet implemented for ASP servers");
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
                HttpRequest?.Dispose();
            }
        }

        private static HttpRequest ConvertAspRequestToDurandalRequest(Microsoft.AspNetCore.Http.HttpRequest clientRequest)
        {
            IHttpHeaders convertedHeaders = new AspHeaderWrapper(clientRequest.Headers);

            string parsedRequestFile;
            string parsedFragment;
            HttpFormParameters parsedGetParams;
            if (!HttpHelpers.TryParseRelativeUrl(
                clientRequest.Path.Value,
                out parsedRequestFile,
                out parsedGetParams,
                out parsedFragment))
            {
                throw new FormatException(string.Format("Cannot parse URL {0}", clientRequest.Path.Value));
            }

            foreach (KeyValuePair<string, StringValues> getParam in clientRequest.Query)
            {
                foreach (string getValue in getParam.Value)
                {
                    parsedGetParams.Add(getParam.Key, getValue);
                }
            }

            HttpRequest returnVal = HttpRequest.CreateIncoming(
                convertedHeaders,
                parsedRequestFile,
                clientRequest.Method,
                clientRequest.HttpContext.Connection.RemoteIpAddress.ToString(),
                parsedGetParams,
                parsedFragment,
                new HttpContentStreamWrapper(clientRequest.Body, ownsStream: true),
                HttpVersion.ParseHttpVersion(clientRequest.Protocol));

            returnVal.RemoteHost = clientRequest.HttpContext.Connection.RemoteIpAddress.ToString();
            return returnVal;
        }
    }
}
