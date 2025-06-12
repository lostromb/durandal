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
    /// Represents a mutable collection of HTTP headers.
    /// </summary>
    public class HttpHeaders : IHttpHeaders
    {
        private readonly SmallDictionary<string, List<string>> _headers;
        private static readonly string[] EMPTY_HEADER_SET = new string[0];

        /// <summary>
        /// Creates a new HttpHeaders dictionary.
        /// </summary>
        public HttpHeaders()
        {
            _headers = new SmallDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase, 4);
        }

        /// <summary>
        /// Creates a new HttpHeaders dictionary with an initial capacity
        /// </summary>
        public HttpHeaders(int initialCapacity)
        {
            _headers = new SmallDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase, initialCapacity);
        }

        /// <summary>
        /// Creates a new HttpHeaders dictionary with the given set of initial entries.
        /// </summary>
        /// <param name="entries"></param>
        public HttpHeaders(IReadOnlyCollection<KeyValuePair<string, string>> entries)
        {
            _headers = new SmallDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase, entries.Count);
            foreach (KeyValuePair<string, string> element in entries)
            {
                Set(element.Key, element.Value);
            }
        }

        /// <summary>
        /// Creates a new HttpHeaders dictionary with the given set of initial entries.
        /// </summary>
        /// <param name="entries"></param>
        public HttpHeaders(IList<KeyValuePair<string, string>> entries)
        {
            _headers = new SmallDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase, entries.Count);
            foreach (KeyValuePair<string, string> element in entries)
            {
                Set(element.Key, element.Value);
            }
        }

        /// <inheritdoc />
        public string this[string headerName]
        {
            get
            {
                return GetFirstHeaderValue(headerName);
            }
            set
            {
                Set(headerName, value);
            }
        }

        /// <inheritdoc />
        public int KeyCount
        {
            get
            {
                return _headers.Count;
            }
        }

        /// <inheritdoc />
        public bool ContainsKey(string headerName)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            List<string> list;
            return _headers.TryGetValue(headerName, out list) && list.Count > 0;
        }

        /// <inheritdoc />
        public bool ContainsValue(string headerName, string expectedValue, StringComparison stringComparison)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            expectedValue.AssertNonNullOrEmpty(nameof(expectedValue));

            int expectedValueLength = expectedValue.Length;
            foreach (Tuple<string, int, int> str in EnumerateValueListInternal(headerName))
            {
                if (str.Item3 >= expectedValueLength &&
                    StringUtils.SubstringEquals(expectedValue, str.Item1, str.Item2, stringComparison))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public IEnumerable<string> EnumerateValueList(string headerName)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            foreach (Tuple<string, int, int> str in EnumerateValueListInternal(headerName))
            {
                yield return str.Item1.Substring(str.Item2, str.Item3);
            }
        }

        /// <summary>
        /// Internal enumerator of header list values as a list of tuples defining a string region.
        /// </summary>
        /// <param name="headerName">The header name to look for</param>
        /// <returns>A set of tuples: item 1 is the full header, item 2 is start index of the substring, item 3 is length of substring</returns>
        private IEnumerable<Tuple<string, int, int>> EnumerateValueListInternal(string headerName)
        {
            List<string> list;
            if (!_headers.TryGetValue(headerName, out list) || list.Count == 0)
            {
                yield break;
            }

            foreach (string headerInstance in list)
            {
                int thisHeaderLength = headerInstance.Length;
                int start = 0;
                int end = headerInstance.IndexOf(',', start);
                if (end < 0)
                {
                    end = thisHeaderLength;
                }

                while (start <= thisHeaderLength)
                {
                    yield return new Tuple<string, int, int>(headerInstance, start, end - start);

                    // Try and iterate to the next entry in the list
                    start = end + 1;

                    // Skip over any whitespace after the comma separating each value
                    while (start < thisHeaderLength && headerInstance[start] == ' ')
                    {
                        start++;
                    }

                    if (start >= thisHeaderLength)
                    {
                        break;
                    }

                    end = headerInstance.IndexOf(',', start);
                    if (end < 0)
                    {
                        end = thisHeaderLength;
                    }
                }
            }
        }

        /// <inheritdoc />
        public bool TryGetValue(string headerName, out string headerValue)
        {
            headerValue = GetFirstHeaderValue(headerName);
            return headerValue != null;
        }

        /// <summary>
        /// Gets the first header value for the given key, or null if not found.
        /// All header comparisons are case insensitive.
        /// </summary>
        /// <param name="headerName">The header to check for</param>
        /// <returns>The fetched header, or null if not found</returns>
        private string GetFirstHeaderValue(string headerName)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            List<string> list;
            if (_headers.TryGetValue(headerName, out list) &&
                list.Count > 0)
            {
                return list[0];
            }

            return null;
        }

        //public IReadOnlyList<string> GetAllHeaderValues(string headerName)
        //{
        //    headerName.AssertNonNullOrEmpty(nameof(headerName));
        //    List<string> list;
        //    if (_headers.TryGetValue(headerName, out list))
        //    {
        //        return list;
        //    }

        //    return EMPTY_HEADER_SET;
        //}

        /// <inheritdoc />
        public void Remove(string headerName)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            _headers.Remove(headerName);
        }

        /// <inheritdoc />
        public void Set(string headerName, string headerValue)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            // Assuming I am reading RFC 9110 § 5.5 correctly, field content is allowed to be an empty string. But disallow null
            headerValue.AssertNonNull(nameof(headerValue));

            if (headerValue.IndexOf('\r') >= 0 || headerValue.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("Header value cannot contain newline characters");
            }

            // Create a list if needed and set the only value in the list to the desired header value
            List<string> list;
            if (_headers.TryGetValue(headerName, out list))
            {
                list.Clear();
            }
            else
            {
                list = new List<string>(1);
                _headers[headerName] = list;
            }

            list.Add(headerValue);
        }

        /// <inheritdoc />
        public void Add(string headerName, string headerValue)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            headerValue.AssertNonNull(nameof(headerValue));

            if (headerValue.IndexOf('\r') >= 0 || headerValue.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("Header value cannot contain newline characters");
            }

            List<string> list;
            if (!_headers.TryGetValue(headerName, out list))
            {
                list = new List<string>(1);
                _headers[headerName] = list;
            }

            list.Add(headerValue);
        }

        public IEnumerator<KeyValuePair<string, IReadOnlyCollection<string>>> GetEnumerator()
        {
            return new HeaderEnumerator(_headers);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new HeaderEnumerator(_headers);
        }

        private class HeaderEnumerator : IEnumerator<KeyValuePair<string, IReadOnlyCollection<string>>>
        {
            private readonly IEnumerator<KeyValuePair<string, List<string>>> _internalEnumerator;
            private int _disposed = 0;

            public HeaderEnumerator(SmallDictionary<string, List<string>> headers)
            {
                _internalEnumerator = headers.GetEnumerator();
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~HeaderEnumerator()
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
