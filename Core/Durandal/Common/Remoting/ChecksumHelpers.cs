using Durandal.Common.Compression;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting
{
    public static class ChecksumHelpers
    {
        public static uint CalculateChecksum(byte[] data)
        {
            PkZipCRC32 crc = new PkZipCRC32();
            for (int c = 0; c < data.Length; c++)
            {
                crc.UpdateCRC(data[c]);
            }

            return (uint)crc.Crc32Result;
        }
    }
}
