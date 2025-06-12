using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer.MetaSchemas
{
    public class SchemaItem
    {
        [JsonProperty("@id")]
        public string Id { get; set; }
    }
}
