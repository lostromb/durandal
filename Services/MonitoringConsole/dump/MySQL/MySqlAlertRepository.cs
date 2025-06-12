using Photon.Common.ICM;
using Durandal.Common.Logger;
using Durandal.Common.Utils.Tasks;
using Durandal.Common.Utils.Time;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.MySQL
{
    public class MySqlAlertRepository : IAlertRepository
    {
        private static readonly TimeSpan CONNECTION_POOL_TIMEOUT = TimeSpan.FromMilliseconds(5000);
        private MySqlConnectionPool _connectionPool;
        private ILogger _logger;
        private IRealTimeProvider _timeProvider;

        /// <summary>
        /// Creates a new alert repository that is backed by mysql
        /// </summary>
        public MySqlAlertRepository(MySqlConnectionPool connectionPool, ILogger logger, IRealTimeProvider timeProvider = null)
        {
            _connectionPool = connectionPool;
            _logger = logger;
            _timeProvider = timeProvider ?? new DefaultRealTimeProvider();
        }

        public async Task<DateTimeOffset?> GetMostRecentAlertTime(string teamName, AlertLevel level)
        {
            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "SELECT recent_incident_begin FROM alert_status WHERE team_name = @TEAM_NAME AND recent_incident_begin IS NOT NULL AND recent_incident_level = @INCIDENT_LEVEL ORDER BY recent_incident_begin DESC LIMIT 1;";
                    command.Parameters.Add("@TEAM_NAME", MySqlDbType.VarChar, 1000).Value = teamName;
                    command.Parameters.Add("@INCIDENT_LEVEL", MySqlDbType.Byte).Value = (int)level;
                    object scalarObj = await command.ExecuteScalarAsync();

                    if (scalarObj == null || scalarObj == DBNull.Value)
                    {
                        return null;
                    }

                    return new DateTimeOffset((long)scalarObj, TimeSpan.Zero);
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

            return null;
        }

        public async Task<IDictionary<string, SuiteAlertStatus>> GetAllSuitesAlertStatus()
        {
            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "SELECT test_name, suite_name, recent_incident_begin, recent_incident_end, recent_incident_level, default_alert_level, team_name FROM alert_status;";
                    DbDataReader reader = await command.ExecuteReaderAsync();

                    IDictionary<string, SuiteAlertStatus> returnVal = new Dictionary<string, SuiteAlertStatus>();
                    
                    while (await reader.ReadAsync())
                    {
                        TestAlertStatus testStatus = new TestAlertStatus();
                        testStatus.TestName = reader.GetString(0);
                        testStatus.SuiteName = reader.GetString(1);
                        if (!reader.IsDBNull(2))
                        {
                            testStatus.MostRecentFailureBegin = new DateTimeOffset(reader.GetInt64(2), TimeSpan.Zero);
                        }
                        if (!reader.IsDBNull(3))
                        {
                            testStatus.MostRecentFailureEnd = new DateTimeOffset(reader.GetInt64(3), TimeSpan.Zero);
                        }
                        testStatus.MostRecentFailureLevel = (AlertLevel)reader.GetByte(4);
                        testStatus.DefaultFailureLevel = (AlertLevel)reader.GetByte(5);
                        if (!reader.IsDBNull(6))
                        {
                            testStatus.OwningTeamName = reader.GetString(6);
                        }

                        if (!returnVal.ContainsKey(testStatus.SuiteName))
                        {
                            returnVal[testStatus.SuiteName] = new SuiteAlertStatus()
                            {
                                SuiteName = testStatus.SuiteName
                            };
                        }

                        returnVal[testStatus.SuiteName].TestStatus.Add(testStatus.TestName, testStatus);
                    }

                    reader.Close();
                    return returnVal;
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

            return null;
        }

        public async Task<SuiteAlertStatus> GetSuiteAlertStatus(string suiteName)
        {
            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "SELECT test_name, recent_incident_begin, recent_incident_end, recent_incident_level, default_alert_level, team_name " +
                        "FROM alert_status WHERE suite_name = @SUITE_NAME;";
                    command.Parameters.Add("@SUITE_NAME", MySqlDbType.VarChar, 1000).Value = suiteName;
                    DbDataReader reader = await command.ExecuteReaderAsync();

                    SuiteAlertStatus returnVal = new SuiteAlertStatus()
                    {
                        SuiteName = suiteName
                    };

                    while (await reader.ReadAsync())
                    {
                        TestAlertStatus testStatus = new TestAlertStatus();
                        testStatus.TestName = reader.GetString(0);
                        testStatus.SuiteName = suiteName;
                        if (!reader.IsDBNull(1))
                        {
                            testStatus.MostRecentFailureBegin = new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero);
                        }
                        if (!reader.IsDBNull(2))
                        {
                            testStatus.MostRecentFailureEnd = new DateTimeOffset(reader.GetInt64(2), TimeSpan.Zero);
                        }
                        testStatus.MostRecentFailureLevel = (AlertLevel)reader.GetByte(3);
                        testStatus.DefaultFailureLevel = (AlertLevel)reader.GetByte(4);
                        if (!reader.IsDBNull(5))
                        {
                            testStatus.OwningTeamName = reader.GetString(5);
                        }

                        returnVal.TestStatus[testStatus.TestName] = testStatus;
                    }

                    reader.Close();
                    return returnVal;
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

            return null;
        }

        public async Task<TestAlertStatus> GetTestAlertStatus(string testName)
        {
            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "SELECT suite_name, recent_incident_begin, recent_incident_end, recent_incident_level, default_alert_level, team_name " +
                        "FROM alert_status WHERE test_name = @TEST_NAME;";
                    command.Parameters.Add("@TEST_NAME", MySqlDbType.VarChar, 1000).Value = testName;
                    DbDataReader reader = await command.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        TestAlertStatus returnVal = new TestAlertStatus();
                        returnVal.TestName = testName;
                        returnVal.SuiteName = reader.GetString(0);
                        if (!reader.IsDBNull(1))
                        {
                            returnVal.MostRecentFailureBegin = new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero);
                        }
                        if (!reader.IsDBNull(2))
                        {
                            returnVal.MostRecentFailureEnd = new DateTimeOffset(reader.GetInt64(2), TimeSpan.Zero);
                        }
                        returnVal.MostRecentFailureLevel = (AlertLevel)reader.GetByte(3);
                        returnVal.DefaultFailureLevel = (AlertLevel)reader.GetByte(4);
                        if (!reader.IsDBNull(5))
                        {
                            returnVal.OwningTeamName = reader.GetString(5);
                        }

                        reader.Close();
                        return returnVal;
                    }
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

            return null;
        }

        public async Task UpdateTestAlertConfig(string testName, string suiteName, AlertLevel newDefaultLevel)
        {
            DateTimeOffset currentTime = _timeProvider.Time;

            // Get existing alert status for this test
            TestAlertStatus testAlertStatus = await GetTestAlertStatus(testName);

            if (testAlertStatus == null)
            {
                testAlertStatus = new TestAlertStatus()
                {
                    TestName = testName,
                    SuiteName = suiteName,
                    DefaultFailureLevel = newDefaultLevel,
                    MostRecentFailureBegin = null,
                    MostRecentFailureEnd = null,
                    MostRecentFailureLevel = AlertLevel.NoAlert,
                    OwningTeamName = null
                };
            }
            
            testAlertStatus.DefaultFailureLevel = newDefaultLevel;

            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("INSERT INTO alert_status" +
                        "(test_name, suite_name, recent_incident_begin, recent_incident_end, recent_incident_level, default_alert_level, team_name) " +
                        "VALUES(@TEST_NAME, @SUITE_NAME, @INCIDENT_BEGIN, @INCIDENT_END, @INCIDENT_LEVEL, @DEFAULT_LEVEL, @TEAM_NAME)" +
                        "ON DUPLICATE KEY UPDATE " +
                        "default_alert_level = @DEFAULT_LEVEL;");

                    command.Parameters.Add("@TEST_NAME", MySqlDbType.VarChar, 255).Value = testAlertStatus.TestName;
                    command.Parameters.Add("@SUITE_NAME", MySqlDbType.VarChar, 255).Value = testAlertStatus.SuiteName;
                    if (testAlertStatus.MostRecentFailureBegin.HasValue)
                    {
                        command.Parameters.Add("@INCIDENT_BEGIN", MySqlDbType.Int64).Value = testAlertStatus.MostRecentFailureBegin.Value.Ticks;
                    }
                    else
                    {
                        command.Parameters.Add("@INCIDENT_BEGIN", MySqlDbType.Int64).Value = DBNull.Value;
                    }
                    if (testAlertStatus.MostRecentFailureEnd.HasValue)
                    {
                        command.Parameters.Add("@INCIDENT_END", MySqlDbType.Int64).Value = testAlertStatus.MostRecentFailureEnd.Value.Ticks;
                    }
                    else
                    {
                        command.Parameters.Add("@INCIDENT_END", MySqlDbType.Int64).Value = DBNull.Value;
                    }

                    command.Parameters.Add("@INCIDENT_LEVEL", MySqlDbType.Byte).Value = (int)testAlertStatus.MostRecentFailureLevel;
                    command.Parameters.Add("@DEFAULT_LEVEL", MySqlDbType.Byte).Value = (int)testAlertStatus.DefaultFailureLevel;
                    command.Parameters.Add("@TEAM_NAME", MySqlDbType.VarChar, 1000).Value = testAlertStatus.OwningTeamName;
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
        }

        public async Task StoreAlertEvent(string testName, string suiteName, AlertLevel failingLevel, string responsibleTeamName)
        {
            DateTimeOffset currentTime = _timeProvider.Time;

            // Get existing alert status for this test
            TestAlertStatus testAlertStatus = await GetTestAlertStatus(testName);

            if (testAlertStatus == null)
            {
                testAlertStatus = new TestAlertStatus()
                {
                    TestName = testName,
                    SuiteName = suiteName,
                    DefaultFailureLevel = AlertLevel.NoAlert,
                    MostRecentFailureBegin = currentTime,
                    MostRecentFailureEnd = currentTime,
                    MostRecentFailureLevel = failingLevel,
                    OwningTeamName = responsibleTeamName
                };
            }

            // Does this event correlate to any previous failure? If not, mark this as the beginning of a new alert event
            if (!testAlertStatus.MostRecentFailureBegin.HasValue ||
                !testAlertStatus.MostRecentFailureEnd.HasValue ||
                testAlertStatus.MostRecentFailureEnd.Value < currentTime - AlertEventProcessor.ALERT_CORRELATION_WINDOW)
            {
                testAlertStatus.MostRecentFailureBegin = currentTime;
            }

            testAlertStatus.MostRecentFailureLevel = failingLevel;
            testAlertStatus.MostRecentFailureEnd = currentTime;
            testAlertStatus.OwningTeamName = responsibleTeamName;

            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("INSERT INTO alert_status" +
                        "(test_name, suite_name, recent_incident_begin, recent_incident_end, recent_incident_level, default_alert_level, team_name) " +
                        "VALUES(@TEST_NAME, @SUITE_NAME, @INCIDENT_BEGIN, @INCIDENT_END, @INCIDENT_LEVEL, @DEFAULT_LEVEL, @TEAM_NAME)" +
                        "ON DUPLICATE KEY UPDATE " +
                        "recent_incident_begin = @INCIDENT_BEGIN," +
                        "recent_incident_end = @INCIDENT_END," +
                        "recent_incident_level = @INCIDENT_LEVEL," +
                        "default_alert_level = @DEFAULT_LEVEL," +
                        "team_name = @TEAM_NAME;");

                    command.Parameters.Add("@TEST_NAME", MySqlDbType.VarChar, 255).Value = testAlertStatus.TestName;
                    command.Parameters.Add("@SUITE_NAME", MySqlDbType.VarChar, 255).Value = testAlertStatus.SuiteName;
                    if (testAlertStatus.MostRecentFailureBegin.HasValue)
                    {
                        command.Parameters.Add("@INCIDENT_BEGIN", MySqlDbType.Int64).Value = testAlertStatus.MostRecentFailureBegin.Value.Ticks;
                    }
                    else
                    {
                        command.Parameters.Add("@INCIDENT_BEGIN", MySqlDbType.Int64).Value = DBNull.Value;
                    }
                    if (testAlertStatus.MostRecentFailureEnd.HasValue)
                    {
                        command.Parameters.Add("@INCIDENT_END", MySqlDbType.Int64).Value = testAlertStatus.MostRecentFailureEnd.Value.Ticks;
                    }
                    else
                    {
                        command.Parameters.Add("@INCIDENT_END", MySqlDbType.Int64).Value = DBNull.Value;
                    }

                    command.Parameters.Add("@INCIDENT_LEVEL", MySqlDbType.Byte).Value = (int)testAlertStatus.MostRecentFailureLevel;
                    command.Parameters.Add("@DEFAULT_LEVEL", MySqlDbType.Byte).Value = (int)testAlertStatus.DefaultFailureLevel;
                    if (string.IsNullOrEmpty(testAlertStatus.OwningTeamName))
                    {
                        command.Parameters.Add("@TEAM_NAME", MySqlDbType.VarChar, 1000).Value = DBNull.Value;
                    }
                    else
                    {
                        command.Parameters.Add("@TEAM_NAME", MySqlDbType.VarChar, 1000).Value = testAlertStatus.OwningTeamName;
                    }

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
        }
    }
}
