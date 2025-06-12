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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Net.Http2
{
    [TestClass]
    public class Http2ClientTests
    {
        [TestMethod]
        public async Task TestHttp2ClientBasicGet()
        {
            byte[] expectedData = new byte[1000];
            new FastRandom().NextBytes(expectedData);

            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 100ms: Send request using default settings
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/"))
                    {
                        requestToSend.RequestHeaders.Add("User-Agent", "Local unit test case");
                        using (HttpResponse response = await session.MakeHttpRequest(
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
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings serverSettings = Http2Settings.ServerDefault();
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);

                    // 325ms: Server reads connection prefix and client settings
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => !frame.Ack && frame.Settings != null && frame.StreamId == 0);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);

                    // 350ms: Server sends settings
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // Send settings ACK to client
                    await Http2SettingsFrame.CreateOutgoingAckFrame().WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 450ms: Read the headers for an incoming request
                    await Http2TestCommon.ExpectFrame<Http2HeadersFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndHeaders && frame.EndStream && frame.StreamId == 1);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);

                    // 500ms: Server sends response headers
                    HttpHeaders responseHeaders = new HttpHeaders();
                    responseHeaders.Add("Content-Type", "application/json");
                    responseHeaders.Add("Content-Length", expectedData.Length.ToString(CultureInfo.InvariantCulture));
                    foreach (var headerFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(responseHeaders, 200, 1, serverSettings, hPackEncoder, endOfStream: false))
                    {
                        logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 1);
                        await headerFrame.WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                        headerFrame.Dispose();
                    }

                    // 500ms: Server sends data
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    PooledBuffer<byte> serverSideResponseData = BufferPool<byte>.Rent(expectedData.Length);
                    ArrayExtensions.MemCopy(expectedData, 0, serverSideResponseData.Buffer, 0, expectedData.Length);
                    await Http2DataFrame.CreateOutgoing(serverSideResponseData, streamId: 1, endStream: true).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                },
                totalTestTime: TimeSpan.FromSeconds(1),
                testStepIncrement: TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public async Task TestHttp2ClientBasicPost()
        {
            byte[] expectedResponseData = new byte[1000];
            byte[] requestData = new byte[100000];
            IRandom rand = new FastRandom();
            rand.NextBytes(expectedResponseData);
            rand.NextBytes(requestData);

            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 100ms: Send request using default settings
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/upload.php", "POST"))
                    {
                        requestToSend.RequestHeaders.Add("User-Agent", "Local unit test case");
                        requestToSend.SetContent(requestData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                        using (HttpResponse response = await session.MakeHttpRequest(
                            requestToSend,
                            logger.Clone("HttpRequest"),
                            testCancel,
                            clientTime).ConfigureAwait(false))
                        {
                            Assert.IsNotNull(response);
                            Assert.AreEqual(200, response.ResponseCode);
                            ArraySegment<byte> clientSideResponseData = await response.ReadContentAsByteArrayAsync(testCancel, clientTime).ConfigureAwait(false);
                            Assert.IsNotNull(clientSideResponseData);
                            Assert.IsTrue(ArrayExtensions.ArrayEquals(clientSideResponseData, new ArraySegment<byte>(expectedResponseData)));
                            await response.FinishAsync(testCancel, clientTime).ConfigureAwait(false);
                        }
                    }
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings serverSettings = Http2Settings.ServerDefault();
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);

                    // 325ms: Server reads connection prefix and client settings
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => !frame.Ack && frame.Settings != null && frame.StreamId == 0);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);

                    // 350ms: Server sends settings
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // Send settings ACK to client
                    await Http2SettingsFrame.CreateOutgoingAckFrame().WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 450ms: Read the headers, send a window update to tell the client to proceed
                    await Http2TestCommon.ExpectFrame<Http2HeadersFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndHeaders && !frame.EndStream && frame.StreamId == 1);
                    await Http2WindowUpdateFrame.CreateOutgoing(0, 1000000).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2WindowUpdateFrame.CreateOutgoing(1, 1000000).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // 850ms: wait for remote end to process window updates
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(400), testCancel).ConfigureAwait(false);

                    // 900ms: Read all data frames for the request
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);

                    int amountReadFromClient = 0;
                    while (amountReadFromClient < requestData.Length)
                    {
                        using (Http2DataFrame requestDataFrame = await Http2TestCommon.ExpectFrameAndReturn<Http2DataFrame>(
                            serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                            (frame) => !frame.EndStream && frame.StreamId == 1))
                        {
                            amountReadFromClient += requestDataFrame.PayloadLength;
                        }
                    }

                    await Http2TestCommon.ExpectFrame<Http2DataFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndStream && frame.StreamId == 1);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);

                    // 950ms: Server sends response headers
                    HttpHeaders responseHeaders = new HttpHeaders();
                    responseHeaders.Add("Content-Type", "application/json");
                    responseHeaders.Add("Content-Length", expectedResponseData.Length.ToString(CultureInfo.InvariantCulture));
                    foreach (var headerFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(responseHeaders, 200, 1, serverSettings, hPackEncoder, endOfStream: false))
                    {
                        logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 1);
                        await headerFrame.WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                        headerFrame.Dispose();
                    }

                    // 975ms: Server sends response data
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    PooledBuffer<byte> serverSideResponseData = BufferPool<byte>.Rent(expectedResponseData.Length);
                    ArrayExtensions.MemCopy(expectedResponseData, 0, serverSideResponseData.Buffer, 0, expectedResponseData.Length);
                    await Http2DataFrame.CreateOutgoing(serverSideResponseData, streamId: 1, endStream: true).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(500), testCancel).ConfigureAwait(false);
                },
                totalTestTime: TimeSpan.FromSeconds(2),
                testStepIncrement: TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public async Task TestHttp2ClientPushPromise()
        {
            byte[] primaryResponseData = new byte[1000];
            byte[] pushPromiseData = new byte[1000];
            new FastRandom().NextBytes(primaryResponseData);
            new FastRandom().NextBytes(pushPromiseData);

            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session and send request
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/"))
                    {
                        requestToSend.RequestHeaders.Add("User-Agent", "Local unit test case");
                        using (HttpResponse response = await session.MakeHttpRequest(
                            requestToSend,
                            logger.Clone("HttpRequest"),
                            testCancel,
                            clientTime).ConfigureAwait(false))
                        {
                            Assert.IsNotNull(response);
                            Assert.AreEqual(200, response.ResponseCode);
                            ArraySegment<byte> clientSideResponseData = await response.ReadContentAsByteArrayAsync(testCancel, clientTime).ConfigureAwait(false);
                            Assert.IsNotNull(clientSideResponseData);
                            Assert.IsTrue(ArrayExtensions.ArrayEquals(clientSideResponseData, new ArraySegment<byte>(primaryResponseData)));
                            await response.FinishAsync(testCancel, clientTime).ConfigureAwait(false);
                        }
                    }

                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 100ms: Send request for the promised stream
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/pushed-stream"))
                    using (HttpResponse response = await session.MakeHttpRequest(
                        requestToSend,
                        logger.Clone("HttpRequest"),
                        testCancel,
                        clientTime).ConfigureAwait(false))
                    {
                        Assert.IsNotNull(response);
                        Assert.AreEqual(200, response.ResponseCode);
                        ArraySegment<byte> clientSideResponseData = await response.ReadContentAsByteArrayAsync(testCancel, clientTime).ConfigureAwait(false);
                        Assert.IsNotNull(clientSideResponseData);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(clientSideResponseData, new ArraySegment<byte>(pushPromiseData)));
                        await response.FinishAsync(testCancel, clientTime).ConfigureAwait(false);
                    }

                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(500), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings serverSettings = Http2Settings.ServerDefault();
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);

                    // 25ms: Server sends settings
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);

                    // 50ms: Server reads connection prefix and client settings
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2SettingsFrame.CreateOutgoingAckFrame().WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 150ms: Read the headers for an incoming request
                    await Http2TestCommon.ExpectFrame<Http2HeadersFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndHeaders && frame.StreamId == 1);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);

                    // 175ms: Server sends push promise frame
                    HttpHeaders promiseRequestHeaders = new HttpHeaders();
                    foreach (var headerFrame in Http2Helpers.ConvertRequestHeadersToPushPromiseHeaderBlock(
                        promiseRequestHeaders,
                        "GET",
                        "/pushed-stream",
                        "localhost",
                        "https",
                        1,
                        2,
                        serverSettings,
                        hPackEncoder,
                        endOfStream: false))
                    {
                        logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 1);
                        await headerFrame.WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                        headerFrame.Dispose();
                    }

                    // Then send response headers for the primary response
                    HttpHeaders responseHeaders = new HttpHeaders();
                    responseHeaders.Add("Content-Type", "application/json");
                    responseHeaders.Add("Content-Length", primaryResponseData.Length.ToString(CultureInfo.InvariantCulture));
                    foreach (var headerFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(responseHeaders, 200, 1, serverSettings, hPackEncoder, endOfStream: false))
                    {
                        logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 1);
                        await headerFrame.WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                        headerFrame.Dispose();
                    }

                    // 200ms: Server sends primary response data followed by headers for the promise response
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    PooledBuffer<byte> serverSideResponseData = BufferPool<byte>.Rent(primaryResponseData.Length);
                    ArrayExtensions.MemCopy(primaryResponseData, 0, serverSideResponseData.Buffer, 0, primaryResponseData.Length);
                    await Http2DataFrame.CreateOutgoing(serverSideResponseData, streamId: 1, endStream: true).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    responseHeaders = new HttpHeaders();
                    responseHeaders.Add("Content-Type", "application/json");
                    responseHeaders.Add("Content-Length", pushPromiseData.Length.ToString(CultureInfo.InvariantCulture));
                    foreach (var headerFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(responseHeaders, 200, 2, serverSettings, hPackEncoder, endOfStream: false))
                    {
                        logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 2);
                        await headerFrame.WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                        headerFrame.Dispose();
                    }

                    // 300ms: Server sends promised response data
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    PooledBuffer<byte> serverSidePromisedData = BufferPool<byte>.Rent(pushPromiseData.Length);
                    ArrayExtensions.MemCopy(pushPromiseData, 0, serverSidePromisedData.Buffer, 0, pushPromiseData.Length);
                    await Http2DataFrame.CreateOutgoing(serverSidePromisedData, streamId: 2, endStream: true).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientSendsWindowUpdates()
        {
            byte[] expectedData = new byte[100000];
            new FastRandom().NextBytes(expectedData);

            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    
                    // 100ms: Client makes request
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/"))
                    {
                        using (HttpResponse response = await session.MakeHttpRequest(
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

                        await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                        await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    }
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings serverSettings = Http2Settings.ServerDefault();
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2SettingsFrame.CreateOutgoingAckFrame().WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 125ms: Server reads incoming request headers
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2HeadersFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndHeaders && frame.StreamId == 1);

                    // 150ms: Server sends response headers
                    HttpHeaders responseHeaders = new HttpHeaders();
                    responseHeaders.Add("Content-Type", "application/json");
                    responseHeaders.Add("Content-Length", expectedData.Length.ToString(CultureInfo.InvariantCulture));
                    foreach (var headerFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(responseHeaders, 200, 1, serverSettings, hPackEncoder, endOfStream: false))
                    {
                        logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 1);
                        await headerFrame.WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                        headerFrame.Dispose();
                    }

                    // 200ms: Server sends data in 10 frames of 10000 bytes each. This is designed to overload the 65536 initial window size
                    // and thus force the client to send a window update frame
                    int frameSize = expectedData.Length / 10;
                    for (int frame = 0; frame < 10; frame++)
                    {
                        int frameStart = frameSize * frame;
                        PooledBuffer<byte> serverSideResponseData = BufferPool<byte>.Rent(frameSize);
                        ArrayExtensions.MemCopy(expectedData, frameStart, serverSideResponseData.Buffer, 0, frameSize);
                        await Http2DataFrame.CreateOutgoing(serverSideResponseData, streamId: 1, endStream: frame == 9).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    }

                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // Assert that client sent a window update frame while we sent our data payload, at least 1 for stream and 1 for the whole connection
                    await Http2TestCommon.ExpectFrame<Http2WindowUpdateFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.StreamId == 1 && frame.WindowSizeIncrement > 0);
                    await Http2TestCommon.ExpectFrame<Http2WindowUpdateFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.StreamId == 0 && frame.WindowSizeIncrement > 0);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientWaitsForGlobalWindowUpdateBeforeSendingData()
        {
            byte[] expectedData = new byte[100000];
            new FastRandom().NextBytes(expectedData);

            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 100ms: Client starts a large POST request
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/", "POST"))
                    {
                        requestToSend.SetContent(expectedData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                        using (HttpResponse response = await session.MakeHttpRequest(
                            requestToSend,
                            logger.Clone("HttpRequest"),
                            testCancel,
                            clientTime).ConfigureAwait(false))
                        {
                            Assert.IsNotNull(response);
                            Assert.AreEqual(200, response.ResponseCode);
                            await response.FinishAsync(testCancel, clientTime).ConfigureAwait(false);
                        }
                    }
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings serverSettings = Http2Settings.ServerDefault();
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);

                    // 325ms: Server reads incoming settings
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => !frame.Ack && frame.StreamId == 0);
                    await Http2SettingsFrame.CreateOutgoingAckFrame().WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // Server reads incoming request header
                    await Http2TestCommon.ExpectFrame<Http2HeadersFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndHeaders && frame.StreamId == 1);

                    // Send a window update for the local stream. Since global limits would still apply, we don't expect to get any actual data yet
                    await Http2WindowUpdateFrame.CreateOutgoing(1, 1000000).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // 325-400ishms: Server starts recieving data. It will hit the 65535 limit and stall.
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(75), testCancel).ConfigureAwait(false);
                    int bytesReadFromClient = 0;
                    while (bytesReadFromClient < 65535)
                    {
                        using (Http2DataFrame incomingDataFrame = await Http2TestCommon.ExpectFrameAndReturn<Http2DataFrame>(
                            serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime, (frame) => frame.StreamId == 1))
                        {
                            bytesReadFromClient += incomingDataFrame.PayloadLength;
                            logger.Log("Now have " + bytesReadFromClient + " bytes");
                        }

                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    }

                    // 700ms: Assert that there's nothing on the wire besides the ACK of our server settings
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime, (frame) => frame.Ack);
                    await Http2TestCommon.AssertSocketIsEmpty(serverSocket);

                    // Send a window update on the global stream.
                    await Http2WindowUpdateFrame.CreateOutgoing(0, 1000000).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // 1200ms: Should have more data now
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(500), testCancel).ConfigureAwait(false);
                    while (bytesReadFromClient < 100000)
                    {
                        using (Http2DataFrame incomingDataFrame = await Http2TestCommon.ExpectFrameAndReturn<Http2DataFrame>(
                            serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime, (frame) => frame.StreamId == 1))
                        {
                            bytesReadFromClient += incomingDataFrame.PayloadLength;
                            logger.Log("Now have " + bytesReadFromClient + " bytes");
                        }
                    }

                    HttpHeaders responseHeaders = new HttpHeaders();
                    responseHeaders.Add("Content-Type", "application/json");
                    responseHeaders.Add("Content-Length", "0");
                    foreach (var headerFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(responseHeaders, 200, 1, serverSettings, hPackEncoder, endOfStream: true))
                    {
                        logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 1);
                        await headerFrame.WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                        headerFrame.Dispose();
                    }
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientWaitsForLocalWindowUpdateBeforeSendingData()
        {
            byte[] expectedData = new byte[100000];
            new FastRandom().NextBytes(expectedData);

            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 100ms: Client starts a large POST request
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/", "POST"))
                    {
                        requestToSend.SetContent(expectedData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                        using (HttpResponse response = await session.MakeHttpRequest(
                            requestToSend,
                            logger.Clone("HttpRequest"),
                            testCancel,
                            clientTime).ConfigureAwait(false))
                        {
                            Assert.IsNotNull(response);
                            Assert.AreEqual(200, response.ResponseCode);
                            await response.FinishAsync(testCancel, clientTime).ConfigureAwait(false);
                        }
                    }
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings serverSettings = Http2Settings.ServerDefault();
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);

                    // 325ms: Server reads incoming settings
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => !frame.Ack && frame.StreamId == 0);
                    await Http2SettingsFrame.CreateOutgoingAckFrame().WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // Server reads incoming request header
                    await Http2TestCommon.ExpectFrame<Http2HeadersFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndHeaders && frame.StreamId == 1);

                    // Send a window update for the global stream. Since local limits would still apply, we don't expect to get any actual data yet
                    await Http2WindowUpdateFrame.CreateOutgoing(0, 1000000).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // 325-400ishms: Server starts recieving data. It will hit the 65535 limit and stall.
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(75), testCancel).ConfigureAwait(false);
                    int bytesReadFromClient = 0;
                    while (bytesReadFromClient < 65535)
                    {
                        using (Http2DataFrame incomingDataFrame = await Http2TestCommon.ExpectFrameAndReturn<Http2DataFrame>(
                            serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime, (frame) => frame.StreamId == 1))
                        {
                            bytesReadFromClient += incomingDataFrame.PayloadLength;
                            logger.Log("Now have " + bytesReadFromClient + " bytes");
                        }

                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    }

                    // 700ms: Assert that there's nothing on the wire besides the ACK of our server settings
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime, (frame) => frame.Ack);
                    await Http2TestCommon.AssertSocketIsEmpty(serverSocket);

                    // Send a window update on the local stream.
                    await Http2WindowUpdateFrame.CreateOutgoing(1, 1000000).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // 1200ms: Should have more data now
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(500), testCancel).ConfigureAwait(false);
                    while (bytesReadFromClient < 100000)
                    {
                        using (Http2DataFrame incomingDataFrame = await Http2TestCommon.ExpectFrameAndReturn<Http2DataFrame>(
                            serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime, (frame) => frame.StreamId == 1))
                        {
                            bytesReadFromClient += incomingDataFrame.PayloadLength;
                            logger.Log("Now have " + bytesReadFromClient + " bytes");
                        }
                    }

                    HttpHeaders responseHeaders = new HttpHeaders();
                    responseHeaders.Add("Content-Type", "application/json");
                    responseHeaders.Add("Content-Length", "0");
                    foreach (var headerFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(responseHeaders, 200, 1, serverSettings, hPackEncoder, endOfStream: true))
                    {
                        logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 1);
                        await headerFrame.WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                        headerFrame.Dispose();
                    }
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientCanUpdateLocalWindowViaSettingsUpdate()
        {
            byte[] expectedData = new byte[100000];
            new FastRandom().NextBytes(expectedData);

            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 100ms: Client starts a large POST request
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/", "POST"))
                    {
                        requestToSend.SetContent(expectedData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                        using (HttpResponse response = await session.MakeHttpRequest(
                            requestToSend,
                            logger.Clone("HttpRequest"),
                            testCancel,
                            clientTime).ConfigureAwait(false))
                        {
                            Assert.IsNotNull(response);
                            Assert.AreEqual(200, response.ResponseCode);
                            await response.FinishAsync(testCancel, clientTime).ConfigureAwait(false);
                        }
                    }
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings serverSettings = Http2Settings.ServerDefault();
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);

                    // 325ms: Server reads incoming settings
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => !frame.Ack && frame.StreamId == 0);
                    await Http2SettingsFrame.CreateOutgoingAckFrame().WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // Server reads incoming request header
                    await Http2TestCommon.ExpectFrame<Http2HeadersFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndHeaders && frame.StreamId == 1);

                    // Send a window update for the global stream. Since local limits would still apply, we don't expect to get any actual data yet
                    await Http2WindowUpdateFrame.CreateOutgoing(0, 5000000).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // 325-400ishms: Server starts recieving data. It will hit the 65535 limit and stall.
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(75), testCancel).ConfigureAwait(false);
                    int bytesReadFromClient = 0;
                    while (bytesReadFromClient < 65535)
                    {
                        using (Http2DataFrame incomingDataFrame = await Http2TestCommon.ExpectFrameAndReturn<Http2DataFrame>(
                            serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime, (frame) => frame.StreamId == 1))
                        {
                            bytesReadFromClient += incomingDataFrame.PayloadLength;
                            logger.Log("Now have " + bytesReadFromClient + " bytes");
                        }

                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    }

                    // 700ms: Assert that there's nothing on the wire besides the ACK of our server settings
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime, (frame) => frame.Ack);
                    await Http2TestCommon.AssertSocketIsEmpty(serverSocket);

                    // Send a settings update which increases our initial window size
                    // We have to add 1 byte to make 100001 because of a minor wrinkle in the code where it won't detect that the payload has ended until it has reserved
                    // at least 1 byte of flow control credit.
                    serverSettings.InitialWindowSize = 100001;
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // 1200ms: Should have more data now
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(500), testCancel).ConfigureAwait(false);
                    while (bytesReadFromClient < 100000)
                    {
                        using (Http2DataFrame incomingDataFrame = await Http2TestCommon.ExpectFrameAndReturn<Http2DataFrame>(
                            serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime, (frame) => frame.StreamId == 1))
                        {
                            bytesReadFromClient += incomingDataFrame.PayloadLength;
                            logger.Log("Now have " + bytesReadFromClient + " bytes");
                        }
                    }

                    HttpHeaders responseHeaders = new HttpHeaders();
                    foreach (var headerFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(responseHeaders, 200, 1, serverSettings, hPackEncoder, endOfStream: true))
                    {
                        using (headerFrame)
                        {
                            logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                                "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 1);
                            await headerFrame.WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                        }
                    }
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientCancelRequest()
        {
            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/"))
                    {
                        requestToSend.RequestHeaders.Add("User-Agent", "Local unit test case");
                        using (HttpResponse response = await session.MakeHttpRequest(
                            requestToSend,
                            logger.Clone("HttpRequest"),
                            testCancel,
                            clientTime).ConfigureAwait(false))
                        {
                            Assert.IsNotNull(response);
                            Assert.AreEqual(200, response.ResponseCode);

                            // This call to Finish should generate a RstStream frame to the server
                            // because we haven't finished reading the stream
                            await response.FinishAsync(testCancel, clientTime).ConfigureAwait(false);
                        }
                    }

                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings serverSettings = Http2Settings.ServerDefault();
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    HttpHeaders responseHeaders = new HttpHeaders();
                    byte[] data = new byte[1000];
                    new FastRandom().NextBytes(data);
                    responseHeaders.Add("Content-Type", "application/json");
                    responseHeaders.Add("Content-Length", data.Length.ToString(CultureInfo.InvariantCulture));
                    foreach (var headerFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(responseHeaders, 200, 1, serverSettings, hPackEncoder, endOfStream: false))
                    {
                        logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 1);
                        await headerFrame.WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                        headerFrame.Dispose();
                    }

                    PooledBuffer<byte> serverSideResponseData = BufferPool<byte>.Rent(data.Length);
                    ArrayExtensions.MemCopy(data, 0, serverSideResponseData.Buffer, 0, data.Length);
                    await Http2DataFrame.CreateOutgoing(serverSideResponseData, streamId: 1, endStream: true).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(150), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2RstStreamFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Error == Http2ErrorCode.Cancel);
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientServerBeginsWithNonSettingsFrame()
        {
            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromSeconds(1), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    // Send a ping from the server and then send settings
                    await Http2PingFrame.CreateOutgoing(BufferPool<byte>.Rent(8), ack: false).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(Http2Settings.ServerDefault()).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2GoAwayFrame>(serverSocket, logger, Http2Settings.ServerDefault(), frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Error == Http2ErrorCode.ProtocolError);
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientServerSendsInvalidSettings()
        {
            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromSeconds(1), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    // Max frame size is invalid here
                    Http2Settings badSettings = Http2Settings.ServerDefault();
                    badSettings.MaxFrameSize = 100;
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(badSettings)
                        .WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2GoAwayFrame>(serverSocket, logger, Http2Settings.ServerDefault(), frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Error == Http2ErrorCode.ProtocolError);
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientServerRequestsEnablePush()
        {
            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromSeconds(1), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    // Server shouldn't be allowed to enable push
                    Http2Settings badSettings = Http2Settings.ServerDefault();
                    badSettings.EnablePush = true;
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(badSettings, serializeAllSettings: true)
                        .WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2GoAwayFrame>(serverSocket, logger, Http2Settings.ServerDefault(), frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Error == Http2ErrorCode.ProtocolError);
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientServerSendsInvalidAckSettings()
        {
            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromSeconds(1), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // We have to manually construct an invalid settings frame here since the code is too good to let us
                    // This is a valid settings frame with ACK set
                    using (PooledBuffer<byte> frameHeaderBuilder = BufferPool<byte>.Rent(9))
                    {
                        PooledBuffer<byte> serializedSettings = Http2Helpers.SerializeSettings(Http2Settings.ServerDefault(), serializeAllSettings: true);
                        BinaryHelpers.UInt24ToByteArrayBigEndian((uint)serializedSettings.Length, frameHeaderBuilder.Buffer, 0);
                        frameHeaderBuilder.Buffer[3] = (byte)Http2FrameType.Settings;
                        frameHeaderBuilder.Buffer[4] = 0x1;
                        BinaryHelpers.Int32ToByteArrayBigEndian(0, frameHeaderBuilder.Buffer, 5); // stream ID
                        await serverSocket.WriteAsync(frameHeaderBuilder.Buffer, 0, 9, testCancel, serverTime).ConfigureAwait(false);
                        await serverSocket.WriteAsync(serializedSettings.Buffer, 0, serializedSettings.Length, testCancel, serverTime).ConfigureAwait(false);
                    }

                    await Http2TestCommon.ExpectFrame<Http2GoAwayFrame>(serverSocket, logger, Http2Settings.ServerDefault(), frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Error == Http2ErrorCode.FrameSizeError);
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientServerSendsSettingsFrameOnWrongStream()
        {
            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromSeconds(1), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // We have to manually construct an invalid settings frame here since the code is too good to let us
                    // This is a valid settings frame with stream ID set to non-zero
                    using (PooledBuffer<byte> frameHeaderBuilder = BufferPool<byte>.Rent(9))
                    {
                        PooledBuffer<byte> serializedSettings = Http2Helpers.SerializeSettings(Http2Settings.ServerDefault(), serializeAllSettings: true);
                        BinaryHelpers.UInt24ToByteArrayBigEndian((uint)serializedSettings.Length, frameHeaderBuilder.Buffer, 0);
                        frameHeaderBuilder.Buffer[3] = (byte)Http2FrameType.Settings;
                        frameHeaderBuilder.Buffer[4] = 0x0;
                        BinaryHelpers.Int32ToByteArrayBigEndian(1, frameHeaderBuilder.Buffer, 5); // stream ID 1
                        await serverSocket.WriteAsync(frameHeaderBuilder.Buffer, 0, 9, testCancel, serverTime).ConfigureAwait(false);
                        await serverSocket.WriteAsync(serializedSettings.Buffer, 0, serializedSettings.Length, testCancel, serverTime).ConfigureAwait(false);
                    }

                    await Http2TestCommon.ExpectFrame<Http2GoAwayFrame>(serverSocket, logger, Http2Settings.ServerDefault(), frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Error == Http2ErrorCode.ProtocolError);
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientServerPartialSettingsFrame()
        {
            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromSeconds(1), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // In this test case we only serialize 3 of the settings fields that are different from the default value.
                    using (PooledBuffer<byte> frameHeaderBuilder = BufferPool<byte>.Rent(9))
                    {
                        Http2Settings settings = Http2Settings.Default();
                        settings.EnablePush = false;
                        settings.InitialWindowSize = 100067;
                        settings.MaxConcurrentStreams = 589;
                        PooledBuffer<byte> serializedSettings = Http2Helpers.SerializeSettings(settings, serializeAllSettings: false);
                        Assert.AreEqual(18, serializedSettings.Length);
                        
                        // Manually construct the settings frame here and send it
                        BinaryHelpers.UInt24ToByteArrayBigEndian((uint)serializedSettings.Length, frameHeaderBuilder.Buffer, 0);
                        frameHeaderBuilder.Buffer[3] = (byte)Http2FrameType.Settings;
                        frameHeaderBuilder.Buffer[4] = 0x0;
                        BinaryHelpers.Int32ToByteArrayBigEndian(0, frameHeaderBuilder.Buffer, 5);
                        await serverSocket.WriteAsync(frameHeaderBuilder.Buffer, 0, 9, testCancel, serverTime).ConfigureAwait(false);
                        await serverSocket.WriteAsync(serializedSettings.Buffer, 0, serializedSettings.Length, testCancel, serverTime).ConfigureAwait(false);
                    }

                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, Http2Settings.ServerDefault(), frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Ack);
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientServerUnknownFutureSettings()
        {
            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromSeconds(1), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    using (PooledBuffer<byte> frameHeaderBuilder = BufferPool<byte>.Rent(9))
                    {
                        // Send setting ID 0x11 (unknown, unreserved key) with value 15.
                        // The only current extension I know of is SETTINGS_ENABLE_CONNECT_PROTOCOL 0x8 which is part of Websockets (RFC 8441)
                        PooledBuffer<byte> serializedSettings = BufferPool<byte>.Rent(6);
                        BinaryHelpers.UInt16ToByteArrayBigEndian((ushort)0x11, serializedSettings.Buffer, 0);
                        BinaryHelpers.UInt32ToByteArrayBigEndian((uint)15, serializedSettings.Buffer, 2);

                        // Manually construct the settings frame here and send it
                        BinaryHelpers.UInt24ToByteArrayBigEndian((uint)serializedSettings.Length, frameHeaderBuilder.Buffer, 0);
                        frameHeaderBuilder.Buffer[3] = (byte)Http2FrameType.Settings;
                        frameHeaderBuilder.Buffer[4] = 0x0;
                        BinaryHelpers.Int32ToByteArrayBigEndian(0, frameHeaderBuilder.Buffer, 5);
                        await serverSocket.WriteAsync(frameHeaderBuilder.Buffer, 0, 9, testCancel, serverTime).ConfigureAwait(false);
                        await serverSocket.WriteAsync(serializedSettings.Buffer, 0, serializedSettings.Length, testCancel, serverTime).ConfigureAwait(false);
                    }

                    // Client should ack even though it has no idea what setting we're sending.
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, Http2Settings.ServerDefault(), frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Ack);
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientServerSettingsFrameWithInvalidLength()
        {
            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromSeconds(1), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // In this test case we truncate the length of the payload by 2 bytes
                    using (PooledBuffer<byte> frameHeaderBuilder = BufferPool<byte>.Rent(9))
                    {
                        PooledBuffer<byte> serializedSettings = Http2Helpers.SerializeSettings(Http2Settings.ServerDefault(), serializeAllSettings: true);
                        BinaryHelpers.UInt24ToByteArrayBigEndian((uint)serializedSettings.Length - 2, frameHeaderBuilder.Buffer, 0);
                        frameHeaderBuilder.Buffer[3] = (byte)Http2FrameType.Settings;
                        frameHeaderBuilder.Buffer[4] = 0x0;
                        BinaryHelpers.Int32ToByteArrayBigEndian(0, frameHeaderBuilder.Buffer, 5);
                        await serverSocket.WriteAsync(frameHeaderBuilder.Buffer, 0, 9, testCancel, serverTime).ConfigureAwait(false);
                        await serverSocket.WriteAsync(serializedSettings.Buffer, 0, serializedSettings.Length - 2, testCancel, serverTime).ConfigureAwait(false);
                    }

                    await Http2TestCommon.ExpectFrame<Http2GoAwayFrame>(serverSocket, logger, Http2Settings.ServerDefault(), frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Error == Http2ErrorCode.FrameSizeError);
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientServerSettingsTimeout()
        {
            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromSeconds(30), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    // Server does nothing for 30 seconds.
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromSeconds(10), testCancel).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromSeconds(10), testCancel).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromSeconds(10), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2GoAwayFrame>(serverSocket, logger, Http2Settings.ServerDefault(), frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Error == Http2ErrorCode.SettingsTimeout);
                },
                totalTestTime: TimeSpan.FromSeconds(40),
                testStepIncrement: TimeSpan.FromSeconds(5),
                sessionPrefs: new Http2SessionPreferences()
                {
                    SettingsTimeout = TimeSpan.FromSeconds(20)
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientSendsPings()
        {
            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(600), testCancel).ConfigureAwait(false);
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    // Establish the session
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings serverSettings = Http2Settings.ServerDefault();
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => !frame.Ack && frame.Settings != null && frame.StreamId == 0);
                    await Http2SettingsFrame.CreateOutgoingAckFrame().WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Ack && frame.StreamId == 0);

                    // Wait for client to send ping
                    for (int c = 0; c < 3; c++)
                    {
                        await serverTime.WaitAsync(TimeSpan.FromSeconds(30), testCancel).ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                        await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                        await Http2TestCommon.ExpectFrame<Http2PingFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                            (frame) => !frame.Ack && frame.StreamId == 0);
                    }
                },
                totalTestTime: TimeSpan.FromSeconds(40),
                testStepIncrement: TimeSpan.FromSeconds(5),
                sessionPrefs: new Http2SessionPreferences()
                {
                    OutgoingPingInterval = TimeSpan.FromSeconds(30),
                });
        }

        [TestMethod]
        public async Task TestHttp2ClientSendsWindowUpdateImmediatelyAfterRequest()
        {
            byte[] expectedData = new byte[1000];
            new FastRandom().NextBytes(expectedData);

            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    // 0ms: Initiate session
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 100ms: Send request using default settings
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/"))
                    {
                        requestToSend.RequestHeaders.Add("User-Agent", "Local unit test case");
                        using (HttpResponse response = await session.MakeHttpRequest(
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
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings serverSettings = Http2Settings.ServerDefault();
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(300), testCancel).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);

                    // 325ms: Server reads connection prefix and client settings
                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => !frame.Ack && frame.Settings != null && frame.StreamId == 0);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);

                    // 350ms: Server sends settings
                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // Send settings ACK to client
                    await Http2SettingsFrame.CreateOutgoingAckFrame().WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // 450ms: Read the headers for an incoming request
                    await Http2TestCommon.ExpectFrame<Http2HeadersFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndHeaders && frame.EndStream && frame.StreamId == 1);
                    // Expect an immediate update to flow windows
                    await Http2TestCommon.ExpectFrame<Http2WindowUpdateFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.WindowSizeIncrement > 20000000 && frame.StreamId == 1);
                    await Http2TestCommon.ExpectFrame<Http2WindowUpdateFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.WindowSizeIncrement > 20000000 && frame.StreamId == 0);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);

                    // 500ms: Server sends response headers
                    HttpHeaders responseHeaders = new HttpHeaders();
                    responseHeaders.Add("Content-Type", "application/json");
                    responseHeaders.Add("Content-Length", expectedData.Length.ToString(CultureInfo.InvariantCulture));
                    foreach (var headerFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(responseHeaders, 200, 1, serverSettings, hPackEncoder, endOfStream: false))
                    {
                        logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Sending outgoing HTTP/2 frame {0} on stream ID {1}", headerFrame.FrameType, 1);
                        await headerFrame.WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                        headerFrame.Dispose();
                    }

                    // 500ms: Server sends data
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(25), testCancel).ConfigureAwait(false);
                    PooledBuffer<byte> serverSideResponseData = BufferPool<byte>.Rent(expectedData.Length);
                    ArrayExtensions.MemCopy(expectedData, 0, serverSideResponseData.Buffer, 0, expectedData.Length);
                    await Http2DataFrame.CreateOutgoing(serverSideResponseData, streamId: 1, endStream: true).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                },
                sessionPrefs: new Http2SessionPreferences()
                {
                    DesiredGlobalConnectionFlowWindow = 24000000,
                },
                totalTestTime: TimeSpan.FromSeconds(1),
                testStepIncrement: TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public async Task TestHttp2ClientHandlesGlobalFlowControlOverflow()
        {
            byte[] someData = new byte[10];
            new FastRandom().NextBytes(someData);

            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    await session.BeginClientSession(testCancel, clientTime, Http2Settings.Default(), "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/", "POST"))
                    {
                        requestToSend.RequestHeaders.Add("User-Agent", "Local unit test case");
                        requestToSend.SetContent(someData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                        using (HttpResponse response = await session.MakeHttpRequest(
                            requestToSend,
                            logger.Clone("HttpRequest"),
                            testCancel,
                            clientTime).ConfigureAwait(false))
                        {
                            Assert.IsNull(response);
                        }
                    }
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    Http2Settings serverSettings = Http2Settings.ServerDefault();
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);

                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => !frame.Ack && frame.Settings != null && frame.StreamId == 0);

                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2SettingsFrame.CreateOutgoingAckFrame().WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // Read request
                    await Http2TestCommon.ExpectFrame<Http2HeadersFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndHeaders && !frame.EndStream && frame.StreamId == 1);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2DataFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.PayloadLength == someData.Length && !frame.EndStream && frame.StreamId == 1);
                    await Http2TestCommon.ExpectFrame<Http2DataFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndStream && frame.StreamId == 1);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Ack);

                    // Server sends some huge window updates on stream 0
                    // The first one should be just below the limit
                    await Http2WindowUpdateFrame.CreateOutgoing(streamId: 0,
                        windowSizeIncrement: Http2Constants.MAX_INITIAL_WINDOW_SIZE - Http2Constants.DEFAULT_INITIAL_WINDOW_SIZE + someData.Length).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.AssertSocketIsEmpty(serverSocket);

                    // The second update of just 1 single byte should trigger an error
                    await Http2WindowUpdateFrame.CreateOutgoing(streamId: 0,
                        windowSizeIncrement: 1).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // Assert that the client told us to go away
                    await Http2TestCommon.ExpectFrame<Http2GoAwayFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Error == Http2ErrorCode.FlowControlError);
                },
                socketLatency: TimeSpan.FromMilliseconds(10),
                totalTestTime: TimeSpan.FromSeconds(1),
                testStepIncrement: TimeSpan.FromMilliseconds(20));
        }

        [TestMethod]
        public async Task TestHttp2ClientHandlesLocalFlowControlOverflow()
        {
            byte[] someData = new byte[10];
            new FastRandom().NextBytes(someData);
            Http2Settings clientSettings = Http2Settings.Default();
            clientSettings.InitialWindowSize = Http2Constants.DEFAULT_INITIAL_WINDOW_SIZE;
            Http2Settings serverSettings = Http2Settings.ServerDefault();
            serverSettings.InitialWindowSize = 1_000_000_000;

            await RunHttp2ClientTest(
                async (Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel) =>
                {
                    await session.BeginClientSession(testCancel, clientTime, clientSettings, "localhost", "https").ConfigureAwait(false);
                    await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    using (HttpRequest requestToSend = HttpRequest.CreateOutgoing("/", "POST"))
                    {
                        requestToSend.RequestHeaders.Add("User-Agent", "Local unit test case");
                        requestToSend.SetContent(someData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                        using (HttpResponse response = await session.MakeHttpRequest(
                            requestToSend,
                            logger.Clone("HttpRequest"),
                            testCancel,
                            clientTime).ConfigureAwait(false))
                        {
                            Assert.IsNull(response);
                        }
                    }
                },
                async (ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel) =>
                {
                    Queue<Http2Frame> frameQueue = new Queue<Http2Frame>();
                    HPackEncoder hPackEncoder = new HPackEncoder();
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);

                    await Http2TestCommon.ReadClientConnectionPrefix(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => !frame.Ack && frame.Settings != null && frame.StreamId == 0);

                    await Http2SettingsFrame.CreateOutgoingSettingsFrame(serverSettings).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await Http2SettingsFrame.CreateOutgoingAckFrame().WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);

                    // Read request
                    await Http2TestCommon.ExpectFrame<Http2HeadersFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndHeaders && !frame.EndStream && frame.StreamId == 1);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(50), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2DataFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.PayloadLength == someData.Length && !frame.EndStream && frame.StreamId == 1);
                    await Http2TestCommon.ExpectFrame<Http2DataFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.EndStream && frame.StreamId == 1);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.ExpectFrame<Http2SettingsFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.Ack);

                    // Server sends some huge window updates on stream 1
                    // The first one should be just below the limit
                    await Http2WindowUpdateFrame.CreateOutgoing(streamId: 1,
                        windowSizeIncrement: Http2Constants.MAX_INITIAL_WINDOW_SIZE - serverSettings.InitialWindowSize + someData.Length).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);
                    await Http2TestCommon.AssertSocketIsEmpty(serverSocket);

                    // The second update of just 1 single byte should trigger an error
                    await Http2WindowUpdateFrame.CreateOutgoing(streamId: 1, windowSizeIncrement: 1).WriteToSocket(serverSocket, testCancel, serverTime).ConfigureAwait(false);
                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel).ConfigureAwait(false);

                    // Assert that the client reset the stream
                    await Http2TestCommon.ExpectFrame<Http2RstStreamFrame>(serverSocket, logger, serverSettings, frameQueue, true, testCancel, serverTime,
                        (frame) => frame.StreamId == 1 && frame.Error == Http2ErrorCode.FlowControlError);
                },
                socketLatency: TimeSpan.FromMilliseconds(10),
                totalTestTime: TimeSpan.FromSeconds(1),
                testStepIncrement: TimeSpan.FromMilliseconds(20));
        }

        private delegate Task Http2TestClientHandler(Http2Session session, ILogger logger, IRealTimeProvider clientTime, CancellationToken testCancel);
        private delegate Task Http2TestServerHandler(ISocket serverSocket, ILogger logger, IRealTimeProvider serverTime, CancellationToken testCancel);

        private static async Task RunHttp2ClientTest(
            Http2TestClientHandler clientThreadImpl,
            Http2TestServerHandler serverThreadImpl,
            TimeSpan? socketLatency = null,
            TimeSpan? totalTestTime = null,
            TimeSpan? testStepIncrement = null,
            Http2SessionPreferences sessionPrefs = null)
        {
            ILogger logger = new ConsoleLogger("Http2Test", LogLevel.All);
            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken testCancel = testCancelSource.Token;
                LockStepRealTimeProvider testGlobalRealTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair(socketLatency.GetValueOrDefault(TimeSpan.FromMilliseconds(200)));
                using (DirectSocket clientSocket = socketPair.ClientSocket)
                using (DirectSocket serverSocket = socketPair.ServerSocket)
                using (Http2Session session = new Http2Session(
                    clientSocket,
                    logger.Clone("ClientSession"),
                    sessionPrefs ?? new Http2SessionPreferences(),
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty))
                {
                    // this is just some extra crazy sanity check to make sure we're
                    // using pooled buffers all correctly in our internal sockets and such
                    bool shredderRunning = true;
                    Task shredder = Task.Run(() =>
                    {
                        Stopwatch timer = Stopwatch.StartNew();
                        while (Volatile.Read(ref shredderRunning) && !testCancel.IsCancellationRequested)
                        {
                            BufferPool<byte>.Shred();
                        }
                    });

                    IRealTimeProvider clientTime = testGlobalRealTime.Fork("ClientTime");
                    Task clientTask = Task.Run(async () =>
                    {
                        try
                        {
                            await clientThreadImpl(session, logger.Clone("Client"), clientTime, testCancel).ConfigureAwait(false);
                        }
                        finally
                        {
                            clientTime.Merge();
                        }
                    });

                    // Simulate the server process here, using raw frames so we can exactly control timing and format
                    // of data that the client has to process.
                    await serverThreadImpl(serverSocket, logger.Clone("Server"), testGlobalRealTime, testCancel).ConfigureAwait(false);

                    TimeSpan testTime = totalTestTime.GetValueOrDefault(TimeSpan.FromSeconds(5));
                    int testIncrement = (int)Math.Max(1, testStepIncrement.GetValueOrDefault(TimeSpan.FromMilliseconds(100)).TotalMilliseconds);
                    testGlobalRealTime.Step(testTime, testIncrement);
                    await clientTask.ConfigureAwait(false);
                    shredderRunning = false;
                    await shredder.ConfigureAwait(false);
                }
            }
        }
    }
}
