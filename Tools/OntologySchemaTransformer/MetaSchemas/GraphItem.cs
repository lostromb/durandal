using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer.MetaSchemas
{
    public class GraphItem
    {
        [JsonProperty("@id")]
        public string Id { get; set; }

        [JsonProperty("@type")]
        [JsonConverter(typeof(ConditionalListConverter<string>))]
        public List<string> Type { get; set; }

        [JsonProperty("http://purl.org/dc/terms/source")]
        [JsonConverter(typeof(ConditionalListConverter<SchemaItem>))]
        public List<SchemaItem> Source { get; set; }

        [JsonProperty("http://schema.org/domainIncludes")]
        [JsonConverter(typeof(ConditionalListConverter<SchemaItem>))]
        public List<SchemaItem> DomainIncludes { get; set; }

        [JsonProperty("http://schema.org/rangeIncludes")]
        [JsonConverter(typeof(ConditionalListConverter<SchemaItem>))]
        public List<SchemaItem> RangeIncludes { get; set; }

        [JsonProperty("rdfs:comment")]
        public string Comment { get; set; }

        [JsonProperty("rdfs:label")]
        public string Label { get; set; }

        [JsonProperty("rdfs:subClassOf")]
        [JsonConverter(typeof(ConditionalListConverter<SchemaItem>))]
        public List<SchemaItem> SubclassOf { get; set; }

        [JsonProperty("rdfs:subPropertyOf")]
        [JsonConverter(typeof(ConditionalListConverter<SchemaItem>))]
        public List<SchemaItem> SubpropertyOf { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
