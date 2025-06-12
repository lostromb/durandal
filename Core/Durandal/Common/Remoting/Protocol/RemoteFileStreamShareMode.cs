using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    [JsonConverter(typeof(RemoteFileStreamShareModeConverter))]
    public enum RemoteFileStreamShareMode
    {
        None = 0x0,
        Read = 0x1,
        Write = 0x2,
        ReadWrite = Read | Write,
        Delete = 0x4,
    }

    public class RemoteFileStreamShareModeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RemoteFileStreamShareMode);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return RemoteFileStreamShareMode.None;
            }
            else if (reader.TokenType == JsonToken.Integer)
            {
                Int64 cast = (Int64)reader.Value;
                return (RemoteFileStreamShareMode)((int)cast);
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
