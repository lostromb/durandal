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
    public class InMemoryDialogActionCache
    {
        private readonly IDictionary<string, CachedItem<DialogAction>> _items;

        public InMemoryDialogActionCache()
        {
            _items = new Dictionary<string, CachedItem<DialogAction>>();
        }

        public InMemoryDialogActionCache(IDictionary<string, CachedItem<DialogAction>> items)
        {
            _items = items;
        }

        public int Count => _items.Count;

        public string Store(DialogAction action, TimeSpan? lifeTime = null)
        {
            string key = Guid.NewGuid().ToString("N");
            CachedItem<DialogAction> cachedAction = new CachedItem<DialogAction>(key, action, lifeTime);
            _items[key] = cachedAction;
            return key;
        }

        public IEnumerable<CachedItem<DialogAction>> GetAllItems()
        {
            return _items.Values;
        }

        private class JsonConverter_Local : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(InMemoryDialogActionCache) == objectType;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }
                else if (reader.TokenType == JsonToken.StartObject)
                {
                    IDictionary<string, CachedItem<DialogAction>> values = new Dictionary<string, CachedItem<DialogAction>>();
                    reader.Read(); // skip start object
                    while (reader.TokenType == JsonToken.PropertyName)
                    {
                        string key = (string)reader.Value;
                        reader.Read();
                        CachedItem<DialogAction> value = serializer.Deserialize(reader, typeof(CachedItem<DialogAction>)) as CachedItem<DialogAction>;
                        values[key] = value;
                        reader.Read(); // go to next property or end of object
                    }
                    //reader.Read(); // skip end object

                    return new InMemoryDialogActionCache(values);
                }
                else
                {
                    throw new JsonException("Could not parse InMemoryDialogActionCache from json");
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
                    InMemoryDialogActionCache castObject = value as InMemoryDialogActionCache;
                    writer.WriteStartObject();
                    foreach (CachedItem<DialogAction> cachedItem in castObject.GetAllItems())
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
