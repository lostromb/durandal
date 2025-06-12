using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Interface for a processor that can decrypt (and by extension, encrypt) potentially sensitive strings, used for compliant instrumentation
    /// </summary>
    public interface IStringDecrypterPii
    {
        /// <summary>
        /// Attempts to decrypt the given cipherText. If the string is not ciphertext, or if the key it requires
        /// is not loaded, this returns false.
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="plainText"></param>
        /// <returns></returns>
        bool TryDecryptString(string cipherText, out string plainText);
    }
}
