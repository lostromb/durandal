using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Ontology
{
    /// <summary>
    /// Represents an abstract entity in the Durandal ontology framework.
    /// An "entity" is not a traditional bag of values that most classes / schemas are in code.
    /// Rather, an entity is simply a view over a set of data properties found inside a <see cref="Durandal.Common.Ontology.KnowledgeContext" />,
    /// expressed internally as relational triples. Thus, it is inaccurate to say that an entity
    /// contains any actual values of itself - the entity data itself is all stored inside its
    /// associated context, and this object is just a pointer.
    /// </summary>
    public class Entity
    {
        /// <summary>
        /// The knowledge context this entity exists in
        /// </summary>
        protected KnowledgeContext _context;

        /// <summary>
        /// The global unique ID of this entity expressed as a URL, where the schema of the URL corresponds to a scheme of some IEntitySource.
        /// If this is null a new randomly generated GUID will be used as the ID, and the scheme will be "mem://" to indicate an in-memory entity only
        /// </summary>
        private string _entityId;

        /// <summary>
        /// The type name this entity should take on, e.g. "http://schema.org/Person"
        /// </summary>
        private string _entityTypeName;

        /// <summary>
        /// An internal structure which tracks the entity type superclasses that this entity implements
        /// </summary>
        private ISet<string> _inheritance;

        /// <summary>
        /// Constructs a new generic entity with the specified type name
        /// </summary>
        /// <param name="context">The knowledge context to associate with</param>
        /// <param name="entityTypeName">The type name this entity should take on, e.g. "http://schema.org/Person"</param>
        /// <param name="entityId">The global unique ID of this entity expressed as a URL, where the schema of the URL corresponds to a scheme of some IEntitySource.
        /// If this is null a new randomly generated GUID will be used as the ID, and the scheme will be "mem://" to indicate an in-memory entity only</param>
        public Entity(KnowledgeContext context, string entityTypeName, string entityId = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException("Cannot create an entity in a null context. If you only need a reference, use EntityReference<T> instead.");
            }

            if (string.IsNullOrEmpty(entityId))
            {
                _entityId = "mem://" + Guid.NewGuid().ToString();
            }
            else
            {
                _entityId = entityId;
            }

            _context = context;
            _entityTypeName = entityTypeName;
            _inheritance = new HashSet<string>();

            if (_context != null)
            {
                _context.Associate(this);
            }
        }

        /// <summary>
        /// Casting constructor, used internally
        /// </summary>
        /// <param name="castFrom"></param>
        /// <param name="expectedTypeName"></param>
        protected Entity(Entity castFrom, string expectedTypeName)
        {
            //if (!string.Equals(castFrom._entityTypeName, expectedTypeName) && !castFrom.InheritsFrom.Contains(expectedTypeName))
            //{
            //    throw new InvalidCastException("Cannot cast entity from " + castFrom._entityTypeName + " to " + expectedTypeName);
            //}

            _context = castFrom._context;
            _entityId = castFrom._entityId;
            _entityTypeName = castFrom._entityTypeName;
            _inheritance = new HashSet<string>();
        }

        /// <summary>
        /// Used internally by KnowledgeContext to preserve inheritance in generic entities
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityId"></param>
        /// <param name="expectedTypeName"></param>
        /// <param name="inheritance"></param>
        internal Entity(KnowledgeContext context, string entityId, string expectedTypeName, ISet<string> inheritance)
        {
            _context = context;
            _entityId = entityId;
            _entityTypeName = expectedTypeName;
            _inheritance = inheritance;
        }

        /// <summary>
        /// Returns the globally unique ID of this entity, with a scheme representing its source, e.g. "satori://AAAA-BBBB-CCCCDDDD"
        /// </summary>
        public string EntityId
        {
            get
            {
                return _entityId;
            }
        }

        /// <summary>
        /// The actual implementation type name of this entity, e.g. "http://schema.org/LocalBusiness"
        /// </summary>
        public string EntityTypeName
        {
            get
            {
                return _entityTypeName;
            }
        }

        internal ISet<string> InheritsFrom
        {
            get
            {
                return InheritsFromInternal;
            }
            set
            {
                _inheritance = value;
            }
        }

        protected virtual ISet<string> InheritsFromInternal
        {
            get
            {
                return _inheritance;
            }
        }

        /// <summary>
        /// Checks if the specified entity is castable to the given type.
        /// </summary>
        /// <typeparam name="T">The type to check for</typeparam>
        /// <returns>True if this entity inherits from the specified type</returns>
        public bool IsA<T>() where T : Entity
        {
            if (typeof(T) == typeof(Entity))
            {
                return true;
            }
            
            return _context.InheritsFrom<T>(EntityTypeName, EntityId);
        }

        /// <summary>
        /// Checks if the specified entity is castable to the given type.
        /// </summary>
        /// <param name="t">The type to check for</param>
        /// <returns>True if this entity inherits from the specified type</returns>
        public bool IsA(Type t)
        {
            if (t == typeof(Entity))
            {
                return true;
            }

            return _context.InheritsFrom(t, EntityTypeName, EntityId);
        }

        /// <summary>
        /// Attempts to cast this entity to a different entity type.
        /// If the cast is not allowed according to the entity's inheritance, this returns null.
        /// </summary>
        /// <typeparam name="T">The target type</typeparam>
        /// <returns></returns>
        public T As<T>() where T : Entity
        {
            if (!IsA<T>())
            {
                return null;
            }

            if (typeof(T) == typeof(Entity))
            {
                return new Entity(this, this.EntityTypeName) as T;
            }

            return Activator.CreateInstance(typeof(T), this) as T;
        }

        /// <summary>
        /// Writes all of this entity's immediate associations into the target knowledge context.
        /// Note that this entity's handle will still refer to its original knowledge context
        /// if any future changes are made to the entity
        /// </summary>
        /// <param name="target">The context to copy this entity to</param>
        /// <param name="recursive">If true, also copy all referenced entities recursively</param>
        public void CopyTo(KnowledgeContext target, bool recursive = false)
        {
            // Detect if source and target context are the same
            if (_context.Equals(target))
            {
                return;
            }

            if (recursive)
            {
                CopyToRecursive(_context, this, target, new HashSet<string>());
            }
            else
            {
                target.Associate(this);
                // Overwrite any existing triples for this entity
                foreach (Triple relation in target.GetPropertiesOf(_entityId))
                {
                    target.Disassociate(_entityId, relation.RelationName);
                }

                foreach (Triple triple in _context.GetPropertiesOf(_entityId))
                {
                    target.Associate(triple);
                }
            }
        }

        private static void CopyToRecursive(KnowledgeContext source, Entity entity, KnowledgeContext target, HashSet<string> processedIds)
        {
            target.Associate(entity);

            // Overwrite any existing triples for this entity
            foreach (Triple relation in target.GetPropertiesOf(entity._entityId))
            {
                target.Disassociate(entity._entityId, relation.RelationName);
            }
            
            foreach (Triple triple in source.GetPropertiesOf(entity._entityId))
            {
                target.Associate(triple);
                if (triple.Value.Type == PrimitiveType.Identifier)
                {
                    // See if we can recurse to another entity in-memory
                    string referencedEntityId = triple.Value.ValueAsEntityId;
                    processedIds.Add(referencedEntityId);
                    Entity referencedEntity = source.GetEntityInMemory(referencedEntityId);
                    if (referencedEntity != null)
                    {
                        CopyToRecursive(source, referencedEntity, target, processedIds);
                    }
                }
            }
        }

        internal KnowledgeContext KnowledgeContext
        {
            get
            {
                return _context;
            }
        }

        internal IEnumerable<Triple> GetPropertiesOfThis(string propertyName)
        {
            return _context.GetPropertiesOf(_entityId, propertyName);
        }

        //protected async Task<IEnumerable<T>> GetEntityRelations<T>(string relationName) where T : Entity
        //{
        //    List<T> returnVal = new List<T>();
        //    foreach (Triple t in GetPropertiesOfThis(relationName))
        //    {
        //        string entityId = t.Value.ValueAsEntityId;
        //        Entity entity = await _context.GetEntity(entityId);
        //        if (entity != null && entity is T)
        //        {
        //            returnVal.Add((T)entity);
        //        }
        //    }

        //    return returnVal;
        //}

        //internal void AddRelation(string newRelationName, string value)
        //{
        //    _context.Associate(new Triple(_entityId, newRelationName, value));
        //}

        //internal void AddRelation(string newRelationName, decimal value)
        //{
        //    _context.Associate(new Triple(_entityId, newRelationName, value));
        //}

        //internal void AddRelation(string newRelationName, bool value)
        //{
        //    _context.Associate(new Triple(_entityId, newRelationName, value));
        //}

        //internal void AddRelation(string newRelationName, Timex value)
        //{
        //    _context.Associate(new Triple(_entityId, newRelationName, value));
        //}

        //internal void AddRelation(string newRelationName, Entity value)
        //{
        //    _context.Associate(new Triple(_entityId, newRelationName, value));
        //}


        //internal void DeleteRelation(string relationName)
        //{
        //    _context.Disassociate(_entityId, relationName);
        //}


        //internal void OverwriteRelation(string newRelationName, string value)
        //{
        //    _context.Overwrite(new Triple(_entityId, newRelationName, value));
        //}

        //internal void OverwriteRelation(string newRelationName, decimal value)
        //{
        //    _context.Overwrite(new Triple(_entityId, newRelationName, value));
        //}

        //internal void OverwriteRelation(string newRelationName, bool value)
        //{
        //    _context.Overwrite(new Triple(_entityId, newRelationName, value));
        //}

        //internal void OverwriteRelation(string newRelationName, Timex value)
        //{
        //    _context.Overwrite(new Triple(_entityId, newRelationName, value));
        //}

        //internal void OverwriteRelation(string newRelationName, Entity value)
        //{
        //    _context.Overwrite(new Triple(_entityId, newRelationName, value));
        //}

        /// <summary>
        /// Serializes this entity to the specified binary writer
        /// </summary>
        /// <param name="outStream"></param>
        public void Serialize(BinaryWriter outStream)
        {
            outStream.Write(_entityId);
            outStream.Write(_entityTypeName);
        }

        /// <summary>
        /// Deserializes an entity from the given binary reader, and associates it with the given knowledge context
        /// </summary>
        /// <param name="context"></param>
        /// <param name="inStream"></param>
        /// <returns></returns>
        public static Entity Deserialize(KnowledgeContext context, BinaryReader inStream)
        {
            string id = inStream.ReadString();
            string typeName = inStream.ReadString();
            return new Entity(context, typeName, id);
        }

        /// <summary>
        /// Returns a string representation of this entity
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _entityTypeName + " (" + _entityId + ")";
        }

        /// <summary>
        /// Fetches from the context all properties related to this entity, as an untyped dictionary.
        /// This method is mainly intended for debugging or prettyprinting, and not as an explicit
        /// serializable representation of an entity
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> ToDebugDictionary()
        {
            Dictionary<string, object> returnVal = new Dictionary<string, object>();

            returnVal.Add("_id", _entityId);
            returnVal.Add("_type", _entityTypeName);
            foreach (Triple triple in _context.GetPropertiesOf(_entityId))
            {
                if (triple.Value.Type == PrimitiveType.Boolean)
                    returnVal[triple.RelationName] = triple.Value.ValueAsBoolean;
                if (triple.Value.Type == PrimitiveType.Number)
                    returnVal[triple.RelationName] = triple.Value.ValueAsNumber;
                if (triple.Value.Type == PrimitiveType.Text)
                    returnVal[triple.RelationName] = triple.Value.ValueAsText;
                if (triple.Value.Type == PrimitiveType.Identifier)
                    returnVal[triple.RelationName] = triple.Value.ValueAsEntityId;
                if (triple.Value.Type == PrimitiveType.Date ||
                    triple.Value.Type == PrimitiveType.Time ||
                    triple.Value.Type == PrimitiveType.DateTime)
                    returnVal[triple.RelationName] = triple.Value.ValueAsTimex.ToIso8601();
            }

            return returnVal;
        }

        /// <summary>
        /// Renders this entity and all of its subproperties as a json object, intended for easy visualization and debugging of entity properties
        /// </summary>
        /// <returns></returns>
        public string ToDebugJson()
        {
            HashSet<string> seenEntities = new HashSet<string>();
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder builder = pooledSb.Builder;
                ToJsonRecursive(builder, this, _context, seenEntities);
                return builder.ToString();
            }
        }

        private void ToJsonRecursive(StringBuilder builder, Entity e, KnowledgeContext context, HashSet<string> seenEntities)
        {
            if (e == null)
            {
                builder.Append("null");
                return;
            }

            if (seenEntities.Contains(e.EntityId))
            {
                builder.Append("\"Circular reference: ");
                builder.Append(e.EntityId);
                builder.Append("\"");
                return;
            }

            seenEntities.Add(e.EntityId);
            builder.Append("{ ");
            builder.Append("\"_id\": \"");
            builder.Append(e.EntityId);
            builder.Append("\", ");
            builder.Append("\"_type\": \"");
            builder.Append(e.EntityTypeName);
            builder.Append("\", ");
            Dictionary<string, List<string>> entityProperties = null;
            bool firstProperty = true;
            foreach (Triple triple in _context.GetPropertiesOf(e.EntityId))
            {
                if (triple.Value.Type != PrimitiveType.Identifier)
                {
                    if (!firstProperty)
                    {
                        builder.Append(", ");
                    }

                    firstProperty = false;
                }

                if (triple.Value.Type == PrimitiveType.Boolean)
                {
                    builder.Append("\"");
                    builder.Append(triple.RelationName);
                    builder.Append("\": ");
                    builder.Append(triple.Value.ValueAsBoolean.ToString().ToLowerInvariant());
                }
                if (triple.Value.Type == PrimitiveType.Number)
                {
                    builder.Append("\"");
                    builder.Append(triple.RelationName);
                    builder.Append("\": ");
                    builder.Append(triple.Value.ValueAsNumber.ToString());
                }
                if (triple.Value.Type == PrimitiveType.Text)
                {
                    builder.Append("\"");
                    builder.Append(triple.RelationName);
                    builder.Append("\": \"");
                    builder.Append(triple.Value.ValueAsText);
                    builder.Append("\"");
                }
                if (triple.Value.Type == PrimitiveType.Date ||
                    triple.Value.Type == PrimitiveType.Time ||
                    triple.Value.Type == PrimitiveType.DateTime)
                {
                    builder.Append("\"");
                    builder.Append(triple.RelationName);
                    builder.Append("\": \"");
                    builder.Append(triple.Value.ValueAsTimex.ToIso8601());
                    builder.Append("\"");
                }
                if (triple.Value.Type == PrimitiveType.Identifier)
                {
                    if (entityProperties == null)
                    {
                        entityProperties = new Dictionary<string, List<string>>();
                    }

                    if (!entityProperties.ContainsKey(triple.RelationName))
                    {
                        entityProperties[triple.RelationName] = new List<string>();
                    }

                    entityProperties[triple.RelationName].Add(triple.Value.ValueAsEntityId);
                }
            }

            if (entityProperties != null)
            {
                foreach (var entityProperty in entityProperties)
                {
                    if (!firstProperty)
                    {
                        builder.Append(", ");
                    }

                    firstProperty = false;
                    builder.Append("\"");
                    builder.Append(entityProperty.Key);
                    builder.Append("\": ");
                    if (entityProperty.Value.Count == 1)
                    {
                        foreach (var subEntityId in entityProperty.Value)
                        {
                            Entity subEntity = context.GetEntityInMemory(subEntityId);
                            ToJsonRecursive(builder, subEntity, context, seenEntities);
                        }
                    }
                    else
                    {
                        builder.Append("[ ");
                        bool firstArrayProperty = true;
                        foreach (var subEntityId in entityProperty.Value)
                        {
                            if (!firstArrayProperty)
                            {
                                builder.Append(", ");
                            }

                            firstArrayProperty = false;
                            Entity subEntity = context.GetEntityInMemory(subEntityId);
                            ToJsonRecursive(builder, subEntity, context, seenEntities);
                        }
                        builder.Append(" ]");
                    }
                }
            }

            builder.Append(" }");
        }

        #region Generic field value accessors, for extensible properties

        /// <summary>
        /// Fetches a custom text property with the given property name
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public TextValue ExtraPropertyText(string propertyName)
        {
            return new TextValue(_context, _entityId, propertyName);
        }

        /// <summary>
        /// Fetches a custom boolean property with the given property name
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public BooleanValue ExtraPropertyBoolean(string propertyName)
        {
            return new BooleanValue(_context, _entityId, propertyName);
        }

        /// <summary>
        /// Fetches a custom number property with the given property name
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public NumberValue ExtraPropertyNumber(string propertyName)
        {
            return new NumberValue(_context, _entityId, propertyName);
        }

        /// <summary>
        /// Fetches a custom timex property with the given property name
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public TimeValue ExtraPropertyTimex(string propertyName)
        {
            return new TimeValue(_context, _entityId, propertyName);
        }

        /// <summary>
        /// Fetches a custom entity property with the given property name and entity type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public IdentifierValue<T> ExtraPropertyEntity<T>(string propertyName) where T : Entity
        {
            return new IdentifierValue<T>(_context, _entityId, propertyName);
        }

        /// <summary>
        /// Lists all entity IDs associated with the given property name, if any
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public IEnumerable<string> ExtraPropertyEntityIds(string propertyName)
        {
            return _context.GetPropertiesOf(_entityId, propertyName)
                .Where((t) => t.Value.Type == PrimitiveType.Identifier)
                .Select((t) => t.Value.ValueAsEntityId);
        }

        #endregion
    }
}
