using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Durandal.Common.Ontology
{
    internal class Primitive
    {
        private byte[] _value;
        private PrimitiveType _type;
#if DEBUG
        private string _stringValue;
#endif

        public Primitive(string value)
        {
            ValueAsText = value;
        }

        public Primitive(decimal value)
        {
            ValueAsNumber = value;
        }

        public Primitive(bool value)
        {
            ValueAsBoolean = value;
        }

        public Primitive(DateTimeEntity value)
        {
            ValueAsTimex = value;
        }

        public Primitive(Entity value)
        {
            ValueAsEntityId = value.EntityId;
        }

        public Primitive(EntityReferenceInternal value)
        {
            ValueAsEntityId = value.EntityId;
        }

        /// <summary>
        /// Used in deserialization
        /// </summary>
        /// <param name="type"></param>
        /// <param name="rawValue"></param>
        private Primitive(PrimitiveType type, byte[] rawValue)
        {
            _type = type;
            _value = rawValue;
        }

        public PrimitiveType Type
        {
            get
            {
                return _type;
            }
        }

        public decimal ValueAsNumber
        {
            get
            {
                if (_type != PrimitiveType.Number)
                {
                    throw new InvalidCastException("Primitive value is not Number; it is " + _type.ToString());
                }

                return BinaryHelpers.ByteArrayToDecimal(_value, 0);
            }
            set
            {
                _type = PrimitiveType.Number;
                _value = new byte[16];
                BinaryHelpers.DecimalToByteArray(value, _value, 0);
#if DEBUG
                _stringValue = value.ToString();
#endif
            }
        }

        public string ValueAsText
        {
            get
            {
                if (_type != PrimitiveType.Text)
                {
                    throw new InvalidCastException("Primitive value is not Text; it is " + _type.ToString());
                }

                return Encoding.UTF8.GetString(_value, 0, _value.Length);
            }
            set
            {
                _type = PrimitiveType.Text;
                _value = Encoding.UTF8.GetBytes(value);
#if DEBUG
                _stringValue = value;
#endif
            }
        }

        public bool ValueAsBoolean
        {
            get
            {
                if (_type != PrimitiveType.Boolean)
                {
                    throw new InvalidCastException("Primitive value is not Boolean; it is " + _type.ToString());
                }

                return _value[0] != 0;
            }
            set
            {
                _type = PrimitiveType.Boolean;
                _value = new byte[] { value ? (byte)0xFF : (byte)0x00 };
#if DEBUG
                _stringValue = value.ToString();
#endif
            }
        }

        public DateTimeEntity ValueAsTimex
        {
            get
            {
                if (_type != PrimitiveType.DateTime)
                {
                    throw new InvalidCastException("Primitive value is not DateTime; it is " + _type.ToString());
                }
                string iso = Encoding.UTF8.GetString(_value, 0, _value.Length);
                return DateTimeEntity.FromIso8601(iso);
            }
            set
            {
                _type = PrimitiveType.DateTime;
                string iso = value.ToIso8601();
                _value = Encoding.UTF8.GetBytes(iso);
#if DEBUG
                _stringValue = iso;
#endif
            }
        }

        public string ValueAsEntityId
        {
            get
            {
                if (_type != PrimitiveType.Identifier)
                {
                    throw new InvalidCastException("Primitive value is not Identifier; it is " + _type.ToString());
                }

                return Encoding.UTF8.GetString(_value, 0, _value.Length);
            }
            set
            {
                _type = PrimitiveType.Identifier;
                _value = Encoding.UTF8.GetBytes(value);
#if DEBUG
                _stringValue = "Entity:" + value;
#endif
            }
        }

#if DEBUG
        public override string ToString()
        {
            return _stringValue;
        }
#endif

        public void Serialize(BinaryWriter outStream, ushort protocolVersion)
        {
            outStream.Write((short)_type);
            if (protocolVersion <= 2)
            {
                // fixed-length
                outStream.Write(_value.Length);
            }
            else
            {
                // variable length encoding
                EncodeVarUInt32((uint)_value.Length, outStream);
            }

            outStream.Write(_value);
        }

        public static Primitive Deserialize(BinaryReader inStream, ushort protocolVersion)
        {
            PrimitiveType type = (PrimitiveType)inStream.ReadInt16();
            int dataLength;
            if (protocolVersion <= 2)
            {
                // fixed-length
                dataLength = inStream.ReadInt32();
            }
            else
            {
                // variable length encoding
                dataLength = (int)DecodeVarUInt32(inStream);
            }

            byte[] data = inStream.ReadBytes(dataLength);
            return new Primitive(type, data);
        }

        /// <summary>
        /// Variable-length encoding of a 32-bit integer, with output varying from 1 to 5 bytes long
        /// </summary>
        /// <param name="value"></param>
        /// <param name="writer"></param>
        private static void EncodeVarUInt32(uint value, BinaryWriter writer)
        {
            // byte 0
            if (value >= 0x80)
            {
                writer.Write((byte)(value | 0x80));
                value >>= 7;
                // byte 1
                if (value >= 0x80)
                {
                    writer.Write((byte)(value | 0x80));
                    value >>= 7;
                    // byte 2
                    if (value >= 0x80)
                    {
                        writer.Write((byte)(value | 0x80));
                        value >>= 7;
                        // byte 3
                        if (value >= 0x80)
                        {
                            writer.Write((byte)(value | 0x80));
                            value >>= 7;
                        }
                    }
                }
            }

            // last byte
            writer.Write((byte)value);
        }

        public static uint DecodeVarUInt32(BinaryReader reader)
        {
            // byte 0
            uint result = reader.ReadByte();
            if (0x80u <= result)
            {
                // byte 1
                uint raw = reader.ReadByte();
                result = (result & 0x7Fu) | ((raw & 0x7Fu) << 7);
                if (0x80u <= raw)
                {
                    // byte 2
                    raw = reader.ReadByte();
                    result |= (raw & 0x7Fu) << 14;
                    if (0x80u <= raw)
                    {
                        // byte 3
                        raw = reader.ReadByte();
                        result |= (raw & 0x7Fu) << 21;
                        if (0x80u <= raw)
                        {
                            // byte 4
                            raw = reader.ReadByte();
                            result |= raw << 28;
                        }
                    }
                }
            }

            return result;
        }
    }
}
