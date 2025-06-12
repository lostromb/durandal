using Newtonsoft.Json;
using OntologySchemaTransformer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer
{
    public class OntologyField
    {
        public string Name;
        public List<OntologyFieldPrimitive> Values;

        public OntologyField(string name)
        {
            Name = name;
            Values = new List<OntologyFieldPrimitive>();
        }

        public void AddPrimitive(PrimitiveType type, string referencedId, string comment)
        {
            Values.Add(new OntologyFieldPrimitive(type, referencedId, comment));
        }

        public override string ToString()
        {
            if (Values.Count == 0)
            {
                return Name;
            }
            else if (Values.Count == 1)
            {
                return Name + ": " + Values[0].ReferencedId;
            }
            else
            {
                string[] union = new string[Values.Count];
                for (int c = 0; c < union.Length; c++)
                {
                    union[c] = Values[c].ReferencedId;
                }

                return Name + ": { " + string.Join(", ", union) + " }";
            }
        }
    }
}
