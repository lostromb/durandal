#pragma warning disable IA5359 // Use unapproved crypto library only in test

namespace Durandal.Tests.Common.Security
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using Org.BouncyCastle.Asn1;
    using Org.BouncyCastle.Asn1.Pkcs;
    using Org.BouncyCastle.Asn1.X509;
    using Org.BouncyCastle.Crypto;
    using Org.BouncyCastle.Crypto.Generators;
    using Org.BouncyCastle.Crypto.Operators;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Crypto.Prng;
    using Org.BouncyCastle.Math;
    using Org.BouncyCastle.Pkcs;
    using Org.BouncyCastle.Security;
    using Org.BouncyCastle.X509;

    /// <summary>
    /// Used to generate dynamic certificates without storing/loading them in the store.
    /// Used in place of hardcoded dummy certificates which 1. can expire, 2. will get flagged by credential scanners, 3. are just opaque base64 so you don't know what's in them.
    /// </summary>
    public static class CertificateGenerator
    {
        // Use the same RSA key for each cert to make tests much faster. Cert thumbprints will still be unique because of serial numbers.
        private static readonly RsaPrivateCrtKeyParameters nonSecretTestKey = new RsaPrivateCrtKeyParameters(
            new BigInteger("133665517820648357944353167988547807366308259631653069029854154754844153646919111483518761894844280532902684072276611750995592237029987169803005304288016049870958439509017860488383033678187712304383825036250375595143640652296480793071979365352616518061357474805707469145302369041636448528255082960844464898883"),
            new BigInteger("65537"),
            new BigInteger("791342614315753892952210647108603525613433705190676881510954301308871806994592600448682112626448889127763575080387038762627062391742603748776508812788962239955381597886858239501178495887701397257525743687753998570756533961651709506747600081671775422340464987503445359711518708969224524638734512740089038913"),
            new BigInteger("12957239184478167624843130820004599769063168234505225188551008089598934915040597545665102001925153031566833705475816328195322788103872278756546285843933643"),
            new BigInteger("10315894915389842430381879185104020194601869032118488244249466295985720410891794749508104936381944596387647225450701619014879373943441417708472684354616681"),
            new BigInteger("9010379153659886954462377649127815278015240690287030726811169914329033367686348056001058312643803107567128176348548962283498493141978961991712169172397757"),
            new BigInteger("7356983268236352676405979084068187434205056024721699529242653847568191804395556629037937601989039908609827176561947342282015457813792337492962827014517433"),
            new BigInteger("5498917185525540734102319457317361844635965408641003696198832230511687247352121843712626726575842042339305725999211528415960848445562528304954014310323060"));


        /// <summary>
        /// Generates a transient X509 certificate with the given subject name and a random RSA private key.
        /// The certificate will be marked valid for a 24-hour window. Don't store it anywhere or use it
        /// for anything besides unit tests.
        /// </summary>
        /// <param name="certificateSubjectName">The subject name of the generated certificate.</param>
        /// <param name="notBeforeTime">The time that the certificate becomes valid. Defaults to 24 hours in the past.</param>
        /// <param name="expireTime">The time that the certificate expires. Defaults to 24 hours in the future.</param>
        /// <param name="withPrivateKey">Whether the private key should be stored in the returned cert.</param>
        /// <param name="exportable">Set this to true if you need to install the cert into an actual physical store.
        /// Marks the private key as exportable and non-ephemeral.</param>
        /// <returns>A generated certificate.</returns>
        public static X509Certificate2 CreateSelfSignCertificate(
            string certificateSubjectName,
            DateTime? notBeforeTime = null,
            DateTime? expireTime = null,
            bool withPrivateKey = true,
            bool exportable = false)
        {
            // Not too much magic here really, simply follow examples from BouncyCastle (http://git.bouncycastle.org/)
            // to create a certificate with a private key attached
            var random = new SecureRandom();
            var certificateGenerator = new X509V3CertificateGenerator();

            // Set certificate parameters
            certificateGenerator.SetSerialNumber(BigInteger.ProbablePrime(120, random));
            certificateGenerator.SetIssuerDN(new X509Name($"CN={certificateSubjectName}"));
            certificateGenerator.SetSubjectDN(new X509Name($"CN={certificateSubjectName}"));
            certificateGenerator.SetNotBefore(notBeforeTime.GetValueOrDefault(DateTime.Now.AddHours(-24)).ToUniversalTime());
            certificateGenerator.SetNotAfter(expireTime.GetValueOrDefault(DateTime.Now.AddHours(24)).ToUniversalTime());

            certificateGenerator.AddExtension(
                X509Extensions.KeyUsage.Id,
                true,
                new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.DataEncipherment));

            // Mark cert as usable for signing & encryption
            var purposes = new List<KeyPurposeID>()
            {
                KeyPurposeID.id_kp_serverAuth,
                KeyPurposeID.id_kp_clientAuth,
                KeyPurposeID.id_kp_codeSigning,
            };

            certificateGenerator.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                true,
                new ExtendedKeyUsage(purposes.ToArray()));

            RsaKeyParameters publicKey = new RsaKeyParameters(false, nonSecretTestKey.PublicExponent, nonSecretTestKey.Modulus);
            var subjectKeyPair = new AsymmetricCipherKeyPair(publicKey, nonSecretTestKey);
            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            var issuerKeyPair = subjectKeyPair;
            const string signatureAlgorithm = "SHA256WithRSA";
            var signatureFactory = new Asn1SignatureFactory(signatureAlgorithm, issuerKeyPair.Private);
            var bouncyCert = certificateGenerator.Generate(signatureFactory);

            X509KeyStorageFlags storageFlags;
            if (exportable)
            {
                storageFlags = X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet;
            }
            else
            {
                // By default, mark keys as ephemeral so they aren't persisted to disk by CryptoAPI
                // (see https://snede.net/the-most-dangerous-constructor-in-net/)
                storageFlags = X509KeyStorageFlags.EphemeralKeySet;
            }

            X509Certificate2 certificate;
            if (withPrivateKey)
            {
                // Convert to C# x509 by exporting as pfx and reimporting using a random password
                Pkcs12Store store = new Pkcs12StoreBuilder().Build();
                store.SetKeyEntry($"{certificateSubjectName}_key", new AsymmetricKeyEntry(subjectKeyPair.Private), new[] { new X509CertificateEntry(bouncyCert) });
                string exportpw = Guid.NewGuid().ToString("x");

                using (var ms = new MemoryStream())
                {
                    store.Save(ms, exportpw.ToCharArray(), random);
                    certificate = new X509Certificate2(
                        rawData: ms.ToArray(),
                        password: exportpw,
                        keyStorageFlags: storageFlags);
                }
            }
            else
            {
                // Use .cer format internally which gives us only the public key info
                certificate = new X509Certificate2(
                    rawData: bouncyCert.GetEncoded(),
                    password: (string)null,
                    keyStorageFlags: storageFlags);
            }

            return certificate;
        }
    }
}

#pragma warning restore IA5359 // Use unapproved crypto library only in test
