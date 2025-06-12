using Durandal.API;
using Durandal.Common.Compression.LZ4;
using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.Dialog.Services
{
    public class UserProfileSerializer : IByteConverter<InMemoryDataStore>
    {
        public InMemoryDataStore Decode(Stream inStream, int length)
        {
            Dictionary<string, byte[]> objects = new Dictionary<string, byte[]>();
            using (LZ4Stream decompressor = new LZ4Stream(inStream, LZ4StreamMode.Decompress))
            {
                using (BinaryReader reader = new BinaryReader(decompressor, StringUtils.UTF8_WITHOUT_BOM))
                {
                    int numObjects = reader.ReadInt32();
                    for (int c = 0; c < numObjects; c++)
                    {
                        string key = reader.ReadString();
                        int dataLength = reader.ReadInt32();
                        byte[] data = reader.ReadBytes(dataLength);
                        objects[key] = data;
                    }
                }
            }

            InMemoryDataStore profile = new InMemoryDataStore(objects);
            return profile;
        }

        public InMemoryDataStore Decode(byte[] input, int offset, int length)
        {
            using (MemoryStream inStream = new MemoryStream(input, offset, length, false))
            {
                return Decode(inStream, length);
            }
        }
        
        public byte[] Encode(InMemoryDataStore input)
        {
            using (RecyclableMemoryStream outStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (LZ4Stream compressor = new LZ4Stream(outStream, LZ4StreamMode.Compress, LZ4StreamFlags.IsolateInnerStream))
                {
                    using (BinaryWriter writer = new BinaryWriter(compressor, StringUtils.UTF8_WITHOUT_BOM))
                    {
                        var allObjects = input.GetAllObjects();
                        writer.Write(allObjects.Count);
                        foreach (var obj in allObjects)
                        {
                            writer.Write(obj.Key);
                            writer.Write(obj.Value.Length);
                            writer.Write(obj.Value);
                        }
                    }
                }

                return outStream.ToArray();
            }
        }

        public int Encode(InMemoryDataStore input, Stream target)
        {
            using (LZ4Stream compressor = new LZ4Stream(target, LZ4StreamMode.Compress, LZ4StreamFlags.IsolateInnerStream))
            {
                using (BinaryWriter writer = new BinaryWriter(compressor, StringUtils.UTF8_WITHOUT_BOM, true))
                {
                    var allObjects = input.GetAllObjects();
                    writer.Write(allObjects.Count);
                    foreach (var obj in allObjects)
                    {
                        writer.Write(obj.Key);
                        writer.Write(obj.Value.Length);
                        writer.Write(obj.Value);
                    }
                }

                return (int)compressor.Length;
            }
        }
    }
}
