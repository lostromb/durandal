using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Extensions.Redis
{
    /// <summary>
    /// Wraps a redis ConnectionMultiplexer that uses a specific connection string
    /// </summary>
    public class RedisConnectionPool : IDisposable
    {
        private readonly string _connectionString;
        private readonly ILogger _connectionLogger;
        private /* readonly */ ConnectionMultiplexer _multiplexer;
        private int _disposed = 0;

        private RedisConnectionPool(string connectionString, ILogger connectionLogger)
        {
            _connectionString = connectionString;
            _connectionLogger = connectionLogger;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RedisConnectionPool()
        {
            Dispose(false);
        }
#endif

        public static async Task<RedisConnectionPool> Create(string connectionString, ILogger connectionLogger)
        {
            RedisConnectionPool returnVal = new RedisConnectionPool(connectionString, connectionLogger);
            await returnVal.Initialize();
            return returnVal;
        }

        private async Task Initialize()
        {
            _multiplexer = await ConnectionMultiplexer.ConnectAsync(_connectionString, new TextWriterLoggerAdapter(_connectionLogger, LogLevel.Vrb));
        }

        internal WeakPointer<ConnectionMultiplexer> GetMultiplexer()
        {
            return new WeakPointer<ConnectionMultiplexer>(_multiplexer);
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
                _multiplexer.Dispose();
            }
        }
    }
}
