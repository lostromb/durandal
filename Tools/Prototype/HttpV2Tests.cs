using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2;
using Durandal.Common.Security;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Prototype
{
    public static class HttpV2Tests
    {
        public static async Task TestListenerServer()
        {
            ILogger logger = new ConsoleLogger("Main");
            using (IThreadPool listenerThreadPool = new TaskThreadPool(NullMetricCollector.WeakSingleton, DimensionSet.Empty, "ServerThreadPool"))
            using (ListenerHttpServer listenerServer = new ListenerHttpServer(
                new ServerBindingInfo[]
                {
                    ServerBindingInfo.WildcardHost(62291)
                },
                logger.Clone("HttpListenerServer"),
                new WeakPointer<IThreadPool>(listenerThreadPool)))
            using (ISocketFactory socketFactory = new RawTcpSocketFactory(logger.Clone("SocketFactory")))
            {
                IHttpServerDelegate myImplementation = new MyServerImplementation(listenerServer, logger);
                await listenerServer.StartServer("TestServer", CancellationToken.None, DefaultRealTimeProvider.Singleton);

                //IHttpClient client = new PortableHttpClient("localhost", 62291, logger.Clone("Client"));
                IHttpClient client = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new Uri("http://localhost:62291"),
                    logger.Clone("Client"),
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    Http2SessionManager.Default,
                    new Http2SessionPreferences());
                while (listenerServer.Running)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                    using (HttpRequest req = HttpRequest.CreateOutgoing("/"))
                    using (HttpResponse resp = await client.SendRequestAsync(req).ConfigureAwait(false))
                    {
                        string responseString = await resp.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        logger.Log("Got response: \"" + responseString + "\"");
                        await resp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }
        }

        public static async Task TestSocketServer()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            using (IThreadPool socketThreadPool = new TaskThreadPool(NullMetricCollector.WeakSingleton, DimensionSet.Empty, "ServerThreadPool"))
            using (ISocketServer rawSockets = new RawTcpSocketServer(
                new ServerBindingInfo[] { new ServerBindingInfo(ServerBindingInfo.WILDCARD_HOSTNAME, 62291, CertificateIdentifier.BySubjectName("durandal-ai.net")) },
                logger,
                DefaultRealTimeProvider.Singleton,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                new WeakPointer<IThreadPool>(socketThreadPool)))
            using (SocketHttpServer socketServer = new SocketHttpServer(rawSockets, logger.Clone("SocketHttpServer"), new CryptographicRandom(), NullMetricCollector.WeakSingleton, DimensionSet.Empty))
            using (ISocketFactory socketFactory = new PooledTcpClientSocketFactory(logger.Clone("SocketFactory"), NullMetricCollector.Singleton, DimensionSet.Empty))
            //using (ISocketFactory socketFactory = new TcpClientSocketFactory(logger.Clone("SocketFactory")))
            {
                IHttpServerDelegate myImplementation = new MyServerImplementation(socketServer, logger);
                await socketServer.StartServer("TestServer", CancellationToken.None, DefaultRealTimeProvider.Singleton);

                //IHttpClient client = new PortableHttpClient("localhost", 62291, logger.Clone("Client"));
                IHttpClient client = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new TcpConnectionConfiguration("localhost", 62291, true)
                    {
                        SslHostname = "durandal-ai.net",
                    },
                    logger.Clone("Client"),
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    Http2SessionManager.Default,
                    new Http2SessionPreferences());

                IRandom rand = new FastRandom();
                List<Task> allTasks = new List<Task>();
                while (socketServer.Running)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);

                    int numThreads = rand.NextInt(1, 8);
                    for (int c = 0; c < numThreads; c++)
                    {
                        allTasks.Add(Task.Run(async () =>
                        {
                            using (HttpRequest req = HttpRequest.CreateOutgoing("/"))
                            using (HttpResponse resp = await client.SendRequestAsync(req).ConfigureAwait(false))
                            {
                                string responseString = await resp.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                                logger.Log("Got response: \"" + responseString + "\"");
                                await resp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                        }));
                    }

                    foreach (Task t in allTasks)
                    {
                        await t.ConfigureAwait(false);
                    }
                }
            }
        }

        public class MyServerImplementation : IHttpServerDelegate
        {
            private readonly ILogger _logger;

            public MyServerImplementation(IHttpServer baseServer, ILogger logger)
            {
                baseServer.RegisterSubclass(this);
                _logger = logger.AssertNonNull(nameof(logger));
            }

            public async Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                //_logger.Log("Got an HTTP request " + context.HttpRequest.DecodedRequestFile);
                //if (string.Equals(context.HttpRequest.DecodedRequestFile, "/"))
                {
                    HttpResponse myResponse = HttpResponse.OKResponse();
                    myResponse.ResponseHeaders.Add("X-Shoutouts-To", "fairlight");
                    //FastRandom rand = new FastRandom();
                    //using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                    //{
                    //    pooledSb.Builder.Append("|-");
                    //    pooledSb.Builder.Append(' ', rand.NextInt(0, 3000));
                    //    pooledSb.Builder.Append("-|");
                    //    myResponse.SetContent(pooledSb.Builder.ToString());
                    //}

                    await Task.Delay(200).ConfigureAwait(false);

                    myResponse.SetContent("Doctor Grant, the phones are working");
                    await context.WritePrimaryResponse(myResponse, _logger.Clone("MyHttpResponse"), cancelToken, realTime).ConfigureAwait(false);
                }
                //else
                //{
                //    await context.WritePrimaryResponse(HttpResponse.NotFoundResponse(), cancelToken, realTime, _logger.Clone("MyHttpResponse")).ConfigureAwait(false);
                //}
            }
        }
    }
}
