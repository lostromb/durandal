using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Remoting;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.Speech.SR;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting.Handlers
{
    /// <summary>
    /// Implements a handler that handles remote metrics on the server (host) side
    /// </summary>
    public class MetricRemoteProcedureRequestHandler : IRemoteProcedureRequestHandler
    {
        private readonly WeakPointer<IMetricCollector> _targetMetrics;
        private readonly Dictionary<string, DimensionSet> _cachedDimensions = new Dictionary<string, DimensionSet>();

        public MetricRemoteProcedureRequestHandler(IMetricCollector targetMetrics)
        {
            _targetMetrics = new WeakPointer<IMetricCollector>(targetMetrics);
        }

        public bool CanHandleRequestType(Type requestType)
        {
            return requestType == typeof(RemoteUploadMetricsRequest);
        }

        public Task HandleRequest(
            PostOffice postOffice,
            IRemoteDialogProtocol remoteProtocol,
            ILogger traceLogger,
            Tuple<object, Type> parsedMessage,
            MailboxMessage originalMessage,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            TaskFactory taskFactory)
        {
            if (parsedMessage.Item2 == typeof(RemoteUploadMetricsRequest))
            {
                RemoteUploadMetricsRequest parsedInterstitialRequest = parsedMessage.Item1 as RemoteUploadMetricsRequest;
                RemoteProcedureResponse<bool> finalResponse;

                try
                {
                    if (parsedInterstitialRequest.Metrics != null)
                    {
                        lock (_cachedDimensions)
                        {
                            foreach (var metricEvent in parsedInterstitialRequest.Metrics.Events)
                            {
                                CounterType type = (CounterType)metricEvent.MetricType;
                                DimensionSet dimensions;
                                if (!_cachedDimensions.TryGetValue(metricEvent.SerializedDimensions, out dimensions))
                                {
                                    dimensions = DimensionSet.Parse(metricEvent.SerializedDimensions);
                                    _cachedDimensions[metricEvent.SerializedDimensions] = dimensions;
                                }

                                if (type == CounterType.Instant)
                                {
                                    int increment = BinaryHelpers.ByteArrayToInt32LittleEndian(metricEvent.SerializedValues.Array, metricEvent.SerializedValues.Offset);
                                    _targetMetrics.Value.ReportInstant(metricEvent.CounterName, dimensions, increment);
                                }
                                else if (type == CounterType.Continuous)
                                {
                                    double value = BinaryHelpers.ByteArrayToDoubleLittleEndian(metricEvent.SerializedValues.Array, metricEvent.SerializedValues.Offset);
                                    _targetMetrics.Value.ReportContinuous(metricEvent.CounterName, dimensions, value);
                                }
                                else if (type == CounterType.Percentile)
                                {
                                    for (int idx = 0; idx < metricEvent.SerializedValues.Count; idx += 8)
                                    {
                                        double value = BinaryHelpers.ByteArrayToDoubleLittleEndian(metricEvent.SerializedValues.Array, metricEvent.SerializedValues.Offset + idx);
                                        _targetMetrics.Value.ReportPercentile(metricEvent.CounterName, dimensions, value);
                                    }
                                }
                            }
                        }
                    }

                    finalResponse = new RemoteProcedureResponse<bool>(parsedInterstitialRequest.MethodName, true);
                }
                catch (Exception e)
                {
                    traceLogger.Log(e, LogLevel.Err);
                    finalResponse = new RemoteProcedureResponse<bool>(parsedInterstitialRequest.MethodName, e);
                }

                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedMetrics");

                // Send back an ACK that metrics were received
                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(finalResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }

            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
