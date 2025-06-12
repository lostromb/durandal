namespace Stromberg.Utils
{
    using System;

    public class CachedItem<T>
    {
        public T Value;
        public DateTime StoreTime;
        public DateTime ExpireTime;
    }
}
