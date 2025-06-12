namespace Durandal.Common.Config
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;

    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using System.Threading;
    using System.Text;
    using Durandal.Common.Parsers;
    using System.Linq;
    using Durandal.Common.Utils;
    using System.Threading.Tasks;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Config.Annotation;
    using Durandal.Common.Config.Accessors;
    using Durandal.Common.Events;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// A simple interface for managing program configurations. Also allows methods
    /// for reflection, type-checking, and annotation.
    /// Subclasses of this type will determine how the values are actually stored,
    /// whether they are in-memory or backed by a database or .ini file.
    /// Configuration keys are case-sensitive strings, potentially tagged
    /// with a single variant parameter to allow dictionaries of keys.
    /// </summary>
    public abstract class AbstractConfiguration : IConfiguration
    {
        protected readonly Dictionary<string, RawConfigValue> _configValues = new Dictionary<string, RawConfigValue>();
        protected readonly ILogger _logger;
        protected readonly ReaderWriterLockAsync _lock = new ReaderWriterLockAsync();
        private readonly Committer _writeCommitter;
        private int _disposed = 0;

        /// <summary>
        /// Initializes the underlying configuration values
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="realTime"></param>
        protected AbstractConfiguration(ILogger logger, IRealTimeProvider realTime)
        {
            _logger = logger;
            _configValues.Clear();
            _writeCommitter = new Committer(CommitChanges, realTime ?? DefaultRealTimeProvider.Singleton);
            ConfigValueChangedEvent = new AsyncEvent<ConfigValueChangedEventArgs<string>>();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AbstractConfiguration()
        {
            Dispose(false);
        }
#endif

        public AsyncEvent<ConfigValueChangedEventArgs<string>> ConfigValueChangedEvent { get; private set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Ensure that the changes committer can write its final changes before shutdown
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                // Flush any pending changes to persistent storage (file, etc.) before disposal.
                _writeCommitter.Commit();
                _writeCommitter.WaitUntilCommitFinished(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                _lock?.Dispose();
                _writeCommitter?.Dispose();
            }
        }

        /// <summary>
        /// Returns true if this configuration contains the specified key. Does not check if there are any variants of the specific key, only
        /// the exact string will match.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(string key)
        {
            int hlock = _lock.EnterReadLock();
            try
            {
                return _configValues.ContainsKey(key);
            }
            finally
            {
                _lock.ExitReadLock(hlock);
            }
        }

        // <summary>
        // Returns true if this configuration contains the specified key, even if such a key also has a variant constraint
        // </summary>
        // <param name="key"></param>
        // <returns></returns>
        //public bool ContainsKeyWithVariant(string key)
        //{
        //    int hlock = _lock.EnterReadLock();
        //    try
        //    {
        //        foreach (string val in _configValues.Keys)
        //        {
        //            if (val.Contains("&"))
        //            {
        //                if (string.Equals(val.Substring(0, val.IndexOf('&')), key))
        //                {
        //                    return true;
        //                }
        //            }
        //            else if (string.Equals(val, key))
        //            {
        //                return true;
        //            }
        //        }
        //    }
        //    finally
        //    {
        //        _lock.ExitReadLock(hlock);
        //    }

        //    return false;
        //}

        // <summary>
        // Returns true if this configuration contains the specified key with the specified variant. There is no fallback to the default variant here
        // </summary>
        // <param name="key"></param>
        // <returns></returns>
        //public bool ContainsKey(string key, IDictionary<string, string> variants = null)
        //{
        //    int hlock = _lock.EnterReadLock();
        //    try
        //    {
        //        RawConfigValue value;
        //        return _configValues.TryGetValue(key, out value) &&
        //            value.GetRawValue(variants) != null; // FIXME WRONG
        //    }
        //    finally
        //    {
        //        _lock.ExitReadLock(hlock);
        //    }
        //}

        /// <summary>
        /// Returns all configuration values that are stored
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, RawConfigValue> GetAllValues()
        {
            int hlock = _lock.EnterReadLock();
            try
            {
                return new Dictionary<string, RawConfigValue>(_configValues);
            }
            finally
            {
                _lock.ExitReadLock(hlock);
            }
        }

        #region Getters

        /// <summary>
        /// Retrieves a string value from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <param name="variants">Variants to use when fetching</param>
        /// <returns></returns>
        public string GetString(string key, string defaultValue = "", IDictionary<string, string> variants = null)
        {
            return GetValueInternal(key, defaultValue, ConfigValueType.String, variants);
        }

        /// <summary>
        /// Retrieves a list of strings from the configuration.
        /// This getter does not have a default value. The default value is an empty list, never null
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="variants">Variants to use when fetching</param>
        /// <returns></returns>
        public IList<string> GetStringList(string key, IDictionary<string, string> variants = null)
        {
            string rawValue = GetValueInternal(key, string.Empty, ConfigValueType.StringList, variants);
            List<string> returnVal = new List<string>();
            if (rawValue.Contains(","))
            {
                //returnVal.AddRange(rawValue.Split(','));
                int startIdx = 0;
                int endIdx = 0;
                while (startIdx < rawValue.Length && endIdx >= 0)
                {
                    endIdx = rawValue.IndexOf(",", startIdx + 1);
                    if (endIdx == startIdx + 1)
                    {
                        // Zero-length list entry - keep it I guess
                        returnVal.Add(string.Empty);
                        startIdx = endIdx;
                        continue;
                    }

                    if (endIdx < 0)
                    {
                        returnVal.Add(rawValue.Substring(startIdx));
                    }
                    else
                    {
                        returnVal.Add(rawValue.Substring(startIdx, endIdx - startIdx));
                    }

                    startIdx = endIdx + 1;
                }
            }
            else if (!string.IsNullOrEmpty(rawValue))
            {
                // Handle if there's only one item in the vector
                returnVal.Add(rawValue);
            }

            return returnVal;
        }

        /// <summary>
        /// Retrieves a dictionary of strings from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="variants">Variants to use when fetching</param>
        /// <returns></returns>
        public IDictionary<string, string> GetStringDictionary(string key, IDictionary<string, string> variants = null)
        {
            string rawValue = GetValueInternal(key, string.Empty, ConfigValueType.StringDictionary, variants);

            // I could use the parser here but the dictionary grammar is fairly simple and string split works much faster anyways
            IDictionary<string, string> returnVal = new Dictionary<string, string>();
            string[] entries;
            if (rawValue.Contains(","))
            {
                entries = rawValue.Split(',');
            }
            else
            {
                entries = new string[] { rawValue };
            }

            foreach (string entry in entries)
            {
                int splitIdx = entry.IndexOf(":");
                if (splitIdx > 0)
                {
                    string a = entry.Substring(0, splitIdx);
                    string b = entry.Substring(splitIdx + 1);
                    returnVal.Add(a, b);
                }
                else
                {
                    throw new FormatException("Invalid configuration value. Dictionary entries must be in the format of \"KEY1:VALUE1,KEY2:VALUE2\": key \"" + key + "\" value \"" + entry);
                }
            }

            return returnVal;

            //try
            //{
            //    return DictionaryGrammar.Dictionary.Parse(rawValue);
            //}
            //catch (ParseException e)
            //{
            //    throw new FormatException("Invalid configuration value. Dictionary entries must be in the format of \"KEY1:VALUE1,KEY2:VALUE2\": key \"" + key + "\". Specific error: " + e.Message);
            //}
        }

        /// <summary>
        /// Retrieve an integer from the configuration. If no value exists, or if it is not an integer,
        /// the default value will be returned.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <param name="variants">Variants to use when fetching</param>
        /// <returns></returns>
        public int GetInt32(string key, int defaultValue = 0, IDictionary<string, string> variants = null)
        {
            string stringVal = GetValueInternal(key, null, ConfigValueType.Int, variants);
            return ConfigValueInt32.ParseValue(stringVal, _logger, defaultValue);
        }

        public IConfigValue<int> CreateInt32Accessor(ILogger logger, string key, int defaultValue = 0, IDictionary<string, string> variants = null)
        {
            return new ConfigValueInt32(new WeakPointer<IConfiguration>(this), logger, key, defaultValue, variants);
        }

        /// <summary>
        /// Retrieve an integer from the configuration. If no value exists, or if it is not an integer,
        /// the default value will be returned.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <param name="variants">Variants to use when fetching</param>
        /// <returns></returns>
        public long GetInt64(string key, long defaultValue = 0, IDictionary<string, string> variants = null)
        {
            string stringVal = GetValueInternal(key, null, ConfigValueType.Int, variants);
            long returnVal = defaultValue;
            if (stringVal != null)
            {
                if (!long.TryParse(stringVal, out returnVal))
                {
                    _logger.Log("Could not parse int value \"" + stringVal + "\"!", LogLevel.Wrn);
                    return defaultValue;
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Retrieves a floating point value from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <param name="variants">Variants to use when fetching</param>
        /// <returns></returns>
        public float GetFloat32(string key, float defaultValue = 0, IDictionary<string, string> variants = null)
        {
            string stringVal = GetValueInternal(key, null, ConfigValueType.Float, variants);
            float returnVal = defaultValue;
            if (stringVal != null)
            {
                if (!float.TryParse(stringVal, out returnVal))
                {
                    _logger.Log("Could not parse float value \"" + stringVal + "\"!", LogLevel.Wrn);
                    return defaultValue;
                }
            }

            return returnVal;
        }

        public IConfigValue<float> CreateFloat32Accessor(ILogger logger, string key, float defaultValue = 0, IDictionary<string, string> variants = null)
        {
            return new ConfigValueFloat32(new WeakPointer<IConfiguration>(this), logger, key, defaultValue, variants);
        }

        /// <summary>
        /// Retrieves a floating point value from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <param name="variants">Variants to use when fetching</param>
        /// <returns></returns>
        public double GetFloat64(string key, double defaultValue = 0, IDictionary<string, string> variants = null)
        {
            string stringVal = GetValueInternal(key, null, ConfigValueType.Float, variants);
            double returnVal = defaultValue;
            if (stringVal != null)
            {
                if (!double.TryParse(stringVal, out returnVal))
                {
                    _logger.Log("Could not parse double value \"" + stringVal + "\"!", LogLevel.Wrn);
                    return defaultValue;
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Retrieves a binary blob value from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="variants">Variants to use when fetching</param>
        /// <returns></returns>
        public byte[] GetBinary(string key, IDictionary<string, string> variants = null)
        {
            string hexBlock = GetString(key);
            if (string.IsNullOrEmpty(hexBlock))
            {
                return BinaryHelpers.EMPTY_BYTE_ARRAY;
            }

            byte[] returnVal = BinaryHelpers.FromHexString(hexBlock);
            return returnVal;
        }

        /// <summary>
        /// Retrieves a boolean value from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <param name="variants">Variants to use when fetching</param>
        /// <returns></returns>
        public bool GetBool(string key, bool defaultValue = false, IDictionary<string, string> variants = null)
        {
            string stringVal = GetValueInternal(key, null, ConfigValueType.Bool, variants);
            return ConfigValueBool.ParseValue(stringVal, _logger, defaultValue);
        }

        public IConfigValue<bool> CreateBoolAccessor(ILogger logger, string key, bool defaultValue = false, IDictionary<string, string> variants = null)
        {
            return new ConfigValueBool(new WeakPointer<IConfiguration>(this), logger, key, defaultValue, variants);
        }

        /// <summary>
        /// Retrieve a TimeSpan from the configuration. If no value exists, or if it is not a valid timespan,
        /// the default value will be returned.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <param name="variants">Variants to use when fetching</param>
        /// <returns></returns>
        public TimeSpan GetTimeSpan(string key, TimeSpan defaultValue = default(TimeSpan), IDictionary<string, string> variants = null)
        {
            string stringVal = GetValueInternal(key, null, ConfigValueType.TimeSpan, variants);
            return ConfigValueTimeSpan.ParseValue(stringVal, _logger, defaultValue);
        }

        public IConfigValue<TimeSpan> CreateTimeSpanAccessor(ILogger logger, string key, TimeSpan defaultValue = default(TimeSpan), IDictionary<string, string> variants = null)
        {
            return new ConfigValueTimeSpan(new WeakPointer<IConfiguration>(this), logger, key, defaultValue, variants);
        }

        /// <summary>
        /// Returns a raw ConfigValue object corresponding to the given key. The returned value is a collection which contains all variants of the key.
        /// This allows you to perform reflection on the type and annotation data of the config value
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <returns></returns>
        public RawConfigValue GetRaw(string key)
        {
            return GetRawValueInternal(key);
        }

#endregion

#region Setters

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        public void Set(string key, string value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null)
        {
            if (value.Contains("\r") || value.Contains("\n"))
            {
                throw new ArgumentException("Configuration value string cannot contain line breaks! " + value);
            }

            SetInternal(key, value, ConfigValueType.String, variants, realTime);
        }

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        public void Set(string key, IEnumerable<string> value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder entryBuilder = pooledSb.Builder;
                bool first = true;
                foreach (var val in value)
                {
                    if (val.Contains(","))
                    {
                        throw new ArgumentException("Configuration strings which are stored in a list cannot contain the , character");
                    }
                    if (value.Contains("\r") || value.Contains("\n"))
                    {
                        throw new ArgumentException("Configuration strings which are stored in a list cannot contain line breaks! " + value);
                    }

                    if (!first)
                    {
                        entryBuilder.Append(",");
                    }
                    else
                    {
                        first = false;
                    }
                    entryBuilder.Append(val);
                }

                

                SetInternal(key, entryBuilder.ToString(), ConfigValueType.StringList, variants, realTime);
            }
        }

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to set the default value</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        public void Set(string key, IDictionary<string, string> value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder entryBuilder = pooledSb.Builder;
                bool first = true;
                foreach (var kvp in value)
                {
                    if (kvp.Key.Contains(",") || kvp.Key.Contains(":") ||
                        kvp.Value.Contains(",") || kvp.Value.Contains(":"))
                    {
                        throw new ArgumentException("Configuration strings that are stored in a dictionary cannot contain the , or : characters");
                    }

                    if (kvp.Value.Contains("\r") || kvp.Value.Contains("\n"))
                    {
                        throw new ArgumentException("Configuration string that are stored in a dictionary cannot contain line breaks! " + value);
                    }

                    if (!first)
                    {
                        entryBuilder.Append(",");
                    }
                    else
                    {
                        first = false;
                    }
                    entryBuilder.Append(kvp.Key);
                    entryBuilder.Append(":");
                    entryBuilder.Append(kvp.Value);
                }

                SetInternal(key, entryBuilder.ToString(), ConfigValueType.StringDictionary, variants, realTime);
            }
        }

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        public void Set(string key, int value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null)
        {
            SetInternal(key, value.ToString(), ConfigValueType.Int, variants, realTime);
        }

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        public void Set(string key, long value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null)
        {
            SetInternal(key, value.ToString(), ConfigValueType.Int, variants, realTime);
        }

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        public void Set(string key, float value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null)
        {
            SetInternal(key, value.ToString(), ConfigValueType.Float, variants, realTime);
        }

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        public void Set(string key, double value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null)
        {
            SetInternal(key, value.ToString(), ConfigValueType.Float, variants, realTime);
        }

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        public void Set(string key, byte[] value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null)
        {
            SetInternal(key, BinaryHelpers.ToHexString(value), ConfigValueType.Binary, variants, realTime);
        }

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        public void Set(string key, bool value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null)
        {
            SetInternal(key, value ? "true" : "false", ConfigValueType.Bool, variants, realTime);
        }

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        public void Set(string key, TimeSpan value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null)
        {
            SetInternal(key, value.PrintTimeSpan(), ConfigValueType.TimeSpan, variants, realTime);
        }

        /// <summary>
        /// Adds a raw configuration entry to the current configuration
        /// </summary>
        /// <param name="rawValue"></param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        public void Set(RawConfigValue rawValue, IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            int hlock = _lock.EnterWriteLock();
            try
            {
                _configValues[rawValue.Name] = rawValue;
            }
            finally
            {
                _lock.ExitWriteLock(hlock);
            }

            ConfigValueChangedEvent.FireInBackground(this, new ConfigValueChangedEventArgs<string>(rawValue.Name, null), _logger, realTime);

            // We assume that CommitChanges() is locked independently, so we release the write lock first
            _writeCommitter.Commit();
        }

#endregion

        private RawConfigValue GetRawValueInternal(string key)
        {
            int hlock = _lock.EnterReadLock();
            try
            {
                // See if any values exist for this key
                RawConfigValue value;
                if (_configValues.TryGetValue(key, out value))
                {
                    return value;
                }
                else
                {
                    _logger.Log("Key " + key + " not found in configuration!", LogLevel.Err);
                    return null;
                }
            }
            finally
            {
                _lock.ExitReadLock(hlock);
            }
        }

        private string GetValueInternal(string key, string defaultValue, ConfigValueType expectedType, IDictionary<string, string> variants = null)
        {
            RawConfigValue returnVal = GetRawValueInternal(key);
            if (returnVal != null)
            {
                if (returnVal.ValueType.Equals(expectedType))
                {
                    if (variants == null || variants.Count == 0)
                    {
                        return returnVal.DefaultValue;
                    }
                    else
                    {
                        string variantValue = returnVal.GetVariantValue(variants);
                        if (variantValue != null)
                        {
                            return variantValue;
                        }
                        else
                        {
                            // no variant matched all constraints; return the fallback value
                            return returnVal.DefaultValue;
                        }
                    }
                }

                _logger.Log(string.Format("Config type mismatch, expected {0} got {1} for key \"{2}\"", expectedType, returnVal.ValueType, returnVal.Name), LogLevel.Err);
                return null;
            }

            return defaultValue;
        }

        /// <summary>
        /// Throws an exception if a configuration key is invalid
        /// </summary>
        /// <param name="key"></param>
        private void ValidateConfigKey(string key)
        {
            if (key.Contains("\r") || key.Contains("\n"))
            {
                throw new ArgumentException("Configuration key cannot contain line breaks! " + key);
            }
            if (key.Contains("&") || key.Contains(":"))
            {
                throw new ArgumentException("Configuration key cannot contain & or : characters! " + key);
            }
        }

        private void SetInternal(string key, string value, ConfigValueType type, IDictionary<string, string> variants, IRealTimeProvider realTime)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            int hlock = _lock.EnterWriteLock();
            try
            {
                // Make sure no linebreaks in the string or the key
                ValidateConfigKey(key);

                RawConfigValue valueCollection;
                if (!_configValues.TryGetValue(key, out valueCollection))
                {
                    valueCollection = new RawConfigValue(key, string.Empty, type);
                    _configValues.Add(key, valueCollection);
                }

                valueCollection.SetValue(value, variants);

                //RawConfigValue valueToWrite = new RawConfigValue(key, type, value);

                //// Are there existing annotations to carry over for this value?
                //if (_configValues.ContainsKey(valueToWrite.NameWithVariants))
                //{
                //    RawConfigValue existingLineItem = _configValues[valueToWrite.NameWithVariants];
                //    foreach (ConfigAnnotation annotation in existingLineItem.Annotations)
                //    {
                //        if (!annotation.GetTypeName().Equals("Type"))
                //        {
                //            valueToWrite.Annotations.Add(annotation);
                //        }
                //    }
                //}

                //_configValues[valueToWrite.NameWithVariants] = valueToWrite;
            }
            finally
            {
                _lock.ExitWriteLock(hlock);
            }

            ConfigValueChangedEvent.FireInBackground(this, new ConfigValueChangedEventArgs<string>(key, null), _logger, realTime);

            // We assume that CommitChanges() is locked independently, so we release the write lock first
            _writeCommitter.Commit();
        }

        /// <summary>
        /// Implemented by a subclass to store this configuration to a more permanent location like a file or a database
        /// </summary>
        protected abstract Task CommitChanges(IRealTimeProvider realTime);
    }
}