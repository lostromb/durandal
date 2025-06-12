using Newtonsoft.Json;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.IO.Json
{
    /// <summary>
    /// Implements a writer for ArraySegment{byte} and byte[] which uses Base64 (much more compact) and is more resilient to null arrays
    /// </summary>
    public class JsonByteArrayConverter : JsonConverter
    {
        private static readonly Type segmentType = typeof(ArraySegment<byte>);
        private static readonly Type arrayType = typeof(byte[]);

        public override bool CanConvert(Type objectType)
        {
            return segmentType.Equals(objectType) ||
                arrayType.Equals(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                // It's an array of integer values, meaning it was serialized using the default JSON.Net behavior.
                // We should handle this anyways for backwards compatability.
                reader.Read();
                List<byte> byteList = new List<byte>();
                while (reader.TokenType == JsonToken.Integer)
                {
                    long val = (long)reader.Value;
                    byteList.Add((byte)val);
                    reader.Read();
                }

                //reader.Read(); // Advance input for the next deserializer to handle

                byte[] data = byteList.ToArray();
                if (arrayType.Equals(objectType))
                {
                    return data;
                }
                else if (segmentType.Equals(objectType))
                {
                    return new ArraySegment<byte>(data, 0, data.Length);
                }
                else
                {
                    throw new JsonSerializationException("Cannot deserialize JSON byte array to " + objectType.ToString());
                }
            }
            else if (reader.TokenType == JsonToken.String)
            {
                string nextString = reader.Value as string;
                byte[] data = Convert.FromBase64String(nextString);

                if (arrayType.Equals(objectType))
                {
                    return data;
                }
                else if (segmentType.Equals(objectType))
                {
                    return new ArraySegment<byte>(data, 0, data.Length);
                }
                else
                {
                    throw new JsonSerializationException("Cannot deserialize JSON byte array to " + objectType.ToString());
                }
            }
            else if (reader.TokenType == JsonToken.Null)
            {
                return existingValue;
            }
            else
            {
                throw new JsonSerializationException("Unexpected token " + reader.TokenType + "; expected a byte array or base64 string");
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is ArraySegment<byte>)
            {
                ArraySegment<byte> castValue = (ArraySegment<byte>)value;
                if (castValue == null || castValue.Array == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    //if (castValue.Array.Length == castValue.Count && castValue.Offset == 0)
                    //{
                    //    // we can pass through the entire array without needing to do intermediate conversion
                    //    writer.WriteValue(castValue.Array);
                    //}
                    //else
                    {
                        string base64 = Convert.ToBase64String(castValue.Array, castValue.Offset, castValue.Count);
                        writer.WriteValue(base64);
                    }
                }
            }
            else if (value is byte[])
            {
                byte[] castValue = (byte[])value;
                if (castValue == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    // Json.Net apparently has its own base64 encoder now, so we just use that?
                    writer.WriteValue(castValue);
                }
            }
        }
    }
}
