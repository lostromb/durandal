using Durandal.Common.Logger;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Context that is maintained by an HTTP server when it receives an incoming request and
    /// is processing the output.
    /// </summary>
    public interface IHttpServerContext
    {
        /// <summary>
        /// Gets the HTTP protocol version that this context is currently operating on.
        /// </summary>
        HttpVersion CurrentProtocolVersion { get; }

        /// <summary>
        /// Indicates whether this context supports upgrade to websocket
        /// </summary>
        bool SupportsWebSocket { get; }

        /// <summary>
        /// Indicates whether this context supports server push (e.g. HTTP/2 Push-Promise)
        /// </summary>
        bool SupportsServerPush { get; }

        /// <summary>
        /// Indicates whether this context and client supports trailers
        /// </summary>
        bool SupportsTrailers { get; }

        bool PrimaryResponseStarted { get; }

        bool PrimaryResponseFinished { get; }

        /// <summary>
        /// Gets the incoming request that is coming to this server.
        /// </summary>
        HttpRequest HttpRequest { get; }

        /// <summary>
        /// Writes the primary HTTP response back to the client. The response will be disposed after it has finished writing.
        /// If using server push, you are NOT allowed to send a push-promise after you have begun the primary response. The typical pattern is
        /// 1. Send push-promise(s) 2. Send the full primary response 3. Write the payload to satisfy the push promises
        /// </summary>
        /// <param name="response">The response to send</param>
        /// <param name="cancelToken">A cancel token for the write</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="traceLogger">A logger (potentially with traceid) to use for the operation</param>
        /// <returns>An async task</returns>
        Task WritePrimaryResponse(
            HttpResponse response,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime);

        /// <summary>
        /// Writes the primary HTTP response back to the client. The response will be disposed after it has finished writing.
        /// If using server push, you are NOT allowed to send a push-promise after you have begun the primary response. The typical pattern is
        /// 1. Send push-promise(s) 2. Send the full primary response 3. Write the payload to satisfy the push promises
        /// </summary>
        /// <param name="response">The response to send</param>
        /// <param name="cancelToken">A cancel token for the write</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="traceLogger">A logger (potentially with traceid) to use for the operation</param>
        /// <param name="trailerNames">A set of trailer names to declare, may be null</param>
        /// <param name="trailerDelegate">A function delegate where the input is the trailer name and the output is an async task which produces the trailer value.</param>
        /// <returns>An async task</returns>
        Task WritePrimaryResponse(
            HttpResponse response,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            IReadOnlyCollection<string> trailerNames,
            Func<string, Task<string>> trailerDelegate);

        /// <summary>
        /// If server push is supported, this will instruct the server to make a simulated request to itself and push it to the client
        /// proactively, after the primary response has been written. This method MUST be called BEFORE the primary response gets written.
        /// The content MUST be static and cacheable, MUST be within the local host's authority (e.g. not an external resource), and IS NOT
        /// guaranteed to be honored by the client. It also cannot expect a request body of any kind.
        /// Careful performance tuning should be done to optimize server push in your specific scenario.
        /// </summary>
        /// <param name="expectedRequestMethod">The expected HTTP verb that the request would use (almost always GET)</param>
        /// <param name="expectedPath">The path that we expect the client to access this resource from</param>
        /// <param name="expectedRequestHeaders">A collection of headers that must be matched by the client. May be null.</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        void PushPromise(
            string expectedRequestMethod,
            string expectedPath,
            HttpHeaders expectedRequestHeaders,
            CancellationToken cancelToken,
            IRealTimeProvider realTime);

        /// <summary>
        /// Accepts a client's request for websocket upgrade, and returns a websocket context to use for
        /// the ensuing conversation.
        /// </summary>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <param name="subProtocol">The sub protocol to use for the socket, as defined in RFC 6455 section 1.9. May be null.</param>
        /// <returns>A websocket</returns>
        Task<IWebSocket> AcceptWebsocketUpgrade(CancellationToken cancelToken, IRealTimeProvider realTime, string subProtocol = null);
    }
}
