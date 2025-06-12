using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.IO.Json
{
    /// <summary>
    /// Converter class which creates a case-insensitive dictionary (OrdinalIgnoreCase) for fields that are annotated with the converter.
    /// Usage: [JsonConverter(typeof(JsonCaseInsensitiveDictionaryConverter&gt;DICT_VALUE_TYPE&lt;))].
    /// </summary>
    /// <typeparam name="T">The type of the values being stored in the dictionary. The key type is always String.</typeparam>
    public class JsonCaseInsensitiveDictionaryConverter<T> : JsonConverter
    {
        private readonly JsonConverter _innerDictionaryConverter = null;

        public JsonCaseInsensitiveDictionaryConverter()
        {
            Type tType = typeof(T);

            if (tType.GenericTypeArguments.Length == 2 &&
                tType.GenericTypeArguments[0] == typeof(string))
            {
                // Make sure the inner type is actually a dictionary
                Type genericTType = tType.GetGenericTypeDefinition();
                if (genericTType == typeof(Dictionary<,>) ||
                    genericTType == typeof(IDictionary<,>) ||
                    genericTType == typeof(IReadOnlyDictionary<,>))
                {
                    // The inner type is itself a dictionary, so create a case-insensitive converter for that dictionary as well
                    Type innerConverterType = typeof(JsonCaseInsensitiveDictionaryConverter<>).MakeGenericType(tType.GenericTypeArguments[1]);
                    _innerDictionaryConverter = Activator.CreateInstance(innerConverterType) as JsonConverter;
                }
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Dictionary<string, T>) ||
                objectType == typeof(IDictionary<string, T>) ||
                objectType == typeof(IReadOnlyDictionary<string, T>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            // Read the start '{' of the dictionary
            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonException("Expected JsonToken.StartObject. Path: " + reader.Path);
            }

            // Read the first token. Could be either a property name or the end '}' of an empty dictionary
            if (!reader.Read() || (reader.TokenType != JsonToken.PropertyName && reader.TokenType != JsonToken.EndObject))
            {
                throw new JsonException("Expected JsonToken.PropertyName or JsonToken.EndObject. Path: " + reader.Path);
            }

            Dictionary<string, T> returnVal = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

            // Read the list of dictionary items
            while (reader.TokenType == JsonToken.PropertyName)
            {
                string propertyName = reader.Value as string;
                if (!reader.Read())
                {
                    throw new JsonException("Unexpected end-of-stream while parsing JSON. Path: " + reader.Path);
                }

                T propertyValue;
                if (_innerDictionaryConverter != null)
                {
                    // Read inner dictionary if applicable
                    propertyValue = (T)_innerDictionaryConverter.ReadJson(reader, typeof(T), null, serializer);
                }
                else
                {
                    propertyValue = serializer.Deserialize<T>(reader);
                }

                if (returnVal.ContainsKey(propertyName))
                {
                    // One small difference introduced by this converter is the fact that the same key with different casings is no longer allowed,
                    // where it previously might have been accepted. This will now throw an exception on key collision
                    throw new JsonException("Case-insensitive dictionary found multiple entries with the key \"" + propertyName + "\". Path: " + reader.Path);
                }

                returnVal.Add(propertyName, propertyValue);

                if (!reader.Read())
                {
                    throw new JsonException("Unexpected end-of-stream while parsing JSON. Path: " + reader.Path);
                }
            }

            // Dictionary<string, T> implements all of the superclasses that this converter supports, so no casting required here
            return returnVal;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            IEnumerable<KeyValuePair<string, T>> valueEnumerator = value as IEnumerable<KeyValuePair<string, T>>;
            if (valueEnumerator == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteStartObject();
                foreach (KeyValuePair<string, T> dictItem in valueEnumerator)
                {
                    writer.WritePropertyName(dictItem.Key);

                    // Write inner dictionary if applicable
                    if (_innerDictionaryConverter != null)
                    {
                        _innerDictionaryConverter.WriteJson(writer, dictItem.Value, serializer);
                    }
                    else
                    {
                        serializer.Serialize(writer, dictItem.Value);
                    }
                }

                writer.WriteEndObject();
            }
        }
    }
}
