using Durandal.API;
using Durandal.Common.Remoting.Protocol;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Dialog.Services;
using System.Threading;
using Durandal.Common.Logger;
using Durandal.Common.Speech.SR;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio;
using Durandal.Common.Time;
using Durandal.Common.Collections;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.Remoting.Handlers;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// The purpose of this class is to listen in on messages coming to the remote dialog executor client (host) from the
    /// dialog executor server (guest). If the guest wants to invoke a service remotely (sometimes called an interstitial request),
    /// then this class will parse that response, dispatch the request to local handlers, and return the response back
    /// to the guest asynchronously using a background callback queue.
    /// </summary>
    public class RemoteProcedureRequestOrchestrator
    {
        private static readonly TaskFactory TASK_FACTORY = new TaskFactory();
        private readonly ILogger _logger;
        private readonly IRemoteDialogProtocol _protocol;
        private readonly WeakPointer<PostOffice> _postOffice;
        private readonly ConcurrentQueue<Task> _interstitialPostbackTasks;
        private readonly IList<IRemoteProcedureRequestHandler> _requestHandlers;

        public RemoteProcedureRequestOrchestrator(
            IRemoteDialogProtocol protocol,
            WeakPointer<PostOffice> postOffice,
            ILogger logger,
            params IRemoteProcedureRequestHandler[] requestHandlers)
        {
            _protocol = protocol;
            _postOffice = postOffice;
            _logger = logger;
            _interstitialPostbackTasks = new ConcurrentQueue<Task>();
            _requestHandlers = new List<IRemoteProcedureRequestHandler>(requestHandlers);
        }

        /// <summary>
        /// Handles an incoming message. If the message was not sent to any current handlers, an error is logged
        /// </summary>
        /// <param name="parsedMessage"></param>
        /// <param name="originalMessage"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public async Task HandleIncomingMessage(Tuple<object, Type> parsedMessage, MailboxMessage originalMessage, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            foreach (IRemoteProcedureRequestHandler handler in _requestHandlers)
            {
                if (handler.CanHandleRequestType(parsedMessage.Item2))
                {
                    Task handlerTask = handler.HandleRequest(_postOffice.Value, _protocol, _logger, parsedMessage, originalMessage, cancelToken, realTime, TASK_FACTORY);
                    if (handlerTask != null)
                    {
                        await PruneCallbackQueue().ConfigureAwait(false);
                        _interstitialPostbackTasks.Enqueue(handlerTask);
                        return;
                    }
                }
            }

            _logger.Log("Unknown remoting message with type " + parsedMessage.Item2.Name, LogLevel.Wrn);
        }

        /// <summary>
        /// Awaits all currently running callbacks that have been queued as a result of processing requests.
        /// </summary>
        /// <returns></returns>
        public async Task Flush()
        {
            Task rr;
            while (_interstitialPostbackTasks.TryDequeue(out rr))
            {
                await rr.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Removes all completed callback tasks from the callback task queue
        /// </summary>
        /// <returns></returns>
        private async Task PruneCallbackQueue()
        {
            Task rr;
            int max = _interstitialPostbackTasks.ApproximateCount; // iterate through the entire queue only once at maximum
            while (_interstitialPostbackTasks.TryDequeue(out rr) && max-- > 0)
            {
                if (rr.IsCompleted)
                {
                    // It's finished. Await it to "finalize" it (by raising any exceptions that may have happened)
                    await rr.ConfigureAwait(false);
                }
                else
                {
                    // Not finished yet. Put it back on the queue
                    _interstitialPostbackTasks.Enqueue(rr);
                }
            }
        }
    }
}
