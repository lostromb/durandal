using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Cache;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.MySql
{
    /// <summary>
    /// An implementation of ICache backed by a MySql database.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MySqlCache<T> : MySqlDataSource, ICache<T> where T : class
    {
        private readonly TimeSpan MAX_TIME_TO_GET_CONNECTION = TimeSpan.FromSeconds(5);
        private const string TABLE_NAME = "cache";
        private readonly WeakPointer<MySqlConnectionPool> _connectionPool;
        private readonly ILogger _logger;
        private readonly IByteConverter<T> _serializer;
        private int _disposed = 0;
        
        public MySqlCache(
            MySqlConnectionPool connectionPool,
            IByteConverter<T> serializer,
            ILogger logger)
                : base(connectionPool, logger)
        {
            _logger = logger;
            _serializer = serializer;
            _connectionPool = new WeakPointer<MySqlConnectionPool>(connectionPool);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~MySqlCache()
        {
            Dispose(false);
        }
#endif

        public async Task Delete(IList<string> keys, bool fireAndForget, ILogger queryLogger)
        {
            if (fireAndForget)
            {
                DeleteMultipleInternalAsync(keys, queryLogger).Forget(queryLogger);
            }
            else
            {
                await DeleteMultipleInternalAsync(keys, queryLogger);
            }
        }

        public async Task Delete(string key, bool fireAndForget, ILogger queryLogger)
        {
            if (fireAndForget)
            {
                DeleteInternalAsync(key, queryLogger).Forget(queryLogger);
            }
            else
            {
                await DeleteInternalAsync(key, queryLogger);
            }
        }

        private async Task DeleteInternalAsync(string key, ILogger queryLogger)
        {
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("DELETE FROM {0} WHERE Id = \'{1}\';", TABLE_NAME, key);
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception e)
                {
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                queryLogger.Log("FAILED to get MySql connection", LogLevel.Err);
            }
        }

        private async Task DeleteMultipleInternalAsync(IList<string> keys, ILogger queryLogger)
        {
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                    {
                        StringBuilder commandBuilder = pooledSb.Builder;
                        commandBuilder.Append("DELETE FROM `");
                        commandBuilder.Append(TABLE_NAME);
                        commandBuilder.Append("` WHERE Id IN (");
                        bool first = true;
                        foreach (string key in keys)
                        {
                            if (first)
                            {
                                commandBuilder.Append("\'");
                                first = false;
                            }
                            else
                            {
                                commandBuilder.Append(",\'");
                            }
                            commandBuilder.Append(key);
                            commandBuilder.Append("\'");
                        }

                        commandBuilder.Append(");");
                        command.CommandText = commandBuilder.ToString();
                        await command.ExecuteNonQueryAsync();
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                queryLogger.Log("FAILED to get MySql connection", LogLevel.Err);
            }
        }

        public Task Store(string key, T item, DateTimeOffset? expireTime, TimeSpan? lifetime, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime)
        {
            CachedItem<T> convertedItem = new CachedItem<T>(key, item, lifetime, expireTime);
            return Store(convertedItem, fireAndForget, queryLogger, realTime);
        }

        public async Task Store(CachedItem<T> item, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime)
        {
            if (fireAndForget)
            {
                StoreInternalAsync(item, queryLogger, realTime).Forget(queryLogger);
            }
            else
            {
                await StoreInternalAsync(item, queryLogger, realTime);
            }
        }

        public async Task Store(IList<CachedItem<T>> items, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime)
        {
            if (fireAndForget)
            {
                StoreMultipleInternalAsync(items, queryLogger, realTime).Forget(queryLogger);
            }
            else
            {
                await StoreMultipleInternalAsync(items, queryLogger, realTime);
            }
        }

        private async Task StoreInternalAsync(CachedItem<T> item, ILogger queryLogger, IRealTimeProvider realTime)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    // If expire time is not set but lifetime is, calculate the expire time from that
                    if (!item.ExpireTime.HasValue && item.LifeTime.HasValue)
                    {
                        item.ExpireTime = realTime.Time + item.LifeTime.Value;
                    }

                    byte[] blob = _serializer.Encode(item.Item);
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("INSERT INTO {0}(Id, Value, ExpireTime, LifetimeSeconds, TraceId) VALUES(\'{1}\', 0x{2}, {3}, {4}, {5}) " +
                         "ON DUPLICATE KEY UPDATE Value=VALUES(Value),ExpireTime=VALUES(ExpireTime),LifetimeSeconds=VALUES(LifetimeSeconds),TraceId=VALUES(TraceId);",
                         TABLE_NAME,
                         item.Key,
                         BinaryHelpers.ToHexString(blob),
                         item.ExpireTime.HasValue ? item.ExpireTime.Value.ToUniversalTime().ToString("\\'yyyy-MM-ddTHH:mm:ss\\'") : "null",
                         item.LifeTime.HasValue ? item.LifeTime.Value.TotalSeconds.ToString() : "null",
                         queryLogger.TraceId.HasValue ? "\'" + CommonInstrumentation.FormatTraceId(queryLogger.TraceId) + "\'" : "null");

                    await command.ExecuteNonQueryAsync();
                    timer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_Store_CacheWrite, item.Key, ref timer), LogLevel.Ins);
                }
                catch (Exception e)
                {
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                queryLogger.Log("FAILED to get MySql connection", LogLevel.Err);
            }
        }

        private async Task StoreMultipleInternalAsync(IList<CachedItem<T>> items, ILogger queryLogger, IRealTimeProvider realTime)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    const int CACHE_BATCH_SIZE = 100;
                    // Write items to cache in batches of at most 100 rows each
                    int itemIdx = 0;
                    using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                    {
                        StringBuilder commandBuilder = pooledSb.Builder;
                        while (itemIdx < items.Count)
                        {
                            commandBuilder.Append("INSERT INTO ");
                            commandBuilder.Append(TABLE_NAME);
                            commandBuilder.Append("(Id, Value, ExpireTime, LifetimeSeconds, TraceId) VALUES");
                            int thisBatchSize = Math.Min(items.Count - itemIdx, CACHE_BATCH_SIZE);
                            for (int c = 0; c < thisBatchSize; c++)
                            {
                                CachedItem<T> item = items[itemIdx + c];

                                // If expire time is not set but lifetime is, calculate the expire time from that
                                if (!item.ExpireTime.HasValue && item.LifeTime.HasValue)
                                {
                                    item.ExpireTime = realTime.Time + item.LifeTime.Value;
                                }

                                byte[] blob = _serializer.Encode(item.Item);
                                if (c == 0)
                                {
                                    commandBuilder.Append("(\'");
                                }
                                else
                                {
                                    commandBuilder.Append(",\r\n(\'");
                                }
                                commandBuilder.Append(item.Key);
                                commandBuilder.Append("\',0x");
                                commandBuilder.Append(BinaryHelpers.ToHexString(blob));
                                commandBuilder.Append(",");
                                commandBuilder.Append(item.ExpireTime.HasValue ? item.ExpireTime.Value.ToUniversalTime().ToString("\\'yyyy-MM-ddTHH:mm:ss\\'") : "null");
                                commandBuilder.Append(",");
                                commandBuilder.Append(item.LifeTime.HasValue ? item.LifeTime.Value.TotalSeconds.ToString() : "null");
                                if (queryLogger.TraceId.HasValue)
                                {
                                    commandBuilder.Append(",\'");
                                    commandBuilder.Append(CommonInstrumentation.FormatTraceId(queryLogger.TraceId));
                                    commandBuilder.Append("\')");
                                }
                                else
                                {
                                    commandBuilder.Append(",null)");
                                }
                            }

                            commandBuilder.Append(" ON DUPLICATE KEY UPDATE Value=VALUES(Value),ExpireTime=VALUES(ExpireTime),LifetimeSeconds=VALUES(LifetimeSeconds),TraceId=VALUES(TraceId);");
                            MySqlCommand command = connection.Value.CreateCommand();
                            command.CommandText = commandBuilder.ToString();

                            await command.ExecuteNonQueryAsync();

                            timer.Stop();
                            for (int c = 0; c < thisBatchSize; c++)
                            {
                                CachedItem<T> item = items[itemIdx + c];
                                queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_Store_CacheWrite, item.Key, ref timer), LogLevel.Ins);
                            }

                            timer.Restart();
                            commandBuilder.Clear();
                            itemIdx += thisBatchSize;
                        }
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                queryLogger.Log("FAILED to get MySql connection", LogLevel.Err);
            }
        }

        public async Task<RetrieveResult<T>> TryRetrieve(string key, ILogger queryLogger, IRealTimeProvider realTime, TimeSpan? maxSpinTime = null)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "cache_retrieve";
                    command.CommandType = CommandType.StoredProcedure;
                    MySqlParameter keyParam = new MySqlParameter("@p_id", MySqlDbType.VarChar, 48)
                        {
                            Direction = ParameterDirection.Input,
                            Value = key
                        };
                    MySqlParameter timeoutParam = new MySqlParameter("@p_timeout", MySqlDbType.Float)
                        {
                            Direction = ParameterDirection.Input,
                            Value = (float)maxSpinTime.GetValueOrDefault(TimeSpan.Zero).TotalSeconds
                        };
                    MySqlParameter returnValParam = new MySqlParameter("@p_returnVal", MySqlDbType.MediumBlob)
                        {
                            Direction = ParameterDirection.Output
                        };
                    command.Parameters.Add(keyParam);
                    command.Parameters.Add(timeoutParam);
                    command.Parameters.Add(returnValParam);
                    await command.ExecuteScalarAsync();
                    if (returnValParam.Value != null && returnValParam.Value is byte[])
                    {
                        byte[] readBlob = (byte[])returnValParam.Value;
                        T returnVal = _serializer.Decode(readBlob, 0, readBlob.Length);
                        timer.Stop();
                        return new RetrieveResult<T>(returnVal, timer.ElapsedMillisecondsPrecise());
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                queryLogger.Log("FAILED to get MySql connection", LogLevel.Err);
            }

            timer.Stop();
            return new RetrieveResult<T>(default(T), timer.ElapsedMillisecondsPrecise(), false);
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
                // TODO dispose of the internal thread pool if we created our own
            }
        }

        protected override IEnumerable<MySqlTableDefinition> Tables =>
            new List<MySqlTableDefinition>()
            {
                new MySqlTableDefinition()
                {
                    TableName = TABLE_NAME,
                    CreateStatement = string.Format(
                        "CREATE TABLE `{0}` (\r\n" +
                        "  `Id` varchar(48) NOT NULL,\r\n" +
                        "  `Value` mediumblob,\r\n" +
                        "  `ExpireTime` datetime DEFAULT NULL,\r\n" +
                        "  `LifetimeSeconds` int(11) DEFAULT NULL,\r\n" +
                        "  `TraceId` varchar(48) DEFAULT NULL,\r\n" +
                        "  PRIMARY KEY(`Id`),\r\n" +
                        "  UNIQUE KEY `Id` (`Id`),\r\n" +
                        "  KEY `TraceId` (`TraceId`)\r\n" +
                        ") ENGINE = InnoDB DEFAULT CHARSET = utf8;\r\n"
                        , TABLE_NAME)
                }
            };

        protected override IEnumerable<MySqlProcedureDefinition> Procedures =>
            new List<MySqlProcedureDefinition>()
            {
                new MySqlProcedureDefinition()
                {
                    ProcedureName = "cache_retrieve",
                    CreateStatement = string.Format(
                        "CREATE PROCEDURE `cache_retrieve`(\r\n" +
                        "    IN p_id VARCHAR(48),\r\n" +
                        "    IN p_timeout FLOAT,\r\n" +
                        "    OUT p_returnVal MEDIUMBLOB)\r\n" +
                        "BEGIN\r\n" +
                        "    DECLARE v_lifeTime INT;\r\n" +
                        "    DECLARE v_expiresAt DATETIME;\r\n" +
                        "    DECLARE v_rowsFound INT;\r\n" +
                        "    DECLARE v_retryCount INT;\r\n" +
                        "    DECLARE v_backoffTime FLOAT;\r\n" +
                        "    DECLARE v_newExpireTime DATETIME;\r\n" +
                        "    SET p_returnVal = NULL;\r\n" +
                        "    SET v_rowsFound = 0;\r\n" +
                        "    SET v_backoffTime = 0.002;\r\n" +
                        "    WHILE v_rowsFound = 0 AND p_timeout >= 0 DO\r\n" +
                        "        # Try to select the row\r\n" +
                        "        SELECT count(Id),ANY_VALUE(Value),ANY_VALUE(ExpireTime),ANY_VALUE(LifetimeSeconds) INTO v_rowsFound,p_returnVal,v_expiresAt,v_lifeTime FROM `{0}` WHERE Id = p_id LIMIT 1;\r\n" +
                        "        IF v_rowsFound > 0 THEN\r\n" +
                        "            # Found an entry. Has it expired?\r\n" +
                        "            IF v_expiresAt IS NULL OR v_expiresAt > UTC_TIMESTAMP() THEN\r\n" +
                        "                # Still valid. Check if it has a TTL and we need to touch it\r\n" +
                        "                IF v_lifeTime IS NOT NULL THEN\r\n" +
                        "                    SET v_newExpireTime = UTC_TIMESTAMP() + INTERVAL v_lifeTime SECOND;\r\n" +
                        "                    # Make sure we don't shorten the expire time\r\n" +
                        "                    IF v_expiresAt < v_newExpireTime THEN\r\n" +
                        "                        UPDATE `{0}` SET ExpireTime=v_newExpireTime WHERE Id = p_id;\r\n" +
                        "                    END IF;\r\n" +
                        "                END IF;\r\n" +
                        "            ELSE\r\n" +
                        "                SET p_returnVal = NULL;\r\n" +
                        "            END IF;\r\n" +
                        "        ELSEIF p_timeout >= 0 THEN\r\n" +
                        "            # Row does not exist; spinwait\r\n" +
                        "            DO SLEEP(v_backoffTime);\r\n" +
                        "            SET p_timeout = p_timeout - v_backoffTime;\r\n" +
                        "            # Increase the timeout by 2ms on each successive cache miss to reduce the spinning cost on long waits\r\n" +
                        "            SET v_backoffTime = v_backoffTime + 0.002;\r\n" +
                        "        END IF;\r\n" +
                        "    END WHILE;\r\n" +
                        "END\r\n",
                        TABLE_NAME)
                }
            };

        protected override IEnumerable<MySqlEventDefinition> Events =>
            new List<MySqlEventDefinition>()
            {
                new MySqlEventDefinition()
                {
                    EventName = "cache_cleanup",
                    CreateStatement = string.Format(
                        "CREATE EVENT `cache_cleanup`\r\n" +
                        "ON SCHEDULE EVERY 1 HOUR DO\r\n" +
                        "DELETE FROM `{0}` WHERE ExpireTime < UTC_TIMESTAMP()",
                        TABLE_NAME)
                }
            };
    }
}
