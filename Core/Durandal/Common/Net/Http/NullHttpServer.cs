using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    public class NullHttpServer : IHttpServer, IHttpServerDelegate
    {
        private static readonly ServerBindingInfo[] ENDPOINTS = new ServerBindingInfo[0];
        private IHttpServerDelegate _subclass;

        public IEnumerable<ServerBindingInfo> Endpoints => ENDPOINTS;

        public bool Running
        {
            get; private set;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_subclass == null)
            {
                return context.WritePrimaryResponse(HttpResponse.NotFoundResponse(), NullLogger.Singleton, cancelToken, realTime);
            }
            else
            {
                return _subclass.HandleConnection(context, cancelToken, realTime);
            }
        }

        public void RegisterSubclass(IHttpServerDelegate subclass)
        {
            _subclass = subclass;
        }

        public Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            Running = true;
            return Task.FromResult(Running);
        }

        public Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            Running = false;
            return DurandalTaskExtensions.NoOpTask;
        }

        public Uri LocalAccessUri
        {
            get
            {
                return new Uri("http://null");
            }
        }
    }
}
