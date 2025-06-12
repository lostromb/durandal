using System;
using System.IO;
using System.Text;

namespace Durandal.Common.IO
{
    /// <summary>
    /// An encoder that converts between string and bytes using UTF8 encoding.
    /// </summary>
    public class StringByteConverter : IByteConverter<string>
    {
        public byte[] Encode(string input)
        {
            if (input == null)
            {
                return null;
            }

            return Encoding.UTF8.GetBytes(input);
        }

        public int Encode(string input, Stream target)
        {
            if (input == null)
            {
                return 0;
            }

            byte[] data = Encoding.UTF8.GetBytes(input);
            target.Write(data, 0, data.Length);
            return data.Length;
        }

        public string Decode(byte[] input, int offset, int length)
        {
            if (input == null)
            {
                return null;
            }

            return Encoding.UTF8.GetString(input, offset, length);
        }

        public string Decode(Stream input, int length)
        {
            if (length == 0)
            {
                return string.Empty;
            }

            byte[] data = new byte[length];
            input.Read(data, 0, length);
            return Encoding.UTF8.GetString(data, 0, length);
        }
    }
}
