using System;
using System.Text;

namespace Durandal.Common.Security
{
    using Durandal.Common.Collections;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;

    /// <summary>
    /// Provides common cryptography functions for Durandal client/server authenticators
    /// </summary>
    public static class CryptographyHelpers
    {
        private static IRandom random = new FastRandom();
        private static readonly long epochTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        
        /// <summary>
        /// Generates a random large number, with the specified maximum value and bit length (default 512 bits)
        /// </summary>
        /// <param name="maxValue">Don't generate a number larger than this</param>
        /// <param name="bitLength">The number of bits in the generated token</param>
        /// <returns></returns>
        public static BigInteger GenerateRandomToken(BigInteger maxValue, int bitLength)
        {
            if (maxValue.BitCount() + 16 < bitLength)
            {
                throw new ArithmeticException("Intractable token generation detected - maximum value is too far below the maximum bit length");
            }

            BigInteger candidate;
            byte[] data = new byte[FastMath.Max(1, bitLength / 8)];

            do
            {
                for (int c = 0; c < data.Length; c++)
                {
                    data[c] = (byte)random.NextInt(1, 255);
                }

                candidate = new BigInteger(data);
            } while (candidate >= maxValue);

            return candidate;
        }
        
        public static byte[] GenerateNonZeroBytes(int bitLength)
        {
            byte[] data = new byte[FastMath.Max(1, bitLength / 8)];
            for (int c = 0; c < data.Length; c++)
            {
                data[c] = (byte)random.NextInt(1, 255);
            }

            return data;
        }

        /// <summary>
        /// Serializes a BigInteger value into a hex string ("51A0F4b3")
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string SerializeKey(BigInteger key)
        {
            return key.ToHexString();
        }

        public static void SerializeKey(BigInteger key, StringBuilder stringBuilder)
        {
            key.ToHexString(stringBuilder);
        }

        /// <summary>
        /// Deserializes a BigInteger value from a hex string ("51A0F4b3")
        /// If parsing fails, this method will throw an ArithmeticException
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static BigInteger DeserializeKey(string key)
        {
            return new BigInteger(key, 16);
        }

        /// <summary>
        /// Returns a cryptographically random 128-bit token, in which the middle
        /// 8 bytes contain a 64-bit Unix epoch time representing the expiration time of a request.
        /// </summary>
        /// <param name="timeUntilExpiration">The time until the token expires</param>
        /// <param name="realTime">Wall clock time to use when calculating token expire time</param>
        /// <returns></returns>
        public static BigInteger GenerateRequestExpireTimeToken(TimeSpan timeUntilExpiration, IRealTimeProvider realTime)
        {
            long epochTimeMs = epochTicks / 10000;
            long expireTime = realTime.TimestampMilliseconds - epochTimeMs + (long)timeUntilExpiration.TotalMilliseconds;
            byte[] byteArray = new byte[16];
            for (int c = 0; c < byteArray.Length; c++)
            {
                byteArray[c] = (byte)random.NextInt(1, 255);
            }
            BinaryHelpers.Int64ToByteArrayLittleEndian(expireTime, byteArray, 4);
            return new BigInteger(byteArray, 16);
        }

        /// <summary>
        /// Inspects a red token and extracts the request expiration time from it.
        /// </summary>
        /// <param name="tokenRed">The red token for a request</param>
        /// <returns>The expiration time attached to this request</returns>
        public static DateTimeOffset ParseRequestExpireTime(BigInteger tokenRed)
        {
            long expireTimeMs = BitConverter.ToInt64(tokenRed.GetBytes(), 4);
            try
            {
                return new DateTimeOffset(epochTicks + (expireTimeMs * 10000), TimeSpan.Zero);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Ticks are invalid, so just return the epoch time
                return new DateTimeOffset(epochTicks, TimeSpan.Zero);
            }
        }
    }
}
