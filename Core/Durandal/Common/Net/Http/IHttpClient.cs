using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// An abstraction of an HTTP/1.1 compatible web client.
    /// Each instance of IHttpClient has a fixed (implicitly pooled) connection to a single host.
    /// </summary>
    public interface IHttpClient : IDisposable
    {
        /// <summary>
        /// Gets the base URI of the host that this client connects to.
        /// </summary>
        Uri ServerAddress { get; }

        /// <summary>
        /// Returns the highest HTTP protocol version supported by this client.
        /// </summary>
        HttpVersion MaxSupportedProtocolVersion { get; }

        /// <summary>
        /// Returns the HTTP protocol version that will be used by requests from this client.
        /// </summary>
        HttpVersion InitialProtocolVersion { get; set; }

        /// <summary>
        /// Sends a simple HTTP request and returns the HTTP response.
        /// </summary>
        /// <param name="request">The HTTP request</param>
        /// <returns>The HTTP response</returns>
        Task<HttpResponse> SendRequestAsync(HttpRequest request);

        /// <summary>
        /// Sends an HTTP request with extra control parameters and returns the HTTP response.
        /// </summary>
        /// <param name="request">The HTTP request</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time, for nonrealtime unit tests</param>
        /// <param name="queryLogger">A logger for the HTTP operation</param>
        /// <returns>The HTTP response</returns>
        Task<HttpResponse> SendRequestAsync(
            HttpRequest request,
            CancellationToken cancelToken,
            IRealTimeProvider realTime = null,
            ILogger queryLogger = null);

        /// <summary>
        /// Sends an HTTP request and returns the HTTP response with instrumentation data.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The instrumented HTTP response</returns>
        Task<NetworkResponseInstrumented<HttpResponse>> SendInstrumentedRequestAsync(HttpRequest request);

        /// <summary>
        /// Sends an HTTP request with extra control parameters and returns the HTTP response with instrumentation data.
        /// </summary>
        /// <param name="request">The HTTP request</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time, for nonrealtime unit tests</param>
        /// <param name="queryLogger">A logger for the HTTP operation</param>
        /// <returns>The instrumented HTTP response</returns>
        Task<NetworkResponseInstrumented<HttpResponse>> SendInstrumentedRequestAsync(
            HttpRequest request,
            CancellationToken cancelToken,
            IRealTimeProvider realTime = null,
            ILogger queryLogger = null);

        /// <summary>
        /// Sets the read timeout for this client.
        /// Typically this is only allowed to be set before any requests have been made.
        /// </summary>
        /// <param name="timeout">The new timeout to set</param>
        void SetReadTimeout(TimeSpan timeout);
    }
}