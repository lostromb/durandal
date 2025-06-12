using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer.MetaSchemas
{
    public class SchemaContext
    {
        [JsonProperty("rdf")]
        public string RDF { get; set; }

        [JsonProperty("rdfs")]
        public string RDFS { get; set; }

        [JsonProperty("xsd")]
        public string XSD { get; set; }
    }
}
