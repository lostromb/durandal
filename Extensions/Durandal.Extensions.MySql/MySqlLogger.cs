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
using Durandal.Common.Tasks;
using Durandal.API;
using Durandal.Common.Instrumentation;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using System.Numerics;

namespace Durandal.Extensions.MySql
{
    /// <summary>
    /// Represents a logger that is backed by a MySql database. Logs are written in batches of up to 10000 events each,
    /// using MySql bulk upload functionality.
    /// </summary>
    public class MySqlLogger : LoggerBase
    {
        /// <summary>
        /// Number of log lines to upload at a time
        /// </summary>
        private const int MAX_LOG_LINES_PER_BATCH = 10000;

        /// <summary>
        /// Minimum number of ms between each log upload attempt.
        /// Actual rate can be slower if repeated failures trigger backoff logic
        /// </summary>
        private static readonly TimeSpan INTERVAL_PER_BATCH_UPLOAD = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Maximum number of log lines to cache locally, not uploaded, before starting to throw away the oldest entries.
        /// Typically this only comes into play if the sql server is not responding for a period of time and the local logger's queue starts to fill up
        /// </summary>
        private const int MAX_LOG_BACKLOG_LENGTH = 100000;
        
        public MySqlLogger(
            MySqlConnectionPool connectionPool,
            string componentName = "Main",
            IThreadPool backgroundLogThreadPool = null,
            ILogger bootstrapLogger = null,
            IMetricCollector metrics = null,
            DimensionSet dimensions = null,
            LogLevel validLogLevels = DEFAULT_LOG_LEVELS,
            LogLevel maxLogLevels = LogLevel.All,
            DataPrivacyClassification maxPrivacyClasses = DataPrivacyClassification.All,
            DataPrivacyClassification defaultPrivacyClass = DataPrivacyClassification.SystemMetadata)
            : base(new SqlLoggerCore(connectionPool, bootstrapLogger, metrics, dimensions),
                new LoggerContext()
                {
                    ComponentName = componentName,
                    TraceId = null,
                    ValidLogLevels = validLogLevels,
                    MaxLogLevels = maxLogLevels,
                    DefaultPrivacyClass = defaultPrivacyClass,
                    ValidPrivacyClasses = DataPrivacyClassification.All,
                    MaxPrivacyClasses = maxPrivacyClasses,
                    BackgroundLoggingThreadPool = backgroundLogThreadPool
                })
        {
        }
        
        public Task Initialize()
        {
            return ((SqlLoggerCore)Core).Initialize();
        }

        /// <summary>
        /// Private constructor for creating inherited logger objects
        /// </summary>
        /// <param name="core">Logger core</param>
        /// <param name="context">Cloned context</param>
        private MySqlLogger(ILoggerCore core, LoggerContext context)
                : base(core, context)
        {
        }

        protected override ILogger CloneImplementation(ILoggerCore core, LoggerContext context)
        {
            return new MySqlLogger(core, context);
        }

        private class SqlLoggerCore : BatchedDataProcessor<PooledLogEvent>, ILoggerCore
        {
            private static readonly TimeSpan MAX_TIME_TO_GET_CONNECTION = TimeSpan.FromMilliseconds(5000);
            private static readonly ILogger BOOTSTRAP_LOGGER = new DebugLogger("MySqlLogger");
            private static readonly char[] ESCAPED_CHARS = new char[] { '\n', '\r', '\\', '\"', '\'' };
            private readonly WeakPointer<MySqlConnectionPool> _connectionPool;
            private readonly MySqlLogTableCreationStub _tableCreationStub;
            private bool _initialized;

            public SqlLoggerCore(MySqlConnectionPool connectionPool, ILogger bootstrapLogger, IMetricCollector metrics, DimensionSet dimensions)
                : base(
                    nameof(MySqlLogger),
                    new BatchedDataProcessorConfig()
                    {
                        BatchSize = MAX_LOG_LINES_PER_BATCH,
                        MaxSimultaneousProcesses = 1,
                        DesiredInterval = INTERVAL_PER_BATCH_UPLOAD,
                        MinimumBackoffTime = TimeSpan.FromSeconds(1),
                        MaximumBackoffTime = TimeSpan.FromSeconds(300),
                        MaxBacklogSize = MAX_LOG_BACKLOG_LENGTH,
                        AllowDroppedItems = true,
                    },
                    DefaultRealTimeProvider.Singleton,
                    bootstrapLogger,
                    metrics,
                    dimensions)
            {
                _connectionPool = new WeakPointer<MySqlConnectionPool>(connectionPool);
                _tableCreationStub = new MySqlLogTableCreationStub(connectionPool, NullLogger.Singleton);
                _initialized = false;
            }

