using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Ontology
{
    /// <summary>
    /// Represents a set of entities expressed in graph form as a set of triples and associations. In its simplest form, when
    /// all data is in-memory, the context is simply a bag that stores all of the entity data. However, when a KnowledgeContext
    /// is associated with one or more external entity sources, it is more accurate to think of it as a small lens which glimpses into
    /// a piece of a much larger knowledge graph. Walking outwards along the graph will automatically load data from the "universe"
    /// and put it into the salient context. A context can be serialized in order to preserve the relevant entities associated
    /// with a particular execution or point in time - however, in order to prevent the context from growing too large, it is
    /// recommended that you also maintain a list of "source" entities that you can use for garbage collection, to filter out
    /// entities that are no longer relevant.
    /// </summary>
    [JsonConverter(typeof(JsonConverter_Local))]
    public class KnowledgeContext
    {
        /// <summary>
        /// Random number used to ensure that serialized blobs are in fact knowledge context objects. The last byte is reserved for the version
        /// </summary>
        private const int MAGIC_NUMBER = 0x5F9A5100;

        /// <summary>
        /// Used to check forwards compatibility with serialized contexts
        /// </summary>
        private const int SERIALIZED_VERSION = 0x03;

        /// <summary>
        /// The set of all triples currently connected to associated entities.
        /// Maps from entity ID => list of triple
        /// </summary>
        private readonly Dictionary<string, List<Triple>> _associatedTriples;

        /// <summary>
        /// The set of all entities that are "relevant" in this context. For in-memory contexts, this will just
        /// contain the finite set of all known entities. However, if entities are shared with an external source,
        /// this set represents the subset of entities that have been downloaded and cached locally.
        /// Maps from entity ID => entity record
        /// </summary>
        private readonly Dictionary<string, Entity> _associatedEntities;

        /// <summary>
        /// Since we can't predict what types of entities will be in the context, we must memoize the inheritance
        /// pattern of all associated entity types locally so we can make accurate inheritance decisions.
        /// Maps from type name => set of all parent types
        /// </summary>
        private readonly Dictionary<string, ISet<string>> _entityInheritance;

        /// <summary>
        /// This is a list of external sources which can be used to dynamically load entities into this context.
        /// These are invoked when a user calls GetEntity(), either directly on the context, or indirectly
        /// by dereferencing an entity reference from a field property of an already existing entity in this context.
        /// </summary>
        private List<IEntitySource> _entitySources;

        /// <summary>
        /// Saves us from having to use reflection to find the type ID of various objects
        /// </summary>
        private Dictionary<Type, TypeIdTuple> _typeIdCache;

        /// <summary>
        /// Random unique ID for this context, mainly used for debugging
        /// </summary>
        private Guid _contextId;

        /// <summary>
        /// Creates an empty knowledge context with an optional set of entity sources.
        /// </summary>
        /// <param name="entitySources">An optional set of providers that can be used to dereference (load) entities that are not currently in the context.</param>
        public KnowledgeContext(IEnumerable<IEntitySource> entitySources = null)
        {
            _associatedTriples = new Dictionary<string, List<Triple>>();
            _associatedEntities = new Dictionary<string, Entity>();
            _entityInheritance = new Dictionary<string, ISet<string>>();
            _typeIdCache = new Dictionary<Type, TypeIdTuple>();
            _entitySources = new List<IEntitySource>();
            if (entitySources != null)
            {
                _entitySources.AddRange(entitySources);
            }

            _contextId = Guid.NewGuid();
        }

        public override bool Equals(object obj)
        {
            if (obj == null ||
                !(obj is KnowledgeContext))
            {
                return false;
            }

            KnowledgeContext other = obj as KnowledgeContext;
            return _contextId == other._contextId;
        }

        public override int GetHashCode()
        {
            return _contextId.GetHashCode();
        }

        public bool IsEmpty
        {
            get
            {

                return _associatedEntities.Count == 0;
            }
        }

        /// <summary>
        /// Attempts to retrieve the entity with the given unique ID from this knowledge context.
        /// If the entity is not immediately available, this method may query external sources
        /// (as provided by each available IEntitySource) to try and load the entity into the current
        /// context.
        /// </summary>
        /// <param name="entityId">The ID of the entity to retrieve. This ID should follow a URL-like format as "graph://entityid-unique-id"</param>
        /// <returns>The entity if found, null otherwise</returns>
        public async Task<Entity> GetEntity(string entityId)
        {
            if (!_associatedEntities.ContainsKey(entityId) &&
                entityId.Contains("://"))
            {
                // If entity is not cached, query our sources to find it
                string entityScheme = entityId.Substring(0, entityId.IndexOf(':'));
                foreach (IEntitySource source in _entitySources)
                {
                    if (string.Equals(entityScheme, source.EntityIdScheme, StringComparison.OrdinalIgnoreCase))
                    {
                        await source.ResolveEntity(this, entityId).ConfigureAwait(false);
                        break;
                    }
                }
            }

            return GetEntityInMemory(entityId);
        }

        /// <summary>
        /// Attempts to retrieve the entity with the given unique ID from this knowledge context.
        /// If the entity is not in memory, this method returns null (Entity sources will not be queried)
        /// </summary>
        /// <param name="entityId">The ID of the entity to retrieve. This ID should follow a URL-like format as "graph://entityid-unique-id"</param>
        /// <returns>The entity if found, null otherwise</returns>
        public Entity GetEntityInMemory(string entityId)
        {
            if (_associatedEntities.ContainsKey(entityId))
            {
                // If inheritance does not match, we need to set inheritance here
                Entity returnVal = _associatedEntities[entityId];
                if (_entityInheritance.ContainsKey(returnVal.EntityTypeName))
                {
                    returnVal.InheritsFrom = _entityInheritance[returnVal.EntityTypeName];
                }

                return returnVal;
            }

            return null;
        }

        /// <summary>
        /// Attempts to retrieve the entity with the given unique ID and the given type from this knowledge context.
        /// If the entity is not immediately available, this method may query external sources
        /// (as provided by each available IEntitySource) to try and load the entity into the current
        /// context.
        /// </summary>
        /// <param name="entityId">The ID of the entity to retrieve. This ID should follow a URL-like format as "graph://entityid-unique-id"</param>
        /// <typeparam name="T">The expected type of the entity to be retrieved</typeparam>
        /// <returns>An entity of the appropriate type, if found. If not found or if the type parameter does not match, return null.</returns>
        public async Task<T> GetEntity<T>(string entityId) where T : Entity
        {
            if (!_associatedEntities.ContainsKey(entityId) &&
                entityId.Contains("://"))
            {
                // If entity is not cached, query our sources to find it
                string entityScheme = entityId.Substring(0, entityId.IndexOf(':'));
                foreach (IEntitySource source in _entitySources)
                {
                    if (string.Equals(entityScheme, source.EntityIdScheme, StringComparison.OrdinalIgnoreCase))
                    {
                        await source.ResolveEntity(this, entityId).ConfigureAwait(false);
                        break;
                    }
                }
            }

            return GetEntityInMemory<T>(entityId);
        }

        /// <summary>
        /// Attempts to retrieve the entity with the given unique ID and the given type from this knowledge context.
        /// If the entity is not immediately available, this method returns null.
        /// </summary>
        /// <param name="entityId">The ID of the entity to retrieve. This ID should follow a URL-like format as "graph://entityid-unique-id"</param>
        /// <typeparam name="T">The expected type of the entity to be retrieved</typeparam>
        /// <returns>An entity of the appropriate type, if found. If not found or if the type parameter does not match, return null.</returns>
        public T GetEntityInMemory<T>(string entityId) where T : Entity
        {
            if (_associatedEntities.ContainsKey(entityId))
            {
                TypeIdTuple targetTypeName = GetTypeIdFromType<T>();
                Entity e = _associatedEntities[entityId];
                if (string.Equals(e.EntityTypeName, targetTypeName.TypeId) ||
                    (_entityInheritance.ContainsKey(e.EntityTypeName) && _entityInheritance[e.EntityTypeName].Contains(targetTypeName.TypeId)))
                {
                    T returnVal = Activator.CreateInstance(typeof(T), e) as T;
                    if (returnVal != null)
                    {
                        return returnVal;
                    }
                }
            }

            return null;
        }

        internal void Associate(Entity toAdd)
        {
            if (_associatedEntities.ContainsKey(toAdd.EntityId))
            {
                _associatedEntities.Remove(toAdd.EntityId);
            }

            // Downcast the entity so we don't rely on built-in type information for anything
            Entity castEntity = new Entity(this, toAdd.EntityId, toAdd.EntityTypeName, toAdd.InheritsFrom);
            _associatedEntities[toAdd.EntityId] = castEntity;

            // Also copy this entity's inheritance pattern if not present
            if (toAdd.InheritsFrom != null && toAdd.InheritsFrom.Count > 0 && !_entityInheritance.ContainsKey(toAdd.EntityTypeName))
            {
                _entityInheritance.Add(toAdd.EntityTypeName, toAdd.InheritsFrom);
            }

            TypeIdTuple thisTypeId = new TypeIdTuple();
            thisTypeId.TypeId = toAdd.EntityTypeName;
            if (toAdd.EntityId.StartsWith("enum://"))
            {
                thisTypeId.EnumId = toAdd.EntityId;
            }

            AddTypeIdToCache(toAdd.GetType(), thisTypeId);
        }

        internal void Associate(Triple toAdd)
        {
            if (!_associatedTriples.ContainsKey(toAdd.EntityId))
            {
                _associatedTriples[toAdd.EntityId] = new List<Triple>();
            }

            _associatedTriples[toAdd.EntityId].Add(toAdd);
        }

        /// <summary>
        /// Given a set of possible entity IDs within this context, return the set of all entities which inherit the given type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ids"></param>
        /// <returns></returns>
        public IList<T> GetEntitiesOfType<T>(ISet<string> ids) where T : Entity
        {
            IList<T> returnVal = new List<T>();
            if (typeof(T) == typeof(Entity))
            {
                foreach (Entity e in _associatedEntities.Values)
                {
                    if (ids.Contains(e.EntityId))
                    {
                        returnVal.Add(e as T);
                    }
                }
            }
            else
            {
                TypeIdTuple targetTypeName = GetTypeIdFromType<T>();
                
                foreach (Entity e in _associatedEntities.Values)
                {
                    if (ids.Contains(e.EntityId))
                    {
                        // Does the given type match inheritance constraints?
                        if (string.Equals(e.EntityTypeName, targetTypeName.TypeId) ||
                            (_entityInheritance.ContainsKey(e.EntityTypeName) && _entityInheritance[e.EntityTypeName].Contains(targetTypeName.TypeId)))
                        {
                            T castEntity = Activator.CreateInstance(typeof(T), e) as T;
                            if (castEntity != null)
                            {
                                returnVal.Add(castEntity);
                            }
                        }
                    }
                }
            }

            return returnVal;
        }

        internal IList<Triple> GetPropertiesOf(string entityId, string relationName = null)
        {
            IList<Triple> returnVal = new List<Triple>();
            List<Triple> rawTriples;
            if (!_associatedTriples.TryGetValue(entityId, out rawTriples))
            {
                return returnVal;
            }

            foreach (Triple t in rawTriples)
            {
                if (t.EntityId == entityId && (relationName == null || relationName.Equals(t.RelationName)))
                {
                    returnVal.Add(t);
                }
            }

            return returnVal;
        }

        internal void Disassociate(string entityId, string relationName)
        {
            // Remove all triples that have this same source entity and relation name
            List<Triple> rawTriples;
            if (!_associatedTriples.TryGetValue(entityId, out rawTriples))
            {
                return;
            }

            List<Triple> triplesToRemove = new List<Triple>();
            foreach (Triple t in rawTriples)
            {
                if (t.EntityId == entityId && t.RelationName == relationName)
                {
                    triplesToRemove.Add(t);
                }
            }

            foreach (Triple t in triplesToRemove)
            {
                rawTriples.Remove(t);
            }
        }

        internal void Overwrite(Triple toSet)
        {
            Disassociate(toSet.EntityId, toSet.RelationName);
            Associate(toSet);
        }

        internal bool InheritsFrom<T>(string childType, string entityId) where T : Entity
        {
            return InheritsFrom(typeof(T), childType, entityId);
        }

        internal bool InheritsFrom(Type T, string childType, string entityId)
        {
            if (T == typeof(Entity))
            {
                return true;
            }

            TypeIdTuple parentType = GetTypeIdFromType(T);

            // Does T refer to an enumerated type? Then compare the exact entity ID to make sure it's the same instance
            if (entityId.StartsWith("enum://") && !string.IsNullOrEmpty(parentType.EnumId))
            {
                return string.Equals(entityId, parentType.EnumId);
            }

            if (string.Equals(childType, parentType.TypeId))
            {
                return true;
            }

            if (_entityInheritance.ContainsKey(childType))
            {
                return _entityInheritance[childType].Contains(parentType.TypeId);
            }

            return false;
        }

        private void AddTypeIdToCache(Type type, TypeIdTuple typeId)
        {
            if (!_typeIdCache.ContainsKey(type))
            {
                _typeIdCache[type] = typeId;
            }
        }

        private TypeIdTuple GetTypeIdFromType<T>() where T : Entity
        {
            return GetTypeIdFromType(typeof(T));
        }

        private TypeIdTuple GetTypeIdFromType(Type targetType)
        {
            if (targetType == typeof(Entity))
            {
                return null;
            }
            
            if (_typeIdCache.ContainsKey(targetType))
            {
                return _typeIdCache[targetType];
            }

            // TODO need a null context here
            Entity dummy = Activator.CreateInstance(targetType, this, null) as Entity;

            TypeIdTuple typeName = new TypeIdTuple();
            typeName.TypeId = dummy.EntityTypeName;

            // Is this an enumerated value?
            if (dummy.EntityId.StartsWith("enum://"))
            {
                typeName.EnumId = dummy.EntityId;
            }

            AddTypeIdToCache(targetType, typeName);
            return typeName;
        }

        private class TypeIdTuple
        {
            public string TypeId;
            public string EnumId;
        }

        public PooledBuffer<byte> Serialize()
        {
            using (RecyclableMemoryStream stream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                Serialize(stream, true);
                return stream.ToPooledBuffer();
            }
        }

        /// <summary>
        /// Serialized all known entities and relations inside this context to the specified stream. This will
        /// only serialize "salient" entities, that is, entities that either exist only in-memory, or entities
        /// that were previously loaded from an external source. References to external entities will be serialized
        /// as references only.
        /// </summary>
        /// <param name="outStream">The stream to write to</param>
        /// <param name="leaveOpen">If true, leave the output stream open after serialization</param>
        /// <returns>True if any data was serialized, false if this context was empty.</returns>
        public void Serialize(Stream outStream, bool leaveOpen)
        {
            using (BinaryWriter writer = new BinaryWriter(outStream, StringUtils.UTF8_WITHOUT_BOM, leaveOpen))
            {
                writer.Write(MAGIC_NUMBER | SERIALIZED_VERSION);
                writer.Write(_contextId.ToByteArray(), 0, 16);
                writer.Write(_associatedEntities.Count);
                foreach (Entity entity in _associatedEntities.Values)
                {
                    entity.Serialize(writer);
                }

                int tripleCount = 0;
                foreach (List<Triple> tripleList in _associatedTriples.Values)
                {
                    tripleCount += tripleList.Count;
                }

                writer.Write(tripleCount);
                foreach (List<Triple> tripleList in _associatedTriples.Values)
                {
                    foreach (Triple relation in tripleList)
                    {
                        relation.Serialize(writer, (ushort)SERIALIZED_VERSION);
                    }
                }

                writer.Write(_entityInheritance.Count);
                foreach (KeyValuePair<string, ISet<string>> inheritance in _entityInheritance)
                {
                    writer.Write(inheritance.Key);
                    writer.Write((int)inheritance.Value.Count);
                    foreach (string baseType in inheritance.Value)
                    {
                        writer.Write(baseType);
                    }
                }
            }
        }

        public static KnowledgeContext Deserialize(byte[] data, IEnumerable<IEntitySource> entitySources = null)
        {
            using (MemoryStream stream = new MemoryStream(data, false))
            {
                return Deserialize(stream, false, entitySources);
            }
        }

        /// <summary>
        /// Deserializes a knowledge context from a serialized stream. The created context will contain all entities
        /// that were relevant in the original context when it was serialized. It may contain references to external
        /// entities, and if those are not resolvable with the new set of entitySources, those entities may always return null.
        /// </summary>
        /// <param name="inStream">The stream to deserialize from</param>
        /// <param name="leaveStreamOpen">If true, leave the inner stream open after deserializing</param>
        /// <param name="entitySources">An optional set of entity sources to associated with the returned knowledge context</param>
        /// <returns></returns>
        public static KnowledgeContext Deserialize(Stream inStream, bool leaveStreamOpen, IEnumerable<IEntitySource> entitySources = null)
        {
            KnowledgeContext returnVal = new KnowledgeContext(entitySources);
            using (BinaryReader reader = new BinaryReader(inStream, StringUtils.UTF8_WITHOUT_BOM, leaveStreamOpen))
            {
                int magicNumber = reader.ReadInt32();
                if ((magicNumber & 0xFFFFFF00) != MAGIC_NUMBER)
                {
                    throw new InvalidDataException("The given stream is not a serialized KnowledgeContext");
                }

                int version = magicNumber & 0xFF;
                if (version > SERIALIZED_VERSION)
                {
                    // Only throw an exception if we are attempting a parse a version from the future
                    throw new InvalidDataException("The given data reports it is of version " + version + " but deserializer was expecting " + SERIALIZED_VERSION);
                }

                byte[] guidBytes = reader.ReadBytes(16);
                returnVal._contextId = new Guid(guidBytes);
                int numEntities = reader.ReadInt32();
                for (int c = 0; c < numEntities; c++)
                {
                    // Creating the entity will automatically associate it with the context, so we don't need to add anything here
                    Entity.Deserialize(returnVal, reader);
                }

                int numRelations = reader.ReadInt32();
                for (int c = 0; c < numRelations; c++)
                {
                    Triple newTrip = Triple.Deserialize(reader, (ushort)version);
                    if (!returnVal._associatedTriples.ContainsKey(newTrip.EntityId))
                    {
                        returnVal._associatedTriples[newTrip.EntityId] = new List<Triple>();
                    }

                    returnVal._associatedTriples[newTrip.EntityId].Add(newTrip);
                }

                int numInheritances = reader.ReadInt32();
                for (int c = 0; c < numInheritances; c++)
                {
                    string subTypeName = reader.ReadString();
                    int numSuperTypes = reader.ReadInt32();
                    HashSet<string> superTypes = new HashSet<string>();
                    for (int z = 0; z < numSuperTypes; z++)
                    {
                        superTypes.Add(reader.ReadString());
                    }

                    returnVal._entityInheritance.Add(subTypeName, superTypes);
                }
            }

            return returnVal;
        }

        private class JsonConverter_Local : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(KnowledgeContext) == objectType;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }
                else if (reader.TokenType == JsonToken.String)
                {
                    string nextString = reader.Value as string;
                    if (string.IsNullOrEmpty(nextString))
                    {
                        return new KnowledgeContext();
                    }
                    else
                    {
                        byte[] data = Convert.FromBase64String(nextString);
                        return KnowledgeContext.Deserialize(data);
                    }
                }
                else
                {
                    throw new JsonSerializationException("Could not parse KnowledgeContext from json");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    KnowledgeContext castObject = (KnowledgeContext)value;
                    if (castObject == null)
                    {
                        writer.WriteNull();
                    }
                    else
                    {
                        if (castObject.IsEmpty)
                        {
                            writer.WriteValue(string.Empty);
                        }
                        else
                        {
                            using (PooledBuffer<byte> serialized = castObject.Serialize())
                            {
                                string base64 = Convert.ToBase64String(serialized.Buffer, 0, serialized.Length);
                                writer.WriteValue(base64);
                            }
                        }
                    }
                }
            }
        }
    }
}
