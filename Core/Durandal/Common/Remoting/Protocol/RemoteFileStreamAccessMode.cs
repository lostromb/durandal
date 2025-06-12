using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    [JsonConverter(typeof(RemoteFileStreamAccessModeConverter))]
    public enum RemoteFileStreamAccessMode
    {
        Unknown = 0,
        Read = 0x1,
        Write = 0x2,
        ReadWrite = Read | Write
    }

    public class RemoteFileStreamAccessModeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RemoteFileStreamAccessMode);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return RemoteFileStreamAccessMode.Unknown;
            }
            else if (reader.TokenType == JsonToken.Integer)
            {
                Int64 cast = (Int64)reader.Value;
                return (RemoteFileStreamAccessMode)((int)cast);
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
