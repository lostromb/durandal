using System.Collections.Generic;
using Newtonsoft.Json;

namespace Durandal.API
{
    /// <summary>
    /// Represents a full timex resolver value, containing the inferred match results as well
    /// as the original timex dictionary (for reinterpretation).
    /// </summary>
    public class TimexEntity
    {
        public int Index { get; set; }
        public int Id { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Modifier { get; set; }
        public string Frequency { get; set; }
        public string Quantity { get; set; }
        public string Comment { get; set; }
        public IDictionary<string, string> TimexDictionary { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
        }

        public static TimexEntity ParseFromJson(string json)
        {
            return JsonConvert.DeserializeObject<TimexEntity>(json);
        }
    }
}
