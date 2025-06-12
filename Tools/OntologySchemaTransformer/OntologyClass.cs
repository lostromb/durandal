using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer
{
    public class OntologyClass
    {
        public string Id;
        public string Comment;
        public string Label;
        public HashSet<string> InheritsFrom;
        public Dictionary<string, OntologyField> Fields;

        public override string ToString()
        {
            return Id + ": " + Label;
        }
    }
}
