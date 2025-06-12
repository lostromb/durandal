//using Durandal.Common.Audio;
//using Durandal.Common.Audio;
//using Durandal.Common.Instrumentation;
//using Durandal.Common.Logger;
//using Durandal.Common.Utils;
//using Durandal.Common.Cache;
//using Durandal.Common.Tasks;
//using Durandal.Common.Time;
//using MySql.Data.MySqlClient;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Data;
//using System.Data.Common;
//using System.Diagnostics;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using Durandal.Common.Dialog.Services;

//namespace Durandal.Extensions.MySql
//{
//    public class MySqlStreamingAudioCache : MySqlDataSource, IStreamingAudioCache
//    {
//        private const string TABLE_NAME = "streams";

//        /// <summary>
//        /// Size in bytes of each blob to store on the stream table
//        /// </summary>
//        private const int CHUNK_SIZE = 8192;

//        /// <summary>
//        /// Milliseconds to wait in between each stream read/write operation if no data is available
//        /// </summary>
//        private const int SPINWAIT_DELAY = 2;
//        private static readonly TimeSpan SPINWAIT_TIME = TimeSpan.FromMilliseconds(SPINWAIT_DELAY);

//        /// <summary>
//        /// The time, in milliseconds, that the operation to get a read stream will wait for data to arrive before giving up
//        /// </summary>
//        private static readonly TimeSpan DEFAULT_INITIAL_READ_STREAM_WAIT_TIME = TimeSpan.FromSeconds(5);

//        /// <summary>
//        /// Amount of time to wait to acquire a pooled SQL connection
//        /// </summary>
//        private const int MAX_TIME_TO_GET_CONNECTION = 5000;

//        private const int MAX_CONCURRENT_WRITES = 8;

//        private readonly WeakPointer<MySqlConnectionPool> _connectionPool;
//        private readonly ILogger _logger;
//        private readonly IThreadPool _threadPool;
//        private readonly IThreadPool _parallelWriteThreadPool;
//        private readonly bool _useCoproc;

//        public MySqlStreamingAudioCache(
//            MySqlConnectionPool connectionPool,
//            ILogger logger,
//            IThreadPool threadPool,
//            IMetricCollector poolMetrics = null,
//            DimensionSet metricDimensions = null,
//            bool useCoproc = true)
//            : base(connectionPool, logger)
//        {
//            _connectionPool = new WeakPointer<MySqlConnectionPool>(connectionPool);
//            _logger = logger;
//            _threadPool = threadPool;
//            _useCoproc = useCoproc;
//            _parallelWriteThreadPool = new FixedCapacityThreadPool(
//                _threadPool,
//                logger,
//                poolMetrics ?? NullMetricCollector.Singleton,
//                metricDimensions ?? DimensionSet.Empty,
//                "SqlStreamingAudioWritePool",
//                MAX_CONCURRENT_WRITES,
//                ThreadPoolOverschedulingBehavior.QuadraticThrottle,
//                TimeSpan.FromMilliseconds(1));
//        }

//        public async Task<RetrieveResult<AudioReadPipe>> GetStreamAsync(string key, ILogger queryLogger, IRealTimeProvider realTime, TimeSpan? maxSpinTime = null)
//        {
//            ReadStream reader = new ReadStream(key, _connectionPool, queryLogger, _parallelWriteThreadPool, _useCoproc);
//            Stopwatch timer = new Stopwatch();
//            timer.Start();
//            AudioReadPipe stream = await reader.GetOutputStream(realTime, maxSpinTime.GetValueOrDefault(DEFAULT_INITIAL_READ_STREAM_WAIT_TIME));
//            timer.Stop();

//            if (stream == null)
//            {
//                queryLogger.Log("MySql streaming audio failed to open read stream", LogLevel.Err);
//                return new RetrieveResult<AudioReadPipe>()
//                {
//                    Success = false,
//                    LatencyMs = timer.ElapsedMillisecondsPrecise()
//                };
//            }

