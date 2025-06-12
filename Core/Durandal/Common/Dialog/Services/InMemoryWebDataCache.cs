using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Cache;
using Durandal.Common.Time;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Dialog.Services
{
    [JsonConverter(typeof(JsonConverter_Local))]
    public class InMemoryWebDataCache
    {
        private readonly IDictionary<string, CachedItem<CachedWebData>> _items;

        public InMemoryWebDataCache()
        {
            _items = new Dictionary<string, CachedItem<CachedWebData>>();
        }

        public InMemoryWebDataCache(IDictionary<string, CachedItem<CachedWebData>> items)
        {
            _items = items;
        }

        public int Count => _items.Count;

        public string Store(CachedWebData item, TimeSpan? lifeTime = null)
        {
            string key = Guid.NewGuid().ToString("N");
            CachedItem<CachedWebData> cachedItem = new CachedItem<CachedWebData>(key, item, lifeTime);
            _items[key] = cachedItem;
            return key;
        }

        public IEnumerable<CachedItem<CachedWebData>> GetAllItems()
        {
            return _items.Values;
        }

        private class JsonConverter_Local : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(InMemoryWebDataCache) == objectType;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }
                else if (reader.TokenType == JsonToken.StartObject)
                {
                    IDictionary<string, CachedItem<CachedWebData>> values = new Dictionary<string, CachedItem<CachedWebData>>();
                    reader.Read(); // skip start object
                    while (reader.TokenType == JsonToken.PropertyName)
                    {
                        string key = (string)reader.Value;
                        reader.Read();
                        CachedItem<CachedWebData> value = serializer.Deserialize(reader, typeof(CachedItem<CachedWebData>)) as CachedItem<CachedWebData>;
                        values[key] = value;
                        reader.Read(); // go to next property or end of object
                    }
                    //reader.Read(); // skip end object

                    return new InMemoryWebDataCache(values);
                }
                else
                {
                    throw new JsonException("Could not parse InMemoryWebDataCache from json");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    InMemoryWebDataCache castObject = value as InMemoryWebDataCache;
                    writer.WriteStartObject();
                    foreach (CachedItem<CachedWebData> cachedItem in castObject.GetAllItems())
                    {
                        writer.WritePropertyName(cachedItem.Key);
                        serializer.Serialize(writer, cachedItem);
                    }

                    writer.WriteEndObject();
                }
            }
        }
    }
}
