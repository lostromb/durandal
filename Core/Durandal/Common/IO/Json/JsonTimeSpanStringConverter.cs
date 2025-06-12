using Durandal.Common.Time;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Durandal.Common.IO.Json
{
    /// <summary>
    /// Serializes TimeSpan objects to JSON strings in any abbreviated form of "hh:mm:ss.fffff".
    /// This means that integers get interpreted as seconds.
    /// </summary>
    public class JsonTimeSpanStringConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TimeSpan) ||
                objectType == typeof(TimeSpan?);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            bool isNullable = objectType == typeof(TimeSpan?);

            TimeSpan? parsedVal = null;
            if (reader.Value != null)
            {
                // Be lenient in what values we accept; parse string or numerical values here
                if (reader.Value is string)
                {
                    parsedVal = TimeSpanExtensions.ParseTimeSpan((string)reader.Value);
                }
                else if (reader.Value is long)
                {
                    parsedVal = new TimeSpan((long)reader.Value);
                }
                else if (reader.Value is int)
                {
                    parsedVal = new TimeSpan((int)reader.Value);
                }
                else
                {
                    throw new JsonException("Unexpected value " + reader.Value + " for JSON field " + reader.Path);
                }
            }

            if (!parsedVal.HasValue)
            {
                if (isNullable)
                {
                    return null;
                }

                throw new ArgumentNullException("Expected a value for non-nullable JSON field: " + reader.Path);
            }

            if (isNullable)
            {
                return parsedVal;
            }
            else
            {
                return parsedVal.Value;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            string stringVal = null;

            if (value == null)
            {
            }
            else if (value is TimeSpan)
            {
                stringVal = ((TimeSpan)value).PrintTimeSpan();
            }
            else if (value is TimeSpan? && ((TimeSpan?)value).HasValue)
            {
                stringVal = (((TimeSpan?)value).Value).PrintTimeSpan();
            }

            if (stringVal != null)
            {
                writer.WriteValue(stringVal);
            }
            else
            {
                writer.WriteNull();
            }
        }
    }
}
