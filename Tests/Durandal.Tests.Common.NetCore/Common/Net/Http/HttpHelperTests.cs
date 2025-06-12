

namespace Durandal.Tests.Common.Net.Http
{
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Remoting;
    using Durandal.Common.Remoting.Protocol;
    using Durandal.Common.IO;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.MathExt;
    using System.Diagnostics;
    using Durandal.Common.Utils;
    using Durandal.Common.Remoting.Proxies;
    using Durandal.Common.Remoting.Handlers;
    using Durandal.Common.Cache;
    using Durandal.Common.Test;
    using Durandal.Common.Collections;

    [TestClass]
    public class HttpHelperTests
    {
        [TestMethod]
        public async Task TestHttpReadResponseFromSocket()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            string stringData = "HTTP/1.1 200 OK\r\n" +
                "Content-Length: 5\r\n" +
                "Content-Type: text/plain \r\n" + // extra whitespace after header
                "Text-Encoding:UTF-8\r\n" + // no whitespace between header key and value
                "\r\n";
            byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
            await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            encodedData = Encoding.UTF8.GetBytes("Hello");
            await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            HttpResponse parsedResponse = await HttpHelpers.ReadResponseFromSocket(pair.ServerSocket, HttpVersion.HTTP_1_1, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            Assert.IsNotNull(parsedResponse);
            Assert.AreEqual(HttpVersion.HTTP_1_1, parsedResponse.ProtocolVersion);
            Assert.AreEqual(200, parsedResponse.ResponseCode);
            Assert.AreEqual("OK", parsedResponse.ResponseMessage);
            Assert.AreEqual(3, parsedResponse.ResponseHeaders.KeyCount);
            Assert.AreEqual("5", parsedResponse.ResponseHeaders["Content-Length"]);
            Assert.AreEqual("text/plain", parsedResponse.ResponseHeaders["Content-Type"]);
            Assert.AreEqual("UTF-8", parsedResponse.ResponseHeaders["Text-Encoding"]);
            Assert.AreEqual(NetworkDirection.Incoming, parsedResponse.Direction);
        }

        [TestMethod]
        public async Task TestHttpResponseReadContentAsString()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            string stringData = "HTTP/1.1 200 OK\r\n" +
                "Content-Length: 5\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";
            byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
            await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            encodedData = Encoding.UTF8.GetBytes("Hello");
            await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            HttpResponse parsedResponse = await HttpHelpers.ReadResponseFromSocket(pair.ServerSocket, HttpVersion.HTTP_1_1, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            Assert.IsNotNull(parsedResponse);
            Assert.AreEqual(200, parsedResponse.ResponseCode);
            Assert.AreEqual(NetworkDirection.Incoming, parsedResponse.Direction);
            string contentString = await parsedResponse.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            Assert.AreEqual("Hello", contentString);
        }

        [TestMethod]
        public async Task TestHttpReadResponseFromSocketWhenContentIsntWrittenYet()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            string stringData = "HTTP/1.1 200 OK\r\n" +
                "Content-Length: 5\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";
            byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
            await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            HttpResponse parsedResponse = await HttpHelpers.ReadResponseFromSocket(pair.ServerSocket, HttpVersion.HTTP_1_1, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            encodedData = Encoding.UTF8.GetBytes("Hello");
            await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            Assert.IsNotNull(parsedResponse);
            Assert.AreEqual(200, parsedResponse.ResponseCode);
            Assert.AreEqual(NetworkDirection.Incoming, parsedResponse.Direction);
            string contentString = await parsedResponse.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            Assert.AreEqual("Hello", contentString);
        }

        [TestMethod]
        public async Task TestHttpParseHttpVersionFromRequestGET_HTTP_10()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            using (pair.ClientSocket)
            using (pair.ServerSocket)
            {
                string stringData = "GET / HTTP/1.0\r\n\r\n";
                byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
                await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                HttpVersion parsedVersion = await HttpHelpers.ParseHttpVersionFromRequest(pair.ServerSocket, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(HttpVersion.HTTP_1_0, parsedVersion);
            }
        }

