using Durandal.Common.Collections;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting.Proxies
{
    /// <summary>
    /// Implements a file access stream which is operating over a remoted method dispatcher.
    /// </summary>
    public class RemotedFileStream : NonRealTimeStream
    {
        // There are 4 main operation modes of the stream:
        // 1. Write-only: Straightforward, just pipeline all the outgoing writes and flush on close.
        // 2. Read-only: Do buffered async reads with an adjusting bandwidth window to try and saturate the connection.
        // 3. Read-write and the remote stream is not seekable: Same as write-only but with read operations mixed
        //    in with the serial pipeline. Speculative reads aren't possible so all read ops are synchronous (slow).
        //    This is considered a degenerate case since read-write implies random access but a non-seekable stream
        //    cancels that out. Warn the user if this ever happens.
        // 4. Read-write: Two separate pipelines, one for serial writes, the other for speculative buffered reads.
        //    Whenever we trigger a write operation, we invalidate the speculative read buffer which triggers
        //    seeking on the remote end. Reads draw from the speculative read cache. Lots of logic has to make
        //    sure the local and remote file positions line up correctly - this is made explicit on the wire
        //    by each individual command having absolute position and length parameters.

        // Divide all reads/writes into chunks of this maximum size to avoid large allocations on the remoting protocol
        private const int MAX_CHUNK_SIZE = 8196;

        // With speculative read enabled, this is the maximum number of read tasks that can possibly be queued
        // This puts a bound on maximum throughput, where throughput = MAX_CHUNK_SIZE * MAX_SPECULATIVE_QUEUE_SIZE / Socket Latency
        // This limit is also used for pipelined writes
        private const float MAX_QUEUE_SIZE = 4092;

        // When invalidating the speculative read queue, reset the queue size to this so we don't queue tons of reads after random access
        // The behavior is same with pipelined writes after a seek
        private const float MAX_QUEUE_SIZE_AFTER_RANDOMACCESS = 32;

        // Queue size decays by this amount after every successful read
        private const float QUEUE_DECAY = 0.1f;

        private readonly ILogger _logger;
        private readonly WeakPointer<RemoteDialogMethodDispatcher> _dispatcherClient;
        private readonly Queue<Task<bool>> _pipelinedWrites;
        private readonly Queue<SpeculativeReadClosure> _speculativeReads;

        private readonly RemoteFileStreamOpenResult _remoteStreamInfo;
        private readonly bool _canDoPipelinedWrites;
        private readonly bool _canDoSpeculativeReads;
        private long _length;
        private long _currentPosition;
        private float _pipelinedWriteDesiredQueueRate = 1.0f;
        private float _speculativeReadDesiredQueueRate = 1.0f;
        private long _nextSpeculativeReadCursor = 0;
        private ArraySegment<byte>? _lastSpeculativeReadResult;
        private int _bytesReadFromLastSpeculativeReadResult = 0;
        private int _disposed = 0;

        public RemotedFileStream(
            VirtualPath filePath,
            RemoteFileStreamOpenResult remoteStreamInfo,
            WeakPointer<RemoteDialogMethodDispatcher> dispatcher,
            ILogger logger)
        {
            _remoteStreamInfo = remoteStreamInfo.AssertNonNull(nameof(remoteStreamInfo));
            _remoteStreamInfo.StreamId.AssertNonNullOrEmpty("StreamId");
            _canDoPipelinedWrites = _remoteStreamInfo.CanWrite && (!_remoteStreamInfo.CanRead || _remoteStreamInfo.CanSeek);
            _canDoSpeculativeReads = _remoteStreamInfo.CanRead && _remoteStreamInfo.CanSeek;
            _length = _remoteStreamInfo.InitialFileLength.GetValueOrDefault(0);
            _dispatcherClient = dispatcher.AssertNonNull(nameof(dispatcher));
            _logger = logger.AssertNonNull(nameof(logger));
            _lastSpeculativeReadResult = null;

            if (_canDoPipelinedWrites)
            {
                _pipelinedWrites = new Queue<Task<bool>>();
            }

            if (_canDoSpeculativeReads)
            {
                _speculativeReads = new Queue<SpeculativeReadClosure>();
            }

            if (_remoteStreamInfo.CanRead && _remoteStreamInfo.CanWrite && !_remoteStreamInfo.CanSeek)
            {
                _logger.Log("Remoted file stream opened in R/W mode, but the remote stream does not support seeking. This is a degenerate case where performance will be slow.", LogLevel.Wrn);
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RemotedFileStream()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public override bool CanRead => _remoteStreamInfo.CanRead;

        /// <inheritdoc />
        public override bool CanSeek => _remoteStreamInfo.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => _remoteStreamInfo.CanWrite;

        /// <inheritdoc />
        public override long Length => _length;

        /// <inheritdoc />
        public override long Position
        {
            get
            {
                return _currentPosition;
            }
            set
            {
                if (!_remoteStreamInfo.CanSeek)
                {
                    throw new NotSupportedException("This stream does not support seeking");
                }

                _currentPosition = value;
                InvalidateSpeculativeReads();
                _pipelinedWriteDesiredQueueRate = Math.Min(_pipelinedWriteDesiredQueueRate, MAX_QUEUE_SIZE_AFTER_RANDOMACCESS);
            }
        }

        /// <inheritdoc />
        public override void Flush()
        {
            FlushAsync().Await();
        }

        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_canDoPipelinedWrites)
            {
                while (_pipelinedWrites.Count > 0)
                {
                    await _pipelinedWrites.Dequeue().ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
        }

        /// <inheritdoc />
        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return ReadAsync(targetBuffer, offset, count, cancelToken, realTime).Await();
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            targetBuffer.AssertNonNull(nameof(targetBuffer));
            offset.AssertNonNegative(nameof(offset));
            count.AssertPositive(nameof(count));
            int maxActualReadSize = Math.Min(count, MAX_CHUNK_SIZE);
            if (_canDoSpeculativeReads)
            {
                // Queue reads up to the desired queue rate
                int roundedQueueRate = (int)Math.Round(_speculativeReadDesiredQueueRate);

                //_logger.Log(string.Format("Stream read: Count {0}, Position {1}, SpecReadPosition {2}, Length {3}, QueueCount {4}, DesiredCount {5}",
                //    count, _currentPosition, _nextSpeculativeReadCursor, _length, _speculativeReads.Count, _speculativeReadDesiredQueueRate));
                while (_nextSpeculativeReadCursor < _length && _speculativeReads.Count < roundedQueueRate)
                {
                    // We know the presumed length of the remote file so we can make sure we don't do a speculative read beyond that bound
                    int readSize = (int)Math.Min(_length - _nextSpeculativeReadCursor, (long)MAX_CHUNK_SIZE);
                    //_logger.Log("Stream read: Queue new speculative read of " + readSize + " at position " + _nextSpeculativeReadCursor);
                    SpeculativeReadClosure speculativeTask = new SpeculativeReadClosure(_remoteStreamInfo.StreamId, _nextSpeculativeReadCursor, readSize, _dispatcherClient);
                    await speculativeTask.Start(realTime, cancelToken).ConfigureAwait(false);
                    _speculativeReads.Enqueue(speculativeTask);
                    _nextSpeculativeReadCursor += readSize;
                }

                // If we don't have a half-used result from the last read, get a new one
                if (!_lastSpeculativeReadResult.HasValue && _speculativeReads.Count > 0)
                {
                    // Dequeue the first one and await it, it should contain the data we're requestin
                    SpeculativeReadClosure firstSpeculativeRead = _speculativeReads.Dequeue();
                    if (!firstSpeculativeRead.WaitForDataTask.IsFinished())
                    {
                        // Network underrun - we requested data and it's not ready yet. Increase the queue rate to compensate
                        // (this is like increasing the TCP window of a download over time)
                        _speculativeReadDesiredQueueRate = Math.Min(MAX_QUEUE_SIZE, _speculativeReadDesiredQueueRate + 1);
                    }
                    else
                    {
                        // Decay the queue rate as well over time - this is to try and adapt to changing network conditions
                        _speculativeReadDesiredQueueRate = Math.Max(1.0f, _speculativeReadDesiredQueueRate - QUEUE_DECAY);
                    }

#if DEBUG
                    if (firstSpeculativeRead.ReadPosition != _currentPosition)
                    {
                        throw new ArgumentException($"Speculative read position is out of sync (expected {_currentPosition}, got {firstSpeculativeRead.ReadPosition})");
                    }
                    if (!string.Equals(firstSpeculativeRead.StreamId, _remoteStreamInfo.StreamId, StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Speculative read is not for correct stream ID (expected {_remoteStreamInfo.StreamId}, got {firstSpeculativeRead.StreamId})");
                    }
#endif

                    _lastSpeculativeReadResult = await firstSpeculativeRead.WaitForDataTask.ConfigureAwait(false);
                    _bytesReadFromLastSpeculativeReadResult = 0;
                    //_logger.Log("Stream read: Cache miss, queued new read result with length " + _lastSpeculativeReadResult.Value.Count);
                }

                if (!_lastSpeculativeReadResult.HasValue)
                {
                    return 0;
                }

                int bytesToCopy = FastMath.Min(maxActualReadSize, _lastSpeculativeReadResult.Value.Count - _bytesReadFromLastSpeculativeReadResult);
                if (bytesToCopy > 0)
                {
                    //_logger.Log("Stream read: Reading " + bytesToCopy + " from speculative buffer (out of " + _lastSpeculativeReadResult.Value.Count + ")");
                    ArrayExtensions.MemCopy(
                        _lastSpeculativeReadResult.Value.Array,
                        _lastSpeculativeReadResult.Value.Offset + _bytesReadFromLastSpeculativeReadResult,
                        targetBuffer,
                        offset,
                        bytesToCopy);
                    _currentPosition += bytesToCopy;
                    _bytesReadFromLastSpeculativeReadResult += bytesToCopy;
                    if (_bytesReadFromLastSpeculativeReadResult == _lastSpeculativeReadResult.Value.Count)
                    {
                        _lastSpeculativeReadResult = null;
                        //_logger.Log("Stream read: Reached end of current read result");
                    }
                }

                //_logger.Log("Stream read w/ speculative: Caller requested " + count + ", returning " + bytesToCopy);
                return bytesToCopy;
            }
            else
            {
                // Degenerate synchronous case.
                // Queue the read request and await it directly.
                Task<ArraySegment<byte>> returnValTask = await _dispatcherClient.Value.FileStream_Read(
                    _remoteStreamInfo.StreamId,
                    _currentPosition,
                    maxActualReadSize,
                    realTime,
                    cancelToken).ConfigureAwait(false);

                ArraySegment<byte> returnVal = await returnValTask.ConfigureAwait(false);
                if (returnVal.Count > 0)
                {
                    ArrayExtensions.MemCopy(returnVal.Array, returnVal.Offset, targetBuffer, offset, returnVal.Count);
                    _currentPosition += returnVal.Count;
                }

                return returnVal.Count;
            }
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!CanSeek)
            {
                throw new NotSupportedException("This stream does not support seeking");
            }

            switch (origin)
            {
                case SeekOrigin.Begin:
                default:
                    _currentPosition = offset;
                    break;
                case SeekOrigin.End:
                    _currentPosition = _length + offset;
                    break;
                case SeekOrigin.Current:
                    _currentPosition += offset;
                    break;
            }

            InvalidateSpeculativeReads();
            _pipelinedWriteDesiredQueueRate = Math.Min(_pipelinedWriteDesiredQueueRate, MAX_QUEUE_SIZE_AFTER_RANDOMACCESS);
            return _currentPosition;
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            // _length = value; // will this even work?
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
        }

        /// <inheritdoc />
        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            WriteAsync(sourceBuffer, offset, count, cancelToken, realTime).Await();
        }

        /// <inheritdoc />
        public override async Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            sourceBuffer.AssertNonNull(nameof(sourceBuffer));
            offset.AssertNonNegative(nameof(offset));
            count.AssertPositive(nameof(count));

            // Chunk the write requests to avoid LOH of temporary buffers inside of remoting messages
            int bytesOfInputProcessed = 0;
            while (bytesOfInputProcessed < count)
            {
                int thisWriteSize = FastMath.Min(MAX_CHUNK_SIZE, count - bytesOfInputProcessed);
                await WriteInternalAsync(sourceBuffer, offset + bytesOfInputProcessed, thisWriteSize, cancelToken, realTime).ConfigureAwait(false);
                bytesOfInputProcessed += thisWriteSize;
            }

            InvalidateSpeculativeReads();
        }

        private async Task WriteInternalAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // This await handles the work of writing the data to the wire.
            // The method returns a Task which will eventually return the final result of the write.
            // But we defer that until later since it would otherwise cost us an entire round-trip of latency
            // just to check if the write succeeded or not.
            // We can pass a reference to the source buffer here and not worry about it changing later
            // because the entire message will be written to the wire by the time this await finishes
            Task<bool> writeResponseTask = await _dispatcherClient.Value.FileStream_Write(
                _remoteStreamInfo.StreamId,
                _currentPosition,
                new ArraySegment<byte>(sourceBuffer, offset, count),
                realTime,
                cancelToken).ConfigureAwait(false);

            if (_canDoPipelinedWrites)
            {
                // Await any completed writes in the pipe. This will potentially raise exceptions from previous writes raised by the remote end.
                while (_pipelinedWrites.Count > 0 && _pipelinedWrites.Peek().IsFinished())
                {
                    await _pipelinedWrites.Dequeue().ConfigureAwait(false);
                }

                // Increase pipeline queue rate
                if (_pipelinedWrites.Count < _pipelinedWriteDesiredQueueRate)
                {
                    _pipelinedWriteDesiredQueueRate = Math.Min(MAX_QUEUE_SIZE, _pipelinedWriteDesiredQueueRate + 1.0f);
                }

                // Write queue is full, wait a bit...
                int currentQueueRate = (int)Math.Round(_pipelinedWriteDesiredQueueRate);
                while (_pipelinedWrites.Count > currentQueueRate)
                {
                    await _pipelinedWrites.Dequeue().ConfigureAwait(false);
                }

                // Queue the continuation task to the write pipeline.
                _pipelinedWrites.Enqueue(writeResponseTask);
                _pipelinedWriteDesiredQueueRate = Math.Max(1.0f, _pipelinedWriteDesiredQueueRate - QUEUE_DECAY);
            }
            else
            {
                // Synchronous write path
                await writeResponseTask.ConfigureAwait(false);
            }

            _currentPosition += count;
            if (_currentPosition > _length)
            {
                _length = _currentPosition;
            }
        }

        private void InvalidateSpeculativeReads()
        {
            if (_canDoSpeculativeReads)
            {
                //_logger.Log("Invalidating all speculative reads");
                _lastSpeculativeReadResult = null;
                _bytesReadFromLastSpeculativeReadResult = 0;
                _nextSpeculativeReadCursor = _currentPosition;
                _speculativeReadDesiredQueueRate = Math.Min(_speculativeReadDesiredQueueRate, MAX_QUEUE_SIZE_AFTER_RANDOMACCESS);
                while (_speculativeReads.Count > 0)
                {
                    _speculativeReads.Dequeue().WaitForDataTask.Forget(_logger);
                }
            }
        }

        /// <inheritdoc />
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
                    // Flush all read + write tasks
                    if (_canDoPipelinedWrites)
                    {
                        while (_pipelinedWrites.Count > 0)
                        {
                            _pipelinedWrites.Dequeue().Await();
                        }
                    }

                    if (_canDoSpeculativeReads)
                    {
                        while (_speculativeReads.Count > 0)
                        {
                            _speculativeReads.Dequeue().WaitForDataTask.Await();
                        }
                    }

                    // Send the close command to remote server
                    _dispatcherClient.Value.FileStream_Close(_remoteStreamInfo.StreamId, DefaultRealTimeProvider.Singleton, CancellationToken.None).Await();
                }
            }
            catch (Exception e)
            {
                _logger.Log(e);
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private class SpeculativeReadClosure
        {
            public Task<ArraySegment<byte>> WaitForDataTask;
            public string StreamId;
            public long ReadPosition;
            public int RequestedLength;
            private WeakPointer<RemoteDialogMethodDispatcher> _dispatcherClient;

            public SpeculativeReadClosure(string streamId, long readPosition, int requestedLength, WeakPointer<RemoteDialogMethodDispatcher> dispatcher)
            {
                StreamId = streamId;
                ReadPosition = readPosition;
                RequestedLength = requestedLength;
                _dispatcherClient = dispatcher;
            }

            public async Task Start(IRealTimeProvider realTime, CancellationToken cancelToken)
            {
                WaitForDataTask = await _dispatcherClient.Value.FileStream_Read(
                    StreamId,
                    ReadPosition,
                    RequestedLength,
                    realTime,
                    cancelToken).ConfigureAwait(false);
            }
        }
    }
}
