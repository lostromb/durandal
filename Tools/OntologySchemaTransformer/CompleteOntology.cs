using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer
{
    public class CompleteOntology
    {
        public Dictionary<string, OntologyClass> Classes;
        public Dictionary<string, OntologyEnumeration> Enumerations;
    }
}