        [TestMethod]
        public async Task TestHttpParseHttpVersionFromRequestGET_HTTP_11()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            using (pair.ClientSocket)
            using (pair.ServerSocket)
            {
                string stringData = "GET / HTTP/1.1\r\n\r\n";
                byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
                await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                HttpVersion parsedVersion = await HttpHelpers.ParseHttpVersionFromRequest(pair.ServerSocket, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(HttpVersion.HTTP_1_1, parsedVersion);
            }
        }

        [TestMethod]
        public async Task TestHttpParseHttpVersionFromRequestTRACE_HTTP_11()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            using (pair.ClientSocket)
            using (pair.ServerSocket)
            {
                string stringData = "TRACE / HTTP/1.1\r\n\r\n";
                byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
                await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                HttpVersion parsedVersion = await HttpHelpers.ParseHttpVersionFromRequest(pair.ServerSocket, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(HttpVersion.HTTP_1_1, parsedVersion);
            }
        }

        [TestMethod]
        public async Task TestHttpParseHttpVersionFromRequestPRI_HTTP_20()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            using (pair.ClientSocket)
            using (pair.ServerSocket)
            {
                string stringData = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"; // This is the designated sequence for initiating an HTTP/2.0 connection in RFC 7540
                byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
                await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                HttpVersion parsedVersion = await HttpHelpers.ParseHttpVersionFromRequest(pair.ServerSocket, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(HttpVersion.HTTP_2_0, parsedVersion);
            }
        }

        [TestMethod]
        public async Task TestHttpParseHttpVersionFromRequestNotAnHttpRequest()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            using (pair.ClientSocket)
            using (pair.ServerSocket)
            {
                byte[] randomData = new byte[8192];
                new FastRandom(943).NextBytes(randomData);
                await pair.ClientSocket.WriteAsync(randomData, 0, randomData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                HttpVersion parsedVersion = await HttpHelpers.ParseHttpVersionFromRequest(pair.ServerSocket, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.IsNull(parsedVersion);
            }
        }

        [TestMethod]
        public async Task TestHttpParseHttpVersionFromRequestSocketClosed()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            using (pair.ClientSocket)
            using (pair.ServerSocket)
            {
                await pair.ClientSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.ReadWrite).ConfigureAwait(false);
                HttpVersion parsedVersion = await HttpHelpers.ParseHttpVersionFromRequest(pair.ServerSocket, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.IsNull(parsedVersion);
            }
        }

        [TestMethod]
        public async Task TestHttpParseHttpVersionFromRequestNotEnoughDataOnSocket()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            using (pair.ClientSocket)
            using (pair.ServerSocket)
            {
                byte[] randomData = new byte[10];
                new FastRandom(31).NextBytes(randomData);
                await pair.ClientSocket.WriteAsync(randomData, 0, randomData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                await pair.ClientSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.Write).ConfigureAwait(false);
                HttpVersion parsedVersion = await HttpHelpers.ParseHttpVersionFromRequest(pair.ServerSocket, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.IsNull(parsedVersion);
            }
        }

