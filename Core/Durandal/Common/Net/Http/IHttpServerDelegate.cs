using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Represents a class which can abstractly handle an incoming HTTP request and provide a response.
    /// The logic is explicitly decoupled from most other "server" concepts of endpoints, ports, etc.
    /// as this abstraction level focuses only on the act of handling HTTP requests in general.
    /// </summary>
    public interface IHttpServerDelegate
    {
        /// <summary>
        /// Overridden in the subclass to execute the specific behavior to handle requests to this server
        /// </summary>
        /// <param name="context">The context of the incoming request and its response(s).</param>
        /// <param name="cancelToken">Cancellation token for when the server is shutting down</param>
        /// <param name="realTime">Thread-specific real time for the request</param>
        /// <returns></returns>
        Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}
