using Durandal.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Durandal.Common.Time;
using System.Text.RegularExpressions;
using Durandal.Common.Collections;
using Durandal.Common.Utils;
using Durandal.Common.IO.Json;
using Durandal.Common.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER && UNSAFE
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace Durandal.Common.Instrumentation
{
    public static class CommonInstrumentation
    {
        /// <summary>
        /// Used to map byte values (lower 4 bits) to hexadecimal chars.
        /// </summary>
        private static readonly string HexDigitsLowerCase = "0123456789abcdef";

        public static readonly string Encrypted_Message_Prefix = "!ENCR|";

        public static readonly string Key_Latency_Client_AudioUPL =                     "Client_AudioUPL";
        public static readonly string Key_Latency_Client_BuildAudioContext =            "Client_BuildAudioContext";
        public static readonly string Key_Latency_Client_E2E =                          "Client_E2E";
        public static readonly string Key_Latency_Client_FinishSpeechReco =             "Client_FinishSpeechReco";
        public static readonly string Key_Latency_Client_GenerateRequestToken =         "Client_GenerateRequestToken";
        public static readonly string Key_Latency_Client_Initialize =                   "Client_Initialize";
        public static readonly string Key_Latency_Client_RunTTS =                       "Client_RunTTS";
        public static readonly string Key_Latency_Client_StartSpeechReco =              "Client_StartSpeechReco";
        public static readonly string Key_Latency_Client_StreamingAudioBeginRead =      "Client_StreamingAudioBeginRead";
        public static readonly string Key_Latency_Client_StreamingAudioRead =           "Client_StreamingAudioRead"; // FIXME need to hook this up again
        public static readonly string Key_Latency_Client_RecordingUtterance =           "Client_RecordingUtterance";
        public static readonly string Key_Latency_Client_TriggerArbitration =           "Client_TriggerArbitration";
        
        public static readonly string Key_Latency_Dialog_Core =                         "Dialog_Core";
        public static readonly string Key_Latency_Dialog_E2E =                          "Dialog_E2E";
        public static readonly string Key_Latency_Dialog_LUCall =                       "Dialog_LUCall";
        public static readonly string Key_Latency_Dialog_ProcessAsyncAudio =            "Dialog_ProcessAsyncAudio";
        public static readonly string Key_Latency_Dialog_ProcessSyncAudio =             "Dialog_ProcessSyncAudio";
        public static readonly string Key_Latency_Dialog_RunTTS =                       "Dialog_RunTTS";
        public static readonly string Key_Latency_Dialog_SessionWriteHotPath =          "Dialog_SessionWriteHotPath";
        public static readonly string Key_Latency_Dialog_StreamingAudioBeginWrite =     "Dialog_StreamingAudioBeginWrite";
        public static readonly string Key_Latency_Dialog_StreamingAudioInitialize =     "Dialog_StreamingAudioInitialize";
        public static readonly string Key_Latency_Dialog_StreamingAudioWrite =          "Dialog_StreamingAudioWrite";
        public static readonly string Key_Latency_Dialog_StreamingAudioTimeInCache =    "Dialog_StreamingAudioTimeInCache";
        public static readonly string Key_Latency_Dialog_Triggers =                     "Dialog_Triggers";
        public static readonly string Key_Latency_Dialog_UserProfileWriteHotPath =      "Dialog_UserProfileWriteHotPath";
        public static readonly string Key_Latency_Dialog_VerifyAuthToken =              "Dialog_VerifyAuthToken";
        public static readonly string Key_Latency_Dialog_WritebackAllState =            "Dialog_WritebackAllState";

        public static readonly string Key_Latency_LG_Train =                            "LG_Train";

        public static readonly string Key_Latency_LU_E2E =                              "LU_E2E";
        public static readonly string Key_Latency_LU_Resolver =                         "LU_Resolver";

        public static readonly string Key_Latency_Plugin_Execute =                      "Plugin_Execute";
        public static readonly string Key_Latency_Plugin_Trigger =                      "Plugin_Trigger";

        public static readonly string Key_Latency_Store_CacheWrite =                    "Store_CacheWrite";
        public static readonly string Key_Latency_Store_OauthSecretWrite =              "Store_OauthSecretWrite";
        public static readonly string Key_Latency_Store_OauthSecretRead =               "Store_OauthSecretRead";
        public static readonly string Key_Latency_Store_HtmlCacheRead =                 "Store_HtmlCacheRead";
        public static readonly string Key_Latency_Store_SessionClear =                  "Store_SessionClear";
        public static readonly string Key_Latency_Store_SessionRead =                   "Store_SessionRead";
        public static readonly string Key_Latency_Store_SessionWriteClientState =       "Store_SessionWriteClientState";
        public static readonly string Key_Latency_Store_SessionWriteRoamingState =      "Store_SessionWriteRoamingState";
        public static readonly string Key_Latency_Store_StreamingAudioBeginRead =       "Store_StreamingAudioBeginRead";
        public static readonly string Key_Latency_Store_UserProfileRead =               "Store_UserProfileRead";
        public static readonly string Key_Latency_Store_WebCacheRead =                  "Store_WebCacheRead";
        public static readonly string Key_Latency_Store_PublicKeyRead =                 "Store_PublicKeyRead";

        public static readonly string Key_Size_Client_Request =                         "Client_Request";
        public static readonly string Key_Size_Client_Response =                        "Client_Response";
        public static readonly string Key_Size_Client_StreamingAudioResponse =          "Client_StreamingAudioResponse"; // TODO implement this on dialog
        public static readonly string Key_Size_Dialog_InputPayload =                    "Dialog_InputPayload";
        public static readonly string Key_Size_Dialog_OutputPayload =                   "Dialog_OutputPayload";
        public static readonly string Key_Size_LU_InputPayload =                        "LU_InputPayload";
        public static readonly string Key_Size_LU_OutputPayload =                       "LU_OutputPayload";
        public static readonly string Key_Size_Store_HtmlCache =                        "Store_HtmlCache";
        public static readonly string Key_Size_Store_SessionRead =                      "Store_SessionRead";
        public static readonly string Key_Size_Store_WebCache =                         "Store_WebCache";
        public static readonly string Key_Size_Store_Session =                          "Store_Session";
        public static readonly string Key_Size_Store_LocalProfile =                     "Store_LocalProfile";
        public static readonly string Key_Size_Store_GlobalProfile =                    "Store_GlobalProfile";

        public static readonly string Key_Counter_Dialog_WebRequestCount =              "Dialog HTTP Requests/sec";
        public static readonly string Key_Counter_Dialog_CoreLatency =                  "Latency_DialogCore";
        public static readonly string Key_Counter_LU_WebRequestCount =                  "LU HTTP Requests/sec";
        public static readonly string Key_Counter_MySql_Connections =                   "MySql Connections/sec";
        public static readonly string Key_Counter_ResourcePool_UsedItems =              "Resource Pool Used Items";
        public static readonly string Key_Counter_ResourcePool_UsedCapacity =           "Resource Pool Usage %";
        public static readonly string Key_Counter_ThreadPool_CapacityThrottles =        "Thread Pool Capacity Throttles/sec";
        public static readonly string Key_Counter_ThreadPool_FatalErrors =              "Thread Pool Fatal Errors/sec";
        public static readonly string Key_Counter_ThreadPool_QueueRate =                "Thread Pool Queue Rate/sec";
        public static readonly string Key_Counter_ThreadPool_RunningWorkItems =         "Thread Pool Running Work Items";
        public static readonly string Key_Counter_ThreadPool_ShedItems =                "Thread Pool Shed Items/sec";
        public static readonly string Key_Counter_ThreadPool_TotalWorkItems =           "Thread Pool Total Work Items";
        public static readonly string Key_Counter_ThreadPool_UnhandledExceptions =      "Thread Pool Unhandled Exceptions/sec";
        public static readonly string Key_Counter_ThreadPool_UsedCapacity =             "Thread Pool Used Capacity %";
        public static readonly string Key_Counter_BatchProcessor_ItemsDropped =         "Batch Items Dropped";
        public static readonly string Key_Counter_BatchProcessor_ItemsFailed =          "Batch Items Failed";
        public static readonly string Key_Counter_BatchProcessor_ItemsSucceeded =       "Batch Items Succeeded";
        public static readonly string Key_Counter_BatchProcessor_ItemsQueued =          "Batch Items Queued";
        public static readonly string Key_Counter_BatchProcessor_BacklogItems =         "Batch Backlog Items";
        public static readonly string Key_Counter_BatchProcessor_BacklogPercent =       "Batch Backlog Size %";
        public static readonly string Key_Counter_QueriesDeprioritized =                "Queries Deprioritized/sec";
        public static readonly string Key_Counter_PostOffice_FragmentWriteTime =        "PO Fragment Write Time ms";
        public static readonly string Key_Counter_PostOffice_FragmentSortTime =         "PO Fragment Sort Time ms";
        public static readonly string Key_Counter_PostOffice_ReadAnyTime =              "PO ReadAny Time ms";
        public static readonly string Key_Counter_PostOffice_ReadSingleMessageTime =    "PO Read Single Message Time ms";
        public static readonly string Key_Counter_PostOffice_FragmentTransitTime =      "PO Fragment Transit Time ms";
        public static readonly string Key_Counter_PostOffice_WriteOperationTime =       "PO Write Operation Time ms";
        public static readonly string Key_Counter_PostOffice_NumFragmentsSent =         "PO Fragments Written/sec";
        public static readonly string Key_Counter_PostOffice_NumBytesSent =             "PO Bytes Written/sec";
        public static readonly string Key_Counter_PostOffice_NumBytesRead =             "PO Bytes Read/sec";
        public static readonly string Key_Counter_PostOffice_FragmentCRCFailures =      "PO Fragment CRC Failures/sec";
        public static readonly string Key_Counter_PostOffice_TransientBoxCount =        "PO Transient Box Count";
        public static readonly string Key_Counter_PostOffice_PermanentBoxCount =        "PO Permanent Box Count";
        public static readonly string Key_Counter_KeepAlive_RoundTripTime =             "Dialog KeepAlive RTT ms";
        public static readonly string Key_Counter_KeepAlive_QualityOfService =          "Dialog KeepAlive QOS";
        public static readonly string Key_Counter_AIMetrics_Sent =                      "AI Metrics Sent/sec";
        public static readonly string Key_Counter_AIMetrics_TracesFinalized =           "AI Traces Finalized/sec";
        public static readonly string Key_Counter_AIMetrics_NullTraces =                "AI Metric Null Traces/sec";
        public static readonly string Key_Counter_MMIO_BufferUsage =                    "MMIO Buffer Usage %";
        public static readonly string Key_Counter_MMIO_ReadTime =                       "MMIO Read Time ms";
        public static readonly string Key_Counter_MMIO_WriteTime =                      "MMIO Write Time ms";
        public static readonly string Key_Counter_MMIO_WriteTotalTime =                 "MMIO Write Total Time ms";
        public static readonly string Key_Counter_MMIO_WriteStalls =                    "MMIO Write Stalls/sec";
        public static readonly string Key_Counter_MMIO_ReadSpinwaitTime =               "MMIO Read Spinwait Time ms";
        public static readonly string Key_Counter_MMIO_ReadSingleSpinwaitTime =         "MMIO Read Single Spinwait Time ms";
        public static readonly string Key_Counter_PushPullBuffer_Underflow_Samples =    "PushPullBuffer Underflow Samples/sec";
        public static readonly string Key_Counter_AsyncAudioReadBuffer_Underflow_Samples = "AsyncAudioReadBuffer Underflow Samples/sec";
        public static readonly string Key_Counter_AsyncAudioWriteBuffer_Overflow_Samples = "AsyncAudioWriteBuffer Overflow Samples/sec";
        public static readonly string Key_Counter_PushPullBuffer_Overflow_Samples =     "PushPullBuffer Overflow Samples/sec";
        public static readonly string Key_Counter_LinearMixer_Overflow_Samples =        "LinearMixer Overflow Samples/sec";
        public static readonly string Key_Counter_BufferPool_BuffersRented =            "BufferPool Rentals/sec";
        public static readonly string Key_Counter_BufferPool_ElementsRented =           "BufferPool Rented/sec";
        public static readonly string Key_Counter_BufferPool_UnpooledAllocations =      "BufferPool UnpooledAlloc/sec";
        public static readonly string Key_Counter_BufferPool_PooledAllocations =        "BufferPool PooledAlloc/sec";
        public static readonly string Key_Counter_BufferPool_ElementsReclaimed =        "BufferPool Reclaimed/sec";
        public static readonly string Key_Counter_BufferPool_ElementsLost =             "BufferPool Lost/sec";
        public static readonly string Key_Counter_RemoteDialogExecutor_StartThread =    "RDE Start Thread ms";

        public static readonly string Key_Counter_MachineCpuUsage =                     "Machine CPU Usage %";
        public static readonly string Key_Counter_MachineMemoryAvailable =              "Machine Available Memory %";
        public static readonly string Key_Counter_ProcessCpuUsage =                     "Process CPU Usage %";
        public static readonly string Key_Counter_MachineFreeDiskSpace =                "Machine Disk Free Space %";
        public static readonly string Key_Counter_MachineDiskUsage =                    "Machine Disk Usage %";
        public static readonly string Key_Counter_MachineDiskReadTime =                 "Machine Disk Read Time %";
        public static readonly string Key_Counter_MachineDiskWriteTime =                "Machine Disk Write Time %";
        public static readonly string Key_Counter_ProcessMemoryPrivateWorkingSet =      "Process Private Working Set KB";
        public static readonly string Key_Counter_ClrThreadCount =                      "CLR Threadpool Thread Count";
        public static readonly string Key_Counter_ClrThreadQueueLength =                "CLR Threadpool Queue Length";
        public static readonly string Key_Counter_ClrAllocationRateKb =                 "CLR Allocation rate KB / sec";
        public static readonly string Key_Counter_ClrTimerCount =                       "CLR Timer Count";
        public static readonly string Key_Counter_ClrGcFragmentation =                  "CLR GC Fragmentation %";
        public static readonly string Key_Counter_ClrContentionRate =                   "CLR Contention Rate / sec";
        public static readonly string Key_Counter_ClrProcessLogicalThreads =            "CLR Process Logical Threads";
        public static readonly string Key_Counter_ClrProcessPhysicalThreads =           "CLR Process Physical Threads";
        public static readonly string Key_Counter_ClrExceptionsThrown =                 "CLR Exceptions Thrown/sec";
        public static readonly string Key_Counter_ClrTimeInJit =                        "CLR Time in JIT ms";
        public static readonly string Key_Counter_ClrLargeObjectHeapKb =                "CLR Large Object Heap KB";
        public static readonly string Key_Counter_ClrPinnedObjectHeapKb =               "CLR Pinned Object Heap KB";
        public static readonly string Key_Counter_ClrTimeInGc =                         "CLR % Time in GC";
        public static readonly string Key_Counter_ClrTotalCommittedKb =                 "CLR Total Committed KB";
        public static readonly string Key_Counter_ClrGen0HeapSizeKb =                   "CLR Gen 0 Heap Size KB";
        public static readonly string Key_Counter_ClrGen1HeapSizeKb =                   "CLR Gen 1 Heap Size KB";
        public static readonly string Key_Counter_ClrGen2HeapSizeKb =                   "CLR Gen 2 Heap Size KB";
        public static readonly string Key_Counter_ClrTotalAppdomains =                  "CLR Total Appdomains";
        public static readonly string Key_Counter_ClrTotalClassesLoaded =               "CLR Total Classes Loaded";
        public static readonly string Key_Counter_ClrTotalAssembliesLoaded =            "CLR Total Assemblies Loaded";
        public static readonly string Key_Counter_TCPV4Connections =                    "TCPv4 Connections Established";
        public static readonly string Key_Counter_TCPV6Connections =                    "TCPv6 Connections Established";

        public static readonly string Key_Counter_WorkerThreadsDestroyed =              "Worker Threads Destroyed / sec";
        public static readonly string Key_Counter_WorkerThreadsCreated =                "Worker Threads Created / sec";
        public static readonly string Key_Counter_IOThreadsDestroyed =                  "IO Threads Destroyed / sec";
        public static readonly string Key_Counter_IOThreadsCreated =                    "IO Threads Created / sec";
        public static readonly string Key_Counter_ReservedWorkerThreads =               "Reserved Worker Threads";
        public static readonly string Key_Counter_ActiveWorkerThreads =                 "Active Worker Threads";
        public static readonly string Key_Counter_WorkerThreadCapacityUsed =            "Worker Thread % Capacity Used";
        public static readonly string Key_Counter_ReservedIOThreads =                   "Reserved IO Threads";
        public static readonly string Key_Counter_ActiveIOThreads =                     "Active IO Threads";
        public static readonly string Key_Counter_IOThreadCapacityUsed =                "IO Thread % Capacity Used";

        public static readonly string Key_Counter_H2_ClientSessionsInitiated =          "HTTP2 New Client Sessions / sec";
        public static readonly string Key_Counter_H2_ServerSessionsInitiated =          "HTTP2 New Server Sessions / sec";
        public static readonly string Key_Counter_H2_ClientSessionsUpgraded =           "HTTP2 Client Upgrades / sec";
        public static readonly string Key_Counter_Http_OutgoingRequests20 =             "HTTP2 Outgoing Requests / sec";
        public static readonly string Key_Counter_Http_OutgoingRequests20Fulfilled =    "HTTP2 PushPromise Fulfilled / sec";
        public static readonly string Key_Counter_Http_OutgoingRequests11 =             "HTTP1.1 Outgoing Requests / sec";
        public static readonly string Key_Counter_Http_OutgoingRequests10 =             "HTTP1.0 Outgoing Requests / sec";
        public static readonly string Key_Counter_Http_IncomingRequests20 =             "HTTP2 Incoming Requests / sec";
        public static readonly string Key_Counter_Http_IncomingRequests11 =             "HTTP1.1 Incoming Requests / sec";
        public static readonly string Key_Counter_Http_IncomingRequests10 =             "HTTP1.0 Incoming Requests / sec";

        public static readonly string Key_Dimension_ServiceName = "ServiceName";
        public static readonly string Key_Dimension_ServiceVersion = "ServiceVersion";
        public static readonly string Key_Dimension_HostName = "HostName";
        public static readonly string Key_Dimension_BufferPoolName = "Pool";
        public static readonly string Key_Dimension_ThreadPoolName = "Pool";
        public static readonly string Key_Dimension_ResourcePoolName = "Pool";
        public static readonly string Key_Dimension_MySqlConnectionName = "MySqlConnection";
        public static readonly string Key_Dimension_IPCMethod = "IPCMethod";
        public static readonly string Key_Dimension_HttpAction = "Action";
        public static readonly string Key_Dimension_ContainerName = "Container";
        public static readonly string Key_Dimension_BatchProcessorName = "BatchProcessor";

        private static readonly JsonSerializerSettings DEFAULT_SERIALIZER_SETTINGS = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateParseHandling = DateParseHandling.None,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffff",
            Converters = new List<JsonConverter>(new JsonConverter[] { new NoBinaryJsonConverter() }),
            Formatting = Formatting.None,
            StringEscapeHandling = StringEscapeHandling.Default
        };

        private static readonly JsonSerializer DEFAULT_SERIALIZER = new JsonSerializer()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateParseHandling = DateParseHandling.None,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffff",
            Formatting = Formatting.None,
            StringEscapeHandling = StringEscapeHandling.Default
        };

        private static readonly JsonMergeSettings DEFAULT_MERGE_SETTINGS = new JsonMergeSettings()
        {
            MergeArrayHandling = MergeArrayHandling.Merge,
            MergeNullValueHandling = MergeNullValueHandling.Merge
        };

        private static readonly JsonMergeSettings INSTRUMENTATION_MERGE_SETTINGS = new JsonMergeSettings()
        {
            MergeArrayHandling = MergeArrayHandling.Concat,
            MergeNullValueHandling = MergeNullValueHandling.Merge
        };

        static CommonInstrumentation()
        {
            DEFAULT_SERIALIZER.Converters.Add(new NoBinaryJsonConverter());
        }

        public static string GenerateLatencyEntry(string component, double latency, DateTimeOffset currentTime)
        {
            DateTimeOffset startTime = currentTime.Subtract(TimeSpanExtensions.TimeSpanFromMillisecondsPrecise(latency));
            return string.Format("{{\"Perf\":{{\"Latency\":{{\"{0}\":{{\"Values\":[{{\"Value\":{1:F2},\"StartTime\":{2}}}]}}}}}}}}", component, latency, startTime.Ticks);
        }

        public static string GenerateInstancedLatencyEntry(string component, string instance, double latency, DateTimeOffset currentTime)
        {
            DateTimeOffset startTime = currentTime.Subtract(TimeSpanExtensions.TimeSpanFromMillisecondsPrecise(latency));
            return string.Format("{{\"Perf\":{{\"Latency\":{{\"{0}\":{{\"Values\":[{{\"Id\":\"{1}\",\"Value\":{2:F2},\"StartTime\":{3}}}]}}}}}}}}", component, instance, latency, startTime.Ticks);
        }

        public static string GenerateSizeEntry(string component, long size)
        {
            return string.Format("{{\"Perf\":{{\"Size\":{{\"{0}\":{{\"Values\":[{{\"Value\":{1}}}]}}}}}}}}", component, size);
        }

        public static string GenerateInstancedSizeEntry(string component, string instance, long size)
        {
            return string.Format("{{\"Perf\":{{\"Size\":{{\"{0}\":{{\"Values\":[{{\"Id\":\"{1}\",\"Value\":{2}}}]}}}}}}}}", component, instance, size);
        }

        public static string GenerateLatencyEntry(string component, int latencyMs)
        {
            return GenerateLatencyEntry(component, (double)latencyMs, HighPrecisionTimer.GetCurrentUTCTime());
        }

        public static string GenerateLatencyEntry(string component, long latencyMs)
        {
            return GenerateLatencyEntry(component, (double)latencyMs, HighPrecisionTimer.GetCurrentUTCTime());
        }
        
        public static string GenerateLatencyEntry(string component, TimeSpan latency)
        {
            return GenerateLatencyEntry(component, latency.TotalMilliseconds, HighPrecisionTimer.GetCurrentUTCTime());
        }

        public static string GenerateLatencyEntry(string component, Stopwatch timer)
        {
            return GenerateLatencyEntry(component, timer.ElapsedMillisecondsPrecise(), HighPrecisionTimer.GetCurrentUTCTime());
        }

        public static string GenerateLatencyEntry(string component, ref ValueStopwatch timer)
        {
            return GenerateLatencyEntry(component, timer.ElapsedMillisecondsPrecise(), HighPrecisionTimer.GetCurrentUTCTime());
        }

        public static string GenerateLatencyEntry(string component, double latencyMs)
        {
            return GenerateLatencyEntry(component, latencyMs, HighPrecisionTimer.GetCurrentUTCTime());
        }

        public static string GenerateInstancedLatencyEntry(string component, string instance, TimeSpan latency)
        {
            return GenerateInstancedLatencyEntry(component, instance, latency.TotalMilliseconds, HighPrecisionTimer.GetCurrentUTCTime());
        }

        public static string GenerateInstancedLatencyEntry(string component, string instance, Stopwatch timer)
        {
            return GenerateInstancedLatencyEntry(component, instance, timer.ElapsedMillisecondsPrecise(), HighPrecisionTimer.GetCurrentUTCTime());
        }

        public static string GenerateInstancedLatencyEntry(string component, string instance, ref ValueStopwatch timer)
        {
            return GenerateInstancedLatencyEntry(component, instance, timer.ElapsedMillisecondsPrecise(), HighPrecisionTimer.GetCurrentUTCTime());
        }

        public static string GenerateInstancedLatencyEntry(string component, string instance, double latencyMs)
        {
            return GenerateInstancedLatencyEntry(component, instance, latencyMs, HighPrecisionTimer.GetCurrentUTCTime());
        }

        public static string GenerateObjectEntry(string path, object obj)
        {
            if (obj == null)
            {
                return WrapValueWithPath(path, "null");
            }

            try
            {
                return WrapJsonObjectWithPath(path, DEFAULT_SERIALIZER, obj);
            }
            catch (Exception e)
            {
                return WrapValueWithPath(path, e.Message);
            }
        }

        public static bool IsEncrypted(string logMessage)
        {
            return logMessage.StartsWith(Encrypted_Message_Prefix, StringComparison.Ordinal);
        }

        public static bool IsEncrypted(StringBuilder logMessageBuffer)
        {
            if (logMessageBuffer.Length < Encrypted_Message_Prefix.Length)
            {
                return false;
            }

            for (int c = 0; c <  Encrypted_Message_Prefix.Length; c++)
            {
                if (logMessageBuffer[c] != Encrypted_Message_Prefix[c])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Accepts a JToken and returns that same token nested inside the specified jpath.
        /// For example, "$.Prop1.Key" will output { "Prop1": { "Key": YOUR_TOKEN } }
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="jpath"></param>
        /// <returns></returns>
        public static JToken PrependPath(JToken obj, string jpath)
        {
            if (string.Equals(jpath, "$"))
            {
                return obj;
            }
            if (!jpath.StartsWith("$."))
            {
                throw new FormatException(string.Format("JPath \"{0}\" is invalid (must be a simple object chain like $.Prop1.Prop2)", jpath));
            }

            JObject root = new JObject();
            JObject iter = root;
            int start = 2;
            int end = jpath.IndexOf('.', start);
            while (end > start)
            {
                JObject newObject = new JObject();
                iter.Add(jpath.Substring(start, end - start), newObject);
                iter = newObject;
                start = end + 1;
                end = jpath.IndexOf('.', start);
            }

            iter.Add(jpath.Substring(start), obj);
            return root;
        }

        /// <summary>
        /// Sets a particular field inside of a JObject to null, if it exists
        /// </summary>
        /// <param name="obj">An object to modify</param>
        /// <param name="jpath">The JPath of the field to modify, such as "$.Struct.Prop1"</param>
        public static void NullifyField(JObject obj, string jpath)
        {
            if (string.Equals(jpath, "$"))
            {
                return;
            }
            if (!jpath.StartsWith("$."))
            {
                throw new FormatException(string.Format("JPath \"{0}\" is invalid (must be a simple object chain like $.Prop1.Prop2)", jpath));
            }
            
            JToken iter = obj;
            int start = 2;
            int end = jpath.IndexOf('.', start);
            string propName;
            while (iter != null && end > start)
            {
                propName = jpath.Substring(start, end - start);
                iter = iter[propName];
                start = end + 1;
                end = jpath.IndexOf('.', start);
            }

            propName = jpath.Substring(start);
            if (iter != null &&
                iter[propName] != null)
            {
                iter[propName] = null;
            }
        }

        public static JObject ToJObject(object obj)
        {
            return JObject.FromObject(obj, DEFAULT_SERIALIZER);
        }

        public static string FromJObject(JToken obj)
        {
            using (PooledStringBuilder builder = StringBuilderPool.Rent())
            using (StringBuilderTextWriter textWriter = new StringBuilderTextWriter(builder.Builder))
            {
                DEFAULT_SERIALIZER.Serialize(textWriter, obj);
                return builder.Builder.ToString();
            }
        }

        public static string FromJObject(JToken obj, StringBuilder stringBuilder)
        {
            using (StringBuilderTextWriter textWriter = new StringBuilderTextWriter(stringBuilder))
            {
                DEFAULT_SERIALIZER.Serialize(textWriter, obj);
                return stringBuilder.ToString();
            }
        }

        public static IReadOnlyDictionary<string, DataPrivacyClassification> GetPrivacyMappingsDialogRequest()
        {
            return DEFAULT_PRIVACY_MAPPINGS_DIALOG_REQUEST;
        }

        private static readonly IReadOnlyDictionary<string, DataPrivacyClassification> DEFAULT_PRIVACY_MAPPINGS_DIALOG_REQUEST = new Dictionary<string, DataPrivacyClassification>()
        {
            { "$.Dialog.ClientRequest.ClientContext.ClientId", DataPrivacyClassification.EndUserPseudonymousIdentifiers },
            { "$.Dialog.ClientRequest.ClientContext.UserId", DataPrivacyClassification.EndUserPseudonymousIdentifiers },
            { "$.Dialog.ClientRequest.ClientContext.ClientName", DataPrivacyClassification.EndUserPseudonymousIdentifiers },
            { "$.Dialog.ClientRequest.ClientContext.Latitude", DataPrivacyClassification.EndUserIdentifiableInformation },
            { "$.Dialog.ClientRequest.ClientContext.Longitude", DataPrivacyClassification.EndUserIdentifiableInformation },
            { "$.Dialog.ClientRequest.ClientContext.ExtraClientContext", DataPrivacyClassification.PublicPersonalData | DataPrivacyClassification.EndUserIdentifiableInformation },
            { "$.Dialog.ClientRequest.TextInput", DataPrivacyClassification.PrivateContent },
            { "$.Dialog.ClientRequest.AudioInput.Data", DataPrivacyClassification.PrivateContent },
            { "$.Dialog.ClientRequest.SpeechInput.RecognizedPhrases[*].DisplayText", DataPrivacyClassification.PrivateContent },
            { "$.Dialog.ClientRequest.SpeechInput.RecognizedPhrases[*].LexicalForm", DataPrivacyClassification.PrivateContent },
            { "$.Dialog.ClientRequest.SpeechInput.RecognizedPhrases[*].PhraseElements", DataPrivacyClassification.PrivateContent },
            { "$.Dialog.ClientRequest.SpeechInput.RecognizedPhrases[*].InverseTextNormalizationResults", DataPrivacyClassification.PrivateContent },
            { "$.Dialog.ClientRequest.SpeechInput.RecognizedPhrases[*].MaskedInverseTextNormalizationResults", DataPrivacyClassification.PrivateContent },
            { "$.Dialog.ClientRequest.RequestData", DataPrivacyClassification.PrivateContent },
        };

        public static IReadOnlyDictionary<string, DataPrivacyClassification> GetPrivacyMappingsDialogResponse(DataPrivacyClassification pluginResponseClassification)
        {
            return new Dictionary<string, DataPrivacyClassification>()
            {
                { "$.Dialog.DialogProcessorResponse.SpokenSsml", pluginResponseClassification },
                { "$.Dialog.DialogProcessorResponse.DisplayedText", pluginResponseClassification },
                { "$.Dialog.DialogProcessorResponse.PresentationHtml", pluginResponseClassification },
                { "$.Dialog.DialogProcessorResponse.AugmentedQuery", DataPrivacyClassification.PrivateContent },
                { "$.Dialog.DialogProcessorResponse.SelectedRecoResult.Utterance", DataPrivacyClassification.PrivateContent },
                { "$.Dialog.DialogProcessorResponse.SelectedRecoResult.TagHyps[*].Utterance", DataPrivacyClassification.PrivateContent },
                { "$.Dialog.DialogProcessorResponse.SelectedRecoResult.TagHyps[*].Slots[*].Value", DataPrivacyClassification.PrivateContent },
                { "$.Dialog.DialogProcessorResponse.SelectedRecoResult.TagHyps[*].Slots[*].LexicalForm", DataPrivacyClassification.PrivateContent },
                { "$.Dialog.DialogProcessorResponse.SelectedRecoResult.TagHyps[*].Annotations", DataPrivacyClassification.PrivateContent },
                { "$.Dialog.DialogProcessorResponse.ResponseData", pluginResponseClassification },
            };
        }

        public static IReadOnlyDictionary<string, DataPrivacyClassification> GetPrivacyMappingsLURequest()
        {
            return DEFAULT_PRIVACY_MAPPINGS_LU_REQUEST;
        }

        private static readonly IReadOnlyDictionary<string, DataPrivacyClassification> DEFAULT_PRIVACY_MAPPINGS_LU_REQUEST = new Dictionary<string, DataPrivacyClassification>()
        {
            { "$.LU.Request.Context.ClientId", DataPrivacyClassification.EndUserPseudonymousIdentifiers },
            { "$.LU.Request.Context.UserId", DataPrivacyClassification.EndUserPseudonymousIdentifiers },
            { "$.LU.Request.Context.ClientName", DataPrivacyClassification.EndUserPseudonymousIdentifiers },
            { "$.LU.Request.Context.Latitude", DataPrivacyClassification.EndUserIdentifiableInformation },
            { "$.LU.Request.Context.Longitude", DataPrivacyClassification.EndUserIdentifiableInformation },
            { "$.LU.Request.Context.ExtraClientContext", DataPrivacyClassification.PublicPersonalData | DataPrivacyClassification.EndUserIdentifiableInformation },
            { "$.LU.Request.SpeechInput.RecognizedPhrases[*].DisplayText", DataPrivacyClassification.PrivateContent },
            { "$.LU.Request.SpeechInput.RecognizedPhrases[*].LexicalForm", DataPrivacyClassification.PrivateContent },
            { "$.LU.Request.SpeechInput.RecognizedPhrases[*].PhraseElements", DataPrivacyClassification.PrivateContent },
            { "$.LU.Request.SpeechInput.RecognizedPhrases[*].InverseTextNormalizationResults", DataPrivacyClassification.PrivateContent },
            { "$.LU.Request.SpeechInput.RecognizedPhrases[*].MaskedInverseTextNormalizationResults", DataPrivacyClassification.PrivateContent },
        };

        public static IReadOnlyDictionary<string, DataPrivacyClassification> GetPrivacyMappingsLUResponse()
        {
            return DEFAULT_PRIVACY_MAPPINGS_LU_RESPONSE;
        }

        private static readonly IReadOnlyDictionary<string, DataPrivacyClassification> DEFAULT_PRIVACY_MAPPINGS_LU_RESPONSE = new Dictionary<string, DataPrivacyClassification>()
        {
            { "$.LU.Response.Results[*].Utterance", DataPrivacyClassification.PrivateContent },
            { "$.LU.Response.Results[*].Recognition", DataPrivacyClassification.PrivateContent },
            { "$.LU.Response.Results[*].Recognition.Utterance", DataPrivacyClassification.PrivateContent },
            { "$.LU.Response.Results[*].Recognition.TagHyps[*].Utterance", DataPrivacyClassification.PrivateContent },
            { "$.LU.Response.Results[*].Recognition.TagHyps[*].Slots[*].Value", DataPrivacyClassification.PrivateContent },
            { "$.LU.Response.Results[*].Recognition.TagHyps[*].Slots[*].LexicalForm", DataPrivacyClassification.PrivateContent },
            { "$.LU.Response.Results[*].Recognition.TagHyps[*].Annotations", DataPrivacyClassification.PrivateContent },
            //{ "$.LU.Response.Results[*].EntityContext", DataPrivacyClassification.PrivateContent },
        };

        public static string GetFirst3DigitsOfTraceId(Guid traceId)
        {
            Span<Guid> guidSpan = stackalloc Guid[1];
            guidSpan[0] = traceId;
            Span<byte> guidBytes = MemoryMarshal.Cast<Guid, byte>(guidSpan);

            char[] chars = new char[3];
            int v;
            v = ((guidBytes[3] >> 4) & 0xF);
            chars[0] = v < 10 ? (char)('0' + v) : (char)('a' + v - 10);
            v = ((guidBytes[3] >> 0) & 0xF);
            chars[1] = v < 10 ? (char)('0' + v) : (char)('a' + v - 10);
            v = ((guidBytes[2] >> 4) & 0xF);
            chars[2] = v < 10 ? (char)('0' + v) : (char)('a' + v - 10);
            return new string(chars);
        }

        /// <summary>
        /// Equivalent of doing StringBuilder.Append(Guid.ToString("N").Substring(0, 3)), but more performant.
        /// </summary>
        /// <param name="traceId">The trace ID to print</param>
        /// <param name="buffer">The string builder to print to</param>
        public static void GetFirst3DigitsOfTraceId(Guid traceId, StringBuilder buffer)
        {
            Span<Guid> guidSpan = stackalloc Guid[1];
            guidSpan[0] = traceId;
            Span<byte> guidBytes = MemoryMarshal.Cast<Guid, byte>(guidSpan);

            // FIXME: Not sure if endianness matters here
            int v;
            v = ((guidBytes[3] >> 4) & 0xF);
            buffer.Append(v < 10 ? (char)('0' + v) : (char)('a' + v - 10));
            v = ((guidBytes[3] >> 0) & 0xF);
            buffer.Append(v < 10 ? (char)('0' + v) : (char)('a' + v - 10));
            v = ((guidBytes[2] >> 4) & 0xF);
            buffer.Append(v < 10 ? (char)('0' + v) : (char)('a' + v - 10));
        }

        public static string FormatTraceId(Guid? traceId)
        {
            if (traceId.HasValue)
            {
                return traceId.Value.ToString("N");
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Equivalent of doing StringBuilder.Append(Guid.ToString("N")), but more performant.
        /// </summary>
        /// <param name="traceId">The trace ID to print</param>
        /// <param name="buffer">The string builder to print to</param>
        public static void FormatTraceId(Guid traceId, StringBuilder buffer)
        {
            buffer.AssertNonNull(nameof(buffer));
            Span<Guid> guidSpan = stackalloc Guid[1];
            guidSpan[0] = traceId;
            ReadOnlySpan<byte> guidBytes = MemoryMarshal.Cast<Guid, byte>(guidSpan);
            Span<char> formattedGuid = stackalloc char[32];
            int iter = 0;

            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[3] >> 4) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[3] >> 0) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[2] >> 4) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[2] >> 0) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[1] >> 4) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[1] >> 0) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[0] >> 4) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[0] >> 0) & 0xF];

            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[5] >> 4) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[5] >> 0) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[4] >> 4) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[4] >> 0) & 0xF];

            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[7] >> 4) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[7] >> 0) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[6] >> 4) & 0xF];
            formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[6] >> 0) & 0xF];

            for (int remain = 8; remain < 16; remain++)
            {
                formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[remain] >> 4) & 0xF];
                formattedGuid[iter++] = HexDigitsLowerCase[(guidBytes[remain] >> 0) & 0xF];
            }

