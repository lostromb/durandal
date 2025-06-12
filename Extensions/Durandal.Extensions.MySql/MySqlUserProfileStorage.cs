using Durandal.Common.Collections;
using Durandal.Common.Dialog.Services;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace Durandal.Extensions.MySql
{
    public class MySqlUserProfileStorage : MySqlDataSource, IUserProfileStorage
    {
        private static readonly TimeSpan MAX_TIME_TO_GET_CONNECTION = TimeSpan.FromMilliseconds(5000);
        private const string TABLE_NAME = "userprofiles";
        private readonly WeakPointer<MySqlConnectionPool> _connectionPool;
        private ILogger _logger;
        private UserProfileSerializer _serializer;

        public MySqlUserProfileStorage(MySqlConnectionPool connectionPool, ILogger logger) : base(connectionPool, logger)
        {
            _logger = logger;
            _connectionPool = new WeakPointer<MySqlConnectionPool>(connectionPool);
            _serializer = new UserProfileSerializer();
        }

        /// <summary>
        /// Upserts the specified user profile objects. Writing a null value is equivalent to deletion.
        /// </summary>
        /// <param name="profilesToUpdate">The set of one or more profiles to update.</param>
        /// <param name="profiles">The collection of profiles.</param>
        /// <param name="userId">The user ID of the profile we're updating</param>
        /// <param name="domain">If updating a plugin-local profile, this is the domain associated with that profile.. Otherwise this may be null</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <returns>True if the write succeeded</returns>
        public async Task<bool> UpdateProfiles(UserProfileType profilesToUpdate, UserProfileCollection profiles, string userId, string domain, ILogger queryLogger)
        {
            // Check if we actually have to do anything
            if ((!profilesToUpdate.HasFlag(UserProfileType.PluginLocal) || profiles.LocalProfile == null || !profiles.LocalProfile.Touched) &&
                (!profilesToUpdate.HasFlag(UserProfileType.PluginGlobal) || profiles.GlobalProfile == null || !profiles.GlobalProfile.Touched) &&
                (!profilesToUpdate.HasFlag(UserProfileType.EntityHistoryGlobal) || profiles.EntityHistory == null || !profiles.EntityHistory.Touched))
            {
                return true;
            }

            // OPT: I can do all of these writes in parallel if needed
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    if (profilesToUpdate.HasFlag(UserProfileType.PluginLocal) && (profiles.LocalProfile == null || profiles.LocalProfile.Touched))
                    {
                        MySqlCommand command = connection.Value.CreateCommand();
                        if (profiles.LocalProfile != null && profiles.LocalProfile.Count > 0)
                        {
                            // If values exist, update the table
                            byte[] serializedProfile = _serializer.Encode(profiles.LocalProfile);
                            command.CommandText = string.Format("INSERT INTO userprofiles(UserId, Domain, Value, LastUpdateTime) " +
                                "VALUES(\'{0}\', \'{1}\', @Blob, UTC_TIMESTAMP()) " +
                                "ON DUPLICATE KEY UPDATE Value=VALUES(Value),LastUpdateTime=UTC_TIMESTAMP();",
                                userId,
                                domain);
                            command.Parameters.Add(new MySqlParameter("@Blob", serializedProfile));
                        }
                        else
                        {
                            // Empty profile; delete from table
                            queryLogger.Log("Local user profile for " + domain + " is empty and so will be deleted from SQL table", LogLevel.Std);
                            command.CommandText = string.Format("DELETE FROM userprofiles WHERE UserId = \'{0}\' AND Domain = \'{1}\';",
                                userId,
                                domain);
                        }

                        await command.ExecuteNonQueryAsync();
                    }

                    if (profilesToUpdate.HasFlag(UserProfileType.PluginGlobal) && (profiles.GlobalProfile == null || profiles.GlobalProfile.Touched))
                    {
                        MySqlCommand command = connection.Value.CreateCommand();
                        if (profiles.GlobalProfile != null && profiles.GlobalProfile.Count > 0)
                        {
                            // If values exist, update the table
                            byte[] serializedProfile = _serializer.Encode(profiles.GlobalProfile);
                            command.CommandText = string.Format("INSERT INTO userprofiles(UserId, Domain, Value, LastUpdateTime) " +
                                "VALUES(\'{0}\', \'\', @Blob, UTC_TIMESTAMP()) " +
                                "ON DUPLICATE KEY UPDATE Value=VALUES(Value),LastUpdateTime=UTC_TIMESTAMP();",
                                userId);
                            command.Parameters.Add(new MySqlParameter("@Blob", serializedProfile));
                        }
                        else
                        {
                            // Empty profile; delete from table
                            queryLogger.Log("Global user profile is empty and so will be deleted from SQL table", LogLevel.Std);
                            command.CommandText = string.Format("DELETE FROM userprofiles WHERE UserId = \'{0}\' AND Domain = \'\';",
                                userId,
                                domain);
                        }

                        await command.ExecuteNonQueryAsync();
                    }

                    if (profilesToUpdate.HasFlag(UserProfileType.EntityHistoryGlobal) && (profiles.EntityHistory == null || profiles.EntityHistory.Touched))
                    {
                        MySqlCommand command = connection.Value.CreateCommand();
                        if (profiles.EntityHistory != null && profiles.EntityHistory.Count > 0)
                        {
                            // If values exist, update the table
                            using (PooledBuffer<byte> serializedProfile = profiles.EntityHistory.Serialize())
                            {
                                byte[] copiedData = new byte[serializedProfile.Length];
                                ArrayExtensions.MemCopy(serializedProfile.Buffer, 0, copiedData, 0, copiedData.Length);
                                command.CommandText = string.Format("INSERT INTO userprofiles(UserId, Domain, Value, LastUpdateTime) " +
                                    "VALUES(\'{0}\', \'.globalhistory\', @Blob, UTC_TIMESTAMP()) " +
                                    "ON DUPLICATE KEY UPDATE Value=VALUES(Value),LastUpdateTime=UTC_TIMESTAMP();",
                                    userId);
                                command.Parameters.Add(new MySqlParameter("@Blob", copiedData));
                            }
                        }
                        else
                        {
                            // Empty profile; delete from table
                            queryLogger.Log("Global history is empty and so will be deleted from SQL table", LogLevel.Std);
                            command.CommandText = string.Format("DELETE FROM userprofiles WHERE UserId = \'{0}\' AND Domain = \'.globalhistory\';",
                                userId,
                                domain);
                        }

                        await command.ExecuteNonQueryAsync();
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log("Error while updating user profile", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                    return false;
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to update user profile", LogLevel.Err);
            }

            return true;
        }

        /// <summary>
        /// Gets one or more profiles for the given user
        /// </summary>
        /// <param name="profilesToFetch">The set of profiles (local, global, etc.) to fetch</param>
        /// <param name="userId">The user ID of the profile we're fetching</param>
        /// <param name="domain">If retrieving plugin-local profile, this is the domain we want. Otherwise this may be null</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <returns>A user profile collection containing the results</returns>
        public async Task<RetrieveResult<UserProfileCollection>> GetProfiles(UserProfileType profilesToFetch, string userId, string domain, ILogger queryLogger)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    // This command should return 3 rows, one for local, one for global, and one for global history
                    command.CommandText = string.Format("SELECT Domain,Value FROM userprofiles WHERE UserId = \'{0}\' AND (Domain = \'{1}\' OR Domain = \'\' OR Domain = \'.globalhistory\');", userId, domain);
                    DbDataReader reader = await command.ExecuteReaderAsync();
                    InMemoryDataStore localProfile = null;
                    InMemoryDataStore globalProfile = null;
                    InMemoryEntityHistory entityHistory = null;
                    while (reader.Read())
                    {
                        string rowDomain = reader.GetString(0);
                        if (string.IsNullOrEmpty(rowDomain))
                        {
                            byte[] data = reader.GetFieldValue<byte[]>(1);
                            globalProfile = _serializer.Decode(data, 0, data.Length);
                        }
                        else if (string.Equals(rowDomain, domain))
                        {
                            byte[] data = reader.GetFieldValue<byte[]>(1);
                            localProfile = _serializer.Decode(data, 0, data.Length);
                        }
                        else if (string.Equals(rowDomain, ".globalhistory"))
                        {
                            byte[] data = reader.GetFieldValue<byte[]>(1);
                            entityHistory = InMemoryEntityHistory.Deserialize(data, 0, data.Length);
                        }
                    }
                    reader.Dispose();

                    UserProfileCollection rr = new UserProfileCollection(localProfile, globalProfile, entityHistory);
                    timer.Stop();
                    return new RetrieveResult<UserProfileCollection>(rr, timer.ElapsedMillisecondsPrecise());
                }
                catch (Exception e)
                {
                    queryLogger.Log("Error while retrieving user profiles from db", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to get user profile", LogLevel.Err);
            }

            timer.Stop();
            return new RetrieveResult<UserProfileCollection>(null, timer.ElapsedMillisecondsPrecise(), false);
        }

        /// <summary>
        /// Clears one or more profiles for a given user
        /// </summary>
        /// <param name="profilesToDelete">The set of profiles to delete</param>
        /// <param name="userId">The user ID</param>
        /// <param name="domain">If clearing a plugin-local profile, this is the domain associated with that profile.. Otherwise this may be null</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <returns>True if the write succeeded</returns>
        public async Task<bool> ClearProfiles(UserProfileType profilesToDelete, string userId, string domain, ILogger queryLogger)
        {
            // OPT: I can do all of these writes in parallel if needed
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    if (profilesToDelete.HasFlag(UserProfileType.PluginLocal))
                    {
                        MySqlCommand command = connection.Value.CreateCommand();
                        command.CommandText = string.Format("DELETE FROM userprofiles WHERE UserId = \'{0}\' AND Domain = \'{1}\';",
                            userId,
                            domain);

                        await command.ExecuteNonQueryAsync();
                    }

                    if (profilesToDelete.HasFlag(UserProfileType.PluginGlobal))
                    {
                        MySqlCommand command = connection.Value.CreateCommand();
                        command.CommandText = string.Format("DELETE FROM userprofiles WHERE UserId = \'{0}\' AND Domain = \'\';",
                            userId,
                            domain);

                        await command.ExecuteNonQueryAsync();
                    }

                    if (profilesToDelete.HasFlag(UserProfileType.EntityHistoryGlobal))
                    {
                        MySqlCommand command = connection.Value.CreateCommand();
                        command.CommandText = string.Format("DELETE FROM userprofiles WHERE UserId = \'{0}\' AND Domain = \'.globalhistory\';",
                            userId,
                            domain);

                        await command.ExecuteNonQueryAsync();
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log("Error while updating user profile", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                    return false;
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to delete user profile", LogLevel.Err);
            }

            return true;
        }

        protected override IEnumerable<MySqlTableDefinition> Tables =>
            new List<MySqlTableDefinition>()
            {
                new MySqlTableDefinition()
                {
                    TableName = TABLE_NAME,
                    CreateStatement = string.Format(
                        "CREATE TABLE `{0}` (\r\n" +
                        "  `UserId` varchar(255) NOT NULL,\r\n" +
                        "  `Domain` varchar(255) NOT NULL,\r\n" +
                        "  `Value` mediumblob,\r\n" +
                        "  `LastUpdateTime` datetime DEFAULT NULL,\r\n" +
                        "  UNIQUE KEY `Id` (`UserId`,`Domain`),\r\n" +
                        "  KEY `ageIdx` (`LastUpdateTime`)\r\n" +
                        ") ENGINE=InnoDB DEFAULT CHARSET=utf8;\r\n"
                        , TABLE_NAME)
                }
            };
        
        protected override IEnumerable<MySqlEventDefinition> Events =>
            new List<MySqlEventDefinition>()
            {
                new MySqlEventDefinition()
                {
                    EventName = "userprofile_cleanup",
                    CreateStatement = string.Format(
                        "CREATE EVENT `userprofile_cleanup`\r\n" +
                        "ON SCHEDULE EVERY 1 HOUR DO\r\n" +
                        "DELETE FROM {0} WHERE LastUpdateTime < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 365 DAY);",
                        TABLE_NAME)
                }
            };
    }
}
