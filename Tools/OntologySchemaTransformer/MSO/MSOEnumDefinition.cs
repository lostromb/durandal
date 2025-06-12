using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer.MSO
{
    public class MSOEnumDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string EnumType { get; set; }
        public string Description { get; set; }
        public bool IsDeprecated { get; set; }

        public override string ToString()
        {
            return Name + " : " + EnumType + " : " + Description;
        }
    }
}
