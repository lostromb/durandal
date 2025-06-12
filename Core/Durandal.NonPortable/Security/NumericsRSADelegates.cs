using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysNum = System.Numerics;

namespace Durandal.Common.Security
{
    /// <summary>
    /// Uses System.Numerics for its raw modular arithmetic
    /// </summary>
    public class NumericsRSADelegates : IRSADelegates
    {
        private static readonly IRSADelegates _fallback = new StandardRSADelegates();
        
        public BigInteger Decrypt(BigInteger M, PublicKey key)
        {
            SysNum.BigInteger e = ConvertToSysNum(key.E);
            SysNum.BigInteger n = ConvertToSysNum(key.N);
            SysNum.BigInteger m = ConvertToSysNum(M);
            SysNum.BigInteger r = SysNum.BigInteger.ModPow(m, e, n);
            return ConvertFromSysNum(r);
        }

        public BigInteger Encrypt(BigInteger M, PrivateKey key)
        {
            // TODO The Chinese remainder algorithm would speed this up substantially
            SysNum.BigInteger d = ConvertToSysNum(key.D);
            SysNum.BigInteger n = ConvertToSysNum(key.N);
            SysNum.BigInteger m = ConvertToSysNum(M);
            SysNum.BigInteger r = SysNum.BigInteger.ModPow(m, d, n);
            return ConvertFromSysNum(r);
        }

        public PrivateKey GenerateRSAKey(int keySizeBits)
        {
            return _fallback.GenerateRSAKey(keySizeBits);
        }

        private static SysNum.BigInteger ConvertToSysNum(BigInteger value)
        {
            byte[] sourceBytes = value.GetBytes();

            int l = sourceBytes.Length;
            int m = l - 1;
            byte[] returnVal = new byte[sourceBytes.Length + 1];
            for (int c = 0; c < l; c++)
            {
                returnVal[m - c] = sourceBytes[c];
            }

            // Always assume the source value is positive
            returnVal[l] = 0;

            SysNum.BigInteger t = new SysNum.BigInteger(returnVal);
            return t;

            //SysNum.BigInteger returnVal = new SysNum.BigInteger(0);

            //byte[] sourceBytes = value.GetBytes();
            //int l = sourceBytes.Length;
            //for (int c = 0; c < l; c++)
            //{
            //    returnVal = (returnVal << 8) + sourceBytes[c];
            //}

            //return returnVal;
        }

        private static BigInteger ConvertFromSysNum(SysNum.BigInteger value)
        {
            byte[] sourceBytes = value.ToByteArray();

            int l = sourceBytes.Length;
            int m = l - 1;
            byte[] returnVal = new byte[sourceBytes.Length + 1];
            for (int c = 0; c < l; c++)
            {
                returnVal[l - c] = sourceBytes[c];
            }

            // Always assume the source value is positive
            returnVal[0] = 0;

            BigInteger t = new BigInteger(returnVal);
            return t;
        }
    }
}
