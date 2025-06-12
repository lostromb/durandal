using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.MySql
{
    /// <summary>
    /// Base class for classes that perform operations on one or more MySql tables.
    /// Provides some common initialization framework to construct databases if needed.
    /// </summary>
    public class MySqlDataSource
    {
        private WeakPointer<MySqlConnectionPool> _connectionPool;
        private ILogger _logger;

        public MySqlDataSource(MySqlConnectionPool pool, ILogger logger)
        {
            _connectionPool = new WeakPointer<MySqlConnectionPool>(pool);
            _logger = logger;
        }

        /// <summary>
        /// Inspects the current database configuration, ensures connections are valid, and that all tables &amp; coprocs exist.
        /// If tables or coprocs are not found, this method will create them automatically, according to the schema
        /// provided by the specific class.
        /// </summary>
        /// <returns></returns>
        public async Task Initialize()
        {
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    foreach (MySqlTableDefinition table in Tables)
                    {
                        await CreateTableIfNotExist(connection.Value, table, _logger);
                    }

                    foreach (MySqlProcedureDefinition procedure in Procedures)
                    {
                        await CreateProcedureIfNotExist(connection.Value, procedure, _logger);
                    }

                    foreach (MySqlEventDefinition dbEvent in Events)
                    {
                        await CreateEventIfNotExist(connection.Value, dbEvent, _logger);
                    }
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(_logger);
                }
            }
        }

        protected virtual IEnumerable<MySqlTableDefinition> Tables => new List<MySqlTableDefinition>();
        protected virtual IEnumerable<MySqlProcedureDefinition> Procedures => new List<MySqlProcedureDefinition>();
        protected virtual IEnumerable<MySqlEventDefinition> Events => new List<MySqlEventDefinition>();

        protected async Task CreateTableIfNotExist(MySqlConnection connection, MySqlTableDefinition table, ILogger logger)
        {
            MySqlCommand command = connection.CreateCommand();
            command.CommandText = string.Format("SHOW TABLES FROM `{0}` LIKE \'{1}\';", connection.Database, table.TableName);
            object result = await command.ExecuteScalarAsync();

            if (result == null)
            {
                logger.Log("CREATING SQL TABLE " + table.TableName);
                command = connection.CreateCommand();
                command.CommandText = table.CreateStatement;
                await command.ExecuteNonQueryAsync();
            }
        }

        protected async Task CreateProcedureIfNotExist(MySqlConnection connection, MySqlProcedureDefinition procedure, ILogger logger)
        {
            MySqlCommand command = connection.CreateCommand();
            command.CommandText = string.Format("SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE = \'PROCEDURE\' AND ROUTINE_SCHEMA = \'{0}\' AND ROUTINE_NAME = \'{1}\';", connection.Database, procedure.ProcedureName);
            object result = await command.ExecuteScalarAsync();

            if ((result is int && ((int)result) == 0) ||
                result is long && ((long)result) == 0)
            {
                // Build the retrieve coproc
                logger.Log("CREATING SQL PROCEDURE " + procedure.ProcedureName);
                command = connection.CreateCommand();
                command.CommandText = procedure.CreateStatement;
                await command.ExecuteNonQueryAsync();
            }
        }

        protected async Task CreateEventIfNotExist(MySqlConnection connection, MySqlEventDefinition dbEvent, ILogger logger)
        {
            MySqlCommand command = connection.CreateCommand();
            command.CommandText = string.Format("SHOW EVENTS FROM `{0}` LIKE \'{1}\';", connection.Database, dbEvent.EventName);
            object result = await command.ExecuteScalarAsync();

            if (result == null)
            {
                // Build the periodic event
                logger.Log("CREATING SQL EVENT " + dbEvent.EventName);
                command = connection.CreateCommand();
                command.CommandText = dbEvent.CreateStatement;
                result = await command.ExecuteNonQueryAsync();
            }
        }
    }
}
