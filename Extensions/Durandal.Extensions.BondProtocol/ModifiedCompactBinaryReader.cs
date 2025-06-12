using Bond;
using Bond.IO;
using Bond.Protocols;
using Durandal.Common.Collections;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Durandal.Extensions.BondProtocol
{
    /// <summary>
    /// Reader for the Compact Binary tagged protocol.
    /// This particular implementation has been modified for the Durandal project to work better with pooled buffers.
    /// </summary>
    /// <typeparam name="I">Implementation of IInputStream interface</typeparam>
    public struct ModifiedCompactBinaryReader<I> : IClonableTaggedProtocolReader, ITaggedProtocolReader, ICloneable<IClonableTaggedProtocolReader>, ICloneable<ModifiedCompactBinaryReader<I>> where I : IInputStream, ICloneable<I>
    {
        private readonly I input;

        private readonly ushort version;
        private readonly bool _copyByteBlobs;

        //
        // Summary:
        //     Create an instance of ModifiedCompactBinaryReader
        //
        // Parameters:
        //   input:
        //     Input payload
        //
        //   version:
        //     Protocol version
        public ModifiedCompactBinaryReader(I input, ushort version = 1, bool copyByteBlobs = false)
        {
            this.input = input;
            this.version = version;
            _copyByteBlobs = copyByteBlobs;
        }

        //
        // Summary:
        //     Clone the reader
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ModifiedCompactBinaryReader<I> ICloneable<ModifiedCompactBinaryReader<I>>.Clone()
        {
            return new ModifiedCompactBinaryReader<I>(input.Clone(), version);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IClonableTaggedProtocolReader ICloneable<IClonableTaggedProtocolReader>.Clone()
        {
            return ((ICloneable<ModifiedCompactBinaryReader<I>>)this).Clone();
        }

        //
        // Summary:
        //     Start reading a struct
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadStructBegin()
        {
            if (2 == version)
            {
                input.ReadVarUInt32();
            }
        }

        //
        // Summary:
        //     Start reading a base of a struct
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadBaseBegin()
        {
        }

        //
        // Summary:
        //     End reading a struct
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadStructEnd()
        {
        }

        //
        // Summary:
        //     End reading a base of a struct
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadBaseEnd()
        {
        }

        //
        // Summary:
        //     Start reading a field
        //
        // Parameters:
        //   type:
        //     An out parameter set to the field type or BT_STOP/BT_STOP_BASE if there is no
        //     more fields in current struct/base
        //
        //   id:
        //     Out parameter set to the field identifier
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadFieldBegin(out BondDataType type, out ushort id)
        {
            uint num = input.ReadUInt8();
            type = (BondDataType)(num & 0x1F);
            num >>= 5;
            switch (num)
            {
                case 0u:
                case 1u:
                case 2u:
                case 3u:
                case 4u:
                case 5u:
                    id = (ushort)num;
                    break;
                case 6u:
                    id = input.ReadUInt8();
                    break;
                default:
                    id = input.ReadUInt16();
                    break;
            }
        }

        //
        // Summary:
        //     End reading a field
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadFieldEnd()
        {
        }

        //
        // Summary:
        //     Start reading a list or set container
        //
        // Parameters:
        //   count:
        //     An out parameter set to number of items in the container
        //
        //   elementType:
        //     An out parameter set to type of container elements
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadContainerBegin(out int count, out BondDataType elementType)
        {
            byte b = input.ReadUInt8();
            elementType = (BondDataType)(b & 0x1F);
            if (2 == version && (b & 0xE0) != 0)
            {
                count = (b >> 5) - 1;
            }
            else
            {
                count = (int)input.ReadVarUInt32();
            }
        }

        //
        // Summary:
        //     Start reading a map container
        //
        // Parameters:
        //   count:
        //     An out parameter set to number of items in the container
        //
        //   keyType:
        //     An out parameter set to the type of map keys
        //
        //   valueType:
        //     An out parameter set to the type of map values
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadContainerBegin(out int count, out BondDataType keyType, out BondDataType valueType)
        {
            keyType = (BondDataType)input.ReadUInt8();
            valueType = (BondDataType)input.ReadUInt8();
            count = (int)input.ReadVarUInt32();
        }

        //
        // Summary:
        //     End reading a container
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadContainerEnd()
        {
        }

        //
        // Summary:
        //     Read an UInt8
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadUInt8()
        {
            return input.ReadUInt8();
        }

        //
        // Summary:
        //     Read an UInt16
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
        {
            return input.ReadVarUInt16();
        }

        //
        // Summary:
        //     Read an UInt32
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
        {
            return input.ReadVarUInt32();
        }

        //
        // Summary:
        //     Read an UInt64
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64()
        {
            return input.ReadVarUInt64();
        }

        //
        // Summary:
        //     Read an Int8
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadInt8()
        {
            return (sbyte)input.ReadUInt8();
        }

        //
        // Summary:
        //     Read an Int16
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16()
        {
            ushort value = input.ReadVarUInt16();
            return (short)((value >> 1) ^ (-(value & 1)));
        }

        //
        // Summary:
        //     Read an Int32
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32()
        {
            uint value = input.ReadVarUInt32();
            return (int)((value >> 1) ^ (-(value & 1)));
        }

        //
        // Summary:
        //     Read an Int64
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64()
        {
            ulong value = input.ReadVarUInt64();
            return (long)((value >> 1) ^ (ulong)(-(long)(value & 1)));
        }

        //
        // Summary:
        //     Read a bool
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBool()
        {
            return input.ReadUInt8() != 0;
        }

        //
        // Summary:
        //     Read a float
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        {
            return input.ReadFloat();
        }

        //
        // Summary:
        //     Read a double
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            return input.ReadDouble();
        }

        //
        // Summary:
        //     Read a UTF-8 string
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadString()
        {
            int num = (int)input.ReadVarUInt32();
            if (num != 0)
            {
                return input.ReadString(Encoding.UTF8, num);
            }

            return string.Empty;
        }

        //
        // Summary:
        //     Read a UTF-16 string
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadWString()
        {
            int num = (int)input.ReadVarUInt32();
            if (num != 0)
            {
                return input.ReadString(Encoding.Unicode, num << 1);
            }

            return string.Empty;
        }

        //
        // Summary:
        //     Read an array of bytes verbatim.
        //     If we are reading from a pooled buffer, this method allocates a new array to store the result.
        //
        // Parameters:
        //   count:
        //     Number of bytes to read
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ReadBytes(int count)
        {
            ArraySegment<byte> returnVal = input.ReadBytes(count);
            if (_copyByteBlobs && returnVal.Array != null && returnVal.Count > 0)
            {
#if DEBUG
                // +8 is just to make sure that the caller is using offsets properly in the returned buffer segment
                byte[] newBuf = new byte[returnVal.Count + 8]; 
                ArrayExtensions.MemCopy(returnVal.Array, returnVal.Offset, newBuf, 8, returnVal.Count);
                returnVal = new ArraySegment<byte>(newBuf, 8, returnVal.Count);
#else
                byte[] newBuf = new byte[returnVal.Count];
                ArrayExtensions.MemCopy(returnVal.Array, returnVal.Offset, newBuf, 0, returnVal.Count);
                returnVal = new ArraySegment<byte>(newBuf);
#endif
            }

            return returnVal;
        }

        //
        // Summary:
        //     Skip a value of specified type
        //
        // Parameters:
        //   type:
        //     Type of the value to skip
        //
        // Exceptions:
        //   T:System.IO.EndOfStreamException:
        public void Skip(BondDataType type)
        {
            switch (type)
            {
                case BondDataType.BT_BOOL:
                case BondDataType.BT_UINT8:
                case BondDataType.BT_INT8:
                    input.SkipBytes(1);
                    break;
                case BondDataType.BT_UINT16:
                case BondDataType.BT_INT16:
                    input.ReadVarUInt16();
                    break;
                case BondDataType.BT_UINT32:
                case BondDataType.BT_INT32:
                    input.ReadVarUInt32();
                    break;
                case BondDataType.BT_FLOAT:
                    input.SkipBytes(4);
                    break;
                case BondDataType.BT_DOUBLE:
                    input.SkipBytes(8);
                    break;
                case BondDataType.BT_UINT64:
                case BondDataType.BT_INT64:
                    input.ReadVarUInt64();
                    break;
                case BondDataType.BT_STRING:
                    input.SkipBytes((int)input.ReadVarUInt32());
                    break;
                case BondDataType.BT_WSTRING:
                    input.SkipBytes((int)(input.ReadVarUInt32() << 1));
                    break;
                case BondDataType.BT_LIST:
                case BondDataType.BT_SET:
                    SkipContainer();
                    break;
                case BondDataType.BT_MAP:
                    SkipMap();
                    break;
                case BondDataType.BT_STRUCT:
                    SkipStruct();
                    break;
                default:
                    throw new InvalidDataException(string.Format("Invalid BondDataType {0}", type));
            }
        }

        private void SkipContainer()
        {
            int count;
            BondDataType elementType;
            ReadContainerBegin(out count, out elementType);
            switch (elementType)
            {
                case BondDataType.BT_UINT8:
                case BondDataType.BT_INT8:
                    input.SkipBytes(count);
                    return;
                case BondDataType.BT_FLOAT:
                    input.SkipBytes(count * 4);
                    return;
                case BondDataType.BT_DOUBLE:
                    input.SkipBytes(count * 8);
                    return;
            }

            while (0 <= --count)
            {
                Skip(elementType);
            }
        }

        private void SkipMap()
        {
            int count;
            BondDataType keyType;
            BondDataType valueType;
            ReadContainerBegin(out count, out keyType, out valueType);
            while (0 <= --count)
            {
                Skip(keyType);
                Skip(valueType);
            }
        }

        private void SkipStruct()
        {
            if (2 == version)
            {
                input.SkipBytes((int)input.ReadVarUInt32());
                return;
            }

            while (true)
            {
                BondDataType type;
                ushort dummy;
                ReadFieldBegin(out type, out dummy);
                switch (type)
                {
                    case BondDataType.BT_STOP:
                        return;
                    case BondDataType.BT_STOP_BASE:
                        continue;
                }

                Skip(type);
            }
        }
    }
}