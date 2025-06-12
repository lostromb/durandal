using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using DurandalHttp = Durandal.Common.Net.Http;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Durandal HTTP server implemented on the backend by a Kestrel web host
    /// </summary>
    public class KestrelHttpServer : DurandalHttp.IHttpServer
    {
        private readonly IList<ServerBindingInfo> _endpoints;
        private readonly Uri _localAccessUri;
        private readonly ILogger _logger;
        private DurandalHttp.IHttpServerDelegate _delegate;
        private IWebHost _host;
        private bool _running;
        private int _disposed = 0;

        public KestrelHttpServer(IEnumerable<ServerBindingInfo> endpoints, ILogger logger)
        {
            _endpoints = new List<ServerBindingInfo>(endpoints.AssertNonNull(nameof(endpoints)));
            _localAccessUri = HttpHelpers.FindBestLocalAccessUrl(endpoints);
            _logger = logger;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~KestrelHttpServer()
        {
            Dispose(false);
        }
#endif

        public IEnumerable<ServerBindingInfo> Endpoints
        {
            get
            {
                return _endpoints;
            }
        }

        public Uri LocalAccessUri
        {
            get
            {
                return _localAccessUri;
            }
        }

        public bool Running
        {
            get
            {
                return true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _host?.Dispose();
            }
        }

        public void RegisterSubclass(DurandalHttp.IHttpServerDelegate subclass)
        {
            _delegate = subclass;
        }

        public async Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_running)
            {
                return true;
            }

            _host = new WebHostBuilder()
                .UseKestrel((KestrelServerOptions options) =>
                {
                    foreach (ServerBindingInfo endpoint in _endpoints)
                    {
                        _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Binding Kestrel server to {0}", endpoint);
                        int actualPort = endpoint.LocalIpPort.GetValueOrDefault(endpoint.UseTls ? 443 : 80);
                        if (endpoint.IsWildcardEndpoint)
                        {
                            if (endpoint.UseTls)
                            {
                                options.ListenAnyIP(actualPort, (listenOptions) =>
                                {
                                    listenOptions.UseHttps(StoreName.My, endpoint.TlsCertificateIdentifier.SubjectName);
                                });
                            }
                            else
                            {
                                options.ListenAnyIP(actualPort);
                            }
                        }
                        else
                        {
                            IPEndPoint parsedEndpoint = IPEndPoint.Parse(endpoint.LocalIpEndpoint);
                            parsedEndpoint.Port = actualPort;

                            if (endpoint.UseTls)
                            {
                                options.Listen(parsedEndpoint, (listenOptions) =>
                                {
                                    listenOptions.UseHttps(StoreName.My, endpoint.TlsCertificateIdentifier.SubjectName);
                                });
                            }
                            else
                            {
                                options.Listen(parsedEndpoint);
                            }
                        }
                    }
                })
                .ConfigureServices((serviceCollection) =>
                {
                    serviceCollection.AddSingleton<DurandalHttp.IHttpServerDelegate>(_delegate);
                })
                .UseStartup<KestrelServerImplementation>()
                .Build();
            await _host.StartAsync().ConfigureAwait(false);
            _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Server is started and ready to receive requests at {0}", _localAccessUri);
            _running = true;
            return true;
        }

        public async Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.WaitForShutdown();
                _host.Dispose();
                _host = null;
            }

            _running = false;
        }

        private class KestrelServerImplementation
        {
            private DurandalHttp.IHttpServerDelegate _delegate;

            // Use this method to add services to the container.
            public void ConfigureServices(IServiceCollection services)
            {
                _delegate = services.BuildServiceProvider().GetService<DurandalHttp.IHttpServerDelegate>();
            }

            // Use this method to configure the HTTP request pipeline.
            public void Configure(IApplicationBuilder app)
            {
                app.Run(RequestDelegate);
            }

            private async Task RequestDelegate(HttpContext httpContext)
            {
                using (AspHttpServerContext durandalContext = new AspHttpServerContext(httpContext, DefaultRealTimeProvider.Singleton))
                {
                    await _delegate.HandleConnection(durandalContext, httpContext.RequestAborted, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                }
            }
        }
    }
}