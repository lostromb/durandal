using System;
using System.Collections.Generic;
using System.Text;
using Durandal.API;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;

namespace Durandal.Common.Instrumentation
{
    public class NullStringEncrypter : IStringEncrypterPii, IStringDecrypterPii
    {
        public string EncryptString(string plaintext)
        {
            return plaintext;
        }

        public void EncryptString(StringBuilder inputBuffer, StringBuilder outputBuffer)
        {
            StringUtils.CopyAcross(inputBuffer, outputBuffer);
        }

        public bool EncryptionRequired(DataPrivacyClassification privacyClass)
        {
            return false;
        }

        public bool TryDecryptString(string cipherText, out string plainText)
        {
            plainText = null;
            return false;
        }
    }
}
