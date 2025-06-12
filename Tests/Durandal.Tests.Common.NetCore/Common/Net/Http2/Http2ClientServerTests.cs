using Durandal.Common.Collections;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Net.Http2
{
    [TestClass]
    public class Http2ClientServerTests
    {
        [TestMethod]
        public async Task TestHttp2ClientServerBasic()
        {
            ILogger logger = new ConsoleLogger("Http2Test", LogLevel.All);
            WeakPointer<IMetricCollector> metrics = NullMetricCollector.WeakSingleton;
            IRandom random = new FastRandom();
            byte[] expectedData = new byte[1000];
            random.NextBytes(expectedData);
            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                CancellationToken testCancel = testCancelSource.Token;
                LockStepRealTimeProvider testGlobalRealTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair(TimeSpan.FromMilliseconds(200));
                using (DirectSocket clientSocket = socketPair.ClientSocket)
                using (DirectSocket serverSocket = socketPair.ServerSocket)
                using (Http2Session clientSession = new Http2Session(clientSocket, logger.Clone("ClientSession"), new Http2SessionPreferences(), metrics, DimensionSet.Empty))
                using (Http2Session serverSession = new Http2Session(serverSocket, logger.Clone("ServerSession"), new Http2SessionPreferences(), metrics, DimensionSet.Empty))
                {
                    IRealTimeProvider clientTime = testGlobalRealTime.Fork("ClientTime");
                    Task clientTask = Task.Run(async () =>
                    {
                        try
                        {
                            // 0ms: Initiate session
                            await clientSession.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                            await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                            // 100ms: Send request using default settings
                            using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/"))
                            {
                                requestToSend.RequestHeaders.Add("User-Agent", "Local unit test case");
                                logger.Log("Client sending request");
                                using (HttpResponse response = await clientSession.MakeHttpRequest(
                                    requestToSend,
                                    logger.Clone("HttpRequest"),
                                    testCancel,
                                    clientTime).ConfigureAwait(false))
                                {
                                    // should be ~500ms by the time the response comes
                                    logger.Log("Client got response");
                                    Assert.IsNotNull(response);
                                    Assert.AreEqual(200, response.ResponseCode);
                                    ArraySegment<byte> clientSideResponseData = await response.ReadContentAsByteArrayAsync(testCancel, clientTime).ConfigureAwait(false);
                                    Assert.IsNotNull(clientSideResponseData);
                                    Assert.IsTrue(ArrayExtensions.ArrayEquals(clientSideResponseData, new ArraySegment<byte>(expectedData)));
                                    await response.FinishAsync(testCancel, clientTime).ConfigureAwait(false);
                                    logger.Log("Client finished response");
                                }

                                await clientTime.WaitAsync(TimeSpan.FromMilliseconds(500), testCancel).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            clientTime.Merge();
                        }
                    });

                    {
                        IRealTimeProvider serverTime = testGlobalRealTime;
                        // 0ms: Initiate session
                        await serverSession.BeginServerSession(testCancel, serverTime, Http2Settings.ServerDefault(), "localhost", "https").ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(200), testCancel).ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);

                        // 250ms: Client has sent settings
                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                        // 350ms: Server receives request and begins sending response
                        logger.Log("Server waiting for request");
                        IHttpServerContext serverContext = 
                            await serverSession.HandleIncomingHttpRequest(
                                logger.Clone("IncomingRequest"),
                                testCancel,
                                serverTime,
                                null).ConfigureAwait(false);

                        using (HttpResponse response = HttpResponse.OKResponse())
                        {
                            response.SetContent(expectedData, "application/json");
                            logger.Log("Server sending response");
                            await serverContext.WritePrimaryResponse(response, logger.Clone("SendingResponse"), testCancel, serverTime);
                            logger.Log("Server sent response");
                        }

                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(650), testCancel).ConfigureAwait(false);
                    }

                    testGlobalRealTime.Step(TimeSpan.FromSeconds(2), 100);
                    await clientTask;
                }
            }
        }

        [TestMethod]
        public async Task TestHttp2ClientServerBasicLoop10()
        {
            ILogger logger = new ConsoleLogger("Http2Test", LogLevel.All);
            WeakPointer<IMetricCollector> metrics = NullMetricCollector.WeakSingleton;
            IRandom random = new FastRandom();
            byte[] expectedData = new byte[10000];
            random.NextBytes(expectedData);
            Http2Settings clientSettings = Http2Settings.Default();
            // InitialWindowSize = 1000000
            Http2Settings serverSettings = Http2Settings.ServerDefault();
            // InitialWindowSize = 1000000
            Http2SessionPreferences h2Preferences = new Http2SessionPreferences();
            // DesiredGlobalConnectionFlowWindow = 1000000

            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                CancellationToken testCancel = testCancelSource.Token;
                LockStepRealTimeProvider testGlobalRealTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair(TimeSpan.FromMilliseconds(200));
                using (DirectSocket clientSocket = socketPair.ClientSocket)
                using (DirectSocket serverSocket = socketPair.ServerSocket)
                using (Http2Session clientSession = new Http2Session(clientSocket, logger.Clone("ClientSession"), h2Preferences, metrics, DimensionSet.Empty))
                using (Http2Session serverSession = new Http2Session(serverSocket, logger.Clone("ServerSession"), h2Preferences, metrics, DimensionSet.Empty))
                {
                    IRealTimeProvider clientTime = testGlobalRealTime.Fork("ClientTime");
                    Task clientTask = Task.Run(async () =>
                    {
                        try
                        {
                            await clientSession.BeginClientSession(testCancel, clientTime, clientSettings, "localhost", "https").ConfigureAwait(false);
                            await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                            for (int loop = 0; loop < 10; loop++)
                            {
                                logger.Log("Client loop " + loop);
                                using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/"))
                                {
                                    requestToSend.RequestHeaders.Add("User-Agent", "Local unit test case");
                                    using (HttpResponse response = await clientSession.MakeHttpRequest(
                                        requestToSend,
                                        logger.Clone("HttpRequest"),
                                        testCancel,
                                        clientTime).ConfigureAwait(false))
                                    {
                                        Assert.IsNotNull(response);
                                        Assert.AreEqual(200, response.ResponseCode);
                                        ArraySegment<byte> clientSideResponseData = await response.ReadContentAsByteArrayAsync(testCancel, clientTime).ConfigureAwait(false);
                                        Assert.IsNotNull(clientSideResponseData);
                                        Assert.IsTrue(ArrayExtensions.ArrayEquals(clientSideResponseData, new ArraySegment<byte>(expectedData)));
                                        await response.FinishAsync(testCancel, clientTime).ConfigureAwait(false);
                                    }
                                }

                                await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            clientTime.Merge();
                        }
                    });

                    {
                        IRealTimeProvider serverTime = testGlobalRealTime;
                        await serverSession.BeginServerSession(testCancel, serverTime, serverSettings, "localhost", "https").ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(200), testCancel).ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);

                        for (int loop = 0; loop < 10; loop++)
                        {
                            logger.Log("Server loop " + loop);
                            IHttpServerContext serverContext =
                                await serverSession.HandleIncomingHttpRequest(
                                    logger.Clone("IncomingRequest"),
                                    testCancel,
                                    serverTime,
                                    null).ConfigureAwait(false);

                            using (HttpResponse response = HttpResponse.OKResponse())
                            {
                                response.SetContent(expectedData, "application/json");
                                await serverContext.WritePrimaryResponse(response, logger.Clone("SendingResponse"), testCancel, serverTime);
                            }

                            await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);
                        }

                        // we have to wait a bit of time for the queued server response frames for the last response
                        // to actually reach the client
                        await serverTime.WaitAsync(TimeSpan.FromSeconds(10), testCancel).ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromSeconds(10), testCancel).ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromSeconds(10), testCancel).ConfigureAwait(false);
                    }

                    await clientTask;
                }
            }
        }

        [TestMethod]
        public async Task TestHttp2ClientServerLarge()
        {
            ILogger logger = new ConsoleLogger("Http2Test", LogLevel.All);
            WeakPointer<IMetricCollector> metrics = NullMetricCollector.WeakSingleton;
            IRandom random = new FastRandom();
            byte[] requestData = new byte[200000];
            byte[] responseData = new byte[200000];
            random.NextBytes(requestData);
            random.NextBytes(responseData);
            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                CancellationToken testCancel = testCancelSource.Token;
                LockStepRealTimeProvider testGlobalRealTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair(TimeSpan.FromMilliseconds(20));
                using (DirectSocket clientSocket = socketPair.ClientSocket)
                using (DirectSocket serverSocket = socketPair.ServerSocket)
                using (Http2Session clientSession = new Http2Session(clientSocket, logger.Clone("ClientSession"), new Http2SessionPreferences(), metrics, DimensionSet.Empty))
                using (Http2Session serverSession = new Http2Session(serverSocket, logger.Clone("ServerSession"), new Http2SessionPreferences(), metrics, DimensionSet.Empty))
                {
                    IRealTimeProvider clientTime = testGlobalRealTime.Fork("ClientTime");
                    Task clientTask = Task.Run(async () =>
                    {
                        try
                        {
                            await clientSession.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);

                            using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/upload.php", "POST"))
                            {
                                requestToSend.SetContent(requestData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                                requestToSend.RequestHeaders.Add("User-Agent", "Local unit test case");

                                using (HttpResponse response = await clientSession.MakeHttpRequest(
                                    requestToSend,
                                    logger.Clone("HttpRequest"),
                                    testCancel,
                                    clientTime).ConfigureAwait(false))
                                {
                                    Assert.IsNotNull(response);
                                    Assert.AreEqual(200, response.ResponseCode);
                                    ArraySegment<byte> clientSideResponseData = await response.ReadContentAsByteArrayAsync(testCancel, clientTime).ConfigureAwait(false);
                                    Assert.IsNotNull(clientSideResponseData);
                                    Assert.IsTrue(ArrayExtensions.ArrayEquals(clientSideResponseData, new ArraySegment<byte>(responseData)));
                                    await response.FinishAsync(testCancel, clientTime).ConfigureAwait(false);
                                }
                            }
                        }
                        finally
                        {
                            clientTime.Merge();
                        }
                    });

                    IRealTimeProvider serverTime = testGlobalRealTime.Fork("ServerTime");
                    Task serverTask = Task.Run(async () =>
                    {
                        try
                        {
                            await serverSession.BeginServerSession(testCancel, serverTime, Http2Settings.ServerDefault(), "localhost", "https").ConfigureAwait(false);

                            IHttpServerContext serverContext =
                                await serverSession.HandleIncomingHttpRequest(
                                    logger.Clone("IncomingRequest"),
                                    testCancel,
                                    serverTime,
                                    null).ConfigureAwait(false);

                            ArraySegment<byte> serverSideRequestData = await serverContext.HttpRequest.ReadContentAsByteArrayAsync(testCancel, serverTime).ConfigureAwait(false);
                            Assert.IsNotNull(serverSideRequestData);
                            Assert.IsTrue(ArrayExtensions.ArrayEquals(serverSideRequestData, new ArraySegment<byte>(requestData)));

                            using (HttpResponse response = HttpResponse.OKResponse())
                            {
                                response.SetContent(responseData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                                await serverContext.WritePrimaryResponse(response, logger.Clone("SendingResponse"), testCancel, serverTime);
                            }
                        }
                        finally
                        {
                            serverTime.Merge();
                        }
                    });

                    testGlobalRealTime.Step(TimeSpan.FromSeconds(10), 100);
                    await clientTask;
                    await serverTask;
                }
            }
        }

        [TestMethod]
        public async Task TestHttp2ClientServerTrailers()
        {
            ILogger logger = new ConsoleLogger("Http2Test", LogLevel.All);
            WeakPointer<IMetricCollector> metrics = NullMetricCollector.WeakSingleton;
            IRandom random = new FastRandom();
            byte[] expectedData = new byte[10000];
            random.NextBytes(expectedData);
            Http2Settings clientSettings = Http2Settings.Default();
            // InitialWindowSize = 1000000
            Http2Settings serverSettings = Http2Settings.ServerDefault();
            // InitialWindowSize = 1000000
            Http2SessionPreferences h2Preferences = new Http2SessionPreferences();
            // DesiredGlobalConnectionFlowWindow = 1000000

            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(150)))
            {
                CancellationToken testCancel = testCancelSource.Token;
                LockStepRealTimeProvider testGlobalRealTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair(TimeSpan.FromMilliseconds(200));
                using (DirectSocket clientSocket = socketPair.ClientSocket)
                using (DirectSocket serverSocket = socketPair.ServerSocket)
                using (Http2Session clientSession = new Http2Session(clientSocket, logger.Clone("ClientSession"), h2Preferences, metrics, DimensionSet.Empty))
                using (Http2Session serverSession = new Http2Session(serverSocket, logger.Clone("ServerSession"), h2Preferences, metrics, DimensionSet.Empty))
                {
                    IRealTimeProvider clientTime = testGlobalRealTime.Fork("ClientTime");
                    Task clientTask = Task.Run(async () =>
                    {
                        try
                        {
                            await clientSession.BeginClientSession(testCancel, clientTime, clientSettings, "localhost", "https").ConfigureAwait(false);
                            await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                            for (int loop = 0; loop < 10; loop++)
                            {
                                logger.Log("Client loop " + loop);
                                using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/"))
                                {
                                    requestToSend.RequestHeaders.Add("User-Agent", "Local unit test case");
                                    requestToSend.RequestHeaders.Add("TE", "trailers");
                                    using (HttpResponse response = await clientSession.MakeHttpRequest(
                                        requestToSend,
                                        logger.Clone("HttpRequest"),
                                        testCancel,
                                        clientTime).ConfigureAwait(false))
                                    {
                                        Assert.IsNotNull(response);
                                        Assert.AreEqual(200, response.ResponseCode);
                                        Assert.IsTrue(response.ResponseHeaders.ContainsKey("Trailer"));
                                        ArraySegment<byte> clientSideResponseData = await response.ReadContentAsByteArrayAsync(testCancel, clientTime).ConfigureAwait(false);
                                        Assert.IsNotNull(clientSideResponseData);
                                        Assert.IsTrue(ArrayExtensions.ArrayEquals(clientSideResponseData, new ArraySegment<byte>(expectedData)));
                                        Assert.IsNotNull(response.ResponseTrailers);
                                        Assert.IsTrue(response.ResponseTrailers.ContainsKey("X-Response-Time"));
                                        await response.FinishAsync(testCancel, clientTime).ConfigureAwait(false);
                                    }
                                }

                                await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            clientTime.Merge();
                        }
                    });

                    {
                        IRealTimeProvider serverTime = testGlobalRealTime;
                        await serverSession.BeginServerSession(testCancel, serverTime, serverSettings, "localhost", "https").ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(200), testCancel).ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);

                        for (int loop = 0; loop < 10; loop++)
                        {
                            logger.Log("Server loop " + loop);
                            IHttpServerContext serverContext =
                                await serverSession.HandleIncomingHttpRequest(
                                    logger.Clone("IncomingRequest"),
                                    testCancel,
                                    serverTime,
                                    null).ConfigureAwait(false);

                            Assert.IsTrue(serverContext.SupportsTrailers);
                            List<string> trailerNames = new List<string>();
                            trailerNames.Add("X-Response-Time");
                            using (HttpResponse response = HttpResponse.OKResponse())
                            {
                                response.SetContent(expectedData, "application/json");
                                await serverContext.WritePrimaryResponse(
                                    response,
                                    logger.Clone("SendingResponse"),
                                    testCancel,
                                    serverTime,
                                    trailerNames,
                                    (string trailerName) => Task.FromResult("2020-10-03T12:00:34"));
                            }

                            await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);
                        }

                        // we have to wait a bit of time for the queued server response frames for the last response
                        // to actually reach the client
                        await serverTime.WaitAsync(TimeSpan.FromSeconds(10), testCancel).ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromSeconds(10), testCancel).ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromSeconds(10), testCancel).ConfigureAwait(false);
                    }

                    await clientTask;
                }
            }
        }
    }
}
