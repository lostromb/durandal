using Durandal.API;
using Durandal.Common.Compression;
using Durandal.Common.Compression.LZ4;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Instrumentation
{
    public class InstrumentationBlob
    {
        private InstrumentationEventList _bondData;

        public InstrumentationBlob()
        {
            _bondData = new InstrumentationEventList();
        }

        private InstrumentationBlob(InstrumentationEventList data)
        {
            _bondData = data;
        }

        public int Count
        {
            get
            {
                return _bondData.Events.Count;
            }
        }

        public void AddEvent(LogEvent e)
        {
            _bondData.Events.Add(InstrumentationEvent.FromLogEvent(e));
        }

        public void AddEvents(IEnumerable<LogEvent> e)
        {
            foreach (LogEvent e2 in e)
            {
                AddEvent(e2);
            }
        }

        public void ReadEventsTo(IList<LogEvent> destination)
        {
            foreach (InstrumentationEvent e in _bondData.Events)
            {
                destination.Add(e.ToLogEvent());
            }
        }

        public List<LogEvent> GetEvents()
        {
            List<LogEvent> returnVal = new List<LogEvent>();
            ReadEventsTo(returnVal);
            return returnVal;
        }
        
        public byte[] Compress(IByteConverter<InstrumentationEventList> serializer)
        {
            using (RecyclableMemoryStream outStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (LZ4Stream compressor = new LZ4Stream(outStream, LZ4StreamMode.Compress, LZ4StreamFlags.IsolateInnerStream, 32768))
                {
                    byte[] payload = serializer.Encode(_bondData);
                    compressor.Write(payload, 0, payload.Length);
                    compressor.Flush();
                    //Debug.WriteLine("Compression ratio is " + ((double)outStream.Position / payload.Length));
                }

                return outStream.ToArray();
            }
        }

        public Task<byte[]> CompressAsync(IByteConverter<InstrumentationEventList> serializer)
        {
            return Task.FromResult(Compress(serializer));
        }

        public static InstrumentationBlob Decompress(byte[] blob, IByteConverter<InstrumentationEventList> deserializer)
        {
            return Decompress(new ArraySegment<byte>(blob), deserializer);
        }

        public static InstrumentationBlob Decompress(ArraySegment<byte> blob, IByteConverter<InstrumentationEventList> deserializer)
        {
            byte[] decompressed;
            using (MemoryStream compressedStream = new MemoryStream(blob.Array, blob.Offset, blob.Count, false))
            {
                using (RecyclableMemoryStream decompressedStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                {
                    using (LZ4Stream decompressor = new LZ4Stream(compressedStream, LZ4StreamMode.Decompress, LZ4StreamFlags.IsolateInnerStream))
                    {
                        decompressor.CopyTo(decompressedStream);
                        decompressor.Flush();
                    }

                    decompressed = decompressedStream.ToArray();
                }
            }

            InstrumentationEventList eventList = deserializer.Decode(decompressed, 0, decompressed.Length);
            return new InstrumentationBlob(eventList);
        }

        public Task<InstrumentationBlob> DecompressAsync(byte[] blob, IByteConverter<InstrumentationEventList> deserializer)
        {
            return Task.FromResult(Decompress(blob, deserializer));
        }
    }
}
