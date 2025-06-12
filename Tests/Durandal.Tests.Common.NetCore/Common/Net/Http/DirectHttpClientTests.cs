

namespace Durandal.Tests.Common.Net.Http
{
    using Durandal.Common.Cache;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using Durandal.Common.Time;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.Test;
    using System.Diagnostics;
    using Durandal.Common.Collections;

    [TestClass]
    public class DirectHttpClientTests
    {
        [TestMethod]
        public async Task TestHttpDirectHttpClientEcho()
        {
            IHttpServerDelegate echoServer = new EchoServerDelegate();
            DirectHttpClient httpClient = new DirectHttpClient(echoServer);
            byte[] randomData = new byte[10000];
            new FastRandom(96561).NextBytes(randomData);
            using (HttpRequest request = HttpRequest.CreateOutgoing("/echo", "POST"))
            {
                request.SetContent(randomData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                using (HttpResponse response = await httpClient.SendRequestAsync(request).ConfigureAwait(false))
                {
                    ArraySegment<byte> responseData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual(200, response.ResponseCode);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(new ArraySegment<byte>(randomData), responseData));
                }
            }
        }

        [TestMethod]
        public async Task TestHttpDirectHttpClientTrailers()
        {
            IHttpServerDelegate echoServer = new TrailerServerDelegate();
            DirectHttpClient httpClient = new DirectHttpClient(echoServer);
            using (HttpRequest request = HttpRequest.CreateOutgoing("/trailers"))
            {
                request.RequestHeaders[HttpConstants.HEADER_KEY_TE] = HttpConstants.HEADER_VALUE_TRAILERS;
                using (HttpResponse response = await httpClient.SendRequestAsync(request).ConfigureAwait(false))
                {
                    Assert.AreEqual(200, response.ResponseCode);
                    Assert.IsNull(response.ResponseTrailers);
                    ArraySegment<byte> responseData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.IsNotNull(response.ResponseTrailers);
                    Assert.IsTrue(response.ResponseTrailers.ContainsKey("X-Render-Time"));
                    Assert.IsTrue(response.ResponseTrailers.ContainsKey("X-Expires-At"));
                    await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        public async Task TestHttpDirectHttpClientTrailersNotSupported()
        {
            IHttpServerDelegate echoServer = new TrailerServerDelegate();
            DirectHttpClient httpClient = new DirectHttpClient(echoServer);
            using (HttpRequest request = HttpRequest.CreateOutgoing("/trailers"))
            {
                request.RequestHeaders[HttpConstants.HEADER_KEY_TE] = "somethingelse";
                using (HttpResponse response = await httpClient.SendRequestAsync(request).ConfigureAwait(false))
                {
                    Assert.AreEqual(500, response.ResponseCode);
                    ArraySegment<byte> responseData = await response.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.IsNull(response.ResponseTrailers);
                }
            }
        }

        private class EchoServerDelegate : IHttpServerDelegate
        {
            public async Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse response = HttpResponse.OKResponse();
                response.SetContent(context.HttpRequest.GetIncomingContentStream(), HttpConstants.MIME_TYPE_OCTET_STREAM);
                await context.WritePrimaryResponse(response, NullLogger.Singleton, cancelToken, realTime);
            }
        }

        private class TrailerServerDelegate : IHttpServerDelegate
        {
            public async Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (!context.SupportsTrailers)
                {
                    await context.WritePrimaryResponse(HttpResponse.ServerErrorResponse(), NullLogger.Singleton, cancelToken, realTime);
                }
                else
                {
                    byte[] randomData = new byte[10000];
                    new FastRandom(658330).NextBytes(randomData);
                    HttpResponse response = HttpResponse.OKResponse();
                    response.SetContent(randomData, HttpConstants.MIME_TYPE_OCTET_STREAM);
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
            }
        }
    }
}
