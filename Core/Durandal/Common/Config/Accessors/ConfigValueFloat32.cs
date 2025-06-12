using Durandal.Common.Cache;
using Durandal.Common.Events;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Config.Accessors
{
    public class ConfigValueFloat32 : IConfigValue<float>
    {
        private readonly WeakPointer<IConfiguration> _sourceConfig;
        private readonly ILogger _logger;
        private readonly string _key;
        private readonly IDictionary<string, string> _variants;
        private readonly float _defaultValue;
        private readonly IReadThroughCache<string, float> _cache;
        private int _disposed = 0;

        public AsyncEvent<ConfigValueChangedEventArgs<float>> ChangedEvent { get; private set; }

        public ConfigValueFloat32(
            WeakPointer<IConfiguration> sourceConfig,
            ILogger logger,
            string key,
            float defaultValue = 0,
            IDictionary<string, string> variants = null)
        {
            _sourceConfig = sourceConfig;
            _logger = logger;
            _key = key;
            _defaultValue = defaultValue;
            _variants = variants;
            ChangedEvent = new AsyncEvent<ConfigValueChangedEventArgs<float>>();
            _cache = new MFUCache<string, float>((rawConfigString) => ParseValue(rawConfigString, _logger, _defaultValue), 10);
            _sourceConfig.Value.ConfigValueChangedEvent.Subscribe(HandleGlobalConfigChangeEvent);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ConfigValueFloat32()
        {
            Dispose(false);
        }
#endif

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
                _sourceConfig.Value.ConfigValueChangedEvent.Unsubscribe(HandleGlobalConfigChangeEvent);
            }
        }

        public float Value
        {
            get
            {
                RawConfigValue rawValue = _sourceConfig.Value.GetRaw(_key);

                if (rawValue == null)
                {
                    return _defaultValue;
                }

                if (!rawValue.ValueType.Equals(ConfigValueType.Float))
                {
                    _logger.Log(
                        string.Format("Config type mismatch, expected {0} got {1} for key \"{2}\"",
                            ConfigValueType.Float,
                            rawValue.ValueType,
                            rawValue.Name),
                        LogLevel.Err);
                    return _defaultValue;
                }

                // Get the raw value from the config, and then pull that string through the cache (to save from reparsing the same value if it hasn't changed)
                string stringVal = rawValue.GetVariantValue(_variants) ?? rawValue.DefaultValue;
                return _cache.GetCache(stringVal);
            }
            set
            {
                _sourceConfig.Value.Set(_key, value);
            }
        }

        /// <summary>
        /// Statically parses a raw configuration value into a float
        /// </summary>
        /// <param name="stringVal">The raw configuration value in string form</param>
        /// <param name="logger">A logger for errors</param>
        /// <param name="defaultValue">The default value to use if parsing fails</param>
        /// <returns></returns>
        public static float ParseValue(string stringVal, ILogger logger, float defaultValue)
        {
            if (string.IsNullOrEmpty(stringVal))
            {
                return defaultValue;
            }

            float returnVal;
            if (!float.TryParse(stringVal, out returnVal))
            {
                logger.Log("Could not parse config value \"" + stringVal + "\" as float; bad string format", LogLevel.Wrn);
                return defaultValue;
            }

            return returnVal;
        }

        private Task HandleGlobalConfigChangeEvent(object source, ConfigValueChangedEventArgs<string> args, IRealTimeProvider realTime)
        {
            if (string.Equals(_key, args.Key))
            {
                return ChangedEvent.Fire(this, new ConfigValueChangedEventArgs<float>(_key, Value), realTime);
            }
            else
            {
                return DurandalTaskExtensions.NoOpTask;
            }
        }
    }
}
