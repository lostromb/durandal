// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Durandal.Extensions.BondProtocol
{
    using Bond.IO;
    using Durandal.Common.IO;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Utils;
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Implements IOutputStream on top of RecyclableMemoryStream.
    /// You don't need to pool instances of this object, but you do need to dispose.
    /// </summary>
    public class MemoryStreamOutputBuffer : IOutputStream, IDisposable
    {
        private readonly byte[] _scratch = new byte[10]; // needs to be enough to hold a VarUInt64 which is 10 bytes
        private readonly int _minCharsToFillStringBuffer;
        private readonly RecyclableMemoryStream _memoryStream;
        private readonly PooledBuffer<byte> _stringScratch;
        private int _disposed = 0;

        public MemoryStreamOutputBuffer()
        {
            _memoryStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default);
            _stringScratch = BufferPool<byte>.Rent();

            // max bytes per char for UTF8 or UTF16 is about 3.3, so give it a little leeway
            _minCharsToFillStringBuffer = (int)Math.Round((float)(_stringScratch.Buffer.Length / 3.5f));

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~MemoryStreamOutputBuffer()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Gets or sets the current position within the buffer
        /// </summary>
        public virtual long Position
        {
            get { return _memoryStream.Position; }
            set { _memoryStream.Position = checked((int)value); }
        }

        public byte[] ToArray()
        {
            return _memoryStream.ToArray();
        }

        public PooledBuffer<byte> ToPooledBuffer()
        {
            return _memoryStream.ToPooledBuffer();
        }

        /// <summary>
        /// Write 8-bit unsigned integer
        /// </summary>
        public void WriteUInt8(byte value)
        {
            _memoryStream.WriteByte(value);
        }

        /// <summary>
        /// Write little-endian encoded 16-bit unsigned integer
        /// </summary>
        public void WriteUInt16(ushort value)
        {
            BinaryHelpers.UInt16ToByteArrayLittleEndian(value, _scratch, 0);
            _memoryStream.Write(_scratch, 0, 2);
        }

        /// <summary>
        /// Write little-endian encoded 32-bit unsigned integer
        /// </summary>
        public virtual void WriteUInt32(uint value)
        {
            BinaryHelpers.UInt32ToByteArrayLittleEndian(value, _scratch, 0);
            _memoryStream.Write(_scratch, 0, 4);
        }

        /// <summary>
        /// Write little-endian encoded 64-bit unsigned integer
        /// </summary>
        public virtual void WriteUInt64(ulong value)
        {
            BinaryHelpers.UInt64ToByteArrayLittleEndian(value, _scratch, 0);
            _memoryStream.Write(_scratch, 0, 8);
        }

        /// <summary>
        /// Write little-endian encoded single precision IEEE 754 float
        /// </summary>
        public virtual void WriteFloat(float value)
        {
            BinaryHelpers.FloatToByteArrayLittleEndian(value, _scratch, 0);
            _memoryStream.Write(_scratch, 0, 4);
        }

        /// <summary>
        /// Write little-endian encoded double precision IEEE 754 float
        /// </summary>
        public virtual void WriteDouble(double value)
        {
            BinaryHelpers.DoubleToByteArrayLittleEndian(value, _scratch, 0);
            _memoryStream.Write(_scratch, 0, 8);
        }

        /// <summary>
        /// Write an array of bytes verbatim
        /// </summary>
        /// <param name="data">Array segment specifying bytes to write</param>
        public virtual void WriteBytes(ArraySegment<byte> data)
        {
            if (data.Array != null && data.Count > 0)
            {
                _memoryStream.Write(data.Array, data.Offset, data.Count);
            }
        }

        /// <summary>
        /// Write variable encoded 16-bit unsigned integer
        /// </summary>
        public void WriteVarUInt16(ushort value)
        {
            int varSize = EncodeVarUInt16(_scratch, value, 0);
            _memoryStream.Write(_scratch, 0, varSize);
        }

        /// <summary>
        /// Write variable encoded 32-bit unsigned integer
        /// </summary>
        public void WriteVarUInt32(uint value)
        {
            int varSize = EncodeVarUInt32(_scratch, value, 0);
            _memoryStream.Write(_scratch, 0, varSize);
        }

        /// <summary>
        /// Write variable encoded 64-bit unsigned integer
        /// </summary>
        public void WriteVarUInt64(ulong value)
        {
            int varSize = EncodeVarUInt64(_scratch, value, 0);
            _memoryStream.Write(_scratch, 0, varSize);
        }

        /// <summary>
        /// Write UTF-8 or UTF-16 encoded string
        /// </summary>
        /// <param name="encoding">String encoding</param>
        /// <param name="value">String value</param>
        /// <param name="size">Size in bytes of encoded string</param>
        public virtual void WriteString(Encoding encoding, string value, int size)
        {
            // Since we don't know the length of the string, nor how many bytes the encoded representation
            // may be, we use a single pooled scratch buffer and write the string in chunks
            for (int startIdx = 0; startIdx < value.Length; startIdx += _minCharsToFillStringBuffer)
            {
                int amountToCopy = Math.Min(_minCharsToFillStringBuffer, value.Length - startIdx);
                int encodedStringSize = encoding.GetBytes(value, startIdx, amountToCopy, _stringScratch.Buffer, 0);
                _memoryStream.Write(_stringScratch.Buffer, 0, encodedStringSize);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _memoryStream?.Dispose();
                _stringScratch?.Dispose();
            }
        }

        private static int EncodeVarUInt16(byte[] data, ushort value, int index)
        {
            // byte 0
            if (value >= 0x80)
            {
                data[index++] = (byte)(value | 0x80);
                value >>= 7;
                // byte 1
                if (value >= 0x80)
                {
                    data[index++] = (byte)(value | 0x80);
                    value >>= 7;
                }
            }
            // byte 2
            data[index++] = (byte)value;
            return index;
        }

        private static int EncodeVarUInt32(byte[] data, uint value, int index)
        {
            // byte 0
            if (value >= 0x80)
            {
                data[index++] = (byte)(value | 0x80);
                value >>= 7;
                // byte 1
                if (value >= 0x80)
                {
                    data[index++] = (byte)(value | 0x80);
                    value >>= 7;
                    // byte 2
                    if (value >= 0x80)
                    {
                        data[index++] = (byte)(value | 0x80);
                        value >>= 7;
                        // byte 3
                        if (value >= 0x80)
                        {
                            data[index++] = (byte)(value | 0x80);
                            value >>= 7;
                        }
                    }
                }
            }
            // last byte
            data[index++] = (byte)value;
            return index;
        }

        private static int EncodeVarUInt64(byte[] data, ulong value, int index)
        {
            // byte 0
            if (value >= 0x80)
            {
                data[index++] = (byte)(value | 0x80);
                value >>= 7;
                // byte 1
                if (value >= 0x80)
                {
                    data[index++] = (byte)(value | 0x80);
                    value >>= 7;
                    // byte 2
                    if (value >= 0x80)
                    {
                        data[index++] = (byte)(value | 0x80);
                        value >>= 7;
                        // byte 3
                        if (value >= 0x80)
                        {
                            data[index++] = (byte)(value | 0x80);
                            value >>= 7;
                            // byte 4
                            if (value >= 0x80)
                            {
                                data[index++] = (byte)(value | 0x80);
                                value >>= 7;
                                // byte 5
                                if (value >= 0x80)
                                {
                                    data[index++] = (byte)(value | 0x80);
                                    value >>= 7;
                                    // byte 6
                                    if (value >= 0x80)
                                    {
                                        data[index++] = (byte)(value | 0x80);
                                        value >>= 7;
                                        // byte 7
                                        if (value >= 0x80)
                                        {
                                            data[index++] = (byte)(value | 0x80);
                                            value >>= 7;
                                            // byte 8
                                            if (value >= 0x80)
                                            {
                                                data[index++] = (byte)(value | 0x80);
                                                value >>= 7;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // last byte
            data[index++] = (byte)value;
            return index;
        }
    }
}
