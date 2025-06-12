using Durandal.Common.Cache;
using Durandal.Common.Collections;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// An implementation of IReadThroughCache which caches in-memory and regularly backs that memory store
    /// to a file which can be persisted between executions of the program. Not designed to be super robust,
    /// but is an easy way to build up a small cache of persistent storage.
    /// </summary>
    /// <typeparam name="K">The key for retrieving items from the cache</typeparam>
    /// <typeparam name="V">The types of items to be cached</typeparam>
    public abstract class FileBackedReadThroughCache<K, V> : IReadThroughCache<K, V>
    {
        private readonly IFileSystem _fileSystem;
        private readonly VirtualPath _cacheFileName;
        private readonly Committer _fileCommitter;
        private readonly ReaderWriterLockAsync _lock = new ReaderWriterLockAsync(8);
        private readonly ILogger _logger;
        private readonly Dictionary<K, V> _memoryCache;
        private int _disposed = 0;

        public FileBackedReadThroughCache(IFileSystem fileSystem, VirtualPath cacheFileName, ILogger logger)
        {
            _fileSystem = fileSystem;
            _logger = logger;
            _cacheFileName = cacheFileName;
            _memoryCache = new Dictionary<K, V>();
            _fileCommitter = new Committer(WriteCache, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public int CacheCapacity => int.MaxValue;

        public int ItemsCached
        {
            get
            {
                int hRead = _lock.EnterReadLock();
                try
                {
                    return _memoryCache.Count;
                }
                finally
                {
                    _lock.ExitReadLock(hRead);
                }
            }
        }

        public void Clear()
        {
            _memoryCache.Clear();
            _fileCommitter.Commit();
        }

        public V GetCache(K key)
        {
            int hRead = _lock.EnterReadLock();
            try
            {
                if (_memoryCache.ContainsKey(key))
                {
                    return _memoryCache[key];
                }
            }
            finally
            {
                _lock.ExitReadLock(hRead);
            }

            RetrieveResult<V> returnVal = CacheMiss(key).Await();
            if (returnVal != null && returnVal.Success)
            {
                int hWrite = _lock.EnterWriteLock();
                try
                {
                    // opt: this can potentially miss multiple times on the same cache key which is a bit wasteful.
                    _memoryCache[key] = returnVal.Result;
                }
                finally
                {
                    _lock.ExitWriteLock(hWrite);
                }

                _fileCommitter.Commit();
                return returnVal.Result;
            }
            else
            {
                return default(V);
            }
        }

        public async Task<V> GetCacheAsync(K key)
        {
            int hRead = await _lock.EnterReadLockAsync().ConfigureAwait(false);
            try
            {
                if (_memoryCache.ContainsKey(key))
                {
                    return _memoryCache[key];
                }
            }
            finally
            {
                _lock.ExitReadLock(hRead);
            }

            RetrieveResult<V> returnVal = await CacheMiss(key).ConfigureAwait(false);
            if (returnVal != null && returnVal.Success)
            {
                int hWrite = await _lock.EnterWriteLockAsync().ConfigureAwait(false);
                try
                {
                    // opt: this can potentially miss multiple times on the same cache key which is a bit wasteful.
                    _memoryCache[key] = returnVal.Result;
                }
                finally
                {
                    _lock.ExitWriteLock(hWrite);
                }

                _fileCommitter.Commit();
                return returnVal.Result;
            }
            else
            {
                return default(V);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _fileCommitter.WaitUntilCommitFinished(CancellationToken.None, DefaultRealTimeProvider.Singleton, 30000);
                _fileCommitter?.Dispose();
                _lock?.Dispose();
            }
        }

        /// <summary>
        /// !!! Must be called by the implementation internally before the cache can be used !!!
        /// Initializes the cache and reads from the existing cache file, if present
        /// </summary>
        /// <returns></returns>
        protected async Task Initialize()
        {
            int hWrite = await _lock.EnterWriteLockAsync().ConfigureAwait(false);
            try
            {
                _memoryCache.Clear();
                if (await _fileSystem.ExistsAsync(_cacheFileName).ConfigureAwait(false))
                {
                    using (Stream rawStream = await _fileSystem.OpenStreamAsync(_cacheFileName, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
                    {
                        await DeserializeCacheFile(rawStream, _memoryCache).ConfigureAwait(false);
                    }

                    _logger.Log("Loaded " + _memoryCache.Count + " cache items from  " + _cacheFileName.FullName);
                }
                else
                {
                    _logger.Log("Cache file " + _cacheFileName.FullName + " does not exist", LogLevel.Wrn);
                }
            }
            finally
            {
                _lock.ExitWriteLock(hWrite);
            }
        }

        protected abstract Task<RetrieveResult<V>> CacheMiss(K key);

        protected abstract Task DeserializeCacheFile(Stream cacheFileInStream, IDictionary<K, V> targetDictionary);

        protected abstract Task SerializeCacheFile(IDictionary<K, V> cachedItems, Stream cacheFileOutStream);

        private async Task WriteCache(IRealTimeProvider realTime)
        {
            int hRead = await _lock.EnterReadLockAsync().ConfigureAwait(false);
            try
            {
                VirtualPath tempFile = _cacheFileName.Container.Combine(Guid.NewGuid().ToString("N") + ".tmp");
                using (Stream rawStream = await _fileSystem.OpenStreamAsync(tempFile, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false))
                {
                    await SerializeCacheFile(_memoryCache, rawStream).ConfigureAwait(false);
                }

                if (await _fileSystem.ExistsAsync(_cacheFileName).ConfigureAwait(false))
                {
                    await _fileSystem.DeleteAsync(_cacheFileName).ConfigureAwait(false);
                }

                await _fileSystem.MoveAsync(tempFile, _cacheFileName).ConfigureAwait(false);
            }
            finally
            {
                _lock.ExitReadLock(hRead);
            }
        }
    }
}
