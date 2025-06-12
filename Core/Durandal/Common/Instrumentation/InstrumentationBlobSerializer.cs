using Durandal.API;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Serializes instrumentation events into a custom binary format
    /// </summary>
    public class InstrumentationBlobSerializer : IByteConverter<InstrumentationEventList>
    {
        // Uniquely identify this type of binary data
        // Bond blobs will write 0x0A2B
        private const short MAGIC_NUMBER = 0x5FA7;

        public InstrumentationEventList Decode(Stream input, int length)
        {
            InstrumentationEventList returnVal = new InstrumentationEventList();

            using (BinaryReader reader = new BinaryReader(input, StringUtils.UTF8_WITHOUT_BOM, true))
            {
                short magic = reader.ReadInt16();
                if (magic != MAGIC_NUMBER)
                {
                    throw new InvalidDataException("Instrumentation blob is not in the expected format");
                }

                int numEvents = reader.ReadInt32();
                for (int c = 0; c < numEvents; c++)
                {
                    InstrumentationEvent e = new InstrumentationEvent();
                    e.Component = reader.ReadString();
                    e.Level = reader.ReadInt16();
                    e.Message = reader.ReadString();
                    e.Timestamp = reader.ReadInt64();
                    e.TraceId = reader.ReadString();
                    returnVal.Events.Add(e);
                }
            }

            return returnVal;
        }

        public InstrumentationEventList Decode(byte[] input, int offset, int length)
        {
            using (MemoryStream stream = new MemoryStream(input, offset, length, false))
            {
                return Decode(stream, length);
            }
        }

        public byte[] Encode(InstrumentationEventList input)
        {
            using (RecyclableMemoryStream stream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (BinaryWriter writer = new BinaryWriter(stream, StringUtils.UTF8_WITHOUT_BOM, true))
                {
                    writer.Write(MAGIC_NUMBER);
                    writer.Write(input.Events.Count);
                    foreach(InstrumentationEvent e in input.Events)
                    {
                        writer.Write(e.Component);
                        writer.Write(e.Level);
                        writer.Write(e.Message);
                        writer.Write(e.Timestamp);
                        writer.Write(e.TraceId);
                    }
                }

                return stream.ToArray();
            }
        }

        public int Encode(InstrumentationEventList input, Stream target)
        {
            int startPos = (int)target.Length;
            using (BinaryWriter writer = new BinaryWriter(target, StringUtils.UTF8_WITHOUT_BOM, true))
            {
                writer.Write(MAGIC_NUMBER);
                writer.Write(input.Events.Count);
                foreach (InstrumentationEvent e in input.Events)
                {
                    writer.Write(e.Component);
                    writer.Write(e.Level);
                    writer.Write(e.Message);
                    writer.Write(e.Timestamp);
                    writer.Write(e.TraceId);
                }

                return (int)target.Length - startPos;
            }
        }
    }
}