//            // Start a background task which will do the reading on the thread pool
//            IRealTimeProvider forkedTime = realTime.Fork("MySqlAudioStreamThread");
//            _threadPool.EnqueueUserAsyncWorkItem(async () =>
//            {
//                try
//                {
//                    await reader.Read(forkedTime);
//                }
//                finally
//                {
//                    forkedTime.Merge();
//                }
//            });

//            return new RetrieveResult<AudioReadPipe>()
//            {
//                Success = true,
//                Result = stream,
//                LatencyMs = timer.ElapsedMillisecondsPrecise()
//            };
//        }

//        public Task Store(string key, AudioReadPipe inStream, ILogger queryLogger, IRealTimeProvider realTime)
//        {
//            WriteStream writer = new WriteStream(key, inStream, _connectionPool, _parallelWriteThreadPool, queryLogger);
//            return writer.Write(realTime);
//        }

//        private class ReadStream
//        {
//            /// <summary>
//            /// A query logger
//            /// </summary>
//            private readonly ILogger _queryLogger;

//            /// <summary>
//            /// A pool of sql connections
//            /// </summary>
//            private readonly WeakPointer<MySqlConnectionPool> _connectionPool;

//            /// <summary>
//            /// The stream ID of the stream being fetched
//            /// </summary>
//            private readonly string _key;

//            /// <summary>
//            /// A dictionary mapping ordinal => binary data for chunks that we have read out-of-order
//            /// </summary>
//            private readonly Dictionary<int, byte[]> _outOfOrderChunks = new Dictionary<int, byte[]>();

//            /// <summary>
//            /// A thread pool to queue async write operations, in this case delete operations for rows that we have just read
//            /// </summary>
//            private readonly IThreadPool _threadPool;

//            private readonly bool _useCoproc;

//            /// <summary>
//            /// The ordinal of the final chunk in this stream
//            /// </summary>
//            private int _lastChunkIdx = int.MaxValue;

//            /// <summary>
//            /// The audio stream to output the data once we read it from the table
//            /// </summary>
//            private AudioWritePipe _audio;

//            private int _latestMissingChunkIdx = 0;
//            private int _latestReceivedChunkIdx = 0;
//            private bool _done;

//            public ReadStream(string key, WeakPointer<MySqlConnectionPool> connectionPool, ILogger queryLogger, IThreadPool writeThreadPool, bool useCoproc)
//            {
//                _key = key;
//                _connectionPool = connectionPool;
//                _queryLogger = queryLogger;
//                _threadPool = writeThreadPool;
//                _useCoproc = useCoproc;
//            }

//            public async Task<AudioReadPipe> GetOutputStream(IRealTimeProvider realTime, TimeSpan maxSpinTime)
//            {
//                PooledResource<MySqlConnection> currentConnection = null;
//                if (_connectionPool.Value.TryGetConnection(MAX_TIME_TO_GET_CONNECTION, ref currentConnection))
//                {
//                    try
//                    {
//                        // FIXME / OPT: This arrangement will buffer the entire stream before it even returns the reader.
//                        await ReadInternal(currentConnection.Value, realTime, (int)maxSpinTime.TotalMilliseconds, 4);
//                        return _audio.GetReadPipe();
//                    }
//                    catch (Exception e)
//                    {
//                        _queryLogger.Log(e, LogLevel.Err);
//                        return null;
//                    }
//                    finally
//                    {
//                        _connectionPool.Value.ReleaseConnection(currentConnection);
//                    }
//                }
//                else
//                {
//                    _queryLogger.Log("Failed to get a database connection to open audio stream", LogLevel.Err);
//                    return null;
//                }
//            }

//            private async Task ReadInternal(MySqlConnection connection, IRealTimeProvider realTime, int spinTimeMs = 500, int maxRows = 5)
//            {
//                // Are we already done? Then do nothing
//                if (_done)
//                {
//                    return;
//                }

