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
    /// Parses numerical Json values like "start": 1.5500000 into TimeSpan structs.
    /// </summary>
    internal class VoskTimeSpanJsonParser : JsonConverter
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
                if (reader.Value is float)
                {
                    parsedVal = TimeSpan.FromSeconds((float)reader.Value);
                }
                else if (reader.Value is double)
                {
                    parsedVal = TimeSpan.FromSeconds((double)reader.Value);
                }
                else if (reader.Value is decimal)
                {
                    parsedVal = TimeSpan.FromSeconds((double)((decimal)reader.Value));
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
            throw new NotImplementedException();
        }
    }
}
