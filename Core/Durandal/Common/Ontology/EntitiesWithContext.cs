using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Ontology
{
    public class EntitiesWithContext
    {
        private KnowledgeContext _context;
        private HashSet<string> _entityIds;

        public EntitiesWithContext(KnowledgeContext context)
        {
            _context = context;
            _entityIds = new HashSet<string>();
        }

        public KnowledgeContext Context
        {
            get
            {
                return _context;
            }
        }
        
        public IEnumerable<string> EntityIds
        {
            get
            {
                return _entityIds;
            }
        }

        public void Add(Entity e)
        {
            if (_entityIds.Contains(e.EntityId))
            {
                return;
            }

            if (e.KnowledgeContext != _context)
            {
                throw new InvalidOperationException("Entity " + e.ToString() + " is not found in the given context");
            }

            _entityIds.Add(e.EntityId);
        }

        /// <summary>
        /// Returns all of the entities in this collection of the specified type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerable<T> GetEntities<T>() where T : Entity
        {
            return _context.GetEntitiesOfType<T>(_entityIds);
        }
    }
}
