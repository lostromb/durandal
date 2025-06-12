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
using System.Diagnostics;
using Durandal.Common.Time;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.MySql
{
    public class MySqlPublicKeyStore : MySqlDataSource, IPublicKeyStore
    {
        private static readonly TimeSpan MAX_TIME_TO_GET_CONNECTION = TimeSpan.FromMilliseconds(5000);
        private const string TABLE_NAME = "client_auth_public";
        private readonly TimeSpan LOCAL_CACHE_TIME = TimeSpan.FromSeconds(5); // Time to cache user's public keys in memory
        private readonly WeakPointer<MySqlConnectionPool> _connectionPool;
        private readonly ILogger _logger;
        private readonly InMemoryCache<ServerSideAuthenticationState> _cache = new InMemoryCache<ServerSideAuthenticationState>();

        public MySqlPublicKeyStore(MySqlConnectionPool connectionPool, ILogger logger) : base(connectionPool, logger)
        {
            _logger = logger;
            _connectionPool = new WeakPointer<MySqlConnectionPool>(connectionPool);
        }

        /// <summary>
        /// Updates a specific client's information (or one row of the database)
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public async Task<bool> UpdateClientState(ServerSideAuthenticationState client)
        {
            if (client.KeyScope == ClientAuthenticationScope.None)
            {
                throw new ArgumentException("Public key scope cannot be None");
            }

            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    const string commandText = "INSERT INTO " + TABLE_NAME + "(ClientId, ClientName, UserId, UserName, Trusted, `Key`, Bits, Salt, RegisteredAt) " +
                        "VALUES(@ClientId, @ClientName, @UserId, @UserName, @Trusted, @Key, @Bits, @Salt, UTC_TIMESTAMP()) " +
                        "ON DUPLICATE KEY UPDATE ClientName=VALUES(ClientName),UserName=VALUES(UserName),Trusted=VALUES(Trusted),`Key`=VALUES(`Key`),Bits=VALUES(Bits),Salt=VALUES(Salt);";
                    
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = commandText;
                    command.Parameters.Add(new MySqlParameter("@ClientId", string.IsNullOrEmpty(client.ClientInfo.ClientId) || !client.KeyScope.HasFlag(ClientAuthenticationScope.Client) ? "" : client.ClientInfo.ClientId));
                    command.Parameters.Add(new MySqlParameter("@ClientName", string.IsNullOrEmpty(client.ClientInfo.ClientName) || !client.KeyScope.HasFlag(ClientAuthenticationScope.Client) ? null : client.ClientInfo.ClientName));
                    command.Parameters.Add(new MySqlParameter("@UserId", string.IsNullOrEmpty(client.ClientInfo.UserId) || !client.KeyScope.HasFlag(ClientAuthenticationScope.User) ? "" : client.ClientInfo.UserId));
                    command.Parameters.Add(new MySqlParameter("@UserName", string.IsNullOrEmpty(client.ClientInfo.UserName) || !client.KeyScope.HasFlag(ClientAuthenticationScope.User) ? null : client.ClientInfo.UserName));
                    command.Parameters.Add(new MySqlParameter("@Trusted", client.Trusted ? 1 : 0));
                    command.Parameters.Add(new MySqlParameter("@Key", client.PubKey.WriteToXml()));
                    command.Parameters.Add(new MySqlParameter("@Bits", client.PubKey.KeyLengthBits));
                    command.Parameters.Add(new MySqlParameter("@Salt", client.SaltValue == null ? null : CryptographyHelpers.SerializeKey(client.SaltValue)));
                    int rows = await command.ExecuteNonQueryAsync();

                    UpdateLocalCache(client);
                    return rows != 0;
                }
                catch (Exception e)
                {
                    _logger.Log("Error while updating client security information", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    return false;
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to update public key", LogLevel.Err);
            }

            return false;
        }

        /// <summary>
        /// Loads the client information for a known user from the database
        /// </summary>
        /// <returns></returns>
        public async Task<RetrieveResult<ServerSideAuthenticationState>> GetClientState(ClientKeyIdentifier keyId)
        {
            RetrieveResult<ServerSideAuthenticationState> localCacheValue = GetFromLocalCache(keyId);
            if (localCacheValue.Success)
            {
                return new RetrieveResult<ServerSideAuthenticationState>(localCacheValue.Result);
            }

            ValueStopwatch latencyCounter = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    if (keyId.Scope == ClientAuthenticationScope.Client)
                    {
                        command.CommandText = string.Format("SELECT UserId,ClientId,ClientName,UserName,Trusted,`Key`,Salt FROM " + TABLE_NAME + " WHERE ClientId = \'{0}\' AND UserId = \'\';", keyId.ClientId);
                    }
                    else if (keyId.Scope == ClientAuthenticationScope.User)
                    {
                        command.CommandText = string.Format("SELECT UserId,ClientId,ClientName,UserName,Trusted,`Key`,Salt FROM " + TABLE_NAME + " WHERE ClientId = \'\' AND UserId = \'{0}\';", keyId.UserId);
                    }
                    else if (keyId.Scope == ClientAuthenticationScope.UserClient)
                    {
                        command.CommandText = string.Format("SELECT UserId,ClientId,ClientName,UserName,Trusted,`Key`,Salt FROM " + TABLE_NAME + " WHERE UserId = \'{0}\' AND ClientId = \'{1}\';", keyId.UserId, keyId.ClientId);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid scope for client key " + keyId.Scope);
                    }

                    DbDataReader reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        // Return the first row that matches
                        ServerSideAuthenticationState newClient = new ServerSideAuthenticationState();
                        newClient.ClientInfo = new ClientIdentifier(null, null, null, null);
                        newClient.ClientInfo.UserId = reader.IsDBNull(0) ? null : reader.GetString(0);
                        newClient.KeyScope = ClientAuthenticationScope.None;
                        if (string.IsNullOrEmpty(newClient.ClientInfo.UserId))
                        {
                            newClient.ClientInfo.UserId = null;
                        }
                        else
                        {
                            newClient.KeyScope |= ClientAuthenticationScope.User;
                        }

                        newClient.ClientInfo.ClientId = reader.IsDBNull(1) ? null : reader.GetString(1);
                        if (string.IsNullOrEmpty(newClient.ClientInfo.ClientId))
                        {
                            newClient.ClientInfo.ClientId = null;
                        }
                        else
                        {
                            newClient.KeyScope |= ClientAuthenticationScope.Client;
                        }

                        newClient.ClientInfo.ClientName = reader.IsDBNull(2) ? null : reader.GetString(2);
                        newClient.ClientInfo.UserName = reader.IsDBNull(3) ? null : reader.GetString(3);
                        newClient.Trusted = reader.GetBoolean(4);
                        newClient.PubKey = PublicKey.ReadFromXml(reader.GetString(5));
                        newClient.SaltValue = reader.IsDBNull(6) ? null : CryptographyHelpers.DeserializeKey(reader.GetString(6));
                        reader.Dispose();
                        latencyCounter.Stop();

                        UpdateLocalCache(newClient);

                        return new RetrieveResult<ServerSideAuthenticationState>(newClient, latencyCounter.ElapsedMillisecondsPrecise());
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
                _logger.Log("FAILED to get MySql connection to get public key", LogLevel.Err);
            }

            latencyCounter.Stop();
            return new RetrieveResult<ServerSideAuthenticationState>(null, latencyCounter.ElapsedMillisecondsPrecise(), false);
        }

        public async Task DeleteClientState(ClientKeyIdentifier keyId)
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

                    DeleteLocalCache(keyId);
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
                _logger.Log("FAILED to get MySql connection to delete public key", LogLevel.Err);
            }
        }

        private void UpdateLocalCache(ServerSideAuthenticationState info)
        {
            string key = (string.IsNullOrEmpty(info.ClientInfo.ClientId) || !info.KeyScope.HasFlag(ClientAuthenticationScope.Client) ? string.Empty : info.ClientInfo.ClientId) +
                ":" +
                (string.IsNullOrEmpty(info.ClientInfo.UserId) || !info.KeyScope.HasFlag(ClientAuthenticationScope.User) ? string.Empty : info.ClientInfo.UserId);

            _cache.Store(key, info, null, LOCAL_CACHE_TIME, false, _logger, DefaultRealTimeProvider.Singleton);
        }

        private void DeleteLocalCache(ClientKeyIdentifier info)
        {
            string key = (string.IsNullOrEmpty(info.ClientId) || !info.Scope.HasFlag(ClientAuthenticationScope.Client) ? string.Empty : info.ClientId) +
                ":" +
                (string.IsNullOrEmpty(info.UserId) || !info.Scope.HasFlag(ClientAuthenticationScope.User) ? string.Empty : info.UserId);

            _cache.Delete(key, false, _logger);
        }

        private RetrieveResult<ServerSideAuthenticationState> GetFromLocalCache(ClientKeyIdentifier keyId)
        {
            string key = (string.IsNullOrEmpty(keyId.ClientId) || !keyId.Scope.HasFlag(ClientAuthenticationScope.Client) ? string.Empty : keyId.ClientId) +
                ":" +
                (string.IsNullOrEmpty(keyId.UserId) || !keyId.Scope.HasFlag(ClientAuthenticationScope.User) ? string.Empty : keyId.UserId);

            return _cache.TryRetrieveTentative(key, DefaultRealTimeProvider.Singleton);
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
                        "  `ClientName` varchar(1024) DEFAULT NULL,\r\n" +
                        "  `UserId` varchar(255) NOT NULL,\r\n" +
                        "  `UserName` varchar(1024) DEFAULT NULL,\r\n" +
                        "  `Trusted` tinyint(1) NOT NULL,\r\n" +
                        "  `Key` TEXT NOT NULL,\r\n" +
                        "  `Bits` int(11) DEFAULT NULL,\r\n" +
                        "  `Salt` varchar(8192) DEFAULT NULL,\r\n" +
                        "  `RegisteredAt` datetime NOT NULL,\r\n" +
                        "  UNIQUE KEY `Id` (`ClientId`,`UserId`)\r\n" +
                        ") ENGINE=InnoDB DEFAULT CHARSET=utf8;\r\n"
                        , TABLE_NAME)
                }
            };
    }
}
