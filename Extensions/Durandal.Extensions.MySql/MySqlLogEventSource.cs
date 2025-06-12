using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.API;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.MySql
{
    public class MySqlLogEventSource : ILogEventSource
    {
        private static readonly TimeSpan MAX_TIME_TO_GET_CONNECTION = TimeSpan.FromSeconds(5);
        private readonly WeakPointer<MySqlConnectionPool> _connectionPool;
        private readonly ILogger _logger;
        private readonly MySqlLogTableCreationStub _tableCreationStub;
        private int _initialized = 0;

        public MySqlLogEventSource(MySqlConnectionPool connectionPool, ILogger logger)
        {
            _logger = logger;
            _connectionPool = new WeakPointer<MySqlConnectionPool>(connectionPool);
            _tableCreationStub = new MySqlLogTableCreationStub(connectionPool, logger);
        }

        public Task Initialize()
        {
            if (!AtomicOperations.ExecuteOnce(ref _initialized))
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            return _tableCreationStub.Initialize();
        }

        public async Task<IEnumerable<LogEvent>> GetLogEvents(FilterCriteria logFilter)
        {
            await Initialize();

            IList<LogEvent> returnVal = new List<LogEvent>();

            IList<string> sqlFilters = new List<string>();

            if (logFilter.StartTime.HasValue && logFilter.EndTime.HasValue)
            {
                sqlFilters.Add("(Timestamp BETWEEN " + logFilter.StartTime.Value.Ticks + " AND " + logFilter.EndTime.Value.Ticks + ")");
            }
            else if (logFilter.StartTime.HasValue)
            {
                sqlFilters.Add("(Timestamp > " + logFilter.StartTime.Value.Ticks + ")");
            }
            else if (logFilter.EndTime.HasValue)
            {
                sqlFilters.Add("(Timestamp < " + logFilter.EndTime.Value.Ticks + ")");
            }

            if (logFilter.Level == LogLevel.None)
            {
                _logger.Log("Filtering a log set based on LogLevel == None. This is probably unintended", LogLevel.Wrn);
                return returnVal;
            }
            else if (logFilter.Level != LogLevel.All)
            {
                // TODO implement filters for multiple log levels, for example std + err
                sqlFilters.Add("(Level = \'" + logFilter.Level.ToChar() + "\')");
            }

            if (logFilter.TraceId.HasValue)
            {
                sqlFilters.Add("(TraceId = \'" + CommonInstrumentation.FormatTraceId(logFilter.TraceId.Value) + "\')");
            }

            if (!string.IsNullOrEmpty(logFilter.ExactComponentName))
            {
                sqlFilters.Add("(Component = \'" + logFilter.ExactComponentName + "\')");
            }

            if (!string.IsNullOrEmpty(logFilter.SearchTerm))
            {
                sqlFilters.Add("(Message LIKE \'%" + logFilter.SearchTerm + "\'%)");
            }

            string commandText = "SELECT TraceId, Timestamp, Component, Level, Message, PrivacyClass FROM logs";
            if (sqlFilters.Count > 0)
            {
                commandText += " WHERE " + string.Join(" AND ", sqlFilters);
            }

            commandText += " ORDER BY Timestamp ASC;";

            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    //_logger.Log("Executing SQL " + commandText);
                    using (MySqlDataReader reader = await MySqlHelper.ExecuteReaderAsync(connection.Value, commandText))
                    {
                        while (await reader.ReadAsync())
                        {
                            string traceId = reader.GetString(0);
                            long utcTicks = reader.GetInt64(1);
                            string component = null;
                            if (!(reader.GetValue(2) is DBNull))
                            {
                                component = reader.GetString(2);
                            }

                            string level = reader.GetString(3);
                            string message = reader.GetString(4);
                            ushort privacyClass = reader.GetUInt16(5);
                            LogEvent reified = new LogEvent(
                                component,
                                message,
                                LoggingLevelManipulators.ParseLevelChar(level),
                                new DateTimeOffset(utcTicks, TimeSpan.Zero),
                                CommonInstrumentation.TryParseTraceIdGuid(traceId),
                                (DataPrivacyClassification)privacyClass);
                            returnVal.Add(reified);
                        }
                    }
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

            return returnVal;
        }
    }
}
