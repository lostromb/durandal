using Durandal.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// This code is taken from Stephen Cleary's Collections package, which I cherry-picked because it is not entirely NetStandard 1.1 compatible
// https://github.com/StephenCleary/Collections
// MIT License
namespace Durandal.Common.Collections
{
    public static class CollectionHelpers
    {
        /// <summary>
        /// Given an <see cref="IEnumerable{T}"/> that you believe to actually be an <see cref="ICollection{T}"/>,
        /// perform the appropriate casting to pull out that collection. If it is some other kind of exotic
        /// enumerable type, this method will return a newly created List containing the source data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IReadOnlyCollection<T> ReifyCollection<T>(IEnumerable<T> source)
        {
            source.AssertNonNull(nameof(source));

            IReadOnlyCollection<T> roCollection = source as IReadOnlyCollection<T>;
            if (roCollection != null)
            {
                return roCollection;
            }

            ICollection<T> collection = source as ICollection<T>;
            if (collection != null)
            {
                return new CollectionWrapper<T>(collection);
            }

            ICollection nonGenericCollection = source as ICollection;
            if (nonGenericCollection != null)
            {
                return new NongenericCollectionWrapper<T>(nonGenericCollection);
            }

            return new List<T>(source);
        }

        /// <summary>
        /// There is a perf bug in .Net Framework in which List.AddRange() does a
        /// potentially large transient allocation. This method is a drop-in
        /// replacement for those use cases with better perf. (the bug was apparently
        /// fixed in .Net Core).
        /// </summary>
        /// <typeparam name="T">The type of objects contained in the list.</typeparam>
        /// <param name="list">The list to add items to.</param>
        /// <param name="toAdd">The collection of items to add.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void FastAddRangeCollection<T>(this List<T> list, ICollection<T> toAdd)
        {
#if NETCOREAPP
            list.AddRange(toAdd);
#else
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (toAdd != null)
            {
                int addCount = toAdd.Count;

                if (addCount != 0)
                {
                    int newCount = list.Count + addCount;

                    // When needed, resize once  
                    if (list.Capacity < newCount)
                    {
                        list.Capacity = Math.Max(list.Capacity * 2, newCount);
                    }

                    foreach (T item in toAdd)
                    {
                        list.Add(item);
                    }
                }
            }
#endif
        }

        /// <summary>
        /// There is a perf bug in .Net Framework in which List.AddRange() does a
        /// potentially large transient allocation. This method is a drop-in
        /// replacement for those use cases with better perf. (the bug was apparently
        /// fixed in .Net Core).
        /// </summary>
        /// <typeparam name="T">The type of objects contained in the list.</typeparam>
        /// <param name="list">The list to add items to.</param>
        /// <param name="toAdd">The collection of items to add.</param>
        /// <param name="knownLength">The actual length of the enumerable (must be greater than zero).</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void FastAddRangeEnumerable<T>(this List<T> list, IEnumerable<T> toAdd, int knownLength)
        {
            list.AssertNonNull(nameof(list));
            toAdd.AssertNonNull(nameof(toAdd));

            if (knownLength == 0)
            {
                return;
            }

            knownLength.AssertPositive(nameof(knownLength));

            int newCount = list.Count + knownLength;

            // When needed, resize once  
            if (list.Capacity < newCount)
            {
                list.Capacity = Math.Max(list.Capacity * 2, newCount);
            }

            foreach (T item in toAdd)
            {
                list.Add(item);
            }
        }

        /// <summary>
        /// There is a perf bug in .Net Framework in which List.AddRange() does a
        /// potentially large transient allocation. This method is a drop-in
        /// replacement for those use cases with better perf. (the bug was apparently
        /// fixed in .Net Core).
        /// </summary>
        /// <typeparam name="T">The type of objects contained in the list.</typeparam>
        /// <param name="list">The list to add items to.</param>
        /// <param name="toAdd">The collection of items to add.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void FastAddRangeReadOnlyCollection<T>(this List<T> list, IReadOnlyCollection<T> toAdd)
        {
#if NETCOREAPP
            list.AddRange(toAdd);
#else
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (toAdd != null)
            {
                int addCount = toAdd.Count;

                if (addCount != 0)
                {
                    int newCount = list.Count + addCount;

                    // When needed, resize once  
                    if (list.Capacity < newCount)
                    {
                        list.Capacity = Math.Max(list.Capacity * 2, newCount);
                    }

                    foreach (T item in toAdd)
                    {
                        list.Add(item);
                    }
                }
            }
#endif
        }

        /// <summary>
        /// There is a perf bug in .Net Framework in which List.AddRange() does a
        /// potentially large transient allocation. This method is a drop-in
        /// replacement for those use cases with better perf. (the bug was apparently
        /// fixed in .Net Core).
        /// </summary>
        /// <typeparam name="T">The type of objects contained in the list.</typeparam>
        /// <param name="list">The list to add items to.</param>
        /// <param name="toAdd">The collection of items to add.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void FastAddRangeList<T>(this List<T> list, IList<T> toAdd)
        {
#if NETCOREAPP
            list.AddRange(toAdd);
#else
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (toAdd != null)
            {
                int addCount = toAdd.Count;

                if (addCount != 0)
                {
                    int newCount = list.Count + addCount;

                    // When needed, resize once  
                    if (list.Capacity < newCount)
                    {
                        list.Capacity = Math.Max(list.Capacity * 2, newCount);
                    }

                    foreach (T item in toAdd)
                    {
                        list.Add(item);
                    }
                }
            }
#endif
        }

        /// <summary>
        /// Casts an ICollection (the ancient .Net construct) to an IReadOnlyCollection{T}.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private sealed class NongenericCollectionWrapper<T> : IReadOnlyCollection<T>
        {
            private readonly ICollection _collection;

            public NongenericCollectionWrapper(ICollection collection)
            {
                if (collection == null)
                    throw new ArgumentNullException("collection");
                _collection = collection;
            }

            public int Count
            {
                get
                {
                    return _collection.Count;
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                foreach (T item in _collection)
                    yield return item;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _collection.GetEnumerator();
            }
        }

        /// <summary>
        /// Casts an ICollection{T} an IReadOnlyCollection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private sealed class CollectionWrapper<T> : IReadOnlyCollection<T>
        {
            private readonly ICollection<T> _collection;

            public CollectionWrapper(ICollection<T> collection)
            {
                if (collection == null)
                    throw new ArgumentNullException("collection");
                _collection = collection;
            }

            public int Count
            {
                get
                {
                    return _collection.Count;
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                return _collection.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _collection.GetEnumerator();
            }
        }
    }
}