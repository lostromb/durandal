using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Monitoring;
using Durandal.Common.Net.Http;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.MonitorConsole.Controllers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace Durandal.MonitorConsole
{
    /// <summary>
    /// HTTP endpoint for viewing monitoring status
    /// </summary>
    public class MonitorHttpServer : IHttpServerDelegate
    {
        private readonly List<AspNetStyleRouteParser> _routes;
        private readonly IFileSystem _contentFileSystem;
        private readonly ILogger _logger;
        
        public MonitorHttpServer(string siteBase, ITestResultStore testResultStore, ILogger logger)
        {
            _logger = logger;
            DashboardController dashboard = new DashboardController(testResultStore);
            _routes = new List<AspNetStyleRouteParser>();
            _routes.Add(new AspNetStyleRouteParser("GET", "^" + siteBase + "/content/(?<fileName>.+?)$",
                (time, match, req) => SendStaticContent(match.Groups["fileName"].Value, req)));
            _routes.Add(new AspNetStyleRouteParser("GET", "^" + siteBase + "/dashboard$",
                (time, match, req) => dashboard.GetAllTestResult(time)));
            _routes.Add(new AspNetStyleRouteParser("GET", "^" + siteBase + "/dashboard/suite/(?<suiteName>[^//]+?)$",
                (time, match, req) => dashboard.GetSuiteResult(time, match.Groups["suiteName"].Value)));
            _routes.Add(new AspNetStyleRouteParser("GET", "^" + siteBase + "/dashboard/test/(?<testName>[^//]+?)$",
                (time, match, req) => dashboard.GetSingleTestResult(time, match.Groups["testName"].Value)));
            _contentFileSystem = new RealFileSystem(_logger.Clone("ContentFileSystem"), "./content");
        }

        public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpResponse resp = await HandleConnectionInternal(serverContext.HttpRequest, cancelToken, realTime).ConfigureAwait(false);
            if (resp != null)
            {
                try
                {
                    await serverContext.WritePrimaryResponse(resp, _logger, cancelToken, realTime).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This method returns an IDisposable so the caller should be responsible for disposal")]
        private async Task<HttpResponse> HandleConnectionInternal(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            foreach (AspNetStyleRouteParser parser in _routes)
            {
                Task<HttpResponse> resp = parser.TryDispatch(request, _logger, realTime);
                if (resp != null)
                {
                    return await resp;
                }
            }
            
            return HttpResponse.NotFoundResponse();
        }

        private async Task<HttpResponse> SendStaticContent(string fileName, HttpRequest request)
        {
            VirtualPath localFile = new VirtualPath(fileName);
            DateTimeOffset? ifModifiedSince = GetClientIfModifiedSinceHeader(request);
            HttpResponse response;
            string responseType = HttpHelpers.ResolveMimeType(localFile.Name);
            if (_contentFileSystem.Exists(localFile))
            {
                // Does the client say they have it cached?
                FileStat localFileStat = await _contentFileSystem.StatAsync(localFile);
                bool isCachedOnClient = localFileStat != null && ifModifiedSince.HasValue && ifModifiedSince.Value > localFileStat.LastWriteTime;

                if (isCachedOnClient)
                {
                    response = HttpResponse.NotModifiedResponse();
                }
                else
                {
                    response = HttpResponse.OKResponse();
                    _logger.Log("Sending " + localFile + " with content type " + responseType, LogLevel.Vrb);
                    response.ResponseHeaders["Cache-Control"] = "max-age=300";
                   response.SetContent(await _contentFileSystem.OpenStreamAsync(localFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false), responseType);
                }
            }
            else
            {
                _logger.Log("Client requested a nonexistent file " + localFile.FullName, LogLevel.Wrn);
                response = HttpResponse.NotFoundResponse();
            }

            return response;
        }

        private DateTimeOffset? GetClientIfModifiedSinceHeader(HttpRequest clientRequest)
        {
            DateTimeOffset cacheTime;
            if (clientRequest.RequestHeaders.ContainsKey("If-Modified-Since"))
            {
                // Check against the file's modified time
                if (DateTimeOffset.TryParse(clientRequest.RequestHeaders["If-Modified-Since"], out cacheTime))
                {
                    return cacheTime;
                }
            }

            return null;
        }

        private class AspNetStyleRouteParser
        {
            private readonly Func<IRealTimeProvider, Match, HttpRequest, Task<HttpResponse>> _delegate;
            private readonly string _httpVerb;
            private readonly string _originalRoute;
            private readonly Regex _regex;

            public AspNetStyleRouteParser(string verb, string route, Func<IRealTimeProvider, Match, HttpRequest, Task<HttpResponse>> impl)
            {
                _httpVerb = verb;
                _originalRoute = route;
                _regex = new Regex(route, RegexOptions.IgnoreCase);
                _delegate = impl;
            }

            public Task<HttpResponse> TryDispatch(HttpRequest request, ILogger logger, IRealTimeProvider realTime)
            {
                if (!string.Equals(_httpVerb, request.RequestMethod, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                Match m = _regex.Match(request.DecodedRequestFile);
                if (m.Success)
                {
                    logger.Log(request.DecodedRequestFile + " matched route " + _originalRoute);
                    return _delegate(realTime, m, request);
                }

                return null;
            }
        }
    }
}
