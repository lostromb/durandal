namespace Durandal.Common.Dialog.Services
{
    using Durandal.API;
    using Durandal.Common.Collections;
    using Durandal.Common.Utils;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Implementation of IDataStore which keeps all values in memory.
    /// This entire class can be serialized to Json using the default converter without any data loss.
    /// </summary>
    [JsonConverter(typeof(JsonConverter_Local))]
    public class InMemoryDataStore : IDataStore
    {
        private IDictionary<string, byte[]> _values = new Dictionary<string, byte[]>();

        public InMemoryDataStore()
        {
            IsReadOnly = false;
            Touched = false;
        }

        public InMemoryDataStore(IDictionary<string, byte[]> values)
        {
            _values = values;
            IsReadOnly = false;
            Touched = false;
        }

        /// <summary>
        /// Indicates whether this cache is read-only or not
        /// </summary>
        public bool IsReadOnly
        {
            get;
            internal set; // The setter is only accessible internally to the dialog engine that is aware of this specific implementation. Kind of a hackish design, yes
        }

        /// <summary>
        /// Indicates whether any values have been modified in this cache since it was loaded
        /// </summary>
        public bool Touched
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets an array of raw data from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public byte[] GetBinary(string key)
        {
            if (_values.ContainsKey(key))
            {
                return _values[key];
            }

            return null;
        }

        /// <summary>
        /// Gets a string value from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public string GetString(string key, string def = "")
        {
            if (_values.ContainsKey(key))
            {
                byte[] serialized = _values[key];
                return Encoding.UTF8.GetString(serialized, 0, serialized.Length);
            }

            return def;
        }

        /// <summary>
        /// Gets an int value from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public int GetInt(string key, int def = 0)
        {
            if (_values.ContainsKey(key))
            {
                byte[] serialized = _values[key];
                if (serialized.Length != 4)
                    throw new InvalidOperationException("Data stored with key \"" + key + "\" is not an int");
                return BitConverter.ToInt32(serialized, 0);
            }

            return def;
        }

        /// <summary>
        /// Gets a float value from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public float GetFloat(string key, float def = 0)
        {
            if (_values.ContainsKey(key))
            {
                byte[] serialized = _values[key];
                if (serialized.Length != 4)
                    throw new InvalidOperationException("Data stored with key \"" + key + "\" is not a float");
                return BitConverter.ToSingle(serialized, 0);
            }

            return def;
        }

        /// <summary>
        /// Gets a boolean value from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public bool GetBool(string key, bool def = false)
        {
            if (_values.ContainsKey(key))
            {
                byte[] serialized = _values[key];
                if (serialized.Length != 1)
                    throw new InvalidOperationException("Data stored with key \"" + key + "\" is not a bool");
                return serialized[0] != 0;
            }

            return def;
        }

        public DateTimeOffset GetDateTime(string key, DateTimeOffset def = default(DateTimeOffset))
        {
            if (_values.ContainsKey(key))
            {
                byte[] serialized = _values[key];
                if (serialized.Length != 16)
                    throw new InvalidOperationException("Data stored with key \"" + key + "\" is not a datetime");
                long timeTicks = BitConverter.ToInt64(serialized, 0);
                long offsetTicks = BitConverter.ToInt64(serialized, 8);
                return new DateTimeOffset(timeTicks, new TimeSpan(offsetTicks));
            }

            return def;
        }

        /// <summary>
        /// Gets an object value from the cache. Objects are expected to be
        /// simple property collections; the serializer attempts to be generic but
        /// can't always encode all the properties of an object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public T GetObject<T>(string key, T def = default(T)) where T : class
        {
            if (_values.ContainsKey(key))
            {
                byte[] serialized = _values[key];
                string jsonValue = Encoding.UTF8.GetString(serialized, 0, serialized.Length);
                return JsonConvert.DeserializeObject<T>(jsonValue);
            }

            return def;
        }

        /// <summary>
        /// Puts a string into the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        public void Put(string key, string obj)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("This cache is read-only");
            Touched = true;

            if (_values.ContainsKey(key))
            {
                _values.Remove(key);
            }

            byte[] serialized = Encoding.UTF8.GetBytes(obj);
            _values.Add(key, serialized);
        }

        /// <summary>
        /// Puts a binary value into the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        public void Put(string key, byte[] obj)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("This cache is read-only");
            Touched = true;

            if (_values.ContainsKey(key))
            {
                _values.Remove(key);
            }

            _values.Add(key, obj);
        }

        /// <summary>
        /// Puts an int into the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        public void Put(string key, int val)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("This cache is read-only");
            Touched = true;

            if (_values.ContainsKey(key))
            {
                _values.Remove(key);
            }

            _values.Add(key, BitConverter.GetBytes(val));
        }

        /// <summary>
        /// Puts a float into the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        public void Put(string key, float val)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("This cache is read-only");
            Touched = true;

            if (_values.ContainsKey(key))
            {
                _values.Remove(key);
            }

            _values.Add(key, BitConverter.GetBytes(val));
        }

        /// <summary>
        /// Puts a boolean value into the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        public void Put(string key, bool val)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("This cache is read-only");
            Touched = true;

            if (_values.ContainsKey(key))
            {
                _values.Remove(key);
            }

            _values.Add(key, new byte[1] { val ? (byte)0xFF : (byte)0x00 });
        }

        public void Put(string key, DateTimeOffset val)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("This cache is read-only");
            Touched = true;

            if (_values.ContainsKey(key))
            {
                _values.Remove(key);
            }

            byte[] serialized = new byte[16];
            BinaryHelpers.Int64ToByteArrayLittleEndian(val.UtcTicks, serialized, 0);
            BinaryHelpers.Int64ToByteArrayLittleEndian(val.Offset.Ticks, serialized, 8);
            _values.Add(key, serialized);
        }

        /// <summary>
        /// Puts an cache into the session. Objects are expected to be
        /// simple property collections; the serializer attempts to be generic but
        /// can't always encode all the properties of an object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="val"></param>
        public void Put<T>(string key, T val) where T : class
        {
            if (IsReadOnly)
                throw new InvalidOperationException("This cache is read-only");
            Touched = true;

            if (_values.ContainsKey(key))
            {
                _values.Remove(key);
            }

            string jsonValue = JsonConvert.SerializeObject(val);
            _values.Add(key, Encoding.UTF8.GetBytes(jsonValue));
        }

        /// <summary>
        /// Tenative retrieval of binary values from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        public bool TryGetBinary(string key, out byte[] returnVal)
        {
            if (_values.TryGetValue(key, out returnVal))
            {
                return true;
            }

            returnVal = null;
            return false;
        }

        /// <summary>
        /// Tenative retrieval of string values from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        public bool TryGetString(string key, out string returnVal)
        {
            byte[] serialized;
            if (_values.TryGetValue(key, out serialized))
            {
                returnVal = Encoding.UTF8.GetString(serialized, 0, serialized.Length);
                return true;
            }

            returnVal = string.Empty;
            return false;
        }

        /// <summary>
        /// Tenative retrieval of int values from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        public bool TryGetInt(string key, out int returnVal)
        {
            byte[] serialized;
            if (_values.TryGetValue(key, out serialized))
            {
                if (serialized.Length != 4)
                    throw new InvalidOperationException("Data stored with key \"" + key + "\" is not an int");
                returnVal = BitConverter.ToInt32(serialized, 0);
                return true;
            }

            returnVal = 0;
            return false;
        }

        /// <summary>
        /// Tenative retrieval of float values from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        public bool TryGetFloat(string key, out float returnVal)
        {
            byte[] serialized;
            if (_values.TryGetValue(key, out serialized))
            {
                if (serialized.Length != 4)
                    throw new InvalidOperationException("Data stored with key \"" + key + "\" is not a float");
                returnVal = BitConverter.ToSingle(serialized, 0);
                return true;
            }

            returnVal = 0;
            return false;
        }

        /// <summary>
        /// Tenative retrieval of boolean values from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        public bool TryGetBool(string key, out bool returnVal)
        {
            byte[] serialized;
            if (_values.TryGetValue(key, out serialized))
            {
                if (serialized.Length != 1)
                    throw new InvalidOperationException("Data stored with key \"" + key + "\" is not a bool");
                returnVal = serialized[0] != 0;
                return true;
            }

            returnVal = false;
            return false;
        }


        public bool TryGetDateTime(string key, out DateTimeOffset returnVal)
        {
            byte[] serialized;
            if (_values.TryGetValue(key, out serialized))
            {
                if (serialized.Length != 16)
                    throw new InvalidOperationException("Data stored with key \"" + key + "\" is not a datetime");
                long timeTicks = BitConverter.ToInt64(serialized, 0);
                long offsetTicks = BitConverter.ToInt64(serialized, 8);
                returnVal = new DateTimeOffset(timeTicks, new TimeSpan(offsetTicks));
                return true;
            }

            returnVal = default(DateTimeOffset);
            return false;
        }

        /// <summary>
        /// Tenative retrieval of objects from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        public bool TryGetObject<T>(string key, out T returnVal) where T : class
        {
            byte[] serialized;
            if (_values.TryGetValue(key, out serialized))
            {
                string jsonValue = Encoding.UTF8.GetString(serialized, 0, serialized.Length);
                returnVal = JsonConvert.DeserializeObject<T>(jsonValue);
                return true;
            }

            returnVal = null;
            return false;
        }

        /// <summary>
        /// Tests if the cache contains a particular key within the current session
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(string key)
        {
            return _values.ContainsKey(key);
        }

        /// <summary>
        /// Removes an object from the cache if it exists
        /// </summary>
        /// <param name="key"></param>
        public void Remove(string key)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("This cache is read-only");
            Touched = true;

            if (_values.ContainsKey(key))
            {
                _values.Remove(key);
            }
        }

        /// <summary>
        /// Deletes all data from the cache. It should go without saying that you should use this sparingly.
        /// </summary>
        public void ClearAll()
        {
            if (IsReadOnly)
                throw new InvalidOperationException("This cache is read-only");
            Touched = true;

            _values.Clear();
        }

        public int Count
        {
            get
            {
                return _values.Count;
            }
        }

        /// <summary>
        /// Returns all of the objects inside this store with their associated keys. Normally this is used for internal operations
        /// that need to persist the profile into some kind of database.
        /// </summary>
        /// <returns></returns>
        public IList<KeyValuePair<string, byte[]>> GetAllObjects()
        {
            return new List<KeyValuePair<string, byte[]>>(_values);
        }

        public int SizeInBytes
        {
            get
            {
                int returnVal = 0;
                foreach (var value in _values.Values)
                {
                    returnVal += value.Length;
                }

                return returnVal;
            }
        }

        private class JsonConverter_Local : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(InMemoryDataStore) == objectType;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }
                else if (reader.TokenType == JsonToken.StartObject)
                {
                    IDictionary<string, byte[]> values = new Dictionary<string, byte[]>();
                    reader.Read(); // skip start object
                    while (reader.TokenType == JsonToken.PropertyName)
                    {
                        string key = (string)reader.Value;
                        string bytes = reader.ReadAsString();
                        values[key] = Convert.FromBase64String(bytes);
                        reader.Read(); // go to next property or end of object
                    }
                    //reader.Read(); // skip end object

                    return new InMemoryDataStore(values);
                }
                else
                {
                    throw new JsonException("Could not parse InMemoryDataStore from json");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    InMemoryDataStore castObject = (InMemoryDataStore)value;
                    writer.WriteStartObject();
                    foreach (var kvp in castObject._values)
                    {
                        writer.WritePropertyName(kvp.Key);
                        writer.WriteValue(Convert.ToBase64String(kvp.Value));
                    }

                    writer.WriteEndObject();
                }
            }
        }
    }
}
