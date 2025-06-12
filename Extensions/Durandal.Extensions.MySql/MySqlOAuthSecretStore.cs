using Durandal.API;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Collections.Indexing;
using Durandal.Common.Security;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Utils;
using Durandal.Common.Cache;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.MySql
{
    /// <summary>
    /// An implementation of IOAuthSecretStore backed by a MySql database.
    /// </summary>
    public class MySqlOAuthSecretStore : MySqlDataSource, IOAuthSecretStore
    {
        private readonly TimeSpan MAX_TIME_TO_GET_CONNECTION = TimeSpan.FromMilliseconds(5000);
        private const string TABLE_NAME = "oauth_secrets";
        private readonly WeakPointer<MySqlConnectionPool> _connectionPool;
        private readonly ILogger _logger;
        private int _disposed = 0;

        public MySqlOAuthSecretStore(MySqlConnectionPool connectionPool, ILogger logger) : base(connectionPool, logger)
        {
            _logger = logger;
            _connectionPool = new WeakPointer<MySqlConnectionPool>(connectionPool);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~MySqlOAuthSecretStore()
        {
            Dispose(false);
        }
#endif

        public async Task SaveState(OAuthState state, Guid? traceId = null)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command;
                    int rows;

                    // Serialize the config and hash it, to detect modified configs
                    byte[] configHash = state.Config.HashConfiguration();

                    // Serialize the state and save it
                    byte[] encodedState = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(state));
                    command = connection.Value.CreateCommand();
                    command.CommandText = "INSERT INTO " + TABLE_NAME + "(Id, UserId, Domain, ConfigName, ConfigHash, LastTouched, State) VALUES(@ID, @USERID, @DOMAIN, @CONFIGNAME, @CONFIGHASH, UTC_TIMESTAMP(), @BLOBDATA) " +
                        "ON DUPLICATE KEY UPDATE LastTouched=UTC_TIMESTAMP(),State=VALUES(State),ConfigHash=VALUES(ConfigHash);";

                    command.Parameters.Add("ID", MySqlDbType.VarChar).Value = state.UniqueId;
                    command.Parameters.Add("USERID", MySqlDbType.VarChar).Value = state.DurandalUserId;
                    command.Parameters.Add("DOMAIN", MySqlDbType.VarChar).Value = state.DurandalPluginId;
                    command.Parameters.Add("CONFIGNAME", MySqlDbType.VarChar).Value = state.Config.ConfigName;
                    command.Parameters.Add("CONFIGHASH", MySqlDbType.Binary).Value = configHash;
                    command.Parameters.Add("BLOBDATA", MySqlDbType.MediumBlob).Value = encodedState;
                    rows = await command.ExecuteNonQueryAsync();

                    // Nuke all previous states also to ensure that there is only one state for each user/domain/config combination
                    // This will also delete any old configs that had the same name but a different hash,
                    command = connection.Value.CreateCommand();
                    command.CommandText = "DELETE FROM " + TABLE_NAME + " WHERE UserId = @USERID AND Domain = @DOMAIN AND ((Id != @ID AND ConfigName = @CONFIGNAME) OR ConfigHash != @CONFIGHASH);";
                    command.Parameters.Add("ID", MySqlDbType.VarChar).Value = state.UniqueId;
                    command.Parameters.Add("USERID", MySqlDbType.VarChar).Value = state.DurandalUserId;
                    command.Parameters.Add("DOMAIN", MySqlDbType.VarChar).Value = state.DurandalPluginId;
                    command.Parameters.Add("CONFIGNAME", MySqlDbType.VarChar).Value = state.Config.ConfigName;
                    command.Parameters.Add("CONFIGHASH", MySqlDbType.Binary).Value = configHash;
                    rows = await command.ExecuteNonQueryAsync();

                    timer.Stop();
                    _logger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_OauthSecretWrite, ref timer), LogLevel.Ins, traceId);
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err, traceId);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to write oauth state", LogLevel.Err, traceId);
            }
        }

        public async Task DeleteState(string stateId, Guid? traceId = null)
        {
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "DELETE FROM " + TABLE_NAME + " WHERE Id = @ID;";
                    command.Parameters.Add("ID", MySqlDbType.VarChar).Value = stateId;
                    int rows = await command.ExecuteNonQueryAsync();
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err, traceId);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to delete oauth state", LogLevel.Err, traceId);
            }
        }

        public async Task<RetrieveResult<OAuthState>> RetrieveState(string stateId, Guid? traceId = null)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "oauth_secret_retrieve_by_id";
                    command.CommandType = CommandType.StoredProcedure;
                    MySqlParameter userIdParam = new MySqlParameter("@p_id", MySqlDbType.VarChar, 48)
                    {
                        Direction = ParameterDirection.Input,
                        Value = stateId
                    };
                    MySqlParameter returnValParam = new MySqlParameter("@p_return_val", MySqlDbType.MediumBlob)
                    {
                        Direction = ParameterDirection.Output
                    };
                    command.Parameters.Add(userIdParam);
                    command.Parameters.Add(returnValParam);
                    await command.ExecuteScalarAsync();
                    if (returnValParam.Value != null && returnValParam.Value is byte[])
                    {
                        byte[] readBlob = (byte[])returnValParam.Value;
                        string jsonState = Encoding.UTF8.GetString(readBlob, 0, readBlob.Length);
                        OAuthState rr = JsonConvert.DeserializeObject<OAuthState>(jsonState);
                        timer.Stop();
                        return new RetrieveResult<OAuthState>(rr, timer.ElapsedMillisecondsPrecise());
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err, traceId);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to read oauth state", LogLevel.Err, traceId);
            }

            timer.Stop();
            return new RetrieveResult<OAuthState>(null, timer.ElapsedMillisecondsPrecise(), false);
        }

        public async Task<RetrieveResult<OAuthState>> RetrieveState(string durandalUserId, PluginStrongName durandalPlugin, OAuthConfig config, Guid? traceId = null)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    byte[] configHash = config.HashConfiguration();

                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "oauth_secret_retrieve_by_user_id";
                    command.CommandType = CommandType.StoredProcedure;
                    MySqlParameter userIdParam = new MySqlParameter("@p_id", MySqlDbType.VarChar, 48)
                    {
                        Direction = ParameterDirection.Input,
                        Value = durandalUserId
                    };
                    MySqlParameter domainParam = new MySqlParameter("@p_domain", MySqlDbType.VarChar, 255)
                    {
                        Direction = ParameterDirection.Input,
                        Value = durandalPlugin.PluginId
                    };
                    MySqlParameter configParam = new MySqlParameter("@p_config_name", MySqlDbType.VarChar, 255)
                    {
                        Direction = ParameterDirection.Input,
                        Value = config.ConfigName
                    };
                    MySqlParameter configHashParam = new MySqlParameter("@p_config_hash", MySqlDbType.Binary, 32)
                    {
                        Direction = ParameterDirection.Input,
                        Value = configHash
                    };
                    MySqlParameter returnValParam = new MySqlParameter("@p_return_val", MySqlDbType.MediumBlob)
                    {
                        Direction = ParameterDirection.Output
                    };
                    command.Parameters.Add(userIdParam);
                    command.Parameters.Add(domainParam);
                    command.Parameters.Add(configParam);
                    command.Parameters.Add(configHashParam);
                    command.Parameters.Add(returnValParam);
                    await command.ExecuteScalarAsync();
                    if (returnValParam.Value != null && returnValParam.Value is byte[])
                    {
                        byte[] readBlob = (byte[])returnValParam.Value;
                        string jsonState = Encoding.UTF8.GetString(readBlob, 0, readBlob.Length);
                        OAuthState rr = JsonConvert.DeserializeObject<OAuthState>(jsonState);
                        timer.Stop();
                        return new RetrieveResult<OAuthState>(rr, timer.ElapsedMillisecondsPrecise());
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err, traceId);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to read oauth state", LogLevel.Err, traceId);
            }

            timer.Stop();
            return new RetrieveResult<OAuthState>(null, timer.ElapsedMillisecondsPrecise(), false);
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

        protected override IEnumerable<MySqlTableDefinition> Tables =>
            new List<MySqlTableDefinition>()
            {
                new MySqlTableDefinition()
                {
                    TableName = TABLE_NAME,
                    CreateStatement = string.Format(
                        "CREATE TABLE `{0}` (\r\n" +
                        "  `Id` varchar(255) NOT NULL,\r\n" +
                        "  `UserId` varchar(255) NOT NULL,\r\n" +
                        "  `Domain` varchar(255) NOT NULL,\r\n" +
                        "  `ConfigName` varchar(255) DEFAULT NULL,\r\n" +
                        "  `LastTouched` datetime NOT NULL,\r\n" +
                        "  `ConfigHash` binary(32) NOT NULL,\r\n" +
                        "  `State` mediumblob,\r\n" +
                        "  PRIMARY KEY (`Id`),\r\n" +
                        "  KEY `Staleness` (`LastTouched`),\r\n" +
                        "  KEY `UserDomain` (`UserId`,`Domain`)\r\n" +
                        ") ENGINE=InnoDB DEFAULT CHARSET=utf8;\r\n"
                        , TABLE_NAME)
                }
            };

        protected override IEnumerable<MySqlProcedureDefinition> Procedures =>
            new List<MySqlProcedureDefinition>()
            {
                new MySqlProcedureDefinition()
                {
                    ProcedureName = "oauth_secret_retrieve_by_id",
                    CreateStatement = string.Format(
                        "CREATE PROCEDURE `oauth_secret_retrieve_by_id`(\r\n" +
                        "	IN p_id VARCHAR(255),\r\n" +
                        "    OUT p_return_val MEDIUMBLOB)\r\n" +
                        "BEGIN\r\n" +
                        "    DECLARE v_last_touched DATETIME;\r\n" +
                        "    DECLARE v_found BOOL;\r\n" +
                        "    SET p_return_val = NULL;\r\n" +
                        "	SELECT EXISTS (SELECT * FROM {0} WHERE Id = p_id) INTO v_found;\r\n" +
                        "	IF v_found THEN\r\n" +
                        "		SELECT LastTouched INTO v_last_touched FROM {0} WHERE Id = p_id;\r\n" +
                        "		# Check the expire time. If valid, touch the expire time and return the value\r\n" +
                        "		IF v_last_touched > UTC_TIMESTAMP() - INTERVAL 60 DAY THEN\r\n" +
                        "			SELECT State INTO p_return_val FROM {0} WHERE Id = p_id;\r\n" +
                        "			UPDATE {0} SET LastTouched=UTC_TIMESTAMP() WHERE Id = p_id;\r\n" +
                        "		END IF;\r\n" +
                        "	END IF;\r\n" +
                        "END\r\n",
                        TABLE_NAME)
                },
                new MySqlProcedureDefinition()
                {
                    ProcedureName = "oauth_secret_retrieve_by_user_id",
                    CreateStatement = string.Format(
                        "CREATE PROCEDURE `oauth_secret_retrieve_by_user_id`(\r\n" +
                        "	IN p_id VARCHAR(255),\r\n" +
                        "    IN p_domain VARCHAR(255),\r\n" +
                        "    IN p_config_name VARCHAR(255),\r\n" +
                        "    IN p_config_hash BINARY(32),\r\n" +
                        "    OUT p_return_val MEDIUMBLOB)\r\n" +
                        "BEGIN\r\n" +
                        "    DECLARE v_last_touched DATETIME;\r\n" +
                        "    DECLARE v_found BOOL;\r\n" +
                        "    DECLARE v_raw_id VARCHAR(255);\r\n" +
                        "    SET p_return_val = NULL;\r\n" +
                        "	SELECT EXISTS (SELECT * FROM {0} WHERE UserId = p_id AND Domain = p_domain AND ConfigName = p_config_name AND ConfigHash = p_config_hash) INTO v_found;\r\n" +
                        "	IF v_found THEN\r\n" +
                        "		SELECT Id,LastTouched INTO v_raw_id, v_last_touched FROM {0} WHERE UserId = p_id AND Domain = p_domain AND ConfigName = p_config_name AND ConfigHash = p_config_hash;\r\n" +
                        "		# Check the expire time. If valid, touch the expire time and return the value\r\n" +
                        "		IF v_last_touched > UTC_TIMESTAMP() - INTERVAL 60 DAY THEN\r\n" +
                        "			SELECT State INTO p_return_val FROM {0} WHERE Id = v_raw_id;\r\n" +
                        "			UPDATE {0} SET LastTouched=UTC_TIMESTAMP() WHERE Id = v_raw_id;\r\n" +
                        "		END IF;\r\n" +
                        "	END IF;\r\n" +
                        "END",
                        TABLE_NAME)
                }
            };

        protected override IEnumerable<MySqlEventDefinition> Events =>
            new List<MySqlEventDefinition>()
            {
                new MySqlEventDefinition()
                {
                    EventName = "oauth_secret_cleanup",
                    CreateStatement = string.Format(
                        "CREATE EVENT `oauth_secret_cleanup`\r\n" +
                        "ON SCHEDULE EVERY 1 HOUR DO\r\n" +
                        "DELETE FROM {0} WHERE LastTouched < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 60 DAY)",
                        TABLE_NAME)
                }
            };
    }
}
