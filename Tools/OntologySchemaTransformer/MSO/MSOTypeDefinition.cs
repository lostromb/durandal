using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer.MSO
{
    public class MSOTypeDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Domains { get; set; }
        public List<string> Includes { get; set; }
        public bool IsDeprecated { get; set; }
        public string Category { get; set; }

        public override string ToString()
        {
            return Name + " : " + Description;
        }
    }
}
