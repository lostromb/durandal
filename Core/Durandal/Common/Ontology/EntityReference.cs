using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Ontology
{
    /// <summary>
    /// Represents a reference to a specific entity ID outside of any context.
    /// </summary>
    /// <typeparam name="T">The type of entity we are referencing</typeparam>
    public class EntityReference<T> where T : Entity
    {
        public EntityReference(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("Cannot reference an entity with an empty ID");
            }

            EntityId = id;
        }

        public string EntityId
        {
            get;
        }

        internal EntityReferenceInternal InternalReference
        {
            get
            {
                return new EntityReferenceInternal(EntityId);
            }
        }
    }

    /// <summary>
    /// A typeless variation of the entity reference class. Needed because a Triple
    /// doesn't have type variables, but references should use type variables to make
    /// it clear what they are referencing. So we downcast internally to reconcile the two.
    /// </summary>
    internal class EntityReferenceInternal
    {
        public EntityReferenceInternal(string id)
        {
            EntityId = id;
        }

        public string EntityId
        {
            get;
        }
    }
}
