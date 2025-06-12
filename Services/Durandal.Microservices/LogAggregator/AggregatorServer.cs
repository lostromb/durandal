using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Net;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Instrumentation;
using Durandal.Common.Tasks;
using Durandal.Common.Net.Http;
using Durandal.Common.File;
using Durandal.API;
using Durandal.Extensions.BondProtocol;
using Durandal.Extensions.MySql;
using Durandal.Common.IO;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Security;

namespace DurandalServices.LogAggregator
{
    public class AggregatorServer : IHttpServerDelegate, IServer
    {
        private readonly ILogger _coreLogger;
        private readonly MySqlLogger _prodLogTarget;
        private readonly MySqlLogger _devLogTarget;
        private readonly MySqlConnectionPool _prodConnectionPool;
        private readonly MySqlConnectionPool _devConnectionPool;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly IHttpServer _baseServer;
        private readonly IByteConverter<InstrumentationEventList> _bondSerializer;
        private readonly IByteConverter<InstrumentationEventList> _binarySerializer;
        private int _disposed = 0;

        public AggregatorServer(
            int port,
            ILogger logger,
            string prodConnectionString,
            string devConnectionString,
            bool useNativeConnectionPool,
            IRealTimeProvider realTime,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet dimensions,
            WeakPointer<IThreadPool> requestThreadPool)
        {
            _coreLogger = logger;
            _metrics = metrics;
            IMetricCollector nullMetrics = NullMetricCollector.Singleton;
            _prodConnectionPool = MySqlConnectionPool.Create(prodConnectionString, _coreLogger.Clone("MySqlConnectionPool-Prod"), metrics.Value, dimensions, "Aggregator-Prod", useNativeConnectionPool).Await();
            _devConnectionPool = MySqlConnectionPool.Create(devConnectionString, _coreLogger.Clone("MySqlConnectionPool-Dev"), metrics.Value, dimensions, "Aggregator-Dev", useNativeConnectionPool).Await();
            _prodLogTarget = new MySqlLogger(
                connectionPool: _prodConnectionPool,
                metrics: nullMetrics,
                bootstrapLogger: _coreLogger,
                validLogLevels: LogLevel.All,
                maxLogLevels: LogLevel.All);
            _devLogTarget = new MySqlLogger(
                connectionPool: _devConnectionPool,
                metrics: nullMetrics,
                bootstrapLogger: _coreLogger,
                validLogLevels: LogLevel.All,
                maxLogLevels: LogLevel.All);
            _baseServer = new SocketHttpServer(
                new RawTcpSocketServer(
                    new ServerBindingInfo[] { new ServerBindingInfo(ServerBindingInfo.WILDCARD_HOSTNAME, port) },
                    logger,
                    realTime,
                    metrics,
                    dimensions,
                    requestThreadPool),
                logger,
                new CryptographicRandom(),
                metrics,
                dimensions);
            _baseServer.RegisterSubclass(this);
            _bondSerializer = new BondByteConverterInstrumentationEventList();
            _binarySerializer = new InstrumentationBlobSerializer();
            _dimensions = dimensions;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AggregatorServer()
        {
            Dispose(false);
        }
#endif

        public IEnumerable<ServerBindingInfo> Endpoints
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

        public async Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await _devLogTarget.Initialize();
            await _prodLogTarget.Initialize();
            return await _baseServer.StartServer(serverName, cancelToken, realTime);
        }

        public Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _baseServer.StopServer(cancelToken, realTime);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!Durandal.Common.Utils.AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _baseServer.Dispose();
            }
        }

        public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpResponse resp = await HandleConnectionInternal(serverContext.HttpRequest, cancelToken, realTime).ConfigureAwait(false);
            if (resp != null)
            {
                try
                {
                    await serverContext.WritePrimaryResponse(resp, _coreLogger, cancelToken, realTime).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _coreLogger.Log(e, LogLevel.Err);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This method returns an IDisposable so the caller should be responsible for disposal")]
        private async Task<HttpResponse> HandleConnectionInternal(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (request.RequestFile.Equals("/log"))
            {
                ArraySegment<byte> payloadData = await request.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                if (payloadData.Array == null || payloadData.Count == 0)
                {
                    return HttpResponse.BadRequestResponse();
                }

                string streamName = "Prod";
                if (request.GetParameters != null && request.GetParameters.ContainsKey("stream"))
                {
                    streamName = request.GetParameters["stream"];
                }

                DimensionSet dimensions = _dimensions.Combine(new MetricDimension("stream", streamName));
                _metrics.Value.ReportInstant("Remote Log Requests / sec", dimensions, 1);

                // Check the format of the incoming data. Assume bond, but also allow "binary"
                IByteConverter <InstrumentationEventList> serializer = _bondSerializer;
                if (request.GetParameters.ContainsKey("format") && string.Equals(RemoteInstrumentationLogger.ENCODING_SCHEME_BINARY, request.GetParameters["format"]))
                {
                    serializer = _binarySerializer;
                }

                // Decompress the event list
                InstrumentationBlob data = InstrumentationBlob.Decompress(payloadData, serializer);

                _coreLogger.Log("Received aggregate " + data.Count + " events for stream " + streamName + " from client " + request.RemoteHost, LogLevel.Std);
                _metrics.Value.ReportInstant("Remote Log Messages / sec", dimensions, data.Count);

                // Echo the events to the SQL db
                ILogger logTarget = _prodLogTarget;
                if (string.Equals(streamName, "Dev", StringComparison.OrdinalIgnoreCase))
                {
                    logTarget = _devLogTarget;
                }

                foreach (var log in data.GetEvents())
                {
                    logTarget.Log(log);
                }

                return HttpResponse.OKResponse();
            }

            return await Task.FromResult(HttpResponse.NotFoundResponse());
        }
    }
}
