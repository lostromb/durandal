using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Dialog.Services
{
    /// <summary>
    /// Represents a generic bag of data of varying types (string, integer, date, binary...)
    /// Used to pass around session contexts, states, or configurations between plugins
    /// and the runtime.
    /// Various stores of this type are used during plugin execution time, which are keyed
    /// on different things and have varying levels of persistence. The most common ones
    /// are SessionStore and LocalUserProfile. SessionStore is to store variables and slot
    /// values that are local to a single conversion. Local user profile is the preferred place
    /// for storing long-term user preferences and user-specific plugin configuration,
    /// such as default location, default actions, query history, styles, profiles,
    /// as well as authentication data such as 3rd party access tokens.
    /// To summarize:
    ///     SessionStore = Transient, user+domain specific, short-term storage
    ///     LocalUserProfile = Persistent, user+domain specific, long-term storage
    ///     GlobalUserProfile = Persistent, user-specific read-only profile storage
    ///     GlobalHistory = Transient, user-specific read+write storage shared across domains
    ///     Configuration = Persistent, global configuration and storage local to one domain
    /// </summary>
    public interface IDataStore
    {
        /// <summary>
        /// Indicates whether this cache is read-only or not
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Indicates whether any values have been modified in this cache since it was loaded
        /// </summary>
        bool Touched { get; }

        int Count { get; }

        /// <summary>
        /// This is automatically done at the start of each new conversation, so there's no real need to call it yourself.
        /// </summary>
        void ClearAll();

        /// <summary>
        /// Tests if the store contains a particular key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool ContainsKey(string key);

        /// <summary>
        /// Returns all of the objects inside this store with their associated keys. Normally this is used for internal operations
        /// that need to persist the store into some kind of database.
        /// </summary>
        /// <returns></returns>
        IList<KeyValuePair<string, byte[]>> GetAllObjects();

        /// <summary>
        /// Gets an array of raw data from the store
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        byte[] GetBinary(string key);

        /// <summary>
        /// Gets a string value from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        string GetString(string key, string def = "");

        /// <summary>
        /// Gets an int value from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        int GetInt(string key, int def = 0);

        /// <summary>
        /// Gets a float value from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        float GetFloat(string key, float def = 0);

        /// <summary>
        /// Gets a boolean value from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        bool GetBool(string key, bool def = false);

        /// <summary>
        /// Gets a DateTimeOffset value from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        DateTimeOffset GetDateTime(string key, DateTimeOffset def = default(DateTimeOffset));

        /// <summary>
        /// Gets an object value from the store. Objects are expected to be
        /// simple property collections; the serializer attempts to be generic but
        /// can't always encode all the properties of an object.
        /// JSON encoding is used for backend storage.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        T GetObject<T>(string key, T def = null) where T : class;

        /// <summary>
        /// Puts a string into the store.
        /// If the object already exists in this store then it will be overwritten.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        void Put(string key, string obj);

        /// <summary>
        /// Puts a binary value into the store.
        /// If the object already exists in this store then it will be overwritten.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        void Put(string key, byte[] obj);

        /// <summary>
        /// Puts an int into the store.
        /// If the object already exists in this store then it will be overwritten.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        void Put(string key, int val);

        /// <summary>
        /// Puts a float into the store.
        /// If the object already exists in this store then it will be overwritten.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        void Put(string key, float val);

        /// <summary>
        /// Puts a boolean value into the store.
        /// If the object already exists in this store then it will be overwritten.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        void Put(string key, bool val);

        /// <summary>
        /// Puts a datetime value into the store.
        /// If the object already exists in this store then it will be overwritten.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        void Put(string key, DateTimeOffset val);

        /// <summary>
        /// Puts an object into the store. Objects are expected to be
        /// simple property collections; the serializer attempts to be generic but
        /// can't always encode all the properties of an object.
        /// If the object already exists in this store then it will be overwritten.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="val"></param>
        void Put<T>(string key, T val) where T : class;

        /// <summary>
        /// Removes an object from the store if it exists
        /// </summary>
        /// <param name="key"></param>
        void Remove(string key);

        /// <summary>
        /// Tenative retrieval of binary values from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        bool TryGetBinary(string key, out byte[] returnVal);

        /// <summary>
        /// Tenative retrieval of string values from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        bool TryGetString(string key, out string returnVal);

        /// <summary>
        /// Tenative retrieval of int values from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        bool TryGetInt(string key, out int returnVal);

        /// <summary>
        /// Tenative retrieval of float values from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        bool TryGetFloat(string key, out float returnVal);

        /// <summary>
        /// Tenative retrieval of boolean values from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        bool TryGetBool(string key, out bool returnVal);
        
        /// <summary>
        /// Tenative retrieval of datetime values from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        bool TryGetDateTime(string key, out DateTimeOffset returnVal);

        /// <summary>
        /// Tenative retrieval of objects from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        bool TryGetObject<T>(string key, out T returnVal) where T : class;
    }
}
