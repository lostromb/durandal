using Durandal.Common.IO;
using Durandal.Common.Ontology;
using Durandal.Common.Statistics;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.Dialog.Services
{
    /// <summary>
    /// Entity history which keeps all entities inside of a local context along with their age.
    /// Instances of this class can be serialized and moved around just like a KnowledgeContext can be.
    /// </summary>
    [JsonConverter(typeof(JsonConverter_Local))]
    public class InMemoryEntityHistory : IEntityHistory
    {
        private int _maxEntityAge;
        private bool _touched;
        private KnowledgeContext _context;

        /// <summary>
        /// Entity references coupled with epoch.
        /// Epoch 0 is the most recent
        /// </summary>
        private Dictionary<string, int> _entitiesWithAge;

        public InMemoryEntityHistory(int maxEntityAge = 10)
        {
            _context = new KnowledgeContext();
            _entitiesWithAge = new Dictionary<string, int>();
            _touched = false;
            _maxEntityAge = maxEntityAge;
        }

        private InMemoryEntityHistory(KnowledgeContext context, Dictionary<string, int> entities, int maxEntityAge)
        {
            _context = context;
            _entitiesWithAge = entities;
            _touched = false;
            _maxEntityAge = maxEntityAge;
        }
        
        [JsonIgnore]
        public bool Touched
        {
            get
            {
                return _touched;
            }
            internal set
            {
                _touched = value;
            }
        }

        [JsonIgnore]
        public int Count => _entitiesWithAge.Count;

        public void AddOrUpdateEntity(Entity entity)
        {
            _touched = true;
            // We do CopyTo here to ensure that all current properties of the entity are represented. This includes properties that have been deleted
            entity.CopyTo(_context, true);
            _entitiesWithAge[entity.EntityId] = 0;
        }

        /// <summary>
        /// Attempts to find an entity in the context specified by ID, and returns it if found,
        /// Otherwise, return null. The entity will also attempt to be type casted to the requested type;
        /// if the actual entity does not implement that type, it will also become null.
        /// </summary>
        /// <typeparam name="T">The type of entity that is expected, or just "Entity" for any entity.</typeparam>
        /// <param name="entityId">The ID of the entity to retrieve</param>
        /// <returns></returns>
        public T GetEntityById<T>(string entityId) where T : Entity
        {
            if (_entitiesWithAge.ContainsKey(entityId))
            {
                return _context.GetEntityInMemory<T>(entityId);
            }

            return null;
        }

        /// <summary>
        /// Returns the number of epochs since the specified entity has been touched (written) in the history.
        /// 0 = current turn, 1 = 1 turn ago, etc.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public int? GetEntityAge(Entity entity)
        {
            if (_entitiesWithAge.ContainsKey(entity.EntityId))
            {
                return _entitiesWithAge[entity.EntityId];
            }

            return null;
        }
        
        //public EntitiesWithContext ToBundle()
        //{
        //    EntitiesWithContext returnVal = new EntitiesWithContext(_context);
        //    foreach (string entityId in _entitiesWithAge.Keys)
        //    {
        //        Entity e = _context.GetEntityInMemory(entityId);
        //        if (e != null)
        //        {
        //            returnVal.Add(e);
        //        }
        //    }

        //    return returnVal;
        //}

        public IList<Hypothesis<T>> FindEntities<T>(int nbest = 1) where T : Entity
        {
            IList<Hypothesis<T>> returnVal = new List<Hypothesis<T>>();
            for (int epoch = 0; epoch < _maxEntityAge; epoch++)
            {
                foreach (KeyValuePair<string, int> item in _entitiesWithAge)
                {
                    if (item.Value == epoch)
                    {
                        Entity e = _context.GetEntityInMemory(item.Key);
                        if (e != null && e.IsA<T>())
                        {
                            int age = item.Value;
                            float conf = Math.Max(0, 1.0f - (age * 0.1f));
                            returnVal.Add(new Hypothesis<T>(e.As<T>(), conf));
                        }
                    }
                }
                
                if (returnVal.Count >= nbest)
                {
                    return returnVal;
                }
            }

            return returnVal;
        }

        public PooledBuffer<byte> Serialize()
        {
            if (_entitiesWithAge.Count == 0)
            {
                return BufferPool<byte>.Rent(0);
            }

            using (RecyclableMemoryStream stream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                Serialize(stream, true);
                return stream.ToPooledBuffer();
            }
        }

        public void Serialize(Stream stream, bool leaveStreamOpen)
        {
            if (_entitiesWithAge.Count == 0)
            {
                return;
            }

            KnowledgeContextSerializer.SerializeKnowledgeContext(_context, stream, leaveStreamOpen);
            using (BinaryWriter writer = new BinaryWriter(stream, StringUtils.UTF8_WITHOUT_BOM, leaveStreamOpen))
            {
                writer.Write(_entitiesWithAge.Count);
                foreach (KeyValuePair<string, int> item in _entitiesWithAge)
                {
                    writer.Write(item.Key);
                    writer.Write(item.Value);
                }

                writer.Write(_maxEntityAge);
            }
        }

        public static InMemoryEntityHistory Deserialize(Stream stream, bool leaveStreamOpen, int maxEntityAge = -1)
        {
            KnowledgeContext context = KnowledgeContextSerializer.TryDeserializeKnowledgeContext(stream, leaveStreamOpen);
            Dictionary<string, int> entities = new Dictionary<string, int>();
            int serializedMaxAge = maxEntityAge;
            using (BinaryReader reader = new BinaryReader(stream, StringUtils.UTF8_WITHOUT_BOM, leaveStreamOpen))
            {
                int count = reader.ReadInt32();
                for (int c = 0; c < count; c++)
                {
                    string id = reader.ReadString();
                    int age = reader.ReadInt32();
                    entities.Add(id, age);
                }
                serializedMaxAge = reader.ReadInt32();
            }

            return new InMemoryEntityHistory(context, entities, maxEntityAge > 0 ? maxEntityAge : serializedMaxAge);
        }

        public static InMemoryEntityHistory Deserialize(byte[] data, int offset, int length, int maxEntityAge = -1)
        {
            if (length == 0)
            {
                return new InMemoryEntityHistory(maxEntityAge);
            }

            using (MemoryStream stream = new MemoryStream(data, offset, length, false))
            {
                return Deserialize(stream, true, maxEntityAge);
            }
        }

        internal void Turn()
        {
            KnowledgeContext newContext = new KnowledgeContext();
            Dictionary<string, int> newEntities = new Dictionary<string, int>();

            // Increment age for each entity, filter out ones that are too old
            foreach (KeyValuePair<string, int> entity in _entitiesWithAge)
            {
                if (entity.Value < _maxEntityAge)
                {
                    newEntities.Add(entity.Key, entity.Value + 1);
                }
            }

            // Copy ones that are still relevant to the new context
            // When we copy an entity we also have to recursively copy all objects still related to it
            foreach (string entityId in newEntities.Keys)
            {
                Entity entityToCopy = _context.GetEntityInMemory(entityId);
                entityToCopy.CopyTo(newContext, true);
            }

            _context = newContext;
            _entitiesWithAge = newEntities;
        }

        private class JsonConverter_Local : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(InMemoryEntityHistory) == objectType;
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
                        return new InMemoryEntityHistory();
                    }
                    else
                    {
                        byte[] data = Convert.FromBase64String(nextString);
                        return InMemoryEntityHistory.Deserialize(data, 0, data.Length);
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
                    InMemoryEntityHistory castObject = (InMemoryEntityHistory)value;
                    if (castObject == null)
                    {
                        writer.WriteNull();
                    }
                    else
                    {
                        using (PooledBuffer<byte> bytes = castObject.Serialize())
                        {
                            if (bytes.Length == 0)
                            {
                                writer.WriteValue(string.Empty);
                            }
                            else
                            {
                                string base64 = Convert.ToBase64String(bytes.Buffer, 0, bytes.Length);
                                writer.WriteValue(base64);
                            }
                        }
                    }
                }
            }
        }
    }
}
