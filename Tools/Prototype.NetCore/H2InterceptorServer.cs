using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2;
using Durandal.Common.Net.Http2.Frames;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Org.BouncyCastle.Bcpg;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Prototype.NetCore
{
    public class H2InterceptorServer : ISocketServerDelegate
    {
        private readonly ISocketServer _baseServer;
        private readonly ISocketFactory _socketFactory;
        private readonly ILogger _logger;
        private readonly TcpConnectionConfiguration _downstreamConfig;

        public H2InterceptorServer(
            ISocketServer baseServer,
            ISocketFactory socketFactory,
            TcpConnectionConfiguration downstreamConfig,
            ILogger logger)
        {
            _baseServer = baseServer.AssertNonNull(nameof(baseServer));
            _socketFactory = socketFactory.AssertNonNull(nameof(socketFactory));
            _downstreamConfig = downstreamConfig.AssertNonNull(nameof(downstreamConfig));
            _logger = logger.AssertNonNull(nameof(logger));
            _baseServer.RegisterSubclass(this);
        }

        /// <inheritdoc />
        public Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _baseServer.StartServer(serverName, cancelToken, realTime);
        }

        public async Task HandleSocketConnection(ISocket clientSocket, ServerBindingInfo bindPoint, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ILogger traceLogger = _logger.CreateTraceLogger(Guid.NewGuid());
            traceLogger.Log("Incoming connection");

            if (!clientSocket.Features.ContainsKey(SocketFeature.NegotiatedHttp2Support))
            {
                traceLogger.Log("Incoming connection did not negotiate h2 support");
                await clientSocket.Disconnect(cancelToken, realTime, NetworkDuplex.ReadWrite, allowLinger: false).ConfigureAwait(false);
                return;
            }

            // Read PRI * HTTP/2 block
            byte[] scratch = new byte[Http2Constants.HTTP2_CONNECTION_PREFACE.Length];

            int initialBytesRead = await clientSocket.ReadAsync(scratch, 0, Http2Constants.HTTP2_CONNECTION_PREFACE.Length, cancelToken, realTime).ConfigureAwait(false);
            if (initialBytesRead != Http2Constants.HTTP2_CONNECTION_PREFACE.Length ||
                !ArrayExtensions.ArrayEquals(scratch, 0, Http2Constants.HTTP2_CONNECTION_PREFACE, 0, initialBytesRead))
            {
                traceLogger.Log("Incoming connection did not send PRI * HTTP/2");
                return;
            }

            ISocket downstreamSocket = await _socketFactory.Connect(_downstreamConfig, traceLogger.Clone("DownstreamSocket"), cancelToken, realTime);

            if (!downstreamSocket.Features.ContainsKey(SocketFeature.NegotiatedHttp2Support))
            {
                traceLogger.Log("Outgoing connection did not negotiate h2 support");
                await downstreamSocket.Disconnect(cancelToken, realTime, NetworkDuplex.ReadWrite, allowLinger: false).ConfigureAwait(false);
                return;
            }

            // Send PRI * downstream
            await downstreamSocket.WriteAsync(scratch, 0, Http2Constants.HTTP2_CONNECTION_PREFACE.Length, cancelToken, realTime);

            traceLogger.Log("Created downstream session");

            Task clientToServerTask = Task.Run(async () => await ProxyHttp2(clientSocket, downstreamSocket, traceLogger.Clone("Browser"), cancelToken, realTime));
            Task serverToClientTask = Task.Run(async () => await ProxyHttp2(downstreamSocket, clientSocket, traceLogger.Clone("Server"), cancelToken, realTime));

            await clientToServerTask;
            await serverToClientTask;
        }

        private static async Task ProxyHttp2(ISocket incomingSocket, ISocket outgoingSocket, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            Http2Settings remoteSettings = Http2Settings.Default();
            remoteSettings.MaxFrameSize = 16777215;

            byte[] scratch = new byte[remoteSettings.MaxFrameSize];
            logger.Log("Pipe opened");

            while (!cancelToken.IsCancellationRequested)
            {
                Http2Frame parsedFrame;
                try
                {
                    parsedFrame = await Http2Frame.ReadFromSocket(
                        incomingSocket,
                        remoteSettings.MaxFrameSize,
                        scratch,
                        true,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    if (parsedFrame == null)
                    {
                        logger.Log("Got null or unrecognized HTTP2 frame", LogLevel.Wrn);
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.Log("Thread is shutting down", LogLevel.Wrn);
                    continue;
                }
                catch (Http2ProtocolException e)
                {
                    logger.Log("Failed to parse incoming HTTP2 frame", LogLevel.Err);
                    logger.Log(e, LogLevel.Err);
                    continue;
                }

                // This scope will handle disposal of the pooled frame data buffer automatically
                using (Http2Frame incomingFrame = parsedFrame)
                {
                    logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata,
                        "FRAME Type {0} Stream {1} Flags {2:X2} Len {3}",
                        incomingFrame.FrameType, incomingFrame.StreamId, incomingFrame.Flags, incomingFrame.PayloadLength);

                    if (incomingFrame.FrameType == Http2FrameType.Headers)
                    {
                    }
                    else if (incomingFrame.FrameType == Http2FrameType.Data)
                    {
                    }
                    else if (incomingFrame.FrameType == Http2FrameType.Settings)
                    {
                        Http2SettingsFrame convertedFrame = incomingFrame as Http2SettingsFrame;
                        if (!convertedFrame.Ack)
                        {
                            logger.Log("   EnablePush: " + convertedFrame.Settings.EnablePush);
                            logger.Log("   HeaderTableSize: " + convertedFrame.Settings.HeaderTableSize);
                            logger.Log("   InitialWindowSize: " + convertedFrame.Settings.InitialWindowSize);
                            logger.Log("   MaxConcurrentStreams: " + convertedFrame.Settings.MaxConcurrentStreams);
                            logger.Log("   MaxFrameSize: " + convertedFrame.Settings.MaxFrameSize);
                            logger.Log("   MaxHeaderListSize: " + convertedFrame.Settings.MaxHeaderListSize);
                        }
                    }
                    else if (incomingFrame.FrameType == Http2FrameType.RstStream)
                    {
                    }
                    else if (incomingFrame.FrameType == Http2FrameType.GoAway)
                    {
                    }
                    else if (incomingFrame.FrameType == Http2FrameType.Ping)
                    {
                    }
                    else if (incomingFrame.FrameType == Http2FrameType.Priority)
                    {
                    }
                    else if (incomingFrame.FrameType == Http2FrameType.PushPromise)
                    {
                    }
                    else if (incomingFrame.FrameType == Http2FrameType.WindowUpdate)
                    {
                        Http2WindowUpdateFrame convertedFrame = incomingFrame as Http2WindowUpdateFrame;
                        logger.Log("Update size " + convertedFrame.WindowSizeIncrement);
                    }
                    else if (incomingFrame.FrameType == Http2FrameType.Continuation)
                    {
                    }
                    else
                    {
                    }

                    await incomingFrame.WriteToSocket(outgoingSocket, cancelToken, realTime).ConfigureAwait(false);
                    //await outgoingSocket.FlushAsync(cancelToken).ConfigureAwait(false);
                }
            }

            logger.Log("Pipe closed");
        }
    }
}
