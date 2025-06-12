using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Durandal.Common.IO;

namespace Durandal.Common.IO.Json
{
    public class JsonByteConverter<T> : IByteConverter<T> where T : class
    {
        public T Decode(Stream input, int length)
        {
            byte[] data = new byte[length];
            input.Read(data, 0, length);
            return Decode(data, 0, length);
        }

        public T Decode(byte[] input, int offset, int length)
        {
            string data = Encoding.UTF8.GetString(input, offset, length);
            return JsonConvert.DeserializeObject<T>(data);
        }

        public byte[] Encode(T input)
        {
            string data = JsonConvert.SerializeObject(input);
            return Encoding.UTF8.GetBytes(data);
        }

        public int Encode(T input, Stream target)
        {
            string data = JsonConvert.SerializeObject(input);
            byte[] blob = Encoding.UTF8.GetBytes(data);
            target.Write(blob, 0, blob.Length);
            return blob.Length;
        }
    }
}
