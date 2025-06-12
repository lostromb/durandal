using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Durandal.Common.Utils;
using Durandal.Common.Logger;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

using SystemX509 = System.Security.Cryptography.X509Certificates;
using BCBigInteger = Org.BouncyCastle.Math.BigInteger;
using DBigInteger = Durandal.Common.MathExt.BigInteger;
using SystemCryptography = System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace Durandal.Common.Security
{
    /// <summary>
    /// Conversion and utility methods for working with X.509 certificates
    /// </summary>
    public static class CertificateHelpers
    {
        /// <summary>
        /// Selects certificate with access to PrivateKey.
        /// </summary>
        /// <param name="certificates"><see cref="SystemX509.X509Certificate2Collection"/></param>
        /// <returns>With multiple certs it returns first <see cref="SystemX509.X509Certificate2"/> with private key. Even when no cert has private key returns first.
        /// When collection has single cert, return the same.
        /// When collection is null or empty returns null.</returns>
        public static SystemX509.X509Certificate2 SelectCertWithPrivateKey(IEnumerable<SystemX509.X509Certificate2> certificates)
        {
            return SelectCertsWithPrivateKey(certificates).FirstOrDefault();
        }

        /// <summary>
        /// Selects all certificates with access to PrivateKey.
        /// </summary>
        /// <param name="certificates"><see cref="SystemX509.X509Certificate2Collection"/></param>
        /// <returns>Returns all <see cref="SystemX509.X509Certificate2"/> certs with an accessible private key.</returns>
        public static IEnumerable<SystemX509.X509Certificate2> SelectCertsWithPrivateKey(IEnumerable<SystemX509.X509Certificate2> certificates)
        {
            if (certificates == null)
            {
                yield break;
            }

            foreach (var cert in certificates)
            {
                bool canAccessPrivateKey = false;

                try
                {
                    if (cert.HasPrivateKey && cert.GetRSAPrivateKey() != null)
                    {
                        canAccessPrivateKey = true;
                    }
                }
                catch (NotSupportedException)
                {
                }
                catch (ArgumentNullException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (CryptographicException)
                {
                }

                if (canAccessPrivateKey)
                {
                    yield return cert;
                }
            }
        }

        /// <summary>
        /// Generate public key xml from the certificate object by exporting <see cref="RSAParameters"/>
        /// For .NetStandard this method helps to replace X509Certificate2.ToXmlString()
        /// </summary>
        /// <param name="certificate">The <see cref="SystemX509.X509Certificate2"/> object</param>
        /// <returns>The public key xml string</returns>
        public static string GetXmlPublicKey(this SystemX509.X509Certificate2 certificate)
        {
            SystemCryptography.RSAParameters rsaParameters = certificate.GetRSAPublicKey().ExportParameters(false);
            return string.Format(
                "<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent></RSAKeyValue>",
                Convert.ToBase64String(rsaParameters.Modulus),
                Convert.ToBase64String(rsaParameters.Exponent));
        }

        /// <summary>
        /// Tests to see if the given certificate will be valid in a 10-minute window surrounding the given time.
        /// </summary>
        /// <param name="cert">The certificate to check.</param>
        /// <param name="atTime">The time to check for validity (usually DateTimeOffset.Now).</param>
        /// <returns>True if the certificate would be safe to use at the given time.</returns>
        public static bool IsCertValidNear(SystemX509.X509Certificate2 cert, DateTimeOffset atTime)
        {
            return IsCertValidNear(cert, atTime, TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Tests to see if the given certificate will be valid in a certain window surrounding the given time.
        /// </summary>
        /// <param name="cert">The certificate to check.</param>
        /// <param name="atTime">The time to check for validity (usually DateTimeOffset.Now).</param>
        /// <param name="timeRange">The buffer window to check for validity. The check is done using currentTime +- this value.</param>
        /// <returns>True if the certificate would be safe to use around the given time.</returns>
        public static bool IsCertValidNear(SystemX509.X509Certificate2 cert, DateTimeOffset atTime, TimeSpan timeRange)
        {
            cert = cert.AssertNonNull(nameof(cert));
            return (atTime.UtcDateTime - timeRange) > cert.NotBefore.ToUniversalTime() && // Start of validity window
                   (atTime.UtcDateTime + timeRange) < cert.NotAfter.ToUniversalTime(); // End of validity window
        }

        ///// <summary>
        ///// Converts a Durandal private key and key identifier into an equivalent self-signed X.509 certificate
        ///// </summary>
        ///// <param name="keyId"></param>
        ///// <param name="durandalKey"></param>
        ///// <returns></returns>
        //public static SystemX509.X509Certificate2 ConvertDurandalKeyToX509Cert(ClientKeyIdentifier keyId, PrivateKey durandalKey)
        //{
        //    AsymmetricCipherKeyPair convertedKey = ConvertPrivateKey(durandalKey);
        //    string subjectName = GenerateCertificateSubjectName(keyId);
        //    return PopulateAndSignCertificate(convertedKey, subjectName, "Durandal self-signed CA", "notapassword");
        //}

        /// <summary>
        /// Converts an X.509 certificate into a Durandal private key
        /// </summary>
        /// <param name="cert"></param>
        /// <returns></returns>
        public static PrivateKey ConvertX509CertificateToDurandalKey(SystemX509.X509Certificate2 cert)
        {
            SystemCryptography.RSA rsaProvider = cert.GetRSAPrivateKey();
            SystemCryptography.RSAParameters privateKeyParams = rsaProvider.ExportParameters(true);
            return new PrivateKey(
                new DBigInteger(privateKeyParams.D),
                new DBigInteger(privateKeyParams.Exponent),
                new DBigInteger(privateKeyParams.Modulus),
                new DBigInteger(privateKeyParams.P),
                new DBigInteger(privateKeyParams.Q),
                new DBigInteger(privateKeyParams.DP),
                new DBigInteger(privateKeyParams.DQ),
                new DBigInteger(privateKeyParams.InverseQ),
                rsaProvider.KeySize);
        }

        /// <summary>
        /// Attempts to install a certificate with the given subject name (derived from the key identifier) into the CurrentUser-Personal certificate store.
        /// If a certificate with the same common name already exists, it will be overwritten.
        /// </summary>
        /// <param name="keyId"></param>
        /// <param name="cert"></param>
        public static void InstallCertificateToUserLocalStore(ClientKeyIdentifier keyId, SystemX509.X509Certificate2 cert)
        {
            string subjectName = GenerateCertificateSubjectName(keyId);
            SystemX509.X509Store machineStore = new SystemX509.X509Store(SystemX509.StoreName.My, SystemX509.StoreLocation.CurrentUser);
            machineStore.Open(SystemX509.OpenFlags.ReadWrite);

            try
            {
                // Does a cert with this CN already exist? If so, delete it
                SystemX509.X509Certificate2Collection findResults = machineStore.Certificates.Find(SystemX509.X509FindType.FindBySubjectName, subjectName, false);
                machineStore.RemoveRange(findResults);
                machineStore.Add(cert);
            }
            finally
            {
                machineStore.Close();
            }
        }

        /// <summary>
        /// Looks inside the CurrentUser-Personal certificate store for a certificate matching the given Durandal key ID, and returns it if found.
        /// </summary>
        /// <param name="keyId"></param>
        /// <returns></returns>
        public static SystemX509.X509Certificate2 GetCertificateForDurandalKey(ClientKeyIdentifier keyId)
        {
            string subjectName = GenerateCertificateSubjectName(keyId);
            SystemX509.X509Store machineStore = new SystemX509.X509Store(SystemX509.StoreName.My, SystemX509.StoreLocation.CurrentUser);
            machineStore.Open(SystemX509.OpenFlags.ReadOnly);
            try
            {
                SystemX509.X509Certificate2Collection findResults = machineStore.Certificates.Find(SystemX509.X509FindType.FindBySubjectName, subjectName, false);
                foreach (var cert in findResults)
                {
                    return cert;
                }

                return null;
            }
            finally
            {
                machineStore.Close();
            }
        }

        ///// <summary>
        ///// Creates a new, self-signed RSA certificate with the given subject name, certificate issuer, password, and key length.
        ///// </summary>
        ///// <param name="subjectName"></param>
        ///// <param name="issuer"></param>
        ///// <param name="password"></param>
        ///// <param name="rsaBits"></param>
        ///// <returns></returns>
        //public static SystemX509.X509Certificate2 GenerateTestCertificate(string subjectName, string issuer, string password, int rsaBits = 2048)
        //{
        //    var keypairgen = new RsaKeyPairGenerator();
        //    keypairgen.Init(new KeyGenerationParameters(new SecureRandom(new CryptoApiRandomGenerator()), rsaBits));
        //    var keypair = keypairgen.GenerateKeyPair();
        //    return PopulateAndSignCertificate(keypair, subjectName, issuer, password);
        //}

        //private static SystemX509.X509Certificate2 PopulateAndSignCertificate(AsymmetricCipherKeyPair keypair, string subjectName, string issuer, string password)
        //{
        //    // Certificate Generator
        //    X509V3CertificateGenerator cGenerator = new X509V3CertificateGenerator();
        //    cGenerator.SetSerialNumber(BCBigInteger.ProbablePrime(120, new Random()));
        //    cGenerator.SetSubjectDN(new X509Name("CN=" + subjectName));
        //    cGenerator.SetIssuerDN(new X509Name("CN=" + issuer));
        //    cGenerator.SetNotBefore(DateTime.UtcNow.Subtract(TimeSpan.FromDays(7)));
        //    cGenerator.SetNotAfter(DateTime.UtcNow.Add(TimeSpan.FromDays(365 * 20))); // Expire in 20 years
        //    cGenerator.SetPublicKey(keypair.Public);

        //    ISignatureFactory signatureFactory = new Asn1SignatureFactory("SHA256withRSA", keypair.Private);
        //    X509Certificate cert = cGenerator.Generate(signatureFactory); // Create a self-signed cert

        //    // Create the PKCS12 store
        //    Pkcs12Store store = new Pkcs12StoreBuilder().Build();

        //    // Add a Certificate entry
        //    X509CertificateEntry certEntry = new X509CertificateEntry(cert);
        //    store.SetCertificateEntry(cert.SubjectDN.ToString(), certEntry); // use DN as the Alias.

        //    // Add a key entry
        //    // FIXME it appears that the signing is never actually performed so windows treats the cert as "invalid"
        //    AsymmetricKeyEntry keyEntry = new AsymmetricKeyEntry(keypair.Private);
        //    store.SetKeyEntry(cert.SubjectDN.ToString() + "_key", keyEntry, new X509CertificateEntry[] { certEntry }); // Create a signing chain of length 1

        //    // Convert BouncyCastle X509 Certificate to .NET's X509Certificate
        //    SystemX509.X509Certificate dotNetCert = DotNetUtilities.ToX509Certificate(cert);
        //    byte[] certBytes = dotNetCert.Export(SystemX509.X509ContentType.Pkcs12, password);

        //    // Convert X509Certificate to X509Certificate2
        //    SystemX509.X509Certificate2 cert2 = new SystemX509.X509Certificate2(certBytes, password);

        //    // Convert BouncyCastle Private Key to RSA
        //    SystemCryptography.RSA rsaPriv = DotNetUtilities.ToRSA(keypair.Private as RsaPrivateCrtKeyParameters);

        //    // Setup RSACryptoServiceProvider with "KeyContainerName" set
        //    SystemCryptography.CspParameters csp = new SystemCryptography.CspParameters();
        //    csp.KeyContainerName = "KeyContainer";

        //    SystemCryptography.RSACryptoServiceProvider rsaPrivate = new SystemCryptography.RSACryptoServiceProvider(csp);

        //    // Import private key from BouncyCastle's rsa
        //    rsaPrivate.ImportParameters(rsaPriv.ExportParameters(true));

        //    // Set private key on our X509Certificate2
        //    cert2.PrivateKey = rsaPrivate;

        //    return cert2;
        //}

        private static string GenerateCertificateSubjectName(ClientKeyIdentifier keyId)
        {
            if (keyId.Scope == ClientAuthenticationScope.Client)
            {
                return "Durandal client " + keyId.ClientId;
            }
            else if (keyId.Scope == ClientAuthenticationScope.User)
            {
                return "Durandal user " + keyId.UserId;
            }
            else if (keyId.Scope == ClientAuthenticationScope.UserClient)
            {
                return "Durandal user " + keyId.UserId + " / client " + keyId.ClientId;
            }
            else
            {
                throw new ArgumentException("Key scope is unknown");
            }
        }

        private static BCBigInteger ConvertBigInteger(DBigInteger input)
        {
            return new BCBigInteger(input.ToHexString(), 16);
        }

        private static AsymmetricCipherKeyPair ConvertPrivateKey(PrivateKey durandalKey)
        {
            BCBigInteger modulus = ConvertBigInteger(durandalKey.N);
            BCBigInteger publicExponent = ConvertBigInteger(durandalKey.E);
            BCBigInteger privateExponent = ConvertBigInteger(durandalKey.D);
            BCBigInteger p = ConvertBigInteger(durandalKey.P);
            BCBigInteger q = ConvertBigInteger(durandalKey.Q);
            BCBigInteger dP = ConvertBigInteger(durandalKey.DP);
            BCBigInteger dQ = ConvertBigInteger(durandalKey.DQ);
            BCBigInteger qInv = ConvertBigInteger(durandalKey.InvQ);

            // Convert the private key
            //Console.WriteLine("Generated modulus is " + durandalKey.N.ToHexString());
            RsaPrivateCrtKeyParameters privKey = new RsaPrivateCrtKeyParameters(modulus, publicExponent, privateExponent, p, q, dP, dQ, qInv);
            RsaKeyParameters pubKey = new RsaKeyParameters(false, modulus, publicExponent);
            AsymmetricCipherKeyPair kp = new AsymmetricCipherKeyPair(pubKey, privKey);
            return kp;
        }

        private static X509Certificate2 CreateSelfSigned()
        {
            X509Certificate2 cert;
            using (RSA rsa = RSA.Create())
            {
                var certReq = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                certReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
                certReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
                cert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow.AddMonths(1));
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    cert = new X509Certificate2(cert.Export(X509ContentType.Pfx));
                }
            }
            return cert;
        }
    }
}
