using Durandal.Common.Tasks;

namespace Durandal.Common.Logger
{
    using Durandal.API;
    using Durandal.Common.Time;
    using Durandal.Common.Instrumentation;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Utils;
    using Durandal.Common.IO;
    using System.ComponentModel;
    using Durandal.Common.MathExt;
    using Durandal.Common.File;

    /// <summary>
    /// A logger which is backed by a concurrent file stream and writes to a virtual filesystem
    /// </summary>  
    public class FileLogger : LoggerBase
    {
        /// <summary>
        /// Creates a logger which ingests log events asynchronously and logs them to a series of files.
        /// </summary>
        /// <param name="fileSystem">The file system that this logger will write to</param>
        /// <param name="componentName">The name of the component emitting the logs</param>
        /// <param name="logFilePrefix">A string to prefix log files with to differentiate them from others (or if multiple loggers are working in the same directory).</param>
        /// <param name="backgroundLogThreadPool">An optional thread pool to dispatch background work items to</param>
        /// <param name="bootstrapLogger">A logger for emitting low-level diagnostic messages such as file stream status and errors</param>
        /// <param name="maxFileSizeBytes">The maximum size a log file can reach (approximately) before rolling over to a new file.</param>
        /// <param name="logDirectory">The directory to place logs into. Defaults to "/logs".</param>
        /// <param name="validLogLevels">Valid log levels (initially) that can be logged</param>
        /// <param name="maxLogLevels">Never log any messages that are outside of this level</param>
        /// <param name="maxPrivacyClasses">Never log any message that are outside of this privacy class</param>
        /// <param name="defaultPrivacyClass">The default privacy class to assign to logged messages that don't specify any other</param>
        /// <param name="fileBufferSize">The size, in bytes, of the internalFileStream output buffer. 32768 is a good default based on experimentation.</param>
        /// <param name="realTime">A definition of real time, used when naming the output files</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Core file logger has static scope whereas constructor has instance scope")]
        public FileLogger(
            IFileSystem fileSystem,
            string componentName = "Main",
            string logFilePrefix = "",
            IThreadPool backgroundLogThreadPool = null,
            ILogger bootstrapLogger = null,
            long? maxFileSizeBytes = null,
            VirtualPath logDirectory = null,
            LogLevel validLogLevels = LogLevel.All,
            LogLevel maxLogLevels = LogLevel.All,
            DataPrivacyClassification maxPrivacyClasses = DataPrivacyClassification.All,
            DataPrivacyClassification defaultPrivacyClass = DataPrivacyClassification.SystemMetadata,
            int fileBufferSize = 32768,
            IRealTimeProvider realTime = null)
            : base(new FileLoggerCore(
                fileSystem,
                bootstrapLogger ?? NullLogger.Singleton,
                logDirectory,
                maxFileSizeBytes,
                logFilePrefix,
                fileBufferSize,
                realTime),
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

        /// <summary>
        /// Private constructor for creating inherited logger objects
        /// </summary>
        /// <param name="core">The logger core (common functionality)</param>
        /// <param name="context">The logger configuration (trace-specific or context-specific functionality)</param>
        private FileLogger(ILoggerCore core, LoggerContext context)
                : base(core, context)
        {
        }

        protected override ILogger CloneImplementation(ILoggerCore core, LoggerContext context)
        {
            return new FileLogger(core, context);
        }

        /// <summary>
        /// Closes and flushes the file stream backing this logger. Any further calls to Log() will throw an exception.
        /// </summary>
        public void DisposeCore()
        {
            if (_context.BackgroundLoggingThreadPool != null)
            {
                _context.BackgroundLoggingThreadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }

            _core.Dispose();
        }

        /// <summary>
        /// This is the context object shared between all clones of the file logger
        /// </summary>
        private class FileLoggerCore : ILoggerCore
        {
            private const int STRING_BUFFER_OVERFLOW_SIZE = 2048;
            private const int MAX_FILE_BUFFER_SIZE = 256 * 1024; // Above a 256K buffer is really not helpful
            private static readonly TimeSpan MIN_FLUSH_INTERVAL = TimeSpan.FromSeconds(10);

            private readonly ILogger _bootstrapLogger;
            private readonly IFileSystem _fileSystem;
            private readonly long? _maxFileSizeBytes;
            private readonly VirtualPath _logDir;
            private readonly string _logPrefix;
            private readonly object _outputLock = new object();
            private readonly int _fileBufferSizeBytes;
            private readonly IRealTimeProvider _realTime;

            // Because we need an intermediate string builder to handle some formatting cases,
            // and because file IO performs better on infrequent large writes, we allocate
            // here this string builder with ~34K char capacity. When it's full, we write
            // the entire buffer as a block to the file output.
            private readonly StringBuilder _giantStringBuilder;

            // this is used when copying data between stringbuilders
            private readonly char[] _scratch = new char[1024];

            // tracks if we have infrequent log updates, we don't want stuff to get stuck
            // in a file buffer forever, so we use this to flush intermittently
            private DateTimeOffset _timeOfLastFileStreamFlush = DateTimeOffset.MinValue;

            // The file output stream - changes as the output file rotates
            private Stream _stream = null;
            private long _bytesWrittenToCurrentFile = 0;

            private int _disposed = 0;

            public FileLoggerCore(
                IFileSystem fileSystem,
                ILogger bootstrapLogger,
                VirtualPath logDirectory,
                long? maxFileSizeBytes,
                string logFilePrefix,
                int fileBufferSize,
                IRealTimeProvider realTime)
            {
                _fileSystem = fileSystem.AssertNonNull(nameof(fileSystem));
                _bootstrapLogger = bootstrapLogger ?? NullLogger.Singleton;
                if (maxFileSizeBytes.HasValue && maxFileSizeBytes <= 0)
                {
                    maxFileSizeBytes.Value.AssertPositive(nameof(maxFileSizeBytes));
                }

                fileBufferSize.AssertPositive(nameof(fileBufferSize));
                _maxFileSizeBytes = maxFileSizeBytes;
                _fileBufferSizeBytes = Math.Max(MAX_FILE_BUFFER_SIZE, (int)Math.Min(_maxFileSizeBytes.GetValueOrDefault(0), (long)fileBufferSize));

                _logPrefix = logFilePrefix ?? string.Empty;
                if(_logPrefix.Length > 0)
                {
                    _logPrefix = _logPrefix + "_";
                }

                _logDir = logDirectory ?? new VirtualPath("\\logs");
                _realTime = realTime ?? DefaultRealTimeProvider.Singleton;

                // note: we are assuming sizeof(char) ~= sizeof(byte) because the strings will be encoded to UTF8 on the output
                _giantStringBuilder = new StringBuilder(capacity: _fileBufferSizeBytes + STRING_BUFFER_OVERFLOW_SIZE);
                if (!_fileSystem.Exists(_logDir))
                {
                    _fileSystem.CreateDirectory(_logDir);
                }

                CreateNewFileStream(_realTime.Time);
            }

            public void Dispose()
            {
                Dispose(true);
            }

            protected void Dispose(bool disposing)
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                if (disposing)
                {
                    try
                    {
                        // Flush the actual log writes in the batch processor
                        try
                        {
                            Flush(CancellationToken.None, _realTime, blocking: true).Await();
                            _stream.Dispose();
                        }
                        catch (Exception e)
                        {
                            _bootstrapLogger.Log("Error while flushing the file log before close", LogLevel.Err);
                            _bootstrapLogger.Log(e, LogLevel.Err);
                        }
                    }
                    catch (Exception e)
                    {
                        _bootstrapLogger.Log("Error while closing file log", LogLevel.Err);
                        _bootstrapLogger.Log(e, LogLevel.Err);
                    }
                }
            }

            public Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking)
            {
                // Flush the batches in the batch processor
                FlushBuffersToFileIfNeeded(_realTime, force: true);

                // And then flush the file stream itself
                _stream.Flush();

                return DurandalTaskExtensions.NoOpTask;
            }