        [TestMethod]
        public async Task TestHttpParseValidHttpRequest()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            using (pair.ClientSocket)
            using (pair.ServerSocket)
            {
                string stringData = "POST /my/very/easy%20method.txt HTTP/1.1\r\n" +
                    "Content-Length: 5\r\n" +
                    "Content-Type:  text/plain \r\n" + // extra whitespace before and after header
                    "Text-Encoding:UTF-8\r\n" + // no whitespace between header key and value
                    "\r\nHello";
                byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
                await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                HttpVersion parsedVersion = await HttpHelpers.ParseHttpVersionFromRequest(pair.ServerSocket, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(HttpVersion.HTTP_1_1, parsedVersion);

                Tuple<PooledBuffer<byte>, int> parsedTuple = await HttpHelpers.ReadHttpHeaderBlock(pair.ServerSocket, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(encodedData.Length - 5, parsedTuple.Item2);
                
                string httpMethod;
                string encodedRequestPath;
                int indexOfFirstHeaderLine;
                HttpVersion protocolVersion;
                HttpHelpers.ParseHttpRequestLine(parsedTuple.Item1.Buffer, parsedTuple.Item2, out httpMethod, out encodedRequestPath, out protocolVersion, out indexOfFirstHeaderLine);

                Assert.AreEqual("POST", httpMethod);
                Assert.AreEqual("/my/very/easy%20method.txt", encodedRequestPath);
                Assert.AreEqual(42, indexOfFirstHeaderLine);
                Assert.AreEqual(HttpVersion.HTTP_1_1, protocolVersion);

                int endOfHeaders;
                HttpHeaders parsedHeaders = HttpHelpers.ParseHttpHeaders(parsedTuple.Item1.Buffer, parsedTuple.Item2, indexOfFirstHeaderLine, out endOfHeaders);

                Assert.IsNotNull(parsedHeaders);
                Assert.AreEqual(3, parsedHeaders.KeyCount);
                Assert.AreEqual("5", parsedHeaders["Content-Length"]);
                Assert.AreEqual("text/plain", parsedHeaders["Content-Type"]);
                Assert.AreEqual("UTF-8", parsedHeaders["Text-Encoding"]);
            }
        }

        [TestMethod]
        public async Task TestHttpParseEmptyStringHeader()
        {
            ILogger logger = new ConsoleLogger();
            DirectSocketPair pair = DirectSocket.CreateSocketPair();
            using (pair.ClientSocket)
            using (pair.ServerSocket)
            {
                string stringData = "POST /my/very/easy%20method.txt HTTP/1.1\r\n" +
                    "Content-Length: 5\r\n" +
                    "Accept: \r\n" +
                    "\r\nHello";
                byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
                await pair.ClientSocket.WriteAsync(encodedData, 0, encodedData.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                HttpVersion parsedVersion = await HttpHelpers.ParseHttpVersionFromRequest(pair.ServerSocket, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(HttpVersion.HTTP_1_1, parsedVersion);

                Tuple<PooledBuffer<byte>, int> parsedTuple = await HttpHelpers.ReadHttpHeaderBlock(pair.ServerSocket, logger, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(encodedData.Length - 5, parsedTuple.Item2);

                int endOfHeaders;
                string httpMethod;
                string encodedRequestPath;
                int indexOfFirstHeaderLine;
                HttpVersion protocolVersion;
                HttpHelpers.ParseHttpRequestLine(parsedTuple.Item1.Buffer, parsedTuple.Item2, out httpMethod, out encodedRequestPath, out protocolVersion, out indexOfFirstHeaderLine);
                HttpHeaders parsedHeaders = HttpHelpers.ParseHttpHeaders(parsedTuple.Item1.Buffer, parsedTuple.Item2, indexOfFirstHeaderLine, out endOfHeaders);

                Assert.IsNotNull(parsedHeaders);
                Assert.AreEqual(2, parsedHeaders.KeyCount);
                Assert.AreEqual("5", parsedHeaders["Content-Length"]);
                Assert.AreEqual(string.Empty, parsedHeaders["Accept"]);
            }
        }

        [TestMethod]
        public void TestHttpGenerateChunkHeader()
        {
            byte[] header = new byte[16];

            // Check the terminator sequence first
            Assert.AreEqual(5, HttpHelpers.GenerateChunkHeaderBytes(0, true, header));
            byte[] expected = Encoding.ASCII.GetBytes("0\r\n\r\n");
            Assert.IsTrue(ArrayExtensions.ArrayEquals(header, 0, expected, 0, expected.Length));

            IRandom random = new FastRandom(75433);
            for (int test = 0; test < 10000; test++)
            {
                int chunkSize = random.NextInt();
                if (chunkSize == 0)
                {
                    continue;
                }

                string expectedString = chunkSize.ToString("X8").TrimStart('0') + "\r\n";
                expected = Encoding.ASCII.GetBytes(expectedString);
                int actualLength = HttpHelpers.GenerateChunkHeaderBytes(chunkSize, false, header);
                string actualString = Encoding.ASCII.GetString(header, 0, expected.Length);
                string errorMessage = "Expected " + expectedString.Replace("\r", "\\r").Replace("\n", "\\n") + " but got " + actualString.Replace("\r", "\\r").Replace("\n", "\\n");
                Assert.AreEqual(expected.Length, actualLength, errorMessage);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(header, 0, expected, 0, expected.Length), errorMessage);
            }
        }

        [TestMethod]
        public void TestHttpVersionComparability()
        {
            Assert.IsTrue(HttpVersion.HTTP_1_0.Equals(HttpVersion.HTTP_1_0));
            Assert.IsTrue(HttpVersion.HTTP_1_1.Equals(HttpVersion.HTTP_1_1));
            Assert.IsTrue(HttpVersion.HTTP_2_0.Equals(HttpVersion.HTTP_2_0));
            Assert.IsFalse(HttpVersion.HTTP_1_0.Equals(HttpVersion.HTTP_1_1));
            Assert.IsFalse(HttpVersion.HTTP_1_0.Equals(null));
            Assert.IsFalse(Equals(HttpVersion.HTTP_1_0, null));
            Assert.IsTrue(Equals(HttpVersion.HTTP_1_1, HttpVersion.HTTP_1_1));
            Assert.IsFalse(Equals(HttpVersion.HTTP_1_0, HttpVersion.HTTP_1_1));
            Assert.IsTrue(HttpVersion.HTTP_1_0 < HttpVersion.HTTP_1_1);
            Assert.IsTrue(HttpVersion.HTTP_1_0 < HttpVersion.HTTP_2_0);
            Assert.IsTrue(HttpVersion.HTTP_2_0 > HttpVersion.HTTP_1_1);
            Assert.IsTrue(HttpVersion.HTTP_2_0 >= new HttpVersion(2, 0));
            Assert.IsTrue(HttpVersion.HTTP_2_0 == new HttpVersion(2, 0));
            Assert.IsTrue(HttpVersion.HTTP_2_0 != HttpVersion.HTTP_1_1);
            Assert.IsTrue(HttpVersion.HTTP_1_0 != HttpVersion.HTTP_1_1);
            Assert.IsTrue(HttpVersion.HTTP_1_0 < HttpVersion.HTTP_1_1);
            Assert.IsTrue(HttpVersion.HTTP_1_0 <= HttpVersion.HTTP_1_1);
            Assert.IsTrue(HttpVersion.HTTP_1_1 <= new HttpVersion(1, 1));
            Assert.IsTrue(HttpVersion.HTTP_1_1 <= new HttpVersion(1, 3));
            Assert.IsTrue(HttpVersion.HTTP_1_1 != new HttpVersion(1, 3));
            Assert.IsTrue(HttpVersion.HTTP_2_0 < new HttpVersion(2, 5));
            Assert.IsTrue(HttpVersion.HTTP_2_0 <= new HttpVersion(2, 5));
            Assert.IsTrue(new HttpVersion(2, 5) > HttpVersion.HTTP_2_0);
            Assert.IsTrue(new HttpVersion(2, 5) >= HttpVersion.HTTP_2_0);
        }

        [TestMethod]
        public void TestHttpVersionParseValid()
        {
            Assert.AreEqual(HttpVersion.HTTP_1_0, HttpVersion.ParseHttpVersion(1, 0));
            Assert.AreEqual(HttpVersion.HTTP_1_0, HttpVersion.ParseHttpVersion('1', '0'));
            Assert.AreEqual(HttpVersion.HTTP_1_0, HttpVersion.ParseHttpVersion("1", "0"));
            Assert.AreEqual(HttpVersion.HTTP_1_0, HttpVersion.ParseHttpVersion("HTTP/1.0"));
            Assert.AreEqual(HttpVersion.HTTP_1_1, HttpVersion.ParseHttpVersion(1, 1));
            Assert.AreEqual(HttpVersion.HTTP_1_1, HttpVersion.ParseHttpVersion('1', '1'));
            Assert.AreEqual(HttpVersion.HTTP_1_1, HttpVersion.ParseHttpVersion("1", "1"));
            Assert.AreEqual(HttpVersion.HTTP_1_1, HttpVersion.ParseHttpVersion("HTTP/1.1"));
            Assert.AreEqual(HttpVersion.HTTP_2_0, HttpVersion.ParseHttpVersion(2, 0));
            Assert.AreEqual(HttpVersion.HTTP_2_0, HttpVersion.ParseHttpVersion('2', '0'));
            Assert.AreEqual(HttpVersion.HTTP_2_0, HttpVersion.ParseHttpVersion("2", "0"));
            Assert.AreEqual(HttpVersion.HTTP_2_0, HttpVersion.ParseHttpVersion("HTTP/2.0"));
        }

        [TestMethod]
        public void TestHttpVersionParseInvalid()
        {
            try
            {
                HttpVersion.ParseHttpVersion('1', 'a');
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }

            try
            {
                HttpVersion.ParseHttpVersion('a', '0');
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
            try
            {
                HttpVersion.ParseHttpVersion("1", "a");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }

            try
            {
                HttpVersion.ParseHttpVersion("a", "0");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }

            try
            {
                HttpVersion.ParseHttpVersion("HTTP-1.0");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }

            try
            {
                HttpVersion.ParseHttpVersion("HTTP");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }

            try
            {
                HttpVersion.ParseHttpVersion("HTTP/1.a");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }

            try
            {
                HttpVersion.ParseHttpVersion("HTTP/a.2");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }

            try
            {
                HttpVersion.ParseHttpVersion("HTTP/1");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }

            try
            {
                HttpVersion.ParseHttpVersion("HTTP/1.11");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlEmptyString()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl(string.Empty, out baseUrl, out getParams, out fragment));
            Assert.AreEqual("/", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(0, getParams.KeyCount);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlBasePath()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/", out baseUrl, out getParams, out fragment));
            Assert.AreEqual("/", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(0, getParams.KeyCount);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlEmptyQuery()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/?", out baseUrl, out getParams, out fragment));
            Assert.AreEqual("/", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(0, getParams.KeyCount);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlLeadingGetParamWithNoValue()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl(
                "/api/test?euri",
                out baseUrl,
                out getParams,
                out fragment));
            Assert.AreEqual("/api/test", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(1, getParams.KeyCount);
            Assert.IsTrue(getParams.ContainsKey("euri"));
            Assert.AreEqual(string.Empty, getParams["euri"]);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlGetParamWithNoValue()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl(
                "/api/test?ns=yt&euri",
                out baseUrl,
                out getParams,
                out fragment));
            Assert.AreEqual("/api/test", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(2, getParams.KeyCount);
            Assert.IsTrue(getParams.ContainsKey("euri"));
            Assert.AreEqual(string.Empty, getParams["euri"]);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlComplex()
        {
            // Youtube does some degroded stuff with url parameters
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl(
                "/api/stats/watchtime?ns=yt&el=detailpage&cpn=0eE-x3mU3u_W9799&ver=2&cmt=186.502&fmt=397&fs=0&rt=232.004&euri&lact=9485&cl=452150644&state=playing&volume=100&cbr=Firefox&cbrver=100.0&c=WEB&cver=2.20220602.00.00&cplayer=UNIPLAYER&cos=Windows&cosver=10.0&cplatform=DESKTOP&hl=en_US&cr=US&uga=m33&len=742.781&rtn=249&afmt=251&idpj=-4&ldpj=-27&rti=232&st=176.512&et=186.502&muted=0&docid=AqcyRxZJCXc&ei=Q3iaYruxB5r8kgaSurK4Dw&plid=AAXgkYWPHxrvIwGl&of=om7JmbosVaTL6WmHnnNJMw&vm=CAEQARgEOjJBS1JhaHdDWTVDaHhyWUJudl9iRkozUTdva1JJQUJKRk5JbkdvWWdTS0xFYWQyT3dtQWJXQVBta0tES2tVRFVWdlhzekdhaVNTMWd2YnZRSlR2M1ZvZjRGY3UyXzFZejM2OTRCN0pwSzBGRzhmU3dJNnpJVTZuTWZnYnkxNzZodzlBRmFpbWRhTFVN",
                out baseUrl,
                out getParams,
                out fragment));
            Assert.AreEqual("/api/stats/watchtime", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(38, getParams.KeyCount);
            Assert.IsTrue(getParams.ContainsKey("euri"));
            Assert.AreEqual(string.Empty, getParams["euri"]);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlSimpleFilePath()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/admin_portal/api/v3.php", out baseUrl, out getParams, out fragment));
            Assert.AreEqual("/admin_portal/api/v3.php", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(0, getParams.KeyCount);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlSimpleFilePathWithFragment()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/admin_portal/api/v3.php#help", out baseUrl, out getParams, out fragment));
            Assert.AreEqual("/admin_portal/api/v3.php", baseUrl);
            Assert.AreEqual("help", fragment);
            Assert.AreEqual(0, getParams.KeyCount);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlSingleParam()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/admin_portal/api/v3.php?key1=value1", out baseUrl, out getParams, out fragment));
            Assert.AreEqual("/admin_portal/api/v3.php", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(1, getParams.KeyCount);
            Assert.IsTrue(getParams.ContainsKey("key1"));
            Assert.IsTrue(getParams.ContainsKey("KEY1"));
            Assert.AreEqual("value1", getParams["key1"]);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlTwoParams()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/admin_portal/api/v3.php?a=b&c=d", out baseUrl, out getParams, out fragment));
            Assert.AreEqual("/admin_portal/api/v3.php", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(2, getParams.KeyCount);
            Assert.IsTrue(getParams.ContainsKey("a"));
            Assert.AreEqual("b", getParams["a"]);
            Assert.IsTrue(getParams.ContainsKey("c"));
            Assert.AreEqual("d", getParams["c"]);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlThreeParams()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/admin_portal/api/v3.php?a=b&c=d&e=f", out baseUrl, out getParams, out fragment));
            Assert.AreEqual("/admin_portal/api/v3.php", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(3, getParams.KeyCount);
            Assert.IsTrue(getParams.ContainsKey("a"));
            Assert.AreEqual("b", getParams["a"]);
            Assert.IsTrue(getParams.ContainsKey("c"));
            Assert.AreEqual("d", getParams["c"]);
            Assert.IsTrue(getParams.ContainsKey("e"));
            Assert.AreEqual("f", getParams["e"]);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlThreeParamsFragment()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/admin_portal/api/v3.php?a=b&c=d&e=f#g", out baseUrl, out getParams, out fragment));
            Assert.AreEqual("/admin_portal/api/v3.php", baseUrl);
            Assert.AreEqual(3, getParams.KeyCount);
            Assert.IsTrue(getParams.ContainsKey("a"));
            Assert.AreEqual("b", getParams["a"]);
            Assert.IsTrue(getParams.ContainsKey("c"));
            Assert.AreEqual("d", getParams["c"]);
            Assert.IsTrue(getParams.ContainsKey("e"));
            Assert.AreEqual("f", getParams["e"]);
            Assert.AreEqual("g", fragment);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlWithABunchOfAmpersands()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/admin_portal/api/v3.php?&&&&&key1=value1&&&&&key2=value2", out baseUrl, out getParams, out fragment));
            Assert.AreEqual("/admin_portal/api/v3.php", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(2, getParams.KeyCount);
            Assert.IsTrue(getParams.ContainsKey("key1"));
            Assert.AreEqual("value1", getParams["key1"]);
            Assert.IsTrue(getParams.ContainsKey("key2"));
            Assert.AreEqual("value2", getParams["key2"]);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlEmptyQueryValue()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/page.html?key1=&key2=value2", out baseUrl, out getParams, out fragment));
            Assert.AreEqual("/page.html", baseUrl);
            Assert.AreEqual(string.Empty, fragment);
            Assert.AreEqual(2, getParams.KeyCount);
            Assert.IsTrue(getParams.ContainsKey("key1"));
            Assert.AreEqual(string.Empty, getParams["key1"]);
            Assert.IsTrue(getParams.ContainsKey("key2"));
            Assert.AreEqual("value2", getParams["key2"]);
        }

        [TestMethod]
        public void TestHttpTryParseRelativeUrlInvalidInputs()
        {
            string baseUrl;
            string fragment;
            HttpFormParameters getParams;
            Assert.IsFalse(HttpHelpers.TryParseRelativeUrl("file.html", out baseUrl, out getParams, out fragment));
            Assert.IsFalse(HttpHelpers.TryParseRelativeUrl("server.com:80", out baseUrl, out getParams, out fragment));
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/file?a", out baseUrl, out getParams, out fragment));
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/file?a=", out baseUrl, out getParams, out fragment));
            Assert.IsFalse(HttpHelpers.TryParseRelativeUrl("/file?a=b&", out baseUrl, out getParams, out fragment));
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/file?a=b&c", out baseUrl, out getParams, out fragment));
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl("/file?a=b&c=", out baseUrl, out getParams, out fragment));
        }

        [TestMethod]
        public void TestHttpRequestGetParametersHandlesMultipleParams()
        {
            HttpRequest req = HttpRequest.CreateOutgoing("/feed");
            req.GetParameters.Add("include", "a");
            req.GetParameters.Add("include", "b");
            req.GetParameters.Add("include", "c");
            req.GetParameters.Add("exclude", "d");
            StringBuilder sb = new StringBuilder();
            req.WriteUriTo(sb);
            string urlString = sb.ToString();
            Assert.IsTrue(urlString.Contains("include=a"));
            Assert.IsTrue(urlString.Contains("include=b"));
            Assert.IsTrue(urlString.Contains("include=c"));
            Assert.IsTrue(urlString.Contains("exclude=d"));

            string basePath;
            HttpFormParameters parsedParameters;
            string fragment;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl(urlString, out basePath, out parsedParameters, out fragment));
            Assert.AreEqual(2, parsedParameters.KeyCount);
            Assert.IsTrue(parsedParameters.ContainsKey("exclude"));
            Assert.AreEqual(1, parsedParameters.GetAllParameterValues("exclude").Count);
            Assert.IsTrue(parsedParameters.ContainsKey("include"));
            Assert.AreEqual(3, parsedParameters.GetAllParameterValues("include").Count);
        }

        [TestMethod]
        public void TestHttpRequestGetParametersCanHaveNoValue()
        {
            HttpRequest req = HttpRequest.CreateOutgoing("/feed");
            req.GetParameters.Add("key", "value");
            req.GetParameters.Add("special_a", "value_a");
            req.GetParameters.Add("special_a", string.Empty);
            req.GetParameters.Add("special_a", string.Empty);
            req.GetParameters.Add("special_b", "value_b");
            req.GetParameters.Add("special_b", null);
            req.GetParameters.Add("special_b", null);
            StringBuilder sb = new StringBuilder();
            req.WriteUriTo(sb);
            string urlString = sb.ToString();
            Assert.IsTrue(urlString.Contains("key=value"));
            Assert.IsTrue(urlString.Contains("special_a&"));
            Assert.IsTrue(urlString.Contains("special_b&"));

            string basePath;
            HttpFormParameters parsedParameters;
            string fragment;
            Assert.IsTrue(HttpHelpers.TryParseRelativeUrl(urlString, out basePath, out parsedParameters, out fragment));
            Assert.AreEqual(3, parsedParameters.KeyCount);
            Assert.IsTrue(parsedParameters.ContainsKey("special_a"));
            Assert.AreEqual(3, parsedParameters.GetAllParameterValues("special_a").Count);
            Assert.IsTrue(parsedParameters.ContainsKey("special_b"));
            Assert.AreEqual(3, parsedParameters.GetAllParameterValues("special_b").Count);
        }
    }
}
