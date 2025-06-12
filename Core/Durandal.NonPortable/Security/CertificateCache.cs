namespace Durandal.Common.Security
{
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;

    /// <summary>
    /// A static certificate loader and cache providing access to centrally-managed <see cref="X509Certificate2"/> objects.
    /// Use <see cref="Instance"/> in place of direct calls to <see cref="X509Store"/> as caching and validity checking is provided for you automatically.
    /// </summary>
    public sealed class CertificateCache : ICertificateCache
    {
        /// <summary>
        /// The default lifetime for a cache entry in the certificate cache. When an entry expires, the physical cert store is queried again to check for
        /// any newly added certificates. This is to support long-running services with certificate hot swapping.
        /// </summary>
        public static readonly TimeSpan DEFAULT_CACHE_LIFETIME = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Lazy factory for CertificateCache singleton.
        /// </summary>
        private static readonly Lazy<CertificateCache> LazyInstance =
                new Lazy<CertificateCache>(() => new CertificateCache(DEFAULT_CACHE_LIFETIME), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Cache for all loaded certificates by this provider.
        /// Key is a struct containing {certFindType}, {storeName }, {storeLocation}, {searchCriteria}".
        /// Value is the list of all certificates which match that criteria, ordered by longest validity first.
        /// The list may be empty if no certificates matched - this is intended
        /// to avoid reopening the cert store on every cache miss.
        /// </summary>
        private readonly ConcurrentDictionary<CertificateCacheKey, CertificateCacheEntry> _certificateCache =
            new ConcurrentDictionary<CertificateCacheKey, CertificateCacheEntry>();

        /// <summary>
        /// The actual backing certificate source on this machine.
        /// Only really relevant for unit tests when we mock it out.
        /// </summary>
        private readonly IPhysicalCertificateStore _physicalStore;

        /// <summary>
        /// The amount of time a certificate can be cached and used
        /// before re-checking the backing certificate store for updates.
        /// If updated, 
        /// </summary>
        private TimeSpan _cacheEntryLifetime;

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificateCache"/> class.
        /// Private constructor so we can only ever instantiate the singleton (outside of unit tests).
        /// </summary>
        /// <param name="cacheLifetime">The maximum amount of time to keep an entry in the cache</param>
        internal CertificateCache(TimeSpan cacheLifetime) : this(cacheLifetime, PhysicalCertificateStore.Instance)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificateCache"/> class.
        /// This is the internal constructor intended for unit testing.
        /// </summary>
        /// <param name="cacheLifetime">The maximum amount of time to keep an entry in the cache</param>
        /// <param name="certStore">The physical backing certificate store to use.</param>
        internal CertificateCache(TimeSpan cacheLifetime, IPhysicalCertificateStore certStore)
        {
            CacheEntryLifetime = cacheLifetime;
            _physicalStore = certStore.AssertNonNull(nameof(certStore));
        }

        /// <summary>
        /// Gets the shared certificate provider for this runtime instance.
        /// </summary>
        public static CertificateCache Instance => LazyInstance.Value;

        /// <summary>
        /// Gets or sets the lifetime to keep entries in the cache before re-querying the backing physical cert store.
        /// Larger values will be more performant, but will leave your code less responsive to events like newly installed
        /// certificates.
        /// </summary>
        public TimeSpan CacheEntryLifetime
        {
            get
            {
                return _cacheEntryLifetime;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Certificate cache lifetime must be greater than zero");
                }

                _cacheEntryLifetime = value;
            }
        }

        /// <inheritdoc />
        public bool TryGetCertificate(
            CertificateIdentifier certificateId,
            IRealTimeProvider realTime,
            out X509Certificate2 certificate)
        {
            return TryGetCertificate(certificateId, StoreName.My, StoreLocation.CurrentUser, realTime, out certificate) ||
                   TryGetCertificate(certificateId, StoreName.My, StoreLocation.LocalMachine, realTime, out certificate);
        }

        /// <inheritdoc />
        public bool TryGetCertificate(
            CertificateIdentifier certificateId,
            StoreName storeName,
            StoreLocation storeLocation,
            IRealTimeProvider realTime,
            out X509Certificate2 certificate)
        {
            certificate = null;
            certificateId.AssertNonNull(nameof(certificateId));
            var lookupKey = new CertificateCacheKey(certificateId, storeName, storeLocation);

            CertificateCacheEntry cacheResult = FetchFromCache(lookupKey, storeName, storeLocation, realTime);

            if (cacheResult.ValidCerts.Count == 0)
            {
                return false;
            }

            certificate = new X509Certificate2(cacheResult.ValidCerts[0]); // Make a clone of the cached cert so the disposal handle is unique
            return true;
        }

        /// <inheritdoc />
        public X509Certificate2 GetCertificate(
            CertificateIdentifier certificateId,
            IRealTimeProvider realTime)
        {
            if (!TryGetCertificate(certificateId, realTime, out X509Certificate2 certificate))
            {
                throw new Exception($"Can't get certificate (find type: {certificateId}) in both CurrentUser and LocalMachine");
            }

            return certificate;
        }

        /// <inheritdoc />
        public X509Certificate2 GetCertificate(
            CertificateIdentifier certificateId,
            StoreName storeName,
            StoreLocation storeLocation,
            IRealTimeProvider realTime)
        {
            if (!TryGetCertificate(certificateId, storeName, storeLocation, realTime, out X509Certificate2 certificate))
            {
                throw new Exception($"Can't get certificate (find type: {certificateId}, store: {storeName}.{storeLocation})");
            }

            return certificate;
        }

        /// <inheritdoc />
        public bool TryGetCertificates(
            CertificateIdentifier certificateId,
            IRealTimeProvider realTime,
            out X509Certificate2Collection certificates)
        {
            // Check both cert stores at once. This logic is different from the single certificate fetch which just returns
            // the first valid cert found.
            // Make sure we don't do a premature || operation on these calls in case early escape skips one of the code paths.
            bool anyCurrentUser = TryGetCertificates(certificateId, StoreName.My, StoreLocation.CurrentUser, realTime, out X509Certificate2Collection currentUserCerts);
            bool anyLocalMachine = TryGetCertificates(certificateId, StoreName.My, StoreLocation.LocalMachine, realTime, out X509Certificate2Collection localMachineCerts);

            if (!anyLocalMachine)
            {
                certificates = currentUserCerts;
            }
            else if (!anyCurrentUser)
            {
                certificates = localMachineCerts;
            }
            else
            {
                // Both stores returned non-null cert stores. Return the union of both collections
                certificates = new X509Certificate2Collection();
                certificates.AddRange(currentUserCerts);
                certificates.AddRange(localMachineCerts);
            }

            return anyCurrentUser || anyLocalMachine;
        }

        /// <inheritdoc />
        public bool TryGetCertificates(
            CertificateIdentifier certificateId,
            StoreName storeName,
            StoreLocation storeLocation,
            IRealTimeProvider realTime,
            out X509Certificate2Collection certificates)
        {
            certificates = null;
            certificateId.AssertNonNull(nameof(certificateId));
            var lookupKey = new CertificateCacheKey(certificateId, storeName, storeLocation);

            CertificateCacheEntry cacheResult = FetchFromCache(lookupKey, storeName, storeLocation, realTime);
            if (cacheResult.ValidCerts.Count == 0)
            {
                return false;
            }

            // There's no point in allocating our own array or fancy reuse or anything like that for perf,
            // because X509Certificate2Collection is very primitive in its implementation and will be slow regardless.
            certificates = new X509Certificate2Collection();
            foreach (var cert in cacheResult.ValidCerts)
            {
                // Make a clone of the cached certificate handle so disposal of one cert won't affect other users
                certificates.Add(new X509Certificate2(cert));
            }

            return true;
        }

        /// <inheritdoc />
        public X509Certificate2Collection GetCertificates(CertificateIdentifier certificateId, IRealTimeProvider realTime)
        {
            if (!TryGetCertificates(certificateId, realTime, out X509Certificate2Collection certificates))
            {
                throw new Exception($"Can't get certificates (find type: {certificateId}) in either CurrentUser or LocalMachine");
            }

            return certificates;
        }

        /// <inheritdoc />
        public X509Certificate2Collection GetCertificates(CertificateIdentifier certificateId, StoreName storeName, StoreLocation storeLocation, IRealTimeProvider realTime)
        {
            if (!TryGetCertificates(certificateId, storeName, storeLocation, realTime, out X509Certificate2Collection certificates))
            {
                throw new Exception($"Can't get certificate (find type: {certificateId}, store: {storeName}.{storeLocation})");
            }

            return certificates;
        }

        /// <summary>
        /// Internal cache fetch method.
        /// </summary>
        /// <param name="lookupKey">The certificate(s) to look for.</param>
        /// <param name="storeName">The physical store name to check.</param>
        /// <param name="storeLocation">The physical store location to check.</param>
        /// <param name="realTime">A definition of real time, mostly for unit tests.</param>
        /// <returns>The non-null certificate cache entry (which may contain an empty list)</returns>
        private CertificateCacheEntry FetchFromCache(CertificateCacheKey lookupKey, StoreName storeName, StoreLocation storeLocation, IRealTimeProvider realTime)
        {
            DateTimeOffset utcNow = realTime.Time;
            CacheOperationClosure dictLookupParams = new CacheOperationClosure()
            {
                CacheEntryLifetime = _cacheEntryLifetime,
                PhysicalStore = _physicalStore,
                StoreLocation = storeLocation,
                StoreName = storeName,
                UtcNow = utcNow
            };

            // First pass - see if there's a cache hit or not. If this function misses, it will create a new cache entry.
#if NETCOREAPP
            CertificateCacheEntry cacheResult = _certificateCache.GetOrAdd(lookupKey, CacheGetOrAddFunction, dictLookupParams);
#else
            CertificateCacheEntry cacheResult = _certificateCache.GetOrAdd(lookupKey, (key) => CacheGetOrAddFunction(key, dictLookupParams));
#endif

            if (cacheResult.ExpireTime < utcNow)
            {
                // If this cache item is expired, do a second more expensive call to update the cache entry in-place.
                // Technically the entire operation could just be done in this single call, but since the case where the cache is
                // expired is much rarer, we optimize for read performance by doing the less expensive GetOrAdd call first.
#if NETCOREAPP
                cacheResult = _certificateCache.AddOrUpdate(lookupKey, CacheGetOrAddFunction, CacheUpdateFunction, dictLookupParams);
#else
                cacheResult = _certificateCache.AddOrUpdate(lookupKey, (key) => CacheGetOrAddFunction(key, dictLookupParams), (key, value) => CacheUpdateFunction(key, value, dictLookupParams));
#endif
            }

            return cacheResult;
        }

        /// <summary>
        /// A struct used to pass parameters to the dictionary lookup functions without allocating an implicit closure.
        /// </summary>
        private struct CacheOperationClosure
        {
            public IPhysicalCertificateStore PhysicalStore;
            public StoreName StoreName;
            public StoreLocation StoreLocation;
            public DateTimeOffset UtcNow;
            public TimeSpan CacheEntryLifetime;
        }

        /// <summary>
        /// Delegate function - creates a new cache entry in the event of a dictionary miss.
        /// </summary>
        /// <param name="key">The certificate id being looked up</param>
        /// <param name="closure">An explicit closure of arguments passed to FetchCertificates()</param>
        /// <returns>A newly created cache entry</returns>
        private static CertificateCacheEntry CacheGetOrAddFunction(CertificateCacheKey key, CacheOperationClosure closure)
        {
            List<X509Certificate2> allCerts = closure.PhysicalStore.GetCertificates(key.CertificateId, closure.StoreName, closure.StoreLocation, withPrivateKey: true).ToList();

            // Filter out any expired certs here.
            // We have to consider the case where a cert may have expired while this program has been running.
            // The cache should also refresh often enough (less than once every 5 minutes) that newly expired certs don't stay cached
            List<X509Certificate2> validCerts = new List<X509Certificate2>(
                allCerts
                .Select((cred) => cred)
                    .Where((cert) => CertificateHelpers.IsCertValidNear(cert, closure.UtcNow, closure.CacheEntryLifetime + closure.CacheEntryLifetime))
                    .OrderByDescending((cert) => cert.NotAfter)
                    .ToList());

            // This function will insert an entry to cache even if empty list to avoid repeated X509Store lookup if the cert is not found
            return new CertificateCacheEntry(
                closure.UtcNow + closure.CacheEntryLifetime,
                allCerts,
                validCerts);
        }

        /// <summary>
        /// Delegate function - checks for expiration of an existing key in the cache
        /// </summary>
        /// <param name="key">The certificate id being looked up</param>
        /// <param name="value">The existing item in the cache, may or may not be expired</param>
        /// <param name="closure">An explicit closure of arguments passed to FetchCertificates()</param>
        /// <returns>A potentially updated cache entry</returns>
        private static CertificateCacheEntry CacheUpdateFunction(CertificateCacheKey key, CertificateCacheEntry value, CacheOperationClosure closure)
        {
            if (value.ExpireTime < closure.UtcNow)
            {
                // Here's a problem. We want to dispose of certificates that are out of date.
                // But - someone might be using them! We don't know!
                // So to be absolutely safe, we don't actually dispose of any certs from the cache. We only add new certificates using set union, and dispose of unused duplicates.
                // We also want to maintain thread safety and prevent the dictionary value from being updated multiple times concurrently.
                // That's why we have to do these slow physical store operations while we're holding the bin lock of the dictionary.
                var updatedCerts = closure.PhysicalStore.GetCertificates(key.CertificateId, closure.StoreName, closure.StoreLocation, withPrivateKey: true);
                Dictionary<string, X509Certificate2> unifiedCertDict = new Dictionary<string, X509Certificate2>(value.AllCertificates.Count);

                // Build a set of all the certificates we've seen before.
                foreach (var cert in value.AllCertificates)
                {
                    unifiedCertDict.Add(cert.Thumbprint, cert);
                }

                // Then check for any newcomers
                foreach (var cert in updatedCerts)
                {
                    if (unifiedCertDict.ContainsKey(cert.Thumbprint))
                    {
                        // Not a new cert. Dispose of the duplicate and keep the existing value.
                        cert.Dispose();
                    }
                    else
                    {
                        // It's a new one we haven't seen before. Add it to the set.
                        unifiedCertDict.Add(cert.Thumbprint, cert);
                    }
                }

                int validCertsCount = 0;
                foreach (var cert in unifiedCertDict)
                {
                    if (CertificateHelpers.IsCertValidNear(cert.Value, closure.UtcNow, closure.CacheEntryLifetime + closure.CacheEntryLifetime))
                    {
                        validCertsCount++;
                    }
                }

                // Only recreate the cache data structures if something has actually changed
                // (which we presume to be incredibly rare, as the case of a hot-swapped cert or one near expiration)
                if (unifiedCertDict.Count != value.AllCertificates.Count || validCertsCount != value.ValidCerts.Count)
                {
                    value.AllCertificates = unifiedCertDict.Values.ToList();
                    value.ValidCerts = unifiedCertDict.Values
                        .Where((cert) => CertificateHelpers.IsCertValidNear(cert, closure.UtcNow, closure.CacheEntryLifetime + closure.CacheEntryLifetime))
                        .OrderByDescending((cert) => cert.NotAfter)
                        .ToList();
                }

                value.ExpireTime = closure.UtcNow + closure.CacheEntryLifetime;
            }

            return value;
        }

        /// <summary>
        /// A custom class to key entries in the <see cref="CertificateCache"/> - used here for speed
        /// as an alternative to string concatenation + comparison
        /// </summary>
        private struct CertificateCacheKey : IEquatable<CertificateCacheKey>
        {
            /// <summary>
            /// The identifier for the certificate(s) to store in the cache.
            /// </summary>
            public CertificateIdentifier CertificateId { get; private set; }

            /// <summary>
            /// The physical store name that these certificates were pulled from.
            /// </summary>
            public StoreName Store { get; private set; }

            /// <summary>
            /// The physical store location that these certificates were pulled from.
            /// </summary>
            public StoreLocation Location { get; private set; }

            /// <summary>
            /// Constructs a new <see cref="CertificateCacheKey"/> with initial parameters.
            /// </summary>
            /// <param name="certificateId"></param>
            /// <param name="store"></param>
            /// <param name="location"></param>
            public CertificateCacheKey(CertificateIdentifier certificateId, StoreName store, StoreLocation location)
            {
                CertificateId = certificateId.AssertNonNull(nameof(certificateId));
                Store = store;
                Location = location;
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                return unchecked(0x751C * CertificateId.GetHashCode()) ^
                        unchecked(0x19B1 * Store.GetHashCode()) ^
                        unchecked(0x4E8A * Location.GetHashCode());
            }

            /// <inheritdoc />
            public override bool Equals(object obj)
            {
                if (obj is null || GetType() != obj.GetType())
                {
                    return false;
                }

                return Equals((CertificateCacheKey)obj);
            }

            /// <inheritdoc />
            public bool Equals(CertificateCacheKey other)
            {
                return Store == other.Store &&
                    Location == other.Location &&
                    CertificateId.Equals(other.CertificateId);
            }

            /// <inheritdoc />
            public override string ToString()
            {
                // SLOW! Not intended for anything besides debugging!
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}_{1}_{2}",
                    CertificateId.ToString(),
                    Enum.GetName(typeof(StoreName), Store),
                    Enum.GetName(typeof(StoreLocation), Location));
            }
        }

        /// <summary>
        /// Represents a single entry into the <see cref="CertificateCache"/>, containing a list of valid certificates for some given lookup key.
        /// </summary>
        private sealed class CertificateCacheEntry
        {
            /// <summary>
            /// Constructs a new <see cref="CertificateCache"/> with initial parameters.
            /// </summary>
            /// <param name="expireTime">The absolute time that this cache entry expires.</param>
            /// <param name="allCertificates">The list of all certificates that match the search key for this entry, ordered by highest expiration time first.</param>
            /// <param name="validCertificates">The list of currently valid certificates that match the search key for this entry, ordered by highest expiration time first.</param>
            public CertificateCacheEntry(
                DateTimeOffset expireTime,
                IReadOnlyList<X509Certificate2> allCertificates,
                IReadOnlyList<X509Certificate2> validCertificates)
            {
                ExpireTime = expireTime;
                AllCertificates = allCertificates;
                ValidCerts = validCertificates;
            }

            /// <summary>
            /// Indicates the time this cache entry becomes stale.
            /// </summary>
            public DateTimeOffset ExpireTime { get; set; }

            /// <summary>
            /// The list of all certificates matching a specific criteria, but possibly expired.
            /// We hang on to references to them forever as an extra safe precaution to avoid the
            /// case where we try and dispose it but someone's still using it.
            /// </summary>
            public IReadOnlyList<X509Certificate2> AllCertificates { get; set; }

            /// <summary>
            /// The list of all certificates that were actually valid and not close to expiring the last time the cache was updated.
            /// This list is strictly ordered so if there are multiple entries, the certificates which expire latest appear first.
            /// </summary>
            public IReadOnlyList<X509Certificate2> ValidCerts { get; set; }
        }
    }
}