#if NET6_0_OR_GREATER
            buffer.Append(formattedGuid);
#else
            foreach (char c in formattedGuid)
            {
                // bogus...
                buffer.Append(c);
            }
#endif
        }

        #region In case you're feeling crazy and want really fast guid -> string conversion

#if NET6_0_OR_GREATER && UNSAFE
        // Allocated on native heap so we never have to pin it.
        // In the future this could be embedded directly into the exe
        private static readonly IntPtr GUID_BYTE_SWAPPER_128;
        private static readonly IntPtr CHAR_MAPPER_256;
        private static readonly IntPtr BIT_MASK_256;

        static CommonInstrumentation()
        {
            GUID_BYTE_SWAPPER_128 = Marshal.AllocHGlobal(16);
            Marshal.Copy(new byte[] { 3, 2, 1, 0, 5, 4, 7, 6, 8, 9, 10, 11, 12, 13, 14, 15 }, 0, GUID_BYTE_SWAPPER_128, 16);
            BIT_MASK_256 = Marshal.AllocHGlobal(32);
            Marshal.Copy(new byte[] { 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF,
                                  0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF }, 0, BIT_MASK_256, 32);
            CHAR_MAPPER_256 = Marshal.AllocHGlobal(32);
            Marshal.Copy(new byte[] {
            (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f',
            (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f'}, 0, CHAR_MAPPER_256, 32);
        }

        public static unsafe void FormatTraceIdAVX2(Guid traceId, StringBuilder buffer)
        {
            if (!Avx2.IsSupported)
            {
                FormatTraceId(traceId, buffer);
                return;
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            char* formattedGuid = stackalloc char[32];
            Span<char> debugOutput = new Span<char>(formattedGuid, 32);

            // Step 1. Do initial byte swapping using SSE 128-bit permute
            Vector128<byte> swappedBytes = Ssse3.Shuffle(
                Ssse3.LoadVector128((byte*)&traceId),
                Ssse3.LoadVector128((byte*)GUID_BYTE_SWAPPER_128));

            // Now map 128 bits of byte data to 512 bytes of Unicode chars
            // Step 2. Split bytes into high and low nibbles and expand
            Vector256<short> widened = Avx2.ConvertToVector256Int16(swappedBytes);

            Vector256<byte> nibbleMask = Avx2.LoadVector256((byte*)BIT_MASK_256);
            Vector256<byte> shifted = Avx2.Or(
                Avx2.And(Avx2.ShiftLeftLogical(widened, 8).AsByte(), nibbleMask),
                Avx2.And(Avx2.ShiftRightLogical(widened, 4).AsByte(), nibbleMask));

            // Step 3. Map nibbles to ASCII hex chars
            Vector256<byte> mappedBytes = Avx2.Shuffle(Avx2.LoadVector256((byte*)CHAR_MAPPER_256), shifted);

            // Step 4. Expand ASCII to Unicode (int16) and copy to output
            Avx2.Store(((short*)formattedGuid) + 0, Avx2.ConvertToVector256Int16(Avx2.ExtractVector128(mappedBytes, 0)));
            Avx2.Store(((short*)formattedGuid) + 16, Avx2.ConvertToVector256Int16(Avx2.ExtractVector128(mappedBytes, 1)));

            buffer.Append(formattedGuid, 32);
        }
#endif

#endregion

        /// <summary>
        /// Shim method to convert old-style string trace IDs into GUIDs
        /// </summary>
        /// <param name="traceIdString"></param>
        /// <returns></returns>
        public static Guid? TryParseTraceIdGuid(string traceIdString)
        {
            Guid returnVal;
            if (Guid.TryParse(traceIdString, out returnVal))
            {
                return returnVal;
            }

            return null;
        }

        public static void WritePrivacyClassification(DataPrivacyClassification privacyClass, StringBuilder output)
        {
            if (privacyClass == DataPrivacyClassification.Unknown)
            {
                output.Append("UNK");
                return;
            }

            bool first = true;
            if ((privacyClass & DataPrivacyClassification.PrivateContent) != 0)
            {
                if (!first)
                {
                    output.Append(',');
                }

                output.Append("PRIV");
                first = false;
            }

            if ((privacyClass & DataPrivacyClassification.EndUserIdentifiableInformation) != 0)
            {
                if (!first)
                {
                    output.Append(',');
                }

                output.Append("EUII");
                first = false;
            }

            if ((privacyClass & DataPrivacyClassification.EndUserPseudonymousIdentifiers) != 0)
            {
                if (!first)
                {
                    output.Append(',');
                }

                output.Append("EUPI");
                first = false;
            }

            if ((privacyClass & DataPrivacyClassification.PublicPersonalData) != 0)
            {
                if (!first)
                {
                    output.Append(',');
                }

                output.Append("PPD");
                first = false;
            }

            if ((privacyClass & DataPrivacyClassification.PublicNonPersonalData) != 0)
            {
                if (!first)
                {
                    output.Append(',');
                }

                output.Append("PNPD");
                first = false;
            }

            if ((privacyClass & DataPrivacyClassification.SystemMetadata) != 0)
            {
                if (!first)
                {
                    output.Append(',');
                }

                output.Append("META");
                first = false;
            }
        }

        public static DataPrivacyClassification ParsePrivacyClassString(string value)
        {
            value.AssertNonNull(nameof(value));
            DataPrivacyClassification returnVal = DataPrivacyClassification.Unknown;

            int start = 0;
            int end = value.IndexOf(',', start);
            if (end < start)
            {
                end = value.Length;
            }

            while (start < value.Length && end > start)
            {
                ReadOnlySpan<char> span = value.AsSpan(start, end - start);
                if ("META".AsSpan().Equals(span, StringComparison.OrdinalIgnoreCase))
                {
                    returnVal |= DataPrivacyClassification.SystemMetadata;
                }
                else if ("PRIV".AsSpan().Equals(span, StringComparison.OrdinalIgnoreCase))
                {
                    returnVal |= DataPrivacyClassification.PrivateContent;
                }
                else if ("EUII".AsSpan().Equals(span, StringComparison.OrdinalIgnoreCase))
                {
                    returnVal |= DataPrivacyClassification.EndUserIdentifiableInformation;
                }
                else if ("EUPI".AsSpan().Equals(span, StringComparison.OrdinalIgnoreCase))
                {
                    returnVal |= DataPrivacyClassification.EndUserPseudonymousIdentifiers;
                }
                else if ("PPD".AsSpan().Equals(span, StringComparison.OrdinalIgnoreCase))
                {
                    returnVal |= DataPrivacyClassification.PublicPersonalData;
                }
                else if ("PNPD".AsSpan().Equals(span, StringComparison.OrdinalIgnoreCase))
                {
                    returnVal |= DataPrivacyClassification.PublicNonPersonalData;
                }
                start = end + 1;
                if (start < value.Length)
                {
                    end = value.IndexOf(',', start);

                    if (end < start)
                    {
                        end = value.Length;
                    }
                }
            }

            return returnVal;
        }

        //public static string GenerateObjectEntryEscaped(string path, object obj)
        //{
        //    string serializedObj = JsonConvert.SerializeObject(obj);
        //    return WrapValueWithPath(path, "\"" + EscapeJson(serializedObj) + "\"");
        //}

        /// <summary>
        /// Accepts a set of instrumentation log events and merges them together into a single coherent JSON object.
        /// This method only works if each instrumentation message is formatted as a json partial impression
        /// </summary>
        /// <param name="jsonImpressions">The list of events to merge</param>
        /// <param name="mergeLogger">A logger for this operation</param>
        /// <returns></returns>
        public static JObject MergeImpressions(IEnumerable<string> jsonImpressions, ILogger mergeLogger)
        {
            // Parse each one as a JObject
            IList<JObject> objects = new List<JObject>();

            foreach (string message in jsonImpressions)
            {
                try
                {
                    JObject j = JObject.Parse(message);
                    if (j != null)
                    {
                        objects.Add(j);
                    }
                }
                catch (JsonException e2)
                {
                    mergeLogger.Log("Json error while merging impressions: " + e2.Message, LogLevel.Err);
                    mergeLogger.Log("This was the input: " + message, LogLevel.Err);
                }
            }

            return MergeJObjects(objects, mergeLogger, INSTRUMENTATION_MERGE_SETTINGS);
        }

        /// <summary>
        /// Given a path such as "One.Two", returns a JSON heirarchical object string in the form of { "One": { "Two": (VALUE) } }
        /// </summary>
        /// <param name="path"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string WrapValueWithPath(string path, string value)
        {
            if (string.IsNullOrEmpty(path))
            {
                return value;
            }

            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder buffer = pooledSb.Builder;
                int bracketsAdded = 0;
                int prevIdx = 0;
                int idx = 0;
                while (idx >= 0)
                {
                    idx = path.IndexOf('.', prevIdx);
                    if (idx > 0 &&
                        idx - prevIdx > 1) // this clause will skip over ".." bits (which should be invalid anyways)
                    {
                        buffer.Append("{\"");
                        for (int c = prevIdx; c < idx; c++)
                        {
                            buffer.Append(path[c]);
                        }
                        buffer.Append("\":");
                        bracketsAdded++;
                        prevIdx = idx + 1;
                    }
                }

                buffer.Append("{\"");
                idx = path.Length;
                for (int c = prevIdx; c < idx; c++)
                {
                    buffer.Append(path[c]);
                }
                buffer.Append("\":");
                bracketsAdded++;

                buffer.Append(value);

                for (int b = 0; b < bracketsAdded; b++)
                {
                    buffer.Append("}");
                }

                return buffer.ToString();
            }
        }

        private static string WrapJsonObjectWithPath(string path, JsonSerializer serializer, object obj)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder buffer = pooledSb.Builder;

                if (string.IsNullOrEmpty(path))
                {
                    using (StringBuilderTextWriter writer = new StringBuilderTextWriter(buffer))
                    {
                        serializer.Serialize(writer, obj);
                        return buffer.ToString();
                    }
                }

                int bracketsAdded = 0;
                int prevIdx = 0;
                int idx = 0;
                while (idx >= 0)
                {
                    idx = path.IndexOf('.', prevIdx);
                    if (idx > 0 &&
                        idx - prevIdx > 1) // this clause will skip over ".." bits (which should be invalid anyways)
                    {
                        buffer.Append("{\"");
                        for (int c = prevIdx; c < idx; c++)
                        {
                            buffer.Append(path[c]);
                        }
                        buffer.Append("\":");
                        bracketsAdded++;
                        prevIdx = idx + 1;
                    }
                }

                buffer.Append("{\"");
                idx = path.Length;
                for (int c = prevIdx; c < idx; c++)
                {
                    buffer.Append(path[c]);
                }
                buffer.Append("\":");
                bracketsAdded++;

                using (StringBuilderTextWriter writer = new StringBuilderTextWriter(buffer))
                {
                    serializer.Serialize(writer, obj);
                }

                for (int b = 0; b < bracketsAdded; b++)
                {
                    buffer.Append("}");
                }

                return buffer.ToString();
            }
        }

        private static string EscapeJson(string json)
        {
            return json.Replace("\"", "\\\"");
        }

        /// <summary>
        /// Returns the union of a set of arbitrary JObjects.
        /// </summary>
        /// <param name="objects"></param>
        /// <param name="mergeLogger"></param>
        /// <returns></returns>
        public static JObject MergeJObjects(IList<JObject> objects, ILogger mergeLogger)
        {
            return MergeJObjects(objects, mergeLogger, DEFAULT_MERGE_SETTINGS);
        }

        /// <summary>
        /// Returns the union of a set of arbitrary JObjects.
        /// </summary>
        /// <param name="objects"></param>
        /// <param name="mergeLogger"></param>
        /// <param name="mergeSettings">Specific merge settings to use</param>
        /// <returns></returns>
        public static JObject MergeJObjects(IList<JObject> objects, ILogger mergeLogger, JsonMergeSettings mergeSettings)
        {
            JObject mergedObject = new JObject();
            foreach (JObject toAdd in objects)
            {
                //RecursiveMergeJson(mergedObject, toAdd.Root, mergeLogger);
                mergedObject.Merge(toAdd, mergeSettings);
            }

            return mergedObject;
        }

        private static bool ArrayContainsOnlyObjects(JArray array)
        {
            foreach (JToken child in array.Children())
            {
                if (child.Type != JTokenType.Object &&
                    child.Type != JTokenType.Null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void RecursiveMergeJson(JToken targetTree, JToken sourceTree, ILogger mergeLogger)
        {
            if (targetTree.Type != sourceTree.Type)
            {
                mergeLogger.Log("Warning: Token types don't match for two merged JTokens; ignoring this node", LogLevel.Wrn);
                mergeLogger.Log(targetTree.Path + " and " + sourceTree.Path, LogLevel.Wrn);
                return;
            }
            
            // Add the tokens at the current level
            if (sourceTree.Type == JTokenType.Object)
            {
                JObject targetTreeObject = targetTree as JObject;

                foreach (var child in sourceTree.Children())
                {
                    if (child.Type == JTokenType.Property)
                    {
                        JProperty childProp = child as JProperty;

                        // If the property has a scalar type we need to recurse into it
                        if (childProp.Value.Type == JTokenType.Object)
                        {
                            if (targetTree[childProp.Name] == null)
                            {
                                // Matching node doesn't exist; create one and then start recursing into it
                                JObject subTree = new JObject();
                                RecursiveMergeJson(subTree, childProp.Value, mergeLogger);
                                targetTreeObject.Add(childProp.Name, subTree);
                            }
                            else
                            {
                                // A property with this name already exists; just recurse into the existing one
                                RecursiveMergeJson(targetTree[childProp.Name] as JObject, childProp.Value, mergeLogger);
                            }
                        }
                        else if (childProp.Value.Type == JTokenType.Array)
                        {
                            JArray sourceArray = childProp.Value as JArray;

                            JArray targetArray;
                            if (targetTree[childProp.Name] == null)
                            {
                                // Create a new array in the target if one doesn't exist
                                targetArray = new JArray();
                                targetTreeObject.Add(childProp.Name, targetArray);
                            }
                            else
                            {
                                targetArray = targetTree[childProp.Name] as JArray;
                            }

                            if (targetArray == null)
                            {
                                mergeLogger.Log("JArray with path " + targetTree.Path + " is messed up somehow", LogLevel.Wrn);
                            }
                            else
                            {
                                foreach (JToken arrayItem in sourceArray.Children())
                                {
                                    targetArray.Add(arrayItem);
                                }

                                //// Is it a primitive array?
                                //if (!ArrayContainsOnlyObjects(sourceArray) ||
                                //    !ArrayContainsOnlyObjects(targetArray))
                                //{
                                //    // In this case, concatenate
                                //    mergeLogger.Log("Concatenating arrays at " + sourceArray.Path + ", this may result in unexpected output...", LogLevel.Wrn);
                                //    foreach (JToken arrayItem in sourceArray.Children())
                                //    {
                                //        targetArray.Add(arrayItem);
                                //    }
                                //}
                                //else
                                //{
                                //    // Both arrays are of objects/nulls. Merge them by index
                                //    for (int arrayItemIdx = 0; arrayItemIdx < sourceArray.Count; arrayItemIdx++)
                                //    {
                                //        JToken sourceArrayItem = sourceArray[arrayItemIdx];
                                //        if (targetArray.Count <= arrayItemIdx)
                                //        {
                                //            // Append from source to target
                                //            targetArray.Add(sourceArrayItem);
                                //        }
                                //        else
                                //        {
                                //            JToken targetArrayItem = targetArray[arrayItemIdx];
                                //            if (targetArrayItem.Type == JTokenType.Null)
                                //            {
                                //                // Overwrite nulls from source to target
                                //                targetArray[arrayItemIdx] = sourceArrayItem;
                                //            }
                                //            else
                                //            {
                                //                // Merge objects from source to target
                                //                RecursiveMergeJson(targetArrayItem, sourceArrayItem, mergeLogger);
                                //            }
                                //        }
                                //    }
                                //}
                            }
                        }
                        else
                        {
                            // Copy value types straight over
                            if (targetTree[childProp.Name] == null)
                            {
                                if (targetTreeObject != null)
                                {
                                    //mergeLogger.Log("Copying " + child.Path, LogLevel.Vrb);
                                    targetTreeObject.Add(childProp.Name, childProp.Value);
                                }
                            }
                            else
                            {
                                // this happens when two objects have primitive values with identical jpaths.
                                // If they are perf metrics, take the largest value
                                //if (child.Path.StartsWith("Perf.Latency") &&
                                //    targetTree[childProp.Name] is JValue &&
                                //    (targetTree[childProp.Name].Type == JTokenType.Integer || targetTree[childProp.Name].Type == JTokenType.Float) &&
                                //    (childProp.Value.Type == JTokenType.Integer || childProp.Value.Type == JTokenType.Float))
                                //{
                                //    float existingLatency = targetTree[childProp.Name].Value<float>();
                                //    float newLatency = childProp.Value.Value<float>();
                                //    if (newLatency > existingLatency)
                                //    {
                                //        targetTreeObject.Remove(childProp.Name);
                                //        targetTreeObject.Add(childProp.Name, childProp.Value);
                                //    }
                                //}

                                mergeLogger.Log("Warning: Clashing values for " + child.Path, LogLevel.Wrn);
                            }
                        }
                    }
                }
            }
            else if (sourceTree.Type == JTokenType.Array)
            {
                JArray sourceArray = sourceTree as JArray;
                JArray targetArray = targetTree as JArray;

                // Concatenate the arrays together
                foreach (JToken arrayItem in sourceArray.Children())
                {
                    targetArray.Add(arrayItem);
                }

                // Is it a primitive array?
                //if (!ArrayContainsOnlyObjects(sourceArray) ||
                //    !ArrayContainsOnlyObjects(targetArray))
                //{
                //    // In this case, concatenate
                //    foreach (JToken arrayItem in sourceArray.Children())
                //    {
                //        targetArray.Add(arrayItem);
                //    }
                //}
                //else
                //{
                //    // Both arrays are of objects/nulls. Merge them by index
                //    for (int arrayItemIdx = 0; arrayItemIdx < sourceArray.Count; arrayItemIdx++)
                //    {
                //        JToken sourceArrayItem = sourceArray[arrayItemIdx];
                //        if (targetArray.Count <= arrayItemIdx)
                //        {
                //            // Append from source to target
                //            targetArray.Add(sourceArrayItem);
                //        }
                //        else
                //        {
                //            JToken targetArrayItem = targetArray[arrayItemIdx];
                //            if (targetArrayItem.Type == JTokenType.Null)
                //            {
                //                // Overwrite nulls from source to target
                //                targetArray[arrayItemIdx] = sourceArrayItem;
                //            }
                //            else
                //            {
                //                // Merge objects from source to target
                //                RecursiveMergeJson(targetArrayItem, sourceArrayItem, mergeLogger);
                //            }
                //        }
                //    }
                //}
            }
        }

        public static IDictionary<DataPrivacyClassification, JToken> SplitObjectByPrivacyClass(
            JToken input,
            DataPrivacyClassification defaultClassification,
            IReadOnlyDictionary<string, DataPrivacyClassification> classifications,
            ILogger traceLogger)
        {
            IDictionary<DataPrivacyClassification, JToken> returnVal = new SmallDictionary<DataPrivacyClassification, JToken>((classifications?.Count).GetValueOrDefault(1));
            if (classifications == null || classifications.Count == 0)
            {
                returnVal[defaultClassification] = input;
                return returnVal;
            }

            // Calculate the set of all potential classifications
            // OPT if we sort the incoming array by length we can reduce this n^2 substring operation by half, or more
            Dictionary<string, DataPrivacyClassification> jpathToClassMappings = new Dictionary<string, DataPrivacyClassification>();
            foreach (var kvp in classifications)
            {
                jpathToClassMappings.Add(kvp.Key, kvp.Value);
            }

            foreach (KeyValuePair<string, DataPrivacyClassification> classIter1 in classifications)
            {
                foreach (KeyValuePair<string, DataPrivacyClassification> classIter2 in classifications)
                {
                    if (classIter2.Key.StartsWith(classIter1.Key, StringComparison.Ordinal))
                    {
                        jpathToClassMappings[classIter2.Key] |= classIter1.Value;
                    }
                }
            }

            // Create a batch of output objects that can represent each potential output class
            Dictionary<DataPrivacyClassification, IntermediateJToken> intermediates = new Dictionary<DataPrivacyClassification, IntermediateJToken>();
            intermediates.Add(defaultClassification, new IntermediateJToken());

            foreach (DataPrivacyClassification privacyClass in jpathToClassMappings.Values)
            {
                if (!intermediates.ContainsKey(privacyClass))
                {
                    intermediates[privacyClass] = new IntermediateJToken();
                }
            }

            // Now start iterating the root object and sort each value into its appropriate class
            Deque<JPathSegment> segmentStack = new Deque<JPathSegment>();
            segmentStack.AddToBack(new JPathSegment(null, input.Type));
            SortPrivacyClassesRecursive(input, defaultClassification, jpathToClassMappings, intermediates, null, "$", ref segmentStack);

            foreach (KeyValuePair<DataPrivacyClassification, IntermediateJToken> output in intermediates)
            {
                if (output.Value.Touched)
                {
                    returnVal[output.Key] = output.Value.RootObj;
                }
            }

            //traceLogger.Log("Split JSON object into " + returnVal.Count + " distinct privacy classes", LogLevel.Vrb);
            return returnVal;
        }

        private class IntermediateJToken
        {
            public JToken RootObj = null;
            public bool Touched = false;

            public void AddValue(Deque<JPathSegment> pathStack, JToken value)
            {
                // Is the value null? Then ignore it
                if (pathStack[pathStack.Count - 1].TokenType == JTokenType.Null)
                {
                    return;
                }


                //using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                //{
                //    StringBuilder buf = pooledSb.Builder;
                //    foreach (JPathSegment segment in pathStack)
                //    {
                //        buf.Append(segment.ToString());
                //    }

                //    //traceLogger.Log("Sorting path " + buf.ToString() + " with value " + value.ToString());
                //}

                // Build up the structure from the root to the property being written
                JToken iter = null;
                foreach (JPathSegment pathSegment in pathStack)
                {
                    // Creating a new subobject...
                    if (pathSegment.TokenType == JTokenType.Object)
                    {
                        if (iter == null)
                        {
                            if (RootObj == null)
                            {
                                // ...as the new root
                                RootObj = new JObject();
                            }

                            iter = RootObj;
                        }
                        else
                        {
                            if (iter is JObject)
                            {
                                // ...as a child of an existing object
                                JObject currentObj = ((JObject)iter);
                                JObject newObj = currentObj[pathSegment.PropKey] as JObject;
                                if (newObj == null)
                                {
                                    newObj = new JObject();
                                    currentObj.Add(pathSegment.PropKey, newObj);
                                }

                                iter = newObj;
                            }
                            else if (iter is JArray)
                            {
                                // ...as an entry in an array
                                JArray currentArray = ((JArray)iter);
                                while (currentArray.Count <= pathSegment.PropIndex)
                                {
                                    currentArray.Add(new JObject());
                                }

                                iter = currentArray[pathSegment.PropIndex];
                            }
                        }
                    }
                    // Creating a new subarray...
                    else if (pathSegment.TokenType == JTokenType.Array)
                    {
                        if (iter == null)
                        {
                            // ...as the new root
                            if (RootObj == null)
                            {
                                RootObj = new JArray();
                            }

                            iter = RootObj;
                        }
                        else
                        {
                            if (iter is JObject)
                            {
                                // ...as a child of an existing object
                                JObject currentObj = ((JObject)iter);
                                JArray newArray = currentObj[pathSegment.PropKey] as JArray;
                                if (newArray == null)
                                {
                                    newArray = new JArray();
                                    currentObj.Add(pathSegment.PropKey, newArray);
                                }

                                iter = newArray;
                            }
                            else if (iter is JArray)
                            {
                                // ...as an entry in an existing array
                                JArray currentArray = ((JArray)iter);
                                iter = currentArray[pathSegment.PropIndex];
                            }
                        }
                    }
                    else
                    {
                        // Copy the actual value into this structure...
                        if (iter is JObject)
                        {
                            // ...as the property of an object
                            JObject currentObj = ((JObject)iter);
                            currentObj.Add(pathSegment.PropKey, value);
                        }
                        else if (iter is JArray)
                        {
                            // ...as an entry in an array
                            JArray currentArray = ((JArray)iter);
                            if (currentArray.Count != pathSegment.PropIndex - 1)
                            {
                                throw new JsonException("Cannot sparsely copy a single value type in the middle of a JArray");
                            }

                            currentArray.Add(value);
                        }
                    }
                }

                Touched = true;
            }
        }

        private class JPathSegment
        {
            public JTokenType TokenType;
            public string PropKey;
            public int PropIndex;

            public JPathSegment(string propName, JTokenType subTokenType)
            {
                TokenType = subTokenType;
                PropKey = propName;
            }

            public JPathSegment(int arrayIndex, JTokenType subTokenType)
            {
                TokenType = subTokenType;
                PropIndex = arrayIndex;
            }

            public override string ToString()
            {
                if (PropKey == null)
                {
                    return "[" + PropIndex + "](" + TokenType.ToString() + ")";
                }
                else
                {
                    return "." + PropKey + "(" + TokenType.ToString() + ")";
                }
            }
        }

        private static void SortPrivacyClassesRecursive(
            JToken iter,
            DataPrivacyClassification currentClass,
            Dictionary<string, DataPrivacyClassification> jpathToClassMappings,
            Dictionary<DataPrivacyClassification, IntermediateJToken> outputs,
            string previousPropName,
            string currentWildcardJPath,
            ref Deque<JPathSegment> pathStack)
        {
            // Did we enter a new tree segment?
            //traceLogger.Log("Processing jpath " + currentWildcardJPath);
            if (jpathToClassMappings.ContainsKey(currentWildcardJPath)) // FIXME use substrings for better accuracy?
            {
                currentClass = jpathToClassMappings[currentWildcardJPath];
                //traceLogger.Log("Switching to classification type " + currentClass.ToString());
            }

            if (iter.Type == JTokenType.Object)
            {
                JObject iterObject = iter as JObject;
                foreach (JProperty child in iterObject.Children<JProperty>())
                {
                    SortPrivacyClassesRecursive(child, currentClass, jpathToClassMappings, outputs, child.Name, currentWildcardJPath + "." + child.Name, ref pathStack);
                }
            }
            else if (iter.Type == JTokenType.Array)
            {
                JArray iterArray = iter as JArray;
                string subJPath = currentWildcardJPath + "[*]";
                int idx = 0;
                foreach (JToken child in iterArray.Children())
                {
                    pathStack.AddToBack(new JPathSegment(idx++, JTokenType.Object));
                    SortPrivacyClassesRecursive(child, currentClass, jpathToClassMappings, outputs, null, subJPath, ref pathStack);
                    pathStack.RemoveFromBack();
                }
            }
            else if (iter.Type == JTokenType.Property)
            {
                JProperty iterProperty = iter as JProperty;

                // Create sub objects on all of our intermediates
                if (iterProperty.Value.Type == JTokenType.Object)
                {
                    pathStack.AddToBack(new JPathSegment(iterProperty.Name, JTokenType.Object));
                    SortPrivacyClassesRecursive(iterProperty.Value, currentClass, jpathToClassMappings, outputs, iterProperty.Name, currentWildcardJPath, ref pathStack);
                    pathStack.RemoveFromBack();
                }
                else if (iterProperty.Value.Type == JTokenType.Array)
                {
                    pathStack.AddToBack(new JPathSegment(iterProperty.Name, JTokenType.Array));
                    SortPrivacyClassesRecursive(iterProperty.Value, currentClass, jpathToClassMappings, outputs, iterProperty.Name, currentWildcardJPath, ref pathStack);
                    pathStack.RemoveFromBack();
                }
                else // If it's a primitive value, we sort it right now
                {
                    IntermediateJToken output = outputs[currentClass];
                    pathStack.AddToBack(new JPathSegment(iterProperty.Name, iterProperty.Value.Type));
                    output.AddValue(pathStack, iterProperty.Value);
                    pathStack.RemoveFromBack();
                }
            }
        }
    }
}
