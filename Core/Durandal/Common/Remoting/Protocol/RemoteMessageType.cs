using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    [JsonConverter(typeof(RemoteMessageTypeConverter))]
    public enum RemoteMessageType
    {
        Unknown = 0,
        Request = 1,
        Response = 2
    }

    public class RemoteMessageTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RemoteMessageType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return RemoteMessageType.Unknown;
            }
            else if (reader.TokenType == JsonToken.Integer)
            {
                Int64 cast = (Int64)reader.Value;
                return (RemoteMessageType)((int)cast);
            }
            else
            {
                throw new JsonSerializationException($"Invalid token found: {reader.Value} ({reader.TokenType}), expected Integer", reader.Path, 0, 0, null);
            }

            //int? val = reader.ReadAsInt32();
            //if (!val.HasValue)
            //{
            //    return RemoteMessageType.Unknown;
            //}
            //else
            //{
            //    return (RemoteMessageType)val.Value;
            //}
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            int castValue = (int)value;
            writer.WriteValue(castValue);
        }
    }
}
