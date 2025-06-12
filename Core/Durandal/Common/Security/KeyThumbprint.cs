using Durandal.Common.IO;
using Durandal.Common.IO.Hashing;
using Durandal.Common.Security;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.Security
{
    public struct KeyThumbprint : IEquatable<KeyThumbprint>
    {
        public uint A;
        public uint B;

        public KeyThumbprint(byte[] bytes)
        {
            if (bytes.Length < 8)
            {
                throw new ArgumentNullException("Key thumbprint must be at least 8 bytes long");
            }

            A = BinaryHelpers.ByteArrayToUInt32LittleEndian(bytes, 0);
            B = BinaryHelpers.ByteArrayToUInt32LittleEndian(bytes, 4);
        }

        public byte[] ToBytes()
        {
            byte[] returnVal = new byte[8];
            BinaryHelpers.UInt32ToByteArrayLittleEndian(A, returnVal, 0);
            BinaryHelpers.UInt32ToByteArrayLittleEndian(B, returnVal, 4);
            return returnVal;
        }

        public override string ToString()
        {
            return BinaryHelpers.ToHexString(ToBytes());
        }

        public override int GetHashCode()
        {
            return (int)A;
        }

        public static KeyThumbprint FromAesKey(byte[] key, IAESDelegates aes)
        {
            // Encrypt the key with itself and an IV of zeroes
            byte[] IV = new byte[key.Length];

            using (RecyclableMemoryStream buffer = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (Stream encrypter = aes.CreateEncryptionStream(buffer, key, IV))
                {
                    encrypter.Write(key, 0, key.Length);
                }

                buffer.Seek(0, SeekOrigin.Begin);

                // And then hash + truncate the encryption
                SHA256 hash = new SHA256();
                byte[] hashedEncryption = hash.ComputeHash(buffer);
                return new KeyThumbprint(hashedEncryption);
            }
        }

        public static KeyThumbprint FromRsaKey(PublicKey key)
        {
            // Hash the N modulo value to produce a thumbprint
            SHA256 hash = new SHA256();
            byte[] hashedEncryption = hash.ComputeHash(key.N.GetBytes());
            return new KeyThumbprint(hashedEncryption);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is KeyThumbprint))
            {
                return false;
            }

            KeyThumbprint other = (KeyThumbprint)obj;
            return Equals(other);
        }

        public bool Equals(KeyThumbprint other)
        {
            return A == other.A &&
                   B == other.B;
        }

        public static bool operator ==(KeyThumbprint left, KeyThumbprint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KeyThumbprint left, KeyThumbprint right)
        {
            return !(left == right);
        }
    }
}
