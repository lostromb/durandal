using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class ContextualEntity
    {
        public Entity Entity;
        public float Relevance;
        public ContextualEntitySource Source;

        public ContextualEntity(Entity entity, ContextualEntitySource source, float relevance)
        {
            Entity = entity;
            Relevance = relevance;
            Source = source;
        }

        public class DescendingComparator : IComparer<ContextualEntity>
        {
            public int Compare(ContextualEntity x, ContextualEntity y)
            {
                return Math.Sign(y.Relevance - x.Relevance);
            }
        }
    }
}
