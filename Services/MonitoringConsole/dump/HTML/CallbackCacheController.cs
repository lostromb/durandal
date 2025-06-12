using Photon.Common.MySQL;
using Durandal.Common.Logger;
using Durandal.Common.Utils.IO;
using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Diagnostics;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights;

namespace Photon.StatusReporter.Controllers
{
    public class CallbackCacheController : ApiController
    {
        /// <summary>
        /// The cache to write to when callbacks come
        /// </summary>
        private static readonly MySqlCache<byte[]> _sqlCache;
        private static readonly MySqlConnectionPool _sqlConnectionPool;
        private static readonly TelemetryClient _telemetry;
        private static readonly string _datacenter;

        private static ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        private static bool _initialized = false;

        /// <summary>
        /// Initializes monitor states and database connections
        /// </summary>
        static CallbackCacheController()
        {
            GhettoGlobalState.Initialize();

            // only allow one initializer to ever run
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_initialized)
                {
                    return;
                }

                _lock.EnterWriteLock();
                try
                {
                    if (!_initialized)
                    {
                        _initialized = true;
                        ILogger serviceLogger = GhettoGlobalState.ServiceLogger.Clone("CallbackCacheController");
                        _sqlConnectionPool = GhettoGlobalState.SqlConnectionPool;
                        _sqlCache = new MySqlCache<byte[]>(_sqlConnectionPool, new PassthroughByteConverter(), serviceLogger.Clone("CallbackCache"));
                        _telemetry = GhettoGlobalState.Telemetry;
                        _datacenter = RoleEnvironment.GetConfigurationSettingValue("Datacenter");
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        [HttpPost]
        [Route("api/callback/{activity_id}/{*extra}")]
        public async Task<IHttpActionResult> CallbackHook([FromUri]string activity_id, [FromUri]string extra)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                // Dump the raw post body into the cache
                Guid activityId = Guid.Parse(activity_id);
                byte[] data = await Request.Content.ReadAsByteArrayAsync();
                await _sqlCache.Store(activityId, data);
            }
            catch (Exception e)
            {
                if (_telemetry != null)
                {
                    _telemetry.TrackException(e);
                }

                return this.InternalServerError(e);
            }
            finally
            {
                timer.Stop();

                if (_telemetry != null)
                {
                    // Log the callback metric
                    EventTelemetry healthEvent = new EventTelemetry()
                    {
                        Timestamp = DateTimeOffset.UtcNow,
                        Name = "Callback"
                    };

                    double storeMs = ((double)timer.ElapsedTicks) * 1000 / Stopwatch.Frequency;
                    healthEvent.Metrics.Add("StoreDuration", storeMs);
                    if (_sqlConnectionPool != null)
                    {
                        healthEvent.Metrics.Add("SqlPoolUsage", _sqlConnectionPool.Usage);
                    }
                    healthEvent.Properties.Add("TraceId", activity_id);
                    healthEvent.Properties.Add("DC", _datacenter);
                    _telemetry.TrackEvent(healthEvent);
                }
            }

            return this.Ok();
        }
    }
}
