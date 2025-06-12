using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.MathExt;
using Newtonsoft.Json;
using Durandal.Common.IO.Json;

namespace Durandal.Common.Instrumentation
{
    public class UnifiedTrace
    {
        public Guid TraceId { get; set; }
        public DateTimeOffset TraceStart { get; set; }
        public DateTimeOffset TraceEnd { get; set; }
        public float TraceDuration { get; set; }
        public int LogCount { get; set; }
        public int ErrorLogCount { get; set; }
        public string InputString { get; set; }
        public string ResponseText { get; set; }
        public InputMethod InteractionType { get; set; }
        public string TriggeredDomain { get; set; }
        public string TriggeredIntent { get; set; }
        public string UserId { get; set; }
        public string ClientId { get; set; }
        public string ErrorMessage { get; set; }
        public float? LUConfidence { get; set; }
        public QueryFlags QueryFlags { get; set; }
        public IList<LogEvent> LogEvents { get; private set; }
        public JObject InstrumentationObject { get; set; }
        public IDictionary<string, UnifiedTraceLatencyCollection> Latencies { get; private set; }
        public IDictionary<string, UnifiedTraceSizeCollection> Sizes { get; private set; }
        public string ClientType { get; set; }
        public FormFactor FormFactor { get; set; }
        public string ClientVersion { get; set; }
        public DialogEventType DialogEventType { get; set; }
        public string DialogHost { get; set; }
        public string LUHost { get; set; }
        public string DialogProtocol { get; set; }
        public string LUProtocol { get; set; }

        public UnifiedTrace()
        {
            LogEvents = new List<LogEvent>();
            Latencies = new Dictionary<string, UnifiedTraceLatencyCollection>();
            Sizes = new Dictionary<string, UnifiedTraceSizeCollection>();
            TraceStart = default(DateTime);
            TraceEnd = default(DateTime);
        }

