using Durandal.API;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.MySql
{
    public class MySqlInstrumentation : MySqlDataSource, IInstrumentationRepository
    {
        private readonly TimeSpan MAX_TIME_TO_GET_CONNECTION = TimeSpan.FromMilliseconds(5000);
        private const string TABLE_NAME = "instrumentation";
        private readonly WeakPointer<MySqlConnectionPool> _connectionPool;
        private ILogger _logger;
        private IByteConverter<InstrumentationEventList> _instrumentationSerializer;

        public MySqlInstrumentation(
            MySqlConnectionPool connectionPool,
            ILogger logger,
            IByteConverter<InstrumentationEventList> instrumentationSerializer)
            : base(connectionPool, logger)
        {
            _logger = logger;
            _connectionPool = new WeakPointer<MySqlConnectionPool>(connectionPool);
            _instrumentationSerializer = instrumentationSerializer;
        }

        /// <summary>
        /// Updates a specific client's information (or one row of the database)
        /// </summary>
        /// <param name="traceInfo">A complete trace</param>
        /// <returns></returns>
        public async Task<bool> WriteTraceData(UnifiedTrace traceInfo)
        {
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    // Create the blob objects first
                    byte[] compressedLogs = null;
                    if (traceInfo.LogEvents != null || traceInfo.LogEvents.Count > 0)
                    {
                        InstrumentationBlob logEventBlob = new InstrumentationBlob();
                        logEventBlob.AddEvents(traceInfo.LogEvents);
                        compressedLogs = logEventBlob.Compress(_instrumentationSerializer);
                    }

                    const string commandText = "INSERT INTO " + TABLE_NAME + " (TraceId, TraceStart, TraceEnd, LogBlob, ImportedToAppInsights) " +
                        "VALUES(@TraceId, @TraceStart, @TraceEnd, @LogBlob, 0) " +
                        "ON DUPLICATE KEY UPDATE TraceStart=VALUES(TraceStart),TraceEnd=VALUES(TraceEnd),LogBlob=VALUES(LogBlob);";
                    
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = commandText;
                    command.Parameters.Add(new MySqlParameter("@TraceId", CommonInstrumentation.FormatTraceId(traceInfo.TraceId)));
                    command.Parameters.Add(new MySqlParameter("@TraceStart", traceInfo.TraceStart.UtcDateTime));
                    command.Parameters.Add(new MySqlParameter("@TraceEnd", traceInfo.TraceEnd.UtcDateTime));
                    command.Parameters.Add(new MySqlParameter("@LogBlob", compressedLogs));
                    int rows = await command.ExecuteNonQueryAsync();
                    return rows != 0;
                }
                catch (Exception e)
                {
                    _logger.Log("Error while updating trace information", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    return false;
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }

            return false;
        }

        /// <summary>
        /// Reads a complete instrumentation event from the database
        /// </summary>
        /// <returns></returns>
        public async Task<UnifiedTrace> GetTraceData(Guid traceId, IStringDecrypterPii piiDecrypter)
        {
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("SELECT LogBlob FROM " + TABLE_NAME + " WHERE TraceId = \'{0}\';", CommonInstrumentation.FormatTraceId(traceId));
                    using (DbDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            // Return the first row that matches
                            byte[] logEventBlob = reader.GetFieldValue<byte[]>(0);
                            InstrumentationBlob logEvents = InstrumentationBlob.Decompress(logEventBlob, _instrumentationSerializer);
                            UnifiedTrace returnVal = UnifiedTrace.CreateFromLogData(traceId, logEvents.GetEvents(), _logger, piiDecrypter);
                            return returnVal;
                        }
                    }

                    _logger.Log("No entries were found in the instrumentation table for the traceid " + traceId, LogLevel.Vrb);
                }
                catch (Exception e)
                {
                    _logger.Log("Error while retrieving recent trace information", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }

            return null;
        }

        /// <summary>
        /// Fetches many traces in parallel
        /// </summary>
        /// <param name="traceIds"></param>
        /// <param name="piiDecrypter">A decrypter for encrypted log messages</param>
        /// <returns></returns>
        public async Task<IList<UnifiedTrace>> GetTraceData(IEnumerable<Guid> traceIds, IStringDecrypterPii piiDecrypter)
        {
            List<UnifiedTrace> returnVal = new List<UnifiedTrace>();

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
                        commandBuilder.Append("SELECT TraceId, LogBlob FROM " + TABLE_NAME + " WHERE TraceId IN (");
                        bool first = true;
                        foreach (Guid guid in traceIds)
                        {
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                commandBuilder.Append(",");
                            }

                            commandBuilder.Append('\'');
                            commandBuilder.Append(CommonInstrumentation.FormatTraceId(guid));
                            commandBuilder.Append('\'');
                        }

                        commandBuilder.Append(");");

                        command.CommandText = commandBuilder.ToString();

                        using (DbDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string traceId = reader.GetString(0);
                                byte[] logEventBlob = reader.GetFieldValue<byte[]>(1);
                                InstrumentationBlob logEvents = InstrumentationBlob.Decompress(logEventBlob, _instrumentationSerializer);
                                UnifiedTrace thisTrace = UnifiedTrace.CreateFromLogData(Guid.Parse(traceId), logEvents.GetEvents(), _logger, piiDecrypter);
                                returnVal.Add(thisTrace);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Log("Error while retrieving recent trace information", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Reads the set of all traceIds that have not been processed (i.e. they are entirely in the transient logs table)
        /// </summary>
        /// <returns></returns>
        public async Task<ISet<Guid>> GetUnprocessedTraceIds(int limit = 100)
        {
            HashSet<Guid> returnVal = new HashSet<Guid>();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("SELECT TraceId FROM logs WHERE TraceId IS NOT NULL AND TraceId <> \"null\" GROUP BY TraceId LIMIT {0};", limit);
                    using (DbDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            returnVal.Add(Guid.Parse(reader.GetString(0)));
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Log("Error while retrieving recent trace information", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Gets a list of processed traceids (that is, ones with data already in the instrumentation table)
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public async Task<ISet<Guid>> GetProcessedTraceIds(DateTimeOffset startTime, DateTimeOffset endTime)
        {
            HashSet<Guid> returnVal = new HashSet<Guid>();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "SELECT TraceId FROM " + TABLE_NAME + " WHERE TraceEnd > @StartTime AND TraceEnd < @EndTime;";
                    command.Parameters.Add(new MySqlParameter("@StartTime", startTime));
                    command.Parameters.Add(new MySqlParameter("@EndTime", endTime));
                    using (DbDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            returnVal.Add(Guid.Parse(reader.GetString(0)));
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Log("Error while retrieving recent trace information", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Deletes all logs from the temporary logs table with the given traceid
        /// </summary>
        /// <param name="traceId"></param>
        public async Task DeleteLogs(Guid traceId)
        {
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("DELETE FROM logs WHERE TraceId = \'{0}\';", CommonInstrumentation.FormatTraceId(traceId));
                    await command.ExecuteNonQueryAsync();
                    _logger.Log("Deleted logs for trace " + traceId, LogLevel.Vrb);
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }
        }

        /// <summary>
        /// Reads the set of all traceIds that have not been uploaded to appinsights
        /// </summary>
        /// <returns></returns>
        public async Task<ISet<Guid>> GetTraceIdsNotImportedToAppInsights(int limit = 100)
        {
            HashSet<Guid> returnVal = new HashSet<Guid>();
            limit = Math.Min(200, limit); // Enforce a max limit on batch size as we will have to send these all back in a giant IN() contraint later on
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("SELECT TraceId FROM " + TABLE_NAME + " WHERE (ImportedToAppInsights IS NULL OR ImportedToAppInsights = 0) " +
                        "AND TraceEnd < utc_timestamp() - INTERVAL 10 MINUTE LIMIT {0};", limit);

                    using (DbDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            returnVal.Add(Guid.Parse(reader.GetString(0)));
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Log("Error while retrieving recent trace information", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }

            return returnVal;
        }

        public async Task MarkTraceIdsAsImportedToAppInsights(IEnumerable<Guid> traceIds)
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
                        commandBuilder.Append("UPDATE instrumentation SET ImportedToAppInsights = 1 WHERE TraceId IN (");
                        bool first = true;
                        foreach (Guid guid in traceIds)
                        {
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                commandBuilder.Append(",");
                            }

                            commandBuilder.Append('\'');
                            commandBuilder.Append(CommonInstrumentation.FormatTraceId(guid));
                            commandBuilder.Append('\'');
                        }

                        commandBuilder.Append(");");

                        command.CommandText = commandBuilder.ToString();
                        await command.ExecuteScalarAsync();
                    }
                }
                catch (Exception e)
                {
                    _logger.Log("Error while updating ImportedToAppInsights flag", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
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
                        "  `TraceId` varchar(255) NOT NULL,\r\n" +
                        "  `TraceStart` datetime DEFAULT NULL,\r\n" +
                        "  `TraceEnd` datetime DEFAULT NULL,\r\n" +
                        "  `LogBlob` mediumblob,\r\n" +
                        "  `ImportedToAppInsights` tinyint(4) DEFAULT NULL,\r\n" +
                        "  PRIMARY KEY (`TraceId`),\r\n" +
                        "  KEY `idx_instrumentation_TraceStart` (`TraceStart`),\r\n" +
                        "  KEY `idx_instrumentation_TraceEnd` (`TraceEnd`),\r\n" +
                        "  KEY `idx_instrumentation_ImportedToAppInsights` (`ImportedToAppInsights`)\r\n" +
                        ") ENGINE=InnoDB DEFAULT CHARSET=utf8 AVG_ROW_LENGTH=3000 ROW_FORMAT=DYNAMIC;"
                        , TABLE_NAME)
                }
            };
        
        protected override IEnumerable<MySqlEventDefinition> Events =>
            new List<MySqlEventDefinition>()
            {
                new MySqlEventDefinition()
                {
                    EventName = "instrumentation_cleanup",
                    CreateStatement = string.Format(
                        "CREATE EVENT `instrumentation_cleanup`\r\n" +
                        "ON SCHEDULE EVERY 1 HOUR DO\r\n" +
                        "DELETE FROM {0} WHERE TraceEnd < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 60 DAY)",
                        TABLE_NAME)
                }
            };
    }
}
