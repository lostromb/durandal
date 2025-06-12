using System;
using System.Threading.Tasks;
using Durandal.Common.Tasks;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// An interface for storing and retrieving items from a persistent database.
    /// </summary>
    /// <typeparam name="T">The type of item to be stored.</typeparam>
    public interface IStore<T> : IDisposable
    {
        /// <summary>
        /// Creates or updates an item with the specified key in the store.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="item"></param>
        void CreateOrUpdate(string key, T item);

        /// <summary>
        /// Deletes the specified item from the store, if it exists.
        /// </summary>
        /// <param name="key"></param>
        void Delete(string key);

        /// <summary>
        /// Attempts to retrieve an item from the store asynchronously.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<RetrieveResult<T>> TryRetrieveAsync(string key);

        /// <summary>
        /// Attempts to retrieve an item from the store.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        RetrieveResult<T> TryRetrieve(string key);
    }
}
