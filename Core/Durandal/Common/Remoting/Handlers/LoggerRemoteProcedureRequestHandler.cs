using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Remoting;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.Speech.SR;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting.Handlers
{
    /// <summary>
    /// Implements a handler that handles remote logging on the server (host) side
    /// </summary>
    public class LoggerRemoteProcedureRequestHandler : IRemoteProcedureRequestHandler
    {
        private readonly ILogger _targetLogger;

        public LoggerRemoteProcedureRequestHandler(ILogger targetLogger)
        {
            _targetLogger = targetLogger;
        }

        public bool CanHandleRequestType(Type requestType)
        {
            return requestType == typeof(RemoteLogMessageRequest);
        }

        public Task HandleRequest(
            PostOffice postOffice,
            IRemoteDialogProtocol remoteProtocol,
            ILogger traceLogger,
            Tuple<object, Type> parsedMessage,
            MailboxMessage originalMessage,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            TaskFactory taskFactory)
        {
            if (parsedMessage.Item2 == typeof(RemoteLogMessageRequest))
            {
                RemoteLogMessageRequest parsedInterstitialRequest = parsedMessage.Item1 as RemoteLogMessageRequest;

                if (parsedInterstitialRequest.LogEvents != null)
                {
                    foreach (var logEvent in parsedInterstitialRequest.LogEvents.Events)
                    {
                        _targetLogger.Log(logEvent.ToLogEvent());
                    }
                }

                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedLogger");

                // Send back an ACK that log messages were received
                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<bool> successResponse = new RemoteProcedureResponse<bool>(parsedInterstitialRequest.MethodName, true);
                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(successResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }

            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
