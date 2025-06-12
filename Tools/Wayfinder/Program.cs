using Durandal.Common.Instrumentation;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Durandal.API;
using System.Threading;
using System.IO.Compression;
using Durandal.Common.Tasks;
using Durandal.Extensions.BondProtocol;
using Durandal.Extensions.MySql;
using Durandal.Common.Security;
using Durandal.Common.ServiceMgmt;

namespace Wayfinder
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Parse command line
            if (args.Length == 0)
            {
                PrintHelpMessage();
            }
            else if (args.Length == 2 && string.Equals("/Trace", args[0], StringComparison.OrdinalIgnoreCase))
            {
                RunSingleTrace(Guid.Parse(args[1])).Await();
            }
            else if (args.Length == 1 && string.Equals("/Analyze", args[0], StringComparison.OrdinalIgnoreCase))
            {
                RunAnalysis().Await();
            }
            else
            {
                PrintHelpMessage();
            }
        }

        public static void PrintHelpMessage()
        {
            Console.WriteLine("Wayfinder.exe /Trace (TRACEID)");
            Console.WriteLine("Wayfinder.exe /Analyze");
        }

        public static async Task RunSingleTrace(Guid traceId)
        {
            ILogger programLogger = new ConsoleLogger();
            IList<byte[]> decryptionKeys = new List<byte[]>();
            //decryptionKeys.Add(AesStringDecrypterPii.GenerateKey("null")); // TODO plumb through keys
            IAESDelegates aesImpl = new SystemAESDelegates();
            IStringDecrypterPii piiDecrypter = new AesStringDecrypterPii(aesImpl, decryptionKeys);

            string connectionString = "Todo:Add connection string";
            using (MySqlConnectionPool connectionPool = await MySqlConnectionPool.Create(connectionString, programLogger.Clone("MySqlConnectionPool"), NullMetricCollector.Singleton, DimensionSet.Empty, "Default", true, 2))
            {
                ILogEventSource logEventSource = new MySqlLogEventSource(connectionPool, programLogger);
                IInstrumentationRepository instrumentationAdapter = new MySqlInstrumentation(connectionPool, programLogger.Clone("MySqlInstrumentation"), new InstrumentationBlobSerializer());

                UnifiedTrace trace = null;

                trace = await GetUnifiedTraceFromTable(instrumentationAdapter, traceId, programLogger, piiDecrypter);
                if (trace == null)
                {
                    programLogger.Log("No trace came back from instrumentation table, checking instant logs...");

                    trace = await GetUnifiedTraceFromLogs(logEventSource, traceId, programLogger, piiDecrypter);
                    if (trace == null)
                    {
                        programLogger.Log("No traces found anywhere.");
                    }
                }

                DirectoryInfo traceDir = new DirectoryInfo("traces");
                if (!traceDir.Exists)
                {
                    traceDir.Create();
                }

                WriteTraceSummary(traceDir.FullName + "\\" + traceId + ".txt", trace, programLogger);

                connectionPool.Dispose();
            }
        }

        public static async Task<UnifiedTrace> GetUnifiedTraceFromTable(IInstrumentationRepository instrumentationAdapter, Guid traceId, ILogger programLogger, IStringDecrypterPii piiDecrypter)
        {
            programLogger.Log("Getting instrumentation for traceId " + traceId);
            UnifiedTrace returnVal = await instrumentationAdapter.GetTraceData(traceId, piiDecrypter);
            return returnVal;
        }

        public static async Task<UnifiedTrace> GetUnifiedTraceFromLogs(ILogEventSource logReader, Guid traceId, ILogger programLogger, IStringDecrypterPii piiDecrypter)
        {
            FilterCriteria filter = new FilterCriteria()
            {
                Level = LogLevel.All,
                TraceId = traceId
            };
            
            programLogger.Log("Getting logs for traceId " + traceId);
            IEnumerable<LogEvent> events = await logReader.GetLogEvents(filter);
            UnifiedTrace returnVal = UnifiedTrace.CreateFromLogData(traceId, events, programLogger, piiDecrypter);

            return returnVal;
        }

        public static void WriteTraceSummary(string outputFile, UnifiedTrace impression, ILogger programLogger)
        {
            if (impression == null)
            {
                programLogger.Log("No trace data, so nothing to write to output file");
                return;
            }

            using (StreamWriter writer = new StreamWriter(outputFile))
            {
                writer.WriteLine("Trace information for \"" + impression.TraceId + "\"");
                writer.WriteLine("Start time:\t" + impression.TraceStart.ToString("yyyy-MM-ddTHH:mm:ss.fffff") + " UTC");
                writer.WriteLine("End time:  \t" + impression.TraceEnd.ToString("yyyy-MM-ddTHH:mm:ss.fffff" + " UTC"));
                writer.WriteLine("Duration:  \t" + impression.TraceDuration + " seconds");
                writer.WriteLine();
                writer.WriteLine("=== IMPRESSION DATA ===");
                writer.WriteLine();
                JObject mergedObject = impression.InstrumentationObject;
                string merged = mergedObject.ToString()/*.Replace(" ", "").Replace("\r", "").Replace("\n", "")*/;
                writer.WriteLine(merged);
                writer.WriteLine();
                writer.WriteLine("=== RAW LOGS ===");
                writer.WriteLine();
                foreach (LogEvent log in impression.LogEvents)
                {
                    if (log.Level != LogLevel.Ins)
                    {
                        writer.WriteLine(log.ToDetailedString());
                    }
                }

                writer.Close();
            }
        }

        public static IList<ColumnDefinition> CreateDefaultSummaryColumns()
        {
            IList<ColumnDefinition> columns = new List<ColumnDefinition>();
            columns.Add(new JPathColumnDefinition("TraceId", "TraceId"));
            columns.Add(new JPathColumnDefinition("StartTime", "StartTime"));
            columns.Add(new JPathColumnDefinition("EndTime", "EndTime"));
            columns.Add(new JPathColumnDefinition("Trace Duration", "TraceDuration"));
            columns.Add(new JPathColumnDefinition("Total Dialog Latency", "Perf.Latency.Dialog_E2E"));
            columns.Add(new JPathColumnDefinition("Client ID", "Dialog.ClientRequest.ClientContext.ClientId"));
            columns.Add(new JPathColumnDefinition("Client Name", "Dialog.ClientRequest.ClientContext.ClientName"));
            columns.Add(new JPathColumnDefinition("Query", "Dialog.DialogProcessorResponse.SelectedRecoResult.Utterance.OriginalText"));
            columns.Add(new JPathColumnDefinition("Input Type", "Dialog.ClientRequest.InputType"));
            columns.Add(new JPathColumnDefinition("Triggered Domain", "Dialog.DialogProcessorResponse.SelectedRecoResult.Domain"));
            columns.Add(new JPathColumnDefinition("Triggered Intent", "Dialog.DialogProcessorResponse.SelectedRecoResult.Intent"));
            columns.Add(new JPathColumnDefinition("LU Confidence", "Dialog.DialogProcessorResponse.SelectedRecoResult.Confidence"));
            columns.Add(new JPathColumnDefinition("Response Text", "Dialog.DialogProcessorResponse.DisplayedText"));
            columns.Add(new JPathColumnDefinition("Error", "Dialog.DialogProcessorResponse.ErrorMessage"));
            return columns;
        }

        public static async Task RunAnalysis()
        {
            ILogger programLogger = new ConsoleLogger();
            IList<byte[]> decryptionKeys = new List<byte[]>(); // TODO plumb through keys
            //decryptionKeys.Add(AesStringDecrypterPii.GenerateKey("null"));
            IStringDecrypterPii piiDecrypter = new AesStringDecrypterPii(new SystemAESDelegates(), decryptionKeys);

            List<UnifiedTrace> allTraces = new List<UnifiedTrace>();

            string connectionString = "Todo:Add connection string";
            DateTimeOffset startTimeUtc = DateTimeOffset.UtcNow - TimeSpan.FromDays(28);
            DateTimeOffset endTimeUtc = DateTimeOffset.UtcNow;

            using (MySqlConnectionPool connectionPool = await MySqlConnectionPool.Create(connectionString, programLogger.Clone("MySqlConnectionPool"), NullMetricCollector.Singleton, DimensionSet.Empty, "Default", true, 50))
            {
                ILogEventSource logEventSource = new MySqlLogEventSource(connectionPool, programLogger);
                IInstrumentationRepository instrumentationAdapter = new MySqlInstrumentation(connectionPool, programLogger.Clone("MySqlInstrumentation"), new InstrumentationBlobSerializer());
                programLogger.Log("Collecting traces....");
                ISet<Guid> allTraceIds = await instrumentationAdapter.GetProcessedTraceIds(startTimeUtc, endTimeUtc);
                using (IThreadPool threadPool = new TaskThreadPool(NullMetricCollector.WeakSingleton, DimensionSet.Empty, "SqlThreadPool"))
                {
                    List<TraceFetchThread> allRetrieveTasks = new List<TraceFetchThread>();
                    foreach (Guid traceId in allTraceIds)
                    {
                        TraceFetchThread thisTask = new TraceFetchThread(traceId, instrumentationAdapter, piiDecrypter);
                        allRetrieveTasks.Add(thisTask);
                        threadPool.EnqueueUserAsyncWorkItem(thisTask.Run);
                    }

                    // Wait for all tasks to finish. There should be a better way of checking this but there's not really
                    while (threadPool.TotalWorkItems > 0)
                    {
                        Thread.Sleep(100);
                    }

                    foreach (TraceFetchThread task in allRetrieveTasks)
                    {
                        if (task.Result != null && task.Result.TraceEnd != default(DateTime))
                        {
                            allTraces.Add(task.Result);
                        }
                    }

                    threadPool.Dispose();
                }

                programLogger.Log("Got " + allTraces.Count + " traces");
                
                connectionPool.Dispose();
            }

            // Write the raw data
            //using (FileStream reportWriter = new FileStream("Analytics.dat", FileMode.Create, FileAccess.Write))
            //{
            //    using (DeflateStream compressor = new DeflateStream(reportWriter, CompressionLevel.Fastest))
            //    {
            //        byte[] scratch;
            //        int numTraces = allTraces.Count;
            //        scratch = BitConverter.GetBytes(numTraces);
            //        compressor.Write(scratch, 0, 4);
            //        foreach (UnifiedTrace trace in allTraces)
            //        {
            //            InstrumentationBlob logBlob = new InstrumentationBlob();
            //            logBlob.AddEvents(trace.LogEvents);
            //            byte[] compressedLogs = logBlob.Compress();
            //            int logLength = compressedLogs.Length;
            //            scratch = BitConverter.GetBytes(logLength);
            //            compressor.Write(scratch, 0, 4);
            //            compressor.Write(compressedLogs, 0, logLength);

            //            trace.InstrumentationObject
            //        }
            //        compressor.Dispose();
            //    }
            //    reportWriter.Dispose();
            //}

            // Now build all the reports we need
            using (StreamWriter reportWriter = new StreamWriter("Latency.tsv"))
            {
                foreach(UnifiedTrace trace in allTraces)
                {
                    if (trace != null && trace.Latencies != null && trace.Latencies.ContainsKey("Dialog_LUCall"))
                    {
                        reportWriter.WriteLine(string.Format("{0}\t{1}", trace.TraceEnd, trace.Latencies["Dialog_LUCall"]));
                    }
                }

                reportWriter.Close();
            }
        }

        private class TraceFetchThread
        {
            public Guid TraceId;
            public UnifiedTrace Result;
            public IInstrumentationRepository Instrumentation;
            public IStringDecrypterPii PiiDecrypter;

            public TraceFetchThread(Guid traceId, IInstrumentationRepository instrumentation, IStringDecrypterPii piiDecrypter)
            {
                TraceId = traceId;
                Instrumentation = instrumentation;
                PiiDecrypter = piiDecrypter;
            }

            public async Task Run()
            {
                Result = await Instrumentation.GetTraceData(TraceId, PiiDecrypter);
            }
        }
    }
}
