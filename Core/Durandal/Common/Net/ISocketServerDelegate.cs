using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    public interface ISocketServerDelegate
    {
        /// <summary>
        /// Overridden in the subclass to execute the specific behavior to handle requests to this server
        /// </summary>
        /// <param name="clientSocket">A socket that is actively connected to a client</param>
        /// <param name="bindPoint">The binding that this socket is associated with locally</param>
        /// <param name="cancelToken">A token that indicates whether the server is aborting</param>
        /// <param name="realTime">A definition of real time, used only for non-realtime unit tests</param>
        /// <returns>An async task</returns>
        Task HandleSocketConnection(ISocket clientSocket, ServerBindingInfo bindPoint, CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}
