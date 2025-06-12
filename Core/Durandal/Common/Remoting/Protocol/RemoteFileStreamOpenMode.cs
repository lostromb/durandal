using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    [JsonConverter(typeof(RemoteFileStreamOpenModeConverter))]
    public enum RemoteFileStreamOpenMode
    {
        Unknown = 0,
        CreateNew = 1,
        Create = 2,
        Open = 3,
        OpenOrCreate = 4,
    }

    public class RemoteFileStreamOpenModeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RemoteFileStreamOpenMode);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return RemoteFileStreamOpenMode.Unknown;
            }
            else if (reader.TokenType == JsonToken.Integer)
            {
                Int64 cast = (Int64)reader.Value;
                return (RemoteFileStreamOpenMode)((int)cast);
            }
            else
            {
                throw new JsonSerializationException($"Invalid token found: {reader.Value} ({reader.TokenType}), expected Integer", reader.Path, 0, 0, null);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            int castValue = (int)value;
            writer.WriteValue(castValue);
        }
    }
}
