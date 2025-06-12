using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Diagnostics;
using Durandal.Common.MathExt;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Collections;

namespace Durandal.Common.Security
{
    /// <summary>
    /// BROKEN. DO NOT USE
    /// Uses Win32 cryptography APIs to process RSA tasks
    /// </summary>
    public class SystemRSADelegates : IRSADelegates, IDisposable
    {
        private RSACryptoServiceProvider _rsa;
        private IRandom _random;
        private int _disposed = 0;

        public SystemRSADelegates(IRandom random = null)
        {
            _random = random ?? new CryptographicRandom();
            _rsa = new RSACryptoServiceProvider();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~SystemRSADelegates()
        {
            Dispose(false);
        }
#endif

        public BigInteger Decrypt(BigInteger M, PublicKey key)
        {
            int byteLength = key.KeyLengthBits / 8;
            RSAParameters convertedKey = new RSAParameters()
            {
                Exponent = ExtractBytes(key.E, byteLength),
                Modulus = ExtractBytes(key.N, byteLength)
            };

            lock (_rsa)
            {
                _rsa.ImportParameters(convertedKey);
                byte[] result = _rsa.Decrypt(ExtractBytes(M, byteLength), true);
                return new BigInteger(result);
            }
        }

        public BigInteger Encrypt(BigInteger M, PrivateKey key)
        {
            int byteLength = key.KeyLengthBits / 8;
            int halfByteLength = byteLength / 2;
            RSAParameters convertedKey = new RSAParameters()
            {
                D = ExtractBytes(key.D, byteLength),
                Modulus = ExtractBytes(key.N, byteLength),
                Exponent = ExtractBytes(key.E, byteLength),
                DP = ExtractBytes(key.DP, halfByteLength),
                DQ = ExtractBytes(key.DQ, halfByteLength),
                InverseQ = ExtractBytes(key.InvQ, halfByteLength),
                P = ExtractBytes(key.P, halfByteLength),
                Q = ExtractBytes(key.Q, halfByteLength)
            };

            lock (_rsa)
            {
                _rsa.ImportParameters(convertedKey);
                byte[] result = _rsa.Encrypt(ExtractBytes(M, byteLength), true);
                return new BigInteger(result);
            }
        }

        private static byte[] ExtractBytes(BigInteger val, int keyLengthBytes)
        {
            byte[] returnVal = new byte[keyLengthBytes];
            byte[] source = val.GetBytes();
            ArrayExtensions.MemCopy(source, 0, returnVal, 0, source.Length);
            return returnVal;
        }

        public PrivateKey GenerateRSAKey(int keySizeBits)
        {
            BigInteger t, p, q, n, e, d;
            BigInteger one = new BigInteger(1);
            PrivateKey returnVal = null;
            while (returnVal == null)
            {
                try
                {
                    do
                    {
                        p = BigInteger.GenPseudoPrime(keySizeBits / 2, 20, _random);
                        q = BigInteger.GenPseudoPrime(keySizeBits / 2, 20, _random);
                        n = p * q;
                        e = new BigInteger(65537);
                        t = (p - one) * (q - one);
                    }
                    while (!t.Gcd(e).Equals(one));

                    d = e.ModInverse(t);

                    // Calculate extra values that can be used for efficient calculation later
                    BigInteger dp = d % (q - 1);
                    BigInteger dq = d % (p - 1);
                    BigInteger iq = q.ModInverse(p);

                    returnVal = new PrivateKey(d, e, n, p, q, dp, dq, iq, keySizeBits);
                }
                catch (ArithmeticException) { } // this can occur if no multiplicative inverse exists. In this case, try the algorithm again
            }

            return returnVal;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _rsa?.Dispose();
            }
        }
    }
}
