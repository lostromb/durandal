using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// Cache which does no actual caching
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class NullCache<K, V> : IReadThroughCache<K, V>
    {
        private Func<K, V> _source;

        public NullCache(Func<K, V> source)
        {
            _source = source;
        }

        public Task<V> GetCacheAsync(K key)
        {
            return Task.FromResult(_source(key));
        }

        public V GetCache(K key)
        {
            return _source(key);
        }

        public void Clear()
        {
        }

        public int CacheCapacity
        {
            get
            {
                return 0;
            }
        }

        public int ItemsCached
        {
            get
            {
                return 0;
            }
        }

        public void Dispose() { }
    }
}
