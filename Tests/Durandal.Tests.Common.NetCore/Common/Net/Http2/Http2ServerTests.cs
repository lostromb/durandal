using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2;
using Durandal.Common.Net.Http2.Frames;
using Durandal.Common.Net.Http2.HPack;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Net.Http2
{
    [TestClass]
    public class Http2ServerTests
    {
        [TestMethod]
        public async Task TestHttp2ServerBasicEcho()
        {
            byte[] requestData = new byte[1000];
            IRandom rand = new FastRandom();
            rand.NextBytes(requestData);

            await RunHttp2ServerTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    // This is just a server that echoes back one request
                    await session.BeginServerSession(testCancel, serverTime, Http2Settings.ServerDefault(), "localhost", "https").ConfigureAwait(false);
                    IHttpServerDelegate serverDelegate = new EchoServerDelegate();
                    IHttpServerContext context = await session.HandleIncomingHttpRequest(logger, testCancel, serverTime, serverDelegate).ConfigureAwait(false);
                    Assert.IsNotNull(context);
                    try
                    {
                        await serverDelegate.HandleConnection(context, testCancel, serverTime).ConfigureAwait(false);
                        Assert.IsTrue(context.PrimaryResponseStarted);
                    }
                    finally
                    {
                        context.HttpRequest?.Dispose();
                    }
                },
                async (ISocket clientSocket, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings clientSettings = Http2Settings.Default();

                    // 100ms: Send PRI * HTTP header
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    logger.Log("Sending client PRI");
                    await Http2TestCommon.WriteClientConnectionPrefix(clientSocket, testCancel, clientTime).ConfigureAwait(false);

                    // 125ms: Send settings
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    logger.Log("Sending client settings");
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(clientSettings).WriteToSocket(clientSocket, testCancel, clientTime).ConfigureAwait(false);

                    // 150ms: Send request headers
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    logger.Log("Sending client headers");
                    HttpHeaders requestHeaders = new HttpHeaders();
                    requestHeaders.Add("Content-Type", "application/json");
                    requestHeaders.Add("Content-Length", requestData.Length.ToString(CultureInfo.InvariantCulture));
                    foreach (var headerFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(requestHeaders, 200, 1, clientSettings, hPackEncoder, endOfStream: false))
                    {
                        logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 1);
                        await headerFrame.WriteToSocket(clientSocket, testCancel, clientTime).ConfigureAwait(false);
                        headerFrame.Dispose();
                    }

                    // 200ms: Send request data
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);
                    logger.Log("Sending client data");
                    PooledBuffer<byte> requestDataBuffer = BufferPool<byte>.Rent(requestData.Length);
                    ArrayExtensions.MemCopy(requestData, 0, requestDataBuffer.Buffer, 0, requestData.Length);
                    await Http2DataFrame.CreateOutgoing(requestDataBuffer, streamId: 1, endStream: true)
                        .WriteToSocket(clientSocket, testCancel, clientTime).ConfigureAwait(false);

                    // 350ms: Server should have sent settings
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(150), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(clientSocket, logger, clientSettings, frameQueue, false, testCancel, clientTime,
                        (frame) => !frame.Ack && frame.Settings != null && frame.StreamId == 0);

                    // 650ms: Server should send a response
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);
                    
                    await Http2TestCommon.ExpectFrame<Http2HeadersFrame>(clientSocket, logger, clientSettings, frameQueue, false, testCancel, clientTime,
                        (frame) => frame.EndHeaders && !frame.EndStream && frame.StreamId == 1);

                    int amountReadFromClient = 0;
                    while (amountReadFromClient < requestData.Length)
                    {
                        using (Http2DataFrame requestDataFrame = await Http2TestCommon.ExpectFrameAndReturn<Http2DataFrame>(
                            clientSocket, logger, clientSettings, frameQueue, false, testCancel, clientTime,
                            (frame) => !frame.EndStream && frame.StreamId == 1))
                        {
                            amountReadFromClient += requestDataFrame.PayloadLength;
                        }
                    }

                    await Http2TestCommon.ExpectFrame<Http2DataFrame>(clientSocket, logger, clientSettings, frameQueue, false, testCancel, clientTime,
                        (frame) => frame.EndStream && frame.StreamId == 1);

                    // At some point there should have been a SETTINGS ACK also
                    //await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    //await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(clientSocket, logger, clientSettings, frameQueue, testCancel, clientTime,
                    //    (frame) => frame.Ack && frame.StreamId == 0);
                },
                totalTestTime: TimeSpan.FromSeconds(2),
                testStepIncrement: TimeSpan.FromMilliseconds(100));
        }

        private class EchoServerDelegate : IHttpServerDelegate
        {
            public async Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse response = HttpResponse.OKResponse();
                ArraySegment<byte> bigContent = await context.HttpRequest.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                response.SetContent(bigContent, HttpConstants.MIME_TYPE_OCTET_STREAM);
                await context.WritePrimaryResponse(response, NullLogger.Singleton, cancelToken, realTime);
            }
        }

        private delegate Task Http2TestClientHandler(ISocket clientSocket, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel);
        private delegate Task Http2TestServerHandler(Http2Session session, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel);

        private static async Task RunHttp2ServerTest(
            Http2TestServerHandler serverThreadImpl,
            Http2TestClientHandler clientThreadImpl,
            TimeSpan? totalTestTime = null,
            TimeSpan? testStepIncrement = null,
            Http2SessionPreferences sessionPrefs = null)
        {
            ILogger logger = new ConsoleLogger("Http2Test", LogLevel.All);
            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken testCancel = testCancelSource.Token;
                LockStepRealTimeProvider testGlobalRealTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair(TimeSpan.FromMilliseconds(200));
                using (DirectSocket clientSocket = socketPair.ClientSocket)
                using (DirectSocket serverSocket = socketPair.ServerSocket)
                using (Http2Session session = new Http2Session(
                    serverSocket,
                    logger.Clone("ServerSession"),
                    sessionPrefs ?? new Http2SessionPreferences(),
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty))
                {
                    IRealTimeProvider serverTime = testGlobalRealTime.Fork("ServerTime");
                    Task serverTask = Task.Run(async () =>
                    {
                        try
                        {
                            await serverThreadImpl(session, logger.Clone("Server"), serverTime, testCancel).ConfigureAwait(false);
                        }
                        finally
                        {
                            serverTime.Merge();
                        }
                    });

                    // Simulate the server process here, using raw frames so we can exactly control timing and format
                    // of data that the client has to process.
                    await clientThreadImpl(clientSocket, logger.Clone("Client"), testGlobalRealTime, testCancel).ConfigureAwait(false);

                    TimeSpan testTime = totalTestTime.GetValueOrDefault(TimeSpan.FromSeconds(5));
                    int testIncrement = (int)Math.Max(1, testStepIncrement.GetValueOrDefault(TimeSpan.FromMilliseconds(100)).TotalMilliseconds);
                    testGlobalRealTime.Step(testTime, testIncrement);
                    await serverTask;
                }
            }
        }
    }
}
