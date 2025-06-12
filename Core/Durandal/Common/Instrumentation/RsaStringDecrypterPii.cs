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
using Durandal.Common.Cache;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using Durandal.Common.Collections;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Class for decrypting encoded PII messages asymmetrically using RSA
    /// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public class RsaStringDecrypterPii : IStringDecrypterPii
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        // String encrypted with this scheme have the format:
        // "pii_rsa2|{8 bytes RSA key thumbprint}|{8 bytes hex AES key thumbprint}|{AES key (16 byte), encypted with RSA to RSA key size, in hex)|IV (16 bytes)|{Base64-encoded ciphertext}"

        protected static readonly string CIPHERTEXT_PREFIX = CommonInstrumentation.Encrypted_Message_Prefix + "pii_rsa2";

        private readonly InMemoryCache<byte[]> _transientKeyCache;
        private readonly IDictionary<KeyThumbprint, PrivateKey> _decryptionKeys;
        private readonly IRSADelegates _rsaImpl;
        private readonly IAESDelegates _aesImpl;
        private readonly IRealTimeProvider _realTime;

        /// <summary>
        /// Creates an RSA/AES string decrypter
        /// </summary>
        /// <param name="rsaImpl"></param>
        /// <param name="aesImpl"></param>
        /// <param name="decryptionKeys"></param>
        /// <param name="realTime"></param>
        public RsaStringDecrypterPii(
            IRSADelegates rsaImpl,
            IAESDelegates aesImpl,
            IEnumerable<PrivateKey> decryptionKeys,
            IRealTimeProvider realTime)
        {
            _rsaImpl = rsaImpl;
            _aesImpl = aesImpl;
            _decryptionKeys = new Dictionary<KeyThumbprint, PrivateKey>();
            if (decryptionKeys != null)
            {
                foreach (PrivateKey key in decryptionKeys)
                {
                    _decryptionKeys[KeyThumbprint.FromRsaKey(key)] = key;
                }
            }

            _transientKeyCache = new InMemoryCache<byte[]>();
            _realTime = realTime;
        }

        public bool TryDecryptString(string cipherText, out string plainText)
        {
            plainText = null;
            
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

            KeyThumbprint rsaKeyThumbprint = new KeyThumbprint(BinaryHelpers.FromHexString(cipherText, splitStart, splitEnd - splitStart));
            PrivateKey decryptionKey;
            if (!_decryptionKeys.TryGetValue(rsaKeyThumbprint, out decryptionKey))
            {
                return false;
            }

            splitStart = splitEnd + 1;
            splitEnd = cipherText.IndexOf('|', splitStart);
            if (splitEnd < 0)
            {
                return false;
            }

            KeyThumbprint aesKeyThumbprint = new KeyThumbprint(BinaryHelpers.FromHexString(cipherText, splitStart, splitEnd - splitStart));
            string transientKeyThumbprintString = aesKeyThumbprint.ToString();

            splitStart = splitEnd + 1;
            splitEnd = cipherText.IndexOf('|', splitStart);
            if (splitEnd < 0)
            {
                return false;
            }

            // See if we have seen this transient key before. If not, decrypt the key bytes using RSA and add it to the cache
            byte[] aesState;
            RetrieveResult<byte[]> cacheResult = _transientKeyCache.TryRetrieveTentative(transientKeyThumbprintString, _realTime);
            if (cacheResult.Success)
            {
                aesState = cacheResult.Result;
            }
            else
            {
                BigInteger encryptedKeyInteger = CryptographyHelpers.DeserializeKey(cipherText.Substring(splitStart, splitEnd - splitStart));
                BigInteger plainKeyInteger = _rsaImpl.Encrypt(encryptedKeyInteger, decryptionKey);

                // Only the first 16 bytes of this value contain actual key data. The rest is random padding
                byte[] plainKeyBytes = plainKeyInteger.GetBytes();

                aesState = new byte[16];
                ArrayExtensions.MemCopy(plainKeyBytes, 0, aesState, 0, 16);
                _transientKeyCache.Store(transientKeyThumbprintString, aesState, null, TimeSpan.FromMinutes(15), false, NullLogger.Singleton, _realTime);
            }

            // Parse the IV
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
            using (FinalizableStream cryptoStream = _aesImpl.CreateDecryptionStream(base64Decoder, aesState, iv))
            using (StreamReader reader = new StreamReader(cryptoStream, StringUtils.UTF8_WITHOUT_BOM))
            {
                plainText = reader.ReadToEnd();
            }

            return true;
        }
    }
}
