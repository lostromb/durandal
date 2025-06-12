using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    [JsonConverter(typeof(RemoteFileStreamSeekOriginConverter))]
    public enum RemoteFileStreamSeekOrigin
    {
        Unknown = 0,
        End = 1,
        Current = 2,
        Begin = 3,
    }

    public class RemoteFileStreamSeekOriginConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RemoteFileStreamSeekOrigin);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return RemoteFileStreamSeekOrigin.Unknown;
            }
            else if (reader.TokenType == JsonToken.Integer)
            {
                Int64 cast = (Int64)reader.Value;
                return (RemoteFileStreamSeekOrigin)((int)cast);
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
