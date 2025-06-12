using Durandal.Common.NLP.Language;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.IO.Json
{
    /// <summary>
    /// Intereprets a JSON property to/from a locale string e.g. "en-US", "pt-br" and a structured <see cref="LanguageCode"/> object.
    /// When formatting language codes as a string, this will prefer the BCP 47 alpha-2 format where available, for example "en-GB", "de-DE".
    /// However, some locales can only be represented in alpha-3 format (for example Filipino "fil", or meta-locales such as "und" or "mul").
    /// If that is the case, the alpha-3 format will be used.
    /// </summary>
    public class JsonLanguageCodeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(LanguageCode);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value != null)
            {
                if (reader.Value is string)
                {
                    string stringVal = (string)reader.Value;
                    return LanguageCode.Parse(stringVal);
                }
                else
                {
                    throw new JsonException("Unexpected value " + reader.Value + " for JSON field " + reader.Path);
                }
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteValue(((LanguageCode)value).ToString());
            }
        }
    }
}
