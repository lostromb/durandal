using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Ontology
{
    public class MemoryEntitySource : IEntitySource
    {
        public string EntityIdScheme => "mem";

        public Task ResolveEntity(KnowledgeContext targetContext, string entityId)
        {
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
