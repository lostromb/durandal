using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Used for very precise instrumentation to measure the rate of data flow over time in a stream.
    /// </summary>
    public class RateMonitoringNonRealTimeStream : NonRealTimeStream
    {
        private readonly NonRealTimeStream _inner;
        private readonly TinyHistogram _histogram;
        private readonly Stopwatch _stopwatch;
        private readonly string _streamName;
        private readonly ILogger _logger;
        private readonly double _maxSpan;
        private readonly MovingPercentile _readTimes;
        private long _totalBytesSent = 0;
        private int _disposed = 0;

        /// <summary>
        /// Creates a new detailed rate monitoring stream
        /// </summary>
        /// <param name="inner">The stream to wrap around</param>
        /// <param name="outputLogger">The logger to write the results to. This will be automatically cloned to streamName so you don't need to clone it yourself</param>
        /// <param name="streamName">The name of this instrumentation stream</param>
        /// <param name="maxSpan">The maximum length of timeline data to collect</param>
        public RateMonitoringNonRealTimeStream(NonRealTimeStream inner, ILogger outputLogger, string streamName, TimeSpan? maxSpan = null)
        {
            _inner = inner.AssertNonNull(nameof(inner));
            _streamName = streamName.AssertNonNullOrEmpty(nameof(streamName));
            _logger = outputLogger.AssertNonNull(nameof(outputLogger)).Clone(_streamName);
            _histogram = new TinyHistogram();
            _maxSpan = maxSpan.HasValue ? maxSpan.Value.TotalMilliseconds : double.MaxValue;
            _readTimes = new MovingPercentile(100, 0.25, 0.5, 0.75, 0.99);
            _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Starting detailed stream instrumentation at {0:yyyy-MM-ddTHH:mm:ss.fffff}", HighPrecisionTimer.GetCurrentUTCTime());
            _stopwatch = Stopwatch.StartNew();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get
            {
                return _inner.Position;
            }
            set
            {
                _inner.Position = value;
            }
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            double startTime = _stopwatch.ElapsedMillisecondsPrecise();
            int returnVal = _inner.Read(targetBuffer, offset, count, cancelToken, realTime);
            double time = _stopwatch.ElapsedMillisecondsPrecise();
            if (time < _maxSpan)
            {
                _histogram.AddValue(time, returnVal);
                _totalBytesSent += returnVal;
            }

            _readTimes.Add(time - startTime);
            return returnVal;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int returnVal = _inner.Read(buffer, offset, count);
            double time = _stopwatch.ElapsedMillisecondsPrecise();
            if (time < _maxSpan)
            {
                _histogram.AddValue(time, returnVal);
                _totalBytesSent += returnVal;
            }

            return returnVal;
        }

        public override async Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            double startTime = _stopwatch.ElapsedMillisecondsPrecise();
            int returnVal = await _inner.ReadAsync(targetBuffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
            double time = _stopwatch.ElapsedMillisecondsPrecise();
            if (time < _maxSpan)
            {
                _histogram.AddValue(time, returnVal);
                _totalBytesSent += returnVal;
            }

            _readTimes.Add(time - startTime);
            return returnVal;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            double time = _stopwatch.ElapsedMillisecondsPrecise();
            if (time < _maxSpan)
            {
                _histogram.AddValue(time, count);
                _totalBytesSent += count;
            }

            _inner.Write(sourceBuffer, offset, count, cancelToken, realTime);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            double time = _stopwatch.ElapsedMillisecondsPrecise();
            if (time < _maxSpan)
            {
                _histogram.AddValue(time, count);
                _totalBytesSent += count;
            }

            _inner.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            double time = _stopwatch.ElapsedMillisecondsPrecise();
            if (time < _maxSpan)
            {
                _histogram.AddValue(time, count);
                _totalBytesSent += count;
            }

            return _inner.WriteAsync(sourceBuffer, offset, count, cancelToken, realTime);
        }

        public override Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _inner.FlushAsync(cancelToken, realTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                if (disposing)
                {
                    _stopwatch?.Stop();
                    try
                    {
                        PooledStringBuilder pooledSb = StringBuilderPool.Rent();
                        pooledSb.Builder.AppendFormat("Total bytes: {0} Total lifetime: {1:F2} ms Timeline: ", _totalBytesSent, _stopwatch.ElapsedMillisecondsPrecise());
                        _histogram.RenderAsOneLine(120, false, pooledSb.Builder);
                        _logger.Log(
                            PooledLogEvent.Create(
                                _logger.ComponentName,
                                pooledSb,
                                LogLevel.Std,
                                HighPrecisionTimer.GetCurrentUTCTime(),
                                _logger.TraceId,
                                DataPrivacyClassification.SystemMetadata));

                        //pooledSb.Builder.Clear();
                        //pooledSb.Builder.AppendFormat("Read performance: p50 {0:F2} ms, p99 {1:F2} ms", _readTimes.GetPercentile(0.5), _readTimes.GetPercentile(0.99));
                    }
                    catch (Exception e)
                    {
                        _logger?.Log(e);
                    }

                    _inner?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
