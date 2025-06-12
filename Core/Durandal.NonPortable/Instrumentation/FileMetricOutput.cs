using Durandal.Common.Logger;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Instrumentation
{
    public class FileMetricOutput : IMetricOutput
    {
        private static readonly byte[] NEWLINE = Encoding.UTF8.GetBytes("\r\n");
        private readonly ILogger _logger;
        private readonly DirectoryInfo _logDir;
        private readonly string _binaryName;
        private readonly object _rolloverLock = new object();
        private readonly int _maxFileSizeBytes = 0;
        private FileStream _stream = null;
        private int _currentFileSize = 0;
        private int _disposed = 0;
        private int _currentSchemaHash = 0;

        public FileMetricOutput(ILogger logger, string binaryName, string logDirectory = ".\\logs", int maxFileSizeBytes = 0)
        {
            _logger = logger ?? NullLogger.Singleton;
            _logDir = new DirectoryInfo(logDirectory);
            _logger.Log("Initializing file metric outputter to " + _logDir.FullName);
            if (!_logDir.Exists)
            {
                _logDir.Create();
            }

            _binaryName = binaryName;
            _maxFileSizeBytes = maxFileSizeBytes;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FileMetricOutput()
        {
            Dispose(false);
        }
#endif

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
                try
                {
                    lock (_rolloverLock)
                    {
                        _stream?.Flush();
                        _stream?.Close();
                    }
                }
                catch (IOException e)
                {
                    Console.WriteLine("Error while closing file log: " + e);
                }
            }
        }

        private void CreateNewFileStream(CounterInstance[] columnSchema)
        {
            if (_stream != null)
            {
                _stream.Close();
            }

            string timeStamp = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH-mm-ss");
            string newFileName = _logDir.FullName + "\\" + _binaryName + "_" + timeStamp + ".metrics.tsv";
            int counter = 2;
            while (System.IO.File.Exists(newFileName))
            {
                newFileName = _logDir.FullName + "\\" + _binaryName + "_" + timeStamp + "_" + counter + ".metrics.tsv";
                counter++;
            }
            
            _stream = new FileStream(
                newFileName,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                32768,
                false);

            _currentFileSize = 0;

            // Write the table schema
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder lineBuilder = pooledSb.Builder;
                lineBuilder.Append("Timestamp");
                foreach (CounterInstance metricName in columnSchema)
                {
                    lineBuilder.Append('\t');
                    lineBuilder.Append(metricName.ToString().Replace('\t', ' '));
                }

                // FIXME this could really be better optimized...
                string line = lineBuilder.ToString();
                byte[] writeBuffer = Encoding.UTF8.GetBytes(line);

                // Write it to file
                _stream.Write(writeBuffer, 0, writeBuffer.Length);
                _stream.Write(NEWLINE, 0, NEWLINE.Length);

                _currentFileSize += writeBuffer.Length + NEWLINE.Length;
            }
        }

        public Task OutputAggregateMetrics(IReadOnlyDictionary<CounterInstance, double?> continuousMetrics)
        {
            try
            {
                lock (_rolloverLock)
                {
                    // Has the metric schema changed?
                    CounterInstance[] sortedKeys = continuousMetrics.Keys.ToArray();
                    if (sortedKeys.Length == 0)
                    {
                        // Nothing to write...
                        return DurandalTaskExtensions.NoOpTask;
                    }

                    // Sort all the counter names to make a table schema and then calculate its hash to detect changes
                    Array.Sort(sortedKeys);
                    int schemaHash = 0;
                    foreach (CounterInstance metricName in sortedKeys)
                    {
                        schemaHash ^= metricName.GetHashCode();
                    }

                    if (schemaHash != _currentSchemaHash)
                    {
                        // Schema has changed, so we need to start a new file
                        CreateNewFileStream(sortedKeys);
                    }

                    _currentSchemaHash = schemaHash;

                    // Can happen on the first run if we haven't created the stream fully yet
                    if (_stream != null)
                    {
                        using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                        {
                            StringBuilder lineBuilder = pooledSb.Builder;
                            lineBuilder.Append(HighPrecisionTimer.GetCurrentUTCTime().ToString("yyyy-MM-ddTHH:mm:ss.fff"));
                            foreach (CounterInstance metricName in sortedKeys)
                            {
                                double? metricValue = continuousMetrics[metricName];
                                lineBuilder.Append('\t');
                                lineBuilder.Append(metricValue.GetValueOrDefault(0));
                            }

                            // FIXME this could also be more optimal...
                            string line = lineBuilder.ToString();
                            byte[] writeBuffer = Encoding.UTF8.GetBytes(line);

                            // Write it to file
                            _stream.Write(writeBuffer, 0, writeBuffer.Length);
                            _stream.Write(NEWLINE, 0, NEWLINE.Length);

                            _currentFileSize += writeBuffer.Length + NEWLINE.Length;
                        }

                        // Roll over the file, if we've reached the one-file limit
                        if (_maxFileSizeBytes > 0 && _currentFileSize >= _maxFileSizeBytes)
                        {
                            _stream.Flush();
                            CreateNewFileStream(sortedKeys);
                        }

                        _stream.Flush();
                    }
                }
            }
            catch (IOException e)
            {
                _logger.Log("Error while writing to file metric stream", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }

            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
