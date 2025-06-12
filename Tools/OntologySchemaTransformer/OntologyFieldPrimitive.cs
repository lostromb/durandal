using Newtonsoft.Json;
using OntologySchemaTransformer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer
{
    public class OntologyFieldPrimitive
    {
        /// <summary>
        /// The written type of this value
        /// </summary>
        public PrimitiveType Type { get; set; }

        public string ReferencedId { get; set; }
        
        public string Comment { get; set; }

        [JsonIgnore]
        public string InheritedFrom { get; set; }
        
        /// <summary>
        /// The actual implementation-level type of this value.
        /// Only meaningful for primitives that refer to entities that are actually primitives themselves, e.g. URL which inherits from String
        /// </summary>
        [JsonIgnore]
        public PrimitiveType BasePrimitiveType { get; set; }

        public OntologyFieldPrimitive(PrimitiveType type, string referencedId, string comment)
        {
            Type = type;
            ReferencedId = referencedId;
            BasePrimitiveType = type;
            Comment = comment;
        }

        //[JsonIgnore]
        //public string ShortReferencedId
        //{
        //    get
        //    {
        //        return ReferencedId.Substring(ReferencedId.LastIndexOf('.') + 1);
        //    }
        //}

        //public override string ToString()
        //{
        //    return ShortReferencedId;
        //}
    }
}