//                DbDataReader reader = null;
//                if (_useCoproc)
//                {
//                    MySqlCommand command = connection.CreateCommand();
//                    command.CommandText = "stream_retrieve";
//                    command.CommandType = CommandType.StoredProcedure;
//                    MySqlParameter keyParam = new MySqlParameter("@p_id", MySqlDbType.VarChar, 48)
//                    {
//                        Direction = ParameterDirection.Input,
//                        Value = _key
//                    };
//                    MySqlParameter timeoutParam = new MySqlParameter("@p_timeout", MySqlDbType.Float)
//                    {
//                        Direction = ParameterDirection.Input,
//                        Value = (float)spinTimeMs / 1000f
//                    };
//                    MySqlParameter startOrdinalParam = new MySqlParameter("@p_startordinal", MySqlDbType.Int32)
//                    {
//                        Direction = ParameterDirection.Input,
//                        Value = _latestMissingChunkIdx
//                    };

//                    command.Parameters.Add(keyParam);
//                    command.Parameters.Add(timeoutParam);
//                    command.Parameters.Add(startOrdinalParam);
//                    reader = await command.ExecuteReaderAsync();
//                }
//                else
//                {
//                    MySqlCommand streamCommand = connection.CreateCommand();

//                    // Optimization: Calculate the set of out-of-order chunks that we have already received such that we never query the same chunk data twice
//                    string ordinalSubquery = string.Empty;
//                    if (_outOfOrderChunks.Count > 0)
//                    {
//                        ordinalSubquery = " AND Ordinal NOT IN (" + string.Join(",", _outOfOrderChunks.Keys) + ")";
//                    }

//                    streamCommand.CommandText = "SELECT Ordinal,IsEnd,Codec,CodecParams,SampleRate,Data FROM streams WHERE StreamId = @STREAMID AND Ordinal >= @MIN" + ordinalSubquery + " LIMIT @LIMIT;";
//                    streamCommand.Parameters.Add("STREAMID", MySqlDbType.VarChar).Value = _key;
//                    streamCommand.Parameters.Add("MIN", MySqlDbType.Int32).Value = _latestMissingChunkIdx;
//                    streamCommand.Parameters.Add("LIMIT", MySqlDbType.Int32).Value = maxRows;

//                    while (reader == null)
//                    {
//                        reader = await streamCommand.ExecuteReaderAsync();

//                        if (reader == null || !reader.HasRows)
//                        {
//                            if (reader != null)
//                            {
//                                reader.Dispose();
//                                reader = null;
//                            }

//                            //_queryLogger.Log("Spinning on read..." + spinTimeMs, LogLevel.Vrb);
//                            int amountToWait = Math.Min(Math.Max(1, spinTimeMs), SPINWAIT_DELAY);
//                            await realTime.WaitAsync(TimeSpan.FromMilliseconds(amountToWait), CancellationToken.None);
//                            spinTimeMs -= amountToWait;
//                        }

//                        if (spinTimeMs <= 0)
//                        {
//                            _queryLogger.Log("Timed out waiting for data from the streaming audio cache", LogLevel.Err);
//                            return;
//                        }
//                    }
//                }

//                if (reader != null)
//                {
//                    while (await reader.ReadAsync())
//                    {
//                        int ordinal = reader.GetInt32(0);
//                        bool isEnd = reader.GetBoolean(1);
//                        if (isEnd)
//                        {
//                            _lastChunkIdx = ordinal;
//                        }

//                        if (_audio == null)
//                        {
//                            string codecName = reader.GetString(2);
//                            string codecParams = reader.GetString(3);
//                            _audio = new EncodedAudioPassthroughPipe(codecName, codecParams);
//                        }

//                        _latestReceivedChunkIdx = Math.Max(_latestReceivedChunkIdx, ordinal);
//                        //_queryLogger.Log("Read chunk #" + ordinal + " from sql", LogLevel.Vrb);

