using Durandal.API;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Interface for a processor that can encrypt (but NOT decrypt) potentially sensitive strings, used for compliant instrumentation
    /// </summary>
    public interface IStringEncrypterPii
    {
        /// <summary>
        /// Encrypts a message using the globally configured encryption key
        /// </summary>
        /// <param name="plaintext"></param>
        /// <returns></returns>
        string EncryptString(string plaintext);

        void EncryptString(StringBuilder inputBuffer, StringBuilder outputBuffer);

        bool EncryptionRequired(DataPrivacyClassification privacyClass);
    }
}
