using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.IO.Json
{
    public class JsonTimeTicksConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTime) ||
                objectType == typeof(DateTimeOffset) ||
                objectType == typeof(DateTime?) ||
                objectType == typeof(DateTimeOffset?);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            bool isDateTimeOffset = objectType == typeof(DateTimeOffset) ||
                objectType == typeof(DateTimeOffset?);
            bool isNullable = objectType == typeof(DateTime?) ||
                objectType == typeof(DateTimeOffset?);

            long? ticks = null;
            if (reader.Value != null)
            {
                if (reader.Value is long)
                {
                    ticks = (long)reader.Value;
                }
                else if (reader.Value is int)
                {
                    ticks = (int)reader.Value;
                }
                else
                {
                    throw new JsonException("Unexpected value " + reader.Value + " for JSON field " + reader.Path);
                }
            }

            if (!ticks.HasValue)
            {
                if (isNullable)
                {
                    return null;
                }

                throw new ArgumentNullException("Expected a value for non-nullable JSON field: " + reader.Path);
            }

            DateTimeOffset parsedTime = new DateTimeOffset(ticks.Value, TimeSpan.Zero);
            if (isDateTimeOffset)
            {
                if (isNullable)
                {
                    return new DateTimeOffset?(parsedTime);
                }
                else
                {
                    return parsedTime;
                }
            }
            else
            {
                if (isNullable)
                {
                    return new DateTime?(parsedTime.UtcDateTime);
                }
                else
                {
                    return parsedTime.UtcDateTime;
                }
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            long? ticks = null;

            if (value == null)
            {
            }
            else if (value is DateTimeOffset)
            {
                ticks = ((DateTimeOffset)value).Ticks;
            }
            else if (value is DateTime)
            {
                ticks = ((DateTime)value).ToUniversalTime().Ticks;
            }
            else if (value is DateTimeOffset? && ((DateTimeOffset?)value).HasValue)
            {
                ticks = ((DateTimeOffset?)value).Value.Ticks;
            }
            else if (value is DateTime? && ((DateTime?)value).HasValue)
            {
                ticks = ((DateTime?)value).Value.ToUniversalTime().Ticks;
            }

            if (ticks.HasValue)
            {
                writer.WriteRawValue(ticks.Value.ToString());
            }
            else
            {
                writer.WriteNull();
            }
        }
    }
}