//                        if (!_outOfOrderChunks.ContainsKey(ordinal))
//                        {
//                            // Read the data only if we don't have this chunk yet
//                            object dataColumn = reader.GetValue(5);
//                            if (dataColumn != null && dataColumn is byte[])
//                            {
//                                byte[] blob = (byte[])dataColumn;
//                                // Is this blob simply the next one to send? Then don't bother even queueing it
//                                if (ordinal == _latestMissingChunkIdx)
//                                {
//                                    _audio.Write(blob, 0, blob.Length);
//                                    //_queryLogger.Log("Wrote chunk #" + _latestMissingChunkIdx + " to pipe", LogLevel.Vrb);
//                                    _latestMissingChunkIdx++;
//                                }
//                                else
//                                {
//                                    _outOfOrderChunks.Add(ordinal, blob);
//                                }
//                            }
//                            else
//                            {
//                                throw new InvalidCastException("Contents of \"Data\" SQL column not in expected binary format!");
//                            }
//                        }
//                    }

//                    reader.Dispose();

//                    // Process out-of-order chunks to see if we can send any to the write pipe
//                    while (_latestMissingChunkIdx <= _latestReceivedChunkIdx && _outOfOrderChunks.ContainsKey(_latestMissingChunkIdx))
//                    {
//                        byte[] nextInOrderChunk = _outOfOrderChunks[_latestMissingChunkIdx];
//                        _audio.Write(nextInOrderChunk, 0, nextInOrderChunk.Length);
//                        _outOfOrderChunks.Remove(_latestMissingChunkIdx);
//                        //_queryLogger.Log("Wrote chunk #" + _latestMissingChunkIdx + " to pipe", LogLevel.Vrb);
//                        _latestMissingChunkIdx++;
//                    }
//                }

//                // Do we need to close the stream?
//                if (_audio != null && _latestMissingChunkIdx >= _lastChunkIdx)
//                {
//                    _queryLogger.Log("Stream read finished", LogLevel.Vrb);
//                    _audio.CloseWrite();
//                    _done = true;
//                }
//            }

//            public async Task Read(IRealTimeProvider realTime)
//            {
//                if (_audio == null)
//                {
//                    throw new InvalidOperationException("You must successfully call GetOutputStream() before you can read from this stream");
//                }

//                PooledResource<MySqlConnection> currentConnection = null;
//                if (_connectionPool.Value.TryGetConnection(MAX_TIME_TO_GET_CONNECTION, ref currentConnection))
//                {
//                    try
//                    {
//                        Stopwatch totalTimer = Stopwatch.StartNew();
//                        while (!_done)
//                        {
//                            await ReadInternal(currentConnection.Value, realTime, 500);
//                            if (totalTimer.ElapsedMilliseconds > 20000)
//                            {
//                                _queryLogger.Log("Deadlock or timeout occurred in streaming audio cache. Aborting read", LogLevel.Err);
//                                _audio.CloseWrite();
//                                _done = true;
//                            }
//                        }
//                    }
//                    catch (Exception e)
//                    {
//                        _queryLogger.Log(e, LogLevel.Err);
//                    }
//                    finally
//                    {
//                        _connectionPool.Value.ReleaseConnection(currentConnection);
//                    }
//                }
//                else
//                {
//                    _queryLogger.Log("Failed to get a database connection to read audio stream", LogLevel.Err);
//                }

//                // Send a fire-and-forget message to delete the rows that we just read
//                _threadPool.EnqueueUserAsyncWorkItem(async () =>
//                {
//                    PooledResource<MySqlConnection> deleteConnection = null;
//                    if (_connectionPool.Value.TryGetConnection(MAX_TIME_TO_GET_CONNECTION, ref deleteConnection))
//                    {
//                        try
//                        {
//                            MySqlCommand deleteCommand = deleteConnection.Value.CreateCommand();
//                            deleteCommand.CommandText = "DELETE FROM streams WHERE StreamId = @STREAMID;";
//                            deleteCommand.Parameters.Add("STREAMID", MySqlDbType.VarChar).Value = _key;
//                            await deleteCommand.ExecuteScalarAsync();
//                        }
//                        catch (Exception e)
//                        {
//                            _queryLogger.Log(e, LogLevel.Err);
//                        }
//                        finally
//                        {
//                            _connectionPool.Value.ReleaseConnection(currentConnection);
//                        }
//                    }
//                });
//            }
//        }

