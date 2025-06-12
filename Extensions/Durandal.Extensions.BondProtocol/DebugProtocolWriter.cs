// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Durandal.Extensions.BondProtocol
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Xml;
    using Bond.Protocols;
    using Bond;
    using Durandal.Common.Logger;
    using System.Threading;
    using Durandal.Common.Time;

    /// <summary>
    /// Protocol writer intended to diagnose bond schema errors at a low level (for example, if an object fails to parse because of a potential schema mismatch, this can tell you what the mismatch is).
    /// It works by simply dumping the individual fields / structs / boundaries to a logger for plain text comparison.
    /// </summary>
    [Reader(typeof(SimpleXmlReader))]
    public struct DebugProtocolWriter : IProtocolWriter, ITextProtocolWriter, IEquatable<DebugProtocolWriter>
    {
        private readonly int _id;
        private readonly ILogger writer;
        private string indent;
        
        public DebugProtocolWriter(ILogger writer)
        {
            this.writer = writer;
            indent = string.Empty;
            _id = new Random().Next();
        }

        public void Flush()
        {
            writer.Flush(CancellationToken.None, DefaultRealTimeProvider.Singleton);
        }

        #region IProtocolWriter

        public void WriteVersion()
        {
            throw new NotImplementedException();
        }

        #region Struct

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStructBegin(Metadata metadata)
        {
            writer.Log(indent + "STRUCT_BEGIN " + metadata.qualified_name);
            PushNamespace();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBaseBegin(Metadata metadata)
        {
            //PushNamespace(metadata);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStructEnd()
        {
            PopNamespace();
            writer.Log(indent + "STRUCT_END");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBaseEnd()
        {
            //PopNamespace();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFieldBegin(BondDataType dataType, ushort id, Metadata metadata)
        {
            writer.Log(indent + "FIELD_BEGIN (" + id + ") " + metadata.name);
            PushNamespace();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFieldEnd()
        {
            PopNamespace();
            writer.Log(indent + "FIELD_END");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFieldOmitted(BondDataType dataType, ushort id, Metadata metadata)
        { }

        #endregion

        #region Containers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteContainerBegin(int count, BondDataType elementType)
        {
            writer.Log(indent + "CONTAINER_BEGIN <" + elementType + ">");
            PushNamespace();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteContainerBegin(int count, BondDataType keyType, BondDataType valueType)
        {
            writer.Log(indent + "CONTAINER_BEGIN <" + keyType + "," + valueType + ">");
            PushNamespace();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteContainerEnd()
        {
            PopNamespace();
            writer.Log(indent + "CONTAINER_END");
        }

        #region ITextProtocolWriter

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteItemBegin()
        {
            //writer.Log(indent + "ITEM_BEGIN");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteItemEnd()
        {
            //writer.Log(indent + "ITEM_END");
        }

        #endregion

        #endregion

        #region Scalars

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt8(sbyte value)
        {
            writer.Log(indent + "VALUE_INT8 " + value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt16(short value)
        {
            writer.Log(indent + "VALUE_INT16 " + value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(int value)
        {
            writer.Log(indent + "VALUE_INT32 " + value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64(long value)
        {
            writer.Log(indent + "VALUE_INT64 " + value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt8(byte value)
        {
            writer.Log(indent + "VALUE_UINT8 " + value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(ushort value)
        {
            writer.Log(indent + "VALUE_UINT16 " + value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(uint value)
        {
            writer.Log(indent + "VALUE_UINT32 " + value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64(ulong value)
        {
            writer.Log(indent + "VALUE_UINT64 " + value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloat(float value)
        {
            writer.Log(indent + "VALUE_FLOAT " + value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value)
        {
            writer.Log(indent + "VALUE_DOUBLE " + value);
        }

        public void WriteBytes(ArraySegment<byte> data)
        {
            writer.Log(indent + "VALUE_BLOB (len " + data.Count + ")");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBool(bool value)
        {
            writer.Log(indent + "VALUE_BOOL " + value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteString(string value)
        {
            // Other protocols depend on expressions such as value.Count to
            // throw an NRE if we've been asked to serialize a non-nullable
            // string field that is set to null. Implementations of
            // System.Xml.XmlWriter may successfully serialize it, so we need
            // to check and throw explicitly before that.
            if (value == null)
            {
                throw new NullReferenceException(
                   "Attempted to serialize a null string. This may indicate a non-nullable string field that was set to null.");
            }
            writer.Log(indent + "VALUE_STRING " + value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteWString(string value)
        {
            // Other protocols depend on expressions such as value.Count to
            // throw an NRE if we've been asked to serialize a non-nullable
            // string field that is set to null. Implementations of
            // System.Xml.XmlWriter may successfully serialize it, so we need
            // to check and throw explicitly before that.
            if (value == null)
            {
                throw new NullReferenceException(
                   "Attempted to serialize a null string. This may indicate a non-nullable string field that was set to null.");
            }
            writer.Log(indent + "VALUE_WSTRING " + value);
        }
        #endregion
        #endregion

        void PushNamespace()
        {
            indent = indent + "  ";
        }

        void PopNamespace()
        {
            indent = indent.Substring(0, Math.Max(0, indent.Length - 2));
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DebugProtocolWriter))
            {
                return false;
            }

            DebugProtocolWriter other = (DebugProtocolWriter)obj;
            return Equals(other);
        }

        public bool Equals(DebugProtocolWriter other)
        {
            return _id == other._id;
        }

        public override int GetHashCode()
        {
            return _id;
        }

        public static bool operator ==(DebugProtocolWriter left, DebugProtocolWriter right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DebugProtocolWriter left, DebugProtocolWriter right)
        {
            return !(left == right);
        }
    }
}