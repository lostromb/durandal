using Durandal.Common.Logger;
using Durandal.Common.Security;
using Durandal.Common.Utils;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using System.Data.Common;
using Durandal.Common.Cache;
using Durandal.Common.Security.Server;
using Durandal.API;
using Durandal.Common.Security.Login;
using System.Diagnostics;
using Durandal.Common.Time;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.MySql
{
    public class MySqlPrivateKeyStore : MySqlDataSource, IPrivateKeyStore
    {
        private readonly TimeSpan MAX_TIME_TO_GET_CONNECTION = TimeSpan.FromMilliseconds(5000);
        private const string TABLE_NAME = "client_auth_private";
        private readonly WeakPointer<MySqlConnectionPool> _connectionPool;
        private readonly ILogger _logger;

        public MySqlPrivateKeyStore(MySqlConnectionPool connectionPool, ILogger logger) : base(connectionPool, logger)
        {
            _logger = logger;
            _connectionPool = new WeakPointer<MySqlConnectionPool>(connectionPool);
        }

        public async Task<RetrieveResult<PrivateKeyVaultEntry>> GetUserInfoByStateKey(string stateKey)
        {
            ValueStopwatch latencyCounter = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("SELECT ClientId, UserId, LastLoginTime, LoginState, LoginInProgress, AuthProvider, `Key`, Salt, ClientName, UserEmail, UserFullName, UserGivenName, UserSurname, UserIconPng FROM {0} WHERE LoginState = @StateKey;", TABLE_NAME);
                    command.Parameters.Add(new MySqlParameter("@StateKey", stateKey));

                    DbDataReader reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        // Return the first row that matches
                        // 0         1        2              3            4              5             6    7     8           9          10             11            12           13
                        // ClientId, UserId, LastLoginTime, LoginState, LoginInProgress, AuthProvider, Key, Salt, ClientName, UserEmail, UserFullName, UserGivenName, UserSurname, UserIconPng
                        PrivateKeyVaultEntry returnVal = new PrivateKeyVaultEntry();
                        returnVal.VaultEntry = new UserClientSecretInfo();
                        returnVal.VaultEntry.ClientId = reader.IsDBNull(0) ? null : reader.GetString(0);
                        returnVal.VaultEntry.UserId = reader.IsDBNull(1) ? null : reader.GetString(1);
                        returnVal.LastLoginTime = new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero);
                        returnVal.LoginState = reader.IsDBNull(3) ? null : reader.GetString(3);
                        returnVal.LoginInProgress = reader.GetBoolean(4);
                        returnVal.VaultEntry.AuthProvider = reader.IsDBNull(5) ? null : reader.GetString(5);
                        returnVal.VaultEntry.PrivateKey = PrivateKey.ReadFromXml(reader.GetString(6));
                        returnVal.VaultEntry.SaltValue = CryptographyHelpers.DeserializeKey(reader.GetString(7));
                        returnVal.VaultEntry.ClientName = reader.IsDBNull(8) ? null : reader.GetString(8);
                        returnVal.VaultEntry.UserEmail = reader.IsDBNull(9) ? null : reader.GetString(9);
                        returnVal.VaultEntry.UserFullName = reader.IsDBNull(10) ? null : reader.GetString(10);
                        returnVal.VaultEntry.UserGivenName = reader.IsDBNull(11) ? null : reader.GetString(11);
                        returnVal.VaultEntry.UserSurname = reader.IsDBNull(12) ? null : reader.GetString(12);
                        returnVal.VaultEntry.UserIconPng = reader.IsDBNull(13) ? null : reader.GetFieldValue<byte[]>(13);
                        reader.Dispose();
                        latencyCounter.Stop();

                        return new RetrieveResult<PrivateKeyVaultEntry>(returnVal, latencyCounter.ElapsedMillisecondsPrecise());
                    }

                    // No results in any rows, so close reader and return null
                    reader.Dispose();
                }
                catch (Exception e)
                {
                    _logger.Log("Error while retrieving client security information", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to get private key", LogLevel.Err);
            }

            latencyCounter.Stop();
            return new RetrieveResult<PrivateKeyVaultEntry>(null, latencyCounter.ElapsedMillisecondsPrecise(), false);
        }

        public async Task<RetrieveResult<PrivateKeyVaultEntry>> GetUserInfoById(ClientKeyIdentifier clientId)
        {
            ValueStopwatch latencyCounter = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    if (clientId.Scope == ClientAuthenticationScope.Client)
                    {
                        command.CommandText = string.Format("SELECT ClientId, UserId, LastLoginTime, LoginState, LoginInProgress, AuthProvider, `Key`, Salt, ClientName, UserEmail, UserFullName, UserGivenName, UserSurname, UserIconPng FROM {0} WHERE ClientId = \'{1}\' AND UserId = \'\';", TABLE_NAME, clientId.ClientId);
                    }
                    else if (clientId.Scope == ClientAuthenticationScope.User)
                    {
                        command.CommandText = string.Format("SELECT ClientId, UserId, LastLoginTime, LoginState, LoginInProgress, AuthProvider, `Key`, Salt, ClientName, UserEmail, UserFullName, UserGivenName, UserSurname, UserIconPng FROM {0} WHERE ClientId = \'\' AND UserId = \'{1}\';", TABLE_NAME, clientId.UserId);
                    }
                    else if (clientId.Scope == ClientAuthenticationScope.UserClient)
                    {
                        command.CommandText = string.Format("SELECT ClientId, UserId, LastLoginTime, LoginState, LoginInProgress, AuthProvider, `Key`, Salt, ClientName, UserEmail, UserFullName, UserGivenName, UserSurname, UserIconPng FROM {0} WHERE UserId = \'{1}\' AND ClientId = \'{2}\';", TABLE_NAME, clientId.UserId, clientId.ClientId);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid scope for client key " + clientId.Scope);
                    }

                    DbDataReader reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        // Return the first row that matches
                        // 0         1        2              3            4              5             6    7     8           9          10             11            12           13
                        // ClientId, UserId, LastLoginTime, LoginState, LoginInProgress, AuthProvider, Key, Salt, ClientName, UserEmail, UserFullName, UserGivenName, UserSurname, UserIconPng
                        PrivateKeyVaultEntry returnVal = new PrivateKeyVaultEntry();
                        returnVal.VaultEntry = new UserClientSecretInfo();
                        returnVal.VaultEntry.ClientId = reader.IsDBNull(0) ? null : reader.GetString(0);
                        returnVal.VaultEntry.UserId = reader.IsDBNull(1) ? null : reader.GetString(1);
                        returnVal.LastLoginTime = new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero);
                        returnVal.LoginState = reader.IsDBNull(3) ? null : reader.GetString(3);
                        returnVal.LoginInProgress = reader.GetBoolean(4);
                        returnVal.VaultEntry.AuthProvider = reader.IsDBNull(5) ? null : reader.GetString(5);
                        returnVal.VaultEntry.PrivateKey = PrivateKey.ReadFromXml(reader.GetString(6));
                        returnVal.VaultEntry.SaltValue = CryptographyHelpers.DeserializeKey(reader.GetString(7));
                        returnVal.VaultEntry.ClientName = reader.IsDBNull(8) ? null : reader.GetString(8);
                        returnVal.VaultEntry.UserEmail = reader.IsDBNull(9) ? null : reader.GetString(9);
                        returnVal.VaultEntry.UserFullName = reader.IsDBNull(10) ? null : reader.GetString(10);
                        returnVal.VaultEntry.UserGivenName = reader.IsDBNull(11) ? null : reader.GetString(11);
                        returnVal.VaultEntry.UserSurname = reader.IsDBNull(12) ? null : reader.GetString(12);
                        returnVal.VaultEntry.UserIconPng = reader.IsDBNull(13) ? null : reader.GetFieldValue<byte[]>(13);
                        reader.Dispose();
                        latencyCounter.Stop();

                        return new RetrieveResult<PrivateKeyVaultEntry>(returnVal, latencyCounter.ElapsedMillisecondsPrecise());
                    }

                    // No results in any rows, so close reader and return null
                    reader.Dispose();
                }
                catch (Exception e)
                {
                    _logger.Log("Error while retrieving client security information", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to get private key", LogLevel.Err);
            }

            latencyCounter.Stop();
            return new RetrieveResult<PrivateKeyVaultEntry>(null, latencyCounter.ElapsedMillisecondsPrecise(), false);
        }

        public async Task UpdateLoggedInUserInfo(PrivateKeyVaultEntry info)
        {
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    const string commandText = "INSERT INTO " + TABLE_NAME + "(ClientId, UserId, LastLoginTime, LoginState, LoginInProgress, AuthProvider, `Key`, Bits, Salt, ClientName, UserEmail, UserFullName, UserGivenName, UserSurname, UserIconPng) " +
                        "VALUES(@ClientId, @UserId, @LastLoginTime, @LoginState, @LoginInProgress, @AuthProvider, @Key, @Bits, @Salt, @ClientName, @UserEmail, @UserFullName, @UserGivenName, @UserSurname, @UserIconPng) " +
                        "ON DUPLICATE KEY UPDATE LastLoginTime=VALUES(LastLoginTime),LoginState=VALUES(LoginState),LoginInProgress=VALUES(LoginInProgress),AuthProvider=VALUES(AuthProvider),`Key`=VALUES(`Key`),Bits=VALUES(Bits),Salt=VALUES(Salt)," +
                        "ClientName=VALUES(ClientName),UserEmail=VALUES(UserEmail),UserFullName=VALUES(UserFullName),UserGivenName=VALUES(UserGivenName),UserSurname=VALUES(UserSurname),UserIconPng=VALUES(UserIconPng);";

                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = commandText;
                    command.Parameters.Add(new MySqlParameter("@ClientId", string.IsNullOrEmpty(info.VaultEntry.ClientId) ? "" : info.VaultEntry.ClientId));
                    command.Parameters.Add(new MySqlParameter("@UserId", string.IsNullOrEmpty(info.VaultEntry.UserId) ? "" : info.VaultEntry.UserId));
                    command.Parameters.Add(new MySqlParameter("@LastLoginTime", info.LastLoginTime.UtcDateTime));
                    command.Parameters.Add(new MySqlParameter("@LoginState", info.LoginState));
                    command.Parameters.Add(new MySqlParameter("@LoginInProgress", info.LoginInProgress ? 1 : 0));
                    command.Parameters.Add(new MySqlParameter("@AuthProvider", info.VaultEntry.AuthProvider));
                    command.Parameters.Add(new MySqlParameter("@Key", info.VaultEntry.PrivateKey.WriteToXml()));
                    command.Parameters.Add(new MySqlParameter("@Bits", info.VaultEntry.PrivateKey.KeyLengthBits));
                    command.Parameters.Add(new MySqlParameter("@Salt", info.VaultEntry.SaltValue == null ? null : CryptographyHelpers.SerializeKey(info.VaultEntry.SaltValue)));
                    command.Parameters.Add(new MySqlParameter("@ClientName", info.VaultEntry.ClientName));
                    command.Parameters.Add(new MySqlParameter("@UserEmail", info.VaultEntry.UserEmail));
                    command.Parameters.Add(new MySqlParameter("@UserFullName", info.VaultEntry.UserFullName));
                    command.Parameters.Add(new MySqlParameter("@UserGivenName", info.VaultEntry.UserGivenName));
                    command.Parameters.Add(new MySqlParameter("@UserSurname", info.VaultEntry.UserSurname));
                    command.Parameters.Add(new MySqlParameter("@UserIconPng", info.VaultEntry.UserIconPng));
                    int rows = await command.ExecuteNonQueryAsync();

                    //return rows != 0;
                }
                catch (Exception e)
                {
                    _logger.Log("Error while updating client security information", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    //return false;
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to update private key", LogLevel.Err);
            }

            //return false;
        }

        public async Task DeleteLoggedInUserInfo(ClientKeyIdentifier keyId)
        {
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "DELETE FROM " + TABLE_NAME + " WHERE ClientId = @ClientId AND UserId = @UserId;";
                    command.Parameters.Add(new MySqlParameter("@ClientId", string.IsNullOrEmpty(keyId.ClientId) || !keyId.Scope.HasFlag(ClientAuthenticationScope.Client) ? "" : keyId.ClientId));
                    command.Parameters.Add(new MySqlParameter("@UserId", string.IsNullOrEmpty(keyId.UserId) || !keyId.Scope.HasFlag(ClientAuthenticationScope.User) ? "" : keyId.UserId));
                    int rows = await command.ExecuteNonQueryAsync();
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
            else
            {
                _logger.Log("FAILED to get MySql connection to delete private key", LogLevel.Err);
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
                        "  `ClientId` varchar(255) NOT NULL,\r\n" +
                        "  `UserId` varchar(255) NOT NULL,\r\n" +
                        "  `LastLoginTime` datetime NOT NULL,\r\n" +
                        "  `LoginState` varchar(1024) DEFAULT NULL,\r\n" +
                        "  `LoginInProgress` tinyint(1) NOT NULL,\r\n" +
                        "  `AuthProvider` varchar(255) NOT NULL,\r\n" +
                        "  `Key` TEXT NOT NULL,\r\n" +
                        "  `Bits` int(11) NOT NULL,\r\n" +
                        "  `Salt` varchar(8192) NOT NULL,\r\n" +
                        "  `ClientName` varchar(1024) DEFAULT NULL,\r\n" +
                        "  `UserEmail` varchar(255) DEFAULT NULL,\r\n" +
                        "  `UserFullName` varchar(255) DEFAULT NULL,\r\n" +
                        "  `UserGivenName` varchar(255) DEFAULT NULL,\r\n" +
                        "  `UserSurname` varchar(255) DEFAULT NULL,\r\n" +
                        "  `UserIconPng` MEDIUMBLOB DEFAULT NULL,\r\n" +
                        "  UNIQUE KEY `Id` (`ClientId`,`UserId`),\r\n" +
                        "  KEY `State` (`LoginState`)\r\n" +
                        ") ENGINE=InnoDB DEFAULT CHARSET=utf8;\r\n"
                        , TABLE_NAME)
                }
            };
    }
}
