using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Represents a collection of latency events for a particular category. For example,
    /// a single trace might include multiple writes to a cache. This collection
    /// will contain the latency of each one of those operations as well as aggregate statistics.
    /// </summary>
    public class UnifiedTraceLatencyCollection
    {
        [JsonProperty("Values")] // Property name _must_ match what is written in CommonInstrumentation.GenerateLatencyEntry
        public IList<UnifiedTraceLatencyEntry> Values { get; set; }

        public float? Average
        {
            get
            {
                float? sum = Sum;
                if (sum.HasValue)
                {
                    return (float)sum.Value / (float)Values.Count;
                }
                else
                {
                    return null;
                }
            }
        }

        public float? Sum
        {
            get
            {
                if (Values == null || Values.Count == 0)
                {
                    return null;
                }

                float returnVal = 0;
                foreach (var value in Values)
                {
                    returnVal += value.Value;
                }

                return returnVal;
            }
        }

        public UnifiedTraceLatencyCollection()
        {
            Values = new List<UnifiedTraceLatencyEntry>();
        }
    }

    /// <summary>
    /// A single instrumented "latency" event, potentially with a unique ID and a start time for timelining
    /// </summary>
    public class UnifiedTraceLatencyEntry
    {
        [JsonProperty("Value")] // Property name _must_ match what is written in CommonInstrumentation.GenerateLatencyEntry
        public float Value { get; set; }

        [JsonProperty("Id")] // Property name _must_ match what is written in CommonInstrumentation.GenerateLatencyEntry
        public string Id { get; set; }

        [JsonProperty("StartTime")] // Property name _must_ match what is written in CommonInstrumentation.GenerateLatencyEntry
        [JsonConverter(typeof(JsonTimeTicksConverter))]
        public DateTimeOffset? StartTime { get; set; }
    }
}
