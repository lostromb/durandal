namespace Durandal.Common.Collections.Indexing
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The purpose of this interface is to define classes that can store a mapping between
    /// objects and keys in a way that allows for memory pooling and compaction. It is best suited
    /// for large, read-only objects that persist for the lifetime of the program (since this
    /// interface does not provide a method to delete objects from the index). The natural use
    /// for this interface is for large (+100mb), static string databases.
    /// </summary>
    public interface ICompactIndex<T> : IDisposable, IEnumerable<T> where T : class
    {
         /// <summary>
        /// Stores a new value in the index and return a key that can retrieve it later.
        /// </summary>
        /// <param name="value">The value to be stored</param>
        /// <returns>A key that can retrieve this object later.</returns>
        Compact<T> Store(T value);

        /// <summary>
        /// Returns the index of the specified object in the store.
        /// If the object does not exist, this returns NULL_INDEX
        /// </summary>
        /// <param name="value">The item to check</param>
        /// <returns>The key that will return this item if passed into Retrieve(key)</returns>
        Compact<T> GetIndex(T value);

        /// <summary>
        /// Tests to see if a given key exists in the index
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool Contains(Compact<T> key);

        /// <summary>
        /// Tests to see if a given object exists in the index
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        bool Contains(T value);

        /// <summary>
        /// Returns the number of items stored in the index
        /// </summary>
        int GetCount();

        /// <summary>
        /// Retrieve a value from the store based on a key.
        /// </summary>
        /// <param name="key">The key to use</param>
        /// <returns>The value that was originally stored under that key</returns>
        T Retrieve(Compact<T> key);

        /// <summary>
        /// Removes all items from this store.
        /// </summary>
        void Clear();

        /// <summary>
        /// Returns the number of bytes of actual memory used this index
        /// </summary>
        /// <returns></returns>
        long MemoryUse { get; }

        /// <summary>
        /// Returns the current compression ratio for stored data
        /// </summary>
        double CompressionRatio { get; }

        Compact<T> GetNullIndex();
    }
}
