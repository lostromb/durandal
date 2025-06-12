using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Net;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Net.Http;
using Durandal.Common.File;
using System.Threading;
using Durandal.Common.Time;
using System.Diagnostics;
using Durandal.API;

namespace Durandal.Common.Speech.Triggers
{
    public class TriggerArbitratorServer : IHttpServerDelegate
    {
        private const int MAX_PARTITIONS = 1000;
        private readonly int _numPartitions;

        private readonly IRealTimeProvider _realTime;
        private readonly TimeSpan _rendevousTime;
        private readonly Dictionary<string, DateTimeOffset>[] _partitionedTriggerTimes;
        private readonly object[] _locks;
        private readonly ILogger _coreLogger;
        private readonly IHttpServer _baseServer;

        public TriggerArbitratorServer(IHttpServer baseServer, ILogger logger, TimeSpan rendevousTime, IRealTimeProvider realTime, int partitions = 1)
        {
            _coreLogger = logger;
            _baseServer = baseServer;
            _baseServer.RegisterSubclass(this);
            _numPartitions = Math.Max(1, Math.Min(MAX_PARTITIONS, partitions));
            _rendevousTime = rendevousTime;
            _realTime = realTime;

            _partitionedTriggerTimes = new Dictionary<string, DateTimeOffset>[_numPartitions];
            _locks = new object[_numPartitions];
            for (int c = 0; c < _numPartitions; c++)
            {
                _partitionedTriggerTimes[c] = new Dictionary<string, DateTimeOffset>();
                _locks[c] = new object();
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
            if (request.RequestFile.Equals("/arbitrate"))
            {
                if (!request.GetParameters.ContainsKey("group"))
                {
                    await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                    return HttpResponse.BadRequestResponse("Missing \"group\" parameter of URL");
                }

                string group = request.GetParameters["group"].ToLowerInvariant();
                int partitionNum = Math.Abs(group.GetHashCode()) % _numPartitions;
                Dictionary<string, DateTimeOffset> triggerTimes = _partitionedTriggerTimes[partitionNum];
                object mutex = _locks[partitionNum];
                DateTimeOffset now = _realTime.Time;

                // Lock the partition targeted by this group
                Monitor.Enter(mutex);
                try
                {
                    // Has this group been triggered before?
                    if (!triggerTimes.ContainsKey(group))
                    {
                        triggerTimes.Add(group, now);
                        _coreLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Triggering \"{0}\" at {1:yyyy-MM-ddTHH:mm:ss.fffff}", group, HighPrecisionTimer.GetCurrentUTCTime());
                        return HttpResponse.OKResponse();
                    }
                    else
                    {
                        // Check to see if the last trigger time was within the last few seconds. If so, reject.
                        DateTimeOffset lastTrigger = triggerTimes[group];
                        if ((now - lastTrigger) < _rendevousTime)
                        {
                            return HttpResponse.TooManyRequestsResponse();
                        }
                        else
                        {
                            triggerTimes[group] = now;
                            _coreLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Triggering \"{0}\" at {1:yyyy-MM-ddTHH:mm:ss.fffff}", group, HighPrecisionTimer.GetCurrentUTCTime());
                            return HttpResponse.OKResponse();
                        }
                    }
                }
                catch (Exception e)
                {
                    _coreLogger.Log(e, LogLevel.Err);
                    return HttpResponse.ServerErrorResponse(e);
                }
                finally
                {
                    Monitor.Exit(mutex);
                }
            }

            return HttpResponse.NotFoundResponse();
        }
    }
}
