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
    using Durandal.Common.Collections;

    /// <summary>
    /// Protocol writer intended exclusively for catching null reference exceptions that can occur inside of non-nullable string fields,
    /// and identifying exactly where in the structure that string is.
    /// </summary>
    [Reader(typeof(SimpleXmlReader))]
    public struct NullRefDiagnosingProtocolWriter : IProtocolWriter, ITextProtocolWriter, IEquatable<NullRefDiagnosingProtocolWriter>
    {
        private readonly int _id;

        private readonly List<string> _path;

        // Contains a single entry with the most recent field that was serialized.
        // Since this is a struct we have to preserve value by reference, which is why we have to have a single-entry array
        // to add an extra level of indirection.
        private readonly string[] _mostRecentField;

        /// <summary>
        /// Constructs a protocol writer
        /// </summary>
        /// <param name="dummy">Nothing; there just had to be an object here for the struct to work</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "CA1801:Remove unused parameter", Justification = "Unused parameter is needed to satisfy the interface")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is needed to satisfy the interface")]
        public NullRefDiagnosingProtocolWriter(object dummy)
        {
            _path = new List<string>();
            _mostRecentField = new string[] { string.Empty };
            _id = new Random().Next();
        }

        public void WriteFieldBegin(BondDataType dataType, ushort id, Metadata metadata)
        {
            _path.Add(metadata.name);
            _mostRecentField[0] = GetCurrentPath();
        }

        public void WriteFieldEnd()
        {
            _path.RemoveAt(_path.Count - 1);
        }

        public void WriteString(string value)
        {
            if (value == null)
            {
                throw new IOException(GetCurrentPath());
            }
        }

        public void WriteWString(string value)
        {
            if (value == null)
            {
                throw new IOException(GetCurrentPath());
            }
        }

        /// <summary>
        /// Gets the field most recently written by this protocol writer
        /// </summary>
        public string MostRecentField
        {
            get
            {
                return _mostRecentField[0];
            }
        }
        
        public void WriteVersion() { }
        public void WriteStructBegin(Metadata metadata) { }
        public void WriteBaseBegin(Metadata metadata) { }
        public void WriteStructEnd() { }
        public void WriteBaseEnd() { }
        public void WriteFieldOmitted(BondDataType dataType, ushort id, Metadata metadata) { }
        public void WriteContainerBegin(int count, BondDataType elementType) { }
        public void WriteContainerBegin(int count, BondDataType keyType, BondDataType valueType) { }
        public void WriteContainerEnd() { }
        public void WriteItemBegin() { }
        public void WriteItemEnd() { }
        public void WriteInt8(sbyte value) { }
        public void WriteInt16(short value) { }
        public void WriteInt32(int value) { }
        public void WriteInt64(long value) { }
        public void WriteUInt8(byte value) { }
        public void WriteUInt16(ushort value) { }
        public void WriteUInt32(uint value) { }
        public void WriteUInt64(ulong value) { }
        public void WriteFloat(float value) { }
        public void WriteDouble(double value) { }
        public void WriteBytes(ArraySegment<byte> data) { }
        public void WriteBool(bool value) { }

        private string GetCurrentPath()
        {
            return string.Join(".", _path);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is NullRefDiagnosingProtocolWriter))
            {
                return false;
            }

            NullRefDiagnosingProtocolWriter other = (NullRefDiagnosingProtocolWriter)obj;
            return Equals(other);
        }

        public bool Equals(NullRefDiagnosingProtocolWriter other)
        {
            return _id == other._id;
        }

        public override int GetHashCode()
        {
            return _id;
        }

        public static bool operator ==(NullRefDiagnosingProtocolWriter left, NullRefDiagnosingProtocolWriter right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NullRefDiagnosingProtocolWriter left, NullRefDiagnosingProtocolWriter right)
        {
            return !(left == right);
        }
    }
}