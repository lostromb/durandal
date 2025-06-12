using Durandal.API;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Durandal.Common.Instrumentation
{
    public class FileLogEventSource : ILogEventSource
    {
        //                                                     1                         2                  3     4             5                                              6
        //                                                     timestamp                 traceid (optional) level component     priv. class (optional)                         message
        private static readonly Regex LOG_PARSER = new Regex(@"\[([0-9T\.\:\-]+?)\](?: \[([0-9a-fA-F]+)\])? \[(\w):(.+?)\](?: \[((?:META|UNK|PRIV|EUII|PPD|PNPD|EUPI|,)+)\])? ([^\r\n]+)");
        private readonly IFileSystem _fileSystem;
        private VirtualPath _inputDirectory;
        private ILogger _programLogger;

        public FileLogEventSource(IFileSystem fileSystem, VirtualPath logFileDir, ILogger programLogger)
        {
            _fileSystem = fileSystem.AssertNonNull(nameof(fileSystem));
            _inputDirectory = logFileDir.AssertNonNull(nameof(logFileDir));
            _programLogger = programLogger;

            if (!_fileSystem.Exists(_inputDirectory))
            {
                _programLogger.Log("The input directory \"" + _inputDirectory + "\" does not exist", LogLevel.Wrn);
            }
        }

        public async Task<IEnumerable<LogEvent>> GetLogEvents(FilterCriteria logFilter)
        {
            IList<LogEvent> returnVal = new List<LogEvent>();

            IList<VirtualPath> inputFiles = new List<VirtualPath>();

            foreach (VirtualPath file in await _fileSystem.ListFilesAsync(_inputDirectory))
            {
                if (string.Equals(file.Extension, ".log", StringComparison.OrdinalIgnoreCase))
                {
                    inputFiles.Add(file);
                }
            }

            foreach (VirtualPath inputFileName in inputFiles)
            {
                string[] timestampFormats = new[] { "yyyy-MM-ddTHH:mm:ss.ffffff", "yyyy-MM-ddTHH:mm:ss.fffff", "yyyy-MM-ddTHH:mm:ss.fff" };
                try
                {
                    using (StreamReader reader = new StreamReader(_fileSystem.OpenStream(inputFileName, FileOpenMode.Open, FileAccessMode.Read)))
                    {
                        while (!reader.EndOfStream)
                        {
                            string nextLine = await reader.ReadLineAsync();
                            Match m = LOG_PARSER.Match(nextLine);
                            if (m.Success)
                            {
                                DataPrivacyClassification privClass = DataPrivacyClassification.Unknown;
                                if (m.Groups[5].Success)
                                {
                                    privClass = CommonInstrumentation.ParsePrivacyClassString(m.Groups[5].Value);
                                }

                                // Parse the event and apply filters
                                LogEvent parsedEvent = new LogEvent(
                                    m.Groups[4].Value,
                                    m.Groups[6].Value,
                                    LoggingLevelManipulators.ParseLevelChar(m.Groups[3].Value),
                                    DateTime.ParseExact(m.Groups[1].Value, timestampFormats, CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AssumeUniversal),
                                    CommonInstrumentation.TryParseTraceIdGuid(m.Groups[2].Value),
                                    privClass);

                                if (logFilter.PassesFilter(parsedEvent))
                                {
                                    returnVal.Add(parsedEvent);
                                }
                            }
                        }

                        reader.Dispose();
                    }
                }
                catch (Exception e)
                {
                    _programLogger.Log($"Cannot access " + inputFileName, LogLevel.Err);
                    _programLogger.Log(e, LogLevel.Err);
                }
            }

            return returnVal;
        }
    }
}
