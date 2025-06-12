using System;
using System.Collections.Generic;
using System.Text;

namespace OntologySchemaTransformer
{
    public interface IClassResolver
    {
        OntologyClass GetClass(string typeId);
    }
}
