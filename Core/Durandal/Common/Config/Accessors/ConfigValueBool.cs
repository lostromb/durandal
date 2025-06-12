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
    public class ConfigValueBool : IConfigValue<bool>
    {
        private readonly WeakPointer<IConfiguration> _sourceConfig;
        private readonly ILogger _logger;
        private readonly string _key;
        private readonly IDictionary<string, string> _variants;
        private readonly bool _defaultValue;
        private int _disposed = 0;

        public AsyncEvent<ConfigValueChangedEventArgs<bool>> ChangedEvent { get; private set; }

        public ConfigValueBool(
            WeakPointer<IConfiguration> sourceConfig,
            ILogger logger,
            string key,
            bool defaultValue = false,
            IDictionary<string, string> variants = null)
        {
            _sourceConfig = sourceConfig;
            _logger = logger;
            _key = key;
            _defaultValue = defaultValue;
            _variants = variants;
            ChangedEvent = new AsyncEvent<ConfigValueChangedEventArgs<bool>>();
            _sourceConfig.Value.ConfigValueChangedEvent.Subscribe(HandleGlobalConfigChangeEvent);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ConfigValueBool()
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

        public bool Value
        {
            get
            {
                RawConfigValue rawValue = _sourceConfig.Value.GetRaw(_key);

                if (rawValue == null)
                {
                    return _defaultValue;
                }

                if (!rawValue.ValueType.Equals(ConfigValueType.Bool))
                {
                    _logger.Log(
                        string.Format("Config type mismatch, expected {0} got {1} for key \"{2}\"",
                            ConfigValueType.Bool,
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
        /// Statically parses a raw configuration value into a bool
        /// </summary>
        /// <param name="stringVal">The raw configuration value in string form</param>
        /// <param name="logger">A logger for errors</param>
        /// <param name="defaultValue">The default value to use if parsing fails</param>
        /// <returns></returns>
        public static bool ParseValue(string stringVal, ILogger logger, bool defaultValue)
        {
            if (string.IsNullOrEmpty(stringVal))
            {
                return defaultValue;
            }
            else
            {
                return stringVal.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }

        private Task HandleGlobalConfigChangeEvent(object source, ConfigValueChangedEventArgs<string> args, IRealTimeProvider realTime)
        {
            if (string.Equals(_key, args.Key))
            {
                return ChangedEvent.Fire(this, new ConfigValueChangedEventArgs<bool>(_key, Value), realTime);
            }
            else
            {
                return DurandalTaskExtensions.NoOpTask;
            }
        }
    }
}
