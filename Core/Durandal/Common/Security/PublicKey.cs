namespace Durandal.Common.Security
{
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.MathExt;
    using Durandal.Common.Utils;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;

    /// <summary>
    /// Represents a public key in the RSA schema
    /// </summary>
    [JsonConverter(typeof(LocalJsonSerializer))]
    public class PublicKey
    {
        /// <summary>
        /// The public exponent (usually 65537)
        /// </summary>
        public BigInteger E;

        /// <summary>
        /// The modulus value
        /// </summary>
        public BigInteger N;

        public int KeyLengthBits;

        /// <summary>
        /// Creates a new public key with the specified exponent and modulus values
        /// </summary>
        /// <param name="e"></param>
        /// <param name="n"></param>
        /// <param name="keyLengthBits">The length of the key</param>
        public PublicKey(BigInteger e, BigInteger n, int keyLengthBits)
        {
            E = e;
            N = n;
            KeyLengthBits = keyLengthBits;
        }

        public string WriteToXml()
        {
            using (PooledStringBuilder sb = StringBuilderPool.Rent())
            {
                sb.Builder.Append("<rsa_public_key E=\"");
                CryptographyHelpers.SerializeKey(E, sb.Builder);
                sb.Builder.Append("\" N=\"");
                CryptographyHelpers.SerializeKey(N, sb.Builder);
                sb.Builder.Append("\" Length=\"");
                sb.Builder.Append(KeyLengthBits);
                sb.Builder.Append("\"/>");
                return sb.Builder.ToString();
            }
        }

        public bool WriteToFile(VirtualPath fileName, IFileSystem fileSystem)
        {
            using (NonRealTimeStream fileStream = fileSystem.OpenStream(fileName, FileOpenMode.Create, FileAccessMode.Write))
            using (Utf8StreamWriter writer = new Utf8StreamWriter(fileStream))
            {
                writer.Write(WriteToXml());
                writer.Dispose();
                return true;
            }
        }

        private static readonly Regex attributeExtractor = new Regex("([a-z]+)=\"([abcdef0-9]+)\"", RegexOptions.IgnoreCase);

        public static PublicKey ReadFromXml(string doc)
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

            if (!attributeDict.ContainsKey("e") || !attributeDict.ContainsKey("n"))
            {
                return null;
            }

            PublicKey returnVal = new PublicKey(
                TryGetKeyAttribute(attributeDict, "e"),
                TryGetKeyAttribute(attributeDict, "n"),
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

        public static PublicKey ReadFromFile(VirtualPath fileName, IFileSystem fileSystem)
        {
            return ReadFromXml(string.Join("", fileSystem.ReadLines(fileName)));
        }

        private class LocalJsonSerializer : JsonConverter
        {
            private static readonly Type _type = typeof(PublicKey);

            public override bool CanConvert(Type objectType)
            {
                return _type.Equals(objectType);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                string nextString = reader.Value as string;
                PublicKey data = null;

                if (nextString != null)
                {
                    data = PublicKey.ReadFromXml(nextString);
                }

                if (_type.Equals(objectType))
                {
                    return data;
                }
                else
                {
                    throw new InvalidCastException("Cannot deserialize XML public key to " + objectType.ToString());
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                PublicKey castValue = value as PublicKey;
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
