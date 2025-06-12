using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Ontology
{
    /// <summary>
    /// Defines a class which is able to query and retrieve entities from some larger
    /// context (an external entity database) using a specific ID scheme.
    /// </summary>
    public interface IEntitySource
    {
        /// <summary>
        /// Specifies the URL-like scheme that this entity source uses, e.g. "freebase"
        /// </summary>
        string EntityIdScheme { get; }

        /// <summary>
        /// Queries this source with a specific entity ID. Any entities that are found
        /// will be put into the target context. This can potentially write more than
        /// one entity into the context, so it does not return a value directly.
        /// </summary>
        /// <param name="targetContext"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        Task ResolveEntity(KnowledgeContext targetContext, string entityId);
    }
}
