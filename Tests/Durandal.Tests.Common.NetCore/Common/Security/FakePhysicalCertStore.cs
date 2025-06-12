// <copyright file="FakePhysicalCertStore.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Durandal.Tests.Common.Security
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Security;

    /// <summary>
    /// Fake implementation of <see cref="IPhysicalCertificateStore"/> for unit testing.
    /// </summary>
    internal class FakePhysicalCertStore : IPhysicalCertificateStore
    {
        private readonly List<StoredCert> _storedCerts = new List<StoredCert>();
        private int _fetchCount = 0;

        /// <inheritdoc />
        public IEnumerable<X509Certificate2> GetCertificates(
            CertificateIdentifier certificateId,
            StoreName storeName,
            StoreLocation storeLocation,
            bool withPrivateKey)
        {
            Interlocked.Increment(ref _fetchCount);
            string subjectName = string.IsNullOrEmpty(certificateId.SubjectName) ? certificateId.SubjectDistinguishedName : certificateId.SubjectName;
            if (!string.IsNullOrEmpty(subjectName))
            {
                IEnumerable<X509Certificate2> certificates = _storedCerts.Where((s) =>
                    string.Equals(s.SubjectName, subjectName, StringComparison.OrdinalIgnoreCase) &&
                    storeName == s.StoreName &&
                    storeLocation == s.StoreLocation)
                    .Select((s) => s.Certificate)
                    .OrderByDescending(cert => cert.NotAfter);

                if (withPrivateKey)
                {
                    return CertificateHelpers.SelectCertsWithPrivateKey(certificates).Select((cert) => new X509Certificate2(cert));
                }
                else
                {
                    // Make a clone of the certificate handle so disposing of one doesn't dispose of a singletons
                    return certificates.Select((cert) => new X509Certificate2(cert));
                }
            }
            else
            {
                throw new Exception("Only subject name lookup is supported in fake cert store");
            }
        }

        /// <summary>
        /// Adds a certificate object to this fake cert store.
        /// </summary>
        /// <param name="subjectName">The subject name of the cert.</param>
        /// <param name="storeLocation">The location to "store" the cert.</param>
        /// <param name="cert">The certificate.</param>
        public void StoreCertificate(string subjectName, StoreLocation storeLocation, X509Certificate2 cert)
        {
            _storedCerts.Add(
                new StoredCert()
                {
                    SubjectName = subjectName,
                    StoreName = StoreName.My,
                    StoreLocation = storeLocation,
                    Certificate = cert,
                });
        }

        /// <summary>
        /// Gets the number of times that this physical store has been queried in its lifetime.
        /// </summary>
        public int FetchCount => _fetchCount;

        public void Dispose()
        {
            // Don't actually dispose of any certificates; we rely on
            // the test driver to manage them
        }

        private class StoredCert
        {
            public string SubjectName { get; set; }

            public StoreName StoreName { get; set; }

            public StoreLocation StoreLocation { get; set; }

            public X509Certificate2 Certificate { get; set; }
        }
    }
}