//        private class WriteStream
//        {
//            private AudioReadPipe _audio;
//            private ILogger _queryLogger;
//            private WeakPointer<MySqlConnectionPool> _connectionPool;
//            private string _key;
//            private IThreadPool _writeThreadPool;

//            public WriteStream(string key, AudioReadPipe audio, WeakPointer<MySqlConnectionPool> connectionPool, IThreadPool writeThreadPool, ILogger queryLogger)
//            {
//                _key = key;
//                _audio = audio;
//                _connectionPool = connectionPool;
//                _queryLogger = queryLogger;
//                _writeThreadPool = writeThreadPool;
//            }

//            private async Task WriteInternal(byte[] buffer, int chunkIdx, string codec, string codecParams, bool endOfStream)
//            {
//                PooledResource<MySqlConnection> currentConnection = null;
//                if (_connectionPool.Value.TryGetConnection(MAX_TIME_TO_GET_CONNECTION, ref currentConnection))
//                {
//                    try
//                    {
//                        MySqlCommand streamCommand = currentConnection.Value.CreateCommand();
//                        streamCommand.CommandText = "INSERT INTO streams(StreamId, Ordinal, IsEnd, WriteTime, Codec, CodecParams, SampleRate, Data) " +
//                            "VALUES(@STREAMID, @ORDINAL, @DONE, UTC_TIMESTAMP(), @CODEC, @CODECPARAMS, @SAMPLERATE, @DATA);";
//                        streamCommand.Parameters.Add("STREAMID", MySqlDbType.VarChar).Value = _key;
//                        streamCommand.Parameters.Add("ORDINAL", MySqlDbType.Int32).Value = chunkIdx;
//                        streamCommand.Parameters.Add("DONE", MySqlDbType.Bit).Value = endOfStream;
//                        streamCommand.Parameters.Add("CODEC", MySqlDbType.VarChar).Value = codec;
//                        streamCommand.Parameters.Add("CODECPARAMS", MySqlDbType.VarChar).Value = codecParams;
//                        streamCommand.Parameters.Add("SAMPLERATE", MySqlDbType.Int32).Value = 0;
//                        streamCommand.Parameters.Add("DATA", MySqlDbType.VarBinary).Value = buffer;
//                        //_queryLogger.Log("Writing StreamId " + _key + " Ordinal " + chunkIdx + " IsEnd " + endOfStream + " Length " + buffer.Length + " bytes", LogLevel.Vrb);
//                        await streamCommand.ExecuteNonQueryAsync();
//                        //_queryLogger.Log("Successfully wrote StreamId " + _key + " Ordinal " + chunkIdx + " IsEnd " + endOfStream + " Length " + buffer.Length + " bytes", LogLevel.Vrb);
//                    }
//                    catch (Exception e)
//                    {
//                        _queryLogger.Log(e, LogLevel.Err);
//                    }
//                    finally
//                    {
//                        _connectionPool.Value.ReleaseConnection(currentConnection);
//                    }
//                }
//                else
//                {
//                    _queryLogger.Log("Failed to get a database connection to write streaming audio", LogLevel.Err);
//                }
//            }

//            public async Task Write(IRealTimeProvider realTime)
//            {
//                //_logger.Log("Opening write stream");
//                string codec = _audio.GetCodec() ;
//                string codecParams = _audio.GetCodecParams();
//                // Read audio in chunks and convert to database blobs
//                byte[] chunk = new byte[CHUNK_SIZE];
//                int curChunkSize = 0;
//                int chunkIdx = 0;
//                int chunksInProgress = 0;
//                Stopwatch totalTimeWaited = Stopwatch.StartNew();

