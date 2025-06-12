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
    using Durandal.Common.Time;

    [TestClass]
    [DoNotParallelize]
    public class PhysicalCertificateStoreTests
    {
        /// <summary>
        /// Tests that <see cref="PhysicalCertificateStore"/> accesses the local machine's cert store as expected.
        /// </summary>
        [TestMethod]
        public void TestPhysicalCertificateStore_CommonPaths()
        {
            // Generate a transient certificate and store it in CurrentUser store on the machine running the test.
            // We can't test the LocalMachine store path unfortunately because that would require admin permissions on the test driver
            string subjectName = $"durandal.unit.test.{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            using (X509Certificate2 transientCertificate = CertificateGenerator.CreateSelfSignCertificate(subjectName, withPrivateKey: false, exportable: true))
            using (X509Certificate2 transientCertificateWithPrivateKey = CertificateGenerator.CreateSelfSignCertificate(subjectName, withPrivateKey: true, exportable: true))
            using (X509Store currentUserStore = new X509Store(StoreName.My, StoreLocation.CurrentUser, OpenFlags.ReadWrite))
            {
                currentUserStore.Open(OpenFlags.ReadWrite);
                currentUserStore.Add(transientCertificate);
                currentUserStore.Add(transientCertificateWithPrivateKey);
                try
                {
                    using (PhysicalCertificateStore store = new PhysicalCertificateStore())
                    {
                        var certs = store.GetCertificates(CertificateIdentifier.BySubjectName("not.existent.certificate"), StoreName.My, StoreLocation.CurrentUser, withPrivateKey: false);
                        Assert.AreEqual(0, certs.Count());

                        certs = store.GetCertificates(CertificateIdentifier.BySubjectName(subjectName), StoreName.My, StoreLocation.CurrentUser, withPrivateKey: false);
                        Assert.AreEqual(2, certs.Count());
                        Assert.IsNotNull(certs.Single((s) => string.Equals(transientCertificate.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
                        Assert.IsNotNull(certs.Single((s) => string.Equals(transientCertificateWithPrivateKey.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
                        foreach (var c in certs) { c.Dispose(); }

                        certs = store.GetCertificates(CertificateIdentifier.BySubjectName(subjectName), StoreName.My, StoreLocation.CurrentUser, withPrivateKey: true);
                        Assert.AreEqual(1, certs.Count());
                        Assert.IsNotNull(certs.Single((s) => string.Equals(transientCertificateWithPrivateKey.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
                        foreach (var c in certs) { c.Dispose(); }

                        certs = store.GetCertificates(CertificateIdentifier.ByThumbprint(transientCertificate.Thumbprint), StoreName.My, StoreLocation.CurrentUser, withPrivateKey: false);
                        Assert.AreEqual(1, certs.Count());
                        Assert.IsNotNull(certs.Single((s) => string.Equals(transientCertificate.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
                        foreach (var c in certs) { c.Dispose(); }

                        certs = store.GetCertificates(CertificateIdentifier.ByThumbprint(transientCertificateWithPrivateKey.Thumbprint), StoreName.My, StoreLocation.CurrentUser, withPrivateKey: false);
                        Assert.AreEqual(1, certs.Count());
                        Assert.IsNotNull(certs.Single((s) => string.Equals(transientCertificateWithPrivateKey.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
                        foreach (var c in certs) { c.Dispose(); }

                        certs = store.GetCertificates(CertificateIdentifier.BySubjectDistinguishedName("not.existent.certificate.sdn"), StoreName.My, StoreLocation.CurrentUser, withPrivateKey: false);
                        Assert.AreEqual(0, certs.Count());

                        // Make sure we can access multiple stores and they return distinct results
                        certs = store.GetCertificates(CertificateIdentifier.BySubjectName(subjectName), StoreName.Disallowed, StoreLocation.CurrentUser, withPrivateKey: false);
                        Assert.AreEqual(0, certs.Count());
                        certs = store.GetCertificates(CertificateIdentifier.BySubjectName(subjectName), StoreName.My, StoreLocation.LocalMachine, withPrivateKey: false);
                        Assert.AreEqual(0, certs.Count());

                        // Make sure if we get the same cert multiple times, and dispose of one of the handles, that the other one remains valid (in other words, handles are not reused)
                        // This logic is important to avoiding memory leaks in the cert cache as it tries to hold on to only one unique handle for each physical cert.
                        X509Certificate2 handleA = store.GetCertificates(CertificateIdentifier.BySubjectName(subjectName), StoreName.My, StoreLocation.CurrentUser, withPrivateKey: true).Single();
                        X509Certificate2 handleB = store.GetCertificates(CertificateIdentifier.BySubjectName(subjectName), StoreName.My, StoreLocation.CurrentUser, withPrivateKey: true).Single();
                        Assert.AreNotEqual(IntPtr.Zero, handleA.Handle);
                        Assert.AreNotEqual(IntPtr.Zero, handleB.Handle);
                        handleA.Dispose();
                        Assert.AreEqual(IntPtr.Zero, handleA.Handle);
                        Assert.AreNotEqual(IntPtr.Zero, handleB.Handle);

                        // Test the certificate cache operating through its singleton against the physical store
                        X509Certificate2 cert;
                        Assert.IsTrue(CertificateCache.Instance.TryGetCertificate(CertificateIdentifier.BySubjectName(subjectName), DefaultRealTimeProvider.Singleton, out cert));
                        cert.Dispose();
                    }
                }
                finally
                {
                    // Make absolutely sure we clean up everything when done.
                    try
                    {
                        currentUserStore.Remove(transientCertificate);
                        currentUserStore.Remove(transientCertificateWithPrivateKey);
                    }
                    catch (Exception)
                    {
                    }

                    currentUserStore.Close();
                }
            }
        }

        /// <summary>
        /// Tests that <see cref="PhysicalCertificateStore"/> detects newly installed certificates while the program is running.
        /// </summary>
        [TestMethod]
        public void TestPhysicalCertificateStore_HotSwapCert()
        {
            // Generate a transient certificate and store it in CurrentUser store on the machine running the test.
            // We can't test the LocalMachine store path unfortunately because that would require admin permissions on the test driver
            string subjectName = $"durandal.unit.test.{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            using (X509Certificate2 transientCertificate = CertificateGenerator.CreateSelfSignCertificate(subjectName, withPrivateKey: false, exportable: true))
            using (X509Store currentUserStore = new X509Store(StoreName.My, StoreLocation.CurrentUser, OpenFlags.ReadWrite))
            {
                currentUserStore.Open(OpenFlags.ReadWrite);

                try
                {
                    using (PhysicalCertificateStore store = new PhysicalCertificateStore())
                    {
                        // Results are initially empty
                        var certs = store.GetCertificates(CertificateIdentifier.BySubjectName(subjectName), StoreName.My, StoreLocation.CurrentUser, withPrivateKey: false);
                        Assert.AreEqual(0, certs.Count());

                        // Add the certificate now
                        currentUserStore.Add(transientCertificate);

                        // And check that it shows up without us having to recreate the backing cert store
                        certs = store.GetCertificates(CertificateIdentifier.BySubjectName(subjectName), StoreName.My, StoreLocation.CurrentUser, withPrivateKey: false);
                        Assert.AreEqual(1, certs.Count());
                        Assert.IsNotNull(certs.Single((s) => string.Equals(transientCertificate.GetSerialNumberString(), s.GetSerialNumberString(), StringComparison.Ordinal)));
                        foreach (var c in certs) { c.Dispose(); }
                    }
                }
                finally
                {
                    // Make absolutely sure we clean up everything when done.
                    try
                    {
                        currentUserStore.Remove(transientCertificate);
                    }
                    catch (Exception)
                    {
                    }

                    currentUserStore.Close();
                }
            }
        }

        [TestMethod]
        public void TestPhysicalCertificateStore_MultipleDisposal()
        {
            using (PhysicalCertificateStore store = new PhysicalCertificateStore())
            {
                store.GetCertificates(CertificateIdentifier.ByThumbprint("not.exist.thumbprint"), StoreName.My, StoreLocation.CurrentUser, false);
                store.Dispose();
            }
        }
    }
}