        /// <summary>
        /// Returns null if there is no trace data to get
        /// </summary>
        /// <param name="traceId"></param>
        /// <param name="logEvents"></param>
        /// <param name="logger"></param>
        /// <param name="piiDecrypter"></param>
        /// <returns></returns>
        public static UnifiedTrace CreateFromLogData(Guid traceId, IEnumerable<LogEvent> logEvents, ILogger logger, IStringDecrypterPii piiDecrypter)
        {
            UnifiedTrace returnVal = new UnifiedTrace()
            {
                TraceId = traceId,
            };

            IList<string> instrumentationStrings = new List<string>();

            string firstErrorMessage = null;

            int unableToDecryptLogMessages = 0;
            foreach (LogEvent e in logEvents)
            {
                if (!e.TraceId.Equals(traceId))
                {
                    continue;
                }

                returnVal.LogEvents.Add(e);
                returnVal.LogCount++;

                // Attempt to decrypt each message
                if (CommonInstrumentation.IsEncrypted(e.Message))
                {
                    string plainText;
                    // If it is enrypted, see if we can decrypt it
                    if (piiDecrypter.TryDecryptString(e.Message, out plainText))
                    {
                        e.Message = plainText;
                    }
                    else
                    {
                        unableToDecryptLogMessages++;
                    }
                }

                if (e.Level == LogLevel.Ins &&
                    !CommonInstrumentation.IsEncrypted(e.Message))
                {
                    // If event is not encrypted, copy it to the instrumentation event list
                    instrumentationStrings.Add(e.Message);
                }

                if (e.Level == LogLevel.Err)
                {
                    returnVal.ErrorLogCount++;
                    if (firstErrorMessage == null)
                    {
                        firstErrorMessage = e.Message;
                    }
                }

                if (e.Timestamp.Year > 2000)
                {
                    if (returnVal.TraceStart.Year < 2000 || e.Timestamp < returnVal.TraceStart)
                    {
                        returnVal.TraceStart = e.Timestamp;
                    }
                    if (returnVal.TraceEnd.Year < 2000 || e.Timestamp > returnVal.TraceEnd)
                    {
                        returnVal.TraceEnd = e.Timestamp;
                    }
                }
            }

            if (unableToDecryptLogMessages > 0)
            {
                logger.Log(unableToDecryptLogMessages + " log message(s) in the trace remain encrypted", LogLevel.Vrb);
            }

            // Nothing to do
            if (instrumentationStrings.Count == 0 && returnVal.LogCount == 0)
            {
                return null;
            }

            // Now compile statistics
            JObject instrumentationObject = CommonInstrumentation.MergeImpressions(instrumentationStrings, logger);

            returnVal.InstrumentationObject = instrumentationObject;

            returnVal.TraceDuration = (float)(returnVal.TraceEnd - returnVal.TraceStart).TotalSeconds;
            returnVal.ClientId = TryExtractJPathStringValue(instrumentationObject, "Dialog.ClientRequest.ClientContext.ClientId");
            returnVal.UserId = TryExtractJPathStringValue(instrumentationObject, "Dialog.ClientRequest.ClientContext.UserId");
            returnVal.InputString = TryExtractJPathStringValue(instrumentationObject, "Dialog.DialogProcessorResponse.SelectedRecoResult.Utterance.OriginalText");
            returnVal.TriggeredDomain = TryExtractJPathStringValue(instrumentationObject, "Dialog.DialogProcessorResponse.SelectedRecoResult.Domain");
            returnVal.TriggeredIntent = TryExtractJPathStringValue(instrumentationObject, "Dialog.DialogProcessorResponse.SelectedRecoResult.Intent");
            returnVal.ResponseText = TryExtractJPathStringValue(instrumentationObject, "Dialog.DialogProcessorResponse.DisplayedText");
            returnVal.ClientType = TryExtractJPathStringValue(instrumentationObject, "Dialog.ClientRequest.ClientContext.ExtraClientContext.ClientType");
            returnVal.ClientVersion = TryExtractJPathStringValue(instrumentationObject, "Dialog.ClientRequest.ClientContext.ExtraClientContext.ClientVersion");
            returnVal.DialogHost = TryExtractJPathStringValue(instrumentationObject, "Dialog.Host");
            returnVal.LUHost = TryExtractJPathStringValue(instrumentationObject, "LU.Host");
            returnVal.DialogProtocol = TryExtractJPathStringValue(instrumentationObject, "Dialog.Protocol");
            returnVal.LUProtocol = TryExtractJPathStringValue(instrumentationObject, "LU.Protocol");

            string eventType = TryExtractJPathStringValue(instrumentationObject, "DialogEventType");
            if (string.IsNullOrEmpty(eventType))
            {
                returnVal.DialogEventType = DialogEventType.Unknown;
            }
            if (string.Equals(eventType, "Query"))
            {
                returnVal.DialogEventType = DialogEventType.Query;
            }
            else if (string.Equals(eventType, "DialogAction"))
            {
                returnVal.DialogEventType = DialogEventType.DialogAction;
            }
            else if (string.Equals(eventType, "DirectAction"))
            {
                returnVal.DialogEventType = DialogEventType.DirectAction;
            }

            string formFactorString = TryExtractJPathStringValue(instrumentationObject, "Dialog.ClientRequest.ClientContext.ExtraClientContext.FormFactor");
            FormFactor formFactor;
            if (Enum.TryParse(formFactorString, out formFactor))
            {
                returnVal.FormFactor = formFactor;
            }

            int? rawInputType = TryExtractJPathIntValue(instrumentationObject, "Dialog.ClientRequest.InteractionType");
            if (rawInputType.HasValue)
            {
                returnVal.InteractionType = (InputMethod)rawInputType.Value;
            }

            int? rawQueryFlags = TryExtractJPathIntValue(instrumentationObject, "Dialog.ClientRequest.QueryFlags");
            if (rawQueryFlags.HasValue)
            {
                returnVal.QueryFlags = (QueryFlags)rawQueryFlags.Value;
            }

            returnVal.ErrorMessage = TryExtractJPathStringValue(instrumentationObject, "Dialog.DialogProcessorResponse.ErrorMessage");
            returnVal.LUConfidence = TryExtractJPathFloatValue(instrumentationObject, "Dialog.DialogProcessorResponse.SelectedRecoResult.Confidence");

            // If no error message, use the first error log message as error message
            if (string.IsNullOrEmpty(returnVal.ErrorMessage) && !string.IsNullOrEmpty(firstErrorMessage))
            {
                returnVal.ErrorMessage = firstErrorMessage;
            }

            JToken latencyNode = instrumentationObject.SelectToken("Perf.Latency");
            if (latencyNode != null)
            {
                foreach (JProperty entry in latencyNode.Children<JProperty>())
                {
                    if (entry.Value.Type == JTokenType.Object)
                    {
                        returnVal.Latencies[entry.Name] = entry.Value.ToObject<UnifiedTraceLatencyCollection>();
                    }
                    else
                    {
                        returnVal.Latencies[entry.Name] = new UnifiedTraceLatencyCollection();
                        returnVal.Latencies[entry.Name].Values.Add(new UnifiedTraceLatencyEntry()
                        {
                            Value = entry.Value.Value<float>()
                        });
                    }
                }
            }
            
            JToken sizeNode = instrumentationObject.SelectToken("Perf.Size");
            if (sizeNode != null)
            {
                foreach (JProperty entry in sizeNode.Children<JProperty>())
                {
                    if (entry.Value.Type == JTokenType.Object)
                    {
                        returnVal.Sizes[entry.Name] = entry.Value.ToObject<UnifiedTraceSizeCollection>();
                    }
                    else
                    {
                        returnVal.Sizes[entry.Name] = new UnifiedTraceSizeCollection();
                        returnVal.Sizes[entry.Name].Values.Add(new UnifiedTraceSizeEntry()
                        {
                            Value = entry.Value.Value<long>()
                        });
                    }
                }
            }

            return returnVal;
        }

        private static string TryExtractJPathStringValue(JToken obj, string path)
        {
            JToken response = obj.SelectToken(path);
            if (response == null)
                return null;
            string returnVal = response.Value<string>();
            return returnVal.Replace("\t", "\\t");
        }

        private static float? TryExtractJPathFloatValue(JToken obj, string path)
        {
            JToken response = obj.SelectToken(path);
            if (response == null)
                return null;
            float returnVal = response.Value<float>();
            return returnVal;
        }

        private static int? TryExtractJPathIntValue(JToken obj, string path)
        {
            JToken response = obj.SelectToken(path);
            if (response == null)
                return null;
            int returnVal = response.Value<int>();
            return returnVal;
        }

        private static void WriteSizeValueIfPresent(JToken obj, string path, string valueName, IDictionary<string, int> target)
        {
            JToken response = obj.SelectToken(path);
            if (response == null)
                return;

            int returnVal = response.Value<int>();
            // potential exception here but we want it to fail loudly
            target.Add(valueName, returnVal);
        }
    }
}
