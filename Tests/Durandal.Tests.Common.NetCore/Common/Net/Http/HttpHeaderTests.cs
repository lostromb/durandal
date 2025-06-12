

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

    [TestClass]
    public class HttpHeaderTests
    {
        [TestMethod]
        public void TestHttpParseHeadersNoData()
        {
            int endOfHeaders;
            HttpHeaders parsedHeaders = HttpHelpers.ParseHttpHeaders(BinaryHelpers.EMPTY_BYTE_ARRAY, 0, 0, out endOfHeaders);
            Assert.IsNotNull(parsedHeaders);
            Assert.AreEqual(0, parsedHeaders.KeyCount);
        }

        [TestMethod]
        public void TestHttpParseHeadersNoHeaders()
        {
            int endOfHeaders;
            string stringData = "\r\n";
            byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
            HttpHeaders parsedHeaders = HttpHelpers.ParseHttpHeaders(encodedData, encodedData.Length, 0, out endOfHeaders);
            Assert.IsNotNull(parsedHeaders);
            Assert.AreEqual(0, parsedHeaders.KeyCount);
        }

        [TestMethod]
        public void TestHttpParseHeadersSmallestPossibleHeader()
        {
            int endOfHeaders;
            string stringData = "A:B\r\n\r\n";
            byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
            HttpHeaders parsedHeaders = HttpHelpers.ParseHttpHeaders(encodedData, encodedData.Length, 0, out endOfHeaders);
            Assert.IsNotNull(parsedHeaders);
            Assert.AreEqual(1, parsedHeaders.KeyCount);
            Assert.AreEqual("B", parsedHeaders["A"]);
        }

        [TestMethod]
        public void TestHttpParseHeadersWellFormed()
        {
            int endOfHeaders;
            string stringData = "Content-Length: 5\r\n" +
                    "Content-Type: text/plain\r\n" +
                    "Text-Encoding: UTF-8\r\n" +
                    "\r\n";
            byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
            HttpHeaders parsedHeaders = HttpHelpers.ParseHttpHeaders(encodedData, encodedData.Length, 0, out endOfHeaders);

            Assert.IsNotNull(parsedHeaders);
            Assert.AreEqual(3, parsedHeaders.KeyCount);
            Assert.AreEqual("5", parsedHeaders["Content-Length"]);
            Assert.AreEqual("text/plain", parsedHeaders["Content-Type"]);
            Assert.AreEqual("UTF-8", parsedHeaders["Text-Encoding"]);
        }

        [TestMethod]
        public void TestHttpParseHeaderWeirdWhitespace()
        {
            int endOfHeaders;
            string stringData = "Content-Length:5\r\n" +
                    "Content-Type:  text/plain \r\n" +
                    "Text-Encoding:     UTF-8     \t\r\n" +
                    "\r\n";
            byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
            HttpHeaders parsedHeaders = HttpHelpers.ParseHttpHeaders(encodedData, encodedData.Length, 0, out endOfHeaders);

            Assert.IsNotNull(parsedHeaders);
            Assert.AreEqual(3, parsedHeaders.KeyCount);
            Assert.AreEqual("5", parsedHeaders["Content-Length"]);
            Assert.AreEqual("text/plain", parsedHeaders["Content-Type"]);
            Assert.AreEqual("UTF-8", parsedHeaders["Text-Encoding"]);
        }

        [TestMethod]
        public void TestHttpParseHeaderMissingValue()
        {
            int endOfHeaders;
            string stringData = "Content-Length:\r\n\r\n";
            byte[] encodedData = Encoding.ASCII.GetBytes(stringData);
            HttpHeaders parsedHeaders = HttpHelpers.ParseHttpHeaders(encodedData, encodedData.Length, 0, out endOfHeaders);
            Assert.IsTrue(parsedHeaders.ContainsKey("Content-Length"));
            Assert.AreEqual(string.Empty, parsedHeaders["Content-Length"]);

            stringData = "Content-Length:       \r\n\r\n";
            encodedData = Encoding.ASCII.GetBytes(stringData);
            parsedHeaders = HttpHelpers.ParseHttpHeaders(encodedData, encodedData.Length, 0, out endOfHeaders);
            Assert.IsTrue(parsedHeaders.ContainsKey("Content-Length"));
            Assert.AreEqual(string.Empty, parsedHeaders["Content-Length"]);
        }

        [TestMethod]
        public void TestHttpParseHeadersUsingCache()
        {
            int endOfHeaders;
            IReadThroughCache<ByteArraySegment, string> stringCache = new MFUStringCache(Encoding.ASCII, 32);
            string stringData = "Content-Length: 5\r\n" +
                    "Content-Type: text/plain\r\n" +
                    "Authorization: Secrets\r\n" +
                    "\r\n";
            byte[] encodedData = Encoding.ASCII.GetBytes(stringData);

            for (int c = 0; c < 10; c++)
            {
                HttpHeaders parsedHeaders = HttpHelpers.ParseHttpHeaders(encodedData, encodedData.Length, 0, out endOfHeaders, stringCache);
                Assert.IsNotNull(parsedHeaders);
                Assert.AreEqual(3, parsedHeaders.KeyCount);
                Assert.AreEqual("5", parsedHeaders["Content-Length"]);
                Assert.AreEqual("text/plain", parsedHeaders["Content-Type"]);
                Assert.AreEqual("Secrets", parsedHeaders["Authorization"]);
            }
        }

        [TestMethod]
        public void TestHeadersContainsValueInvalidArgs()
        {
            HttpHeaders headers = new HttpHeaders();

            try
            {
                headers.ContainsValue(null, "chunked", StringComparison.Ordinal);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            try
            {
                headers.ContainsValue(string.Empty, "chunked", StringComparison.Ordinal);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            try
            {
                headers.ContainsValue("Transfer-Encoding", null, StringComparison.Ordinal);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            try
            {
                headers.ContainsValue("Transfer-Encoding", string.Empty, StringComparison.Ordinal);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestHeadersContainsValueEmptyHeaders()
        {
            HttpHeaders headers = new HttpHeaders();
            Assert.IsFalse(headers.ContainsValue("Transfer-Encoding", "chunked", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TestHeadersContainsValueSingleHeader()
        {
            HttpHeaders headers = new HttpHeaders();
            headers.Add("Transfer-Encoding", "chunked");
            Assert.IsTrue(headers.ContainsValue("Transfer-Encoding", "chunked", StringComparison.Ordinal));
            Assert.IsFalse(headers.ContainsValue("Transfer-Encoding", "something else", StringComparison.Ordinal));
            Assert.IsFalse(headers.ContainsValue("Set-Cookie", "something else", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TestHeadersContainsValueMultipleHeadersSingleValue()
        {
            HttpHeaders headers = new HttpHeaders();
            headers.Add("Set-Cookie", "cookieA");
            headers.Add("Set-Cookie", "cookieB");
            headers.Add("Set-Cookie", "cookieC");
            headers.Add("Set-Cookie", "cookieD");
            Assert.IsTrue(headers.ContainsValue("Set-Cookie", "cookieA", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Set-Cookie", "cookieB", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Set-Cookie", "cookieC", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Set-Cookie", "cookieD", StringComparison.Ordinal));
            Assert.IsFalse(headers.ContainsValue("Set-Cookie", "cookieE", StringComparison.Ordinal));
            Assert.IsFalse(headers.ContainsValue("Transfer-Encoding", "something else", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TestHeadersContainsValueSingleHeaderMultiValue()
        {
            HttpHeaders headers = new HttpHeaders();
            headers.Add("Set-Cookie", "cookieA,cookieB,cookieC,cookieD");
            Assert.IsTrue(headers.ContainsValue("Set-Cookie", "cookieA", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Set-Cookie", "cookieB", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Set-Cookie", "cookieC", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Set-Cookie", "cookieD", StringComparison.Ordinal));
            Assert.IsFalse(headers.ContainsValue("Set-Cookie", "cookieE", StringComparison.Ordinal));
            Assert.IsFalse(headers.ContainsValue("Transfer-Encoding", "something else", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TestHeadersContainsValueMultiHeaderMultiValue()
        {
            HttpHeaders headers = new HttpHeaders();
            headers.Add("Accept-Encoding", "gzip, compress,deflate, gzip;q=1.0,   *;q=0.5");
            headers.Add("Accept-Encoding", "br, identity");
            headers.Add("Accept-Encoding", "bzip2");
            Assert.IsTrue(headers.ContainsValue("Accept-Encoding", "gzip", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Accept-Encoding", "compress", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Accept-Encoding", "deflate", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Accept-Encoding", "gzip;q=1.0", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Accept-Encoding", "*;q=0.5", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Accept-Encoding", "br", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Accept-Encoding", "identity", StringComparison.Ordinal));
            Assert.IsTrue(headers.ContainsValue("Accept-Encoding", "bzip2", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TestHeadersEnumerateValueListInvalidArgs()
        {
            HttpHeaders headers = new HttpHeaders();

            try
            {
                headers.EnumerateValueList(null).ToList();
                Assert.Fail("Should have thrown an ArgumentNullException");
            }
            catch (ArgumentNullException) { }

            try
            {
                headers.EnumerateValueList(string.Empty).ToList();
                Assert.Fail("Should have thrown an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestHeadersEnumerateValueListEmptyHeaders()
        {
            HttpHeaders headers = new HttpHeaders();
            Assert.AreEqual(0, headers.EnumerateValueList("Transfer-Encoding").ToList().Count);
        }

        [TestMethod]
        public void TestHeadersEnumerateValueListSingleHeader()
        {
            HttpHeaders headers = new HttpHeaders();
            headers.Add("Transfer-Encoding", "chunked");
            IList<string> enumerated = headers.EnumerateValueList("Transfer-Encoding").ToList();
            Assert.AreEqual(1, enumerated.Count);
            Assert.AreEqual("chunked", enumerated[0]);
        }

        [TestMethod]
        public void TestHeadersEnumerateValueListMultipleHeadersSingleValue()
        {
            HttpHeaders headers = new HttpHeaders();
            headers.Add("Set-Cookie", "cookieA");
            headers.Add("Set-Cookie", "cookieB");
            headers.Add("Set-Cookie", "cookieC");
            headers.Add("Set-Cookie", "cookieD");
            IList<string> enumerated = headers.EnumerateValueList("Set-Cookie").ToList();
            Assert.AreEqual(4, enumerated.Count);
            Assert.AreEqual("cookieA", enumerated[0]);
            Assert.AreEqual("cookieB", enumerated[1]);
            Assert.AreEqual("cookieC", enumerated[2]);
            Assert.AreEqual("cookieD", enumerated[3]);
        }

        [TestMethod]
        public void TestHeadersEnumerateValueListSingleHeaderMultiValue()
        {
            HttpHeaders headers = new HttpHeaders();
            headers.Add("Set-Cookie", "cookieA,cookieB,cookieC,cookieD");
            IList<string> enumerated = headers.EnumerateValueList("Set-Cookie").ToList();
            Assert.AreEqual(4, enumerated.Count);
            Assert.AreEqual("cookieA", enumerated[0]);
            Assert.AreEqual("cookieB", enumerated[1]);
            Assert.AreEqual("cookieC", enumerated[2]);
            Assert.AreEqual("cookieD", enumerated[3]);
        }

        [TestMethod]
        public void TestHeadersEnumerateValueListMultiHeaderMultiValue()
        {
            HttpHeaders headers = new HttpHeaders();
            headers.Add("Accept-Encoding", "gzip, compress,deflate, gzip;q=1.0,   *;q=0.5");
            headers.Add("Accept-Encoding", "br, identity");
            headers.Add("Accept-Encoding", "bzip2");
            IList<string> enumerated = headers.EnumerateValueList("Accept-Encoding").ToList();
            Assert.AreEqual(8, enumerated.Count);
            Assert.AreEqual("gzip", enumerated[0]);
            Assert.AreEqual("compress", enumerated[1]);
            Assert.AreEqual("deflate", enumerated[2]);
            Assert.AreEqual("gzip;q=1.0", enumerated[3]);
            Assert.AreEqual("*;q=0.5", enumerated[4]);
            Assert.AreEqual("br", enumerated[5]);
            Assert.AreEqual("identity", enumerated[6]);
            Assert.AreEqual("bzip2", enumerated[7]);
        }
    }
}
