using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Ontology
{
    /// <summary>
    /// An adapter that reads/writes entity triples from a knowledge context
    /// </summary>
    /// <typeparam name="T">The runtime type of the entity being referred to</typeparam>
    public class IdentifierValue<T> where T : Entity
    {
        private readonly KnowledgeContext _context;
        private readonly string _entityId;
        private readonly string _fieldName;

        /// <summary>
        /// Constructs a new IdentifierValue
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityId"></param>
        /// <param name="fieldName"></param>
        public IdentifierValue(KnowledgeContext context, string entityId, string fieldName)
        {
            _context = context;
            _entityId = entityId;
            _fieldName = fieldName;
        }

        /// <summary>
        /// Queries the context to dereference this entity, if possible.
        /// If the referenced entity exists in the context or is otherwise accessible,
        /// it will be returned. Otherwise this returns null.
        /// </summary>
        /// <returns></returns>
        public Task<T> GetValue()
        {
            foreach (Triple triple in _context.GetPropertiesOf(_entityId, _fieldName))
            {
                if (triple.Value.Type == PrimitiveType.Identifier)
                {
                    return _context.GetEntity<T>(triple.Value.ValueAsEntityId);
                }
            }

            return Task.FromResult<T>(null);
        }

        /// <summary>
        /// Attempts to retrieve any locally stored value for this entity reference.
        /// If the target of the reference is not locally cached, this call will return
        /// null rather than querying external sources to try and resolve it.
        /// </summary>
        /// <returns></returns>
        public T ValueInMemory
        {
            get
            {
                foreach (Triple triple in _context.GetPropertiesOf(_entityId, _fieldName))
                {
                    if (triple.Value.Type == PrimitiveType.Identifier)
                    {
                        T potentialReturnVal = _context.GetEntityInMemory<T>(triple.Value.ValueAsEntityId);
                        if (potentialReturnVal != null)
                        {
                            return potentialReturnVal;
                        }

                        // Potential return val can be null if it does not match the expected type of entity
                        // For example, an overloaded association that can be one of several types of entities,
                        // and we want to fetch only the first value of a specific type
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Sets the value of this field to an entity value.
        /// The entity being added should already be in the same KnowledgeContext
        /// as the current entity.
        /// </summary>
        /// <param name="value"></param>
        public void SetValue(T value)
        {
            if (value != null)
            {
                _context.Overwrite(new Triple(_entityId, _fieldName, value));
            }
            else
            {
                _context.Disassociate(_entityId, _fieldName);
            }
        }

        /// <summary>
        /// Sets the value of this field to an entity reference.
        /// This is used if you know the ID of the target entity but have not fetched it yet.
        /// </summary>
        /// <param name="value"></param>
        public void SetValue(EntityReference<T> value)
        {
            if (value != null)
            {
                _context.Overwrite(new Triple(_entityId, _fieldName, value.InternalReference));
            }
            else
            {
                _context.Disassociate(_entityId, _fieldName);
            }
        }

        /// <summary>
        /// Retrieves the list of all values for this property, potentially
        /// querying the KnowledgeContext to resolve entity references.
        /// </summary>
        /// <returns></returns>
        public async Task<IList<T>> List()
        {
            List<T> returnVal = new List<T>();
            foreach (Triple triple in _context.GetPropertiesOf(_entityId, _fieldName))
            {
                if (triple.Value.Type == PrimitiveType.Identifier)
                {
                    returnVal.Add(await _context.GetEntity<T>(triple.Value.ValueAsEntityId).ConfigureAwait(false));
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Retrieves the list of all values for this property, but does not
        /// query any external sources to resolve unknown references
        /// </summary>
        /// <returns></returns>
        public IList<T> ListInMemory()
        {
            List<T> returnVal = new List<T>();
            foreach (Triple triple in _context.GetPropertiesOf(_entityId, _fieldName))
            {
                if (triple.Value.Type == PrimitiveType.Identifier)
                {
                    T entity = _context.GetEntityInMemory<T>(triple.Value.ValueAsEntityId);
                    if (entity != null)
                    {
                        returnVal.Add(entity);
                    }
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Adds an entity value to the list of possible values for this property
        /// </summary>
        /// <param name="value"></param>
        public void Add(T value)
        {
            _context.Associate(new Triple(_entityId, _fieldName, value));
        }

        /// <summary>
        /// Adds an entity reference to the list of possible values for this property
        /// </summary>
        /// <param name="value"></param>
        public void Add(EntityReference<T> value)
        {
            _context.Associate(new Triple(_entityId, _fieldName, value.InternalReference));
        }
    }
}
