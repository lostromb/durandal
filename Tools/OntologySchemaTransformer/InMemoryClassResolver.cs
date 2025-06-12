using System;
using System.Collections.Generic;
using System.Text;

namespace OntologySchemaTransformer
{
    public class InMemoryClassResolver : IClassResolver
    {
        public InMemoryClassResolver()
        {
            KnownClasses = new Dictionary<string, OntologyClass>();
        }

        public IDictionary<string, OntologyClass> KnownClasses
        {
            get;
        }

        public void Add(OntologyClass toAdd)
        {
            if (!KnownClasses.ContainsKey(toAdd.Id))
            {
                KnownClasses.Add(toAdd.Id, toAdd);
            }
        }

        public OntologyClass GetClass(string typeId)
        {
            OntologyClass returnVal = null;
            KnownClasses.TryGetValue(typeId, out returnVal);
            return returnVal;
        }
    }
}