            public void LoggerImplementation(PooledLogEvent logEvent)
            {
                if (_disposed != 0)
                {
                    throw new ObjectDisposedException(nameof(FileLogger));
                }

                lock (_outputLock)
                {
                    // Write log values piecewise for performance. This correlates with the format string
                    // "[{Timestamp:yyyy-MM-ddTHH:mm:ss.fffff}] [{TraceId:N}] [{Level}:{Component}] {Message}"
                    _giantStringBuilder.Append('[');
                    StringUtils.FormatDateTime_ISO8601WithMicroseconds(logEvent.Timestamp, _giantStringBuilder);
                    if (logEvent.TraceId.HasValue)
                    {
                        _giantStringBuilder.Append("] [");
                        CommonInstrumentation.FormatTraceId(logEvent.TraceId.Value, _giantStringBuilder);
                    }

                    _giantStringBuilder.Append("] [");
                    _giantStringBuilder.Append(logEvent.Level.ToChar());
                    _giantStringBuilder.Append(':');
                    _giantStringBuilder.Append(logEvent.Component);
                    _giantStringBuilder.Append("] ");

                    if (logEvent.PrivacyClassification != DataPrivacyClassification.Unknown)
                    {
                        _giantStringBuilder.Append('[');
                        CommonInstrumentation.WritePrivacyClassification(logEvent.PrivacyClassification, _giantStringBuilder);
                        _giantStringBuilder.Append("] ");
                    }

                    // Copy from string builder to string builder, kind of strange I know
                    int inIdx = 0;
                    while (inIdx < logEvent.MessageBuffer.Builder.Length)
                    {
                        int canCopy = FastMath.Min(_scratch.Length, logEvent.MessageBuffer.Builder.Length - inIdx);
                        logEvent.MessageBuffer.Builder.CopyTo(inIdx, _scratch, 0, canCopy);
                        inIdx += canCopy;
                        StringUtils.ReplaceNewlinesWithSpace(_scratch, 0, canCopy);
                        _giantStringBuilder.Append(_scratch, 0, canCopy);
                    }

                    logEvent.Dispose();
                    _giantStringBuilder.Append("\r\n");

                    FlushBuffersToFileIfNeeded(_realTime, force: false);
                }
            }

