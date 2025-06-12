using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Cache;
using Durandal.Common.IO;
using Durandal.Common.IO.Json;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.Redis
{
    public class RedisCache<T> : ICache<T> where T : class
    {
        private readonly ILogger _logger;
        private readonly IByteConverter<T> _serializer;
        private readonly WeakPointer<ConnectionMultiplexer> _redisConnection;
        private int _disposed;

        public RedisCache(
            WeakPointer<RedisConnectionPool> redisConnectionPool,
            IMetricCollector metrics,
            IByteConverter<T> serializer,
            ILogger logger)
        {
            _logger = logger;
            _serializer = serializer;
            _redisConnection = redisConnectionPool.Value.GetMultiplexer();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RedisCache()
        {
            Dispose(false);
        }
#endif

        public async Task Delete(string key, bool fireAndForget, ILogger queryLogger)
        {
            try
            {
                IDatabase db = _redisConnection.Value.GetDatabase();
                await db.KeyDeleteAsync(key, fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None);
            }
            catch (Exception e)
            {
                queryLogger.Log(e, LogLevel.Err);
            }
        }

        public async Task Delete(IList<string> keys, bool fireAndForget, ILogger queryLogger)
        {
            try
            {
                IDatabase db = _redisConnection.Value.GetDatabase();
                RedisKey[] convertedKeys = new RedisKey[keys.Count];
                for (int c = 0; c < keys.Count; c++)
                {
                    convertedKeys[c] = keys[c];
                }

                await db.KeyDeleteAsync(convertedKeys, fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None);
            }
            catch (Exception e)
            {
                queryLogger.Log(e, LogLevel.Err);
            }
        }

        public Task Store(string key, T item, DateTimeOffset? expireTime, TimeSpan? lifetime, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime)
        {
            CachedItem<T> convertedItem = new CachedItem<T>(key, item, lifetime, expireTime);
            return Store(convertedItem, fireAndForget, queryLogger, realTime);
        }

        public async Task Store(CachedItem<T> item, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();

            try
            {
                // If expire time is not set but lifetime is, calculate the expire time from that
                if (!item.ExpireTime.HasValue && item.LifeTime.HasValue)
                {
                    item.ExpireTime = realTime.Time + item.LifeTime.Value;
                }

                RedisCacheItem convertedItem = new RedisCacheItem()
                {
                    SerializedItem = _serializer.Encode(item.Item),
                    ExpireTime = item.ExpireTime,
                    LifeTime = item.LifeTime
                };

                string json = JsonConvert.SerializeObject(convertedItem, Formatting.None);
                byte[] blob = Encoding.UTF8.GetBytes(json);
                TimeSpan? redisTtl = convertedItem.GetRedisTtl(realTime);
                IDatabase db = _redisConnection.Value.GetDatabase();
                await db.StringSetAsync(item.Key, blob, redisTtl, When.Always, fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None);
                timer.Stop();
                queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_Store_CacheWrite, item.Key, ref timer), LogLevel.Ins);
            }
            catch (Exception e)
            {
                queryLogger.Log(e, LogLevel.Err);
            }
        }

        public async Task Store(IList<CachedItem<T>> items, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();

            try
            {
                List<Task> tasks = new List<Task>();
                IDatabase db = _redisConnection.Value.GetDatabase();
                foreach (CachedItem<T> item in items)
                {
                    // If expire time is not set but lifetime is, calculate the expire time from that
                    if (!item.ExpireTime.HasValue && item.LifeTime.HasValue)
                    {
                        item.ExpireTime = realTime.Time + item.LifeTime.Value;
                    }

                    RedisCacheItem convertedItem = new RedisCacheItem()
                    {
                        SerializedItem = _serializer.Encode(item.Item),
                        ExpireTime = item.ExpireTime,
                        LifeTime = item.LifeTime
                    };

                    string json = JsonConvert.SerializeObject(convertedItem, Formatting.None);
                    byte[] blob = Encoding.UTF8.GetBytes(json);
                    TimeSpan? redisTtl = convertedItem.GetRedisTtl(realTime);
                    tasks.Add(db.StringSetAsync(item.Key, blob, redisTtl, When.Always, fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None));
                }
            
                foreach (Task t in tasks)
                {
                    await t;
                }

                timer.Stop();
                foreach (CachedItem<T> item in items)
                {
                    queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_Store_CacheWrite, item.Key, ref timer), LogLevel.Ins);
                }
            }
            catch (Exception e)
            {
                queryLogger.Log(e, LogLevel.Err);
            }
        }

        public async Task<RetrieveResult<T>> TryRetrieve(string key, ILogger queryLogger, IRealTimeProvider realTime, TimeSpan? maxSpinTime = null)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();

            try
            {
                IDatabase db = _redisConnection.Value.GetDatabase();

                do
                {
                    RedisValue val = await db.StringGetAsync(key);
                    if (!val.IsNull)
                    {
                        byte[] rawBlob = (byte[])val.Box();
                        string json = Encoding.UTF8.GetString(rawBlob, 0, rawBlob.Length);
                        RedisCacheItem parsedItem = JsonConvert.DeserializeObject<RedisCacheItem>(json);

                        // Touch the value if needed
                        if (parsedItem.LifeTime.HasValue)
                        {
                            // But make sure we don't reduce the TTL either (if it has a long initial expire time and then a shorter TTL, for whatever reason)
                            if (parsedItem.ExpireTime.HasValue)
                            {
                                TimeSpan currentExpireTime = parsedItem.ExpireTime.Value - realTime.Time;
                                if (parsedItem.LifeTime.Value > currentExpireTime)
                                {
                                    await db.KeyExpireAsync(key, parsedItem.LifeTime, CommandFlags.FireAndForget);
                                }
                            }
                            else
                            {
                                await db.KeyExpireAsync(key, parsedItem.LifeTime, CommandFlags.FireAndForget);
                            }
                        }

                        T result = _serializer.Decode(parsedItem.SerializedItem, 0, parsedItem.SerializedItem.Length);
                        timer.Stop();
                        return new RetrieveResult<T>(result, timer.ElapsedMillisecondsPrecise());
                    }
                }
                while (maxSpinTime.HasValue && timer.Elapsed < maxSpinTime.Value);
            }
            catch (Exception e)
            {
                queryLogger.Log(e, LogLevel.Err);
            }

            timer.Stop();
            return new RetrieveResult<T>(default(T), timer.ElapsedMillisecondsPrecise(), false);
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
            }
        }

        /// <summary>
        /// Redis doesn't persist absolute expiry or TTS times so we have to wrap cached objects in this class here
        /// </summary>
        private class RedisCacheItem
        {
            [JsonProperty("item")]
            [JsonConverter(typeof(JsonByteArrayConverter))]
            public byte[] SerializedItem { get; set; }

            [JsonProperty("expire")]
            [JsonConverter(typeof(JsonEpochTimeConverter))]
            public DateTimeOffset? ExpireTime { get; set; }

            [JsonProperty("ttl")]
            [JsonConverter(typeof(JsonTimeSpanStringConverter))]
            public TimeSpan? LifeTime { get; set; }

            /// <summary>
            /// Converts the ExpireTime / LifeTime parameters into a TTL value that represents the minimum time that this item should be cached in Redis.
            /// This can potentially return null, in which case the value should be stored persistently.
            /// </summary>
            /// <param name="realTime"></param>
            /// <returns></returns>
            public TimeSpan? GetRedisTtl(IRealTimeProvider realTime)
            {
                TimeSpan? redisTtl;
                if (ExpireTime.HasValue)
                {
                    // Item has a fixed expire time
                    redisTtl = ExpireTime.Value - realTime.Time;
                }
                else if (LifeTime.HasValue)
                {
                    // Item has a lifetime but no fixed expire time
                    redisTtl = LifeTime;
                }
                else
                {
                    // Item is persistent
                    redisTtl = null;
                }

                return redisTtl;
            }
        }
    }
}
