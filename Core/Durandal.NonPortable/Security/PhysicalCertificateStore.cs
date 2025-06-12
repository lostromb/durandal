namespace Durandal.Common.Security
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Security;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Utils;

    /// <summary>
    /// Implements the <see cref="IPhysicalCertificateStore"/> interface using the actual current system's
    /// certificate stores. This is the class that will actually be used in production.
    /// </summary>
    public sealed class PhysicalCertificateStore : IPhysicalCertificateStore
    {
        /// <summary>
        /// Lazy factory for PhysicalCertificateStore singleton.
        /// </summary>
        private static readonly Lazy<PhysicalCertificateStore> LazyInstance =
                new Lazy<PhysicalCertificateStore>(() => new PhysicalCertificateStore(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Rather than opening an X509Store every time, we open them once and keep them open
        /// </summary>
        private readonly ConcurrentDictionary<PhysicalStoreLookupKey, X509Store> _allPhysicalStores = new ConcurrentDictionary<PhysicalStoreLookupKey, X509Store>();

        /// <summary>
        /// Flag for atomic disposal of this class.
        /// </summary>
        private int _disposed = 0;

        internal PhysicalCertificateStore()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PhysicalCertificateStore()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Gets the shared physical certificate store for this runtime instance.
        /// </summary>
        public static PhysicalCertificateStore Instance => LazyInstance.Value;

        /// <inheritdoc />
        public IEnumerable<X509Certificate2> GetCertificates(
            CertificateIdentifier certificateId,
            StoreName storeName,
            StoreLocation storeLocation,
            bool withPrivateKey)
        {
            certificateId.AssertNonNull(nameof(certificateId));

            X509FindType findType;
            string criteria;
            if (!string.IsNullOrEmpty(certificateId.SubjectDistinguishedName))
            {
                findType = X509FindType.FindBySubjectDistinguishedName;
                criteria = certificateId.SubjectDistinguishedName;
            }
            else if (!string.IsNullOrEmpty(certificateId.SubjectName))
            {
                findType = X509FindType.FindBySubjectName;
                criteria = certificateId.SubjectName;
            }
            else if (!string.IsNullOrEmpty(certificateId.Thumbprint))
            {
                findType = X509FindType.FindByThumbprint;
                criteria = certificateId.Thumbprint;
            }
            else
            {
                throw new ArgumentException("Could not determine X509FindType for certificate identifier " + certificateId.ToString());
            }

            PhysicalStoreLookupKey storeKey = new PhysicalStoreLookupKey(storeName, storeLocation);
            X509Store handleToStore = _allPhysicalStores.GetOrAdd(storeKey, OpenNewSystemStore);
            var certificates = handleToStore.Certificates
                .Find(findType, criteria, false)
                .Find(X509FindType.FindByTimeValid, DateTime.UtcNow, validOnly: false)
                .Cast<X509Certificate2>()
                .OrderByDescending(cert => cert.NotAfter);

            if (withPrivateKey)
            {
                return CertificateHelpers.SelectCertsWithPrivateKey(certificates);
            }

            return certificates;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Implements the Dispose pattern.
        /// </summary>
        /// <param name="disposing">Whether Dispose was called on this instance.</param>
        private void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            try
            {
                if (disposing)
                {
                    foreach (var store in _allPhysicalStores.Values)
                    {
                        store.Dispose();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Static function to open a new store, put down here so we don't allocate a closure when
        /// querying the store dictionary.
        /// </summary>
        /// <param name="key">The store key being opened.</param>
        /// <returns>A newly opened <see cref="X509Store"/> corresponding to that lookup key.</returns>
        private static X509Store OpenNewSystemStore(PhysicalStoreLookupKey key)
        {
            X509Store returnVal = new X509Store(key.Store_Name, key.Store_Location);
            returnVal.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            return returnVal;
        }

        /// <summary>
        /// The lookup key for uniquely identifying physical certificate stores on the system.
        /// Consists of a pair of StoreName and StoreLocation.
        /// </summary>
        private struct PhysicalStoreLookupKey : IEquatable<PhysicalStoreLookupKey>
        {
            /// <summary>
            /// The StoreName parameter.
            /// </summary>
            public StoreName Store_Name { get; private set; }

            /// <summary>
            /// The StoreLocation parameter.
            /// </summary>
            public StoreLocation Store_Location { get; private set; }

            /// <summary>
            /// Constructs a new <see cref="PhysicalStoreLookupKey"/> with constant parameters.
            /// </summary>
            /// <param name="storeName">The physical store name (e.g. My)</param>
            /// <param name="storeLocation">The physical store location (e.g. CurrentUser)</param>
            public PhysicalStoreLookupKey(StoreName storeName, StoreLocation storeLocation)
            {
                Store_Name = storeName;
                Store_Location = storeLocation;
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                // Since these are enums with small ranges, treat them as int16s
                return ((int)Store_Name) | (((int)Store_Location) << 16);
            }

            /// <inheritdoc />
            public bool Equals(PhysicalStoreLookupKey other)
            {
                return Store_Name == other.Store_Name &&
                    Store_Location == other.Store_Location;
            }
        }
    }
}
