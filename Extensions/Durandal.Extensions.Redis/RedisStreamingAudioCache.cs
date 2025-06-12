using Durandal.Common.Audio;
using Durandal.Common.Collections;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Extensions.Redis
{
    /// <summary>
    /// Streaming audio cache backed by Redis lists
    /// </summary>
    public class RedisStreamingAudioCache : IStreamingAudioCache
    {
        private static readonly TimeSpan AUDIO_STREAM_LIFETIME = TimeSpan.FromSeconds(30);
        private readonly WeakPointer<ConnectionMultiplexer> _redisConnection;
        private int _disposed = 0;

        public RedisStreamingAudioCache(WeakPointer<RedisConnectionPool> connectionPool)
        {
            _redisConnection = connectionPool.Value.GetMultiplexer();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RedisStreamingAudioCache()
        {
            Dispose(false);
        }
#endif

        public Task<NonRealTimeStream> CreateAudioWriteStream(string key, string codec, string codecParams, ILogger queryLogger, IRealTimeProvider realTime)
        {
            RedisAudioWriteStream returnVal = new RedisAudioWriteStream(_redisConnection, queryLogger, key);
            returnVal.Initialize(codec, codecParams);
            return Task.FromResult<NonRealTimeStream>(returnVal);
        }

        public async Task<RetrieveResult<IAudioDataSource>> TryGetAudioReadStream(string key, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan? maxSpinTime = null)
        {
            queryLogger.Log("Fetching Redis audio stream with key " + key);
            ValueStopwatch fetchTimer = ValueStopwatch.StartNew();
            RedisAudioReadStream returnVal = new RedisAudioReadStream(_redisConnection, key, queryLogger.Clone("RedisAudioReadStream"));
            bool initializedOk = await returnVal.Initialize(cancelToken, realTime, maxSpinTime).ConfigureAwait(false);
            fetchTimer.Stop();
            if (initializedOk)
            {
                return new RetrieveResult<IAudioDataSource>(returnVal, fetchTimer.ElapsedMillisecondsPrecise());
            }
            else
            {
                queryLogger.Log("Redis streaming audio failed to open read stream", LogLevel.Err);
                returnVal.Dispose();
                return new RetrieveResult<IAudioDataSource>(null, fetchTimer.ElapsedMillisecondsPrecise(), false);
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

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
            }
        }

        /// <summary>
        /// A structured representation of the stream frames that actually get stored in Redis.
        /// On the redis side these are turned to byte[] arrays using whatever internal format Redis uses (presumably base64).
        /// </summary>
        private class StreamFrame
        {
            public ArraySegment<byte> Buffer { get; set; }

            public int ChunkIdx { get; set; }

            public string Codec { get; set; }

            public string CodecParams { get; set; }

            public bool EndOfStream { get; set; }

            public long? StreamWriteTime { get; set; }
        }

        /// <summary>
        /// Encodes a redis stream frame to a byte array using custom binary encoding.
        /// This method used to encode to Json internally, but that caused too much allocation overhead,
        /// especially since we have to pass a concrete string or byte[] to Redis in the end anyways.
        /// </summary>
        /// <param name="frame">The frame to encode.</param>
        /// <returns>A byte array containing exactly the contents of the encoded frame.</returns>
        private static byte[] EncodeFrame(StreamFrame frame)
        {
            using (RecyclableMemoryStream stream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (BinaryWriter writer = new BinaryWriter(stream, StringUtils.UTF8_WITHOUT_BOM, leaveOpen: true))
                {
                    writer.Write(frame.ChunkIdx);
                    if (!string.IsNullOrEmpty(frame.Codec))
                    {
                        writer.Write(true);
                        writer.Write(frame.Codec);
                    }
                    else
                    {
                        writer.Write(false);
                    }

                    if (!string.IsNullOrEmpty(frame.CodecParams))
                    {
                        writer.Write(true);
                        writer.Write(frame.CodecParams);
                    }
                    else
                    {
                        writer.Write(false);
                    }

                    writer.Write(frame.EndOfStream);

                    if (frame.StreamWriteTime.HasValue)
                    {
                        writer.Write(true);
                        writer.Write(frame.StreamWriteTime.Value);
                    }
                    else
                    {
                        writer.Write(false);
                    }

                    // Write the data at the very end.
                    // This lets us use a speedhack when reading where we can just say Buffer = Slice(Position, Length)
                    if (frame.Buffer.Array == null || frame.Buffer.Count == 0)
                    {
                        writer.Write(0);
                    }
                    else
                    {
                        writer.Write(frame.Buffer.Count);
                        writer.Write(frame.Buffer.Array, frame.Buffer.Offset, frame.Buffer.Count);
                    }
                }

                return stream.ToArray(); // Wasteful, but again, Redis can only accept a byte[] or string parameter
            }
        }

        /// <summary>
        /// Decodes a stream frame from Redis using our custom binary encoding.
        /// </summary>
        /// <param name="data">The data array to read</param>
        /// <param name="offset">Array offset</param>
        /// <param name="length">Array length</param>
        /// <returns>A decoded stream frame</returns>
        private static StreamFrame DecodeFrame(byte[] data, int offset, int length)
        {
            using (MemoryStream input = new MemoryStream(data, offset, length))
            using (BinaryReader reader = new BinaryReader(input, StringUtils.UTF8_WITHOUT_BOM))
            {
                StreamFrame returnVal = new StreamFrame();
                returnVal.ChunkIdx = reader.ReadInt32();

                if (reader.ReadBoolean())
                {
                    returnVal.Codec = reader.ReadString();
                }

                if (reader.ReadBoolean())
                {
                    returnVal.CodecParams = reader.ReadString();
                }

                returnVal.EndOfStream = reader.ReadBoolean();

                if (reader.ReadBoolean())
                {
                    returnVal.StreamWriteTime = reader.ReadInt64();
                }

                int dataLength = reader.ReadInt32();
                if (dataLength > 0)
                {
                    // Hack hack hack - Return a pointer to the array we are currently reading from rather than reallocate
                    returnVal.Buffer = new ArraySegment<byte>(data, offset + (int)input.Position, dataLength);
                }
                else
                {
                    returnVal.Buffer = new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
                }

                return returnVal;
            }
        }

        private class RedisAudioReadStream : NonRealTimeStream, IAudioDataSource
        {
            private const int CHUNKS_READ_AHEAD = 5;
            private const int TIMEOUT_MS = 2000;
            private readonly IDatabase _redisDb;
            private readonly string _cacheKey;

            // The read stream is constantly popping from the list in a pipeline, to prevent the latency
            // that happens if you wait until the first one has finished before you queue the next one.
            // This means that a few read tasks will still be active after the stream has finished reading, 
            private readonly Queue<Tuple<int, Task<RedisValue>>> _pipelinedReadTasks;

            // The way that this whole pipeline is set up, it should not even be possible
            // for frames to be written or read out of order.
            // Nevertheless, on the chance that it does happen, we add some resiliency by buffering the out of order frames here
            private readonly FastConcurrentDictionary<int, StreamFrame> _outOfOrderFrames;

            private readonly ILogger _traceLogger;

            // This is used to track the retrieve status of each chunk.
            // The basic algorithm is:
            // - Find the next chunk in order that we don't have yet
            // - Queue a redis ListReadByIndex command to try and fetch that chunk
            // - Continue until we have CHUNKS_READ_AHEAD read tasks queued
            // - After we have CHUNKS_READ_AHEAD read-ahead, restart back at 0
            private readonly List<AudioChunkRetrieveStatus> _chunkRetrieveStatus;

            // number of chunks we are currently reading ahead
            private int _currentChunkReadAhead = 0;

            // index of the next chunk we should try and fetch
            private int _nextChunkToFetch = 0;

            // used to enforce stream packet ordering
            private int _expectedChunkIdx;

            // if true, the stream is done
            private bool _endOfStream = false;

            // the last data block we read from the cache
            private ArraySegment<byte> _currentBlock = new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);

            // number of bytes we have read from current block
            private int _currentBlockIdx = 0;

            // used to measure timeout if we receive a bunch of null redis values for a long time
            private long _lastSuccessfulReadTime;

            private int _disposed = 0;

            public RedisAudioReadStream(WeakPointer<ConnectionMultiplexer> redisConnection, string cacheKey, ILogger traceLogger)
            {
                _cacheKey = cacheKey;
                _expectedChunkIdx = 0;
                _redisDb = redisConnection.Value.GetDatabase();
                _pipelinedReadTasks = new Queue<Tuple<int, Task<RedisValue>>>();
                _traceLogger = traceLogger;
                _outOfOrderFrames = new FastConcurrentDictionary<int, StreamFrame>(10);
                _chunkRetrieveStatus = new List<AudioChunkRetrieveStatus>();
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~RedisAudioReadStream()
            {
                Dispose(false);
            }
#endif

            public string Codec { get; set; }

            public string CodecParams { get; set; }

            public async Task<bool> Initialize(CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan? maxSpinTime = null)
            {
                // Implement the maxSpinTime by just manipulating the last successful read time so that the normal timeout logic will apply
                _lastSuccessfulReadTime = realTime.TimestampMilliseconds - TIMEOUT_MS + (long)maxSpinTime.GetValueOrDefault(TimeSpan.Zero).TotalMilliseconds;
                StreamFrame firstStreamFrame = await ReadNextStreamFrameInOrder(cancelToken, realTime).ConfigureAwait(false);
                if (firstStreamFrame == null)
                {
                    // Timed out on first packet fetch
                    return false;
                }

                if (firstStreamFrame.EndOfStream)
                {
                    _endOfStream = true;
                    _traceLogger.Log("First frame in redis stream was marked as EndOfStream. This is probably not intended.", LogLevel.Wrn);
                    return false;
                }
                else
                {
                    Codec = firstStreamFrame.Codec;
                    CodecParams = firstStreamFrame.CodecParams;

                    // Log instrumentation
                    if (firstStreamFrame.StreamWriteTime.HasValue)
                    {
                        long currentTicks = HighPrecisionTimer.GetCurrentTicks();
                        TimeSpan timeHeaderSpentInCache = TimeSpan.FromTicks(currentTicks - firstStreamFrame.StreamWriteTime.Value);
                        _traceLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_StreamingAudioTimeInCache, timeHeaderSpentInCache), LogLevel.Ins);
                    }

                    return true;
                }
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => 0;

            public override long Position
            {
                get
                {
                    return 0;
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public NonRealTimeStream AudioDataReadStream => this;

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return ReadAsync(targetBuffer, offset, count, cancelToken, realTime).Await();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return ReadAsync(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
            }

            public override async Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                int bytesRead = 0;

                try
                {
                    while (bytesRead == 0 && !_endOfStream && !cancelToken.IsCancellationRequested)
                    {
                        // Enqueue a new block of data if needed
                        if (_currentBlock.Array == null || _currentBlock.Count == 0)
                        {
                            StreamFrame nextStreamFrame = await ReadNextStreamFrameInOrder(cancelToken, realTime).ConfigureAwait(false);

                            if (nextStreamFrame != null)
                            {
                                if (nextStreamFrame.EndOfStream)
                                {
                                    _endOfStream = true;

                                    // Clear out the read queue when we read the last frame
                                    // This has the chance of causing a bit of stutter - it might be technically
                                    // more correct to put this into Dispose(), but the async and stateful
                                    // nature of it makes that tricky.
                                    while (_pipelinedReadTasks.Count > 0)
                                    {
                                        await _pipelinedReadTasks.Dequeue().Item2.ConfigureAwait(false);
                                    }

                                    //_traceLogger.Log("Deleting streaming audio key " + _cacheKey + " after successful read", LogLevel.Vrb);
                                    //await _redisDb.KeyDeleteAsync(_cacheKey, CommandFlags.FireAndForget);
                                }
                                else
                                {
                                    _currentBlock = nextStreamFrame.Buffer;
                                    _currentBlockIdx = 0;
                                }
                            }
                        }

                        // Read as much as we can from the current block of data, if any
                        if (_currentBlock.Array != null && _currentBlock.Count > _currentBlockIdx)
                        {
                            int amountCanReadFromPreviousBlock = Math.Min(count - bytesRead, _currentBlock.Count - _currentBlockIdx);
                            ArrayExtensions.MemCopy(
                                _currentBlock.Array,
                                _currentBlock.Offset + _currentBlockIdx,
                                targetBuffer,
                                offset + bytesRead,
                                amountCanReadFromPreviousBlock);
                            //_traceLogger.Log(BinaryHelpers.ToHexString(targetBuffer, offset, amountCanReadFromPreviousBlock));
                            _currentBlockIdx += amountCanReadFromPreviousBlock;
                            if (_currentBlockIdx == _currentBlock.Count)
                            {
                                _currentBlock = new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
                                _currentBlockIdx = 0;
                            }

                            bytesRead += amountCanReadFromPreviousBlock;
                        }
                    }
                }
                catch (RedisTimeoutException e)
                {
                    _traceLogger.Log(e, LogLevel.Err);
                    _endOfStream = true;
                }

                return bytesRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new NotImplementedException();
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
                        // Don't await the pipelined read tasks because they can throw exceptions, also because we don't
                        // want to delay on network reads during the dispose operation.
                        // Anyway, the end of stream handler should have flushed all of these anyways.
                        _pipelinedReadTasks.Clear();
                    }

                    _traceLogger.Log("Closed redis audio read stream", LogLevel.Vrb);
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }

            private async Task<StreamFrame> ReadNextStreamFrameInOrder(CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                StreamFrame returnVal;
                while (!_endOfStream)
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        _endOfStream = true;
                        return null;
                    }

                    // Queue up some read tasks in the pipeline
                    QueueReadAheadTasks();

                    // See if the next stream frame in the order has been read
                    if (_outOfOrderFrames.TryGetValueAndRemove(_expectedChunkIdx, out returnVal))
                    {
                        _expectedChunkIdx++;
                        return returnVal;
                    }

                    Tuple<int, Task<RedisValue>> nextFetchResult = _pipelinedReadTasks.Dequeue();
                    RedisValue val = await nextFetchResult.Item2.ConfigureAwait(false);
                    if (!val.IsNullOrEmpty)
                    {
                        _lastSuccessfulReadTime = realTime.TimestampMilliseconds;
                        byte[] blob = (byte[])val.Box();
                        StreamFrame newFrame = DecodeFrame(blob, 0, blob.Length);
                        //_traceLogger.Log("Read stream frame " + newFrame.ChunkIdx);
                        _outOfOrderFrames[newFrame.ChunkIdx] = newFrame;
                        _chunkRetrieveStatus[nextFetchResult.Item1] = AudioChunkRetrieveStatus.Found;
                    }
                    else
                    {
                        _chunkRetrieveStatus[nextFetchResult.Item1] = AudioChunkRetrieveStatus.NotFound;

                        // If we haven't found any valid data in a while, or we've canceled, trigger the timeout path
                        if ((int)(realTime.TimestampMilliseconds - _lastSuccessfulReadTime) > TIMEOUT_MS)
                        {
                            // Timed out. Just end the stream
                            _traceLogger.Log("Timed out while reading from redis audio stream", LogLevel.Err);
                            _endOfStream = true;
                            return null;
                        }
                    }
                }

                return null;
            }

            /// <summary>
            /// Routine to fill up the pipelined read tasks to start fetching chunks we will need soon
            /// </summary>
            private void QueueReadAheadTasks()
            {
                while (_pipelinedReadTasks.Count < CHUNKS_READ_AHEAD)
                {
                    // Find the next chunk that is not already fetched or being fetched
                    while (_nextChunkToFetch < _chunkRetrieveStatus.Count &&
                        _chunkRetrieveStatus[_nextChunkToFetch] != AudioChunkRetrieveStatus.NotFound)
                    {
                        _nextChunkToFetch++;
                    }

                    // Enqueue the task
                    _pipelinedReadTasks.Enqueue(new Tuple<int, Task<RedisValue>>(
                        _nextChunkToFetch,
                        _redisDb.ListGetByIndexAsync(_cacheKey, _nextChunkToFetch)));

                    // Update the status list
                    if (_nextChunkToFetch == _chunkRetrieveStatus.Count)
                    {
                        _chunkRetrieveStatus.Add(AudioChunkRetrieveStatus.Fetching);
                    }
                    else
                    {
                        _chunkRetrieveStatus[_nextChunkToFetch] = AudioChunkRetrieveStatus.Fetching;
                    }

                    _nextChunkToFetch++;
                    _currentChunkReadAhead++;

                    // Reset back to start if we've hit the read-ahead limit
                    if (_currentChunkReadAhead >= CHUNKS_READ_AHEAD)
                    {
                        _nextChunkToFetch = 0;
                        _currentChunkReadAhead = 0;
                    }
                }
            }

            /// <summary>
            /// Used to track the retrieval status of each current + future data chunk
            /// </summary>
            private enum AudioChunkRetrieveStatus : byte
            {
                NotFound = 0,
                Fetching = 1,
                Found = 2,
            }
        }

        private class RedisAudioWriteStream : NonRealTimeStream
        {
            private readonly IDatabase _redisDb;
            private readonly string _cacheKey;
            private readonly Queue<Task> _pipelinedTasks;
            private readonly ILogger _traceLogger;
            private int _disposed = 0;
            private int _chunkIdx;

            public RedisAudioWriteStream(WeakPointer<ConnectionMultiplexer> redisConnection, ILogger traceLogger, string cacheKey)
            {
                _cacheKey = cacheKey;
                _chunkIdx = 0;
                _traceLogger = traceLogger;
                _redisDb = redisConnection.Value.GetDatabase();
                _pipelinedTasks = new Queue<Task>();
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~RedisAudioWriteStream()
            {
                Dispose(false);
            }
#endif

            public void Initialize(string codec, string codecParams)
            {
                StreamFrame headerPacket = new StreamFrame()
                {
                    Codec = codec,
                    CodecParams = codecParams,
                    ChunkIdx = _chunkIdx++,
                    StreamWriteTime = HighPrecisionTimer.GetCurrentTicks()
                };

                byte[] encodedPacket = EncodeFrame(headerPacket);

                //_traceLogger.Log("Writing header stream frame " + json);
                _pipelinedTasks.Enqueue(_redisDb.ListRightPushAsync(_cacheKey, encodedPacket));

                // Set the TTL of the key - this will set an absolute expire time which persists even after pushing/popping the list
                _redisDb.KeyExpireAsync(_cacheKey, AUDIO_STREAM_LIFETIME, CommandFlags.FireAndForget);
            }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => 0;

            public override long Position
            {
                get
                {
                    return 0;
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                StreamFrame frame = new StreamFrame()
                {
                    ChunkIdx = _chunkIdx++,
                    Buffer = new ArraySegment<byte>(sourceBuffer, offset, count)
                };

                byte[] encodedPacket = EncodeFrame(frame);

                //_traceLogger.Log("Writing stream frame " + frame.ChunkIdx);
                _pipelinedTasks.Enqueue(_redisDb.ListRightPushAsync(_cacheKey, encodedPacket, When.Always, CommandFlags.FireAndForget));
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                Write(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }

            public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                Write(sourceBuffer, offset, count, cancelToken, realTime);
                return DurandalTaskExtensions.NoOpTask;
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
                        StreamFrame trailerPacket = new StreamFrame()
                        {
                            ChunkIdx = _chunkIdx++,
                            EndOfStream = true
                        };

                        byte[] encodedPacket = EncodeFrame(trailerPacket);
                        //_traceLogger.Log("Writing trailer stream frame " + json);
                        _pipelinedTasks.Enqueue(_redisDb.ListRightPushAsync(_cacheKey, encodedPacket, When.Always, CommandFlags.FireAndForget));

                        foreach (Task b in _pipelinedTasks)
                        {
                            // this shouldn't incur any delay because of the fire and forget flag
                            b.Await();
                        }

                        _traceLogger.Log("Closed redis audio write stream", LogLevel.Vrb);
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }
    }
}
