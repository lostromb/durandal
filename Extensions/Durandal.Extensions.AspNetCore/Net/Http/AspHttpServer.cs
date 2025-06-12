using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System.Threading;

using DurandalHttp = Durandal.Common.Net.Http;
using AspHttp = Microsoft.AspNetCore.Http;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Durandal HTTP server interface implemented as Asp.Net Core middleware
    /// </summary>
    public class AspHttpServer : DurandalHttp.IHttpServer
    {
        private DurandalHttp.IHttpServerDelegate _delegate;

        public AspHttpServer()
        {
        }

        public IEnumerable<ServerBindingInfo> Endpoints
        {
            get
            {
                return new ServerBindingInfo[] { new ServerBindingInfo(ServerBindingInfo.WILDCARD_HOSTNAME, 80) };
            }
        }

        public Uri LocalAccessUri
        {
            get
            {
                return new Uri("http://localhost");
            }
        }

        public bool Running
        {
            get
            {
                return true;
            }
        }

        public void Dispose()
        {
        }

        public void RegisterSubclass(DurandalHttp.IHttpServerDelegate subclass)
        {
            _delegate = subclass;
        }

        public Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult(true);
        }

        public Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return DurandalTaskExtensions.NoOpTask;
        }

        /// <summary>
        /// Terminal middleware component for use with <see cref="Microsoft.AspNetCore.Builder.RunExtensions.Run(Microsoft.AspNetCore.Builder.IApplicationBuilder, Microsoft.AspNetCore.Http.RequestDelegate)"/>.
        /// </summary>
        /// <param name="context">The incoming HTTP context.</param>
        /// <returns>An async task.</returns>
        public async Task Middleware(AspHttp.HttpContext context)
        {
            using (AspHttpServerContext durandalContext = new AspHttpServerContext(context, DefaultRealTimeProvider.Singleton))
            {
                await _delegate.HandleConnection(durandalContext, context.RequestAborted, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Terminal middleware component for use with <see cref="Microsoft.AspNetCore.Builder.UseExtensions.Use(Microsoft.AspNetCore.Builder.IApplicationBuilder, Func{AspHttp.HttpContext, Func{Task}, Task})"/>.
        /// The next middleware in the chain is actually ignored, so the semantics don't really line up.
        /// </summary>
        /// <param name="context">The incoming HTTP context.</param>
        /// <param name="nextMiddleware">The next middleware in the stack. Will be ignored.</param>
        /// <returns>An async task.</returns>
        [Obsolete("This is terminal middleware. Use IApplicationBuilder.Run instead")]
        public Task Middleware(AspHttp.HttpContext context, Func<Task> nextMiddleware)
        {
            return Middleware(context);
        }
    }
}
