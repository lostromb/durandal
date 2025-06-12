using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer
{
    public class OntologyEnumeration
    {
        public string Id;
        public string Comment;
        public string Label;
        public string Type;

        public override string ToString()
        {
            return Id + ": " + Label;
        }
    }
}
