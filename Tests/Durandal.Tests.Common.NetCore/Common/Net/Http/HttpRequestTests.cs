using Durandal.Common.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Net.Http
{
    [TestClass]
    public class HttpRequestTests
    {
        [TestMethod]
        public void TestHttpCreateRequestBasic()
        {
            HttpRequest req = HttpRequest.CreateOutgoing("/pictures", "POST");
            Assert.AreEqual("POST", req.RequestMethod);
            Assert.AreEqual("/pictures", req.RequestFile);
            Assert.IsNotNull(req.GetParameters);
        }

        [TestMethod]
        public void TestHttpCreateRequestWithGetParameters()
        {
            HttpRequest req = HttpRequest.CreateOutgoing("/pictures?foo=bar&two=2", "POST");
            Assert.AreEqual("POST", req.RequestMethod);
            Assert.AreEqual("/pictures", req.RequestFile);
            Assert.AreEqual(2, req.GetParameters.KeyCount);
            Assert.AreEqual("bar", req.GetParameters["foo"]);
            Assert.AreEqual("2", req.GetParameters["two"]);
        }

        [TestMethod]
        public void TestHttpCreateRequestWithSpaces()
        {
            HttpRequest req = HttpRequest.CreateOutgoing("/filename+with+spaces.wav");
            Assert.AreEqual("GET", req.RequestMethod);
            Assert.AreEqual("/filename+with+spaces.wav", req.RequestFile);
            Assert.AreEqual("/filename with spaces.wav", req.DecodedRequestFile);
        }

        [TestMethod]
        public void TestHttpCreateRequestWithCompoundPath()
        {
            HttpRequest req = HttpRequest.CreateOutgoing("/this/is/a/long/path", "POST");
            Assert.AreEqual("POST", req.RequestMethod);
            Assert.AreEqual("/this/is/a/long/path", req.RequestFile);
            Assert.AreEqual("/this/is/a/long/path", req.DecodedRequestFile);
        }

        [TestMethod]
        public void TestHttpCreateRequestWithComplexGetParams()
        {
            HttpRequest req = HttpRequest.CreateOutgoing("/ck/a?!&&p=113ff0aa69c6&ptn=3&hsh=3&fclid=2e68e4f4-df3f-6ff9-0d58-f5e4de8c6ee4&u=a1aHRZXM&ntb=1", "GET");
            Assert.AreEqual("GET", req.RequestMethod);
            Assert.AreEqual("/ck/a", req.RequestFile);
            Assert.AreEqual("/ck/a", req.DecodedRequestFile);
            Assert.AreEqual(7, req.GetParameters.KeyCount);
            Assert.IsTrue(req.GetParameters.ContainsKey("!"));
            Assert.AreEqual(string.Empty, req.GetParameters["!"]);
            Assert.IsTrue(req.GetParameters.ContainsKey("p"));
            Assert.AreEqual("113ff0aa69c6", req.GetParameters["p"]);
            Assert.IsTrue(req.GetParameters.ContainsKey("ptn"));
            Assert.AreEqual("3", req.GetParameters["ptn"]);
            Assert.IsTrue(req.GetParameters.ContainsKey("hsh"));
            Assert.AreEqual("3", req.GetParameters["hsh"]);
            Assert.IsTrue(req.GetParameters.ContainsKey("fclid"));
            Assert.AreEqual("2e68e4f4-df3f-6ff9-0d58-f5e4de8c6ee4", req.GetParameters["fclid"]);
            Assert.IsTrue(req.GetParameters.ContainsKey("u"));
            Assert.AreEqual("a1aHRZXM", req.GetParameters["u"]);
            Assert.IsTrue(req.GetParameters.ContainsKey("ntb"));
            Assert.AreEqual("1", req.GetParameters["ntb"]);
        }

        [TestMethod]
        public void TestHttpCreateRequestWithComplexGetParams2()
        {
            string requestPath = "/api/config.json?key=9UX54-L57TE-34QD8-DURP5-GFD2R&d=www.c.org&t=5708371&v=1.720.0&if=&sl=0&si=75bd2ffc-c7ab-49f5-bea7-ba7d4c3286be-sbl28f&plugins=AK,ConfigOverride,Continuity,PageParams,IFrameDelay,AutoXHR,SPA,History,Angular,Backbone,Ember,RT,CrossDomain,BW,PaintTiming,NavigationTiming,ResourceTiming,Memory,CACHE_RELOAD,Errors,TPAnalytics,UserTiming,Akamai,Early,EventTiming,LOGN&acao=&ak.ai=523468";
            HttpRequest req = HttpRequest.CreateOutgoing(requestPath, "GET");
            Assert.AreEqual("GET", req.RequestMethod);
            Assert.AreEqual("/api/config.json", req.RequestFile);
            Assert.AreEqual("/api/config.json", req.DecodedRequestFile);
        }

        [TestMethod]
        public void TestHttpCreateRequestFromFullyQualifiedUrlString()
        {
            HttpRequest req = HttpRequest.CreateOutgoing("https://www.durandal.com/welcome.html");
            Assert.AreEqual("GET", req.RequestMethod);
            Assert.AreEqual("/welcome.html", req.RequestFile);
            Assert.AreEqual("/welcome.html", req.DecodedRequestFile);
        }

        [TestMethod]
        public void TestHttpCreateRequestFromFullyQualifiedUrlStringTrailingSlash()
        {
            HttpRequest req = HttpRequest.CreateOutgoing("https://www.durandal.com/");
            Assert.AreEqual("GET", req.RequestMethod);
            Assert.AreEqual("/", req.RequestFile);
            Assert.AreEqual("/", req.DecodedRequestFile);
        }

        [TestMethod]
        public void TestHttpCreateRequestFromFullyQualifiedUrlStringDefaultPath()
        {
            HttpRequest req = HttpRequest.CreateOutgoing("https://www.durandal.com");
            Assert.AreEqual("GET", req.RequestMethod);
            Assert.AreEqual("/", req.RequestFile);
            Assert.AreEqual("/", req.DecodedRequestFile);
        }
    }
}
