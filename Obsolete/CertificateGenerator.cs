

namespace Prototype.NetCore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using SystemX509 = System.Security.Cryptography.X509Certificates;
    using SystemCryptography = System.Security.Cryptography;
    using Org.BouncyCastle.Crypto.Generators;
    using Org.BouncyCastle.Crypto;
    using Org.BouncyCastle.Security;
    using Org.BouncyCastle.Crypto.Prng;
    using Org.BouncyCastle.X509;
    using Org.BouncyCastle.Asn1.X509;
    using Org.BouncyCastle.Math;
    using Org.BouncyCastle.Crypto.Operators;
    using Org.BouncyCastle.Pkcs;
    using Org.BouncyCastle.Crypto.Parameters;
    using System.IO;
    using Org.BouncyCastle.Asn1.Pkcs;
    using Org.BouncyCastle.Asn1;
    using Org.BouncyCastle.OpenSsl;

    internal static class CertificateGenerator
    {
        public static SystemX509.X509Certificate2 GenCertificate1(string subjectName)
        {
            var kpGen = new RsaKeyPairGenerator();
            kpGen.Init(new KeyGenerationParameters(new SecureRandom(new CryptoApiRandomGenerator()), 2048));

            AsymmetricCipherKeyPair keyPair = kpGen.GenerateKeyPair();
            var gen = new X509V3CertificateGenerator();
            var certificateName = new X509Name("CN=" + subjectName);
            BigInteger serialNumber = BigInteger.ProbablePrime(120, new Random());
            gen.SetSerialNumber(serialNumber);
            gen.SetSubjectDN(certificateName);
            gen.SetIssuerDN(certificateName);
            gen.SetNotAfter(DateTime.Now.AddYears(20));
            gen.SetNotBefore(DateTime.Now.AddHours(-24));
            gen.SetSignatureAlgorithm("SHA256WithRSAEncryption");
            gen.SetPublicKey(keyPair.Public);

            gen.AddExtension(X509Extensions.AuthorityKeyIdentifier.Id, false,
                             new AuthorityKeyIdentifier(
                                 SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public),
                                 new GeneralNames(new GeneralName(certificateName)), serialNumber));

            gen.AddExtension(X509Extensions.KeyUsage.Id, false, new KeyUsage(KeyUsage.DataEncipherment | KeyUsage.DigitalSignature));

            X509Certificate newCert = gen.Generate(keyPair.Private);

            var newStore = new Pkcs12Store();

            var certEntry = new X509CertificateEntry(newCert);

            newStore.SetCertificateEntry("invalid_certificate", certEntry);

            newStore.SetKeyEntry(
                Environment.MachineName,
                new AsymmetricKeyEntry(keyPair.Private),
                new[] { certEntry }
                );

            var memoryStream = new MemoryStream();
            newStore.Save(
                memoryStream,
                new char[0],
                new SecureRandom(new CryptoApiRandomGenerator())
                );

            return new SystemX509.X509Certificate2(memoryStream.ToArray());
        }

        /// <summary>
        /// Creates a new, self-signed RSA certificate with the given subject name, certificate issuer, password, and key length.
        /// </summary>
        /// <param name="subjectName"></param>
        /// <param name="issuer"></param>
        /// <param name="password"></param>
        /// <param name="rsaBits"></param>
        /// <returns></returns>
        public static SystemX509.X509Certificate2 GenCertificate2(string subjectName, string issuer, string password)
        {
            var keypairgen = new RsaKeyPairGenerator();
            keypairgen.Init(new KeyGenerationParameters(new SecureRandom(new CryptoApiRandomGenerator()), 2048));
            var keypair = keypairgen.GenerateKeyPair();
            return PopulateAndSignCertificate(keypair, subjectName, issuer, password);
        }

        private static SystemX509.X509Certificate2 PopulateAndSignCertificate(AsymmetricCipherKeyPair keypair, string subjectName, string issuer, string password)
        {
            // Certificate Generator
            X509V3CertificateGenerator cGenerator = new X509V3CertificateGenerator();
            cGenerator.SetSerialNumber(BigInteger.ProbablePrime(120, new Random()));
            cGenerator.SetSubjectDN(new X509Name("CN=" + subjectName));
            cGenerator.SetIssuerDN(new X509Name("CN=" + issuer));
            cGenerator.SetNotBefore(DateTime.UtcNow.Subtract(TimeSpan.FromDays(7)));
            cGenerator.SetNotAfter(DateTime.UtcNow.Add(TimeSpan.FromDays(365 * 20))); // Expire in 20 years
            cGenerator.SetPublicKey(keypair.Public);

            ISignatureFactory signatureFactory = new Asn1SignatureFactory("SHA256withRSA", keypair.Private);
            X509Certificate cert = cGenerator.Generate(signatureFactory); // Create a self-signed cert

            // Create the PKCS12 store
            Pkcs12Store store = new Pkcs12StoreBuilder().Build();

            // Add a Certificate entry
            X509CertificateEntry certEntry = new X509CertificateEntry(cert);
            store.SetCertificateEntry(cert.SubjectDN.ToString(), certEntry); // use DN as the Alias.

            // Add a key entry
            // FIXME it appears that the signing is never actually performed so windows treats the cert as "invalid"
            AsymmetricKeyEntry keyEntry = new AsymmetricKeyEntry(keypair.Private);
            store.SetKeyEntry(cert.SubjectDN.ToString() + "_key", keyEntry, new X509CertificateEntry[] { certEntry }); // Create a signing chain of length 1

            // Convert BouncyCastle X509 Certificate to .NET's X509Certificate
            SystemX509.X509Certificate dotNetCert = DotNetUtilities.ToX509Certificate(cert);
            byte[] certBytes = dotNetCert.Export(SystemX509.X509ContentType.Pkcs12, password);

            // Convert X509Certificate to X509Certificate2
            SystemX509.X509Certificate2 cert2 = new SystemX509.X509Certificate2(certBytes, password);

            // Convert BouncyCastle Private Key to RSA
            SystemCryptography.RSA rsaPriv = DotNetUtilities.ToRSA(keypair.Private as RsaPrivateCrtKeyParameters);

            // Setup RSACryptoServiceProvider with "KeyContainerName" set
            SystemCryptography.CspParameters csp = new SystemCryptography.CspParameters();
            csp.KeyContainerName = "KeyContainer";

            SystemCryptography.RSACryptoServiceProvider rsaPrivate = new SystemCryptography.RSACryptoServiceProvider(csp);

            // Import private key from BouncyCastle's rsa
            rsaPrivate.ImportParameters(rsaPriv.ExportParameters(true));

            // Set private key on our X509Certificate2
            cert2.PrivateKey = rsaPrivate;

            return cert2;
        }

        public static void GenCertificate3(string subjectName, out byte[] pkcs12Data, out SystemX509.X509Certificate2 x509Certificate2)
        {
            // Generating Random Numbers
            var randomGenerator = new CryptoApiRandomGenerator();
            var random = new SecureRandom(randomGenerator);

            var kpgen = new RsaKeyPairGenerator();
            kpgen.Init(new KeyGenerationParameters(random, 2048));
            var subjectKeyPair = kpgen.GenerateKeyPair();

            var gen = new X509V3CertificateGenerator();

            X509Name certName = new X509Name("CN=" + subjectName);

            BigInteger serialNo;
            serialNo = BigInteger.ProbablePrime(120, random);

            gen.SetSerialNumber(serialNo);
            gen.SetSubjectDN(certName);
            gen.SetIssuerDN(certName);

            gen.SetNotBefore(DateTime.UtcNow.AddHours(-2)); // go back 2 hours just to be safe
            gen.SetNotAfter(DateTime.UtcNow.AddYears(20));
            gen.SetSignatureAlgorithm("SHA256WithRSA");
            gen.SetPublicKey(subjectKeyPair.Public);

            gen.AddExtension(
                X509Extensions.BasicConstraints.Id,
                true,
                new BasicConstraints(false));

            gen.AddExtension(X509Extensions.KeyUsage.Id,
                true,
                new KeyUsage(KeyUsage.DigitalSignature));

            // handle our key purposes
            var purposes = new List<KeyPurposeID>();
            purposes.Add(KeyPurposeID.IdKPServerAuth);
            purposes.Add(KeyPurposeID.IdKPClientAuth);
            purposes.Add(KeyPurposeID.IdKPCodeSigning);

            gen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                true,
                new ExtendedKeyUsage(purposes.ToArray()));

            var certificate = gen.Generate(subjectKeyPair.Private, random);

            RsaPrivateCrtKeyParameters rsaparams = subjectKeyPair.Private as RsaPrivateCrtKeyParameters;

            //PrivateKeyInfo info = PrivateKeyInfoFactory.CreatePrivateKeyInfo(subjectKeyPair.Private);

            //var seq = (Asn1Sequence)Asn1Object.FromByteArray(info.PrivateKeyData.GetDerEncoded());
            //if (seq.Count != 9)
            //{
            //    throw new PemException("Malformed sequence in RSA private key.");
            //}

            //var rsa = RsaPrivateKeyStructure.GetInstance(seq);
            //RsaPrivateCrtKeyParameters rsaparams = new RsaPrivateCrtKeyParameters(
            //    rsa.Modulus, rsa.PublicExponent, rsa.PrivateExponent, rsa.Prime1, rsa.Prime2, rsa.Exponent1, rsa.Exponent2, rsa.Coefficient);

            // this is exportable to get the bytes of the key to our file system in an encrypted manner
            SystemCryptography.RSAParameters rsaParameters = DotNetUtilities.ToRSAParameters(rsaparams);
            SystemCryptography.CspParameters cspParameters = new SystemCryptography.CspParameters();
            cspParameters.KeyContainerName = Guid.NewGuid().ToString();
            SystemCryptography.RSACryptoServiceProvider rsaKey = new SystemCryptography.RSACryptoServiceProvider(2048, cspParameters);
            rsaKey.PersistKeyInCsp = false; // do not persist          
            rsaKey.ImportParameters(rsaParameters);

            var x509 = new SystemX509.X509Certificate2(certificate.GetEncoded());
            x509.PrivateKey = rsaKey;

            // this is non-exportable
            SystemCryptography.CspParameters cspParametersNoExport = new SystemCryptography.CspParameters();
            cspParametersNoExport.KeyContainerName = Guid.NewGuid().ToString();
            cspParametersNoExport.Flags = SystemCryptography.CspProviderFlags.UseNonExportableKey;
            SystemCryptography.RSACryptoServiceProvider rsaKey2 = new SystemCryptography.RSACryptoServiceProvider(2048, cspParametersNoExport);
            rsaKey2.PersistKeyInCsp = false; // do not persist   
            rsaKey2.ImportParameters(rsaParameters);

            x509Certificate2 = new SystemX509.X509Certificate2(certificate.GetEncoded());
            x509Certificate2.PrivateKey = rsaKey2;

            //// Generating Random Numbers
            //var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-()#$%^&@+=!";
            //var rnd = new Random();

            //password = new string(
            //    Enumerable.Repeat(chars, 15)
            //              .Select(s => s[rnd.Next(s.Length)])
            //              .ToArray());

            pkcs12Data = x509.Export(SystemX509.X509ContentType.Pfx);
        }

        public static SystemX509.X509Certificate2 GenCertificate4(string subject)
        {

            var random = new SecureRandom();
            var certificateGenerator = new X509V3CertificateGenerator();

            var serialNumber = BigInteger.ProbablePrime(120, random);
            certificateGenerator.SetSerialNumber(serialNumber);

            certificateGenerator.SetIssuerDN(new X509Name($"CN={subject}"));
            certificateGenerator.SetSubjectDN(new X509Name($"CN={subject}"));
            certificateGenerator.SetNotBefore(DateTime.UtcNow.Date);
            certificateGenerator.SetNotAfter(DateTime.UtcNow.Date.AddYears(1));

            const int strength = 2048;
            var keyGenerationParameters = new KeyGenerationParameters(random, strength);
            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);

            var subjectKeyPair = keyPairGenerator.GenerateKeyPair();
            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            var issuerKeyPair = subjectKeyPair;
            const string signatureAlgorithm = "SHA256WithRSA";
            var signatureFactory = new Asn1SignatureFactory(signatureAlgorithm, issuerKeyPair.Private);
            var bouncyCert = certificateGenerator.Generate(signatureFactory);

            // Lets convert it to X509Certificate2
            SystemX509.X509Certificate2 certificate;

            Pkcs12Store store = new Pkcs12StoreBuilder().Build();
            store.SetKeyEntry($"{subject}_key", new AsymmetricKeyEntry(subjectKeyPair.Private), new[] { new X509CertificateEntry(bouncyCert) });
            string exportpw = Guid.NewGuid().ToString("x");

            using (var ms = new System.IO.MemoryStream())
            {
                store.Save(ms, exportpw.ToCharArray(), random);
                certificate = new SystemX509.X509Certificate2(ms.ToArray(), exportpw, SystemX509.X509KeyStorageFlags.Exportable);
            }

            //Console.WriteLine($"Generated cert with thumbprint {certificate.Thumbprint}");
            return certificate;
        }
    }
}
