using Durandal.Common.Security;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.IO;
using Durandal.Common.Instrumentation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Durandal.Common.Utils;
using Durandal.API;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Class for decrypting encoded PII messages
    /// </summary>
    public class AesStringDecrypterPii : IStringDecrypterPii
    {
        private static readonly string CIPHERTEXT_PREFIX = CommonInstrumentation.Encrypted_Message_Prefix + "pii_aes1";
        
        private readonly IDictionary<KeyThumbprint, byte[]> _decryptionKeys;
        private readonly IAESDelegates _aes;
        
        /// <summary>
        /// Creates an AES string decrypter
        /// </summary>
        /// <param name="aes"></param>
        /// <param name="decryptionKeys"></param>
        public AesStringDecrypterPii(IAESDelegates aes, IEnumerable<byte[]> decryptionKeys)
        {
            _aes = aes;
            _decryptionKeys = new Dictionary<KeyThumbprint, byte[]>();
            if (decryptionKeys != null)
            {
                foreach (byte[] key in decryptionKeys)
                {
                    _decryptionKeys[KeyThumbprint.FromAesKey(key, _aes)] = key;
                }
            }
        }

        public bool TryDecryptString(string cipherText, out string plainText)
        {
            plainText = null;

            try
            {
                // Split encryption scheme, key hash, IV, and payload
                if (!cipherText.StartsWith(CIPHERTEXT_PREFIX, StringComparison.Ordinal))
                {
                    return false;
                }

                // Split encryption scheme, key thumbprint, encrypted key, and payload
                int splitStart = cipherText.IndexOf('|', CIPHERTEXT_PREFIX.Length) + 1;
                int splitEnd = cipherText.IndexOf('|', splitStart);
                if (splitEnd < 0)
                {
                    return false;
                }

                KeyThumbprint keyThumbprint = new KeyThumbprint(BinaryHelpers.FromHexString(cipherText, splitStart, splitEnd - splitStart));
                byte[] aesKey;
                if (!_decryptionKeys.TryGetValue(keyThumbprint, out aesKey))
                {
                    return false;
                }

                splitStart = splitEnd + 1;
                splitEnd = cipherText.IndexOf('|', splitStart);
                if (splitEnd < 0)
                {
                    return false;
                }

                byte[] iv = BinaryHelpers.FromHexString(cipherText, splitStart, splitEnd - splitStart);

                splitStart = splitEnd + 1;
                splitEnd = cipherText.Length;

                // Then finally we have everything to decrypt the ciphertext
                using (StringStream stringReader = new StringStream(cipherText, splitStart, splitEnd - splitStart, StringUtils.UTF8_WITHOUT_BOM))
                using (Base64AsciiDecodingStream base64Decoder = new Base64AsciiDecodingStream(stringReader, StreamDirection.Read, false))
                using (FinalizableStream cryptoStream = _aes.CreateDecryptionStream(base64Decoder, aesKey, iv))
                using (StreamReader reader = new StreamReader(cryptoStream, StringUtils.UTF8_WITHOUT_BOM))
                {
                    plainText = reader.ReadToEnd();
                }

                return true;
            }
            catch (Exception) { }

            return false;
        }
    }
}
