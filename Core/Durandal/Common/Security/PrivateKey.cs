namespace Durandal.Common.Security
{
    using System;
    using System.IO;
    using System.Text;
    using System.Xml;

    using Durandal.Common.File;
    using System.Text.RegularExpressions;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Durandal.Common.MathExt;
    using Durandal.Common.IO;
    using Durandal.Common.Utils;

    /// <summary>
    /// Represents a private key in the RSA schema (the D value is the secret key)
    /// </summary>
    [JsonConverter(typeof(LocalJsonSerializer))]
    public class PrivateKey : PublicKey
    {
        /// <summary>
        /// The secret key value
        /// </summary>
        public BigInteger D;

        /// <summary>
        /// Prime #1 used in generation
        /// </summary>
        public BigInteger P;

        /// <summary>
        /// Prime #2 used in generation
        /// </summary>
        public BigInteger Q;

        /// <summary>
        /// Prime factor #1 used to speed decryption
        /// </summary>
        public BigInteger DP;

        /// <summary>
        /// Prime factor #2 used to speed decryption
        /// </summary>
        public BigInteger DQ;

        /// <summary>
        /// Inverse prime factor used to speed decryption
        /// </summary>
        public BigInteger InvQ;

        /// <summary>
        /// Creates a private key with the specified parameter values
        /// </summary>
        /// <param name="d">The secret exponent</param>
        /// <param name="e">The public exponent (usually 65537)</param>
        /// <param name="n">The public modulus value</param>
        /// <param name="p"></param>
        /// <param name="q"></param>
        /// <param name="dp"></param>
        /// <param name="dq"></param>
        /// <param name="iq"></param>
        /// <param name="keyLengthBits">The length of the key</param>
        public PrivateKey(BigInteger d, BigInteger e, BigInteger n, BigInteger p, BigInteger q, BigInteger dp, BigInteger dq, BigInteger iq, int keyLengthBits) : base(e, n, keyLengthBits)
        {
            D = d;
            Q = q;
            P = p;
            DP = dp;
            DQ = dq;
            InvQ = iq;
        }

        /// <summary>
        /// Exports the public parameters of this key as a PublicKey object
        /// </summary>
        /// <returns></returns>
        public PublicKey GetPublicKey()
        {
            return new PublicKey(E, N, KeyLengthBits);
        }

        public new string WriteToXml()
        {
            using (PooledStringBuilder sb = StringBuilderPool.Rent())
            {
                sb.Builder.Append("<rsa_private_key D=\"");
                CryptographyHelpers.SerializeKey(D, sb.Builder);
                sb.Builder.Append("\" E=\"");
                CryptographyHelpers.SerializeKey(E, sb.Builder);
                sb.Builder.Append("\" N=\"");
                CryptographyHelpers.SerializeKey(N, sb.Builder);
                sb.Builder.Append("\" P=\"");
                CryptographyHelpers.SerializeKey(P, sb.Builder);
                sb.Builder.Append("\" Q=\"");
                CryptographyHelpers.SerializeKey(Q, sb.Builder);
                sb.Builder.Append("\" DP=\"");
                CryptographyHelpers.SerializeKey(DP, sb.Builder);
                sb.Builder.Append("\" DQ=\"");
                CryptographyHelpers.SerializeKey(DQ, sb.Builder);
                sb.Builder.Append("\" IQ=\"");
                CryptographyHelpers.SerializeKey(InvQ, sb.Builder);
                sb.Builder.Append("\" Length=\"");
                sb.Builder.Append(KeyLengthBits);
                sb.Builder.Append("\"/>");
                return sb.Builder.ToString();
            }
        }

        public new bool WriteToFile(VirtualPath fileName, IFileSystem fileSystem)
        {
            using (Stream baseStream = fileSystem.OpenStream(fileName, FileOpenMode.Create, FileAccessMode.Write))
            {
                if (baseStream == null)
                    return false;

                using (Utf8StreamWriter writer = new Utf8StreamWriter(baseStream))
                {
                    writer.Write(WriteToXml());
                    return true;
                }
            }
        }

        public async Task<bool> WriteToFileAsync(VirtualPath fileName, IFileSystem fileSystem)
        {
            using (Stream baseStream = await fileSystem.OpenStreamAsync(fileName, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false))
            {
                if (baseStream == null)
                    return false;

                using (Utf8StreamWriter writer = new Utf8StreamWriter(baseStream))
                {
                    writer.Write(WriteToXml());
                    return true;
                }
            }
        }

        private static readonly Regex attributeExtractor = new Regex("([a-z]+)=\"([abcdef0-9]+)\"", RegexOptions.IgnoreCase);

        public new static PrivateKey ReadFromXml(string doc)
        {
            if (string.IsNullOrEmpty(doc))
            {
                return null;
            }

            MatchCollection matches = attributeExtractor.Matches(doc);

            IDictionary<string, string> attributeDict = new Dictionary<string, string>();
            foreach (Match m in matches)
            {
                attributeDict.Add(m.Groups[1].Value.ToLower(), m.Groups[2].Value);
            }

            if (!attributeDict.ContainsKey("d") ||
                !attributeDict.ContainsKey("e") ||
                !attributeDict.ContainsKey("n") ||
                !attributeDict.ContainsKey("p") ||
                !attributeDict.ContainsKey("q") ||
                !attributeDict.ContainsKey("dp") ||
                !attributeDict.ContainsKey("dq") ||
                !attributeDict.ContainsKey("iq") ||
                !attributeDict.ContainsKey("length"))
            {
                return null;
            }

            PrivateKey returnVal = new PrivateKey(
                TryGetKeyAttribute(attributeDict, "d"),
                TryGetKeyAttribute(attributeDict, "e"),
                TryGetKeyAttribute(attributeDict, "n"),
                TryGetKeyAttribute(attributeDict, "p"),
                TryGetKeyAttribute(attributeDict, "q"),
                TryGetKeyAttribute(attributeDict, "dp"),
                TryGetKeyAttribute(attributeDict, "dq"),
                TryGetKeyAttribute(attributeDict, "iq"),
                TryGetKeyLengthAttribute(attributeDict, "length"));

            return returnVal;
        }

        private static BigInteger TryGetKeyAttribute(IDictionary<string, string> dict, string key)
        {
            if (dict.ContainsKey(key))
            {
                return CryptographyHelpers.DeserializeKey(dict[key]);
            }

            return null;
        }

        private static int TryGetKeyLengthAttribute(IDictionary<string, string> dict, string key)
        {
            int returnVal;
            if (dict.ContainsKey(key) && int.TryParse(dict[key], out returnVal))
            {
                return returnVal;
            }

            return 0;
        }

        public new static PrivateKey ReadFromFile(VirtualPath fileName, IFileSystem fileSystem)
        {
            using (Stream baseStream = fileSystem.OpenStream(fileName, FileOpenMode.Open, FileAccessMode.Read))
            {
                using (RecyclableMemoryStream bucket = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                {
                    baseStream.CopyTo(bucket);
                    string entireFile = Encoding.UTF8.GetString(bucket.ToArray(), 0, (int)bucket.Length);
                    return ReadFromXml(entireFile);
                }
            }
        }

        public static async Task<PrivateKey> ReadFromFileAsync(VirtualPath fileName, IFileSystem fileSystem)
        {
            using (Stream baseStream = await fileSystem.OpenStreamAsync(fileName, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
            {
                using (RecyclableMemoryStream bucket = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                {
                    await baseStream.CopyToAsync(bucket).ConfigureAwait(false);
                    string entireFile = Encoding.UTF8.GetString(bucket.ToArray(), 0, (int)bucket.Length);
                    return ReadFromXml(entireFile);
                }
            }
        }

        private class LocalJsonSerializer : JsonConverter
        {
            private static readonly Type _type = typeof(PrivateKey);

            public override bool CanConvert(Type objectType)
            {
                return _type.Equals(objectType);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                string nextString = reader.Value as string;
                PrivateKey data = null;

                if (nextString != null)
                {
                    data = PrivateKey.ReadFromXml(nextString);
                }

                if (_type.Equals(objectType))
                {
                    return data;
                }
                else
                {
                    throw new InvalidCastException("Cannot deserialize XML private key to " + objectType.ToString());
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                PrivateKey castValue = value as PrivateKey;
                if (castValue == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    string encoded = castValue.WriteToXml();
                    writer.WriteValue(encoded);
                }
            }
        }
    }
}