            public Task Initialize()
            {
                if (!_initialized)
                {
                    _initialized = true;
                    return _tableCreationStub.Initialize();
                }
                else
                {
                    return DurandalTaskExtensions.NoOpTask;
                }
            }

            public void LoggerImplementation(PooledLogEvent e)
            {
                Ingest(e);
            }

            public Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking)
            {
                return base.Flush(realTime, blocking ? TimeSpan.FromSeconds(5) : TimeSpan.Zero);
            }

            /// <summary>
            /// This rather complicated method does the work of copying an input string to a text writer,
            /// while at the same time escaping any \, ", and ' chars that it encounters, so as not
            /// to break the format of the batch log file that we upload to Sql.
            /// </summary>
            /// <param name="input">The string to be escaped</param>
            /// <param name="outputWriter">The text writer to output the escaped string to.</param>
            private static void WriteEscapedStringToTextWriter(StringBuilder input, TextWriter outputWriter)
            {
                const int bufSize = BufferPool<char>.DEFAULT_BUFFER_SIZE;
                const int safeBufSize = bufSize - 1;
                using (PooledBuffer<char> pooledBuf = BufferPool<char>.Rent(bufSize))
                {
                    char[] charBuf = pooledBuf.Buffer;
                    int srcChar = 0;
                    int dstChar = 0;
                    int nextProblemChar = 0;
                    int inputLength = input.Length;
                    while (srcChar < inputLength)
                    {
                        // Find the next problem area.
                        nextProblemChar = StringUtils.IndexOfAnyInStringBuilder(input, srcChar, ESCAPED_CHARS);

                        int charsToAdvance;
                        if (nextProblemChar < 0)
                        {
                            // No problems ahead. Do piecewise copy until the end
                            charsToAdvance = inputLength - srcChar;
                        }
                        else
                        {
                            // Advance only until the next substitution point
                            charsToAdvance = nextProblemChar - srcChar;
                        }

                        // Advance that many chars
                        while (charsToAdvance > 0)
                        {
                            int thisSliceSize = FastMath.Min(charsToAdvance, safeBufSize - dstChar);
                            input.CopyTo(srcChar, charBuf, dstChar, thisSliceSize);
                            dstChar += thisSliceSize;
                            srcChar += thisSliceSize;
                            charsToAdvance -= thisSliceSize;

                            if (dstChar >= safeBufSize)
                            {
                                outputWriter.Write(charBuf, 0, dstChar);
                                dstChar = 0;
                            }
                        }

                        // Then handle the substitution if needed
                        if (nextProblemChar >= 0)
                        {
                            switch (input[nextProblemChar])
                            {
                                case '\\':
                                    charBuf[dstChar++] = '\\';
                                    charBuf[dstChar++] = '\\';
                                    break;
                                case '\"':
                                    charBuf[dstChar++] = '\\';
                                    charBuf[dstChar++] = '\"';
                                    break;
                                case '\'':
                                    charBuf[dstChar++] = '\\';
                                    charBuf[dstChar++] = '\'';
                                    break;
                                case '\r':
                                    charBuf[dstChar++] = '\\';
                                    charBuf[dstChar++] = '\r';
                                    break;
                                case '\n':
                                    charBuf[dstChar++] = '\\';
                                    charBuf[dstChar++] = '\n';
                                    break;
                            }

                            srcChar++;
                        }

                        if (dstChar >= safeBufSize)
                        {
                            outputWriter.Write(charBuf, 0, dstChar);
                            dstChar = 0;
                        }
                    }

                    if (dstChar > 0)
                    {
                        outputWriter.Write(charBuf, 0, dstChar);
                    }
                }
            }

