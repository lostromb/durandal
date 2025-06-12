using Durandal.Extensions.MySql;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DurandalServices.Instrumentation
{
    // Singleton for instrumentation SQL thread pool
    public class InstrumentationConnectionPool
    {
        private static object _mutex = new object();
        private static MySqlConnectionPool _pool = null;

        public static MySqlConnectionPool GetSharedPool(string connectionString, ILogger logger, IMetricCollector metrics, DimensionSet dimensions, bool useNativePool)
        {
            lock (_mutex)
            {
                if (_pool == null)
                {
                    _pool = MySqlConnectionPool.Create(connectionString, logger, metrics, dimensions, "Instrumentation", useNativePool).Await();
                }

                return _pool;
            }
        }
    }
}
