

namespace Durandal.Tests.Common.Net.Http
{
    using Durandal.Common.Collections;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Net.Http2;
    using Durandal.Common.Net.WebSocket;
    using Durandal.Common.Remoting;
    using Durandal.Common.Remoting.Handlers;
    using Durandal.Common.Remoting.Protocol;
    using Durandal.Common.Remoting.Proxies;
    using Durandal.Common.Security;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Tasks;
    using Durandal.Common.Test;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.Extensions.BondProtocol;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class HttpListenerServerTests
    {
        private const int LISTENER_PORT = 62291;
        private static UnitTestHttpServer _listenerServer;
        private static Http2SessionManager _h2SessionManager;
        private static ISocketFactory _socketFactory;
        private static IRealTimeProvider _realTime = DefaultRealTimeProvider.Singleton;
        private static ILogger _logger = new DebugLogger("HttpTests", LogLevel.All);// new ConsoleLogger("HttpTests", LogLevel.All);

        public enum HttpClientImplementation
        {
            Socket,
            Portable
        }

        public enum HttpServerImplementation
        {
            Listener
        }

        [ClassInitialize]
        public static void StartServer(TestContext context)
        {
            try
            {
                ILogger logger = _logger.Clone("ListenerServer");
                IThreadPool listenerThreadPool = new TaskThreadPool(NullMetricCollector.WeakSingleton, DimensionSet.Empty, "ListenerThreadPool");
                _listenerServer = new UnitTestHttpServer(
                    new ListenerHttpServer(
                        new ServerBindingInfo[] { new ServerBindingInfo(ServerBindingInfo.WILDCARD_HOSTNAME, LISTENER_PORT) },
                        logger,
                        new WeakPointer<IThreadPool>(listenerThreadPool)),
                    logger);
                if (!_listenerServer.StartServer("TestListenerServer", CancellationToken.None, _realTime).Await())
                {
                    _listenerServer.StopServer(CancellationToken.None, _realTime).Await();
                    _listenerServer = null;
                }
            }
            catch (Exception e)
            {
                _logger.Log("Error occurred while starting the listener server", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
                _listenerServer = null;
            }

            _socketFactory = new PooledTcpClientSocketFactory(_logger.Clone("SocketFactory"), NullMetricCollector.Singleton, DimensionSet.Empty);
            _h2SessionManager = new Http2SessionManager();
        }

        [ClassCleanup]
        public static void StopServer()
        {
            if (_listenerServer != null)
            {
                _listenerServer.StopServer(CancellationToken.None, _realTime).AwaitWithTimeout(3000);
                _listenerServer.Dispose();
            }

            _socketFactory?.Dispose();

            //Thread.Sleep(3000);
        }

        [TestInitialize]
        public void ResetServer()
        {
            if (_listenerServer != null)
            {
                _listenerServer.Reset();
            }

            _h2SessionManager.ShutdownAllActiveSessions().Await();
        }

        [TestMethod]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, true, DisplayName = "Socket To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, false, DisplayName = "Socket To Listener HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, true, DisplayName = "Portable To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, false, DisplayName = "Portable To Listener HTTP/1.1")]
        public async Task TestHttpClientServerBasicSmallRequest(HttpClientImplementation clientType, HttpServerImplementation serverType, bool useHttp10)
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(serverType);
            using (IHttpClient client = BuildTestHttpClient(clientType, serverType, useHttp10))
            {
                _logger.Log("Begin HTTP test case");
                try
                {
                    ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(200));
                    targetServer.AddHandler("/small", async (context, cancelToken, realTime) =>
                    {
                        HttpResponse response = HttpResponse.OKResponse();
                        response.SetContent(fakeData, "text/plain; charset=UTF-8");
                        await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    });

                    for (int c = 0; c < 10; c++)
                    {
                        using (HttpRequest request = HttpRequest.CreateOutgoing("/small", "GET"))
                        using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                        {
                            Assert.AreEqual(200, response.ResponseCode);
                            ArraySegment<byte> responseBody = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, responseBody));
                            Assert.AreEqual("text/plain; charset=UTF-8", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
                            await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    _logger.Log("End HTTP test case");
                }
            }
        }

        [TestMethod]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, 1, 0, DisplayName = "Socket To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, 1, 1, DisplayName = "Socket To Listener HTTP/1.1")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, 2, 0, DisplayName = "Socket To Listener HTTP/2.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, 1, 0, DisplayName = "Portable To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, 1, 1, DisplayName = "Portable To Listener HTTP/1.1")]
        public async Task TestHttpClientServerBasicLargeRequest(HttpClientImplementation clientType, HttpServerImplementation serverType, int httpMajor, int httpMinor)
        {
            HttpVersion clientVersion = HttpVersion.ParseHttpVersion(httpMajor, httpMinor);
            UnitTestHttpServer targetServer = GetTargetHttpServer(serverType);
            using (IHttpClient client = BuildTestHttpClient(clientType, serverType, clientVersion))
            {
                _logger.Log("Begin HTTP test case");
                try
                {
                    ArraySegment<byte> expectedRequestData = new ArraySegment<byte>(GetRandomPayload(280000));
                    ArraySegment<byte> expectedResponseData = new ArraySegment<byte>(GetRandomPayload(280000));
                    targetServer.AddHandler("/large", async (context, cancelToken, realTime) =>
                    {
                        ArraySegment<byte> actualRequestBody = await context.HttpRequest.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedRequestData, actualRequestBody));

                        HttpResponse response = HttpResponse.OKResponse();
                        response.SetContent(expectedResponseData, "application/json");
                        await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    });

                    for (int c = 0; c < 10; c++)
                    {
                        using (HttpRequest request = HttpRequest.CreateOutgoing("/large", "POST"))
                        {
                            request.SetContent(expectedRequestData, "application/json");
                            using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                            {
                                Assert.AreEqual(200, response.ResponseCode);
                                //Assert.AreEqual(clientVersion, response.ProtocolVersion);
                                ArraySegment<byte> actualResponseBody = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedResponseData, actualResponseBody));
                                Assert.AreEqual("application/json", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
                                Assert.IsTrue(response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_SERVER_WORK_TIME));
                                await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }
                    }
                }
                finally
                {
                    _logger.Log("End HTTP test case");
                }
            }
        }

        [TestMethod]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, true, DisplayName = "Socket To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, false, DisplayName = "Socket To Listener HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, true, DisplayName = "Portable To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, false, DisplayName = "Portable To Listener HTTP/1.1")]
        public async Task TestHttpClientServerBasicVaryingRequest(HttpClientImplementation clientType, HttpServerImplementation serverType, bool useHttp10)
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(serverType);
            using (IHttpClient client = BuildTestHttpClient(clientType, serverType, useHttp10))
            {
                _logger.Log("Begin HTTP test case");
                try
                {
                    targetServer.AddHandler("/dev/rand", async (context, cancelToken, realTime) =>
                    {
                        HttpRequest incomingRequest = context.HttpRequest;
                        // Produce a stream of random bytes with the given seed and length;
                        string getParam;
                        int seed;
                        int length;
                        if (!incomingRequest.GetParameters.TryGetValue("seed", out getParam) ||
                            !int.TryParse(getParam, out seed) ||
                            !incomingRequest.GetParameters.TryGetValue("length", out getParam) ||
                            !int.TryParse(getParam, out length))
                        {
                            await context.WritePrimaryResponse(HttpResponse.BadRequestResponse(), _logger, cancelToken, realTime).ConfigureAwait(false);
                            return;
                        }

                        using (PipeStream pipe = new PipeStream())
                        {
                            PipeStream.PipeWriteStream writeStream = pipe.GetWriteStream();

                            Task bgTask = Task.Run(() =>
                            {
                                using (writeStream)
                                {
                                    FastRandom rand = new FastRandom(seed);
                                    byte[] buf = new byte[length];
                                    rand.NextBytes(buf);

                                    int written = 0;
                                    int remaining = length;
                                    while (remaining > 0)
                                    {
                                        int chunkSize = Math.Min(remaining, 1024);
                                        writeStream.Write(buf, written, chunkSize);
                                        remaining -= chunkSize;
                                        written += chunkSize;
                                    }
                                }
                            });

                            HttpResponse response = HttpResponse.OKResponse();
                            response.SetContent(pipe.GetReadStream(), HttpConstants.MIME_TYPE_OCTET_STREAM);
                            await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                        }
                    });

                    IRandom randGenerator = new FastRandom();
                    for (int c = 0; c < 10; c++)
                    {
                        int seed = randGenerator.NextInt();
                        int length = randGenerator.NextInt(500, 100000);
                        using (HttpRequest request = HttpRequest.CreateOutgoing("/dev/rand", "GET"))
                        {
                            request.GetParameters["seed"] = seed.ToString();
                            request.GetParameters["length"] = length.ToString();
                            using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                            {
                                Assert.AreEqual(200, response.ResponseCode);
                                ArraySegment<byte> responseBody = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                                FastRandom expectedDataGenerator = new FastRandom(seed);
                                byte[] expectedData = new byte[length];
                                expectedDataGenerator.NextBytes(expectedData);
                                Assert.IsTrue(ArrayExtensions.ArrayEquals(new ArraySegment<byte>(expectedData), responseBody));
                                await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }
                    }
                }
                finally
                {
                    _logger.Log("End HTTP test case");
                }
            }
        }

        [TestMethod]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, true, DisplayName = "Socket To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, false, DisplayName = "Socket To Listener HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, true, DisplayName = "Portable To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, false, DisplayName = "Portable To Listener HTTP/1.1")]
        public async Task TestHttpClientServerPooledBufferResponse(HttpClientImplementation clientType, HttpServerImplementation serverType, bool useHttp10)
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(serverType);
            using (IHttpClient client = BuildTestHttpClient(clientType, serverType, useHttp10))
            {
                _logger.Log("Begin HTTP test case");
                try
                {
                    ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(200000));
                    targetServer.AddHandler("/small", async (context, cancelToken, realTime) =>
                    {
                        HttpResponse response = HttpResponse.OKResponse();
                        PooledBuffer<byte> pooledBuf = BufferPool<byte>.Rent(200000);
                        ArrayExtensions.MemCopy(fakeData.Array, fakeData.Offset, pooledBuf.Buffer, 0, fakeData.Count);
                        response.SetContent(pooledBuf, "text/plain; charset=UTF-8");
                        await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    });

                    for (int c = 0; c < 10; c++)
                    {
                        using (HttpRequest request = HttpRequest.CreateOutgoing("/small", "GET"))
                        using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                        {
                            Assert.AreEqual(200, response.ResponseCode);
                            ArraySegment<byte> responseBody = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, responseBody));
                            Assert.AreEqual("text/plain; charset=UTF-8", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
                            await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    _logger.Log("End HTTP test case");
                }
            }
        }

        [TestMethod]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, true, DisplayName = "Socket To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, false, DisplayName = "Socket To Listener HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, true, DisplayName = "Portable To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, false, DisplayName = "Portable To Listener HTTP/1.1")]
        public async Task TestHttpClientServerConvolutedGetParameters(HttpClientImplementation clientType, HttpServerImplementation serverType, bool useHttp10)
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(serverType);
            using (IHttpClient client = BuildTestHttpClient(clientType, serverType, useHttp10))
            {
                _logger.Log("Begin HTTP test case");
                try
                {
                    targetServer.AddHandler("/testgetparams", async (context, cancelToken, realTime) =>
                    {
                        Assert.IsTrue(context.HttpRequest.GetParameters.ContainsKey("?&=&/"));
                        Assert.AreEqual("?&=&/", context.HttpRequest.GetParameters["?&=&/"]);
                        Assert.IsTrue(context.HttpRequest.GetParameters.ContainsKey("%&?&/"));
                        Assert.AreEqual("%&?&/", context.HttpRequest.GetParameters["%&?&/"]);
                        await context.WritePrimaryResponse(HttpResponse.OKResponse(), _logger, cancelToken, realTime).ConfigureAwait(false);
                    });

                    for (int c = 0; c < 10; c++)
                    {
                        using (HttpRequest request = HttpRequest.CreateOutgoing("/testgetparams", "GET"))
                        {
                            request.GetParameters.Add("?&=&/", "?&=&/");
                            request.GetParameters.Add("%&?&/", "%&?&/");
                            Assert.IsTrue(request.GetParameters.ContainsKey("?&=&/"));
                            Assert.AreEqual("?&=&/", request.GetParameters["?&=&/"]);
                            Assert.IsTrue(request.GetParameters.ContainsKey("%&?&/"));
                            Assert.AreEqual("%&?&/", request.GetParameters["%&?&/"]);
                            using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                            {
                                Assert.AreEqual(200, response.ResponseCode);
                                await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }
                    }
                }
                finally
                {
                    _logger.Log("End HTTP test case");
                }
            }
        }

        [TestMethod]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, DisplayName = "Socket To Listener")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, DisplayName = "Portable To Listener")]
        public async Task TestHttpClientServerChunkedDataLockstep(HttpClientImplementation clientType, HttpServerImplementation serverType)
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(serverType);
            using (IHttpClient client = BuildTestHttpClient(clientType, serverType, false))
            using (AutoResetEvent rateLimiter = new AutoResetEvent(false))
            {
                _logger.Log("Begin HTTP test case");
                try
                {
                    ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(120000));
                    targetServer.AddHandler("/chunked", async (context, cancelToken, realTime) =>
                    {
                        using (PipeStream stream = new PipeStream())
                        {
                            HttpResponse response = HttpResponse.OKResponse();
                            response.SetContent(stream.GetReadStream(), "application/json");
                            PipeStream.PipeWriteStream writeStream = stream.GetWriteStream();

                            // Stream the chunked response asynchronously and with a rate limiter
                            Task backgroundTask = Task.Run(() =>
                            {
                                using (writeStream)
                                {
                                    int CHUNK_SIZE = 50;
                                    int cursor = 0;
                                    while (cursor < fakeData.Count)
                                    {
                                        int size = Math.Min(CHUNK_SIZE, fakeData.Count - cursor);
                                        byte[] buf = new byte[size];
                                        Array.Copy(fakeData.Array, fakeData.Offset + cursor, buf, 0, size);
                                        writeStream.Write(buf, 0, size);
                                        cursor += size;
                                        rateLimiter.WaitOne();
                                    }
                                }
                            });

                            await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                        }
                    });

                    for (int c = 0; c < 10; c++)
                    {
                        using (HttpRequest request = HttpRequest.CreateOutgoing("/chunked", "GET"))
                        using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                        {
                            Assert.AreEqual(200, response.ResponseCode);
                            Assert.AreEqual("application/json", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
                            Stream payloadStream = response.ReadContentAsStream();
                            Assert.IsNotNull(payloadStream);

                            rateLimiter.Set();
                            // Download the streaming response
                            using (MemoryStream bucket = new MemoryStream())
                            {
                                byte[] buf = new byte[4096];
                                int bytesRead = await payloadStream.ReadAsync(buf, 0, buf.Length);
                                while (bytesRead > 0)
                                {
                                    bucket.Write(buf, 0, bytesRead);
                                    rateLimiter.Set();
                                    bytesRead = await payloadStream.ReadAsync(buf, 0, buf.Length);
                                }

                                Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, new ArraySegment<byte>(bucket.ToArray())));
                            }

                            await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    _logger.Log("End HTTP test case");
                }
            }
        }

        [TestMethod]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, true, DisplayName = "Socket To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, false, DisplayName = "Socket To Listener HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, true, DisplayName = "Portable To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, false, DisplayName = "Portable To Listener HTTP/1.1")]
        public async Task TestHttpClientServerSendFixedContentLengthWhenKnown(HttpClientImplementation clientType, HttpServerImplementation serverType, bool useHttp10)
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(serverType);
            using (IHttpClient client = BuildTestHttpClient(clientType, serverType, useHttp10))
            {
                _logger.Log("Begin HTTP test case");
                try
                {
                    ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(200));
                    targetServer.AddHandler("/small", async (context, cancelToken, realTime) =>
                    {
                        HttpResponse response = HttpResponse.OKResponse();
                        response.SetContent(fakeData, "text/plain; charset=UTF-8");
                        await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    });

                    for (int c = 0; c < 10; c++)
                    {
                        using (HttpRequest request = HttpRequest.CreateOutgoing("/small", "GET"))
                        using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                        {
                            Assert.AreEqual(200, response.ResponseCode);
                            ArraySegment<byte> responseBody = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, responseBody));
                            Assert.AreEqual("text/plain; charset=UTF-8", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
                            Assert.IsTrue(response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_CONTENT_LENGTH));
                            Assert.IsFalse(response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING));
                            await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    _logger.Log("End HTTP test case");
                }
            }
        }

        [TestMethod]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, false, DisplayName = "System Websocket To Listener HTTP/1.1")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, false, DisplayName = "Durandal Websocket To Listener HTTP/1.1")]
        public async Task TestHttpClientServerWebSockets(HttpClientImplementation clientImplementation, HttpServerImplementation serverImplementation, bool useHttp2)
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(serverImplementation);
            IWebSocketClientFactory webSocketClient;
            if (clientImplementation == HttpClientImplementation.Socket)
            {
                ISocketFactory socketFactory = new TcpClientSocketFactory();
                webSocketClient = new WebSocketClientFactory(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new WeakPointer<IHttp2SessionManager>(_h2SessionManager),
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new CryptographicRandom());
            }
            else
            {
                webSocketClient = new SystemWebSocketClientFactory();
            }

            _logger.Log("Begin HTTP test case");
            try
            {
                ArraySegment<byte> fakeBinaryData = new ArraySegment<byte>(GetRandomPayload(10000));
                ArraySegment<byte> fakeTextData = new ArraySegment<byte>(Encoding.UTF8.GetBytes("This is a test websocket message"));
                targetServer.AddHandler("/websocket", async (context, cancelToken, realTime) =>
                {
                    Assert.AreEqual("/websocket", context.HttpRequest.RequestFile);
                    Assert.IsTrue(context.HttpRequest.GetParameters.ContainsKey("getparam"));
                    Assert.AreEqual("somevalue", context.HttpRequest.GetParameters["getparam"]);
                    Assert.IsTrue(context.HttpRequest.RequestHeaders.ContainsKey("X-CustomHeader"));
                    Assert.AreEqual("customheadervalue", context.HttpRequest.RequestHeaders["X-CustomHeader"]);
                    using (IWebSocket serverSocket = await context.AcceptWebsocketUpgrade(cancelToken, realTime, "chat"))
                    {
                        // Send one binary message
                        _logger.Log("Server sending binary message");
                        await serverSocket.SendAsync(fakeBinaryData, WebSocketMessageType.Binary, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                        // Receive one text message
                        _logger.Log("Server receiving text message");
                        using (WebSocketBufferResult recvMessage = await serverSocket.ReceiveAsBufferAsync(
                            CancellationToken.None,
                            DefaultRealTimeProvider.Singleton).ConfigureAwait(false))
                        {
                            _logger.Log("Server received text message");
                            Assert.IsTrue(recvMessage.Success);
                            Assert.AreEqual(WebSocketMessageType.Text, recvMessage.MessageType);
                            Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeTextData, recvMessage.Result.AsArraySegment));
                        }

                        // Send close
                        _logger.Log("Server initiating close");
                        await serverSocket.CloseWrite(CancellationToken.None, realTime, WebSocketCloseReason.NormalClosure, "Debug message").ConfigureAwait(false);
                        _logger.Log("Server waiting for final close");
                        await serverSocket.WaitForGracefulClose(CancellationToken.None, realTime);
                        _logger.Log("Server fully closed");
                    }
                });

                int port = LISTENER_PORT;
                TcpConnectionConfiguration connectionConfig = new TcpConnectionConfiguration("localhost", port, false);
                WebSocketConnectionParams connectionParams = new WebSocketConnectionParams()
                {
                    AdditionalHeaders = new HttpHeaders(),
                    AvailableProtocols = new string[] { "chat", "superchat" },
                };

                connectionParams.AdditionalHeaders.Add("X-CustomHeader", "customheadervalue");
                using (IWebSocket clientSocket = await webSocketClient.OpenWebSocketConnection(
                    _logger.Clone("ClientWebSocket"),
                    connectionConfig,
                    "/websocket?getparam=somevalue",
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton,
                    connectionParams).ConfigureAwait(false))
                {
                    // Receive one binary message
                    _logger.Log("Client receiving binary message");
                    WebSocketBufferResult recvMessage = await clientSocket.ReceiveAsBufferAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(recvMessage.Success);
                    _logger.Log("Client received binary message");
                    Assert.AreEqual(WebSocketMessageType.Binary, recvMessage.MessageType);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeBinaryData, recvMessage.Result.AsArraySegment));

                    // Send one text message
                    _logger.Log("Client sending text message");
                    await clientSocket.SendAsync(fakeTextData, WebSocketMessageType.Text, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    _logger.Log("Client sent test message");

                    // Get close message
                    recvMessage = await clientSocket.ReceiveAsBufferAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    _logger.Log("Client got close notification");
                    Assert.IsFalse(recvMessage.Success);
                    Assert.AreEqual(WebSocketCloseReason.NormalClosure, recvMessage.CloseReason);
                    Assert.AreEqual("Debug message", recvMessage.CloseMessage);
                    _logger.Log("Client closing write");
                    await clientSocket.CloseWrite(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    _logger.Log("Client fully closed");
                }
            }
            finally
            {
                _logger.Log("End HTTP test case");
            }
        }

        //[TestMethod]
        //public async Task TestHttpPToLChunkedParallel()
        //{
        //    AssertListenerServerHasStarted();
        //    ILogger logger = new ConsoleLogger();
        //    bool failureOccurred = false;
        //    Stopwatch testTimer = Stopwatch.StartNew();
        //    TimeSpan allowedTestTime = TimeSpan.FromSeconds(3);
        //    CancellationTokenSource testKiller = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        //    using (IThreadPool threadPool = new CustomThreadPool(logger.Clone("ThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ThreadPool", 10, false))
        //    {
        //        for (int thread = 0; thread < threadPool.ThreadCount; thread++)
        //        {
        //            threadPool.EnqueueUserAsyncWorkItem(async () =>
        //            {
        //                PortableHttpClient client = new PortableHttpClient("localhost", LISTENER_PORT, logger);
        //                FastRandom randomSeed = new FastRandom();
        //                try
        //                {
        //                    while (testTimer.Elapsed < allowedTestTime &&
        //                            !testKiller.Token.IsCancellationRequested)
        //                    {
        //                        HttpRequest request = new HttpRequest()
        //                        {
        //                            ProtocolVersion = "HTTP/1.1",
        //                            RequestFile = "/dev/rand",
        //                            RequestMethod = "GET"
        //                        };

        //                        int seed = randomSeed.NextInt();
        //                        int length = randomSeed.NextInt(500, 100000);

        //                        request.GetParameters["seed"] = seed.ToString();
        //                        request.GetParameters["length"] = length.ToString();

        //                        using (HttpResponse response = await client.SendRequestAsync(request, testKiller.Token))
        //                        {
        //                            Assert.IsNotNull(response);
        //                            Assert.AreEqual(200, response.ResponseCode);
        //                            Assert.AreEqual("application/octet-stream", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
        //                            Stream payloadStream = response.ReadContentAsStream();
        //                            Assert.IsNotNull(payloadStream);

        //                            byte[] expectedResponse = new byte[length];
        //                            FastRandom expectedResponseGenerator = new FastRandom(seed);
        //                            expectedResponseGenerator.NextBytes(expectedResponse);

        //                            // Download the streaming response
        //                            using (MemoryStream bucket = new MemoryStream())
        //                            {
        //                                using (PooledBuffer<byte> buf = BufferPool<byte>.Rent())
        //                                {
        //                                    int bytesRead = await payloadStream.ReadAsync(buf.Buffer, 0, 4096);
        //                                    while (bytesRead > 0)
        //                                    {
        //                                        bucket.Write(buf.Buffer, 0, bytesRead);
        //                                        bytesRead = await payloadStream.ReadAsync(buf.Buffer, 0, 4096);
        //                                    }

        //                                    Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedResponse, bucket.ToArray()));
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //                catch (Exception e)
        //                {
        //                    logger.Log(e, LogLevel.Err);
        //                    failureOccurred = true;
        //                    throw;
        //                }
        //            });
        //        }

        //        await threadPool.WaitForCurrentTasksToFinish(testKiller.Token, DefaultRealTimeProvider.Singleton);
        //        testKiller.Token.ThrowIfCancellationRequested();

        //        // this is done to allow the thread pool to propagate exceptions
        //        for (int thread = 0; thread < 100; thread++)
        //        {
        //            threadPool.EnqueueUserWorkItem(() => { });
        //        }
        //    }

        //    Assert.IsFalse(failureOccurred);
        //}

        /// <summary>
        /// Using HTTP is a convenient way to test the named pipe socket implementations at a high level
        /// </summary>
        /// <returns></returns>
        [Ignore] // Named pipes are still hackish and unreliable
        [TestMethod]
        public async Task TestHttpOverNamedPipe()
        {
            ILogger logger = _logger.Clone("PipeServer");
            UnitTestHttpServer namedPipeServer = new UnitTestHttpServer(
                new SocketHttpServer(
                    new NamedPipeServer(logger.Clone("NamedPipeServer"), "durandaltest", 64),
                    logger,
                    new CryptographicRandom(),
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty),
                logger);
            if (!(await namedPipeServer.StartServer("TestPipeServer", CancellationToken.None, _realTime)))
            {
                await namedPipeServer.StopServer(CancellationToken.None, _realTime);
                namedPipeServer = null;
                Assert.Inconclusive("Failed to start named pipe server");
            }

            ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(200));
            namedPipeServer.AddHandler("/small", async (context, cancelToken, realTime) =>
            {
                HttpResponse response = HttpResponse.OKResponse();
                response.SetContent(fakeData, "application/json");
                await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
            });

            SocketHttpClient client = new SocketHttpClient(
                new WeakPointer<ISocketFactory>(new NamedPipeClientSocketFactory()),
                new TcpConnectionConfiguration("durandaltest", 0, false),
                logger,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                new WeakPointer<IHttp2SessionManager>(_h2SessionManager),
                new Http2SessionPreferences());
            
            try
            {
                for (int c = 0; c < 10; c++)
                {
                    using (HttpRequest request = HttpRequest.CreateOutgoing("/small", "GET"))
                    using (NetworkResponseInstrumented<HttpResponse> result = await client.SendInstrumentedRequestAsync(request).ConfigureAwait(false))
                    {
                        Assert.IsTrue(result.Success);
                        HttpResponse response = result.Response;
                        Assert.AreEqual(200, response.ResponseCode);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false)));
                        Assert.AreEqual("application/json", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
                    }
                }
            }
            finally
            {
                namedPipeServer.Dispose();
                client.Dispose();
            }
        }

        /// <summary>
        /// Test that we can bind a single socket server to multiple wildcard endpoints and query them sequentially
        /// </summary>
        [TestMethod]
        public async Task TestHttpMultipleSocketBindings()
        {
            using (IThreadPool serverThreadPool = new TaskThreadPool())
            {
                UnitTestHttpServer server = null;
                List<ServerBindingInfo> endpoints = new List<ServerBindingInfo>();
                endpoints.Add(ServerBindingInfo.Wildcard());
                endpoints.Add(ServerBindingInfo.Wildcard());
                endpoints.Add(ServerBindingInfo.Wildcard());

                try
                {
                    ILogger logger = _logger.Clone("SocketMultiServer");

                    server = new UnitTestHttpServer(
                        new SocketHttpServer(
                            new RawTcpSocketServer(
                                endpoints,
                                logger,
                                _realTime,
                                NullMetricCollector.WeakSingleton,
                                DimensionSet.Empty,
                                new WeakPointer<IThreadPool>(serverThreadPool)),
                            logger,
                        new CryptographicRandom(),
                        NullMetricCollector.WeakSingleton,
                        DimensionSet.Empty),
                        logger);
                    Assert.IsTrue(await server.StartServer("TestMultiSocketServer", CancellationToken.None, _realTime));

                    ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(200));
                    server.AddHandler("/small", async (context, cancelToken, realTime) =>
                    {
                        HttpResponse response = HttpResponse.OKResponse();
                        response.SetContent(fakeData, "application/json");
                        await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    });

                    // Test the default local endpoint parser
                    IHttpClient client = new PortableHttpClient(server.LocalAccessUri, logger, NullMetricCollector.WeakSingleton, DimensionSet.Empty);
                    using (HttpRequest request = HttpRequest.CreateOutgoing("/small", "GET"))
                    using (NetworkResponseInstrumented<HttpResponse> result = await client.SendInstrumentedRequestAsync(request, CancellationToken.None, _realTime))
                    {
                        Assert.IsTrue(result.Success);
                        HttpResponse response = result.Response;
                        Assert.AreEqual(200, response.ResponseCode);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false)));
                        Assert.AreEqual("application/json", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
                        await result.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    for (int c = 0; c < 10; c++)
                    {
                        foreach (ServerBindingInfo endpoint in server.Endpoints)
                        {
                            client = new PortableHttpClient(new Uri("http://" + endpoint.LocalIpEndpoint + ":" + endpoint.LocalIpPort.Value), logger, NullMetricCollector.WeakSingleton, DimensionSet.Empty);
                            using (HttpRequest request = HttpRequest.CreateOutgoing("/small", "GET"))
                            using (NetworkResponseInstrumented<HttpResponse> result2 = await client.SendInstrumentedRequestAsync(request, CancellationToken.None, _realTime))
                            {
                                Assert.IsTrue(result2.Success);
                                HttpResponse response = result2.Response;
                                Assert.AreEqual(200, response.ResponseCode);
                                Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false)));
                                Assert.AreEqual("application/json", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
                                await result2.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                            }
                        }
                    }
                }
                finally
                {
                    await server.StopServer(CancellationToken.None, _realTime);
                }
            }
        }

        /// <summary>
        /// Asserts that headers with an empty value are supported
        /// </summary>
        [TestMethod]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, true, DisplayName = "Socket To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Listener, false, DisplayName = "Socket To Listener HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, true, DisplayName = "Portable To Listener HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Listener, false, DisplayName = "Portable To Listener HTTP/1.1")]
        public async Task TestHttpClientServerEmptyHeaderValue(HttpClientImplementation clientType, HttpServerImplementation serverType, bool useHttp10)
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(serverType);
            using (IHttpClient client = BuildTestHttpClient(clientType, serverType, useHttp10))
            {
                _logger.Log("Begin HTTP test case");
                try
                {
                    targetServer.AddHandler("/noheader", async (context, cancelToken, realTime) =>
                    {
                        HttpResponse response;
                        if (!context.HttpRequest.RequestHeaders.ContainsKey("X-Empty-Request-Header"))
                        {
                            response = HttpResponse.ServerErrorResponse();
                        }
                        else
                        {
                            response = HttpResponse.OKResponse();
                            response.ResponseHeaders.Add("X-Empty-Response-Header", string.Empty);
                        }

                        await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    });

                    using (HttpRequest request = HttpRequest.CreateOutgoing("/noheader", "GET"))
                    {
                        request.RequestHeaders.Add("X-Empty-Request-Header", string.Empty);
                        using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                        {
                            Assert.AreEqual(200, response.ResponseCode);
                            Assert.IsTrue(response.ResponseHeaders.ContainsKey("X-Empty-Response-Header"));
                            Assert.AreEqual(string.Empty, response.ResponseHeaders["X-Empty-Response-Header"]);
                            await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    _logger.Log("End HTTP test case");
                }
            }
        }

        [TestMethod]
        public async Task TestHttpRemotedJson()
        {
            await TestHttpRemoted(new JsonRemoteDialogProtocol());
        }

        [TestMethod]
        public async Task TestHttpRemotedBond()
        {
            await TestHttpRemoted(new BondRemoteDialogProtocol());
        }

        private async Task TestHttpRemoted(IRemoteDialogProtocol remoteProtocol)
        {
            AssertListenerServerHasStarted();

            // Use the large chunked-transfer payload to ensure that it gets buffered properly when it's remoted
            ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(60000));
            _listenerServer.AddHandler("/large", async (context, cancelToken, serverTime) =>
            {
                using (PipeStream stream = new PipeStream())
                {
                    HttpResponse response = HttpResponse.OKResponse();
                    response.SetContent(stream.GetReadStream(), "application/json");
                    PipeStream.PipeWriteStream writeStream = stream.GetWriteStream();

                    // Stream the chunked response asynchronously
                    Task backgroundTask = Task.Run(() =>
                    {
                        using (writeStream)
                        {
                            int CHUNK_SIZE = 50;
                            int cursor = 0;
                            while (cursor < fakeData.Count)
                            {
                                int size = Math.Min(CHUNK_SIZE, fakeData.Count - cursor);
                                byte[] buf = new byte[size];
                                Array.Copy(fakeData.Array, fakeData.Offset + cursor, buf, 0, size);
                                writeStream.Write(buf, 0, size);
                                cursor += size;
                            }
                        }
                    });

                    await context.WritePrimaryResponse(response, _logger, cancelToken, serverTime).ConfigureAwait(false);
                }
            });

            PortableHttpClientFactory actualHttpClient = new PortableHttpClientFactory();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            ILogger testLogger = new DebugLogger("Main", LogLevel.All);

            CancellationTokenSource testFinished = new CancellationTokenSource();
            testFinished.CancelAfter(TimeSpan.FromSeconds(30));
            Task serverThread = DurandalTaskExtensions.NoOpTask;
            RemoteDialogMethodDispatcher clientDispatcher = null;

            try
            {
                // Create a socket pair and a post office for each end
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair();

                using (PostOffice serverPostOffice = new PostOffice(socketPair.ServerSocket, testLogger, TimeSpan.FromSeconds(30), true, realTime))
                {
                    MailboxId serverMailbox = serverPostOffice.CreatePermanentMailbox(realTime, 0);
                    IRealTimeProvider serverTime = realTime.Fork("SocketServer");

                    RemoteProcedureRequestOrchestrator serverRemotedServiceOrchestrator = new RemoteProcedureRequestOrchestrator(
                        remoteProtocol,
                        new WeakPointer<PostOffice>(serverPostOffice),
                        testLogger,
                        new HttpRemoteProcedureRequestHandler(actualHttpClient));

                    serverThread = Task.Run(async () =>
                    {
                        try
                        {
                            while (!testFinished.IsCancellationRequested)
                            {
                                RetrieveResult<MailboxMessage> message = await serverPostOffice.TryReceiveMessage(
                                    serverMailbox,
                                    testFinished.Token,
                                    TimeSpan.FromMinutes(1),
                                    serverTime).ConfigureAwait(false);

                                if (message.Success)
                                {
                                    Tuple<object, Type> parsedMessage = remoteProtocol.Parse(message.Result.Buffer, testLogger);
                                    await serverRemotedServiceOrchestrator.HandleIncomingMessage(
                                        parsedMessage,
                                        message.Result,
                                        testFinished.Token,
                                        serverTime).ConfigureAwait(false);
                                }
                            }
                        }
                        finally
                        {
                            serverTime.Merge();
                        }
                    });

                    using (PostOffice clientPostOffice = new PostOffice(socketPair.ClientSocket, testLogger, TimeSpan.FromSeconds(30), false, realTime))
                    {
                        MailboxId clientMailbox = clientPostOffice.CreatePermanentMailbox(realTime, 0);

                        clientDispatcher = new RemoteDialogMethodDispatcher(clientPostOffice, clientMailbox, testLogger, remoteProtocol);

                        // Run tests
                        RemotedHttpClientFactory remotedHttpClientFactory = new RemotedHttpClientFactory(
                            clientDispatcher,
                            realTime,
                            testLogger);

                        IHttpClient client = remotedHttpClientFactory.CreateHttpClient("localhost", LISTENER_PORT, false, testLogger.Clone("RemoteHttp"));
                        
                        for (int c = 0; c < 10; c++)
                        {
                            using (HttpRequest request = HttpRequest.CreateOutgoing("/large", "GET"))
                            using (NetworkResponseInstrumented<HttpResponse> result = await client.SendInstrumentedRequestAsync(request).ConfigureAwait(false))
                            {
                                Assert.IsTrue(result.Success);
                                HttpResponse response = result.Response;
                                BufferPool<byte>.Shred();
                                Assert.AreEqual(200, response.ResponseCode);
                                Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, await response.ReadContentAsByteArrayAsync(testFinished.Token, realTime).ConfigureAwait(false)));
                                Assert.AreEqual("application/json", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
                            }
                        }

                        Assert.IsFalse(testFinished.IsCancellationRequested, "Test ran too long and was canceled");
                    }
                }
            }
            finally
            {
                testFinished.Cancel();
                clientDispatcher?.Stop();
                await serverThread.ConfigureAwait(false);
            }
        }

        #region Helpers

        private void AssertListenerServerHasStarted()
        {
            if (_listenerServer == null)
            {
                Assert.Inconclusive("Test will not run because the listener HTTP server failed to start. Please check your privileges and this system's HTTP configuration");
            }
        }

        private IHttpClient BuildTestHttpClient(HttpClientImplementation clientType, HttpServerImplementation serverType, bool useHttp10)
        {
            return BuildTestHttpClient(clientType, serverType, useHttp10 ? HttpVersion.HTTP_1_0 : HttpVersion.HTTP_1_1);
        }

        private IHttpClient BuildTestHttpClient(HttpClientImplementation clientType, HttpServerImplementation serverType, HttpVersion httpVersion)
        {
            int targetPort;
            switch (serverType)
            {
                case HttpServerImplementation.Listener:
                    targetPort = LISTENER_PORT;
                    break;
                default:
                    throw new NotImplementedException();
            }

            IHttpClient returnVal;
            switch (clientType)
            {
                case HttpClientImplementation.Portable:
                    returnVal = new PortableHttpClient("localhost", targetPort, false, _logger.Clone("HttpClient"), NullMetricCollector.WeakSingleton, DimensionSet.Empty);
                    break;
                case HttpClientImplementation.Socket:
                    returnVal = new SocketHttpClient(
                        new WeakPointer<ISocketFactory>(_socketFactory),
                        new TcpConnectionConfiguration("localhost", targetPort, false),
                        _logger.Clone("HttpClient"),
                        NullMetricCollector.WeakSingleton,
                        DimensionSet.Empty,
                        new WeakPointer<IHttp2SessionManager>(_h2SessionManager),
                        new Http2SessionPreferences());
                    break;
                default:
                    returnVal = new NullHttpClient();
                    break;
            }

            returnVal.InitialProtocolVersion = httpVersion;
            return returnVal;
        }

        private UnitTestHttpServer GetTargetHttpServer(HttpServerImplementation serverType)
        {
            switch (serverType)
            {
                case HttpServerImplementation.Listener:
                    AssertListenerServerHasStarted();
                    return _listenerServer;
                default:
                    throw new NotImplementedException();
            }
        }

        private static byte[] GetRandomPayload(int length)
        {
            byte[] returnVal = new byte[length];
            //for (int c = 0; c < length; c++)
            //{
            //    returnVal[c] = (byte)('G' + (c % 10));
            //}

            IRandom rand = new FastRandom();
            rand.NextBytes(returnVal);
            return returnVal;
        }

        private class UnitTestHttpServer : IHttpServerDelegate, IHttpServer
        {
            private readonly IDictionary<string, Func<IHttpServerContext, CancellationToken, IRealTimeProvider, Task>> _handlers;
            private readonly IHttpServer _baseServer;
            private readonly ILogger _logger;
            private int _disposed = 0;

            public UnitTestHttpServer(IHttpServer baseServer, ILogger logger)
            {
                _handlers = new Dictionary<string, Func<IHttpServerContext, CancellationToken, IRealTimeProvider, Task>>();
                _logger = logger;
                _baseServer = baseServer;
                _baseServer.RegisterSubclass(this);
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~UnitTestHttpServer()
            {
                Dispose(false);
            }
#endif

            public void Reset()
            {
                _handlers.Clear();
            }

            public void AddHandler(string decodedUrlPath, Func<IHttpServerContext, CancellationToken, IRealTimeProvider, Task> handler)
            {
                _handlers[decodedUrlPath] = handler;
            }

            public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                try
                {
                    Func<IHttpServerContext, CancellationToken, IRealTimeProvider, Task> handler;
                    if (_handlers.TryGetValue(serverContext.HttpRequest.DecodedRequestFile, out handler))
                    {
                        await handler(serverContext, cancelToken, realTime);
                    }
                    else
                    {
                        await serverContext.WritePrimaryResponse(HttpResponse.NotFoundResponse(), _logger, cancelToken, realTime).ConfigureAwait(false);
                    }

                    if (!serverContext.PrimaryResponseStarted)
                    {
                        await serverContext.WritePrimaryResponse(HttpResponse.ServerErrorResponse("Handler did not write a response"), _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e);

                    if (!serverContext.PrimaryResponseStarted)
                    {
                        await serverContext.WritePrimaryResponse(HttpResponse.ServerErrorResponse(e), _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                }
            }

            public IEnumerable<ServerBindingInfo> Endpoints
            {
                get
                {
                    return _baseServer.Endpoints;
                }
            }

            public bool Running
            {
                get
                {
                    return _baseServer.Running;
                }
            }

            public Uri LocalAccessUri
            {
                get
                {
                    return _baseServer.LocalAccessUri;
                }
            }

            public Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return _baseServer.StartServer(serverName, cancelToken, realTime);
            }

            public Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return _baseServer.StopServer(cancelToken, realTime);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                if (disposing)
                {
                    _baseServer.Dispose();
                }
            }

            public void RegisterSubclass(IHttpServerDelegate subclass)
            {
                throw new InvalidOperationException("Cannot subclass this class");
            }
        }

        #endregion
    }
}
