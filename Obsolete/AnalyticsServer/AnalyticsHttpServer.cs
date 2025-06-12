using Durandal.Common.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Utils.Tasks;
using System.IO;
using DurandalServices.Instrumentation.Analytics.Html;
using DurandalServices.Instrumentation.Analytics.Charting;
using System.Drawing;
using Durandal.Common.Net.Http;

namespace DurandalServices.Instrumentation
{
    public class AnalyticsHttpServer : IHttpServerDelegate, IServer
    {
        private AnalyticsChartGenerator _htmlFactory;
        private ILogger _logger;
        private IHttpServer _baseServer;

        public AnalyticsHttpServer(AnalyticsChartGenerator htmlFactory, int port, ILogger logger, IThreadPool requestThreadPool = null)
        {
            _htmlFactory = htmlFactory;
            _logger = logger;
            _baseServer = new HttpSocketServer(new Win32SocketServer(new string[] { "http://*:" + port }, logger, requestThreadPool), logger);
            _baseServer.RegisterSubclass(this);
        }

        public async Task<HttpResponse> HandleConnection(HttpRequest request)
        {
            HttpResponse response = HttpResponse.ServerErrorResponse();

            HttpResponse dynamicResponse = await HandleDynamicPageRequests(request);
            if (dynamicResponse != null)
            {
                return dynamicResponse;
            }

            return HandleStaticPageRequests(request);
        }

        private async Task<HttpResponse> HandleDynamicPageRequests(HttpRequest request)
        {
            HttpResponse response = null;
            
            // Handle index page requests
            if (request.RequestFile.Equals("/"))
            {
                return HttpResponse.RedirectResponse(_htmlFactory.DefaultUrl);
            }

            // Handle dynamic pages if the URL matches
            string dynamicPage = await _htmlFactory.RenderRoute(request.RequestFile);
            if (!string.IsNullOrEmpty(dynamicPage))
            {
                string renderedPage = dynamicPage;
                response = HttpResponse.OKResponse();
                response.PayloadData = Encoding.UTF8.GetBytes(renderedPage);
                response.ResponseHeaders["Content-Type"] = "text/html";
            }

            return await Task.FromResult(response);
        }

        private HttpResponse HandleStaticPageRequests(HttpRequest request)
        {
            HttpResponse response = null;

            // Resolve the URL TODO: Security!!!!
            string resolvedURL = Environment.CurrentDirectory + "\\static" + request.RequestFile.Replace('/', '\\');
            // Determine the file type to use in the header
            string responseType = HttpHelpers.ResolveMimeType(resolvedURL);

            FileInfo targetFile = new FileInfo(resolvedURL);
            if (targetFile.Exists)
            {
                bool isCachedOnClient = false;
                DateTime cacheTime;

                // Does the client say they have it cached?
                if (request.RequestHeaders.ContainsKey("If-Modified-Since"))
                {
                    // Check against the file's modified time
                    if (DateTime.TryParse(request.RequestHeaders["If-Modified-Since"], out cacheTime))
                    {
                        isCachedOnClient = cacheTime > targetFile.LastWriteTime;
                    }
                }

                if (isCachedOnClient)
                {
                    response = HttpResponse.NotModifiedResponse();
                }
                else
                {
                    response = HttpResponse.OKResponse();
                    _logger.Log("Sending " + resolvedURL + " with content type " + responseType, LogLevel.Vrb);
                    response.ResponseHeaders["Content-Type"] = responseType;
                    response.ResponseHeaders["Cache-Control"] = "max-age=300"; // We assume that /view data is pretty much static, so tell the client to cache it more aggressively
                    response.PayloadData = File.ReadAllBytes(resolvedURL);
                }
            }
            else
            {
                _logger.Log("Client requested a nonexistent file " + request.RequestFile, LogLevel.Wrn);
                response = HttpResponse.NotFoundResponse();
            }

            return response;
        }

        public IEnumerable<string> Endpoints
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

        public bool StartServer(string serverName)
        {
            return _baseServer.StartServer(serverName);
        }

        public void StopServer()
        {
            _baseServer.StopServer();
        }

        public void Dispose()
        {
            _baseServer.Dispose();
        }
    }
}