//                // We have to queue our work item data to a concurrent queue to prevent issues with overlapping closure memory (using an incrementer within a closure, in this case chunkIdx)
//                ConcurrentQueue<Tuple<byte[], int, bool>> writeIngestionQueue = new ConcurrentQueue<Tuple<byte[], int, bool>>();

//                int bytesRead = -1;
//                while (bytesRead != 0)
//                {
//                    bytesRead = await _audio.ReadAsync(chunk, curChunkSize, CHUNK_SIZE - curChunkSize);
//                    //if (bytesRead == 0)
//                    //{
//                    //    // If no audio is immediately available, yield for a little bit.
//                    //    // This prevents us from wasting spin cycles when this task is on the critical path
//                    //    await realTime.WaitAsync(SPINWAIT_TIME, CancellationToken.None);

//                    //    if (totalTimeWaited.ElapsedMilliseconds > 20000)
//                    //    {
//                    //        _queryLogger.Log("Timeout or deadlock occurred while writing SQL audio stream", LogLevel.Err);
//                    //        return;
//                    //    }
//                    //}
//                    //else
//                    if (bytesRead > 0)
//                    {
//                        curChunkSize += bytesRead;
//                        if (curChunkSize == CHUNK_SIZE)
//                        {
//                            byte[] newBuf = new byte[CHUNK_SIZE];
//                            ArrayExtensions.MemCopy(chunk, 0, newBuf, 0, CHUNK_SIZE);
//                            writeIngestionQueue.Enqueue(new Tuple<byte[], int, bool>(newBuf, chunkIdx++, false));

//                            Interlocked.Increment(ref chunksInProgress);
//                            _writeThreadPool.EnqueueUserAsyncWorkItem(async () =>
//                            {
//                                try
//                                {
//                                    Tuple<byte[], int, bool> workItem;
//                                    if (writeIngestionQueue.TryDequeue(out workItem))
//                                    {
//                                        await WriteInternal(workItem.Item1, workItem.Item2, codec, codecParams, workItem.Item3);
//                                    }
//                                    else
//                                    {
//                                        _queryLogger.Log("Couldn't get streaming audio chunk for writing", LogLevel.Wrn);
//                                    }
//                                }
//                                finally
//                                {
//                                    Interlocked.Decrement(ref chunksInProgress);
//                                }
//                            });
//                            curChunkSize = 0;
//                        }
//                    }
//                }

//                byte[] remainder = new byte[curChunkSize];
//                if (curChunkSize > 0)
//                {
//                    ArrayExtensions.MemCopy(chunk, 0, remainder, 0, curChunkSize);
//                }

//                // Finish the stream
//                writeIngestionQueue.Enqueue(new Tuple<byte[], int, bool>(remainder, chunkIdx++, true));
//                Interlocked.Increment(ref chunksInProgress);
//                _writeThreadPool.EnqueueUserAsyncWorkItem(async () =>
//                {
//                    try
//                    {
//                        Tuple<byte[], int, bool> workItem;
//                        if (writeIngestionQueue.TryDequeue(out workItem))
//                        {
//                            await WriteInternal(workItem.Item1, workItem.Item2, codec, codecParams, workItem.Item3);
//                        }
//                        else
//                        {
//                            _queryLogger.Log("Couldn't get streaming audio chunk for writing", LogLevel.Wrn);
//                        }
//                    }
//                    finally
//                    {
//                        Interlocked.Decrement(ref chunksInProgress);
//                    }
//                });

//                while (chunksInProgress > 0)
//                {
//                    await realTime.WaitAsync(SPINWAIT_TIME, CancellationToken.None);
//                    if (totalTimeWaited.ElapsedMilliseconds > 20000)
//                    {
//                        _queryLogger.Log("Timeout or deadlock occurred while finishing SQL audio stream", LogLevel.Err);
//                        return;
//                    }
//                }
//            }
//        }