            protected override async ValueTask<bool> Process(ArraySegment<PooledLogEvent> items, IRealTimeProvider realTime)
            {
                try
                {
                    //Debug.WriteLine("Preparing batch...");
                    int batchSize = 0;
                    string tmpFileName = Path.Combine(Path.GetTempPath(), "DurandalLogger-" + Path.GetRandomFileName().Substring(0, 8) + ".tmp");
                    FileInfo file = new FileInfo(tmpFileName);

                    using (Stream fileStream = new FileStream(file.FullName, FileMode.CreateNew, FileAccess.Write))
                    using (Utf8StreamWriter batchFileOut = new Utf8StreamWriter(fileStream))
                    {
                        batchFileOut.NewLine = "\r\n";

                        // WORKAROUND: We need to insert a "dummy" line at the beginning, even if we ignore it, because delimiters act strange on the first line of a batch file
                        batchFileOut.WriteLine("TraceId\tTimestamp\tComponent\tLevel\tMessage\tPrivacyClass");
                        foreach (PooledLogEvent e in items)
                        {
                            if (e == null)
                            {
                                continue;
                            }

                            try
                            {
                                if (!e.TraceId.HasValue || e.MessageBuffer.Builder.Length == 0)
                                {
                                    continue;
                                }

                                // Original code
                                //string line = string.Format("\"{0}\"\t{1}\t{2}\t\"{3}\"\t\"{4}\"\t{5}",
                                //    CommonInstrumentation.FormatTraceId(e.TraceId),
                                //    e.Timestamp.ToUniversalTime().Ticks,
                                //    string.IsNullOrEmpty(e.Component) ? "NULL" : "\"" + e.Component + "\"",
                                //    e.Level.ToChar(),
                                //    EscapeStringForFile(e.Message),
                                //    (ushort)e.PrivacyClassification);

                                batchFileOut.Write("\"");
                                batchFileOut.Write(CommonInstrumentation.FormatTraceId(e.TraceId)); // 0
                                batchFileOut.Write("\"\t");
                                batchFileOut.Write(e.Timestamp.ToUniversalTime().Ticks); // 1
                                if (string.IsNullOrEmpty(e.Component))
                                {
                                    batchFileOut.Write("\tNULL\t\"");
                                }
                                else
                                {
                                    batchFileOut.Write("\t\"");
                                    batchFileOut.Write(e.Component); // 2
                                    batchFileOut.Write("\"\t\"");
                                }
                                batchFileOut.Write(e.Level.ToChar()); // 3
                                batchFileOut.Write("\"\t\"");
                                WriteEscapedStringToTextWriter(e.MessageBuffer.Builder, batchFileOut); // 4
                                batchFileOut.Write("\"\t");
                                batchFileOut.Write((ushort)e.PrivacyClassification); // 5
                                batchFileOut.WriteLine();
                                batchSize++;
                            }
                            finally
                            {
                                e.Dispose();
                            }
                        }

                        batchFileOut.Dispose();
                    }

                    //Debug.WriteLine(file.FullName);

                    if (batchSize == 0)
                    {
                        //Debug.WriteLine("Nothing to upload!");
                        return true;
                    }

                    RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
                    if (pooledConnection.Success)
                    {
                        PooledResource<MySqlConnection> connection = pooledConnection.Result;
                        try
                        {
                            MySqlBulkLoader loader = new MySqlBulkLoader(connection.Value);
                            loader.Local = true;
                            loader.FileName = file.FullName;
                            loader.FieldQuotationCharacter = '\"';
                            loader.FieldTerminator = "\t";
                            loader.NumberOfLinesToSkip = 1;
                            loader.TableName = "logs";
                            loader.LineTerminator = "\r\n";
                            loader.EscapeCharacter = '\\';
                            loader.CharacterSet = "utf8";
                            //loader.Columns = new List<string>(new string[] { "TraceId", "Timestamp", "Component", "Level", "Message" });

                            //Console.WriteLine("Uploading " + batchSize + " log events");
                            int rowsUpdated = await loader.LoadAsync();
                            //Console.WriteLine(rowsUpdated + " rows updated");
                            return true;
                        }
                        finally
                        {
                            _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(BOOTSTRAP_LOGGER);
                            try
                            {
                                file.Delete();
                            }
                            catch (Exception e)
                            {
                                BOOTSTRAP_LOGGER.Log(e);
                            }
                        }
                    }

                    return false;
                }
                catch (Exception e)
                {
                    BOOTSTRAP_LOGGER.Log(e);
                }

                return false;
            }
        }
    }
}
