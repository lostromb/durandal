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
    public class ConfigValueInt32 : IConfigValue<int>
    {
        private readonly WeakPointer<IConfiguration> _sourceConfig;
        private readonly ILogger _logger;
        private readonly string _key;
        private readonly IDictionary<string, string> _variants;
        private readonly int _defaultValue;
        private int _disposed = 0;

        public AsyncEvent<ConfigValueChangedEventArgs<int>> ChangedEvent { get; private set; }

        public ConfigValueInt32(
            WeakPointer<IConfiguration> sourceConfig,
            ILogger logger,
            string key,
            int defaultValue = 0,
            IDictionary<string, string> variants = null)
        {
            _sourceConfig = sourceConfig;
            _logger = logger;
            _key = key;
            _defaultValue = defaultValue;
            _variants = variants;
            ChangedEvent = new AsyncEvent<ConfigValueChangedEventArgs<int>>();
            _sourceConfig.Value.ConfigValueChangedEvent.Subscribe(HandleGlobalConfigChangeEvent);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ConfigValueInt32()
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

        public int Value
        {
            get
            {
                RawConfigValue rawValue = _sourceConfig.Value.GetRaw(_key);

                if (rawValue == null)
                {
                    return _defaultValue;
                }

                if (!rawValue.ValueType.Equals(ConfigValueType.Int))
                {
                    _logger.Log(
                        string.Format("Config type mismatch, expected {0} got {1} for key \"{2}\"",
                            ConfigValueType.Int,
                            rawValue.ValueType,
                            rawValue.Name),
                        LogLevel.Err);
                    return _defaultValue;
                }

                string stringVal = rawValue.GetVariantValue(_variants) ?? rawValue.DefaultValue;
                return ParseValue(stringVal, _logger, _defaultValue);
            }
            set
            {
                _sourceConfig.Value.Set(_key, value);
            }
        }

        /// <summary>
        /// Statically parses a raw configuration value into an int32
        /// </summary>
        /// <param name="stringVal">The raw configuration value in string form</param>
        /// <param name="logger">A logger for errors</param>
        /// <param name="defaultValue">The default value to use if parsing fails</param>
        /// <returns></returns>
        public static int ParseValue(string stringVal, ILogger logger, int defaultValue)
        {
            if (string.IsNullOrEmpty(stringVal))
            {
                return defaultValue;
            }

            long returnVal;
            if (!long.TryParse(stringVal, out returnVal))
            {
                logger.Log("Could not parse config value \"" + stringVal + "\" as Int32; bad string format", LogLevel.Wrn);
                return defaultValue;
            }

            if (returnVal > int.MaxValue || returnVal < int.MinValue)
            {
                logger.Log("Could not parse config value \"" + stringVal + "\" as Int32; value too large", LogLevel.Wrn);
                return defaultValue;
            }

            return (int)returnVal;
        }

        private Task HandleGlobalConfigChangeEvent(object source, ConfigValueChangedEventArgs<string> args, IRealTimeProvider realTime)
        {
            if (string.Equals(_key, args.Key))
            {
                return ChangedEvent.Fire(this, new ConfigValueChangedEventArgs<int>(_key, Value), realTime);
            }
            else
            {
                return DurandalTaskExtensions.NoOpTask;
            }
        }
    }
}
