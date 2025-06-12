using Durandal.Common.Utils;
using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class SynthesizedWord
    {
        public string Word { get; set; }

        [JsonConverter(typeof(JsonTimeSpanTicksConverter))]
        public TimeSpan Offset { get; set; }

        [JsonConverter(typeof(JsonTimeSpanTicksConverter))]
        public TimeSpan ApproximateLength { get; set; }

        public override string ToString()
        {
            return string.Format("\"{0}\"       START {1} LEN {2}", Word, Offset, ApproximateLength.TotalMilliseconds);
        }
    }
}
