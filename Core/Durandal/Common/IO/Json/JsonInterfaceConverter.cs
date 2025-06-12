using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.IO.Json
{
    /// <summary>
    /// Defines a JsonConverter which deserializes fields of an interface type into
    /// a default implementation of that interface (since Json wouldn't know how to deserialize otherwise)
    /// </summary>
    /// <typeparam name="Interface">The interface (field) type</typeparam>
    /// <typeparam name="Implementation">The implementation type that you want the field to be deserialized to</typeparam>
    public class InterfaceConverter<Interface, Implementation> : JsonConverter<Interface>
        where Implementation : Interface
    {
        public override void WriteJson(JsonWriter writer, Interface value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }

        public override Interface ReadJson(JsonReader reader, Type objectType, Interface existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize<Implementation>(reader);
        }
    }
}
