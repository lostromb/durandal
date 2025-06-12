using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Cache
{
    public class CachedItem<E>
    {
        /// <summary>
        /// The key to cache this item with. Required value.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The item being cached. Required value.
        /// </summary>
        public E Item { get; set; }

        /// <summary>
        /// If set, this is the keepalive time of the object. Whenever the object is read from the cache,
        /// its expire time will be touched such that it will continue to have at least this lifetime.
        /// </summary>
        public TimeSpan? LifeTime { get; set; }

        /// <summary>
        /// The absolute expire time of this item in the cache.
        /// If this is not set, the item should be cached indefinitely.
        /// </summary>
        public DateTimeOffset? ExpireTime { get; set; }

        public CachedItem(string key, E item, TimeSpan? lifeTime = null, DateTimeOffset? expireTime = null)
        {
            Key = key;
            Item = item;
            LifeTime = lifeTime;
            ExpireTime = expireTime;
        }
    }
}