//        protected override IEnumerable<MySqlTableDefinition> Tables =>
//            new List<MySqlTableDefinition>()
//            {
//                new MySqlTableDefinition()
//                {
//                    TableName = TABLE_NAME,
//                    CreateStatement = string.Format(
//                        "CREATE TABLE `{0}` (\r\n" +
//                        "  `Id` int(11) NOT NULL AUTO_INCREMENT,\r\n" +
//                        "  `StreamId` varchar(255) NOT NULL,\r\n" +
//                        "  `Ordinal` int(11) NOT NULL,\r\n" +
//                        "  `IsEnd` tinyint(1) NOT NULL,\r\n" +
//                        "  `WriteTime` datetime NOT NULL,\r\n" +
//                        "  `Codec` varchar(255) DEFAULT NULL,\r\n" +
//                        "  `CodecParams` varchar(4000) DEFAULT NULL,\r\n" +
//                        "  `SampleRate` int(11) DEFAULT NULL,\r\n" +
//                        "  `Data` varbinary({1}),\r\n" +
//                        "  PRIMARY KEY (`Id`),\r\n" +
//                        "  KEY `StreamIdx` (`StreamId`)\r\n" +
//                        ") ENGINE=MEMORY AUTO_INCREMENT=10 DEFAULT CHARSET=utf8;\r\n"
//                        , TABLE_NAME, CHUNK_SIZE)
//                }
//            };

//        protected override IEnumerable<MySqlProcedureDefinition> Procedures =>
//            new List<MySqlProcedureDefinition>()
//            {
//                new MySqlProcedureDefinition()
//                {
//                    ProcedureName = "stream_retrieve",
//                    CreateStatement = string.Format(
//                        "CREATE PROCEDURE `stream_retrieve`( \r\n" +
//                        "	 IN p_id VARCHAR(48), \r\n" +
//                        "    IN p_timeout FLOAT, \r\n" +
//                        "    IN p_startordinal INT) \r\n" +
//                        "BEGIN \r\n" +
//                        "    DECLARE v_found BOOLEAN; \r\n" +
//                        "    DECLARE v_backoff_time FLOAT; \r\n" +
//                        "    SET v_found = false; \r\n" +
//                        "    SET v_backoff_time = 0.002; \r\n" +
//                        "    WHILE v_found = false AND p_timeout >= 0 DO \r\n" +
//                        "        # Make sure the row exists first. If not, sleep and retry \r\n" +
//                        "		SELECT EXISTS (SELECT * FROM {0} WHERE StreamId = p_id AND Ordinal >= p_startordinal) INTO v_found; \r\n" +
//                        "		IF v_found THEN \r\n" +
//                        "           SELECT `Ordinal`,`IsEnd`,`Codec`,`CodecParams`,`SampleRate`,`Data` FROM {0} WHERE StreamId = p_id AND Ordinal >= p_startordinal; \r\n" +
//                        "		ELSEIF p_timeout >= 0 THEN \r\n" +
//                        "            DO SLEEP(v_backoff_time); \r\n" +
//                        "            SET p_timeout = p_timeout - v_backoff_time; \r\n" +
//                        "            # Increase the time waited by 2ms with each successive cache miss, to make spinning less costly for long waits \r\n" +
//                        "            SET v_backoff_time = v_backoff_time + 0.002; \r\n" +
//                        "		END IF; \r\n" +
//                        "	END WHILE; \r\n" +
//                        "END\r\n",
//                        TABLE_NAME)
//                }
//            };

//        protected override IEnumerable<MySqlEventDefinition> Events =>
//            new List<MySqlEventDefinition>()
//            {
//                new MySqlEventDefinition()
//                {
//                    EventName = "streams_cleanup",
//                    CreateStatement = string.Format(
//                        "CREATE EVENT `streams_cleanup`\r\n" +
//                        "ON SCHEDULE EVERY 1 MINUTE DO\r\n" +
//                        "DELETE FROM {0} WHERE WriteTime < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 1 MINUTE)",
//                        TABLE_NAME)
//                }
//            };
//    }
//}
