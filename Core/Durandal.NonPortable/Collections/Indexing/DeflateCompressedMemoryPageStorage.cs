using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Collections.Indexing
{
    /// <summary>
    /// Compressed memory paging system which uses DeflateStream to compress memory
    /// </summary>
    public class DeflateCompressedMemoryPageStorage : CachedTransformMemoryPageStorage
    {
        public DeflateCompressedMemoryPageStorage(int cacheSize = 20) : base(cacheSize)
        {
        }

        protected override byte[] Compress(byte[] input)
        {
            byte[] returnVal;
            using (MemoryStream inputStream = new MemoryStream(input, false))
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    using (DeflateStream compressor = new DeflateStream(outputStream, CompressionMode.Compress, true))
                    {
                        inputStream.CopyTo(compressor);
                        inputStream.Flush();
                        compressor.Flush();
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
                    using (DeflateStream compressor = new DeflateStream(inputStream, CompressionMode.Decompress))
                    {
                        compressor.CopyTo(outputStream);
                        inputStream.Flush();
                        compressor.Flush();
                        inputStream.Close();
                        compressor.Close();
                        outputStream.Close();
                    }
                    returnVal = outputStream.ToArray();
                }
            }
            return returnVal;
        }
    }
}
