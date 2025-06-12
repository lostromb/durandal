using Durandal.Common.Logger;
using Durandal.Common.Utils;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Utils.Tasks;

namespace Photon.Common.MySQL
{
    /// <summary>
    /// A fast, readily available cache of MySql database connections.
    /// Yes, I know that a pool is already made available using MySql helpers, and I am doing exactly what they told
    /// me not to do. However, managing my own pool has some performance benefits in obtaining new connections
    /// (presumably because the native pool will ping the connection to make sure it is valid before returning it)
    /// </summary>
    public class MySqlConnectionPool : IDisposable
    {
        private int _poolSize;
        private MySqlConnection[] _connections;
        private ILogger _logger;
        private ResourcePool<MySqlConnection> _pool;
        private string _connectionString;
        private bool _useNativePool;

        /// <summary>
        /// Constructs a pool of MySql connections with the specified connection string.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="logger"></param>
        public MySqlConnectionPool(string connectionString, ILogger logger, bool useNativePool = true, int poolSize = 8)
        {
            _poolSize = poolSize;
            _logger = logger;
            _useNativePool = useNativePool;
            Initialize(connectionString);
        }

        /// <summary>
        /// Use this constructor for debugging if you can
        /// </summary>
        /// <param name="server"></param>
        /// <param name="database"></param>
        /// <param name="user"></param>
        /// <param name="pass"></param>
        /// <param name="logger"></param>
        public MySqlConnectionPool(string server, string database, string user, string pass, ILogger logger, bool useNativePool = true, int poolSize = 8)
        {
            _poolSize = poolSize;
            _logger = logger;
            _useNativePool = useNativePool;

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

            Initialize(connStringBuilder.GetConnectionString(true));
        }

        public double Usage
        {
            get
            {
                if (!_useNativePool && _pool != null)
                {
                    return _pool.Usage;
                }
                else
                {
                    return -1;
                }
            }
        }

        public bool TryGetConnection(ref PooledResource<MySqlConnection> connection)
        {
            return TryGetConnection(ref connection, TimeSpan.Zero);
        }

        public bool TryGetConnection(ref PooledResource<MySqlConnection> connection, int msTimeout)
        {
            return TryGetConnection(ref connection, TimeSpan.FromMilliseconds(msTimeout));
        }

        public bool TryGetConnection(ref PooledResource<MySqlConnection> connection, TimeSpan timeToWait)
        {
            if (_useNativePool)
            {
                try
                {
                    connection = PooledResource<MySqlConnection>.Wrap(new MySqlConnection(_connectionString));
                    connection.Value.Open();
                    return true;
                }
                catch (MySqlException e)
                {
                    _logger.Log("MySql connection pool encountered an error: " + e.Message, LogLevel.Err);
                    return false;
                }
            }
            else
            {
                return _pool.TryGetResource(ref connection, timeToWait);
            }
        }

        public bool ReleaseConnection(ref PooledResource<MySqlConnection> connection, bool succeeded = true)
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
                        connection.Value.Open();
                    }

                    return _pool.ReleaseResource(ref connection);
                }
            }
            catch (Exception e)
            {
                _logger.Log("MySql connection pool encountered an error: " + e.Message, LogLevel.Err);
            }

            return false;
        }

        public void Dispose()
        {
            if (!_useNativePool)
            {
                _pool.Dispose();

                //foreach (MySqlConnection conn in _connections)
                //{
                //    try
                //    {
                //        conn.Close();
                //        conn.Dispose();
                //    }
                //    catch (Exception) { }
                //}
            }
        }

        private void Initialize(string connectionString)
        {
            _connectionString = connectionString;

            if (!_connectionString.ToLowerInvariant().Contains("utf8"))
            {
                _logger.Log("Your SQL connection string does not specify \"utf8\" string encoding. This is highly recommended to prevent encoding errors.", LogLevel.Wrn);
            }

            if (!_useNativePool)
            {
                _connections = new MySqlConnection[_poolSize];
                try
                {
                    for (int c = 0; c < _poolSize; c++)
                    {
                        _connections[c] = new MySqlConnection(connectionString);
                        _connections[c].Open();
                    }
                }
                catch (MySqlException e)
                {
                    _logger.Log("MySql connection pool encountered an error: " + e.Message, LogLevel.Err);
                }

                _pool = new ResourcePool<MySqlConnection>(_connections, _logger, KeepAliveFunctor, 60000);
            }
        }

        /// <summary>
        /// Executed by the pool watchdog at regular intervals
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private MySqlConnection KeepAliveFunctor(MySqlConnection input)
        {
            if (input == null)
            {
                return null;
            }

            // Cycle connections to make sure they stay open
            try
            {
                input.Close();
                input.Open();
            }
            catch (MySqlException e)
            {
                _logger.Log("MySql connection watchdog encountered an error: " + e.Message, LogLevel.Err);
            }

            return input;
        }
    }
}
