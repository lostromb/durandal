using Durandal.API;
using Durandal.Common.Instrumentation;
using Durandal.Common.Security;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Collections;
using Durandal.Common.Tasks;
using System.Threading;
using Durandal.Common.Logger;

namespace Durandal.Common.Instrumentation
{
    public class RsaStringEncrypterPii : IStringEncrypterPii
    {
        // String encrypted with this scheme have the format:
        // "!ENCR|pii_rsa2|{8 bytes hex RSA key thumbprint}|{8 bytes hex AES key thumbprint}|{AES key (16 byte), encypted with RSA to RSA key size, in hex)|IV (16 byte hex, unique per message)|{Base64-encoded ciphertext}"

        private static readonly string CIPHERTEXT_PREFIX = CommonInstrumentation.Encrypted_Message_Prefix + "pii_rsa2";
        private static readonly TimeSpan TRANSIENT_KEY_LIFETIME = TimeSpan.FromMinutes(5);
        private static readonly TaskFactory TASK_FACTORY = new TaskFactory();

        private const int KEY_SIZE_BYTES = 16; // AES128
        private const int BLOCK_SIZE_BYTES = 16;

        private readonly FastRandom _fastRandom = new FastRandom(); // used for fast IV generation
        private readonly IRandom _srand; // secure random used for AES key generation
        private readonly IRSADelegates _rsaImpl;
        private readonly IAESDelegates _aesImpl;
        private readonly PublicKey _encryptionKey;
        private readonly KeyThumbprint _rsaKeyThumbprint;
        private readonly IRealTimeProvider _realTime;
        private readonly DataPrivacyClassification _privacyClassesToEncrypt;
        private readonly object _lock = new object();

        private Task _transientKeyUpdateTask;
        private DateTimeOffset _lastTransientKeyGenerationTime;
        private byte[] _aesKey;
        private string _encryptedMessageHeader;

        public RsaStringEncrypterPii(
             IRSADelegates rsaImpl,
             IAESDelegates aesImpl,
             IRandom secureRandom,
             IRealTimeProvider realTime,
             DataPrivacyClassification privacyClassesToEncrypt,
             PublicKey encryptionKey)
        {
            if (encryptionKey == null)
            {
                throw new ArgumentNullException(nameof(encryptionKey));
            }

            _aesImpl = aesImpl;
            _rsaImpl = rsaImpl;
            _srand = secureRandom;
            _realTime = realTime;
            _encryptionKey = encryptionKey;
            _privacyClassesToEncrypt = privacyClassesToEncrypt;
            _rsaKeyThumbprint = KeyThumbprint.FromRsaKey(_encryptionKey);
            _lastTransientKeyGenerationTime = _realTime.Time;
            GenerateNewTransientKey();
        }
        
