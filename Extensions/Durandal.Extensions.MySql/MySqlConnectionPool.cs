using Durandal.Common.Logger;
using Durandal.Common.Utils;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using System.Threading;
using Durandal.Common.Instrumentation;
using Durandal.Common.Time;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.MySql
{
    /// <summary>
    /// A fast, readily available cache of MySql database connections.
    /// Yes, I know that a pool is already made available using MySql helpers, and I am doing exactly what they told
    /// me not to do. However, managing my own pool has some performance benefits in obtaining new connections
    /// (presumably because the native pool will ping the connection to make sure it is valid before returning it)
    /// </summary>
    public class MySqlConnectionPool : IDisposable, IMetricSource
    {
        private static readonly TimeSpan OPEN_CONNECTION_MAX_TIMEOUT = TimeSpan.FromSeconds(10);
        private readonly int _poolSize;
        private readonly ILogger _logger;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly string _poolName;
        private readonly string _connectionString;
        private readonly bool _useNativePool;
        private MySqlConnection[] _connections;
        private ResourcePool<MySqlConnection> _pool;
        private int _disposed = 0;
        
        private MySqlConnectionPool(string connectionString, ILogger logger, IMetricCollector metrics, DimensionSet dimensions, string poolName, bool useNativePool, int poolSize)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("MySQL connection string is null");
            }

            _poolSize = poolSize;
            _logger = logger;
            _metrics = new WeakPointer<IMetricCollector>(metrics ?? NullMetricCollector.Singleton);
            _useNativePool = useNativePool;
            _connectionString = connectionString;
            _poolName = poolName;
            _metricDimensions = dimensions ?? DimensionSet.Empty;
            _metricDimensions = _metricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_MySqlConnectionName, _poolName));
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~MySqlConnectionPool()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Constructs a pool of MySql connections with the specified connection string.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="logger"></param>
        /// <param name="metrics"></param>
        /// <param name="dimensions"></param>
        /// <param name="poolName"></param>
        /// <param name="useNativePool"></param>
        /// <param name="poolSize"></param>
        /// <returns></returns>
        public static async Task<MySqlConnectionPool> Create(
            string connectionString,
            ILogger logger,
            IMetricCollector metrics, 
            DimensionSet dimensions,
            string poolName = "Default",
            bool useNativePool = true,
            int poolSize = 8)
        {
            MySqlConnectionPool returnVal = new MySqlConnectionPool(connectionString, logger, metrics, dimensions, poolName, useNativePool, poolSize);
            await returnVal.Initialize();
            return returnVal;
        }

        /// <summary>
        /// Use this constructor for debugging if you need
        /// </summary>
        /// <param name="server"></param>
        /// <param name="database"></param>
        /// <param name="user"></param>
        /// <param name="pass"></param>
        /// <param name="logger"></param>
        /// <param name="metrics"></param>
        /// <param name="dimensions"></param>
        /// <param name="poolName"></param>
        /// <param name="useNativePool"></param>
        /// <param name="poolSize"></param>
        /// <returns></returns>
        public static Task<MySqlConnectionPool> Create(
            string server,
            string database,
            string user,
            string pass,
            ILogger logger,
            IMetricCollector metrics,
            DimensionSet dimensions,
            string poolName = "Default",
            bool useNativePool = true,
            int poolSize = 8)
        {
            MySqlConnectionStringBuilder connStringBuilder = new MySqlConnectionStringBuilder();
            connStringBuilder.Server = server;
            connStringBuilder.Port = 3306;
            connStringBuilder.Database = database;
            connStringBuilder.UserID = user;
            connStringBuilder.Password = pass;
            connStringBuilder.AllowBatch = true;
            connStringBuilder.Pooling = true;
            connStringBuilder.UseCompression = true;
            connStringBuilder.ConnectionLifeTime = 300;
            connStringBuilder.CharacterSet = "utf8";

            return Create(connStringBuilder.GetConnectionString(true), logger, metrics, dimensions, poolName, useNativePool, poolSize);
        }

        private async Task Initialize()
        {
            if (!_connectionString.ToLowerInvariant().Contains("utf8"))
            {
                _logger.Log("Your SQL connection string does not specify \"utf8\" string encoding. This is highly recommended to prevent encoding errors.", LogLevel.Wrn);
            }

            if (!_useNativePool)
            {
                _connections = new MySqlConnection[_poolSize];
                
                for (int c = 0; c < _poolSize; c++)
                {
                    try
                    {
                        _connections[c] = new MySqlConnection(_connectionString);
                        await _connections[c].OpenAsync();
                    }
                    catch (MySqlException e)
                    {
                        _logger.Log("MySql connection pool encountered an error", LogLevel.Err);
                        _logger.Log(e, LogLevel.Err);
                    }
                }

                _pool = new ResourcePool<MySqlConnection>(_connections, _logger, _metricDimensions, "MySqlConnections", KeepAliveFunctor, TimeSpan.FromSeconds(60), DefaultRealTimeProvider.Singleton);
                _metrics.Value.AddMetricSource(_pool);
            }
        }

        /// <summary>
        /// Executed by the pool watchdog at regular intervals
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private async Task<MySqlConnection> KeepAliveFunctor(MySqlConnection input)
        {
            // Cycle connections to make sure they stay open
            try
            {
                input.Close();
                await input.OpenAsync().ConfigureAwait(false);
            }
            catch (MySqlException e)
            {
                _logger.Log("MySql connection watchdog encountered an error", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }

            return input;
        }


        /// <summary>
        /// Fetches a pooled MySql connection
        /// </summary>
        /// <returns>A retrieve result potentially containing a pooled connection</returns>
        public Task<RetrieveResult<PooledResource<MySqlConnection>>> TryGetConnectionAsync()
        {
            return TryGetConnectionAsync(OPEN_CONNECTION_MAX_TIMEOUT);
        }

        /// <summary>
        /// Fetches a pooled MySql connection, failing if the operation exceeds a certain timeout
        /// </summary>
        /// <param name="timeToWait">The maximum amount of time to wait</param>
        /// <returns>A retrieve result potentially containing a pooled connection</returns>
        public async Task<RetrieveResult<PooledResource<MySqlConnection>>> TryGetConnectionAsync(TimeSpan timeToWait)
        {
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_MySql_Connections, _metricDimensions, 1);
            if (_useNativePool)
            {
                try
                {
                    MySqlConnection connection = new MySqlConnection(_connectionString);
                    await connection.OpenAsync().ConfigureAwait(false);
                    return new RetrieveResult<PooledResource<MySqlConnection>>(PooledResource<MySqlConnection>.Wrap(connection));
                }
                catch (MySqlException e)
                {
                    _logger.Log("MySql connection pool encountered an error", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    return new RetrieveResult<PooledResource<MySqlConnection>>();
                }
            }
            else
            {
                return await _pool.TryGetResourceAsync(timeToWait).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Releases a pooled SQL connection. This has the potential to block to reopen a failed connection,
        /// so if you really want to optimize you can ignore the result.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="succeeded"></param>
        /// <returns></returns>
        public async Task<bool> ReleaseConnectionAsync(PooledResource<MySqlConnection> connection, bool succeeded = true)
        {
            try
            {
                if (_useNativePool)
                {
                    connection.Value.Close();
                    connection.Value.Dispose();
                    return true;
                }
                else
                {
                    if (!succeeded)
                    {
                        // Cycle the connection
                        //_logger.Log("Reopening a sql connection");
                        connection.Value.Close();
                        await connection.Value.OpenAsync().ConfigureAwait(false);
                    }
                
                    return _pool.ReleaseResource(connection);
                }
            }
            catch (Exception e)
            {
                _logger.Log("MySql connection pool encountered an error", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }

            return false;
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

            if (disposing && !_useNativePool)
            {
                _metrics.Value.RemoveMetricSource(_pool);
                _pool.Dispose();

                foreach (MySqlConnection conn in _connections)
                {
                    if (conn != null)
                    {
                        try
                        {
                            conn.Close();
                            conn.Dispose();
                        }
                        catch (Exception) { }
                    }
                }
            }
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
            _pool.ReportMetrics(reporter);
        }

        public void InitializeMetrics(IMetricCollector collector)
        {
            _pool.InitializeMetrics(collector);
        }
    }
}
