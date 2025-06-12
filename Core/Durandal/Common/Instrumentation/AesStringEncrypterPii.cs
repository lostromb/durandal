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
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.Logger;

namespace Durandal.Common.Instrumentation
{
    public class AesStringEncrypterPii : IStringEncrypterPii
    {
        // Strings encrypted with this scheme have the format:
        // "!ENCR|pii_aes1|{8 bytes hex key thumbprint}|{16 bytes hex IV}|{Base64-encoded ciphertext}"

        private static readonly string CIPHERTEXT_PREFIX = CommonInstrumentation.Encrypted_Message_Prefix + "pii_aes1";
        private const int BLOCK_SIZE_BYTES = 16;

        private readonly DataPrivacyClassification _privacyClassesToEncrypt;
        private readonly IRandom _srand;
        private readonly byte[] _encryptionKey;
        private readonly KeyThumbprint _encryptionKeyThumbprint;
        private readonly IAESDelegates _aes;

        /// <summary>
        /// Creates an AES string encrypter/decrypter
        /// </summary>
        /// <param name="aes">Implementation of cipher</param>
        /// <param name="random">Secure random for generating rolling keys</param>
        /// <param name="privacyClassesToEncrypt"></param>
        /// <param name="encryptionKey">The AES encryption key, with bit length equal to the desired strength of the cipher (128, 192, or 256).</param>
        public AesStringEncrypterPii(
            IAESDelegates aes,
            IRandom random,
            DataPrivacyClassification privacyClassesToEncrypt,
            byte[] encryptionKey)
        {
            encryptionKey.AssertNonNull(nameof(encryptionKey));
            if (encryptionKey.Length != 16 &&
                encryptionKey.Length != 24 &&
                encryptionKey.Length != 32)
            {
                throw new ArgumentException("AES key must be 128, 192, or 256 bits");
            }

            _aes = aes;
            _srand = random;
            _encryptionKey = encryptionKey;
            _privacyClassesToEncrypt = privacyClassesToEncrypt;
            _encryptionKeyThumbprint = KeyThumbprint.FromAesKey(_encryptionKey, _aes);
        }

        public void EncryptString(StringBuilder inputBuffer, StringBuilder outputBuffer)
        {
            byte[] IV = new byte[BLOCK_SIZE_BYTES];
            _srand.NextBytes(IV);

            using (RecyclableMemoryStream buffer = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            using (FinalizableStream cryptoStream = _aes.CreateEncryptionStream(buffer, _encryptionKey, IV))
            {
                using (StringBuilderReadStream reader = new StringBuilderReadStream(inputBuffer, StringUtils.UTF8_WITHOUT_BOM))
                {
                    reader.CopyToPooled(cryptoStream);
                }

                cryptoStream.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                buffer.Seek(0, SeekOrigin.Begin);

                outputBuffer.Append(CIPHERTEXT_PREFIX);
                outputBuffer.Append("|");
                outputBuffer.Append(BinaryHelpers.ToHexString(_encryptionKeyThumbprint.ToBytes()));
                outputBuffer.Append("|");
                outputBuffer.Append(BinaryHelpers.ToHexString(IV));
                outputBuffer.Append("|");
                BinaryHelpers.EncodeBase64ToStringBuilder(buffer, outputBuffer);
            }
        }

        public string EncryptString(string plaintext)
        {
            byte[] IV = new byte[BLOCK_SIZE_BYTES];
            _srand.NextBytes(IV);
            
            using (RecyclableMemoryStream buffer = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            using (FinalizableStream cryptoStream = _aes.CreateEncryptionStream(buffer, _encryptionKey, IV))
            {
                using (Utf8StreamWriter writer = new Utf8StreamWriter(cryptoStream, leaveOpen: true))
                {
                    writer.Write(plaintext);
                }

                cryptoStream.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                byte[] payload = buffer.ToArray();

                using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                {
                    StringBuilder outputBuf = pooledSb.Builder;
                    outputBuf.Append(CIPHERTEXT_PREFIX);
                    outputBuf.Append("|");
                    outputBuf.Append(BinaryHelpers.ToHexString(_encryptionKeyThumbprint.ToBytes()));
                    outputBuf.Append("|");
                    outputBuf.Append(BinaryHelpers.ToHexString(IV));
                    outputBuf.Append("|");
                    BinaryHelpers.EncodeBase64ToStringBuilder(payload, 0, payload.Length, outputBuf);
                    return outputBuf.ToString();
                }
            }
        }

        public bool EncryptionRequired(DataPrivacyClassification privacyClass)
        {
            return (_privacyClassesToEncrypt & privacyClass) != 0;
        }
    }
}