        /// <inheritdoc />
        public void EncryptString(StringBuilder inputBuffer, StringBuilder outputBuffer)
        {
            string messageHeader;
            byte[] aesKey;

            // Try and minimize lock use by just capturing the current state of the AES key and background update task.
            // Even if the key changes halfway through this method, it will still use the old key we cache here.
            lock (_lock)
            {
                messageHeader = _encryptedMessageHeader;
                aesKey = _aesKey;

                // Has our transient key expired? If so, queue up a background worker task to update it.
                // Don't do it on the hot path because it requires slow RSA processing.
                if (_lastTransientKeyGenerationTime + TRANSIENT_KEY_LIFETIME < _realTime.Time)
                {
                    // If a previous transient key update failed, propagate the exception up
                    if (_transientKeyUpdateTask != null && _transientKeyUpdateTask.IsFinished())
                    {
                        if (_transientKeyUpdateTask.IsFaulted)
                        {
                            ExceptionDispatchInfo.Capture(_transientKeyUpdateTask.Exception).Throw();
                        }

                        _transientKeyUpdateTask = null;
                    }

                    // Make sure we don't queue a bunch of update tasks at once. The task will set itself to null when it's done
                    if (_transientKeyUpdateTask == null)
                    {
                        _lastTransientKeyGenerationTime = _realTime.Time;
                        _transientKeyUpdateTask = TASK_FACTORY.StartNew(GenerateNewTransientKey);
                    }
                }
            }

            // Generate a random IV for this message
            byte[] iv = new byte[BLOCK_SIZE_BYTES];
            _fastRandom.NextBytes(iv);
            using (RecyclableMemoryStream buffer = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            using (FinalizableStream cryptoStream = _aesImpl.CreateEncryptionStream(buffer, _aesKey, iv))
            {
                using (StringBuilderReadStream inputReader = new StringBuilderReadStream(inputBuffer, StringUtils.UTF8_WITHOUT_BOM))
                {
                    inputReader.CopyToPooled(cryptoStream);
                }

                cryptoStream.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                outputBuffer.Append(messageHeader);
                BinaryHelpers.ToHexString(iv, 0, BLOCK_SIZE_BYTES, outputBuffer);
                outputBuffer.Append('|');

                buffer.Seek(0, SeekOrigin.Begin);
                BinaryHelpers.EncodeBase64ToStringBuilder(buffer, outputBuffer);
            }
        }

        /// <inheritdoc />
        public string EncryptString(string plaintext)
        {
            string messageHeader;
            byte[] aesKey;

            // Try and minimize lock use by just capturing the current state of the AES key and background update task.
            // Even if the key changes halfway through this method, it will still use the old key we cache here.
            lock (_lock)
            {
                messageHeader = _encryptedMessageHeader;
                aesKey = _aesKey;

                // Has our transient key expired? If so, queue up a background worker task to update it.
                // Don't do it on the hot path because it requires slow RSA processing.
                if (_lastTransientKeyGenerationTime + TRANSIENT_KEY_LIFETIME < _realTime.Time)
                {
                    // If a previous transient key update failed, propagate the exception up
                    if (_transientKeyUpdateTask != null && _transientKeyUpdateTask.IsFinished())
                    {
                        if (_transientKeyUpdateTask.IsFaulted)
                        {
                            ExceptionDispatchInfo.Capture(_transientKeyUpdateTask.Exception).Throw();
                        }

                        _transientKeyUpdateTask = null;
                    }

                    // Make sure we don't queue a bunch of update tasks at once. The task will set itself to null when it's done
                    if (_transientKeyUpdateTask == null)
                    {
                        _lastTransientKeyGenerationTime = _realTime.Time;
                        _transientKeyUpdateTask = TASK_FACTORY.StartNew(GenerateNewTransientKey);
                    }
                }
            }

            // Generate a random IV for this message
            byte[] iv = new byte[BLOCK_SIZE_BYTES];
            _fastRandom.NextBytes(iv);
            using (RecyclableMemoryStream buffer = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            using (FinalizableStream cryptoStream = _aesImpl.CreateEncryptionStream(buffer, _aesKey, iv))
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                using (Utf8StreamWriter writer = new Utf8StreamWriter(cryptoStream, leaveOpen: true))
                {
                    writer.Write(plaintext);
                }

                cryptoStream.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                using (PooledBuffer<byte> payload = buffer.ToPooledBuffer())
                {
                    pooledSb.Builder.Append(messageHeader);
                    BinaryHelpers.ToHexString(iv, 0, BLOCK_SIZE_BYTES, pooledSb.Builder);
                    pooledSb.Builder.Append("|");
                    BinaryHelpers.EncodeBase64ToStringBuilder(payload.Buffer, 0, (int)buffer.Length, pooledSb.Builder);
                    return pooledSb.Builder.ToString();
                }
            }
        }

        /// <inheritdoc />
        public bool EncryptionRequired(DataPrivacyClassification privacyClass)
        {
            return (_privacyClassesToEncrypt & privacyClass) != 0;
        }

        private void GenerateNewTransientKey()
        {
            // Generate a random AES key and IV
            byte[] newAesKey = new byte[KEY_SIZE_BYTES];
            _srand.NextBytes(newAesKey);

            KeyThumbprint newKeyThumbprint = KeyThumbprint.FromAesKey(newAesKey, _aesImpl);

            // Encrypt the key + IV using one-way RSA encryption
            byte[] plainKeyBytes = new byte[(_encryptionKey.KeyLengthBits / 8) - 8];
            _srand.NextBytes(plainKeyBytes); // embed it inside a field of random data
            ArrayExtensions.MemCopy(newAesKey, 0, plainKeyBytes, 0, KEY_SIZE_BYTES);
            BigInteger plainKeyInteger = new BigInteger(plainKeyBytes);
            BigInteger encryptedKeyInteger = _rsaImpl.Decrypt(plainKeyInteger, _encryptionKey);

            // Prebuild the encrypted message header because it never changes
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder headerBuf = pooledSb.Builder;
                headerBuf.Append(CIPHERTEXT_PREFIX);
                headerBuf.Append("|");
                byte[] thumbprint = _rsaKeyThumbprint.ToBytes();
                BinaryHelpers.ToHexString(thumbprint, 0, thumbprint.Length, headerBuf);
                headerBuf.Append("|");
                thumbprint = newKeyThumbprint.ToBytes();
                BinaryHelpers.ToHexString(thumbprint, 0, thumbprint.Length, headerBuf);
                headerBuf.Append("|");
                CryptographyHelpers.SerializeKey(encryptedKeyInteger, headerBuf);
                headerBuf.Append("|");

                lock (_lock)
                {
                    _encryptedMessageHeader = headerBuf.ToString();
                    _aesKey = newAesKey;
                    _transientKeyUpdateTask = null;
                }
            }
        }
    }
}
