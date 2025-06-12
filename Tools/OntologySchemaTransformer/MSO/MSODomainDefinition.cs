using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer.MSO
{
    public class MSODomainDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string DomainGroup { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
