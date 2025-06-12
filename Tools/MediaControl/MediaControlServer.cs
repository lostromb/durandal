using Durandal.Common.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using Newtonsoft.Json;
using Durandal.MediaProtocol;
using Durandal.Common.Time;
using Durandal.Common.Instrumentation;
using System.Threading;

namespace MediaControl
{
    public class MediaControlServer : IHttpServerDelegate, IDisposable
    {
        private ISocketServer _socketServerImpl;
        private IHttpServer _serverImpl;
        private WinampController _winamp;
        private IThreadPool _serverThreadPool;

        public MediaControlServer(int port, ILogger logger, WinampController winamp)
        {
            _serverThreadPool = new CustomThreadPool(logger.Clone("HttpThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "HTTP", 4);
            _socketServerImpl = new Win32SocketServer(new string[] { "http://*:" + port }, logger, DefaultRealTimeProvider.Singleton, NullMetricCollector.Singleton, DimensionSet.Empty, _serverThreadPool);
            _serverImpl = new HttpSocketServer(_socketServerImpl, logger);
            _serverImpl.RegisterSubclass(this);

            _winamp = winamp;
        }
        
        public Uri LocalAccessUri
        {
            get
            {
                return _serverImpl.LocalAccessUri;
            }
        }

        public IEnumerable<string> Endpoints
        {
            get
            {
                return _serverImpl.Endpoints;
            }
        }

        public bool Running
        {
            get
            {
                return _serverImpl.Running;
            }
        }
        
        public Task<bool> StartServer(string serverName)
        {
            return _serverImpl.StartServer(serverName);
        }

        public void StopServer()
        {
            _serverImpl.StopServer();
        }

        public void Dispose()
        {
            _serverImpl.Dispose();
            _socketServerImpl.Dispose();
        }

        public async Task<HttpResponse> HandleConnection(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (!string.Equals("POST", request.RequestMethod))
            {
                return HttpResponse.MethodNotAllowedResponse();
            }
            
            if (string.Equals(request.DecodedRequestFile, "/audio", StringComparison.OrdinalIgnoreCase))
            {
                string payload = request.GetPayloadAsString(Encoding.UTF8);
                MediaControlRequest mediaRequest = JsonConvert.DeserializeObject<MediaControlRequest>(payload);
                MediaControlResponse mediaResponse = await _winamp.Process(mediaRequest);
                string responseJson = JsonConvert.SerializeObject(mediaResponse);
                HttpResponse response = HttpResponse.OKResponse();
                response.WriteBinaryPayload(Encoding.UTF8.GetBytes(responseJson));
                return response;
            }
            else if (string.Equals(request.DecodedRequestFile, "/video", StringComparison.OrdinalIgnoreCase))
            {
                return HttpResponse.ServerErrorResponse();
            }

            return HttpResponse.NotFoundResponse();
        }
    }
}
