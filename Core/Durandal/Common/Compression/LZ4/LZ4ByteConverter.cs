using Durandal.Common.File;
using Durandal.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.Compression.LZ4
{
    public class LZ4ByteConverter<T> : IByteConverter<T> where T : class
    {
        private readonly IByteConverter<T> _inner;
        private readonly bool _highCompression;
        private readonly int _blockSize;

        public LZ4ByteConverter(IByteConverter<T> inner, bool highCompression = false, int blockSize = 1048576)
        {
            _inner = inner;
            _highCompression = highCompression;
            _blockSize = blockSize;
        }

        public T Decode(byte[] input, int offset, int length)
        {
            using (RecyclableMemoryStream decompressedStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (MemoryStream rawStream = new MemoryStream(input, offset, length, false))
                {
                    using (LZ4Stream decompressor = new LZ4Stream(rawStream, LZ4StreamMode.Decompress))
                    {
                        byte[] lengthHeaderBytes = new byte[4];
                        decompressor.Read(lengthHeaderBytes, 0, 4);
                        decompressor.CopyTo(decompressedStream);
                        byte[] decompressedData = decompressedStream.ToArray();
                        return _inner.Decode(decompressedData, 0, decompressedData.Length);
                    }
                }
            }
        }

        public T Decode(Stream input, int length)
        {
            using (RecyclableMemoryStream decompressedStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (LZ4Stream decompressor = new LZ4Stream(input, LZ4StreamMode.Decompress))
                {
                    byte[] lengthHeaderBytes = new byte[4];
                    decompressor.Read(lengthHeaderBytes, 0, 4);
                    int uncompressedLength = BitConverter.ToInt32(lengthHeaderBytes, 0);
                    return _inner.Decode(decompressor, uncompressedLength);
                }
            }
        }

        public byte[] Encode(T input)
        {
            byte[] decompressedData = _inner.Encode(input);
            
            using (MemoryStream rawStream = new MemoryStream(decompressedData, 0, decompressedData.Length))
            {
                using (RecyclableMemoryStream compressedStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                {
                    LZ4StreamFlags streamFlags = LZ4StreamFlags.IsolateInnerStream;
                    if (_highCompression)
                    {
                        streamFlags |= LZ4StreamFlags.HighCompression;
                    }

                    using (LZ4Stream compressor = new LZ4Stream(
                        compressedStream,
                        LZ4StreamMode.Compress,
                        streamFlags,
                        _blockSize))
                    {
                        byte[] lengthHeader = BitConverter.GetBytes(decompressedData.Length);
                        compressor.Write(lengthHeader, 0, 4);
                        rawStream.CopyTo(compressor);
                    }

                    return compressedStream.ToArray();
                } 
            }
        }

        public int Encode(T input, Stream target)
        {
            using (LZ4Stream compressor = new LZ4Stream(
                target,
                LZ4StreamMode.Compress,
                _highCompression ? LZ4StreamFlags.HighCompression : LZ4StreamFlags.Default,
                _blockSize))
            {
                // Since we have to prefix the output with the length, we have to serialize the
                // entire object to a byte array first, which means we can't just directly stream
                // it the optimal way
                byte[] rawData = _inner.Encode(input);
                byte[] lengthHeader = BitConverter.GetBytes(rawData.Length);
                compressor.Write(lengthHeader, 0, 4);
                compressor.Write(rawData, 0, rawData.Length);
                return (int)compressor.Length;
            }
        }
    }
}
