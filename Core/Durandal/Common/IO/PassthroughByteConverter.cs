using Durandal.Common.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.IO
{
    public class PassthroughByteConverter : IByteConverter<byte[]>
    {
        public byte[] Decode(Stream input, int length)
        {
            byte[] block = new byte[length];
            input.Read(block, 0, length);
            return block;
        }

        public byte[] Decode(byte[] input, int offset, int length)
        {
            byte[] returnVal = new byte[length];
            ArrayExtensions.MemCopy(input, offset, returnVal, 0, length);
            return returnVal;
        }

        public byte[] Encode(byte[] input)
        {
            return input;
        }

        public int Encode(byte[] input, Stream target)
        {
            target.Write(input, 0, input.Length);
            return input.Length;
        }
    }
}