            private void FlushBuffersToFileIfNeeded(IRealTimeProvider realTime, bool force)
            {
                // We've either filled a giant string builder, or it's been a while since last flush. Write it to file all at once
                bool overdueForFlush = (realTime.Time - _timeOfLastFileStreamFlush) > MIN_FLUSH_INTERVAL;
                if (force || overdueForFlush || _giantStringBuilder.Length >= _fileBufferSizeBytes)
                {
                    // again, we are assuming sizeof(char) ~= sizeof(byte) because the Unicode strings will actually be output as UTF8 to the file
                    using (StringBuilderReadStream inStream = new StringBuilderReadStream(_giantStringBuilder, StringUtils.UTF8_WITHOUT_BOM, maskNewline: false))
                    using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent(_fileBufferSizeBytes))
                    {
                        int amountRead = 1;
                        while (amountRead > 0)
                        {
                            amountRead = inStream.Read(scratch.Buffer, 0, _fileBufferSizeBytes);
                            if (amountRead > 0)
                            {
                                _stream.Write(scratch.Buffer, 0, amountRead);
                                _bytesWrittenToCurrentFile += amountRead;
                            }
                        }
                    }

                    _giantStringBuilder.Clear();
                    _giantStringBuilder.EnsureCapacity(_fileBufferSizeBytes + STRING_BUFFER_OVERFLOW_SIZE);

                    if (_maxFileSizeBytes.HasValue && _bytesWrittenToCurrentFile >= _maxFileSizeBytes)
                    {
                        // Roll over the file, if we've reached the one-file limit
                        _stream.Flush();
                        CreateNewFileStream(realTime.Time);
                    }
                    else if (overdueForFlush)
                    {
                        // Or just flush if we have infrequent logs and haven't flushed in a while
                        _stream.Flush();
                    }

                    _timeOfLastFileStreamFlush = realTime.Time;
                }
            }

            private void CreateNewFileStream(DateTimeOffset currentTime)
            {
                if (_stream != null)
                {
                    _stream.Dispose();
                }

                string timeStamp = currentTime.ToString("yyyy-MM-ddTHH-mm-ss");
                VirtualPath newFileName = _logDir.Combine(_logPrefix + timeStamp + ".log");
                int counter = 2;
                while (_fileSystem.Exists(newFileName))
                {
                    newFileName = _logDir.Combine(_logPrefix + timeStamp + "_" + counter + ".log"); 
                    counter++;
                }

                _stream = _fileSystem.OpenStream(
                    newFileName,
                    FileOpenMode.CreateNew,
                    FileAccessMode.Write,
                    bufferSizeHint: _fileBufferSizeBytes);
                _bytesWrittenToCurrentFile = 0;
                // useAsync = false, doesn't seem to give a speed boost (but maybe it could in the future?)
            }
        }
    }
}
