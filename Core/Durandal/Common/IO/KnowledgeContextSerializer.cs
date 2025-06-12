using Durandal.Common.Compression.LZ4;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.IO
{
    public static class KnowledgeContextSerializer
    {
        // Flag indicating that the context is compressed with LZ4
        private const byte FLAG_LZ4 = 0x01;

        public static KnowledgeContext TryDeserializeKnowledgeContext(ArraySegment<byte> blob)
        {
            KnowledgeContext returnVal = null;
            if (blob != null && blob.Count > 0)
            {
                using (MemoryStream readStream = new MemoryStream(blob.Array, blob.Offset, blob.Count))
                {
                    return TryDeserializeKnowledgeContext(readStream, false);
                }
            }

            return returnVal;
        }

        public static KnowledgeContext TryDeserializeKnowledgeContext(Stream readStream, bool leaveOpen)
        {
            KnowledgeContext returnVal = null;

            // Read the first byte to see if there are any flags
            byte flags = (byte)readStream.ReadByte();
            if ((flags & FLAG_LZ4) != 0)
            {
                LZ4StreamFlags lz4Flags = leaveOpen ? LZ4StreamFlags.IsolateInnerStream : LZ4StreamFlags.None;
                using (LZ4Stream decompressor = new LZ4Stream(readStream, LZ4StreamMode.Decompress, lz4Flags))
                {
                    returnVal = KnowledgeContext.Deserialize(decompressor, leaveOpen);
                }
            }
            else
            {
                // this code path shouldn't actually be used
                returnVal = KnowledgeContext.Deserialize(readStream, leaveOpen);
            }

            return returnVal;
        }

        public static PooledBuffer<byte> SerializeKnowledgeContext(KnowledgeContext context)
        {
            using (RecyclableMemoryStream contextSerializeStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                SerializeKnowledgeContext(context, contextSerializeStream, true);
                return contextSerializeStream.ToPooledBuffer();
            }
        }

        public static void SerializeKnowledgeContext(KnowledgeContext context, Stream targetStream, bool leaveOpen)
        {
            targetStream.WriteByte(FLAG_LZ4);
            LZ4StreamFlags lz4Flags = leaveOpen ? LZ4StreamFlags.IsolateInnerStream : LZ4StreamFlags.None;
            using (LZ4Stream compressor = new LZ4Stream(targetStream, LZ4StreamMode.Compress, lz4Flags))
            {
                context.Serialize(compressor, leaveOpen);
                compressor.Flush();
            }
        }
    }
}
