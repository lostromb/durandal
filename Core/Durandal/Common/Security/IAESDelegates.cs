using Durandal.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.Security
{
    /// <summary>
    /// Defines a provider for an AES encryption / decryption stream, with variable key size, CBC chaining mode, and PKCS7 padding.
    /// </summary>
    public interface IAESDelegates
    {
        FinalizableStream CreateEncryptionStream(Stream innerStream, byte[] encryptionKey, byte[] IV);
        FinalizableStream CreateDecryptionStream(Stream innerStream, byte[] encryptionKey, byte[] IV);
        byte[] GenerateKey(string passphrase, int keySizeBytes);
    }
}
