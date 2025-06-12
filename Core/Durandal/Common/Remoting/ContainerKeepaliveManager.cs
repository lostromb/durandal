namespace Durandal.Common.Remoting
{
    using Durandal.API;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Remoting.Protocol;
    using Durandal.Common.Collections;
    using Durandal.Common.IO;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Utils;
    using Durandal.Common.MathExt;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Events;
    using Durandal.Common.Instrumentation.Profiling;
    using Durandal.Common.Config.Accessors;
    using Durandal.Common.ServiceMgmt;

    public class ContainerKeepaliveManager : IDisposable
    {
        private readonly WeakPointer<PostOffice> _postOffice;
        private readonly ILogger _serviceLogger;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly IRemoteDialogProtocol _remotingProtocol;
        private readonly CancellationTokenSource _cancelToken;
        private readonly MovingAverage _qualityOfService;
        private readonly IConfigValue<TimeSpan> _keepAliveInterval;
        private readonly IConfigValue<TimeSpan> _keepAliveTimeout;
        private readonly IConfigValue<float> _failureThreshold;
        private bool _isBelowThreshold = false;
        private Task _backgroundThread = null;
        private int _disposed = 0;

        /// <summary>
        /// Event that fires if the quality of service goes below the threshold.
        /// </summary>
        public AsyncEvent<EventArgs> HealthCrossedFailureThresholdEvent { get; private set; }

        public ContainerKeepaliveManager(
            PostOffice postOffice,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            ILogger serviceLogger,
            IRemoteDialogProtocol remotingProtocol,
            RemotingConfiguration remotingConfig)
        {
            _postOffice = new WeakPointer<PostOffice>(postOffice.AssertNonNull(nameof(postOffice)));
            _serviceLogger = serviceLogger.AssertNonNull(nameof(serviceLogger));
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            _remotingProtocol = remotingProtocol.AssertNonNull(nameof(remotingProtocol));
            remotingConfig.AssertNonNull(nameof(remotingConfig));
            if (remotingConfig.KeepAliveFailureThreshold < 0 || remotingConfig.KeepAliveFailureThreshold > 1)
            {
                throw new ArgumentOutOfRangeException("Failure threshold must be between 0 and 1");
            }

            if (remotingConfig.KeepAlivePingTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("Keep alive ping timeout must be a positive integer");
            }

            _failureThreshold = remotingConfig.KeepAliveFailureThresholdAccessor(_serviceLogger);
            _qualityOfService = new MovingAverage(50, 1.0);
            _cancelToken = new CancellationTokenSource();
            _keepAliveInterval = remotingConfig.KeepAlivePingIntervalAccessor(_serviceLogger);
            _keepAliveTimeout = remotingConfig.KeepAlivePingTimeoutAccessor(_serviceLogger);
            HealthCrossedFailureThresholdEvent = new AsyncEvent<EventArgs>();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        ~ContainerKeepaliveManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the quality of service for keepalive response rate, from 0 (total failure) to 1 (total success)
        /// </summary>
        public double QualityOfService
        {
            get
            {
                return _qualityOfService.Average;
            }
        }

        public async Task Start(IRealTimeProvider realTime)
        {
            await DurandalTaskExtensions.NoOpTask;
            CancellationToken cancelToken = _cancelToken.Token;
            IRealTimeProvider forkedTimeProvider = realTime.Fork(nameof(ContainerKeepaliveManager));

            if (!cancelToken.IsCancellationRequested)
            {
                _backgroundThread = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(async () =>
                {
                    try
                    {
                        while (!cancelToken.IsCancellationRequested)
                        {
                            if (_keepAliveInterval.Value == TimeSpan.Zero)
                            {
                                // If we are configured not to run keepalive right now, I guess we just sleep for a while
                                await forkedTimeProvider.WaitAsync(TimeSpan.FromSeconds(10).Vary(0.1), cancelToken).ConfigureAwait(false);
                            }
                            else
                            {
                                try
                                {
                                    await SendKeepAlive(cancelToken, forkedTimeProvider).ConfigureAwait(false);
                                }
                                catch (Exception e)
                                {
                                    _serviceLogger.Log(e, LogLevel.Err);
                                    // If an unexpected error happens like bad configuration, wait a while so we don't get stuck in an infinite busy loop
                                    await forkedTimeProvider.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
                                }

                                await forkedTimeProvider.WaitAsync(_keepAliveInterval.Value.Vary(0.1), cancelToken).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        forkedTimeProvider.Merge();
                    }
                });
            }
        }

        public async Task Stop()
        {
            _cancelToken.Cancel();
            if (_backgroundThread != null)
            {
                await _backgroundThread.ConfigureAwait(false);
            }
        }

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

            if (!_cancelToken.IsCancellationRequested)
            {
                _cancelToken.Cancel();
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _cancelToken?.Dispose();
                _keepAliveInterval?.Dispose();
                _keepAliveTimeout?.Dispose();
                _failureThreshold?.Dispose();
            }
        }

        private async Task SendKeepAlive(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            KeepAliveRequest remoteRequest = new KeepAliveRequest();
            PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
            MailboxId transientMailbox = _postOffice.Value.CreateTransientMailbox(realTime);
            MailboxMessage message = new MailboxMessage(transientMailbox, _remotingProtocol.ProtocolId, serializedRequest);
            uint operationId = MicroProfiler.GenerateOperationId();
            MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_SendRequestStart, operationId);
            uint messageId = _postOffice.Value.GenerateMessageId();
            message.MessageId = messageId;
            long startTime = HighPrecisionTimer.GetCurrentTicks();
            await _postOffice.Value.SendMessage(message, cancelToken, realTime);
            MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_SendRequestFinish, operationId);
            if (cancelToken.IsCancellationRequested)
            {
                return;
            }

            MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_RecvResponseStart, operationId);
            RetrieveResult<MailboxMessage> response = await _postOffice.Value.TryReceiveMessage(transientMailbox, cancelToken, _keepAliveTimeout.Value, realTime).ConfigureAwait(false);
            long endTime = HighPrecisionTimer.GetCurrentTicks();
            MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_RecvResponseFinish, operationId);

            if (cancelToken.IsCancellationRequested)
            {
                return;
            }

            if (!response.Success)
            {
                await ReportKeepaliveFailure(realTime);
                throw new TimeoutException("Timed out waiting for keepalive response");
            }

            MailboxMessage responseMessage = response.Result;
            if (responseMessage == null || responseMessage.ReplyToId != messageId)
            {
                await ReportKeepaliveFailure(realTime);
                throw new Exception("Null or invalid keepalive response");
            }

            Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(responseMessage.Buffer, _serviceLogger);
            if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<long>))
            {
                await ReportKeepaliveFailure(realTime);
                throw new Exception("Can't parse keepalive response");
            }

            RemoteProcedureResponse<long> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<long>;
            if (finalResponse.Exception != null)
            {
                await ReportKeepaliveFailure(realTime);
                finalResponse.RaiseExceptionIfPresent();
            }

            TimeSpan roundTripTime = TimeSpan.FromTicks(endTime - startTime);
            _qualityOfService.Add(1);
            _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_KeepAlive_RoundTripTime, _metricDimensions, roundTripTime.TotalMilliseconds);
            _metrics.Value.ReportContinuous(CommonInstrumentation.Key_Counter_KeepAlive_QualityOfService, _metricDimensions, _qualityOfService.Average);

            // Recalculate the "below threshold" flag in case it was failing beforehand but has since recovered.
            _isBelowThreshold = _qualityOfService.Average <= _failureThreshold.Value;
        }

        private async Task ReportKeepaliveFailure(IRealTimeProvider realTime)
        {
            _qualityOfService.Add(0);
            _metrics.Value.ReportContinuous(CommonInstrumentation.Key_Counter_KeepAlive_QualityOfService, _metricDimensions, _qualityOfService.Average);

            if (!_isBelowThreshold && _qualityOfService.Average <= _failureThreshold.Value)
            {
                _isBelowThreshold = true;
                await HealthCrossedFailureThresholdEvent.Fire(this, new EventArgs(), realTime);
            }
        }
    }
}
