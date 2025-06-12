namespace Durandal.Tests.Common.Security
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Durandal.Common.Security;
    using Durandal.Common.Utils;
    using Durandal.Common.Time;

    [TestClass]
    public class CertificateCacheTests
    {
        private static X509Certificate2 currentlyValidCert;
        private static X509Certificate2 certWithoutPrivateKey;
        private static X509Certificate2 notYetValidCert;
        private static X509Certificate2 expiredCert;
        private static X509Certificate2 expiringSoonCert;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            context = context.AssertNonNull(nameof(context));
            currentlyValidCert = CertificateGenerator.CreateSelfSignCertificate("unit.test.subject.a", notBeforeTime: DateTime.Now.AddDays(-30), expireTime: DateTime.Now.AddDays(30));
            certWithoutPrivateKey = CertificateGenerator.CreateSelfSignCertificate("unit.test.subject.b", withPrivateKey: false);
            notYetValidCert = CertificateGenerator.CreateSelfSignCertificate("unit.test.subject.c", notBeforeTime: DateTime.Now.AddDays(3), expireTime: DateTime.Now.AddDays(7));
            expiredCert = CertificateGenerator.CreateSelfSignCertificate("unit.test.subject.d", notBeforeTime: DateTime.Now.AddDays(-7), expireTime: DateTime.Now.AddDays(-3));
            expiringSoonCert = CertificateGenerator.CreateSelfSignCertificate("unit.test.subject.e", notBeforeTime: DateTime.Now.AddDays(-7), expireTime: DateTime.Now.AddDays(1));
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            currentlyValidCert?.Dispose();
            certWithoutPrivateKey?.Dispose();
            notYetValidCert?.Dispose();
            expiredCert?.Dispose();
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificate(X509FindType, string)"/> will throw exception if cert if not found.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestCertificateCache_GetCertificate_ThrowsWhenNoCertFound()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            provider.GetCertificate(CertificateIdentifier.BySubjectName("not.exist"), DefaultRealTimeProvider.Singleton);
        }

        /// <summary>
        /// Tests that the constructor for <see cref="CertificateCache"/> will reject cache lifetime of zero
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestCertificateCache_ZeroCacheTime()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            CertificateCache provider = new CertificateCache(TimeSpan.Zero, certStore);
        }

        /// <summary>
        /// Tests that the constructor for <see cref="CertificateCache"/> will reject negative cache lifetime
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestCertificateCache_NegativeCacheTime()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(-5), certStore);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificate(X509FindType, string, StoreLocation)"/> will throw exception if cert if not found.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestCertificateCache_GetCertificate2_ThrowsWhenNoCertFound()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            provider.GetCertificate(CertificateIdentifier.BySubjectName("not.exist"), StoreName.My, StoreLocation.LocalMachine, DefaultRealTimeProvider.Singleton);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificate(X509FindType, string, out X509Certificate2)"/> will return false if cert if not found.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificate_ReturnsFalse()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("not.exist"), DefaultRealTimeProvider.Singleton, out _));
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificate(X509FindType, string, StoreLocation, out X509Certificate2)"/> will return false if cert if not found.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificate2_ReturnsFalse()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("not.exist"), StoreName.My, StoreLocation.CurrentUser, DefaultRealTimeProvider.Singleton, out _));
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificates(X509FindType, string, out X509Certificate2)"/> will return false if cert if not found.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates_ReturnsFalse()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsFalse(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("not.exist"), DefaultRealTimeProvider.Singleton, out _));
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificates(X509FindType, string, StoreLocation, out X509Certificate2)"/> will return false if cert if not found.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates2_ReturnsFalse()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsFalse(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("not.exist"), StoreName.My, StoreLocation.CurrentUser, DefaultRealTimeProvider.Singleton, out _));
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificates(X509FindType, string)"/> will throw exception if cert if not found.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestCertificateCache_GetCertificates_ThrowsWhenNoCertFound()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            provider.GetCertificates(CertificateIdentifier.BySubjectName("not.exist"), DefaultRealTimeProvider.Singleton);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificates(X509FindType, string, StoreLocation)"/> will throw exception if cert if not found.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestCertificateCache_GetCertificates2_ThrowsWhenNoCertFound()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            provider.GetCertificates(CertificateIdentifier.BySubjectName("not.exist"), StoreName.My, StoreLocation.CurrentUser, DefaultRealTimeProvider.Singleton);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificates(X509FindType, string, out X509Certificate2)"/> will return false if cert if not valid.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates_DoesntReturnExpiredCerts()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, expiredCert);
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, notYetValidCert);
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, certWithoutPrivateKey);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsFalse(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("bad"), DefaultRealTimeProvider.Singleton, out _));
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificates(X509FindType, string, StoreLocation, out X509Certificate2)"/> will return false if cert if not valid.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates2_DoesntReturnExpiredCerts()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, expiredCert);
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, notYetValidCert);
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, certWithoutPrivateKey);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsFalse(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("bad"), StoreName.My, StoreLocation.CurrentUser, DefaultRealTimeProvider.Singleton, out _));
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificates(X509FindType, string)"/> will throw exception if cert if not valid.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestCertificateCache_GetCertificates_ThrowsWhenCertIsExpired()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, expiredCert);
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, notYetValidCert);
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, certWithoutPrivateKey);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            provider.GetCertificates(CertificateIdentifier.BySubjectName("bad"), DefaultRealTimeProvider.Singleton);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificates(X509FindType, string, StoreLocation)"/> will throw exception if cert if not valid.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestCertificateCache_GetCertificates2_ThrowsWhenCertIsExpired()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, expiredCert);
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, notYetValidCert);
            certStore.StoreCertificate("bad", StoreLocation.CurrentUser, certWithoutPrivateKey);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            provider.GetCertificates(CertificateIdentifier.BySubjectName("bad"), StoreName.My, StoreLocation.CurrentUser, DefaultRealTimeProvider.Singleton);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificate(X509FindType, string, StoreLocation)"/> can't find a certificate
        /// if it exists, but isn't in the search location we specify.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestCertificateCache_GetCertificate_CantFindInOtherStoreLocation()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.a", StoreLocation.CurrentUser, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            provider.GetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.LocalMachine, DefaultRealTimeProvider.Singleton);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificate(X509FindType, string)"/> returns valid certificates when expected.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_GetCertificate_CurrentUser_GoldenPath()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.a", StoreLocation.CurrentUser, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);

            X509Certificate2 cert = provider.GetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(cert);
            Assert.IsTrue(CertificateHelpers.IsCertValidNear(cert, DateTime.Now));
            Assert.IsTrue(cert.HasPrivateKey);

            cert = provider.GetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.CurrentUser, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(cert);
            Assert.IsTrue(CertificateHelpers.IsCertValidNear(cert, DateTime.Now));
            Assert.IsTrue(cert.HasPrivateKey);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificate(X509FindType, string)"/> behaves correctly when fetching a cert multiple times in a row.
        /// Namely, a unique disposal handle is passed to the caller and they can safely dispose of it.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_GetCertificate_CurrentUser_DisposeOfHandle()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.a", StoreLocation.CurrentUser, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);

            for (int loop = 0; loop < 10; loop++)
            {
                using (X509Certificate2 cert = provider.GetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), DefaultRealTimeProvider.Singleton))
                {
                    Assert.IsNotNull(cert);
                    Assert.IsNotNull(cert.Thumbprint);
                    Assert.IsTrue(cert.HasPrivateKey);
                }
            }
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificate(X509FindType, string)"/> behaves correctly when fetching a cert multiple times in a row.
        /// Namely, a unique disposal handle is passed to the caller and they can safely dispose of it.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_GetCertificates_CurrentUser_DisposeOfHandle()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.a", StoreLocation.CurrentUser, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);

            for (int loop = 0; loop < 10; loop++)
            {
                var certs = provider.GetCertificates(CertificateIdentifier.BySubjectName("unit.test.subject.a"), DefaultRealTimeProvider.Singleton);
                foreach (var cert in certs)
                {
                    Assert.IsNotNull(cert);
                    Assert.IsNotNull(cert.Thumbprint);
                    Assert.IsTrue(cert.HasPrivateKey);
                    cert.Dispose();
                }
            }
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificate(X509FindType, string)"/> returns valid certificates when expected.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_GetCertificate_LocalMachine_GoldenPath()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.a", StoreLocation.LocalMachine, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);

            X509Certificate2 cert = provider.GetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(cert);
            Assert.IsTrue(CertificateHelpers.IsCertValidNear(cert, DateTime.Now));
            Assert.IsTrue(cert.HasPrivateKey);

            cert = provider.GetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.LocalMachine, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(cert);
            Assert.IsTrue(CertificateHelpers.IsCertValidNear(cert, DateTime.Now));
            Assert.IsTrue(cert.HasPrivateKey);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificate(X509FindType, string, out X509Certificate2)"/> returns valid certificates when expected.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificate_CurrentUser_GoldenPath()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.a", StoreLocation.CurrentUser, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);

            X509Certificate2 cert;
            Assert.IsTrue(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), DefaultRealTimeProvider.Singleton, out cert));
            Assert.IsNotNull(cert);
            Assert.IsTrue(CertificateHelpers.IsCertValidNear(cert, DateTime.Now));
            Assert.IsTrue(cert.HasPrivateKey);

            Assert.IsTrue(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.CurrentUser, DefaultRealTimeProvider.Singleton, out cert));
            Assert.IsNotNull(cert);
            Assert.IsTrue(CertificateHelpers.IsCertValidNear(cert, DateTime.Now));
            Assert.IsTrue(cert.HasPrivateKey);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificate(X509FindType, string, out X509Certificate2)"/> returns valid certificates when expected.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificate_LocalMachine_GoldenPath()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.a", StoreLocation.LocalMachine, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);

            X509Certificate2 cert;
            Assert.IsTrue(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), DefaultRealTimeProvider.Singleton, out cert));
            Assert.IsNotNull(cert);
            Assert.IsTrue(CertificateHelpers.IsCertValidNear(cert, DateTime.Now));
            Assert.IsTrue(cert.HasPrivateKey);

            Assert.IsTrue(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.LocalMachine, DefaultRealTimeProvider.Singleton, out cert));
            Assert.IsNotNull(cert);
            Assert.IsTrue(CertificateHelpers.IsCertValidNear(cert, DateTime.Now));
            Assert.IsTrue(cert.HasPrivateKey);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificateCredentials(X509FindType, string, StoreLocation)"/> can't find a certificate
        /// if it exists, but isn't in the search location we specify.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestCertificateCache_GetCertificateCredentials_CantFindInOtherStoreLocation()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.a", StoreLocation.CurrentUser, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            provider.GetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.LocalMachine, DefaultRealTimeProvider.Singleton);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificates(X509FindType, string, out X509Certificate2Collection)"/>
        /// returns valid certificates when expected.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates_GoldenPath()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, currentlyValidCert);
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, expiringSoonCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsTrue(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton, out var foundCerts));
            Assert.AreEqual(2, foundCerts.Count());
            Assert.IsNotNull(foundCerts.Single((s) => string.Equals(currentlyValidCert.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
            Assert.IsNotNull(foundCerts.Single((s) => string.Equals(expiringSoonCert.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificates(X509FindType, string, StoreLocation, out X509Certificate2Collection)"/>
        /// returns valid certificates when expected.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates2_GoldenPath()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, currentlyValidCert);
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, expiringSoonCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsTrue(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("subjectname"), StoreName.My, StoreLocation.CurrentUser, DefaultRealTimeProvider.Singleton, out var foundCerts));
            Assert.AreEqual(2, foundCerts.Count());
            Assert.IsNotNull(foundCerts.Single((s) => string.Equals(currentlyValidCert.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
            Assert.IsNotNull(foundCerts.Single((s) => string.Equals(expiringSoonCert.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificates(X509FindType, string)"/>
        /// returns valid certificates when expected.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_GetCertificates_GoldenPath()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, currentlyValidCert);
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, expiringSoonCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            var foundCerts = provider.GetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton);
            Assert.AreEqual(2, foundCerts.Count());
            Assert.IsNotNull(foundCerts.Single((s) => string.Equals(currentlyValidCert.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
            Assert.IsNotNull(foundCerts.Single((s) => string.Equals(expiringSoonCert.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificates(X509FindType, string, StoreLocation)"/>
        /// returns valid certificates when expected.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_GetCertificates2_GoldenPath()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, currentlyValidCert);
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, expiringSoonCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            var foundCerts = provider.GetCertificates(CertificateIdentifier.BySubjectName("subjectname"), StoreName.My, StoreLocation.CurrentUser, DefaultRealTimeProvider.Singleton);
            Assert.AreEqual(2, foundCerts.Count());
            Assert.IsNotNull(foundCerts.Single((s) => string.Equals(currentlyValidCert.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
            Assert.IsNotNull(foundCerts.Single((s) => string.Equals(expiringSoonCert.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificate(X509FindType, string, out X509Certificate)"/> can't find a certificate
        /// if it exists, but does not have a private key.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificate_OnlyReturnsPrivateKeyCerts()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.b", StoreLocation.CurrentUser, certWithoutPrivateKey);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.ab"), DefaultRealTimeProvider.Singleton, out _));
        }

        /// <summary>
        /// Tests that the certificate provider cache will only hit the backend once for a specific search criteria, even if no certificates are returned.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates_OnlyHitsBackendCacheOnce()
        {
            var certStore = new FakePhysicalCertStore();
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            ManualTimeProvider testTimeProvider = new ManualTimeProvider();
            testTimeProvider.Time = new DateTimeOffset(2020, 10, 31, 12, 0, 0, TimeSpan.Zero);

            Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out _));
            Assert.AreEqual(1, certStore.FetchCount);
            Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out _));
            Assert.AreEqual(1, certStore.FetchCount);

            Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.LocalMachine, testTimeProvider, out _));
            Assert.AreEqual(2, certStore.FetchCount);
            Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.LocalMachine, testTimeProvider, out _));
            Assert.AreEqual(2, certStore.FetchCount);

            Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), testTimeProvider, out _));
            Assert.AreEqual(2, certStore.FetchCount);

            for (int c = 0; c < 10; c++)
            {
                Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), testTimeProvider, out _));
                Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.LocalMachine, testTimeProvider, out _));
                Assert.IsFalse(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("unit.test.subject.a"), testTimeProvider, out _));
            }

            Assert.AreEqual(2, certStore.FetchCount);
        }

        /// <summary>
        /// Quick sanity check that our fake cert store behaves the same as the real one in regards to returning unique handles for each requested cert.
        /// </summary>
        [TestMethod]
        public void TestFakePhysicalCertStore_UniqueCertHandles()
        {
            var certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.a", StoreLocation.CurrentUser, currentlyValidCert);
            X509Certificate2 handleA = certStore.GetCertificates(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.CurrentUser, true).Single();
            X509Certificate2 handleB = certStore.GetCertificates(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.CurrentUser, true).Single();
            X509Certificate2 handleC = certStore.GetCertificates(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.CurrentUser, true).Single();
            X509Certificate2 handleD = certStore.GetCertificates(CertificateIdentifier.BySubjectName("unit.test.subject.a"), StoreName.My, StoreLocation.CurrentUser, true).Single();
            Assert.IsNotNull(handleA.Thumbprint);
            Assert.IsNotNull(handleB.Thumbprint);
            Assert.IsNotNull(handleC.Thumbprint);
            Assert.IsNotNull(handleD.Thumbprint);
            handleC.Dispose();
            Assert.IsNotNull(handleA.Thumbprint);
            Assert.IsNotNull(handleB.Thumbprint);
            Assert.IsNotNull(handleD.Thumbprint);
            handleA.Dispose();
            Assert.IsNotNull(handleB.Thumbprint);
            Assert.IsNotNull(handleD.Thumbprint);
            handleD.Dispose();
            Assert.IsNotNull(handleB.Thumbprint);
            handleB.Dispose();
        }

        /// <summary>
        /// Tests that the background cache of the cert store still queries the physical store periodically
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificate_RefreshesBackgroundCache()
        {
            ManualTimeProvider testTimeProvider = new ManualTimeProvider();
            testTimeProvider.Time = new DateTimeOffset(2020, 10, 31, 12, 0, 0, TimeSpan.Zero);

            var certStore = new FakePhysicalCertStore();
            using (X509Certificate2 testCert = CertificateGenerator.CreateSelfSignCertificate("unit.test.subject.x", notBeforeTime: testTimeProvider.Time.AddDays(-7).UtcDateTime, expireTime: testTimeProvider.Time.AddDays(7).UtcDateTime))
            {
                certStore.StoreCertificate("unit.test.subject.x", StoreLocation.CurrentUser, testCert);
                CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
                CertificateIdentifier certId = CertificateIdentifier.BySubjectName("unit.test.subject.x");

                X509Certificate2 cert;
                Assert.IsTrue(provider.TryGetCertificate(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out cert));
                Assert.AreEqual(1, certStore.FetchCount);
                Assert.IsNotNull(cert.Thumbprint);
                cert.Dispose();
                Assert.IsTrue(provider.TryGetCertificate(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out cert));
                Assert.AreEqual(1, certStore.FetchCount);
                Assert.IsNotNull(cert.Thumbprint);
                cert.Dispose();

                for (int c = 0; c < 10; c++)
                {
                    testTimeProvider.Time = testTimeProvider.Time.AddMinutes(6);
                    Assert.IsTrue(provider.TryGetCertificate(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out cert));
                    Assert.AreEqual(c + 2, certStore.FetchCount);
                    Assert.IsNotNull(cert.Thumbprint);
                    cert.Dispose();
                    Assert.IsTrue(provider.TryGetCertificate(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out cert));
                    Assert.AreEqual(c + 2, certStore.FetchCount);
                    Assert.IsNotNull(cert.Thumbprint);
                    cert.Dispose();
                }
            }
        }

        /// <summary>
        /// Tests that the background cache of the cert store still queries the physical store periodically
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates_RefreshesBackgroundCache()
        {
            ManualTimeProvider testTimeProvider = new ManualTimeProvider();
            testTimeProvider.Time = new DateTimeOffset(2020, 10, 31, 12, 0, 0, TimeSpan.Zero);

            var certStore = new FakePhysicalCertStore();
            using (X509Certificate2 testCert = CertificateGenerator.CreateSelfSignCertificate("unit.test.subject.x", notBeforeTime: testTimeProvider.Time.AddDays(-7).UtcDateTime, expireTime: testTimeProvider.Time.AddDays(7).UtcDateTime))
            {
                certStore.StoreCertificate("unit.test.subject.x", StoreLocation.CurrentUser, testCert);
                CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
                CertificateIdentifier certId = CertificateIdentifier.BySubjectName("unit.test.subject.x");

                X509Certificate2Collection certs;
                Assert.IsTrue(provider.TryGetCertificates(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out certs));
                Assert.AreEqual(1, certStore.FetchCount);
                Assert.AreEqual(1, certs.Count);
                Assert.IsNotNull(certs[0].Thumbprint);
                foreach (var cert in certs)
                {
                    cert.Dispose();
                }

                Assert.IsTrue(provider.TryGetCertificates(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out certs));
                Assert.AreEqual(1, certStore.FetchCount);
                Assert.AreEqual(1, certs.Count);
                Assert.IsNotNull(certs[0].Thumbprint);
                foreach (var cert in certs)
                {
                    cert.Dispose();
                }

                for (int c = 0; c < 10; c++)
                {
                    testTimeProvider.Time = testTimeProvider.Time.AddMinutes(6);
                    Assert.IsTrue(provider.TryGetCertificates(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out certs));
                    Assert.AreEqual(c + 2, certStore.FetchCount);
                    Assert.AreEqual(1, certs.Count);
                    Assert.IsNotNull(certs[0].Thumbprint);
                    foreach (var cert in certs)
                    {
                        cert.Dispose();
                    }

                    Assert.IsTrue(provider.TryGetCertificates(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out certs));
                    Assert.AreEqual(c + 2, certStore.FetchCount);
                    Assert.AreEqual(1, certs.Count);
                    Assert.IsNotNull(certs[0].Thumbprint);
                    foreach (var cert in certs)
                    {
                        cert.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Tests that the background cache of the cert store will pick up newly added certs with later expiration
        /// and start returning them seamlessly
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_HandlesNewCertificateHotSwap()
        {
            ManualTimeProvider testTimeProvider = new ManualTimeProvider();
            testTimeProvider.Time = new DateTimeOffset(2020, 10, 31, 12, 0, 0, TimeSpan.Zero);

            var certStore = new FakePhysicalCertStore();
            using (X509Certificate2 oldCert = CertificateGenerator.CreateSelfSignCertificate("unit.test.subject.x", notBeforeTime: testTimeProvider.Time.AddDays(-7).UtcDateTime, expireTime: testTimeProvider.Time.AddDays(2).UtcDateTime))
            using (X509Certificate2 newCert = CertificateGenerator.CreateSelfSignCertificate("unit.test.subject.x", notBeforeTime: testTimeProvider.Time.AddDays(-2).UtcDateTime, expireTime: testTimeProvider.Time.AddDays(14).UtcDateTime))
            {
                certStore.StoreCertificate("unit.test.subject.x", StoreLocation.CurrentUser, oldCert);
                CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
                CertificateIdentifier certId = CertificateIdentifier.BySubjectName("unit.test.subject.x");

                X509Certificate2 cert;
                X509Certificate2Collection certs;
                for (int loop = 0; loop < 3; loop++)
                {
                    Assert.IsTrue(provider.TryGetCertificate(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out cert));
                    Assert.AreEqual(1, certStore.FetchCount);
                    Assert.AreEqual(oldCert.Thumbprint, cert.Thumbprint);
                    cert.Dispose();

                    Assert.IsTrue(provider.TryGetCertificates(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out certs));
                    Assert.AreEqual(1, certStore.FetchCount);
                    Assert.AreEqual(1, certs.Count);
                    Assert.AreEqual(oldCert.Thumbprint, certs[0].Thumbprint);
                    foreach (var c in certs)
                    {
                        c.Dispose();
                    }
                }

                certStore.StoreCertificate("unit.test.subject.x", StoreLocation.CurrentUser, newCert);
                testTimeProvider.Time = testTimeProvider.Time.AddMinutes(6);

                for (int loop = 0; loop < 3; loop++)
                {
                    Assert.IsTrue(provider.TryGetCertificate(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out cert));
                    Assert.AreEqual(2, certStore.FetchCount);
                    Assert.AreEqual(newCert.Thumbprint, cert.Thumbprint);
                    cert.Dispose();

                    Assert.IsTrue(provider.TryGetCertificates(certId, StoreName.My, StoreLocation.CurrentUser, testTimeProvider, out certs));
                    Assert.AreEqual(2, certStore.FetchCount);
                    Assert.AreEqual(2, certs.Count);
                    Assert.AreEqual(newCert.Thumbprint, certs[0].Thumbprint);
                    Assert.AreEqual(oldCert.Thumbprint, certs[1].Thumbprint);
                    foreach (var c in certs)
                    {
                        c.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificate(X509FindType, string, out X509Certificate)"/>
        /// won't return certs that are not yet valid or have expired
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificate_IgnoresExpiredAndNotYetValidCerts()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.c", StoreLocation.CurrentUser, notYetValidCert);
            certStore.StoreCertificate("unit.test.subject.d", StoreLocation.CurrentUser, expiredCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.c"), DefaultRealTimeProvider.Singleton, out _));
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificate(X509FindType, string, out X509Certificate)"/>
        /// will return a valid certificate if there is a mix of valid + invalid certs
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificate_IgnoresExpiredAndNotYetValidCerts2()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("unit.test.subject.c", StoreLocation.CurrentUser, notYetValidCert);
            certStore.StoreCertificate("unit.test.subject.c", StoreLocation.CurrentUser, expiredCert);
            certStore.StoreCertificate("unit.test.subject.c", StoreLocation.CurrentUser, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsTrue(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("unit.test.subject.c"), DefaultRealTimeProvider.Singleton, out var foundCert));
            Assert.AreEqual(currentlyValidCert.GetSerialNumberString(), foundCert.GetSerialNumberString());
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificate(X509FindType, string, out X509Certificate)"/>
        /// will stop returning a certificate if it expires while in the cache
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_StopsReturningExpiredCertificates()
        {
            ManualTimeProvider testTimeProvider = new ManualTimeProvider();
            testTimeProvider.Time = new DateTimeOffset(2020, 10, 31, 12, 0, 0, TimeSpan.Zero);
            using (X509Certificate2 aboutToExpireCert = CertificateGenerator.CreateSelfSignCertificate(
                "about.to.expire",
                notBeforeTime: testTimeProvider.Time.AddDays(-7).UtcDateTime,
                expireTime: testTimeProvider.Time.AddMinutes(10).AddSeconds(3).UtcDateTime)) // 10 minutes is the buffer time used by the cache, so it should stop being considered valid 3 seconds from now
            {
                FakePhysicalCertStore certStore = new FakePhysicalCertStore();
                certStore.StoreCertificate("about.to.expire", StoreLocation.CurrentUser, aboutToExpireCert);
                CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
                Assert.IsTrue(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("about.to.expire"), testTimeProvider, out _));
                Assert.IsTrue(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("about.to.expire"), testTimeProvider, out _));

                testTimeProvider.Time = testTimeProvider.Time.AddMinutes(5).AddSeconds(3);

                Assert.IsFalse(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("about.to.expire"), testTimeProvider, out _));
                Assert.IsFalse(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("about.to.expire"), testTimeProvider, out _));
            }
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificate(X509FindType, string, out X509Certificate)"/>
        /// will return the certificate with the expire time farthest in the future, if duplicates are found.
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_UsesMostUpdatedCertsWhenPossible()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, expiringSoonCert);
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsTrue(provider.TryGetCertificate(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton, out var foundCert));
            Assert.AreEqual(currentlyValidCert.GetSerialNumberString(), foundCert.GetSerialNumberString());
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificates(X509FindType, string)"/>
        /// will return the correct union if certs are found in CurrentUser store
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_GetCertificates_FetchesFromCurrentUserStore()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            var foundCerts = provider.GetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton);
            Assert.AreEqual(1, foundCerts.Count);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificates(X509FindType, string)"/>
        /// will return the correct union if certs are found in LocalMachine store
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_GetCertificates_FetchesFromLocalMachineStore()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.LocalMachine, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            var foundCerts = provider.GetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton);
            Assert.AreEqual(1, foundCerts.Count);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificates(X509FindType, string)"/>
        /// will return the correct union if certs are found in multiple stores
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_GetCertificates_UnionsProperlyFromBothStores()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.LocalMachine, currentlyValidCert);
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, expiringSoonCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            var foundCerts = provider.GetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton);
            Assert.AreEqual(2, foundCerts.Count);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificates(X509FindType, string)"/>
        /// will return the correct result if an invalid certificate is in CurrentUser and a valid one in LocalMachine
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_GetCertificates_UnionsProperlyFromBothStores2()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.LocalMachine, currentlyValidCert);
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, expiredCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            var foundCerts = provider.GetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton);
            Assert.AreEqual(1, foundCerts.Count);
            Assert.AreEqual(currentlyValidCert.GetSerialNumberString(), foundCerts.First().GetSerialNumberString());
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.GetCertificates(X509FindType, string)"/>
        /// will return the correct result if an invalid certificate is in LocalMachine and a valid one in CurrentUser
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_GetCertificates_UnionsProperlyFromBothStores3()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, currentlyValidCert);
            certStore.StoreCertificate("subjectname", StoreLocation.LocalMachine, expiredCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            var foundCerts = provider.GetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton);
            Assert.AreEqual(1, foundCerts.Count);
            Assert.AreEqual(currentlyValidCert.GetSerialNumberString(), foundCerts.First().GetSerialNumberString());
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificates(X509FindType, string, out X509Certificate2Collection)"/>
        /// will return the correct union if certs are found in CurrentUser store
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates_FetchesFromCurrentUserStore()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsTrue(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton, out var foundCerts));
            Assert.AreEqual(1, foundCerts.Count);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificates(X509FindType, string, out X509Certificate2Collection)"/>
        /// will return the correct union if certs are found in LocalMachine store
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates_FetchesFromLocalMachineStore()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.LocalMachine, currentlyValidCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsTrue(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton, out var foundCerts));
            Assert.AreEqual(1, foundCerts.Count);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificates(X509FindType, string, out X509Certificate2Collection)"/>
        /// will return the correct union if certs are found in multiple stores
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates_UnionsProperlyFromBothStores()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.LocalMachine, currentlyValidCert);
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, expiringSoonCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsTrue(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton, out var foundCerts));
            Assert.AreEqual(2, foundCerts.Count);
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificates(X509FindType, string, out X509Certificate2Collection)"/>
        /// will return the correct result if an invalid certificate is in CurrentUser and a valid one in LocalMachine
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates_UnionsProperlyFromBothStores2()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.LocalMachine, currentlyValidCert);
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, expiredCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsTrue(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton, out var foundCerts));
            Assert.AreEqual(1, foundCerts.Count);
            Assert.AreEqual(currentlyValidCert.GetSerialNumberString(), foundCerts.First().GetSerialNumberString());
        }

        /// <summary>
        /// Tests that <see cref="CertificateCache.TryGetCertificates(X509FindType, string, out X509Certificate2Collection)"/>
        /// will return the correct result if an invalid certificate is in LocalMachine and a valid one in CurrentUser
        /// </summary>
        [TestMethod]
        public void TestCertificateCache_TryGetCertificates_UnionsProperlyFromBothStores3()
        {
            FakePhysicalCertStore certStore = new FakePhysicalCertStore();
            certStore.StoreCertificate("subjectname", StoreLocation.CurrentUser, currentlyValidCert);
            certStore.StoreCertificate("subjectname", StoreLocation.LocalMachine, expiredCert);
            CertificateCache provider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
            Assert.IsTrue(provider.TryGetCertificates(CertificateIdentifier.BySubjectName("subjectname"), DefaultRealTimeProvider.Singleton, out var foundCerts));
            Assert.AreEqual(1, foundCerts.Count);
            Assert.AreEqual(currentlyValidCert.GetSerialNumberString(), foundCerts.First().GetSerialNumberString());
        }

        /// <summary>
        /// Tests that the certificate provider's internal cache is completely thread safe.
        /// </summary>
        [TestMethod]
        public async Task TestCertificateCache_ThreadSafety()
        {
            ConcurrentQueue<CertificateCache> providerQueue = new ConcurrentQueue<CertificateCache>();
            const int NUM_THREADS = 8;
            const int NUM_ITERATIONS = 1000;
            using (Barrier workBarrier = new Barrier(NUM_THREADS + 1))
            using (CancellationTokenSource testAbort = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                CancellationToken testAbortToken = testAbort.Token;
                List<Task> threads = new List<Task>();

                // Set up test threads
                for (int thread = 0; thread < NUM_THREADS; thread++)
                {
                    threads.Add(Task.Run(() =>
                    {
                        /////// WORKER THREAD SPACE /////////
                        Random rand = new Random();
                        for (int iteration = 0; iteration < NUM_ITERATIONS; iteration++)
                        {
                            /////// BARRIER SIGNAL - Thread ready
                            // Wait for supervisor to create new provider
                            workBarrier.SignalAndWait(testAbortToken);

                            // Get provider for this thread to use
                            Assert.IsTrue(providerQueue.TryDequeue(out CertificateCache currentProvider));

                            /////// BARRIER SIGNAL - Begin iteration
                            workBarrier.SignalAndWait(testAbortToken);

                            // Do some random operations on the certificate provider
                            for (int thrash = 0; thrash < 50; thrash++)
                            {
                                switch (rand.Next(0, 10))
                                {
                                    case 0:
                                    case 1:
                                        Assert.IsTrue(currentProvider.TryGetCertificate(CertificateIdentifier.BySubjectName("cert.a"), DefaultRealTimeProvider.Singleton, out _));
                                        break;
                                    case 2:
                                    case 3:
                                        Assert.IsNotNull(currentProvider.GetCertificate(CertificateIdentifier.BySubjectName("cert.b"), DefaultRealTimeProvider.Singleton));
                                        break;
                                    case 4:
                                    case 5:
                                        Assert.IsTrue(currentProvider.TryGetCertificates(CertificateIdentifier.BySubjectName("cert.a"), DefaultRealTimeProvider.Singleton, out _));
                                        break;
                                    case 7:
                                    case 6:
                                        Assert.IsNotNull(currentProvider.GetCertificates(CertificateIdentifier.BySubjectName("cert.a"), DefaultRealTimeProvider.Singleton));
                                        break;
                                    default:
                                        Assert.IsFalse(currentProvider.TryGetCertificate(CertificateIdentifier.BySubjectName("not.exist"), DefaultRealTimeProvider.Singleton, out _));
                                        break;
                                }
                            }
                        }
                        /////////////////////////////////////
                    }));
                }

                ///////// SUPERVISOR SPACE ///////////
                Random rand = new Random();
                X509Certificate2[] fillerCerts = new X509Certificate2[]
                {
                    currentlyValidCert,
                    expiredCert,
                    expiringSoonCert,
                    notYetValidCert,
                    certWithoutPrivateKey,
                };

                for (int iteration = 0; iteration < NUM_ITERATIONS; iteration++)
                {
                    // Prepare the CertificateCache for the next iteration
                    FakePhysicalCertStore certStore = new FakePhysicalCertStore();

                    // Set up some different configurations of the backing store.
                    // Only guarantee that there is at least one valid cert.a and cert.b, the rest doesn't matter
                    int preFillerCerts = rand.Next(0, 5);
                    for (int c = 0; c < preFillerCerts; c++)
                    {
                        certStore.StoreCertificate(
                            "cert." + ('a' + rand.Next(0, 5)),
                            rand.Next(0, 1) == 0 ? StoreLocation.CurrentUser : StoreLocation.LocalMachine,
                            fillerCerts[rand.Next(0, fillerCerts.Length)]);
                    }

                    certStore.StoreCertificate("cert.a", rand.Next(0, 1) == 0 ? StoreLocation.CurrentUser : StoreLocation.LocalMachine, currentlyValidCert);
                    certStore.StoreCertificate("cert.b", rand.Next(0, 1) == 0 ? StoreLocation.CurrentUser : StoreLocation.LocalMachine, currentlyValidCert);

                    int postFillerCerts = rand.Next(0, 5);
                    for (int c = 0; c < postFillerCerts; c++)
                    {
                        certStore.StoreCertificate(
                            "cert." + ('a' + rand.Next(0, 5)),
                            rand.Next(0, 1) == 0 ? StoreLocation.CurrentUser : StoreLocation.LocalMachine,
                            fillerCerts[rand.Next(0, fillerCerts.Length)]);
                    }

                    CertificateCache newProvider = new CertificateCache(TimeSpan.FromMinutes(5), certStore);
                    for (int thread = 0; thread < NUM_THREADS; thread++)
                    {
                        providerQueue.Enqueue(newProvider); // have to queue 1 separate provider for each thread
                    }

                    /////// BARRIER SIGNAL - Thread ready
                    workBarrier.SignalAndWait(testAbortToken);

                    /////// BARRIER SIGNAL - Begin iteration
                    workBarrier.SignalAndWait(testAbortToken);
                }

                foreach (Task t in threads)
                {
                    // Any exceptions from the worker threads will be propagated here
                    await t.ConfigureAwait(false);
                }

                /////////////////////////////////////
            }
        }
    }
}
