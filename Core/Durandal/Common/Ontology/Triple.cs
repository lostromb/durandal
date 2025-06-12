using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Ontology
{
    internal class Triple
    {
        public string EntityId;
        public string RelationName;
        public Primitive Value;

        private Triple(string entityId, string relationName, Primitive value)
        {
            EntityId = entityId;
            RelationName = relationName;
            Value = value;
        }

        public Triple(string entityId, string relationName, string value)
            : this(entityId, relationName, new Primitive(value)) { }

        public Triple(string entityId, string relationName, decimal value)
            : this(entityId, relationName, new Primitive(value)) { }

        public Triple(string entityId, string relationName, bool value)
            : this(entityId, relationName, new Primitive(value)) { }

        public Triple(string entityId, string relationName, DateTimeEntity value)
            : this(entityId, relationName, new Primitive(value)) { }

        public Triple(string entityId, string relationName, Entity value)
            : this(entityId, relationName, new Primitive(value)) { }

        public Triple(string entityId, string relationName, EntityReferenceInternal value)
            : this(entityId, relationName, new Primitive(value)) { }

        public override string ToString()
        {
            return string.Format("{0}.{1} => {2}", EntityId, RelationName, Value.ToString());
        }

        public void Serialize(BinaryWriter outStream, ushort protocolVersion)
        {
            outStream.Write(EntityId);
            outStream.Write(RelationName);
            Value.Serialize(outStream, protocolVersion);
        }

        public static Triple Deserialize(BinaryReader inStream, ushort protocolVersion)
        {
            string id = inStream.ReadString();
            string relationName = inStream.ReadString();
            Primitive value = Primitive.Deserialize(inStream, protocolVersion);
            return new Triple(id, relationName, value);
        }
    }
}
