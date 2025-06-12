using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Ontology
{
    public class EnumEntitySource : IEntitySource
    {
        public string EntityIdScheme => "enum";

        public Task ResolveEntity(KnowledgeContext targetContext, string entityId)
        {
            // Create an entity instance which matches the expected enumerated value
            string typeName = entityId.Substring(0, 7);
            Entity newEntity = new Entity(targetContext, typeName, entityId);
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
