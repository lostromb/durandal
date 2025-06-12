using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Used to maintain a connection between a single HttpResponse returned by a client and its
    /// broader context (such as sockets, resource pools, sessions, etc.)
    /// </summary>
    public interface IHttpClientContext
    {
        /// <summary>
        /// The version of the HTTP protocol that was negotiated in this session.
        /// </summary>
        HttpVersion ProtocolVersion { get; }

        /// <summary>
        /// Signals the http context that a response has finished reading and we should release its resources.
        /// Functionally, you should assume that this will close the connection entirely and allow no further
        /// communication.
        /// </summary>
        /// <param name="sourceResponse">The HTTP response that this context is associated with</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        Task FinishAsync(HttpResponse sourceResponse, CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}
