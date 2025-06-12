using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer.MetaSchemas
{
    public class ConditionalListConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                // It's an array
                List<T> listObj = serializer.Deserialize<List<T>>(reader);
                return listObj;
            }
            else
            {
                // It's a single object
                List<T> returnVal = new List<T>();
                T singleObj = serializer.Deserialize<T>(reader);
                returnVal.Add(singleObj);
                return returnVal;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Not needed
        }
    }
}
