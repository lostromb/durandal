using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.IO.Json
{
    /// <summary>
    /// Intereprets a JSON property from a 64-bit Unix epoch time to a structured UTC DateTime or DateTimeOffset object
    /// </summary>
    public class JsonEpochTimeConverter : JsonConverter
    {
        private DateTimeOffset EPOCH_ORIGIN = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

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

            long? epochSeconds = null;
            if (reader.Value != null)
            {
                if (reader.Value is long)
                {
                    epochSeconds = (long)reader.Value;
                }
                else if (reader.Value is int)
                {
                    epochSeconds = (int)reader.Value;
                }
                else
                {
                    throw new JsonException("Unexpected value " + reader.Value + " for JSON field " + reader.Path);
                }
            }

            if (!epochSeconds.HasValue)
            {
                if (isNullable)
                {
                    return null;
                }

                throw new ArgumentNullException("Expected a value for non-nullable JSON field: " + reader.Path);
            }
            
            DateTimeOffset parsedTime = EPOCH_ORIGIN.AddSeconds(epochSeconds.Value);
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
            long? epoch = null;
            
            if (value == null)
            {
            }
            else if (value is DateTimeOffset)
            {
                epoch = (((DateTimeOffset)value) - EPOCH_ORIGIN).Ticks / TimeSpan.TicksPerSecond;
            }
            else if (value is DateTime)
            {
                epoch = (((DateTime)value).ToUniversalTime() - EPOCH_ORIGIN.UtcDateTime).Ticks / TimeSpan.TicksPerSecond;
            }
            else if (value is DateTimeOffset? && ((DateTimeOffset?)value).HasValue)
            {
                epoch = (((DateTimeOffset?)value).Value - EPOCH_ORIGIN).Ticks / TimeSpan.TicksPerSecond;
            }
            else if (value is DateTime? && ((DateTime?)value).HasValue)
            {
                epoch = (((DateTime?)value).Value.ToUniversalTime() - EPOCH_ORIGIN.UtcDateTime).Ticks / TimeSpan.TicksPerSecond;
            }

            if (epoch.HasValue)
            {
                writer.WriteRawValue(epoch.Value.ToString());
            }
            else
            {
                writer.WriteNull();
            }
        }
    }
}
