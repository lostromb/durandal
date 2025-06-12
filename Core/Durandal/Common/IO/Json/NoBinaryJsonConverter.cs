using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.IO.Json
{
    /// <summary>
    /// A converter which suppresses the writing of large binary arrays, for things like instrumentation logs
    /// </summary>
    public class NoBinaryJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.Equals(typeof(byte[])) ||
                objectType.Equals(typeof(ArraySegment<byte>));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            writer.WriteEndArray();
        }
    }
}
