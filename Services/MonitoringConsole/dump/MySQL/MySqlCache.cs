using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.NLP.Indexing;
using Durandal.Common.Utils;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Utils.Cache;
using Durandal.Common.Utils.IO;
using Durandal.Common.Utils.Tasks;

namespace Photon.Common.MySQL
{
    /// <summary>
    /// An implementation of a Cache backed by a MySql database.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MySqlCache<T> where T : class
    {
        private static readonly TimeSpan CONNECTION_POOL_TIMEOUT = TimeSpan.FromMilliseconds(5000);
        private readonly MySqlConnectionPool _connectionPool;
        private readonly ILogger _logger;
        private readonly IByteConverter<T> _serializer;

        public MySqlCache(MySqlConnectionPool connectionPool, IByteConverter<T> serializer, ILogger logger)
        {
            _logger = logger ?? new NullLogger();
            _serializer = serializer;
            _connectionPool = connectionPool;
        }

        public async Task Store(Guid activityId, T item)
        {
            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    byte[] blob = _serializer.Encode(item);
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "INSERT INTO cache(activity_id, value, creation_time) VALUES(@KEY, @BLOBDATA, @CREATETIME);";
                    command.Parameters.Add("KEY", MySqlDbType.Binary).Value = activityId.ToByteArray();
                    command.Parameters.Add("BLOBDATA", MySqlDbType.MediumBlob).Value = blob;
                    command.Parameters.Add("CREATETIME", MySqlDbType.DateTime).Value = DateTime.UtcNow;
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.ReleaseConnection(ref connection);
                }
            }
            else
            {
                _logger.Log("Failed to get a sql connection after " + CONNECTION_POOL_TIMEOUT.TotalMilliseconds + "ms", LogLevel.Wrn);
            }
        }

        public async Task<RetrieveResult<T>> TryRetrieveAsync(Guid key, int maxSpinMs = 0)
        {
            Stopwatch timer = Stopwatch.StartNew();

            RetrieveResult<T> returnVal = new RetrieveResult<T>()
            {
                Success = false
            };

            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "cache_retrieve";
                    command.CommandType = CommandType.StoredProcedure;
                    MySqlParameter keyParam = new MySqlParameter("@p_activity_id", MySqlDbType.Binary, 16)
                    {
                        Direction = ParameterDirection.Input,
                        Value = key.ToByteArray()
                    };
                    MySqlParameter timeoutParam = new MySqlParameter("@p_timeout", MySqlDbType.Float)
                    {
                        Direction = ParameterDirection.Input,
                        Value = (float)maxSpinMs / 1000f
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
                        returnVal.Result = _serializer.Decode(readBlob, 0, readBlob.Length);
                        returnVal.Success = true;
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e.Message, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.ReleaseConnection(ref connection);
                }
            }
            else
            {
                _logger.Log("Failed to get a sql connection after " + CONNECTION_POOL_TIMEOUT.TotalMilliseconds + "ms", LogLevel.Wrn);
            }

            timer.Stop();
            returnVal.LatencyMs = (int)timer.ElapsedMilliseconds;
            return returnVal;
        }
    }
}
