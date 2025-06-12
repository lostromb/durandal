using Durandal.Common.Utils;
using Durandal.Common.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Compression.LZ4;

namespace Durandal.Common.Collections.Indexing
{
    /// <summary>
    /// Compressed memory paging system which uses LZ4 to compress memory
    /// </summary>
    public class LZ4CompressedMemoryPageStorage : CachedTransformMemoryPageStorage
    {
        private bool _highCompression;

        public LZ4CompressedMemoryPageStorage(bool highCompression = false, int pageCacheSize = 20) : base(pageCacheSize)
        {
            _highCompression = highCompression;
        }

        protected override byte[] Compress(byte[] input)
        {
            byte[] returnVal;
            using (MemoryStream inputStream = new MemoryStream(input, false)) // fixme use recyclable memory streams for better perf
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    using (LZ4Stream compressor = new LZ4Stream(outputStream, LZ4StreamMode.Compress, _highCompression ? LZ4StreamFlags.HighCompression : LZ4StreamFlags.Default, input.Length * 2))
                    {
                        inputStream.CopyTo(compressor);
                        inputStream.Flush();
                        compressor.Flush();
                        inputStream.Dispose();
                        compressor.Dispose();
                        outputStream.Dispose();
                    }

                    returnVal = outputStream.ToArray();
                }
            }

            return returnVal;
        }

        protected override byte[] Decompress(byte[] compressed)
        {
            byte[] returnVal;
            using (MemoryStream inputStream = new MemoryStream(compressed, false))
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    using (LZ4Stream decompressor = new LZ4Stream(inputStream, LZ4StreamMode.Decompress))
                    {
                        decompressor.CopyTo(outputStream);
                        inputStream.Flush();
                        decompressor.Flush();
                        inputStream.Dispose();
                        decompressor.Dispose();
                        outputStream.Dispose();
                    }

                    returnVal = outputStream.ToArray();
                }
            }

            return returnVal;
        }
    }
}
