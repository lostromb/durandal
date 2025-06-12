using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Remoting;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.Speech.SR;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting.Handlers
{
    /// <summary>
    /// Implements a handler that handles remoted HTTP requests on the server (host) side
    /// </summary>
    public class HttpRemoteProcedureRequestHandler : IRemoteProcedureRequestHandler
    {
        private readonly IHttpClientFactory _target;

        public HttpRemoteProcedureRequestHandler(IHttpClientFactory target)
        {
            _target = target;
        }

        public bool CanHandleRequestType(Type requestType)
        {
            return requestType == typeof(RemoteHttpRequest);
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
            if (parsedMessage.Item2 == typeof(RemoteHttpRequest))
            {
                RemoteHttpRequest req = parsedMessage.Item1 as RemoteHttpRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedHttp");

                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        using (RecyclableMemoryStream wireStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                        {
                            StreamSocket socketWrapper = new StreamSocket(wireStream);
                            RemoteProcedureResponse<ArraySegment<byte>> successResponse;

                            try
                            {
                                // Create HTTP client
                                // OPT: Clients can potentially be pooled based on URI rather than creating a new one every time
                                Uri baseUri = new Uri(string.Format("{0}://{1}:{2}",
                                    req.UseSSL ? "https" : "http",
                                    req.TargetHost,
                                    req.TargetPort));
                                IHttpClient actualClient = _target.CreateHttpClient(baseUri, traceLogger);

                                // Parse http request from wire
                                traceLogger.Log("Reading virtual HTTP request");
                                wireStream.Write(req.WireRequest.Array, req.WireRequest.Offset, req.WireRequest.Count);
                                wireStream.Seek(0, SeekOrigin.Begin);

                                using (HttpRequest httpReq = await HttpHelpers.ReadRequestFromSocket(
                                    socketWrapper,
                                    HttpVersion.HTTP_1_1,
                                    traceLogger,
                                    cancelToken,
                                    threadLocalTime).ConfigureAwait(false))
                                {
                                    traceLogger.Log("Sending actual HTTP request");
                                    httpReq.MakeProxied();

                                    // Make actual request by proxying the incoming request from client
                                    using (HttpResponse httpResp = await actualClient.SendRequestAsync(httpReq, cancelToken, threadLocalTime, traceLogger).ConfigureAwait(false))
                                    {
                                        traceLogger.Log("Got actual HTTP response");
                                        if (httpResp != null)
                                        {
                                            try
                                            {
                                                // Convert http response to wire format
                                                wireStream.Seek(0, SeekOrigin.Begin);
                                                wireStream.SetLength(0);
                                                httpResp.MakeProxied();
                                                traceLogger.Log("Writing virtual HTTP response to memory stream");
                                                await HttpHelpers.WriteResponseToSocket(
                                                    httpResp,
                                                    HttpVersion.HTTP_1_1,
                                                    socketWrapper,
                                                    cancelToken,
                                                    threadLocalTime,
                                                    traceLogger,
                                                    () => "Remoted HTTP proxy").ConfigureAwait(false);
                                                ArraySegment<byte> returnVal = new ArraySegment<byte>(wireStream.ToArray());
                                                successResponse = new RemoteProcedureResponse<ArraySegment<byte>>(req.MethodName, returnVal);
                                            }
                                            finally
                                            {
                                                await httpResp.FinishAsync(cancelToken, threadLocalTime).ConfigureAwait(false);
                                                traceLogger.Log("Finished actual HTTP response");
                                            }
                                        }
                                        else
                                        {
                                            // Null response from actual client. Send an exception message back to the caller.
                                            traceLogger.Log("Actual HTTP client call returned null");
                                            successResponse = new RemoteProcedureResponse<ArraySegment<byte>>(req.MethodName, new Exception("The HTTP call failed."));
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                traceLogger.Log(e);
                                successResponse = new RemoteProcedureResponse<ArraySegment<byte>>(req.MethodName, e);
                            }

                            traceLogger.Log("Serializing virtual HTTP post office response of size " + successResponse.ReturnVal.Count + " bytes");
                            PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(successResponse, traceLogger);
                            traceLogger.Log("Serialized HTTP response length is " + serializedResponse.Length + " bytes");
                            MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                            interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                            interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                            await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                            traceLogger.Log("Wrote serialized response back to post office");
                        }
                    }
                    catch (Exception e)
                    {
                        traceLogger.Log(e);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else
            {
                return DurandalTaskExtensions.NoOpTask;
            }
        }
    }
}
