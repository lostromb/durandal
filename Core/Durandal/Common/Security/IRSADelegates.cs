using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security
{
    public interface IRSADelegates
    {
        /// <summary>
        /// Raw encryption method. Only a private key can execute this. The input value must be lower than the modulo value N
        /// </summary>
        /// <param name="M"></param>
        /// <param name="key"></param>
        /// <returns>VALUE ^ D % N</returns>
        BigInteger Encrypt(BigInteger M, PrivateKey key);

        /// <summary>
        /// Raw decryption method. Anyone with the public key can perform this operation.
        /// For public keys this is the method is the same as SignVerify
        /// </summary>
        /// <param name="M"></param>
        /// <param name="key"></param>
        /// <returns>VALUE ^ E % N</returns>
        BigInteger Decrypt(BigInteger M, PublicKey key);

        /// <summary>
        /// Generates a new RSA private key of the specified bit length
        /// </summary>
        /// <param name="keySizeBits"></param>
        /// <returns></returns>
        PrivateKey GenerateRSAKey(int keySizeBits);
    }
}
