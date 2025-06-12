using Durandal.Common.Logger;
using Durandal.Common.Utils;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Utils.Tasks;
using Durandal.Common.Utils.MathExt;
using Photon.Common.Schemas;
using Photon.Common.TestResultStore;

namespace Photon.Common.MySQL
{
    public class MySqlTestResultStore : ITestResultStore
    {
        private readonly MySqlConnectionPool _connectionPool;
        private readonly ILogger _logger;
        private readonly TimeSpan CONNECTION_POOL_TIMEOUT = TimeSpan.FromMilliseconds(5000);

        public MySqlTestResultStore(MySqlConnectionPool connectionPool, ILogger logger)
        {
            _logger = logger;
            _connectionPool = connectionPool;
        }

        public async Task Store(SingleTestResultInternal testResult)
        {
            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("INSERT INTO test_results(timestamp, test_name, test_suite_name, success, latency, trace_id, error_message, datacenter) " +
                        "VALUES(@TIME, @TESTNAME, @TESTSUITENAME, @SUCCESS, @LATENCY, @TRACEID, @ERROR, @DATACENTER);");
                    command.Parameters.Add("@TIME", MySqlDbType.DateTime).Value = testResult.Timestamp.UtcDateTime;
                    command.Parameters.Add("@TESTNAME", MySqlDbType.VarChar, 255).Value = testResult.TestName;
                    command.Parameters.Add("@TESTSUITENAME", MySqlDbType.VarChar, 255).Value = testResult.TestSuiteName;
                    command.Parameters.Add("@SUCCESS", MySqlDbType.Bit, 1).Value = testResult.Success;
                    command.Parameters.Add("@LATENCY", MySqlDbType.Float).Value = (float)testResult.LatencyMs;
                    command.Parameters.Add("@TRACEID", MySqlDbType.Binary, 16).Value = testResult.TraceId.ToByteArray();
                    command.Parameters.Add("@ERROR", MySqlDbType.Text).Value = testResult.ErrorMessage;
                    command.Parameters.Add("@DATACENTER", MySqlDbType.VarChar, 255).Value = testResult.DatacenterName;
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

        public async Task<TestSuiteStatus> GetSuiteTestStatus(string suiteName, TimeSpan window)
        {
            TestSuiteStatus returnVal = null;
            
            // Run an aggregate command to the get the results for each test case in the suite
            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "SELECT test_name,success,latency,timestamp,trace_id,error_message,datacenter FROM test_results WHERE test_suite_name = @SUITENAME AND timestamp > UTC_TIMESTAMP - INTERVAL @WINDOW SECOND ORDER BY timestamp DESC;";
                    command.Parameters.Add("@SUITENAME", MySqlDbType.VarChar, 255).Value = suiteName;
                    command.Parameters.Add("@WINDOW", MySqlDbType.Int32).Value = (int)window.TotalSeconds;
                    DbDataReader reader = await command.ExecuteReaderAsync();

                    returnVal = new TestSuiteStatus();
                    returnVal.SuiteName = suiteName;
                    returnVal.MonitoringWindowSize = window;

                    List<SingleTestResultInternal> individualTestResults = new List<SingleTestResultInternal>();

                    while (await reader.ReadAsync())
                    {
                        // Each row from the reader is a single test result
                        SingleTestResultInternal singleTestResult = new SingleTestResultInternal();
                        singleTestResult.TestName = reader.GetString(0);
                        singleTestResult.Success = reader.GetBoolean(1);
                        singleTestResult.LatencyMs = reader.GetFloat(2);
                        singleTestResult.Timestamp = new DateTimeOffset(reader.GetDateTime(3));
                        byte[] guidBytes = new byte[16];
                        reader.GetBytes(4, 0, guidBytes, 0, 16);
                        singleTestResult.TraceId = new Guid(guidBytes);
                        if (!reader.IsDBNull(5))
                        {
                            singleTestResult.ErrorMessage = reader.GetString(5);
                        }
                        if (!reader.IsDBNull(6))
                        {
                            singleTestResult.DatacenterName = reader.GetString(6);
                        }

                        individualTestResults.Add(singleTestResult);

                        if (!returnVal.TestResults.ContainsKey(singleTestResult.TestName))
                        {
                            TestMonitorStatus thisTestResult = new TestMonitorStatus();
                            thisTestResult.TestName = singleTestResult.TestName;
                            thisTestResult.TestSuiteName = suiteName;
                            thisTestResult.MonitoringWindowSize = window;
                            returnVal.TestResults.Add(thisTestResult.TestName, thisTestResult);
                        }
                    }

                    reader.Close();
                    
                    // Organize individual test results into test monitor status objects and calculate statistics
                    foreach (TestMonitorStatus singleMonitorStatus in returnVal.TestResults.Values)
                    {
                        CalculateTestLevelStatistics(singleMonitorStatus, individualTestResults);
                    }

                    // Now collect aggregate statistics for the suite
                    StaticAverage suiteMeanLatency = new StaticAverage();
                    StaticAverage suiteMedianLatency = new StaticAverage();
                    StaticAverage suitePassRate = new StaticAverage();
                    int suiteTotalTests = 0;
                    foreach (TestMonitorStatus testResult in returnVal.TestResults.Values)
                    {
                        suiteTotalTests += testResult.TestsRan;
                        suiteMeanLatency.Add(testResult.MeanLatency);
                        suiteMedianLatency.Add(testResult.MedianLatency);
                        suitePassRate.Add(testResult.PassRate);
                    }

                    returnVal.MeanLatency = (float)suiteMeanLatency.Average;
                    returnVal.MedianLatency = (float)suiteMedianLatency.Average;
                    returnVal.PassRate = (float)suitePassRate.Average;
                    returnVal.TestsRan = suiteTotalTests;
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
            
            return returnVal;
        }

        public async Task<TestMonitorStatus> GetTestStatus(string testName, TimeSpan window)
        {
            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "SELECT test_suite_name,success,latency,timestamp,trace_id,error_message,datacenter FROM test_results WHERE test_name = @TESTNAME AND timestamp > UTC_TIMESTAMP - INTERVAL @WINDOW SECOND ORDER BY timestamp DESC;";
                    command.Parameters.Add("@TESTNAME", MySqlDbType.VarChar, 255).Value = testName;
                    command.Parameters.Add("@WINDOW", MySqlDbType.Int32).Value = (int)window.TotalSeconds;
                    DbDataReader reader = await command.ExecuteReaderAsync();

                    if (!reader.HasRows)
                    {
                        return null;
                    }

                    List<SingleTestResultInternal> individualTestResults = new List<SingleTestResultInternal>();
                    
                    while (await reader.ReadAsync())
                    {
                        // Each row from the reader is a single test result
                        SingleTestResultInternal singleTestResult = new SingleTestResultInternal();
                        singleTestResult.TestName = testName;
                        singleTestResult.TestSuiteName = reader.GetString(0);
                        singleTestResult.Success = reader.GetBoolean(1);
                        singleTestResult.LatencyMs = reader.GetFloat(2);
                        singleTestResult.Timestamp = new DateTimeOffset(reader.GetDateTime(3));
                        byte[] guidBytes = new byte[16];
                        reader.GetBytes(4, 0, guidBytes, 0, 16);
                        singleTestResult.TraceId = new Guid(guidBytes);
                        if (!reader.IsDBNull(5))
                        {
                            singleTestResult.ErrorMessage = reader.GetString(5);
                        }
                        if (!reader.IsDBNull(6))
                        {
                            singleTestResult.DatacenterName = reader.GetString(6);
                        }

                        individualTestResults.Add(singleTestResult);
                    }

                    reader.Close();

                    TestMonitorStatus returnVal = new TestMonitorStatus();
                    returnVal.TestName = testName;
                    returnVal.TestSuiteName = individualTestResults[0].TestSuiteName;
                    returnVal.MonitoringWindowSize = window;
                    CalculateTestLevelStatistics(returnVal, individualTestResults);
                    
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
        
        public async Task<Dictionary<string, TestSuiteStatus>> GetAllSuitesStatus(TimeSpan window)
        {
            Dictionary<string, TestSuiteStatus> allSuitesStatus = new Dictionary<string, TestSuiteStatus>();
            Dictionary<string, TestMonitorStatus> allTestsStatus = new Dictionary<string, TestMonitorStatus>();

            allTestsStatus.Values.OrderBy((key) => key.TestName);

            // Run an aggregate command to the get the results for each test case that is known to the system
            PooledResource<MySqlConnection> connection = null;
            if (_connectionPool.TryGetConnection(ref connection, CONNECTION_POOL_TIMEOUT))
            {
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "SELECT test_suite_name,test_name,success,latency,timestamp,trace_id,error_message,datacenter FROM test_results WHERE timestamp > UTC_TIMESTAMP - INTERVAL @WINDOW SECOND ORDER BY timestamp DESC;";
                    command.Parameters.Add("@WINDOW", MySqlDbType.Int32).Value = (int)window.TotalSeconds;
                    DbDataReader reader = await command.ExecuteReaderAsync();

                    List<SingleTestResultInternal> individualTestResults = new List<SingleTestResultInternal>();

                    while (await reader.ReadAsync())
                    {
                        // Each row from the reader is a single test result, could be from any test or suite
                        SingleTestResultInternal singleTestResult = new SingleTestResultInternal();
                        singleTestResult.TestSuiteName = reader.GetString(0);
                        singleTestResult.TestName = reader.GetString(1);
                        singleTestResult.Success = reader.GetBoolean(2);
                        singleTestResult.LatencyMs = reader.GetFloat(3);
                        singleTestResult.Timestamp = new DateTimeOffset(reader.GetDateTime(4));
                        byte[] guidBytes = new byte[16];
                        reader.GetBytes(5, 0, guidBytes, 0, 16);
                        singleTestResult.TraceId = new Guid(guidBytes);
                        if (!reader.IsDBNull(6))
                        {
                            singleTestResult.ErrorMessage = reader.GetString(6);
                        }
                        if (!reader.IsDBNull(7))
                        {
                            singleTestResult.DatacenterName = reader.GetString(7);
                        }

                        individualTestResults.Add(singleTestResult);

                        // Aggregate test cases
                        if (!allTestsStatus.ContainsKey(singleTestResult.TestName))
                        {
                            TestMonitorStatus monitor = new TestMonitorStatus();
                            monitor.TestName = singleTestResult.TestName;
                            monitor.TestSuiteName = singleTestResult.TestSuiteName;
                            monitor.MonitoringWindowSize = window;
                            allTestsStatus.Add(monitor.TestName, monitor);
                        }

                        // Aggregate suites
                        if (!allSuitesStatus.ContainsKey(singleTestResult.TestSuiteName))
                        {
                            TestSuiteStatus suite = new TestSuiteStatus();
                            suite.SuiteName = singleTestResult.TestSuiteName;
                            suite.MonitoringWindowSize = window;
                            allSuitesStatus.Add(suite.SuiteName, suite);
                        }
                    }

                    reader.Close();

                    // Aggregate statistics per-test
                    foreach (TestMonitorStatus singleMonitorStatus in allTestsStatus.Values)
                    {
                        CalculateTestLevelStatistics(singleMonitorStatus, individualTestResults);

                        TestSuiteStatus enclosingSuite = allSuitesStatus[singleMonitorStatus.TestSuiteName];
                        enclosingSuite.TestResults.Add(singleMonitorStatus.TestName, singleMonitorStatus);
                    }

                    // Aggregate the statistics per-suite
                    foreach (TestSuiteStatus suite in allSuitesStatus.Values)
                    {
                        StaticAverage suiteMeanLatency = new StaticAverage();
                        StaticAverage suiteMedianLatency = new StaticAverage();
                        StaticAverage suitePassRate = new StaticAverage();
                        int suiteTotalTests = 0;
                        foreach (TestMonitorStatus testResult in suite.TestResults.Values)
                        {
                            suiteTotalTests += testResult.TestsRan;
                            suiteMeanLatency.Add(testResult.MeanLatency);
                            suiteMedianLatency.Add(testResult.MedianLatency);
                            suitePassRate.Add(testResult.PassRate);
                        }

                        suite.MeanLatency = (float)suiteMeanLatency.Average;
                        suite.MedianLatency = (float)suiteMedianLatency.Average;
                        suite.PassRate = (float)suitePassRate.Average;
                        suite.TestsRan = suiteTotalTests;
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

            return allSuitesStatus;
        }

        private static void CalculateTestLevelStatistics(TestMonitorStatus monitorStatus, List<SingleTestResultInternal> allTestResults)
        {
            List<double> latencies = new List<double>();
            StaticAverage meanLatency = new StaticAverage();
            StaticAverage meanSuccess = new StaticAverage();
            int runCount = 0;
            monitorStatus.LastErrors = new List<ErrorResult>();

            foreach (SingleTestResultInternal result in allTestResults)
            {
                if (string.Equals(result.TestName, monitorStatus.TestName))
                {
                    meanLatency.Add(result.LatencyMs);
                    meanSuccess.Add(result.Success ? 100 : 0);
                    latencies.Add(result.LatencyMs);
                    runCount++;

                    if (!result.Success)
                    {
                        // Process error results while we're here
                        monitorStatus.LastErrors.Add(new ErrorResult()
                        {
                            Timestamp = result.Timestamp,
                            TraceId = result.TraceId,
                            Message = result.ErrorMessage,
                            Datacenter = result.DatacenterName
                        });
                    }
                }
            }

            monitorStatus.PassRate = (float)meanSuccess.Average;
            monitorStatus.TestsRan = runCount;
            monitorStatus.MeanLatency = (float)meanLatency.Average;
            monitorStatus.MedianLatency = 0;
            if (runCount > 0)
            {
                latencies.Sort();
                monitorStatus.MedianLatency = (float)latencies[runCount / 2];
            }
        }
    }
}
