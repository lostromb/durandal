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
using Durandal.Common.Test.FVT;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.MySql
{
    public class MySqlFunctionalTestIdentityStore : MySqlDataSource, IFunctionalTestIdentityStore
    {
        private readonly TimeSpan MAX_TIME_TO_GET_CONNECTION = TimeSpan.FromSeconds(5);
        private const string TABLE_NAME = "test_identities";
        private readonly WeakPointer<MySqlConnectionPool> _connectionPool;
        private readonly ILogger _logger;

        public MySqlFunctionalTestIdentityStore(
            MySqlConnectionPool connectionPool,
            IMetricCollector metrics,
            DimensionSet dimensions,
            ILogger logger)
                : base(connectionPool, logger)
        {
            _logger = logger;
            _connectionPool = new WeakPointer<MySqlConnectionPool>(connectionPool);
        }

        public async Task InsertRandomIdentities(ILogger traceLogger)
        {
            string[] possibleFeatures = new string[] { "usagelogs", "complaint", "aadlogin", "msalogin", "fitbit_auth", "securethumbnails", "mobileenabled", "threshold", "lgv2api", "adhoc_account", "nodisplay", "revoked_certificate", "outlook_enabled" };
            FastRandom rand = new FastRandom();

            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    byte[] pkey = new byte[4000];
                    HashSet<string> features = new HashSet<string>();
                    for (int loop = 0; loop < 5000; loop++)
                    {
                        traceLogger.Log("Insert user " + loop);
                        string userId = "testuser:" + Guid.NewGuid().ToString("N");
                        string clientId = "testclient:" + Guid.NewGuid().ToString("N");
                        rand.NextBytes(pkey);
                        string privateKey = BinaryHelpers.ToHexString(pkey);
                        features.Clear();
                        while (rand.NextBool())
                        {
                            int featureId = rand.NextInt(0, possibleFeatures.Length);
                            if (!features.Contains(possibleFeatures[featureId]))
                            {
                                features.Add(possibleFeatures[featureId]);
                            }
                        }

                        string featureString = "," + string.Join(",", features) + ",";
                        using (MySqlCommand command = connection.Value.CreateCommand())
                        {
                            command.CommandText = string.Format("INSERT INTO test_identities(UserId, PrivateKey, UserFeatures, UserFeatureCount) VALUES(\'{0}\', \'{1}\', \'{2}\', {3});",
                                MySqlHelper.EscapeString(userId),
                                MySqlHelper.EscapeString(privateKey),
                                MySqlHelper.EscapeString(featureString),
                                features.Count);
                            await command.ExecuteNonQueryAsync();
                        }

                        traceLogger.Log("Insert client " + loop);
                        rand.NextBytes(pkey);
                        privateKey = BinaryHelpers.ToHexString(pkey);
                        features.Clear();
                        while (rand.NextBool())
                        {
                            int featureId = rand.NextInt(0, possibleFeatures.Length);
                            if (!features.Contains(possibleFeatures[featureId]))
                            {
                                features.Add(possibleFeatures[featureId]);
                            }
                        }

                        featureString = "," + string.Join(",", features) + ",";
                        using (MySqlCommand command = connection.Value.CreateCommand())
                        {
                            command.CommandText = string.Format("INSERT INTO test_identities(ClientId, PrivateKey, ClientFeatures, ClientFeatureCount) VALUES(\'{0}\', \'{1}\', \'{2}\', {3});",
                                MySqlHelper.EscapeString(clientId),
                                MySqlHelper.EscapeString(privateKey),
                                MySqlHelper.EscapeString(featureString),
                                features.Count);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }
                catch (Exception e)
                {
                    traceLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(traceLogger);
                }
            }
            else
            {
                traceLogger.Log("FAILED to get MySql connection", LogLevel.Err);
            }
        }

        public async Task<FunctionalTestIdentityPair> GetIdentities(FunctionalTestFeatureConstraints userConstraints, FunctionalTestFeatureConstraints clientConstraints, ILogger traceLogger)
        {
            await DurandalTaskExtensions.NoOpTask;
            return null;
            // Table layout:
            // User ID
            // Client ID
            // Scope?
            // Key
            // Constraints (comma-separated)
            // 

            // Step 1: Query the db for a list of likely candidates

            // Step 2: Acquire a lock to one of the candidates on the list

            // Step 3: Retry if necessary

            //PooledResource<MySqlConnection> connection = null;
            //if (_connectionPool.Value.TryGetConnection(MAX_TIME_TO_GET_CONNECTION, out connection))
            //{
            //    try
            //    {
            //        MySqlCommand command = connection.Value.CreateCommand();
            //        command.CommandText = "cache_retrieve";
            //        command.CommandType = CommandType.StoredProcedure;
            //        MySqlParameter keyParam = new MySqlParameter("@p_id", MySqlDbType.VarChar, 48)
            //        {
            //            Direction = ParameterDirection.Input,
            //            Value = key
            //        };
            //        MySqlParameter timeoutParam = new MySqlParameter("@p_timeout", MySqlDbType.Float)
            //        {
            //            Direction = ParameterDirection.Input,
            //            Value = (float)maxSpinTime.GetValueOrDefault(TimeSpan.Zero).TotalSeconds
            //        };
            //        MySqlParameter returnValParam = new MySqlParameter("@p_returnVal", MySqlDbType.MediumBlob)
            //        {
            //            Direction = ParameterDirection.Output
            //        };
            //        command.Parameters.Add(keyParam);
            //        command.Parameters.Add(timeoutParam);
            //        command.Parameters.Add(returnValParam);
            //        await command.ExecuteScalarAsync();
            //        if (returnValParam.Value != null && returnValParam.Value is byte[])
            //        {
            //            byte[] readBlob = (byte[])returnValParam.Value;
            //            returnVal.Result = _serializer.Decode(readBlob, 0, readBlob.Length);
            //            returnVal.Success = true;
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        queryLogger.Log(e, LogLevel.Err);
            //    }
            //    finally
            //    {
            //        _connectionPool.Value.ReleaseConnection(connection);
            //    }
            //}
            //else
            //{
            //    queryLogger.Log("FAILED to get MySql connection", LogLevel.Err);
            //}

            //return returnVal;
        }

        public async Task ReleaseIdentities(FunctionalTestIdentityPair identities, ILogger traceLogger)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "UPDATE {0} SET Reserved = 0 WHERE ClientId=\"\" AND UserId=\"\"";
                    await command.ExecuteNonQueryAsync();
                    timer.Stop();
                }
                catch (Exception e)
                {
                    traceLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(traceLogger);
                }
            }
            else
            {
                //queryLogger.Log("FAILED to get MySql connection", LogLevel.Err);
            }
        }

        protected override IEnumerable<MySqlTableDefinition> Tables =>
            new List<MySqlTableDefinition>()
            {
                //new MySqlTableDefinition()
                //{
                //    TableName = TABLE_NAME,
                //    CreateStatement = string.Format(
                //        "CREATE TABLE `{0}` (\r\n" +
                //        "  `Id` varchar(48) NOT NULL,\r\n" +
                //        "  `Value` mediumblob,\r\n" +
                //        "  `ExpireTime` datetime DEFAULT NULL,\r\n" +
                //        "  `LifetimeSeconds` int(11) DEFAULT NULL,\r\n" +
                //        "  `TraceId` varchar(48) DEFAULT NULL,\r\n" +
                //        "  PRIMARY KEY(`Id`),\r\n" +
                //        "  UNIQUE KEY `Id` (`Id`),\r\n" +
                //        "  KEY `TraceId` (`TraceId`)\r\n" +
                //        ") ENGINE = InnoDB DEFAULT CHARSET = utf8;\r\n"
                //        , TABLE_NAME)
                //}
            };

        protected override IEnumerable<MySqlProcedureDefinition> Procedures =>
            new List<MySqlProcedureDefinition>()
            {
                //new MySqlProcedureDefinition()
                //{
                //    ProcedureName = "cache_retrieve",
                //    CreateStatement = string.Format(
                //        "CREATE PROCEDURE `cache_retrieve`(\r\n" +
                //        "    IN p_id VARCHAR(48),\r\n" +
                //        "    IN p_timeout FLOAT,\r\n" +
                //        "    OUT p_returnVal MEDIUMBLOB)\r\n" +
                //        "BEGIN\r\n" +
                //        "    DECLARE v_lifeTime INT;\r\n" +
                //        "    DECLARE v_expiresAt DATETIME;\r\n" +
                //        "    DECLARE v_rowsFound INT;\r\n" +
                //        "    DECLARE v_retryCount INT;\r\n" +
                //        "    DECLARE v_backoffTime FLOAT;\r\n" +
                //        "    DECLARE v_newExpireTime DATETIME;\r\n" +
                //        "    SET p_returnVal = NULL;\r\n" +
                //        "    SET v_rowsFound = 0;\r\n" +
                //        "    SET v_backoffTime = 0.002;\r\n" +
                //        "    WHILE v_rowsFound = 0 AND p_timeout >= 0 DO\r\n" +
                //        "        # Try to select the row\r\n" +
                //        "        SELECT count(Id),Value,ExpireTime,LifetimeSeconds INTO v_rowsFound,p_returnVal,v_expiresAt,v_lifeTime FROM `{0}` WHERE Id = p_id LIMIT 1;\r\n" +
                //        "        IF v_rowsFound > 0 THEN\r\n" +
                //        "            # Found an entry. Has it expired?\r\n" +
                //        "            IF v_expiresAt IS NULL OR v_expiresAt > UTC_TIMESTAMP() THEN\r\n" +
                //        "                # Still valid. Check if it has a TTL and we need to touch it\r\n" +
                //        "                IF v_lifeTime IS NOT NULL THEN\r\n" +
                //        "                    SET v_newExpireTime = UTC_TIMESTAMP() + INTERVAL v_lifeTime SECOND;\r\n" +
                //        "                    # Make sure we don't shorten the expire time\r\n" +
                //        "                    IF v_expiresAt < v_newExpireTime THEN\r\n" +
                //        "                        UPDATE `{0}` SET ExpireTime=v_newExpireTime WHERE Id = p_id;\r\n" +
                //        "                    END IF;\r\n" +
                //        "                END IF;\r\n" +
                //        "            ELSE\r\n" +
                //        "                SET p_returnVal = NULL;\r\n" +
                //        "            END IF;\r\n" +
                //        "        ELSEIF p_timeout >= 0 THEN\r\n" +
                //        "            # Row does not exist; spinwait\r\n" +
                //        "            DO SLEEP(v_backoffTime);\r\n" +
                //        "            SET p_timeout = p_timeout - v_backoffTime;\r\n" +
                //        "            # Increase the timeout by 2ms on each successive cache miss to reduce the spinning cost on long waits\r\n" +
                //        "            SET v_backoffTime = v_backoffTime + 0.002;\r\n" +
                //        "        END IF;\r\n" +
                //        "    END WHILE;\r\n" +
                //        "END\r\n",
                //        TABLE_NAME)
                //}
            };


        protected override IEnumerable<MySqlEventDefinition> Events =>
            new List<MySqlEventDefinition>()
            {
                //new MySqlEventDefinition()
                //{
                //    EventName = "cache_cleanup",
                //    CreateStatement = string.Format(
                //        "CREATE EVENT `cache_cleanup`\r\n" +
                //        "ON SCHEDULE EVERY 1 HOUR DO\r\n" +
                //        "DELETE FROM `{0}` WHERE ExpireTime < UTC_TIMESTAMP()",
                //        TABLE_NAME)
                //}
            };
    }
}
