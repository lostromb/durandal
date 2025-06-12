using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.Time;
using Durandal.Common.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Net.WebSocket
{
    [TestClass]
    public class WebSocketTests
    {
        [TestMethod]
        public async Task TestBasicWebsocket()
        {
            ILogger logger = new ConsoleLogger();
            using (CancellationTokenSource testCancel = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger);
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
                IRealTimeProvider clientTime = lockStepTime.Fork("Client");
                IRealTimeProvider serverTime = lockStepTime.Fork("Server");
                IRandom rand = new FastRandom(76445);
                byte[] fakeData = new byte[1000000];
                rand.NextBytes(fakeData);

                List<int> lengths = new List<int>() { 1, 100, 1000, 10000, 100000, 1000000 };
                Task clientTask = Task.Run(async () =>
                {
                    try
                    {
                        using (IWebSocket clientEndpoint = new WebSocketClient(
                            new WeakPointer<ISocket>(socketPair.ClientSocket),
                            true,
                            logger.Clone("Client"),
                            clientTime,
                            false,
                            new FastRandom(76432)))
                        {
                            // Send byte paylods of various lengths
                            foreach (int writeLength in lengths)
                            {
                                await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel.Token).ConfigureAwait(false);
                                logger.Log("Client sending " + writeLength + " bytes");
                                bool sendOk = await clientEndpoint.SendAsync(
                                    new ArraySegment<byte>(fakeData, 0, writeLength),
                                    WebSocketMessageType.Binary,
                                    testCancel.Token,
                                    clientTime).ConfigureAwait(false);

                                logger.Log("Client sent " + writeLength + " bytes");
                                Assert.IsTrue(sendOk);

                                await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel.Token).ConfigureAwait(false);
                                // Assert we got it back
                                using (WebSocketBufferResult receiveResult = await clientEndpoint.ReceiveAsBufferAsync(testCancel.Token, clientTime))
                                {
                                    logger.Log("Client received " + receiveResult.Result.Length + " bytes");
                                    Assert.IsTrue(receiveResult.Success);
                                    Assert.AreEqual(writeLength, receiveResult.Result.Length);
                                    Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, 0, receiveResult.Result.Buffer, 0, writeLength));
                                }
                            }

                            logger.Log("Closing client");
                            await clientEndpoint.CloseWrite(testCancel.Token, clientTime).ConfigureAwait(false);
                            await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel.Token).ConfigureAwait(false);
                            logger.Log("Waiting for full client close");
                            await clientEndpoint.WaitForGracefulClose(testCancel.Token, clientTime).ConfigureAwait(false);
                            logger.Log("Client closed");
                        }
                    }
                    finally
                    {
                        clientTime.Merge();
                    }
                });

                Task serverTask = Task.Run(async () =>
                {
                    try
                    {
                        using (IWebSocket serverEndpoint = new WebSocketClient(
                            new WeakPointer<ISocket>(socketPair.ServerSocket),
                            true,
                            logger.Clone("Server"),
                            serverTime,
                            true,
                            new FastRandom(87658)))
                        {
                            // Receive some data
                            foreach (int writeLength in lengths)
                            {
                                await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel.Token).ConfigureAwait(false);
                                logger.Log("Server expecting " + writeLength + " bytes");
                                using (WebSocketBufferResult receiveResult = await serverEndpoint.ReceiveAsBufferAsync(testCancel.Token, serverTime))
                                {
                                    Assert.IsTrue(receiveResult.Success);
                                    Assert.AreEqual(writeLength, receiveResult.Result.Length);
                                    Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, 0, receiveResult.Result.Buffer, 0, receiveResult.Result.Length));
                                    logger.Log("Server received " + receiveResult.Result.Length + " bytes");

                                    await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel.Token).ConfigureAwait(false);
                                    // And echo it back
                                    logger.Log("Server sending " + receiveResult.Result.Length + " bytes");
                                    bool sendOk = await serverEndpoint.SendAsync(
                                        new ArraySegment<byte>(receiveResult.Result.Buffer, 0, receiveResult.Result.Length),
                                        WebSocketMessageType.Binary,
                                        testCancel.Token,
                                        serverTime).ConfigureAwait(false);
                                    logger.Log("Server sent " + receiveResult.Result.Length + " bytes");
                                    Assert.IsTrue(sendOk);
                                }
                            }

                            logger.Log("Closing server");
                            await serverEndpoint.CloseWrite(testCancel.Token, serverTime).ConfigureAwait(false);
                            await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel.Token).ConfigureAwait(false);
                            logger.Log("Waiting for full server close");
                            await serverEndpoint.WaitForGracefulClose(testCancel.Token, serverTime).ConfigureAwait(false);
                            logger.Log("Server closed");
                        }
                    }
                    finally
                    {
                        serverTime.Merge();
                    }
                });

                lockStepTime.Step(TimeSpan.FromSeconds(10), 20);
                await clientTask;
                await serverTask;
            }
        }

        [TestMethod]
        public async Task TestWebSocketPingLoop()
        {
            ILogger logger = new ConsoleLogger();
            using (CancellationTokenSource testCancel = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger);
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
                IRealTimeProvider clientTime = lockStepTime.Fork("Client");
                IRealTimeProvider serverTime = lockStepTime.Fork("Server");
                IRandom rand = new FastRandom(76445);
                byte[] fakeData = new byte[100];
                rand.NextBytes(fakeData);

                Task clientTask = Task.Run(async () =>
                {
                    try
                    {
                        using (IWebSocket clientEndpoint = new WebSocketClient(
                            new WeakPointer<ISocket>(socketPair.ClientSocket),
                            true,
                            logger.Clone("Client"),
                            clientTime,
                            false,
                            new FastRandom(76432)))
                        {
                            // Wait 65 seconds, that should be enough for 2 pings back and forth
                            await clientTime.WaitAsync(TimeSpan.FromSeconds(65), testCancel.Token).ConfigureAwait(false);

                            // Now exchange messages
                            bool sendOk = await clientEndpoint.SendAsync(
                                new ArraySegment<byte>(fakeData, 0, 100),
                                WebSocketMessageType.Binary,
                                testCancel.Token,
                                clientTime).ConfigureAwait(false);

                            Assert.IsTrue(sendOk);

                            await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel.Token).ConfigureAwait(false);

                            using (WebSocketBufferResult receiveResult = await clientEndpoint.ReceiveAsBufferAsync(testCancel.Token, clientTime))
                            {
                                Assert.IsTrue(receiveResult.Success);
                                Assert.AreEqual(100, receiveResult.Result.Length);
                                Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, 0, receiveResult.Result.Buffer, 0, 100));
                            }

                            await clientEndpoint.CloseWrite(testCancel.Token, clientTime).ConfigureAwait(false);
                            await clientTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel.Token).ConfigureAwait(false);
                            await clientEndpoint.WaitForGracefulClose(testCancel.Token, clientTime).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        clientTime.Merge();
                    }
                });

                Task serverTask = Task.Run(async () =>
                {
                    try
                    {
                        using (IWebSocket serverEndpoint = new WebSocketClient(
                            new WeakPointer<ISocket>(socketPair.ServerSocket),
                            true,
                            logger.Clone("Server"),
                            serverTime,
                            true,
                            new FastRandom(87658)))
                        {
                            // Wait 65 seconds, that should be enough for 2 pings back and forth
                            await serverTime.WaitAsync(TimeSpan.FromSeconds(65), testCancel.Token).ConfigureAwait(false);

                            // Receive some data
                            await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel.Token).ConfigureAwait(false);
                            using (WebSocketBufferResult receiveResult = await serverEndpoint.ReceiveAsBufferAsync(testCancel.Token, serverTime))
                            {
                                Assert.IsTrue(receiveResult.Success);
                                Assert.AreEqual(100, receiveResult.Result.Length);
                                Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, 0, receiveResult.Result.Buffer, 0, receiveResult.Result.Length));

                                await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel.Token).ConfigureAwait(false);

                                // And echo it back
                                bool sendOk = await serverEndpoint.SendAsync(
                                    new ArraySegment<byte>(receiveResult.Result.Buffer, 0, receiveResult.Result.Length),
                                    WebSocketMessageType.Binary,
                                    testCancel.Token,
                                    serverTime).ConfigureAwait(false);
                                Assert.IsTrue(sendOk);
                            }

                            await serverEndpoint.CloseWrite(testCancel.Token, serverTime).ConfigureAwait(false);
                            await serverTime.WaitAsync(TimeSpan.FromMilliseconds(100), testCancel.Token).ConfigureAwait(false);
                            await serverEndpoint.WaitForGracefulClose(testCancel.Token, serverTime).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        serverTime.Merge();
                    }
                });

                lockStepTime.Step(TimeSpan.FromSeconds(60), 10000);
                lockStepTime.Step(TimeSpan.FromSeconds(10), 100);
                await clientTask;
                await serverTask;
            }
        }
    }
}
