using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Default implementation of HTTP form parameters, usually URL or "Get" parameters, or sometimes url-encoded form parameters.
    /// </summary>
    public class HttpFormParameters : IHttpFormParameters
    {
        private readonly SmallDictionary<string, List<string>> _parameters;
        private static readonly string[] EMPTY_PARAMETER_SET = new string[0];

        /// <summary>
        /// Creates a new HttpQueryParameters dictionary with case-insensitive comparison.
        /// </summary>
        public HttpFormParameters()
            : this(initialCapacity: 2, caseSensitive: false, initialEntries: null)
        {
        }

        /// <summary>
        /// Creates a new HttpQueryParameters dictionary with an initial capacity and case-insensitive comparison.
        /// </summary>
        /// <param name="initialCapacity">A hint for the initial capacity of the collection.</param>
        public HttpFormParameters(int initialCapacity)
            : this(initialCapacity: initialCapacity, caseSensitive: false, initialEntries: null)
        {
        }

        /// <summary>
        /// Creates a new HttpQueryParameters dictionary with an initial capacity and case sensitivity
        /// </summary>
        /// <param name="initialCapacity">A hint for the initial capacity of the collection.</param>
        /// <param name="caseSensitive">Whether key comparisons should be case sensitive (default false).</param>
        public HttpFormParameters(int initialCapacity, bool caseSensitive)
            : this(initialCapacity: initialCapacity, caseSensitive: caseSensitive, initialEntries: null)
        {
        }

        /// <summary>
        /// Creates a new HttpQueryParameters dictionary from a set of initial entries.
        /// </summary>
        /// <param name="initialEntries">A set of initial entries to populate the collection with.</param>
        /// <param name="caseSensitive">Whether key comparisons should be case sensitive (default false).</param>
        public HttpFormParameters(IList<KeyValuePair<string, string>> initialEntries, bool caseSensitive)
            : this(initialCapacity: initialEntries.Count, caseSensitive: caseSensitive, initialEntries: initialEntries)
        {
            initialEntries.AssertNonNull(nameof(initialEntries));
        }

        /// <summary>
        /// Creates a new HttpQueryParameters dictionary.
        /// </summary>
        /// <param name="initialCapacity">A hint for the initial capacity of the collection.</param>
        /// <param name="caseSensitive">Whether key comparisons should be case sensitive (default false).</param>
        /// <param name="initialEntries">A set of initial entries to populate the collection with.</param>
        public HttpFormParameters(int initialCapacity, bool caseSensitive, IEnumerable<KeyValuePair<string, string>> initialEntries)
        {
            _parameters = new SmallDictionary<string, List<string>>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase, initialCapacity);

            if (initialEntries != null)
            {
                foreach (KeyValuePair<string, string> element in initialEntries)
                {
                    Add(element.Key, element.Value);
                }
            }
        }

        /// <inheritdoc />
        public string this[string parameterName]
        {
            get
            {
                return GetFirstParameterValue(parameterName);
            }
            set
            {
                Set(parameterName, value);
            }
        }

        /// <inheritdoc />
        public int KeyCount
        {
            get
            {
                return _parameters.Count;
            }
        }

        /// <inheritdoc />
        public bool ContainsKey(string parameterName)
        {
            ValidateParameterName(parameterName);
            List<string> list;
            return _parameters.TryGetValue(parameterName, out list) && list.Count > 0;
        }

        /// <inheritdoc />
        public bool TryGetValue(string parameterName, out string parameterValue)
        {
            parameterValue = GetFirstParameterValue(parameterName);
            return parameterValue != null;
        }

        /// <summary>
        /// Gets the first parameter value for the given key, or null if not found.
        /// Parameter lookups depend on the case sensitivity specified at collection creation, but is usually case-insensitive.
        /// </summary>
        /// <param name="parameterName">The parameter to check for</param>
        /// <returns>The fetched parameter, or null if not found</returns>
        private string GetFirstParameterValue(string parameterName)
        {
            ValidateParameterName(parameterName);

            List<string> list;
            if (_parameters.TryGetValue(parameterName, out list) &&
                list.Count > 0)
            {
                return list[0];
            }

            return null;
        }

        /// <inheritdoc />
        public IReadOnlyCollection<string> GetAllParameterValues(string parameterName)
        {
            ValidateParameterName(parameterName);

            List<string> list;
            if (_parameters.TryGetValue(parameterName, out list))
            {
                return list;
            }

            return EMPTY_PARAMETER_SET;
        }

        /// <inheritdoc />
        public IDictionary<string, string> ToSimpleDictionary()
        {
            SmallDictionary<string, string> returnVal = new SmallDictionary<string, string>(_parameters._comparer, KeyCount);
            foreach (var item in _parameters)
            {
                if (item.Value.Count > 0)
                {
                    returnVal[item.Key] = item.Value[0];
                }
                else if (item.Value.Count > 1)
                {
                    throw new Exception("A form parameter dictionary with multiple values for the same key cannot be converted to a simple dictionary");
                }
            }

            return returnVal;
        }

        /// <inheritdoc />
        public void Remove(string parameterName)
        {
            ValidateParameterName(parameterName);

            List<string> list;
            if (_parameters.TryGetValue(parameterName, out list))
            {
                list.Clear();
            }
        }

        /// <inheritdoc />
        public void Set(string parameterName, string value)
        {
            ValidateParameterName(parameterName);
            value = value ?? string.Empty;

            if (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("Parameter value cannot contain newline characters");
            }

            // Create a list if needed and set the only value in the list to the desired parameter value
            List<string> list;
            if (_parameters.TryGetValue(parameterName, out list))
            {
                list.Clear();
            }
            else
            {
                list = new List<string>(1);
                _parameters[parameterName] = list;
            }

            list.Add(value);
        }

        /// <inheritdoc />
        public void Add(string parameterName, string value)
        {
            ValidateParameterName(parameterName);
            value = value ?? string.Empty;

            if (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("Parameter value cannot contain newline characters");
            }

            List<string> list;
            if (!_parameters.TryGetValue(parameterName, out list))
            {
                list = new List<string>(1);
                _parameters[parameterName] = list;
            }

            list.Add(value);
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, IReadOnlyCollection<string>>> GetEnumerator()
        {
            return new FormParameterEnumerator(_parameters);
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new FormParameterEnumerator(_parameters);
        }

        private static void ValidateParameterName(string paramName)
        {
            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentException("Parameter name cannot be null or empty");
            }

            if (paramName.IndexOf('\r') >= 0 || paramName.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("Parameter name cannot contain newline characters");
            }
        }

        private class FormParameterEnumerator : IEnumerator<KeyValuePair<string, IReadOnlyCollection<string>>>
        {
            private readonly IEnumerator<KeyValuePair<string, List<string>>> _internalEnumerator;
            private int _disposed = 0;

            public FormParameterEnumerator(SmallDictionary<string, List<string>> parameters)
            {
                _internalEnumerator = parameters.GetEnumerator();
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~FormParameterEnumerator()
            {
                Dispose(false);
            }
#endif

            public KeyValuePair<string, IReadOnlyCollection<string>> Current
            {
                get
                {
                    return new KeyValuePair<string, IReadOnlyCollection<string>>(_internalEnumerator.Current.Key, _internalEnumerator.Current.Value);
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return _internalEnumerator.Current;
                }
            }

            public bool MoveNext()
            {
                return _internalEnumerator.MoveNext();
            }

            public void Reset()
            {
                _internalEnumerator.Reset();
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
                    _internalEnumerator.Dispose();
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }
    }
}
