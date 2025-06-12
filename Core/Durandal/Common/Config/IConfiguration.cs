using Durandal.Common.Config.Accessors;
using Durandal.Common.Events;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Config
{
    /// <summary>
    /// Represents a flat set of configuration values with support for variant constraints on keys, annotations, and change listeners.
    /// </summary>
    public interface IConfiguration : IDisposable
    {
        /// <summary>
        /// Returns true if this configuration contains the specified key. Does not check if there are any variants of the specific key, only
        /// the exact string will match.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool ContainsKey(string key);

        // <summary>
        // Returns true if this configuration contains the specified key, even if such a key also has a variant constraint
        // </summary>
        // <param name="key"></param>
        // <returns></returns>
        //bool ContainsKeyWithVariant(string key);

        // <summary>
        // Returns true if this configuration contains the specified key with the specified variant. There is no fallback to the default variant here
        // </summary>
        // <param name="key">The key to look for</param>
        // <param name="variants">Variants which must apply</param>
        // <returns>True if the given key exists with all of the variant constraints explicitly specified.</returns>
        //bool ContainsKey(string key, IDictionary<string, string> variants = null);

        /// <summary>
        /// Returns all configuration values that are stored
        /// </summary>
        /// <returns></returns>
        IDictionary<string, RawConfigValue> GetAllValues();

        /// <summary>
        /// Retrieves a string value from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to use the default value</param>
        /// <returns></returns>
        string GetString(string key, string defaultValue = "", IDictionary<string, string> variants = null);

        /// <summary>
        /// Retrieves a list of strings from the configuration.
        /// This getter does not have a default value. The default value is an empty list, never null
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to use the default value</param>
        /// <returns></returns>
        IList<string> GetStringList(string key, IDictionary<string, string> variants = null);

        /// <summary>
        /// Retrieves a dictionary of strings from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to use the default value</param>
        /// <returns></returns>
        IDictionary<string, string> GetStringDictionary(string key, IDictionary<string, string> variants = null);

        /// <summary>
        /// Retrieve an integer from the configuration. If no value exists, or if it is not an integer,
        /// the default value will be returned.
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to use the default value</param>
        /// <returns></returns>
        int GetInt32(string key, int defaultValue = 0, IDictionary<string, string> variants = null);

        IConfigValue<int> CreateInt32Accessor(ILogger logger, string key, int defaultValue = 0, IDictionary<string, string> variants = null);

        /// <summary>
        /// Retrieve an integer from the configuration. If no value exists, or if it is not an integer,
        /// the default value will be returned.
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to use the default value</param>
        /// <returns></returns>
        long GetInt64(string key, long defaultValue = 0, IDictionary<string, string> variants = null);

        /// <summary>
        /// Retrieves a floating point value from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to use the default value</param>
        /// <returns></returns>
        float GetFloat32(string key, float defaultValue = 0, IDictionary<string, string> variants = null);

        IConfigValue<float> CreateFloat32Accessor(ILogger logger, string key, float defaultValue = 0, IDictionary<string, string> variants = null);

        /// <summary>
        /// Retrieves a floating point value from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to use the default value</param>
        /// <returns></returns>
        double GetFloat64(string key, double defaultValue = 0, IDictionary<string, string> variants = null);

        /// <summary>
        /// Retrieves a binary blob value from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to use the default value</param>
        /// <returns></returns>
        byte[] GetBinary(string key, IDictionary<string, string> variants = null);

        /// <summary>
        /// Retrieves a boolean value from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to use the default value</param>
        /// <returns></returns>
        bool GetBool(string key, bool defaultValue = false, IDictionary<string, string> variants = null);

        IConfigValue<bool> CreateBoolAccessor(ILogger logger, string key, bool defaultValue = false, IDictionary<string, string> variants = null);

        /// <summary>
        /// Retrieves a TimeSpan value from the configuration
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to use the default value</param>
        /// <returns></returns>
        TimeSpan GetTimeSpan(string key, TimeSpan defaultValue = default(TimeSpan), IDictionary<string, string> variants = null);

        IConfigValue<TimeSpan> CreateTimeSpanAccessor(ILogger logger, string key, TimeSpan defaultValue = default(TimeSpan), IDictionary<string, string> variants = null);

        /// <summary>
        /// Returns a raw ConfigValue object corresponding to the given key, variant name and variant value.
        /// This allows you to perform reflection on the type and annotation data of the config value
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <returns></returns>
        RawConfigValue GetRaw(string key);

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to set the default value</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        void Set(string key, string value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null);

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to set the default value</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        void Set(string key, IEnumerable<string> value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null);

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to set the default value</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        void Set(string key, IDictionary<string, string> value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null);

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to set the default value</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        void Set(string key, int value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null);

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to set the default value</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        void Set(string key, long value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null);

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to set the default value</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        void Set(string key, float value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null);

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to set the default value</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        void Set(string key, double value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null);

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to set the default value</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        void Set(string key, byte[] value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null);

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to set the default value</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        void Set(string key, bool value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null);

        /// <summary>
        /// Sets a specific value in the configuration to a new value.
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="value">The value to write</param>
        /// <param name="variants">Variants which this particular value is contingent on, or null to set the default value</param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        void Set(string key, TimeSpan value, IDictionary<string, string> variants = null, IRealTimeProvider realTime = null);

        /// <summary>
        /// Adds a raw configuration entry to the current configuration
        /// </summary>
        /// <param name="rawValue"></param>
        /// <param name="realTime">Nonrealtime provider; ignore this unless you are unit testing.</param>
        void Set(RawConfigValue rawValue, IRealTimeProvider realTime = null);

        /// <summary>
        /// Event that is fired whenever any configuration value is changed.
        /// </summary>
        AsyncEvent<ConfigValueChangedEventArgs<string>> ConfigValueChangedEvent { get; }
    }
}
