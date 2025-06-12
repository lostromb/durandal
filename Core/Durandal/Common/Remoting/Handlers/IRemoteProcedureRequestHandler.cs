using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting.Handlers
{
    public interface IRemoteProcedureRequestHandler
    {
        bool CanHandleRequestType(Type requestType);

        /// <summary>
        /// Handles a single incoming remoted request.
        /// This method returns a Task representing background work that is being done to fulfill the request.
        /// It is typically some blocking async work (e.g. file access) followed by a call to post the response
        /// message to the socket asynchronously. Implementers should take care to reduce the critical-path latency
        /// of this method using Task.Run or similar so as to keep the message handler from being stuck and unable to
        /// handle new incoming requests.
        /// </summary>
        /// <param name="postOffice">A post office to send the message's response to.</param>
        /// <param name="remoteProtocol">The remote protocol used to serialize messages.</param>
        /// <param name="traceLogger">A trace logger</param>
        /// <param name="parsedMessage">The parsed remoting message, as a generic</param>
        /// <param name="originalMessage">The original post office message that sent this request (and may be waiting for a reply)</param>
        /// <param name="cancelToken">A cancellation token, usually tied to either the entire service shutdown or some long timeout.</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="taskFactory">A factory for creating background tasks that might be spawned to handle this request.</param>
        /// <returns>An async task representing the bulk of the work of handling the incoming request, if any.</returns>
        Task HandleRequest(
            PostOffice postOffice,
            IRemoteDialogProtocol remoteProtocol,
            ILogger traceLogger,
            Tuple<object, Type> parsedMessage,
            MailboxMessage originalMessage,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            TaskFactory taskFactory);
    }
}
