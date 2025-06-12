using Newtonsoft.Json;
using OntologySchemaTransformer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer
{
    public class PrimitiveTypeJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PrimitiveType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string stringVal = reader.ReadAsString();
            PrimitiveType enumValue = (PrimitiveType)Enum.Parse(typeof(PrimitiveType), stringVal);
            return enumValue;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            string stringVal = Enum.GetName(typeof(PrimitiveType), value);
            writer.WriteValue(stringVal);
        }
    }
}
