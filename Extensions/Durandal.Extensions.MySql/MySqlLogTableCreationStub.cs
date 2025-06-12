using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Extensions.MySql
{
    /// <summary>
    /// Since the logs table is implemented in multiple places, this class centralizes the MySqlDataSource stuff to
    /// ensure the table and its dependencies are properly setup
    /// </summary>
    public class MySqlLogTableCreationStub : MySqlDataSource
    {
        private const string TABLE_NAME = "logs";

        public MySqlLogTableCreationStub(MySqlConnectionPool connectionPool, ILogger metaLogger)
            : base(connectionPool, metaLogger)
        {
        }

        protected override IEnumerable<MySqlTableDefinition> Tables =>
            new List<MySqlTableDefinition>()
            {
                new MySqlTableDefinition()
                {
                    TableName = TABLE_NAME,
                    CreateStatement = string.Format(
                        "CREATE TABLE `{0}` (\r\n" +
                        "  `TraceId` VARCHAR(48) DEFAULT NULL,\r\n" +
                        "  `Timestamp` BIGINT NOT NULL,\r\n" +
                        "  `Component` VARCHAR(255) DEFAULT NULL,\r\n" +
                        "  `Level` CHAR(1) NOT NULL,\r\n" +
                        "  `Message` TEXT NOT NULL,\r\n" +
                        "  `PrivacyClass` SMALLINT UNSIGNED NOT NULL,\r\n" +
                        "  KEY `idx_logs_TraceId` (`TraceId`),\r\n" +
                        "  KEY `idx_logs_TraceEnd` (`Timestamp`)\r\n" +
                        ") ENGINE=InnoDB DEFAULT CHARSET=utf8;"
                        , TABLE_NAME)
                }
            };

        protected override IEnumerable<MySqlEventDefinition> Events =>
            new List<MySqlEventDefinition>()
            {
                new MySqlEventDefinition()
                {
                    EventName = "logs_cleanup",
                    CreateStatement = string.Format(
                        "CREATE EVENT `logs_cleanup` \r\n" +
                        "ON SCHEDULE EVERY 1 HOUR DO \r\n" +
                        "DELETE FROM {0} WHERE Timestamp < (UNIX_TIMESTAMP(DATE_SUB(UTC_TIMESTAMP(), INTERVAL 60 DAY)) * 10000000) + 621355968000000000",
                        TABLE_NAME)
                }
            };
    }
}
