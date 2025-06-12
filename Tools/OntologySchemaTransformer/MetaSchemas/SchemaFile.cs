using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer.MetaSchemas
{
    public class SchemaFile
    {
        [JsonProperty("@context")]
        public SchemaContext Context { get; set; }

        [JsonProperty("@graph")]
        public List<GraphItem> Graph { get; set; }
    }
}
