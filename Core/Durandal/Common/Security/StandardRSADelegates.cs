using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security
{
    /// <summary>
    /// A highly compatible implementation of RSA intrinsics
    /// </summary>
    public class StandardRSADelegates : IRSADelegates
    {
        public IRandom _random;

        public StandardRSADelegates(IRandom randomProvider = null)
        {
            _random = randomProvider ?? new DefaultRandom();
        }

        public BigInteger Decrypt(BigInteger M, PublicKey key)
        {
            if (M >= key.N)
            {
                throw new ArithmeticException("Cannot decrypt a value larger than the RSA modulus");
            }
            if (M < BigInteger.Zero)
            {
                throw new ArithmeticException("Cannot decrypt a negative number");
            }

            return M.ModPow(key.E, key.N);
        }

        public BigInteger Encrypt(BigInteger M, PrivateKey key)
        {
            // Is the value too large to encrypt? Throw an exception
            if (M >= key.N)
            {
                throw new ArithmeticException("Cannot encrypt a value larger than the RSA modulus");
            }
            if (M < BigInteger.Zero)
            {
                throw new ArithmeticException("Cannot encrypt a negative number");
            }

            // Naive way
            if (key.DP == null || key.DQ == null || key.InvQ == null || key.P == null || key.Q == null)
            {
                return M.ModPow(key.D, key.N);
            }
            
            // Use the Chinese remainder algorithm for 3x faster encryption
            BigInteger m1 = M.ModPow(key.DP, key.P);
            BigInteger m2 = M.ModPow(key.DQ, key.Q);
            BigInteger h = (key.InvQ * (m1 - m2)) % key.P;
            // prevent modulo from going below zero
            if (h < BigInteger.Zero)
            {
                h = h + key.P;
            }

            BigInteger returnVal = m2 + (h * key.Q);
            return returnVal;
        }

        public PrivateKey GenerateRSAKey(int keySizeBits)
        {
            BigInteger t, p, q, n, e, d, qMinus1, pMinus1;
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
                        qMinus1 = (q - BigInteger.One);
                        pMinus1 = (p - BigInteger.One);
                        t = qMinus1 * pMinus1;
                    }
                    while (!t.Gcd(e).Equals(BigInteger.One));

                    d = e.ModInverse(t);

                    // Calculate extra values that can be used for efficient calculation later
                    BigInteger dp = d % pMinus1;
                    if (dp < 0)
                    {
                        dp = dp + pMinus1;
                    }

                    BigInteger dq = d % qMinus1;
                    if (dq < 0)
                    {
                        dq = dq + qMinus1;
                    }

                    BigInteger iq = q.ModInverse(p);

                    returnVal = new PrivateKey(d, e, n, p, q, dp, dq, iq, keySizeBits);
                }
                catch (ArithmeticException) { } // this can occur if no multiplicative inverse exists. In this case, try the algorithm again
            }

            return returnVal;
        }
    }
}
