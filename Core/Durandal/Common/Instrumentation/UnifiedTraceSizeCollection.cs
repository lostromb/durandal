using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Represents a collection of size events for a particular category. For example,
    /// a single trace might include multiple writes to a cache. This collection
    /// will contain the size of each one of those operations as well as aggregate statistics.
    /// </summary>
    public class UnifiedTraceSizeCollection
    {
        [JsonProperty("Values")] // Property name _must_ match what is written in CommonInstrumentation.GenerateSizeEntry
        public IList<UnifiedTraceSizeEntry> Values { get; set; }

        public double? Average
        {
            get
            {
                long? sum = Sum;
                if (sum.HasValue)
                {
                    return (double)sum.Value / (double)Values.Count;
                }
                else
                {
                    return null;
                }
            }
        }

        public long? Sum
        {
            get
            {
                if (Values == null || Values.Count == 0)
                {
                    return null;
                }

                long returnVal = 0;
                foreach (var value in Values)
                {
                    returnVal += value.Value;
                }

                return returnVal;
            }
        }

        public UnifiedTraceSizeCollection()
        {
            Values = new List<UnifiedTraceSizeEntry>();
        }
    }

    /// <summary>
    /// A single instrumented "size" event, potentially with a unique ID
    /// </summary>
    public class UnifiedTraceSizeEntry
    {
        [JsonProperty("Value")] // Property name _must_ match what is written in CommonInstrumentation.GenerateSizeEntry
        public long Value { get; set; }

        [JsonProperty("Id")] // Property name _must_ match what is written in CommonInstrumentation.GenerateSizeEntry
        public string Id { get; set; }
    }
}
