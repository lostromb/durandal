

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
    public class HttpServerClientTests
    {
        private const int SOCKET_PORT = 62299;
        private static UnitTestHttpServer _listenerServer;
        private static UnitTestHttpServer _socketServer;
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
            Socket,
            Kestrel,
        }

        [ClassInitialize]
        public static void StartServer(TestContext context)
        {
            try
            {
                ILogger logger = _logger.Clone("SocketServer");
                IThreadPool socketThreadPool = new TaskThreadPool(NullMetricCollector.WeakSingleton, DimensionSet.Empty, "SocketThreadPool");
                _socketServer = new UnitTestHttpServer(
                    new SocketHttpServer(
                        new RawTcpSocketServer(
                            new ServerBindingInfo[] { new ServerBindingInfo(ServerBindingInfo.WILDCARD_HOSTNAME, SOCKET_PORT) },
                            logger,
                            _realTime,
                            NullMetricCollector.WeakSingleton,
                            DimensionSet.Empty,
                            new WeakPointer<IThreadPool>(socketThreadPool)),
                        logger,
                        new CryptographicRandom(),
                        NullMetricCollector.WeakSingleton,
                        DimensionSet.Empty),
                    logger);

                if (!_socketServer.StartServer("TestSocketServer", CancellationToken.None, _realTime).Await())
                {
                    _socketServer.StopServer(CancellationToken.None, _realTime).Await();
                    _socketServer = null;
                }
            }
            catch (Exception e)
            {
                _logger.Log("Error occurred while starting the socket server", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
                _socketServer = null;
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

            if (_socketServer != null)
            {
                _socketServer.StopServer(CancellationToken.None, _realTime).AwaitWithTimeout(3000);
                _socketServer.Dispose();
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

            if (_socketServer != null)
            {
                _socketServer.Reset();
            }

            _h2SessionManager.ShutdownAllActiveSessions().Await();
        }

        [TestMethod]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, true, DisplayName = "Socket To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, false, DisplayName = "Socket To Socket HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, true, DisplayName = "Portable To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, false, DisplayName = "Portable To Socket HTTP/1.1")]
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
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, 1, 0, DisplayName = "Socket To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, 1, 1, DisplayName = "Socket To Socket HTTP/1.1")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, 2, 0, DisplayName = "Socket To Socket HTTP/2.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, 1, 0, DisplayName = "Portable To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, 1, 1, DisplayName = "Portable To Socket HTTP/1.1")]
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
                                Assert.AreEqual("OK", response.ResponseMessage);
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
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, true, DisplayName = "Socket To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, false, DisplayName = "Socket To Socket HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, true, DisplayName = "Portable To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, false, DisplayName = "Portable To Socket HTTP/1.1")]
        public async Task TestHttpClientServerBasicNoResponseBody(HttpClientImplementation clientType, HttpServerImplementation serverType, bool useHttp10)
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(serverType);
            using (IHttpClient client = BuildTestHttpClient(clientType, serverType, useHttp10))
            {
                _logger.Log("Begin HTTP test case");
                try
                {
                    ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(200));
                    targetServer.AddHandler("/accept", async (context, cancelToken, realTime) =>
                    {
                        HttpResponse response = HttpResponse.AcceptedResponse();
                        response.ResponseHeaders["DocumentId"] = "SomeDocumentId";
                        await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    });

                    for (int c = 0; c < 10; c++)
                    {
                        using (HttpRequest request = HttpRequest.CreateOutgoing("/accept", "GET"))
                        using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                        {
                            Assert.AreEqual(202, response.ResponseCode);
                            Assert.AreEqual("Accepted", response.ResponseMessage);
                            Assert.IsTrue(response.ResponseHeaders.ContainsKey("DocumentId"));
                            Assert.AreEqual("SomeDocumentId", response.ResponseHeaders["DocumentId"]);
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
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, true, DisplayName = "Socket To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, false, DisplayName = "Socket To Socket HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, true, DisplayName = "Portable To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, false, DisplayName = "Portable To Socket HTTP/1.1")]
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
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, true, DisplayName = "Socket To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, false, DisplayName = "Socket To Socket HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, true, DisplayName = "Portable To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, false, DisplayName = "Portable To Socket HTTP/1.1")]
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
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, true, DisplayName = "Socket To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, false, DisplayName = "Socket To Socket HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, true, DisplayName = "Portable To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, false, DisplayName = "Portable To Socket HTTP/1.1")]
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
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, DisplayName = "Socket To Socket")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, DisplayName = "Portable To Socket")]
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
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, true, DisplayName = "Socket To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, false, DisplayName = "Socket To Socket HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, true, DisplayName = "Portable To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, false, DisplayName = "Portable To Socket HTTP/1.1")]
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
        public async Task TestHttpClientServerTrailers()
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(HttpServerImplementation.Socket);
            using (IHttpClient client = BuildTestHttpClient(HttpClientImplementation.Socket, HttpServerImplementation.Socket, useHttp10: false))
            {
                _logger.Log("Begin HTTP test case");
                try
                {
                    targetServer.AddHandler("/fixed", async (context, cancelToken, realTime) =>
                    {
                        HttpResponse response = HttpResponse.OKResponse();
                        ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(200));
                        response.SetContent(fakeData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                        await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    });
                    targetServer.AddHandler("/chunked", async (context, cancelToken, realTime) =>
                    {
                        HttpResponse response = HttpResponse.OKResponse();
                        ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(200));
                        response.SetContent(new MemoryStream(fakeData.Array, fakeData.Offset, fakeData.Count), HttpConstants.MIME_TYPE_OCTET_STREAM);
                        await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    });
                    targetServer.AddHandler("/trailers", async (context, cancelToken, realTime) =>
                    {
                        if (!context.SupportsTrailers)
                        {
                            await context.WritePrimaryResponse(HttpResponse.ServerErrorResponse(), NullLogger.Singleton, cancelToken, realTime);
                        }
                        else
                        {
                            byte[] randomData = new byte[10000];
                            new FastRandom(658330).NextBytes(randomData);
                            MemoryStream responseStream = new MemoryStream(randomData);
                            HttpResponse response = HttpResponse.OKResponse();
                            response.SetContent(responseStream, HttpConstants.MIME_TYPE_OCTET_STREAM);
                            List<string> trailerNames = new List<string>()
                            {
                                "X-Render-Time",
                                "X-Expires-At"
                            };

                            Stopwatch renderTimer = Stopwatch.StartNew();
                            await context.WritePrimaryResponse(response, NullLogger.Singleton, cancelToken, realTime, trailerNames, (string trailerName) =>
                            {
                                if (string.Equals(trailerName, "X-Render-Time"))
                                {
                                    renderTimer.Stop();
                                    return Task.FromResult(renderTimer.ElapsedMillisecondsPrecise().ToString());
                                }
                                else if (string.Equals(trailerName, "X-Expires-At"))
                                {
                                    return Task.FromResult(realTime.Time.ToString());
                                }
                                else
                                {
                                    return Task.FromResult(string.Empty);
                                }
                            });
                        }
                    });

                    IRandom rand = new FastRandom();
                    for (int c = 0; c < 30; c++)
                    {
                        // Alternate between random response formattings to ensure that the socket is in a valid state between every single request
                        switch (rand.NextInt(0, 3))
                        {
                            case 0:
                                using (HttpRequest request = HttpRequest.CreateOutgoing("/trailers", "GET"))
                                {
                                    request.RequestHeaders[HttpConstants.HEADER_KEY_TE] = HttpConstants.HEADER_VALUE_TRAILERS;
                                    using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                                    {
                                        Assert.AreEqual(200, response.ResponseCode);
                                        Assert.IsTrue(response.ResponseHeaders.ContainsKey("Trailer"));
                                        ArraySegment<byte> responseData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                                        Assert.IsNotNull(response.ResponseTrailers);
                                        Assert.IsTrue(response.ResponseTrailers.ContainsKey("X-Render-Time"));
                                        Assert.IsTrue(response.ResponseTrailers.ContainsKey("X-Expires-At"));
                                        await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                                    }
                                }
                                break;
                            case 1:
                                using (HttpRequest request = HttpRequest.CreateOutgoing("/fixed", "GET"))
                                using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                                {
                                    Assert.AreEqual(200, response.ResponseCode);
                                    ArraySegment<byte> responseData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                                    await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                                }
                                break;
                            case 2:
                                using (HttpRequest request = HttpRequest.CreateOutgoing("/chunked", "GET"))
                                using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                                {
                                    Assert.AreEqual(200, response.ResponseCode);
                                    ArraySegment<byte> responseData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                                    await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                                }
                                break;
                        }
                    }
                }
                finally
                {
                    _logger.Log("End HTTP test case");
                }
            }
        }

        /// <summary>
        /// Ensures that the socket server can properly detect the header delimiter (\r\n\r\n) even when it spans a read buffer boundary
        /// </summary>
        [TestMethod]
        public async Task TestHttpSToSHeaderDelimiterParsesProperly()
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(HttpServerImplementation.Socket);
            using (IHttpClient client = BuildTestHttpClient(HttpClientImplementation.Socket, HttpServerImplementation.Socket, false))
            {
                _logger.Log("Begin HTTP test case");
                StringBuilder headerBuilder = new StringBuilder();
                try
                {
                    ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(200));
                    targetServer.AddHandler("/small", async (context, cancelToken, realTime) =>
                    {
                        HttpResponse response = HttpResponse.OKResponse();
                        response.SetContent(fakeData, "application/json");
                        await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    });

                    for (int c = 0; c < 1024; c++)
                    {
                        using (HttpRequest request = HttpRequest.CreateOutgoing("/small", "GET"))
                        {
                            headerBuilder.Append('0');
                            request.RequestHeaders.Add("TestHeader", headerBuilder.ToString());
                            using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                            {
                                Assert.AreEqual(200, response.ResponseCode);
                                ArraySegment<byte> responseBody = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                                Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, responseBody));
                                Assert.AreEqual("application/json", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
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
        public async Task TestHttpSToSUpgradeFrom11To20()
        {
            UnitTestHttpServer targetServer = GetTargetHttpServer(HttpServerImplementation.Socket);
            using (IHttpClient client = BuildTestHttpClient(HttpClientImplementation.Socket, HttpServerImplementation.Socket, HttpVersion.HTTP_2_0))
            using (CancellationTokenSource cancelTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                CancellationToken testCancel = cancelTokenSource.Token;
                _logger.Log("Begin HTTP test case");
                try
                {
                    ArraySegment<byte> requestData = new ArraySegment<byte>(GetRandomPayload(200));
                    ArraySegment<byte> responseData = new ArraySegment<byte>(GetRandomPayload(200));
                    targetServer.AddHandler("/small", async (context, cancelToken, realTime) =>
                    {
                        HttpResponse response = HttpResponse.OKResponse();
                        response.SetContent(responseData, "application/json");
                        await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    });

                    for (int c = 0; c < 10; c++)
                    {
                        using (HttpRequest request = HttpRequest.CreateOutgoing("/small", "POST"))
                        {
                            request.SetContent(requestData, "application/json");
                            using (HttpResponse response = await client.SendRequestAsync(request).ConfigureAwait(false))
                            {
                                Assert.AreEqual(200, response.ResponseCode);
                                Assert.AreEqual(HttpVersion.HTTP_2_0, response.ProtocolVersion);
                                ArraySegment<byte> responseBody = await response.ReadContentAsByteArrayAsync(testCancel, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                                Assert.IsTrue(ArrayExtensions.ArrayEquals(responseData, responseBody));
                                Assert.AreEqual("application/json", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
                                await response.FinishAsync(testCancel, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
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
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, false, DisplayName = "System Websocket To Socket HTTP/1.1")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, false, DisplayName = "Durandal Websocket To Socket HTTP/1.1")]
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

                int port = SOCKET_PORT;
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
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, true, DisplayName = "Socket To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Socket, HttpServerImplementation.Socket, false, DisplayName = "Socket To Socket HTTP/1.1")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, true, DisplayName = "Portable To Socket HTTP/1.0")]
        [DataRow(HttpClientImplementation.Portable, HttpServerImplementation.Socket, false, DisplayName = "Portable To Socket HTTP/1.1")]
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

        /// <summary>
        /// Tests the socket HTTP client/server directly using in-memory buffers
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestHttpSocketDirect()
        {
            using (Http2SessionManager localSessionManager = new Http2SessionManager())
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                CancellationToken testAbort = cts.Token;
                ILogger logger = new ConsoleLogger();
                SocketHttpServer server = new SocketHttpServer(
                    new NullSocketServer(),
                    logger.Clone("Test"),
                    new CryptographicRandom(),
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty);
                server.RegisterSubclass(_socketServer);
                ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(10000));
                _socketServer.AddHandler("/this/is/a/test/path", async (context, cancelToken, realTime) =>
                {
                    // Echo the input data back to caller
                    ArraySegment<byte> inputData = await context.HttpRequest.ReadContentAsByteArrayAsync(testAbort, realTime).ConfigureAwait(false);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, inputData));
                    HttpResponse response = HttpResponse.OKResponse();
                    response.SetContent(inputData, "application/octet-stream");
                    await context.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                });

                ISocketFactory socketFactory = new DirectSocketFactory(server, logger.Clone("DirectSocketFactory"), new TaskThreadPool());
                IHttpClient client = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("fakehost", 0),
                    logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(localSessionManager),
                    new Http2SessionPreferences());

                //client.InitialProtocolVersion = HttpVersion.HTTP_1_1;
                using (HttpRequest request = HttpRequest.CreateOutgoing("/this/is/a/test/path", "POST"))
                {
                    request.SetContent(fakeData, "application/octet-stream");
                    using (NetworkResponseInstrumented<HttpResponse> response = await client.SendInstrumentedRequestAsync(request, testAbort, _realTime))
                    {
                        Assert.IsFalse(testAbort.IsCancellationRequested, "The test took too long and was aborted");
                        Assert.IsTrue(response.Success);
                        Assert.AreEqual(200, response.Response.ResponseCode);
                        ArraySegment<byte> actualContent = await response.Response.ReadContentAsByteArrayAsync(testAbort, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, actualContent));
                        await response.FinishAsync(testAbort, _realTime).ConfigureAwait(false);
                    }
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
            AssertSocketServerHasStarted();

            PortableHttpClientFactory actualHttpClient = new PortableHttpClientFactory();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            ILogger testLogger = new DebugLogger("Main", LogLevel.All);

            CancellationTokenSource testFinished = new CancellationTokenSource();
            testFinished.CancelAfter(TimeSpan.FromSeconds(30));
            CancellationToken testAbortToken = testFinished.Token;
            Task serverThread = DurandalTaskExtensions.NoOpTask;
            RemoteDialogMethodDispatcher clientDispatcher = null;

            ILogger serverLogger = testLogger.Clone("ServerHandler");

            // Use the large chunked-transfer payload to ensure that it gets buffered properly when it's remoted
            ArraySegment<byte> fakeData = new ArraySegment<byte>(GetRandomPayload(60000));
            _socketServer.AddHandler("/large", async (context, cancelToken, serverTime) =>
            {
                serverLogger.Log("Processing request");
                using (PipeStream stream = new PipeStream())
                {
                    HttpResponse response = HttpResponse.OKResponse();
                    response.SetContent(stream.GetReadStream(), "application/json");
                    PipeStream.PipeWriteStream writeStream = stream.GetWriteStream();

                    // Stream the chunked response asynchronously
                    IRealTimeProvider bgTaskTime = serverTime.Fork("ServerHandler");
                    Task backgroundTask = Task.Run(() =>
                    {
                        try
                        {
                            using (writeStream)
                            {
                                int CHUNK_SIZE = 50;
                                int cursor = 0;
                                while (cursor < fakeData.Count && !testAbortToken.IsCancellationRequested)
                                {
                                    int size = Math.Min(CHUNK_SIZE, fakeData.Count - cursor);
                                    byte[] buf = new byte[size];
                                    Array.Copy(fakeData.Array, fakeData.Offset + cursor, buf, 0, size);
                                    writeStream.Write(buf, 0, size, cancelToken, bgTaskTime);
                                    cursor += size;
                                }


                                serverLogger.Log("Finished writing response payload");
                            }
                        }
                        finally
                        {
                            bgTaskTime.Merge();
                        }
                    });

                    await context.WritePrimaryResponse(response, _logger, testAbortToken, serverTime).ConfigureAwait(false);
                    serverLogger.Log("Wrote primary response");
                }
            });

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
                            while (!testAbortToken.IsCancellationRequested)
                            {
                                RetrieveResult<MailboxMessage> message = await serverPostOffice.TryReceiveMessage(
                                    serverMailbox,
                                    testAbortToken,
                                    TimeSpan.FromMinutes(1),
                                    serverTime).ConfigureAwait(false);

                                if (message.Success)
                                {
                                    Tuple<object, Type> parsedMessage = remoteProtocol.Parse(message.Result.Buffer, testLogger);
                                    await serverRemotedServiceOrchestrator.HandleIncomingMessage(
                                        parsedMessage,
                                        message.Result,
                                        testAbortToken,
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

                        IHttpClient client = remotedHttpClientFactory.CreateHttpClient("localhost", SOCKET_PORT, false, testLogger.Clone("RemoteHttp"));
                        
                        for (int c = 0; c < 10 && !testAbortToken.IsCancellationRequested; c++)
                        {
                            testLogger.Log("Client making request");
                            using (HttpRequest request = HttpRequest.CreateOutgoing("/large", "GET"))
                            using (NetworkResponseInstrumented<HttpResponse> result = await client.SendInstrumentedRequestAsync(request, testAbortToken, realTime).ConfigureAwait(false))
                            {
                                testLogger.Log("Client got response");
                                Assert.IsTrue(result.Success);
                                using (HttpResponse response = result.Response)
                                {
                                    BufferPool<byte>.Shred();
                                    Assert.AreEqual(200, response.ResponseCode);
                                    Assert.IsTrue(ArrayExtensions.ArrayEquals(fakeData, await response.ReadContentAsByteArrayAsync(testAbortToken, realTime).ConfigureAwait(false)));
                                    Assert.AreEqual("application/json", response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE]);
                                    await response.FinishAsync(testAbortToken, realTime);
                                }
                            }
                        }

                        Assert.IsFalse(testAbortToken.IsCancellationRequested, "Test ran too long and was canceled");
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

        private void AssertSocketServerHasStarted()
        {
            if (_socketServer == null)
            {
                Assert.Inconclusive("Test will not run because the socket HTTP server failed to start. Please check your privileges and this system's HTTP configuration");
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
                case HttpServerImplementation.Socket:
                    targetPort = SOCKET_PORT;
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
                case HttpServerImplementation.Socket:
                    AssertSocketServerHasStarted();
                    return _socketServer;
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
