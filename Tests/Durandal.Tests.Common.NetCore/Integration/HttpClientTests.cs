using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Integration
{
    [TestClass]
    [DoNotParallelize]
    public class HttpClientTests
    {
        private static ILogger _logger = new ConsoleLogger("Main", LogLevel.All);

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestPortableHttp()
        {
            ILogger logger = new ConsoleLogger();
            IHttpClientFactory factory = new PortableHttpClientFactory();
            IHttpClient httpClient = factory.CreateHttpClient("marathon.bungie.org", 80, false, logger);
            using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
            using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger))
            {
                try
                {
                    Assert.IsNotNull(responseWrapper);
                    Assert.IsTrue(responseWrapper.Success);
                    HttpResponse response = responseWrapper.Response;
                    Assert.IsTrue(response.ResponseCode == 200 || response.ResponseCode == 301 || response.ResponseCode == 302);
                    ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(byteData.Count > 1000);
                }
                finally
                {
                    if (responseWrapper != null)
                    {
                        await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestPortableHttps()
        {
            ILogger logger = new ConsoleLogger();
            IHttpClientFactory factory = new PortableHttpClientFactory();
            IHttpClient httpClient = factory.CreateHttpClient("marathon.bungie.org", 443, true, logger);
            using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
            using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger))
            {
                try
                {
                    Assert.IsNotNull(responseWrapper);
                    Assert.IsTrue(responseWrapper.Success);
                    HttpResponse response = responseWrapper.Response;
                    Assert.AreEqual(200, response.ResponseCode);
                    ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(byteData.Count > 1000);
                }
                finally
                {
                    if (responseWrapper != null)
                    {
                        await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestTcpClientSocketHttpsDecoupled()
        {
            using (Http2SessionManager httpSessionManager = new Http2SessionManager())
            {
                const string remoteHost = "www.bing.com";
                System.Net.IPAddress[] bingAddresses = await System.Net.Dns.GetHostAddressesAsync(remoteHost).ConfigureAwait(false);
                Assert.IsNotNull(bingAddresses, "Could not resolve DNS address for " + remoteHost);
                Assert.AreNotEqual(0, bingAddresses.Length, "Could not resolve IP addresses for " + remoteHost);

                ILogger logger = new ConsoleLogger();
                TcpConnectionConfiguration config = new TcpConnectionConfiguration()
                {
                    DnsHostname = bingAddresses[0].ToString(),
                    Port = 443,
                    UseTLS = true,
                    SslHostname = remoteHost
                };

                HttpRequest request = HttpRequest.CreateOutgoing("/", "GET");
                using (ISocketFactory socketFactory = new TcpClientSocketFactory(logger, System.Security.Authentication.SslProtocols.None, ignoreCertErrors: false))
                using (IHttpClient httpClient = new SocketHttpClient(
                        new WeakPointer<ISocketFactory>(socketFactory),
                        config,
                        logger,
                        NullMetricCollector.WeakSingleton,
                        DimensionSet.Empty,
                        new WeakPointer<IHttp2SessionManager>(httpSessionManager),
                        new Http2SessionPreferences()))
                {
                    httpClient.InitialProtocolVersion = HttpVersion.HTTP_1_1;
                    using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger))
                    {
                        try
                        {
                            Assert.IsNotNull(responseWrapper);
                            Assert.IsTrue(responseWrapper.Success);
                            HttpResponse response = responseWrapper.Response;
                            Assert.AreEqual(200, response.ResponseCode);
                            ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            Assert.IsTrue(byteData.Count > 0);
                        }
                        finally
                        {
                            if (responseWrapper != null)
                            {
                                await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestTcpClientSocketHttp1_1()
        {
            using (Http2SessionManager sessionManager = new Http2SessionManager())
            {
                ISocketFactory socketFactory = new TcpClientSocketFactory(_logger);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 80, false),
                    _logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(sessionManager),
                    new Http2SessionPreferences());
                httpClient.InitialProtocolVersion = HttpVersion.HTTP_1_1;
                using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger))
                {
                    try
                    {
                        Assert.IsNotNull(responseWrapper);
                        Assert.IsTrue(responseWrapper.Success);
                        HttpResponse response = responseWrapper.Response;
                        Assert.IsTrue(response.ResponseCode == 200 || response.ResponseCode == 302);
                        ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(byteData.Count > 1000);
                    }
                    finally
                    {
                        if (responseWrapper != null)
                        {
                            await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestTcpClientSocketHttp2()
        {
            using (Http2SessionManager sessionManager = new Http2SessionManager())
            {
                ISocketFactory socketFactory = new TcpClientSocketFactory(_logger);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 80, false),
                    _logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(sessionManager),
                    new Http2SessionPreferences());
                httpClient.InitialProtocolVersion = HttpVersion.HTTP_2_0;
                using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger))
                {
                    try
                    {
                        Assert.IsNotNull(responseWrapper);
                        Assert.IsTrue(responseWrapper.Success);
                        HttpResponse response = responseWrapper.Response;
                        Assert.IsTrue(response.ResponseCode == 200 || response.ResponseCode == 302);
                        ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(byteData.Count > 1000);
                    }
                    finally
                    {
                        if (responseWrapper != null)
                        {
                            await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestTcpClientSocketHttps1_1()
        {
            using (Http2SessionManager sessionManager = new Http2SessionManager())
            {
                ISocketFactory socketFactory = new TcpClientSocketFactory(_logger);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 443, true),
                    _logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(sessionManager),
                    new Http2SessionPreferences());
                httpClient.InitialProtocolVersion = HttpVersion.HTTP_1_1;
                using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger))
                {
                    try
                    {
                        Assert.IsNotNull(responseWrapper);
                        Assert.IsTrue(responseWrapper.Success);
                        HttpResponse response = responseWrapper.Response;
                        Assert.AreEqual(200, response.ResponseCode);
                        ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(byteData.Count > 1000);
                    }
                    finally
                    {
                        if (responseWrapper != null)
                        {
                            await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestTcpClientSocketHttps2()
        {
            using (Http2SessionManager sessionManager = new Http2SessionManager())
            {
                ISocketFactory socketFactory = new TcpClientSocketFactory(_logger);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 443, true),
                    _logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(sessionManager),
                    new Http2SessionPreferences());
                httpClient.InitialProtocolVersion = HttpVersion.HTTP_2_0;
                using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger))
                {
                    try
                    {
                        Assert.IsNotNull(responseWrapper);
                        Assert.IsTrue(responseWrapper.Success);
                        HttpResponse response = responseWrapper.Response;
                        Assert.AreEqual(200, response.ResponseCode);
                        ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(byteData.Count > 1000);
                    }
                    finally
                    {
                        if (responseWrapper != null)
                        {
                            await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestPooledTcpClientSocketHttp1_1()
        {
            using (Http2SessionManager sessionManager = new Http2SessionManager())
            {
                ISocketFactory socketFactory = new PooledTcpClientSocketFactory(_logger, NullMetricCollector.Singleton, DimensionSet.Empty);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 80, false),
                    _logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(sessionManager),
                    new Http2SessionPreferences());
                httpClient.InitialProtocolVersion = HttpVersion.HTTP_1_1;
                for (int loop = 0; loop < 3; loop++)
                {
                    using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                    using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger))
                    {
                        try
                        {
                            Assert.IsNotNull(responseWrapper);
                            Assert.IsTrue(responseWrapper.Success);
                            HttpResponse response = responseWrapper.Response;
                            Assert.IsTrue(response.ResponseCode == 200 || response.ResponseCode == 302);
                            ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            Assert.IsTrue(byteData.Count > 1000);
                        }
                        finally
                        {
                            if (responseWrapper != null)
                            {
                                await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestPooledTcpClientSocketHttp2()
        {
            using (Http2SessionManager sessionManager = new Http2SessionManager())
            {
                ISocketFactory socketFactory = new PooledTcpClientSocketFactory(_logger, NullMetricCollector.Singleton, DimensionSet.Empty);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 80, false),
                    _logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(sessionManager),
                    new Http2SessionPreferences());
                httpClient.InitialProtocolVersion = HttpVersion.HTTP_2_0;
                for (int loop = 0; loop < 3; loop++)
                {
                    using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                    using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger))
                    {
                        try
                        {
                            Assert.IsNotNull(responseWrapper);
                            Assert.IsTrue(responseWrapper.Success);
                            HttpResponse response = responseWrapper.Response;
                            Assert.IsTrue(response.ResponseCode == 200 || response.ResponseCode == 302);
                            ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            Assert.IsTrue(byteData.Count > 1000);
                        }
                        finally
                        {
                            if (responseWrapper != null)
                            {
                                await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestPooledTcpClientSocketHttps1_1()
        {
            using (Http2SessionManager sessionManager = new Http2SessionManager())
            {
                ISocketFactory socketFactory = new PooledTcpClientSocketFactory(_logger, NullMetricCollector.Singleton, DimensionSet.Empty);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 443, true),
                    _logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(sessionManager),
                    new Http2SessionPreferences());
                httpClient.InitialProtocolVersion = HttpVersion.HTTP_1_1;
                for (int loop = 0; loop < 3; loop++)
                {
                    using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                    using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger))
                    {
                        try
                        {
                            Assert.IsNotNull(responseWrapper);
                            Assert.IsTrue(responseWrapper.Success);
                            HttpResponse response = responseWrapper.Response;
                            Assert.AreEqual(200, response.ResponseCode);
                            ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            Assert.IsTrue(byteData.Count > 1000);
                        }
                        finally
                        {
                            if (responseWrapper != null)
                            {
                                await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestPooledTcpClientSocketHttps2()
        {
            using (Http2SessionManager sessionManager = new Http2SessionManager())
            {
                ISocketFactory socketFactory = new PooledTcpClientSocketFactory(_logger, NullMetricCollector.Singleton, DimensionSet.Empty);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 443, true),
                    _logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(sessionManager),
                    new Http2SessionPreferences());
                httpClient.InitialProtocolVersion = HttpVersion.HTTP_2_0;
                for (int loop = 0; loop < 3; loop++)
                {
                    using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                    using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _logger))
                    {
                        try
                        {
                            Assert.IsNotNull(responseWrapper);
                            Assert.IsTrue(responseWrapper.Success);
                            HttpResponse response = responseWrapper.Response;
                            Assert.AreEqual(200, response.ResponseCode);
                            ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            Assert.IsTrue(byteData.Count > 1000);
                        }
                        finally
                        {
                            if (responseWrapper != null)
                            {
                                await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRawTcpSocketHttp()
        {
            using (Http2SessionManager httpSessionManager = new Http2SessionManager())
            {
                ILogger logger = new ConsoleLogger();
                ISocketFactory socketFactory = new RawTcpSocketFactory(logger);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 80, false),
                    logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(httpSessionManager),
                    new Http2SessionPreferences());

                using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger))
                {
                    try
                    {
                        Assert.IsNotNull(responseWrapper);
                        Assert.IsTrue(responseWrapper.Success);
                        HttpResponse response = responseWrapper.Response;
                        Assert.IsTrue(response.ResponseCode == 200 || response.ResponseCode == 302);
                        ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(byteData.Count > 1000);
                    }
                    finally
                    {
                        if (responseWrapper != null)
                        {
                            await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestRawTcpSocketHttps()
        {
            using (Http2SessionManager httpSessionManager = new Http2SessionManager())
            {
                ILogger logger = new ConsoleLogger();
                ISocketFactory socketFactory = new RawTcpSocketFactory(logger);
                IHttpClient httpClient = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("www.bing.com", 443, true),
                    logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    new WeakPointer<IHttp2SessionManager>(httpSessionManager),
                    new Http2SessionPreferences());
                using (HttpRequest request = HttpRequest.CreateOutgoing("/"))
                using (NetworkResponseInstrumented<HttpResponse> responseWrapper = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, logger))
                {
                    try
                    {
                        Assert.IsNotNull(responseWrapper);
                        Assert.IsTrue(responseWrapper.Success);
                        HttpResponse response = responseWrapper.Response;
                        Assert.AreEqual(200, response.ResponseCode);
                        ArraySegment<byte> byteData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.IsTrue(byteData.Count > 1000);
                    }
                    finally
                    {
                        if (responseWrapper != null)
                        {
                            await responseWrapper.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }
    }
}
